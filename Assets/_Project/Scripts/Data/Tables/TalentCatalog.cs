using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProjectH.Battle;
using ProjectH.Data.Csv;
using UnityEngine;

namespace ProjectH.Data.Tables
{
    public readonly struct TalentDefinition
    {
        public TalentDefinition(
            string talentTag,
            string name,
            string description,
            float hpPct,
            float staminaPct,
            float agilityPct,
            float intelligencePct,
            float strengthPct,
            float attackPct,
            float defensePct)
        {
            TalentTag = talentTag;
            Name = name;
            Description = description;
            HpPct = hpPct;
            StaminaPct = staminaPct;
            AgilityPct = agilityPct;
            IntelligencePct = intelligencePct;
            StrengthPct = strengthPct;
            AttackPct = attackPct;
            DefensePct = defensePct;
        }

        public string TalentTag { get; }
        public string Name { get; }
        public string Description { get; }
        public float HpPct { get; }
        public float StaminaPct { get; }
        public float AgilityPct { get; }
        public float IntelligencePct { get; }
        public float StrengthPct { get; }
        public float AttackPct { get; }
        public float DefensePct { get; }
    }

    public sealed class TalentCatalog
    {
        private readonly Dictionary<string, TalentDefinition> talentsByTag;
        private readonly Dictionary<string, List<(string talentTag, int weight)>> poolByRoleTag;

        private TalentCatalog(
            Dictionary<string, TalentDefinition> talentsByTag,
            Dictionary<string, List<(string talentTag, int weight)>> poolByRoleTag)
        {
            this.talentsByTag = talentsByTag;
            this.poolByRoleTag = poolByRoleTag;
        }

        public static bool TryLoad(out TalentCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;

            var talentsAsset = Resources.Load<TextAsset>("Tables/talents");
            if (talentsAsset == null)
            {
                error = "Missing table: Resources/Tables/talents.csv";
                return false;
            }

            var poolAsset = Resources.Load<TextAsset>("Tables/talent_pool");
            if (poolAsset == null)
            {
                error = "Missing table: Resources/Tables/talent_pool.csv";
                return false;
            }

            var talents = BuildTalents(CsvParser.Parse(talentsAsset.text));
            var pool = BuildPool(CsvParser.Parse(poolAsset.text));
            catalog = new TalentCatalog(talents, pool);
            return true;
        }

        public string DrawTalentTag(string roleTag, System.Random rng)
        {
            if (string.IsNullOrWhiteSpace(roleTag) || rng == null)
            {
                return "NONE";
            }

            if (!poolByRoleTag.TryGetValue(roleTag.Trim(), out var entries) || entries.Count == 0)
            {
                return "NONE";
            }

            var totalWeight = entries.Sum(x => Math.Max(1, x.weight));
            var roll = rng.Next(0, totalWeight);
            var acc = 0;
            foreach (var entry in entries)
            {
                acc += Math.Max(1, entry.weight);
                if (roll < acc)
                {
                    return entry.talentTag;
                }
            }

            return "NONE";
        }

        public bool TryGetTalent(string talentTag, out TalentDefinition definition)
        {
            return talentsByTag.TryGetValue(talentTag ?? string.Empty, out definition);
        }

        public string GetTalentName(string talentTag)
        {
            return TryGetTalent(talentTag, out var definition) ? definition.Name : "재능 없음";
        }

        public string GetTalentDescription(string talentTag)
        {
            return TryGetTalent(talentTag, out var definition) ? definition.Description : "효과 없음";
        }

        public BattleStatBlock ApplyTalent(BattleStatBlock stat, string talentTag)
        {
            if (!TryGetTalent(talentTag, out var talent))
            {
                return stat;
            }

            stat.MaxHp = Scale(stat.MaxHp, talent.HpPct, 1);
            stat.Stamina = Scale(stat.Stamina, talent.StaminaPct, 0);
            stat.Agility = Scale(stat.Agility, talent.AgilityPct, 0);
            stat.Intelligence = Scale(stat.Intelligence, talent.IntelligencePct, 0);
            stat.Strength = Scale(stat.Strength, talent.StrengthPct, 0);
            stat.Attack = Scale(stat.Attack, talent.AttackPct, 1);
            stat.Defense = Scale(stat.Defense, talent.DefensePct, 0);
            return stat;
        }

        private static Dictionary<string, TalentDefinition> BuildTalents(CsvTable table)
        {
            var map = new Dictionary<string, TalentDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var talentTag = Get(row, "talentTag");
                if (string.IsNullOrWhiteSpace(talentTag))
                {
                    continue;
                }

                map[talentTag] = new TalentDefinition(
                    talentTag,
                    Get(row, "name"),
                    Get(row, "description"),
                    ParseFloat(Get(row, "hpPct")),
                    ParseFloat(Get(row, "staminaPct")),
                    ParseFloat(Get(row, "agilityPct")),
                    ParseFloat(Get(row, "intelligencePct")),
                    ParseFloat(Get(row, "strengthPct")),
                    ParseFloat(Get(row, "attackPct")),
                    ParseFloat(Get(row, "defensePct")));
            }

            return map;
        }

        private static Dictionary<string, List<(string talentTag, int weight)>> BuildPool(CsvTable table)
        {
            var map = new Dictionary<string, List<(string talentTag, int weight)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var roleTag = Get(row, "roleTag");
                var talentTag = Get(row, "talentTag");
                var weight = Math.Max(1, ParseInt(Get(row, "weight")));
                if (string.IsNullOrWhiteSpace(roleTag) || string.IsNullOrWhiteSpace(talentTag))
                {
                    continue;
                }

                if (!map.TryGetValue(roleTag, out var entries))
                {
                    entries = new List<(string talentTag, int weight)>();
                    map[roleTag] = entries;
                }

                entries.Add((talentTag, weight));
            }

            return map;
        }

        private static int Scale(int value, float pct, int minValue)
        {
            return Mathf.Max(minValue, Mathf.RoundToInt(value * (1f + pct)));
        }

        private static int ParseInt(string raw)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static float ParseFloat(string raw)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var value) ? (value ?? string.Empty).Trim() : string.Empty;
        }
    }
}
