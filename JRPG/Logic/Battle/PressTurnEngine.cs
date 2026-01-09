using System;
using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Resource State Engine for the Press Turn System.
    /// Manages Turn Icons using a High-Fidelity SMT III: Nocturne state machine.
    /// Tracks Full (Solid) and Blinking (Flashing) icons independently to allow Turn Chaining.
    /// </summary>
    public class PressTurnEngine
    {
        // Internal state counters representing the Nocturne Icon Queue.
        private int _fullIcons;
        private int _blinkingIcons;
        private int _initialMemberCount;

        /// <summary>
        /// Gets the current number of Solid icons.
        /// </summary>
        public int FullIcons => _fullIcons;

        /// <summary>
        /// Gets the current number of Flashing icons.
        /// </summary>
        public int BlinkingIcons => _blinkingIcons;

        /// <summary>
        /// SMT III Rule: Initialize the phase with one full icon per active, alive member.
        /// </summary>
        /// <param name="activeMemberCount">Number of combatants capable of acting this phase.</param>
        public void StartPhase(int activeMemberCount)
        {
            _initialMemberCount = Math.Max(0, activeMemberCount);
            _fullIcons = _initialMemberCount;
            _blinkingIcons = 0;
        }

        /// <summary>
        /// Checks if there is any action potential left in the current phase.
        /// </summary>
        public bool HasTurnsRemaining() => (_fullIcons + _blinkingIcons) > 0;

        /// <summary>
        /// Total count of icons visible on the bar, regardless of state.
        /// </summary>
        public int GetTotalIconCount() => _fullIcons + _blinkingIcons;

        /// <summary>
        /// Logic for consuming icons based on the outcome of an action.
        /// Adheres to strict SMT III: Nocturne chaining rules.
        /// </summary>
        /// <param name="hitType">The affinity/hit result from CombatMath.</param>
        /// <param name="isCritical">Whether the attack resulted in a critical hit.</param>
        public void ConsumeAction(HitType hitType, bool isCritical)
        {
            if (!HasTurnsRemaining()) return;

            // SMT III RULE 1: Repel or Absorb results in PHASE TERMINATION.
            // Turn ends immediately, losing all icons.
            if (hitType == HitType.Repel || hitType == HitType.Absorb)
            {
                TerminatePhase();
                return;
            }

            // SMT III RULE 2: Miss or Null (Block) results in a penalty of 2 ICONS.
            // This consumes the active icon and the one following it.
            if (hitType == HitType.Miss || hitType == HitType.Null)
            {
                ConsumeIconsInternal(2);
                return;
            }

            // SMT III RULE 3: Weakness or Critical results in Turn Chaining.
            // Chaining: If a Full icon is available ANYWHERE on the bar, it is converted 
            // to Blinking. This happens even if a Blinking icon is currently lead.
            if (hitType == HitType.Weakness || isCritical)
            {
                if (_fullIcons > 0)
                {
                    _fullIcons--;
                    _blinkingIcons++;
                }
                else
                {
                    _blinkingIcons--;
                }
                return;
            }

            // SMT III RULE 4: Normal Action.
            // Consumes the current active icon. 
            // Priority: Blinking icons are used first if they are at the front of the queue.
            if (_blinkingIcons > 0)
            {
                _blinkingIcons--;
            }
            else
            {
                _fullIcons--;
            }

            // Ensure we don't fall into negative states.
            if (_fullIcons < 0) _fullIcons = 0;
            if (_blinkingIcons < 0) _blinkingIcons = 0;
        }

        /// <summary>
        /// SMT III Rule: Passing a turn.
        /// Passing on a Solid icon [O] converts it to Blinking [X].
        /// Passing on a Blinking icon [X] consumes it entirely.
        /// Chaining is NOT available for Passing.
        /// </summary>
        public void Pass()
        {
            if (!HasTurnsRemaining()) return;

            // In Nocturne, Passing always targets the current active icon.
            // If the current lead icon is blinking, it is destroyed.
            if (_blinkingIcons > 0)
            {
                _blinkingIcons--;
            }
            // If the lead icon is solid, it becomes blinking (moves to back).
            else if (_fullIcons > 0)
            {
                _fullIcons--;
                _blinkingIcons++;
            }
        }

        /// <summary>
        /// Internal helper to handle multi-icon penalties (like Miss/Null).
        /// SMT III logic: Take from blinking icons first, then solid.
        /// </summary>
        private void ConsumeIconsInternal(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_blinkingIcons > 0)
                {
                    _blinkingIcons--;
                }
                else if (_fullIcons > 0)
                {
                    _fullIcons--;
                }
            }
        }

        /// <summary>
        /// Forced termination of the phase.
        /// </summary>
        public void TerminatePhase()
        {
            _fullIcons = 0;
            _blinkingIcons = 0;
        }

        /// <summary>
        /// Pure logic for displaying icons. 
        /// Returns a formatted string for the console, but is GUI-ready 
        /// since it relies on the internal icon properties.
        /// </summary>
        public string GetIconsDisplay()
        {
            if (!HasTurnsRemaining()) return "[EMPTY]";

            List<string> icons = new List<string>();

            // Nocturne UI shows solid icons first, then the earned blinking ones.
            for (int i = 0; i < _fullIcons; i++)
            {
                icons.Add("[O]");
            }

            for (int i = 0; i < _blinkingIcons; i++)
            {
                icons.Add("[X]");
            }

            return string.Join(" ", icons);
        }
    }
}