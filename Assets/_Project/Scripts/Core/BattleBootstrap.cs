using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Battle;
using ProjectH.Data;
using ProjectH.Data.Tables;
using ProjectH.UI.Battle;
using UnityEngine;

namespace ProjectH.Core
{
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Header("Turn")]
        [SerializeField] private string battleSetupId = string.Empty;
        private float turnIntervalSec;

        private readonly Dictionary<string, GameObject> runtimeViews = new();
        private BattleEventBus eventBus;
        private BattleRoster roster;
        private BattleSimulation simulation;
        private float elapsed;
        private bool paused;
        private bool isActionAnimating;

        private static BattleBootstrap instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[battle] Duplicate BattleBootstrap detected. Destroying duplicate instance.");
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.OnPublished -= OnBattleEvent;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            UnitRuntimeIdGenerator.Reset();

            eventBus = new BattleEventBus();
            eventBus.OnPublished += OnBattleEvent;

            roster = new BattleRoster();

            if (!TryBuildSetupFromTables(out var allies, out var enemies, out var tables, out var loadError))
            {
                Debug.LogError($"[battle] {loadError}");
                enabled = false;
                return;
            }

            SpawnTeam(allies, BattleTeam.Ally);
            SpawnTeam(enemies, BattleTeam.Enemy);

            var skillsByTemplateId = BuildSkillLookup(tables, roster);
            simulation = new BattleSimulation(eventBus, roster, new BattleTurnService(), skillsByTemplateId, new System.Random());
            simulation.Start();
        }

        private void Update()
        {
            if (paused || isActionAnimating)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < turnIntervalSec)
            {
                return;
            }

            elapsed = 0f;
            var progressed = simulation.TickOneTurn();
            if (!progressed)
            {
                paused = true;
                Debug.Log("[battle] finished");
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            Debug.Log($"[battle] paused={paused}");
        }

        private bool TryBuildSetupFromTables(out BattleUnitDefinition[] allies, out BattleUnitDefinition[] enemies, out GameCsvTables tables, out string error)
        {
            allies = Array.Empty<BattleUnitDefinition>();
            enemies = Array.Empty<BattleUnitDefinition>();
            tables = null;
            error = string.Empty;

            if (!GameCsvTables.TryLoad(out tables, out var loadError))
            {
                error = loadError;
                return false;
            }

            if (!tables.TryGetBattleSetup(battleSetupId, out var setup, out error))
            {
                return false;
            }

            turnIntervalSec = setup.TurnIntervalSec;

            List<string> allyIds;
            string enemyLocationId;
            int enemyWaveIndex;

            if (PlayerAccountService.TryGetDispatch(out var dispatchedLocationId, out var dispatchedWaveIndex, out var dispatchedAllies))
            {
                allyIds = dispatchedAllies;
                enemyLocationId = dispatchedLocationId;
                enemyWaveIndex = dispatchedWaveIndex;
                PlayerAccountService.ClearDispatch();
            }
            else
            {
                var maxPartySize = Mathf.Max(1, tables.GetDefineInt("maxPartySize", 4));
                allyIds = PlayerAccountService.OwnedTemplateIds.Take(maxPartySize).ToList();
                enemyLocationId = setup.EnemyLocationId;
                enemyWaveIndex = setup.EnemyWaveIndex;
            }

            if (allyIds.Count == 0)
            {
                error = "No player mercenaries selected. Dispatch from Dungeon scene or recruit mercenaries first.";
                return false;
            }

            if (!TryBuildDefinitionsFromIds(
                    tables,
                    allyIds,
                    BattleTeam.Ally,
                    setup.AllyStartX,
                    setup.AllyStartY,
                    setup.AllySpacingX,
                    out allies,
                    out error))
            {
                return false;
            }

            var enemyIds = tables.BuildEnemyTemplateIds(enemyLocationId, enemyWaveIndex);
            if (enemyIds.Count == 0)
            {
                error = $"location_waves.csv has no rows for locationId={enemyLocationId}, waveIndex={enemyWaveIndex}";
                return false;
            }

            if (!TryBuildDefinitionsFromIds(
                    tables,
                    enemyIds,
                    BattleTeam.Enemy,
                    setup.EnemyStartX,
                    setup.EnemyStartY,
                    setup.EnemySpacingX,
                    out enemies,
                    out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 모든 살아있는 유닛의 templateId → skills 딕셔너리를 구성합니다.
        /// PassiveApplicator도 여기서 적용합니다.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<SkillDefinition>> BuildSkillLookup(
            GameCsvTables tables, BattleRoster roster)
        {
            var result = new Dictionary<string, IReadOnlyList<SkillDefinition>>();
            var allUnits = new List<BattleUnit>();
            allUnits.AddRange(roster.Allies);
            allUnits.AddRange(roster.Enemies);

            foreach (var unit in allUnits)
            {
                if (result.ContainsKey(unit.TemplateId))
                {
                    continue;
                }

                var skills = tables.GetSkills(unit.TemplateId);
                result[unit.TemplateId] = skills;

                // 패시브 반영 스탯 계산
                var computed = PassiveApplicator.Apply(unit.BaseStat, skills);
                unit.SetComputedStat(computed);
            }

            return result;
        }

        private static List<string> ParseIdList(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return list;
            }

            var parts = csv.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var id = p.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    list.Add(id);
                }
            }

            return list;
        }

        private static bool TryBuildDefinitionsFromIds(
            GameCsvTables tables,
            List<string> entityIds,
            BattleTeam team,
            float startX,
            float startY,
            float spacingX,
            out BattleUnitDefinition[] defs,
            out string error)
        {
            var list = new List<BattleUnitDefinition>(entityIds.Count);
            error = string.Empty;

            for (var i = 0; i < entityIds.Count; i++)
            {
                var id = entityIds[i];
                if (!tables.TryGetCombatUnit(id, out var unitRow))
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"combat_units.csv missing entityId={id}";
                    return false;
                }

                list.Add(new BattleUnitDefinition
                {
                    templateId = unitRow.EntityId,
                    displayName = string.IsNullOrWhiteSpace(unitRow.Name) ? unitRow.EntityId : unitRow.Name,
                    statBlock = new BattleStatBlock
                    {
                        MaxHp          = unitRow.MaxHp,
                        MaxMana        = unitRow.MaxMana,
                        Stamina        = unitRow.Stamina,
                        Agility        = unitRow.Agility,
                        Intelligence   = unitRow.Intelligence,
                        Strength       = unitRow.Strength,
                        Attack         = unitRow.Attack,
                        Defense        = unitRow.Defense,
                        HpRegen        = unitRow.HpRegen,
                        ThornPhysical  = unitRow.ThornPhysical,
                        ThornMagical   = unitRow.ThornMagical,
                        Evasion        = unitRow.Evasion,
                        CritChance     = unitRow.CritChance,
                        CritDamage     = unitRow.CritDamage,
                        LifeSteal      = unitRow.LifeSteal,
                        Counter        = unitRow.Counter,
                        ExpGain        = unitRow.ExpGain,
                        HealPower      = unitRow.HealPower,
                        AttackRangeType = unitRow.AttackRangeType,
                        DamageType     = unitRow.DamageType,
                    },
                    prefabResourcePath = unitRow.PrefabResourcePath,
                    spawnX = startX + spacingX * i,
                    spawnY = startY,
                });
            }

            defs = list.ToArray();
            return true;
        }

        private void SpawnTeam(BattleUnitDefinition[] defs, BattleTeam team)
        {
            if (defs == null)
            {
                return;
            }

            foreach (var def in defs)
            {
                var unit = new BattleUnit(def.templateId, team, def.statBlock);
                roster.Add(unit);

                var view = CreateUnitView(def, team, unit.RuntimeUnitId);
                runtimeViews[unit.RuntimeUnitId] = view;

                eventBus.Publish(new BattleEvent(BattleEventType.Spawned, unit.RuntimeUnitId, unit.RuntimeUnitId, 0));
            }
        }

        private static GameObject CreateUnitView(BattleUnitDefinition def, BattleTeam team, string runtimeUnitId)
        {
            GameObject go = null;
            if (!string.IsNullOrWhiteSpace(def.prefabResourcePath))
            {
                var prefab = Resources.Load<GameObject>(def.prefabResourcePath);
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                    go.name = runtimeUnitId;
                }
            }

            if (go == null)
            {
                go = new GameObject(runtimeUnitId);
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
            }

            if (sr.sprite == null)
            {
                sr.sprite = BuildFallbackSprite();
                sr.color = team == BattleTeam.Ally ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(1f, 0.4f, 0.4f, 1f);
            }

            sr.sortingOrder = 10;

            if (go.GetComponent<Animator>() == null)
            {
                go.AddComponent<Animator>();
            }

            var view = go.GetComponent<UnitView>();
            if (view == null)
            {
                view = go.AddComponent<UnitView>();
            }

            view.Bind(runtimeUnitId, def.templateId, team == BattleTeam.Enemy);
            go.transform.position = new Vector3(def.spawnX, def.spawnY, 0f);
            return go;
        }

        private static Sprite BuildFallbackSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Spawned:
                    Debug.Log($"[battle] spawned {e.SourceRuntimeId}");
                    break;
                case BattleEventType.TurnStarted:
                    Debug.Log($"[battle] turn {e.SourceRuntimeId} -> {e.TargetRuntimeId}");
                    if (runtimeViews.TryGetValue(e.SourceRuntimeId, out var actorGo) && actorGo != null)
                    {
                        var actorView = actorGo.GetComponent<UnitView>();
                        UnitView targetView = null;
                        if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var targetGo) && targetGo != null)
                        {
                            targetView = targetGo.GetComponent<UnitView>();
                        }

                        if (actorView != null && targetView != null)
                        {
                            StartCoroutine(PlayAttackMotion(actorView, targetView));
                        }
                        else
                        {
                            actorView?.OnTurnStarted();
                        }
                    }
                    break;
                case BattleEventType.Damaged:
                    Debug.Log($"[battle] damage {e.TargetRuntimeId} -{e.Value}");
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var damagedGo) && damagedGo != null)
                    {
                        var damagedView = damagedGo.GetComponent<UnitView>();
                        damagedView?.OnDamaged(e.Value);
                    }
                    break;
                case BattleEventType.Died:
                    Debug.Log($"[battle] died {e.TargetRuntimeId}");
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var deadGo) && deadGo != null)
                    {
                        var deadView = deadGo.GetComponent<UnitView>();
                        if (deadView != null)
                        {
                            deadView.OnDied();
                        }
                        else
                        {
                            deadGo.SetActive(false);
                        }
                    }
                    break;
                case BattleEventType.ActiveSkillUsed:
                    Debug.Log($"[battle] skill used by {e.SourceRuntimeId}");
                    break;
                case BattleEventType.Healed:
                    Debug.Log($"[battle] healed {e.TargetRuntimeId} +{e.Value}");
                    break;
                case BattleEventType.Evaded:
                    Debug.Log($"[battle] evaded {e.TargetRuntimeId}");
                    break;
                case BattleEventType.ThornsReflected:
                    Debug.Log($"[battle] thorns {e.TargetRuntimeId} -{e.Value}");
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var thornGo) && thornGo != null)
                    {
                        thornGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    }
                    break;
                case BattleEventType.CounterAttacked:
                    Debug.Log($"[battle] counter {e.SourceRuntimeId} -> {e.TargetRuntimeId} -{e.Value}");
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var counterGo) && counterGo != null)
                    {
                        counterGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    }
                    break;
            }
        }

        private IEnumerator PlayAttackMotion(UnitView actorView, UnitView targetView)
        {
            if (actorView == null || targetView == null)
            {
                yield break;
            }

            isActionAnimating = true;

            var t = actorView.transform;
            var start = t.position;
            var approach = targetView.GetApproachPointForAttacker(actorView, actorView.ApproachGap, actorView.ApproachYOffset);

            Debug.Log($"[battle] move {actorView.name} -> {targetView.name} gap={actorView.ApproachGap:F2} in={actorView.ApproachSpeed:F2} out={actorView.RetreatSpeed:F2} hold={actorView.AttackHoldSec:F2}");
            yield return MoveTo(t, approach, actorView.ApproachSpeed);

            actorView.OnTurnStarted();

            var hold = actorView.AttackHoldSec;
            if (hold > 0f)
            {
                yield return new WaitForSeconds(hold);
            }

            if (actorView != null && actorView.gameObject.activeInHierarchy)
            {
                yield return MoveTo(t, start, actorView.RetreatSpeed);
            }

            isActionAnimating = false;
        }

        private static IEnumerator MoveTo(Transform t, Vector3 target, float speed)
        {
            if (t == null)
            {
                yield break;
            }

            var s = Mathf.Max(0.01f, speed);
            while ((t.position - target).sqrMagnitude > 0.0004f)
            {
                t.position = Vector3.MoveTowards(t.position, target, s * Time.deltaTime);
                yield return null;
            }

            t.position = target;
        }
    }
}
