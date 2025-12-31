using System;
using System.Collections.Generic;

namespace JRPGPrototype
{
    public class InventoryManager
    {
        // Items (ID -> Quantity)
        private Dictionary<string, int> _inventory = new Dictionary<string, int>();

        // Weapons (List of IDs)
        public List<string> OwnedWeapons { get; private set; } = new List<string>();

        // --- Item Management ---
        public void AddItem(string itemId, int quantity)
        {
            if (Database.Items.ContainsKey(itemId))
            {
                if (!_inventory.ContainsKey(itemId))
                    _inventory[itemId] = 0;
                _inventory[itemId] += quantity;
            }
        }

        public int GetQuantity(string itemId)
        {
            return _inventory.ContainsKey(itemId) ? _inventory[itemId] : 0;
        }

        public void RemoveItem(string itemId, int quantity)
        {
            if (_inventory.ContainsKey(itemId))
            {
                _inventory[itemId] -= quantity;
                if (_inventory[itemId] <= 0)
                    _inventory.Remove(itemId);
            }
        }

        public bool HasItem(string itemId) => GetQuantity(itemId) > 0;

        // --- Weapon Management ---
        public void AddWeapon(string weaponId)
        {
            if (Database.Weapons.ContainsKey(weaponId) && !OwnedWeapons.Contains(weaponId))
            {
                OwnedWeapons.Add(weaponId);
            }
        }
    }
}