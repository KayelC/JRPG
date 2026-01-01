using System;
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
            int selectedIndex = 0;
            var inventory = Database.ShopInventory;

            while (true)
            {
                // --- DRAW MENU ---
                Console.Clear();
                Console.WriteLine($"--- WEAPON & ITEM SHOP ---");
                Console.WriteLine($"Macca: {_economy.Macca}");
                Console.WriteLine("Use UP/DOWN to navigate. ENTER to inspect. ESC to exit.\n");

                for (int i = 0; i < inventory.Count; i++)
                {
                    ShopEntry entry = inventory[i];
                    string typeLabel = entry.IsWeapon ? "[WEP]" : "[ITM]";

                    if (i == selectedIndex)
                    {
                        // Highlight Selected Line
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine($"> {typeLabel} {entry.Name,-20} {entry.BasePrice,5} M");
                        Console.ResetColor();
                    }
                    else
                    {
                        // Normal Line
                        Console.WriteLine($"  {typeLabel} {entry.Name,-20} {entry.BasePrice,5} M");
                    }
                }

                // --- INPUT HANDLING ---
                ConsoleKeyInfo keyInfo = Console.ReadKey(true); // 'true' intercepts the key

                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = inventory.Count - 1; // Wrap around to bottom
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex++;
                    if (selectedIndex >= inventory.Count) selectedIndex = 0; // Wrap around to top
                }
                else if (keyInfo.Key == ConsoleKey.Escape)
                {
                    break; // Exit Shop
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    InspectAndBuy(inventory[selectedIndex], player);
                }
            }
        }

        private void InspectAndBuy(ShopEntry entry, Combatant player)
        {
            Console.Clear();
            Console.WriteLine($"--- INSPECT: {entry.Name} ---\n");

            // Fetch Details
            string desc = "";
            string stats = "";

            if (entry.IsWeapon)
            {
                if (Database.Weapons.TryGetValue(entry.Id, out var wData))
                {
                    desc = $"Type: {wData.Type} | Ranged: {(wData.IsLongRange ? "Yes" : "No")}";
                    stats = $"Power: {wData.Power} | Accuracy: {wData.Accuracy}%";
                }
                else
                {
                    desc = "Unknown Weapon Data";
                }
            }
            else
            {
                if (Database.Items.TryGetValue(entry.Id, out var iData))
                {
                    desc = iData.Description;
                    stats = $"Type: {iData.Type} | Effect: {iData.EffectValue}";
                }
                else
                {
                    desc = "Unknown Item Data";
                }
            }

            // --- DISCOUNT LOGIC ---
            int cha = player.CharacterStats[StatType.CHA];
            // Discount: 1% off per CHA point. Max 50% discount.
            double discountMult = Math.Max(0.5, 1.0 - (cha * 0.01));
            int finalPrice = (int)(entry.BasePrice * discountMult);
            int discountAmount = entry.BasePrice - finalPrice;

            Console.WriteLine($"Description: {desc}");
            Console.WriteLine($"Stats:       {stats}");
            Console.WriteLine("-----------------------------");
            Console.WriteLine($"Base Price:  {entry.BasePrice} M");

            if (discountAmount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Your Price:  {finalPrice} M (CHA Bonus: -{discountAmount} M)");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"Your Price:  {finalPrice} M");
            }

            Console.WriteLine($"Current Macca: {_economy.Macca}");
            Console.WriteLine("\n[ENTER] Buy   [ESC] Cancel");

            // --- BUY CONFIRMATION LOOP ---
            while (true)
            {
                var input = Console.ReadKey(true).Key;

                if (input == ConsoleKey.Escape)
                {
                    return; // Return to Shop List
                }
                else if (input == ConsoleKey.Enter)
                {
                    if (_economy.SpendMacca(finalPrice))
                    {
                        if (entry.IsWeapon)
                        {
                            _inventory.AddWeapon(entry.Id);
                        }
                        else
                        {
                            _inventory.AddItem(entry.Id, 1);
                        }

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\nSuccessfully bought {entry.Name}!");
                        Console.ResetColor();
                        Thread.Sleep(1000);
                        return;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nNot enough Macca! (Need {finalPrice - _economy.Macca} more)");
                        Console.ResetColor();
                        Thread.Sleep(1000);
                        return;
                    }
                }
            }
        }
    }
}