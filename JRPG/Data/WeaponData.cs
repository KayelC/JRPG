using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class WeaponData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // Maps to Element

        [JsonProperty("power")]
        public int Power { get; set; }

        [JsonProperty("accuracy")]
        public int Accuracy { get; set; }

        [JsonProperty("is_long_range")]
        public bool IsLongRange { get; set; }
    }
}