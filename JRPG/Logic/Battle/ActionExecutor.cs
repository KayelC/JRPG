using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle
{
    public class ActionExecutor
    {
        private readonly StatusSystem _status;

        public ActionExecutor(StatusSystem statusSystem)
        {
            _status = statusSystem;
        }

        /// <summary>
        /// Executes a basic attack and returns the result.
        /// </summary>
        public CombatResult ExecuteBasicAttack(Combatant attacker, Combatant target, BattleKnowledge knowledge)
        {
            Element element = attacker.WeaponElement;

            if (!CombatMath.CheckHit(attacker, target, element, "95%"))
            {
                var miss = new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
                knowledge.Learn(target.SourceId, element, Affinity.Normal);
                return miss;
            }

            int damage = CombatMath.CalculateDamage(attacker, target, attacker.Level + 10, element, out bool isCritical);
            var result = target.ReceiveDamage(damage, element, isCritical);

            UpdateKnowledgeBank(target, element, result, knowledge);
            return result;
        }

        /// <summary>
        /// Executes a skill on one or more targets and returns the "Worst" result for Turn Icon calculation.
        /// </summary>
        public (HitType worstHit, bool advantageTriggered) ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill, BattleKnowledge knowledge)
        {
            // Pay Cost
            var cost = skill.ParseCost();
            if (cost.isHP)
            {
                attacker.CurrentHP = Math.Max(1, attacker.CurrentHP - cost.value);
            }
            else
            {
                attacker.CurrentSP -= cost.value;
            }

            // Recovery/Enhance Logic (Always counts as a Normal Hit icon-wise)
            if (skill.Category.Contains("Recovery") || skill.Category.Contains("Enhance"))
            {
                foreach (var t in targets)
                {
                    if (skill.Category.Contains("Recovery")) ExecuteRecoveryAction(t, skill);
                    else _status.ApplyStatModifier(t, skill.Name);
                }
                return (HitType.Normal, false);
            }

            // Offensive Logic
            Element element = ElementHelper.FromCategory(skill.Category);
            List<CombatResult> results = new List<CombatResult>();

            foreach (var target in targets)
            {
                if (!CombatMath.CheckHit(attacker, target, element, skill.Accuracy))
                {
                    results.Add(new CombatResult { Type = HitType.Miss, Message = "MISS!" });
                    continue;
                }

                int damage = CombatMath.CalculateDamage(attacker, target, skill.GetPowerVal(), element, out bool isCritical);
                var res = target.ReceiveDamage(damage, element, isCritical);

                if (res.Type != HitType.Null && res.Type != HitType.Repel && res.Type != HitType.Absorb)
                {
                    _status.TryInflictAilment(attacker, target, skill.Effect);
                }

                UpdateKnowledgeBank(target, element, res, knowledge);
                results.Add(res);
            }

            return CalculateWorstResult(results);
        }

        private (HitType, bool) CalculateWorstResult(List<CombatResult> results)
        {
            // SMT III Priority: Repel/Absorb > Miss/Null > Weak/Crit > Normal
            if (results.Any(r => r.Type == HitType.Repel || r.Type == HitType.Absorb))
                return (HitType.Repel, false);

            if (results.Any(r => r.Type == HitType.Miss || r.Type == HitType.Null))
                return (HitType.Miss, false);

            bool advantage = results.Any(r => r.Type == HitType.Weakness || r.IsCritical);

            return (HitType.Normal, advantage);
        }

        private void ExecuteRecoveryAction(Combatant target, SkillData skill)
        {
            if (skill.Effect.Contains("Cure")) target.CheckCure(skill.Effect);

            if (target.IsDead && skill.Effect.Contains("Revive"))
            {
                target.CurrentHP = target.MaxHP / 2;
            }
            else if (!target.IsDead)
            {
                int heal = skill.GetPowerVal();
                if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
            }
        }

        private void UpdateKnowledgeBank(Combatant target, Element elem, CombatResult res, BattleKnowledge knowledge)
        {
            Affinity aff = Affinity.Normal;
            switch (res.Type)
            {
                case HitType.Weakness: aff = Affinity.Weak; break;
                case HitType.Null: aff = Affinity.Null; break;
                case HitType.Repel: aff = Affinity.Repel; break;
                case HitType.Absorb: aff = Affinity.Absorb; break;
                default:
                    if (res.Message.Contains("Resisted")) aff = Affinity.Resist;
                    break;
            }
            knowledge.Learn(target.SourceId, elem, aff);
        }
    }
}