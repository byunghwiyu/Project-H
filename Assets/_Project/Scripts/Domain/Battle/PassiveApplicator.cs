using System.Collections.Generic;
using UnityEngine;

namespace ProjectH.Battle
{
    /// <summary>
    /// 패시브 스킬을 BattleStatBlock에 반영합니다.
    /// 새 스킬 추가 시: Apply() 내부 switch에 case 추가.
    /// </summary>
    public static class PassiveApplicator
    {
        public static BattleStatBlock Apply(BattleStatBlock baseStat, IReadOnlyList<SkillDefinition> skills)
        {
            var s = baseStat;

            foreach (var skill in skills)
            {
                if (skill.Kind != SkillKind.Passive)
                {
                    continue;
                }

                ApplyPassive(ref s, skill);
            }

            return s;
        }

        private static void ApplyPassive(ref BattleStatBlock s, SkillDefinition skill)
        {
            // 스킬ID 기반으로 개별 구현. 새 스킬 추가 시 case 추가.
            switch (skill.SkillId)
            {
                // ── 용병 패시브 ──────────────────────────────────────────
                // case "PS_IRON_SKIN":    // TODO: 구현 예정
                // case "PS_BATTLE_RUSH":  // TODO: 구현 예정
                // case "PS_TACTICAL_AURA":
                // case "PS_LEGEND_WILL":
                // ── 몬스터 패시브 ─────────────────────────────────────────
                // case "PS_POUNCE":
                // case "PS_DIRTY_STRIKE":
                // case "PS_ARC_SURGE":
                // case "PS_VOID_SHELL":
                // case "PS_BEDROCK":
                // case "PS_TIDAL_SKIN":
                // case "PS_BURNING_CORE":
                default:
                    Debug.LogWarning($"[PassiveApplicator] 미구현 패시브: {skill.SkillId}");
                    break;
            }
        }
    }
}
