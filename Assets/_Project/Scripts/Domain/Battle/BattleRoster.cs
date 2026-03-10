using System.Collections.Generic;
using System.Linq;

namespace ProjectH.Battle
{
    public sealed class BattleRoster
    {
        private readonly List<BattleUnit> allies = new();
        private readonly List<BattleUnit> enemies = new();
        private readonly Dictionary<string, BattleUnit> byRuntimeId = new();

        public IReadOnlyList<BattleUnit> Allies => allies;
        public IReadOnlyList<BattleUnit> Enemies => enemies;

        public void Add(BattleUnit unit)
        {
            if (unit.Team == BattleTeam.Ally)
            {
                allies.Add(unit);
            }
            else
            {
                enemies.Add(unit);
            }

            byRuntimeId[unit.RuntimeUnitId] = unit;
        }

        public bool TryGetAlive(string runtimeUnitId, out BattleUnit unit)
        {
            if (byRuntimeId.TryGetValue(runtimeUnitId, out unit) && unit.IsAlive)
            {
                return true;
            }

            unit = null;
            return false;
        }

        public bool HasAlive(BattleTeam team)
        {
            var source = team == BattleTeam.Ally ? allies : enemies;
            return source.Any(x => x.IsAlive);
        }

        /// <summary>사망한 적 유닛을 roster에서 제거하고 (runtimeId, templateId) 목록을 반환합니다.</summary>
        public List<(string runtimeId, string templateId)> ExtractKilledEnemies()
        {
            var killed = new List<(string, string)>();
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (!e.IsAlive)
                {
                    killed.Add((e.RuntimeUnitId, e.TemplateId));
                    byRuntimeId.Remove(e.RuntimeUnitId);
                    enemies.RemoveAt(i);
                }
            }

            return killed;
        }

        public BattleUnit NextAlive(BattleTeam team, int startIndex)
        {
            var source = team == BattleTeam.Ally ? allies : enemies;
            if (source.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var idx = (startIndex + i) % source.Count;
                if (source[idx].IsAlive)
                {
                    return source[idx];
                }
            }

            return null;
        }
    }
}
