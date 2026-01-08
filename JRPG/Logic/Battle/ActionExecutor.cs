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
        /// Executes a basic physical attack using the combatant's equipped weapon.
        /// </summary>
        public CombatResult ExecuteBasicAttack(Combatant attacker, Combatant target, BattleKnowledge knowledge)
        {
            Element element = attacker.WeaponElement;

            if (!CombatMath.CheckHit(attacker, target, element, "95%"))
            {
                var missResult = new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
                knowledge.Learn(target.SourceId, element, Affinity.Normal);
                return missResult;
            }

            int damage = CombatMath.CalculateDamage(attacker, target, attacker.Level + 10, element, out bool isCritical);
            var result = target.ReceiveDamage(damage, element, isCritical);

            UpdateKnowledgeBank(target, element, result, knowledge);

            return result;
        }

        /// <summary>
        /// Executes a skill on multiple targets and calculates the aggregate result for the Press Turn system.
        /// </summary>
        public (HitType worstHit, bool advantageTriggered) ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill, BattleKnowledge knowledge)
        {
            // 1. Pay the Cost
            var cost = skill.ParseCost();
            if (cost.isHP)
            {
                attacker.CurrentHP -= cost.value;
                if (attacker.CurrentHP < 1) attacker.CurrentHP = 1;
            }
            else
            {
                attacker.CurrentSP -= cost.value;
            }

            // 2. Handle Non-Offensive Skills (Recovery/Enhance)
            if (skill.Category.Contains("Recovery"))
            {
                foreach (var target in targets)
                {
                    ExecuteRecoveryAction(target, skill);
                }
                return (HitType.Normal, false);
            }

            if (skill.Category.Contains("Enhance"))
            {
                foreach (var target in targets)
                {
                    _status.ApplyStatModifier(target, skill.Name);
                }
                return (HitType.Normal, false);
            }

            // 3. Handle Offensive Skills
            Element element = ElementHelper.FromCategory(skill.Category);
            List<CombatResult> results = new List<CombatResult>();

            foreach (var target in targets)
            {
                if (!CombatMath.CheckHit(attacker, target, element, skill.Accuracy))
                {
                    results.Add(new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 });
                    continue;
                }

                int skillPower = skill.GetPowerVal();
                int damage = CombatMath.CalculateDamage(attacker, target, skillPower, element, out bool isCritical);

                var res = target.ReceiveDamage(damage, element, isCritical);

                if (res.Type != HitType.Null && res.Type != HitType.Repel && res.Type != HitType.Absorb)
                {
                    if (!string.IsNullOrEmpty(skill.Effect))
                    {
                        _status.TryInflictAilment(attacker, target, skill.Effect);
                    }
                }

                UpdateKnowledgeBank(target, element, res, knowledge);
                results.Add(res);
            }

            return CalculateWorstResult(results);
        }

        /// <summary>
        /// Executes an item on one or more targets. 
        /// Returns a tuple indicating the battle result and whether the item was actually usable (effective).
        /// </summary>
        public (HitType worstHit, bool advantageTriggered, bool wasEffective) ExecuteItem(List<Combatant> targets, ItemData item, BattleKnowledge knowledge)
        {
            List<CombatResult> results = new List<CombatResult>();
            bool atLeastOneEffective = false;

            foreach (var target in targets)
            {
                CombatResult res = new CombatResult { Type = HitType.Normal, DamageDealt = 0 };
                bool effectiveOnThisTarget = false;

                switch (item.Type)
                {
                    case "Healing":
                    case "Healing_All":
                        if (!target.IsDead && target.CurrentHP < target.MaxHP)
                        {
                            int hpBefore = target.CurrentHP;
                            target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                            res.Message = $"Healed {target.CurrentHP - hpBefore} HP!";
                            effectiveOnThisTarget = true;
                        }
                        else res.Message = "No effect.";
                        break;

                    case "Spirit":
                        if (!target.IsDead && target.CurrentSP < target.MaxSP)
                        {
                            int spBefore = target.CurrentSP;
                            target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                            res.Message = $"Restored {target.CurrentSP - spBefore} SP!";
                            effectiveOnThisTarget = true;
                        }
                        else res.Message = "No effect.";
                        break;

                    case "Revive":
                        if (target.IsDead)
                        {
                            target.CurrentHP = (int)(target.MaxHP * (item.EffectValue / 100.0));
                            if (target.CurrentHP < 1) target.CurrentHP = 1;
                            res.Message = "Revived!";
                            effectiveOnThisTarget = true;
                        }
                        else res.Message = "Already alive.";
                        break;

                    case "Cure":
                        if (!target.IsDead && target.CurrentAilment != null)
                        {
                            res.Message = $"Cured {target.CurrentAilment.Name}!";
                            target.RemoveAilment();
                            effectiveOnThisTarget = true;
                        }
                        else res.Message = "No ailment.";
                        break;

                    case "Offensive":
                        Element gemElem = ElementHelper.FromCategory(item.Description);
                        if (!CombatMath.CheckHit(null, target, gemElem, "95%"))
                        {
                            res = new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
                        }
                        else
                        {
                            int gemDmg = CombatMath.CalculateDamage(null, target, item.EffectValue, gemElem, out bool gemCrit);
                            res = target.ReceiveDamage(gemDmg, gemElem, gemCrit);
                            UpdateKnowledgeBank(target, gemElem, res, knowledge);
                        }
                        effectiveOnThisTarget = true; // Offensive items are always "used" even if they miss
                        break;

                    default:
                        res.Message = "Used.";
                        effectiveOnThisTarget = true;
                        break;
                }

                if (effectiveOnThisTarget) atLeastOneEffective = true;
                results.Add(res);
            }

            var worst = CalculateWorstResult(results);
            return (worst.worstHit, worst.advantageTriggered, atLeastOneEffective);
        }

        private (HitType worstHit, bool advantageTriggered) CalculateWorstResult(List<CombatResult> results)
        {
            if (results.Any(r => r.Type == HitType.Repel || r.Type == HitType.Absorb))
                return (HitType.Repel, false);

            if (results.Any(r => r.Type == HitType.Miss || r.Type == HitType.Null))
                return (HitType.Miss, false);

            bool advantage = results.Any(r => r.Type == HitType.Weakness || r.IsCritical);

            return (HitType.Normal, advantage);
        }

        private void ExecuteRecoveryAction(Combatant target, SkillData skill)
        {
            if (skill.Effect.Contains("Cure"))
            {
                target.CheckCure(skill.Effect);
            }

            if (target.IsDead && skill.Effect.Contains("Revive"))
            {
                target.CurrentHP = target.MaxHP / 2;
            }
            else if (!target.IsDead)
            {
                int healAmount = skill.GetPowerVal();
                if (skill.Effect.Contains("50%") && skill.Effect.Contains("HP")) healAmount = target.MaxHP / 2;

                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
            }
        }

        private void UpdateKnowledgeBank(Combatant target, Element elem, CombatResult res, BattleKnowledge knowledge)
        {
            Affinity discoveredAff = Affinity.Normal;
            switch (res.Type)
            {
                case HitType.Weakness: discoveredAff = Affinity.Weak; break;
                case HitType.Null: discoveredAff = Affinity.Null; break;
                case HitType.Repel: discoveredAff = Affinity.Repel; break;
                case HitType.Absorb: discoveredAff = Affinity.Absorb; break;
                default:
                    if (res.Message.Contains("Resisted")) discoveredAff = Affinity.Resist;
                    break;
            }
            knowledge.Learn(target.SourceId, elem, discoveredAff);
        }
    }
}