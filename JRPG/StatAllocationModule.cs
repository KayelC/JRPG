using System;

namespace JRPGPrototype
{
    public static class StatAllocationModule
    {
        public static void OpenMenu(Combatant player)
        {
            while (player.StatPoints > 0)
            {
                Console.Clear();
                Console.WriteLine($"=== STAT ALLOCATION (Points: {player.StatPoints}) ===");
                Console.WriteLine("Select a stat to increase:");

                Console.WriteLine($"[1] STR: {player.CharacterStats[StatType.STR]}");
                Console.WriteLine($"[2] MAG: {player.CharacterStats[StatType.MAG]}");
                Console.WriteLine($"[3] END: {player.CharacterStats[StatType.END]} (+5 MaxHP bonus)");
                Console.WriteLine($"[4] AGI: {player.CharacterStats[StatType.AGI]}");
                Console.WriteLine($"[5] LUK: {player.CharacterStats[StatType.LUK]}");
                Console.WriteLine($"[6] INT: {player.CharacterStats[StatType.INT]} (+3 MaxSP bonus)");
                Console.WriteLine($"[7] CHA: {player.CharacterStats[StatType.CHA]} (Social/Negotiation)");
                Console.WriteLine("[0] Exit");

                Console.Write("> ");
                string input = Console.ReadLine();

                if (input == "0") break;

                StatType? selected = input switch
                {
                    "1" => StatType.STR,
                    "2" => StatType.MAG,
                    "3" => StatType.END,
                    "4" => StatType.AGI,
                    "5" => StatType.LUK,
                    "6" => StatType.INT,
                    "7" => StatType.CHA,
                    _ => null
                };

                if (selected.HasValue)
                {
                    player.AllocateStat(selected.Value);
                    Console.WriteLine($"Increased {selected.Value}!");
                }
            }
            Console.WriteLine("Allocation complete or paused.");
        }
    }
}