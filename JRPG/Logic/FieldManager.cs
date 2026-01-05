using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

                int choice = _io.RenderMenu(header, options, _mainMenuIndex);

                if (choice == -1) continue;

                _mainMenuIndex = choice;

                switch (choice)
                {
                    case 0: PrepareDungeonEntry(); break;
                    case 1: OpenCityMenu(); break;
                    case 2: OpenInventoryMenu(false); break;
                    case 3: OpenSeamlessStatusMenu(); break;
                    case 4:
                        _io.WriteLine("The nose is long... but this feature isn't ready yet.");
                        _io.Wait(1000);
                        break;
                }

                if (_player.CurrentHP <= 0) return;
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

                // Navigation
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

                // Special Objects
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

                // Menus
                options.Add("Inventory");
                actions.Add(() => OpenInventoryMenu(true));

                options.Add("Status");
                actions.Add(() => OpenSeamlessStatusMenu());

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
                    // Pass the first Enemy ID as the Boss ID for tracking
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

            // Suffix Logic
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

            PartyManager party = new PartyManager(_player);
            BattleManager battle = new BattleManager(party, enemies, _inventory, _economy, _io, isBoss);
            battle.StartBattle();

            if (battle.TraestoUsed)
            {
                _dungeon.WarpToFloor(1);
                return true;
            }

            return _player.CurrentHP > 0;
        }

        // --- CITY LOGIC ---
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

        // --- SEAMLESS STATUS & STOCK MANAGEMENT ---

        private void OpenSeamlessStatusMenu()
        {
            while (true)
            {
                _io.Clear();
                _io.WriteLine("=== STATUS & PARAMETERS ===");
                _io.WriteLine($"Name: {_player.Name} (Lv.{_player.Level}) | Class: {_player.Class}");
                _io.WriteLine($"HP: {_player.CurrentHP}/{_player.MaxHP}  SP: {_player.CurrentSP}/{_player.MaxSP}");
                _io.WriteLine($"EXP: {_player.Exp}/{_player.ExpRequired}  Next: {_player.ExpRequired - _player.Exp}");
                _io.WriteLine("-----------------------------");

                var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>();
                foreach (var stat in stats)
                {
                    int total = _player.GetStat(stat);
                    int baseVal = _player.CharacterStats[stat];
                    string color = total > baseVal ? "+" : "";
                    _io.WriteLine($"{stat,-4}: {total,3} ({baseVal,3}{color})");
                }
                _io.WriteLine($"Points Available: {_player.StatPoints}");
                _io.WriteLine("-----------------------------");

                if (_player.Class == ClassType.PersonaUser || _player.Class == ClassType.WildCard)
                {
                    RenderPersonaDetails();
                }
                else if (_player.Class == ClassType.Operator)
                {
                    RenderDemonStock();
                }

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

        private void RenderPersonaDetails()
        {
            if (_player.ActivePersona == null)
            {
                _io.WriteLine("No Persona Equipped.");
                return;
            }
            var p = _player.ActivePersona;
            _io.WriteLine($"[ACTIVE PERSONA]: {p.Name} (Lv.{p.Level}) Arcana: {p.Arcana}");
            _io.WriteLine("Skills:");
            foreach (var s in p.SkillSet) _io.WriteLine($" - {s}");
        }

        private void RenderDemonStock()
        {
            _io.WriteLine($"[COMP STOCK]: {_player.DemonStock.Count} Demons");
            int count = 0;
            foreach (var d in _player.DemonStock.Take(3))
            {
                _io.WriteLine($" {++count}. {d.Name} (Lv.{d.Level}) HP:{d.CurrentHP}/{d.MaxHP}");
            }
            if (_player.DemonStock.Count > 3) _io.WriteLine(" ...");
        }

        private void OpenStatAllocation()
        {
            StatAllocationModule.OpenMenu(_player, _io);
        }

        private void OpenPersonaStockMenu()
        {
            if (_player.PersonaStock.Count == 0)
            {
                _io.WriteLine("No other Personas in stock.");
                _io.Wait(800);
                return;
            }

            while (true)
            {
                List<string> options = new List<string>();
                foreach (var p in _player.PersonaStock)
                {
                    options.Add($"{p.Name} (Lv.{p.Level}) {p.Arcana}");
                }
                options.Add("Cancel");

                int idx = _io.RenderMenu("=== SWITCH PERSONA ===", options, 0);

                if (idx == -1 || idx == options.Count - 1) return;

                // Perform Swap
                Persona selected = _player.PersonaStock[idx];
                Persona current = _player.ActivePersona;

                _player.ActivePersona = selected;
                _player.PersonaStock[idx] = current; // Swap current back into stock slot

                _io.WriteLine($"Switched to {selected.Name}!");
                _player.RecalculateResources(); // Re-calc stats based on new Persona
                _io.Wait(800);
                return; // Exit menu after swap
            }
        }

        private void OpenDemonStockMenu()
        {
            if (_player.DemonStock.Count == 0)
            {
                _io.WriteLine("COMP is empty.");
                _io.Wait(800);
                return;
            }

            while (true)
            {
                List<string> options = new List<string>();
                foreach (var d in _player.DemonStock)
                {
                    options.Add($"{d.Name} (Lv.{d.Level}) HP:{d.CurrentHP}/{d.MaxHP}");
                }
                options.Add("Back");

                int idx = _io.RenderMenu("=== DEMON STOCK ===", options, 0);

                if (idx == -1 || idx == options.Count - 1) return;

                // View Details of Selected Demon
                Combatant demon = _player.DemonStock[idx];
                _io.Clear();
                _io.WriteLine($"=== {demon.Name} ===");
                _io.WriteLine($"Level: {demon.Level} | Race: {demon.Class}"); // Usually Demon class
                _io.WriteLine($"HP: {demon.CurrentHP}/{demon.MaxHP}  SP: {demon.CurrentSP}/{demon.MaxSP}");
                _io.WriteLine("\nStats:");
                foreach (StatType s in Enum.GetValues(typeof(StatType)))
                {
                    if (s != StatType.INT && s != StatType.CHA) // Demons don't use Operator stats
                        _io.WriteLine($" {s}: {demon.CharacterStats[s]}");
                }
                _io.WriteLine("\nSkills:");
                if (demon.ActivePersona != null) // Demons store skills in their "Persona" shell for now
                {
                    foreach (var s in demon.ActivePersona.SkillSet) _io.WriteLine($" - {s}");
                }
                else
                {
                    _io.WriteLine(" (None)");
                }

                _io.WriteLine("\n[Press Any Key]");
                _io.ReadKey();
            }
        }

        // --- INVENTORY UPDATES ---
        private void OpenInventoryMenu(bool inDungeon)
        {
            while (true)
            {
                string header = "=== INVENTORY ===";
                List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment" };

                // Add Class Specific Tab
                if (_player.Class == ClassType.WildCard) options.Add("Personas");
                if (_player.Class == ClassType.Operator) options.Add("Demons (COMP)");

                int invChoice = _io.RenderMenu(header, options, _inventoryMenuIndex);
                if (invChoice == -1) return;
                _inventoryMenuIndex = invChoice;

                if (invChoice == 0) ShowItemMenu(inDungeon);
                else if (invChoice == 1) ShowSkillMenu();
                else if (invChoice == 2) ShowEquipSlotMenu();
                else if (invChoice == 3)
                {
                    if (_player.Class == ClassType.WildCard) OpenPersonaStockMenu();
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

            bool used = false;
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
            while (true)
            {
                var skills = _player.ActivePersona.SkillSet;
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