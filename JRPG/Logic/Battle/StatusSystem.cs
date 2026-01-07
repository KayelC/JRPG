using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle
{
    public class StatusSystem
    {
        private readonly Random _rnd = new Random();

        // Map for resolving skill effect strings to database ailment keys.
        private readonly Dictionary<string, string> _effectToAilmentMap = new Dictionary<string, string>
        {
            { "Poisons", "Poison" },
            { "Freezes", "Freeze" },
            { "Shocks", "Shock" },
            { "Instills Fear", "Fear" },
            { "Panic", "Panic" },
            { "Charms", "Charm" },
            { "Enrages", "Rage" },
            { "Distresses", "Distress" }
        };

        /// <summary>
        /// Attempts to apply an ailment to a target based on the skill's effect string.
        /// </summary>
        public bool TryInflictAilment(Combatant attacker, Combatant target, string skillEffect)
        {
            string ailmentName = string.Empty;
            foreach (var kvp in _effectToAilmentMap)
            {
                if (skillEffect.Contains(kvp.Key))
                {
                    ailmentName = kvp.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(ailmentName) || !Database.Ailments.TryGetValue(ailmentName, out var data))
            {
                return false;
            }

            // Calculate Base Chance from the skill string.
            int baseChance = 40;
            var match = System.Text.RegularExpressions.Regex.Match(skillEffect, @"\((\d+)% chance\)");
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out baseChance);
            }

            // SMT Logic: Infliction = Base + (AttackerLUK - TargetLUK).
            int lukDiff = attacker.GetStat(StatType.LUK) - target.GetStat(StatType.LUK);
            int finalChance = baseChance + lukDiff;

            if (_rnd.Next(100) < Math.Clamp(finalChance, 5, 95))
            {
                return target.InflictAilment(data, 3);
            }

            return false;
        }

        /// <summary>
        /// Processes turn-end logic for a combatant.
        /// Handles Poison damage, Buff/Debuff decay, and Natural Recovery.
        /// </summary>
        public List<string> ProcessTurnEnd(Combatant c)
        {
            var logs = new List<string>();

            // 1. Process Buff/Debuff expiration.
            logs.AddRange(c.TickBuffs());

            // 2. Process Ailment Logic.
            if (c.CurrentAilment != null)
            {
                // A. Handle DOT damage (Poison).
                if (c.CurrentAilment.DotPercent > 0)
                {
                    int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent);
                    c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                    logs.Add($"{c.Name} takes {dmg} damage from {c.CurrentAilment.Name}.");
                }

                // B. Handle Natural Recovery (SMT III Rule).
                // Certain ailments can break early based on a Luck roll.
                if (c.CurrentAilment.RemovalTriggers.Contains("NaturalRoll"))
                {
                    int recoveryChance = 20 + (c.GetStat(StatType.LUK) / 2);
                    if (_rnd.Next(100) < recoveryChance)
                    {
                        logs.Add($"{c.Name} recovered from {c.CurrentAilment.Name}!");
                        c.RemoveAilment();
                        return logs;
                    }
                }

                // C. Decrement Duration.
                c.AilmentDuration--;
                if (c.AilmentDuration <= 0)
                {
                    logs.Add($"{c.Name}'s {c.CurrentAilment.Name} wore off.");
                    c.RemoveAilment();
                }
            }

            return logs;
        }

        /// <summary>
        /// Handles the application of stat modifiers for Kaja, Nda, Heat Riser, and Debilitate.
        /// </summary>
        public void ApplyStatModifier(Combatant target, string skillName)
        {
            int duration = 3;

            if (skillName.Contains("Tarukaja"))
            {
                target.AddBuff("Attack", duration);
            }
            else if (skillName.Contains("Tarunda"))
            {
                target.AddBuff("AttackDown", duration);
            }
            else if (skillName.Contains("Rakukaja"))
            {
                target.AddBuff("Defense", duration);
            }
            else if (skillName.Contains("Rakunda"))
            {
                target.AddBuff("DefenseDown", duration);
            }
            else if (skillName.Contains("Sukukaja"))
            {
                target.AddBuff("Agility", duration);
            }
            else if (skillName.Contains("Sukunda"))
            {
                target.AddBuff("AgilityDown", duration);
            }
            else if (skillName == "Heat Riser")
            {
                target.AddBuff("Attack", duration);
                target.AddBuff("Defense", duration);
                target.AddBuff("Agility", duration);
            }
            else if (skillName == "Debilitate")
            {
                target.AddBuff("AttackDown", duration);
                target.AddBuff("DefenseDown", duration);
                target.AddBuff("AgilityDown", duration);
            }
        }
    }
}