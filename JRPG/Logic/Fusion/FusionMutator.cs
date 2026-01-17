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
    /// Handles the atomic transaction of removing parents/sacrifices and 
    /// instantiating children across different character classes.
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

        #region Stock Access Logic

        /// <summary>
        /// Retrieves the list of fusible demons for an Operator class.
        /// Source: Active Party + DemonStock.
        /// </summary>
        public List<Combatant> GetFusibleDemonPool(Combatant owner)
        {
            // Start with demons currently on the battlefield
            var pool = _partyManager.ActiveParty
                .Where(c => c.Class == ClassType.Demon)
                .ToList();

            // Add demons sitting in the owner's digital stock
            pool.AddRange(owner.DemonStock);

            return pool.Distinct().ToList();
        }

        /// <summary>
        /// Retrieves the list of fusible personas for PersonaUser/Wildcard classes.
        /// Source: Active Persona + PersonaStock.
        /// </summary>
        public List<Persona> GetFusiblePersonaPool(Combatant owner)
        {
            var pool = new List<Persona>();

            // Include the currently equipped persona if it exists
            if (owner.ActivePersona != null)
            {
                pool.Add(owner.ActivePersona);
            }

            // Add personas sitting in the owner's internal stock
            pool.AddRange(owner.PersonaStock);

            return pool.Distinct().ToList();
        }

        #endregion

        #region Fusion Execution Logic (Atomic Transactions)

        /// <summary>
        /// The primary entry point for committing a fusion to the game state.
        /// Dispatches logic based on the owner's class type.
        /// </summary>
        public void ExecuteFusion(Combatant owner, List<object> parents, string resultId, List<string> chosenSkills, Combatant sacrifice = null)
        {
            switch (owner.Class)
            {
                case ClassType.Operator:
                    // Cast parents to Combatants for Demon Fusion
                    List<Combatant> demonParents = parents.Cast<Combatant>().ToList();
                    ExecuteDemonFusion(owner, demonParents, resultId, chosenSkills, sacrifice);
                    break;

                case ClassType.PersonaUser:
                case ClassType.WildCard:
                    // Cast parents to Personas for Persona Fusion
                    List<Persona> personaParents = parents.Cast<Persona>().ToList();
                    ExecutePersonaFusion(owner, personaParents, resultId, chosenSkills);
                    break;

                default:
                    _io.WriteLine($"The {owner.Class} class is not authorized to perform rituals.", ConsoleColor.Red);
                    break;
            }
        }

        /// <summary>
        /// Handles the consumption of Demon Combatants and creation of a new child Demon.
        /// </summary>
        private void ExecuteDemonFusion(Combatant owner, List<Combatant> parents, string resultId, List<string> chosenSkills, Combatant sacrifice)
        {
            // 1. Purge Parents and Sacrifice from existence
            List<Combatant> allMaterials = new List<Combatant>(parents);
            if (sacrifice != null) allMaterials.Add(sacrifice);

            foreach (var mat in allMaterials)
            {
                // Remove from active battlefield if present
                if (_partyManager.ActiveParty.Contains(mat))
                {
                    _partyManager.ReturnDemon(owner, mat);
                }

                // Remove from stock
                owner.DemonStock.Remove(mat);
            }

            // 2. Instantiate the Child Demon
            // Using the factory method to ensure base stats are initialized correctly
            Combatant child = Combatant.CreateDemon(resultId, Database.Personas[resultId].Level);

            // 3. Inject Chosen Skills (HD Version Deterministic Inheritance)
            foreach (var skillName in chosenSkills)
            {
                if (!child.ExtraSkills.Contains(skillName))
                {
                    child.ExtraSkills.Add(skillName);
                }
            }

            // 4. Apply Sacrificial Bonuses (Nocturne Fidelity)
            if (sacrifice != null)
            {
                // Grant EXP based on the sacrifice's power
                int bonusExp = (int)(sacrifice.Level * 250 * sacrifice.CharacterStats.Values.Average());
                child.GainExp(bonusExp);
            }

            // 5. Finalize Child Resources
            child.RecalculateResources();
            child.CurrentHP = child.MaxHP;
            child.CurrentSP = child.MaxSP;

            // 6. Placement Logic
            if (!_partyManager.SummonDemon(owner, child))
            {
                // If party is full, move to stock
                owner.DemonStock.Add(child);
                _io.WriteLine($"{child.Name} was sent to your digital stock.", ConsoleColor.Cyan);
            }
            else
            {
                _io.WriteLine($"{child.Name} has joined your active party!", ConsoleColor.Green);
            }
        }

        /// <summary>
        /// Handles the consumption of Persona objects and creation of a new Persona.
        /// </summary>
        private void ExecutePersonaFusion(Combatant owner, List<Persona> parents, string resultId, List<string> chosenSkills)
        {
            // 1. Purge Parent Personas
            foreach (var p in parents)
            {
                if (owner.ActivePersona == p) owner.ActivePersona = null;
                owner.PersonaStock.Remove(p);
            }

            // 2. Create the new Persona using the Database Template
            PersonaData data = Database.Personas[resultId];
            Persona child = data.ToPersona();

            // 3. Inject Chosen Skills
            foreach (var skillName in chosenSkills)
            {
                if (!child.SkillSet.Contains(skillName))
                {
                    child.SkillSet.Add(skillName);
                }
            }

            // 4. Placement Logic for Persona Users
            owner.PersonaStock.Add(child);

            // Auto-equip if the slot is now empty for convenience
            if (owner.ActivePersona == null)
            {
                owner.ActivePersona = child;
                _io.WriteLine($"{child.Name} has been manifested and equipped!", ConsoleColor.Green);
            }
            else
            {
                _io.WriteLine($"{child.Name} has been added to your Persona stock.", ConsoleColor.Cyan);
            }

            owner.RecalculateResources();
        }

        #endregion

        #region Compendium Transactions

        /// <summary>
        /// Logic for committing a Compendium Recall transaction.
        /// </summary>
        public bool FinalizeRecall(Combatant owner, Combatant recalledDemon, int cost)
        {
            if (_economy.SpendMacca(cost))
            {
                // Classes are checked to determine where the demon goes
                if (owner.Class == ClassType.Operator)
                {
                    if (!_partyManager.SummonDemon(owner, recalledDemon))
                    {
                        owner.DemonStock.Add(recalledDemon);
                    }
                }
                else
                {
                    // For non-operators, recalled demons default to stock
                    owner.DemonStock.Add(recalledDemon);
                }

                return true;
            }
            return false;
        }

        #endregion
    }
}