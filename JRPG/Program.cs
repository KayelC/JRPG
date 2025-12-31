using System;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Data
            Database.LoadData();

            Console.WriteLine("\n=== BATTLE SIMULATOR INITIALIZING ===");

            // 2. Initialize Inventory & Add Test Items
            InventoryManager inventory = new InventoryManager();
            inventory.AddItem("101", 3); // Medicine x3
            inventory.AddItem("106", 1); // Snuff Soul x1
            inventory.AddItem("112", 2); // Dis-Poison x2
            Console.WriteLine("[System] Inventory Initialized with Test Items.");

            // 3. Create Test Combatants
            Combatant player = new Combatant("Hero");

            // Set stats - Reduced HP to test healing
            player.CharacterStats[StatType.STR] = 15;
            player.CharacterStats[StatType.AGI] = 15;
            player.CharacterStats[StatType.LUK] = 15;
            player.CharacterStats[StatType.END] = 12;
            player.CharacterStats[StatType.INT] = 10;

            if (Database.Personas.TryGetValue("orpheus", out var pData))
            {
                player.ActivePersona = pData.ToPersona();
                player.RecalculateResources();

                // Damage player to test healing
                player.CurrentHP -= 50;
                player.CurrentSP -= 20;
            }

            // --- Enemy ---
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

            // 4. Manual Weapon Injection
            string weaponIdToTest = "17";

            if (Database.Weapons.TryGetValue(weaponIdToTest, out var weapon))
            {
                player.EquippedWeapon = weapon;
            }
            else
            {
                Console.WriteLine($"[Warning] Weapon ID '{weaponIdToTest}' not found. Defaulting to Unarmed.");
            }

            if (Database.Weapons.TryGetValue("1", out var enemyWeapon))
            {
                enemy.EquippedWeapon = enemyWeapon;
            }

            // 5. Console Verification
            Console.WriteLine("\n--- SIMULATION CONFIGURATION ---");
            Console.WriteLine($"Player: {player.Name} (HP: {player.CurrentHP}/{player.MaxHP} | SP: {player.CurrentSP}/{player.MaxSP})");
            Console.WriteLine(">> Player starts damaged to facilitate ITEM testing.");

            if (player.EquippedWeapon != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Equipped Weapon: {player.EquippedWeapon.Name}");
                Console.ResetColor();
            }
            Console.WriteLine("--------------------------------\n");
            Console.WriteLine("Press Enter to Start Battle...");
            Console.ReadLine();

            // 6. Run the Battle (with Inventory)
            if (player.ActivePersona != null && enemy.ActivePersona != null)
            {
                new BattleManager(player, enemy, inventory).StartBattle();
            }
            else
            {
                Console.WriteLine("[Error] Combatants failed to initialize Personas.");
            }
        }
    }
}