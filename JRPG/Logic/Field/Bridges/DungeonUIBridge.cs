using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Specialized UI Bridge for Dungeon Exploration (Tartarus).
    /// Handles navigation prompts, floor selection, and environmental feedback.
    /// </summary>
    public class DungeonUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;

        public DungeonUIBridge(IGameIO io, FieldUIState uiState)
        {
            _io = io;
            _uiState = uiState;
        }

        #region Dungeon HUD and Main Actions

        /// <summary>
        /// Renders the main exploration HUD and returns the user's selected action.
        /// Logic: Conditionally adds options based on floor number, floor type, and terminal presence.
        /// </summary>
        public string ShowFloorActionMenu(DungeonFloorResult floorInfo, Combatant player)
        {
            string header = $"=== TARTARUS: {floorInfo.BlockName.ToUpper()} ===\n" +
                            $"Floor: {floorInfo.FloorNumber}\n" +
                            $"Info: {floorInfo.Description}\n" +
                            $"HP: {player.CurrentHP,3}/{player.MaxHP,3} | SP: {player.CurrentSP,3}/{player.MaxSP,3}";

            List<string> options = new List<string>();

            // 1. Navigation Logic
            if (floorInfo.Type != DungeonEventType.BlockEnd)
            {
                options.Add("Ascend Stairs");
            }
            else
            {
                options.Add("Barrier (Cannot Pass)");
            }

            if (floorInfo.FloorNumber > 1)
            {
                options.Add("Descend Stairs");
            }

            // 2. Floor-Specific Features
            if (floorInfo.FloorNumber == 1)
            {
                options.Add("Clock (Heal)");
                options.Add("Terminal (Warp)");
                options.Add("Return to City");
            }
            else if (floorInfo.HasTerminal)
            {
                options.Add("Access Terminal (Return)");
            }

            // 3. Global Field Actions
            options.Add("Inventory");
            options.Add("Status");

            if (player.Class == ClassType.Operator)
            {
                options.Add("Organize Party");
            }

            // Ensure the cursor index doesn't exceed the newly built list size
            if (_uiState.DungeonMenuIndex >= options.Count) _uiState.DungeonMenuIndex = 0;

            int choice = _io.RenderMenu(header, options, _uiState.DungeonMenuIndex);

            if (choice == -1) return "Cancel";

            _uiState.DungeonMenuIndex = choice;
            return options[choice];
        }

        #endregion

        #region Entry Point and Warp UI

        /// <summary>
        /// Renders the menu to select a starting floor from unlocked terminals.
        /// Feature: Distinct labeling for the Lobby (Floor 1).
        /// </summary>
        public int? SelectEntryPoint(List<int> unlockedTerminals)
        {
            List<string> options = new List<string>();
            foreach (int t in unlockedTerminals)
            {
                options.Add(t == 1 ? "Lobby (Entrance)" : $"Floor {t}");
            }
            options.Add("Cancel");

            int choice = _io.RenderMenu("=== SELECT ENTRY POINT ===", options, 0);

            if (choice == -1 || choice == options.Count - 1) return null;

            return unlockedTerminals[choice];
        }

        /// <summary>
        /// Specialized menu for the Terminal System (Warping).
        /// Identifies the current floor as a disabled option to prevent redundant warps.
        /// </summary>
        public int? SelectWarpDestination(List<int> unlockedTerminals, int currentFloor)
        {
            List<string> labels = new List<string>();
            List<bool> disabledList = new List<bool>();

            foreach (int f in unlockedTerminals)
            {
                string name = (f == 1) ? "Lobby" : $"Floor {f}";
                bool isCurrent = (f == currentFloor);

                labels.Add(isCurrent ? $"{name} (Current)" : name);
                disabledList.Add(isCurrent);
            }

            labels.Add("Cancel");
            disabledList.Add(false);

            int choice = _io.RenderMenu("=== TERMINAL SYSTEM ===", labels, 0, disabledList);

            if (choice == -1 || choice == labels.Count - 1) return null;

            return unlockedTerminals[choice];
        }

        #endregion

        #region Environmental Feedback

        /// <summary>
        /// Visual/Audio feedback for entering a SafeRoom.
        /// </summary>
        public void ReportSafeRoom()
        {
            _io.WriteLine("The air here is calm.", ConsoleColor.Green);
            _io.Wait(800);
        }

        /// <summary>
        /// High-alert feedback for approaching a Boss room.
        /// </summary>
        public void ReportBossRoom()
        {
            _io.WriteLine("!!! POWERFUL SHADOW DETECTED !!!", ConsoleColor.Red);
            _io.Wait(1000);
        }

        /// <summary>
        /// Feedback for a successful escape or boss defeat.
        /// </summary>
        public void ReportBossDefeated()
        {
            _io.WriteLine("The Guardian has been defeated!", ConsoleColor.Cyan);
            _io.Wait(1500);
        }

        /// <summary>
        /// Feedback for a navigation action.
        /// </summary>
        public void ReportMovement(bool ascending)
        {
            _io.WriteLine(ascending ? "Ascending..." : "Descending...");
            _io.Wait(500);
        }

        /// <summary>
        /// Feedback for being blocked by a block barrier.
        /// </summary>
        public void ReportBarrierBlocked()
        {
            _io.WriteLine("The path is sealed.", ConsoleColor.Gray);
            _io.Wait(1000);
        }

        #endregion
    }
}