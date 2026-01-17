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
    /// The mathematical core of the Fusion Sub-System.
    /// Handles Race-combination lookups, Level-based tiering, 
    /// Skill inheritance limits, and Fusion Accident permutations.
    /// </summary>
    public class FusionEngine
    {
        private readonly IGameIO _io;
        private readonly Random _rnd = new Random();

        // The master lookup table: Dictionary<RaceA, Dictionary<RaceB, ResultRace>>
        private Dictionary<string, Dictionary<string, string>> _fusionTable;

        public FusionEngine(IGameIO io)
        {
            _io = io;
            InitializeFusionTable();
        }

        #region Core Fusion Logic

        /// <summary>
        /// Calculates the resulting demon from a binary fusion.
        /// Logic: Identifies Target Race -> Calculates Target Level -> Finds Nearest Tier.
        /// </summary>
        public (string targetPersonaId, bool isAccident) PredictResult(Combatant parentA, Combatant parentB, int playerLevel)
        {
            string raceA = parentA.ActivePersona?.Arcana ?? "Unknown";
            string raceB = parentB.ActivePersona?.Arcana ?? "Unknown";

            // 1. Identify Resulting Race
            if (!_fusionTable.TryGetValue(raceA, out var branch) || !branch.TryGetValue(raceB, out string resultRace))
            {
                // If combination is invalid, return null to signal "Error/Impossible"
                return (null, false);
            }

            // 2. Determine if an Accident occurs (High-Fidelity: ~1.5% base chance, or higher on Full Moon)
            bool isAccident = _rnd.Next(0, 100) < 2; // Fixed 2% for prototype balance

            // 3. Calculate Target Level Tier
            // SMT III Formula: (LevelA + LevelB) / 2 + 1
            int targetLevel = ((parentA.Level + parentB.Level) / 2) + 1;

            // 4. Fetch all demons of the resulting race from the Database
            var racePool = Database.Personas.Values
                .Where(p => p.Arcana == resultRace)
                .OrderBy(p => p.Level)
                .ToList();

            if (!racePool.Any()) return (null, false);

            PersonaData resultData;

            if (isAccident)
            {
                // Dynamic Accident Logic: Select a lower-rank demon of the same race
                // This rewards the player with a result, but punishes the quality/tier.
                resultData = racePool.First(); // Default to the absolute weakest of that race
            }
            else
            {
                // Standard Logic: Find the demon whose base level is the lowest level equal to or higher than target
                resultData = racePool.FirstOrDefault(p => p.Level >= targetLevel) ?? racePool.Last();
            }

            return (resultData.Id, isAccident);
        }

        /// <summary>
        /// SMT III HD Fidelity: Determines the total pool of skills available to be inherited.
        /// Deterministic model: Returns all unique parent skills for the UI to display.
        /// </summary>
        public List<string> GetInheritableSkills(Combatant parentA, Combatant parentB)
        {
            var skillsA = parentA.GetConsolidatedSkills();
            var skillsB = parentB.GetConsolidatedSkills();

            // Combine and remove duplicates
            return skillsA.Union(skillsB).Distinct().ToList();
        }

        /// <summary>
        /// Calculates the maximum number of skill slots the child can inherit.
        /// Formula based on total unique skills across parents:
        /// 1-6 skills = 1 slot, 7-9 = 2 slots, 10-13 = 3 slots, 14-18 = 4 slots, 19+ = 5-6 slots.
        /// </summary>
        public int GetInheritanceSlotCount(Combatant parentA, Combatant parentB)
        {
            int totalUniqueSkills = GetInheritableSkills(parentA, parentB).Count;

            if (totalUniqueSkills >= 24) return 6;
            if (totalUniqueSkills >= 19) return 5;
            if (totalUniqueSkills >= 14) return 4;
            if (totalUniqueSkills >= 10) return 3;
            if (totalUniqueSkills >= 7) return 2;
            return 1;
        }

        #endregion

        #region Validation Logic

        /// <summary>
        /// Checks if a fusion is legal based on level restrictions.
        /// </summary>
        public bool IsLevelRequirementMet(string targetPersonaId, int playerLevel)
        {
            if (Database.Personas.TryGetValue(targetPersonaId, out var data))
            {
                return data.Level <= playerLevel;
            }
            return false;
        }

        #endregion

        #region Data Initialization (Race Table)

        /// <summary>
        /// Hardcoded Fusion Table mirroring standard SMT III archetypes.
        /// Refinement: In final build, this should move to fusion_table.json.
        /// </summary>
        private void InitializeFusionTable()
        {
            _fusionTable = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            AddFusion("Fool", "Magician", "Tyrant");
            AddFusion("Fool", "Priestess", "Vile");
            AddFusion("Magician", "Priestess", "Divine");
            AddFusion("Magician", "Chariot", "Genma");
            AddFusion("Priestess", "Chariot", "Divine");
            AddFusion("Hierophant", "Chariot", "Holy");
            AddFusion("Empress", "Emperor", "Tower");
            AddFusion("Justice", "Strength", "Angel");
            AddFusion("Death", "Hanged Man", "Foul");

            // Symmetrical mapping: Ensure (A,B) results in same as (B,A)
            MirrorTable();
        }

        private void AddFusion(string raceA, string raceB, string result)
        {
            if (!_fusionTable.ContainsKey(raceA))
                _fusionTable[raceA] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _fusionTable[raceA][raceB] = result;
        }

        private void MirrorTable()
        {
            var keys = _fusionTable.Keys.ToList();
            foreach (var raceA in keys)
            {
                foreach (var raceB in _fusionTable[raceA].Keys.ToList())
                {
                    string result = _fusionTable[raceA][raceB];
                    AddFusion(raceB, raceA, result);
                }
            }
        }

        #endregion
    }
}