using System;

namespace JRPGPrototype
{
    public class EconomyManager
    {
        public int Macca { get; private set; } = 0;

        public void AddMacca(int amount)
        {
            Macca += amount;
            Console.WriteLine($"[Economy] Gained {amount} Macca. Total: {Macca}");
        }

        public bool SpendMacca(int amount)
        {
            if (Macca >= amount)
            {
                Macca -= amount;
                return true;
            }
            return false;
        }
    }
}