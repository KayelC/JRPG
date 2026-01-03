namespace JRPGPrototype.Core
{
    public class CombatResult
    {
        public int DamageDealt { get; set; }
        public HitType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
    }
}