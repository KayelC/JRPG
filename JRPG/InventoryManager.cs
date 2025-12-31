using System;
using System.Collections.Generic;

namespace JRPGPrototype
{
    public class InventoryManager
    {
        private Dictionary<string, int> _inventory = new Dictionary<string, int>();

        public void AddItem(string itemId, int quantity)
        {
            if (Database.Items.ContainsKey(itemId))
            {
                if (!_inventory.ContainsKey(itemId))
                    _inventory[itemId] = 0;
                _inventory[itemId] += quantity;
            }
        }

        public bool UseItem(string itemId, Combatant target)
        {
            if (!_inventory.ContainsKey(itemId) || _inventory[itemId] <= 0)
            {
                Console.WriteLine("Item not found or empty!");
                return false;
            }

            if (!Database.Items.TryGetValue(itemId, out var item))
                return false;

            bool used = false;

            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    // Assuming for Healing_All, the loop is handled externally if multiple targets
                    // Here we apply to the single target passed in.
                    int oldHp = target.CurrentHP;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                    Console.WriteLine($"{target.Name} recovered {target.CurrentHP - oldHp} HP.");
                    used = true;
                    break;

                case "Spirit":
                    int oldSp = target.CurrentSP;
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                    Console.WriteLine($"{target.Name} recovered {target.CurrentSP - oldSp} SP.");
                    used = true;
                    break;

                case "Revive":
                    if (target.CurrentHP <= 0)
                    {
                        // Revive logic: EffectValue is usually percentage in Persona, JSON says 50 or 100.
                        // Assuming 50 means 50% HP.
                        int reviveHp = (int)(target.MaxHP * (item.EffectValue / 100.0));
                        target.CurrentHP = Math.Max(1, reviveHp);
                        Console.WriteLine($"{target.Name} is revived with {target.CurrentHP} HP!");
                        used = true;
                    }
                    else
                    {
                        Console.WriteLine("Target is not dead!");
                    }
                    break;

                case "Cure":
                    if (target.CurrentAilment != null)
                    {
                        // If effect_value is 0, it relies on Description logic usually, 
                        // but specifically checks "Dis-Poison" vs "Patra".
                        // Logic based on Item ID or Name is often needed if Type is generic "Cure".
                        // Simulating based on Name for specificity or cure all if generic.

                        bool cures = false;
                        if (item.Name == "Dis-Poison" && target.CurrentAilment.Name == "Poison") cures = true;
                        else if (item.Name == "Patra Card" && (target.CurrentAilment.Name == "Fear" || target.CurrentAilment.Name == "Panic" || target.CurrentAilment.Name == "Distress")) cures = true;

                        if (cures)
                        {
                            Console.WriteLine($"{target.Name} is cured of {target.CurrentAilment.Name}!");
                            target.RemoveAilment();
                            used = true;
                        }
                        else
                        {
                            Console.WriteLine("It had no effect.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Target has no ailment.");
                    }
                    break;

                default:
                    Console.WriteLine($"Item type {item.Type} not fully implemented.");
                    break;
            }

            if (used)
            {
                _inventory[itemId]--;
                if (_inventory[itemId] <= 0) _inventory.Remove(itemId);
                return true;
            }

            return false;
        }
    }
}