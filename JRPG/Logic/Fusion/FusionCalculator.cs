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
    /// The mathematical kernel for the Fusion Sub-System.
    /// Manages Race-based lookups and tier-matching logic based on SMT III: Nocturne formulas.
    /// Handles deterministic skill inheritance calculations and accident probabilities.
    /// </summary>
    public class FusionCalculator
    {
        private readonly IGameIO _io;
        private readonly Random _rnd = new Random();

        // Lookup dictionary: Dictionary<RaceA, Dictionary<RaceB, ResultRace>>
        private readonly Dictionary<string, Dictionary<string, string>> _raceTable;

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
        /// Predicts the fusion result, handling normal, special, rank, and Mitama fusions.
        /// Accounts for Moon Phase influence on Fusion Accidents.
        /// </summary>
        /// <param name="a">The first parent participant.</param>
        /// <param name="b">The second parent participant.</param>
        /// <param name="moonPhase">The current phase from the MoonPhaseSystem.</param>
        /// <returns>A tuple containing the fusion operation type, a target ID, and an accident flag.</returns>
        public (FusionOperationType operation, string targetEntityId, bool isAccident) CalculateResult(Combatant a, Combatant b, int moonPhase)
        {
            string raceA = a.ActivePersona?.Race ?? "Unknown";
            string raceB = b.ActivePersona?.Race ?? "Unknown";

            // 1. Accident Logic (calculated upfront)
            int accidentThreshold = (moonPhase == 8) ? 12 : 1;
            bool isAccident = _rnd.Next(0, 100) < accidentThreshold;

            // 2. Mitama Fusion Check (Highest Priority)
            if (raceA == "Mitama" || raceB == "Mitama")
            {
                Combatant parentToBoost = (raceA == "Mitama") ? b : a;
                // Ensure Mitama fusion is only with a non-Mitama demon
                if (parentToBoost == null || parentToBoost.ActivePersona?.Race == "Mitama")
                {
                    return (FusionOperationType.NoFusionPossible, null, false);
                }
                return (FusionOperationType.StatBoostFusion, parentToBoost.SourceId.ToLower(), isAccident);
            }

            // 3. Identify Resulting string from the lookup table
            if (!_raceTable.TryGetValue(raceA, out var branch) || !branch.TryGetValue(raceB, out string resultString))
            {
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            // 4. Handle Special Cases: Rank Up/Down
            if (resultString == "1" || resultString == "-1")
            {
                Combatant parentToRank = null;
                // The parent to modify is the one that is NOT an Elemental
                if (a.ActivePersona?.Race != "Element") { parentToRank = a; }
                else if (b.ActivePersona?.Race != "Element") { parentToRank = b; }

                if (parentToRank != null)
                {
                    var operation = (resultString == "1") ? FusionOperationType.RankUpParent : FusionOperationType.RankDownParent;
                    return (operation, parentToRank.SourceId.ToLower(), isAccident);
                }
                else
                {
                    // This case is invalid if fusion table is correct (e.g. Element + Element can't rank up)
                    return (FusionOperationType.NoFusionPossible, null, false);
                }
            }

            // 5. Handle Special Cases: Direct ID results (Elementals)
            // If the result string directly matches a key in the Entity Database, it's a direct creation.
            if (Database.Personas.ContainsKey(resultString.ToLower()))
            {
                return (FusionOperationType.CreateNewDemon, resultString.ToLower(), isAccident);
            }

            // 6. Normal Race Fusion (Level-Based)
            // Use Base Level from PersonaData for fusion result level, plus a random nudge.
            PersonaData templateA = Database.Personas[a.SourceId.ToLower()];
            PersonaData templateB = Database.Personas[b.SourceId.ToLower()];
            int avgBaseLevel = (templateA.Level + templateB.Level) / 2;
            int targetLevel = avgBaseLevel + _rnd.Next(1, 6); // Add 1 to 5 to the average base level.

            var racePool = Database.Personas.Values
                .Where(p => p.Race.Equals(resultString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Level)
                .ToList();

            if (!racePool.Any())
            {
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            PersonaData resultData;
            if (isAccident)
            {
                resultData = racePool.First(); // Accident results in the lowest rank of the target race
            }
            else
            {
                // Find nearest match where p.Level >= targetLevel, or default to highest available.
                resultData = racePool.FirstOrDefault(p => p.Level >= targetLevel) ?? racePool.Last();
            }

            return (FusionOperationType.CreateNewDemon, resultData.Id, isAccident);
        }

        /// <summary>
        /// Aggregates all unique skills from parents to determine the total inheritable pool.
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