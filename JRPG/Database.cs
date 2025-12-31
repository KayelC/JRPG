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

        // New Dictionaries
        public static Dictionary<string, WeaponData> Weapons = new Dictionary<string, WeaponData>();
        public static Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();

        public static void LoadData()
        {
            // 1. Load Skills
            LoadFile("skills_by_category.json", (json) => {
                var skillCats = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(json);
                foreach (var cat in skillCats)
                    foreach (var s in cat.Value)
                        if (!Skills.ContainsKey(s.Name)) Skills.Add(s.Name, s);
                Console.WriteLine($"[System] Loaded {Skills.Count} skills.");
            });

            // 2. Load Personas
            LoadFile("persona_data.json", (json) => {
                var pList = JsonConvert.DeserializeObject<List<PersonaData>>(json);
                foreach (var p in pList)
                    if (!Personas.ContainsKey(p.Id)) Personas.Add(p.Id, p);
                Console.WriteLine($"[System] Loaded {Personas.Count} personas.");
            });

            // 3. Load Ailments
            LoadFile("status_ailments.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AilmentData>>>(json);
                if (root != null && root.ContainsKey("ailments"))
                    foreach (var a in root["ailments"])
                        if (!Ailments.ContainsKey(a.Name)) Ailments.Add(a.Name, a);
                Console.WriteLine($"[System] Loaded {Ailments.Count} ailments.");
            });

            // 4. Load Weapons
            LoadFile("weapons.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<WeaponData>>>(json);
                if (root != null && root.ContainsKey("weapons"))
                    foreach (var w in root["weapons"])
                        if (!Weapons.ContainsKey(w.Id)) Weapons.Add(w.Id, w);
                Console.WriteLine($"[System] Loaded {Weapons.Count} weapons.");
            });

            // 5. Load Items
            LoadFile("items.json", (json) => {
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<ItemData>>>(json);
                if (root != null && root.ContainsKey("items"))
                    foreach (var i in root["items"])
                        if (!Items.ContainsKey(i.Id)) Items.Add(i.Id, i);
                Console.WriteLine($"[System] Loaded {Items.Count} items.");
            });
        }

        private static void LoadFile(string filename, Action<string> onSuccess)
        {
            if (File.Exists(filename))
            {
                string json = File.ReadAllText(filename);
                onSuccess(json);
            }
            else
            {
                Console.WriteLine($"[Error] {filename} not found!");
            }
        }
    }
}