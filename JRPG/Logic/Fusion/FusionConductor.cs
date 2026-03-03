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
            if (operation == FusionOperationType.NoFusionPossible || string.IsNullOrEmpty(targetEntityId))
            {
                _io.WriteLine("The spirits remain silent. This combination yields no result.", ConsoleColor.Red);
                _io.Wait(1000);
                return;
            }

            // --- Identify Result and Inherent Skills early for UI duplicate-checking ---
            List<string> inherentSkills = new List<string>();
            PersonaData resultTemplate = null;

            if (operation == FusionOperationType.CreateNewDemon)
            {
                Database.Personas.TryGetValue(targetEntityId.ToLower(), out resultTemplate);
                inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
            }
            else if (operation == FusionOperationType.RankUpParent || operation == FusionOperationType.RankDownParent)
            {
                Combatant rankTarget = (parentA.ActivePersona.Race != "Element") ? parentA : parentB;
                int rankDir = operation == FusionOperationType.RankUpParent ? 1 : -1;
                resultTemplate = Database.Personas.Values
                    .Where(p => p.Race == rankTarget.ActivePersona.Race && p.Rank == rankTarget.ActivePersona.Rank + rankDir)
                    .OrderBy(p => p.Level).FirstOrDefault();
                inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
            }
            else if (operation == FusionOperationType.StatBoostFusion)
            {
                // In Stat Boost, the "Inherent Skills" are the skills the target already has
                Combatant boostTarget = (parentA.ActivePersona.Race == "Mitama") ? parentB : parentA;
                inherentSkills = boostTarget.GetConsolidatedSkills();
            }

            if (resultTemplate == null && operation != FusionOperationType.StatBoostFusion)
            {
                _io.WriteLine("Target data not found.", ConsoleColor.Red);
                return;
            }

            // --- Skill Selection (Now duplicate-aware) ---
            var parentList = new List<Combatant> { parentA, parentB };
            if (sacrifice != null) parentList.Add(sacrifice);

            var inheritablePool = _calculator.GetInheritableSkills(parentList.ToArray());
            int maxInheritSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray());

            // Sacrificial Bonus: Boost slots by 2 (Max 8)
            if (isSacrificial) maxInheritSlots = Math.Min(8, maxInheritSlots + 2);

            // Pass inherentSkills to the UI so it can gray them out
            List<string> selectedSkills = _uiBridge.SelectInheritedSkills(inheritablePool, maxInheritSlots, inherentSkills);
            if (selectedSkills == null) return; // User aborted

            // --- Verification and Staging ---
            Combatant stagedDemon = null;
            Combatant originalParent = null;

            if (operation == FusionOperationType.CreateNewDemon)
            {
                stagedDemon = Combatant.CreatePlayerDemon(resultTemplate.Id, resultTemplate.Level);
            }
            else if (operation == FusionOperationType.RankUpParent || operation == FusionOperationType.RankDownParent)
            {
                originalParent = (parentA.ActivePersona.Race != "Element") ? parentA : parentB;
                stagedDemon = Combatant.CreatePlayerDemon(resultTemplate.Id, resultTemplate.Level);
            }
            else if (operation == FusionOperationType.StatBoostFusion)
            {
                originalParent = (parentA.ActivePersona.Race == "Mitama") ? parentB : parentA;
                Combatant mitamaCom = (parentA.ActivePersona.Race == "Mitama") ? parentA : parentB;
                stagedDemon = Combatant.CreatePlayerDemon(originalParent.SourceId, originalParent.Level);
                foreach (var st in originalParent.CharacterStats) stagedDemon.CharacterStats[st.Key] = st.Value;
                foreach (var mod in originalParent.ActivePersona.StatModifiers) stagedDemon.ActivePersona.StatModifiers[mod.Key] = mod.Value;
                ApplyMitamaStatBoost(stagedDemon, mitamaCom.ActivePersona.Name);
                stagedDemon.RecalculateResources();
            }

            // Universal Confirmation - Level Cap and Consent checked here for ALL operations
            if (!_uiBridge.ConfirmRitual(stagedDemon, originalParent, selectedSkills, _player.Level, operation))
                return;

            // --- Ritual Execution Visuals ---
            _uiBridge.DisplayRitualSequence(isAccident);

            // --- State Mutation based on FusionOperationType ---
            switch (operation)
            {
                case FusionOperationType.CreateNewDemon:
                    _mutator.ExecuteFusion(_player, parents, targetEntityId, selectedSkills, sacrifice);
                    break;

                case FusionOperationType.RankUpParent:
                case FusionOperationType.RankDownParent:
                    object rankObj = (parentA.ActivePersona.Race != "Element") ? p1 : p2;
                    if (operation == FusionOperationType.RankUpParent) _mutator.ExecuteRankUpFusion(_player, rankObj, selectedSkills, sacrifice);
                    else _mutator.ExecuteRankDownFusion(_player, rankObj, selectedSkills, sacrifice);
                    break;
                case FusionOperationType.StatBoostFusion:
                    object boostObj = (parentA.ActivePersona.Race == "Mitama") ? p2 : p1;
                    object mitamaObj = (parentA.ActivePersona.Race == "Mitama") ? p1 : p2;
                    _mutator.ExecuteStatBoostFusion(_player, boostObj, mitamaObj, selectedSkills, sacrifice);
                    break;
            }

            _io.Wait(1500);
        }

        private void ApplyMitamaStatBoost(Combatant demon, string mitamaName)
        {
            var mods = demon.ActivePersona.StatModifiers;
            if (mitamaName == "Ara Mitama") { mods[StatType.St] = Math.Min(40, mods.GetValueOrDefault(StatType.St) + 2); mods[StatType.Ag] = Math.Min(40, mods.GetValueOrDefault(StatType.Ag) + 1); }
            else if (mitamaName == "Nigi Mitama") { mods[StatType.Ma] = Math.Min(40, mods.GetValueOrDefault(StatType.Ma) + 2); mods[StatType.Lu] = Math.Min(40, mods.GetValueOrDefault(StatType.Lu) + 1); }
            else if (mitamaName.Contains("Kus")) { mods[StatType.Vi] = Math.Min(40, mods.GetValueOrDefault(StatType.Vi) + 2); mods[StatType.Ag] = Math.Min(40, mods.GetValueOrDefault(StatType.Ag) + 1); }
            else if (mitamaName == "Saki Mitama") { mods[StatType.Vi] = Math.Min(40, mods.GetValueOrDefault(StatType.Vi) + 2); mods[StatType.Lu] = Math.Min(40, mods.GetValueOrDefault(StatType.Lu) + 1); }
        }

        #endregion

        #region Compendium and Helpers

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
                    hasAvailableSlot = (_partyManager.ActiveParty.Count < 4 || _partyManager.HasOpenDemonStockSlot(_player));
                    break;
                case ClassType.WildCard:
                    hasAvailableSlot = _partyManager.HasOpenPersonaStockSlot(_player);
                    break;
                default:
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
                // Operators pool all demons at their disposal (Active Party + DemonStock)
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
            else // Human cannot register demons/personas
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
            // Ensure stats and skills are copied so WildCards can inherit them during fusion
            var transientPersona = new Persona
            {
                Name = p.Name,
                Level = p.Level,
                Race = p.Race,
                Rank = p.Rank,
                Exp = p.Exp
            };

            transientPersona.SkillSet.AddRange(p.SkillSet);
            foreach (var stat in p.StatModifiers)
            {
                transientPersona.StatModifiers[stat.Key] = stat.Value;
            }

            Combatant c = new Combatant(p.Name, ClassType.Demon)
            {
                Level = p.Level,
                ActivePersona = transientPersona
            };
            return c;
        }

        #endregion
    }
}