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
    /// The Root Orchestrator for the Fusion Sub-System.
    /// Manages the high-level sequences for Binary Fusion, Sacrificial Fusion, 
    /// Compendium registration, and Recall.
    /// </summary>
    public class FusionConductor
    {
        private readonly IGameIO _io;
        private readonly Combatant _player;
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly FieldUIState _uiState;

        // Internal Logic Components
        private readonly FusionCalculator _calculator;
        private readonly FusionMutator _mutator;
        private readonly CompendiumRegistry _compendium;
        private readonly CathedralUIBridge _uiBridge;

        public FusionConductor(
            IGameIO io,
            Combatant player,
            PartyManager partyManager,
            EconomyManager economy,
            FieldUIState uiState)
        {
            _io = io;
            _player = player;
            _partyManager = partyManager;
            _economy = economy;
            _uiState = uiState;

            // Initializing the specialized engines and bridges
            _calculator = new FusionCalculator(_io);
            _mutator = new FusionMutator(_partyManager, _economy, _io);
            _compendium = new CompendiumRegistry(_io);
            _uiBridge = new CathedralUIBridge(_io, _uiState, _compendium);
        }

        /// <summary>
        /// Public entry point for the Cathedral of Shadows.
        /// Runs the primary interaction loop.
        /// </summary>
        public void EnterCathedral()
        {
            while (true)
            {
                // UI displays context-sensitive options based on Moon Phase
                string choice = _uiBridge.ShowCathedralMainMenu(MoonPhaseSystem.CurrentPhase);

                if (choice == "Back") return;

                switch (choice)
                {
                    case "Binary Fusion":
                        PerformFusionRitual(isSacrificial: false);
                        break;

                    case "Sacrificial Fusion":
                        // Note: UI only permits this option during Full Moon
                        PerformFusionRitual(isSacrificial: true);
                        break;

                    case "Browse Compendium":
                        HandleCompendiumRecall();
                        break;

                    case "Register Demon":
                        HandleRegistration();
                        break;
                }
            }
        }

        #region Fusion Ritual Sequence

        /// <summary>
        /// Manages the multi-step workflow of creating a new entity.
        /// Logic: Handles participant selection, result prediction, and deterministic skill inheritance.
        /// </summary>
        private void PerformFusionRitual(bool isSacrificial)
        {
            // 1. Participant Selection
            // Logic: Source pools are class-dependent to ensure stock integrity.
            List<object> participantPool = new List<object>();
            switch (_player.Class)
            {
                case ClassType.Operator:
                    // Operators draw from Active Party and DemonStock
                    var demons = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                    demons.AddRange(_player.DemonStock);
                    participantPool = demons.Distinct().Cast<object>().ToList();
                    break;

                case ClassType.PersonaUser:
                case ClassType.WildCard:
                    // PersonaUsers draw from ActivePersona and PersonaStock
                    var personas = new List<Persona>();
                    if (_player.ActivePersona != null) personas.Add(_player.ActivePersona);
                    personas.AddRange(_player.PersonaStock);
                    participantPool = personas.Distinct().Cast<object>().ToList();
                    break;

                default:
                    _io.WriteLine("Your current essence is incompatible with the ritual circle.", ConsoleColor.Red);
                    _io.Wait(1000);
                    return;
            }

            if (participantPool.Count < (isSacrificial ? 3 : 2))
            {
                string countNeeded = isSacrificial ? "three" : "two";
                _io.WriteLine($"You need at least {countNeeded} participants for this ritual.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // 2. Participant Selection
            List<object> parents = new List<object>();

            // Select Parent 1
            object p1 = _uiBridge.SelectRitualParticipant(participantPool, "CHOOSE THE FIRST PARTICIPANT:", parents);
            if (p1 == null) return;
            parents.Add(p1);

            // Select Parent 2
            object p2 = _uiBridge.SelectRitualParticipant(participantPool, "CHOOSE THE SECOND PARTICIPANT:", parents);
            if (p2 == null) return;
            parents.Add(p2);

            // Select Sacrifice (Full Moon only)
            Combatant sacrifice = null;
            if (isSacrificial)
            {
                // Fidelity Note: Sacrifices are always Demons (Combatants) even for Persona Users
                var sacrificePool = _mutator.GetFusibleDemonPool(_player);
                sacrifice = _uiBridge.SelectRitualParticipant(sacrificePool, "CHOOSE THE SACRIFICIAL OFFERING:", new List<Combatant>());
                if (sacrifice == null) return;
            }

            // 2. Result Calculation
            // We create transient Combatants for Persona participants so the Calculator can remain type-pure.
            Combatant parentA = (p1 is Combatant c1) ? c1 : CreateTransientCombatant((Persona)p1);
            Combatant parentB = (p2 is Combatant c2) ? c2 : CreateTransientCombatant((Persona)p2);

            var (resultId, isAccident) = _calculator.CalculateResult(parentA, parentB, MoonPhaseSystem.CurrentPhase);

            if (string.IsNullOrEmpty(resultId) || !Database.Personas.TryGetValue(resultId, out var resultData))
            {
                _io.WriteLine("The spirits remain silent. This combination yields no result.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // 3. Skill Inheritance
            // Build the collective parent list for inheritance math
            var parentList = new List<Combatant> { parentA, parentB };
            if (sacrifice != null) parentList.Add(sacrifice);

            var inheritablePool = _calculator.GetInheritableSkills(parentList.ToArray());
            int maxInheritSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray());

            // Sacrificial Bonus: Boost slots by 2 (Max 8)
            if (isSacrificial) maxInheritSlots = Math.Min(8, maxInheritSlots + 2);

            List<string> selectedSkills = _uiBridge.SelectInheritedSkills(inheritablePool, maxInheritSlots);
            if (selectedSkills == null) return; // User aborted

            // 4. Verification and Ritual
            if (!_uiBridge.ConfirmRitual(resultData, selectedSkills, _player.Level)) return;

            // 5. Ritual Execution
            _uiBridge.DisplayRitualSequence(isAccident);

            // 6. State Mutation
            // This atomic transaction handles the consumption of parents and instantiation of the child.
            _mutator.ExecuteFusion(_player, parents, resultData.Id, selectedSkills, sacrifice);

            _io.Wait(1500);
        }

        #endregion

        #region Compendium Registration and Recall

        /// <summary>
        /// Handles the UI flow and logic for Compendium recruitment.
        /// Logic: Forks slot-checking based on player class and stock type.
        /// </summary>
        private void HandleCompendiumRecall()
        {
            Combatant entry = _uiBridge.ShowCompendiumRecallMenu();
            if (entry == null) return;

            int cost = _compendium.CalculateRecallCost(entry.SourceId);

            // Class-Specific Slot Validation
            bool hasAvailableSlot = false;
            switch (_player.Class)
            {
                case ClassType.Operator:
                    // Operators need room in either party or demon stock
                    hasAvailableSlot = (_partyManager.ActiveParty.Count < 4 || _partyManager.HasOpenDemonStockSlot(_player));
                    break;
                case ClassType.PersonaUser:
                case ClassType.WildCard:
                    // Persona users need room in their persona stock
                    hasAvailableSlot = _partyManager.HasOpenPersonaStockSlot(_player);
                    break;
            }

            if (!hasAvailableSlot)
            {
                _io.WriteLine("You have no vessel capable of containing this soul.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            if (_economy.Macca < cost)
            {
                _io.WriteLine($"The required donation of {cost} Macca is missing.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // Transaction commitment
            Combatant snapshot = _compendium.GetRecallEntry(entry.SourceId);
            if (snapshot != null)
            {
                if (_mutator.FinalizeRecall(_player, snapshot, cost))
                {
                    _io.WriteLine($"{snapshot.Name} has been materialized.", ConsoleColor.Cyan);
                    _io.Wait(800);
                }
            }
        }

        /// <summary>
        /// Handles the UI flow for recording current progress to the Compendium.
        /// Logic: Operators register all Demons (Party + Stock); PersonaUsers register spiritual masks.
        /// </summary>
        private void HandleRegistration()
        {
            if (_player.Class == ClassType.Operator)
            {
                // Refinement: Operators now pool all demons at their disposal (Active Party + DemonStock)
                var registrationPool = _partyManager.ActiveParty
                    .Where(c => c.Class == ClassType.Demon)
                    .ToList();

                registrationPool.AddRange(_player.DemonStock);

                // Ensure distinct entries and then prompt UI selection
                Combatant selected = _uiBridge.SelectDemonToRegister(registrationPool.Distinct().ToList());

                if (selected != null)
                {
                    _compendium.RegisterDemon(selected);
                }
            }
            else
            {
                // Registration source for PersonaUsers is their PersonaStock
                Persona p = _uiBridge.SelectRitualParticipant(_player.PersonaStock, "SELECT PERSONA TO RECORD:", new List<Persona>());
                if (p != null)
                {
                    // Convert Persona to transient Combatant for Compendium format compatibility
                    _compendium.RegisterDemon(CreateTransientCombatant(p));
                }
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Converts a Persona into a transient Combatant object.
        /// This allows spiritual masks to be processed by the Demon-centric logic of the Calculator and Registry.
        /// </summary>
        private Combatant CreateTransientCombatant(Persona p)
        {
            Combatant c = new Combatant(p.Name, ClassType.Demon)
            {
                Level = p.Level,
                ActivePersona = p
            };
            return c;
        }

        #endregion
    }
}