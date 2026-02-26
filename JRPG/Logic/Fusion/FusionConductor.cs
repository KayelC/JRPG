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
        /// Manages the multi-step workflow of creating a new entity or modifying an existing one via fusion.
        /// Logic: Handles participant selection, result prediction, and deterministic skill inheritance.
        /// </summary>
        private void PerformFusionRitual(bool isSacrificial)
        {
            // 1. Establish the pool of participants based on Character Class
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

                // Only WildCard can fuse Personas
                case ClassType.WildCard:
                    // WildCards draw from ActivePersona and PersonaStock
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
                // Sacrifices are always Demons (Combatants) even for WildCards
                var sacrificePool = _mutator.GetFusibleDemonPool(_player);
                sacrifice = _uiBridge.SelectRitualParticipant(sacrificePool, "CHOOSE THE SACRIFICIAL OFFERING:", new List<Combatant>());
                if (sacrifice == null) return;
            }

            // 3. Result Calculation (now returns operation type and target ID)
            // We create transient Combatants for Persona participants so the Calculator can remain type-pure.
            Combatant parentA = (p1 is Combatant c1) ? c1 : CreateTransientCombatant((Persona)p1);
            Combatant parentB = (p2 is Combatant c2) ? c2 : CreateTransientCombatant((Persona)p2);

            var (operation, targetEntityId, isAccident) = _calculator.CalculateResult(parentA, parentB, MoonPhaseSystem.CurrentPhase);

            // If NoFusionPossible, immediately return.
            if (operation == FusionOperationType.NoFusionPossible)
            {
                _io.WriteLine("The spirits remain silent. This combination yields no result.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // --- Skill Inheritance (only for new demon creation) ---
            List<string> selectedSkills = new List<string>();
            if (operation == FusionOperationType.CreateNewDemon)
            {
                var parentList = new List<Combatant> { parentA, parentB };
                if (sacrifice != null) parentList.Add(sacrifice);

                var inheritablePool = _calculator.GetInheritableSkills(parentList.ToArray());
                int maxInheritSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray());

                // Sacrificial Bonus: Boost slots by 2 (Max 8)
                if (isSacrificial) maxInheritSlots = Math.Min(8, maxInheritSlots + 2);

                selectedSkills = _uiBridge.SelectInheritedSkills(inheritablePool, maxInheritSlots);
                if (selectedSkills == null) return; // User aborted
            }

            // --- Verification and Ritual ---
            // Only confirm if it's a new demon creation. Rank/Stat boosts have different UX.
            if (operation == FusionOperationType.CreateNewDemon)
            {
                // Fetch resultData for UI preview
                if (!Database.Personas.TryGetValue(targetEntityId.ToLower(), out var resultData))
                {
                    _io.WriteLine("Error: Resulting demon template not found.", ConsoleColor.Red);
                    _io.Wait(1000);
                    return;
                }

                // Fusion proceeds ONLY if ConfirmRitual returns true
                if (!_uiBridge.ConfirmRitual(resultData, selectedSkills, _player.Level)) return;
            }

            // --- Ritual Execution Visuals ---
            _uiBridge.DisplayRitualSequence(isAccident);

            // --- 6. State Mutation based on FusionOperationType ---
            switch (operation)
            {
                case FusionOperationType.CreateNewDemon:
                    _mutator.ExecuteFusion(_player, parents, targetEntityId, selectedSkills, sacrifice);
                    break;
                case FusionOperationType.RankUpParent:
                    // parentA is the base for rank changes, as per discussion
                    _mutator.ExecuteRankUpFusion(_player, parentA, sacrifice);
                    break;
                case FusionOperationType.RankDownParent:
                    _mutator.ExecuteRankDownFusion(_player, parentA, sacrifice);
                    break;
                case FusionOperationType.StatBoostFusion:
                    // For Stat Boost, parentA is the demon to boost, parentB is the Mitama (or vice versa)
                    Combatant demonToBoost = (parentA.ActivePersona.Race == "Mitama") ? parentB : parentA;
                    Combatant mitamaParent = (parentA.ActivePersona.Race == "Mitama") ? parentA : parentB;
                    _mutator.ExecuteStatBoostFusion(_player, demonToBoost, mitamaParent, sacrifice);
                    break;
                case FusionOperationType.NoFusionPossible:
                    // Already handled above, but included for completeness.
                    _io.WriteLine("The fusion yielded no result.", ConsoleColor.Red);
                    break;
            }

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
                case ClassType.WildCard:
                    // WildCard users need room in their persona stock
                    hasAvailableSlot = _partyManager.HasOpenPersonaStockSlot(_player);
                    break;
                default: // PersonaUser or Human should not try to recall demons
                    _io.WriteLine("Your class cannot recall demons.", ConsoleColor.Red);
                    _io.Wait(1000);
                    return;
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
        /// Logic: Operators register all Demons (Party + Stock); WildCards register spiritual masks.
        /// </summary>
        private void HandleRegistration()
        {
            if (_player.Class == ClassType.Operator)
            {
                // Operators now pool all demons at their disposal (Active Party + DemonStock)
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
            else if (_player.Class == ClassType.WildCard)
            {
                // Registration source for WildCards is their PersonaStock
                Persona p = _uiBridge.SelectRitualParticipant(_player.PersonaStock, "SELECT PERSONA TO RECORD:", new List<Persona>());
                if (p != null)
                {
                    // Convert Persona to transient Combatant for Compendium format compatibility
                    _compendium.RegisterDemon(CreateTransientCombatant(p));
                }
            }
            else // PersonaUser or Human cannot register demons/personas
            {
                _io.WriteLine("Your class cannot register demons.", ConsoleColor.Red);
                _io.Wait(1000);
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
            // Create a new Persona instance to avoid modifying the original
            var transientPersona = new Persona
            {
                Name = p.Name,
                Level = p.Level,
                Race = p.Race,
                Rank = p.Rank
            };

            Combatant c = new Combatant(p.Name, ClassType.Demon)
            {
                Level = p.Level,
                ActivePersona = transientPersona // Use the new instance
            };
            return c;
        }

        #endregion
    }
}