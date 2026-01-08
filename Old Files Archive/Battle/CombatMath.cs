using System;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle
{
    public static class CombatMath
    {
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// SMT Square-root Formula: 5.0 * sqrt(Power * (Atk/Def))
        /// Rebuilt from Legacy Line 468-469.
        /// </summary>
        public static int CalculateDamage(Combatant attacker, Combatant target, int skillPower, Element element, out bool isCritical)
        {
            isCritical = false;
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);

            // 1. Determine Stats based on SMT Rules
            StatType atkStat = isPhysical ? StatType.STR : StatType.MAG;
            double atkPower = attacker.GetStat(atkStat);
            double defPower = target.GetStat(StatType.END) + target.GetDefense();

            // 2. Base Formula
            double ratio = atkPower / Math.Max(1.0, defPower);
            double dmgBase = 5.0 * Math.Sqrt(skillPower * ratio);

            // 3. Critical Logic (Physical Only)
            if (isPhysical && !target.IsGuarding)
            {
                if (target.IsRigidBody)
                {
                    isCritical = true;
                }
                else
                {
                    // Legacy Formula: (AtkLUK - DefLUK) / 2 + 5
                    int critChance = (attacker.GetStat(StatType.LUK) - target.GetStat(StatType.LUK)) / 2 + 5;
                    if (_rnd.Next(0, 100) < Math.Clamp(critChance, 2, 40))
                    {
                        isCritical = true;
                    }
                }

                if (isCritical) dmgBase *= 1.5;
            }

            // 4. Variance (95% - 105%)
            double variance = 0.95 + (_rnd.NextDouble() * 0.1);
            int finalDamage = (int)(dmgBase * variance);

            return Math.Max(1, finalDamage);
        }

        /// <summary>
        /// SMT III Initiative Roll: Weighted Agility comparison.
        /// Rebuilt from Legacy Line 64-71.
        /// </summary>
        public static bool RollInitiative(double playerAvgAgi, double enemyAvgAgi)
        {
            double pRoll = playerAvgAgi * (0.9 + _rnd.NextDouble() * 0.2);
            double eRoll = enemyAvgAgi * (0.9 + _rnd.NextDouble() * 0.2);
            return pRoll >= eRoll;
        }

        /// <summary>
        /// Hit/Evasion check based on AGI difference.
        /// </summary>
        public static bool CheckHit(Combatant attacker, Combatant target, Element element, string skillAccuracy)
        {
            if (target.IsRigidBody) return true;

            int baseAcc = 95;
            bool isPhysical = (element == Element.Slash || element == Element.Strike || element == Element.Pierce);
            if (isPhysical) baseAcc = 85;

            if (int.TryParse(skillAccuracy.Replace("%", ""), out int parsed))
            {
                baseAcc = parsed;
            }

            // AGI heavily influences hit and evasion.
            int agiDiff = attacker.GetStat(StatType.AGI) - target.GetStat(StatType.AGI);
            double finalChance = baseAcc + (agiDiff * 2);

            return _rnd.Next(0, 100) < Math.Clamp(finalChance, 5, 99);
        }

        /// <summary>
        /// SMT III Rule: Guarding combatants suppress their elemental weaknesses.
        /// </summary>
        public static Affinity GetEffectiveAffinity(Combatant target, Element element)
        {
            if (element == Element.Almighty || element == Element.None) return Affinity.Normal;

            Affinity baseAff = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;

            if (target.IsGuarding && baseAff == Affinity.Weak)
            {
                return Affinity.Normal;
            }

            return baseAff;
        }
    }
}