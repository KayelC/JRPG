using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    public class DungeonFloorResult
    {
        public int FloorNumber { get; set; }
        public string BlockName { get; set; }
        public DungeonEventType Type { get; set; }
        public string Description { get; set; }
        public string EnemyId { get; set; }
        public bool HasTerminal { get; set; }
    }

    public class DungeonManager
    {
        private DungeonState _state;
        private DungeonData _data;
        private Random _rnd = new Random();

        public DungeonManager(DungeonState state)
        {
            _state = state;

            if (Database.Dungeons.TryGetValue(_state.CurrentDungeonId, out var dungeonData))
            {
                _data = dungeonData;
            }
            else
            {
                _data = new DungeonData
                {
                    Name = "Unknown Void",
                    Blocks = new List<BlockData>()
                };
            }
        }

        public int CurrentFloor => _state.CurrentFloor;

        // --- NAVIGATION ---
        public void Ascend()
        {
            _state.CurrentFloor++;
            if (_state.CurrentFloor > _state.MaxFloorReached)
            {
                _state.MaxFloorReached = _state.CurrentFloor;
            }
        }

        public void Descend()
        {
            if (_state.CurrentFloor > 1) _state.CurrentFloor--;
        }

        public void WarpToFloor(int floor)
        {
            _state.CurrentFloor = floor;
        }

        // --- CORE LOGIC ---
        public DungeonFloorResult ProcessCurrentFloor()
        {
            // 0. Handle Lobby (Floor 1)
            // Tartarus blocks start at Floor 2 in JSON, so Floor 1 is special.
            if (_state.CurrentFloor == 1)
            {
                return new DungeonFloorResult
                {
                    FloorNumber = 1,
                    BlockName = "Entrance",
                    Type = DungeonEventType.SafeRoom,
                    Description = "The Lobby. A large clock ticks quietly.",
                    HasTerminal = true // Lobby always has a terminal
                };
            }

            var result = new DungeonFloorResult
            {
                FloorNumber = _state.CurrentFloor,
                BlockName = "Unknown Block",
                Type = DungeonEventType.Empty,
                Description = "A quiet corridor in the tower."
            };

            // 1. Identify Current Block
            var block = GetCurrentBlock();
            if (block == null)
            {
                result.Description = "You are outside the known map.";
                return result;
            }

            result.BlockName = block.Name;

            // 2. Check for Fixed Floors WITHIN this Block
            var fixedData = block.FixedFloors?.FirstOrDefault(f => f.Floor == _state.CurrentFloor);

            if (fixedData != null)
            {
                result.Description = fixedData.Description;
                result.HasTerminal = fixedData.HasTerminal;

                if (fixedData.HasTerminal)
                {
                    _state.UnlockTerminal(_state.CurrentFloor);
                }

                switch (fixedData.Type)
                {
                    case "Boss":
                        if (_state.IsBossDefeated(fixedData.Id))
                        {
                            result.Type = DungeonEventType.Empty;
                            result.Description = "The area is quiet. The guardian has been defeated.";
                        }
                        else
                        {
                            result.Type = DungeonEventType.Boss;
                            result.EnemyId = fixedData.Id;
                        }
                        break;
                    case "SafeRoom":
                        result.Type = DungeonEventType.SafeRoom;
                        break;
                    case "BlockEnd":
                        result.Type = DungeonEventType.BlockEnd;
                        break;
                    default:
                        result.Type = DungeonEventType.Empty;
                        break;
                }
                return result;
            }

            // 3. Random Encounters
            result.Type = DungeonEventType.Battle;
            result.Description = "Shadows lurk in the darkness...";
            result.EnemyId = GetRandomEnemyFromBlock(block);

            return result;
        }

        public List<int> GetUnlockedTerminals()
        {
            return _state.UnlockedTerminals.OrderBy(x => x).ToList();
        }

        public void RegisterBossDefeat(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId))
            {
                _state.MarkBossDefeated(bossId);
            }
        }

        private BlockData GetCurrentBlock()
        {
            return _data.Blocks.FirstOrDefault(b =>
                b.FloorRange != null &&
                b.FloorRange.Length >= 2 &&
                _state.CurrentFloor >= b.FloorRange[0] &&
                _state.CurrentFloor <= b.FloorRange[1]);
        }

        private string GetRandomEnemyFromBlock(BlockData block)
        {
            if (block.EnemyPool == null || block.EnemyPool.Count == 0) return "E_slime";
            return block.EnemyPool[_rnd.Next(block.EnemyPool.Count)];
        }
    }
}