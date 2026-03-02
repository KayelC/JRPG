using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling reflection shields like Tetrakarn (Physical) and Makarakarn (Magic).
    /// </summary>
    public class ShieldEffect : IBattleEffect
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
                // Shields cannot be placed on fallen combatants
                if (target.IsDead) continue;

                // 1. Logic: Identify the shield type based on the skill name/metadata
                // Tetrakarn usually contains "Tetra", Makarakarn contains "Makara"
                if (metadata.Contains("Tetra", StringComparison.OrdinalIgnoreCase))
                {
                    target.PhysKarnActive = true;
                    messenger.Publish($"{target.Name} is protected by a physical shield!", ConsoleColor.White);
                }
                else if (metadata.Contains("Makara", StringComparison.OrdinalIgnoreCase))
                {
                    target.MagicKarnActive = true;
                    messenger.Publish($"{target.Name} is protected by a magic shield!", ConsoleColor.White);
                }

                // 2. Press Turn Logic: Deploying a shield is a successful neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}