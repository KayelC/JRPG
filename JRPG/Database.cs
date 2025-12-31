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

        public static void LoadData()
        {
            // Load Skills
            if (File.Exists("skills_by_category.json"))
            {
                string json = File.ReadAllText("skills_by_category.json");
                var skillCats = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(json);
                foreach (var cat in skillCats)
                {
                    foreach (var s in cat.Value)
                    {
                        if (!Skills.ContainsKey(s.Name)) Skills.Add(s.Name, s);
                    }
                }
                Console.WriteLine($"[System] Loaded {Skills.Count} skills.");
            }
            else
            {
                Console.WriteLine("[Error] skills_by_category.json not found!");
            }

            // Load Personas
            if (File.Exists("persona_data.json"))
            {
                string json = File.ReadAllText("persona_data.json");
                var pList = JsonConvert.DeserializeObject<List<PersonaData>>(json);
                foreach (var p in pList)
                {
                    if (!Personas.ContainsKey(p.Id)) Personas.Add(p.Id, p);
                }
                Console.WriteLine($"[System] Loaded {Personas.Count} personas.");
            }
            else
            {
                Console.WriteLine("[Error] persona_data.json not found!");
            }

            // Load Ailments
            if (File.Exists("status_ailments.json"))
            {
                string json = File.ReadAllText("status_ailments.json");
                var root = JsonConvert.DeserializeObject<Dictionary<string, List<AilmentData>>>(json);
                if (root != null && root.ContainsKey("ailments"))
                {
                    foreach (var a in root["ailments"])
                    {
                        if (!Ailments.ContainsKey(a.Name)) Ailments.Add(a.Name, a);
                    }
                }
                Console.WriteLine($"[System] Loaded {Ailments.Count} ailments.");
            }
            else
            {
                Console.WriteLine("[Error] status_ailments.json not found!");
            }
        }
    }
}