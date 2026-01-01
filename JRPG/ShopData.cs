using System.Collections.Generic;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    // The categories available in the shop
    public enum ShopCategory
    {
        Item,
        Weapon,
        Armor,
        Boots,
        Accessory
    }

    // The single entry in the unified shop list
    public class ShopEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BasePrice { get; set; }
        public ShopCategory Category { get; set; } // Replaces 'IsWeapon'
    }

    // --- JSON Helper Classes ---
    public class ShopJsonRoot
    {
        [JsonProperty("items")]
        public List<ShopJsonItem> Items { get; set; }
        [JsonProperty("weapons")]
        public List<ShopJsonItem> Weapons { get; set; }
        [JsonProperty("armor")]
        public List<ShopJsonItem> Armor { get; set; }
        [JsonProperty("boots")]
        public List<ShopJsonItem> Boots { get; set; }
        [JsonProperty("accessories")]
        public List<ShopJsonItem> Accessories { get; set; }
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