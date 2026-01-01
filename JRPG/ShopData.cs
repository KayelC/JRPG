using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class ShopEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BasePrice { get; set; }
        public bool IsWeapon { get; set; }
    }

    // Helper classes for JSON Deserialization
    public class ShopJsonRoot
    {
        [JsonProperty("items")]
        public List<ShopJsonItem> Items { get; set; }

        [JsonProperty("weapons")]
        public List<ShopJsonItem> Weapons { get; set; }
    }

    public class ShopJsonItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("_name")]
        public string Name { get; set; }

        [JsonProperty("price")]
        public int Price { get; set; }
    }
}