using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The authoritative executor of battle actions.
    /// Manages the live-reactive interaction between combatants, handling damage, reflection, and data analysis.
    /// Uses the IBattleMessenger mediator for all broadcasts.
    /// </summary>
    public class ActionProcessor
    {
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;
        private readonly IBattleMessenger _messenger;

        public ActionProcessor(StatusRegistry status, BattleKnowledge knowledge, IBattleMessenger messenger)
        {
            _status = status;
            _knowledge = knowledge;
            _messenger = messenger;
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

            _messenger.Publish($"{attacker.Name} attacks {target.Name}!");

            if (!CombatMath.CheckHit(attacker, target, element, "90%"))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                _messenger.Publish("MISS!", ConsoleColor.Gray, 400);

                // Missing consumes charge
                attacker.IsCharged = false;
                return new CombatResult { Type = HitType.Miss, Message = "MISS!", DamageDealt = 0 };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);

            if (aff == Affinity.Repel)
            {
                _messenger.Publish($"{target.Name} repelled the attack!", ConsoleColor.Red);
                attacker.IsCharged = false;
                return ProcessRepelEvent(attacker, element, power);
            }

            int damage = CombatMath.CalculateDamage(attacker, target, power, element, out bool isCritical);
            CombatResult result = target.ReceiveDamage(damage, element, isCritical);

            _knowledge.Learn(target.SourceId, element, aff);

            // Report the result via Event
            ReportDamageResult(result, target.Name);

            // Consume physical charge
            attacker.IsCharged = false;
            return result;
        }

        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            List<CombatResult> results = new List<CombatResult>();
            _messenger.Publish($"{attacker.Name} uses {skill.Name}!", ConsoleColor.White, 200);

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
            _messenger.Publish($"{user.Name} used {item.Name}!", ConsoleColor.White, 200);

            // TRAESTO GEM: Battle Escape (Player remains on floor)
            if (item.Name == "Traesto Gem")
            {
                _messenger.Publish("A blinding light creates a path to safety!", ConsoleColor.White, 800);
                return true;
            }

            if (item.Name == "Goho-M")
            {
                _messenger.Publish($"{item.Name} cannot be used in the heat of battle!", ConsoleColor.Gray, 100);
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
                            _messenger.Publish($"{target.Name} recovered {target.CurrentHP - oldHP} HP.");
                            anyEffect = true;
                        }
                        break;

                    case "Spirit":
                        if (target.CurrentSP < target.MaxSP)
                        {
                            int oldSP = target.CurrentSP;
                            target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                            _messenger.Publish($"{target.Name} recovered {target.CurrentSP - oldSP} SP.");
                            anyEffect = true;
                        }
                        break;

                    case "Revive":
                        if (target.IsDead)
                        {
                            int revVal = item.EffectValue >= 100 ? target.MaxHP : target.MaxHP / 2;
                            target.CurrentHP = revVal;
                            _messenger.Publish($"{target.Name} was revived!", ConsoleColor.Green);
                            anyEffect = true;
                        }
                        break;

                    case "Cure":
                        if (_status.CheckAndExecuteCure(target, item.Name))
                        {
                            _messenger.Publish($"{target.Name} was cured.");
                            anyEffect = true;
                        }
                        break;
                }
            }

            if (!anyEffect) _messenger.Publish("It had no effect...");
            return anyEffect;
        }

        // Orchestrates the analysis logic and requests a UI display via the messenger.
        public void ExecuteAnalyze(Combatant target)
        {
            // 1. LOGIC: Update the Knowledge Database (Record all current affinities)
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _knowledge.Learn(target.SourceId, elem, aff);
            }

            // 2. BROADCAST: Pass the target to the messenger. 
            // The BattleLogger will pick this up and draw the Analysis screen.
            _messenger.Publish(message: null, analysisTarget: target);
        }

        private CombatResult ProcessRepelEvent(Combatant attacker, Element element, int power)
        {
            int reflectedDmg = CombatMath.CalculateReflectedDamage(attacker, power, element);
            _messenger.Publish($"{attacker.Name} is hit by the reflection!", ConsoleColor.Red);

            CombatResult result = attacker.ReceiveDamage(reflectedDmg, element, false);
            ReportDamageResult(result, attacker.Name);

            result.Type = HitType.Repel;
            return result;
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
                    _messenger.Publish("Blocked!", ConsoleColor.Gray);
                    return new CombatResult { Type = HitType.Null, Message = "Blocked!" };
                }

                // IK Rule: Check success against math kernel
                if (CombatMath.CalculateInstantKill(attacker, target, skill.Accuracy))
                {
                    int hpBefore = target.CurrentHP;
                    target.CurrentHP = 0; // Death
                    _knowledge.Learn(target.SourceId, element, ikAff);
                    _messenger.Publish("INSTANT KILL!", ConsoleColor.Red);
                    return new CombatResult { Type = HitType.Weakness, DamageDealt = hpBefore, Message = "INSTANT KILL!" };
                }
                else
                {
                    // IK Rule: Miss only costs 1 Icon.
                    _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                    _messenger.Publish("MISS!", ConsoleColor.Gray);
                    return new CombatResult { Type = HitType.Normal, Message = "MISS!" };
                }
            }

            // Standard Offensive Processing
            if (!CombatMath.CheckHit(attacker, target, element, skill.Accuracy))
            {
                _knowledge.Learn(target.SourceId, element, Affinity.Normal);
                _messenger.Publish("MISS!", ConsoleColor.Gray);
                return new CombatResult { Type = HitType.Miss, Message = "MISS!" };
            }

            Affinity aff = CombatMath.GetEffectiveAffinity(target, element);
            if (aff == Affinity.Repel) return ProcessRepelEvent(attacker, element, power);

            int damage = CombatMath.CalculateDamage(attacker, target, power, element, out bool isCritical);
            CombatResult result = target.ReceiveDamage(damage, element, isCritical);

            if (result.Type != HitType.Null && result.Type != HitType.Absorb)
            {
                if (_status.TryInflict(attacker, target, skill.Effect))
                {
                    _messenger.Publish($"Infected with {skill.Effect}!", ConsoleColor.Magenta);
                }
            }

            _knowledge.Learn(target.SourceId, element, aff);
            ReportDamageResult(result, target.Name);
            return result;
        }

        private CombatResult ProcessUtilitySkill(Combatant attacker, Combatant target, SkillData skill)
        {
            string skillName = skill.Name;

            // 1. DEKAJA: Remove positive buffs from enemies
            if (skillName == "Dekaja")
            {
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    if (target.Buffs[k] > 0) target.Buffs[k] = 0;
                }
                _messenger.Publish($"{target.Name}'s stat bonuses were nullified!");
            }
            // 2. DEKUNDA: Remove negative debuffs from allies
            else if (skillName == "Dekunda")
            {
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    if (target.Buffs[k] < 0) target.Buffs[k] = 0;
                }
                _messenger.Publish($"{target.Name}'s stat penalties were nullified!");
            }
            // 3. KARNS: Reflect Shields
            else if (skillName == "Tetrakarn")
            {
                target.PhysKarnActive = true;
                _messenger.Publish("Physical Shield deployed.");
            }
            else if (skillName == "Makarakarn")
            {
                target.MagicKarnActive = true;
                _messenger.Publish("Magic Shield deployed.");
            }
            // 4. CHARGES: Power / Mind Charge
            else if (skillName == "Power Charge")
            {
                target.IsCharged = true;
                _messenger.Publish($"{target.Name} is focusing power!");
            }
            else if (skillName == "Mind Charge")
            {
                target.IsMindCharged = true;
                _messenger.Publish($"{target.Name} is focusing spiritual energy!");
            }
            // 5. BREAKS: Elemental Resistance Removal
            else if (skillName.Contains("Break"))
            {
                Element el = ElementHelper.FromCategory(skillName);
                if (el != Element.Almighty)
                {
                    if (target.BrokenAffinities.ContainsKey(el)) target.BrokenAffinities[el] = 3;
                    else target.BrokenAffinities.Add(el, 3);
                    _messenger.Publish($"{target.Name}'s {el} resistance was broken!");
                }
            }
            // 6. Healing / Recovery
            else if (skill.Category.Contains("Recovery"))
            {
                // Strict Cure logic: Do not display success if the cure type doesn't match the ailment
                bool cured = false;
                if (skill.Effect.Contains("Cure"))
                {
                    cured = _status.CheckAndExecuteCure(target, skill.Effect);
                    if (cured) _messenger.Publish($"{target.Name} was cured!");
                }

                if (target.IsDead && skill.Effect.Contains("Revive"))
                {
                    target.CurrentHP = skill.Effect.Contains("fully") ? target.MaxHP : target.MaxHP / 2;
                    _messenger.Publish($"{target.Name} was revived!", ConsoleColor.Green);
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
                    if (!cured) _messenger.Publish($"{target.Name} recovered {actualHealed} HP.");
                }
            }
            // Standard Buffs/Debuffs (Kaja/Nda)
            else if (skill.Category.Contains("Enhance"))
            {
                _status.ApplyStatChange(skill.Name, target);
                _messenger.Publish($"{target.Name}'s stats were modified!");
            }

            return new CombatResult { Type = HitType.Normal, Message = "Success" };
        }

        private void ReportDamageResult(CombatResult result, string targetName)
        {
            if (result.DamageDealt > 0)
            {
                string msg = $"{targetName} took {result.DamageDealt} damage";
                if (result.IsCritical) msg += " (CRITICAL)";
                if (result.Type == HitType.Weakness) msg += " (WEAKNESS)";
                _messenger.Publish(msg);
            }
            else if (result.Type == HitType.Absorb)
            {
                _messenger.Publish($"{targetName} absorbed the attack!", ConsoleColor.Green);
            }
            else if (result.Type == HitType.Null)
            {
                _messenger.Publish($"{targetName} blocked the attack!");
            }
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
                double demonPwr = (c.Level + c.GetStat(StatType.St)) * pwrValue / 15.0;
                return (int)Math.Max(1, Math.Floor(demonPwr));
            }

            // Case 2: Humans/Operators with weapons
            if (c.EquippedWeapon != null) return c.EquippedWeapon.Power;

            // Case 3: Humans without weapons (Unarmed)
            return c.Level + (c.GetStat(StatType.St) * 2);
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