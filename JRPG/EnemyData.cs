using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class EnemyData
    {
        [JsonProperty("Id")]
        public string Id { get; set; }

        [JsonProperty("PersonaId")]
        public string PersonaId { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Level")]
        public int Level { get; set; }

        [JsonProperty("Stats")]
        public Dictionary<string, int> Stats { get; set; }

        [JsonProperty("Affinities")]
        public Dictionary<string, string> Affinities { get; set; }

        [JsonProperty("Skills")]
        public List<string> Skills { get; set; }

        [JsonProperty("ExpYield")]
        public int ExpYield { get; set; }

        [JsonProperty("MaccaYield")]
        public int MaccaYield { get; set; }

        [JsonProperty("Drops")]
        public List<DropData> Drops { get; set; }
    }

    public class DropData
    {
        [JsonProperty("ItemId")]
        public string ItemId { get; set; }
        [JsonProperty("Chance")]
        public double Chance { get; set; }
    }
}