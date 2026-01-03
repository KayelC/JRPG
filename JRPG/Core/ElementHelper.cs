using System;

namespace JRPGPrototype.Core
{
    public static class ElementHelper
    {
        public static Element FromCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return Element.Almighty;
            string cat = category.ToLower();
            if (cat.Contains("slash")) return Element.Slash;
            if (cat.Contains("strike")) return Element.Strike;
            if (cat.Contains("pierce")) return Element.Pierce;
            if (cat.Contains("fire")) return Element.Fire;
            if (cat.Contains("ice")) return Element.Ice;
            if (cat.Contains("elec")) return Element.Elec;
            if (cat.Contains("wind")) return Element.Wind;
            if (cat.Contains("light")) return Element.Light;
            if (cat.Contains("dark")) return Element.Dark;
            return Element.Almighty;
        }

        public static Element ParseElement(string input)
        {
            if (string.Equals(input, "Electric", StringComparison.OrdinalIgnoreCase)) return Element.Elec;
            if (string.Equals(input, "Darkness", StringComparison.OrdinalIgnoreCase)) return Element.Dark;
            if (Enum.TryParse(input, true, out Element elem)) return elem;
            return Element.None;
        }

        public static Affinity ParseAffinity(string input)
        {
            if (string.Equals(input, "Reflect", StringComparison.OrdinalIgnoreCase)) return Affinity.Repel;
            if (string.Equals(input, "Absorb", StringComparison.OrdinalIgnoreCase)) return Affinity.Absorb;
            if (string.Equals(input, "Block", StringComparison.OrdinalIgnoreCase)) return Affinity.Null;
            if (Enum.TryParse(input, true, out Affinity aff)) return aff;
            return Affinity.Normal;
        }
    }
}