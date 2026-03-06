using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Linq;
using System.Text.RegularExpressions; // Added for skill parsing

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Mathematical Kernel of the Battle Sub-System.
    /// Provides pure, stateless functions for damage, accuracy, and initiative calculations.
    /// </summary>
    public static class CombatMath
    {
        private static readonly Random _rnd = new Random();

        // --- NEW YIELD CALCULATIONS ---

        /// <summary>
        /// Calculates EXP based on the Cubic progression curve: 1.5 * Level^3.
        /// Adjusted for group encounters (approx 3-4 enemies per battle) and normalized stat bonus.
        /// </summary>
        public static int CalculateExpYield(Combatant enemy)
        {
            // DIVISOR: How many enemies of the same level required to gain 1 level?
            // Set to 50.0 to target approx. 14-16 battles per level given 3-4 enemies per battle.
            const double EnemiesPerLevel = 50.0;

            // 1. Base Yield matching player's Cubic progression
            double baseYield = (1.5 * Math.Pow(enemy.Level, 3)) / EnemiesPerLevel;

            // 2. Normalized Stat Density Bonus
            // Calculates how "strong" this enemy is compared to an average enemy of that level.
            // Average SMT stats ~ (Level * 3) + 15
            double expectedStats = (enemy.Level * 3) + 15;
            double actualStats = enemy.GetStat(StatType.St) + enemy.GetStat(StatType.Ma) +
                                 enemy.GetStat(StatType.Vi) + enemy.GetStat(StatType.Ag) +
                                 enemy.GetStat(StatType.Lu);

            // Bonus: 1% extra EXP for every point above the average curve. Capped at 2.0x total multiplier.
            // This ensures Bosses/Beefy demons give more, but standard demons don't inflate.
            double statMultiplier = 1.0 + Math.Max(0, (actualStats - expectedStats) / 100.0);
            statMultiplier = Math.Min(2.0, statMultiplier);

            double finalExpValue = baseYield * statMultiplier;

            // Ensure the final integer value is at least 1 to prevent infinite loops at low levels
            return Math.Max(1, (int)Math.Floor(finalExpValue));
        }

        /// <summary>
        /// Calculates Macca based on a Quadratic curve (Level^2).
        /// Scaled down to account for higher kill counts in group battles, and adjusted to hit 3-6M by Lv99.
        /// </summary>
        public static int CalculateMaccaYield(Combatant enemy)
        {
            // MACCA_MULTIPLIER: Adjusted to 0.25 to target 3-6 million Macca by Lv99.
            const double MACCA_BASE_MULTIPLIER = 0.25;

            // 1. Quadratic Base: MACCA_BASE_MULTIPLIER * Level^2
            double baseMacca = MACCA_BASE_MULTIPLIER * Math.Pow(enemy.Level, 2);

            // 2. Luck Bonus (High Lu enemies drop more cash)
            // Luck contributes linearly to Macca yield
            double luckBonus = enemy.GetStat(StatType.Lu) * 5;

            // 3. Variance (+/- 10%)
            double variance = 0.9 + (_rnd.NextDouble() * 0.2);

            return (int)Math.Floor((baseMacca + luckBonus) * variance);
        }

        // --- SMT III Damage Formula: 5.0 * sqrt(Power * (Atk/Def)) ---
        /// <summary>
        /// Calculates the raw potency an attacker deals to a target.
        /// Removed affinity multipliers to prevent double-calculation and Absorb bugs.
        /// 
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

            // Select the correct Attack Stat (SMT Standard: Phys = St, Magic = Ma)
            StatType atkStatType = isPhysical ? StatType.St : StatType.Ma;
            double atkPower = attacker.GetStat(atkStatType);

            // Determine the Defensive Stat (Vi + Armor Defense)
            double defPower = target.GetStat(StatType.Vi) + target.GetDefense();

            // Apply Stacking Multipliers (Kaja/Nda)
            // Linear 25% logic: +4 = 200%, 0 = 100%, -4 = 50%
            atkPower *= GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Attack", 0));
            defPower *= GetStatMultiplier(target.Buffs.GetValueOrDefault("Defense", 0));

            // Apply Passive Skills (Amps/Boosts/Drivers handled via GetStat calls
            // if implemented there, otherwise would be here. Preserving current stat-access structure.
            //atkPower *= GetPassiveDamageMultiplier(attacker, element);

            // Apply Charges (Power Charge / Mind Charge)
            if (isPhysical && attacker.IsCharged)
            {
                atkPower *= 1.9;
            }
            else if (!isPhysical && attacker.IsMindCharged)
            {
                atkPower *= 1.9;
            }

            // Apply Ailment Penalties
            if (attacker.CurrentAilment != null)
            {
                atkPower *= attacker.CurrentAilment.DamageDealMult;
            }

            // Calculate Base Ratio and Execute Formula
            double ratio = atkPower / Math.Max(1.0, defPower); // Ensure no division by zero or negative defense

            // Execute Square-root Formula
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

            // Damage Variance (95% to 105%)
            double variance = 0.95 + (_rnd.NextDouble() * 0.1);

            // FIX: Removed the Elemental Affinity Multiplier switch from this class.
            // Affinities are now handled exclusively by Combatant.ReceiveDamage to prevent Absorb sign-flip bugs.

            return (int)Math.Floor(dmgBase * variance);
        }

        /// <summary>
        /// Hit/Evasion check.
        /// Formula: SkillAccuracy + (AttackerAg - TargetAg) * 2 + (AttackerLu - TargetLu)
        /// </summary>
        public static bool CheckHit(Combatant attacker, Combatant target, Element element, string skillAccuracy)
        {
            // If target is Rigid (Frozen/Shocked), all attacks hit
            if (target.IsRigidBody) return true;

            // 1. Data-Driven Accuracy: Use value from JSON.
            int baseAccuracy = 90; // Default for basic attacks if skillAccuracy is empty
            if (!string.IsNullOrEmpty(skillAccuracy) &&
                int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAccuracy = parsed;
            }

            // 2. Passive Accuracy/Evasion skills (Vidyaraja's Blessing, Dodge/Evade)
            double hitMult = 1.0;
            var attackerSkills = attacker.GetConsolidatedSkills();
            if (attackerSkills.Contains("Vidyaraja's Blessing")) hitMult *= 1.15; // 15% hit bonus

            double evadeMult = 1.0;
            var targetSkills = target.GetConsolidatedSkills();
            string elName = element.ToString();
            // Dodge/Evade apply to specific element types
            if (targetSkills.Any(s => s.Contains("Dodge") && s.Contains(elName))) evadeMult *= 0.85; // 15% evade bonus
            if (targetSkills.Any(s => s.Contains("Evade") && s.Contains(elName))) evadeMult *= 0.60; // 40% evade bonus

            // 3. Agility & Luck Influence
            int attackerAg = attacker.GetStat(StatType.Ag);
            int targetAg = target.GetStat(StatType.Ag);
            int attackerLu = attacker.GetStat(StatType.Lu);
            int targetLu = target.GetStat(StatType.Lu);

            double atkValue = (attackerAg * GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0))) * hitMult;
            double defValue = (targetAg * GetStatMultiplier(target.Buffs.GetValueOrDefault("Agility", 0))) * evadeMult;

            // Final Chance Calculation: Clamped between 5% and 99%
            double finalChance = baseAccuracy + ((atkValue - defValue) * 2) + (attackerLu - targetLu);

            return _rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 99);
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

        /// <summary>
        /// Scans attacker skills for Boost (1.25x), Amp (1.5x), and Driver (1.75x).
        /// Multipliers stack multiplicatively.
        /// </summary>
        private static double GetPassiveDamageMultiplier(Combatant attacker, Element element)
        {
            double multiplier = 1.0;
            var skills = attacker.GetConsolidatedSkills();
            string elName = element.ToString();

            if (skills.Any(s => s.Contains(elName) && s.Contains("Boost"))) multiplier *= 1.25;
            if (skills.Any(s => s.Contains(elName) && s.Contains("Amp"))) multiplier *= 1.5;
            if (skills.Any(s => s.Contains(elName) && s.Contains("Driver"))) multiplier *= 1.75;

            // Magic Ability check
            bool isMagic = (element != Element.Slash && element != Element.Strike && element != Element.Pierce && element != Element.Almighty);
            if (isMagic && skills.Contains("Magic Ability")) multiplier *= 1.25;

            return multiplier;
        }

        public static bool CalculateInstantKill(Combatant attacker, Combatant target, string skillAccuracy)
        {
            int baseAccuracy = 40; // Default for instant kill skills
            if (!string.IsNullOrEmpty(skillAccuracy) && int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAccuracy = parsed;
            }

            int lukDiff = attacker.GetStat(StatType.Lu) - target.GetStat(StatType.Lu);
            double multi = GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0));

            // Clamp the final chance
            return _rnd.Next(0, 100) < Math.Clamp((baseAccuracy + lukDiff) * multi, 5, 95);
        }

        public static int CalculateReflectedDamage(Combatant originalAttacker, int skillPower, Element element)
        {
            // Reflected damage is calculated against the original attacker's
            // stats and affinities. 
            // We pass false for isCritical because reflected hits cannot crit the attacker.
            return CalculateDamage(originalAttacker, originalAttacker, skillPower, element, out _);
        }

        /// <summary>
        /// Calculates the probability of a physical critical hit based on Luck.
        /// </summary>
        public static int CalculateCritChance(Combatant attacker, Combatant target)
        {
            int attackerLuck = attacker.GetStat(StatType.Lu);
            int targetLuck = target.GetStat(StatType.Lu);
            int chance = ((attackerLuck - targetLuck) / 2) + 5;

            // Apply Agility/Luck buff influence on crit
            double multi = GetStatMultiplier(attacker.Buffs.GetValueOrDefault("Agility", 0));

            // Passive Boosts
            var skills = attacker.GetConsolidatedSkills();
            if (skills.Contains("Apt Pupil")) multi *= 2.0;
            if (skills.Contains("Rebellion")) multi *= 1.2;

            // Clamp the final chance
            return (int)Math.Clamp(chance * multi, 2, 40);
        }

        /// <summary>
        /// Prioritizes Shields > Breaks > Base Persona Affinities.
        /// </summary>
        public static Affinity GetEffectiveAffinity(Combatant target, Element element)
        {
            // 1. Check for Active Shields (Karns take absolute priority)
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);
            if (isPhysical && target.PhysKarnActive) return Affinity.Repel;
            if (!isPhysical && target.MagicKarnActive && element != Element.Almighty) return Affinity.Repel;

            // 2. Check for Elemental Breaks (Reduces immunity to Normal)
            if (target.BrokenAffinities.ContainsKey(element)) return Affinity.Normal;

            // 3. Almighty and None elements cannot be resisted/nullified
            if (element == Element.Almighty || element == Element.None) return Affinity.Normal;

            // 4. Base Persona/Demon Affinity (from PersonaData)
            Affinity baseAff = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;

            // 5. Guarding (reduces Weakness to Normal)
            if (target.IsGuarding && baseAff == Affinity.Weak) return Affinity.Normal;

            // 6. Rigid Body (Freeze/Shocked) negates physical resistances but keeps weaknesses.
            // Rule: If rigid, physical Null/Resist/Repel becomes Normal, Absorb becomes Normal. Weakness remains.
            if (target.IsRigidBody && isPhysical)
            {
                if (baseAff == Affinity.Resist || baseAff == Affinity.Null || baseAff == Affinity.Repel || baseAff == Affinity.Absorb)
                {
                    return Affinity.Normal;
                }
            }

            return baseAff;
        }

        /// <summary>
        /// SMT III Initiative Roll: Weighted Agility variance.
        /// </summary>
        public static bool RollInitiative(double playerAvgAg, double enemyAvgAg)
        {
            // Rolls are 90%-110% of average agility
            double pRoll = playerAvgAg * (0.9 + _rnd.NextDouble() * 0.2);
            double eRoll = enemyAvgAg * (0.9 + _rnd.NextDouble() * 0.2);

            return pRoll >= eRoll;
        }
    }
}