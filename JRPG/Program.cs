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

            // 2. Create Test Combatants
            // --- Player ---
            Combatant player = new Combatant("Hero");

            // Set base stats suitable for testing (Balanced AGI/LUK)
            player.CharacterStats[StatType.STR] = 15;
            player.CharacterStats[StatType.AGI] = 15;
            player.CharacterStats[StatType.LUK] = 15;
            player.CharacterStats[StatType.END] = 12;
            player.CharacterStats[StatType.INT] = 10;

            if (Database.Personas.TryGetValue("slime", out var pData))
            {
                player.ActivePersona = pData.ToPersona();
                player.RecalculateResources();
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

            // 3. Manual Weapon Injection
            // CHANGE THIS ID TO TEST DIFFERENT WEAPONS:
            // "1"  = Shortsword (Slash, Melee, 92 Acc) -> Miss causes [DOWN]
            // "8"  = Short Bow (Pierce, Ranged, 98 Acc) -> -20% Acc Penalty, No Down penalty
            // "17" = Gae Bolg (Wind, Melee, 94 Acc) -> Elemental Melee
            // "25" = Scrub Brush (Strike, Melee, 30 Acc) -> High chance to Miss and Fall
            string weaponIdToTest = "25";

            if (Database.Weapons.TryGetValue(weaponIdToTest, out var weapon))
            {
                player.EquippedWeapon = weapon;
            }
            else
            {
                Console.WriteLine($"[Warning] Weapon ID '{weaponIdToTest}' not found. Defaulting to Unarmed.");
            }

            // Give enemy a basic weapon
            if (Database.Weapons.TryGetValue("1", out var enemyWeapon))
            {
                enemy.EquippedWeapon = enemyWeapon;
            }

            // 4. Console Verification
            Console.WriteLine("\n--- SIMULATION CONFIGURATION ---");
            Console.WriteLine($"Player: {player.Name} (AGI: {player.GetStat(StatType.AGI)})");

            if (player.EquippedWeapon != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Equipped Weapon: {player.EquippedWeapon.Name} (ID: {player.EquippedWeapon.Id})");
                Console.WriteLine($"Stats: Type [{player.EquippedWeapon.Type}] | Power [{player.EquippedWeapon.Power}] | Base Acc [{player.EquippedWeapon.Accuracy}%]");

                Console.Write("Mechanic Check: ");
                if (player.IsLongRange)
                {
                    Console.WriteLine("RANGED [TRUE]");
                    Console.WriteLine(">> Logic: Attacks will suffer -20% Accuracy, but missing is safe.");
                }
                else
                {
                    Console.WriteLine("RANGED [FALSE] (Melee)");
                    Console.WriteLine(">> Logic: Standard Accuracy. WARNING: Missing an attack will cause [DOWN] state.");
                }
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Weapon: Unarmed (Strike / Melee)");
            }
            Console.WriteLine("--------------------------------\n");
            Console.WriteLine("Press Enter to Start Battle...");
            Console.ReadLine();

            // 5. Run the Battle
            if (player.ActivePersona != null && enemy.ActivePersona != null)
            {
                new BattleManager(player, enemy).StartBattle();
            }
            else
            {
                Console.WriteLine("[Error] Combatants failed to initialize Personas.");
            }
        }
    }
}