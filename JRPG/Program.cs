using System;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Infrastructure
            IGameIO io = new ConsoleIO();
            io.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            // 2. Initialize Data
            Database.LoadData();

            // 3. Setup Managers
            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();
            DungeonState dungeonState = new DungeonState();

            // 4. Create Player
            Combatant player = new Combatant("Hero");

            // Basic starter stats
            player.CharacterStats[StatType.STR] = 8;
            player.CharacterStats[StatType.MAG] = 8;
            player.CharacterStats[StatType.END] = 8;
            player.CharacterStats[StatType.AGI] = 8;
            player.CharacterStats[StatType.LUK] = 5;
            player.CharacterStats[StatType.INT] = 10;
            player.CharacterStats[StatType.CHA] = 10;
            player.StatPoints = 5;

            // Load Starter Persona
            if (Database.Personas.TryGetValue("orpheus", out var pData))
                player.ActivePersona = pData.ToPersona();

            // Finalize Resource Calculation
            player.RecalculateResources();
            player.CurrentHP = player.MaxHP;
            player.CurrentSP = player.MaxSP;

            // Seed items
            inventory.AddItem("101", 5); // Medicine
            inventory.AddItem("108", 2); // Soul Drop
            inventory.AddItem("114", 3); // Goho-M
            inventory.AddItem("113", 3); // Traesto Gem (Battle Escape)
            inventory.AddEquipment("1", ShopCategory.Weapon); // Shortsword
            inventory.AddEquipment("201", ShopCategory.Armor); // School Uniform

            // Equip Default Gear
            if (Database.Weapons.TryGetValue("1", out var w)) player.EquippedWeapon = w;
            if (Database.Armors.TryGetValue("201", out var a)) player.EquippedArmor = a;

            // Give some starting money
            economy.AddMacca(5000);

            // 5. Game Loop
            FieldManager field = new FieldManager(player, inventory, economy, dungeonState, io);

            bool appRunning = true;
            while (appRunning)
            {
                // Run the main game loop logic
                field.NavigateMenus();

                // If NavigateMenus returns, it means we either quit or died.
                if (player.CurrentHP <= 0)
                {
                    io.Clear();
                    io.WriteLine("\n[GAME OVER] You have collapsed...", ConsoleColor.Red);
                    io.Wait(2000);
                    io.WriteLine("You are dragged back to the entrance by a mysterious force.");
                    io.Wait(2000);

                    // Revive at Lobby with penalty
                    player.CurrentHP = 1;
                    player.IsDown = false;
                    player.IsDizzy = false;
                    player.RemoveAilment();

                    // Reset Dungeon State
                    dungeonState.ResetToEntry();

                    // Loop continues (Respawn)
                }
                else
                {
                    // Clean exit from menu
                    appRunning = false;
                }
            }

            // End State
            io.Clear();
            io.WriteLine("\n[GAME SESSION ENDED]", ConsoleColor.Red);
            io.WriteLine("Press any key to exit...");
            io.ReadKey();
        }
    }
}