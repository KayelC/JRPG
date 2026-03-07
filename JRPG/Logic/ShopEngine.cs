using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic
{
    /// <summary>
    /// Pure Logic Engine for Shop transactions.
    /// Handles mathematical calculations, Luck-based pricing, and state mutations 
    /// for Inventory and Economy. Routinely publishes narrative logs to the Messenger.
    /// </summary>
    public class ShopEngine
    {
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly IFieldMessenger _messenger;

        public ShopEngine(InventoryManager inventory, EconomyManager economy, IFieldMessenger messenger)
        {
            _inventory = inventory;
            _economy = economy;
            _messenger = messenger;
        }

        #region Price Calculations

        /// <summary>
        /// Calculates the final purchase price based on the base price and the player's Luck.
        /// Formula: Multiplier = Max(0.5, 1.0 - (Luck * 0.01))
        /// </summary>
        public int CalculateBuyPrice(ShopEntry entry, Combatant player)
        {
            int luk = player.GetStat(StatType.Lu);
            double discountMult = Math.Max(0.5, 1.0 - (luk * 0.01));
            return (int)(entry.BasePrice * discountMult);
        }

        /// <summary>
        /// Calculates the final selling price based on the base price and the player's Luck.
        /// Formula: Multiplier = 0.50 + (Luck * 0.01)
        /// </summary>
        public int CalculateSellPrice(string id, ShopCategory cat, Combatant player)
        {
            var entry = Database.ShopInventory.FirstOrDefault(e => e.Id == id && e.Category == cat);
            int basePrice = entry?.BasePrice ?? 100;

            int luk = player.GetStat(StatType.Lu);
            double sellMult = 0.50 + (luk * 0.01);
            return (int)(basePrice * sellMult);
        }

        #endregion

        #region Transaction Execution

        /// <summary>
        /// Validates funds and executes the purchase of an item or piece of equipment.
        /// Includes a safety check to ensure Name/Id hydration.
        /// </summary>
        public bool ExecutePurchase(ShopEntry entry, Combatant player)
        {
            int finalPrice = CalculateBuyPrice(entry, player);

            if (_economy.Macca < finalPrice)
            {
                _messenger.Publish("\nNot enough Macca!", ConsoleColor.Gray, 800);
                return false;
            }

            if (_economy.SpendMacca(finalPrice))
            {
                switch (entry.Category)
                {
                    case ShopCategory.Weapon:
                    case ShopCategory.Armor:
                    case ShopCategory.Boots:
                    case ShopCategory.Accessory:
                        _inventory.AddEquipment(entry.Id, entry.Category);
                        break;
                    case ShopCategory.Item:
                        _inventory.AddItem(entry.Id, 1);
                        break;
                }

                _messenger.Publish("\nBought!", ConsoleColor.Gray, 500);
                return true;
            }

            return false;
        }

        // Executes the sale of a player-owned item or unequipped piece of equipment.
        public void ExecuteSale(string id, ShopCategory category, Combatant player)
        {
            int price = CalculateSellPrice(id, category, player);

            if (category == ShopCategory.Item)
            {
                _inventory.RemoveItem(id, 1);
            }
            else
            {
                _inventory.RemoveEquipment(id, category);
            }

            _economy.AddMacca(price);
            _messenger.Publish("\nSold!", ConsoleColor.Gray, 500);
        }

        #endregion

        #region Inspection Logic

        /// <summary>
        /// Generates the descriptive and statistical strings for an item.
        /// Patches Name and Id if the Database object is unhydrated.
        /// </summary>
        public (string desc, string stats) GetItemDetails(ShopEntry entry)
        {
            string desc = "";
            string stats = "";

            switch (entry.Category)
            {
                case ShopCategory.Weapon:
                    var w = Database.Weapons[entry.Id];
                    if (string.IsNullOrEmpty(w.Name)) w.Name = entry.Name;
                    if (string.IsNullOrEmpty(w.Id)) w.Id = entry.Id;
                    desc = $"Type: {w.Type}";
                    stats = $"Pow: {w.Power} Acc: {w.Accuracy}";
                    break;
                case ShopCategory.Armor:
                    var a = Database.Armors[entry.Id];
                    if (string.IsNullOrEmpty(a.Name)) a.Name = entry.Name;
                    if (string.IsNullOrEmpty(a.Id)) a.Id = entry.Id;
                    desc = a.Description;
                    stats = $"DEF: {a.Defense} EVA: {a.Evasion}";
                    break;
                case ShopCategory.Boots:
                    var b = Database.Boots[entry.Id];
                    if (string.IsNullOrEmpty(b.Name)) b.Name = entry.Name;
                    if (string.IsNullOrEmpty(b.Id)) b.Id = entry.Id;
                    desc = b.Description;
                    stats = $"EVA: {b.Evasion}";
                    break;
                case ShopCategory.Accessory:
                    var acc = Database.Accessories[entry.Id];
                    if (string.IsNullOrEmpty(acc.Name)) acc.Name = entry.Name;
                    if (string.IsNullOrEmpty(acc.Id)) acc.Id = entry.Id;
                    desc = acc.Description;
                    stats = $"{acc.ModifierStat} +{acc.ModifierValue}";
                    break;
                case ShopCategory.Item:
                    var i = Database.Items[entry.Id];
                    if (string.IsNullOrEmpty(i.Name)) i.Name = entry.Name;
                    if (string.IsNullOrEmpty(i.Id)) i.Id = entry.Id;
                    desc = i.Description;
                    stats = $"Effect: {i.EffectValue}";
                    break;
            }

            return (desc, stats);
        }

        #endregion
    }
}