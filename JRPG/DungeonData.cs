using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class DungeonRoot
    {
        [JsonProperty("dungeons")]
        public List<DungeonData> Dungeons { get; set; }
    }

    public class DungeonData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("blocks")]
        public List<BlockData> Blocks { get; set; }
    }

    public class BlockData
    {
        [JsonProperty("block_id")]
        public string BlockId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("floor_range")]
        public int[] FloorRange { get; set; } // [Start, End]

        [JsonProperty("enemy_pool")]
        public List<string> EnemyPool { get; set; }

        // MOVED HERE to match JSON schema
        [JsonProperty("fixed_floors")]
        public List<FixedFloorData> FixedFloors { get; set; }

        public int StartFloor => FloorRange != null && FloorRange.Length > 0 ? FloorRange[0] : 0;
        public int EndFloor => FloorRange != null && FloorRange.Length > 1 ? FloorRange[1] : 0;
    }

    public class FixedFloorData
    {
        [JsonProperty("floor")]
        public int Floor { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // "Boss", "SafeRoom", "BlockEnd"

        [JsonProperty("id")]
        public string Id { get; set; } // Enemy ID if Boss

        [JsonProperty("has_terminal")]
        public bool HasTerminal { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}