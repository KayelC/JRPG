using System.Collections.Generic;

namespace JRPGPrototype
{
    public class DungeonState
    {
        public string CurrentDungeonId { get; set; } = "tartarus";

        // The floor the player is currently standing on.
        // We default to 1 (Lobby) when not exploring.
        public int CurrentFloor { get; set; } = 1;

        // Tracks the deepest floor reached to allow logic checks (e.g. "Can I warp here?")
        public int MaxFloorReached { get; set; } = 1;

        // Tracks unlocked terminals (Floor IDs)
        // We initialize with 1 so the Lobby is always a valid warp point.
        public HashSet<int> UnlockedTerminals { get; set; } = new HashSet<int>() { 1 };

        // Tracks defeated bosses (Enemy IDs) to prevent respawning fixed bosses
        public HashSet<string> DefeatedBosses { get; set; } = new HashSet<string>();

        public void UnlockTerminal(int floor)
        {
            if (!UnlockedTerminals.Contains(floor))
            {
                UnlockedTerminals.Add(floor);
            }
        }

        public void MarkBossDefeated(string bossId)
        {
            if (!DefeatedBosses.Contains(bossId))
            {
                DefeatedBosses.Add(bossId);
            }
        }

        public bool IsBossDefeated(string bossId)
        {
            return DefeatedBosses.Contains(bossId);
        }

        // --- NEW: Reset Logic ---
        public void ResetToEntry()
        {
            CurrentFloor = 1; // 1 represents the Lobby/Entrance
        }
    }
}