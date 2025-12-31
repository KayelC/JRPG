using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JRPGPrototype
{
    public class FieldManager
    {
        private Combatant _player;
        private InventoryManager _inventory;
        private EconomyManager _economy;

        public FieldManager(Combatant player, InventoryManager inventory, EconomyManager economy)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
        }

        public void NavigateMenus()
        {
            bool stayInMenu = true;
            while (stayInMenu)
            {
                Console.Clear();
                Console.WriteLine("\n=== FIELD PREPARATION MENU ===");
                Console.WriteLine($"Macca: {_economy.Macca}");
                Console.WriteLine("------------------------------");
                Console.WriteLine($"[1] Use Item");
                Console.WriteLine($"[2] Use Skill");
                Console.WriteLine($"[3] Equipment");
                Console.WriteLine($"[4] Status");

                if (_player.StatPoints > 0)
                    Console.WriteLine($"[!] Allocate Stats ({_player.StatPoints} pts)");
                else
                    Console.WriteLine($"[5] Allocate Stats (0 pts)");

                Console.WriteLine($"[6] Proceed to Battle");
                Console.Write("> ");

                string input = Console.ReadLine();
                switch (input)
                {
                    case "1": ShowItemMenu(); break;
                    case "2": ShowSkillMenu(); break;
                    case "3": ShowEquipMenu(); break;
                    case "4": ShowStatus(); break;
                    case "5":
                    case "!":
                        if (_player.StatPoints > 0) StatAllocationModule.OpenMenu(_player);
                        else Console.WriteLine("No points to allocate.");
                        break;
                    case "6": stayInMenu = false; break;
                    default: Console.WriteLine("Invalid selection."); break;
                }

                if (stayInMenu) { Console.WriteLine("\nPress Enter..."); Console.ReadLine(); }
            }
        }

        private void ShowStatus()
        {
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
            // Helper to get persona stat safely
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
            Console.WriteLine($"CHA:   {_player.CharacterStats[StatType.CHA]} (Negotiation)");
            Console.WriteLine("--------------------------");
        }

        // --- ITEM LOGIC ---
        private void ShowItemMenu()
        {
            Console.WriteLine("\n--- FIELD ITEM MENU ---");
            var items = Database.Items.Values
                .Where(i => _inventory.GetQuantity(i.Id) > 0)
                .ToList();

            if (items.Count == 0)
            {
                Console.WriteLine("Inventory is empty.");
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {items[i].Name} x{_inventory.GetQuantity(items[i].Id)} - {items[i].Description}");
            }

            Console.Write("Select Item # (or 'b' to back): ");
            string choice = Console.ReadLine();
            if (choice.ToLower() == "b") return;

            if (int.TryParse(choice, out int idx) && idx > 0 && idx <= items.Count)
            {
                UseItemField(items[idx - 1].Id, _player);
            }
        }

        private void UseItemField(string itemId, Combatant target)
        {
            if (!_inventory.HasItem(itemId))
            {
                Console.WriteLine("Error: Item not in inventory.");
                return;
            }

            ItemData item = Database.Items[itemId];
            bool used = false;

            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    if (target.CurrentHP >= target.MaxHP) { Console.WriteLine("HP is already full."); }
                    else
                    {
                        int old = target.CurrentHP;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                        Console.WriteLine($"Used {item.Name}. Restored {target.CurrentHP - old} HP.");
                        used = true;
                    }
                    break;

                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP) { Console.WriteLine("SP is already full."); }
                    else
                    {
                        int old = target.CurrentSP;
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        Console.WriteLine($"Used {item.Name}. Restored {target.CurrentSP - old} SP.");
                        used = true;
                    }
                    break;

                case "Cure":
                    if (target.CurrentAilment == null) { Console.WriteLine("Target is healthy."); }
                    else
                    {
                        bool cured = false;
                        if (item.Name == "Dis-Poison" && target.CurrentAilment.Name == "Poison") cured = true;
                        else if (item.Name == "Patra Card")
                        {
                            if (target.CheckCure("Cure All")) cured = true;
                        }

                        if (cured)
                        {
                            Console.WriteLine($"Used {item.Name}. Cured {target.CurrentAilment?.Name ?? "Ailment"}!");
                            target.RemoveAilment();
                            used = true;
                        }
                        else Console.WriteLine("This item does not cure the current ailment.");
                    }
                    break;

                default:
                    Console.WriteLine($"Cannot use {item.Name} in the field.");
                    break;
            }

            if (used) _inventory.RemoveItem(itemId, 1);
        }

        // --- SKILL LOGIC ---
        private void ShowSkillMenu()
        {
            Console.WriteLine("\n--- FIELD SKILL MENU ---");
            var skills = _player.ActivePersona.SkillSet;
            var usableSkills = new List<string>();

            foreach (var sName in skills)
            {
                if (Database.Skills.TryGetValue(sName, out var sData))
                {
                    if (sData.Category.Contains("Recovery") ||
                        sData.Effect.Contains("restore") ||
                        sData.Effect.Contains("Cure") ||
                        sData.Effect.Contains("Dispel"))
                    {
                        usableSkills.Add(sName);
                    }
                }
            }

            if (usableSkills.Count == 0)
            {
                Console.WriteLine("No field-usable skills available.");
                return;
            }

            for (int i = 0; i < usableSkills.Count; i++)
            {
                Database.Skills.TryGetValue(usableSkills[i], out var sData);
                Console.WriteLine($"{i + 1}. {usableSkills[i]} ({sData.Cost}) - {sData.Effect}");
            }

            Console.Write("Select Skill # (or 'b' to back): ");
            string choice = Console.ReadLine();
            if (choice.ToLower() == "b") return;

            if (int.TryParse(choice, out int idx) && idx > 0 && idx <= usableSkills.Count)
            {
                string sName = usableSkills[idx - 1];
                if (Database.Skills.TryGetValue(sName, out var sData))
                {
                    UseSkillField(sData, _player, _player);
                }
            }
        }

        private void UseSkillField(SkillData skill, Combatant user, Combatant target)
        {
            var cost = skill.ParseCost();
            if (cost.isHP) { if (user.CurrentHP <= cost.value) { Console.WriteLine("Not enough HP!"); return; } }
            else { if (user.CurrentSP < cost.value) { Console.WriteLine("Not enough SP!"); return; } }

            bool effectApplied = false;

            if (skill.Effect.Contains("restores", StringComparison.OrdinalIgnoreCase))
            {
                if (target.CurrentHP >= target.MaxHP) { Console.WriteLine("HP is already max!"); return; }

                int healAmount = 50;
                if (skill.Effect.Contains("Slightly")) healAmount = 50;
                else if (skill.Effect.Contains("Moderately")) healAmount = 150;
                else if (skill.Effect.Contains("Fully")) healAmount = 9999;

                var match = Regex.Match(skill.Effect, @"\((\d+)\)");
                if (match.Success) int.TryParse(match.Groups[1].Value, out healAmount);

                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                Console.WriteLine($"[{user.Name}] cast {skill.Name}! Restored HP to {target.CurrentHP}/{target.MaxHP}.");
                effectApplied = true;
            }
            else if (skill.Effect.Contains("Cure") || skill.Effect.Contains("Dispels"))
            {
                if (target.CurrentAilment == null) { Console.WriteLine("No ailment to cure!"); return; }
                if (target.CheckCure(skill.Effect))
                {
                    Console.WriteLine($"[{user.Name}] cast {skill.Name}! Ailment removed.");
                    effectApplied = true;
                }
                else { Console.WriteLine("Skill had no effect on this ailment."); return; }
            }

            if (effectApplied)
            {
                if (cost.isHP) user.CurrentHP -= cost.value;
                else user.CurrentSP -= cost.value;
            }
        }

        // --- EQUIPMENT ---
        private void ShowEquipMenu()
        {
            Console.WriteLine("\n--- EQUIPMENT MENU ---");
            if (_inventory.OwnedWeapons.Count == 0)
            {
                Console.WriteLine("No weapons in inventory.");
                return;
            }

            for (int i = 0; i < _inventory.OwnedWeapons.Count; i++)
            {
                string wId = _inventory.OwnedWeapons[i];
                if (Database.Weapons.TryGetValue(wId, out var w))
                {
                    string equipped = (_player.EquippedWeapon?.Id == wId) ? " [E]" : "";
                    Console.WriteLine($"{i + 1}. {w.Name} (Pow: {w.Power}, Acc: {w.Accuracy}){equipped}");
                }
            }

            Console.Write("Select Weapon # to Equip (or 'b' to back): ");
            string choice = Console.ReadLine();
            if (choice.ToLower() == "b") return;

            if (int.TryParse(choice, out int idx) && idx > 0 && idx <= _inventory.OwnedWeapons.Count)
            {
                string newId = _inventory.OwnedWeapons[idx - 1];
                if (Database.Weapons.TryGetValue(newId, out var newWep))
                {
                    _player.EquippedWeapon = newWep;
                    Console.WriteLine($"Equipped {newWep.Name}!");
                }
            }
        }
    }
}