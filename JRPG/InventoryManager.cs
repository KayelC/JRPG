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

        // Optional: Helper to check if item exists and has stock
        public bool HasItem(string itemId)
        {
            return GetQuantity(itemId) > 0;
        }
    }
}