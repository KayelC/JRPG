using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace JRPGPrototype
{
    // Enum used by FieldManager to tell the shop which section to open
    public enum ShopType { Weapon, Item, Armor, Boots, Accessory }

    public class ShopManager
    {
        private InventoryManager _inventory;
        private EconomyManager _economy;

        public ShopManager(InventoryManager inventory, EconomyManager economy)
        {
            _inventory = inventory;
            _economy = economy;
        }

        public void OpenShop(Combatant player, ShopType shopType)
        {
            int shopIndex = 0;
            string title = shopType.ToString().ToUpper() + " SHOP";

            while (true)
            {
                string header = $"--- {title} ---\nMacca: {_economy.Macca}";
                List<string> options = new List<string> { "Buy", "Sell", "Exit" };

                int choice = MenuUI.RenderMenu(header, options, shopIndex);

                if (choice == -1) return;
                shopIndex = choice;

                if (choice == 0) BuyMenu(player, shopType);
                else if (choice == 1) SellMenu(player, shopType);
                else return;
            }
        }

        private void BuyMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;

            // Map the FieldManager's ShopType to the Database's ShopCategory
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            // Filter the unified list based on the new Category property
            var filteredInventory = Database.ShopInventory
                .Where(e => e.Category == targetCategory)
                .ToList();

            if (filteredInventory.Count == 0)
            {
                Console.WriteLine("This shop has no stock.");
                Thread.Sleep(800);
                return;
            }

            while (true)
            {
                List<string> options = new List<string>();
                foreach (var entry in filteredInventory)
                {
                    options.Add($"{entry.Name,-18} {entry.BasePrice,5} M");
                }

                string header = $"--- BUY ({shopType}) ---\nMacca: {_economy.Macca}";

                int idx = MenuUI.RenderMenu(header, options, listIndex, null, (index) =>
                {
                    var entry = filteredInventory[index];
                    InspectItem(entry, player, true);
                });

                if (idx == -1) return;

                listIndex = idx;
                var selected = filteredInventory[idx];
                int finalPrice = GetBuyPrice(selected, player);

                if (ConfirmPurchase(selected.Name, finalPrice))
                {
                    if (_economy.SpendMacca(finalPrice))
                    {
                        // Add to specific inventory list based on category
                        switch (selected.Category)
                        {
                            case ShopCategory.Weapon: _inventory.AddEquipment(selected.Id, ShopCategory.Weapon); break;
                            case ShopCategory.Armor: _inventory.AddEquipment(selected.Id, ShopCategory.Armor); break;
                            case ShopCategory.Boots: _inventory.AddEquipment(selected.Id, ShopCategory.Boots); break;
                            case ShopCategory.Accessory: _inventory.AddEquipment(selected.Id, ShopCategory.Accessory); break;
                            case ShopCategory.Item: _inventory.AddItem(selected.Id, 1); break;
                        }

                        Console.WriteLine("\nBought!");
                        Thread.Sleep(500);
                    }
                    else
                    {
                        Console.WriteLine("\nNot enough Macca!");
                        Thread.Sleep(800);
                    }
                }
            }
        }

        private void SellMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            while (true)
            {
                List<object> sellables = new List<object>();
                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                // Build list based on specific shop type
                if (targetCategory == ShopCategory.Weapon)
                {
                    foreach (var id in _inventory.OwnedWeapons)
                    {
                        if (Database.Weapons.TryGetValue(id, out var obj))
                        {
                            sellables.Add(obj);
                            bool equipped = player.EquippedWeapon?.Id == id;
                            options.Add($"{obj.Name,-15}{(equipped ? "[E]" : "")} ({GetSellPrice(obj.Id, targetCategory, player)} M)");
                            disabled.Add(equipped);
                        }
                    }
                }
                else if (targetCategory == ShopCategory.Armor)
                {
                    foreach (var id in _inventory.OwnedArmor)
                    {
                        if (Database.Armors.TryGetValue(id, out var obj))
                        {
                            sellables.Add(obj);
                            bool equipped = player.EquippedArmor?.Id == id;
                            options.Add($"{obj.Name,-15}{(equipped ? "[E]" : "")} ({GetSellPrice(obj.Id, targetCategory, player)} M)");
                            disabled.Add(equipped);
                        }
                    }
                }
                else if (targetCategory == ShopCategory.Boots)
                {
                    foreach (var id in _inventory.OwnedBoots)
                    {
                        if (Database.Boots.TryGetValue(id, out var obj))
                        {
                            sellables.Add(obj);
                            bool equipped = player.EquippedBoots?.Id == id;
                            options.Add($"{obj.Name,-15}{(equipped ? "[E]" : "")} ({GetSellPrice(obj.Id, targetCategory, player)} M)");
                            disabled.Add(equipped);
                        }
                    }
                }
                else if (targetCategory == ShopCategory.Accessory)
                {
                    foreach (var id in _inventory.OwnedAccessories)
                    {
                        if (Database.Accessories.TryGetValue(id, out var obj))
                        {
                            sellables.Add(obj);
                            bool equipped = player.EquippedAccessory?.Id == id;
                            options.Add($"{obj.Name,-15}{(equipped ? "[E]" : "")} ({GetSellPrice(obj.Id, targetCategory, player)} M)");
                            disabled.Add(equipped);
                        }
                    }
                }
                else // Items
                {
                    foreach (var id in _inventory.GetAllItemIds())
                    {
                        if (Database.Items.TryGetValue(id, out var obj))
                        {
                            sellables.Add(obj);
                            int qty = _inventory.GetQuantity(id);
                            options.Add($"{obj.Name,-15} x{qty} ({GetSellPrice(obj.Id, targetCategory, player)} M)");
                            disabled.Add(false);
                        }
                    }
                }

                if (sellables.Count == 0)
                {
                    Console.WriteLine("Nothing to sell in this category.");
                    Thread.Sleep(1000);
                    return;
                }

                if (listIndex >= sellables.Count) listIndex = Math.Max(0, sellables.Count - 1);

                string header = $"--- SELL ({shopType}) ---\nMacca: {_economy.Macca}";

                int idx = MenuUI.RenderMenu(header, options, listIndex, disabled, (index) =>
                {
                    Console.WriteLine("Selling gives 50% value + Luck Bonus.");
                });

                if (idx == -1) return;

                listIndex = idx;
                var selectedObj = sellables[idx];

                // Sell Execution
                string sellId = "";
                if (selectedObj is WeaponData w) sellId = w.Id;
                if (selectedObj is ArmorData a) sellId = a.Id;
                if (selectedObj is BootData b) sellId = b.Id;
                if (selectedObj is AccessoryData acc) sellId = acc.Id;
                if (selectedObj is ItemData i) sellId = i.Id;

                if (targetCategory == ShopCategory.Item)
                {
                    _inventory.RemoveItem(sellId, 1);
                }
                else
                {
                    _inventory.RemoveEquipment(sellId, targetCategory);
                }

                _economy.AddMacca(GetSellPrice(sellId, targetCategory, player));

                Console.WriteLine("\nSold!");
                Thread.Sleep(500);
            }
        }

        // --- HELPERS ---

        private ShopCategory MapTypeToCategory(ShopType type)
        {
            return type switch
            {
                ShopType.Weapon => ShopCategory.Weapon,
                ShopType.Item => ShopCategory.Item,
                ShopType.Armor => ShopCategory.Armor,
                ShopType.Boots => ShopCategory.Boots,
                ShopType.Accessory => ShopCategory.Accessory,
                _ => ShopCategory.Item
            };
        }

        private bool ConfirmPurchase(string name, int price)
        {
            Console.WriteLine($"\nBuy {name} for {price} M?");
            int choice = MenuUI.RenderMenu("Confirm?", new List<string> { "Yes", "No" });
            return choice == 0;
        }

        private int GetBuyPrice(ShopEntry entry, Combatant player)
        {
            int cha = player.CharacterStats[StatType.CHA];
            double discountMult = Math.Max(0.5, 1.0 - (cha * 0.01));
            return (int)(entry.BasePrice * discountMult);
        }

        private int GetSellPrice(string id, ShopCategory cat, Combatant player)
        {
            var entry = Database.ShopInventory.FirstOrDefault(e => e.Id == id && e.Category == cat);
            int basePrice = entry?.BasePrice ?? 100;

            int luk = player.GetStat(StatType.LUK);
            double sellMult = 0.50 + (luk * 0.01);
            return (int)(basePrice * sellMult);
        }

        private void InspectItem(ShopEntry entry, Combatant player, bool isBuying)
        {
            string desc = "";
            string stats = "";

            switch (entry.Category)
            {
                case ShopCategory.Weapon:
                    var w = Database.Weapons[entry.Id];
                    desc = $"Type: {w.Type}";
                    stats = $"Pow: {w.Power} Acc: {w.Accuracy}";
                    break;
                case ShopCategory.Armor:
                    var a = Database.Armors[entry.Id];
                    desc = a.Description;
                    stats = $"DEF: {a.Defense} EVA: {a.Evasion}";
                    break;
                case ShopCategory.Boots:
                    var b = Database.Boots[entry.Id];
                    desc = b.Description;
                    stats = $"EVA: {b.Evasion}";
                    break;
                case ShopCategory.Accessory:
                    var acc = Database.Accessories[entry.Id];
                    desc = acc.Description;
                    stats = $"{acc.ModifierStat} +{acc.ModifierValue}";
                    break;
                case ShopCategory.Item:
                    var i = Database.Items[entry.Id];
                    desc = i.Description;
                    stats = $"Effect: {i.EffectValue}";
                    break;
            }

            int price = isBuying ? GetBuyPrice(entry, player) : 0;
            Console.WriteLine($"Info: {desc}");
            Console.WriteLine($"Stats: {stats}");
            if (isBuying) Console.WriteLine($"Price: {price} M (Base: {entry.BasePrice})");
        }
    }
}