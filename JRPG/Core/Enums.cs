namespace JRPGPrototype.Core
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
        Weak,
        Resist,
        Null,
        Repel,
        Absorb
    }

    public enum StatType
    {
        STR, MAG, AGI, END, LUK,
        INT, CHA
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

    public enum DungeonEventType
    {
        Empty,
        Battle,
        Boss,
        SafeRoom,
        BlockEnd
    }
}