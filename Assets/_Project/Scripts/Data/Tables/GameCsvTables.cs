using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProjectH.Battle;
using ProjectH.Data.Csv;
using UnityEngine;

namespace ProjectH.Data.Tables
{
    public readonly struct CombatUnitRow
    {
        public CombatUnitRow(
            string entityType, string entityId, string name,
            string attackRangeType, string damageType,
            int maxHp, int maxMana, int stamina, int agility, int intelligence, int strength,
            int attack, int defense, int hpRegen, int thornPhysical, int thornMagical,
            float evasion, float critChance, float critDamage,
            float lifeSteal, float counter, float expGain, float healPower,
            string prefabResourcePath)
        {
            EntityType = entityType;
            EntityId = entityId;
            Name = name;
            AttackRangeType = attackRangeType;
            DamageType = damageType;
            MaxHp = maxHp;
            MaxMana = maxMana;
            Stamina = stamina;
            Agility = agility;
            Intelligence = intelligence;
            Strength = strength;
            Attack = attack;
            Defense = defense;
            HpRegen = hpRegen;
            ThornPhysical = thornPhysical;
            ThornMagical = thornMagical;
            Evasion = evasion;
            CritChance = critChance;
            CritDamage = critDamage;
            LifeSteal = lifeSteal;
            Counter = counter;
            ExpGain = expGain;
            HealPower = healPower;
            PrefabResourcePath = prefabResourcePath;
        }

        public string EntityType { get; }
        public string EntityId { get; }
        public string Name { get; }
        public string AttackRangeType { get; }
        public string DamageType { get; }
        public int MaxHp { get; }
        public int MaxMana { get; }
        public int Stamina { get; }
        public int Agility { get; }
        public int Intelligence { get; }
        public int Strength { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int HpRegen { get; }
        public int ThornPhysical { get; }
        public int ThornMagical { get; }
        public float Evasion { get; }
        public float CritChance { get; }
        public float CritDamage { get; }
        public float LifeSteal { get; }
        public float Counter { get; }
        public float ExpGain { get; }
        public float HealPower { get; }
        public string PrefabResourcePath { get; }
    }

    public readonly struct BattleSetupRow
    {
        public BattleSetupRow(
            string setupId,
            string enemyLocationId,
            int enemyWaveIndex,
            float turnIntervalSec,
            float allyStartX,
            float allyStartY,
            float allySpacingX,
            float enemyStartX,
            float enemyStartY,
            float enemySpacingX)
        {
            SetupId = setupId;
            EnemyLocationId = enemyLocationId;
            EnemyWaveIndex = enemyWaveIndex;
            TurnIntervalSec = turnIntervalSec;
            AllyStartX = allyStartX;
            AllyStartY = allyStartY;
            AllySpacingX = allySpacingX;
            EnemyStartX = enemyStartX;
            EnemyStartY = enemyStartY;
            EnemySpacingX = enemySpacingX;
        }

        public string SetupId { get; }
        public string EnemyLocationId { get; }
        public int EnemyWaveIndex { get; }
        public float TurnIntervalSec { get; }
        public float AllyStartX { get; }
        public float AllyStartY { get; }
        public float AllySpacingX { get; }
        public float EnemyStartX { get; }
        public float EnemyStartY { get; }
        public float EnemySpacingX { get; }
    }

    public readonly struct RecruitPoolEntry
    {
        public RecruitPoolEntry(string poolId, string templateId, int weight, int offerCount)
        {
            PoolId = poolId;
            TemplateId = templateId;
            Weight = weight;
            OfferCount = offerCount;
        }

        public string PoolId { get; }
        public string TemplateId { get; }
        public int Weight { get; }
        public int OfferCount { get; }
    }

    public readonly struct LocationRow
    {
        public LocationRow(string locationId, string name, bool isOpen)
        {
            LocationId = locationId;
            Name = name;
            IsOpen = isOpen;
        }

        public string LocationId { get; }
        public string Name { get; }
        public bool IsOpen { get; }
    }

    public readonly struct CharacterRow
    {
        public CharacterRow(
            string templateId,
            string name,
            int grade,
            string roleTag,
            string promotionRouteA,
            string promotionRouteB)
        {
            TemplateId = templateId;
            Name = name;
            Grade = grade;
            RoleTag = roleTag;
            PromotionRouteA = promotionRouteA;
            PromotionRouteB = promotionRouteB;
        }

        public string TemplateId { get; }
        public string Name { get; }
        public int Grade { get; }
        public string RoleTag { get; }
        public string PromotionRouteA { get; }
        public string PromotionRouteB { get; }
    }

    public readonly struct PromotionRuleRow
    {
        public PromotionRuleRow(
            int gradeFrom,
            int gradeTo,
            int requiredLevel,
            int timeSeconds,
            int costCredits,
            string route,
            float multiplierBonus)
        {
            GradeFrom = gradeFrom;
            GradeTo = gradeTo;
            RequiredLevel = requiredLevel;
            TimeSeconds = timeSeconds;
            CostCredits = costCredits;
            Route = route;
            MultiplierBonus = multiplierBonus;
        }

        public int GradeFrom { get; }
        public int GradeTo { get; }
        public int RequiredLevel { get; }
        public int TimeSeconds { get; }
        public int CostCredits { get; }
        public string Route { get; }
        public float MultiplierBonus { get; }
    }

    public readonly struct LevelCurveRow
    {
        public LevelCurveRow(int level, int expToNext, float statMultiplier)
        {
            Level = level;
            ExpToNext = expToNext;
            StatMultiplier = statMultiplier;
        }

        public int Level { get; }
        public int ExpToNext { get; }
        public float StatMultiplier { get; }
    }

    public readonly struct StageRuleRow
    {
        public StageRuleRow(
            string locationId,
            float battleStageWeight,
            float exploreStageWeight,
            int bossEveryStageClears,
            int hiddenEveryStageClears,
            float hiddenEnterChance)
        {
            LocationId = locationId;
            BattleStageWeight = battleStageWeight;
            ExploreStageWeight = exploreStageWeight;
            BossEveryStageClears = bossEveryStageClears;
            HiddenEveryStageClears = hiddenEveryStageClears;
            HiddenEnterChance = hiddenEnterChance;
        }

        public string LocationId { get; }
        public float BattleStageWeight { get; }
        public float ExploreStageWeight { get; }
        public int BossEveryStageClears { get; }
        public int HiddenEveryStageClears { get; }
        public float HiddenEnterChance { get; }
    }

    public readonly struct EncounterRow
    {
        public EncounterRow(
            string locationId,
            string stageType,
            string encounterId,
            int weight,
            string monsterTemplateId,
            int count)
        {
            LocationId = locationId;
            StageType = stageType;
            EncounterId = encounterId;
            Weight = weight;
            MonsterTemplateId = monsterTemplateId;
            Count = count;
        }

        public string LocationId { get; }
        public string StageType { get; }
        public string EncounterId { get; }
        public int Weight { get; }
        public string MonsterTemplateId { get; }
        public int Count { get; }
    }

    public readonly struct DropRow
    {
        public DropRow(
            string monsterTemplateId,
            string itemId,
            string itemName,
            float dropRate,
            string equipType,
            int grade,
            int statValue)
        {
            MonsterTemplateId = monsterTemplateId;
            ItemId = itemId;
            ItemName = itemName;
            DropRate = dropRate;
            EquipType = equipType;
            Grade = grade;
            StatValue = statValue;
        }

        public string MonsterTemplateId { get; }
        public string ItemId { get; }
        public string ItemName { get; }
        public float DropRate { get; }
        public string EquipType { get; }
        public int Grade { get; }
        public int StatValue { get; }
        public bool IsEquipment => !string.IsNullOrWhiteSpace(EquipType) && Grade > 0;
    }

    public readonly struct RecipeMaterialRequirement
    {
        public RecipeMaterialRequirement(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }

        public string ItemId { get; }
        public int Amount { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Amount > 0;
    }

    public readonly struct RecipeRow
    {
        public RecipeRow(
            string recipeId,
            string resultEquipType,
            int resultGrade,
            int statValue,
            int costCredits,
            string material1ItemId,
            int material1Amount,
            string material2ItemId,
            int material2Amount,
            string material3ItemId,
            int material3Amount,
            string material4ItemId,
            int material4Amount,
            string material5ItemId,
            int material5Amount,
            int craftSeconds)
        {
            RecipeId = recipeId;
            ResultEquipType = resultEquipType;
            ResultGrade = resultGrade;
            StatValue = statValue;
            CostCredits = costCredits;
            Material1ItemId = material1ItemId;
            Material1Amount = material1Amount;
            Material2ItemId = material2ItemId;
            Material2Amount = material2Amount;
            Material3ItemId = material3ItemId;
            Material3Amount = material3Amount;
            Material4ItemId = material4ItemId;
            Material4Amount = material4Amount;
            Material5ItemId = material5ItemId;
            Material5Amount = material5Amount;
            CraftSeconds = craftSeconds;
        }

        public string RecipeId { get; }
        public string ResultEquipType { get; }
        public int ResultGrade { get; }
        public int StatValue { get; }
        public int CostCredits { get; }
        public string Material1ItemId { get; }
        public int Material1Amount { get; }
        public string Material2ItemId { get; }
        public int Material2Amount { get; }
        public string Material3ItemId { get; }
        public int Material3Amount { get; }
        public string Material4ItemId { get; }
        public int Material4Amount { get; }
        public string Material5ItemId { get; }
        public int Material5Amount { get; }
        public int CraftSeconds { get; }

        public RecipeMaterialRequirement GetMaterialRequirement(int index)
        {
            switch (index)
            {
                case 1: return new RecipeMaterialRequirement(Material1ItemId, Material1Amount);
                case 2: return new RecipeMaterialRequirement(Material2ItemId, Material2Amount);
                case 3: return new RecipeMaterialRequirement(Material3ItemId, Material3Amount);
                case 4: return new RecipeMaterialRequirement(Material4ItemId, Material4Amount);
                case 5: return new RecipeMaterialRequirement(Material5ItemId, Material5Amount);
                default: return default;
            }
        }

        public IReadOnlyList<RecipeMaterialRequirement> GetMaterialRequirements()
        {
            var list = new List<RecipeMaterialRequirement>(5);
            for (var i = 1; i <= 5; i++)
            {
                var requirement = GetMaterialRequirement(i);
                if (requirement.IsValid)
                {
                    list.Add(requirement);
                }
            }

            return list;
        }
    }

    public readonly struct OfficeLevelRow
    {
        public OfficeLevelRow(int officeLevel, int offerResetSeconds, int minGrade, int maxGrade,
            int offerCount, int rerollCostCredits, int upgradeCostCredits)
        {
            OfficeLevel = officeLevel;
            OfferResetSeconds = offerResetSeconds;
            MinGrade = minGrade;
            MaxGrade = maxGrade;
            OfferCount = offerCount;
            RerollCostCredits = rerollCostCredits;
            UpgradeCostCredits = upgradeCostCredits;
        }

        public int OfficeLevel { get; }
        public int OfferResetSeconds { get; }
        public int MinGrade { get; }
        public int MaxGrade { get; }
        public int OfferCount { get; }
        public int RerollCostCredits { get; }
        public int UpgradeCostCredits { get; }
        public bool IsMaxLevel => UpgradeCostCredits <= 0;
    }

    public readonly struct BattlePositionRow
    {
        public BattlePositionRow(string locationId, string side, int slotIndex, float normX, float normY)
        {
            LocationId = locationId;
            Side       = side;
            SlotIndex  = slotIndex;
            NormX      = normX;
            NormY      = normY;
        }

        public string LocationId { get; }
        public string Side       { get; }   // "ally" or "enemy"
        public int    SlotIndex  { get; }
        public float  NormX      { get; }   // viewport 0~1
        public float  NormY      { get; }   // viewport 0~1
    }

    public sealed class GameCsvTables
    {
        private static readonly string[] RequiredTableNames =
        {
            "battle_positions",
            "battle_setup",
            "characters",
            "combat_skills",
            "combat_units",
            "define_table",
            "field_stage_encounters",
            "field_stage_rules",
            "level_curve",
            "locations",
            "location_waves",
            "monster_drops",
            "office_level",
            "promotion_rules",
            "recruit_pool",
            "recipes",
            "talents",
        };

        private readonly Dictionary<string, CombatUnitRow> combatUnitsById;
        private readonly Dictionary<string, BattleSetupRow> battleSetupsById;
        private readonly string firstBattleSetupId;
        private readonly Dictionary<string, List<RecruitPoolEntry>> recruitPoolById;
        private readonly List<LocationRow> locations;
        private readonly Dictionary<string, int> defineValues;
        private readonly Dictionary<string, List<(string monsterTemplateId, int count)>> waveMonsterRows;
        private readonly Dictionary<string, List<SkillDefinition>> skillsByOwnerId;
        private readonly Dictionary<string, StageRuleRow> stageRulesById;
        private readonly Dictionary<string, List<EncounterRow>> encountersByKey;
        private readonly Dictionary<string, List<DropRow>> dropsByTemplateId;
        private readonly Dictionary<string, string> itemNameById;
        private readonly Dictionary<int, LevelCurveRow> levelCurveByLevel;
        private readonly int maxLevel;
        private readonly Dictionary<string, CharacterRow> characterRowsById;
        private readonly List<RecipeRow> recipes;
        // Key: "gradeFrom::route" e.g. "1::A"
        private readonly Dictionary<string, PromotionRuleRow> promotionRulesByKey;
        private readonly Dictionary<int, OfficeLevelRow> officeLevelRows;
        private readonly int maxOfficeLevel;
        // Key: "locationId::side" e.g. "SITE_ALPHA::ally"
        private readonly Dictionary<string, List<BattlePositionRow>> battlePositionsByKey;

        private GameCsvTables(
            Dictionary<string, CombatUnitRow> combatUnitsById,
            Dictionary<string, BattleSetupRow> battleSetupsById,
            string firstBattleSetupId,
            Dictionary<string, List<RecruitPoolEntry>> recruitPoolById,
            List<LocationRow> locations,
            Dictionary<string, int> defineValues,
            Dictionary<string, List<(string monsterTemplateId, int count)>> waveMonsterRows,
            Dictionary<string, List<SkillDefinition>> skillsByOwnerId,
            Dictionary<string, StageRuleRow> stageRulesById,
            Dictionary<string, List<EncounterRow>> encountersByKey,
            Dictionary<string, List<DropRow>> dropsByTemplateId,
            Dictionary<int, LevelCurveRow> levelCurveByLevel,
            Dictionary<string, CharacterRow> characterRowsById,
            List<RecipeRow> recipes,
            Dictionary<string, PromotionRuleRow> promotionRulesByKey,
            Dictionary<int, OfficeLevelRow> officeLevelRows,
            Dictionary<string, List<BattlePositionRow>> battlePositionsByKey)
        {
            this.combatUnitsById = combatUnitsById;
            this.battleSetupsById = battleSetupsById;
            this.firstBattleSetupId = firstBattleSetupId;
            this.recruitPoolById = recruitPoolById;
            this.locations = locations;
            this.defineValues = defineValues;
            this.waveMonsterRows = waveMonsterRows;
            this.skillsByOwnerId = skillsByOwnerId;
            this.stageRulesById = stageRulesById;
            this.encountersByKey = encountersByKey;
            this.dropsByTemplateId = dropsByTemplateId;
            itemNameById = BuildItemNameIndex(dropsByTemplateId);
            this.levelCurveByLevel = levelCurveByLevel;
            maxLevel = levelCurveByLevel.Count > 0 ? levelCurveByLevel.Keys.Max() : 1;
            this.characterRowsById = characterRowsById;
            this.recipes = recipes;
            this.promotionRulesByKey = promotionRulesByKey;
            this.officeLevelRows = officeLevelRows;
            maxOfficeLevel = officeLevelRows.Count > 0 ? officeLevelRows.Keys.Max() : 1;
            this.battlePositionsByKey = battlePositionsByKey;
        }

        public static bool TryLoad(out GameCsvTables tables, out string error)
        {
            tables = null;
            error = string.Empty;

            var loadedTables = new Dictionary<string, CsvTable>(StringComparer.OrdinalIgnoreCase);
            foreach (var tableName in RequiredTableNames)
            {
                var asset = Resources.Load<TextAsset>($"Tables/{tableName}");
                if (asset == null)
                {
                    error = $"Missing table: Resources/Tables/{tableName}.csv";
                    return false;
                }

                var table = CsvParser.Parse(asset.text);
                if (table.Headers.Length == 0)
                {
                    error = $"Invalid/empty table: Resources/Tables/{tableName}.csv";
                    return false;
                }

                loadedTables[tableName] = table;
            }

            var combatUnitsById = BuildCombatUnits(loadedTables["combat_units"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var waveRows = BuildWaveRows(loadedTables["location_waves"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var battleSetupsById = BuildBattleSetups(loadedTables["battle_setup"], out var firstBattleSetupId, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var recruitPoolById = BuildRecruitPools(loadedTables["recruit_pool"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var locations = BuildLocations(loadedTables["locations"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var defineValues = BuildDefineValues(loadedTables["define_table"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var skillsByOwnerId = BuildSkills(loadedTables["combat_skills"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var stageRulesById = BuildStageRules(loadedTables["field_stage_rules"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var encountersByKey = BuildEncounters(loadedTables["field_stage_encounters"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var dropsByTemplateId = BuildDrops(loadedTables["monster_drops"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var levelCurveByLevel = BuildLevelCurve(loadedTables["level_curve"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var characterRowsById = BuildCharacters(loadedTables["characters"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var promotionRulesByKey = BuildPromotionRules(loadedTables["promotion_rules"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var recipes = BuildRecipes(loadedTables["recipes"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var officeLevelRows = BuildOfficeLevels(loadedTables["office_level"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var battlePositionsByKey = BuildBattlePositions(loadedTables["battle_positions"], out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            tables = new GameCsvTables(combatUnitsById, battleSetupsById, firstBattleSetupId, recruitPoolById, locations, defineValues, waveRows, skillsByOwnerId, stageRulesById, encountersByKey, dropsByTemplateId, levelCurveByLevel, characterRowsById, recipes, promotionRulesByKey, officeLevelRows, battlePositionsByKey);
            return true;
        }

        public bool TryGetCombatUnit(string entityId, out CombatUnitRow row)
        {
            return combatUnitsById.TryGetValue(entityId ?? string.Empty, out row);
        }

        public bool TryGetBattleSetup(string setupId, out BattleSetupRow row, out string error)
        {
            error = string.Empty;

            if (battleSetupsById.Count == 0)
            {
                row = default;
                error = "battle_setup.csv has no rows";
                return false;
            }

            if (string.IsNullOrWhiteSpace(setupId))
            {
                return battleSetupsById.TryGetValue(firstBattleSetupId, out row);
            }

            if (!battleSetupsById.TryGetValue(setupId.Trim(), out row))
            {
                error = $"battle_setup.csv missing setupId={setupId}";
                return false;
            }

            return true;
        }

        public IReadOnlyList<LocationRow> GetOpenLocations()
        {
            return locations.Where((l) => l.IsOpen).ToList();
        }

        public IReadOnlyList<SkillDefinition> GetSkills(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return System.Array.Empty<SkillDefinition>();
            }

            return skillsByOwnerId.TryGetValue(ownerId.Trim(), out var list)
                ? list
                : System.Array.Empty<SkillDefinition>();
        }

        public bool TryGetCharacterRow(string templateId, out CharacterRow row)
        {
            return characterRowsById.TryGetValue(templateId ?? string.Empty, out row);
        }

        public IReadOnlyList<RecipeRow> GetRecipes()
        {
            return recipes.ToList();
        }

        /// <summary>route: "A" ?????"B"</summary>
        public bool TryGetPromotionRule(int gradeFrom, string route, out PromotionRuleRow row)
        {
            var key = $"{gradeFrom}::{(route ?? string.Empty).ToUpperInvariant()}";
            return promotionRulesByKey.TryGetValue(key, out row);
        }

        public int GetMaxLevel() => maxLevel;

        public bool TryGetOfficeLevelRow(int level, out OfficeLevelRow row)
        {
            return officeLevelRows.TryGetValue(level, out row);
        }

        public int GetMaxOfficeLevel() => maxOfficeLevel;

        /// <summary>
        /// 현장·진영에 해당하는 스폰 슬롯 목록을 slotIndex 오름차순으로 반환합니다.
        /// side: "ally" 또는 "enemy"
        /// </summary>
        public IReadOnlyList<BattlePositionRow> GetBattleSlots(string locationId, string side)
        {
            var key = $"{locationId ?? string.Empty}::{(side ?? string.Empty).ToLowerInvariant()}";
            return battlePositionsByKey.TryGetValue(key, out var list)
                ? list
                : System.Array.Empty<BattlePositionRow>();
        }

        public int GetExpToNextLevel(int level)
        {
            return levelCurveByLevel.TryGetValue(level, out var row) ? row.ExpToNext : int.MaxValue;
        }

        public float GetLevelStatMultiplier(int level)
        {
            return levelCurveByLevel.TryGetValue(level, out var row) ? row.StatMultiplier : 1f;
        }

        public bool TryGetStageRule(string locationId, out StageRuleRow row)
        {
            return stageRulesById.TryGetValue(locationId ?? string.Empty, out row);
        }

        public IReadOnlyList<EncounterRow> GetEncounterRows(string locationId, string stageType)
        {
            var key = BuildEncounterKey(locationId, stageType);
            return encountersByKey.TryGetValue(key, out var list) ? list : System.Array.Empty<EncounterRow>();
        }

        public IReadOnlyList<DropRow> GetDropRows(string monsterTemplateId)
        {
            if (string.IsNullOrWhiteSpace(monsterTemplateId))
            {
                return System.Array.Empty<DropRow>();
            }

            return dropsByTemplateId.TryGetValue(monsterTemplateId.Trim(), out var list)
                ? list
                : System.Array.Empty<DropRow>();
        }

        public string GetItemName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return itemId;
            }

            return itemNameById.TryGetValue(itemId.Trim(), out var name) ? name : itemId;
        }

        private static Dictionary<string, string> BuildItemNameIndex(Dictionary<string, List<DropRow>> dropsByTemplateId)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var list in dropsByTemplateId.Values)
            {
                foreach (var row in list)
                {
                    if (!string.IsNullOrWhiteSpace(row.ItemId) && !string.IsNullOrWhiteSpace(row.ItemName))
                    {
                        index[row.ItemId] = row.ItemName;
                    }
                }
            }

            return index;
        }

        public int GetDefineInt(string key, int fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            return defineValues.TryGetValue(key.Trim(), out var v) ? v : fallback;
        }

        public bool TryDrawRecruitOffers(string poolId, int overrideOfferCount, int minGrade, int maxGrade,
            out List<CombatUnitRow> offers, out string error)
        {
            offers = new List<CombatUnitRow>();
            error = string.Empty;

            if (!recruitPoolById.TryGetValue(poolId ?? string.Empty, out var entries) || entries.Count == 0)
            {
                error = $"recruit_pool.csv missing poolId={poolId}";
                return false;
            }

            var offerCount = overrideOfferCount > 0 ? overrideOfferCount : entries[0].OfferCount;
            if (offerCount <= 0)
            {
                offerCount = 3;
            }

            var candidates = new List<RecruitPoolEntry>(entries);

            // Filter by grade if a valid range is provided
            if (minGrade > 0 || maxGrade > 0)
            {
                candidates = candidates.Where(entry =>
                {
                    if (!characterRowsById.TryGetValue(entry.TemplateId, out var charRow))
                    {
                        return true; // keep unknown entries
                    }

                    var grade = charRow.Grade;
                    var aboveMin = minGrade <= 0 || grade >= minGrade;
                    var belowMax = maxGrade <= 0 || grade <= maxGrade;
                    return aboveMin && belowMax;
                }).ToList();
            }

            if (candidates.Count == 0)
            {
                error = $"No recruitable entries in grade range [{minGrade},{maxGrade}] for poolId={poolId}";
                return false;
            }

            var rng = new System.Random();

            while (offers.Count < offerCount && candidates.Count > 0)
            {
                var total = candidates.Sum((x) => Math.Max(1, x.Weight));
                var roll = rng.Next(0, total);
                var acc = 0;
                var pickedIndex = 0;
                for (var i = 0; i < candidates.Count; i++)
                {
                    acc += Math.Max(1, candidates[i].Weight);
                    if (roll < acc)
                    {
                        pickedIndex = i;
                        break;
                    }
                }

                var picked = candidates[pickedIndex];
                candidates.RemoveAt(pickedIndex);

                if (TryGetCombatUnit(picked.TemplateId, out var unit))
                {
                    offers.Add(unit);
                }
            }

            if (offers.Count == 0)
            {
                error = $"No recruitable entries resolved for poolId={poolId}";
                return false;
            }

            return true;
        }

        public List<string> BuildEnemyTemplateIds(string locationId, int waveIndex)
        {
            var key = BuildWaveKey(locationId, waveIndex);
            if (!waveMonsterRows.TryGetValue(key, out var rows))
            {
                return new List<string>();
            }

            var list = new List<string>();
            foreach (var row in rows)
            {
                for (var i = 0; i < row.count; i++)
                {
                    list.Add(row.monsterTemplateId);
                }
            }

            return list;
        }

        private static Dictionary<string, CombatUnitRow> BuildCombatUnits(CsvTable table, out string error)
        {
            var map = new Dictionary<string, CombatUnitRow>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var entityId = Get(r, "entityId");
                if (string.IsNullOrWhiteSpace(entityId))
                {
                    error = "combat_units.csv row has empty entityId";
                    return map;
                }

                var entityType      = Get(r, "entityType");
                var name            = Get(r, "name");
                var attackRangeType = Get(r, "attackRangeType");
                var damageType      = Get(r, "damageType");
                var maxHp           = ParseInt(Get(r, "maxHp"));
                var maxMana         = ParseInt(Get(r, "maxMana"));
                var stamina         = ParseInt(Get(r, "stamina"));
                var agility         = ParseInt(Get(r, "agility"));
                var intelligence    = ParseInt(Get(r, "intelligence"));
                var strength        = ParseInt(Get(r, "strength"));
                var attack          = ParseInt(Get(r, "attack"));
                var defense         = ParseInt(Get(r, "defense"));
                var hpRegen         = ParseInt(Get(r, "hpRegen"));
                var thornPhysical   = ParseInt(Get(r, "thornPhysical"));
                var thornMagical    = ParseInt(Get(r, "thornMagical"));
                var evasion         = ParseFloat(Get(r, "evasion"));
                var critChance      = ParseFloat(Get(r, "critChance"));
                var critDamage      = ParseFloat(Get(r, "critDamage"));
                var lifeSteal       = ParseFloat(Get(r, "lifeSteal"));
                var counter         = ParseFloat(Get(r, "counter"));
                var expGain         = ParseFloat(Get(r, "expGain"));
                var healPower       = ParseFloat(Get(r, "healPower"));
                var prefabResourcePath = Get(r, "prefabResourcePath");

                if (maxHp <= 0)
                {
                    error = $"combat_units.csv invalid maxHp: {entityId}";
                    return map;
                }

                map[entityId] = new CombatUnitRow(
                    entityType, entityId, name,
                    attackRangeType, damageType,
                    maxHp, maxMana, stamina, agility, intelligence, strength,
                    attack, defense, hpRegen, thornPhysical, thornMagical,
                    evasion, critChance, critDamage,
                    lifeSteal, counter, expGain, healPower,
                    prefabResourcePath);
            }

            if (map.Count == 0)
            {
                error = "combat_units.csv has no rows";
            }

            return map;
        }

        private static Dictionary<string, BattleSetupRow> BuildBattleSetups(CsvTable table, out string firstSetupId, out string error)
        {
            var map = new Dictionary<string, BattleSetupRow>(StringComparer.OrdinalIgnoreCase);
            firstSetupId = string.Empty;
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var setupId = Get(r, "setupId");
                var enemyLocationId = Get(r, "enemyLocationId");
                var enemyWaveIndex = ParseInt(Get(r, "enemyWaveIndex"));
                var turnIntervalSec = ParseFloat(Get(r, "turnIntervalSec"));
                var allyStartX = ParseFloat(Get(r, "allyStartX"));
                var allyStartY = ParseFloat(Get(r, "allyStartY"));
                var allySpacingX = ParseFloat(Get(r, "allySpacingX"));
                var enemyStartX = ParseFloat(Get(r, "enemyStartX"));
                var enemyStartY = ParseFloat(Get(r, "enemyStartY"));
                var enemySpacingX = ParseFloat(Get(r, "enemySpacingX"));

                if (string.IsNullOrWhiteSpace(setupId) ||
                    string.IsNullOrWhiteSpace(enemyLocationId) ||
                    turnIntervalSec <= 0f)
                {
                    error = "battle_setup.csv has an invalid row";
                    return map;
                }

                if (string.IsNullOrEmpty(firstSetupId))
                {
                    firstSetupId = setupId;
                }

                map[setupId] = new BattleSetupRow(
                    setupId,
                    enemyLocationId,
                    enemyWaveIndex,
                    turnIntervalSec,
                    allyStartX,
                    allyStartY,
                    allySpacingX,
                    enemyStartX,
                    enemyStartY,
                    enemySpacingX);
            }

            if (map.Count == 0)
            {
                error = "battle_setup.csv has no rows";
            }

            return map;
        }

        private static Dictionary<string, List<(string monsterTemplateId, int count)>> BuildWaveRows(CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<(string monsterTemplateId, int count)>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var locationId = Get(r, "locationId");
                var waveIndex = ParseInt(Get(r, "waveIndex"));
                var monsterTemplateId = Get(r, "monsterTemplateId");
                var count = Math.Max(1, ParseInt(Get(r, "count")));

                if (string.IsNullOrWhiteSpace(locationId) || waveIndex <= 0 || string.IsNullOrWhiteSpace(monsterTemplateId))
                {
                    error = "location_waves.csv row has invalid locationId/waveIndex/monsterTemplateId";
                    return map;
                }

                var key = BuildWaveKey(locationId, waveIndex);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<(string monsterTemplateId, int count)>();
                    map[key] = list;
                }

                list.Add((monsterTemplateId, count));
            }

            if (map.Count == 0)
            {
                error = "location_waves.csv has no rows";
            }

            return map;
        }

        private static Dictionary<string, List<RecruitPoolEntry>> BuildRecruitPools(CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<RecruitPoolEntry>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var poolId = Get(r, "poolId");
                var templateId = Get(r, "templateId");
                var weight = Math.Max(1, ParseInt(Get(r, "weight")));
                var offerCount = Math.Max(1, ParseInt(Get(r, "offerCount")));

                if (string.IsNullOrWhiteSpace(poolId) || string.IsNullOrWhiteSpace(templateId))
                {
                    error = "recruit_pool.csv row has invalid poolId/templateId";
                    return map;
                }

                if (!map.TryGetValue(poolId, out var list))
                {
                    list = new List<RecruitPoolEntry>();
                    map[poolId] = list;
                }

                list.Add(new RecruitPoolEntry(poolId, templateId, weight, offerCount));
            }

            if (map.Count == 0)
            {
                error = "recruit_pool.csv has no rows";
            }

            return map;
        }

        private static List<LocationRow> BuildLocations(CsvTable table, out string error)
        {
            var list = new List<LocationRow>();
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var locationId = Get(r, "locationId");
                var name = Get(r, "name");
                var isOpen = ParseInt(Get(r, "isOpen")) > 0;
                if (string.IsNullOrWhiteSpace(locationId))
                {
                    error = "locations.csv row has empty locationId";
                    return list;
                }

                list.Add(new LocationRow(locationId, name, isOpen));
            }

            if (list.Count == 0)
            {
                error = "locations.csv has no rows";
            }

            return list;
        }

        private static Dictionary<string, int> BuildDefineValues(CsvTable table, out string error)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var key = Get(r, "key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = ParseInt(Get(r, "value"));
            }

            return map;
        }

        private static Dictionary<string, List<SkillDefinition>> BuildSkills(CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<SkillDefinition>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var ownerId    = Get(r, "ownerId");
                var skillId    = Get(r, "skillId");
                var skillName  = Get(r, "skillName");
                var kindRaw    = Get(r, "kind");
                var effectType = Get(r, "effectType");
                var value1     = ParseFloat(Get(r, "value1"));

                if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(skillId))
                {
                    continue;
                }

                var kind = string.Equals(kindRaw, "active", StringComparison.OrdinalIgnoreCase)
                    ? SkillKind.Active
                    : SkillKind.Passive;

                if (!map.TryGetValue(ownerId, out var list))
                {
                    list = new List<SkillDefinition>();
                    map[ownerId] = list;
                }

                list.Add(new SkillDefinition(ownerId, skillId, skillName, kind, effectType, value1));
            }

            return map;
        }

        private static Dictionary<string, CharacterRow> BuildCharacters(CsvTable table, out string error)
        {
            var map = new Dictionary<string, CharacterRow>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var templateId       = Get(r, "templateId");
                var name             = Get(r, "name");
                var grade            = ParseInt(Get(r, "grade"));
                var roleTag          = Get(r, "roleTag");
                var promotionRouteA  = Get(r, "promotionRouteA");
                var promotionRouteB  = Get(r, "promotionRouteB");

                if (string.IsNullOrWhiteSpace(templateId))
                {
                    continue;
                }

                map[templateId] = new CharacterRow(templateId, name, grade, roleTag, promotionRouteA, promotionRouteB);
            }

            return map;
        }

        private static List<RecipeRow> BuildRecipes(CsvTable table, out string error)
        {
            var list = new List<RecipeRow>();
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var recipeId = Get(r, "recipeId");
                var resultEquipType = Get(r, "resultEquipType");
                var resultGrade = ParseInt(Get(r, "resultGrade"));
                var statValue = ParseInt(Get(r, "statValue"));
                var costCredits = ParseInt(Get(r, "costCredits"));
                var material1ItemId = Get(r, "material1ItemId");
                var material1Amount = ParseInt(Get(r, "material1Amount"));
                var material2ItemId = Get(r, "material2ItemId");
                var material2Amount = ParseInt(Get(r, "material2Amount"));
                var material3ItemId = Get(r, "material3ItemId");
                var material3Amount = ParseInt(Get(r, "material3Amount"));
                var material4ItemId = Get(r, "material4ItemId");
                var material4Amount = ParseInt(Get(r, "material4Amount"));
                var material5ItemId = Get(r, "material5ItemId");
                var material5Amount = ParseInt(Get(r, "material5Amount"));
                var craftSeconds = ParseInt(Get(r, "craftSeconds"));

                if (string.IsNullOrWhiteSpace(recipeId) || string.IsNullOrWhiteSpace(resultEquipType))
                {
                    continue;
                }

                list.Add(new RecipeRow(recipeId, resultEquipType, resultGrade, statValue, costCredits, material1ItemId, material1Amount, material2ItemId, material2Amount, material3ItemId, material3Amount, material4ItemId, material4Amount, material5ItemId, material5Amount, craftSeconds));
            }

            if (list.Count == 0)
            {
                error = "recipes.csv has no rows";
            }

            return list;
        }

        private static Dictionary<string, PromotionRuleRow> BuildPromotionRules(CsvTable table, out string error)
        {
            var map = new Dictionary<string, PromotionRuleRow>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var gradeFrom       = ParseInt(Get(r, "gradeFrom"));
                var gradeTo         = ParseInt(Get(r, "gradeTo"));
                var requiredLevel   = ParseInt(Get(r, "requiredLevel"));
                var timeSeconds     = ParseInt(Get(r, "timeSeconds"));
                var costCredits     = ParseInt(Get(r, "costCredits"));
                var route           = Get(r, "route").ToUpperInvariant();
                var multiplierBonus = ParseFloat(Get(r, "multiplierBonus"));

                if (gradeFrom <= 0 || string.IsNullOrWhiteSpace(route))
                {
                    continue;
                }

                var key = $"{gradeFrom}::{route}";
                map[key] = new PromotionRuleRow(gradeFrom, gradeTo, requiredLevel, timeSeconds, costCredits, route, multiplierBonus);
            }

            return map;
        }

        private static Dictionary<int, LevelCurveRow> BuildLevelCurve(CsvTable table, out string error)
        {
            var map = new Dictionary<int, LevelCurveRow>();
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var level         = ParseInt(Get(r, "level"));
                var expToNext     = ParseInt(Get(r, "expToNext"));
                var statMult      = ParseFloat(Get(r, "statMultiplier"));

                if (level <= 0)
                {
                    continue;
                }

                if (statMult <= 0f)
                {
                    statMult = 1f;
                }

                map[level] = new LevelCurveRow(level, expToNext, statMult);
            }

            if (map.Count == 0)
            {
                error = "level_curve.csv has no rows";
            }

            return map;
        }

        private static Dictionary<int, OfficeLevelRow> BuildOfficeLevels(CsvTable table, out string error)
        {
            var map = new Dictionary<int, OfficeLevelRow>();
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var officeLevel         = ParseInt(Get(r, "officeLevel"));
                var offerResetSeconds   = ParseInt(Get(r, "offerResetSeconds"));
                var minGrade            = ParseInt(Get(r, "minGrade"));
                var maxGrade            = ParseInt(Get(r, "maxGrade"));
                var offerCount          = ParseInt(Get(r, "offerCount"));
                var rerollCostCredits   = ParseInt(Get(r, "rerollCostCredits"));
                var upgradeCostCredits  = ParseInt(Get(r, "upgradeCostCredits"));

                if (officeLevel <= 0)
                {
                    continue;
                }

                if (offerCount <= 0) offerCount = 3;
                if (minGrade <= 0) minGrade = 1;
                if (maxGrade <= 0) maxGrade = 99;

                map[officeLevel] = new OfficeLevelRow(officeLevel, offerResetSeconds, minGrade, maxGrade,
                    offerCount, rerollCostCredits, upgradeCostCredits);
            }

            if (map.Count == 0)
            {
                error = "office_level.csv has no rows";
            }

            return map;
        }

        private static Dictionary<string, List<BattlePositionRow>> BuildBattlePositions(
            CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<BattlePositionRow>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var locationId = Get(r, "locationId");
                var side       = (Get(r, "side") ?? string.Empty).Trim().ToLowerInvariant();
                var slotIndex  = ParseInt(Get(r, "slotIndex"));
                var normX      = ParseFloat(Get(r, "normX"));
                var normY      = ParseFloat(Get(r, "normY"));

                if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(side))
                {
                    continue;
                }

                var key = $"{locationId.Trim()}::{side}";
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<BattlePositionRow>();
                    map[key] = list;
                }

                list.Add(new BattlePositionRow(locationId.Trim(), side, slotIndex,
                    Mathf.Clamp01(normX), Mathf.Clamp01(normY)));
            }

            // slotIndex 오름차순 정렬
            foreach (var list in map.Values)
            {
                list.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            }

            if (map.Count == 0)
            {
                error = "battle_positions.csv has no rows";
            }

            return map;
        }

        private static Dictionary<string, StageRuleRow> BuildStageRules(CsvTable table, out string error)
        {
            var map = new Dictionary<string, StageRuleRow>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var locationId          = Get(r, "locationId");
                var battleStageWeight   = ParseFloat(Get(r, "battleStageWeight"));
                var exploreStageWeight  = ParseFloat(Get(r, "exploreStageWeight"));
                var bossEvery           = ParseInt(Get(r, "bossEveryStageClears"));
                var hiddenEvery         = ParseInt(Get(r, "hiddenEveryStageClears"));
                var hiddenChance        = ParseFloat(Get(r, "hiddenEnterChance"));

                if (string.IsNullOrWhiteSpace(locationId))
                {
                    continue;
                }

                map[locationId] = new StageRuleRow(locationId, battleStageWeight, exploreStageWeight, bossEvery, hiddenEvery, hiddenChance);
            }

            return map;
        }

        private static Dictionary<string, List<EncounterRow>> BuildEncounters(CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<EncounterRow>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var locationId        = Get(r, "locationId");
                var stageType         = Get(r, "stageType");
                var encounterId       = Get(r, "encounterId");
                var weight            = Math.Max(1, ParseInt(Get(r, "weight")));
                var monsterTemplateId = Get(r, "monsterTemplateId");
                var count             = Math.Max(1, ParseInt(Get(r, "count")));

                if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(stageType) ||
                    string.IsNullOrWhiteSpace(encounterId) || string.IsNullOrWhiteSpace(monsterTemplateId))
                {
                    continue;
                }

                var key = BuildEncounterKey(locationId, stageType);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<EncounterRow>();
                    map[key] = list;
                }

                list.Add(new EncounterRow(locationId, stageType.ToUpperInvariant(), encounterId, weight, monsterTemplateId, count));
            }

            return map;
        }

        private static Dictionary<string, List<DropRow>> BuildDrops(CsvTable table, out string error)
        {
            var map = new Dictionary<string, List<DropRow>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            foreach (var r in table.Rows)
            {
                var monsterTemplateId = Get(r, "monsterTemplateId");
                var itemId            = Get(r, "itemId");
                var itemName          = Get(r, "itemName");
                var dropRate          = ParseFloat(Get(r, "dropRate"));
                var equipType         = Get(r, "equipType");
                var grade             = ParseInt(Get(r, "grade"));
                var statValue         = ParseInt(Get(r, "statValue"));

                if (string.IsNullOrWhiteSpace(monsterTemplateId) || string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                if (!map.TryGetValue(monsterTemplateId, out var list))
                {
                    list = new List<DropRow>();
                    map[monsterTemplateId] = list;
                }

                list.Add(new DropRow(monsterTemplateId, itemId, itemName, dropRate, equipType, grade, statValue));
            }

            return map;
        }

        private static string BuildEncounterKey(string locationId, string stageType)
        {
            return $"{locationId.Trim()}::{stageType.Trim().ToUpperInvariant()}";
        }

        private static string BuildWaveKey(string locationId, int waveIndex)
        {
            return $"{locationId.Trim()}::{waveIndex}";
        }

        private static int ParseInt(string raw)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private static float ParseFloat(string raw)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var v) ? (v ?? string.Empty).Trim() : string.Empty;
        }
    }
}
