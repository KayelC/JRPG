using System;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle
{
    public static class CombatMath
    {
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// Calculates damage using the SMT square-root formula: 5.0 * sqrt(Power * (Atk/Def))
        /// Also handles Critical logic and Elemental Affinities.
        /// </summary>
        public static int CalculateDamage(Combatant attacker, Combatant target, int skillPower, Element element, out bool isCritical)
        {
            isCritical = false;
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);

            // 1. Determine Attack Stat
            // SMT Rule: Physical elements use STR. Magical elements use MAG.
            StatType atkStat = isPhysical ? StatType.STR : StatType.MAG;
            double atkPower = attacker.GetStat(atkStat);

            // 2. Determine Defense Stat
            // Endurance + Armor Defense
            double defPower = target.GetStat(StatType.END) + target.GetDefense();

            // 3. Base SMT Formula
            double dmgBase = 5.0 * Math.Sqrt(skillPower * (atkPower / Math.Max(1, defPower)));

            // 4. Critical Logic (Strict SMT III Fidelity)
            if (isPhysical && !target.IsGuarding)
            {
                // Rule: Rigid Body (Frozen/Shocked) results in 100% Critical rate for Physical hits.
                if (target.IsRigidBody)
                {
                    isCritical = true;
                }
                // Rule: Magic cannot Critical naturally. Physical checks Luck.
                else
                {
                    int critChance = (attacker.GetStat(StatType.LUK) - target.GetStat(StatType.LUK)) / 2 + 5;
                    if (_rnd.Next(100) < Math.Clamp(critChance, 2, 40))
                    {
                        isCritical = true;
                    }
                }

                if (isCritical)
                {
                    dmgBase *= 1.5;
                }
            }

            // 5. Elemental Affinities (Damage Multipliers)
            Affinity aff = GetEffectiveAffinity(target, element);
            switch (aff)
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
                case Affinity.Absorb:
                    return (int)dmgBase * -1; // Negative results indicate healing
            }

            // 6. Guarding modifier (50% reduction)
            if (target.IsGuarding)
            {
                dmgBase *= 0.5;
            }

            // 7. Damage Variance (95% - 105%)
            double variance = 0.95 + (_rnd.NextDouble() * 0.1);
            int finalDamage = (int)(dmgBase * variance);

            return Math.Max(1, finalDamage);
        }

        /// <summary>
        /// Determines if an attack hits based on Agility and Luck.
        /// </summary>
        public static bool CheckHit(Combatant attacker, Combatant target, Element element, string skillAccuracy)
        {
            // Rule: Frozen/Shocked targets cannot evade.
            if (target.IsRigidBody)
            {
                return true;
            }

            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);

            // SMT Rule: Magic is naturally more accurate than Physical.
            int baseAcc = 95;
            if (isPhysical)
            {
                baseAcc = 85;
            }

            if (int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAcc = parsed;
            }

            // AGI heavily influences hit and evasion.
            int agiDiff = attacker.GetStat(StatType.AGI) - target.GetStat(StatType.AGI);
            double finalChance = baseAcc + (agiDiff * 2);

            // Cap the hit chance between 5% and 99%.
            return _rnd.Next(100) < Math.Clamp(finalChance, 5, 99);
        }

        /// <summary>
        /// SMT III Rule: Guarding combatants suppress their elemental weaknesses.
        /// This prevents the attacker from gaining extra turns.
        /// </summary>
        public static Affinity GetEffectiveAffinity(Combatant target, Element element)
        {
            if (element == Element.Almighty || element == Element.None)
            {
                return Affinity.Normal;
            }

            Affinity baseAff = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;

            // If guarding, "Weak" results are downgraded to "Normal" for the Turn Economy.
            if (target.IsGuarding && baseAff == Affinity.Weak)
            {
                return Affinity.Normal;
            }

            return baseAff;
        }
    }
}