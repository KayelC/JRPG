using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class PersonaData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Arcana { get; set; }
        public Dictionary<string, int> Stats { get; set; }
        public Dictionary<string, string> Affinities { get; set; }
        public List<string> BaseSkills { get; set; }

        // Load the level-up skills from JSON
        // JSON format: "LearnedSkills": { "3": "Dia", "5": "Media" }
        public Dictionary<string, string> LearnedSkills { get; set; }

        public Persona ToPersona()
        {
            var p = new Persona
            {
                Name = this.Name,
                Level = this.Level,
                Arcana = this.Arcana,
                SkillSet = new List<string>(this.BaseSkills ?? new List<string>())
            };

            // Parse Learned Skills (String Key -> Int Key)
            if (this.LearnedSkills != null)
            {
                foreach (var kvp in this.LearnedSkills)
                {
                    if (int.TryParse(kvp.Key, out int lvl))
                    {
                        p.SkillsToLearn[lvl] = kvp.Value;
                    }
                }
            }

            // 1. Parse Affinities
            if (this.Affinities != null)
            {
                foreach (var kvp in this.Affinities)
                {
                    Element elem = ParseElement(kvp.Key);
                    Affinity aff = ParseAffinity(kvp.Value);
                    if (elem != Element.None) p.AffinityMap[elem] = aff;
                }
            }

            // 2. Parse Stats
            if (this.Stats != null)
            {
                foreach (var kvp in this.Stats)
                {
                    if (Enum.TryParse(kvp.Key, true, out StatType stat))
                    {
                        p.StatModifiers[stat] = kvp.Value;
                    }
                }
            }

            return p;
        }

        private Element ParseElement(string input)
        {
            if (string.Equals(input, "Electric", StringComparison.OrdinalIgnoreCase)) return Element.Elec;
            if (string.Equals(input, "Darkness", StringComparison.OrdinalIgnoreCase)) return Element.Dark;
            if (Enum.TryParse(input, true, out Element elem)) return elem;
            return Element.None;
        }

        private Affinity ParseAffinity(string input)
        {
            if (string.Equals(input, "Reflect", StringComparison.OrdinalIgnoreCase)) return Affinity.Repel;
            if (string.Equals(input, "Absorb", StringComparison.OrdinalIgnoreCase)) return Affinity.Absorb;
            if (string.Equals(input, "Block", StringComparison.OrdinalIgnoreCase)) return Affinity.Null;
            if (Enum.TryParse(input, true, out Affinity aff)) return aff;
            return Affinity.Normal;
        }
    }
}