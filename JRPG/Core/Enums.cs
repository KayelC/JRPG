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
        Weak,   // Consumes minimal icons (Press Turn)
        Resist, // Normal damage
        Null,   // Consumes extra icons (Penalty)
        Repel,  // Consumes extra icons + Reflect
        Absorb  // Consumes extra icons + Heal
    }

    public enum StatType
    {
        STR, MAG, AGI, END, LUK, // Common
        INT, CHA                 // Player Exclusive
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

    // Defines the "Job" or "Race" of the entity
    public enum ClassType
    {
        Human,          // Basic, Item user, Gun user
        PersonaUser,    // Can equip 1 Persona
        WildCard,       // Can equip multiple Personas
        Operator,       // Controls Demons, uses Commander Skills
        Demon,          // Summoned entity
        Avatar          // DDS Transformation (DLC)
    }

    // Defines who is pulling the strings (Crucial for Multiplayer)
    public enum ControllerType
    {
        LocalPlayer,    // Input from keyboard/gamepad
        AI,             // Controlled by script (Enemy or Auto-Battle Ally)
        NetworkPlayer   // Controlled by remote client (Future proofing)
    }
}