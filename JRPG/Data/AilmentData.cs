using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class AilmentData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("action_restriction")]
        public string ActionRestriction { get; set; } // None, SkipTurn, etc.

        [JsonProperty("evasion_mult")]
        public double EvasionMult { get; set; }

        [JsonProperty("crit_bonus_chance")]
        public double CritBonusChance { get; set; }

        [JsonProperty("damage_taken_mult")]
        public double DamageTakenMult { get; set; }

        [JsonProperty("damage_deal_mult")]
        public double DamageDealMult { get; set; } = 1.0;

        [JsonProperty("dot_percent")]
        public double DotPercent { get; set; }

        [JsonProperty("extra_turns")]
        public int ExtraTurns { get; set; } = 0;

        [JsonProperty("removal_triggers")]
        public List<string> RemovalTriggers { get; set; }

        [JsonProperty("cure_keyword")]
        public string CureKeyword { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}