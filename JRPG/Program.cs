using System;
using System.Collections.Generic;
using JRPGPrototype.Services;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            IGameIO io = new ConsoleIO();
            io.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            Database.LoadData();

            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();
            DungeonState dungeonState = new DungeonState();

            Combatant player = new Combatant("Hero");

            //player.CharacterStats[StatType.STR] = 8;
            //player.CharacterStats[StatType.MAG] = 8;
            //player.CharacterStats[StatType.END] = 8;
            //player.CharacterStats[StatType.AGI] = 8;
            //player.CharacterStats[StatType.LUK] = 5;

            player.StatPoints = 0;

            // Scenario Logic
            io.WriteLine("Select Test Scenario:");
            io.WriteLine("1. Human (Basic)");
            io.WriteLine("2. Persona User (Orpheus)");
            io.WriteLine("3. Wild Card (Orpheus + Stock)");
            io.WriteLine("4. Operator (Demons + COMP)");

            var key = io.ReadKey();
            switch (key.KeyChar)
            {
                case '1':
                    player.Class = ClassType.Human;
                    break;
                case '2':
                    player.Class = ClassType.PersonaUser;
                    if (Database.Personas.TryGetValue("orpheus", out var p1)) player.ActivePersona = p1.ToPersona();
                    break;
                case '3':
                    player.Class = ClassType.WildCard;
                    if (Database.Personas.TryGetValue("orpheus", out var p2)) player.ActivePersona = p2.ToPersona();
                    if (Database.Personas.TryGetValue("pixie", out var p3)) player.PersonaStock.Add(p3.ToPersona());
                    break;
                case '4':
                    player.Class = ClassType.Operator;
                    // UPDATED: Use CreateDemon to ensure proper skill progression
                    //player.DemonStock.Add(Combatant.CreateDemon("pixie", 1));
                    player.DemonStock.Add(Combatant.CreateDemon("michael", 99));
                    break;
            }

            player.RecalculateResources();
            player.CurrentHP = player.MaxHP;
            player.CurrentSP = player.MaxSP;

            inventory.AddItem("101", 5);
            inventory.AddItem("108", 2);
            inventory.AddItem("114", 3);
            inventory.AddItem("113", 3);
            inventory.AddEquipment("1", ShopCategory.Weapon);
            inventory.AddEquipment("201", ShopCategory.Armor);

            if (Database.Weapons.TryGetValue("1", out var w)) player.EquippedWeapon = w;
            if (Database.Armors.TryGetValue("201", out var a)) player.EquippedArmor = a;

            economy.AddMacca(5000);

            FieldManager field = new FieldManager(player, inventory, economy, dungeonState, io);

            bool appRunning = true;
            while (appRunning)
            {
                field.NavigateMenus();

                if (player.CurrentHP <= 0)
                {
                    io.Clear();
                    io.WriteLine("\n[GAME OVER] You have collapsed...", ConsoleColor.Red);
                    io.Wait(2000);
                    io.WriteLine("You are dragged back to the entrance by a mysterious force.");
                    io.Wait(2000);

                    player.CurrentHP = 1;
                    player.RemoveAilment();
                    player.CleanupBattleState();

                    dungeonState.ResetToEntry();
                }
                else
                {
                    appRunning = false;
                }
            }

            io.Clear();
            io.WriteLine("\n[GAME SESSION ENDED]", ConsoleColor.Red);
            io.WriteLine("Press any key to exit...");
            io.ReadKey();
        }
    }
}