using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectH.Account
{
    public static class PlayerAccountService
    {
        [Serializable]
        private sealed class SaveData
        {
            public List<string> ownedTemplateIds = new List<string>();
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
                return data.ownedTemplateIds;
            }
        }

        public static void Recruit(string templateId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return;
            }

            if (!data.ownedTemplateIds.Contains(templateId))
            {
                data.ownedTemplateIds.Add(templateId);
                Save();
            }
        }

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
            data.ownedTemplateIds ??= new List<string>();
            data.dispatchTemplateIds ??= new List<string>();
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}
