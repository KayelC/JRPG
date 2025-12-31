using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    public class Persona
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Arcana { get; set; } = string.Empty;

        // Stats
        public Dictionary<Element, Affinity> AffinityMap { get; set; } = new Dictionary<Element, Affinity>();
        public Dictionary<StatType, int> StatModifiers { get; set; } = new Dictionary<StatType, int>();
        public List<string> SkillSet { get; set; } = new List<string>();

        // Skills to Learn: Key = Level, Value = Skill Name
        public Dictionary<int, string> SkillsToLearn { get; set; } = new Dictionary<int, string>();

        // Growth
        public int Exp { get; set; }
        public int ExpRequired => (int)(1.5 * Math.Pow(Level, 3));

        public Affinity GetAffinity(Element elem)
        {
            return AffinityMap.ContainsKey(elem) ? AffinityMap[elem] : Affinity.Normal;
        }

        public void GainExp(int amount)
        {
            Exp += amount;
            while (Exp >= ExpRequired)
            {
                Exp -= ExpRequired;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            Level++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[PERSONA] {Name} grew to Lv.{Level}!");
            Console.ResetColor();

            // 1. Stat Growth (Random)
            Random rnd = new Random();
            var validStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            for (int i = 0; i < 3; i++)
            {
                StatType stat = validStats[rnd.Next(validStats.Length)];
                if (StatModifiers.ContainsKey(stat)) StatModifiers[stat]++;
                else StatModifiers[stat] = 1;
                Console.WriteLine($"-> {stat} increased!");
            }

            // 2. Skill Learning Check
            if (SkillsToLearn.ContainsKey(Level))
            {
                string newSkill = SkillsToLearn[Level];
                // Prevent duplicate learning
                if (!SkillSet.Contains(newSkill))
                {
                    SkillSet.Add(newSkill);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"-> {Name} learned a new skill: {newSkill}!");
                    Console.ResetColor();
                }
            }
        }
    }
}