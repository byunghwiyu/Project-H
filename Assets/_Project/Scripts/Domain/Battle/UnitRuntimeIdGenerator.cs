namespace ProjectH.Battle
{
    public static class UnitRuntimeIdGenerator
    {
        private static int allyCounter;
        private static int enemyCounter;

        public static void Reset()
        {
            allyCounter = 0;
            enemyCounter = 0;
        }

        public static string Next(BattleTeam team)
        {
            if (team == BattleTeam.Ally)
            {
                allyCounter++;
                return $"ALLY-{allyCounter}";
            }

            enemyCounter++;
            return $"ENEMY-{enemyCounter}";
        }
    }
}
