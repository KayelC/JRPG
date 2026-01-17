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
    /// The persistent storage authority for the Demonic Compendium.
    /// Handles deep-cloning of demon states and dynamic recall cost calculation.
    /// Uses normalized Species IDs (Persona IDs) as the unique registry keys.
    /// </summary>
    public class CompendiumRegistry
    {
        // Key: Normalized Species ID (e.g., "pixie", not "E_pixie")
        // Value: The snapshot of the Combatant
        private readonly Dictionary<string, Combatant> _demonEntries;

        private readonly IGameIO _io;

        public CompendiumRegistry(IGameIO io)
        {
            _io = io;
            _demonEntries = new Dictionary<string, Combatant>(StringComparer.OrdinalIgnoreCase);
        }

        #region Registration Logic

        /// <summary>
        /// Saves a permanent deep-copy snapshot of a demon's current state.
        /// Feature: Normalizes the ID to ensure species-level uniqueness.
        /// </summary>
        public void RegisterDemon(Combatant demon)
        {
            if (demon == null || demon.Class != ClassType.Demon)
            {
                _io.WriteLine("Invalid entity. Only demons can be registered in the Compendium.", ConsoleColor.Red);
                return;
            }

            // High Fidelity Fix: Ensure we are using the base species ID, not the instance/enemy ID.
            string speciesId = ResolveSpeciesId(demon);

            // Create an immutable snapshot
            Combatant snapshot = CloneCombatant(demon);
            snapshot.SourceId = speciesId; // Ensure the snapshot itself is normalized

            if (_demonEntries.ContainsKey(speciesId))
            {
                _demonEntries[speciesId] = snapshot;
                _io.WriteLine($"{demon.Name} data has been updated in the registry.", ConsoleColor.Cyan);
            }
            else
            {
                _demonEntries.Add(speciesId, snapshot);
                _io.WriteLine($"{demon.Name} has been recorded in the Compendium.", ConsoleColor.Green);
            }

            _io.Wait(600);
        }

        #endregion

        #region Recall and Cost Logic

        /// <summary>
        /// Calculates the Macca cost to recall a demon using the power-scaling formula.
        /// </summary>
        public int CalculateRecallCost(string speciesId)
        {
            string cleanId = speciesId.Replace("E_", ""); // Safety normalization

            if (!_demonEntries.TryGetValue(cleanId, out var snapshot))
            {
                return 0;
            }

            // 1. Get Base Price from Database (Fallback to 2000 if not in shop)
            int basePrice = 2000;
            var shopEntry = Database.ShopInventory.FirstOrDefault(s => s.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase));
            if (shopEntry != null)
            {
                basePrice = shopEntry.BasePrice;
            }

            // 2. Calculate Level Premium
            int levelMod = snapshot.Level * 100;

            // 3. Calculate Stat Premium
            int statsSum = snapshot.CharacterStats.Values.Sum();
            int statsMod = statsSum * 50;

            // 4. Calculate Skill Premium
            int skillCount = snapshot.GetConsolidatedSkills().Count;
            int skillMod = skillCount * 200;

            return basePrice + levelMod + statsMod + skillMod;
        }

        /// <summary>
        /// Retrieves a deep-copy of a registered demon for recruitment.
        /// </summary>
        public Combatant GetRecallEntry(string speciesId)
        {
            string cleanId = speciesId.Replace("E_", ""); // Safety normalization

            if (_demonEntries.TryGetValue(cleanId, out var snapshot))
            {
                return CloneCombatant(snapshot);
            }

            return null;
        }

        #endregion

        #region Retrieval and Metadata

        public List<Combatant> GetAllRegisteredDemons()
        {
            return _demonEntries.Values
                .OrderBy(d => d.Level)
                .ThenBy(d => d.Name)
                .ToList();
        }

        public bool HasEntry(string speciesId)
        {
            string cleanId = speciesId.Replace("E_", "");
            return _demonEntries.ContainsKey(cleanId);
        }

        #endregion

        #region Normalization and Cloning Kernels

        /// <summary>
        /// Resolves the base species ID for a combatant. 
        /// Prefers the ActivePersona's identity over the instance SourceId.
        /// </summary>
        private string ResolveSpeciesId(Combatant c)
        {
            // If the demon has an active persona template, that is its true species ID.
            // Since Persona instances don't store their ID, we strip the "E_" from SourceId
            // or rely on the fact that player-owned demons should be normalized.
            return c.SourceId.Replace("E_", "").ToLower();
        }

        private Combatant CloneCombatant(Combatant original)
        {
            Combatant clone = new Combatant(original.Name, original.Class)
            {
                SourceId = original.SourceId.Replace("E_", ""), // Force normalization on clone
                Level = original.Level,
                Exp = original.Exp,
                StatPoints = original.StatPoints,
                BaseHP = original.BaseHP,
                BaseSP = original.BaseSP,
                OwnerId = original.OwnerId,
                BattleControl = original.BattleControl,
                Controller = original.Controller,
                ActivePersona = original.ActivePersona
            };

            foreach (var stat in original.CharacterStats)
            {
                clone.CharacterStats[stat.Key] = stat.Value;
            }

            foreach (var skill in original.ExtraSkills)
            {
                clone.ExtraSkills.Add(skill);
            }

            clone.RecalculateResources();
            clone.CurrentHP = clone.MaxHP;
            clone.CurrentSP = clone.MaxSP;

            return clone;
        }

        #endregion
    }
}