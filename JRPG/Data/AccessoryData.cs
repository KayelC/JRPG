using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class AccessoryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("_name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("modifierStat")]
        public string ModifierStat { get; set; } // "STR", "MAG", etc.

        [JsonProperty("modifierValue")]
        public int ModifierValue { get; set; }
    }
}