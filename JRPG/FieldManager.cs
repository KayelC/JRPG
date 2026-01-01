using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace JRPGPrototype
{
    public class FieldManager
    {
        private Combatant _player;
        private InventoryManager _inventory;
        private EconomyManager _economy;
        private ShopManager _shop;

        // Persistent Cursors for Menu Hierarchies
        private int _mainMenuIndex = 0;
        private int _statusMenuIndex = 0;
        private int _inventoryMenuIndex = 0;
        private int _itemMenuIndex = 0;
        private int _skillMenuIndex = 0;
        private int _equipSlotIndex = 0;
        private int _cityMenuIndex = 0;

        public FieldManager(Combatant player, InventoryManager inventory, EconomyManager economy)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _shop = new ShopManager(inventory, economy);
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

                int choice = MenuUI.RenderMenu(header, options, _mainMenuIndex);

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
                    case 3: return;
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

                int cityChoice = MenuUI.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1) return;

                _cityMenuIndex = cityChoice;

                switch (cityChoice)
                {
                    case 0: _shop.OpenShop(_player, ShopType.Weapon); break;
                    case 1:
                        // Simple sub-selector for Clothing Store
                        int clothingChoice = MenuUI.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots" });
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

                int invChoice = MenuUI.RenderMenu(header, options, _inventoryMenuIndex);

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

                int statusChoice = MenuUI.RenderMenu(header, options, _statusMenuIndex);

                if (statusChoice == -1) return;
                _statusMenuIndex = statusChoice;

                if (statusChoice == 0) ShowOperatorStatus();
                else if (statusChoice == 1) ShowPersonaStatus();
                else if (statusChoice == 2 && canAllocate)
                {
                    StatAllocationModule.OpenMenu(_player);
                }
            }
        }

        // --- VIEW: OPERATOR STATUS ---
        private void ShowOperatorStatus()
        {
            Console.Clear();
            Console.WriteLine("\n=== OPERATOR STATUS ===");
            Console.WriteLine($"Name:  {_player.Name} (Lv.{_player.Level})");
            Console.WriteLine($"Class: Operator");
            Console.WriteLine($"EXP:   {_player.Exp}/{_player.ExpRequired}");
            Console.WriteLine($"HP:    {_player.CurrentHP}/{_player.MaxHP}");
            Console.WriteLine($"SP:    {_player.CurrentSP}/{_player.MaxSP}");
            Console.WriteLine($"Cond:  {(_player.CurrentAilment?.Name ?? "Healthy")}");
            Console.WriteLine($"Def:   {_player.GetDefense()} | Eva: {_player.GetEvasion()}");

            Console.WriteLine("\n--- EQUIPMENT ---");
            Console.WriteLine($"Weapon: {_player.EquippedWeapon?.Name ?? "None"}");
            Console.WriteLine($"Armor:  {_player.EquippedArmor?.Name ?? "None"}");
            Console.WriteLine($"Boots:  {_player.EquippedBoots?.Name ?? "None"}");
            Console.WriteLine($"Acc:    {_player.EquippedAccessory?.Name ?? "None"}");

            Console.WriteLine("\n--- STATS BREAKDOWN (Base + Persona) ---");
            int GetPStat(StatType t) => _player.ActivePersona?.StatModifiers.ContainsKey(t) == true ? _player.ActivePersona.StatModifiers[t] : 0;

            string personaName = _player.ActivePersona?.Name ?? "None";
            Console.WriteLine($"Persona: {personaName}");
            Console.WriteLine("    [Total]   [Base]");

            Console.WriteLine($"STR:   {_player.GetStat(StatType.STR),-5} (Base: {_player.CharacterStats[StatType.STR]})");
            Console.WriteLine($"MAG:   {_player.GetStat(StatType.MAG),-5} (Base: {_player.CharacterStats[StatType.MAG]})");
            Console.WriteLine($"END:   {_player.GetStat(StatType.END),-5} (Base: {_player.CharacterStats[StatType.END]})");
            Console.WriteLine($"AGI:   {_player.GetStat(StatType.AGI),-5} (Base: {_player.CharacterStats[StatType.AGI]})");
            Console.WriteLine($"LUK:   {_player.GetStat(StatType.LUK),-5} (Base: {_player.CharacterStats[StatType.LUK]})");

            Console.WriteLine("\n--- OPERATOR EXCLUSIVE ---");
            Console.WriteLine($"INT:   {_player.CharacterStats[StatType.INT]} (Determines MaxSP)");
            Console.WriteLine($"CHA:   {_player.CharacterStats[StatType.CHA]} (Negotiation & Shop Discount)");
            Console.WriteLine("\n[ESC] Back");
            Console.ReadKey(true);
        }

        // --- VIEW: PERSONA DEEP DIVE ---
        private void ShowPersonaStatus()
        {
            Console.Clear();
            Persona p = _player.ActivePersona;

            if (p == null)
            {
                Console.WriteLine("\nNo Active Persona equipped.");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine($"\n=== PERSONA SHEET: {p.Name.ToUpper()} ===");
            Console.WriteLine($"Level:  {p.Level}");
            Console.WriteLine($"Arcana: {p.Arcana}");
            Console.WriteLine($"EXP:    {p.Exp}/{p.ExpRequired}");

            Console.WriteLine("\n--- RAW STATS ---");
            var displayStats = new[] { StatType.STR, StatType.MAG, StatType.END, StatType.AGI, StatType.LUK };
            foreach (var stat in displayStats)
            {
                int val = p.StatModifiers.ContainsKey(stat) ? p.StatModifiers[stat] : 0;
                Console.WriteLine($"{stat,-4}: {val,3}");
            }

            Console.WriteLine("\n--- EQUIPPED SKILLS ---");
            if (p.SkillSet.Count == 0) Console.WriteLine("(None)");
            foreach (var skill in p.SkillSet)
            {
                string details = "";
                if (Database.Skills.TryGetValue(skill, out var sData))
                {
                    details = $"| {sData.Cost,-5} | {sData.Effect}";
                }
                Console.WriteLine($"- {skill,-15} {details}");
            }

            Console.WriteLine("\n--- POTENTIAL (Future Skills) ---");
            var futureSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key).ToList();

            if (futureSkills.Any())
            {
                foreach (var kvp in futureSkills)
                {
                    Console.WriteLine($"Lv.{kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                Console.WriteLine("(Mastered)");
            }

            Console.WriteLine("\n[ESC] Back");
            Console.ReadKey(true);
        }

        // --- INVENTORY LOGIC (Items) ---

        private void ShowItemMenu()
        {
            while (true)
            {
                var items = Database.Items.Values.Where(itm => _inventory.GetQuantity(itm.Id) > 0).ToList();
                if (items.Count == 0)
                {
                    Console.WriteLine("Inventory is empty.");
                    Thread.Sleep(800);
                    return;
                }

                List<string> options = new List<string>();
                foreach (var item in items)
                {
                    options.Add($"{item.Name} x{_inventory.GetQuantity(item.Id)}");
                }

                if (_itemMenuIndex >= options.Count) _itemMenuIndex = 0;

                int idx = MenuUI.RenderMenu("=== USE ITEM ===", options, _itemMenuIndex, null, (index) =>
                {
                    Console.WriteLine($"Effect: {items[index].Description}");
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
                    if (target.CurrentHP >= target.MaxHP) Console.WriteLine("HP is full.");
                    else
                    {
                        int old = target.CurrentHP;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                        Console.WriteLine($"Restored {target.CurrentHP - old} HP.");
                        used = true;
                    }
                    break;
                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP) Console.WriteLine("SP is full.");
                    else
                    {
                        int old = target.CurrentSP;
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        Console.WriteLine($"Restored {target.CurrentSP - old} SP.");
                        used = true;
                    }
                    break;
                case "Cure":
                    if (target.CurrentAilment == null) Console.WriteLine("Healthy.");
                    else
                    {
                        bool cured = false;
                        if (item.Name == "Dis-Poison" && target.CurrentAilment.Name == "Poison") cured = true;
                        else if (item.Name == "Patra Card" && target.CheckCure("Cure All")) cured = true;
                        if (cured) { Console.WriteLine($"Cured {target.CurrentAilment?.Name}!"); target.RemoveAilment(); used = true; }
                        else Console.WriteLine("No effect.");
                    }
                    break;
                default: Console.WriteLine("Cannot use here."); break;
            }

            if (used) { _inventory.RemoveItem(itemId, 1); Thread.Sleep(800); }
            else Thread.Sleep(800);
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
                    Console.WriteLine("No usable skills.");
                    Thread.Sleep(800);
                    return;
                }

                List<string> options = new List<string>();
                foreach (var skillName in usableSkills)
                {
                    var d = Database.Skills[skillName];
                    options.Add($"{skillName} ({d.Cost})");
                }

                if (_skillMenuIndex >= options.Count) _skillMenuIndex = 0;

                int idx = MenuUI.RenderMenu("=== FIELD SKILLS ===", options, _skillMenuIndex, null, (index) =>
                {
                    Console.WriteLine($"Effect: {Database.Skills[usableSkills[index]].Effect}");
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
                Console.WriteLine("Not enough resources."); Thread.Sleep(800); return;
            }

            bool effectApplied = false;
            if (skill.Effect != null && skill.Effect.Contains("restores", StringComparison.OrdinalIgnoreCase))
            {
                if (target.CurrentHP >= target.MaxHP) { Console.WriteLine("HP full."); }
                else
                {
                    int healAmount = 50;
                    var match = Regex.Match(skill.Effect, @"\((\d+)\)");
                    if (match.Success) int.TryParse(match.Groups[1].Value, out healAmount);
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                    Console.WriteLine($"Restored HP.");
                    effectApplied = true;
                }
            }
            else if (skill.Effect != null && skill.Effect.Contains("Cure") && target.CurrentAilment != null)
            {
                if (target.CheckCure(skill.Effect)) { Console.WriteLine("Cured."); effectApplied = true; }
            }

            if (effectApplied) { if (cost.isHP) user.CurrentHP -= cost.value; else user.CurrentSP -= cost.value; Thread.Sleep(800); }
            else Thread.Sleep(800);
        }

        // --- EQUIPMENT LOGIC ---

        private void ShowEquipSlotMenu()
        {
            while (true)
            {
                string header = "=== EQUIPMENT SLOTS ===";
                List<string> options = new List<string>
                {
                    $"Weapon:   {_player.EquippedWeapon?.Name ?? "None"}",
                    $"Armor:    {_player.EquippedArmor?.Name ?? "None"}",
                    $"Boots:    {_player.EquippedBoots?.Name ?? "None"}",
                    $"Accessory:{_player.EquippedAccessory?.Name ?? "None"}"
                };

                int slotChoice = MenuUI.RenderMenu(header, options, _equipSlotIndex);
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
            // Generic list builder for any gear type
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
                Console.WriteLine("No equipment in this category.");
                Thread.Sleep(800);
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
                    disabled.Add(isEquipped); // Can't re-equip currently equipped
                }

                if (listIndex >= names.Count) listIndex = 0;

                int choice = MenuUI.RenderMenu($"=== EQUIP {category.ToString().ToUpper()} ===", names, listIndex, disabled, (index) =>
                {
                    string id = ids[index];
                    // Preview Stats Logic
                    if (category == ShopCategory.Weapon) Console.WriteLine($"Pow: {Database.Weapons[id].Power} Acc: {Database.Weapons[id].Accuracy}");
                    if (category == ShopCategory.Armor) Console.WriteLine($"Def: {Database.Armors[id].Defense} Eva: {Database.Armors[id].Evasion}");
                    if (category == ShopCategory.Boots) Console.WriteLine($"Eva: {Database.Boots[id].Evasion}");
                    if (category == ShopCategory.Accessory) Console.WriteLine($"Effect: {Database.Accessories[id].ModifierStat} +{Database.Accessories[id].ModifierValue}");
                });

                if (choice == -1) return;
                listIndex = choice;

                string selectedId = ids[choice];
                if (category == ShopCategory.Weapon) _player.EquippedWeapon = Database.Weapons[selectedId];
                if (category == ShopCategory.Armor) _player.EquippedArmor = Database.Armors[selectedId];
                if (category == ShopCategory.Boots) _player.EquippedBoots = Database.Boots[selectedId];
                if (category == ShopCategory.Accessory) _player.EquippedAccessory = Database.Accessories[selectedId];

                Console.WriteLine("Equipped!");
                Thread.Sleep(500);
            }
        }
    }
}