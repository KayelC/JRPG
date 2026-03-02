using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// The primary strategy for all offensive actions.
    /// Handles Affinities, Critical Hits, Reflection, and Knowledge discovery.
    /// </summary>
    public class DamageEffect : IBattleEffect
    {
        private readonly Element _element;

        /// <summary>
        /// Constructor allows the Registry to create one instance per Element type.
        /// </summary>
        public DamageEffect(Element element)
        {
            _element = element;
        }

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
                if (target.IsDead) continue;

                // 1. Logic: Check for Repel (Shields or Innate)
                // Repel takes absolute priority in the SMT calculation stack.
                Affinity aff = CombatMath.GetEffectiveAffinity(target, _element);

                if (aff == Affinity.Repel)
                {
                    messenger.Publish($"{target.Name} repelled the attack!", ConsoleColor.Red);

                    // Calculate damage based on the USER'S stats (reflecting it back)
                    int refDmg = CombatMath.CalculateReflectedDamage(user, power, _element);
                    var repResult = user.ReceiveDamage(refDmg, _element, false);

                    messenger.Publish($"{user.Name} is hit by the reflection!", ConsoleColor.Red);
                    ReportDamageResult(repResult, user.Name, messenger);

                    // Force the type to Repel so the Conductor knows to terminate the phase
                    repResult.Type = HitType.Repel;
                    results.Add(repResult);
                    continue;
                }

                // 2. Logic: Execute Standard Damage Math
                // Returns the final damage value and whether it was a critical hit.
                int damage = CombatMath.CalculateDamage(user, target, power, _element, out bool isCritical);
                CombatResult result = target.ReceiveDamage(damage, _element, isCritical);

                // 3. Knowledge: Record the discovery for the Player's UI/AI memory
                knowledge.Learn(target.SourceId, _element, aff);

                // 4. UI: Report the result (Damage, Weakness, Block, etc.)
                ReportDamageResult(result, target.Name, messenger);

                // 5. Logic: Secondary Ailment Infliction
                // Only try to infect if the attack actually landed (not Nulled or Absorbed)
                if (result.Type != HitType.Null && result.Type != HitType.Absorb)
                {
                    // metadata usually contains the skill effect string (e.g., "Poison 40%")
                    if (status.TryInflict(user, target, metadata))
                    {
                        // The messenger for infection is handled inside the Publish if successful
                    }
                }

                results.Add(result);
            }

            return results;
        }

        // Standardized reporting for damage results.
        private void ReportDamageResult(CombatResult result, string targetName, IBattleMessenger messenger)
        {
            if (result.DamageDealt > 0)
            {
                string msg = $"{targetName} took {result.DamageDealt} damage";
                if (result.IsCritical) msg += " (CRITICAL)";
                if (result.Type == HitType.Weakness) msg += " (WEAKNESS)";

                messenger.Publish(msg);
            }
            else if (result.Type == HitType.Absorb)
            {
                messenger.Publish($"{targetName} absorbed the attack!", ConsoleColor.Green);
            }
            else if (result.Type == HitType.Null)
            {
                messenger.Publish($"{targetName} blocked the attack!");
            }
        }
    }
}