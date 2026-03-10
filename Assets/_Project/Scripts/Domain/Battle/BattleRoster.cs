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
