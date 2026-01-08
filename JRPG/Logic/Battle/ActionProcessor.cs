using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Definitive Action Executor for the Battle Sub-System.
    /// Handles Ma-/Me- targeting, Reactive Iteration, and Forced Ailment Behaviors.
    /// </summary>
    public class ActionProcessor
    {
        private readonly IGameIO _io;
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;
        private readonly Random _rnd = new Random();

        public ActionProcessor(IGameIO io, StatusRegistry status, BattleKnowledge knowledge)
        {
            _io = io;
            _status = status;
            _knowledge = knowledge;
        }

        /// <summary>
        /// Checks if a skill targets the entire side based on "Ma-", "Me-", or unique keywords.
        /// </summary>
        public bool IsMultiTarget(SkillData skill)
        {
            string name = skill.Name.ToLower();
            string effect = skill.Effect.ToLower();

            return name.StartsWith("ma") ||
                   name.StartsWith("me") ||
                   effect.Contains("party") ||
                   effect.Contains("all enemies") ||
                   name == "amrita" ||
                   name == "salvation";
        }

        public CombatResult ExecuteAttack(Combatant attacker, Combatant target)
        {
            if (target.IsDead) return new CombatResult { Type = HitType.Miss, DamageDealt = 0 };

            Element element = attacker.WeaponElement;
            int power = GetActionPower(attacker);

            _io.WriteLine($"{attacker.Name} attacks {target.Name}!");

            if (!CombatMath.CheckHit(attacker, target, element, "90%"))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                return new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);

            if (aff == Affinity.Repel)
            {
                _io.WriteLine($"{target.Name} repelled the attack!", ConsoleColor.Red);
                return ProcessRepelEvent(attacker, element, power);
            }

            int damage = CombatMath.CalculateDamage(attacker, target, power, element, out bool isCritical);
            CombatResult result = target.ReceiveDamage(damage, element, isCritical);

            _knowledge.Learn(target.SourceId, element, aff);
            return result;
        }

        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            List<CombatResult> results = new List<CombatResult>();
            _io.WriteLine($"{attacker.Name} uses {skill.Name}!");

            // Cost Deduction
            var cost = skill.ParseCost();
            if (cost.isHP)
            {
                int hpCost = (int)(attacker.MaxHP * (cost.value / 100.0));
                attacker.CurrentHP = Math.Max(1, attacker.CurrentHP - hpCost);
            }
            else
            {
                attacker.CurrentSP -= cost.value;
            }

            // Reactive Iteration Loop
            foreach (var target in targets)
            {
                // Reactive check: Ensure target didn't die or flee from a previous hit in this same action
                if (target.IsDead && !skill.Effect.Contains("Revive")) continue;

                CombatResult res;
                if (IsOffensive(skill))
                {
                    res = ProcessOffensiveSkill(attacker, target, skill);
                }
                else
                {
                    res = ProcessUtilitySkill(attacker, target, skill);
                }

                results.Add(res);

                // SMT III Rule: If any hit is repelled, stop the multi-target sequence immediately
                if (res.Type == HitType.Repel) break;
            }

            return results;
        }

        /// <summary>
        /// Executes an item on one or more targets.
        /// Maps the item type to recovery, cure, or revival logic.
        /// </summary>
        public void ExecuteItem(Combatant user, List<Combatant> targets, ItemData item)
        {
            _io.WriteLine($"{user.Name} used {item.Name}!");

            foreach (var target in targets)
            {
                // Reactive check: skip if dead unless it's a revive item
                if (target.IsDead && item.Type != "Revive") continue;

                switch (item.Type)
                {
                    case "Healing":
                    case "Healing_All":
                        int heal = item.EffectValue;
                        if (item.EffectValue >= 9999) heal = target.MaxHP;
                        int oldHP = target.CurrentHP;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                        _io.WriteLine($"{target.Name} recovered {target.CurrentHP - oldHP} HP.");
                        break;

                    case "Spirit":
                        int oldSP = target.CurrentSP;
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        _io.WriteLine($"{target.Name} recovered {target.CurrentSP - oldSP} SP.");
                        break;

                    case "Revive":
                        if (target.IsDead)
                        {
                            // Items like Balm of Life restore 100% (9999), Revival Beads restore 50%
                            int revVal = item.EffectValue >= 100 ? target.MaxHP : target.MaxHP / 2;
                            target.CurrentHP = revVal;
                            _io.WriteLine($"{target.Name} was revived!");
                        }
                        break;

                    case "Cure":
                        if (_status.CheckAndExecuteCure(target, item.Name))
                        {
                            _io.WriteLine($"{target.Name} was cured of ailments.");
                        }
                        else
                        {
                            _io.WriteLine("No effect.");
                        }
                        break;
                }
            }
        }


        /// <summary>
        /// Re-implemented Analyze: Fully populates the knowledge bank and reveals UI data.
        /// </summary>
        public void ExecuteAnalyze(Combatant target)
        {
            _io.Clear();
            _io.WriteLine($"=== ANALYSIS: {target.Name} ===", ConsoleColor.Yellow);
            _io.WriteLine($"Level: {target.Level} | HP: {target.CurrentHP}/{target.MaxHP} | SP: {target.CurrentSP}/{target.MaxSP}");
            _io.WriteLine("--------------------------------------------------");
            _io.WriteLine("Affinities:");

            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _io.WriteLine($"  {elem,-10}: {aff}");
                _knowledge.Learn(target.SourceId, elem, aff);
            }

            _io.WriteLine("--------------------------------------------------");
            _io.WriteLine("Press any key to continue...", ConsoleColor.Gray);
            _io.ReadKey();
        }

        private CombatResult ProcessOffensiveSkill(Combatant attacker, Combatant target, SkillData skill)
        {
            Element element = ElementHelper.FromCategory(skill.Category);
            int power = skill.GetPowerVal();

            if (!CombatMath.CheckHit(attacker, target, element, skill.Accuracy))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                return new CombatResult { Type = HitType.Miss, Message = "MISS!" };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);

            if (aff == Affinity.Repel)
            {
                return ProcessRepelEvent(attacker, element, power);
            }

            int damage = CombatMath.CalculateDamage(attacker, target, power, element, out bool isCritical);
            CombatResult result = target.ReceiveDamage(damage, element, isCritical);

            if (result.Type != HitType.Null && result.Type != HitType.Absorb)
            {
                _status.TryInflict(attacker, target, skill.Effect);
            }

            _knowledge.Learn(target.SourceId, element, aff);
            return result;
        }

        private CombatResult ProcessUtilitySkill(Combatant attacker, Combatant target, SkillData skill)
        {
            // 1. Curing Logic
            if (skill.Category.Contains("Recovery") && skill.Effect.Contains("Cure"))
            {
                if (_status.CheckAndExecuteCure(target, skill.Effect))
                    _io.WriteLine($"{target.Name} was cured!");
            }

            // 2. Revival Logic
            if (target.IsDead && skill.Effect.Contains("Revive"))
            {
                // Recarm (50%) vs Samarecarm (Full)
                int heal = skill.Effect.Contains("fully") ? target.MaxHP : target.MaxHP / 2;
                target.CurrentHP = heal;
                _io.WriteLine($"{target.Name} was revived!");
            }
            // 3. Standard Healing
            else if (!target.IsDead && skill.Category.Contains("Recovery"))
            {
                int heal = skill.GetPowerVal();
                if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                if (skill.Effect.Contains("Fully")) heal = target.MaxHP;

                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                _io.WriteLine($"{target.Name} recovered health.");
            }

            // 4. Buffs/Debuffs
            if (skill.Category.Contains("Enhance"))
            {
                _status.ApplyStatChange(skill.Name, target);
            }

            return new CombatResult { Type = HitType.Normal, Message = "Success" };
        }

        private CombatResult ProcessRepelEvent(Combatant attacker, Element element, int power)
        {
            int reflectedDmg = CombatMath.CalculateReflectedDamage(attacker, power, element);
            _io.WriteLine($"{attacker.Name} is hit by the reflection!", ConsoleColor.Red);
            CombatResult result = attacker.ReceiveDamage(reflectedDmg, element, false);
            result.Type = HitType.Repel;
            return result;
        }

        private int GetActionPower(Combatant c)
        {
            if (c.EquippedWeapon != null) return c.EquippedWeapon.Power;
            return c.Level + (c.GetStat(StatType.STR) * 2);
        }

        private bool IsOffensive(SkillData skill)
        {
            string cat = skill.Category.ToLower();
            return !cat.Contains("recovery") && !cat.Contains("enhance");
        }
    }
}