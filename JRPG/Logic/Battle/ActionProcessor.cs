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
    /// The authoritative executor of battle actions.
    /// Manages the live-reactive interaction between combatants, handling damage,
    /// reflection, and data analysis with SMT III fidelity.
    /// </summary>
    public class ActionProcessor
    {
        private readonly IGameIO _io;
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;

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

            foreach (var target in targets)
            {
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

                if (res.Type == HitType.Repel) break;
            }

            return results;
        }

        /// <summary>
        /// Executes an item and returns true if at least one target was affected.
        /// Prevents consuming items that have no effect.
        /// </summary>
        public bool ExecuteItem(Combatant user, List<Combatant> targets, ItemData item)
        {
            bool anyEffect = false;
            _io.WriteLine($"{user.Name} used {item.Name}!");

            // Handle Dungeon Escape Items (Traesto Gem / Goho-M)
            if (item.Type == "Utility" && (item.Name == "Traesto Gem" || item.Name == "Goho-M"))
            {
                _io.WriteLine("A mysterious light envelops the party...");
                return true; // These always work if they reach execution
            }

            foreach (var target in targets)
            {
                if (target.IsDead && item.Type != "Revive") continue;

                switch (item.Type)
                {
                    case "Healing":
                    case "Healing_All":
                        if (target.CurrentHP < target.MaxHP)
                        {
                            int heal = item.EffectValue >= 9999 ? target.MaxHP : item.EffectValue;
                            int oldHP = target.CurrentHP;
                            target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                            _io.WriteLine($"{target.Name} recovered {target.CurrentHP - oldHP} HP.");
                            anyEffect = true;
                        }
                        break;

                    case "Spirit":
                        if (target.CurrentSP < target.MaxSP)
                        {
                            int oldSP = target.CurrentSP;
                            target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                            _io.WriteLine($"{target.Name} recovered {target.CurrentSP - oldSP} SP.");
                            anyEffect = true;
                        }
                        break;

                    case "Revive":
                        if (target.IsDead)
                        {
                            int revVal = item.EffectValue >= 100 ? target.MaxHP : target.MaxHP / 2;
                            target.CurrentHP = revVal;
                            _io.WriteLine($"{target.Name} was revived!");
                            anyEffect = true;
                        }
                        break;

                    case "Cure":
                        if (_status.CheckAndExecuteCure(target, item.Name))
                        {
                            _io.WriteLine($"{target.Name} was cured.");
                            anyEffect = true;
                        }
                        break;
                }
            }

            if (!anyEffect)
            {
                _io.WriteLine("It had no effect...");
            }
            return anyEffect;
        }

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
            if (skill.Category.Contains("Recovery"))
            {
                if (skill.Effect.Contains("Cure"))
                {
                    _status.CheckAndExecuteCure(target, skill.Effect);
                }

                if (target.IsDead && skill.Effect.Contains("Revive"))
                {
                    target.CurrentHP = target.MaxHP / 2;
                    _io.WriteLine($"{target.Name} was revived!");
                }
                else if (!target.IsDead)
                {
                    int heal = skill.GetPowerVal();
                    if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                    if (skill.Effect.Contains("full")) heal = target.MaxHP;

                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                    _io.WriteLine($"{target.Name} recovered health.");
                }
            }

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