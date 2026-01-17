using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Bridges
{
    /// <summary>
    /// Specialized UI Bridge for the Cathedral of Shadows.
    /// Handles Parent Selection, HD-style Skill Inheritance Selection, 
    /// Compendium browsing, and Ritual presentation.
    /// </summary>
    public class FusionUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly CompendiumManager _compendium;

        public FusionUIBridge(IGameIO io, FieldUIState uiState, CompendiumManager compendium)
        {
            _io = io;
            _uiState = uiState;
            _compendium = compendium;
        }

        #region Main Cathedral Menus

        /// <summary>
        /// Renders the top-level Cathedral menu.
        /// </summary>
        public string ShowCathedralMainMenu()
        {
            string header = "=== CATHEDRAL OF SHADOWS ===\n\"Welcome to the Cathedral of Shadows, where demons gather.\"";
            List<string> options = new List<string>
            {
                "Binary Fusion",
                "Browse Compendium",
                "Register Demon",
                "Back"
            };

            int choice = _io.RenderMenu(header, options, 0);
            if (choice == -1 || choice == options.Count - 1) return "Back";

            return options[choice];
        }

        #endregion

        #region Fusion Selection UI

        /// <summary>
        /// Selects the first parent (Demon A) for fusion.
        /// </summary>
        public Combatant SelectFirstParent(List<Combatant> availableDemons)
        {
            if (availableDemons.Count < 2)
            {
                _io.WriteLine("You need at least two demons to perform a fusion.", ConsoleColor.Red);
                _io.Wait(1000);
                return null;
            }

            List<string> labels = availableDemons.Select(d =>
                $"{d.Name,-15} (Lv.{d.Level})").ToList();
            labels.Add("Cancel");

            int choice = _io.RenderMenu("SELECT FIRST PARENT:", labels, 0);
            if (choice == -1 || choice == labels.Count - 1) return null;

            return availableDemons[choice];
        }

        /// <summary>
        /// Selects the second parent (Demon B) for fusion.
        /// Logic: Automatically excludes the first parent to prevent self-fusion.
        /// </summary>
        public Combatant SelectSecondParent(List<Combatant> availableDemons, Combatant firstParent)
        {
            var validChoices = availableDemons.Where(d => d != firstParent).ToList();

            List<string> labels = validChoices.Select(d =>
                $"{d.Name,-15} (Lv.{d.Level})").ToList();
            labels.Add("Cancel");

            int choice = _io.RenderMenu($"FUSING WITH {firstParent.Name.ToUpper()}. SELECT SECOND PARENT:", labels, 0);
            if (choice == -1 || choice == labels.Count - 1) return null;

            return validChoices[choice];
        }

        #endregion

        #region Skill Selection UI

        /// <summary>
        /// High Fidelity HD Feature: Deterministic Skill Selection.
        /// Allows the player to manually pick which skills the child inherits.
        /// </summary>
        public List<string> SelectInheritedSkills(List<string> pool, int maxSlots)
        {
            List<string> selected = new List<string>();

            while (selected.Count < maxSlots)
            {
                _io.Clear();
                string header = $"=== SKILL INHERITANCE ===\nChoose skills to pass down. (Slots: {selected.Count}/{maxSlots})";

                List<string> labels = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var skill in pool)
                {
                    bool alreadyPicked = selected.Contains(skill);
                    labels.Add(alreadyPicked ? $"{skill} [SELECTED]" : skill);
                    disabled.Add(alreadyPicked);
                }

                if (selected.Count > 0) labels.Add("Done Selecting");
                else labels.Add("Cancel Fusion");

                disabled.Add(false);

                int choice = _io.RenderMenu(header, labels, 0, disabled);

                // Exit Logic
                if (choice == -1) return null;
                if (choice == labels.Count - 1)
                {
                    if (selected.Count == 0) return null; // Cancel
                    break; // Done
                }

                selected.Add(pool[choice]);
            }

            return selected;
        }

        #endregion

        #region Preview and Ritual UI

        /// <summary>
        /// Displays a detailed preview of the fusion result before confirmation.
        /// </summary>
        public bool ConfirmFusion(PersonaData result, List<string> inheritedSkills, int playerLevel)
        {
            _io.Clear();
            _io.WriteLine("=== FUSION PREVIEW ===", ConsoleColor.Yellow);
            _io.WriteLine($"Result: {result.Name} ({result.Arcana})");
            _io.WriteLine($"Base Level: {result.Level}");
            _io.WriteLine("------------------------------");
            _io.WriteLine("Projected Inherited Skills:");
            foreach (var s in inheritedSkills)
            {
                _io.WriteLine($" - {s}", ConsoleColor.Cyan);
            }
            _io.WriteLine("------------------------------");

            if (result.Level > playerLevel)
            {
                _io.WriteLine($"[WARNING] Your level ({playerLevel}) is too low to control this demon.", ConsoleColor.Red);
                _io.WriteLine("Ritual cannot be performed.", ConsoleColor.Gray);
                _io.Wait(1500);
                return false;
            }

            List<string> options = new List<string> { "Begin Ritual", "Cancel" };
            return _io.RenderMenu("Begin the sacrifice?", options, 0) == 0;
        }

        public void DisplayRitualSequence(bool isAccident)
        {
            _io.Clear();
            _io.WriteLine("The circle glows with a frightening intensity...");
            _io.Wait(1000);
            _io.WriteLine("The spiritual energy begins to coalesce...");
            _io.Wait(1000);

            if (isAccident)
            {
                _io.WriteLine("!!! SOMETHING IS WRONG !!!", ConsoleColor.Red);
                _io.WriteLine("The energy has become unstable!", ConsoleColor.Red);
                _io.Wait(1500);
            }
        }

        #endregion

        #region Compendium UI

        /// <summary>
        /// Renders the Compendium browsing list.
        /// Displays name, level, and the calculated recall cost.
        /// </summary>
        public Combatant ShowCompendiumRecallMenu()
        {
            var entries = _compendium.GetRegisteredEntries();

            if (!entries.Any())
            {
                _io.WriteLine("The Compendium is currently empty.", ConsoleColor.Gray);
                _io.Wait(1000);
                return null;
            }

            string header = "=== DEMONIC COMPENDIUM ===\nSelect a demon to recall:";
            List<string> labels = new List<string>();

            foreach (var entry in entries)
            {
                int cost = _compendium.CalculateRecallCost(entry.SourceId);
                labels.Add($"{entry.Name,-15} (Lv.{entry.Level}) | {cost} M");
            }
            labels.Add("Back");

            int choice = _io.RenderMenu(header, labels, 0);
            if (choice == -1 || choice == labels.Count - 1) return null;

            return entries[choice];
        }

        /// <summary>
        /// Prompts the player to select a demon from their current party to register.
        /// </summary>
        public Combatant SelectDemonToRegister(List<Combatant> activeParty)
        {
            var demonsOnly = activeParty.Where(c => c.Class == ClassType.Demon).ToList();

            if (!demonsOnly.Any())
            {
                _io.WriteLine("You have no demons in your party to register.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            string header = "=== REGISTER DEMON ===\nOverwrites previous entry with current stats/skills:";
            List<string> labels = demonsOnly.Select(d => $"{d.Name,-15} (Lv.{d.Level})").ToList();
            labels.Add("Cancel");

            int choice = _io.RenderMenu(header, labels, 0);
            if (choice == -1 || choice == labels.Count - 1) return null;

            return demonsOnly[choice];
        }

        #endregion
    }
}