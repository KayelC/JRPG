using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class ArmorData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")] // FIXED: Was "_name", caused null names
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("defense")]
        public int Defense { get; set; }

        [JsonProperty("evasion")]
        public int Evasion { get; set; }
    }
}