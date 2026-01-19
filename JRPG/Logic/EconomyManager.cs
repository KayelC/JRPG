using System;

namespace JRPGPrototype.Logic
{
    public class EconomyManager
    {
        public int Macca { get; private set; } = 0;

        public void AddMacca(int amount)
        {
            Macca += amount;
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