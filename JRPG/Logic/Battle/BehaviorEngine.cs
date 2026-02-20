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
    /// Determines the optimal action for an AI combatant using a Unified Tactical Model.
    /// Prioritizes Press Turn maximization and lethal efficiency over defensive spam.
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
        /// Implements the Tiered Priority Ladder: Kill > Weakness > Crisis > Rigid > Pass > Pressure.
        /// </summary>
        public (SkillData skill, List<Combatant> targets) DetermineBestAction(
            Combatant actor,
            List<Combatant> allies,
            List<Combatant> opponents,
            BattleKnowledge knowledge,
            int fullIcons,
            int blinkingIcons)
        {
            // --- Step 1: Ailment Hijack Logic (Highest Priority) ---
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

            // --- STEP 2: GATHER AND FILTER VALID ACTIONS (Effectiveness Gate) ---
            // We get all skills + Basic Attack (null)
            var actionPool = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && d.Category != "Passive Skills" && CanAfford(actor, d))
                .ToList();

            // Perform the "Effectiveness Gate" check to prune useless moves
            var validSkills = actionPool.Where(s => IsActionEffective(actor, s, allies, opponents, knowledge)).ToList();

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
                        int estDmg = CombatMath.CalculateDamage(actor, target, skill.GetPowerVal(), ElementHelper.FromCategory(skill.Category), out _);
                        if (estDmg >= target.CurrentHP)
                        {
                            return (skill, targets);
                        }
                    }
                }
            }

            // TIER 2: PRESS TURN EXPLOITATION
            // Hunt for known weaknesses to gain extra actions.
            foreach (var skill in validSkills)
            {
                if (IsOffensive(skill))
                {
                    Element element = ElementHelper.FromCategory(skill.Category);
                    var targets = DetermineTargetList(actor, null, allies, opponents, skill);
                    if (targets.Any(t => knowledge.IsWeaknessKnown(t.SourceId, element)))
                    {
                        // SMT Rule: Only use if NO target in the list is known to Null/Repel/Absorb
                        if (!targets.Any(t => knowledge.IsResistanceKnown(t.SourceId, element)))
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
                var healSkill = validSkills.FirstOrDefault(s => s.Category.Contains("Recovery") && !s.Effect.Contains("Revive"));
                if (healSkill != null)
                {
                    return (healSkill, DetermineTargetList(actor, criticalAlly, allies, opponents, healSkill));
                }
            }

            // TIER 4: CRITICAL FISHING (RIGID TARGETS)
            // If an enemy is Frozen or Shocked, Physical attacks are 100% Critical.
            var rigidTarget = opponents.FirstOrDefault(o => !o.IsDead && o.IsRigidBody);
            if (rigidTarget != null)
            {
                var physSkill = validSkills.FirstOrDefault(s => IsPhysical(s));
                if (physSkill != null) return (physSkill, DetermineTargetList(actor, rigidTarget, allies, opponents, physSkill));
                // Basic Attack (Physical)
                return (null, new List<Combatant> { rigidTarget });
            }

            // TIER 5: INFORMED PASS (Strategic Rotation)
            // Condition: Lead is Solid [O], total icons > 1, and an ally behind has a weakness to exploit.
            if (fullIcons > 0 && (fullIcons + blinkingIcons) > 1)
            {
                bool allyHasAdvantage = allies.Any(a => a != actor && !a.IsDead && HasKnownAdvantage(a, opponents, knowledge));
                if (allyHasAdvantage)
                {
                    // Return a "Pass" signal. Our conductor interprets (null, empty list) as Pass.
                    return (null, new List<Combatant>());
                }
            }

            // TIER 6: STANDARD PRESSURE
            // Use highest power offensive skill that isn't a known risk.
            var offensiveOptions = validSkills.Where(s => IsOffensive(s)).OrderByDescending(s => s.GetPowerVal()).ToList();
            if (offensiveOptions.Any())
            {
                var bestSkill = offensiveOptions.First();
                return (bestSkill, DetermineTargetList(actor, null, allies, opponents, bestSkill));
            }

            // TIER 7: DESPERATION
            // Default to basic attack on random enemy.
            return (null, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
        }

        /// <summary>
        /// Sabotages the team when Charmed.
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
        /// The Effectiveness Gate: Prevents the AI from taking turns that do nothing.
        /// </summary>
        private bool IsActionEffective(Combatant actor, SkillData skill, List<Combatant> allies, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            // Basic attacks are always "effective" as a baseline.
            if (skill == null) return true;

            string name = skill.Name.ToLower();

            // 1. Healing check: Don't heal if targets are > 70% HP
            if (skill.Category.Contains("Recovery") && !skill.Effect.Contains("Revive"))
            {
                return allies.Any(a => !a.IsDead && (double)a.CurrentHP / a.MaxHP < 0.70);
            }

            // 2. Buff/Debuff check: Don't buff if at +3, don't debuff if at -3.
            bool isBuff = name.EndsWith("kaja") || name == "heat riser";
            bool isDebuff = name.EndsWith("nda") || name == "debilitate";

            if (isBuff) return allies.Any(a => !a.IsDead && a.Buffs.Values.Any(v => v < 3));
            if (isDebuff) return opponents.Any(o => !o.IsDead && o.Buffs.Values.Any(v => v > -3));

            // 3. Risk check: Don't use elements known to be Null/Repel/Absorb
            if (IsOffensive(skill))
            {
                Element element = ElementHelper.FromCategory(skill.Category);
                if (opponents.Any(o => !o.IsDead && knowledge.IsResistanceKnown(o.SourceId, element)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasKnownAdvantage(Combatant ally, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            var skills = ally.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && d.Category != "Passive Skills");

            foreach (var skill in skills)
            {
                Element e = ElementHelper.FromCategory(skill.Category);
                if (opponents.Any(o => !o.IsDead && knowledge.IsWeaknessKnown(o.SourceId, e))) return true;
            }
            return false;
        }

        private List<Combatant> DetermineTargetList(Combatant actor, Combatant primaryTarget, List<Combatant> allies, List<Combatant> opponents, SkillData skill)
        {
            List<Combatant> targets = new List<Combatant>();
            if (skill == null) // Basic Attack
            {
                targets.Add(primaryTarget ?? opponents[_rnd.Next(opponents.Count)]);
                return targets;
            }

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
            bool isMulti = nameLower.StartsWith("ma") ||
                           nameLower.StartsWith("me") ||
                           effectLower.Contains("all foes") ||
                           effectLower.Contains("all allies") ||
                           effectLower.Contains("party") ||
                           nameLower == "debilitate"; // Debilitate is always Multi-Target

            // --- REFINED SIDE IDENTIFICATION ---
            // SMT Rule: Buffs (kaja/Heat Riser) -> Allies. Debuffs (nda/Debilitate) -> Opponents.
            bool isBuff = nameLower.EndsWith("kaja") || nameLower == "heat riser";
            bool isDebuff = nameLower.EndsWith("nda") || nameLower == "debilitate";

            // Identify which side the skill is intended for
            bool targetsAllies = skill.Category.Contains("Recovery") ||
                                 isBuff ||
                                 effectLower.Contains("ally") ||
                                 effectLower.Contains("party");

            // If it's explicitly a debuff, ensure it targets opponents regardless of category.
            if (isDebuff) targetsAllies = false;

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
            if (skill == null || !IsOffensive(skill)) return false;
            Element element = ElementHelper.FromCategory(skill.Category);
            return opponents.Any(target => !target.IsDead && knowledge.IsResistanceKnown(target.SourceId, element));
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

        private bool IsOffensive(SkillData skill)
        {
            if (skill == null) return true;
            string cat = skill.Category.ToLower();
            return !cat.Contains("recovery") && !cat.Contains("enhance");
        }

        private bool IsPhysical(SkillData skill)
        {
            if (skill == null) return true;
            Element element = ElementHelper.FromCategory(skill.Category);
            return element == Element.Slash || element == Element.Strike || element == Element.Pierce;
        }
    }
}
