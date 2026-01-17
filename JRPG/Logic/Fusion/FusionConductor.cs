using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The Root Orchestrator for the Fusion Sub-System (Cathedral of Shadows).
    /// Manages the lifecycle of demonic fusion, compendium registration, and recall.
    /// </summary>
    public class FusionConductor
    {
        private readonly IGameIO _io;
        private readonly Combatant _player;
        private readonly PartyManager _party;
        private readonly EconomyManager _economy;
        private readonly FieldUIState _uiState;

        // Sub-System Components
        private readonly FusionEngine _engine;
        private readonly CompendiumManager _compendium;
        private readonly FusionUIBridge _ui;

        public FusionConductor(
            IGameIO io,
            Combatant player,
            PartyManager party,
            EconomyManager economy,
            FieldUIState uiState)
        {
            _io = io;
            _player = player;
            _party = party;
            _economy = economy;
            _uiState = uiState;

            // Initialize internal sub-sub-system components
            _engine = new FusionEngine(_io);
            _compendium = new CompendiumManager(_economy, _io);
            _ui = new FusionUIBridge(_io, _uiState, _compendium);
        }

        /// <summary>
        /// Public entry point called by the FieldConductor.
        /// Runs the main Cathedral loop.
        /// </summary>
        public void EnterCathedral()
        {
            while (true)
            {
                string choice = _ui.ShowCathedralMainMenu();

                if (choice == "Back") return;

                switch (choice)
                {
                    case "Binary Fusion":
                        PerformBinaryFusion();
                        break;

                    case "Browse Compendium":
                        PerformCompendiumRecall();
                        break;

                    case "Register Demon":
                        PerformRegistration();
                        break;
                }
            }
        }

        #region Fusion Workflow

        /// <summary>
        /// Manages the step-by-step process of fusing two demons.
        /// Flow: Select Parents -> Predict -> Skill Selection -> Confirm -> Ritual.
        /// </summary>
        private void PerformBinaryFusion()
        {
            // 1. Parent Selection
            var availableDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();

            Combatant parentA = _ui.SelectFirstParent(availableDemons);
            if (parentA == null) return;

            Combatant parentB = _ui.SelectSecondParent(availableDemons, parentA);
            if (parentB == null) return;

            // 2. Result Prediction
            var (resultId, isAccident) = _engine.PredictResult(parentA, parentB, _player.Level);

            if (resultId == null)
            {
                _io.WriteLine("These demons share no common lineage. Fusion is impossible.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            if (!Database.Personas.TryGetValue(resultId, out var resultData))
            {
                _io.WriteLine("Data Error: Resulting demon not found in database.", ConsoleColor.Red);
                return;
            }

            // 3. HD Skill Inheritance Selection
            var inheritablePool = _engine.GetInheritableSkills(parentA, parentB);
            int maxInheritSlots = _engine.GetInheritanceSlotCount(parentA, parentB);

            List<string> selectedSkills = _ui.SelectInheritedSkills(inheritablePool, maxInheritSlots);
            if (selectedSkills == null) return; // User cancelled during skill selection

            // 4. Confirmation (Level Check handled within UI bridge)
            if (!_ui.ConfirmFusion(resultData, selectedSkills, _player.Level)) return;

            // 5. The Ritual Sequence
            _ui.DisplayRitualSequence(isAccident);

            // 6. State Mutation: Consume Parents
            // Note: We use ReturnDemon logic to clear them from party, then ensure they aren't in stock.
            _party.ReturnDemon(_player, parentA);
            _player.DemonStock.Remove(parentA);

            _party.ReturnDemon(_player, parentB);
            _player.DemonStock.Remove(parentB);

            // 7. State Mutation: Create Child
            // We use the CreateDemon factory to get base stats, then apply inherited skills.
            Combatant child = Combatant.CreateDemon(resultData.Id, resultData.Level);

            foreach (var skill in selectedSkills)
            {
                if (!child.GetConsolidatedSkills().Contains(skill))
                {
                    child.ExtraSkills.Add(skill);
                }
            }

            // Finalize Child
            child.RecalculateResources();
            child.CurrentHP = child.MaxHP;
            child.CurrentSP = child.MaxSP;

            // 8. Add to Party
            if (_party.SummonDemon(_player, child))
            {
                _io.WriteLine($"{child.Name} has been created and joined your party!", ConsoleColor.Green);
            }
            else
            {
                _io.WriteLine($"{child.Name} has been created and sent to stock.", ConsoleColor.Cyan);
                _player.DemonStock.Add(child);
            }

            _io.Wait(1500);
        }

        #endregion

        #region Compendium and Registration Workflow

        /// <summary>
        /// Handles the UI and Logic for recalling a demon from the compendium.
        /// </summary>
        private void PerformCompendiumRecall()
        {
            Combatant selectedEntry = _ui.ShowCompendiumRecallMenu();
            if (selectedEntry == null) return;

            // Check if player has room in stock or party
            bool hasRoom = _party.ActiveParty.Count < 4 || _party.HasOpenDemonStockSlot(_player);

            if (!hasRoom)
            {
                _io.WriteLine("You have no room in your party or stock to recall this demon.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            Combatant recalledDemon = _compendium.RecallDemon(selectedEntry.SourceId);

            if (recalledDemon != null)
            {
                // Attempt to add to active party first for better UX
                if (!_party.SummonDemon(_player, recalledDemon))
                {
                    _player.DemonStock.Add(recalledDemon);
                    _io.WriteLine($"{recalledDemon.Name} has been placed in your stock.");
                }
                _io.Wait(800);
            }
        }

        /// <summary>
        /// Handles the UI and Logic for registering an active demon's state.
        /// </summary>
        private void PerformRegistration()
        {
            Combatant toRegister = _ui.SelectDemonToRegister(_party.ActiveParty);
            if (toRegister == null) return;

            _compendium.RegisterDemon(toRegister);
        }

        #endregion
    }
}