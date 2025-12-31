namespace JRPGPrototype
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

            return Element.Almighty; // Default for Almighty, Ailment, Recovery, etc.
        }
    }
}