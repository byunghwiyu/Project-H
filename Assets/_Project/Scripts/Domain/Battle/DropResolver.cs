using System;
using System.Collections.Generic;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Battle
{
    public static class DropResolver
    {
        public static List<DropRow> Resolve(
            IEnumerable<string> killedTemplateIds,
            GameCsvTables tables,
            System.Random rng)
        {
            var dropped = new List<DropRow>();
            foreach (var templateId in killedTemplateIds)
            {
                var drops = tables.GetDropRows(templateId);
                foreach (var drop in drops)
                {
                    if (rng.NextDouble() < drop.DropRate)
                    {
                        dropped.Add(drop);
                        Debug.Log($"[drop] {templateId} -> {drop.ItemId} ({drop.ItemName})");
                    }
                }
            }

            return dropped;
        }
    }
}