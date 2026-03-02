using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling the Dekunda effect: Nullifying all negative stat penalties on targets.
    /// </summary>
    public class DekundaEffect : IBattleEffect
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
                // Usually used on the user's own party members
                if (target.IsDead) continue;

                bool penaltiesRemoved = false;

                // 1. Logic: Iterate through the Buffs dictionary keys (Attack, Defense, Agility)
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    // Dekunda ONLY removes negative debuffs (< 0). 
                    // Positive buffs (> 0) remain on the target.
                    if (target.Buffs[k] < 0)
                    {
                        target.Buffs[k] = 0;
                        penaltiesRemoved = true;
                    }
                }

                // 2. UI Feedback: Report the status change
                if (penaltiesRemoved)
                {
                    messenger.Publish($"{target.Name}'s stat penalties were nullified!", ConsoleColor.White);
                }
                else
                {
                    messenger.Publish($"{target.Name} had no stat penalties to clear.");
                }

                // 3. Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}