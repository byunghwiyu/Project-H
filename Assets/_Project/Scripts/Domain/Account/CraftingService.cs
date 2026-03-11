using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Account
{
    public static class CraftingService
    {
        [Serializable]
        public sealed class CraftJobRecord
        {
            public string jobId;
            public string recipeId;
            public long completeUnix;
        }

        [Serializable]
        private sealed class CraftingSaveData
        {
            public List<CraftJobRecord> jobs = new List<CraftJobRecord>();
        }

        private const string SaveKey = "ProjectH.Crafting";
        private static bool initialized;
        private static CraftingSaveData data;

        public static IReadOnlyList<CraftJobRecord> GetJobs()
        {
            EnsureLoaded();
            return data.jobs.Select(CloneJob).OrderBy(x => x.completeUnix).ToList();
        }

        public static long GetRemainingSeconds(string jobId)
        {
            EnsureLoaded();
            var job = data.jobs.FirstOrDefault(x => x.jobId == jobId);
            if (job == null)
            {
                return -1;
            }

            return job.completeUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static bool CanStartCraft(RecipeRow recipe, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(recipe.RecipeId))
            {
                error = "Invalid recipe.";
                return false;
            }

            if (PlayerAccountService.Credits < recipe.CostCredits)
            {
                error = "Not enough credits.";
                return false;
            }

            foreach (var requirement in recipe.GetMaterialRequirements())
            {
                if (InventoryService.GetMaterialAmount(requirement.ItemId) < requirement.Amount)
                {
                    error = $"Missing material: {requirement.ItemId}";
                    return false;
                }
            }

            return true;
        }

        public static bool TryStartCraft(RecipeRow recipe, out string error)
        {
            EnsureLoaded();
            if (!CanStartCraft(recipe, out error))
            {
                return false;
            }

            if (!PlayerAccountService.TrySpendCredits(recipe.CostCredits))
            {
                error = "Failed to spend credits.";
                return false;
            }

            foreach (var requirement in recipe.GetMaterialRequirements())
            {
                if (!InventoryService.TryConsumeMaterial(requirement.ItemId, requirement.Amount))
                {
                    PlayerAccountService.AddCredits(recipe.CostCredits);
                    error = $"Failed to consume material: {requirement.ItemId}";
                    return false;
                }
            }

            data.jobs.Add(new CraftJobRecord
            {
                jobId = GenerateJobId(),
                recipeId = recipe.RecipeId,
                completeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0, recipe.CraftSeconds),
            });
            Save();
            error = string.Empty;
            return true;
        }

        public static bool TryCompleteCraft(string jobId, GameCsvTables tables, out string equipmentId, out string error)
        {
            EnsureLoaded();
            equipmentId = string.Empty;
            error = string.Empty;

            if (tables == null)
            {
                error = "Missing tables.";
                return false;
            }

            var job = data.jobs.FirstOrDefault(x => x.jobId == jobId);
            if (job == null)
            {
                error = "Craft job not found.";
                return false;
            }

            if (GetRemainingSeconds(jobId) > 0)
            {
                error = "Craft is not finished yet.";
                return false;
            }

            var recipe = tables.GetRecipes().FirstOrDefault(x => x.RecipeId == job.recipeId);
            if (string.IsNullOrWhiteSpace(recipe.RecipeId))
            {
                error = $"Recipe not found: {job.recipeId}";
                return false;
            }

            equipmentId = InventoryService.AddEquipment(recipe.RecipeId, BuildCraftedItemName(recipe), recipe.ResultEquipType, recipe.ResultGrade, recipe.StatValue);
            data.jobs.Remove(job);
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
                data = new CraftingSaveData();
                Save();
                return;
            }

            data = JsonUtility.FromJson<CraftingSaveData>(raw) ?? new CraftingSaveData();
            data.jobs ??= new List<CraftJobRecord>();
            data.jobs.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.jobId) || string.IsNullOrWhiteSpace(x.recipeId));
        }

        private static string BuildCraftedItemName(RecipeRow recipe)
        {
            var slotName = recipe.ResultEquipType switch
            {
                "weapon" => "Crafted Weapon",
                "armor" => "Crafted Armor",
                "accessory" => "Crafted Accessory",
                "extra" => "Crafted Extra",
                _ => "Crafted Item",
            };

            return $"{slotName} G{recipe.ResultGrade}";
        }

        private static CraftJobRecord CloneJob(CraftJobRecord source)
        {
            return new CraftJobRecord
            {
                jobId = source.jobId,
                recipeId = source.recipeId,
                completeUnix = source.completeUnix,
            };
        }

        private static string GenerateJobId()
        {
            return $"craft_{Guid.NewGuid():N}";
        }

        private static void Save()
        {
            var raw = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}