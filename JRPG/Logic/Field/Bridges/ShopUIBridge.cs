using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Interactive UI Bridge for Shop operations.
    /// Handles menu loops, user input via IGameIO, and coordinates with the ShopEngine.
    /// </summary>
    public class ShopUIBridge
    {
        private readonly IGameIO _io;
        private readonly IFieldMessenger _messenger;
        private readonly ShopEngine _engine;
        private readonly EconomyManager _economy;
        private readonly InventoryManager _inventory;

        public ShopUIBridge(
            IGameIO io,
            IFieldMessenger messenger,
            ShopEngine engine,
            EconomyManager economy,
            InventoryManager inventory)
        {
            _io = io;
            _messenger = messenger;
            _engine = engine;
            _economy = economy;
            _inventory = inventory;
        }

        // The main entry point for a shop session.
        public void OpenShop(Combatant player, ShopType shopType)
        {
            int shopIndex = 0;
            string title = shopType.ToString().ToUpper() + " SHOP";

            while (true)
            {
                string header = $"--- {title} ---\nMacca: {_economy.Macca}";
                List<string> options = new List<string> { "Buy", "Sell", "Exit" };

                int choice = _io.RenderMenu(header, options, shopIndex);

                if (choice == -1 || choice == 2) return; // Exit logic
                shopIndex = choice;

                if (choice == 0) BuyMenu(player, shopType);
                else if (choice == 1) SellMenu(player, shopType);
            }
        }

        private void BuyMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            var filteredStock = Database.ShopInventory
                .Where(e => e.Category == targetCategory)
                .ToList();

            if (filteredStock.Count == 0)
            {
                _messenger.Publish("This shop has no stock.", ConsoleColor.Gray, 800);
                return;
            }

            while (true)
            {
                List<string> options = filteredStock.Select(entry =>
                    $"{entry.Name,-18} {entry.BasePrice,5} M").ToList();

                string header = $"--- BUY ({shopType}) ---\nMacca: {_economy.Macca}";

                int idx = _io.RenderMenu(header, options, listIndex, null, (index) =>
                {
                    var entry = filteredStock[index];
                    ShowItemInspection(entry, player, isBuying: true);
                });

                if (idx == -1) return;
                listIndex = idx;

                var selected = filteredStock[idx];
                int finalPrice = _engine.CalculateBuyPrice(selected, player);

                if (ConfirmTransaction(selected.Name, finalPrice, isBuying: true))
                {
                    _engine.ExecutePurchase(selected, player);
                }
            }
        }

        private void SellMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            while (true)
            {
                List<object> sellables = GetSellableObjects(targetCategory);
                if (sellables.Count == 0)
                {
                    _messenger.Publish("Nothing to sell in this category.", ConsoleColor.Gray, 1000);
                    return;
                }

                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                foreach (var obj in sellables)
                {
                    string id = GetIdFromObject(obj);
                    string name = GetNameFromObject(obj);
                    bool equipped = IsEquipped(obj, player);
                    int price = _engine.CalculateSellPrice(id, targetCategory, player);

                    options.Add($"{name,-15}{(equipped ? " [E]" : "")} ({price} M)");
                    disabled.Add(equipped);
                }

                if (listIndex >= sellables.Count) listIndex = Math.Max(0, sellables.Count - 1);

                string header = $"--- SELL ({shopType}) ---\nMacca: {_economy.Macca}";

                int idx = _io.RenderMenu(header, options, listIndex, disabled, (index) =>
                {
                    _messenger.Publish("Selling gives 50% value + Luck Bonus.");
                });

                if (idx == -1) return;
                listIndex = idx;

                var selectedObj = sellables[idx];
                string sellId = GetIdFromObject(selectedObj);

                if (ConfirmTransaction(GetNameFromObject(selectedObj),
                    _engine.CalculateSellPrice(sellId, targetCategory, player), isBuying: false))
                {
                    _engine.ExecuteSale(sellId, targetCategory, player);
                }
            }
        }

        #region Helpers and UI Coordination

        private bool ConfirmTransaction(string name, int price, bool isBuying)
        {
            string verb = isBuying ? "Buy" : "Sell";
            _messenger.Publish($"\n{verb} {name} for {price} M?");

            int choice = _io.RenderMenu("Confirm?", new List<string> { "Yes", "No" }, 0);
            return choice == 0;
        }

        private void ShowItemInspection(ShopEntry entry, Combatant player, bool isBuying)
        {
            var (desc, stats) = _engine.GetItemDetails(entry);
            int price = isBuying ? _engine.CalculateBuyPrice(entry, player) : 0;

            _messenger.Publish($"Info: {desc}");
            _messenger.Publish($"Stats: {stats}");
            if (isBuying)
            {
                _messenger.Publish($"Price: {price} M (Base: {entry.BasePrice})");
            }
        }

        private List<object> GetSellableObjects(ShopCategory category)
        {
            List<object> list = new List<object>();
            switch (category)
            {
                case ShopCategory.Weapon:
                    foreach (var id in _inventory.OwnedWeapons)
                        if (Database.Weapons.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Armor:
                    foreach (var id in _inventory.OwnedArmor)
                        if (Database.Armors.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Boots:
                    foreach (var id in _inventory.OwnedBoots)
                        if (Database.Boots.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Accessory:
                    foreach (var id in _inventory.OwnedAccessories)
                        if (Database.Accessories.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Item:
                    foreach (var id in _inventory.GetAllItemIds())
                        if (Database.Items.TryGetValue(id, out var o)) list.Add(o);
                    break;
            }
            return list;
        }

        private string GetIdFromObject(object obj) => obj switch
        {
            WeaponData w => w.Id,
            ArmorData a => a.Id,
            BootData b => b.Id,
            AccessoryData acc => acc.Id,
            ItemData i => i.Id,
            _ => ""
        };

        private string GetNameFromObject(object obj) => obj switch
        {
            WeaponData w => w.Name,
            ArmorData a => a.Name,
            BootData b => b.Name,
            AccessoryData acc => acc.Name,
            ItemData i => i.Name,
            _ => ""
        };

        private bool IsEquipped(object obj, Combatant p) => obj switch
        {
            WeaponData w => p.EquippedWeapon?.Id == w.Id,
            ArmorData a => p.EquippedArmor?.Id == a.Id,
            BootData b => p.EquippedBoots?.Id == b.Id,
            AccessoryData acc => p.EquippedAccessory?.Id == acc.Id,
            _ => false
        };

        private ShopCategory MapTypeToCategory(ShopType type) => type switch
        {
            ShopType.Weapon => ShopCategory.Weapon,
            ShopType.Item => ShopCategory.Item,
            ShopType.Armor => ShopCategory.Armor,
            ShopType.Boots => ShopCategory.Boots,
            ShopType.Accessory => ShopCategory.Accessory,
            _ => ShopCategory.Item
        };

        #endregion
    }
}