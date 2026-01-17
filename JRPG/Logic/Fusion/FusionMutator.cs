using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The state-mutation authority for the Fusion Sub-System.
    /// Handles the atomic transactions for participant consumption, child instantiation,
    /// and class-specific stock management (DemonStock vs PersonaStock).
    /// </summary>
    public class FusionMutator
    {
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly IGameIO _io;

        public FusionMutator(PartyManager partyManager, EconomyManager economy, IGameIO io)
        {
            _partyManager = partyManager;
            _economy = economy;
            _io = io;
        }

        #region Stock Access Management

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
        /// Retrieves the list of fusible entities for a Persona User or WildCard.
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

        #region Fusion Execution (Atomic Transactions)

        /// <summary>
        /// Commits the fusion ritual to the game state.
        /// Dispatches the transaction to specific logic paths based on the owner's ClassType.
        /// </summary>
        public void ExecuteFusion(Combatant owner, List<object> materials, string resultId, List<string> chosenSkills, Combatant sacrifice = null)
        {
            switch (owner.Class)
            {
                case ClassType.Operator:
                    // Operators perform Demon-to-Demon fusion
                    List<Combatant> demonMaterials = materials.Cast<Combatant>().ToList();
                    ExecuteDemonToDemonFusion(owner, demonMaterials, resultId, chosenSkills, sacrifice);
                    break;

                case ClassType.PersonaUser:
                case ClassType.WildCard:
                    // Spiritualists perform Persona-to-Persona fusion
                    List<Persona> personaMaterials = materials.Cast<Persona>().ToList();
                    ExecutePersonaToPersonaFusion(owner, personaMaterials, resultId, chosenSkills);
                    break;

                default:
                    _io.WriteLine($"Ritual Aborted: The {owner.Class} class is not authorized for demonic synthesis.", ConsoleColor.Red);
                    break;
            }
        }

        /// <summary>
        /// Logic for consuming biological Demon entities to create a new Combatant.
        /// </summary>
        private void ExecuteDemonToDemonFusion(Combatant owner, List<Combatant> materials, string resultId, List<string> chosenSkills, Combatant sacrifice)
        {
            // 1. Transaction Start: Remove all materials from the world
            List<Combatant> allParticipants = new List<Combatant>(materials);
            if (sacrifice != null) allParticipants.Add(sacrifice);

            foreach (var participant in allParticipants)
            {
                // Remove from active battlefield if present
                if (_partyManager.ActiveParty.Contains(participant))
                {
                    _partyManager.ReturnDemon(owner, participant);
                }

                // Ensure removal from stock
                owner.DemonStock.Remove(participant);
            }

            // 2. Transaction Phase: Instantiate Child
            Combatant child = Combatant.CreateDemon(resultId, Database.Personas[resultId].Level);

            // 3. Chosen Skill Injection
            foreach (var skill in chosenSkills)
            {
                if (!child.ExtraSkills.Contains(skill))
                {
                    child.ExtraSkills.Add(skill);
                }
            }

            // 4. Sacrifice Logic: Grant EXP bonus based on sacrifice power
            if (sacrifice != null)
            {
                int expBonus = (int)(sacrifice.Level * 250);
                child.GainExp(expBonus);
            }

            // 5. Finalize Child Entity
            child.RecalculateResources();
            child.CurrentHP = child.MaxHP;
            child.CurrentSP = child.MaxSP;

            // 6. Transaction End: Placement
            if (!_partyManager.SummonDemon(owner, child))
            {
                // Fallback to stock if party is full
                owner.DemonStock.Add(child);
                _io.WriteLine($"{child.Name} has been manifested and sent to stock.", ConsoleColor.Cyan);
            }
            else
            {
                _io.WriteLine($"{child.Name} has joined your active party!", ConsoleColor.Green);
            }
        }

        /// <summary>
        /// Logic for consuming spiritual Persona masks to create a new Persona.
        /// </summary>
        private void ExecutePersonaToPersonaFusion(Combatant owner, List<Persona> materials, string resultId, List<string> chosenSkills)
        {
            // 1. Transaction Start: Remove parent personas
            foreach (var persona in materials)
            {
                // If the parent was equipped, unequip it
                if (owner.ActivePersona == persona)
                {
                    owner.ActivePersona = null;
                }

                owner.PersonaStock.Remove(persona);
            }

            // 2. Transaction Phase: Create new Persona essence
            PersonaData template = Database.Personas[resultId];
            Persona child = template.ToPersona();

            // 3. Chosen Skill Injection
            foreach (var skill in chosenSkills)
            {
                if (!child.SkillSet.Contains(skill))
                {
                    child.SkillSet.Add(skill);
                }
            }

            // 4. Transaction End: Placement
            owner.PersonaStock.Add(child);

            // Auto-equip for UX convenience if current slot is vacant
            if (owner.ActivePersona == null)
            {
                owner.ActivePersona = child;
                _io.WriteLine($"{child.Name} has been manifested and equipped.", ConsoleColor.Green);
            }
            else
            {
                _io.WriteLine($"{child.Name} has been added to your Persona stock.", ConsoleColor.Cyan);
            }

            owner.RecalculateResources();
        }

        #endregion

        #region Compendium Recall Logic

        /// <summary>
        /// Finalizes the recall transaction from the Compendium.
        /// Feature: Correctly forks logic to populate DemonStock or PersonaStock.
        /// </summary>
        public bool FinalizeRecall(Combatant owner, Combatant snapshot, int cost)
        {
            if (_economy.Macca < cost)
            {
                _io.WriteLine("Recall Aborted: Insufficient Macca.", ConsoleColor.Red);
                return false;
            }

            if (_economy.SpendMacca(cost))
            {
                if (owner.Class == ClassType.Operator)
                {
                    // Operators receive the Demon entity (Combatant)
                    if (!_partyManager.SummonDemon(owner, snapshot))
                    {
                        owner.DemonStock.Add(snapshot);
                    }
                }
                else
                {
                    // PersonaUsers/WildCards receive the spiritual essence (Persona)
                    // We must extract the ActivePersona from the Combatant snapshot
                    Persona essence = snapshot.ActivePersona;

                    // Fidelity Requirement: Deep-copy skills from the combatant back to the persona 
                    // This ensures learned skills from the registration snapshot are preserved.
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
}