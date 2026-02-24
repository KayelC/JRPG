using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The mathematical kernel for the Fusion Sub-System.
    /// Manages Race-based lookups and tier-matching logic based on SMT III: Nocturne formulas.
    /// Handles deterministic skill inheritance calculations and accident probabilities.
    /// </summary>
    public class FusionCalculator
    {
        private readonly IGameIO _io;
        private readonly Random _rnd = new Random();

        // Lookup dictionary: Dictionary<RaceA, Dictionary<RaceB, ResultRace>>
        private Dictionary<string, Dictionary<string, string>> _raceTable;

        public FusionCalculator(IGameIO io)
        {
            _io = io;
            _raceTable = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            LoadFusionTable();
        }

        /// <summary>
        /// Hydrates the internal Race mapping from the centrally loaded Database.
        /// Ensures the sub-system remains data-driven and easily balanced.
        /// </summary>
        private void LoadFusionTable()
        {
            try
            {
                if (Database.FusionRecipes != null && Database.FusionRecipes.Count > 0)
                {
                    foreach (var recipe in Database.FusionRecipes)
                    {
                        RegisterMapping(recipe.ParentA, recipe.ParentB, recipe.Result);
                        // Ensure commutativity: A + B yields the same as B + A
                        RegisterMapping(recipe.ParentB, recipe.ParentA, recipe.Result);
                    }
                }
                else
                {
                    _io.WriteLine("[FusionCalculator] Warning: Fusion recipes not found in Database. Fusion will be unavailable.", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                _io.WriteLine($"[FusionCalculator] Critical Error loading fusion data: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Internal helper to populate the 2D lookup table.
        /// </summary>
        private void RegisterMapping(string a, string b, string res)
        {
            if (!_raceTable.ContainsKey(a))
            {
                _raceTable[a] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            _raceTable[a][b] = res;
        }

        /// <summary>
        /// Predicts the fusion result using the SMT III Formula: (Avg Base Lvl) + 1.
        /// Accounts for Moon Phase influence on Fusion Accidents.
        /// </summary>
        /// <param name="a">The first parent participant.</param>
        /// <param name="b">The second parent participant.</param>
        /// <param name="moonPhase">The current phase from the MoonPhaseSystem.</param>
        /// <returns>A tuple containing the Resulting Persona ID and an accident flag.</returns>
        public (string resultPersonaId, bool isAccident) CalculateResult(Combatant a, Combatant b, int moonPhase)
        {
            string raceA = a.ActivePersona?.Race ?? "Unknown";
            string raceB = b.ActivePersona?.Race ?? "Unknown";

            // 1. Identify Resulting Race from the lookup table
            if (!_raceTable.TryGetValue(raceA, out var branch) || !branch.TryGetValue(raceB, out string resultRace))
            {
                return (null, false); // Fusion is impossible for these specific Race combinations
            }

            // 2. Accident Logic
            // Normal base chance is very low (~0.4%). Full Moon (Phase 8) increases this to ~12.5%.
            int accidentThreshold = (moonPhase == 8) ? 12 : 1;
            bool isAccident = _rnd.Next(0, 100) < accidentThreshold;

            // 3. Level Tiering Logic
            // Fidelity Note: Use the Persona's Base Level (Template Level), not the parent's current level.
            int targetLevel = ((a.ActivePersona.Level + b.ActivePersona.Level) / 2) + 1;

            // 4. Fetch all candidates within the resulting Race from the global database
            var racePool = Database.Personas.Values
                .Where(p => p.Race.Equals(resultRace, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Level)
                .ToList();

            if (!racePool.Any())
            {
                return (null, false);
            }

            PersonaData resultData;

            if (isAccident)
            {
                // Dynamic Accident logic: Returns a lower-rank demon of the same race as the intended result.
                // This simulates a ritual collapse while still providing a usable entity.
                resultData = racePool.First();
            }
            else
            {
                // Standard Success logic: Find the nearest match where p.Level >= targetLevel.
                // If no higher-level demon exists in the race, default to the highest available (Last).
                resultData = racePool.FirstOrDefault(p => p.Level >= targetLevel) ?? racePool.Last();
            }

            return (resultData.Id, isAccident);
        }

        /// <summary>
        /// Aggregates all unique skills from parents to determine the total inheritable pool.
        /// Works with variable parent counts (Binary or Sacrificial).
        /// </summary>
        public List<string> GetInheritableSkills(params Combatant[] parents)
        {
            List<string> pool = new List<string>();
            foreach (var p in parents)
            {
                if (p != null)
                {
                    // Union ensures we only track unique skill names
                    pool = pool.Union(p.GetConsolidatedSkills()).ToList();
                }
            }
            return pool.Distinct().ToList();
        }

        /// <summary>
        /// Calculates the number of skill slots available for inheritance based on total unique parent skills.
        /// Implements the standard SMT III scaling tiers.
        /// </summary>
        public int GetInheritanceSlotCount(params Combatant[] parents)
        {
            int uniqueSkillCount = GetInheritableSkills(parents).Count;

            // Inheritance Scaling (SMT III / Persona Standard)
            // 1-6 skills = 1 slot
            // 7-9 skills = 2 slots
            // 10-13 skills = 3 slots
            // 14-18 skills = 4 slots
            // 19-23 skills = 5 slots
            // 24+ skills = 6 slots
            if (uniqueSkillCount >= 24) return 6;
            if (uniqueSkillCount >= 19) return 5;
            if (uniqueSkillCount >= 14) return 4;
            if (uniqueSkillCount >= 10) return 3;
            if (uniqueSkillCount >= 7) return 2;

            return 1;
        }


    }
}