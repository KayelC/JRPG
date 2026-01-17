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
    /// Manages Arcana-based lookups and tier-matching logic.
    /// </summary>
    public class FusionCalculator
    {
        private readonly IGameIO _io;
        private readonly Random _rnd = new Random();

        // Lookup dictionary: Dictionary<ArcanaA, Dictionary<ArcanaB, ResultArcana>>
        private Dictionary<string, Dictionary<string, string>> _arcanaTable;

        public FusionCalculator(IGameIO io)
        {
            _io = io;
            _arcanaTable = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            LoadFusionTable();
        }

        /// <summary>
        /// Hydrates the internal Arcana mapping from fusion_table.json.
        /// </summary>
        private void LoadFusionTable()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Jsons", "fusion_table.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var content = JsonConvert.DeserializeObject<FusionTableSchema>(json);

                    foreach (var recipe in content.Recipes)
                    {
                        RegisterMapping(recipe.ParentA, recipe.ParentB, recipe.Result);
                        // Ensure commutativity: A+B = B+A
                        RegisterMapping(recipe.ParentB, recipe.ParentA, recipe.Result);
                    }
                }
            }
            catch (Exception ex)
            {
                _io.WriteLine($"[FusionCalculator] Critical Error loading fusion data: {ex.Message}", ConsoleColor.Red);
            }
        }

        private void RegisterMapping(string a, string b, string res)
        {
            if (!_arcanaTable.ContainsKey(a))
            {
                _arcanaTable[a] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            _arcanaTable[a][b] = res;
        }

        /// <summary>
        /// Predicts the fusion result using the Formula: (Avg Base Lvl) + 1.
        /// Handles Fusion Accidents via Moon Phase input.
        /// </summary>
        public (string resultPersonaId, bool isAccident) CalculateResult(Combatant a, Combatant b, int moonPhase)
        {
            string arcanaA = a.ActivePersona?.Arcana ?? "Unknown";
            string arcanaB = b.ActivePersona?.Arcana ?? "Unknown";

            // 1. Identify Resulting Arcana
            if (!_arcanaTable.TryGetValue(arcanaA, out var branch) || !branch.TryGetValue(arcanaB, out string resultArcana))
            {
                return (null, false); // Fusion is impossible for these Arcanas
            }

            // 2. Accident Logic
            // Base chance: 1/256 (~0.4%). Full Moon: 1/8 (12.5%).
            int accidentThreshold = (moonPhase == 8) ? 12 : 1;
            bool isAccident = _rnd.Next(0, 100) < accidentThreshold;

            // 3. Level Tiering Logic
            // We use the Persona's Base Level (found in Database), not the Combatant's current level.
            int targetLevel = ((a.ActivePersona.Level + b.ActivePersona.Level) / 2) + 1;

            // 4. Find valid candidate within the resulting Arcana
            var arcanaPool = Database.Personas.Values
                .Where(p => p.Arcana.Equals(resultArcana, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Level)
                .ToList();

            if (!arcanaPool.Any()) return (null, false);

            PersonaData resultData;

            if (isAccident)
            {
                // Dynamic Accident logic: Get a lower rank of the same race as intended
                // This simulates the "instability" of the ritual.
                resultData = arcanaPool.First();
            }
            else
            {
                // Find nearest match where p.Level >= targetLevel
                resultData = arcanaPool.FirstOrDefault(p => p.Level >= targetLevel) ?? arcanaPool.Last();
            }

            return (resultData.Id, isAccident);
        }

        /// <summary>
        /// Determines the number of skills the child can inherit.
        /// Scaling based on total unique parent skills.
        /// </summary>
        public int GetInheritanceSlotCount(params Combatant[] parents)
        {
            int uniqueSkillCount = parents
                .SelectMany(p => p.GetConsolidatedSkills())
                .Distinct()
                .Count();

            // Nocturne-standard scaling
            if (uniqueSkillCount >= 24) return 6;
            if (uniqueSkillCount >= 19) return 5;
            if (uniqueSkillCount >= 14) return 4;
            if (uniqueSkillCount >= 10) return 3;
            if (uniqueSkillCount >= 7) return 2;
            return 1;
        }

        #region Internal Schema

        private class FusionTableSchema
        {
            [JsonProperty("recipes")]
            public List<FusionRecipeEntry> Recipes { get; set; }
        }

        private class FusionRecipeEntry
        {
            public string ParentA { get; set; }
            public string ParentB { get; set; }
            public string Result { get; set; }
        }

        #endregion
    }
}