namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// Persistent data container for the Field UI.
    /// Ensures cursor positions and selection states are maintained across menu transitions.
    /// </summary>
    public class FieldUIState
    {
        public int MainMenuIndex { get; set; } = 0;
        public int InventoryMenuIndex { get; set; } = 0;
        public int ItemMenuIndex { get; set; } = 0;
        public int SkillMenuIndex { get; set; } = 0;
        public int EquipSlotIndex { get; set; } = 0;
        public int CityMenuIndex { get; set; } = 0;
        public int DungeonMenuIndex { get; set; } = 0;
        public int StatusHubIndex { get; set; } = 0;
        public int EquipListIndex { get; set; } = 0;
    }
}