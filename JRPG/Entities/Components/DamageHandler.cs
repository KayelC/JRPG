using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using System;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Interaction Engine for the Entities module.
    /// Responsible for calculating the final outcome of an offensive action against a target.
    /// Processes affinities, critical modifiers, status-driven technicals, and resource updates.
    /// </summary>
    public static class DamageHandler
    {
        /// <summary>
        /// Processes damage application against a combatant and returns a CombatResult.
        /// Maintains 100% accuracy with original SMT-hybrid damage logic and affinity messaging.
        /// </summary>
        /// <param name="target">The combatant receiving the action.</param>
        /// <param name="damage">The raw damage value calculated by the Battle Engine.</param>
        /// <param name="element">The elemental type of the attack.</param>
        /// <param name="isCritical">Whether the attack was initially determined to be a critical hit.</param>
        /// <returns>A CombatResult containing damage dealt, hit type, and UI feedback strings.</returns>
        public static CombatResult ApplyDamage(Combatant target, int damage, Element element, bool isCritical)
        {
            // 1. Determine Base Affinity
            // Uses the target's Active Persona to look up resistance levels.
            Affinity aff = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;
            var result = new CombatResult();

            // Check if the current element is Physical for Technical logic
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);

            // 2. Guarding State Logic
            // Guarding reduces damage by 50%, prevents critical hits, and negates weaknesses.
            if (target.IsGuarding)
            {
                damage = (int)(damage * 0.5);
                isCritical = false;
                if (aff == Affinity.Weak)
                {
                    aff = Affinity.Normal;
                }
            }

            // 3. Technical/RigidBody Logic (SMT III Fidelity)
            // If the target is under "Freeze" or "Shock", any Physical hit becomes an automatic Critical.
            if (target.IsRigidBody && isPhysical)
            {
                isCritical = true;
            }

            // 4. Finalize Critical State and Apply Multiplier (1.5x)
            result.IsCritical = isCritical;
            if (isCritical)
            {
                damage = (int)(damage * 1.5);
            }

            // 5. NEW: Ailment-Based Technical Multipliers (Bind / Stun)
            // If the target is Bound or Stunned, Physical attacks deal 50% more damage.
            if (target.CurrentAilment != null && isPhysical)
            {
                string ailmentName = target.CurrentAilment.Name;
                if (ailmentName == "Bind" || ailmentName == "Stun")
                {
                    damage = (int)(damage * 1.5);
                    result.Message = "TECHNICAL!";
                }
            }

            // 6. Affinity Interaction Stack
            // This determines how the raw damage is modified by the target's elemental resistances.
            switch (aff)
            {
                case Affinity.Weak:
                    result.Type = HitType.Weakness;
                    result.DamageDealt = (int)(damage * 1.5f);
                    result.Message = "WEAKNESS STRUCK!";
                    break;

                case Affinity.Resist:
                    result.Type = HitType.Normal;
                    result.DamageDealt = (int)(damage * 0.5f);
                    result.Message = result.IsCritical ? "CRITICAL (Resisted)!" : "Resisted.";
                    break;

                case Affinity.Null:
                    result.Type = HitType.Null;
                    result.DamageDealt = 0;
                    result.Message = "Blocked!";
                    break;

                case Affinity.Repel:
                    result.Type = HitType.Repel;
                    result.DamageDealt = 0;
                    result.Message = "Repelled!";
                    // Repel is special: it returns immediately as the caller handles reflection 
                    // logic against the original attacker. No HP deduction occurs on this target.
                    return result;

                case Affinity.Absorb:
                    result.Type = HitType.Absorb;
                    // Absorb heals the target. Logic ensures current HP does not exceed Max.
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + damage);
                    result.DamageDealt = 0;
                    result.Message = $"Absorbed {damage} HP!";
                    return result;

                default: // Affinity.Normal
                    result.Type = HitType.Normal;
                    result.DamageDealt = damage;
                    if (result.IsCritical && string.IsNullOrEmpty(result.Message))
                    {
                        result.Message = "CRITICAL HIT!";
                    }
                    break;
            }

            // 7. Apply Final State Mutation
            // Deduct HP based on the result, ensuring HP never drops below 0.
            target.CurrentHP = Math.Max(0, target.CurrentHP - result.DamageDealt);

            // 8. Ailment Trigger: Removal Logic (e.g., Wake on Hit for Sleep)
            if (result.DamageDealt > 0 && target.CurrentAilment != null)
            {
                if (target.CurrentAilment.RemovalTriggers != null &&
                    target.CurrentAilment.RemovalTriggers.Contains("OnHit"))
                {
                    string oldAilment = target.CurrentAilment.Name;
                    target.RemoveAilment();

                    // Append feedback to the message so the player knows why the state changed
                    if (string.IsNullOrEmpty(result.Message))
                        result.Message = $"{target.Name} recovered from {oldAilment}!";
                    else
                        result.Message += $" {target.Name} woke up!";
                }
            }

            return result;
        }
    }
}