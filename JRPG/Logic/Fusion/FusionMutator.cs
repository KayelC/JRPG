using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// </summary>
        public void ExecuteFusion(Combatant owner, List<object> materials, string resultId, List<string> chosenSkills, Combatant? sacrifice = null)
        {
            switch (owner.Class)
            {
                case ClassType.Operator:
                    List<Combatant> demonMaterials = materials.Cast<Combatant>().ToList();
                    ExecuteDemonToDemonFusion(owner, demonMaterials, resultId, chosenSkills, sacrifice);
                    break;

                case ClassType.WildCard:
                    List<Persona> personaMaterials = materials.Cast<Persona>().ToList();
                    ExecutePersonaToPersonaFusion(owner, personaMaterials, resultId, chosenSkills);
                    break;

                default:
                    _io.WriteLine($"Ritual Aborted: The {owner.Class} class is not authorized for this synthesis.", ConsoleColor.Red);
                    break;
            }
        }

        /// <summary>
        /// Logic for consuming biological Demon entities to create a new Allied Combatant.
        /// Allies follow progression rules (start with base skills).
        /// </summary>
        private void ExecuteDemonToDemonFusion(Combatant owner, List<Combatant> materials, string resultId, List<string> chosenSkills, Combatant? sacrifice)
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

            // 2. Transaction Phase: Instantiate Allied Child
            // Use CreatePlayerDemon to ensure progression rules are followed.
            Combatant child = Combatant.CreatePlayerDemon(resultId, Database.Personas[resultId.ToLower()].Level);

            // 3. Chosen Skill Injection (Inheritance)
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

        // Logic for consuming spiritual Persona masks to create a new Persona.
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

            // 2. Transaction Phase: Create new Persona essence from template
            PersonaData template = Database.Personas[resultId.ToLower()];
            Persona child = template.ToPersona();

            // 3. Chosen Skill Injection (Inheritance)
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

        /// <summary>
        /// Executes a "Rank Up" fusion, replacing an existing demon/persona with its next higher rank counterpart.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The specific combatant demon (not elemental) to rank up.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteRankUpFusion(Combatant owner, object parentToModify, List<string> chosenSkills, Combatant? sacrifice)
        {
            ExecuteRankChange(owner, parentToModify, 1, chosenSkills, sacrifice);
        }

        /// <summary>
        /// Executes a "Rank Down" fusion, replacing an existing demon/persona with its next lower rank counterpart.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The specific combatant demon (not elemental) to rank down.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteRankDownFusion(Combatant owner, object parentToModify, List<string> chosenSkills, Combatant? sacrifice)
        {
            ExecuteRankChange(owner, parentToModify, -1, chosenSkills, sacrifice);
        }

        /// <summary>
        /// Handles the core logic for Rank Up/Down fusions, replacing the parent with a new entity of target rank.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The original combatant demon undergoing the rank change.</param>
        /// <param name="rankDirection">+1 for Rank Up, -1 for Rank Down.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        private void ExecuteRankChange(Combatant owner, object parentToModify, int rankDirection, List<string> chosenSkills, Combatant? sacrifice)
        {
            if (owner.Class == ClassType.Operator)
            {
                Combatant originalDemon = (Combatant)parentToModify;

                // The elemental has already been consumed. We only consume the sacrifice here.
                if (sacrifice != null)
                {
                    if (_partyManager.ActiveParty.Contains(sacrifice)) _partyManager.ReturnDemon(owner, sacrifice);
                    owner.DemonStock.Remove(sacrifice);
                }

                string parentRace = originalDemon.ActivePersona.Race;
                int targetRank = originalDemon.ActivePersona.Rank + rankDirection;

                var nextRankDemonData = Database.Personas.Values
                    .Where(p => p.Race == parentRace && p.Rank == targetRank)
                    .OrderBy(p => p.Level)
                    .FirstOrDefault();

                if (nextRankDemonData == null) return; // Defensive return

                Combatant newDemon = Combatant.CreatePlayerDemon(nextRankDemonData.Id, nextRankDemonData.Level);

                newDemon.ExtraSkills.Clear();
                newDemon.ExtraSkills.AddRange(chosenSkills);
                newDemon.ExtraSkills = newDemon.ExtraSkills.Distinct().ToList();

                if (sacrifice != null)
                {
                    int expBonus = (int)(sacrifice.Level * 250);
                    newDemon.GainExp(expBonus);
                }

                _io.WriteLine($"{originalDemon.Name} has transformed into {newDemon.Name}!", ConsoleColor.Magenta);
                ReplaceDemonInState(owner, originalDemon, newDemon);
            }
            else if (owner.Class == ClassType.WildCard)
            {
                Persona originalPersona = (Persona)parentToModify;
                int targetRank = originalPersona.Rank + rankDirection;

                var nextRankData = Database.Personas.Values
                    .Where(p => p.Race == originalPersona.Race && p.Rank == targetRank)
                    .OrderBy(p => p.Level)
                    .FirstOrDefault();

                if (nextRankData == null) return;

                Persona newPersona = nextRankData.ToPersona();

                newPersona.SkillSet.Clear();
                newPersona.SkillSet.AddRange(chosenSkills);
                newPersona.SkillSet = newPersona.SkillSet.Distinct().ToList();

                _io.WriteLine($"{originalPersona.Name} has transformed into {newPersona.Name}!", ConsoleColor.Magenta);
                ReplacePersonaInState(owner, originalPersona, newPersona);
            }
        }

        /// <summary>
        /// Executes a Mitama fusion, boosting specific stats of the target demon or persona.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="demonToBoost">The demon whose stats will be boosted.</param>
        /// <param name="mitamaParent">The Mitama demon being consumed for the boost.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteStatBoostFusion(Combatant owner, object entityToBoost, object mitamaParent, List<string> chosenSkills, Combatant? sacrifice)
        {
            if (owner.Class == ClassType.Operator)
            {
                Combatant demonToBoost = (Combatant)entityToBoost;
                Combatant mitamaDemon = (Combatant)mitamaParent;

                // Consume the Mitama parent and any sacrifice
                if (_partyManager.ActiveParty.Contains(mitamaDemon)) _partyManager.ReturnDemon(owner, mitamaDemon);
                owner.DemonStock.Remove(mitamaDemon);

                if (sacrifice != null)
                {
                    if (_partyManager.ActiveParty.Contains(sacrifice)) _partyManager.ReturnDemon(owner, sacrifice);
                    owner.DemonStock.Remove(sacrifice);
                }

                // Create a new Combatant instance to apply boosts to.
                Combatant boostedDemon = Combatant.CreatePlayerDemon(demonToBoost.SourceId, demonToBoost.Level);
                boostedDemon.Exp = demonToBoost.Exp;

                boostedDemon.ExtraSkills.Clear();
                boostedDemon.ExtraSkills.AddRange(chosenSkills);

                // Ensure the copied persona has the same modifiers as the demonToBoost's active persona
                foreach (var statMod in demonToBoost.ActivePersona.StatModifiers)
                {
                    boostedDemon.ActivePersona.StatModifiers[statMod.Key] = statMod.Value;
                }

                ApplyMitamaBoosts(boostedDemon.ActivePersona, mitamaDemon.ActivePersona.Name);
                boostedDemon.RecalculateResources();

                // Apply sacrifice EXP bonus
                if (sacrifice != null)
                {
                    int expBonus = (int)(sacrifice.Level * 250);
                    boostedDemon.GainExp(expBonus);
                }

                _io.WriteLine($"{demonToBoost.Name}'s stats have been enhanced!", ConsoleColor.Magenta);
                ReplaceDemonInState(owner, demonToBoost, boostedDemon);
            }
            else if (owner.Class == ClassType.WildCard)
            {
                Persona personaToBoost = (Persona)entityToBoost;
                Persona mitamaPersona = (Persona)mitamaParent;

                // Consume Mitama
                if (owner.ActivePersona == mitamaPersona) owner.ActivePersona = null;
                owner.PersonaStock.Remove(mitamaPersona);

                // Clone base template
                var template = Database.Personas.Values.FirstOrDefault(p => p.Name == personaToBoost.Name);
                if (template == null) return;
                Persona newPersona = template.ToPersona();

                // Restore state
                newPersona.Level = personaToBoost.Level;
                newPersona.Exp = personaToBoost.Exp;

                // Apply Chosen skills
                newPersona.SkillSet.Clear();
                newPersona.SkillSet.AddRange(chosenSkills);

                foreach (var statMod in personaToBoost.StatModifiers)
                {
                    newPersona.StatModifiers[statMod.Key] = statMod.Value;
                }

                ApplyMitamaBoosts(newPersona, mitamaPersona.Name);

                _io.WriteLine($"{personaToBoost.Name}'s stats have been enhanced!", ConsoleColor.Magenta);
                ReplacePersonaInState(owner, personaToBoost, newPersona);
            }
        }

        // Centralized application of Mitama Stat Boosts to a Persona Object.
        private void ApplyMitamaBoosts(Persona targetPersona, string mitamaName)
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
                var mods = targetPersona.StatModifiers;
                int current = mods.GetValueOrDefault(entry.Key, 0);
                if (current < 40)
                {
                    mods[entry.Key] = Math.Min(40, current + entry.Value);
                    _io.WriteLine($" -> {entry.Key} increased by {entry.Value}!", ConsoleColor.Cyan);
                }
                else
                {
                    _io.WriteLine($" -> {entry.Key} is already at its maximum!", ConsoleColor.Yellow);
                }
            }
        }

        /// <summary>
        /// Atomically replaces an old demon with a new one in the player's active party or stock.
        /// Preserves party slot if applicable.
        /// </summary>
        private void ReplaceDemonInState(Combatant owner, Combatant oldDemon, Combatant newDemon)
        {
            // Transfer essential live state from old to new.
            newDemon.OwnerId = oldDemon.OwnerId;
            newDemon.Controller = oldDemon.Controller;
            newDemon.BattleControl = oldDemon.BattleControl;

            // If the demon was in the active party, replace it directly in its slot
            if (_partyManager.ActiveParty.Contains(oldDemon))
            {
                int slot = oldDemon.PartySlot;
                _partyManager.ActiveParty[slot] = newDemon;
                newDemon.PartySlot = slot;

                // Also remove from stock if it was also there (e.g. active persona for wild card)
                owner.DemonStock.Remove(oldDemon);
            }
            else // It was in the stock
            {
                owner.DemonStock.Remove(oldDemon);
                owner.DemonStock.Add(newDemon);
            }

            // Clean up the old demon's active party slot if it was in the party (should be -1 after returnDemon)
            oldDemon.PartySlot = -1;
            owner.RecalculateResources();
        }

        // Atomically replaces an old persona with a new one.
        private void ReplacePersonaInState(Combatant owner, Persona oldPersona, Persona newPersona)
        {
            if (owner.ActivePersona == oldPersona)
            {
                owner.ActivePersona = newPersona;
                _io.WriteLine($"{newPersona.Name} has been manifested and equipped.", ConsoleColor.Green);
            }
            else
            {
                owner.PersonaStock.Remove(oldPersona);
                owner.PersonaStock.Add(newPersona);
                _io.WriteLine($"{newPersona.Name} has been added to your Persona stock.", ConsoleColor.Cyan);
            }
            owner.RecalculateResources();
        }

        #endregion

        #region Compendium Recall Logic

        /// <summary>
        /// Finalizes the recall transaction from the Compendium.
        /// Correctly forks logic to populate DemonStock or PersonaStock.
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
}