using System.Collections.Generic;

namespace JRPGPrototype
{
    public class Persona
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public string Arcana { get; set; }
        public Dictionary<Element, Affinity> AffinityMap { get; set; } = new Dictionary<Element, Affinity>();
        public Dictionary<StatType, int> StatModifiers { get; set; } = new Dictionary<StatType, int>();
        public List<string> SkillSet { get; set; } = new List<string>();

        public Affinity GetAffinity(Element elem)
        {
            return AffinityMap.ContainsKey(elem) ? AffinityMap[elem] : Affinity.Normal;
        }
    }
}