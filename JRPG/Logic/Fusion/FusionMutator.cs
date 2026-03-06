using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The state-mutation authority for the Fusion Sub-System.
    /// Strategy Runner that executes fusion transactions via a Registry.
    /// Handles the atomic transactions for participant consumption, child instantiation, 
    /// and class-specific stock management (DemonStock vs PersonaStock).
    /// </summary>
    public class FusionMutator
    {
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly IFusionMessenger _messenger;
        private readonly FusionStrategyRegistry _registry;

        public FusionMutator(PartyManager partyManager, EconomyManager economy, IFusionMessenger messenger)
        {
            _partyManager = partyManager;
            _economy = economy;
            _messenger = messenger;
            _registry = new FusionStrategyRegistry();
        }

        #region Stock Access Management (Preserved for Conductor usage)

        /// <summary>
        /// Retrieves the list of fusible entities for an Operator.
        /// Sources: Active Party (Demons only) and the digital DemonStock.
        /// </summary>
        public List<Combatant> GetFusibleDemonPool(Combatant owner)
        {
            List<Combatant> pool = new List<Combatant>();

            // 1. Add demons currently active in the battle party
            var activeDemons = _partyManager.ActiveParty
                .Where(c => c.Class == ClassType.Demon)
                .ToList();

            pool.AddRange(activeDemons);

            // 2. Add demons stored in the owner's stock
            if (owner.DemonStock != null)
            {
                pool.AddRange(owner.DemonStock);
            }

            return pool.Distinct().ToList();
        }

        /// <summary>
        /// Retrieves the list of fusible entities for a WildCard.
        /// Sources: The currently manifested ActivePersona and the internal PersonaStock.
        /// </summary>
        public List<Persona> GetFusiblePersonaPool(Combatant owner)
        {
            List<Persona> pool = new List<Persona>();

            // 1. Add the currently equipped persona
            if (owner.ActivePersona != null)
            {
                pool.Add(owner.ActivePersona);
            }

            // 2. Add personas stored in the owner's internal stock
            if (owner.PersonaStock != null)
            {
                pool.AddRange(owner.PersonaStock);
            }

            return pool.Distinct().ToList();
        }

        #endregion

        #region Fusion Execution

        /// <summary>
        /// Commits the fusion ritual to the game state.
        /// Dispatches the transaction to specific logic paths based on the owner's ClassType.
        /// Executes a fusion strategy based on the operation type.
        /// </summary>
        public void ExecuteFusionTransaction(FusionContext context, FusionOperationType type)
        {
            var strategy = _registry.GetStrategy(type);
            if (strategy != null)
            {
                strategy.Execute(context);
            }
            else
            {
                _messenger.Publish($"[System Error] No strategy found for {type}", ConsoleColor.Red);
            }
        }

        #region Compendium Recall Logic

        /// <summary>
        /// Finalizes the recall transaction from the Compendium.
        /// Uses Messenger for all feedback.
        /// </summary>
        public bool FinalizeRecall(Combatant owner, Combatant snapshot, int cost)
        {
            if (_economy.Macca < cost)
            {
                _messenger.Publish("Recall Aborted: Insufficient Macca.", ConsoleColor.Red);
                return false;
            }

            if (_economy.SpendMacca(cost))
            {
                if (owner.Class == ClassType.Operator)
                {
                    // Operators receive the Demon entity
                    if (!_partyManager.SummonDemon(owner, snapshot))
                    {
                        owner.DemonStock.Add(snapshot);
                    }
                }
                else
                {
                    // WildCards receive the Persona
                    Persona essence = snapshot.ActivePersona;

                    var combinedSkills = snapshot.GetConsolidatedSkills();
                    essence.SkillSet.Clear();
                    foreach (var s in combinedSkills)
                    {
                        essence.SkillSet.Add(s);
                    }

                    owner.PersonaStock.Add(essence);
                }

                return true;
            }

            return false;
        }

        #endregion
    }
        #endregion
}