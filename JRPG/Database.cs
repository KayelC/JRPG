using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace JRPGPrototype
{
    public static class Database
    {
        public static Dictionary<string, SkillData> Skills = new Dictionary<string, SkillData>();
        public static Dictionary<string, PersonaData> Personas = new Dictionary<string, PersonaData>();
        public static Dictionary<string, AilmentData> Ailments = new Dictionary<string, AilmentData>();
        public static Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();

        // Equipment Dictionaries
        public static Dictionary<string, WeaponData> Weapons = new Dictionary<string, WeaponData>();
        public static Dictionary<string, ArmorData> Armors = new Dictionary<string, ArmorData>();
        public static Dictionary<string, BootData> Boots = new Dictionary<string, BootData>();
        public static Dictionary<string, AccessoryData> Accessories = new Dictionary<string, AccessoryData>();

        // Unified Shop List
        public static List<ShopEntry> ShopInventory = new List<ShopEntry>();

        public static void LoadData()
        {
            // 1. Skills
            LoadFile("skills_by_category.json", (json) => {
                var skillCats = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(json);
                foreach (var cat in skillCats)
                    foreach (var s in cat.Value) if (!Skills.ContainsKey(s.Name)) Skills.Add(s.Name, s);
                Console.WriteLine($"[System] Loaded {Skills.Count} skills.");
            });

            // 2. Personas
            LoadFile("persona_data.json", (json) => {
                var pList = JsonConvert.DeserializeObject<List<PersonaData>>(json);
                foreach (var p in pList) if (!Personas.ContainsKey(p.Id)) Personas.Add(p.Id, p);
                Console.WriteLine($"[System] Loaded {Personas.Count} personas.");
            });

            // 3. Ailments
            LoadFile("status_ailments.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AilmentData>>>(json);
                if (root != null && root.ContainsKey("ailments"))
                    foreach (var a in root["ailments"]) if (!Ailments.ContainsKey(a.Name)) Ailments.Add(a.Name, a);
                Console.WriteLine($"[System] Loaded {Ailments.Count} ailments.");
            });

            // 4. Items
            LoadFile("items.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<ItemData>>>(json);
                if (root != null && root.ContainsKey("items"))
                    foreach (var i in root["items"]) if (!Items.ContainsKey(i.Id)) Items.Add(i.Id, i);
                Console.WriteLine($"[System] Loaded {Items.Count} items.");
            });

            // 5. Weapons
            LoadFile("weapons.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<WeaponData>>>(json);
                if (root != null && root.ContainsKey("weapons"))
                    foreach (var w in root["weapons"]) if (!Weapons.ContainsKey(w.Id)) Weapons.Add(w.Id, w);
                Console.WriteLine($"[System] Loaded {Weapons.Count} weapons.");
            });

            // 6. Armor
            LoadFile("armor.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<ArmorData>>>(json);
                if (root != null && root.ContainsKey("armor"))
                    foreach (var a in root["armor"]) if (!Armors.ContainsKey(a.Id)) Armors.Add(a.Id, a);
                Console.WriteLine($"[System] Loaded {Armors.Count} armor.");
            });

            // 7. Boots
            LoadFile("boots.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<BootData>>>(json);
                if (root != null && root.ContainsKey("boots"))
                    foreach (var b in root["boots"]) if (!Boots.ContainsKey(b.Id)) Boots.Add(b.Id, b);
                Console.WriteLine($"[System] Loaded {Boots.Count} boots.");
            });

            // 8. Accessories
            LoadFile("accessories.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AccessoryData>>>(json);
                if (root != null && root.ContainsKey("accessories"))
                    foreach (var acc in root["accessories"]) if (!Accessories.ContainsKey(acc.Id)) Accessories.Add(acc.Id, acc);
                Console.WriteLine($"[System] Loaded {Accessories.Count} accessories.");
            });

            // 9. Shop Inventory
            LoadFile("shop_inventory.json", (json) => {
                var root = JsonConvert.DeserializeObject<ShopJsonRoot>(json);
                ShopInventory.Clear();

                AddShopEntries(root.Items, ShopCategory.Item);
                AddShopEntries(root.Weapons, ShopCategory.Weapon);
                AddShopEntries(root.Armor, ShopCategory.Armor);
                AddShopEntries(root.Boots, ShopCategory.Boots);
                AddShopEntries(root.Accessories, ShopCategory.Accessory);

                Console.WriteLine($"[System] Loaded {ShopInventory.Count} shop entries.");
            });
        }

        private static void AddShopEntries(List<ShopJsonItem> items, ShopCategory cat)
        {
            if (items == null) return;
            foreach (var i in items)
            {
                ShopInventory.Add(new ShopEntry { Id = i.Id, Name = i.Name, BasePrice = i.Price, Category = cat });
            }
        }

        private static void LoadFile(string filename, Action<string> onSuccess)
        {
            if (File.Exists(filename)) onSuccess(File.ReadAllText(filename));
            else Console.WriteLine($"[Error] {filename} not found!");
        }
    }
}