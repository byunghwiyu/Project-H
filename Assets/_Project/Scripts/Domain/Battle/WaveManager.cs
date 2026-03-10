using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Battle
{
    public sealed class WaveManager
    {
        private readonly string locationId;
        private readonly GameCsvTables tables;
        private readonly Random rng;
        private int stagesClearedCount;

        public int StagesClearedCount => stagesClearedCount;

        public WaveManager(string locationId, GameCsvTables tables, Random rng)
        {
            this.locationId = locationId;
            this.tables = tables;
            this.rng = rng;
            stagesClearedCount = 0;
        }

        /// <summary>다음 스테이지 타입을 결정합니다. stagesClearedCount는 변경하지 않습니다.</summary>
        public WaveStageType DetermineNextStageType()
        {
            if (!tables.TryGetStageRule(locationId, out var rule))
            {
                return WaveStageType.Battle;
            }

            // 보스 체크 (최우선)
            if (stagesClearedCount > 0 && rule.BossEveryStageClears > 0 &&
                stagesClearedCount % rule.BossEveryStageClears == 0)
            {
                return WaveStageType.Boss;
            }

            // 은닉 체크
            if (stagesClearedCount > 0 && rule.HiddenEveryStageClears > 0 &&
                stagesClearedCount % rule.HiddenEveryStageClears == 0 &&
                rng.NextDouble() < rule.HiddenEnterChance)
            {
                return WaveStageType.Hidden;
            }

            // 일반 / 탐험 가중치 추첨
            var totalWeight = rule.BattleStageWeight + rule.ExploreStageWeight;
            if (totalWeight <= 0f)
            {
                return WaveStageType.Battle;
            }

            var roll = rng.NextDouble() * totalWeight;
            return roll < rule.BattleStageWeight ? WaveStageType.Battle : WaveStageType.Explore;
        }

        /// <summary>stageType에 해당하는 encounter를 가중치 추첨하여 적 templateId 목록(count 반영)을 반환합니다.</summary>
        public List<string> SelectEncounterEnemyIds(WaveStageType stageType)
        {
            var stageTypeStr = stageType switch
            {
                WaveStageType.Boss   => "BOSS",
                WaveStageType.Hidden => "HIDDEN",
                _                    => "BATTLE",
            };

            var rows = tables.GetEncounterRows(locationId, stageTypeStr);
            if (rows.Count == 0)
            {
                Debug.LogWarning($"[WaveManager] No encounters for locationId={locationId} stageType={stageTypeStr}");
                return new List<string>();
            }

            // encounterId별로 그룹화하여 가중치 추첨
            var encounterGroups = rows
                .GroupBy(r => r.EncounterId)
                .Select(g => (id: g.Key, weight: g.First().Weight, rows: g.ToList()))
                .ToList();

            var totalWeight = encounterGroups.Sum(g => g.weight);
            var roll = rng.NextDouble() * totalWeight;
            var acc = 0.0;
            List<EncounterRow> picked = null;

            foreach (var (_, weight, grpRows) in encounterGroups)
            {
                acc += weight;
                if (roll < acc)
                {
                    picked = grpRows;
                    break;
                }
            }

            picked ??= encounterGroups[^1].rows;

            // count 반영하여 templateId 목록 생성
            var result = new List<string>();
            foreach (var r in picked)
            {
                for (var i = 0; i < r.Count; i++)
                {
                    result.Add(r.MonsterTemplateId);
                }
            }

            return result;
        }

        /// <summary>현재 wave 클리어를 기록합니다.</summary>
        public void OnWaveCleared()
        {
            stagesClearedCount++;
            Debug.Log($"[wave] Stage cleared. Total clears: {stagesClearedCount}");
        }
    }
}
