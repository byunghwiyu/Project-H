using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Account
{
    public static class InventoryService
    {
        [Serializable]
        public sealed class MaterialRecord
        {
            public string itemId;
            public int amount;
        }

        [Serializable]
        public sealed class EquipmentRecord
        {
            public string equipmentId;
            public string itemId;
            public string itemName;
            public string equipType;
            public int grade;
            public int statValue;
            public string ownerMercenaryId;
        }

        [Serializable]
        private sealed class InventorySaveData
        {
            public List<MaterialRecord> materials = new List<MaterialRecord>();
            public List<EquipmentRecord> equipments = new List<EquipmentRecord>();
        }

        private const string SaveKey = "ProjectH.Inventory";
        private static bool initialized;
        private static InventorySaveData data;

        public static IReadOnlyList<MaterialRecord> GetAllMaterials()
        {
            EnsureLoaded();
            return data.materials.Select(CloneMaterial).ToList();
        }

        public static IReadOnlyList<EquipmentRecord> GetAllEquipments()
        {
            EnsureLoaded();
            return data.equipments.Select(CloneEquipment).ToList();
        }

        public static IReadOnlyList<EquipmentRecord> GetEquipmentsByOwner(string mercenaryId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(mercenaryId))
            {
                return Array.Empty<EquipmentRecord>();
            }

            var ownerId = mercenaryId.Trim();
            return data.equipments
                .Where(x => string.Equals(x.ownerMercenaryId, ownerId, StringComparison.OrdinalIgnoreCase))
                .Select(CloneEquipment)
                .ToList();
        }

        public static IReadOnlyList<EquipmentRecord> GetUnequippedEquipments()
        {
            EnsureLoaded();
            return data.equipments
                .Where(x => string.IsNullOrWhiteSpace(x.ownerMercenaryId))
                .Select(CloneEquipment)
                .ToList();
        }

        public static IReadOnlyList<EquipmentRecord> GetUnequippedEquipments(string equipType)
        {
            EnsureLoaded();
            var normalizedType = NormalizeEquipType(equipType);
            return data.equipments
                .Where(x =>
                    string.IsNullOrWhiteSpace(x.ownerMercenaryId) &&
                    string.Equals(NormalizeEquipType(x.equipType), normalizedType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.grade)
                .ThenByDescending(x => x.statValue)
                .ThenBy(x => x.itemName)
                .Select(CloneEquipment)
                .ToList();
        }

        public static bool TryGetEquipment(string equipmentId, out EquipmentRecord record)
        {
            EnsureLoaded();
            record = null;
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                return false;
            }

            var found = data.equipments.FirstOrDefault(x => x.equipmentId == equipmentId.Trim());
            if (found == null)
            {
                return false;
            }

            record = CloneEquipment(found);
            return true;
        }

        public static int GetMaterialAmount(string itemId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            var record = data.materials.FirstOrDefault(x => x.itemId == itemId.Trim());
            return record != null ? record.amount : 0;
        }

        public static int GetTotalMaterialCount()
        {
            EnsureLoaded();
            return data.materials.Sum(x => Math.Max(0, x.amount));
        }

        public static void AddMaterial(string itemId, int amount)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            var normalizedItemId = itemId.Trim();
            var record = data.materials.FirstOrDefault(x => x.itemId == normalizedItemId);
            if (record == null)
            {
                data.materials.Add(new MaterialRecord
                {
                    itemId = normalizedItemId,
                    amount = amount,
                });
            }
            else
            {
                record.amount += amount;
            }

            Save();
        }

        public static bool TryConsumeMaterial(string itemId, int amount)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return false;
            }

            var normalizedItemId = itemId.Trim();
            var record = data.materials.FirstOrDefault(x => x.itemId == normalizedItemId);
            if (record == null || record.amount < amount)
            {
                return false;
            }

            record.amount -= amount;
            if (record.amount <= 0)
            {
                data.materials.Remove(record);
            }

            Save();
            return true;
        }

        public static bool TryConsumeAnyMaterials(int amount)
        {
            EnsureLoaded();
            if (amount <= 0)
            {
                return true;
            }

            if (GetTotalMaterialCount() < amount)
            {
                return false;
            }

            var remaining = amount;
            foreach (var material in data.materials.OrderBy(x => x.itemId).ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }

                var used = Math.Min(material.amount, remaining);
                material.amount -= used;
                remaining -= used;
            }

            data.materials.RemoveAll(x => x == null || x.amount <= 0);
            Save();
            return true;
        }

        public static string AddEquipment(string itemId, string itemName, string equipType, int grade, int statValue)
        {
            EnsureLoaded();
            var record = new EquipmentRecord
            {
                equipmentId = GenerateEquipmentId(),
                itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim(),
                itemName = string.IsNullOrWhiteSpace(itemName) ? itemId : itemName.Trim(),
                equipType = NormalizeEquipType(equipType),
                grade = Math.Max(0, grade),
                statValue = statValue,
                ownerMercenaryId = string.Empty,
            };

            data.equipments.Add(record);
            Save();
            return record.equipmentId;
        }

        public static void AddDrop(DropRow drop)
        {
            if (drop.IsEquipment)
            {
                AddEquipment(drop.ItemId, drop.ItemName, drop.EquipType, drop.Grade, drop.StatValue);
                return;
            }

            AddMaterial(drop.ItemId, 1);
        }

        public static bool TryEquip(string equipmentId, string mercenaryId, out string error)
        {
            EnsureLoaded();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(equipmentId) || string.IsNullOrWhiteSpace(mercenaryId))
            {
                error = "Missing equipment or mercenary id.";
                return false;
            }

            var record = data.equipments.FirstOrDefault(x => x.equipmentId == equipmentId.Trim());
            if (record == null)
            {
                error = $"Equipment not found: {equipmentId}";
                return false;
            }

            if (PlayerAccountService.GetRecordByMercenaryId(mercenaryId) == null)
            {
                error = $"Mercenary not found: {mercenaryId}";
                return false;
            }

            if (!PlayerAccountService.IsSupportedEquipType(record.equipType))
            {
                error = $"Unsupported equip type: {record.equipType}";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(record.ownerMercenaryId) &&
                !string.Equals(record.ownerMercenaryId, mercenaryId, StringComparison.OrdinalIgnoreCase))
            {
                PlayerAccountService.TryClearEquipmentReference(record.equipmentId, out _, out _);
            }

            if (!PlayerAccountService.TryAssignEquipmentToSlot(mercenaryId, record.equipType, record.equipmentId, out var replacedEquipmentId))
            {
                error = "Failed to assign equipment slot.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(replacedEquipmentId) && replacedEquipmentId != record.equipmentId)
            {
                var replaced = data.equipments.FirstOrDefault(x => x.equipmentId == replacedEquipmentId);
                if (replaced != null)
                {
                    replaced.ownerMercenaryId = string.Empty;
                }
            }

            record.ownerMercenaryId = mercenaryId.Trim();
            Save();
            return true;
        }

        public static bool TryUnequip(string equipmentId, out string error)
        {
            EnsureLoaded();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                error = "Missing equipment id.";
                return false;
            }

            var record = data.equipments.FirstOrDefault(x => x.equipmentId == equipmentId.Trim());
            if (record == null)
            {
                error = $"Equipment not found: {equipmentId}";
                return false;
            }

            if (!PlayerAccountService.TryClearEquipmentReference(record.equipmentId, out _, out _))
            {
                error = "Equipment is not currently equipped.";
                return false;
            }

            record.ownerMercenaryId = string.Empty;
            Save();
            return true;
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
                data = new InventorySaveData();
                Save();
                return;
            }

            data = JsonUtility.FromJson<InventorySaveData>(raw) ?? new InventorySaveData();
            data.materials ??= new List<MaterialRecord>();
            data.equipments ??= new List<EquipmentRecord>();

            var mutated = false;
            foreach (var material in data.materials)
            {
                if (material == null)
                {
                    continue;
                }

                material.itemId = string.IsNullOrWhiteSpace(material.itemId) ? string.Empty : material.itemId.Trim();
                material.amount = Math.Max(0, material.amount);
            }

            var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var equipment in data.equipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(equipment.equipmentId) || !knownIds.Add(equipment.equipmentId))
                {
                    equipment.equipmentId = GenerateEquipmentId();
                    knownIds.Add(equipment.equipmentId);
                    mutated = true;
                }

                equipment.itemId = string.IsNullOrWhiteSpace(equipment.itemId) ? string.Empty : equipment.itemId.Trim();
                equipment.itemName = string.IsNullOrWhiteSpace(equipment.itemName) ? equipment.itemId : equipment.itemName.Trim();
                equipment.equipType = NormalizeEquipType(equipment.equipType);
                equipment.ownerMercenaryId = string.IsNullOrWhiteSpace(equipment.ownerMercenaryId) ? string.Empty : equipment.ownerMercenaryId.Trim();
                equipment.grade = Math.Max(0, equipment.grade);
            }

            if (mutated)
            {
                Save();
            }
        }

        private static string GenerateEquipmentId()
        {
            return $"equip_{Guid.NewGuid():N}";
        }

        private static string NormalizeEquipType(string equipType)
        {
            return string.IsNullOrWhiteSpace(equipType) ? string.Empty : equipType.Trim().ToLowerInvariant();
        }

        private static MaterialRecord CloneMaterial(MaterialRecord source)
        {
            return new MaterialRecord
            {
                itemId = source.itemId,
                amount = source.amount,
            };
        }

        private static EquipmentRecord CloneEquipment(EquipmentRecord source)
        {
            return new EquipmentRecord
            {
                equipmentId = source.equipmentId,
                itemId = source.itemId,
                itemName = source.itemName,
                equipType = source.equipType,
                grade = source.grade,
                statValue = source.statValue,
                ownerMercenaryId = source.ownerMercenaryId,
            };
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}