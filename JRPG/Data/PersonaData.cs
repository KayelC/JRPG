using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class PersonaData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }

        [JsonProperty("Race")]
        public string Race { get; set; }

        [JsonProperty("Stats")]
        public Dictionary<string, int> RawStats { get; set; }

        [JsonProperty("Affinities")]
        public Dictionary<string, string> RawAffinities { get; set; }

        public List<string> BaseSkills { get; set; }

        [JsonProperty("LearnedSkills")]
        public Dictionary<string, string> LearnedSkillsRaw { get; set; }

        public string FamiliarDialogue { get; set; }

        /// <summary>
        /// Converts the raw PersonaData (JSON schema) into a runtime Persona entity.
        /// Handles mapping of string-based stats/affinities to enums.
        /// </summary>
        public Persona ToPersona()
        {
            var p = new Persona
            {
                Name = this.Name,
                Level = this.Level,
                Race = this.Race,
                SkillSet = new List<string>(this.BaseSkills ?? new List<string>())
            };

            // Parse Learned Skills (string key for level, string value for skill name)
            if (this.LearnedSkillsRaw != null)
            {
                foreach (var kvp in this.LearnedSkillsRaw)
                {
                    if (int.TryParse(kvp.Key, out int lvl))
                    {
                        p.SkillsToLearn[lvl] = kvp.Value;
                    }
                }
            }

            // Parse Affinities using Core ElementHelper
            if (this.RawAffinities != null)
            {
                foreach (var kvp in this.RawAffinities)
                {
                    Element elem = ElementHelper.ParseElement(kvp.Key);
                    Affinity aff = ElementHelper.ParseAffinity(kvp.Value);
                    if (elem != Element.None) p.AffinityMap[elem] = aff;
                }
            }

            // Parse Stats from raw string keys (St, Ma, etc.) to Enum keys
            if (this.RawStats != null)
            {
                foreach (var kvp in this.RawStats)
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