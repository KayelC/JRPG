using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    public class InventoryManager
    {
        // Items (ID -> Quantity)
        private Dictionary<string, int> _inventory = new Dictionary<string, int>();

        // Equipment Lists (IDs of owned gear)
        public List<string> OwnedWeapons { get; private set; } = new List<string>();
        public List<string> OwnedArmor { get; private set; } = new List<string>();
        public List<string> OwnedBoots { get; private set; } = new List<string>();
        public List<string> OwnedAccessories { get; private set; } = new List<string>();

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

        public int GetQuantity(string itemId) => _inventory.ContainsKey(itemId) ? _inventory[itemId] : 0;

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

        public List<string> GetAllItemIds() => _inventory.Keys.ToList();

        // --- Equipment Management ---
        public void AddEquipment(string id, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Weapon:
                    if (!OwnedWeapons.Contains(id) && Database.Weapons.ContainsKey(id)) OwnedWeapons.Add(id);
                    break;
                case ShopCategory.Armor:
                    if (!OwnedArmor.Contains(id) && Database.Armors.ContainsKey(id)) OwnedArmor.Add(id);
                    break;
                case ShopCategory.Boots:
                    if (!OwnedBoots.Contains(id) && Database.Boots.ContainsKey(id)) OwnedBoots.Add(id);
                    break;
                case ShopCategory.Accessory:
                    if (!OwnedAccessories.Contains(id) && Database.Accessories.ContainsKey(id)) OwnedAccessories.Add(id);
                    break;
            }
        }

        public void RemoveEquipment(string id, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Weapon: OwnedWeapons.Remove(id); break;
                case ShopCategory.Armor: OwnedArmor.Remove(id); break;
                case ShopCategory.Boots: OwnedBoots.Remove(id); break;
                case ShopCategory.Accessory: OwnedAccessories.Remove(id); break;
            }
        }
    }
}