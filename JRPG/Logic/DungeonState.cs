using System.Collections.Generic;

namespace JRPGPrototype.Logic
{
    public class DungeonState
    {
        public string CurrentDungeonId { get; set; } = "tartarus";
        public int CurrentFloor { get; set; } = 1;
        public int MaxFloorReached { get; set; } = 1;
        public HashSet<int> UnlockedTerminals { get; set; } = new HashSet<int>() { 1 };
        public HashSet<string> DefeatedBosses { get; set; } = new HashSet<string>();

        public void UnlockTerminal(int floor) { if (!UnlockedTerminals.Contains(floor)) UnlockedTerminals.Add(floor); }
        public void MarkBossDefeated(string bossId) { if (!DefeatedBosses.Contains(bossId)) DefeatedBosses.Add(bossId); }
        public bool IsBossDefeated(string bossId) => DefeatedBosses.Contains(bossId);
        public void ResetToEntry() => CurrentFloor = 1;
    }
}