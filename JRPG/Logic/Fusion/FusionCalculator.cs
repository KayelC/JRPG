using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The mathematical kernel for the Fusion Sub-System. 
    /// Manages Race-based lookups and tier-matching logic based on recipe formulas.
    /// Handles deterministic skill inheritance calculations and accident probabilities.
    /// Fully decoupled diagnostic tracing via IFusionMessenger.
    /// </summary>
    public class FusionCalculator
    {
        private readonly IGameIO _io;
        private readonly IFusionMessenger _messenger;
        private readonly Random _rnd = new Random();

        // Lookup dictionary: Dictionary<RaceA, Dictionary<RaceB, ResultString>>
        private readonly Dictionary<string, Dictionary<string, string>> _raceTable;

        public FusionCalculator(IGameIO io, IFusionMessenger messenger)
        {
            _io = io;
            _messenger = messenger;
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
                    _messenger.Publish("[FusionCalculator] Warning: Fusion recipes not found in Database.", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                _messenger.Publish($"[FusionCalculator] Critical Error loading fusion data: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// Internal helper to populate the 2D lookup table.
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
        public (FusionOperationType operation, string? targetEntityId, bool isAccident) CalculateResult(Combatant a, Combatant b, int moonPhase)
        {
            if (a.ActivePersona == null || b.ActivePersona == null)
                return (FusionOperationType.NoFusionPossible, null, false);

            // Establish Identifiers
            string idA = a.SourceId;
            string idB = b.SourceId;
            string raceA = a.ActivePersona.Race;
            string raceB = b.ActivePersona.Race;

            // Accident Roll
            int accidentThreshold = (moonPhase == 8) ? 12 : 1;
            bool isAccident = _rnd.Next(0, 100) < accidentThreshold;

            // --- TIER 0: GLOBAL MITAMA OVERRIDE ---
            // If one parent is a Mitama, it's always a Stat Boost, unless the other is also a Mitama.
            bool aIsMitama = raceA.Equals("Mitama", StringComparison.OrdinalIgnoreCase);
            bool bIsMitama = raceB.Equals("Mitama", StringComparison.OrdinalIgnoreCase);

            if (aIsMitama || bIsMitama)
            {
                // If both are Mitamas, typically no result
                if (aIsMitama && bIsMitama)
                {
                    _messenger.Publish("[Fusion Trace] Mitama + Mitama fusion is not supported.", ConsoleColor.DarkGray);
                    return (FusionOperationType.NoFusionPossible, null, false);
                }

                // Identify the non-Mitama parent to receive the boost
                Combatant target = aIsMitama ? b : a;

                // Check: Non-Mitama parent cannot be an Element (Elements only Rank Up/Down)
                if (target.ActivePersona.Race.Equals("Element", StringComparison.OrdinalIgnoreCase))
                {
                    _messenger.Publish("[Fusion Trace] Elements cannot receive Mitama stat boosts.", ConsoleColor.DarkGray);
                    return (FusionOperationType.NoFusionPossible, null, false);
                }

                _messenger.Publish($"[Fusion Trace] Mitama Global Override: Stat boosting {target.Name}", ConsoleColor.DarkGray);
                return (FusionOperationType.StatBoostFusion, target.SourceId.ToLower(), isAccident);
            }

            // --- TIER 1 & 2: TABLE LOOKUP ---
            string? resultString = null;

            // Search by Specific IDs
            if (_raceTable.TryGetValue(idA, out var idBranch) && idBranch.TryGetValue(idB, out resultString))
            {
                _messenger.Publish($"[Fusion Trace] Match found via Specific IDs: {idA} + {idB}", ConsoleColor.DarkGray);
            }
            // Search by Races
            else if (_raceTable.TryGetValue(raceA, out var raceBranch) && raceBranch.TryGetValue(raceB, out resultString))
            {
                _messenger.Publish($"[Fusion Trace] Match found via Races: {raceA} + {raceB}", ConsoleColor.DarkGray);
            }

            if (resultString == null)
            {
                _messenger.Publish($"[Fusion Trace] No combination found for {idA}({raceA}) + {idB}({raceB})", ConsoleColor.DarkGray);
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            // PRIORITY 1: Literal ID Search (Element/Mitama Creation)
            string lookupId = resultString.ToLower();
            if (Database.Personas.ContainsKey(lookupId))
            {
                _messenger.Publish($"[Fusion Trace] Result identified as Entity ID: {lookupId}", ConsoleColor.DarkGray);
                return (FusionOperationType.CreateNewDemon, lookupId, isAccident);
            }

            // PRIORITY 2: Rank Up/Down Logic
            if (resultString == "1" || resultString == "-1")
            {
                Combatant? parentToRank = null;
                if (!raceA.Equals("Element", StringComparison.OrdinalIgnoreCase)) parentToRank = a;
                else if (!raceB.Equals("Element", StringComparison.OrdinalIgnoreCase)) parentToRank = b;

                if (parentToRank != null)
                {
                    int rankDir = (resultString == "1") ? 1 : -1;
                    int targetRank = parentToRank.ActivePersona.Rank + rankDir;

                    // Find the new entity in the database that matches the Race and the Target Rank
                    var nextRankData = Database.Personas.Values.FirstOrDefault(p =>
                        p.Race.Equals(parentToRank.ActivePersona.Race, StringComparison.OrdinalIgnoreCase) &&
                        p.Rank == targetRank);

                    if (nextRankData != null)
                    {
                        var op = (resultString == "1") ? FusionOperationType.RankUpParent : FusionOperationType.RankDownParent;
                        _messenger.Publish($"[Fusion Trace] Rank Mutation: Target {nextRankData.Name} identified.", ConsoleColor.DarkGray);

                        // Return the NEW ID, not the parent's ID
                        return (op, nextRankData.Id.ToLower(), isAccident);
                    }
                }
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            // PRIORITY 3: Normal Race Fusion (Level-Based)
            // resultString is assumed to be a Race Name (e.g., "Fury")

            // Get templates to find Base Levels
            if (!Database.Personas.TryGetValue(a.SourceId.ToLower(), out var templateA) ||
                !Database.Personas.TryGetValue(b.SourceId.ToLower(), out var templateB))
            {
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            int avgBaseLevel = (templateA.Level + templateB.Level) / 2;
            int targetLevel = avgBaseLevel + _rnd.Next(1, 6); // Add 1 to 5 to the average base level.

            // Fetch all demons of the resulting race
            var racePool = Database.Personas.Values
                .Where(p => p.Race.Equals(resultString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Level).ToList();

            if (!racePool.Any())
            {
                _messenger.Publish($"[Fusion Trace] Resulting Race '{resultString}' has no members in database.", ConsoleColor.DarkGray);
                return (FusionOperationType.NoFusionPossible, null, false);
            }

            PersonaData resultData;
            if (isAccident)
            {
                resultData = racePool.First();
                _messenger.Publish("[Fusion Trace] Fusion Accident! Result shifted to lowest tier.", ConsoleColor.DarkGray);
            }
            else
            {
                // Find nearest match where p.Level >= targetLevel, or default to the highest in that race
                resultData = racePool.FirstOrDefault(p => p.Level >= targetLevel) ?? racePool.Last();

                // Rule: If the result is one of the parents, move to the next tier in the pool
                if (resultData.Id == templateA.Id || resultData.Id == templateB.Id)
                {
                    int idx = racePool.IndexOf(resultData);
                    if (idx + 1 < racePool.Count) resultData = racePool[idx + 1];
                    _messenger.Publish("[Fusion Trace] Result matches parent; shifting to next tier.", ConsoleColor.DarkGray);
                }
            }

            _messenger.Publish($"[Fusion Trace] Normal Fusion: Predicted Result -> {resultData.Name}", ConsoleColor.DarkGray);
            return (FusionOperationType.CreateNewDemon, resultData.Id, isAccident);
        }

        // Aggregates all unique skills from parents to determine the total inheritable pool.
        public List<string> GetInheritableSkills(params Combatant[] parents)
        {
            List<string> pool = new List<string>();
            foreach (var p in parents)
            {
                if (p == null) continue;

                foreach (var skillName in p.GetConsolidatedSkills())
                {
                    // 1. Find the skill in the database to check its effect text
                    if (Database.Skills.TryGetValue(skillName, out var skillData))
                    {
                        // Filter out skills marked as exclusive in their effect description
                        bool isExclusive = skillData.Effect.Contains("exclusive", StringComparison.OrdinalIgnoreCase);

                        if (!isExclusive)
                        {
                            pool.Add(skillName);
                        }
                    }
                }
            }
            return pool.Distinct().ToList();
        }

        /// <summary>
        /// Retrieves skills from parents that are specifically marked as exclusive.
        /// Used for UI display purposes (graying out).
        /// </summary>
        public List<string> GetExclusiveSkills(params Combatant[] parents)
        {
            List<string> pool = new List<string>();
            foreach (var p in parents)
            {
                if (p == null) continue;

                foreach (var skillName in p.GetConsolidatedSkills())
                {
                    if (Database.Skills.TryGetValue(skillName, out var skillData))
                    {
                        if (skillData.Effect.Contains("exclusive", StringComparison.OrdinalIgnoreCase))
                        {
                            pool.Add(skillName);
                        }
                    }
                }
            }
            // Use Union or Distinct to prevent duplicates in the pool
            return pool.Distinct().ToList();
        }

        // Calculates the number of skill slots available for inheritance based on total unique parent skills.
        public int GetInheritanceSlotCount(params Combatant[] parents)
        {
            // Slots are calculated based on the LEGAL inheritable skills only.
            int uniqueSkillCount = GetInheritableSkills(parents).Count;

            // Inheritance Scaling
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