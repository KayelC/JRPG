using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling stat modifications (Buffs and Debuffs).
    /// Works for single-target and multi-target skills.
    /// </summary>
    public class BuffEffect : IBattleEffect
    {
        public List<CombatResult> Apply(
            Combatant user,
            List<Combatant> targets,
            int power,
            string metadata,
            IBattleMessenger messenger,
            StatusRegistry status,
            BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // Buffs and debuffs typically only affect living combatants
                if (target.IsDead) continue;

                // 1. Logic: Use the StatusRegistry to apply the change.
                // The 'metadata' passed here is the Name of the skill (e.g., "Tarukaja", "Rakunda").
                // The StatusRegistry handles the stacking limits [-4 to +4] and specific stat routing.
                status.ApplyStatChange(metadata, target);

                // 2. UI Feedback: Standard notification of the modification
                messenger.Publish($"{target.Name}'s stats were modified!");

                // 3. Press Turn Logic: Buffing/Debuffing is a successful neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            // Fallback for empty target lists
            if (results.Count == 0)
            {
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}