using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Specialized UI Bridge for Status screens, Persona management, 
    /// and Party Organization (COMP).
    /// </summary>
    public class StatusUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly PartyManager _party;

        public StatusUIBridge(IGameIO io, FieldUIState uiState, PartyManager party)
        {
            _io = io;
            _uiState = uiState;
            _party = party;
        }

        #region Status Hub and Stat Allocation

        /// <summary>
        /// Renders the primary Status Hub. 
        /// Logic: Displays current human stats and provides access to specialized stocks based on class.
        /// </summary>
        public string ShowStatusHub(Combatant player)
        {
            string header = RenderHumanStatusToString(player) + $"\nPoints Available: {player.StatPoints}";

            List<string> options = new List<string> { "Allocate Stats", "Change Equipment" };

            // Class-specific menu augmentation
            if (player.Class == ClassType.WildCard || player.Class == ClassType.PersonaUser)
            {
                options.Add("Persona Stock");
            }

            if (player.Class == ClassType.Operator)
            {
                options.Add("Demon Stock");
            }

            options.Add("Back");

            int choice = _io.RenderMenu(header, options, _uiState.StatusHubIndex);
            if (choice == -1 || choice == options.Count - 1) return "Back";

            _uiState.StatusHubIndex = choice;
            return options[choice];
        }

        #endregion

        #region Persona Stock and Details

        /// <summary>
        /// Renders the list of Personas currently carried by the player.
        /// Marks the equipped Persona with [E].
        /// </summary>
        public Persona SelectPersonaFromStock(Combatant player)
        {
            var allPersonas = new List<Persona>();
            if (player.ActivePersona != null) allPersonas.Add(player.ActivePersona);
            allPersonas.AddRange(player.PersonaStock);

            if (allPersonas.Count == 0)
            {
                _io.WriteLine("No Personas available.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            List<string> options = allPersonas.Select(p =>
                $"{p.Name,-15} (Lv.{p.Level}) {p.Arcana,-10} {(p == player.ActivePersona ? "[E]" : "")}").ToList();
            options.Add("Back");

            int idx = _io.RenderMenu("=== PERSONA STOCK ===", options, 0);
            if (idx == -1 || idx == options.Count - 1) return null;

            return allPersonas[idx];
        }

        /// <summary>
        /// Renders the detailed stat sheet for a specific Persona.
        /// </summary>
        public string ShowPersonaDetails(Persona p, bool isEquipped)
        {
            string header = GetPersonaDetailString(p, isEquipped);
            List<string> options = new List<string>();

            if (!isEquipped) options.Add("Equip Persona");
            options.Add("Back");

            int choice = _io.RenderMenu(header, options, 0);
            if (choice == -1 || choice == options.Count - 1) return "Back";

            return options[choice];
        }

        #endregion

        #region Demon Stock and Organization (COMP)

        /// <summary>
        /// Renders the list of Demons in the party and stock.
        /// Marks their current location (Field vs Stock).
        /// </summary>
        public Combatant SelectDemonFromStock(Combatant player)
        {
            var allDemons = _party.ActiveParty.Where(m => m.Class == ClassType.Demon).ToList();
            allDemons.AddRange(player.DemonStock);

            if (allDemons.Count == 0)
            {
                _io.WriteLine("No demons found.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            List<string> options = allDemons.Select(d =>
                $"{d.Name,-15} (Lv.{d.Level}) {(_party.ActiveParty.Contains(d) ? "[PARTY]" : "[STOCK]")}").ToList();
            options.Add("Back");

            int idx = _io.RenderMenu("=== DEMON OVERVIEW ===", options, 0);
            if (idx == -1 || idx == options.Count - 1) return null;

            return allDemons[idx];
        }

        /// <summary>
        /// Renders the Organize Party screen with 4 fixed slots.
        /// </summary>
        public int ShowOrganizationSlots()
        {
            string header = "=== ORGANIZE PARTY ===\nSelect a slot to manage:";
            List<string> options = new List<string>();

            for (int i = 0; i < 4; i++)
            {
                if (i < _party.ActiveParty.Count)
                {
                    var member = _party.ActiveParty[i];
                    options.Add($"Slot {i + 1}: {member.Name,-15} (Lv.{member.Level})");
                }
                else
                {
                    options.Add($"Slot {i + 1}: [EMPTY]");
                }
            }
            options.Add("Back");

            int choice = _io.RenderMenu(header, options, 0);
            if (choice == -1 || choice == options.Count - 1) return -1;

            return choice;
        }

        /// <summary>
        /// UI for managing a specific party member (Return to COMP logic).
        /// </summary>
        public string ShowMemberManagementMenu(Combatant member)
        {
            List<string> options = new List<string> { "Return to COMP", "Back" };
            int choice = _io.RenderMenu($"Manage {member.Name}", options, 0);

            if (choice == -1 || choice == options.Count - 1) return "Back";
            return options[choice];
        }

        /// <summary>
        /// UI for summoning a demon from stock into an empty slot.
        /// </summary>
        public Combatant SelectSummonTarget(Combatant player)
        {
            if (!player.DemonStock.Any())
            {
                _io.WriteLine("No demons in stock.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            var names = player.DemonStock.Select(d => $"{d.Name,-15} (Lv.{d.Level})").ToList();
            names.Add("Cancel");

            int idx = _io.RenderMenu("SUMMON DEMON", names, 0);
            if (idx == -1 || idx == names.Count - 1) return null;

            return player.DemonStock[idx];
        }

        /// <summary>
        /// Displays detailed stat sheet for a demon Combatant.
        /// </summary>
        public void ShowDemonDetails(Combatant demon)
        {
            string header = GetDemonDetailString(demon);
            List<string> options = new List<string> { "Back" };
            _io.RenderMenu(header, options, 0);
        }

        #endregion

        #region String Rendering Logic (High Fidelity)

        public string RenderHumanStatusToString(Combatant entity)
        {
            string output = "=== STATUS & PARAMETERS ===\n";
            output += $"Name: {entity.Name} (Lv.{entity.Level}) | Class: {entity.Class}\n";
            output += $"HP: {entity.CurrentHP,3}/{entity.MaxHP,3} SP: {entity.CurrentSP,3}/{entity.MaxSP,3}\n";
            output += $"EXP: {entity.Exp,6}/{entity.ExpRequired,6} Next: {entity.ExpRequired - entity.Exp,6}\n";
            output += "-----------------------------\n";

            var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>();
            foreach (var stat in stats)
            {
                int total = entity.GetStat(stat);
                int baseVal = entity.CharacterStats[stat];
                int mod = total - baseVal;

                if (mod > 0)
                {
                    output += $"{stat,-4}: {total,3} (+{mod})\n";
                }
                else
                {
                    output += $"{stat,-4}: {total,3}\n";
                }
            }
            output += "-----------------------------";
            return output;
        }

        private string GetPersonaDetailString(Persona p, bool isEquipped)
        {
            string output = $"=== PERSONA DETAILS {(isEquipped ? "[EQUIPPED]" : "")} ===\n";
            output += $"Name: {p.Name} (Lv.{p.Level}) | Arcana: {p.Arcana}\n";
            output += $"EXP: {p.Exp,6}/{p.ExpRequired,6} Next: {p.ExpRequired - p.Exp,6}\n";
            output += "-----------------------------\nRaw Stats:\n";

            var displayStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in displayStats)
            {
                int val = p.StatModifiers.ContainsKey(stat) ? p.StatModifiers[stat] : 0;
                output += $" {stat,-4}: {val,3}\n";
            }
            output += "-----------------------------\nSkills:\n";
            foreach (var s in p.SkillSet) output += $" - {s}\n";

            var nextSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key).Take(3).ToList();
            if (nextSkills.Any())
            {
                output += "\nNext to Learn:\n";
                foreach (var ns in nextSkills) output += $" [Lv.{ns.Key,2}] {ns.Value}\n";
            }
            else if (p.SkillsToLearn.Any())
            {
                output += "\n(Mastered)\n";
            }

            return output;
        }

        private string GetDemonDetailString(Combatant demon)
        {
            string output = "=== DEMON DETAILS ===\n";
            output += $"Name: {demon.Name} (Lv.{demon.Level})\n";
            output += $"HP: {demon.CurrentHP,3}/{demon.MaxHP,3} SP: {demon.CurrentSP,3}/{demon.MaxSP,3}\n";
            output += $"EXP: {demon.Exp,6}/{demon.ExpRequired,6} Next: {demon.ExpRequired - demon.Exp,6}\n";
            output += "-----------------------------\n";

            var stats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in stats)
            {
                int total = demon.GetStat(stat);
                output += $"{stat,-4}: {total,3}\n";
            }
            output += "-----------------------------\nSkills:\n";
            foreach (var s in demon.GetConsolidatedSkills()) output += $" - {s}\n";

            if (demon.ActivePersona != null)
            {
                var nextSkills = demon.ActivePersona.SkillsToLearn.Where(k => k.Key > demon.Level).OrderBy(k => k.Key).Take(3).ToList();
                if (nextSkills.Any())
                {
                    output += "\nNext to Learn:\n";
                    foreach (var ns in nextSkills) output += $" [Lv.{ns.Key,2}] {ns.Value}\n";
                }
                else if (demon.ActivePersona.SkillsToLearn.Any())
                {
                    output += "\n(Mastered)\n";
                }
            }

            return output;
        }

        #endregion
    }
}