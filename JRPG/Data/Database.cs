using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace JRPGPrototype.Data
{
    public static class Database
    {
        public static Dictionary<string, SkillData> Skills = new Dictionary<string, SkillData>();
        public static Dictionary<string, PersonaData> Personas = new Dictionary<string, PersonaData>();
        public static Dictionary<string, AilmentData> Ailments = new Dictionary<string, AilmentData>();
        public static Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();
        public static Dictionary<string, EnemyData> Enemies = new Dictionary<string, EnemyData>();
        public static Dictionary<string, DungeonData> Dungeons = new Dictionary<string, DungeonData>();

        public static Dictionary<string, WeaponData> Weapons = new Dictionary<string, WeaponData>();
        public static Dictionary<string, ArmorData> Armors = new Dictionary<string, ArmorData>();
        public static Dictionary<string, BootData> Boots = new Dictionary<string, BootData>();
        public static Dictionary<string, AccessoryData> Accessories = new Dictionary<string, AccessoryData>();

        public static List<ShopEntry> ShopInventory = new List<ShopEntry>();

        public static void LoadData()
        {
            LoadFile("skills_by_category.json", (json) => {
                var skillCats = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(json);
                foreach (var cat in skillCats)
                    foreach (var s in cat.Value) if (!Skills.ContainsKey(s.Name)) Skills.Add(s.Name, s);
                Console.WriteLine($"[System] Loaded {Skills.Count} skills.");
            });

            LoadFile("persona_data.json", (json) => {
                var pList = JsonConvert.DeserializeObject<List<PersonaData>>(json);
                foreach (var p in pList) if (!Personas.ContainsKey(p.Id)) Personas.Add(p.Id, p);
                Console.WriteLine($"[System] Loaded {Personas.Count} personas.");
            });

            LoadFile("enemies.json", (json) => {
                var eList = JsonConvert.DeserializeObject<List<EnemyData>>(json);
                foreach (var e in eList) if (!Enemies.ContainsKey(e.Id)) Enemies.Add(e.Id, e);
                Console.WriteLine($"[System] Loaded {Enemies.Count} enemies.");
            });

            LoadFile("status_ailments.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AilmentData>>>(json);
                if (root != null && root.ContainsKey("ailments"))
                    foreach (var a in root["ailments"]) if (!Ailments.ContainsKey(a.Name)) Ailments.Add(a.Name, a);
                Console.WriteLine($"[System] Loaded {Ailments.Count} ailments.");
            });

            LoadFile("items.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<ItemData>>>(json);
                if (root != null && root.ContainsKey("items"))
                    foreach (var i in root["items"]) if (!Items.ContainsKey(i.Id)) Items.Add(i.Id, i);
                Console.WriteLine($"[System] Loaded {Items.Count} items.");
            });

            LoadEquipment("weapons.json", "weapons", Weapons);
            LoadEquipment("armor.json", "armor", Armors);
            LoadEquipment("boots.json", "boots", Boots);
            LoadEquipment("accessories.json", "accessories", Accessories);

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

            LoadFile("tartarus.json", (json) => {
                var root = JsonConvert.DeserializeObject<DungeonRoot>(json);
                if (root != null && root.Dungeons != null)
                {
                    foreach (var d in root.Dungeons)
                    {
                        if (!Dungeons.ContainsKey(d.Id))
                        {
                            Dungeons.Add(d.Id, d);
                            Console.WriteLine($"[System] Loaded Dungeon: {d.Name} with {d.Blocks.Count} blocks.");
                        }
                    }
                }
            });
        }

        private static void LoadEquipment<T>(string filename, string jsonKey, Dictionary<string, T> targetDict) where T : class
        {
            LoadFile(filename, (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<T>>>(json);
                if (root != null && root.ContainsKey(jsonKey))
                {
                    foreach (var item in root[jsonKey])
                    {
                        var prop = item.GetType().GetProperty("Id");
                        if (prop != null)
                        {
                            string id = (string)prop.GetValue(item);
                            if (!targetDict.ContainsKey(id)) targetDict.Add(id, item);
                        }
                    }
                    Console.WriteLine($"[System] Loaded {targetDict.Count} {jsonKey}.");
                }
            });
        }

        private static void AddShopEntries(List<ShopJsonItem> items, ShopCategory cat)
        {
            if (items == null) return;
            foreach (var i in items)
                ShopInventory.Add(new ShopEntry { Id = i.Id, Name = i.Name, BasePrice = i.Price, Category = cat });
        }

        private static void LoadFile(string filename, Action<string> onSuccess)
        {
            // UPDATE: Look into Data/Jsons subfolder
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Jsons", filename);
            if (File.Exists(path))
            {
                onSuccess(File.ReadAllText(path));
            }
            else
            {
                Console.WriteLine($"[Error] {filename} not found at {path}!");
            }
        }
    }
}