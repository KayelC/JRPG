using System;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Mathematical Kernel of the Battle Sub-System.
    /// Provides pure, stateless functions for damage, accuracy, and initiative calculations.
    /// </summary>
    public static class CombatMath
    {
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// SMT Square-root Damage Formula: 5.0 * sqrt(Power * (Atk/Def))
        /// Implements Linear Stacking Multipliers: +4 = 2.0x, -4 = 0.5x.
        /// </summary>
        /// <param name="attacker">The entity performing the action.</param>
        /// <param name="target">The entity receiving the action.</param>
        /// <param name="skillPower">The base power of the skill or weapon.</param>
        /// <param name="element">The elemental type of the attack.</param>
        /// <param name="isCritical">Output parameter indicating if the hit was a critical.</param>
        /// <returns>
        /// Positive Value: Damage dealt to target.
        /// Zero: Attack was Nulled or Repelled (Caller must check affinity to trigger reflection).
        /// Negative Value: Amount the target is healed (Absorb).
        /// </returns>

        /// <summary>
        /// SMT Square-root Damage Formula: 5.0 * sqrt(Power * (Atk/Def))
        /// Also handles Critical multipliers and status-based modifiers.
        /// </summary>
        public static int CalculateDamage(Combatant attacker, Combatant target, int skillPower, Element element, out bool isCritical)
        {
            isCritical = false;

            // Determine if the attack is Physical or Magical
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);

            // Select the correct Attack Stat (SMT Standard: Phys = STR, Magic = MAG)
            StatType atkStat = isPhysical ? StatType.STR : StatType.MAG;
            double atkPower = attacker.GetStat(atkStat);

            // Determine the Defensive Stat (Endurance + Armor Defense)
            double defPower = target.GetStat(StatType.END) + target.GetDefense();

            // Apply Stacking Multipliers (Kaja/Nda)
            // Linear 25% logic: +4 = 200%, 0 = 100%, -4 = 50%
            atkPower *= GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Attack", 0));
            defPower *= GetStatMultiplier(target.Buffs.GetValueOrDefault("Defense", 0));

            // Apply Ailment Penalties
            if (attacker.CurrentAilment != null)
            {
                atkPower *= attacker.CurrentAilment.DamageDealMult;
            }

            // Calculate Base Ratio and Execute Formula
            double ratio = atkPower / Math.Max(1.0, defPower);

            // Execute SMT Square-root Formula
            double dmgBase = 5.0 * Math.Sqrt(skillPower * ratio);

            // Apply Target Ailment Vulnerabilities
            if (target.CurrentAilment != null)
            {
                dmgBase *= target.CurrentAilment.DamageTakenMult;
            }

            // Critical Logic (Physical Only)
            if (isPhysical && !target.IsGuarding)
            {
                int critChance = CalculateCritChance(attacker, target);
                // Rule: Rigid Body (Frozen/Shocked) results in 100% Critical rate
                if (target.IsRigidBody || _rnd.Next(0, 100) < critChance)
                {
                    isCritical = true;
                    dmgBase *= 1.5; // Critical Multiplier
                }
            }

            // Elemental Affinity Multipliers
            Affinity effectiveAff = GetEffectiveAffinity(target, element);
            switch (effectiveAff)
            {
                case Affinity.Weak:
                    dmgBase *= 1.5;
                    break;
                case Affinity.Resist:
                    dmgBase *= 0.5;
                    break;
                case Affinity.Null:
                case Affinity.Repel:
                    return 0;
                // Return 0. The ActionProcessor will see the 'Repel' affinity 
                // and then call CalculateReflectedDamage.
                case Affinity.Absorb:
                    return (int)Math.Floor(dmgBase) * -1;
            }

            // Damage Variance (95% to 105%)
            double variance = 0.95 + (_rnd.NextDouble() * 0.1);
            int finalDamage = (int)Math.Floor(dmgBase * variance);

            return finalDamage;
        }

        /// <summary>
        /// Calculates the linear multiplier for a stat based on its stack count.
        /// Scaling: +4 = 2.0x (25% per stack), -4 = 0.5x (12.5% per stack reduction)
        /// to ensure -4 is exactly half.
        /// </summary>
        private static double GetStatMultiplier(int stacks)
        {
            if (stacks == 0) return 1.0;
            if (stacks > 0) return 1.0 + (stacks * 0.25);

            // For negative stacks, we use 0.125 to reach 0.5 at -4 linearly
            return 1.0 + (stacks * 0.125);
        }

        public static bool CalculateInstantKill(Combatant attacker, Combatant target, string skillAccuracy)
        {
            int baseAccuracy = 40;
            if (!string.IsNullOrEmpty(skillAccuracy) && int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAccuracy = parsed;
            }

            int lukDiff = attacker.GetStat(StatType.LUK) - target.GetStat(StatType.LUK);
            double multi = GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0));

            double finalChance = (baseAccuracy + lukDiff) * multi;

            // Roll against Luck-weighted accuracy
            return _rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 95);
        }

        public static int CalculateReflectedDamage(Combatant originalAttacker, int skillPower, Element element)
        {
            // In SMT III, reflected damage is calculated against the original attacker's 
            // stats and affinities. 
            // We pass false for isCritical because reflected hits cannot crit the attacker.
            return CalculateDamage(originalAttacker, originalAttacker, skillPower, element, out _);
        }

        /// <summary>
        /// Calculates the probability of a physical critical hit based on Luck.
        /// </summary>
        public static int CalculateCritChance(Combatant attacker, Combatant target)
        {
            int attackerLuck = attacker.GetStat(StatType.LUK);
            int targetLuck = target.GetStat(StatType.LUK);
            int chance = ((attackerLuck - targetLuck) / 2) + 5;

            // Apply Agility/Luck buff influence on crit
            double multi = GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0));
            return (int)Math.Clamp(chance * multi, 2, 40);
        }

        /// <summary>
        /// SMT III Rule: Calculates the Affinity taking into account Guarding state and Status Ailments.
        /// </summary>
        public static Affinity GetEffectiveAffinity(Combatant target, Element element)
        {
            if (element == Element.Almighty || element == Element.None) return Affinity.Normal;

            Affinity baseAff = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;

            if (target.IsGuarding && baseAff == Affinity.Weak) return Affinity.Normal;

            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);
            if (target.IsRigidBody && isPhysical)
            {
                // Rigid Body (Freeze/Shock) negates physical resistances but keeps weaknesses.
                if (baseAff == Affinity.Resist || baseAff == Affinity.Null || baseAff == Affinity.Repel)
                {
                    return Affinity.Normal;
                }
            }

            return baseAff;
        }

        /// <summary>
        /// SMT III Initiative Roll: Weighted Agility variance.
        /// </summary>
        public static bool RollInitiative(double playerAvgAgi, double enemyAvgAgi)
        {
            double pRoll = playerAvgAgi * (0.9 + _rnd.NextDouble() * 0.2);
            double eRoll = enemyAvgAgi * (0.9 + _rnd.NextDouble() * 0.2);
            return pRoll >= eRoll;
        }

        /// <summary>
        /// SMT III High-Fidelity Hit/Evasion check.
        /// Formula: SkillAccuracy + (AttackerAGI - TargetAGI) * 2 + (AttackerLUK - TargetLUK)
        /// </summary>
        public static bool CheckHit(Combatant attacker, Combatant target, Element element, string skillAccuracy)
        {
            if (target.IsRigidBody) return true;

            // Data-Driven Accuracy: Use value from JSON.
            // MODIFIED: Basic attacks (where skillAccuracy is empty) now use 90%.
            int baseAccuracy = 90;
            if (!string.IsNullOrEmpty(skillAccuracy) && int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAccuracy = parsed;
            }

            // Agility Influence: (AGI Diff * 2) 
            int attackerAgi = attacker.GetStat(StatType.AGI);
            int targetAgi = target.GetStat(StatType.AGI);
            
            // Luck Influence: (LUK Diff) 
            int attackerLuk = attacker.GetStat(StatType.LUK);
            int targetLuk = target.GetStat(StatType.LUK);

            // Apply Agility Buffs to the hit/evasion math
            double atkMulti = GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0));
            double defMulti = GetStatMultiplier(target.Buffs.GetValueOrDefault("Agility", 0));

            double finalChance = baseAccuracy + (((attackerAgi * atkMulti) - (targetAgi * defMulti)) * 2) + (attackerLuk - targetLuk);

            return _rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 99);
        }
    }
}