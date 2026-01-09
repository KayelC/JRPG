using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Cognitive Layer of the Battle Sub-System.
    /// Determines the optimal action for an AI combatant by analyzing affinities,
    /// battle knowledge, and current ailment restrictions.
    /// </summary>
    public class BehaviorEngine
    {
        private readonly Random _rnd = new Random();
        private readonly StatusRegistry _statusRegistry;

        public BehaviorEngine(StatusRegistry statusRegistry)
        {
            _statusRegistry = statusRegistry;
        }

        /// <summary>
        /// The primary entry point for AI decision making.
        /// Adheres to SMT III priority: Ailment Override > Recovery > Weakness Exploitation > Risk Aversion.
        /// </summary>
        public (SkillData skill, List<Combatant> targets) DetermineBestAction(
            Combatant actor,
            List<Combatant> allies,
            List<Combatant> opponents,
            BattleKnowledge knowledge)
        {
            // Ailment Hijack Logic (Highest Priority)
            // We check the registry to see if the actor's turn is being dictated by an ailment.
            TurnStartResult turnState = _statusRegistry.ProcessTurnStart(actor);

            if (turnState == TurnStartResult.ForcedPhysical)
            {
                // Rage Logic: Perform a basic attack on a random enemy.
                return (null, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
            }

            if (turnState == TurnStartResult.ForcedConfusion)
            {
                // Charm Logic: Sabotage the team by healing enemies or attacking allies.
                return DetermineConfusedAction(actor, allies, opponents);
            }

            // Gather Usable Skills
            // Only consider skills the actor has the HP/SP to cast.
            var usableSkills = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && CanAfford(actor, d))
                .ToList();

            // 1. Emergency Recovery (Filter for LIVE allies only)
            var dyingAlly = allies.FirstOrDefault(a => !a.IsDead && (double)a.CurrentHP / a.MaxHP < 0.35);
            if (dyingAlly != null)
            {
                var healSkill = usableSkills.FirstOrDefault(s => s.Category.Contains("Recovery") && !s.Effect.Contains("Revive"));
                if (healSkill != null)
                {
                    return (healSkill, DetermineTargetList(actor, dyingAlly, allies, opponents, healSkill));
                }
            }

            // 2. Smart Curing (Check if skill actually cures the active ailment)
            var ailedAlly = allies.FirstOrDefault(a => !a.IsDead && a.CurrentAilment != null);
            if (ailedAlly != null)
            {
                var cureSkill = usableSkills.FirstOrDefault(s => s.Effect.Contains("Cure") || s.Effect.Contains(ailedAlly.CurrentAilment.Name));
                if (cureSkill != null)
                {
                    return (cureSkill, new List<Combatant> { ailedAlly });
                }
            }

            // Tactical Filtering
            // Filter out skills that target elements the AI KNOWS will result in a -4 Icon penalty or Phase Termination.
            var safeSkills = usableSkills.Where(s => !IsKnownRisk(s, opponents, knowledge)).ToList();

            // Weakness Hunting
            // Look for a skill that hits a known weakness of at least one enemy.
            foreach (var skill in safeSkills)
            {
                if (IsOffensive(skill))
                {
                    Element element = ElementHelper.FromCategory(skill.Category);
                    foreach (var target in opponents.Where(o => !o.IsDead))
                    {
                        if (knowledge.IsWeaknessKnown(target.SourceId, element))
                        {
                            return (skill, DetermineTargetList(actor, target, allies, opponents, skill));
                        }
                    }
                }
            }

            // Rigid Body Exploitation
            var rigidTarget = opponents.FirstOrDefault(o => !o.IsDead && o.IsRigidBody);
            if (rigidTarget != null)
            {
                var physSkill = safeSkills.FirstOrDefault(s => IsPhysical(s));
                if (physSkill != null) return (physSkill, DetermineTargetList(actor, rigidTarget, allies, opponents, physSkill));
                
                // Fallback to basic attack if it's physical.
                return (null, new List<Combatant> { rigidTarget });
            }

            // Support & Buffing
            var supportSkill = safeSkills.FirstOrDefault(s => s.Category.Contains("Enhance"));
            if (supportSkill != null && _rnd.Next(0, 100) < 40)
            {
                return (supportSkill, DetermineTargetList(actor, null, allies, opponents, supportSkill));
            }

            // Default Action
            // Choose a random safe skill or perform a basic attack.
            if (safeSkills.Count > 0 && _rnd.Next(0, 100) < 70)
            {
                var randomSkill = safeSkills[_rnd.Next(safeSkills.Count)];
                return (randomSkill, DetermineTargetList(actor, null, allies, opponents, randomSkill));
            }

            // Fallback: Attack random enemy.
            return (null, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
        }

        /// <summary>
        /// SMT III High Fidelity: Determines action when Charmed.
        /// Sabotages the team by healing opponents or striking allies.
        /// </summary>
        private (SkillData, List<Combatant>) DetermineConfusedAction(Combatant actor, List<Combatant> allies, List<Combatant> opponents)
        {
            var skills = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && CanAfford(actor, d))
                .ToList();

            // 50% Chance: Aid the enemies
            if (_rnd.Next(0, 100) < 50)
            {
                var healSkill = skills.FirstOrDefault(s => s.Category.Contains("Recovery") && !s.Effect.Contains("Revive"));
                if (healSkill != null) return (healSkill, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
            }

            // 50% Chance (or fallback): Attack a random ally
            // Default to basic physical attack for sabotage
            return (null, new List<Combatant> { allies[_rnd.Next(allies.Count)] });
        }

        /// <summary>
        /// Precision Target Identification.
        /// Handles Ma-/Me- prefixes and specific keyword boundaries (Party/All Allies) to avoid greedy string matches.
        /// </summary>
        private List<Combatant> DetermineTargetList(Combatant actor, Combatant primaryTarget, List<Combatant> allies, List<Combatant> opponents, SkillData skill)
        {
            List<Combatant> targets = new List<Combatant>();
            string nameLower = skill.Name.ToLower();
            string effectLower = skill.Effect.ToLower();

            // Refined Multi-Target Detection
            // Checks for Maha (Ma-) or Media (Me-) prefixes, or explicit group keywords.
            // Avoids the "Heal 1 ally" greedy bug by checking for "all allies" or "party".
            bool isMulti = nameLower.StartsWith("ma") ||
                           nameLower.StartsWith("me") ||
                           effectLower.Contains("all foes") ||
                           effectLower.Contains("all allies") ||
                           effectLower.Contains("party");

            // Identify which side the skill is intended for
            bool targetsAllies = skill.Category.Contains("Recovery") ||
                                 skill.Category.Contains("Enhance") ||
                                 effectLower.Contains("ally") ||
                                 effectLower.Contains("party");

            var side = targetsAllies ? allies : opponents;

            if (isMulti)
            {
                // Multi-target skills only target living members unless it's a Revive skill
                targets.AddRange(side.Where(s => skill.Effect.Contains("Revive") ? s.IsDead : !s.IsDead));
            }
            else
            {
                // Single-target skills: use the tactically chosen target or random from side
                targets.Add(primaryTarget ?? side[_rnd.Next(side.Count)]);
            }

            return targets;
        }

        /// <summary>
        /// Checks if an offensive skill poses a risk of losing icons based on persistent player knowledge.
        /// </summary>
        private bool IsKnownRisk(SkillData skill, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            if (!IsOffensive(skill)) return false;
            Element element = ElementHelper.FromCategory(skill.Category);
            return opponents.Any(target => !target.IsDead && knowledge.IsResistanceKnown(target.SourceId, element));
        }

        private bool CanAfford(Combatant actor, SkillData skill)
        {
            var cost = skill.ParseCost();
            if (cost.isHP)
            {
                int hpRequirement = (int)(actor.MaxHP * (cost.value / 100.0));
                return actor.CurrentHP > hpRequirement;
            }
            return actor.CurrentSP >= cost.value;
        }

        private bool IsOffensive(SkillData skill)
        {
            string cat = skill.Category.ToLower();
            return !cat.Contains("recovery") && !cat.Contains("enhance");
        }

        private bool IsPhysical(SkillData skill)
        {
            Element element = ElementHelper.FromCategory(skill.Category);
            return element == Element.Slash || element == Element.Strike || element == Element.Pierce;
        }
    }
}