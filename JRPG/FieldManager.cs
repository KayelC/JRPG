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
        private int _equipMenuIndex = 0;
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
                // Removed Location, Ensured HP/SP are visible
                string header = $"=== FIELD MENU ===\nMacca: {_economy.Macca}\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                List<string> options = new List<string>
                {
                    "City",         // 0
                    "Inventory",    // 1
                    "Status",       // 2
                    "Proceed to Battle" // 3
                };

                int choice = MenuUI.RenderMenu(header, options, _mainMenuIndex);

                if (choice == -1) continue; // Loop at root

                _mainMenuIndex = choice;

                switch (choice)
                {
                    case 0: OpenCityMenu(); break;
                    case 1: OpenInventoryMenu(); break;
                    case 2: OpenStatusMenu(); break;
                    case 3: return; // Proceed to Battle
                }
            }
        }

        // --- SUB-MENU: CITY ---
        private void OpenCityMenu()
        {
            while (true)
            {
                string header = $"=== CITY SERVICES ===\nMacca: {_economy.Macca}\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";
                List<string> options = new List<string> { "Weapon Shop", "Item Shop" };

                int cityChoice = MenuUI.RenderMenu(header, options, _cityMenuIndex);

                if (cityChoice == -1) return; // Back to Main
                _cityMenuIndex = cityChoice;

                if (cityChoice == 0) _shop.OpenShop(_player, isWeaponShop: true);
                else if (cityChoice == 1) _shop.OpenShop(_player, isWeaponShop: false);
            }
        }

        // --- SUB-MENU: INVENTORY ---
        private void OpenInventoryMenu()
        {
            while (true)
            {
                string header = $"=== INVENTORY ===\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";
                List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment" };

                int invChoice = MenuUI.RenderMenu(header, options, _inventoryMenuIndex);

                if (invChoice == -1) return; // Back to Main
                _inventoryMenuIndex = invChoice;

                switch (invChoice)
                {
                    case 0: ShowItemMenu(); break;
                    case 1: ShowSkillMenu(); break;
                    case 2: ShowEquipMenu(); break;
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

                if (statusChoice == -1) return; // Back to Main
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
            Console.WriteLine($"Wep:   {_player.EquippedWeapon?.Name ?? "Unarmed"}");

            Console.WriteLine("\n--- STATS BREAKDOWN (Base + Persona) ---");
            int GetPStat(StatType t) => _player.ActivePersona?.StatModifiers.ContainsKey(t) == true ? _player.ActivePersona.StatModifiers[t] : 0;

            string personaName = _player.ActivePersona?.Name ?? "None";
            Console.WriteLine($"Persona: {personaName}");
            Console.WriteLine("       [Total]   [Base]   [Persona]");

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

        // --- INVENTORY LOGIC (Items, Skills, Equip) ---

        private void ShowItemMenu()
        {
            while (true)
            {
                var items = Database.Items.Values.Where(itm => _inventory.GetQuantity(itm.Id) > 0).ToList();
                if (items.Count == 0) { Console.WriteLine("Inventory is empty."); Thread.Sleep(800); return; }

                List<string> options = new List<string>();
                foreach (var item in items)
                {
                    options.Add($"{item.Name} x{_inventory.GetQuantity(item.Id)}");
                }

                // Header now includes HP/SP for real-time tracking
                string header = $"=== USE ITEM ===\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                if (_itemMenuIndex >= options.Count) _itemMenuIndex = 0;

                int idx = MenuUI.RenderMenu(header, options, _itemMenuIndex, null, (index) =>
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

                if (usableSkills.Count == 0) { Console.WriteLine("No usable skills."); Thread.Sleep(800); return; }

                List<string> options = new List<string>();
                foreach (var skillName in usableSkills)
                {
                    var d = Database.Skills[skillName];
                    options.Add($"{skillName} ({d.Cost})");
                }

                if (_skillMenuIndex >= options.Count) _skillMenuIndex = 0;

                string header = $"=== FIELD SKILLS ===\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";

                int idx = MenuUI.RenderMenu(header, options, _skillMenuIndex, null, (index) =>
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

        private void ShowEquipMenu()
        {
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

                if (_equipMenuIndex >= options.Count) _equipMenuIndex = 0;

                int idx = MenuUI.RenderMenu("=== EQUIP WEAPON ===", options, _equipMenuIndex, null, (index) =>
                {
                    string wId = _inventory.OwnedWeapons[index];
                    var w = Database.Weapons[wId];
                    var cur = _player.EquippedWeapon;

                    Console.WriteLine($"New: Power {w.Power} | Acc {w.Accuracy}% | {(w.IsLongRange ? "Ranged" : "Melee")}");
                    if (cur != null)
                        Console.WriteLine($"Cur: Power {cur.Power} | Acc {cur.Accuracy}% | {(cur.IsLongRange ? "Ranged" : "Melee")}");
                });

                if (idx == -1) return;
                _equipMenuIndex = idx;

                string selectedWeaponId = _inventory.OwnedWeapons[idx];
                _player.EquippedWeapon = Database.Weapons[selectedWeaponId];
            }
        }
    }
}