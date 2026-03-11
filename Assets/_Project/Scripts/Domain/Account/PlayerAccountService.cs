using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Account
{
    public static class PlayerAccountService
    {
        [Serializable]
        public sealed class MercenaryRecord
        {
            public string mercenaryId;
            public string templateId;
            public string talentTag = "NONE";
            public int level = 1;
            public int exp = 0;
            public string promotionTargetId = "";
            public long promotionStartUnix = 0;
            public int promotionDurationSeconds = 0;
            public string equippedWeaponId = "";
            public string equippedArmorId = "";
            public string equippedAccessoryId = "";
            public string equippedExtraId = "";
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<string> ownedTemplateIds = new List<string>();
            public List<MercenaryRecord> mercenaryRecords = new List<MercenaryRecord>();
            public string dispatchLocationId;
            public int dispatchWaveIndex;
            public List<string> dispatchTemplateIds = new List<string>();
            public List<string> dispatchMercenaryIds = new List<string>();
            public int credits;
            public bool creditsInitialized;
            public int officeLevel = 1;
        }

        private const string SaveKey = "ProjectH.PlayerAccount";
        private static bool initialized;
        private static SaveData data;

        public static int Credits
        {
            get
            {
                EnsureLoaded();
                return data.credits;
            }
        }

        public static IReadOnlyList<MercenaryRecord> GetOwnedMercenaries()
        {
            EnsureLoaded();
            return data.mercenaryRecords.ToList();
        }

        public static MercenaryRecord GetRecordByMercenaryId(string mercenaryId)
        {
            EnsureLoaded();
            return data.mercenaryRecords.FirstOrDefault(r => r.mercenaryId == mercenaryId);
        }

        public static void Recruit(string templateId, string talentTag)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return;
            }

            data.mercenaryRecords.Add(new MercenaryRecord
            {
                mercenaryId = GenerateMercenaryId(),
                templateId = templateId.Trim(),
                talentTag = NormalizeTalentTag(talentTag),
            });
            Save();
        }

        public static int GetLevel(string mercenaryId)
        {
            var record = GetRecordByMercenaryId(mercenaryId);
            return record != null ? record.level : 1;
        }

        public static int GetExp(string mercenaryId)
        {
            var record = GetRecordByMercenaryId(mercenaryId);
            return record != null ? record.exp : 0;
        }

        public static List<(int oldLevel, int newLevel)> AddExp(string mercenaryId, int amount, GameCsvTables tables)
        {
            EnsureLoaded();

            var levelUps = new List<(int oldLevel, int newLevel)>();
            if (amount <= 0 || tables == null)
            {
                return levelUps;
            }

            var record = GetRecordByMercenaryId(mercenaryId);
            if (record == null)
            {
                return levelUps;
            }

            record.exp += amount;
            var maxLevel = tables.GetMaxLevel();

            while (record.level < maxLevel)
            {
                var expNeeded = tables.GetExpToNextLevel(record.level);
                if (record.exp < expNeeded)
                {
                    break;
                }

                record.exp -= expNeeded;
                var oldLevel = record.level;
                record.level++;
                levelUps.Add((oldLevel, record.level));
            }

            Save();
            return levelUps;
        }

        public static bool IsPromoting(string mercenaryId)
        {
            var record = GetRecordByMercenaryId(mercenaryId);
            return record != null && !string.IsNullOrWhiteSpace(record.promotionTargetId);
        }

        public static long GetPromotionRemainingSeconds(string mercenaryId)
        {
            var record = GetRecordByMercenaryId(mercenaryId);
            if (record == null || string.IsNullOrWhiteSpace(record.promotionTargetId))
            {
                return -1;
            }

            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - record.promotionStartUnix;
            return record.promotionDurationSeconds - elapsed;
        }

        public static bool StartPromotion(string mercenaryId, string targetTemplateId, int durationSeconds, int costCredits)
        {
            EnsureLoaded();

            var record = GetRecordByMercenaryId(mercenaryId);
            if (record == null || !string.IsNullOrWhiteSpace(record.promotionTargetId))
            {
                return false;
            }

            if (data.credits < costCredits)
            {
                return false;
            }

            data.credits -= costCredits;
            record.promotionTargetId = targetTemplateId ?? string.Empty;
            record.promotionStartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            record.promotionDurationSeconds = Math.Max(0, durationSeconds);
            Save();
            return true;
        }

        public static bool TryCompletePromotion(string mercenaryId, out string newTemplateId)
        {
            EnsureLoaded();

            newTemplateId = string.Empty;
            var record = GetRecordByMercenaryId(mercenaryId);
            if (record == null || string.IsNullOrWhiteSpace(record.promotionTargetId))
            {
                return false;
            }

            if (GetPromotionRemainingSeconds(mercenaryId) > 0)
            {
                return false;
            }

            newTemplateId = record.promotionTargetId;
            record.templateId = newTemplateId;
            record.promotionTargetId = string.Empty;
            record.promotionStartUnix = 0;
            record.promotionDurationSeconds = 0;
            Save();
            return true;
        }

        public static void AddCredits(int amount)
        {
            EnsureLoaded();
            if (amount <= 0)
            {
                return;
            }

            data.credits += amount;
            Save();
        }

        public static bool TrySpendCredits(int amount)
        {
            EnsureLoaded();
            if (amount < 0 || data.credits < amount)
            {
                return false;
            }

            data.credits -= amount;
            Save();
            return true;
        }

        public static void SetDispatch(string locationId, int waveIndex, List<string> mercenaryIds)
        {
            EnsureLoaded();
            data.dispatchLocationId = locationId ?? string.Empty;
            data.dispatchWaveIndex = Math.Max(1, waveIndex);
            data.dispatchMercenaryIds = mercenaryIds == null ? new List<string>() : new List<string>(mercenaryIds);
            data.dispatchTemplateIds.Clear();
            Save();
        }

        public static bool TryGetDispatch(out string locationId, out int waveIndex, out List<string> mercenaryIds)
        {
            EnsureLoaded();
            locationId = data.dispatchLocationId ?? string.Empty;
            waveIndex = Math.Max(1, data.dispatchWaveIndex);
            mercenaryIds = new List<string>(data.dispatchMercenaryIds ?? new List<string>());
            return !string.IsNullOrWhiteSpace(locationId) && mercenaryIds.Count > 0;
        }

        public static void ClearDispatch()
        {
            EnsureLoaded();
            data.dispatchLocationId = string.Empty;
            data.dispatchWaveIndex = 1;
            data.dispatchMercenaryIds.Clear();
            data.dispatchTemplateIds.Clear();
            Save();
        }

        public static int GetOfficeLevel()
        {
            EnsureLoaded();
            return Math.Max(1, data.officeLevel);
        }

        public static bool TryUpgradeOffice(GameCsvTables tables, out string error)
        {
            EnsureLoaded();
            error = string.Empty;

            var currentLevel = GetOfficeLevel();
            if (!tables.TryGetOfficeLevelRow(currentLevel, out var currentRow))
            {
                error = "Current office level data not found.";
                return false;
            }

            if (currentRow.IsMaxLevel)
            {
                error = "Already at max office level.";
                return false;
            }

            if (!tables.TryGetOfficeLevelRow(currentLevel + 1, out _))
            {
                error = "Next office level data not found.";
                return false;
            }

            if (data.credits < currentRow.UpgradeCostCredits)
            {
                error = $"Not enough credits. Need {currentRow.UpgradeCostCredits}C.";
                return false;
            }

            data.credits -= currentRow.UpgradeCostCredits;
            data.officeLevel = currentLevel + 1;
            Save();
            return true;
        }

        public static bool IsSupportedEquipType(string equipType)
        {
            switch (NormalizeEquipType(equipType))
            {
                case "weapon":
                case "armor":
                case "accessory":
                case "extra":
                    return true;
                default:
                    return false;
            }
        }

        public static string GetEquippedItemId(string mercenaryId, string equipType)
        {
            var record = GetRecordByMercenaryId(mercenaryId);
            return GetEquippedItemId(record, equipType);
        }

        internal static bool TryAssignEquipmentToSlot(string mercenaryId, string equipType, string equipmentId, out string replacedEquipmentId)
        {
            EnsureLoaded();
            replacedEquipmentId = string.Empty;

            var record = GetRecordByMercenaryId(mercenaryId);
            var normalizedType = NormalizeEquipType(equipType);
            if (record == null || !IsSupportedEquipType(normalizedType))
            {
                return false;
            }

            replacedEquipmentId = GetEquippedItemId(record, normalizedType);
            if (string.Equals(replacedEquipmentId, equipmentId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            SetEquippedItemId(record, normalizedType, equipmentId);
            Save();
            return true;
        }

        internal static bool TryClearEquipmentReference(string equipmentId, out string mercenaryId, out string equipType)
        {
            EnsureLoaded();
            mercenaryId = string.Empty;
            equipType = string.Empty;

            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                return false;
            }

            foreach (var record in data.mercenaryRecords)
            {
                if (record == null)
                {
                    continue;
                }

                if (string.Equals(record.equippedWeaponId, equipmentId, StringComparison.OrdinalIgnoreCase))
                {
                    record.equippedWeaponId = string.Empty;
                    mercenaryId = record.mercenaryId;
                    equipType = "weapon";
                    Save();
                    return true;
                }

                if (string.Equals(record.equippedArmorId, equipmentId, StringComparison.OrdinalIgnoreCase))
                {
                    record.equippedArmorId = string.Empty;
                    mercenaryId = record.mercenaryId;
                    equipType = "armor";
                    Save();
                    return true;
                }

                if (string.Equals(record.equippedAccessoryId, equipmentId, StringComparison.OrdinalIgnoreCase))
                {
                    record.equippedAccessoryId = string.Empty;
                    mercenaryId = record.mercenaryId;
                    equipType = "accessory";
                    Save();
                    return true;
                }

                if (string.Equals(record.equippedExtraId, equipmentId, StringComparison.OrdinalIgnoreCase))
                {
                    record.equippedExtraId = string.Empty;
                    mercenaryId = record.mercenaryId;
                    equipType = "extra";
                    Save();
                    return true;
                }
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            var raw = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                data = new SaveData();
                InitCredits();
                return;
            }

            data = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData();
            data.ownedTemplateIds ??= new List<string>();
            data.mercenaryRecords ??= new List<MercenaryRecord>();
            data.dispatchTemplateIds ??= new List<string>();
            data.dispatchMercenaryIds ??= new List<string>();

            var mutated = false;

            if (data.mercenaryRecords.Count == 0 && data.ownedTemplateIds.Count > 0)
            {
                foreach (var templateId in data.ownedTemplateIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    data.mercenaryRecords.Add(new MercenaryRecord
                    {
                        mercenaryId = GenerateMercenaryId(),
                        templateId = templateId.Trim(),
                        talentTag = "NONE",
                    });
                }

                data.ownedTemplateIds.Clear();
                mutated = true;
            }

            var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in data.mercenaryRecords)
            {
                if (string.IsNullOrWhiteSpace(record.mercenaryId) || !knownIds.Add(record.mercenaryId))
                {
                    record.mercenaryId = GenerateMercenaryId();
                    knownIds.Add(record.mercenaryId);
                    mutated = true;
                }

                if (string.IsNullOrWhiteSpace(record.talentTag))
                {
                    record.talentTag = "NONE";
                    mutated = true;
                }

                record.equippedWeaponId ??= string.Empty;
                record.equippedArmorId ??= string.Empty;
                record.equippedAccessoryId ??= string.Empty;
                record.equippedExtraId ??= string.Empty;
            }

            if (data.dispatchMercenaryIds.Count == 0 && data.dispatchTemplateIds.Count > 0)
            {
                foreach (var templateId in data.dispatchTemplateIds)
                {
                    var record = data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
                    if (record != null)
                    {
                        data.dispatchMercenaryIds.Add(record.mercenaryId);
                    }
                }

                data.dispatchTemplateIds.Clear();
                mutated = true;
            }

            if (!data.creditsInitialized)
            {
                InitCredits();
                return;
            }

            if (mutated)
            {
                Save();
            }
        }

        private static void InitCredits()
        {
            data.credits = 500;
            data.creditsInitialized = true;
            Save();
        }

        private static string GenerateMercenaryId()
        {
            return $"merc_{Guid.NewGuid():N}";
        }

        private static string GetEquippedItemId(MercenaryRecord record, string equipType)
        {
            if (record == null)
            {
                return string.Empty;
            }

            switch (NormalizeEquipType(equipType))
            {
                case "weapon":
                    return record.equippedWeaponId ?? string.Empty;
                case "armor":
                    return record.equippedArmorId ?? string.Empty;
                case "accessory":
                    return record.equippedAccessoryId ?? string.Empty;
                case "extra":
                    return record.equippedExtraId ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static void SetEquippedItemId(MercenaryRecord record, string equipType, string equipmentId)
        {
            if (record == null)
            {
                return;
            }

            var value = equipmentId ?? string.Empty;
            switch (NormalizeEquipType(equipType))
            {
                case "weapon":
                    record.equippedWeaponId = value;
                    break;
                case "armor":
                    record.equippedArmorId = value;
                    break;
                case "accessory":
                    record.equippedAccessoryId = value;
                    break;
                case "extra":
                    record.equippedExtraId = value;
                    break;
            }
        }

        private static string NormalizeEquipType(string equipType)
        {
            return string.IsNullOrWhiteSpace(equipType) ? string.Empty : equipType.Trim().ToLowerInvariant();
        }

        private static string NormalizeTalentTag(string talentTag)
        {
            return string.IsNullOrWhiteSpace(talentTag) ? "NONE" : talentTag.Trim();
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}