using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling status ailment removal (Skills like Patra, Items like Dis-Poison).
    /// </summary>
    public class CureEffect : IBattleEffect
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
                // Cure effects typically only work on the living
                if (target.IsDead) continue;

                // 1. Logic: Check if the target actually has an ailment
                if (target.CurrentAilment == null)
                {
                    messenger.Publish($"{target.Name} is not suffering from any ailments.");
                    results.Add(new CombatResult { Type = HitType.Normal });
                    continue;
                }

                // 2. State Mutation: Delegate the removal logic to the StatusRegistry
                // 'metadata' represents the skill name or item name used to search for cure keywords
                if (status.CheckAndExecuteCure(target, metadata))
                {
                    messenger.Publish($"{target.Name} was cured of their ailment!", ConsoleColor.White);
                }
                else
                {
                    // This happens if you use a Poison-cure item on a target that is Sleeping
                    messenger.Publish($"The {metadata} had no effect on {target.Name}.");
                }

                // 3. Press Turn Logic: Successful use of a utility action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}