using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The Root Orchestrator for the Fusion Sub-System.
    /// Manages the high-level sequences for Binary Fusion, Sacrificial Fusion, 
    /// Compendium registration, and Recall.
    /// Decoupled narration via IFusionMessenger and logic via FusionMutator strategies.
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

        // Decoupled Components
        private readonly IFusionMessenger _messenger;
        private readonly FusionLogger _logger;

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

            // 1. Initialize Communication Tower
            _messenger = new FusionMessenger();
            _logger = new FusionLogger(_io);
            _logger.Subscribe(_messenger);

            // 2. Initialize Logic & Bridges
            _calculator = new FusionCalculator(_io, _messenger);
            _mutator = new FusionMutator(_partyManager, _economy, _messenger);
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
                    case "Binary Fusion": PerformFusionRitual(isSacrificial: false); break;
                    case "Sacrificial Fusion": PerformFusionRitual(isSacrificial: true); break;
                    case "Browse Compendium": HandleCompendiumRecall(); break;
                    case "Register Demon": HandleRegistration(); break;
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
            List<object> participantPool = new List<object>();

            while (true) // OUTER LOOP: Participant Selection
            {
                // 1. Establish the pool of participants based on Character Class
                // Logic: Source pools are class-dependent to ensure stock integrity.
                if (_player.Class == ClassType.Operator)
                {
                    // Operators draw from Active Party and DemonStock
                    var demons = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                    demons.AddRange(_player.DemonStock);
                    participantPool = demons.Distinct().Cast<object>().ToList();
                }
                else if (_player.Class == ClassType.WildCard)
                {
                    // WildCards draw from ActivePersona and PersonaStock
                    var personas = new List<Persona>();
                    if (_player.ActivePersona != null) personas.Add(_player.ActivePersona);
                    personas.AddRange(_player.PersonaStock);
                    participantPool = personas.Distinct().Cast<object>().ToList();
                }

                if (participantPool.Count < (isSacrificial ? 3 : 2))
                {
                    _messenger.Publish($"You need at least {(isSacrificial ? "three" : "two")} participants.", ConsoleColor.Red, 1000);
                    return;
                }

                // 2. Participant Selection
                List<object> parents = new List<object>();

                // Select Parent 1
                object? p1 = _uiBridge.SelectRitualParticipant<object>(participantPool, "CHOOSE THE FIRST PARTICIPANT:", parents);
                if (p1 == null) return;
                parents.Add(p1);

                // Select Parent 2
                object? p2 = _uiBridge.SelectRitualParticipant<object>(participantPool, "CHOOSE THE SECOND PARTICIPANT:", parents);
                if (p2 == null) continue; // Go back to start of parent selection
                parents.Add(p2);

                // Select Sacrifice (Full Moon only)
                object? sacrifice = null;
                if (isSacrificial)
                {
                    // Filter from participantPool as object so WildCards can sacrifice Personas
                    List<object> sacrificePool = participantPool.Where(x => !parents.Contains(x)).ToList();
                    sacrifice = _uiBridge.SelectRitualParticipant<object>(sacrificePool, "CHOOSE THE SACRIFICIAL OFFERING:", parents);

                    if (sacrifice == null) continue;
                }

                // 3. Result Calculation (now returns operation type and target ID)
                // We create transient Combatants for Persona participants so the Calculator can remain type-pure.
                Combatant parentA = (p1 is Combatant c1) ? c1 : CreateTransientCombatant((Persona)p1);
                Combatant parentB = (p2 is Combatant c2) ? c2 : CreateTransientCombatant((Persona)p2);

                var (operation, targetId, isAccident) = _calculator.CalculateResult(parentA, parentB, MoonPhaseSystem.CurrentPhase);

                // If NoFusionPossible, immediately return.
                if (operation == FusionOperationType.NoFusionPossible || string.IsNullOrEmpty(targetId))
                {
                    _messenger.Publish("The spirits remain silent. This combination yields no result.", ConsoleColor.Red, 1000);
                    continue;
                }

                // --- 4. Identify Result and Inherent Skills early ---
                List<string> inherentSkills = new List<string>();
                PersonaData? resultTemplate = null;

                if (operation == FusionOperationType.CreateNewDemon)
                {
                    Database.Personas.TryGetValue(targetId.ToLower(), out resultTemplate);
                    inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
                }
                else if (operation == FusionOperationType.StatBoostFusion)
                {
                    // In Stat Boost, the "Inherent Skills" are the skills the target already has
                    Combatant boostTarget = (parentA.ActivePersona.Race == "Mitama") ? parentB : parentA;
                    inherentSkills = boostTarget.GetConsolidatedSkills();
                }
                else if (operation == FusionOperationType.RankUpParent || operation == FusionOperationType.RankDownParent)
                {
                    // For Rank mutations, inherent skills are the base kit of the result tier
                    Database.Personas.TryGetValue(targetId.ToLower(), out resultTemplate);
                    inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
                }

                while (true) // INNER LOOP: Skill Selection <-> Preview
                {
                    var parentList = new List<Combatant> { parentA, parentB };
                    if (sacrifice != null) parentList.Add((sacrifice is Combatant sc) ? sc : CreateTransientCombatant((Persona)sacrifice));

                    var pool = _calculator.GetInheritableSkills(parentList.ToArray());
                    int maxSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray()) + (isSacrificial ? 2 : 0);

                    List<string>? chosenSkills = _uiBridge.SelectInheritedSkills(pool, Math.Min(8, maxSlots), inherentSkills);
                    if (chosenSkills == null) break;

                    // 5. Stage Result for UI Preview (Incorporating Sacrificial XP Transfer)
                    Combatant? staged = CreateStagedDemon(operation, targetId, p1, p2, sacrifice, chosenSkills);
                    if (staged == null) { _messenger.Publish("Error staging fusion result.", ConsoleColor.Red); break; }

                    // Confirmation Logic
                    int confirm = _uiBridge.ConfirmRitual(staged,
                        (parentA.ActivePersona.Race != "Element") ? parentA : parentB, chosenSkills,
                        _player.Level, operation);

                    if (confirm == 1) continue; // Back to Skills
                    if (confirm == 2) break;    // Back to Selection

                    // --- EXECUTION ---
                    _messenger.Publish("The sacrificial circle glows with a cold, blue light...", delay: 1200, clearScreen: true);
                    _messenger.Publish("The participants are reduced to pure spiritual data...", delay: 1200);
                    _messenger.Publish("The streams of energy collide and begin to merge...", delay: 1200);

                    if (isAccident) _messenger.Publish("!!! WARNING: LUNAR INTERFERENCE DETECTED !!!", ConsoleColor.Red, 2000);

                    var context = new FusionContext(_player, parents, sacrifice, chosenSkills, targetId, _messenger, _partyManager);
                    _mutator.ExecuteFusionTransaction(context, operation);

                    _messenger.Publish(null, delay: 1500, waitForInput: true);
                    return; // Fusion complete
                }
            }
        }

        /// <summary>
        /// Creates a high-fidelity dummy combatant for the UI confirmation screen.
        /// Simulates the exact results of the fusion strategy before it is executed.
        /// </summary>
        private Combatant? CreateStagedDemon(FusionOperationType op, string id, object p1, object p2, object? sacrifice, List<string> skills)
        {
            if (!Database.Personas.TryGetValue(id.ToLower(), out var template)) return null;

            // 1. Initialize the base result from the template
            Combatant staged = Combatant.CreatePlayerDemon(id, template.Level);

            // 2. Apply the manually selected inherited skills
            staged.ExtraSkills.Clear();
            staged.ExtraSkills.AddRange(skills);

            // 3. Logic Branching: Match the Strategy math exactly
            if (op == FusionOperationType.StatBoostFusion)
            {
                // Identify which parent is the 'Target' and which is the 'Mitama'
                Combatant targetCom = (p1 is Combatant c1 && c1.ActivePersona.Race != "Mitama") ? c1 : (Combatant)p2;
                Combatant mitamaCom = (p1 is Combatant m1 && m1.ActivePersona.Race == "Mitama") ? m1 : (Combatant)p2;

                // Copy the target's actual current state to the dummy
                staged.Exp = targetCom.Exp;
                foreach (var st in targetCom.CharacterStats) staged.CharacterStats[st.Key] = st.Value;
                foreach (var mod in targetCom.ActivePersona.StatModifiers) staged.ActivePersona.StatModifiers[mod.Key] = mod.Value;

                // Simulate the Mitama boost on the dummy
                ApplyPreviewBoost(staged, mitamaCom.ActivePersona!.Name);
                staged.RecalculateResources();
            }
            else if (op == FusionOperationType.RankUpParent || op == FusionOperationType.RankDownParent)
            {
                // Identify the target undergoing the rank change
                Combatant original = (p1 is Combatant c1 && c1.ActivePersona.Race != "Element") ? c1 : (Combatant)p2;

                // Carry over modifiers to the higher/lower tier version
                foreach (var mod in original.ActivePersona.StatModifiers) staged.ActivePersona.StatModifiers[mod.Key] = mod.Value;

                staged.RecalculateResources();
            }

            // Apply sacrificial XP breakthrough math to the preview dummy for honest UI feedback
            if (sacrifice != null)
            {
                int earnedXP = (sacrifice is Combatant com) ? com.LifetimeEarnedExp : ((Persona)sacrifice).LifetimeEarnedExp;
                int transferXP = (int)(earnedXP / 1.5);
                staged.GainExp(transferXP);
            }

            return staged;
        }

        private void ApplyPreviewBoost(Combatant demon, string mitamaName)
        {
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();
            switch (mitamaName)
            {
                case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
                case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
                case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
                case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
            }

            foreach (var entry in boosts)
            {
                var mods = demon.ActivePersona!.StatModifiers;
                int current = mods.GetValueOrDefault(entry.Key, 0);
                mods[entry.Key] = Math.Min(40, current + entry.Value);
            }
        }

        #endregion

        #region Compendium and Helpers

        /// <summary>
        /// Handles the UI flow and logic for Compendium recruitment.
        /// Logic: Forks slot-checking based on player class and stock type.
        /// </summary>
        private void HandleCompendiumRecall()
        {
            Combatant? entry = _uiBridge.ShowCompendiumRecallMenu();
            if (entry == null) return;

            int cost = _compendium.CalculateRecallCost(entry.SourceId);

            bool canRecall = _player.Class switch
            {
                ClassType.Operator => _partyManager.ActiveParty.Count < 4 || _partyManager.HasOpenDemonStockSlot(_player),
                ClassType.WildCard => _partyManager.HasOpenPersonaStockSlot(_player),
                _ => false
            };

            if (!canRecall) { _messenger.Publish("You have no vessel capable of containing this soul.", ConsoleColor.Red, 1000); return; }

            Combatant? snapshot = _compendium.GetRecallEntry(entry.SourceId);
            if (snapshot != null && _mutator.FinalizeRecall(_player, snapshot, cost))
            {
                _messenger.Publish($"{snapshot.Name} has been materialized.", ConsoleColor.Cyan, 800);
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
                var pool = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                pool.AddRange(_player.DemonStock);
                Combatant? selected = _uiBridge.SelectDemonToRegister(pool.Distinct().ToList());
                if (selected != null) _compendium.RegisterDemon(selected);
            }
            else if (_player.Class == ClassType.WildCard)
            {
                // Registration source for WildCards is their PersonaStock
                Persona? p = _uiBridge.SelectRitualParticipant<Persona>(_player.PersonaStock, "SELECT PERSONA TO RECORD:", new List<Persona>());
                if (p != null) _compendium.RegisterDemon(CreateTransientCombatant(p));
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
            var transient = new Persona
            {
                Name = p.Name,
                Level = p.Level,
                Race = p.Race,
                Rank = p.Rank,
                Exp = p.Exp,
                LifetimeEarnedExp = p.LifetimeEarnedExp
            };
            transient.SkillSet.AddRange(p.SkillSet);
            foreach (var stat in p.StatModifiers) transient.StatModifiers[stat.Key] = stat.Value;
            return new Combatant(p.Name, ClassType.Demon)
            {
                Level = p.Level,
                ActivePersona = transient,
                SourceId = p.Name,
                LifetimeEarnedExp = p.LifetimeEarnedExp
            };
        }

        #endregion
    }
}