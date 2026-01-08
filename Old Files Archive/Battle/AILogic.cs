using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle
{
    public class AILogic
    {
        private readonly Random _rnd = new Random();

        /// <summary>
        /// Selects the optimal skill and target list for an AI combatant.
        /// Utilizes BattleKnowledge to avoid losing turn icons.
        /// </summary>
        public (SkillData skill, List<Combatant> targets) DetermineAction(Combatant actor, List<Combatant> allies, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            // Gather all skills the combatant can currently pay for.
            var usableSkills = actor.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var data) ? data : null)
                .Where(d => d != null && HasResources(actor, d))
                .ToList();

            // 1. Check for Emergency Healing.
            var dyingAlly = allies.FirstOrDefault(a => (double)a.CurrentHP / a.MaxHP < 0.35);
            if (dyingAlly != null)
            {
                var healSkill = usableSkills.FirstOrDefault(s => s.Category.Contains("Recovery"));
                if (healSkill != null)
                {
                    return (healSkill, new List<Combatant> { dyingAlly });
                }
            }

            // 2. Exploit Known Weaknesses.
            foreach (var target in opponents.Where(o => !o.IsDead))
            {
                foreach (var skill in usableSkills)
                {
                    Element elem = ElementHelper.FromCategory(skill.Category);
                    if (knowledge.IsWeaknessKnown(target.SourceId, elem))
                    {
                        return (skill, new List<Combatant> { target });
                    }
                }
            }

            // 3. Filter out dangerous skills (those known to hit Null/Repel/Absorb).
            var safeSkills = new List<SkillData>();
            foreach (var skill in usableSkills)
            {
                Element e = ElementHelper.FromCategory(skill.Category);
                bool isDangerous = false;
                foreach (var target in opponents)
                {
                    if (knowledge.IsResistanceKnown(target.SourceId, e))
                    {
                        isDangerous = true;
                        break;
                    }
                }
                if (!isDangerous)
                {
                    safeSkills.Add(skill);
                }
            }

            // 4. Select a random safe skill, or fallback to Basic Attack.
            SkillData selected = null;
            if (safeSkills.Count > 0)
            {
                selected = safeSkills[_rnd.Next(safeSkills.Count)];
            }

            if (selected == null)
            {
                // Fallback: Perform basic attack on a random opponent.
                return (null, new List<Combatant> { opponents[_rnd.Next(opponents.Count)] });
            }

            // 5. Construct the final target list.
            List<Combatant> finalTargets = new List<Combatant>();
            if (selected.Effect.Contains("all", StringComparison.OrdinalIgnoreCase))
            {
                finalTargets.AddRange(opponents.Where(o => !o.IsDead));
            }
            else
            {
                finalTargets.Add(opponents[_rnd.Next(opponents.Count)]);
            }

            return (selected, finalTargets);
        }

        private bool HasResources(Combatant c, SkillData s)
        {
            var cost = s.ParseCost();
            if (cost.isHP)
            {
                return c.CurrentHP > cost.value;
            }
            return c.CurrentSP >= cost.value;
        }
    }
}