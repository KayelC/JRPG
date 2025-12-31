using System;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Database
            Console.WriteLine("Initializing Database...");
            Database.LoadData();
            Console.Clear();

            Console.WriteLine("=== SHIN MEGAMI TENSEI: PROTOTYPE BOOT ===");

            // 2. Initialize Managers
            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();

            // 3. Seed Initial State
            inventory.AddItem("101", 5); // Medicine x5
            inventory.AddItem("106", 2); // Snuff Soul x2
            inventory.AddItem("112", 2); // Dis-Poison x2

            inventory.AddWeapon("1");  // Shortsword
            inventory.AddWeapon("8");  // Short Bow
            inventory.AddWeapon("17"); // Gae Bolg

            economy.AddMacca(500);

            // 4. Create Player (The Operator)
            Combatant player = new Combatant("Hero");

            player.CharacterStats[StatType.STR] = 8;
            player.CharacterStats[StatType.MAG] = 8;
            player.CharacterStats[StatType.END] = 8;
            player.CharacterStats[StatType.AGI] = 8;
            player.CharacterStats[StatType.LUK] = 5;
            player.CharacterStats[StatType.INT] = 10;
            player.CharacterStats[StatType.CHA] = 10;

            player.StatPoints = 5;

            if (Database.Personas.TryGetValue("orpheus", out var pData))
            {
                player.ActivePersona = pData.ToPersona();
            }

            player.RecalculateResources();
            player.CurrentHP = player.MaxHP;
            player.CurrentSP = player.MaxSP;

            if (Database.Weapons.TryGetValue("1", out var defWep))
                player.EquippedWeapon = defWep;

            // 5. Game Loop
            bool gameRunning = true;
            int encounterCount = 1;

            while (gameRunning)
            {
                // PHASE 1: FIELD PREPARATION
                FieldManager field = new FieldManager(player, inventory, economy);
                field.NavigateMenus();

                // PHASE 2: GENERATE ENEMY
                Combatant enemy = GenerateEnemy(encounterCount);

                Console.Clear();
                Console.WriteLine($"\n!!! ENCOUNTER {encounterCount} STARTED !!!");
                Console.WriteLine($"Enemy: {enemy.Name} (Lv.{enemy.Level})");
                Console.WriteLine("Loading Battle...\n");
                System.Threading.Thread.Sleep(1000);

                // PHASE 3: BATTLE
                BattleManager battle = new BattleManager(player, enemy, inventory, economy);
                battle.StartBattle();

                // PHASE 4: POST-BATTLE CHECK
                if (player.CurrentHP <= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[GAME OVER] Your demon summoning days are over...");
                    Console.ResetColor();
                    gameRunning = false;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[VICTORY] You survived the encounter.");
                    Console.ResetColor();

                    encounterCount++;
                    Console.WriteLine("Returning to Field Menu...");
                    Console.WriteLine("Press Enter to continue.");
                    Console.ReadLine();
                }
            }
        }

        static Combatant GenerateEnemy(int encounterNum)
        {
            string personaId = (encounterNum % 2 != 0) ? "slime" : "high-pixie";
            string name = (encounterNum % 2 != 0) ? "Slime" : "High Pixie";
            string weaponId = "1";

            Combatant enemy = new Combatant(name);

            if (Database.Personas.TryGetValue(personaId, out var eData))
            {
                enemy.ActivePersona = eData.ToPersona();
                enemy.Level = eData.Level;
            }

            // Scale Enemy Stats
            int baseStat = 5 + (enemy.Level / 2);
            foreach (StatType s in Enum.GetValues(typeof(StatType)))
            {
                enemy.CharacterStats[s] = baseStat;
            }

            if (Database.Weapons.TryGetValue(weaponId, out var wData))
            {
                enemy.EquippedWeapon = wData;
            }

            enemy.RecalculateResources();

            // --- BUG FIX: Explicitly fill HP/SP after recalculation ---
            enemy.CurrentHP = enemy.MaxHP;
            enemy.CurrentSP = enemy.MaxSP;

            return enemy;
        }
    }
}