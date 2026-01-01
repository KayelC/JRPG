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

        public FieldManager(Combatant player, InventoryManager inventory, EconomyManager economy)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _shop = new ShopManager(inventory, economy);
        }

        public void NavigateMenus()
        {
            int mainIndex = 0;
            while (true)
            {
                string header = $"=== FIELD MENU ===\nMacca: {_economy.Macca}\nHP: {_player.CurrentHP}/{_player.MaxHP} | SP: {_player.CurrentSP}/{_player.MaxSP}";
                List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment", "Status", "Shop", "Proceed to Battle" };

                if (_player.StatPoints > 0) options.Add($"Allocate Stats ({_player.StatPoints} pts)");

                int choice = MenuUI.RenderMenu(header, options, mainIndex);
                if (choice != -1) mainIndex = choice;

                switch (choice)
                {
                    case 0: ShowItemMenu(); break;
                    case 1: ShowSkillMenu(); break;
                    case 2: ShowEquipMenu(); break;
                    case 3: ShowStatus(); break;
                    case 4: _shop.OpenShop(_player); break;
                    case 5: return;
                    case 6:
                        if (_player.StatPoints > 0) StatAllocationModule.OpenMenu(_player);
                        break;
                }
            }
        }

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

                // Fixed naming collision here
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

                // Use descriptive variable name to avoid ambiguity
                string selectedWeaponId = _inventory.OwnedWeapons[idx];
                _player.EquippedWeapon = Database.Weapons[selectedWeaponId];
            }
        }

        private void ShowStatus()
        {
            Console.Clear();
            Console.WriteLine("\n=== STATUS SHEET ===");
            Console.WriteLine($"Name:  {_player.Name} (Lv.{_player.Level})");
            Console.WriteLine($"Class: Operator");
            Console.WriteLine($"EXP:   {_player.Exp}/{_player.ExpRequired}");
            Console.WriteLine($"HP:    {_player.CurrentHP}/{_player.MaxHP}");
            Console.WriteLine($"SP:    {_player.CurrentSP}/{_player.MaxSP}");
            Console.WriteLine($"Cond:  {(_player.CurrentAilment?.Name ?? "Healthy")}");
            Console.WriteLine($"Wep:   {_player.EquippedWeapon?.Name ?? "Unarmed"}");
            Console.WriteLine($"Macca: {_economy.Macca}");

            Console.WriteLine("\n--- STATS BREAKDOWN ---");
            int GetPStat(StatType t) => _player.ActivePersona?.StatModifiers.ContainsKey(t) == true ? _player.ActivePersona.StatModifiers[t] : 0;

            string personaName = _player.ActivePersona?.Name ?? "None";
            Console.WriteLine($"Persona: {personaName} (Lv.{_player.ActivePersona?.Level ?? 0})");
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
    }
}