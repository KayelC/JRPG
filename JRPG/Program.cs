using System;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Data
            Database.LoadData();

            Console.WriteLine("\n=== JRPG PROTOTYPE INITIALIZING ===");

            // 2. Setup Inventory
            InventoryManager inventory = new InventoryManager();
            inventory.AddItem("101", 3); // Medicine x3
            inventory.AddItem("106", 1); // Snuff Soul x1
            inventory.AddItem("112", 2); // Dis-Poison x2

            // Add Weapons
            inventory.AddWeapon("17"); // Gae Bolg
            inventory.AddWeapon("1");  // Shortsword
            inventory.AddWeapon("8");  // Short Bow

            Console.WriteLine("[System] Inventory populated.");

            // 3. Create Player
            Combatant player = new Combatant("Hero");
            player.CharacterStats[StatType.STR] = 15;
            player.CharacterStats[StatType.AGI] = 15;
            player.CharacterStats[StatType.LUK] = 15;
            player.CharacterStats[StatType.END] = 12;
            player.CharacterStats[StatType.INT] = 10;

            if (Database.Personas.TryGetValue("orpheus", out var pData))
            {
                player.ActivePersona = pData.ToPersona();
                player.RecalculateResources();
                // Test Damage
                player.CurrentHP = 20;
                player.CurrentSP = 10;
            }
            // Default Weapon
            if (Database.Weapons.TryGetValue("1", out var defWep)) player.EquippedWeapon = defWep;

            // 4. Create Enemy
            Combatant enemy = new Combatant("Shadow");
            enemy.CharacterStats[StatType.STR] = 12;
            enemy.CharacterStats[StatType.AGI] = 12;
            enemy.CharacterStats[StatType.LUK] = 5;
            enemy.CharacterStats[StatType.END] = 10;
            enemy.CharacterStats[StatType.INT] = 8;
            if (Database.Personas.TryGetValue("slime", out var eData))
            {
                enemy.ActivePersona = eData.ToPersona();
                enemy.RecalculateResources();
            }
            if (Database.Weapons.TryGetValue("1", out var eWep)) enemy.EquippedWeapon = eWep;

            // 5. Enter Field/Setup Menu
            // The game loop stays here until the player chooses to "Proceed"
            FieldManager fieldManager = new FieldManager(player, inventory);
            fieldManager.NavigateMenus();

            // 6. Start Battle
            if (player.ActivePersona != null && enemy.ActivePersona != null)
            {
                // Passing inventory allows item use during battle as well (via BattleManager logic)
                new BattleManager(player, enemy, inventory).StartBattle();
            }
            else
            {
                Console.WriteLine("[Error] Combatants failed to initialize.");
            }
        }
    }
}