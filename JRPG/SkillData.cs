using Newtonsoft.Json;
using System.Collections.Generic;

namespace JRPGPrototype
{
    public class SkillData
    {
        [JsonProperty("Skill")]
        public string Name { get; set; }
        
        public string Effect { get; set; }

        // Use nullable int because your JSON has 'NaN' for status skills
        public string Power { get; set; } 
        public string Accuracy { get; set; }
        
        public string Cost { get; set; } // e.g., "6 SP" or "10% HP"
        public string Category { get; set; }

        // Helper properties to use in logic
        public int GetPowerVal() => int.TryParse(Power, out int v) ? v : 0;
        
        public (int value, bool isPercentage, bool isHP) ParseCost()
        {
            // simple parser logic for "6 SP" vs "10% HP"
            if (string.IsNullOrEmpty(Cost)) return (0, false, false);
            
            bool isHp = Cost.Contains("HP");
            bool isPercent = Cost.Contains("%");
            string numPart = Cost.Replace("SP", "").Replace("HP", "").Replace("%", "").Trim();
            
            int.TryParse(numPart, out int val);
            return (val, isPercent, isHp);
        }
    }

    public static class SkillDatabase
    {
        // Dictionary mapping Skill Name -> SkillData object
        public static Dictionary<string, SkillData> AllSkills = new Dictionary<string, SkillData>();

        public static void LoadSkills(string jsonContent)
        {
            // Your JSON is a Dictionary<Category, List<Skill>>
            var data = JsonConvert.DeserializeObject<Dictionary<string, List<SkillData>>>(jsonContent);
            
            foreach(var category in data)
            {
                foreach(var skill in category.Value)
                {
                    if(!AllSkills.ContainsKey(skill.Name))
                        AllSkills.Add(skill.Name, skill);
                }
            }
            Console.WriteLine($"Loaded {AllSkills.Count} skills into the database.");
        }
    }
}