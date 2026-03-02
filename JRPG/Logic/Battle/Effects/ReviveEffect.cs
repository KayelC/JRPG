using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling revival logic (e.g., Recarm, Samarecarm, Balm of Life).
    /// </summary>
    public class ReviveEffect : IBattleEffect
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
                // Logic: Revival effects only work on targets that are currently dead.
                if (!target.IsDead)
                {
                    // Optionally skip reporting for multi-target revives where some are alive
                    continue;
                }

                // 1. Calculate HP restoration amount
                // Samarecarm/Samarecarmdra uses "fully" or power 100
                int hpToRestore = power;
                if (metadata.Contains("full") || metadata.Contains("fully") || power >= 100)
                {
                    hpToRestore = target.MaxHP;
                }
                else
                {
                    // Recarm standard usually revives with 50% HP or a flat power value
                    hpToRestore = (power > 0) ? power : target.MaxHP / 2;
                }

                // 2. State Mutation: Set HP
                // Setting HP above 0 automatically removes the IsDead state in the Combatant class
                target.CurrentHP = Math.Min(target.MaxHP, hpToRestore);

                // 3. UI Feedback
                messenger.Publish($"{target.Name} was revived!", ConsoleColor.Green);

                // 4. Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            // If no one was dead and the loop finished without reviving anyone
            if (results.Count == 0)
            {
                messenger.Publish("It had no effect...");
                results.Add(new CombatResult { Type = HitType.Miss });
            }

            return results;
        }
    }
}