using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            IGameIO io = new ConsoleIO();
            io.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            Database.LoadData(io);

            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();
            DungeonState dungeonState = new DungeonState();

            Combatant player = new Combatant("Hero");

            // Persistent Knowledge Bank: This allows the player to "remember" affinities 
            // across different battles in the same session.
            BattleKnowledge playerKnowledge = new BattleKnowledge();

            player.StatPoints = 0;

            // Scenario Logic
            io.WriteLine("Select Test Scenario:");
            io.WriteLine("1. Human (Basic)");
            io.WriteLine("2. Persona User (Orpheus)");
            io.WriteLine("3. Wild Card (Orpheus + Stock)");
            io.WriteLine("4. Operator (Demons + COMP)");
            io.WriteLine("5. DEBUG: Ailment & Technical Test Suite");

            var key = io.ReadKey();
            bool jumpToBattle = false; // Flag to skip field menus for debugging

            switch (key.KeyChar)
            {
                case '1':
                    player.Class = ClassType.Human;
                    break;
                case '2':
                    player.Class = ClassType.PersonaUser;
                    if (Database.Personas.TryGetValue("orpheus", out var p1))
                        player.ActivePersona = p1.ToPersona();
                    break;
                case '3':
                    player.Class = ClassType.WildCard;
                    if (Database.Personas.TryGetValue("orpheus", out var p2))
                        player.ActivePersona = p2.ToPersona();
                    if (Database.Personas.TryGetValue("pixie", out var p3))
                        player.PersonaStock.Add(p3.ToPersona());
                    if (Database.Personas.TryGetValue("high_pixie", out var p4))
                        player.PersonaStock.Add(p4.ToPersona());
                    break;
                case '4':
                    player.Class = ClassType.Operator;
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("michael", 99));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("pixie", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("high_pixie", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("orpheus", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("io", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("hermes", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("medea", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("mou_ryo", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("flaemis", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("aquans", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("erthrys", 50));
                    break;

                case '5':
                    // DEBUG SUITE SETUP
                    player.Class = ClassType.WildCard;
                    player.Level = 50;
                    // Give the player a custom persona with the tools needed to test
                    if (Database.Personas.TryGetValue("orpheus", out var debugP))
                    {
                        var pInstance = debugP.ToPersona();

                        // Inject specific skills for testing
                        pInstance.SkillSet.Clear();

                        pInstance.SkillSet.Add("Dormina");
                        pInstance.SkillSet.Add("Lullaby");
                        pInstance.SkillSet.Add("Shibaboo");
                        pInstance.SkillSet.Add("Binding Cry");
                        pInstance.SkillSet.Add("Bash");
                        pInstance.SkillSet.Add("Stun Needle");
                        pInstance.SkillSet.Add("Toxic Sting");
                        pInstance.SkillSet.Add("Venom Bite");
                        pInstance.SkillSet.Add("Patra");


                        player.ActivePersona = pInstance;
                    }
                    jumpToBattle = true; // Signal to jump straight to combat
                    break;
            }

            // stat debug :
            player.Level = 80;
            player.StatPoints = 5;

            player.RecalculateResources();

            //player.CurrentHP = player.MaxHP;
            //player.CurrentSP = player.MaxSP;

            player.CurrentHP = 5000;
            player.CurrentSP = 5000;

            inventory.AddItem("101", 5);
            inventory.AddItem("108", 2);
            inventory.AddItem("114", 3);
            inventory.AddItem("113", 3);
            inventory.AddEquipment("1", ShopCategory.Weapon);
            inventory.AddEquipment("201", ShopCategory.Armor);
            inventory.AddEquipment("301", ShopCategory.Boots);
            inventory.AddEquipment("401", ShopCategory.Accessory);

            if (Database.Weapons.TryGetValue("1", out var w)) player.EquippedWeapon = w;
            if (Database.Armors.TryGetValue("201", out var a)) player.EquippedArmor = a;
            if (Database.Boots.TryGetValue("301", out var b)) player.EquippedBoots = b;
            if (Database.Accessories.TryGetValue("401", out var acc))
                player.EquippedAccessory = acc;

            economy.AddMacca(5000000);

            // Advance Moon to a neutral phase for standard testing
            for (int i = 0; i < 4; i++)
            {
                MoonPhaseSystem.Advance();
            }

            // DEBUG INJECTION: If Scenario 5 is chosen, run a controlled battle immediately
            if (jumpToBattle)
            {
                io.Clear();
                io.WriteLine("=== DEBUG SESSION: AILMENT & TECHNICAL TESTING ===", ConsoleColor.Yellow);
                io.WriteLine("Testing Sleep (Wake on Hit), Bind (No Skills), Stun (1-Turn), and Phys Techs.");

                // Create a "Target Dummy" Enemy
                List<Combatant> enemies = new List<Combatant> {
                    CombatantFactory.CreateEnemy("E_slime")
                };
                enemies[0].Name = "Target Dummy";

                // Override stats for a long-lasting test subject
                enemies[0].BaseHP = 9999;
                enemies[0].CurrentHP = 9999;
                // Set stats to 1 so the dummy doesn't accidentally kill the tester
                foreach (var stat in Enum.GetValues(typeof(StatType)))
                {
                    enemies[0].CharacterStats[(StatType)stat] = 1;
                }

                PartyManager pm = new PartyManager(player);
                BattleConductor debugBattle = new BattleConductor(pm, enemies, inventory, economy, io, playerKnowledge, false);
                debugBattle.StartBattle();

                io.WriteLine("\nDebug Battle Concluded. Press any key to exit.");
                io.ReadKey();
                return; // End the program after the debug battle
            }

            FieldConductor field = new FieldConductor(
                player,
                inventory,
                economy,
                dungeonState,
                io,
                playerKnowledge
            );

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