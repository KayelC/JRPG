using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling self-buff charges (Power Charge and Mind Charge).
    /// These effects stay active until the next valid offensive action is taken.
    /// </summary>
    public class ChargeEffect : IBattleEffect
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
                // Charges can only be applied to living combatants
                if (target.IsDead) continue;

                // 1. Logic: Identify if it's a Physical (Power) or Magical (Mind) charge
                if (metadata.Contains("Power", StringComparison.OrdinalIgnoreCase))
                {
                    target.IsCharged = true;
                    messenger.Publish($"{target.Name} is focusing physical power!", ConsoleColor.Gray);
                }
                else if (metadata.Contains("Mind", StringComparison.OrdinalIgnoreCase))
                {
                    target.IsMindCharged = true;
                    messenger.Publish($"{target.Name} is focusing spiritual energy!", ConsoleColor.Gray);
                }
                else
                {
                    // Fallback for generic charges if added later
                    messenger.Publish($"{target.Name} is focusing energy...");
                }

                // 2. Press Turn Logic: Charging is a successful turn action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}