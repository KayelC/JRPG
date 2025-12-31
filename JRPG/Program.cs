using System;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            Database.LoadData();

            // Set IDs from your JSON
            string playerPersonaId = "orpheus";
            string enemyPersonaId = "jack-frost";

            // --- Player Character (The Operator) ---
            Combatant player = new Combatant("Hero");
            player.CharacterStats[StatType.STR] = 12;
            player.CharacterStats[StatType.MAG] = 10;
            player.CharacterStats[StatType.END] = 15;
            player.CharacterStats[StatType.AGI] = 12;
            player.CharacterStats[StatType.LUK] = 10;
            player.CharacterStats[StatType.INT] = 14; // Affects SP
            player.CharacterStats[StatType.CHA] = 10; // Affects Social (Future)

            if (Database.Personas.TryGetValue(playerPersonaId, out var pData))
            {
                player.ActivePersona = pData.ToPersona();
                player.RecalculateResources();
                Console.WriteLine($"[SYSTEM] {player.Name} synchronized with {player.ActivePersona.Name}.");
            }

            // --- Enemy Shadow ---
            Combatant enemy = new Combatant("Shadow");
            if (Database.Personas.TryGetValue(enemyPersonaId, out var eData))
            {
                enemy.Name = eData.Name;
                enemy.ActivePersona = eData.ToPersona();

                // Scale enemy operator stats to their level
                int baseStat = 10 + (eData.Level / 5);
                foreach (StatType s in Enum.GetValues(typeof(StatType)))
                    enemy.CharacterStats[s] = baseStat;

                enemy.RecalculateResources();
            }

            // Start
            if (player.ActivePersona != null && enemy.ActivePersona != null)
                new BattleManager(player, enemy).StartBattle();
        }
    }
}