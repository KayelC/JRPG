using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle; // For CombatMath
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Engines
{
    /// <summary>
    /// The authoritative logic engine for status ailments and stat modifications.
    /// Manages application, turn-start restrictions, and turn-end recovery/damage.
    /// Handles the lifecycle of Passive Skills including Auto-Kaja and Regenerates.
    /// </summary>
    public class StatusRegistry
    {
        private readonly Random _rnd = new Random();
        private IBattleMessenger? _messenger;

        // Allows the conductor to inject the shared communication mediator.
        public void SetMessenger(IBattleMessenger messenger)
        {
            _messenger = messenger;
        }

        /// <summary>
        /// Centralized "Effectiveness Gate" logic.
        /// Returns true if the action would result in zero change to the targets.
        /// </summary>
        public bool IsActionRedundant(Combatant actor, SkillData skill, List<Combatant> targets)
        {
            if (skill == null) return false;

            string effect = skill.Effect.ToLower();
            string category = skill.Category.ToLower();

            // --- RULE 0: Damaging Skills are NEVER Redundant ---
            // If the skill has a power value, the primary intent is damage. 
            // For example : "Toxic Sting" isn't blocked just because the target is already poisoned.
            if (skill.Power != "-" && skill.Power != "NaN")
            {
                return false;
            }

            // 1. Ailment Redundancy
            // Search if we are trying to inflict an ailment the target already has
            foreach (var ailment in Database.Ailments.Values)
            {
                if (effect.Contains(ailment.Name.ToLower()))
                {
                    // If ALL targets already have this specific ailment, it's redundant.
                    if (targets.All(t => t.CurrentAilment?.Name == ailment.Name))
                    {
                        return true;
                    }
                }
            }

            // 2. Recovery Redundancy (HP/SP)
            if (category.Contains("recovery") && !effect.Contains("revive") && !effect.Contains("cure") && !effect.Contains("dispel"))
            {
                bool isSpHeal = effect.Contains("sp") || effect.Contains("spirit");
                if (isSpHeal)
                {
                    if (targets.All(t => t.CurrentSP >= t.MaxSP)) return true;
                }
                else
                {
                    if (targets.All(t => t.CurrentHP >= t.MaxHP)) return true;
                }
            }

            // 3. Cure Redundancy
            if (effect.Contains("cure") || effect.Contains("dispel") || effect.Contains("patra"))
            {
                // Redundant if none of the targets have an ailment to remove
                if (targets.All(t => t.CurrentAilment == null)) return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to inflict an ailment on a target.
        /// Parses probability using Regex and matches against the status_ailments.json library.
        /// </summary>
        public bool TryInflict(Combatant attacker, Combatant target, string skillEffect)
        {
            if (string.IsNullOrEmpty(skillEffect) || target.IsDead) return false;

            // --- PASSIVE TRIGGER: Ailment Protection ---
            // Unshaken Will prevents all mental ailments from succeeding.
            var targetPassives = target.GetConsolidatedSkills();
            if (targetPassives.Contains("Unshaken Will"))
            {
                return false;
            }

            AilmentData? ailmentToApply = null;
            foreach (var ailment in Database.Ailments.Values)
            {
                if (skillEffect.Contains(ailment.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ailmentToApply = ailment;
                    break;
                }
            }

            if (ailmentToApply == null) return false;

            // FIX: Match the pattern "(XX% chance)" as seen in skills_database.json
            int baseChance = 100;
            Match match = Regex.Match(skillEffect, @"\((\d+)%");
            if (match.Success)
            {
                baseChance = int.Parse(match.Groups[1].Value);
            }

            int finalChance = baseChance + (attacker.GetStat(StatType.Lu) - target.GetStat(StatType.Lu));

            if (_rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 95))
            {
                bool success = target.InflictAilment(ailmentToApply, 3);
                if (success && _messenger != null)
                {
                    _messenger.Publish($"{target.Name} was inflicted with {ailmentToApply.Name}!", ConsoleColor.Magenta);
                }
                return success;
            }

            return false;
        }

        /// <summary>
        /// Curing Logic. 
        /// Checks if the skill effect explicitly lists the target's current ailment or uses "Cure all".
        /// </summary>
        public bool CheckAndExecuteCure(Combatant target, string skillEffect)
        {
            if (target.CurrentAilment == null) return false;

            string effectLower = skillEffect.ToLower();
            bool curesAll = effectLower.Contains("cure all") ||
                           effectLower.Contains("cures all") ||
                           effectLower.Contains("amrita") ||
                           effectLower.Contains("salvation");

            if (curesAll || effectLower.Contains(target.CurrentAilment.Name.ToLower()) ||
                effectLower.Contains("dispel") || effectLower.Contains("dispels"))
            {
                target.RemoveAilment();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes Auto-Kaja passives at the start of battle.
        /// Correctly distinguishes between Single-Target and Party-Wide (Ma) variants.
        /// </summary>
        /// <param name="actor">The owner of the passive skill.</param>
        /// <param name="allies">The list of all living allies on the actor's side.</param>
        public void ProcessInitialPassives(Combatant actor, List<Combatant> allies)
        {
            var skills = actor.GetConsolidatedSkills();

            // 1. Single-Target Auto-Skills (User Only)
            if (skills.Contains("Auto-Tarukaja")) ApplyStatChange("Tarukaja", actor);
            if (skills.Contains("Auto-Rakukaja")) ApplyStatChange("Rakukaja", actor);
            if (skills.Contains("Auto-Sukukaja")) ApplyStatChange("Sukukaja", actor);

            // 2. Party-Wide Auto-Skills (Maha Variants)
            // We iterate through the provided ally list to apply the buff to everyone.
            if (skills.Contains("Auto-Mataru") || skills.Contains("Auto-Maraku") || skills.Contains("Auto-Masuku"))
            {
                foreach (var ally in allies)
                {
                    if (ally.IsDead) continue;

                    if (skills.Contains("Auto-Mataru")) ApplyStatChange("Matarukaja", ally);
                    if (skills.Contains("Auto-Maraku")) ApplyStatChange("Marakukaja", ally);
                    if (skills.Contains("Auto-Masuku")) ApplyStatChange("Masukukaja", ally);
                }
            }
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

                case "LimitedAction":
                    return TurnStartResult.LimitedAction;

                case "ChanceSkip":
                    // Panic: 50% chance to lose turn.
                    return _rnd.Next(0, 100) < 50 ? TurnStartResult.Skip : TurnStartResult.CanAct;

                case "ChanceSkipOrFlee":
                    // Fear logic.
                    int fearRoll = _rnd.Next(0, 100);
                    if (fearRoll < 15) // 15% chance to flee
                    {
                        if (actor.Class != ClassType.Demon)
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
        /// Handles turn-end logic including Poison damage, Recovery rolls, and Passive Restoration.
        /// Distressed, Weak, etc., are handled by CombatMath, but this manages their duration.
        /// </summary>
        public void ProcessTurnEnd(Combatant actor)
        {
            // --- PASSIVE TRIGGER: Turn-End Restoration ---
            var skills = actor.GetConsolidatedSkills();

            // 1. HP Restoration (Regenerate / Spring of Life)
            int hpRecovery = 0;
            if (skills.Contains("Spring of Life")) hpRecovery += (int)(actor.MaxHP * 0.08);

            if (skills.Contains("Regenerate 3")) hpRecovery += (int)(actor.MaxHP * 0.06);
            else if (skills.Contains("Regenerate 2")) hpRecovery += (int)(actor.MaxHP * 0.04);
            else if (skills.Contains("Regenerate 1")) hpRecovery += (int)(actor.MaxHP * 0.02);

            // Sleep restores 10% HP/SP per turn
            if (actor.CurrentAilment?.Name == "Sleep")
            {
                hpRecovery += (int)(actor.MaxHP * 0.10);
            }

            if (hpRecovery > 0 && actor.CurrentHP < actor.MaxHP)
            {
                actor.CurrentHP = Math.Min(actor.MaxHP, actor.CurrentHP + hpRecovery);
                _messenger?.Publish($"{actor.Name} restored {hpRecovery} HP.");
            }

            // 2. SP Restoration (Invigorate)
            int spRecovery = 0;
            if (skills.Contains("Invigorate 3")) spRecovery += 7;
            else if (skills.Contains("Invigorate 2")) spRecovery += 5;
            else if (skills.Contains("Invigorate 1")) spRecovery += 3;

            if (actor.CurrentAilment?.Name == "Sleep")
            {
                spRecovery += (int)(actor.MaxSP * 0.10);
            }

            if (spRecovery > 0 && actor.CurrentSP < actor.MaxSP)
            {
                actor.CurrentSP = Math.Min(actor.MaxSP, actor.CurrentSP + spRecovery);
                _messenger?.Publish($"{actor.Name} restored {spRecovery} SP via passives.");
            }

            if (actor.CurrentAilment == null) return;

            AilmentData ailment = actor.CurrentAilment;

            // Handle Poison DOT
            if (ailment.DotPercent > 0)
            {
                int damage = (int)(actor.MaxHP * 0.13); // Fixed 13% per legacy requirement
                if (damage < 1) damage = 1;

                actor.CurrentHP -= damage;

                // Poison cannot kill a combatant, it leaves them at 1 HP.
                // if (actor.CurrentHP < 1) actor.CurrentHP = 1;
                // I decided to make Poison Lethal by commenting it out, I can always add it back by Uncommenting if needed.

                _messenger?.Publish($"{actor.Name} is hurt by {ailment.Name}! ({damage} DMG)");
            }

            // Immediate Removal Triggers
            if (ailment.RemovalTriggers.Contains("OneTurn"))
            {
                actor.RemoveAilment();
                _messenger?.Publish($"{actor.Name} is no longer {ailment.Name}.");
                return;
            }

            // Natural Recovery (Luck Roll)
            else if (ailment.RemovalTriggers.Contains("NaturalRoll"))
            {
                int recoveryChance = 20 + (actor.GetStat(StatType.Lu) / 2);
                if (_rnd.Next(0, 100) < recoveryChance)
                {
                    actor.RemoveAilment();
                    _messenger?.Publish($"{actor.Name} recovered from {ailment.Name}!");
                    return;
                }
            }

            // Turn Decay
            actor.AilmentDuration--;
            if (actor.AilmentDuration <= 0 && actor.CurrentAilment != null)
            {
                actor.RemoveAilment();
                _messenger?.Publish($"{actor.Name}'s {ailment.Name} wore off.");
            }

            return;
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

            if (!isBuff && !isDebuff) return;

            int delta = isBuff ? 1 : -1;

            // Omni-Modifiers
            if (skill == "heat riser") { ChangeBuff(target, "Attack", 1); ChangeBuff(target, "Defense", 1); ChangeBuff(target, "Agility", 1); return; }
            if (skill == "debilitate") { ChangeBuff(target, "Attack", -1); ChangeBuff(target, "Defense", -1); ChangeBuff(target, "Agility", -1); return; }

            // Root Parsing
            if (skill.Contains("taru")) ChangeBuff(target, "Attack", delta);
            if (skill.Contains("raku")) ChangeBuff(target, "Defense", delta);
            if (skill.Contains("suku")) ChangeBuff(target, "Agility", delta);
        }

        private void ChangeBuff(Combatant target, string stat, int delta)
        {
            int current = target.Buffs.GetValueOrDefault(stat, 0);
            target.Buffs[stat] = Math.Clamp(current + delta, -4, 4);
        }
    }
}