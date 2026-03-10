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
            // 승급 타이머 (비어 있으면 승급 중 아님)
            public string promotionTargetId = "";
            public long promotionStartUnix = 0;
            public int promotionDurationSeconds = 0;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<string> ownedTemplateIds = new List<string>(); // 레거시
            public List<MercenaryRecord> mercenaryRecords = new List<MercenaryRecord>();
            public string dispatchLocationId;
            public int dispatchWaveIndex;
            public List<string> dispatchTemplateIds = new List<string>();
            public int credits = 0;
            public bool creditsInitialized = false;
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

        public static int Credits
        {
            get { EnsureLoaded(); return data.credits; }
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

            data.mercenaryRecords.Add(new MercenaryRecord { templateId = templateId });
            Save();
        }

        public static MercenaryRecord GetRecord(string templateId)
        {
            EnsureLoaded();
            return data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
        }

        public static int GetLevel(string templateId)
        {
            var r = GetRecord(templateId);
            return r != null ? r.level : 1;
        }

        public static int GetExp(string templateId)
        {
            var r = GetRecord(templateId);
            return r != null ? r.exp : 0;
        }

        // ────────────────────── 경험치 & 레벨업 ──────────────────────

        public static List<(int oldLevel, int newLevel)> AddExp(
            string templateId, int amount, GameCsvTables tables)
        {
            EnsureLoaded();
            var levelUps = new List<(int, int)>();
            if (amount <= 0 || tables == null) return levelUps;

            var record = data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
            if (record == null) return levelUps;

            record.exp += amount;
            var maxLevel = tables.GetMaxLevel();

            while (record.level < maxLevel)
            {
                var expNeeded = tables.GetExpToNextLevel(record.level);
                if (record.exp < expNeeded) break;
                record.exp -= expNeeded;
                var old = record.level;
                record.level++;
                levelUps.Add((old, record.level));
            }

            Save();
            return levelUps;
        }

        // ────────────────────── 승급 ──────────────────────

        public static bool IsPromoting(string templateId)
        {
            var r = GetRecord(templateId);
            return r != null && !string.IsNullOrEmpty(r.promotionTargetId);
        }

        /// <summary>승급 완료까지 남은 초. 0 이하면 완료 가능.</summary>
        public static long GetPromotionRemainingSeconds(string templateId)
        {
            var r = GetRecord(templateId);
            if (r == null || string.IsNullOrEmpty(r.promotionTargetId)) return -1;
            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - r.promotionStartUnix;
            return r.promotionDurationSeconds - elapsed;
        }

        /// <summary>
        /// 승급을 시작합니다.
        /// 레벨/크레딧 요건이 충족된 경우에만 호출하세요.
        /// </summary>
        public static bool StartPromotion(
            string templateId, string targetTemplateId, int durationSeconds, int costCredits)
        {
            EnsureLoaded();
            var record = data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
            if (record == null) return false;
            if (!string.IsNullOrEmpty(record.promotionTargetId)) return false; // 이미 진행 중
            if (data.credits < costCredits) return false;

            data.credits -= costCredits;
            record.promotionTargetId = targetTemplateId;
            record.promotionStartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            record.promotionDurationSeconds = durationSeconds;
            Save();
            return true;
        }

        /// <summary>
        /// 승급 완료를 확정합니다. 타이머가 끝난 경우에만 templateId가 변경됩니다.
        /// </summary>
        /// <returns>true면 성공적으로 변환됨</returns>
        public static bool TryCompletePromotion(string templateId, out string newTemplateId)
        {
            EnsureLoaded();
            newTemplateId = string.Empty;
            var record = data.mercenaryRecords.FirstOrDefault(r => r.templateId == templateId);
            if (record == null || string.IsNullOrEmpty(record.promotionTargetId)) return false;
            if (GetPromotionRemainingSeconds(templateId) > 0) return false;

            newTemplateId = record.promotionTargetId;
            record.templateId = newTemplateId;
            record.promotionTargetId = string.Empty;
            record.promotionStartUnix = 0;
            record.promotionDurationSeconds = 0;
            Save();
            return true;
        }

        // ────────────────────── 크레딧 ──────────────────────

        public static void AddCredits(int amount)
        {
            EnsureLoaded();
            if (amount <= 0) return;
            data.credits += amount;
            Save();
        }

        public static bool TrySpendCredits(int amount)
        {
            EnsureLoaded();
            if (data.credits < amount) return false;
            data.credits -= amount;
            Save();
            return true;
        }

        // ────────────────────── 파견 ──────────────────────

        public static void SetDispatch(string locationId, int waveIndex, List<string> allyTemplateIds)
        {
            EnsureLoaded();
            data.dispatchLocationId = locationId ?? string.Empty;
            data.dispatchWaveIndex = Math.Max(1, waveIndex);
            data.dispatchTemplateIds = allyTemplateIds == null
                ? new List<string>()
                : new List<string>(allyTemplateIds);
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
            if (initialized) return;
            initialized = true;

            var raw = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                data = new SaveData();
                InitCredits();
                return;
            }

            data = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData();
            data.ownedTemplateIds    ??= new List<string>();
            data.mercenaryRecords    ??= new List<MercenaryRecord>();
            data.dispatchTemplateIds ??= new List<string>();

            // 레거시 마이그레이션
            if (data.mercenaryRecords.Count == 0 && data.ownedTemplateIds.Count > 0)
            {
                foreach (var id in data.ownedTemplateIds)
                {
                    data.mercenaryRecords.Add(new MercenaryRecord { templateId = id });
                }
                data.ownedTemplateIds.Clear();
                Save();
            }

            if (!data.creditsInitialized)
            {
                InitCredits();
            }
        }

        private static void InitCredits()
        {
            // define_table의 startingCredits 값을 사용하되,
            // 이 시점에서 tables를 로드할 수 없으므로 기본값 500 사용.
            // Phase 4-1에서 tables 기반으로 개선 가능.
            data.credits = 500;
            data.creditsInitialized = true;
            Save();
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}
