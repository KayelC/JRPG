using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling HP recovery for both Items and Skills.
    /// </summary>
    public class HealEffect : IBattleEffect
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
                // Cannot heal the dead (use ReviveEffect for that)
                if (target.IsDead) continue;

                int oldHP = target.CurrentHP;
                int healAmount = power;

                // 1. Logic: Check metadata for percentage flags (e.g. "50%", "full")
                // These strings usually come from the Skill Effect or Item Description
                if (metadata.Contains("50%"))
                {
                    healAmount = target.MaxHP / 2;
                }
                else if (metadata.Contains("full") || metadata.Contains("fully") || power >= 9999)
                {
                    healAmount = target.MaxHP;
                }

                // 2. State Mutation: Apply the healing capped at MaxHP
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);

                int actualHealed = target.CurrentHP - oldHP;

                // 3. UI Feedback: Only report if something actually happened
                if (actualHealed > 0)
                {
                    messenger.Publish($"{target.Name} recovered {actualHealed} HP.", ConsoleColor.Green);
                }
                else
                {
                    messenger.Publish($"{target.Name} is already at full health.");
                }

                // 4. Press Turn Logic: Healing is a neutral action (Normal hit)
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}