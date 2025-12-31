using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class WeaponData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // Maps to Element (Slash, Pierce, etc.)

        [JsonProperty("power")]
        public int Power { get; set; }

        [JsonProperty("accuracy")]
        public int Accuracy { get; set; }

        [JsonProperty("is_long_range")]
        public bool IsLongRange { get; set; }
    }
}