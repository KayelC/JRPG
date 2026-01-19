using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using JRPGPrototype.Core;
using JRPGPrototype.Services;

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

        // Added support for the Fusion Recipe Table
        public static List<FusionRecipe> FusionRecipes = new List<FusionRecipe>();

        // Initialize the property to prevent null reference on failed load
        public static NegotiationQuestionRoot NegotiationQuestions { get; private set; } = new NegotiationQuestionRoot();

        public static List<ShopEntry> ShopInventory = new List<ShopEntry>();

        /// <summary>
        /// Orchestrates the hydration of the static database.
        /// </summary>
        public static void LoadData(IGameIO io)
        {
            LoadFile(io, "skills_by_category.json", (json) => {
                var skillCats = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(json);
                if (skillCats != null)
                {
                    foreach (var cat in skillCats)
                        foreach (var s in cat.Value) if (!Skills.ContainsKey(s.Name))
                                Skills.Add(s.Name, s);
                    io.WriteLine($"[System] Loaded {Skills.Count} skills.", ConsoleColor.Green);
                }
            });

            LoadFile(io, "persona_data.json", (json) => {
                var pList = JsonConvert.DeserializeObject<List<PersonaData>>(json);
                if (pList != null)
                {
                    foreach (var p in pList) if (!Personas.ContainsKey(p.Id))
                            Personas.Add(p.Id, p);
                    io.WriteLine($"[System] Loaded {Personas.Count} personas.", ConsoleColor.Green);
                }
            });

            LoadFile(io, "enemies.json", (json) => {
                var eList = JsonConvert.DeserializeObject<List<EnemyData>>(json);
                if (eList != null)
                {
                    foreach (var e in eList) if (!Enemies.ContainsKey(e.Id))
                            Enemies.Add(e.Id, e);
                    io.WriteLine($"[System] Loaded {Enemies.Count} enemies.", ConsoleColor.Green);
                }
            });

            LoadFile(io, "status_ailments.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AilmentData>>>(json);
                if (root != null && root.ContainsKey("ailments"))
                {
                    foreach (var a in root["ailments"]) if (!Ailments.ContainsKey(a.Name))
                            Ailments.Add(a.Name, a);
                    io.WriteLine($"[System] Loaded {Ailments.Count} ailments.", ConsoleColor.Green);
                }
            });

            // Loading logic for the fusion recipes (Arcana-based)
            LoadFile(io, "fusion_table.json", (json) => {
                var root = JsonConvert.DeserializeObject<FusionTableRoot>(json);
                if (root != null && root.Recipes != null)
                {
                    FusionRecipes = root.Recipes;
                }
                io.WriteLine($"[System] Loaded {FusionRecipes.Count} fusion recipes.", ConsoleColor.Green);
            });

            // Loading logic for the negotiation questions
            LoadFile(io, "questions.json", (json) => {
                var root = JsonConvert.DeserializeObject<NegotiationQuestionRoot>(json);
                if (root != null)
                {
                    NegotiationQuestions = root;
                }
                else
                {
                    NegotiationQuestions = new NegotiationQuestionRoot { Questions = new Dictionary<PersonalityType, List<NegotiationQuestion>>() };
                }
                io.WriteLine($"[System] Loaded negotiation questions.", ConsoleColor.Green);
            });

            LoadFile(io, "items.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<ItemData>>>(json);
                if (root != null && root.ContainsKey("items"))
                {
                    foreach (var i in root["items"]) if (!Items.ContainsKey(i.Id))
                            Items.Add(i.Id, i);
                    io.WriteLine($"[System] Loaded {Items.Count} items.", ConsoleColor.Green);
                }
            });

            LoadEquipment(io, "weapons.json", "weapons", Weapons);
            LoadEquipment(io, "armor.json", "armor", Armors);
            LoadEquipment(io, "boots.json", "boots", Boots);
            LoadEquipment(io, "accessories.json", "accessories", Accessories);

            LoadFile(io, "shop_inventory.json", (json) => {
                var root = JsonConvert.DeserializeObject<ShopJsonRoot>(json);
                if (root != null)
                {
                    ShopInventory.Clear();
                    AddShopEntries(root.Items, ShopCategory.Item);
                    AddShopEntries(root.Weapons, ShopCategory.Weapon);
                    AddShopEntries(root.Armor, ShopCategory.Armor);
                    AddShopEntries(root.Boots, ShopCategory.Boots);
                    AddShopEntries(root.Accessories, ShopCategory.Accessory);
                    io.WriteLine($"[System] Loaded {ShopInventory.Count} shop entries.", ConsoleColor.Green);
                }
            });

            LoadFile(io, "tartarus.json", (json) => {
                var root = JsonConvert.DeserializeObject<DungeonRoot>(json);
                if (root != null && root.Dungeons != null)
                {
                    foreach (var d in root.Dungeons)
                    {
                        if (!Dungeons.ContainsKey(d.Id))
                        {
                            Dungeons.Add(d.Id, d);
                            io.WriteLine($"[System] Loaded Dungeon: {d.Name} with {d.Blocks.Count} blocks.", ConsoleColor.Green);
                        }
                    }
                }
            });
        }

        private static void LoadEquipment<T>(IGameIO io, string filename, string jsonKey, Dictionary<string, T> targetDict) where T : class
        {
            LoadFile(io, filename, (json) => {
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
                    io.WriteLine($"[System] Loaded {targetDict.Count} {jsonKey}.", ConsoleColor.Green);
                }
            });
        }

        private static void AddShopEntries(List<ShopJsonItem> items, ShopCategory cat)
        {
            if (items == null) return;
            foreach (var i in items)
                ShopInventory.Add(new ShopEntry { Id = i.Id, Name = i.Name, BasePrice = i.Price, Category = cat });
        }

        private static void LoadFile(IGameIO io, string filename, Action<string> onSuccess)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Jsons", filename);
            if (File.Exists(path))
            {
                onSuccess(File.ReadAllText(path));
            }
            else
            {
                // Fix: Errors now reported via IGameIO with red highlight
                io.WriteLine($"[Error] Data integrity failure: {filename} not found at {path}!", ConsoleColor.Red);
            }
        }
    }

    #region Helper Fusion Classes

    public class FusionTableRoot
    {
        [JsonProperty("recipes")]
        public List<FusionRecipe> Recipes { get; set; }
    }

    public class FusionRecipe
    {
        [JsonProperty("parentA")]
        public string ParentA { get; set; }

        [JsonProperty("parentB")]
        public string ParentB { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }
    }

    #endregion
}