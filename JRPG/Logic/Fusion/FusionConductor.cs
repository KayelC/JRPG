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
    /// Manages the sequences for Binary Fusion, Sacrificial Fusion, 
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

            // Initializing the Sub-Sub-System components
            _calculator = new FusionCalculator(_io);
            _mutator = new FusionMutator(_partyManager, _economy, _io);
            _compendium = new CompendiumRegistry(_io);
            _uiBridge = new CathedralUIBridge(_io, _uiState, _compendium);
        }

        /// <summary>
        /// The main entry point for the Fusion Sub-System.
        /// Manages the primary loop of the Cathedral of Shadows.
        /// </summary>
        public void EnterCathedral()
        {
            while (true)
            {
                // UI provides contextual options based on the current Moon Phase
                string choice = _uiBridge.ShowCathedralMainMenu(MoonPhaseSystem.CurrentPhase);

                if (choice == "Back") return;

                switch (choice)
                {
                    case "Binary Fusion":
                        PerformRitual(isSacrificial: false);
                        break;

                    case "Sacrificial Fusion":
                        PerformRitual(isSacrificial: true);
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

        #region Fusion Ritual Workflow

        /// <summary>
        /// Orchestrates the fusion ritual from participant selection to child creation.
        /// Logic: Handles both 2-demon and 3-demon (sacrificial) fusions.
        /// </summary>
        private void PerformRitual(bool isSacrificial)
        {
            // 1. Establish the pool of participants based on Character Class
            // Implements the class-based logic as requested to ensure safety.
            List<object> participantPool = new List<object>();
            switch (_player.Class)
            {
                case ClassType.Operator:
                    // Operators fuse Demons from Party + Stock
                    var demons = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                    demons.AddRange(_player.DemonStock);
                    participantPool = demons.Distinct().Cast<object>().ToList();
                    break;

                case ClassType.PersonaUser:
                case ClassType.WildCard:
                    // Persona Users fuse Personas from their internal stock
                    var personas = new List<Persona>();
                    if (_player.ActivePersona != null) personas.Add(_player.ActivePersona);
                    personas.AddRange(_player.PersonaStock);
                    participantPool = personas.Distinct().Cast<object>().ToList();
                    break;

                default:
                    _io.WriteLine("Your current class is incapable of performing fusions.", ConsoleColor.Red);
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

            object p1 = _uiBridge.SelectRitualParticipant(participantPool, "SELECT FIRST PARENT:", parents);
            if (p1 == null) return;
            parents.Add(p1);

            object p2 = _uiBridge.SelectRitualParticipant(participantPool, "SELECT SECOND PARENT:", parents);
            if (p2 == null) return;
            parents.Add(p2);

            Combatant sacrifice = null;
            if (isSacrificial)
            {
                // Sacrifices are always Demons (Combatants) even for Persona Users in SMT fidelity
                var demonPool = _player.DemonStock.Concat(_partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon)).Distinct().ToList();
                sacrifice = _uiBridge.SelectRitualParticipant(demonPool, "SELECT SACRIFICIAL OFFERING:", new List<Combatant>());
                if (sacrifice == null) return;
            }

            // 3. Result Prediction
            // We need parent objects to calculate the resulting Arcana and Level tier
            Combatant parentA = (p1 is Combatant c1) ? c1 : CreateTransientCombatant((Persona)p1);
            Combatant parentB = (p2 is Combatant c2) ? c2 : CreateTransientCombatant((Persona)p2);

            var (resultId, isAccident) = _calculator.CalculateResult(parentA, parentB, MoonPhaseSystem.CurrentPhase);

            if (string.IsNullOrEmpty(resultId) || !Database.Personas.TryGetValue(resultId, out var resultData))
            {
                _io.WriteLine("The chosen combination yields no result in this era.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // 4. HD Skill Selection
            var parentList = new List<Combatant> { parentA, parentB };
            if (sacrifice != null) parentList.Add(sacrifice);

            var inheritablePool = _calculator.GetInheritableSkills(parentList.ToArray());
            int maxSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray());

            // If it's a sacrifice, Nocturne rules grant 1-2 additional slots
            if (isSacrificial) maxSlots = Math.Min(8, maxSlots + 2);

            List<string> selectedSkills = _uiBridge.SelectInheritedSkills(inheritablePool, maxSlots);
            if (selectedSkills == null) return; // Aborted

            // 5. Confirmation
            if (!_uiBridge.ConfirmRitual(resultData, selectedSkills, _player.Level)) return;

            // 6. Ritual Execution
            _uiBridge.DisplayRitualSequence(isAccident);

            // 7. State Mutation (Atomic Transaction)
            _mutator.ExecuteFusion(_player, parents, resultData.Id, selectedSkills, sacrifice);

            _io.Wait(1500);
        }

        #endregion

        #region Compendium and Registration Logic

        private void HandleCompendiumRecall()
        {
            Combatant entry = _uiBridge.ShowCompendiumRecallMenu();
            if (entry == null) return;

            int cost = _compendium.CalculateRecallCost(entry.SourceId);

            // Check slots and funds before proceeding to the mutation logic
            bool hasSlot = (_player.Class == ClassType.Operator)
                ? (_partyManager.ActiveParty.Count < 4 || _partyManager.HasOpenDemonStockSlot(_player))
                : _partyManager.HasOpenDemonStockSlot(_player);

            if (!hasSlot)
            {
                _io.WriteLine("You have no space to house this demon.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            if (_economy.Macca < cost)
            {
                _io.WriteLine("You lack the required Macca for this recall.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // Transaction execution
            Combatant recalledDemon = _compendium.GetRecallEntry(entry.SourceId);
            if (recalledDemon != null)
            {
                if (_mutator.FinalizeRecall(_player, recalledDemon, cost))
                {
                    _io.WriteLine($"{recalledDemon.Name} has returned from the void.", ConsoleColor.Cyan);
                    _io.Wait(800);
                }
            }
        }

        private void HandleRegistration()
        {
            // Registration is only possible for Demons in the active party
            Combatant selected = _uiBridge.SelectDemonToRegister(_partyManager.ActiveParty);
            if (selected != null)
            {
                _compendium.RegisterDemon(selected);
            }
        }

        #endregion

        #region Helper Utilities

        /// <summary>
        /// Creates a temporary Combatant from a Persona.
        /// Used to pass Persona data into the FusionCalculator which expects Combatant inputs.
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