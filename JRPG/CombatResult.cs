namespace JRPGPrototype
{
    // Updated CombatResult to track Criticals
    public class CombatResult
    {
        public int DamageDealt { get; set; }
        public HitType Type { get; set; }
        public string Message { get; set; }
        public bool IsCritical { get; set; }
    }
}   