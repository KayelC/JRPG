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
    /// Manages the demonic registry. 
    /// Handles the registration of custom demon states and the monetary recall of stored demons.
    /// </summary>
    public class CompendiumManager
    {
        // Key: SourceId (The unique identifier for the demon species)
        // Value: A snapshot of the Combatant at the time of registration
        private readonly Dictionary<string, Combatant> _registry = new Dictionary<string, Combatant>();

        private readonly EconomyManager _economy;
        private readonly IGameIO _io;

        public CompendiumManager(EconomyManager economy, IGameIO io)
        {
            _economy = economy;
            _io = io;
        }

        #region Registration Logic

        /// <summary>
        /// Saves a high-fidelity snapshot of a demon. 
        /// If the demon is already registered, it overwrites the previous entry.
        /// </summary>
        public void RegisterDemon(Combatant demon)
        {
            if (demon.Class != ClassType.Demon)
            {
                _io.WriteLine("Only entities of the Demon class can be registered.", ConsoleColor.Red);
                return;
            }

            // Create a deep-copy snapshot to ensure future changes to the active demon
            // do not affect the stored compendium entry.
            Combatant snapshot = CloneCombatant(demon);

            if (_registry.ContainsKey(demon.SourceId))
            {
                _registry[demon.SourceId] = snapshot;
                _io.WriteLine($"{demon.Name} has been updated in the Compendium.", ConsoleColor.Cyan);
            }
            else
            {
                _registry.Add(demon.SourceId, snapshot);
                _io.WriteLine($"{demon.Name} has been registered for the first time.", ConsoleColor.Green);
            }

            _io.Wait(600);
        }

        #endregion

        #region Recall Logic

        /// <summary>
        /// Calculates the Macca cost to recall a demon based on its power level.
        /// Formula: BaseCost + (Level * 100) + (TotalStats * 50) + (SkillCount * 200).
        /// </summary>
        public int CalculateRecallCost(string sourceId)
        {
            if (!_registry.TryGetValue(sourceId, out var snapshot))
                return 0;

            // Base price pulled from Database (defaulting to 1000 if not found)
            int basePrice = 1000;
            var shopEntry = Database.ShopInventory.FirstOrDefault(s => s.Id == sourceId);
            if (shopEntry != null) basePrice = shopEntry.BasePrice;

            int levelMod = snapshot.Level * 100;

            int statsSum = 0;
            foreach (var stat in snapshot.CharacterStats.Values) statsSum += stat;
            int statsMod = statsSum * 50;

            int skillMod = snapshot.GetConsolidatedSkills().Count * 200;

            return basePrice + levelMod + statsMod + skillMod;
        }

        /// <summary>
        /// Attempts to recall a demon from the registry.
        /// Validates funds and returns a fresh Combatant instance based on the snapshot.
        /// </summary>
        public Combatant RecallDemon(string sourceId)
        {
            if (!_registry.TryGetValue(sourceId, out var snapshot))
            {
                _io.WriteLine("Demon not found in Compendium.", ConsoleColor.Red);
                return null;
            }

            int cost = CalculateRecallCost(sourceId);

            if (_economy.Macca < cost)
            {
                _io.WriteLine($"Insufficient funds. Recall requires {cost} Macca.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            if (_economy.SpendMacca(cost))
            {
                _io.WriteLine($"Recalling {snapshot.Name}...", ConsoleColor.Cyan);
                _io.Wait(1000);
                return CloneCombatant(snapshot);
            }

            return null;
        }

        #endregion

        #region Metadata and Cloning

        /// <summary>
        /// Returns the full list of registered demons for the UI.
        /// </summary>
        public List<Combatant> GetRegisteredEntries()
        {
            return _registry.Values.OrderBy(d => d.Level).ToList();
        }

        public bool IsRegistered(string sourceId)
        {
            return _registry.ContainsKey(sourceId);
        }

        /// <summary>
        /// Helper method to create a deep copy of a Combatant.
        /// This ensures the Compendium remains an immutable archive.
        /// </summary>
        private Combatant CloneCombatant(Combatant original)
        {
            Combatant clone = new Combatant(original.Name, original.Class)
            {
                SourceId = original.SourceId,
                Level = original.Level,
                Exp = original.Exp,
                StatPoints = original.StatPoints,
                BaseHP = original.BaseHP,
                BaseSP = original.BaseSP,
                CurrentHP = original.MaxHP, // Recalled demons are fully healed
                CurrentSP = original.MaxSP,
                OwnerId = original.OwnerId,
                ActivePersona = original.ActivePersona // References the same PersonaData definition
            };

            // Copy Stats
            foreach (var stat in original.CharacterStats)
            {
                clone.CharacterStats[stat.Key] = stat.Value;
            }

            // Copy Skills
            foreach (var skill in original.ExtraSkills)
            {
                clone.ExtraSkills.Add(skill);
            }

            clone.RecalculateResources();
            return clone;
        }

        #endregion
    }
}