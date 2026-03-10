using System;
using UnityEngine;

namespace ProjectH.Battle
{
    public static class DamageCalculator
    {
        /// <summary>
        /// 기본 공격 대미지를 계산합니다.
        /// multiplier: 스킬 배율 (기본 공격 시 1.0f)
        /// </summary>
        public static DamageResult Calculate(BattleUnit actor, BattleUnit target, float multiplier, System.Random rng)
        {
            // 1. 회피 판정
            if (target.Stat.Evasion > 0f && rng.NextDouble() < target.Stat.Evasion)
            {
                return new DamageResult(0, evaded: true, isCrit: false, thornDealt: 0, lifeStealHeal: 0);
            }

            // 2. 기본 대미지 (피해 타입별)
            var baseDamage = ComputeBaseDamage(actor, target);

            // 3. 스킬 배율 적용
            var damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));

            // 4. 치명타 판정
            var isCrit = actor.Stat.CritChance > 0f && rng.NextDouble() < actor.Stat.CritChance;
            if (isCrit)
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * actor.Stat.CritDamage));
            }

            // 5. 가시 피해 (근거리 공격자만)
            var thornDealt = 0;
            if (string.Equals(actor.Stat.AttackRangeType, "melee", StringComparison.OrdinalIgnoreCase))
            {
                thornDealt = target.Stat.ThornPhysical + target.Stat.ThornMagical;
            }

            // 6. 생명력 흡수
            var lifeStealHeal = actor.Stat.LifeSteal > 0f
                ? Mathf.Max(0, Mathf.RoundToInt(damage * actor.Stat.LifeSteal))
                : 0;

            return new DamageResult(damage, evaded: false, isCrit, thornDealt, lifeStealHeal);
        }

        private static int ComputeBaseDamage(BattleUnit actor, BattleUnit target)
        {
            var atk = actor.Stat.Attack;
            var def = target.Stat.Defense;

            return actor.Stat.DamageType switch
            {
                "magic"  => Mathf.Max(1, atk - Mathf.FloorToInt(def * 0.5f)),
                "chaos"  => Mathf.Max(1, atk),
                _        => Mathf.Max(1, atk - def), // physical (default)
            };
        }
    }
}
