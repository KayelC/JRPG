using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Services;

namespace JRPGPrototype
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

        // Persistent Cursors
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
                // Main Hub Header
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
                    case 2: OpenInventoryMenu(false); break; // false = not in dungeon
                    case 3: OpenStatusMenu(); break;
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

            // If only Lobby is unlocked, just go to first floor
            if (terminals.Count <= 1)
            {
                _dungeon.WarpToFloor(1); // Warp to Lobby (Floor 1)
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
            // No need to map 1->2 anymore, we have a real lobby at Floor 1 now.

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

                // 1. Navigation
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

                // Descend Logic
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

                // 2. Special Interactive Objects (Lobby / Terminal)
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
                    actions.Add(() => {
                        _io.WriteLine("Teleporting to Lobby...");
                        _io.Wait(1000);
                        _dungeon.WarpToFloor(1);
                    });
                }

                // 3. Menus
                options.Add("Inventory");
                actions.Add(() => OpenInventoryMenu(true));

                options.Add("Status");
                actions.Add(() => OpenStatusMenu());

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
                    if (TriggerEncounter(info.EnemyId, false)) { }
                    break;

                case DungeonEventType.Boss:
                    _io.WriteLine("!!! POWERFUL SHADOW DETECTED !!!", ConsoleColor.Red);
                    _io.Wait(1000);
                    if (TriggerEncounter(info.EnemyId, true))
                    {
                        _io.WriteLine("The Guardian has been defeated!", ConsoleColor.Cyan);
                        _dungeon.RegisterBossDefeat(info.EnemyId);
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

        private bool TriggerEncounter(string enemyId, bool isBoss)
        {
            Combatant enemy;
            if (Database.Enemies.TryGetValue(enemyId, out var eData))
            {
                enemy = Combatant.CreateFromData(eData);
            }
            else
            {
                _io.WriteLine($"[Error] Could not load enemy: {enemyId}. Spawning Slime.");
                if (Database.Enemies.TryGetValue("E_slime", out var fallback))
                    enemy = Combatant.CreateFromData(fallback);
                else
                    enemy = new Combatant("Glitch Slime");
            }

            BattleManager battle = new BattleManager(_player, enemy, _inventory, _economy, _io);
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

                int invChoice = _io.RenderMenu(header, options, _inventoryMenuIndex);

                if (invChoice == -1) return;
                _inventoryMenuIndex = invChoice;

                switch (invChoice)
                {
                    case 0: ShowItemMenu(inDungeon); break;
                    case 1: ShowSkillMenu(); break;
                    case 2: ShowEquipSlotMenu(); break;
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

                    // --- UX UPDATE: Gray out unusable items ---

                    // Traesto Gem: Battle Only
                    if (item.Name == "Traesto Gem")
                    {
                        label += " (Battle Only)";
                        isDisabled = true;
                    }

                    // Goho-M: Dungeon Only
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

        private void OpenStatusMenu()
        {
            while (true)
            {
                string header = $"=== STATUS & GROWTH ===\nName: {_player.Name} (Lv.{_player.Level})";
                List<string> options = new List<string> { "Operator Sheet", "Persona Sheet" };

                bool canAllocate = _player.StatPoints > 0;
                if (canAllocate)
                {
                    options.Add($"Allocate Stats ({_player.StatPoints} pts)");
                }

                int statusChoice = _io.RenderMenu(header, options, _statusMenuIndex);

                if (statusChoice == -1) return;
                _statusMenuIndex = statusChoice;

                if (statusChoice == 0) ShowOperatorStatus();
                else if (statusChoice == 1) ShowPersonaStatus();
                else if (statusChoice == 2 && canAllocate)
                {
                    StatAllocationModule.OpenMenu(_player, _io);
                }
            }
        }

        private void ShowOperatorStatus()
        {
            _io.Clear();
            _io.WriteLine("\n=== OPERATOR STATUS ===");
            _io.WriteLine($"Name: {_player.Name} (Lv.{_player.Level})");
            _io.WriteLine($"Class: Operator");
            _io.WriteLine($"EXP: {_player.Exp}/{_player.ExpRequired}");
            _io.WriteLine($"HP: {_player.CurrentHP}/{_player.MaxHP}");
            _io.WriteLine($"SP: {_player.CurrentSP}/{_player.MaxSP}");
            _io.WriteLine($"Cond: {(_player.CurrentAilment?.Name ?? "Healthy")}");
            _io.WriteLine($"Def: {_player.GetDefense()} | Eva: {_player.GetEvasion()}");

            _io.WriteLine("\n--- EQUIPMENT ---");
            _io.WriteLine($"Weapon: {_player.EquippedWeapon?.Name ?? "None"}");
            _io.WriteLine($"Armor: {_player.EquippedArmor?.Name ?? "None"}");
            _io.WriteLine($"Boots: {_player.EquippedBoots?.Name ?? "None"}");
            _io.WriteLine($"Acc: {_player.EquippedAccessory?.Name ?? "None"}");

            _io.WriteLine("\n--- STATS BREAKDOWN (Base + Persona) ---");
            string personaName = _player.ActivePersona?.Name ?? "None";
            _io.WriteLine($"Persona: {personaName}");
            _io.WriteLine("             [Total]   [Base]");

            _io.WriteLine($"STR: {_player.GetStat(StatType.STR),-5} (Base: {_player.CharacterStats[StatType.STR]})");
            _io.WriteLine($"MAG: {_player.GetStat(StatType.MAG),-5} (Base: {_player.CharacterStats[StatType.MAG]})");
            _io.WriteLine($"END: {_player.GetStat(StatType.END),-5} (Base: {_player.CharacterStats[StatType.END]})");
            _io.WriteLine($"AGI: {_player.GetStat(StatType.AGI),-5} (Base: {_player.CharacterStats[StatType.AGI]})");
            _io.WriteLine($"LUK: {_player.GetStat(StatType.LUK),-5} (Base: {_player.CharacterStats[StatType.LUK]})");

            _io.WriteLine("\n--- OPERATOR EXCLUSIVE ---");
            _io.WriteLine($"INT: {_player.CharacterStats[StatType.INT]} (Determines MaxSP)");
            _io.WriteLine($"CHA: {_player.CharacterStats[StatType.CHA]} (Negotiation & Shop Discount)");
            _io.WriteLine("\n[ESC] Back");
            _io.ReadKey();
        }

        private void ShowPersonaStatus()
        {
            _io.Clear();
            Persona p = _player.ActivePersona;

            if (p == null)
            {
                _io.WriteLine("\nNo Active Persona equipped.");
                _io.ReadKey();
                return;
            }

            _io.WriteLine($"\n=== PERSONA SHEET: {p.Name.ToUpper()} ===");
            _io.WriteLine($"Level: {p.Level}");
            _io.WriteLine($"Arcana: {p.Arcana}");
            _io.WriteLine($"EXP: {p.Exp}/{p.ExpRequired}");

            _io.WriteLine("\n--- RAW STATS ---");
            var displayStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in displayStats)
            {
                int val = p.StatModifiers.ContainsKey(stat) ? p.StatModifiers[stat] : 0;
                _io.WriteLine($"{stat,-4}: {val,3}");
            }

            _io.WriteLine("\n--- EQUIPPED SKILLS ---");
            if (p.SkillSet.Count == 0) _io.WriteLine("(None)");
            foreach (var skill in p.SkillSet)
            {
                string details = "";
                if (Database.Skills.TryGetValue(skill, out var sData))
                {
                    details = $"| {sData.Cost,-5} | {sData.Effect}";
                }
                _io.WriteLine($"- {skill,-15} {details}");
            }

            _io.WriteLine("\n--- POTENTIAL (Future Skills) ---");
            var futureSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key).ToList();

            if (futureSkills.Any())
            {
                foreach (var kvp in futureSkills)
                {
                    _io.WriteLine($"Lv.{kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                _io.WriteLine("(Mastered)");
            }

            _io.WriteLine("\n[ESC] Back");
            _io.ReadKey();
        }
    }
}