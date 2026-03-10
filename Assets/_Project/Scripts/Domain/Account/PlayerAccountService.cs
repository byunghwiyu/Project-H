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
            public string templateId;
            public int level = 1;
            public int exp = 0;
        }

        [Serializable]
        private sealed class SaveData
        {
            // 레거시 필드 — 마이그레이션 후 비워짐
            public List<string> ownedTemplateIds = new List<string>();
            // 현재 사용 필드
            public List<MercenaryRecord> mercenaryRecords = new List<MercenaryRecord>();
            public string dispatchLocationId;
            public int dispatchWaveIndex;
            public List<string> dispatchTemplateIds = new List<string>();
        }

        private const string SaveKey = "ProjectH.PlayerAccount";
        private static bool initialized;
        private static SaveData data;

        public static IReadOnlyList<string> OwnedTemplateIds
        {
            get
            {
                EnsureLoaded();
                return data.mercenaryRecords.Select(r => r.templateId).ToList();
            }
        }

        // ────────────────────── 용병 모집/조회 ──────────────────────

        public static void Recruit(string templateId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return;
            }

            if (data.mercenaryRecords.Any(r => r.templateId == templateId))
            {
                return;
            }

            data.mercenaryRecords.Add(new MercenaryRecord { templateId = templateId, level = 1, exp = 0 });
            Save();
        }

        public static MercenaryRecord GetRecord(string templateId)
        {
            EnsureLoaded();
            return data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
        }

        public static int GetLevel(string templateId)
        {
            var record = GetRecord(templateId);
            return record != null ? record.level : 1;
        }

        public static int GetExp(string templateId)
        {
            var record = GetRecord(templateId);
            return record != null ? record.exp : 0;
        }

        // ────────────────────── 경험치 & 레벨업 ──────────────────────

        /// <summary>
        /// templateId 용병에게 경험치를 부여하고 레벨업이 발생하면 (이전레벨, 새레벨) 목록을 반환합니다.
        /// </summary>
        public static List<(int oldLevel, int newLevel)> AddExp(
            string templateId,
            int amount,
            GameCsvTables tables)
        {
            EnsureLoaded();
            var levelUps = new List<(int, int)>();

            if (amount <= 0 || tables == null)
            {
                return levelUps;
            }

            var record = data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
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

        // ────────────────────── 파견 ──────────────────────

        public static void SetDispatch(string locationId, int waveIndex, List<string> allyTemplateIds)
        {
            EnsureLoaded();
            data.dispatchLocationId = locationId ?? string.Empty;
            data.dispatchWaveIndex = Math.Max(1, waveIndex);
            data.dispatchTemplateIds = allyTemplateIds == null ? new List<string>() : new List<string>(allyTemplateIds);
            Save();
        }

        public static bool TryGetDispatch(out string locationId, out int waveIndex, out List<string> allyTemplateIds)
        {
            EnsureLoaded();

            locationId = data.dispatchLocationId ?? string.Empty;
            waveIndex = Math.Max(1, data.dispatchWaveIndex);
            allyTemplateIds = new List<string>(data.dispatchTemplateIds ?? new List<string>());

            return !string.IsNullOrWhiteSpace(locationId) && allyTemplateIds.Count > 0;
        }

        public static void ClearDispatch()
        {
            EnsureLoaded();
            data.dispatchLocationId = string.Empty;
            data.dispatchWaveIndex = 1;
            data.dispatchTemplateIds.Clear();
            Save();
        }

        // ────────────────────── 내부 ──────────────────────

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
                return;
            }

            data = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData();
            data.ownedTemplateIds   ??= new List<string>();
            data.mercenaryRecords   ??= new List<MercenaryRecord>();
            data.dispatchTemplateIds ??= new List<string>();

            // 레거시 데이터 마이그레이션: ownedTemplateIds → mercenaryRecords
            if (data.mercenaryRecords.Count == 0 && data.ownedTemplateIds.Count > 0)
            {
                foreach (var id in data.ownedTemplateIds)
                {
                    data.mercenaryRecords.Add(new MercenaryRecord { templateId = id, level = 1, exp = 0 });
                }

                data.ownedTemplateIds.Clear();
                Save();
            }
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}
