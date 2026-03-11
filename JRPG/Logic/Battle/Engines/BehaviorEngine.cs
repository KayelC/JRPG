using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Battle;           // For CombatMath
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger (used in StatusRegistry)

namespace JRPGPrototype.Logic.Battle.Engines
{
    /// <summary>
    /// The Cognitive Layer of the Battle Sub-System.
    /// Determines the optimal action for an AI combatant using a Unified Tactical Model.
    /// Prioritizes Press Turn maximization, Technical (Rigid Body) exploitation, and lethal efficiency.
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
        /// Implements the Tiered Priority Ladder: Kill > Weakness/Rigid > Crisis > Rigid (Ailment) > Pass > Pressure.
        /// </summary>
        public (SkillData? skill, List<Combatant> targets) DetermineBestAction(
            Combatant actor,
            List<Combatant> allies,
            List<Combatant> opponents,
            BattleKnowledge knowledge,
            int fullIcons,
            int blinkingIcons)
        {
            // --- Step 1: Ailment Hijack Logic (Highest Priority) ---
            TurnStartResult turnState = _statusRegistry.ProcessTurnStart(actor);

            if (turnState == TurnStartResult.ForcedPhysical)
            {
                // Rage Logic: Perform a basic attack.
                // Uses DetermineTargetList to ensure we respect targeting rules even while Enraged.
                var forcedTargets = DetermineTargetList(actor, null, allies, opponents, null);
                return (null, forcedTargets);
            }

            if (turnState == TurnStartResult.ForcedConfusion)
            {
                // Charm Logic: Sabotage the team by healing enemies or attacking allies.
                return DetermineConfusedAction(actor, allies, opponents);
            }

            // --- STEP 2: GATHER AND FILTER VALID ACTIONS (Effectiveness Gate) ---
            // We get all skills + Basic Attack (null)
            var actionPool = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && d.Category != "Passive Skills" && CanAfford(actor, d))
                .ToList();

            // Add basic attack to the potential pool
            actionPool.Add(null);

            // Perform the "Effectiveness Gate" check to prune redundant moves.
            var validSkills = actionPool.Where(s =>
            {
                if (s == null) return true;
                var targets = DetermineTargetList(actor, null, allies, opponents, s);
                return !_statusRegistry.IsActionRedundant(actor, s, targets);
            }).ToList();

            // --- STEP 3: THE PRIORITY LADDER ---

            // TIER 1: THE KILL-SHOT
            // If any valid attack can reduce an opponent to 0 HP, take it.
            foreach (var skill in validSkills)
            {
                if (IsOffensive(skill))
                {
                    var targets = DetermineTargetList(actor, null, allies, opponents, skill);
                    foreach (var target in targets)
                    {
                        int estDmg = CombatMath.CalculateDamage(actor, target,
                            skill?.GetPowerVal() ?? 15, // 15 is standard melee power
                            skill != null ? ElementHelper.FromCategory(skill.Category) : actor.WeaponElement,
                            out _);

                        if (estDmg >= target.CurrentHP)
                        {
                            return (skill, targets);
                        }
                    }
                }
            }

            // TIER 2: PRESS TURN & RIGID EXPLOITATION
            foreach (var skill in validSkills)
            {
                if (IsOffensive(skill))
                {
                    Element element = skill != null ? ElementHelper.FromCategory(skill.Category) : actor.WeaponElement;
                    var targets = DetermineTargetList(actor, null, allies, opponents, skill);

                    bool hasRigidExploit = targets.Any(t => t.IsRigidBody && IsPhysical(skill));
                    bool hasWeaknessPotential = targets.Any(t => knowledge.IsWeaknessKnown(t.SourceId, element));

                    if (hasRigidExploit || hasWeaknessPotential)
                    {
                        // Risk Aversion: Only use if NO target in the list is known to resist/nullify
                        if (!IsKnownRisk(skill, targets, knowledge))
                        {
                            return (skill, targets);
                        }
                    }
                }
            }

            // TIER 3: CRISIS RECOVERY
            // Heal allies who are below 35% HP.
            var criticalAlly = allies.FirstOrDefault(a => !a.IsDead && (double)a.CurrentHP / a.MaxHP < 0.35);
            if (criticalAlly != null)
            {
                var healSkill = validSkills.FirstOrDefault(s => s != null &&
                    s.Category.Contains("Recovery") && !s.Effect.Contains("Revive"));

                if (healSkill != null)
                {
                    return (healSkill, DetermineTargetList(actor, criticalAlly, allies, opponents, healSkill));
                }
            }

            // TIER 4: CRITICAL FISHING (RIGID TARGETS)
            // If an enemy is Frozen, Shocked, Bound or Stunned Physical attacks are 100% Critical.
            var rigidTarget = opponents.FirstOrDefault(o => !o.IsDead && o.IsRigidBody);
            if (rigidTarget != null)
            {
                var physSkill = validSkills.FirstOrDefault(s => s != null && IsPhysical(s));
                if (physSkill != null) return (physSkill, DetermineTargetList(actor, rigidTarget, allies, opponents, physSkill));

                // Basic Attack (Physical)
                return (null, new List<Combatant> { rigidTarget });
            }

            // TIER 5: INFORMED PASS (Strategic Rotation)
            // Condition: Lead is Solid [O], total icons > 1, and an ally behind has a weakness to exploit.
            if (fullIcons > 0 && (fullIcons + blinkingIcons) > 1)
            {
                bool allyHasAdvantage = allies.Any(a => a != actor && !a.IsDead &&
                    HasKnownAdvantage(a, opponents, knowledge));

                if (allyHasAdvantage)
                {
                    // Return a "Pass" signal. (null, empty list)
                    return (null, new List<Combatant>());
                }
            }

            // TIER 6: STANDARD PRESSURE
            var offensiveOptions = validSkills.Where(s => IsOffensive(s))
                .OrderByDescending(s => s?.GetPowerVal() ?? 15).ToList();

            if (offensiveOptions.Any())
            {
                var bestSkill = offensiveOptions.First();
                if (!IsKnownRisk(bestSkill, opponents, knowledge))
                {
                    return (bestSkill, DetermineTargetList(actor, null, allies, opponents, bestSkill));
                }
            }

            // TIER 7: DESPERATION
            // Default to basic attack on target chosen by DetermineTargetList (respects Sleep)
            return (null, DetermineTargetList(actor, null, allies, opponents, null));
        }

        // Checks if an offensive skill poses a risk of losing icons based on persistent player knowledge.
        private bool IsKnownRisk(SkillData? skill, List<Combatant> currentTargets, BattleKnowledge knowledge)
        {
            if (skill == null || !IsOffensive(skill)) return false;
            Element element = ElementHelper.FromCategory(skill.Category);
            // Rule: One Repel/Absorb/Null in a group skill kills the turn.
            return currentTargets.Any(target => !target.IsDead && knowledge.IsResistanceKnown(target.SourceId, element));
        }

        private (SkillData? skill, List<Combatant> targets) DetermineConfusedAction(Combatant actor, List<Combatant> allies, List<Combatant> opponents)
        {
            var skills = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && CanAfford(actor, d))
                .ToList();

            // 50% Chance: Aid the enemies
            if (_rnd.Next(0, 100) < 50)
            {
                var healSkill = skills.FirstOrDefault(s => s != null &&
                    s.Category.Contains("Recovery") && !s.Effect.Contains("Revive"));

                if (healSkill != null)
                    return (healSkill, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
            }

            // 50% Chance (or fallback): Attack a random ally
            // Default to basic physical attack for sabotage
            return (null, new List<Combatant> { allies[_rnd.Next(allies.Count)] });
        }

        private bool HasKnownAdvantage(Combatant ally, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            var skills = ally.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && d.Category != "Passive Skills");

            foreach (var skill in skills)
            {
                Element e = ElementHelper.FromCategory(skill!.Category);
                if (opponents.Any(o => !o.IsDead && knowledge.IsWeaknessKnown(o.SourceId, e))) return true;
            }
            return false;
        }

        private List<Combatant> DetermineTargetList(Combatant actor, Combatant? primaryTarget, List<Combatant> allies, List<Combatant> opponents, SkillData? skill)
        {
            List<Combatant> targets = new List<Combatant>();

            // Basic Attack Early Return
            if (skill == null)
            {
                targets.Add(primaryTarget ?? opponents[_rnd.Next(opponents.Count)]);
                return targets;
            }
            // Determine side and target type
            bool targetsAllies = false;
            bool isMulti = false;

            if (skill != null)
            {
            string nameLower = skill.Name.ToLower();
            string effectLower = skill.Effect.ToLower();

            // Self-Targeting logic for Charge skills
            if (nameLower.Contains("charge"))
            {
                return new List<Combatant> { actor };
            }

            // --- REFINED MULTI-TARGET LOGIC ---
                // Checks for Maha (Ma-) or Media (Me-) prefixes, or explicit group keywords.
                // Avoids the "Heal 1 ally" greedy bug by checking for "all allies" or "party".
                isMulti = nameLower.StartsWith("ma") ||
                           nameLower.StartsWith("me") ||
                           effectLower.Contains("all foes") ||
                           effectLower.Contains("all allies") ||
                           effectLower.Contains("party") ||
                               nameLower == "debilitate"; // Debilitate is always Multi-Target

            // --- REFINED SIDE IDENTIFICATION ---
                // Buffs (kaja/Heat Riser) -> Allies. Debuffs (nda/Debilitate) -> Opponents.
                bool isBuff = nameLower.EndsWith("kaja") || nameLower == "heat riser";
            bool isDebuff = nameLower.EndsWith("nda") || nameLower == "debilitate";

                // Identify which side the skill is intended for
                targetsAllies = skill.Category.Contains("Recovery") ||
                                 isBuff ||
                                 effectLower.Contains("ally") ||
                                 effectLower.Contains("party");

                // If it's explicitly a debuff, ensure it targets opponents regardless of category.
            if (isDebuff) targetsAllies = false;
            }

            var side = targetsAllies ? allies : opponents;

            if (isMulti)
            {
                // Multi-target skills only target living members unless it's a Revive skill
                targets.AddRange(side.Where(s => skill.Effect.Contains("Revive") ? s.IsDead : !s.IsDead));
                return targets;
            }
            else
            {
                // Single-target skills: use the tactically chosen target or random from side
                targets.Add(primaryTarget ?? side[_rnd.Next(side.Count)]);
            }

            // AI: Smart Targeting
            if (!targetsAllies)
            {
                // 1. Rigid Exploitation: Prioritize Rigid targets for Physical hits
                if (IsPhysical(skill))
                {
                    var techTarget = side.FirstOrDefault(s => !s.IsDead && s.IsRigidBody);
                    if (techTarget != null)
                    {
                        targets.Add(techTarget);
                        return targets;
                    }
                }

                // 2. Crowd Control Awareness: Avoid Sleepers if others are available
                var nonSleepers = side.Where(s => !s.IsDead && s.CurrentAilment?.Name != "Sleep").ToList();
                if (nonSleepers.Any())
                {
                    targets.Add(nonSleepers[_rnd.Next(nonSleepers.Count)]);
                }
                else
                {
                    // Everyone is asleep - forced to wake one up
                    var anyTarget = side.Where(s => !s.IsDead).ToList();
                    if (anyTarget.Any()) targets.Add(anyTarget[_rnd.Next(anyTarget.Count)]);
                }
            }
            else
            {
                // Ally selection: If it's a revive, pick a dead one. Otherwise, pick living.
                var validSide = skill.Effect.Contains("Revive") ? side.Where(s => s.IsDead).ToList() : side.Where(s => !s.IsDead).ToList();
                if (validSide.Any()) targets.Add(validSide[_rnd.Next(validSide.Count)]);
            }

            return targets;
        }

        private bool CanAfford(Combatant actor, SkillData skill)
        {
            if (skill == null) return true;
            var cost = skill.ParseCost();
            if (cost.isHP)
            {
                int hpRequirement = (int)(actor.MaxHP * (cost.value / 100.0));
                return actor.CurrentHP > hpRequirement;
            }
            return actor.CurrentSP >= cost.value;
        }

        private bool IsOffensive(SkillData? skill)
        {
            if (skill == null) return true;
            string cat = skill.Category.ToLower();
            return !cat.Contains("recovery") && !cat.Contains("enhance");
        }

        private bool IsPhysical(SkillData? skill)
        {
            if (skill == null) return true;
            Element element = ElementHelper.FromCategory(skill.Category);
            return element == Element.Slash || element == Element.Strike || element == Element.Pierce;
        }
    }
}