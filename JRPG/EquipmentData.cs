using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    // --- New Equipment Models ---

    public class ArmorData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Defense { get; set; }
        public int Evasion { get; set; }
    }

    public class BootData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Evasion { get; set; }
    }

    public class AccessoryData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [JsonProperty("modifierStat")]
        public string ModifierStat { get; set; } // "STR", "MAG", etc.
        [JsonProperty("modifierValue")]
        public int ModifierValue { get; set; }
    }
}
