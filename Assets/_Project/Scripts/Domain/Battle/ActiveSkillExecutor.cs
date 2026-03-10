using System.Collections.Generic;
using UnityEngine;

namespace ProjectH.Battle
{
    /// <summary>
    /// 액티브 스킬을 실행합니다.
    /// 새 스킬 추가 시: TryExecute() 내부 switch에 case 추가.
    /// </summary>
    public static class ActiveSkillExecutor
    {
        /// <summary>
        /// 마나가 가득 찬 경우 액티브 스킬을 실행합니다.
        /// 실행되면 true, 마나 부족 또는 스킬 없음이면 false 반환.
        /// </summary>
        public static bool TryExecute(
            BattleUnit actor,
            BattleRoster roster,
            IReadOnlyList<SkillDefinition> skills,
            System.Random rng,
            BattleEventBus events)
        {
            if (!actor.IsManaFull)
            {
                return false;
            }

            SkillDefinition? activeSkill = null;
            foreach (var skill in skills)
            {
                if (skill.Kind == SkillKind.Active)
                {
                    activeSkill = skill;
                    break;
                }
            }

            if (activeSkill == null)
            {
                return false;
            }

            actor.ConsumeMana();
            events.Publish(new BattleEvent(BattleEventType.ActiveSkillUsed, actor.RuntimeUnitId, actor.RuntimeUnitId, 0));

            ExecuteSkill(actor, roster, activeSkill.Value, rng, events);
            return true;
        }

        private static void ExecuteSkill(
            BattleUnit actor,
            BattleRoster roster,
            SkillDefinition skill,
            System.Random rng,
            BattleEventBus events)
        {
            // 스킬ID 기반으로 개별 구현. 새 스킬 추가 시 case 추가.
            switch (skill.SkillId)
            {
                // ── 용병 액티브 ──────────────────────────────────────────
                // case "AC_SHIELD_SLAM":  // TODO: 구현 예정
                // case "AC_BLADE_FRENZY": // TODO: 구현 예정
                // case "AC_FIELD_MEDIC":
                // case "AC_STORM_BREAKER":
                // ── 몬스터 액티브 ─────────────────────────────────────────
                // case "AC_FERAL_BITE":
                // case "AC_AMBUSH":
                // case "AC_ACID_BURST":
                // case "AC_SOUL_DRAIN":
                // case "AC_STONE_CRASH":
                // case "AC_CHAOS_SPLASH":
                // case "AC_INFERNO_RING":
                default:
                    Debug.LogWarning($"[ActiveSkillExecutor] 미구현 액티브 스킬: {skill.SkillId}");
                    break;
            }
        }
    }
}
