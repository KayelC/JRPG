using System;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Data
            Database.LoadData();
            Console.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            // 2. Setup Managers
            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();

            // 3. Seed Items
            inventory.AddItem("101", 5);
            inventory.AddItem("112", 2);

            // Seed Weapons
            inventory.AddEquipment("1", ShopCategory.Weapon); // Shortsword
            inventory.AddEquipment("8", ShopCategory.Weapon); // Short Bow

            // Seed Armor/Boots/Accessories for testing
            inventory.AddEquipment("201", ShopCategory.Armor); // School Uniform
            inventory.AddEquipment("301", ShopCategory.Boots); // Rubber Soles
            inventory.AddEquipment("403", ShopCategory.Accessory); // Lucky Coin

            economy.AddMacca(5000); // More money for shopping test

            // 4. Create Player
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
                player.ActivePersona = pData.ToPersona();

            player.RecalculateResources();
            player.CurrentHP = player.MaxHP;
            player.CurrentSP = player.MaxSP;

            // Default Equips
            if (Database.Weapons.TryGetValue("1", out var defWep)) player.EquippedWeapon = defWep;
            if (Database.Armors.TryGetValue("201", out var defArm)) player.EquippedArmor = defArm;

            // 5. Game Loop
            bool gameRunning = true;
            int encounterCount = 1;

            while (gameRunning)
            {
                FieldManager field = new FieldManager(player, inventory, economy);
                field.NavigateMenus();

                Combatant enemy = GenerateEnemy(encounterCount);

                Console.Clear();
                Console.WriteLine($"\n!!! ENCOUNTER {encounterCount} STARTED !!!");
                System.Threading.Thread.Sleep(1000);

                BattleManager battle = new BattleManager(player, enemy, inventory, economy);
                battle.StartBattle();

                if (player.CurrentHP <= 0)
                {
                    Console.WriteLine("\n[GAME OVER]");
                    gameRunning = false;
                }
                else
                {
                    encounterCount++;
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                }
            }
        }

        static Combatant GenerateEnemy(int encounterNum)
        {
            string personaId = (encounterNum % 2 != 0) ? "slime" : "high-pixie";
            string name = (encounterNum % 2 != 0) ? "Slime" : "High Pixie";

            Combatant enemy = new Combatant(name);
            if (Database.Personas.TryGetValue(personaId, out var eData))
            {
                enemy.ActivePersona = eData.ToPersona();
                enemy.Level = eData.Level;
            }

            int baseStat = 5 + (enemy.Level / 2);
            foreach (StatType s in Enum.GetValues(typeof(StatType)))
                enemy.CharacterStats[s] = baseStat;

            if (Database.Weapons.TryGetValue("1", out var wData)) enemy.EquippedWeapon = wData;

            // Give enemy some armor too
            if (Database.Armors.TryGetValue("201", out var aData)) enemy.EquippedArmor = aData;

            enemy.RecalculateResources();
            enemy.CurrentHP = enemy.MaxHP;
            enemy.CurrentSP = enemy.MaxSP;

            return enemy;
        }
    }
}