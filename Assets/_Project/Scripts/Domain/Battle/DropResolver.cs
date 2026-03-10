using System;
using System.Collections.Generic;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Battle
{
    public static class DropResolver
    {
        /// <summary>사망한 적 templateId 목록을 기반으로 드랍 아이템 목록을 반환합니다.</summary>
        public static List<string> Resolve(
            IEnumerable<string> killedTemplateIds,
            GameCsvTables tables,
            System.Random rng)
        {
            var dropped = new List<string>();
            foreach (var templateId in killedTemplateIds)
            {
                var drops = tables.GetDropRows(templateId);
                foreach (var drop in drops)
                {
                    if (rng.NextDouble() < drop.DropRate)
                    {
                        dropped.Add(drop.ItemId);
                        Debug.Log($"[drop] {templateId} → {drop.ItemId} ({drop.ItemName})");
                    }
                }
            }

            return dropped;
        }
    }
}
