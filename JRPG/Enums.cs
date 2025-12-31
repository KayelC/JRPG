namespace JRPGPrototype
{
    // Based on your Element System table
    public enum Element
    {
        Slash, Strike, Pierce, // Physical
        Fire, Ice, Elec, Wind, // Magical
        Light, Dark, Almighty, // Instakill/Special
        None // For support/passive
    }

    // Based on Affinities table
    public enum Affinity
    {
        Normal,
        Weak,   // Takes extra damage, causes Knockdown
        Resist, // Takes reduced damage
        Null,   // 0 Damage
        Repel,  // Reflects damage
        Absorb  // Heals HP
    }

    // Based on Human Side stats
    public enum StatType
    {
        STR, // Strength (Physical)
        MAG, // Magic (Spells)
        AGI, // Turn order/Evasion
        CHA, // Negotiation
        LUK, // Crits/Status
        END, // Defense/HP calculation
        INT  // SP calculation
    }
}