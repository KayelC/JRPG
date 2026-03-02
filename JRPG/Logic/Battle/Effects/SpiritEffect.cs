using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling SP recovery for Items (like Soul Food) and Skills.
    /// </summary>
    public class SpiritEffect : IBattleEffect
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
                // SP recovery typically only works on living targets
                if (target.IsDead) continue;

                int oldSP = target.CurrentSP;
                int recoveryAmount = power;

                // 1. Logic: Check metadata for percentage flags if applicable (e.g. "50% SP")
                if (metadata.Contains("50%"))
                {
                    recoveryAmount = target.MaxSP / 2;
                }
                else if (metadata.Contains("full") || metadata.Contains("fully") || power >= 9999)
                {
                    recoveryAmount = target.MaxSP;
                }

                // 2. State Mutation: Apply recovery capped at MaxSP
                target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + recoveryAmount);

                int actualRecovered = target.CurrentSP - oldSP;

                // 3. UI Feedback: Broadcast the result via the messenger
                if (actualRecovered > 0)
                {
                    messenger.Publish($"{target.Name} recovered {actualRecovered} SP.", ConsoleColor.Cyan);
                }
                else
                {
                    messenger.Publish($"{target.Name}'s SP is already full.");
                }

                // 4. Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}