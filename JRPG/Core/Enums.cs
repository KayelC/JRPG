namespace JRPGPrototype.Core
{
    public enum Element
    {
        Slash, Strike, Pierce,        // Physical
        Fire, Ice, Elec, Wind, Earth, // Magical
        Light, Dark, Almighty,        // Special
        Mind, Nerve, Curse,           // Ailment Vectors
        None
    }

    public enum Affinity
    {
        Normal,
        Weak,   // Consumes half icon
        Resist, // Normal damage
        Null,   // Consumes 2 icons
        Repel,  // Consumes ALL icons + Reflect
        Absorb  // Consumes ALL icons + Heal
    }

    public enum StatType
    {
        St, Ma, Vi, Ag, Lu, // Common
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

    public enum ClassType
    {
        Human,
        PersonaUser,
        WildCard,
        Operator,
        Demon,
        Avatar
    }

    public enum ControllerType
    {
        LocalPlayer,
        AI,
        NetworkPlayer
    }

    public enum ControlState
    {
        DirectControl,
        ActFreely
    }

    // Defines the conversational archetypes linked to Arcanas
    public enum PersonalityType
    {
        Timid,      // Priestess, Hermit
        Arrogant,   // Magician, Emperor
        Childlike,  // Fool, Chariot
        Sultry,     // Empress, Lovers
        Honorable,  // Justice, Hierophant
        Gloomy,     // Hanged Man, Death
        Upbeat,     // Sun, Star
        Formal      // Temperance, Judgement
    }

    //Enum to communicate exploration results back to the FieldConductor.
    public enum ExplorationEvent
    {
        None,
        Encounter,
        BossEncounter
    }

    /// <summary>
    /// Explicit signals returned by the Service Engine to inform the Conductor 
    /// of side-effects requiring state transitions.
    /// </summary>
    public enum ItemUsageResult
    {
        None,
        Applied,
        Failed,
        RequestDungeonExit
    }

    public enum FusionOperationType
    {
        CreateNewDemon, // The result is a specific demon ID to be created.
        RankUpParent,    // Signal to rank up one of the parents.
        RankDownParent,  // Signal to rank down one of the parents.
        StatBoostFusion, // Signal that Mitama fusion for stat boosting should occur
        NoFusionPossible // The combination yields nothing.
    }
}