using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Services;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic
{
    public class FieldManager
    {
        private Combatant _player;
        private InventoryManager _inventory;
        private EconomyManager _economy;
        private ShopManager _shop;
        private DungeonManager _dungeon;
        private DungeonState _dungeonState;
        private IGameIO _io;

        private PartyManager _partyManager;

        private int _mainMenuIndex = 0;
        private int _statusMenuIndex = 0;
        private int _inventoryMenuIndex = 0;
        private int _itemMenuIndex = 0;
        private int _skillMenuIndex = 0;
        private int _equipSlotIndex = 0;
        private int _cityMenuIndex = 0;
        private int _dungeonMenuIndex = 0;

        public FieldManager(Combatant player, InventoryManager inventory, EconomyManager economy, DungeonState dungeonState, IGameIO io)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _dungeonState = dungeonState;
            _io = io;
            _shop = new ShopManager(inventory, economy, io);
            _dungeon = new DungeonManager(dungeonState);
            _partyManager = new PartyManager(_player);
        }

        public void NavigateMenus()
        {
            while (true)
            {
                string header = $"=== FIELD MENU ===\n" +
                                $"Macca: {_economy.Macca}\n" +
                                $"HP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                List<string> options = new List<string>
                {
                    "Explore Tartarus",
                    "City Services",
                    "Inventory",
                    "Status",
                    "Velvet Room"
                };

                if (_player.Class == ClassType.Operator) options.Add("Organize Party");

                int choice = _io.RenderMenu(header, options, _mainMenuIndex);

                if (choice == -1) continue;

                _mainMenuIndex = choice;
                string selectedOption = options[choice];

                if (selectedOption == "Explore Tartarus") PrepareDungeonEntry();
                else if (selectedOption == "City Services") OpenCityMenu();
                else if (selectedOption == "Inventory") OpenInventoryMenu(false);
                else if (selectedOption == "Status") OpenSeamlessStatusMenu();
                else if (selectedOption == "Organize Party") OpenOrganizeMenu();
                else if (selectedOption == "Velvet Room")
                {
                    _io.WriteLine("The nose is long... but this feature isn't ready yet.");
                    _io.Wait(1000);
                }

                if (_player.CurrentHP <= 0) return;
            }
        }

        // --- 1. HUMAN STATUS RENDERER (Updated UX) ---
        private void RenderHumanStatus(Combatant entity)
        {
            _io.WriteLine("=== STATUS & PARAMETERS ===");
            _io.WriteLine($"Name: {entity.Name} (Lv.{entity.Level}) | Class: {entity.Class}");
            _io.WriteLine($"HP: {entity.CurrentHP}/{entity.MaxHP}  SP: {entity.CurrentSP}/{entity.MaxSP}");
            _io.WriteLine($"EXP: {entity.Exp}/{entity.ExpRequired}  Next: {entity.ExpRequired - entity.Exp}");
            _io.WriteLine("-----------------------------");

            // UX UPDATE: Conditional Math Display
            var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>();
            foreach (var stat in stats)
            {
                int total = entity.GetStat(stat);
                int baseVal = entity.CharacterStats[stat];

                // INT and CHA never show math, just the value
                if (stat == StatType.INT || stat == StatType.CHA)
                {
                    _io.WriteLine($"{stat,-4}: {total,3}");
                    continue;
                }

                // If Persona User or Wild Card, show the calculation: Base + Mod = Total
                if (entity.Class == ClassType.PersonaUser || entity.Class == ClassType.WildCard)
                {
                    int mod = total - baseVal;
                    _io.WriteLine($"{stat,-4}: {baseVal,3} + {mod,3} = {total,3}");
                }
                else
                {
                    // Operator / Human: Standard format "Total (Base+)"
                    string color = total > baseVal ? "+" : "";
                    _io.WriteLine($"{stat,-4}: {total,3} ({baseVal,3}{color})");
                }
            }
            _io.WriteLine("-----------------------------");

            // Context: Persona Info (Summary)
            if (entity.Class == ClassType.PersonaUser || entity.Class == ClassType.WildCard)
            {
                if (entity.ActivePersona != null)
                {
                    Persona p = entity.ActivePersona;
                    _io.WriteLine($"[ACTIVE PERSONA]: {p.Name} (Lv.{p.Level}) Arcana: {p.Arcana}");
                    _io.WriteLine("Skills:");
                    foreach (var s in p.SkillSet) _io.WriteLine($" - {s}");
                }
                else
                {
                    _io.WriteLine("[ACTIVE PERSONA]: None");
                }
            }
            // Context: Operator Info (Summary)
            else if (entity.Class == ClassType.Operator)
            {
                if (entity.ExtraSkills.Count > 0)
                {
                    _io.WriteLine("Commander Skills:");
                    foreach (var s in entity.ExtraSkills) _io.WriteLine($" - {s}");
                }
                else
                {
                    _io.WriteLine("No Commander Skills.");
                }
            }
        }

        // --- 2. PERSONA STOCK DETAIL RENDERER (Updated UX) ---
        private void RenderPersonaStockDetail(Persona p, bool isEquipped)
        {
            _io.WriteLine($"=== PERSONA DETAILS {(isEquipped ? "[EQUIPPED]" : "")} ===");
            _io.WriteLine($"Name: {p.Name} (Lv.{p.Level}) | Arcana: {p.Arcana}");
            _io.WriteLine($"EXP: {p.Exp}/{p.ExpRequired}");
            _io.WriteLine("-----------------------------");

            // UX UPDATE: Removed "Raw Stats:" header, just showing stats directly
            var displayStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in displayStats)
            {
                int val = p.StatModifiers.ContainsKey(stat) ? p.StatModifiers[stat] : 0;
                _io.WriteLine($" {stat}: {val,3}");
            }
            _io.WriteLine("-----------------------------");

            _io.WriteLine("Equipped Skills:");
            if (p.SkillSet.Count == 0) _io.WriteLine(" (None)");
            foreach (var s in p.SkillSet) _io.WriteLine($" - {s}");

            var nextSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key).Take(3);
            if (nextSkills.Any())
            {
                _io.WriteLine("Next to Learn:");
                foreach (var ns in nextSkills) _io.WriteLine($" [Lv.{ns.Key}] {ns.Value}");
            }
            else { _io.WriteLine(" (Mastered)"); }
        }

        // --- 3. DEMON DETAIL RENDERER ---
        private void RenderDemonDetail(Combatant demon)
        {
            _io.WriteLine("=== DEMON DETAILS ===");
            _io.WriteLine($"Name: {demon.Name} (Lv.{demon.Level})");
            _io.WriteLine($"HP: {demon.CurrentHP}/{demon.MaxHP}  SP: {demon.CurrentSP}/{demon.MaxSP}");
            _io.WriteLine("-----------------------------");

            var stats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in stats)
            {
                int rawVal = demon.CharacterStats.ContainsKey(stat) ? demon.CharacterStats[stat] : 0;
                _io.WriteLine($"{stat,-4}: {rawVal,3}");
            }
            _io.WriteLine("-----------------------------");

            _io.WriteLine("Skills:");

            List<string> skills = demon.GetConsolidatedSkills();

            if (skills.Count > 0)
            {
                foreach (var s in skills) _io.WriteLine($" - {s}");
            }
            else
            {
                _io.WriteLine(" (None)");
            }

            if (demon.ActivePersona != null)
            {
                var nextSkills = demon.ActivePersona.SkillsToLearn.Where(k => k.Key > demon.Level).OrderBy(k => k.Key).Take(3);
                if (nextSkills.Any())
                {
                    _io.WriteLine("Next to Learn:");
                    foreach (var ns in nextSkills) _io.WriteLine($" [Lv.{ns.Key}] {ns.Value}");
                }
                else if (skills.Count > 0)
                {
                    _io.WriteLine(" (Mastered)");
                }
            }
        }

        // --- SEAMLESS STATUS SCREEN MENU ---
        private void OpenSeamlessStatusMenu()
        {
            while (true)
            {
                _io.Clear();
                RenderHumanStatus(_player);

                _io.WriteLine($"\nPoints Available: {_player.StatPoints}");
                _io.WriteLine("\n[A]llocate Stats  [E]quip  " +
                             (_player.Class == ClassType.WildCard ? "[P]ersona Stock" : "") +
                             (_player.Class == ClassType.Operator ? "[D]emon Stock" : "") +
                             "  [ESC] Back");

                var key = _io.ReadKey();
                if (key.Key == ConsoleKey.Escape) return;

                if (key.Key == ConsoleKey.A)
                {
                    if (_player.StatPoints > 0) OpenStatAllocation();
                    else { _io.WriteLine("No points available."); _io.Wait(500); }
                }
                else if (key.Key == ConsoleKey.E)
                {
                    ShowEquipSlotMenu();
                }
                else if (key.Key == ConsoleKey.P && _player.Class == ClassType.WildCard)
                {
                    OpenPersonaStockMenu();
                }
                else if (key.Key == ConsoleKey.D && _player.Class == ClassType.Operator)
                {
                    OpenDemonStockMenu();
                }
            }
        }

        private void OpenStatAllocation()
        {
            StatAllocationModule.OpenMenu(_player, _io);
        }

        // --- PERSONA STOCK MENU (Wild Card) ---
        private void OpenPersonaStockMenu()
        {
            var allPersonas = new List<Persona>();
            if (_player.ActivePersona != null) allPersonas.Add(_player.ActivePersona);
            allPersonas.AddRange(_player.PersonaStock);

            if (allPersonas.Count == 0)
            {
                _io.WriteLine("No Personas available.");
                _io.Wait(800);
                return;
            }

            while (true)
            {
                List<string> options = new List<string>();
                foreach (var p in allPersonas)
                {
                    string label = $"{p.Name} (Lv.{p.Level}) {p.Arcana}";
                    if (p == _player.ActivePersona) label += " [E]";
                    options.Add(label);
                }
                options.Add("Back");

                int idx = _io.RenderMenu("=== PERSONA STOCK ===", options, 0);

                if (idx == -1 || idx == options.Count - 1) return;

                ViewPersonaDetails(allPersonas[idx]);

                allPersonas.Clear();
                if (_player.ActivePersona != null) allPersonas.Add(_player.ActivePersona);
                allPersonas.AddRange(_player.PersonaStock);
            }
        }

        private void ViewPersonaDetails(Persona p)
        {
            bool isEquipped = (p == _player.ActivePersona);
            while (true)
            {
                _io.Clear();
                RenderPersonaStockDetail(p, isEquipped);
                _io.WriteLine("\n[E]quip  [ESC] Back");

                var key = _io.ReadKey();
                if (key.Key == ConsoleKey.Escape) return;

                if (key.Key == ConsoleKey.E)
                {
                    if (isEquipped)
                    {
                        _io.WriteLine("Already equipped.");
                        _io.Wait(500);
                    }
                    else
                    {
                        int stockIndex = _player.PersonaStock.IndexOf(p);
                        if (stockIndex != -1)
                        {
                            Persona oldActive = _player.ActivePersona;
                            _player.ActivePersona = p;
                            _player.PersonaStock[stockIndex] = oldActive;

                            _io.WriteLine($"Equipped {p.Name}!");
                            _player.RecalculateResources();
                            _io.Wait(800);
                            return;
                        }
                    }
                }
            }
        }

        // --- DEMON STOCK MENU (Operator) ---
        private void OpenDemonStockMenu()
        {
            var allDemons = new List<Combatant>();
            foreach (var member in _partyManager.ActiveParty)
            {
                if (member.Class == ClassType.Demon) allDemons.Add(member);
            }
            allDemons.AddRange(_player.DemonStock);

            if (allDemons.Count == 0)
            {
                _io.WriteLine("No demons found.");
                _io.Wait(800);
                return;
            }

            while (true)
            {
                List<string> options = new List<string>();
                foreach (var d in allDemons)
                {
                    string status = _partyManager.ActiveParty.Contains(d) ? "[PARTY]" : "[STOCK]";
                    options.Add($"{d.Name} (Lv.{d.Level}) {status}");
                }
                options.Add("Back");

                int idx = _io.RenderMenu("=== DEMON OVERVIEW ===", options, 0);

                if (idx == -1 || idx == options.Count - 1) return;

                ViewDemonDetails(allDemons[idx]);
            }
        }

        private void ViewDemonDetails(Combatant demon)
        {
            while (true)
            {
                _io.Clear();
                RenderDemonDetail(demon);
                _io.WriteLine("\n[ESC] Back");

                var key = _io.ReadKey();
                if (key.Key == ConsoleKey.Escape) return;
            }
        }

        // --- PARTY ORGANIZATION (Operator) ---
        private void OpenOrganizeMenu()
        {
            while (true)
            {
                _io.Clear();
                _io.WriteLine("=== ORGANIZE PARTY ===");
                _io.WriteLine("Active Party:");
                for (int i = 0; i < _partyManager.ActiveParty.Count; i++)
                {
                    _io.WriteLine($" {i + 1}. {_partyManager.ActiveParty[i].Name} (Lv.{_partyManager.ActiveParty[i].Level})");
                }

                _io.WriteLine("\n[S]ummon Demon  [R]eturn Demon  [ESC] Back");

                var key = _io.ReadKey();
                if (key.Key == ConsoleKey.Escape) return;

                if (key.Key == ConsoleKey.S)
                {
                    if (_player.DemonStock.Count == 0) { _io.WriteLine("No demons in stock."); _io.Wait(500); continue; }

                    List<string> demonOpts = _player.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList();
                    demonOpts.Add("Cancel");

                    int idx = _io.RenderMenu("SUMMON DEMON", demonOpts, 0);
                    if (idx == -1 || idx == demonOpts.Count - 1) continue;

                    if (_partyManager.SummonDemon(_player.DemonStock[idx]))
                    {
                        _io.WriteLine($"{_player.DemonStock[idx].Name} added to party.");
                        _player.DemonStock.RemoveAt(idx);
                        _io.Wait(500);
                    }
                    else
                    {
                        _io.WriteLine("Party is full.");
                        _io.Wait(500);
                    }
                }
                else if (key.Key == ConsoleKey.R)
                {
                    if (_partyManager.ActiveParty.Count <= 1) { _io.WriteLine("No demons to return."); _io.Wait(500); continue; }

                    List<string> activeOpts = new List<string>();
                    for (int i = 1; i < _partyManager.ActiveParty.Count; i++)
                    {
                        activeOpts.Add(_partyManager.ActiveParty[i].Name);
                    }
                    activeOpts.Add("Cancel");

                    int idx = _io.RenderMenu("RETURN DEMON", activeOpts, 0);
                    if (idx == -1 || idx == activeOpts.Count - 1) continue;

                    int realIndex = idx + 1; // +1 because we skipped player
                    Combatant demon = _partyManager.ActiveParty[realIndex];

                    _partyManager.ReturnDemon(demon);
                    _player.DemonStock.Add(demon);
                    _io.WriteLine($"{demon.Name} returned to stock.");
                    _io.Wait(500);
                }
            }
        }

        // --- DUNGEON ENTRY LOGIC ---
        private void PrepareDungeonEntry()
        {
            List<int> terminals = _dungeon.GetUnlockedTerminals();

            if (terminals.Count <= 1)
            {
                _dungeon.WarpToFloor(1);
                ExploreDungeon();
                return;
            }

            List<string> options = new List<string>();
            foreach (int t in terminals)
            {
                options.Add(t == 1 ? "Lobby (Entrance)" : $"Floor {t}");
            }
            options.Add("Cancel");

            int choice = _io.RenderMenu("=== SELECT ENTRY POINT ===", options, 0);

            if (choice == -1 || choice == options.Count - 1) return;

            int selectedFloor = terminals[choice];
            _dungeon.WarpToFloor(selectedFloor);
            ExploreDungeon();
        }

        // --- DUNGEON CRAWLER LOOP ---
        private void ExploreDungeon()
        {
            HandleNewFloor(_dungeon.ProcessCurrentFloor());

            while (_player.CurrentHP > 0)
            {
                DungeonFloorResult floorInfo = _dungeon.ProcessCurrentFloor();

                string header = $"=== TARTARUS: {floorInfo.BlockName.ToUpper()} ===\n" +
                                $"Floor: {floorInfo.FloorNumber}\n" +
                                $"Info: {floorInfo.Description}\n" +
                                $"HP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                List<string> options = new List<string>();
                List<Action> actions = new List<Action>();

                if (floorInfo.Type != DungeonEventType.BlockEnd)
                {
                    options.Add("Ascend Stairs");
                    actions.Add(() => {
                        _io.WriteLine("Ascending...");
                        _io.Wait(500);
                        _dungeon.Ascend();
                        HandleNewFloor(_dungeon.ProcessCurrentFloor());
                    });
                }
                else
                {
                    options.Add("Barrier (Cannot Pass)");
                    actions.Add(() => {
                        _io.WriteLine("The path is sealed.");
                        _io.Wait(1000);
                    });
                }

                if (floorInfo.FloorNumber > 1)
                {
                    options.Add("Descend Stairs");
                    actions.Add(() => {
                        _io.WriteLine("Descending...");
                        _io.Wait(500);
                        _dungeon.Descend();
                        HandleNewFloor(_dungeon.ProcessCurrentFloor());
                    });
                }

                if (floorInfo.FloorNumber == 1)
                {
                    options.Add("Clock (Heal)");
                    actions.Add(() => OpenHospitalMenu());

                    options.Add("Terminal (Warp)");
                    actions.Add(() => OpenTerminalMenu());

                    options.Add("Return to City");
                    actions.Add(() => ExitDungeon());
                }
                else if (floorInfo.HasTerminal)
                {
                    options.Add("Access Terminal (Return)");
                    actions.Add(() => OpenTerminalMenu());
                }

                options.Add("Inventory");
                actions.Add(() => OpenInventoryMenu(true));

                options.Add("Status");
                actions.Add(() => OpenSeamlessStatusMenu());

                if (_player.Class == ClassType.Operator)
                {
                    options.Add("Organize Party");
                    actions.Add(() => OpenOrganizeMenu());
                }

                int choice = _io.RenderMenu(header, options, _dungeonMenuIndex);

                if (choice == -1) continue;

                _dungeonMenuIndex = choice;
                actions[choice].Invoke();

                if (_dungeonState.CurrentFloor == 1 && choice == options.IndexOf("Return to City")) return;
            }
        }

        private void ExitDungeon()
        {
            _dungeonState.ResetToEntry();
        }

        private void OpenTerminalMenu()
        {
            List<int> floors = _dungeon.GetUnlockedTerminals();
            List<string> options = new List<string>();
            foreach (int f in floors)
            {
                options.Add(f == 1 ? "Lobby (Current)" : $"Floor {f}");
            }
            options.Add("Cancel");

            int choice = _io.RenderMenu("=== TERMINAL SYSTEM ===", options, 0);

            if (choice == -1 || choice == options.Count - 1) return;

            int targetFloor = floors[choice];
            if (targetFloor == _dungeonState.CurrentFloor) return;

            _io.WriteLine($"Warping to Floor {targetFloor}...");
            _io.Wait(1000);
            _dungeon.WarpToFloor(targetFloor);
            HandleNewFloor(_dungeon.ProcessCurrentFloor());
        }

        private void HandleNewFloor(DungeonFloorResult info)
        {
            switch (info.Type)
            {
                case DungeonEventType.Battle:
                    if (TriggerEncounter(info.EnemyIds, false)) { }
                    break;

                case DungeonEventType.Boss:
                    _io.WriteLine("!!! POWERFUL SHADOW DETECTED !!!", ConsoleColor.Red);
                    _io.Wait(1000);
                    string bossId = info.EnemyIds.FirstOrDefault();
                    if (TriggerEncounter(info.EnemyIds, true))
                    {
                        _io.WriteLine("The Guardian has been defeated!", ConsoleColor.Cyan);
                        _dungeon.RegisterBossDefeat(bossId);
                        _io.Wait(1500);
                    }
                    break;

                case DungeonEventType.SafeRoom:
                    if (info.FloorNumber != 1)
                    {
                        _io.WriteLine("The air here is calm.", ConsoleColor.Green);
                        _io.Wait(800);
                    }
                    break;
            }
        }

        private bool TriggerEncounter(List<string> enemyIds, bool isBoss)
        {
            List<Combatant> enemies = new List<Combatant>();

            foreach (string id in enemyIds)
            {
                if (Database.Enemies.TryGetValue(id, out var eData))
                {
                    enemies.Add(Combatant.CreateFromData(eData));
                }
                else
                {
                    _io.WriteLine($"[Error] Could not load enemy: {id}. Spawning Slime.");
                    if (Database.Enemies.TryGetValue("E_slime", out var fallback))
                        enemies.Add(Combatant.CreateFromData(fallback));
                    else
                        enemies.Add(new Combatant("Glitch Slime"));
                }
            }

            var groups = enemies.GroupBy(e => e.Name);
            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    int counter = 0;
                    foreach (var enemy in group)
                    {
                        enemy.Name += $" {(char)('A' + counter)}";
                        counter++;
                    }
                }
            }

            BattleManager battle = new BattleManager(_partyManager, enemies, _inventory, _economy, _io, isBoss);
            battle.StartBattle();

            if (battle.TraestoUsed)
            {
                _dungeon.WarpToFloor(1);
                return true;
            }

            return _player.CurrentHP > 0;
        }

        private void OpenCityMenu()
        {
            while (true)
            {
                string header = $"=== CITY SERVICES ===\nMacca: {_economy.Macca}";
                List<string> options = new List<string>
                {
                    "Blacksmith (Weapons)",
                    "Clothing Store (Armor/Boots)",
                    "Jeweler (Accessories)",
                    "Pharmacy (Items)",
                    "Hospital (Heal)"
                };

                int cityChoice = _io.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1) return;

                _cityMenuIndex = cityChoice;

                switch (cityChoice)
                {
                    case 0: _shop.OpenShop(_player, ShopType.Weapon); break;
                    case 1:
                        int clothingChoice = _io.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots" }, 0);
                        if (clothingChoice == 0) _shop.OpenShop(_player, ShopType.Armor);
                        else if (clothingChoice != -1) _shop.OpenShop(_player, ShopType.Boots);
                        break;
                    case 2: _shop.OpenShop(_player, ShopType.Accessory); break;
                    case 3: _shop.OpenShop(_player, ShopType.Item); break;
                    case 4: OpenHospitalMenu(); break;
                }
            }
        }

        private void OpenHospitalMenu()
        {
            while (true)
            {
                int hpMissing = _player.MaxHP - _player.CurrentHP;
                int spMissing = _player.MaxSP - _player.CurrentSP;

                int hpCost = hpMissing * 1;
                int spCost = spMissing * 5;
                int totalCost = hpCost + spCost;

                string header = $"=== HOSPITAL / CLOCK ===\n" +
                                $"Current Macca: {_economy.Macca}\n" +
                                $"HP: {_player.CurrentHP}/{_player.MaxHP}\n" +
                                $"SP: {_player.CurrentSP}/{_player.MaxSP}";

                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                if (totalCost > 0)
                {
                    options.Add($"Treat Wounds ({totalCost} M)");
                    disabled.Add(false);
                }
                else
                {
                    options.Add("You are perfectly healthy.");
                    disabled.Add(true);
                }
                options.Add("Leave");
                disabled.Add(false);

                int choice = _io.RenderMenu(header, options, 0, disabled);

                if (choice == -1 || choice == options.Count - 1) return;

                if (choice == 0 && totalCost > 0)
                {
                    if (_economy.SpendMacca(totalCost))
                    {
                        _player.CurrentHP = _player.MaxHP;
                        _player.CurrentSP = _player.MaxSP;
                        _io.WriteLine("Treatment complete. Take care.", ConsoleColor.Green);
                        _io.Wait(1000);
                    }
                    else
                    {
                        _io.WriteLine("You don't have enough money.", ConsoleColor.Red);
                        _io.Wait(1000);
                    }
                }
            }
        }

        private void OpenInventoryMenu(bool inDungeon)
        {
            while (true)
            {
                string header = "=== INVENTORY ===";
                List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment" };

                if (_player.Class == ClassType.WildCard || _player.Class == ClassType.PersonaUser) options.Add("Personas");
                if (_player.Class == ClassType.Operator) options.Add("Demons (COMP)");

                int invChoice = _io.RenderMenu(header, options, _inventoryMenuIndex);

                if (invChoice == -1) return;
                _inventoryMenuIndex = invChoice;

                if (invChoice == 0) ShowItemMenu(inDungeon);
                else if (invChoice == 1) ShowSkillMenu();
                else if (invChoice == 2) ShowEquipSlotMenu();
                else if (invChoice == 3)
                {
                    if (_player.Class == ClassType.WildCard || _player.Class == ClassType.PersonaUser) OpenPersonaStockMenu();
                    if (_player.Class == ClassType.Operator) OpenDemonStockMenu();
                }

                if (inDungeon && _dungeonState.CurrentFloor == 1) return;
            }
        }

        private void ShowItemMenu(bool inDungeon)
        {
            while (true)
            {
                var items = Database.Items.Values.Where(itm => _inventory.GetQuantity(itm.Id) > 0).ToList();
                if (items.Count == 0)
                {
                    _io.WriteLine("Inventory is empty.");
                    _io.Wait(800);
                    return;
                }

                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var item in items)
                {
                    string label = $"{item.Name} x{_inventory.GetQuantity(item.Id)}";
                    bool isDisabled = false;

                    if (item.Name == "Traesto Gem")
                    {
                        label += " (Battle Only)";
                        isDisabled = true;
                    }

                    if (item.Name == "Goho-M" && !inDungeon)
                    {
                        label += " (Dungeon Only)";
                        isDisabled = true;
                    }

                    options.Add(label);
                    disabled.Add(isDisabled);
                }

                if (_itemMenuIndex >= options.Count) _itemMenuIndex = 0;

                int idx = _io.RenderMenu("=== USE ITEM ===", options, _itemMenuIndex, disabled, (index) =>
                {
                    _io.WriteLine($"Effect: {items[index].Description}");
                });

                if (idx == -1) return;
                _itemMenuIndex = idx;

                UseItemField(items[idx].Id, _player, inDungeon);

                if (inDungeon && _dungeonState.CurrentFloor == 1) return;
            }
        }

        private void UseItemField(string itemId, Combatant target, bool inDungeon)
        {
            if (!_inventory.HasItem(itemId)) return;
            ItemData item = Database.Items[itemId];
            bool used = false;

            if (item.Name == "Goho-M")
            {
                if (!inDungeon)
                {
                    _io.WriteLine("You are not in a Dungeon.");
                    _io.Wait(800);
                    return;
                }

                _io.WriteLine("Using Goho-M... Returning to Lobby.");
                _io.Wait(1000);
                _inventory.RemoveItem(itemId, 1);
                ExitDungeon();
                return;
            }

            if (item.Name == "Traesto Gem")
            {
                _io.WriteLine("Can only be used in battle.");
                _io.Wait(800);
                return;
            }

            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    if (target.CurrentHP >= target.MaxHP) _io.WriteLine("HP is full.");
                    else
                    {
                        int old = target.CurrentHP;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                        _io.WriteLine($"Restored {target.CurrentHP - old} HP.");
                        used = true;
                    }
                    break;
                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP) _io.WriteLine("SP is full.");
                    else
                    {
                        int old = target.CurrentSP;
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        _io.WriteLine($"Restored {target.CurrentSP - old} SP.");
                        used = true;
                    }
                    break;
                case "Cure":
                    if (target.CurrentAilment == null) _io.WriteLine("Healthy.");
                    else
                    {
                        bool cured = false;
                        if (item.Name == "Dis-Poison" && target.CurrentAilment.Name == "Poison") cured = true;
                        else if (item.Name == "Patra Card" && target.CheckCure("Cure All")) cured = true;

                        if (cured) { _io.WriteLine($"Cured {target.CurrentAilment?.Name}!"); target.RemoveAilment(); used = true; }
                        else _io.WriteLine("No effect.");
                    }
                    break;
                default: _io.WriteLine("Cannot use here."); break;
            }

            if (used) { _inventory.RemoveItem(itemId, 1); _io.Wait(800); }
            else _io.Wait(800);
        }

        private void ShowSkillMenu()
        {
            var skills = _player.GetConsolidatedSkills();
            var usableSkills = new List<string>();
            foreach (var skillName in skills)
            {
                if (Database.Skills.TryGetValue(skillName, out var d))
                    if (d.Category != null && d.Effect != null)
                        if (d.Category.Contains("Recovery") || d.Effect.Contains("restore") || d.Effect.Contains("Cure"))
                            usableSkills.Add(skillName);
            }

            if (usableSkills.Count == 0)
            {
                _io.WriteLine("No usable skills.");
                _io.Wait(800);
                return;
            }

            List<string> options = new List<string>();
            foreach (var skillName in usableSkills)
            {
                var d = Database.Skills[skillName];
                options.Add($"{skillName} ({d.Cost})");
            }

            if (_skillMenuIndex >= options.Count) _skillMenuIndex = 0;

            int idx = _io.RenderMenu("=== FIELD SKILLS ===", options, _skillMenuIndex, null, (index) =>
            {
                _io.WriteLine($"Effect: {Database.Skills[usableSkills[index]].Effect}");
            });

            if (idx == -1) return;
            _skillMenuIndex = idx;

            UseSkillField(Database.Skills[usableSkills[idx]], _player, _player);
        }

        private void UseSkillField(SkillData skill, Combatant user, Combatant target)
        {
            var cost = skill.ParseCost();
            if ((cost.isHP && user.CurrentHP <= cost.value) || (!cost.isHP && user.CurrentSP < cost.value))
            {
                _io.WriteLine("Not enough resources."); _io.Wait(800); return;
            }

            bool effectApplied = false;
            if (skill.Effect != null && skill.Effect.Contains("restores", StringComparison.OrdinalIgnoreCase))
            {
                if (target.CurrentHP >= target.MaxHP) { _io.WriteLine("HP full."); }
                else
                {
                    int healAmount = 50;
                    var match = System.Text.RegularExpressions.Regex.Match(skill.Effect, @"\((\d+)\)");
                    if (match.Success) int.TryParse(match.Groups[1].Value, out healAmount);

                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                    _io.WriteLine($"Restored HP.");
                    effectApplied = true;
                }
            }
            else if (skill.Effect != null && skill.Effect.Contains("Cure") && target.CurrentAilment != null)
            {
                if (target.CheckCure(skill.Effect)) { _io.WriteLine("Cured."); effectApplied = true; }
            }

            if (effectApplied)
            {
                if (cost.isHP) user.CurrentHP -= cost.value; else user.CurrentSP -= cost.value;
                _io.Wait(800);
            }
            else _io.Wait(800);
        }

        private void ShowEquipSlotMenu()
        {
            while (true)
            {
                string header = "=== EQUIPMENT SLOTS ===";
                List<string> options = new List<string>
                {
                    $"Weapon: {_player.EquippedWeapon?.Name ?? "None"}",
                    $"Armor: {_player.EquippedArmor?.Name ?? "None"}",
                    $"Boots: {_player.EquippedBoots?.Name ?? "None"}",
                    $"Accessory:{_player.EquippedAccessory?.Name ?? "None"}"
                };

                int slotChoice = _io.RenderMenu(header, options, _equipSlotIndex);
                if (slotChoice == -1) return;
                _equipSlotIndex = slotChoice;

                if (slotChoice == 0) ShowEquipSelectionList(ShopCategory.Weapon);
                else if (slotChoice == 1) ShowEquipSelectionList(ShopCategory.Armor);
                else if (slotChoice == 2) ShowEquipSelectionList(ShopCategory.Boots);
                else if (slotChoice == 3) ShowEquipSelectionList(ShopCategory.Accessory);
            }
        }

        private void ShowEquipSelectionList(ShopCategory category)
        {
            List<string> ids = category switch
            {
                ShopCategory.Weapon => _inventory.OwnedWeapons,
                ShopCategory.Armor => _inventory.OwnedArmor,
                ShopCategory.Boots => _inventory.OwnedBoots,
                ShopCategory.Accessory => _inventory.OwnedAccessories,
                _ => new List<string>()
            };

            if (ids.Count == 0)
            {
                _io.WriteLine("No equipment in this category.");
                _io.Wait(800);
                return;
            }

            int listIndex = 0;
            while (true)
            {
                List<string> names = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var id in ids)
                {
                    string name = "Unknown";
                    bool isEquipped = false;

                    if (category == ShopCategory.Weapon) { name = Database.Weapons[id].Name; isEquipped = _player.EquippedWeapon?.Id == id; }
                    if (category == ShopCategory.Armor) { name = Database.Armors[id].Name; isEquipped = _player.EquippedArmor?.Id == id; }
                    if (category == ShopCategory.Boots) { name = Database.Boots[id].Name; isEquipped = _player.EquippedBoots?.Id == id; }
                    if (category == ShopCategory.Accessory) { name = Database.Accessories[id].Name; isEquipped = _player.EquippedAccessory?.Id == id; }

                    names.Add($"{name}{(isEquipped ? " [E]" : "")}");
                    disabled.Add(isEquipped);
                }

                if (listIndex >= names.Count) listIndex = 0;

                int choice = _io.RenderMenu($"=== EQUIP {category.ToString().ToUpper()} ===", names, listIndex, disabled, (index) =>
                {
                    string id = ids[index];
                    if (category == ShopCategory.Weapon) _io.WriteLine($"Pow: {Database.Weapons[id].Power} Acc: {Database.Weapons[id].Accuracy}");
                    if (category == ShopCategory.Armor) _io.WriteLine($"Def: {Database.Armors[id].Defense} Eva: {Database.Armors[id].Evasion}");
                    if (category == ShopCategory.Boots) _io.WriteLine($"Eva: {Database.Boots[id].Evasion}");
                    if (category == ShopCategory.Accessory) _io.WriteLine($"Effect: {Database.Accessories[id].ModifierStat} +{Database.Accessories[id].ModifierValue}");
                });

                if (choice == -1) return;
                listIndex = choice;

                string selectedId = ids[choice];
                if (category == ShopCategory.Weapon) _player.EquippedWeapon = Database.Weapons[selectedId];
                if (category == ShopCategory.Armor) _player.EquippedArmor = Database.Armors[selectedId];
                if (category == ShopCategory.Boots) _player.EquippedBoots = Database.Boots[selectedId];
                if (category == ShopCategory.Accessory) _player.EquippedAccessory = Database.Accessories[selectedId];

                _io.WriteLine("Equipped!");
                _io.Wait(500);
            }
        }
    }
}