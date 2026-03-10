using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling status ailment removal (Skills like Patra, Items like Dis-Poison).
    /// Handles both specific cures and "Cure All" variants.
    /// </summary>
    public class CureEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power,
        string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status,
        BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            // Combine both to ensure item names ("Dis-Poison") and skill effects ("Cures Poison") are both visible to the logic parser.
            string cureData = $"{actionName} {actionEffect}";

            foreach (var target in targets)
            {
                // Cure effects only work on the living.
                if (target.IsDead) continue;

                // 1. Logic Check: Verify if the target is even suffering from a status.
                if (target.CurrentAilment == null)
                {
                    messenger.Publish($"{target.Name} is not suffering from any ailments.");
                    continue;
                }

                // 2. Execution logic: Delegate the removal check to the StatusRegistry.
                // The Registry handles the string matching between the skill metadata and the current ailment.
                if (status.CheckAndExecuteCure(target, cureData))
                {
                    // Success: The ailment was removed.
                    messenger.Publish($"{target.Name} was cured of their ailment!", ConsoleColor.White);

                    // Cures are neutral actions.
                    results.Add(new CombatResult { Type = HitType.Normal });
                }
                else
                {
                    // Failure: The target has an ailment, but this skill is not compatible with it.
                    // (e.g. using Patra, which cures mental ailments, while the target is Poisoned).
                    messenger.Publish($"The action had no effect on {target.Name}.");

                    // Failed actions are still standard turn-consuming neutral actions.
                    //results.Add(new CombatResult { Type = HitType.Normal });
                }
            }

            // Defensive check: Ensure the conductor always receives at least one result packet to prevent hangs.
            if (results.Count == 0)
            {
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}