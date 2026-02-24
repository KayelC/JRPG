using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Services;

namespace JRPGPrototype.Entities
{
    public class Persona
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Race { get; set; } = string.Empty;

        // Stats & affinities
        public Dictionary<Element, Affinity> AffinityMap { get; set; } = new Dictionary<Element, Affinity>();
        public Dictionary<StatType, int> StatModifiers { get; set; } = new Dictionary<StatType, int>();

        // Skills
        public List<string> SkillSet { get; set; } = new List<string>();
        public Dictionary<int, string> SkillsToLearn { get; set; } = new Dictionary<int, string>();

        // Growth
        public int Exp { get; set; }
        public int ExpRequired => (int)(1.5 * Math.Pow(Level, 3));

        public Affinity GetAffinity(Element elem)
        {
            return AffinityMap.ContainsKey(elem) ? AffinityMap[elem] : Affinity.Normal;
        }

        public void GainExp(int amount, IGameIO io = null)
        {
            Exp += amount;
            while (Exp >= ExpRequired)
            {
                Exp -= ExpRequired;
                LevelUp(io);
            }
        }

        private void LevelUp(IGameIO io)
        {
            Level++;

            if (io != null)
            {
                io.WriteLine($"\n[PERSONA] {Name} grew to Lv.{Level}!",
                    ConsoleColor.Green);
            }

            // 1. Stat Growth (Random)
            Random rnd = new Random();
            var validStats = new[] { StatType.St, StatType.Ma, StatType.Vi, StatType.Ag, StatType.Lu };

            // Gain 1 point randomly, capped at 40
            for (int i = 0; i < 1; i++) // Currently only 1 point per level, can be adjusted
            {
                StatType stat = validStats[rnd.Next(validStats.Length)];
                if (StatModifiers.ContainsKey(stat))
                {
                    if (StatModifiers[stat] < 40) // Capped at 40 per stat
                    {
                        StatModifiers[stat]++;
                        if (io != null) io.WriteLine($"-> {stat} increased!");
                    }
                }
                else
                {
                    StatModifiers[stat] = 1;
                    if (io != null) io.WriteLine($"-> {stat} increased!");
                }
            }

            // 2. Skill Learning Check
            if (SkillsToLearn.ContainsKey(Level))
            {
                string newSkill = SkillsToLearn[Level];
                // Prevent duplicate learning
                if (!SkillSet.Contains(newSkill))
                {
                    SkillSet.Add(newSkill);
                    if (io != null) io.WriteLine($"-> {Name} learned a new skill: {newSkill}!", ConsoleColor.Cyan);
                }
            }
        }

        //Force Sync for Instantiation
        // Called when creating a Demon/Persona at a specific level to ensure it has correct stats/skills
        public void ScaleToLevel(int targetLevel)
        {
            if (targetLevel <= Level)
            {
                RecalculateSkills();
                return;
            }

            // Simulate Level Ups without IO logs
            while (Level < targetLevel)
            {
                LevelUp(null); // Pass null to avoid console spam
            }
        }

        public void RecalculateSkills()
        {
            foreach (var kvp in SkillsToLearn)
            {
                if (kvp.Key <= Level)
                {
                    if (!SkillSet.Contains(kvp.Value))
                    {
                        SkillSet.Add(kvp.Value);
                    }
                }
            }
        }
    }
}