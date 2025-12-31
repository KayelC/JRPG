using System;
using System.Collections.Generic;

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
        // We can ignore "LearnedSkills" for this prototype or add it if needed later

        public Persona ToPersona()
        {
            var p = new Persona
            {
                Name = this.Name,
                Level = this.Level,
                Arcana = this.Arcana,
                SkillSet = new List<string>(this.BaseSkills ?? new List<string>())
            };

            // 1. Parse Affinities with robust mapping
            if (this.Affinities != null)
            {
                foreach (var kvp in this.Affinities)
                {
                    Element elem = ParseElement(kvp.Key);
                    Affinity aff = ParseAffinity(kvp.Value);

                    if (elem != Element.None)
                    {
                        p.AffinityMap[elem] = aff;
                    }
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

        // Helper to handle JSON ("Electric") -> Enum ("Elec") mismatches
        private Element ParseElement(string input)
        {
            if (string.Equals(input, "Electric", StringComparison.OrdinalIgnoreCase)) return Element.Elec;
            if (string.Equals(input, "Darkness", StringComparison.OrdinalIgnoreCase)) return Element.Dark;

            if (Enum.TryParse(input, true, out Element elem)) return elem;

            return Element.None;
        }

        // Helper to handle JSON ("Reflect") -> Enum ("Repel") mismatches
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