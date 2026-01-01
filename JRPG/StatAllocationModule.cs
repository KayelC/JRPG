using System;
using System.Collections.Generic;
using System.Threading;

namespace JRPGPrototype
{
    public static class StatAllocationModule
    {
        public static void OpenMenu(Combatant player)
        {
            int currentIndex = 0;

            while (player.StatPoints > 0)
            {
                List<string> options = new List<string>();
                foreach (StatType s in Enum.GetValues(typeof(StatType)))
                {
                    int val = player.CharacterStats[s];
                    options.Add($"{s}: {val}");
                }

                int idx = MenuUI.RenderMenu($"=== STAT ALLOCATION (Pts: {player.StatPoints}) ===", options, currentIndex, null, (index) =>
                {
                    StatType s = (StatType)index;
                    string bonus = "";
                    if (s == StatType.END) bonus = "+5 MaxHP";
                    else if (s == StatType.INT) bonus = "+3 MaxSP";
                    else if (s == StatType.STR) bonus = "Phys Dmg";
                    else if (s == StatType.MAG) bonus = "Magic Dmg";
                    else if (s == StatType.CHA) bonus = "Negotiation/Shop";

                    Console.WriteLine($"Highlight: {s}");
                    Console.WriteLine($"Current: {player.CharacterStats[s]}");
                    Console.WriteLine($"Bonus: {bonus}");
                });

                if (idx != -1)
                {
                    player.AllocateStat((StatType)idx);
                    currentIndex = idx; // Keep selection
                    Console.WriteLine("Stat Increased!");
                    Thread.Sleep(200);
                }
                else return;
            }
        }
    }
}