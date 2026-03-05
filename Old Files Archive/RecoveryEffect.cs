using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Consolidated strategy for the 'Recovery' category.
    /// Matches the old monolithic logic for Revive, Cure, and HP recovery.
    /// </summary>
    public class RecoveryEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                bool actionApplied = false;

                // 1. Logic: Curing (Patra, Me Patra, etc.)
                if (metadata.Contains("Cure", StringComparison.OrdinalIgnoreCase))
                {
                    if (status.CheckAndExecuteCure(target, metadata))
                    {
                        messenger.Publish($"{target.Name} was cured!", ConsoleColor.White);
                        actionApplied = true;
                    }
                }

                // 2. Logic: Reviving (Recarm, Samarecarm)
                if (target.IsDead && metadata.Contains("Revive", StringComparison.OrdinalIgnoreCase))
                {
                    // Logic from old system: "fully" or 100 power = MaxHP, otherwise 50%
                    int revVal = (metadata.Contains("fully", StringComparison.OrdinalIgnoreCase) || power >= 100)
                                 ? target.MaxHP
                                 : target.MaxHP / 2;

                    target.CurrentHP = revVal;
                    messenger.Publish($"{target.Name} was revived!", ConsoleColor.Green);
                    actionApplied = true;
                }

                // 3. Logic: HP Recovery (Dia, Media)
                else if (!target.IsDead)
                {
                    int oldHP = target.CurrentHP;
                    int healAmount = power;

                    // Handle "NaN" power parsing from effect string (Legacy requirement)
                    if (healAmount == 0)
                    {
                        Match match = Regex.Match(metadata, @"\((\d+)\)");
                        if (match.Success) healAmount = int.Parse(match.Groups[1].Value);
                    }

                    // Handle percentage flags
                    if (metadata.Contains("50%")) healAmount = target.MaxHP / 2;
                    if (metadata.Contains("full", StringComparison.OrdinalIgnoreCase)) healAmount = target.MaxHP;

                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                    int actualHealed = target.CurrentHP - oldHP;

                    if (actualHealed > 0)
                    {
                        messenger.Publish($"{target.Name} recovered {actualHealed} HP.", ConsoleColor.Green);
                        actionApplied = true;
                    }
                    else if (!actionApplied)
                    {
                        messenger.Publish($"{target.Name} is already at full health.");
                    }
                }

                if (actionApplied)
                {
                    results.Add(new CombatResult { Type = HitType.Normal });
                }
            }

            // Fallback if no targets were valid
            if (!results.Any()) results.Add(new CombatResult { Type = HitType.Normal });

            return results;
        }
    }
}