using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectH.Battle
{
    public sealed class BattleSimulation
    {
        private readonly BattleEventBus events;
        private readonly BattleRoster roster;
        private readonly BattleTurnService turnService;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SkillDefinition>> skillsByTemplateId;
        private readonly System.Random rng;

        public BattleSimulation(
            BattleEventBus events,
            BattleRoster roster,
            BattleTurnService turnService,
            IReadOnlyDictionary<string, IReadOnlyList<SkillDefinition>> skillsByTemplateId,
            System.Random rng)
        {
            this.events             = events;
            this.roster             = roster;
            this.turnService        = turnService;
            this.skillsByTemplateId = skillsByTemplateId;
            this.rng                = rng;
        }

        public void Start()
        {
            turnService.InitializeFirstTeam(roster.Allies, roster.Enemies);
        }

        public bool TickOneTurn()
        {
            if (!roster.HasAlive(BattleTeam.Ally) || !roster.HasAlive(BattleTeam.Enemy))
            {
                return false;
            }

            var actor = turnService.NextActor(roster);
            if (actor == null)
            {
                return false;
            }

            // 1. HP 재생
            if (actor.Stat.HpRegen > 0)
            {
                var healed = actor.ApplyHeal(actor.Stat.HpRegen);
                if (healed > 0)
                {
                    events.Publish(new BattleEvent(BattleEventType.Healed, actor.RuntimeUnitId, actor.RuntimeUnitId, healed));
                }
            }

            // 2. 마나 회복: MaxMana × 10% + Intelligence × 10%
            if (actor.Stat.MaxMana > 0)
            {
                var manaRecovery = Mathf.RoundToInt(actor.Stat.MaxMana * 0.10f + actor.Stat.Intelligence * 0.10f);
                actor.RecoverMana(manaRecovery);
            }

            var defenderTeam = actor.Team == BattleTeam.Ally ? BattleTeam.Enemy : BattleTeam.Ally;
            var target = roster.NextAlive(defenderTeam, 0);
            if (target == null)
            {
                return false;
            }

            events.Publish(new BattleEvent(BattleEventType.TurnStarted, actor.RuntimeUnitId, target.RuntimeUnitId, 0));

            // 3. 스킬 우선순위: 액티브 → 기본 공격
            var skills = GetSkills(actor.TemplateId);
            var usedSkill = ActiveSkillExecutor.TryExecute(actor, roster, skills, rng, events);
            if (!usedSkill)
            {
                // 기본 공격
                ExecuteBasicAttack(actor, target);
            }

            return true;
        }

        private void ExecuteBasicAttack(BattleUnit actor, BattleUnit target)
        {
            var result = DamageCalculator.Calculate(actor, target, 1.0f, rng);

            if (result.Evaded)
            {
                events.Publish(new BattleEvent(BattleEventType.Evaded, actor.RuntimeUnitId, target.RuntimeUnitId, 0));
                return;
            }

            var dealt = target.ApplyDamage(result.Dealt);
            events.Publish(new BattleEvent(BattleEventType.Damaged, actor.RuntimeUnitId, target.RuntimeUnitId, dealt));

            if (!target.IsAlive)
            {
                events.Publish(new BattleEvent(BattleEventType.Died, actor.RuntimeUnitId, target.RuntimeUnitId, 0));
                return;
            }

            // 가시 피해
            if (result.ThornDealt > 0)
            {
                var thornHit = actor.ApplyDamage(result.ThornDealt);
                events.Publish(new BattleEvent(BattleEventType.ThornsReflected, target.RuntimeUnitId, actor.RuntimeUnitId, thornHit));
                if (!actor.IsAlive)
                {
                    events.Publish(new BattleEvent(BattleEventType.Died, target.RuntimeUnitId, actor.RuntimeUnitId, 0));
                    return;
                }
            }

            // 생명력 흡수
            if (result.LifeStealHeal > 0)
            {
                var healed = actor.ApplyHeal(result.LifeStealHeal);
                if (healed > 0)
                {
                    events.Publish(new BattleEvent(BattleEventType.Healed, actor.RuntimeUnitId, actor.RuntimeUnitId, healed));
                }
            }

            // 반격
            if (target.Stat.Counter > 0f && rng.NextDouble() < target.Stat.Counter)
            {
                var counterResult = DamageCalculator.Calculate(target, actor, 1.0f, rng);
                if (!counterResult.Evaded && counterResult.Dealt > 0)
                {
                    var counterDealt = actor.ApplyDamage(counterResult.Dealt);
                    events.Publish(new BattleEvent(BattleEventType.CounterAttacked, target.RuntimeUnitId, actor.RuntimeUnitId, counterDealt));
                    if (!actor.IsAlive)
                    {
                        events.Publish(new BattleEvent(BattleEventType.Died, target.RuntimeUnitId, actor.RuntimeUnitId, 0));
                    }
                }
            }
        }

        private IReadOnlyList<SkillDefinition> GetSkills(string templateId)
        {
            if (skillsByTemplateId.TryGetValue(templateId, out var skills))
            {
                return skills;
            }

            return Array.Empty<SkillDefinition>();
        }
    }
}
