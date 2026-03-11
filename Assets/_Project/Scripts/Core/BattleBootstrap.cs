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
        private BattleHUD hud;
        private GameCsvTables tables;
        private BattleSetupRow setup;
        private string locationId;
        private System.Random rng;
        private WaveManager waveManager;
        private Dictionary<string, IReadOnlyList<SkillDefinition>> skillsByTemplateId;
        private TalentCatalog talentCatalog;

        // Session result tracking
        private int maxWaves;
        private int creditsPerWave;
        private readonly List<DropRow> sessionDrops = new();
        private readonly List<string> sessionEventLogs = new();

        private static BattleBootstrap instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
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

            maxWaves = Mathf.Max(1, tables.GetDefineInt("dispatchWaveCount", 5));
            creditsPerWave = Mathf.Max(0, tables.GetDefineInt("creditsPerWave", 80));

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
            if (progressed)
            {
                return;
            }

            if (!roster.HasAlive(BattleTeam.Ally))
            {
                paused = true;
                hud?.ShowGameOver(() => SceneNavigator.TryLoad("Dungeon"));
                return;
            }

            waveManager.OnWaveCleared();
            var cleared = waveManager.StagesClearedCount;
            hud?.OnWaveCleared(cleared);
            var killed = roster.ExtractKilledEnemies();
            CleanupEnemyViews(killed);
            ResolveAndLogDrops(killed);
            DistributeExp(killed);

            // Award credits for this wave
            if (creditsPerWave > 0)
            {
                PlayerAccountService.AddCredits(creditsPerWave);
                sessionEventLogs.Add($"+{creditsPerWave} credits");
            }

            // Check dispatch wave limit
            if (cleared >= maxWaves)
            {
                paused = true;
                hud?.ShowVictory(
                    waveManager.StagesClearedCount,
                    maxWaves,
                    sessionDrops,
                    sessionEventLogs,
                    () => SceneNavigator.TryLoad("Office"));
                return;
            }

            SpawnNextWave();
        }

        public void TogglePause()
        {
            paused = !paused;
        }

        private bool TryInitialize(out BattleUnitDefinition[] allyDefs, out string error)
        {
            allyDefs = Array.Empty<BattleUnitDefinition>();
            error = string.Empty;

            if (!GameCsvTables.TryLoad(out tables, out var loadError))
            {
                error = loadError;
                return false;
            }

            if (!TalentCatalog.TryLoad(out talentCatalog, out var talentError))
            {
                error = talentError;
                return false;
            }

            if (!tables.TryGetBattleSetup(battleSetupId, out setup, out error))
            {
                return false;
            }

            turnIntervalSec = setup.TurnIntervalSec;
            List<string> allyMercenaryIds;
            if (PlayerAccountService.TryGetDispatch(out var dispatchedLocationId, out _, out var dispatchedAllies))
            {
                allyMercenaryIds = dispatchedAllies;
                locationId = dispatchedLocationId;
                PlayerAccountService.ClearDispatch();
            }
            else
            {
                var maxPartySize = Mathf.Max(1, tables.GetDefineInt("maxPartySize", 4));
                allyMercenaryIds = PlayerAccountService.GetOwnedMercenaries().Take(maxPartySize).Select(x => x.mercenaryId).ToList();
                locationId = setup.EnemyLocationId;
            }

            if (allyMercenaryIds.Count == 0)
            {
                error = "No player mercenaries selected.";
                return false;
            }

            return TryBuildAllyDefinitions(allyMercenaryIds, out allyDefs, out error);
        }

        private void SpawnNextWave()
        {
            const int autoPassLimit = 20;
            var autoPassCount = 0;
            WaveStageType stageType;
            while (true)
            {
                stageType = waveManager.DetermineNextStageType();
                if (stageType != WaveStageType.Explore && stageType != WaveStageType.Hidden)
                {
                    break;
                }

                waveManager.OnWaveCleared();
                autoPassCount++;
                if (autoPassCount >= autoPassLimit)
                {
                    stageType = WaveStageType.Battle;
                    break;
                }
            }

            var enemyIds = waveManager.SelectEncounterEnemyIds(stageType);
            if (enemyIds.Count == 0)
            {
                paused = true;
                return;
            }

            if (!TryBuildEnemyDefinitions(enemyIds, out var defs, out var error))
            {
                Debug.LogError($"[wave] {error}");
                paused = true;
                return;
            }

            SpawnTeam(defs, BattleTeam.Enemy);
            ApplyPassivesAndExtendLookup(roster.Enemies);
            simulation = new BattleSimulation(eventBus, roster, new BattleTurnService(), skillsByTemplateId, rng);
            simulation.Start();
            elapsed = 0f;
        }

        private bool TryBuildAllyDefinitions(List<string> mercenaryIds, out BattleUnitDefinition[] defs, out string error)
        {
            var list = new List<BattleUnitDefinition>(mercenaryIds.Count);
            error = string.Empty;
            var positions = ResolvePositions(locationId, "ally", mercenaryIds.Count);

            for (var i = 0; i < mercenaryIds.Count; i++)
            {
                var record = PlayerAccountService.GetRecordByMercenaryId(mercenaryIds[i]);
                if (record == null)
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"Missing mercenary record: {mercenaryIds[i]}";
                    return false;
                }

                if (!tables.TryGetCombatUnit(record.templateId, out var row))
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"combat_units.csv missing entityId={record.templateId}";
                    return false;
                }

                var stat = BuildStatBlock(row);
                stat = ApplyLevelMultiplier(stat, tables.GetLevelStatMultiplier(record.level));
                stat = talentCatalog.ApplyTalent(stat, record.talentTag);
                stat = ApplyEquipmentBonuses(stat, InventoryService.GetEquipmentsByOwner(record.mercenaryId));
                tables.TryGetCharacterRow(record.templateId, out var characterRow);
                var (wx, wy) = positions[i];
                list.Add(new BattleUnitDefinition
                {
                    mercenaryId = record.mercenaryId,
                    templateId = row.EntityId,
                    talentTag = record.talentTag,
                    displayName = string.IsNullOrWhiteSpace(characterRow.Name) ? row.Name : characterRow.Name,
                    statBlock = stat,
                    prefabResourcePath = row.PrefabResourcePath,
                    spawnX = wx,
                    spawnY = wy,
                });
            }

            defs = list.ToArray();
            return true;
        }

        private bool TryBuildEnemyDefinitions(List<string> enemyIds, out BattleUnitDefinition[] defs, out string error)
        {
            var list = new List<BattleUnitDefinition>(enemyIds.Count);
            error = string.Empty;
            var positions = ResolvePositions(locationId, "enemy", enemyIds.Count);

            for (var i = 0; i < enemyIds.Count; i++)
            {
                if (!tables.TryGetCombatUnit(enemyIds[i], out var row))
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"combat_units.csv missing entityId={enemyIds[i]}";
                    return false;
                }

                var (wx, wy) = positions[i];
                list.Add(new BattleUnitDefinition
                {
                    mercenaryId = string.Empty,
                    templateId = row.EntityId,
                    talentTag = "NONE",
                    displayName = string.IsNullOrWhiteSpace(row.Name) ? row.EntityId : row.Name,
                    statBlock = BuildStatBlock(row),
                    prefabResourcePath = row.PrefabResourcePath,
                    spawnX = wx,
                    spawnY = wy,
                });
            }

            defs = list.ToArray();
            return true;
        }

        /// <summary>
        /// battle_positions.csv 슬롯 기반 월드 좌표 목록을 반환합니다.
        /// 슬롯 수 > 유닛 수 → 셔플 후 앞 N개 사용
        /// 슬롯 수 ≤ 유닛 수 → 슬롯 순서 wrap 반복
        /// 슬롯 없음 → battle_setup.csv 선형 간격 폴백
        /// </summary>
        private List<(float x, float y)> ResolvePositions(string locId, string side, int count)
        {
            var slots = tables.GetBattleSlots(locId, side);
            var result = new List<(float, float)>(count);

            if (slots.Count == 0)
            {
                // 폴백: battle_setup.csv 선형 배치
                var startX = side == "ally" ? setup.AllyStartX : setup.EnemyStartX;
                var startY = side == "ally" ? setup.AllyStartY : setup.EnemyStartY;
                var spacX  = side == "ally" ? setup.AllySpacingX : setup.EnemySpacingX;
                for (var i = 0; i < count; i++)
                    result.Add((startX + spacX * i, startY));
                return result;
            }

            if (count <= slots.Count)
            {
                // 슬롯을 셔플 후 앞 N개 선택
                var shuffled = new List<BattlePositionRow>(slots);
                for (var i = shuffled.Count - 1; i > 0; i--)
                {
                    var j = rng.Next(0, i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }

                for (var i = 0; i < count; i++)
                {
                    var w = ViewportToWorld(shuffled[i].NormX, shuffled[i].NormY);
                    result.Add((w.x, w.y));
                }
            }
            else
            {
                // 유닛이 슬롯보다 많으면 wrap
                for (var i = 0; i < count; i++)
                {
                    var slot = slots[i % slots.Count];
                    var w = ViewportToWorld(slot.NormX, slot.NormY);
                    result.Add((w.x, w.y));
                }
            }

            return result;
        }

        private static Vector2 ViewportToWorld(float normX, float normY)
        {
            if (Camera.main == null)
                return new Vector2(normX * 10f - 5f, normY * 10f - 5f); // 카메라 없을 때 임시값

            var depth = Mathf.Abs(Camera.main.transform.position.z);
            var worldPos = Camera.main.ViewportToWorldPoint(new Vector3(normX, normY, depth));
            return new Vector2(worldPos.x, worldPos.y);
        }

        private static BattleStatBlock BuildStatBlock(CombatUnitRow row)
        {
            return new BattleStatBlock
            {
                MaxHp = row.MaxHp,
                MaxMana = row.MaxMana,
                Stamina = row.Stamina,
                Agility = row.Agility,
                Intelligence = row.Intelligence,
                Strength = row.Strength,
                Attack = row.Attack,
                Defense = row.Defense,
                HpRegen = row.HpRegen,
                ThornPhysical = row.ThornPhysical,
                ThornMagical = row.ThornMagical,
                Evasion = row.Evasion,
                CritChance = row.CritChance,
                CritDamage = row.CritDamage,
                LifeSteal = row.LifeSteal,
                Counter = row.Counter,
                ExpGain = row.ExpGain,
                HealPower = row.HealPower,
                AttackRangeType = row.AttackRangeType,
                DamageType = row.DamageType,
            };
        }

        private static BattleStatBlock ApplyLevelMultiplier(BattleStatBlock stat, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f))
            {
                return stat;
            }

            stat.MaxHp = Mathf.Max(1, Mathf.RoundToInt(stat.MaxHp * multiplier));
            stat.MaxMana = Mathf.Max(0, Mathf.RoundToInt(stat.MaxMana * multiplier));
            stat.Stamina = Mathf.RoundToInt(stat.Stamina * multiplier);
            stat.Intelligence = Mathf.RoundToInt(stat.Intelligence * multiplier);
            stat.Strength = Mathf.RoundToInt(stat.Strength * multiplier);
            stat.Attack = Mathf.Max(1, Mathf.RoundToInt(stat.Attack * multiplier));
            stat.Defense = Mathf.RoundToInt(stat.Defense * multiplier);
            stat.HpRegen = Mathf.RoundToInt(stat.HpRegen * multiplier);
            stat.ThornPhysical = Mathf.RoundToInt(stat.ThornPhysical * multiplier);
            stat.ThornMagical = Mathf.RoundToInt(stat.ThornMagical * multiplier);
            return stat;
        }

        private void ApplyPassivesAndExtendLookup(IReadOnlyList<BattleUnit> units)
        {
            foreach (var unit in units)
            {
                if (!skillsByTemplateId.ContainsKey(unit.TemplateId))
                {
                    skillsByTemplateId[unit.TemplateId] = tables.GetSkills(unit.TemplateId);
                }

                unit.SetComputedStat(PassiveApplicator.Apply(unit.BaseStat, skillsByTemplateId[unit.TemplateId]));
            }
        }

        private void SpawnTeam(BattleUnitDefinition[] defs, BattleTeam team)
        {
            if (defs == null)
            {
                return;
            }

            foreach (var def in defs)
            {
                var unit = new BattleUnit(def.mercenaryId, def.templateId, team, def.statBlock);
                roster.Add(unit);
                var go = CreateUnitView(def, team, unit.RuntimeUnitId);
                runtimeViews[unit.RuntimeUnitId] = go;

                // 초기 HP/MP 게이지 설정
                go.GetComponent<UnitView>()?.UpdateStats(unit.Hp, unit.Stat.MaxHp, unit.Mana, unit.Stat.MaxMana);

                eventBus.Publish(new BattleEvent(BattleEventType.Spawned, unit.RuntimeUnitId, unit.RuntimeUnitId, 0));
            }
        }

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

        private void ResolveAndLogDrops(List<(string runtimeId, string templateId)> killed)
        {
            var dropped = DropResolver.Resolve(killed.Select(k => k.templateId), tables, rng);
            foreach (var drop in dropped)
            {
                InventoryService.AddDrop(drop);
                sessionDrops.Add(drop);
            }

            if (dropped.Count > 0)
            {
                Debug.Log($"[wave] Drops: {string.Join(", ", dropped.Select(x => x.ItemName))}");
            }
        }

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

            var aliveAllies = roster.Allies.Where(u => u.IsAlive && !string.IsNullOrWhiteSpace(u.MercenaryId)).ToList();
            if (aliveAllies.Count == 0)
            {
                return;
            }

            var expPerAlly = Mathf.Max(1, Mathf.RoundToInt(totalExp / aliveAllies.Count));
            foreach (var ally in aliveAllies)
            {
                var levelUps = PlayerAccountService.AddExp(ally.MercenaryId, expPerAlly, tables);
                var record = PlayerAccountService.GetRecordByMercenaryId(ally.MercenaryId);
                var name = record != null ? record.templateId : ally.TemplateId;
                Debug.Log($"[exp] {name} +{expPerAlly} EXP");
                foreach (var (_, newLevel) in levelUps)
                {
                    var msg = $"{name} Lv.{newLevel} UP";
                    hud?.LogMessage($"<color=#FFD700>{msg}</color>");
                    sessionEventLogs.Add(msg);
                }
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

        private static BattleStatBlock ApplyEquipmentBonuses(BattleStatBlock stat, IReadOnlyList<InventoryService.EquipmentRecord> equipments)
        {
            if (equipments == null)
            {
                return stat;
            }

            foreach (var equipment in equipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                switch (equipment.equipType)
                {
                    case "weapon":
                        stat.Attack += equipment.statValue;
                        break;
                    case "armor":
                        stat.Defense += equipment.statValue;
                        break;
                    case "accessory":
                        stat.MaxHp += equipment.statValue;
                        break;
                    case "extra":
                        stat.MaxMana += equipment.statValue;
                        break;
                }
            }

            return stat;
        }

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.TurnStarted:
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

                    // 마나 회복이 TurnStarted 직전에 발생하므로 액터 게이지 갱신
                    RefreshUnitView(e.SourceRuntimeId);
                    break;

                case BattleEventType.Damaged:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var damagedGo) && damagedGo != null)
                    {
                        damagedGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    }

                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.Healed:
                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.Died:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var deadGo) && deadGo != null)
                    {
                        var deadView = deadGo.GetComponent<UnitView>();
                        if (deadView != null) deadView.OnDied(); else deadGo.SetActive(false);
                    }

                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.ThornsReflected:
                case BattleEventType.CounterAttacked:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var hitGo) && hitGo != null)
                    {
                        hitGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    }

                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.ActiveSkillUsed:
                    // 스킬 사용 시 마나 소모 → 액터 게이지 갱신
                    RefreshUnitView(e.SourceRuntimeId);
                    break;
            }
        }

        private void RefreshUnitView(string runtimeId)
        {
            if (!roster.TryGetUnit(runtimeId, out var unit)) return;
            if (!runtimeViews.TryGetValue(runtimeId, out var go) || go == null) return;
            go.GetComponent<UnitView>()?.UpdateStats(unit.Hp, unit.Stat.MaxHp, unit.Mana, unit.Stat.MaxMana);
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
            yield return MoveTo(t, approach, actorView.ApproachSpeed);
            actorView.OnTurnStarted();
            if (actorView.AttackHoldSec > 0f)
            {
                yield return new WaitForSeconds(actorView.AttackHoldSec);
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

            var moveSpeed = Mathf.Max(0.01f, speed);
            while ((t.position - target).sqrMagnitude > 0.0004f)
            {
                t.position = Vector3.MoveTowards(t.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }

            t.position = target;
        }
    }
}
