using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class ItemData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // Healing, Spirit, Revive, Cure, Utility, Barrier

        [JsonProperty("effect_value")]
        public int EffectValue { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}