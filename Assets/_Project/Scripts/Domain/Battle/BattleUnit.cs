namespace ProjectH.Battle
{
    public sealed class BattleUnit
    {
        public BattleUnit(string templateId, BattleTeam team, BattleStatBlock baseStat)
        {
            RuntimeUnitId = UnitRuntimeIdGenerator.Next(team);
            TemplateId = templateId;
            Team = team;
            BaseStat = baseStat;
            // Phase 1-2에서 PassiveApplicator가 패시브/재능/장비 반영 후 SetComputedStat 호출.
            // 현재는 BaseStat을 그대로 사용.
            Stat = baseStat;

            Hp = Stat.MaxHp;
            Mana = 0;
        }

        public string RuntimeUnitId { get; }
        public string TemplateId { get; }
        public BattleTeam Team { get; }

        /// <summary>CSV에서 읽어온 원본 스탯 (불변)</summary>
        public BattleStatBlock BaseStat { get; }

        /// <summary>패시브/재능/장비 반영 후 실제 전투에 사용되는 스탯</summary>
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

        /// <summary>턴마다 마나 회복. 최대치 초과 시 최대치로 클램프.</summary>
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

        /// <summary>액티브 스킬 사용 후 마나 초기화.</summary>
        public void ConsumeMana()
        {
            Mana = 0;
        }

        /// <summary>Phase 1-2에서 PassiveApplicator가 패시브 반영 스탯을 주입</summary>
        public void SetComputedStat(BattleStatBlock computed)
        {
            Stat = computed;
        }
    }
}
