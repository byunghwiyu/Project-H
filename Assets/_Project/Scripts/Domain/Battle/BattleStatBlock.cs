namespace ProjectH.Battle
{
    public struct BattleStatBlock
    {
        // 정수 스탯
        public int MaxHp;
        public int MaxMana;
        public int Stamina;
        public int Agility;
        public int Intelligence;
        public int Strength;
        public int Attack;
        public int Defense;
        public int HpRegen;
        public int ThornPhysical;
        public int ThornMagical;

        // 퍼센트 스탯
        public float Evasion;
        public float CritChance;
        public float CritDamage;
        public float LifeSteal;
        public float Counter;
        public float ExpGain;
        public float HealPower;

        // 타입
        public string AttackRangeType; // "melee" / "ranged"
        public string DamageType;      // "physical" / "magic" / "chaos"
    }
}
