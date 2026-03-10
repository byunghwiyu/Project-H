using System.Collections.Generic;
using System.Linq;

namespace ProjectH.Battle
{
    public sealed class BattleTurnService
    {
        private BattleTeam currentTeam;

        public BattleTeam InitializeFirstTeam(IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies)
        {
            currentTeam = ComputeFirstTeam(allies, enemies);
            return currentTeam;
        }

        /// <summary>현재 팀에서 민첩이 가장 높은 생존 유닛을 반환하고 팀을 교대합니다.</summary>
        public BattleUnit NextActor(BattleRoster roster)
        {
            var actor = HighestAgilityAlive(roster, currentTeam);
            if (actor == null)
            {
                return null;
            }

            currentTeam = currentTeam == BattleTeam.Ally ? BattleTeam.Enemy : BattleTeam.Ally;
            return actor;
        }

        private static BattleTeam ComputeFirstTeam(IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies)
        {
            var allyAvg  = AverageAgility(allies);
            var enemyAvg = AverageAgility(enemies);
            return allyAvg >= enemyAvg ? BattleTeam.Ally : BattleTeam.Enemy;
        }

        private static BattleUnit HighestAgilityAlive(BattleRoster roster, BattleTeam team)
        {
            var source = team == BattleTeam.Ally ? roster.Allies : roster.Enemies;
            BattleUnit best = null;
            foreach (var u in source)
            {
                if (!u.IsAlive)
                {
                    continue;
                }

                if (best == null || u.Stat.Agility > best.Stat.Agility)
                {
                    best = u;
                }
            }

            return best;
        }

        private static float AverageAgility(IReadOnlyList<BattleUnit> units)
        {
            if (units.Count == 0)
            {
                return 0f;
            }

            return (float)units.Average(x => x.Stat.Agility);
        }
    }
}
