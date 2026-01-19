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
    /// The high-fidelity UI authority for the Cathedral of Shadows.
    /// Handles ritual presentation, deterministic skill inheritance, 
    /// and Compendium visualization.
    /// </summary>
    public class CathedralUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly CompendiumRegistry _compendium;

        public CathedralUIBridge(IGameIO io, FieldUIState uiState, CompendiumRegistry compendium)
        {
            _io = io;
            _uiState = uiState;
            _compendium = compendium;
        }

        #region Navigation Menus

        /// <summary>
        /// Renders the main portal to the Cathedral.
        /// Logic: Contextually displays "Sacrificial Fusion" only during the Full Moon.
        /// </summary>
        public string ShowCathedralMainMenu(int moonPhase)
        {
            _io.Clear();
            string phaseName = MoonPhaseSystem.GetPhaseName();
            string header = $"=== CATHEDRAL OF SHADOWS === [LUNAR PHASE: {phaseName}]\n" +
                            "\"The convergence of souls begins here.\"\n";

            List<string> options = new List<string> { "Binary Fusion" };

            // Sacrificial Fusion is unlocked strictly on Full Moon (Phase 8)
            if (moonPhase == 8)
            {
                options.Add("Sacrificial Fusion");
            }

            options.Add("Browse Compendium");
            options.Add("Register Demon");
            options.Add("Back");

            int choice = _io.RenderMenu(header, options, 0);

            if (choice == -1 || choice == options.Count - 1) return "Back";
            return options[choice];
        }

        #endregion

        #region Ritual Participant Selection

        /// <summary>
        /// Renders a list of participants for the ritual.
        /// Excludes entities already selected (exclusions) to prevent self-fusion.
        /// </summary>
        public T SelectRitualParticipant<T>(List<T> pool, string prompt, List<T> exclusions) where T : class
        {
            var validChoices = pool.Where(x => !exclusions.Contains(x)).ToList();

            if (!validChoices.Any())
            {
                _io.WriteLine("No further candidates available for this ritual.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            List<string> labels = new List<string>();
            foreach (var item in validChoices)
            {
                if (item is Combatant c)
                {
                    labels.Add($"{c.Name,-15} (Lv.{c.Level}) {c.ActivePersona?.Arcana}");
                }
                else if (item is Persona p)
                {
                    labels.Add($"{p.Name,-15} (Lv.{p.Level}) {p.Arcana}");
                }
            }
            labels.Add("Cancel");

            int choice = _io.RenderMenu(prompt, labels, 0);

            if (choice == -1 || choice == labels.Count - 1) return null;
            return validChoices[choice];
        }

        #endregion

        #region Skill Selection

        /// <summary>
        /// Deterministic Skill Selection.
        /// Allows the player to manually select exactly which skills pass to the child.
        /// </summary>
        public List<string> SelectInheritedSkills(List<string> pool, int maxSlots)
        {
            List<string> selected = new List<string>();

            while (selected.Count < maxSlots)
            {
                _io.Clear();
                string header = $"=== SKILL INHERITANCE ===\nChoose skills to pass down to the new creation.\n" +
                                $"Selected: {selected.Count} / {maxSlots} slots filled.\n";

                List<string> labels = new List<string>();
                List<bool> disabledList = new List<bool>();

                foreach (var skillName in pool)
                {
                    bool isPicked = selected.Contains(skillName);
                    labels.Add(isPicked ? $"[X] {skillName}" : $"[ ] {skillName}");
                    disabledList.Add(isPicked);
                }

                if (selected.Count > 0) labels.Add("Confirm Selection");
                else labels.Add("Abort Fusion");

                disabledList.Add(false);

                // Render with secondary info callback to show skill effect descriptions
                int choice = _io.RenderMenu(header, labels, 0, disabledList, (idx) =>
                {
                    if (idx >= 0 && idx < pool.Count)
                    {
                        if (Database.Skills.TryGetValue(pool[idx], out var data))
                        {
                            _io.WriteLine($"Skill Detail: {data.Effect}", ConsoleColor.Cyan);
                        }
                    }
                });

                if (choice == -1) return null;

                // Handle the "Confirm/Abort" bottom option
                if (choice == labels.Count - 1)
                {
                    if (selected.Count == 0) return null; // Abort
                    break; // Confirm
                }

                selected.Add(pool[choice]);
            }

            return selected;
        }

        #endregion

        #region Ritual Presentation

        /// <summary>
        /// Final confirmation screen displaying the results of the planned fusion.
        /// Implemented using onHighlight to show the child preview in Yellow during the decision.
        /// </summary>
        public bool ConfirmRitual(PersonaData result, List<string> inheritedSkills, int playerLevel)
        {
            // Requirement: Level Constraint Check (Forbidden fusions cannot be confirmed)
            if (result.Level > playerLevel)
            {
                _io.Clear();
                _io.WriteLine("=== RITUAL FORBIDDEN ===", ConsoleColor.Red);
                _io.WriteLine($"The resulting being, {result.Name} (Lv.{result.Level}), exceeds your authority.");
                _io.WriteLine($"Your current level: {playerLevel}", ConsoleColor.Gray);
                _io.WriteLine("\nThe spirits refuse to stabilize.", ConsoleColor.Red);
                _io.Wait(2000);
                return false;
            }

            List<string> options = new List<string> { "Commence Ritual", "Wait" };

            // We use onHighlight to render the preview data consistently beneath the menu
            int choice = _io.RenderMenu("Is this creation acceptable?", options, 0, null, (idx) =>
            {
                _io.WriteLine("\n--- PROJECTED RESULT ---", ConsoleColor.Yellow);
                _io.WriteLine($"Form   : {result.Name}", ConsoleColor.Yellow);
                _io.WriteLine($"Arcana : {result.Arcana}", ConsoleColor.Yellow);
                _io.WriteLine($"Level  : {result.Level}", ConsoleColor.Yellow);
                _io.WriteLine("------------------------");
                _io.WriteLine("Inherited Skill Pool:");
                foreach (var s in inheritedSkills)
                {
                    _io.WriteLine($" > {s}", ConsoleColor.Yellow);
                }
                _io.WriteLine("------------------------");
            });

            return choice == 0;
        }

        /// <summary>
        /// Orchestrates the visual sequence of the fusion ritual.
        /// Handles the atmospheric delays and accident feedback.
        /// </summary>
        public void DisplayRitualSequence(bool isAccident)
        {
            _io.Clear();
            _io.WriteLine("The sacrificial circle glows with a cold, blue light...");
            _io.Wait(1200);
            _io.WriteLine("The participants are reduced to pure spiritual data...");
            _io.Wait(1200);
            _io.WriteLine("The streams of energy collide and begin to merge...");
            _io.Wait(1200);

            if (isAccident)
            {
                _io.WriteLine("!!! WARNING: LUNAR INTERFERENCE DETECTED !!!", ConsoleColor.Red);
                _io.WriteLine("The fusion process has become unstable!", ConsoleColor.Red);
                _io.Wait(2000);
            }
        }

        #endregion

        #region Compendium UI

        /// <summary>
        /// Renders the scrollable Compendium registry.
        /// </summary>
        public Combatant ShowCompendiumRecallMenu()
        {
            var entries = _compendium.GetAllRegisteredDemons();

            if (!entries.Any())
            {
                _io.WriteLine("The Compendium is empty. You must register a demon first.", ConsoleColor.Gray);
                _io.Wait(1000);
                return null;
            }

            string header = "=== DEMONIC COMPENDIUM ===\nRecall the data of a previously registered demon.\n";
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
        /// Prompts the player to choose a demon from their party to save to the Compendium.
        /// </summary>
        public Combatant SelectDemonToRegister(List<Combatant> party)
        {
            var demonsOnly = party.Where(c => c.Class == ClassType.Demon).ToList();

            if (!demonsOnly.Any())
            {
                _io.WriteLine("You have no demons in your party to register.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            string header = "=== REGISTER DEMON ===\nSelect a demon to overwrite its current snapshot in the registry.\n";
            List<string> labels = demonsOnly.Select(d => $"{d.Name,-15} (Lv.{d.Level})").ToList();
            labels.Add("Cancel");

            int choice = _io.RenderMenu(header, labels, 0);
            if (choice == -1 || choice == labels.Count - 1) return null;

            return demonsOnly[choice];
        }

        #endregion
    }
}