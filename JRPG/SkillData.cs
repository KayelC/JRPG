using Newtonsoft.Json;

namespace JRPGPrototype
{
    public class SkillData
    {
        [JsonProperty("Skill")]
        public string Name { get; set; }

        public string Effect { get; set; }

        public string Power { get; set; }
        public string Accuracy { get; set; }
        public string Cost { get; set; } // e.g. "6 SP"
        public string Category { get; set; }

        public int GetPowerVal()
        {
            if (int.TryParse(Power, out int v)) return v;
            return 0;
        }

        public (int value, bool isPercentage, bool isHP) ParseCost()
        {
            if (string.IsNullOrEmpty(Cost)) return (0, false, false);

            bool isHp = Cost.Contains("HP");
            bool isPercent = Cost.Contains("%");

            string numPart = Cost.Replace("SP", "").Replace("HP", "").Replace("%", "").Trim();

            int.TryParse(numPart, out int val);
            return (val, isPercent, isHp);
        }
    }
}