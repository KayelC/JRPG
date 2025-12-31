namespace JRPGPrototype
{
    public enum Element
    {
        Slash, Strike, Pierce, // Physical
        Fire, Ice, Elec, Wind, // Magical
        Light, Dark, Almighty, // Special
        None
    }

    public enum Affinity
    {
        Normal,
        Weak,   // 1.5x Dmg + One More
        Resist, // 0.5x Dmg
        Null,   // 0 Dmg
        Repel,  // Reflect
        Absorb  // Heal
    }

    public enum StatType
    {
        STR, MAG, AGI, END, LUK, // Common
        INT, CHA                 // Operator Exclusive
    }

    public enum HitType
    {
        Normal,
        Critical,
        Weakness,
        Miss,
        Repel,
        Absorb,
        Null
    }
}