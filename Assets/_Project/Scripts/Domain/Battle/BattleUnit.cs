namespace ProjectH.Battle
{
    public sealed class BattleUnit
    {
        public BattleUnit(string mercenaryId, string templateId, BattleTeam team, BattleStatBlock baseStat)
        {
            RuntimeUnitId = UnitRuntimeIdGenerator.Next(team);
            MercenaryId = mercenaryId ?? string.Empty;
            TemplateId = templateId;
            Team = team;
            BaseStat = baseStat;
            Stat = baseStat;
            Hp = Stat.MaxHp;
            Mana = 0;
        }

        public string RuntimeUnitId { get; }
        public string MercenaryId { get; }
        public string TemplateId { get; }
        public BattleTeam Team { get; }
        public BattleStatBlock BaseStat { get; }
        public BattleStatBlock Stat { get; private set; }
        public int Hp { get; private set; }
        public int Mana { get; private set; }

        public bool IsAlive => Hp > 0;
        public bool IsManaFull => Mana >= Stat.MaxMana;

        public int ApplyDamage(int value)
        {
            if (!IsAlive)
            {
                return 0;
            }

            var damage = value < 0 ? 0 : value;
            Hp -= damage;
            if (Hp < 0)
            {
                Hp = 0;
            }

            return damage;
        }

        public int ApplyHeal(int value)
        {
            if (!IsAlive || value <= 0)
            {
                return 0;
            }

            var before = Hp;
            Hp += value;
            if (Hp > Stat.MaxHp)
            {
                Hp = Stat.MaxHp;
            }

            return Hp - before;
        }

        public void RecoverMana(int amount)
        {
            if (amount <= 0 || Stat.MaxMana <= 0)
            {
                return;
            }

            Mana += amount;
            if (Mana > Stat.MaxMana)
            {
                Mana = Stat.MaxMana;
            }
        }

        public void ConsumeMana()
        {
            Mana = 0;
        }

        public void SetComputedStat(BattleStatBlock computed)
        {
            Stat = computed;
        }
    }
}
