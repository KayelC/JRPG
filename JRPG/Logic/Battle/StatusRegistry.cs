using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System.Text.RegularExpressions;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// Defines the possible states an actor can be in at the start of their turn.
    /// </summary>
    public enum TurnStartResult
    {
        CanAct,          // Normal turn
        Skip,            // Turn lost (Freeze, Shock)
        ForcedPhysical,  // Must perform a basic attack on a random enemy (Rage)
        ForcedConfusion, // Must perform a random skill on an ally or heal an enemy (Charm)
        FleeBattle,      // Protagonist flees (Battle Ends via Escape)
        ReturnToCOMP     // Demon flees (Combatant returns to stock)
    }

    /// <summary>
    /// The authoritative logic engine for status ailments and stat modifications.
    /// Manages application, turn-start restrictions, and turn-end recovery/damage.
    /// </summary>
    public class StatusRegistry
    {
        private readonly Random _rnd = new Random();

        /// <summary>
        /// Attempts to inflict an ailment on a target.
        /// Parses probability using Regex and matches against the status_ailments.json library.
        /// </summary>
        public bool TryInflict(Combatant attacker, Combatant target, string skillEffect)
        {
            if (string.IsNullOrEmpty(skillEffect) || target.IsDead) return false;

            AilmentData ailmentToApply = null;
            foreach (var ailment in Database.Ailments.Values)
            {
                if (skillEffect.Contains(ailment.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ailmentToApply = ailment;
                    break;
                }
            }

            if (ailmentToApply == null) return false;

            int baseChance = 100;
            Match match = Regex.Match(skillEffect, @"(\d+)%");
            if (match.Success)
            {
                baseChance = int.Parse(match.Groups[1].Value);
            }

            int attackerLuck = attacker.GetStat(StatType.LUK);
            int targetLuck = target.GetStat(StatType.LUK);
            int finalChance = baseChance + (attackerLuck - targetLuck);

            if (_rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 95))
            {
                return target.InflictAilment(ailmentToApply, 3);
            }

            return false;
        }

        /// <summary>
        /// High-Fidelity Curing Logic. 
        /// Checks if the skill effect explicitly lists the target's current ailment or uses "Cure all".
        /// </summary>
        public bool CheckAndExecuteCure(Combatant target, string skillEffect)
        {
            if (target.CurrentAilment == null) return false;

            bool curesAll = skillEffect.Contains("Cure all", StringComparison.OrdinalIgnoreCase) ||
                            skillEffect.Contains("Cures all", StringComparison.OrdinalIgnoreCase);

            if (curesAll || skillEffect.Contains(target.CurrentAilment.Name, StringComparison.OrdinalIgnoreCase))
            {
                target.RemoveAilment();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called at the start of a combatant's action phase.
        /// Implements forced behaviors and movement restrictions.
        /// </summary>
        public TurnStartResult ProcessTurnStart(Combatant actor)
        {
            // Guarding always drops at the start of the actor's own turn.
            actor.IsGuarding = false;

            if (actor.CurrentAilment == null) return TurnStartResult.CanAct;

            string restriction = actor.CurrentAilment.ActionRestriction;

            switch (restriction)
            {
                case "SkipTurn":
                    return TurnStartResult.Skip;

                case "ChanceSkip":
                    // Panic: 50% chance to lose turn.
                    return _rnd.Next(0, 100) < 50 ? TurnStartResult.Skip : TurnStartResult.CanAct;

                case "ChanceSkipOrFlee":
                    // Fear logic.
                    int fearRoll = _rnd.Next(0, 100);
                    if (fearRoll < 15) // 15% chance to flee
                    {
                        if (actor.Class == ClassType.Human || actor.Class == ClassType.WildCard || actor.Class == ClassType.PersonaUser || actor.Class == ClassType.Operator || actor.Class == ClassType.Avatar)
                            return TurnStartResult.FleeBattle;

                        if (actor.Class == ClassType.Demon)
                            return TurnStartResult.ReturnToCOMP;
                    }
                    return fearRoll < 55 ? TurnStartResult.Skip : TurnStartResult.CanAct; // 40% skip turn

                case "ConfusedAction":
                    return TurnStartResult.ForcedConfusion;

                case "ForceAttack":
                    return TurnStartResult.ForcedPhysical;

                default:
                    return TurnStartResult.CanAct;
            }
        }

        /// <summary>
        /// Handles turn-end logic including Poison damage and recovery.
        /// Distressed, Weak, etc., are handled by CombatMath, but this manages their duration.
        /// </summary>
        public List<string> ProcessTurnEnd(Combatant actor)
        {
            List<string> logs = new List<string>();
            if (actor.CurrentAilment == null) return logs;

            AilmentData ailment = actor.CurrentAilment;

            // 1. Handle Poison DOT (Legacy formula restored)
            if (ailment.DotPercent > 0)
            {
                int damage = (int)(actor.MaxHP * 0.13); // Fixed 13% per legacy requirement
                if (damage < 1) damage = 1;

                actor.CurrentHP -= damage;

                // Rule: Poison cannot kill a combatant; it leaves them at 1 HP.
                // Replaced "Shadow" name check with a universal non-lethal check.
                if (actor.CurrentHP < 1)
                {
                    actor.CurrentHP = 1;
                }

                logs.Add($"{actor.Name} is hurt by {ailment.Name}! ({damage} DMG)");
            }

            // 2. Immediate Removal Triggers
            if (ailment.RemovalTriggers.Contains("OneTurn"))
            {
                actor.RemoveAilment();
                logs.Add($"{actor.Name} is no longer {ailment.Name}.");
                return logs;
            }

            // 3. Natural Recovery
            if (ailment.RemovalTriggers.Contains("NaturalRoll"))
            {
                int recoveryChance = 20 + (actor.GetStat(StatType.LUK) / 2);
                if (_rnd.Next(0, 100) < recoveryChance)
                {
                    actor.RemoveAilment();
                    logs.Add($"{actor.Name} recovered from {ailment.Name}!");
                    return logs;
                }
            }

            // 4. Turn Decay
            actor.AilmentDuration--;
            if (actor.AilmentDuration <= 0)
            {
                actor.RemoveAilment();
                logs.Add($"{actor.Name}'s {ailment.Name} wore off.");
            }

            return logs;
        }

        /// <summary>
        /// Applies stat changes with a strict [-4, 4] stacking cap.
        /// Parses word-by-word to handle Omni-buffs.
        /// </summary>
        public void ApplyStatChange(string skillName, Combatant target)
        {
            string skill = skillName.ToLower();
            bool isBuff = skill.EndsWith("kaja") || skill == "heat riser";
            bool isDebuff = skill.EndsWith("nda") || skill == "debilitate";

            // Omni-Modifiers
            if (skill == "heat riser") { ChangeBuff(target, "Attack", 1); ChangeBuff(target, "Defense", 1); ChangeBuff(target, "Agility", 1); return; }
            if (skill == "debilitate") { ChangeBuff(target, "Attack", -1); ChangeBuff(target, "Defense", -1); ChangeBuff(target, "Agility", -1); return; }

            // Root Parsing
            if (skill.Contains("taru")) ChangeBuff(target, "Attack", isBuff ? 1 : -1);
            if (skill.Contains("raku")) ChangeBuff(target, "Defense", isBuff ? 1 : -1);
            if (skill.Contains("suku")) ChangeBuff(target, "Agility", isBuff ? 1 : -1);
        }

        private void ChangeBuff(Combatant target, string stat, int delta)
        {
            int current = target.Buffs.GetValueOrDefault(stat, 0);
            int newVal = Math.Clamp(current + delta, -4, 4);
            target.Buffs[stat] = newVal;
        }

    }
}