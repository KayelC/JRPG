using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Data
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

            // Parse Learned Skills
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

            // Parse Affinities using Core ElementHelper
            if (this.Affinities != null)
            {
                foreach (var kvp in this.Affinities)
                {
                    Element elem = ElementHelper.ParseElement(kvp.Key);
                    Affinity aff = ElementHelper.ParseAffinity(kvp.Value);
                    if (elem != Element.None) p.AffinityMap[elem] = aff;
                }
            }

            // Parse Stats
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
    }
}