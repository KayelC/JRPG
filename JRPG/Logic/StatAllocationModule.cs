using System;
using System.Collections.Generic;
using JRPGPrototype.Services;
using JRPGPrototype.Entities;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic
{
    public static class StatAllocationModule
    {
        public static void OpenMenu(Combatant player, IGameIO io)
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

                int idx = io.RenderMenu($"=== STAT ALLOCATION (Pts: {player.StatPoints}) ===", options, currentIndex, null, (index) =>
                {
                    StatType s = (StatType)index;
                    string bonus = "";
                    if (s == StatType.END) bonus = "+5 MaxHP";
                    else if (s == StatType.INT) bonus = "+3 MaxSP";
                    else if (s == StatType.STR) bonus = "Phys Dmg";
                    else if (s == StatType.MAG) bonus = "Magic Dmg";
                    else if (s == StatType.CHA) bonus = "Negotiation/Shop";

                    io.WriteLine($"Highlight: {s}");
                    io.WriteLine($"Current: {player.CharacterStats[s]}");
                    io.WriteLine($"Bonus: {bonus}");
                });

                if (idx != -1)
                {
                    player.AllocateStat((StatType)idx);
                    currentIndex = idx; // Keep selection
                    io.WriteLine("Stat Increased!");
                    io.Wait(200);
                }
                else return;
            }
        }
    }
}