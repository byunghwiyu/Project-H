using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Data;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Battle
{
    [Serializable]
    public sealed class AllySessionState
    {
        public string mercenaryId;
        public string templateId;
        public string displayName;
        public int hp;
        public int maxHp;
        public string iconResourcePath;
    }

    public sealed class BattleSession
    {
        public string SessionId { get; }
        public string LocationId { get; }
        public string LocationName { get; }
        public IReadOnlyList<string> MercenaryIds { get; }
        public long StartTimeUnix { get; }

        public int WavesClearedCount => waveManager?.StagesClearedCount ?? 0;
        public int RewardBudget { get; private set; }
        public int CollectedRewardCount { get; private set; }
        public int TotalCreditsEarned { get; private set; }
        public int TotalExpEarned { get; private set; }
        public IReadOnlyDictionary<string, int> PerMercenaryExp => perMercenaryExp;
        public IReadOnlyList<DropRow> CollectedDrops => collectedDrops;
        public IReadOnlyList<AllySessionState> AllyStates => allyStates;

        public bool IsComplete { get; private set; }
        public string CompletionReason { get; private set; }
        public float RewardProgress => RewardBudget > 0 ? (float)CollectedRewardCount / RewardBudget : 0f;

        public string StatusText
        {
            get
            {
                if (!IsComplete) return "전투 중...";
                return CompletionReason == "victory" ? "파견 완료" : "전투 패배";
            }
        }

        // 외부 접근용 (BattleBootstrap 뷰어)
        public BattleEventBus EventBus => eventBus;
        public BattleRoster Roster => roster;
        public GameCsvTables Tables => tables;
        public float TurnIntervalSec => turnIntervalSec;
        public float Elapsed => elapsed;

        public event Action<int> OnWaveCleared;
        public event Action<List<BattleUnit>> OnEnemiesSpawned;
        public event Action<string> OnSessionCompleted;

        private BattleRoster roster;
        private WaveManager waveManager;
        private BattleSimulation simulation;
        private BattleEventBus eventBus;
        private GameCsvTables tables;
        private TalentCatalog talentCatalog;
        private Dictionary<string, IReadOnlyList<SkillDefinition>> skillsByTemplateId;
        private System.Random rng;
        private float turnIntervalSec;
        private float elapsed;
        private int creditsPerWave;

        private readonly Dictionary<string, int> perMercenaryExp = new();
        private readonly List<DropRow> collectedDrops = new();
        private readonly List<AllySessionState> allyStates = new();

        public BattleSession(string locationId, string locationName, List<string> mercenaryIds,
            GameCsvTables tables, TalentCatalog talentCatalog)
        {
            SessionId = $"session_{Guid.NewGuid():N}";
            LocationId = locationId;
            LocationName = locationName;
            MercenaryIds = new List<string>(mercenaryIds);
            StartTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            this.tables = tables;
            this.talentCatalog = talentCatalog;
            rng = new System.Random();
            eventBus = new BattleEventBus();
            roster = new BattleRoster();
            skillsByTemplateId = new Dictionary<string, IReadOnlyList<SkillDefinition>>();

            foreach (var mercId in mercenaryIds)
                perMercenaryExp[mercId] = 0;
        }

        public bool TryInitialize(out string error)
        {
            error = string.Empty;

            if (tables.TryGetBattleSetup("", out var setup, out _))
                turnIntervalSec = setup.TurnIntervalSec;
            else
                turnIntervalSec = Mathf.Max(0.1f, tables.GetDefineInt("turnSeconds", 3));

            RewardBudget = Mathf.Max(1, tables.GetDefineInt("dispatchRewardBudget", 20));
            creditsPerWave = Mathf.Max(0, tables.GetDefineInt("creditsPerWave", 80));

            if (!TryBuildAllyDefinitions(out var allyDefs, out error))
                return false;

            foreach (var def in allyDefs)
            {
                var unit = new BattleUnit(def.mercenaryId, def.templateId, BattleTeam.Ally, def.statBlock);
                roster.Add(unit);

                if (!skillsByTemplateId.ContainsKey(unit.TemplateId))
                    skillsByTemplateId[unit.TemplateId] = tables.GetSkills(unit.TemplateId);
                unit.SetComputedStat(PassiveApplicator.Apply(unit.BaseStat, skillsByTemplateId[unit.TemplateId]));

                allyStates.Add(new AllySessionState
                {
                    mercenaryId = def.mercenaryId,
                    templateId = def.templateId,
                    displayName = def.displayName,
                    hp = unit.Hp,
                    maxHp = unit.Stat.MaxHp,
                    iconResourcePath = def.prefabResourcePath,
                });
            }

            waveManager = new WaveManager(LocationId, tables, rng);
            SpawnNextWave();
            return simulation != null;
        }

        public bool Tick(float deltaTime)
        {
            if (IsComplete) return false;

            elapsed += deltaTime;
            if (elapsed < turnIntervalSec) return true;

            elapsed = 0f;
            var progressed = simulation.TickOneTurn();

            SyncAllyStates();

            if (progressed) return true;

            if (!roster.HasAlive(BattleTeam.Ally))
            {
                Complete("defeat");
                return false;
            }

            waveManager.OnWaveCleared();
            OnWaveCleared?.Invoke(waveManager.StagesClearedCount);
            var killed = roster.ExtractKilledEnemies();
            ResolveDrops(killed);
            DistributeExp(killed);

            if (creditsPerWave > 0)
            {
                PlayerAccountService.AddCredits(creditsPerWave);
                TotalCreditsEarned += creditsPerWave;
            }

            if (CollectedRewardCount >= RewardBudget)
            {
                Complete("victory");
                return false;
            }

            SpawnNextWave();
            return true;
        }

        public float GetElapsedSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - StartTimeUnix;
        }

        public float GetExpPerHour()
        {
            var sec = GetElapsedSeconds();
            return sec > 0 ? TotalExpEarned / sec * 3600f : 0f;
        }

        public void Abort()
        {
            if (!IsComplete) Complete("retreat");
        }

        private void Complete(string reason)
        {
            IsComplete = true;
            CompletionReason = reason;
            SyncAllyStates();
            OnSessionCompleted?.Invoke(reason);
        }

        private void SyncAllyStates()
        {
            foreach (var state in allyStates)
            {
                var unit = roster.Allies.FirstOrDefault(u => u.MercenaryId == state.mercenaryId);
                if (unit != null)
                {
                    state.hp = unit.Hp;
                    state.maxHp = unit.Stat.MaxHp;
                }
            }
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
                    break;
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
                Complete("victory");
                return;
            }

            if (!TryBuildEnemyDefinitions(enemyIds, out var defs, out _))
            {
                Complete("defeat");
                return;
            }

            var spawnedEnemies = new List<BattleUnit>(defs.Length);
            foreach (var def in defs)
            {
                var unit = new BattleUnit(def.mercenaryId, def.templateId, BattleTeam.Enemy, def.statBlock);
                roster.Add(unit);

                if (!skillsByTemplateId.ContainsKey(unit.TemplateId))
                    skillsByTemplateId[unit.TemplateId] = tables.GetSkills(unit.TemplateId);
                unit.SetComputedStat(PassiveApplicator.Apply(unit.BaseStat, skillsByTemplateId[unit.TemplateId]));
                spawnedEnemies.Add(unit);
            }

            OnEnemiesSpawned?.Invoke(spawnedEnemies);

            simulation = new BattleSimulation(eventBus, roster, new BattleTurnService(), skillsByTemplateId, rng);
            simulation.Start();
            elapsed = 0f;
        }

        private void ResolveDrops(List<(string runtimeId, string templateId)> killed)
        {
            var dropped = DropResolver.Resolve(killed.Select(k => k.templateId), tables, rng);
            foreach (var drop in dropped)
            {
                InventoryService.AddDrop(drop);
                collectedDrops.Add(drop);
                CollectedRewardCount++;
            }
        }

        private void DistributeExp(List<(string runtimeId, string templateId)> killed)
        {
            var totalExp = 0f;
            foreach (var (_, templateId) in killed)
            {
                if (tables.TryGetCombatUnit(templateId, out var row))
                    totalExp += row.ExpGain;
            }

            if (totalExp <= 0f) return;

            var aliveAllies = roster.Allies.Where(u => u.IsAlive && !string.IsNullOrWhiteSpace(u.MercenaryId)).ToList();
            if (aliveAllies.Count == 0) return;

            var expPerAlly = Mathf.Max(1, Mathf.RoundToInt(totalExp / aliveAllies.Count));
            foreach (var ally in aliveAllies)
            {
                PlayerAccountService.AddExp(ally.MercenaryId, expPerAlly, tables);
                perMercenaryExp.TryGetValue(ally.MercenaryId, out var prev);
                perMercenaryExp[ally.MercenaryId] = prev + expPerAlly;
                TotalExpEarned += expPerAlly;
            }
        }

        private bool TryBuildAllyDefinitions(out BattleUnitDefinition[] defs, out string error)
        {
            var list = new List<BattleUnitDefinition>(MercenaryIds.Count);
            error = string.Empty;

            for (var i = 0; i < MercenaryIds.Count; i++)
            {
                var record = PlayerAccountService.GetRecordByMercenaryId(MercenaryIds[i]);
                if (record == null)
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"Missing mercenary record: {MercenaryIds[i]}";
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

                list.Add(new BattleUnitDefinition
                {
                    mercenaryId = record.mercenaryId,
                    templateId = row.EntityId,
                    talentTag = record.talentTag,
                    displayName = string.IsNullOrWhiteSpace(characterRow.Name) ? row.Name : characterRow.Name,
                    statBlock = stat,
                    prefabResourcePath = row.PrefabResourcePath,
                    spawnX = 0f,
                    spawnY = 0f,
                });
            }

            defs = list.ToArray();
            return true;
        }

        private bool TryBuildEnemyDefinitions(List<string> enemyIds, out BattleUnitDefinition[] defs, out string error)
        {
            var list = new List<BattleUnitDefinition>(enemyIds.Count);
            error = string.Empty;

            for (var i = 0; i < enemyIds.Count; i++)
            {
                if (!tables.TryGetCombatUnit(enemyIds[i], out var row))
                {
                    defs = Array.Empty<BattleUnitDefinition>();
                    error = $"combat_units.csv missing entityId={enemyIds[i]}";
                    return false;
                }

                list.Add(new BattleUnitDefinition
                {
                    mercenaryId = string.Empty,
                    templateId = row.EntityId,
                    talentTag = "NONE",
                    displayName = string.IsNullOrWhiteSpace(row.Name) ? row.EntityId : row.Name,
                    statBlock = BuildStatBlock(row),
                    prefabResourcePath = row.PrefabResourcePath,
                    spawnX = 0f,
                    spawnY = 0f,
                });
            }

            defs = list.ToArray();
            return true;
        }

        private static BattleStatBlock BuildStatBlock(CombatUnitRow row)
        {
            return new BattleStatBlock
            {
                MaxHp = row.MaxHp, MaxMana = row.MaxMana,
                Stamina = row.Stamina, Agility = row.Agility,
                Intelligence = row.Intelligence, Strength = row.Strength,
                Attack = row.Attack, Defense = row.Defense,
                HpRegen = row.HpRegen, ThornPhysical = row.ThornPhysical,
                ThornMagical = row.ThornMagical, Evasion = row.Evasion,
                CritChance = row.CritChance, CritDamage = row.CritDamage,
                LifeSteal = row.LifeSteal, Counter = row.Counter,
                ExpGain = row.ExpGain, HealPower = row.HealPower,
                AttackRangeType = row.AttackRangeType, DamageType = row.DamageType,
            };
        }

        private static BattleStatBlock ApplyLevelMultiplier(BattleStatBlock stat, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f)) return stat;
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

        private static BattleStatBlock ApplyEquipmentBonuses(BattleStatBlock stat, IReadOnlyList<InventoryService.EquipmentRecord> equipments)
        {
            if (equipments == null) return stat;
            foreach (var eq in equipments)
            {
                if (eq == null) continue;
                switch (eq.equipType)
                {
                    case "weapon": stat.Attack += eq.statValue; break;
                    case "armor": stat.Defense += eq.statValue; break;
                    case "accessory": stat.MaxHp += eq.statValue; break;
                    case "extra": stat.MaxMana += eq.statValue; break;
                }
            }
            return stat;
        }
    }
}
