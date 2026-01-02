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

            // 4. Create Player (Hardcoded starter for now, but fully constructable)
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

            // Seed items for testing
            inventory.AddItem("101", 5); // Medicine
            inventory.AddEquipment("1", ShopCategory.Weapon); // Shortsword
            inventory.AddEquipment("201", ShopCategory.Armor); // School Uniform

            // Equip Default Gear
            if (Database.Weapons.TryGetValue("1", out var w)) player.EquippedWeapon = w;
            if (Database.Armors.TryGetValue("201", out var a)) player.EquippedArmor = a;

            // Give some starting money
            economy.AddMacca(5000);

            bool gameRunning = true;
            int encounterCount = 0;

            while (gameRunning)
            {
                // Field Phase
                FieldManager field = new FieldManager(player, inventory, economy, io);
                field.NavigateMenus();

                // Battle Phase
                // Simulate traversing to next encounter (since DungeonManager isn't fully active yet)
                encounterCount++;
                io.Clear();
                io.WriteLine($"\n!!! ENCOUNTER {encounterCount} STARTED !!!", ConsoleColor.Red);
                io.Wait(1000);

                // --- NEW ENEMY GENERATION USING DATABASE ---
                string enemyId = (encounterCount % 2 != 0) ? "E_slime" : "E_high-pixie";
                Combatant enemy;

                if (Database.Enemies.TryGetValue(enemyId, out var eData))
                {
                    enemy = Combatant.CreateFromData(eData);
                }
                else
                {
                    // Fallback to avoid crash if JSON is missing IDs
                    io.WriteLine("[Error] Enemy data not found, spawning generic.");
                    enemy = new Combatant("Glitch");
                }

                // Inject IO into BattleManager
                BattleManager battle = new BattleManager(player, enemy, inventory, economy, io);
                battle.StartBattle();

                if (player.CurrentHP <= 0)
                {
                    io.WriteLine("\n[GAME OVER]");
                    gameRunning = false;
                }
                else
                {
                    io.WriteLine("Press any key to continue...");
                    io.ReadKey();
                }
            }
        }
    }
}