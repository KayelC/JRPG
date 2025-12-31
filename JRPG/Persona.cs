namespace JRPGPrototype
{
    public class Persona
    {
        public string Name { get; set; }
        public string Arcana { get; set; } // Fool, Magician, etc.
        public int Level { get; set; }
        
        // Dictionary to store affinities. Default is Normal.
        public Dictionary<Element, Affinity> Affinities { get; set; } = new Dictionary<Element, Affinity>();
        
        // List of skill names this Persona knows
        public List<string> SkillSet { get; set; } = new List<string>();

        public Affinity GetAffinity(Element elem)
        {
            return Affinities.ContainsKey(elem) ? Affinities[elem] : Affinity.Normal;
        }
    }
}