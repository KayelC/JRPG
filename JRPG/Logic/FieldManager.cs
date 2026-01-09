using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        // Persistent Knowledge Bank for the player across encounters
        private BattleKnowledge _playerKnowledge = new BattleKnowledge();

        // Menu Indices to preserve cursor position (UX Fidelity)
        private int _mainMenuIndex = 0;
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
                    "Status"
                };

                if (_player.Class == ClassType.Operator) options.Add("Organize Party");
                options.Add("Exit Game");

                int choice = _io.RenderMenu(header, options, _mainMenuIndex);

                if (choice == -1) continue;

                _mainMenuIndex = choice;
                string selectedOption = options[choice];

                if (selectedOption == "Explore Tartarus") PrepareDungeonEntry();
                else if (selectedOption == "City Services") OpenCityMenu();
                else if (selectedOption == "Inventory") OpenInventoryMenu(false);
                else if (selectedOption == "Status") OpenSeamlessStatusMenu();
                else if (selectedOption == "Organize Party") OpenOrganizeMenu();
          
                else if (selectedOption == "Exit Game") return;

                if (_player.CurrentHP <= 0) return;
            }
        }

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
                    actions.Add(() =>
                    {
                        _io.WriteLine("Ascending...");
                        _io.Wait(500);
                        _dungeon.Ascend();
                        HandleNewFloor(_dungeon.ProcessCurrentFloor());
                    });
                }
                else
                {
                    options.Add("Barrier (Cannot Pass)");
                    actions.Add(() =>
                    {
                        _io.WriteLine("The path is sealed.");
                        _io.Wait(1000);
                    });
                }

                if (floorInfo.FloorNumber > 1)
                {
                    options.Add("Descend Stairs");
                    actions.Add(() =>
                    {
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

        private void HandleNewFloor(DungeonFloorResult info)
        {
            switch (info.Type)
            {
                case DungeonEventType.Battle:
                    TriggerEncounter(info.EnemyIds, false);
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
                    enemies.Add(Combatant.CreateFromData(eData));
            }

            var groups = enemies.GroupBy(e => e.Name);
            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    int counter = 0;
                    foreach (var enemy in group) { enemy.Name += $" {(char)('A' + counter)}"; counter++; }
                }
            }

            BattleConductor battle = new BattleConductor(_partyManager, enemies, _inventory, _economy, _io, _playerKnowledge, isBoss);
            battle.StartBattle();

            return _player.CurrentHP > 0;
        }

        private void OpenTerminalMenu()
        {
            List<int> floors = _dungeon.GetUnlockedTerminals();
            List<string> options = floors.Select(f => f == 1 ? "Lobby (Current)" : $"Floor {f}").ToList();
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
                    "Hospital (Heal)",
                    "Back"
                };

                int cityChoice = _io.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1 || cityChoice == options.Count - 1) return;

                _cityMenuIndex = cityChoice;

                switch (cityChoice)
                {
                    case 0: _shop.OpenShop(_player, ShopType.Weapon); break;
                    case 1:
                        int clothingChoice = _io.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots", "Back" }, 0);
                        if (clothingChoice == 0) _shop.OpenShop(_player, ShopType.Armor);
                        else if (clothingChoice == 1) _shop.OpenShop(_player, ShopType.Boots);
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
                int totalCost = (hpMissing * 1) + (spMissing * 5);

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
                        _io.WriteLine("Treatment complete!", ConsoleColor.Green);
                        _io.Wait(1000);
                    }
                    else
                    {
                        _io.WriteLine("Not enough money!", ConsoleColor.Red);
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

                if (_player.Class == ClassType.Operator) options.Add("Demons (COMP)");

                int invChoice = _io.RenderMenu(header, options, _inventoryMenuIndex);

                if (invChoice == -1) return;
                _inventoryMenuIndex = invChoice;

                if (invChoice == 0) ShowItemMenu(inDungeon);
                else if (invChoice == 1) ShowSkillMenu();
                else if (invChoice == 2) ShowEquipSlotMenu();
                else if (invChoice == 3) OpenDemonStockMenu();

                if (inDungeon && _dungeonState.CurrentFloor == 1) return;
            }
        }

        private void ShowItemMenu(bool inDungeon)
        {
            var humanoidParty = _partyManager.ActiveParty.Where(c => c.Class != ClassType.Demon).ToList();
            List<string> userLabels = humanoidParty.Select(c => c.Name).ToList();
            userLabels.Add("Back");

            int userIdx = _io.RenderMenu("Who is using the item?", userLabels, 0);
            if (userIdx == -1 || userIdx == userLabels.Count - 1) return;
            Combatant itemUser = humanoidParty[userIdx];

            while (true)
            {
                var items = Database.Items.Values.Where(itm => _inventory.GetQuantity(itm.Id) > 0).ToList();
                if (items.Count == 0)
                {
                    _io.WriteLine("No usable items remaining.");
                    _io.Wait(800);
                    return;
                }

                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var item in items)
                {
                    string label = $"{item.Name} x{_inventory.GetQuantity(item.Id)}";
                    bool isDisabled = false;

                    if (item.Name == "Traesto Gem") { label += " (Battle Only)"; isDisabled = true; }
                    if (item.Name == "Goho-M" && !inDungeon) { label += " (Dungeon Only)"; isDisabled = true; }

                    options.Add(label);
                    disabled.Add(isDisabled);
                }
                options.Add("Back");
                disabled.Add(false);

                if (_itemMenuIndex >= options.Count) _itemMenuIndex = 0;

                int itemIdx = _io.RenderMenu($"User: {itemUser.Name} | Select Item:", options, _itemMenuIndex, disabled, (index) =>
                {
                    if (index >= 0 && index < items.Count)
                    {
                        _io.WriteLine($"Description: {items[index].Description}");
                    }
                });

                if (itemIdx == -1 || itemIdx == options.Count - 1) return;
                _itemMenuIndex = itemIdx;

                ItemData selectedItem = items[itemIdx];

                if (selectedItem.Name == "Goho-M")
                {
                    UseItemField(selectedItem.Id, itemUser, itemUser, inDungeon);
                    return;
                }

                var targetPool = _partyManager.ActiveParty.ToList();
                List<string> targetLabels = targetPool.Select(c => $"{c.Name} (HP: {c.CurrentHP}/{c.MaxHP})").ToList();
                targetLabels.Add("Back");

                int targetIdx = _io.RenderMenu($"Using {selectedItem.Name}. Select Target:", targetLabels, 0);
                if (targetIdx == -1 || targetIdx == targetLabels.Count - 1) continue;

                UseItemField(selectedItem.Id, itemUser, targetPool[targetIdx], inDungeon);
            }
        }

        private void UseItemField(string itemId, Combatant user, Combatant target, bool inDungeon)
        {
            if (!_inventory.HasItem(itemId)) return;
            ItemData item = Database.Items[itemId];
            bool used = false;

            if (item.Name == "Goho-M")
            {
                _io.WriteLine("Using Goho-M... Returning to Lobby.");
                _io.Wait(1000);
                _inventory.RemoveItem(itemId, 1);
                ExitDungeon();
                return;
            }

            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    if (target.CurrentHP >= target.MaxHP) _io.WriteLine($"{target.Name}'s HP is already full.");
                    else
                    {
                        int healAmount = item.EffectValue >= 9999 ? target.MaxHP : item.EffectValue;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                        _io.WriteLine($"{target.Name} recovered health.");
                        used = true;
                    }
                    break;
                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP) _io.WriteLine($"{target.Name}'s SP is already full.");
                    else
                    {
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        _io.WriteLine($"{target.Name} recovered SP.");
                        used = true;
                    }
                    break;
                case "Cure":
                    StatusRegistry sr = new StatusRegistry();
                    if (sr.CheckAndExecuteCure(target, item.Name))
                    {
                        _io.WriteLine($"{target.Name} was cured!");
                        used = true;
                    }
                    else _io.WriteLine("The item had no effect.");
                    break;
            }

            if (used)
            {
                _inventory.RemoveItem(itemId, 1);
                _io.Wait(800);
            }
            else _io.Wait(800);
        }

        private void ShowSkillMenu()
        {
            while (true)
            {
                List<Combatant> candidates = new List<Combatant>();
                if (_player.Class != ClassType.Demon) candidates.Add(_player);

                if (_player.Class == ClassType.Operator)
                {
                    candidates.AddRange(_partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon));
                    candidates.AddRange(_player.DemonStock);
                }

                List<string> performerLabels = candidates.Select(c => $"{c.Name} (SP: {c.CurrentSP}/{c.MaxSP})").ToList();
                performerLabels.Add("Back");

                int perfIdx = _io.RenderMenu("Who is performing the skill?", performerLabels, 0);
                if (perfIdx == -1 || perfIdx == performerLabels.Count - 1) return;

                Combatant performer = candidates[perfIdx];

                while (true)
                {
                    var skillPool = performer.GetConsolidatedSkills()
                        .Select(s => Database.Skills.TryGetValue(s, out var d) ? d : null)
                        .Where(d => d != null && d.Category != "Passive Skills" &&
                                   (d.Category.Contains("Recovery") || d.Effect.Contains("Cure")))
                        .ToList();

                    if (!skillPool.Any())
                    {
                        _io.WriteLine($"{performer.Name} has no field-usable skills.");
                        _io.Wait(800);
                        break;
                    }

                    List<string> skillLabels = skillPool.Select(s => $"{s.Name} ({s.Cost})").ToList();
                    skillLabels.Add("Back");

                    if (_skillMenuIndex >= skillLabels.Count) _skillMenuIndex = 0;

                    int skillIdx = _io.RenderMenu($"{performer.Name}'s Skills:", skillLabels, _skillMenuIndex, null, (index) =>
                    {
                        if (index >= 0 && index < skillPool.Count)
                            _io.WriteLine($"Effect: {skillPool[index].Effect}");
                    });

                    if (skillIdx == -1 || skillIdx == skillLabels.Count - 1) break;
                    _skillMenuIndex = skillIdx;

                    SkillData selectedSkill = skillPool[skillIdx];

                    var targetPool = _partyManager.ActiveParty.ToList();
                    List<string> targetLabels = targetPool.Select(c => $"{c.Name} (HP: {c.CurrentHP}/{c.MaxHP})").ToList();
                    targetLabels.Add("Back");

                    int targetIdx = _io.RenderMenu($"Performer: {performer.Name} | Skill: {selectedSkill.Name}. Select Target:", targetLabels, 0);
                    if (targetIdx == -1 || targetIdx == targetLabels.Count - 1) continue;

                    UseSkillField(selectedSkill, performer, targetPool[targetIdx]);
                }
            }
        }

        private void UseSkillField(SkillData skill, Combatant user, Combatant target)
        {
            var cost = skill.ParseCost();
            if (user.CurrentSP < cost.value)
            {
                _io.WriteLine("Not enough SP.");
                _io.Wait(800);
                return;
            }

            bool applied = false;
            StatusRegistry sr = new StatusRegistry();

            if (skill.Category.Contains("Recovery") || skill.Effect.Contains("Cure"))
            {
                if (skill.Effect.Contains("Cure"))
                {
                    applied = sr.CheckAndExecuteCure(target, skill.Effect);
                }
                else
                {
                    int heal = 0;
                    Match m = Regex.Match(skill.Effect, @"\((\d+)\)");
                    if (m.Success) heal = int.Parse(m.Groups[1].Value);
                    if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                    if (skill.Effect.Contains("full")) heal = target.MaxHP;

                    if (target.CurrentHP < target.MaxHP)
                    {
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                        _io.WriteLine($"{target.Name} was healed.");
                        applied = true;
                    }
                }
            }

            if (applied)
            {
                user.CurrentSP -= cost.value;
                _io.Wait(800);
            }
            else
            {
                _io.WriteLine("It had no effect.");
                _io.Wait(800);
            }
        }

        #region Status Screens (Menu-Driven)

        private void OpenSeamlessStatusMenu()
        {
            int statusHubIndex = 0;
            while (true)
            {
                string header = RenderHumanStatusToString(_player) + $"\nPoints Available: {_player.StatPoints}";
                List<string> options = new List<string> { "Allocate Stats", "Change Equipment" };

                if (_player.Class == ClassType.WildCard || _player.Class == ClassType.PersonaUser)
                    options.Add("Persona Stock");

                if (_player.Class == ClassType.Operator)
                    options.Add("Demon Stock");

                options.Add("Back");

                int choice = _io.RenderMenu(header, options, statusHubIndex);
                if (choice == -1 || choice == options.Count - 1) return;

                statusHubIndex = choice;
                string selected = options[choice];

                if (selected == "Allocate Stats") OpenStatAllocation();
                else if (selected == "Change Equipment") ShowEquipSlotMenu();
                else if (selected == "Persona Stock") OpenPersonaStockMenu();
                else if (selected == "Demon Stock") OpenDemonStockMenu();
            }
        }

        private void OpenPersonaStockMenu()
        {
            int personaListIndex = 0;
            while (true)
            {
                var allPersonas = new List<Persona>();
                if (_player.ActivePersona != null) allPersonas.Add(_player.ActivePersona);
                allPersonas.AddRange(_player.PersonaStock);

                if (allPersonas.Count == 0) { _io.WriteLine("No Personas available."); _io.Wait(800); return; }

                List<string> options = allPersonas.Select(p =>
                    $"{p.Name} (Lv.{p.Level}) {p.Arcana} {(p == _player.ActivePersona ? "[E]" : "")}").ToList();
                options.Add("Back");

                if (personaListIndex >= options.Count) personaListIndex = options.Count - 1;

                int idx = _io.RenderMenu("=== PERSONA STOCK ===", options, personaListIndex);
                if (idx == -1 || idx == options.Count - 1) return;

                personaListIndex = idx;
                ViewPersonaDetails(allPersonas[idx]);
            }
        }

        private void ViewPersonaDetails(Persona p)
        {
            int menuIndex = 0;
            while (true)
            {
                bool isEquipped = (p == _player.ActivePersona);
                string header = GetPersonaDetailString(p, isEquipped);
                List<string> options = new List<string>();
                if (!isEquipped) options.Add("Equip Persona");
                options.Add("Back");

                int choice = _io.RenderMenu(header, options, menuIndex);
                if (choice == -1 || selectedIsBack(choice, options)) return;

                if (options[choice] == "Equip Persona")
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

        private void OpenDemonStockMenu()
        {
            int demonListIndex = 0;
            while (true)
            {
                var allDemons = _partyManager.ActiveParty.Where(m => m.Class == ClassType.Demon).ToList();
                allDemons.AddRange(_player.DemonStock);

                if (allDemons.Count == 0) { _io.WriteLine("No demons found."); _io.Wait(800); return; }

                List<string> options = allDemons.Select(d =>
                    $"{d.Name} (Lv.{d.Level}) {(_partyManager.ActiveParty.Contains(d) ? "[PARTY]" : "[STOCK]")}").ToList();
                options.Add("Back");

                if (demonListIndex >= options.Count) demonListIndex = options.Count - 1;

                int idx = _io.RenderMenu("=== DEMON OVERVIEW ===", options, demonListIndex);
                if (idx == -1 || idx == options.Count - 1) return;

                demonListIndex = idx;
                ViewDemonDetails(allDemons[idx]);
            }
        }

        private void ViewDemonDetails(Combatant demon)
        {
            while (true)
            {
                string header = GetDemonDetailString(demon);
                List<string> options = new List<string> { "Back" };
                int choice = _io.RenderMenu(header, options, 0);
                if (choice == -1 || choice == 0) return;
            }
        }

        private void OpenStatAllocation() { StatAllocationModule.OpenMenu(_player, _io); }

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
                    $"Accessory:{_player.EquippedAccessory?.Name ?? "None"}",
                    "Back"
                };

                int slotChoice = _io.RenderMenu(header, options, _equipSlotIndex);
                if (slotChoice == -1 || slotChoice == options.Count - 1) return;
                _equipSlotIndex = slotChoice;

                ShopCategory cat = slotChoice switch { 0 => ShopCategory.Weapon, 1 => ShopCategory.Armor, 2 => ShopCategory.Boots, _ => ShopCategory.Accessory };
                ShowEquipSelectionList(cat);
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

            if (ids.Count == 0) { _io.WriteLine("No equipment in this category."); _io.Wait(800); return; }

            int listIndex = 0;
            while (true)
            {
                List<string> names = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var id in ids)
                {
                    string name = category switch
                    {
                        ShopCategory.Weapon => Database.Weapons[id].Name,
                        ShopCategory.Armor => Database.Armors[id].Name,
                        ShopCategory.Boots => Database.Boots[id].Name,
                        _ => Database.Accessories[id].Name
                    };
                    bool equipped = category switch
                    {
                        ShopCategory.Weapon => _player.EquippedWeapon?.Id == id,
                        ShopCategory.Armor => _player.EquippedArmor?.Id == id,
                        ShopCategory.Boots => _player.EquippedBoots?.Id == id,
                        _ => _player.EquippedAccessory?.Id == id
                    };
                    names.Add($"{name}{(equipped ? " [E]" : "")}");
                    disabled.Add(equipped);
                }
                names.Add("Back");
                disabled.Add(false);

                int choice = _io.RenderMenu($"=== EQUIP {category.ToString().ToUpper()} ===", names, listIndex, disabled, (index) =>
                {
                    if (index >= 0 && index < ids.Count)
                    {
                        string id = ids[index];
                        if (category == ShopCategory.Weapon) _io.WriteLine($"Pow: {Database.Weapons[id].Power} Acc: {Database.Weapons[id].Accuracy}");
                        else if (category == ShopCategory.Armor) _io.WriteLine($"Def: {Database.Armors[id].Defense} Eva: {Database.Armors[id].Evasion}");
                        else if (category == ShopCategory.Boots) _io.WriteLine($"Eva: {Database.Boots[id].Evasion}");
                        else _io.WriteLine($"Effect: {Database.Accessories[id].ModifierStat} +{Database.Accessories[id].ModifierValue}");
                    }
                });

                if (choice == -1 || choice == names.Count - 1) return;
                listIndex = choice;

                string selectedId = ids[choice];
                if (category == ShopCategory.Weapon) _player.EquippedWeapon = Database.Weapons[selectedId];
                else if (category == ShopCategory.Armor) _player.EquippedArmor = Database.Armors[selectedId];
                else if (category == ShopCategory.Boots) _player.EquippedBoots = Database.Boots[selectedId];
                else _player.EquippedAccessory = Database.Accessories[selectedId];

                _io.WriteLine("Equipped!"); _io.Wait(500);
            }
        }

        #endregion

        #region Helper Renderers

        private string RenderHumanStatusToString(Combatant entity)
        {
            string output = "=== STATUS & PARAMETERS ===\n";
            output += $"Name: {entity.Name} (Lv.{entity.Level}) | Class: {entity.Class}\n";
            output += $"HP: {entity.CurrentHP}/{entity.MaxHP} SP: {entity.CurrentSP}/{entity.MaxSP}\n";
            output += "-----------------------------\n";

            var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>();
            foreach (var stat in stats)
            {
                int total = entity.GetStat(stat);
                int baseVal = entity.CharacterStats[stat];

                if (stat == StatType.INT || stat == StatType.CHA)
                {
                    output += $"{stat,-4}: {total,3}\n";
                    continue;
                }

                int mod = total - baseVal;
                if (mod > 0)
                {
                    output += $"{stat,-4}: {total,3} (+{mod})\n";
                }
                else
                {
                    output += $"{stat,-4}: {total,3}\n";
                }
            }
            output += "-----------------------------";
            return output;
        }

        private string GetPersonaDetailString(Persona p, bool isEquipped)
        {
            string output = $"=== PERSONA DETAILS {(isEquipped ? "[EQUIPPED]" : "")} ===\n";
            output += $"Name: {p.Name} (Lv.{p.Level}) | Arcana: {p.Arcana}\n";
            output += $"EXP: {p.Exp}/{p.ExpRequired} Next: {p.ExpRequired - p.Exp}\n"; // Updated to include Next
            output += "-----------------------------\nRaw Stats:\n";

            var displayStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in displayStats)
            {
                int val = p.StatModifiers.ContainsKey(stat) ? p.StatModifiers[stat] : 0;
                output += $"  {stat}: {val,3}\n";
            }
            output += "-----------------------------\nSkills:\n";
            foreach (var s in p.SkillSet) output += $" - {s}\n";

            var nextSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key).Take(3).ToList();
            if (nextSkills.Any())
            {
                output += "\nNext to Learn:\n";
                foreach (var ns in nextSkills) output += $" [Lv.{ns.Key}] {ns.Value}\n";
            }
            else if (p.SkillsToLearn.Any())
            {
                output += "\n(Mastered)\n";
            }

            return output;
        }

        private string GetDemonDetailString(Combatant demon)
        {
            string output = "=== DEMON DETAILS ===\n";
            output += $"Name: {demon.Name} (Lv.{demon.Level})\n";
            output += $"HP: {demon.CurrentHP}/{demon.MaxHP} SP: {demon.CurrentSP}/{demon.MaxSP}\n";
            output += $"EXP: {demon.Exp}/{demon.ExpRequired} Next: {demon.ExpRequired - demon.Exp}\n"; // Added missing EXP details
            output += "-----------------------------\n";

            var stats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in stats)
            {
                int total = demon.GetStat(stat);
                output += $"{stat,-4}: {total,3}\n";
            }
            output += "-----------------------------\nSkills:\n";
            foreach (var s in demon.GetConsolidatedSkills()) output += $" - {s}\n";

            if (demon.ActivePersona != null)
            {
                var nextSkills = demon.ActivePersona.SkillsToLearn.Where(k => k.Key > demon.Level).OrderBy(k => k.Key).Take(3).ToList();
                if (nextSkills.Any())
                {
                    output += "\nNext to Learn:\n";
                    foreach (var ns in nextSkills) output += $" [Lv.{ns.Key}] {ns.Value}\n";
                }
                else if (demon.ActivePersona.SkillsToLearn.Any())
                {
                    output += "\n(Mastered)\n";
                }
            }

            return output;
        }


        private bool selectedIsBack(int index, List<string> options) { return index == options.Count - 1; }

        private void OpenOrganizeMenu()
        {
            int orgIndex = 0;
            while (true)
            {
                string header = "=== ORGANIZE PARTY ===\nSelect a slot to manage:";
                List<string> options = new List<string>();

                for (int i = 0; i < 4; i++)
                {
                    if (i < _partyManager.ActiveParty.Count)
                    {
                        var member = _partyManager.ActiveParty[i];
                        options.Add($"Slot {i + 1}: {member.Name} (Lv.{member.Level})");
                    }
                    else options.Add($"Slot {i + 1}: [EMPTY]");
                }
                options.Add("Back");

                int choice = _io.RenderMenu(header, options, orgIndex);
                if (choice == -1 || choice == options.Count - 1) return;
                orgIndex = choice;

                if (choice < _partyManager.ActiveParty.Count) ManagePartyMember(choice);
                else if (choice == _partyManager.ActiveParty.Count && _partyManager.ActiveParty.Count < 4) OpenSummonFromOrg();
            }
        }

        private void ManagePartyMember(int index)
        {
            Combatant member = _partyManager.ActiveParty[index];
            if (member == _player) { _io.WriteLine("The protagonist cannot be removed."); _io.Wait(800); return; }

            List<string> options = new List<string> { "Return to COMP", "Back" };
            int choice = _io.RenderMenu($"Manage {member.Name}", options, 0);
            if (choice == 0)
            {
                _partyManager.ReturnDemon(member);
                _player.DemonStock.Add(member);
                _io.WriteLine($"{member.Name} returned to stock.");
                _io.Wait(600);
            }
        }

        private void OpenSummonFromOrg()
        {
            if (!_player.DemonStock.Any()) { _io.WriteLine("No demons in stock."); _io.Wait(800); return; }
            var names = _player.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList();
            names.Add("Cancel");
            int idx = _io.RenderMenu("SUMMON DEMON", names, 0);
            if (idx == -1 || idx == names.Count - 1) return;

            if (_partyManager.SummonDemon(_player.DemonStock[idx]))
            {
                _io.WriteLine($"{_player.DemonStock[idx].Name} joined the party!");
                _player.DemonStock.RemoveAt(idx);
                _io.Wait(800);
            }
        }

        #endregion
    }
}