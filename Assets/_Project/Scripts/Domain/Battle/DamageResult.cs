namespace ProjectH.Battle
{
    public readonly struct DamageResult
    {
        public DamageResult(int dealt, bool evaded, bool isCrit, int thornDealt, int lifeStealHeal)
        {
            Dealt         = dealt;
            Evaded        = evaded;
            IsCrit        = isCrit;
            ThornDealt    = thornDealt;
            LifeStealHeal = lifeStealHeal;
        }

        public int  Dealt         { get; }
        public bool Evaded        { get; }
        public bool IsCrit        { get; }
        public int  ThornDealt    { get; }  // 가시 피해 - actor에게 반사
        public int  LifeStealHeal { get; }  // 흡수 회복 - actor가 회복
    }
}
