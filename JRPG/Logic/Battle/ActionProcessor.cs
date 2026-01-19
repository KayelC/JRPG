using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                   effect.Contains("all foes") ||
                   effect.Contains("all allies") ||
                   effect.Contains("party") ||
                   name == "amrita" ||
                   name == "salvation";
        }

        public CombatResult ExecuteAttack(Combatant attacker, Combatant target)
        {
            if (target.IsDead) return new CombatResult { Type = HitType.Miss, DamageDealt = 0 };

            Element element = attacker.WeaponElement;
            // PWR for standard attack is considered 15 for the formula
            int power = GetActionPower(attacker, 15);

            _io.WriteLine($"{attacker.Name} attacks {target.Name}!");

            if (!CombatMath.CheckHit(attacker, target, element, "90%"))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);

                // SMT Rule: Missing consumes charge
                attacker.IsCharged = false;

                return new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);

            if (aff == Affinity.Repel)
            {
                _io.WriteLine($"{target.Name} repelled the attack!", ConsoleColor.Red);
                attacker.IsCharged = false;
                return ProcessRepelEvent(attacker, element, power);
            }

            int damage = CombatMath.CalculateDamage(attacker, target, power, element, out bool isCritical);
            CombatResult result = target.ReceiveDamage(damage, element, isCritical);

            _knowledge.Learn(target.SourceId, element, aff);

            // Consume physical charge
            attacker.IsCharged = false;

            return result;
        }

        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            List<CombatResult> results = new List<CombatResult>();
            _io.WriteLine($"{attacker.Name} uses {skill.Name}!");

            // 1. Calculate Cost with Passive Deductions (Spell/Arms Master)
            var cost = skill.ParseCost();
            int costValue = cost.value;
            var passives = attacker.GetConsolidatedSkills();

            bool isPhysical = IsPhysicalElement(ElementHelper.FromCategory(skill.Category));
            bool isMagic = !isPhysical;

            // Apply Arms Master (Physical HP cost reduction)
            if (cost.isHP && isPhysical && passives.Contains("Arms Master"))
            {
                costValue /= 2;
            }
            // Apply Spell Master (Magic SP cost reduction)
            else if (!cost.isHP && isMagic && passives.Contains("Spell Master"))
            {
                costValue /= 2;
            }

            // 2. Deduct Cost
            if (cost.isHP)
            {
                // percentage HP costs are calculated based on MaxHP
                int hpCost = (int)(attacker.MaxHP * (costValue / 100.0));
                attacker.CurrentHP = Math.Max(1, attacker.CurrentHP - hpCost);
            }
            else
            {
                attacker.CurrentSP -= costValue;
            }

            // 3. Execution Loop
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

            // 4. Consume Charges: Only consume the relevant charge
            if (isPhysical) attacker.IsCharged = false;
            else attacker.IsMindCharged = false;

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

            // TRAESTO GEM: Battle Escape (Player remains on floor)
            if (item.Name == "Traesto Gem")
            {
                _io.WriteLine("A blinding light creates a path to safety!");
                return true;
            }

            if (item.Name == "Goho-M")
            {
                _io.WriteLine($"{item.Name} cannot be used in the heat of battle!");
                return false;
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

            if (!anyEffect) _io.WriteLine("It had no effect...");
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

                // Manual State Management
                // We want the element name white, but the affinity highlighted
                _io.Write($" {elem,-10}: ");

                ConsoleColor affColor = aff switch
                {
                    Affinity.Weak => ConsoleColor.Red,
                    Affinity.Resist => ConsoleColor.Green,
                    Affinity.Null => ConsoleColor.Cyan,
                    Affinity.Repel => ConsoleColor.Blue,
                    Affinity.Absorb => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };

                _io.WriteLine($"{aff}", affColor);

                _knowledge.Learn(target.SourceId, elem, aff);
            }

            _io.WriteLine("--------------------------------------------------");
            _io.WriteLine("Press any key to continue...", ConsoleColor.Gray);
            _io.ReadKey();
        }

        private CombatResult ProcessOffensiveSkill(Combatant attacker, Combatant target, SkillData skill)
        {
            Element element = ElementHelper.FromCategory(skill.Category);
            // Power for skills is parsed from data
            int power = GetActionPower(attacker, skill.GetPowerVal());

            bool isInstantKill = skill.Effect.ToLower().Contains("instant kill");

            if (isInstantKill)
            {
                Affinity ikAff = CombatMath.GetEffectiveAffinity(target, element);

                // IK Rule: Null/Repel/Absorb function as normal blocks
                if (ikAff == Affinity.Null || ikAff == Affinity.Repel || ikAff == Affinity.Absorb)
                {
                    _knowledge.Learn(target.SourceId, element, ikAff);
                    return new CombatResult { Type = HitType.Null, Message = "Blocked!" };
                }

                // IK Rule: Check success against math kernel
                if (CombatMath.CalculateInstantKill(attacker, target, skill.Accuracy))
                {
                    int hpBefore = target.CurrentHP;
                    target.CurrentHP = 0; // Death
                    _knowledge.Learn(target.SourceId, element, ikAff);
                    return new CombatResult { Type = HitType.Weakness, DamageDealt = hpBefore, Message = "INSTANT KILL!" };
                }
                else
                {
                    // IK Rule: Miss only costs 1 Icon.
                    _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                    return new CombatResult { Type = HitType.Normal, Message = "MISS!" };
                }
            }

            // Standard Offensive Processing
            if (!CombatMath.CheckHit(attacker, target, element, skill.Accuracy))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                return new CombatResult { Type = HitType.Miss, Message = "MISS!" };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);
            if (aff == Affinity.Repel) return ProcessRepelEvent(attacker, element, power);

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
            string skillName = skill.Name;
            string effect = skill.Effect;

            // 1. DEKAJA: Remove positive buffs from enemies
            if (skillName == "Dekaja")
            {
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    if (target.Buffs[k] > 0) target.Buffs[k] = 0;
                }
                _io.WriteLine($"{target.Name}'s stat bonuses were nullified!");
            }

            // 2. DEKUNDA: Remove negative debuffs from allies
            if (skillName == "Dekunda")
            {
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    if (target.Buffs[k] < 0) target.Buffs[k] = 0;
                }
                _io.WriteLine($"{target.Name}'s stat penalties were nullified!");
            }

            // 3. KARNS: Reflect Shields
            if (skillName == "Tetrakarn") target.PhysKarnActive = true;
            if (skillName == "Makarakarn") target.MagicKarnActive = true;

            // 4. CHARGES: Power / Mind Charge
            if (skillName == "Power Charge") target.IsCharged = true;
            if (skillName == "Mind Charge") target.IsMindCharged = true;

            // 5. BREAKS: Elemental Resistance Removal
            if (skillName.Contains("Break"))
            {
                Element el = ElementHelper.FromCategory(skillName);
                if (el != Element.Almighty)
                {
                    if (target.BrokenAffinities.ContainsKey(el))
                        target.BrokenAffinities[el] = 3;
                    else target.BrokenAffinities.Add(el, 3);
                    _io.WriteLine($"{target.Name}'s {el} resistance was broken!");
                }
            }

            // 6. Healing / Recovery
            if (skill.Category.Contains("Recovery"))
            {
                // Strict Cure logic: Do not display success if the cure type doesn't match the ailment
                bool cured = false;
                if (skill.Effect.Contains("Cure")) cured = _status.CheckAndExecuteCure(target, skill.Effect);

                if (target.IsDead && skill.Effect.Contains("Revive"))
                {
                    target.CurrentHP = skill.Effect.Contains("fully") ? target.MaxHP : target.MaxHP / 2;
                    _io.WriteLine($"{target.Name} was revived!");
                }
                else if (!target.IsDead)
                {
                    // Handle "NaN" power by parsing the number inside parentheses in the Effect string
                    int heal = skill.GetPowerVal();
                    if (heal == 0)
                    {
                        Match match = Regex.Match(skill.Effect, @"\((\d+)\)");
                        if (match.Success) heal = int.Parse(match.Groups[1].Value);
                    }

                    if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                    if (skill.Effect.Contains("full")) heal = target.MaxHP;

                    int oldHP = target.CurrentHP;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);

                    int actualHealed = target.CurrentHP - oldHP;
                    _io.WriteLine($"{target.Name} recovered {actualHealed} HP.");
                }
                else if (cured) _io.WriteLine($"{target.Name} was cured!");
            }

            // Standard Buffs/Debuffs (Kaja/Nda)
            if (skill.Category.Contains("Enhance"))
            {
                _status.ApplyStatChange(skill.Name, target);
                _io.WriteLine($"{target.Name}'s stats were modified!");
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

        /// <summary>
        /// Attack Power calculation.
        /// Demons use dynamic scaling (Lv+STR)*PWR/15. Humans use Weapon Power or Lv+(STR*2).
        /// </summary>
        private int GetActionPower(Combatant c, int pwrValue)
        {
            // Case 1: Demons (or Avatars) use the requested growth-based formula
            if (c.Class == ClassType.Demon || c.Class == ClassType.Avatar)
            {
                // (Lv + STR) * PWR / 15
                double demonPwr = (c.Level + c.GetStat(StatType.STR)) * pwrValue / 15.0;
                return (int)Math.Max(1, Math.Floor(demonPwr));
            }

            // Case 2: Humans/Operators with weapons
            if (c.EquippedWeapon != null) return c.EquippedWeapon.Power;

            // Case 3: Humans without weapons (Unarmed)
            return c.Level + (c.GetStat(StatType.STR) * 2);
        }

        private bool IsOffensive(SkillData skill)
        {
            string cat = skill.Category.ToLower();
            return !cat.Contains("recovery") && !cat.Contains("enhance");
        }

        private bool IsPhysicalElement(Element e)
        {
            return e == Element.Slash || e == Element.Strike || e == Element.Pierce;
        }
    }
}