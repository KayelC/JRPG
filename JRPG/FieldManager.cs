using System;
using System.Collections.Generic;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    public class FieldManager
    {
        private Combatant _player;
        private InventoryManager _inventory;
        private EconomyManager _economy;
        private ShopManager _shop;
        private IGameIO _io;

        // Persistent Cursors for Menu Hierarchies
        private int _mainMenuIndex = 0;
        private int _statusMenuIndex = 0;
        private int _inventoryMenuIndex = 0;
        private int _itemMenuIndex = 0;
        private int _skillMenuIndex = 0;
        private int _equipSlotIndex = 0;
        private int _cityMenuIndex = 0;

        public FieldManager(Combatant player, InventoryManager inventory, EconomyManager economy, IGameIO io)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _io = io;
            _shop = new ShopManager(inventory, economy, io);
        }

        public void NavigateMenus()
        {
            while (true)
            {
                string header = $"=== FIELD MENU ===\nMacca: {_economy.Macca}\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                List<string> options = new List<string>
                {
                    "City",
                    "Inventory",
                    "Status",
                    "Proceed to Battle"
                };

                int choice = _io.RenderMenu(header, options, _mainMenuIndex);

                if (choice == -1)
                {
                    continue;
                }

                _mainMenuIndex = choice;

                switch (choice)
                {
                    case 0: OpenCityMenu(); break;
                    case 1: OpenInventoryMenu(); break;
                    case 2: OpenStatusMenu(); break;
                    case 3: return; // Returns to Program.cs to trigger battle loop
                }
            }
        }

        // --- SUB-MENU: CITY ---
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
                    "Pharmacy (Items)"
                };

                int cityChoice = _io.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1) return;

                _cityMenuIndex = cityChoice;

                switch (cityChoice)
                {
                    case 0: _shop.OpenShop(_player, ShopType.Weapon); break;
                    case 1:
                        // Simple sub-selector for Clothing Store
                        int clothingChoice = _io.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots" }, 0);
                        if (clothingChoice == 0) _shop.OpenShop(_player, ShopType.Armor);
                        else if (clothingChoice == 1) _shop.OpenShop(_player, ShopType.Boots);
                        break;
                    case 2: _shop.OpenShop(_player, ShopType.Accessory); break;
                    case 3: _shop.OpenShop(_player, ShopType.Item); break;
                }
            }
        }

        // --- SUB-MENU: INVENTORY ---
        private void OpenInventoryMenu()
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
                    case 0: ShowItemMenu(); break;
                    case 1: ShowSkillMenu(); break;
                    case 2: ShowEquipSlotMenu(); break;
                }
            }
        }

        // --- SUB-MENU: STATUS TREE ---
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

        // --- VIEW: OPERATOR STATUS ---
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

        // --- VIEW: PERSONA DEEP DIVE ---
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

        // --- INVENTORY LOGIC (Items) ---
        private void ShowItemMenu()
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
                foreach (var item in items)
                {
                    options.Add($"{item.Name} x{_inventory.GetQuantity(item.Id)}");
                }

                if (_itemMenuIndex >= options.Count) _itemMenuIndex = 0;

                int idx = _io.RenderMenu("=== USE ITEM ===", options, _itemMenuIndex, null, (index) =>
                {
                    _io.WriteLine($"Effect: {items[index].Description}");
                });

                if (idx == -1) return;
                _itemMenuIndex = idx;

                UseItemField(items[idx].Id, _player);
            }
        }

        private void UseItemField(string itemId, Combatant target)
        {
            if (!_inventory.HasItem(itemId)) return;
            ItemData item = Database.Items[itemId];
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

        // --- SKILL LOGIC ---
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

        // --- EQUIPMENT LOGIC ---
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