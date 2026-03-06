using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// The primary strategy for all offensive actions (Physical and Magical).
    /// Handles Accuracy, Affinities, Critical Hits, Reflection, Instant Kills, and Knowledge discovery.
    /// Includes Evasion UI tracking and centralized Charge consumption.
    /// Includes Vampiric Drain logic based on metadata text.
    /// </summary>
    public class DamageEffect : IBattleEffect
    {
        private readonly Element _element;

        // Constructor allows the Registry to create one instance per Element type.
        public DamageEffect(Element element)
        {
            _element = element;
        }

        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            // Feature Flags based on the Effect string
            bool isInstantKill = actionEffect.ToLower().Contains("instant kill");
            bool drainsHP = actionEffect.Contains("Drains HP", StringComparison.OrdinalIgnoreCase);
            bool drainsSP = actionEffect.Contains("Drains SP", StringComparison.OrdinalIgnoreCase);
            bool pureSPDrain = drainsSP && !drainsHP; // Identifies skills like "Spirit Drain"

            // Determine if the element is physical for charge consumption rules
            bool isPhysical = (_element == Element.Slash || _element == Element.Strike || _element == Element.Pierce);

            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                // 1. Logic: Accuracy Gate
                // Extract accuracy percentage from metadata (e.g. "Agilao (90%)") or default to 95%
                string accStr = "95%"; // Default
                Match accMatch = Regex.Match(actionEffect, @"(\d+)%");
                if (accMatch.Success) accStr = accMatch.Value;

                if (!CombatMath.CheckHit(user, target, _element, accStr))
                {
                    results.Add(new CombatResult { Type = HitType.Miss });
                    messenger.Publish("MISS!", ConsoleColor.Gray, 400);
                    continue;
                }

                // 2. Logic: Check for Repel (Shields or Innate)
                // Repel takes absolute priority in the calculation stack.
                Affinity aff = CombatMath.GetEffectiveAffinity(target, _element);

                if (aff == Affinity.Repel)
                {
                    messenger.Publish($"{target.Name} repelled the attack!", ConsoleColor.Red);

                    // Calculate reflected damage against the USER using their own stats
                    int refDmg = CombatMath.CalculateReflectedDamage(user, power, _element);
                    var repResult = user.ReceiveDamage(refDmg, _element, false);

                    messenger.Publish($"{user.Name} is hit by the reflection!", ConsoleColor.Red);
                    ReportDamageResult(repResult, user.Name, messenger);

                    // Rule: A repelled attack immediately ends the phase (Repel is worst case HitType)
                    repResult.Type = HitType.Repel;
                    results.Add(repResult);

                    // Stop processing other targets for this skill if one repels (SMT Standard)
                    break;
                }

                // 3. Handle Instant Kill logic
                if (isInstantKill)
                {
                    // IK Rule: Null/Absorb function as normal blocks against Instant Kill
                    if (aff == Affinity.Null || aff == Affinity.Absorb)
                    {
                        knowledge.Learn(target.SourceId, _element, aff);
                        messenger.Publish("Blocked!", ConsoleColor.Gray);
                        results.Add(new CombatResult { Type = HitType.Null });
                        continue;
                    }

                    // Extract the percentage accuracy from the metadata
                    string ikAccuracy = "25%"; // Default
                    Match match = Regex.Match(actionEffect, @"(\d+)%");
                    if (match.Success) ikAccuracy = match.Value;

                    if (CombatMath.CalculateInstantKill(user, target, ikAccuracy))
                    {
                        int hpBefore = target.CurrentHP;
                        target.CurrentHP = 0; // Immediate Death
                        knowledge.Learn(target.SourceId, _element, aff);
                        messenger.Publish("INSTANT KILL!", ConsoleColor.Red);

                        // IK hits count as Weakness for Press Turn purposes
                        results.Add(new CombatResult { Type = HitType.Weakness, DamageDealt = hpBefore });
                        continue;
                    }
                }

                // 4. Logic: Execute Standard Damage Math
                // CalculateDamage now returns RAW potency. Affinities are handled by ReceiveDamage.
                int rawDamage = CombatMath.CalculateDamage(user, target, power, _element, out bool isCritical);
                CombatResult result;

                // 5. Body Logic: Apply Damage (Handling standard vs SP Drains)
                if (pureSPDrain)
                {
                    // SMT Rule: Spirit Drain specifically damages the SP pool, completely bypassing HP.
                    int spDamage = Math.Min(target.CurrentSP, rawDamage);
                    target.CurrentSP -= spDamage;
                    result = new CombatResult { Type = HitType.Normal, DamageDealt = spDamage };

                    knowledge.Learn(target.SourceId, _element, aff);
                    messenger.Publish($"{target.Name} lost {spDamage} SP!");
                }
                else
                {
                    // Standard HP Damage (Deathtouch, Life Drain, Kyuketsu, Beast Roar)
                    result = target.ReceiveDamage(rawDamage, _element, isCritical);

                    // 6. Knowledge: Record the discovery for the Player's UI/AI memory
                    knowledge.Learn(target.SourceId, _element, aff);

                // 7. UI: Report the result (Damage, Weakness, Block, etc.)
                    ReportDamageResult(result, target.Name, messenger);
                }

                // 6. Secondary Ailment Infliction
                if (result.Type != HitType.Null && result.Type != HitType.Absorb && result.Type != HitType.Repel)
                {
                    // Execute the infliction check (it handles its own UI publishing if successful)
                    _ = status.TryInflict(user, target, actionEffect);


                    // --- 7. VAMPIRIC RESTORATION LOGIC ---
                    if ((drainsHP || drainsSP) && result.DamageDealt > 0)
                    {
                        if (drainsHP)
                        {
                            int oldHP = user.CurrentHP;
                            user.CurrentHP = Math.Min(user.MaxHP, user.CurrentHP + result.DamageDealt);
                            int recovered = user.CurrentHP - oldHP;
                            if (recovered > 0) messenger.Publish($"{user.Name} drained {recovered} HP!", ConsoleColor.Green);
                        }

                        if (drainsSP)
                        {
                            int oldSP = user.CurrentSP;
                            // Note: Kyuketsu yields 1:1 SP based on HP damage dealt.
                            user.CurrentSP = Math.Min(user.MaxSP, user.CurrentSP + result.DamageDealt);
                            int recovered = user.CurrentSP - oldSP;
                            if (recovered > 0) messenger.Publish($"{user.Name} drained {recovered} SP!", ConsoleColor.Cyan);
                        }
                    }
                }

                results.Add(result);
            }

            // 9. ENGINE LOGIC: Centralized Charge Management
            // Charges are cleared regardless of hit/miss once an offensive action finishes executing.
            // Placed outside the loop so AoE attacks properly benefit all targets before consumption.
            if (isPhysical) user.IsCharged = false;
            else user.IsMindCharged = false;

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