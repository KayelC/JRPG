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
        private int _inventoryMenuIndex = 0;
        private int _statusMenuIndex = 0;
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

                // Main Menu Options
                List<string> options = new List<string> { "Inventory", "Status", "City" };

                if (_player.StatPoints > 0)
                    options.Add($"Allocate Stats ({_player.StatPoints} pts)");
                else
                    options.Add("Allocate Stats (0 pts)");

                options.Add("Proceed to Battle");

                int choice = MenuUI.RenderMenu(header, options, _mainMenuIndex);

                if (choice != -1) _mainMenuIndex = choice;

                // Handle Selections
                switch (choice)
                {
                    case 0: OpenInventoryMenu(); break; // New Sub-Menu
                    case 1: OpenStatusMenu(); break;    // New Sub-Menu
                    case 2: OpenCityMenu(); break;      // New Sub-Menu
                    case 3: // Allocate Stats
                        if (_player.StatPoints > 0) StatAllocationModule.OpenMenu(_player);
                        else
                        {
                            Console.WriteLine("No points to allocate.");
                            Thread.Sleep(500);
                        }
                        break;
                    case 4: return; // Proceed
                    case -1: // Escape handling (Root Level)
                        // Do nothing, just loop (effectively ignores Escape at root)
                        break;
                }
            }
        }

        // --- SUB-MENU LAYERS ---

        private void OpenInventoryMenu()
        {
            while (true)
            {
                string header = "=== INVENTORY MANAGEMENT ===";
                List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment" };

                int inventoryChoice = MenuUI.RenderMenu(header, options, _inventoryMenuIndex);

                if (inventoryChoice == -1) return; // Back to Main Menu

                _inventoryMenuIndex = inventoryChoice; // Persist

                switch (inventoryChoice)
                {
                    case 0: ShowItemMenu(); break;
                    case 1: ShowSkillMenu(); break;
                    case 2: ShowEquipMenu(); break;
                }
            }
        }

        private void OpenStatusMenu()
        {
            while (true)
            {
                string header = "=== STATUS ===";
                List<string> options = new List<string> { "Operator Status", "Persona Status" };

                int statusChoice = MenuUI.RenderMenu(header, options, _statusMenuIndex);

                if (statusChoice == -1) return; // Back to Main Menu

                _statusMenuIndex = statusChoice; // Persist

                switch (statusChoice)
                {
                    case 0: ShowOperatorStatus(); break;
                    case 1: ShowPersonaStatus(); break;
                }
            }
        }

        private void OpenCityMenu()
        {
            while (true)
            {
                string header = $"=== CITY SERVICES ===\nMacca: {_economy.Macca}";
                List<string> options = new List<string> { "Weapon/Item Shop" };

                int cityChoice = MenuUI.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1) return; // Back to Main Menu

                _cityMenuIndex = cityChoice; // Persist

                switch (cityChoice)
                {
                    case 0: _shop.OpenShop(_player); break;
                }
            }
        }

        // --- STATUS SCREENS ---

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
            Console.WriteLine($"Wep:   {_player.EquippedWeapon?.Name ?? "Unarmed"}");
            Console.WriteLine($"Macca: {_economy.Macca}");

            Console.WriteLine("\n--- STATS BREAKDOWN ---");
            // Safe accessor for persona stats
            int GetPStat(StatType t) => _player.ActivePersona?.StatModifiers.ContainsKey(t) == true ? _player.ActivePersona.StatModifiers[t] : 0;

            string personaName = _player.ActivePersona?.Name ?? "None";
            Console.WriteLine($"Persona: {personaName}");
            Console.WriteLine("       [Total]   [Base]   [Persona]");

            Console.WriteLine($"STR:   {_player.GetStat(StatType.STR),-9} {_player.CharacterStats[StatType.STR],-8} +{GetPStat(StatType.STR)}");
            Console.WriteLine($"MAG:   {_player.GetStat(StatType.MAG),-9} {_player.CharacterStats[StatType.MAG],-8} +{GetPStat(StatType.MAG)}");
            Console.WriteLine($"END:   {_player.GetStat(StatType.END),-9} {_player.CharacterStats[StatType.END],-8} +{GetPStat(StatType.END)}");
            Console.WriteLine($"AGI:   {_player.GetStat(StatType.AGI),-9} {_player.CharacterStats[StatType.AGI],-8} +{GetPStat(StatType.AGI)}");
            Console.WriteLine($"LUK:   {_player.GetStat(StatType.LUK),-9} {_player.CharacterStats[StatType.LUK],-8} +{GetPStat(StatType.LUK)}");

            Console.WriteLine("\n--- OPERATOR EXCLUSIVE ---");
            Console.WriteLine($"INT:   {_player.CharacterStats[StatType.INT]} (Determines MaxSP)");
            Console.WriteLine($"CHA:   {_player.CharacterStats[StatType.CHA]} (Negotiation & Shop Discount)");
            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey(true);
        }

        private void ShowPersonaStatus()
        {
            Console.Clear();
            Persona p = _player.ActivePersona;

            if (p == null)
            {
                Console.WriteLine("\nNo Active Persona equipped.");
                Console.WriteLine("\nPress any key to return...");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine($"\n=== PERSONA STATUS: {p.Name.ToUpper()} ===");
            Console.WriteLine($"Level:  {p.Level}");
            Console.WriteLine($"Arcana: {p.Arcana}");
            Console.WriteLine($"EXP:    {p.Exp}/{p.ExpRequired}");

            Console.WriteLine("\n--- STAT MODIFIERS ---");
            foreach (var kvp in p.StatModifiers)
            {
                Console.WriteLine($"{kvp.Key,-4}: +{kvp.Value}");
            }

            Console.WriteLine("\n--- CURRENT SKILL SET ---");
            foreach (var skill in p.SkillSet)
            {
                string details = "";
                if (Database.Skills.TryGetValue(skill, out var sData))
                {
                    details = $"({sData.Cost}) - {sData.Effect}";
                }
                Console.WriteLine($"- {skill,-15} {details}");
            }

            Console.WriteLine("\n--- SKILLS TO LEARN ---");
            // Filter skills that have a level higher than current level
            var futureSkills = p.SkillsToLearn.Where(k => k.Key > p.Level).OrderBy(k => k.Key);

            if (futureSkills.Any())
            {
                foreach (var kvp in futureSkills)
                {
                    Console.WriteLine($"Lv.{kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                Console.WriteLine("(No upcoming skills known)");
            }

            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey(true);
        }

        // --- FUNCTIONALITY IMPLEMENTATIONS ---

        private void ShowItemMenu()
        {
            int listIndex = 0;
            while (true)
            {
                var items = Database.Items.Values.Where(itm => _inventory.GetQuantity(itm.Id) > 0).ToList();
                if (items.Count == 0) { Console.WriteLine("Inventory is empty."); Thread.Sleep(800); return; }

                List<string> options = new List<string>();
                foreach (var item in items)
                {
                    options.Add($"{item.Name} x{_inventory.GetQuantity(item.Id)}");
                }

                if (listIndex >= options.Count) listIndex = Math.Max(0, options.Count - 1);

                int idx = MenuUI.RenderMenu("=== ITEM MENU ===", options, listIndex, null, (index) =>
                {
                    Console.WriteLine($"Effect: {items[index].Description}");
                });

                if (idx == -1) return;
                listIndex = idx;

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

        private void ShowSkillMenu()
        {
            int listIndex = 0;
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

                if (usableSkills.Count == 0) { Console.WriteLine("No usable skills."); Thread.Sleep(800); return; }

                List<string> options = new List<string>();
                foreach (var skillName in usableSkills)
                {
                    var d = Database.Skills[skillName];
                    options.Add($"{skillName} ({d.Cost})");
                }

                int idx = MenuUI.RenderMenu("=== FIELD SKILLS ===", options, listIndex, null, (index) =>
                {
                    Console.WriteLine($"Effect: {Database.Skills[usableSkills[index]].Effect}");
                });

                if (idx == -1) return;
                listIndex = idx;

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

        private void ShowEquipMenu()
        {
            int listIndex = 0;
            while (true)
            {
                if (_inventory.OwnedWeapons.Count == 0) { Console.WriteLine("No weapons."); Thread.Sleep(800); return; }

                List<string> options = new List<string>();
                foreach (var weaponId in _inventory.OwnedWeapons)
                {
                    var w = Database.Weapons[weaponId];
                    string equipMark = (_player.EquippedWeapon?.Id == weaponId) ? " [E]" : "";
                    options.Add($"{w.Name}{equipMark}");
                }

                if (listIndex >= options.Count) listIndex = Math.Max(0, options.Count - 1);

                int idx = MenuUI.RenderMenu("=== EQUIP WEAPON ===", options, listIndex, null, (index) =>
                {
                    string wId = _inventory.OwnedWeapons[index];
                    var w = Database.Weapons[wId];
                    var cur = _player.EquippedWeapon;

                    Console.WriteLine($"New: Power {w.Power} | Acc {w.Accuracy}% | {(w.IsLongRange ? "Ranged" : "Melee")}");
                    if (cur != null)
                        Console.WriteLine($"Cur: Power {cur.Power} | Acc {cur.Accuracy}% | {(cur.IsLongRange ? "Ranged" : "Melee")}");
                });

                if (idx == -1) return;
                listIndex = idx;

                string selectedWeaponId = _inventory.OwnedWeapons[idx];
                _player.EquippedWeapon = Database.Weapons[selectedWeaponId];
            }
        }
    }
}