using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public class BootData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("_name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("evasion")]
        public int Evasion { get; set; }
    }
}