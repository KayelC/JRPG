using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Field.Bridges;

namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// The logic engine for dungeon traversal and environmental events.
    /// Handles floor transitions, procedural encounter generation, and grouping logic.
    /// </summary>
    public class ExplorationProcessor
    {
        private readonly IFieldMessenger _messenger;
        private readonly DungeonManager _dungeonManager;
        private readonly DungeonState _dungeonState;
        private readonly DungeonUIBridge _dungeonUI;
        private readonly FieldServiceEngine _serviceEngine;

        public ExplorationProcessor(
            IFieldMessenger messenger,
            DungeonManager dungeonManager,
            DungeonState dungeonState,
            DungeonUIBridge dungeonUI,
            FieldServiceEngine serviceEngine)
        {
            _messenger = messenger;
            _dungeonManager = dungeonManager;
            _dungeonState = dungeonState;
            _dungeonUI = dungeonUI;
            _serviceEngine = serviceEngine;
        }

        #region Navigation Logic

        /// <summary>
        /// Logic for moving to a higher floor.
        /// Automatically updates the MaxFloorReached flag via DungeonManager.
        /// </summary>
        public DungeonFloorResult PerformAscension()
        {
            _dungeonUI.ReportMovement(ascending: true);
            _dungeonManager.Ascend();
            return _dungeonManager.ProcessCurrentFloor();
        }

        /// <summary>
        /// Logic for moving to a lower floor.
        /// </summary>
        public DungeonFloorResult PerformDescension()
        {
            _dungeonUI.ReportMovement(ascending: false);
            _dungeonManager.Descend();
            return _dungeonManager.ProcessCurrentFloor();
        }

        /// <summary>
        /// Handles the warp transaction via Terminal.
        /// </summary>
        public DungeonFloorResult PerformWarp(int floor)
        {
            _messenger.Publish($"Warping to Floor {floor}...", delay: 1000);

            _dungeonManager.WarpToFloor(floor);
            return _dungeonManager.ProcessCurrentFloor();
        }

        #endregion

        #region Floor Trigger Processing

        /// <summary>
        /// Evaluates a new floor entry and executes immediate environmental events.
        /// Logic: Handles terminal unlocks, safe-room announcements, and boss alerts.
        /// </summary>
        public ExplorationEvent ProcessFloorEntry(DungeonFloorResult floorInfo)
        {
            // 1. Handle Persistent Terminal Unlocks
            if (floorInfo.HasTerminal)
            {
                _serviceEngine.UnlockTerminal(floorInfo.FloorNumber);
            }

            // 2. Process Environmental Type
            switch (floorInfo.Type)
            {
                case DungeonEventType.SafeRoom:
                    // Only report if it's not the lobby (which has its own logic)
                    if (floorInfo.FloorNumber != 1)
                    {
                        _dungeonUI.ReportSafeRoom();
                    }
                    return ExplorationEvent.None;

                case DungeonEventType.Battle:
                    // Return encounter event for the Conductor to handle
                    return ExplorationEvent.Encounter;

                case DungeonEventType.Boss:
                    _dungeonUI.ReportBossRoom();
                    return ExplorationEvent.BossEncounter;

                case DungeonEventType.BlockEnd:
                    // Handled as part of the navigation menu, no immediate trigger
                    return ExplorationEvent.None;

                default:
                    return ExplorationEvent.None;
            }
        }

        #endregion

        #region Encounter Preparation

        /// <summary>
        /// Translates raw Enemy IDs into a hydrated list of Combatants using the new factory.
        /// Feature: SMT Grouping Logic (Pixie A, Pixie B).
        /// </summary>
        public List<Combatant> PrepareEncounter(List<string> enemyIds)
        {
            List<Combatant> enemies = new List<Combatant>();

            // 1. Hydrate the combatants using the programmatic factory method in Combatant.cs
            foreach (string id in enemyIds)
            {
                enemies.Add(Combatant.CreateEnemy(id));
            }

            // 2. High-Fidelity Naming Logic (Grouping)
            // Groups enemies by name and appends alphabetical suffixes if duplicates exist.
            var groups = enemies.GroupBy(e => e.Name);
            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    int counter = 0;
                    foreach (var enemy in group)
                    {
                        // Assign alphabetical suffix based on occurrence (A, B, C...)
                        enemy.Name += $" {(char)('A' + counter)}";
                        counter++;
                    }
                }
            }

            return enemies;
        }

        #endregion
    }
}