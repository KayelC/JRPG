using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace JRPGPrototype
{
    public class ShopManager
    {
        private InventoryManager _inventory;
        private EconomyManager _economy;

        public ShopManager(InventoryManager inventory, EconomyManager economy)
        {
            _inventory = inventory;
            _economy = economy;
        }

        public void OpenShop(Combatant player)
        {
            int mainIndex = 0;
            while (true)
            {
                int choice = MenuUI.RenderMenu($"--- SHOP (Macca: {_economy.Macca}) ---", new List<string> { "Buy", "Sell", "Exit" }, mainIndex);

                if (choice == 0)
                {
                    mainIndex = 0;
                    BuyMenu(player);
                }
                else if (choice == 1)
                {
                    mainIndex = 1;
                    SellMenu(player);
                }
                else return;
            }
        }

        private void BuyMenu(Combatant player)
        {
            int listIndex = 0;
            var inventory = Database.ShopInventory;
            List<string> options = new List<string>();
            foreach (var entry in inventory)
            {
                string typeLabel = entry.IsWeapon ? "[WEP]" : "[ITM]";
                options.Add($"{typeLabel} {entry.Name,-15} {entry.BasePrice,5} M");
            }

            while (true)
            {
                int idx = MenuUI.RenderMenu($"--- BUY (Macca: {_economy.Macca}) ---", options, listIndex, null, (index) =>
                {
                    var entry = inventory[index];
                    InspectItem(entry, player, true);
                });

                if (idx == -1) return;

                listIndex = idx;
                var selected = inventory[idx];
                int finalPrice = GetBuyPrice(selected, player);

                if (ConfirmPurchase(selected.Name, finalPrice))
                {
                    if (_economy.SpendMacca(finalPrice))
                    {
                        if (selected.IsWeapon) _inventory.AddWeapon(selected.Id);
                        else _inventory.AddItem(selected.Id, 1);
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

        private void SellMenu(Combatant player)
        {
            int listIndex = 0;

            while (true)
            {
                List<object> sellables = new List<object>();
                List<string> options = new List<string>();
                List<bool> disabled = new List<bool>();

                // Items
                foreach (var id in _inventory.GetAllItemIds())
                {
                    if (Database.Items.TryGetValue(id, out var item))
                    {
                        sellables.Add(item);
                        int qty = _inventory.GetQuantity(id);
                        int price = GetSellPrice(item.Id, false, player);
                        options.Add($"[ITM] {item.Name,-15} x{qty} ({price} M)");
                        disabled.Add(false);
                    }
                }

                // Weapons
                foreach (var id in _inventory.OwnedWeapons)
                {
                    if (Database.Weapons.TryGetValue(id, out var wep))
                    {
                        sellables.Add(wep);
                        bool isEquipped = (player.EquippedWeapon?.Id == id);
                        string equippedTag = isEquipped ? " [EQUIPPED]" : "";
                        int price = GetSellPrice(wep.Id, true, player);

                        options.Add($"[WEP] {wep.Name,-15}{equippedTag} ({price} M)");
                        disabled.Add(isEquipped); // Prevent selling equipped
                    }
                }

                if (sellables.Count == 0)
                {
                    Console.WriteLine("Nothing to sell.");
                    Thread.Sleep(1000);
                    return;
                }

                if (listIndex >= sellables.Count) listIndex = sellables.Count - 1;

                int idx = MenuUI.RenderMenu($"--- SELL (Macca: {_economy.Macca}) ---", options, listIndex, disabled, (index) =>
                {
                    Console.WriteLine("Selling gives 50% value + Luck Bonus.");
                });

                if (idx == -1) return;

                listIndex = idx;
                var obj = sellables[idx];

                if (obj is WeaponData w)
                {
                    _inventory.RemoveWeapon(w.Id);
                    _economy.AddMacca(GetSellPrice(w.Id, true, player));
                }
                else if (obj is ItemData i)
                {
                    _inventory.RemoveItem(i.Id, 1);
                    _economy.AddMacca(GetSellPrice(i.Id, false, player));
                }
                Console.WriteLine("\nSold!");
                Thread.Sleep(500);
            }
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

        private int GetSellPrice(string id, bool isWeapon, Combatant player)
        {
            var entry = Database.ShopInventory.FirstOrDefault(e => e.Id == id && e.IsWeapon == isWeapon);
            int basePrice = entry?.BasePrice ?? 100;
            int luk = player.GetStat(StatType.LUK);
            double sellMult = 0.50 + (luk * 0.01);
            return (int)(basePrice * sellMult);
        }

        private void InspectItem(ShopEntry entry, Combatant player, bool isBuying)
        {
            string desc = "";
            if (entry.IsWeapon)
            {
                var w = Database.Weapons[entry.Id];
                desc = $"Pow: {w.Power} | Acc: {w.Accuracy} | {(w.IsLongRange ? "Ranged" : "Melee")}";
            }
            else
            {
                var i = Database.Items[entry.Id];
                desc = i.Description;
            }

            int price = isBuying ? GetBuyPrice(entry, player) : 0;
            Console.WriteLine($"Stats: {desc}");
            if (isBuying) Console.WriteLine($"Price: {price} M (Base: {entry.BasePrice})");
        }
    }
}