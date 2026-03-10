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

        // HUD
        private BattleHUD hud;

        // 연속 전투 상태
        private GameCsvTables tables;
        private BattleSetupRow setup;
        private string locationId;
        private System.Random rng;
        private WaveManager waveManager;
        private Dictionary<string, IReadOnlyList<SkillDefinition>> skillsByTemplateId;

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
            rng = new System.Random();
            skillsByTemplateId = new Dictionary<string, IReadOnlyList<SkillDefinition>>();

            if (!TryInitialize(out var allyDefs, out var error))
            {
                Debug.LogError($"[battle] {error}");
                enabled = false;
                return;
            }

            SpawnTeam(allyDefs, BattleTeam.Ally);
            ApplyPassivesAndExtendLookup(roster.Allies);

            waveManager = new WaveManager(locationId, tables, rng);

            hud = new GameObject("BattleHUD").AddComponent<BattleHUD>();
            hud.Setup(
                eventBus,
                roster,
                id => runtimeViews.TryGetValue(id, out var go) && go != null ? go.transform.position : Vector3.zero,
                () => SceneNavigator.TryLoad("Dungeon"),
                TogglePause);

            SpawnNextWave();
        }

        private void Update()
        {
            hud?.SetProgress(elapsed, turnIntervalSec);

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
                if (!roster.HasAlive(BattleTeam.Ally))
                {
                    paused = true;
                    Debug.Log("[battle] Game over - all allies defeated");
                    hud?.ShowGameOver(() => SceneNavigator.TryLoad("Dungeon"));
                }
                else
                {
                    // 적 전멸 → 다음 wave
                    waveManager.OnWaveCleared();
                    hud?.OnWaveCleared(waveManager.StagesClearedCount);
                    var killed = roster.ExtractKilledEnemies();
                    CleanupEnemyViews(killed);
                    ResolveAndLogDrops(killed);
                    DistributeExp(killed);
                    SpawnNextWave();
                }
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            Debug.Log($"[battle] paused={paused}");
        }

        /// <summary>
        /// 테이블 로드, setup 읽기, ally 정의 생성.
        /// locationId는 파견 데이터 또는 setup.EnemyLocationId에서 결정됩니다.
        /// </summary>
        private bool TryInitialize(out BattleUnitDefinition[] allyDefs, out string error)
        {
            allyDefs = Array.Empty<BattleUnitDefinition>();
            error = string.Empty;

            if (!GameCsvTables.TryLoad(out tables, out var loadError))
            {
                error = loadError;
                return false;
            }

            if (!tables.TryGetBattleSetup(battleSetupId, out setup, out error))
            {
                return false;
            }

            turnIntervalSec = setup.TurnIntervalSec;

            List<string> allyIds;
            if (PlayerAccountService.TryGetDispatch(out var dispatchedLocationId, out _, out var dispatchedAllies))
            {
                allyIds = dispatchedAllies;
                locationId = dispatchedLocationId;
                PlayerAccountService.ClearDispatch();
            }
            else
            {
                var maxPartySize = Mathf.Max(1, tables.GetDefineInt("maxPartySize", 4));
                allyIds = PlayerAccountService.OwnedTemplateIds.Take(maxPartySize).ToList();
                locationId = setup.EnemyLocationId;
            }

            if (allyIds.Count == 0)
            {
                error = "No player mercenaries selected. Dispatch from Dungeon scene or recruit mercenaries first.";
                return false;
            }

            if (!TryBuildDefinitionsFromIds(
                    tables, allyIds, BattleTeam.Ally,
                    setup.AllyStartX, setup.AllyStartY, setup.AllySpacingX,
                    out allyDefs, out error))
            {
                return false;
            }

            // 레벨에 따른 스탯 배율 적용
            for (var i = 0; i < allyDefs.Length; i++)
            {
                var level = PlayerAccountService.GetLevel(allyDefs[i].templateId);
                var mult = tables.GetLevelStatMultiplier(level);
                allyDefs[i] = ApplyLevelMultiplier(allyDefs[i], mult);
            }

            return true;
        }

        /// <summary>
        /// 다음 전투 wave를 스폰합니다.
        /// EXPLORE/HIDDEN 스테이지는 자동으로 통과(미구현)하고 전투 가능한 스테이지까지 넘어갑니다.
        /// </summary>
        private void SpawnNextWave()
        {
            // EXPLORE/HIDDEN은 자동 통과 (무한 루프 방지를 위해 상한 설정)
            const int autoPassLimit = 20;
            var autoPassCount = 0;
            WaveStageType stageType;

            while (true)
            {
                stageType = waveManager.DetermineNextStageType();
                if (stageType == WaveStageType.Explore || stageType == WaveStageType.Hidden)
                {
                    Debug.Log($"[wave] {stageType} stage - auto pass (미구현)");
                    waveManager.OnWaveCleared();
                    autoPassCount++;
                    if (autoPassCount >= autoPassLimit)
                    {
                        Debug.LogWarning("[wave] Auto-pass limit reached. Defaulting to Battle stage.");
                        stageType = WaveStageType.Battle;
                        break;
                    }

                    continue;
                }

                break;
            }

            var enemyIds = waveManager.SelectEncounterEnemyIds(stageType);
            if (enemyIds.Count == 0)
            {
                Debug.LogError($"[wave] No encounter found for {stageType} at locationId={locationId}");
                paused = true;
                return;
            }

            if (!TryBuildDefinitionsFromIds(
                    tables, enemyIds, BattleTeam.Enemy,
                    setup.EnemyStartX, setup.EnemyStartY, setup.EnemySpacingX,
                    out var defs, out var err))
            {
                Debug.LogError($"[wave] {err}");
                paused = true;
                return;
            }

            Debug.Log($"[wave] Stage {waveManager.StagesClearedCount + 1}: {stageType} — {enemyIds.Count} enemies");
            SpawnTeam(defs, BattleTeam.Enemy);
            ApplyPassivesAndExtendLookup(roster.Enemies);

            simulation = new BattleSimulation(eventBus, roster, new BattleTurnService(), skillsByTemplateId, rng);
            simulation.Start();
            elapsed = 0f;
        }

        /// <summary>사망한 적 유닛의 뷰(GameObject)를 제거합니다.</summary>
        private void CleanupEnemyViews(List<(string runtimeId, string templateId)> killed)
        {
            foreach (var (runtimeId, _) in killed)
            {
                if (runtimeViews.TryGetValue(runtimeId, out var go) && go != null)
                {
                    Destroy(go);
                }

                runtimeViews.Remove(runtimeId);
            }
        }

        /// <summary>드랍 아이템을 계산하고 로그에 출력합니다. (인벤토리 미구현)</summary>
        private void ResolveAndLogDrops(List<(string runtimeId, string templateId)> killed)
        {
            var templateIds = killed.Select(k => k.templateId);
            var dropped = DropResolver.Resolve(templateIds, tables, rng);
            if (dropped.Count > 0)
            {
                Debug.Log($"[wave] Drops: {string.Join(", ", dropped)}");
            }
        }

        /// <summary>
        /// 사망한 적의 ExpGain을 합산하여 생존 아군에게 균등 분배합니다.
        /// 레벨업 발생 시 HUD 로그에 출력합니다.
        /// </summary>
        private void DistributeExp(List<(string runtimeId, string templateId)> killed)
        {
            var totalExp = 0f;
            foreach (var (_, templateId) in killed)
            {
                if (tables.TryGetCombatUnit(templateId, out var row))
                {
                    totalExp += row.ExpGain;
                }
            }

            if (totalExp <= 0f)
            {
                return;
            }

            var aliveAllies = roster.Allies.Where(u => u.IsAlive).ToList();
            if (aliveAllies.Count == 0)
            {
                return;
            }

            var expPerAlly = Mathf.Max(1, Mathf.RoundToInt(totalExp / aliveAllies.Count));

            foreach (var ally in aliveAllies)
            {
                var levelUps = PlayerAccountService.AddExp(ally.TemplateId, expPerAlly, tables);
                Debug.Log($"[exp] {ally.TemplateId} +{expPerAlly} EXP (Lv.{PlayerAccountService.GetLevel(ally.TemplateId)} / {PlayerAccountService.GetExp(ally.TemplateId)} exp)");

                foreach (var (oldLv, newLv) in levelUps)
                {
                    Debug.Log($"[levelup] {ally.TemplateId} Lv.{oldLv} → Lv.{newLv}");
                    hud?.LogMessage($"<color=#FFD700>★ {ally.TemplateId} Lv.{newLv} 레벨업!</color>");
                }
            }
        }

        /// <summary>레벨 multiplier를 BattleUnitDefinition의 정수형 스탯에 적용합니다.</summary>
        private static BattleUnitDefinition ApplyLevelMultiplier(BattleUnitDefinition def, float mult)
        {
            if (Mathf.Approximately(mult, 1f))
            {
                return def;
            }

            var s = def.statBlock;
            def.statBlock = new BattleStatBlock
            {
                MaxHp          = Mathf.Max(1, Mathf.RoundToInt(s.MaxHp * mult)),
                MaxMana        = Mathf.Max(0, Mathf.RoundToInt(s.MaxMana * mult)),
                Stamina        = Mathf.RoundToInt(s.Stamina * mult),
                Agility        = s.Agility,
                Intelligence   = Mathf.RoundToInt(s.Intelligence * mult),
                Strength       = Mathf.RoundToInt(s.Strength * mult),
                Attack         = Mathf.Max(1, Mathf.RoundToInt(s.Attack * mult)),
                Defense        = Mathf.RoundToInt(s.Defense * mult),
                HpRegen        = Mathf.RoundToInt(s.HpRegen * mult),
                ThornPhysical  = Mathf.RoundToInt(s.ThornPhysical * mult),
                ThornMagical   = Mathf.RoundToInt(s.ThornMagical * mult),
                Evasion        = s.Evasion,
                CritChance     = s.CritChance,
                CritDamage     = s.CritDamage,
                LifeSteal      = s.LifeSteal,
                Counter        = s.Counter,
                ExpGain        = s.ExpGain,
                HealPower      = s.HealPower,
                AttackRangeType = s.AttackRangeType,
                DamageType     = s.DamageType,
            };
            return def;
        }

        /// <summary>
        /// 유닛 목록에 대해 스킬 룩업을 확장하고 패시브 스탯을 계산합니다.
        /// 이미 등록된 templateId라도 새 유닛 인스턴스에 패시브를 재적용합니다.
        /// </summary>
        private void ApplyPassivesAndExtendLookup(IReadOnlyList<BattleUnit> units)
        {
            foreach (var unit in units)
            {
                if (!skillsByTemplateId.ContainsKey(unit.TemplateId))
                {
                    skillsByTemplateId[unit.TemplateId] = tables.GetSkills(unit.TemplateId);
                }

                var skills = skillsByTemplateId[unit.TemplateId];
                var computed = PassiveApplicator.Apply(unit.BaseStat, skills);
                unit.SetComputedStat(computed);
            }
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
