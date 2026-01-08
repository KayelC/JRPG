using System;
using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle
{
    public class TurnSystem
    {
        // 2 Ticks = 1 Full Icon. 1 Tick = 1 Blinking Icon.
        private int _remainingTicks;
        private int _initialIconCount;

        /// <summary>
        /// SMT III Rule: Initialize icons based on active, alive party members.
        /// </summary>
        public void StartPhase(int activeMemberCount)
        {
            _initialIconCount = activeMemberCount;
            _remainingTicks = activeMemberCount * 2;
        }

        public bool IsPhaseOver() => _remainingTicks <= 0;

        public int GetRemainingIcons() => (int)Math.Ceiling(_remainingTicks / 2.0);

        /// <summary>
        /// Handles the Press Turn Icon consumption based on SMT III fidelity.
        /// </summary>
        public void HandleActionResults(HitType hitType, bool isCritical)
        {
            if (IsPhaseOver()) return;

            // SMT III Priority Logic:
            // 1. Repel/Absorb = LOSE EVERYTHING
            if (hitType == HitType.Repel || hitType == HitType.Absorb)
            {
                _remainingTicks = 0;
                return;
            }

            // 2. Miss/Null = LOSE 2 ICONS (4 Ticks)
            if (hitType == HitType.Miss || hitType == HitType.Null)
            {
                _remainingTicks -= 4;
                if (_remainingTicks < 0) _remainingTicks = 0;
                return;
            }

            // 3. Weakness/Critical = Consumes HALF an icon (1 Tick)
            // Note: If an icon is already "Blinking" (1 tick left), it just finishes it.
            if (hitType == HitType.Weakness || isCritical)
            {
                _remainingTicks -= 1;
                return;
            }

            // 4. Normal Action = Consumes 1 FULL icon (2 Ticks)
            // If the current icon is "Blinking" (1 tick left), we consume that 1 tick.
            // If it is "Solid", we consume 2 ticks to remove the icon entirely.
            if (_remainingTicks % 2 != 0)
            {
                _remainingTicks -= 1; // Consume the blinking part
            }
            else
            {
                _remainingTicks -= 2; // Consume a full solid icon
            }

            if (_remainingTicks < 0) _remainingTicks = 0;
        }

        /// <summary>
        /// SMT III Pass Rule: Passing on a Solid icon flips it to Blinking and moves it to the back.
        /// Passing on a Blinking icon consumes it.
        /// </summary>
        public void Pass()
        {
            if (IsPhaseOver()) return;

            if (_remainingTicks % 2 == 0)
            {
                // It's a solid icon. We "spend" half of it to make it blinking.
                _remainingTicks -= 1;
            }
            else
            {
                // It's already blinking. Passing consumes the rest.
                _remainingTicks -= 1;
            }
        }

        public string GetIconsDisplay()
        {
            if (_remainingTicks <= 0) return "[EMPTY]";

            string display = "";
            int tempTicks = _remainingTicks;

            // We represent the SMT III UI: [O] for Solid, [X] for Blinking
            while (tempTicks > 0)
            {
                if (tempTicks >= 2)
                {
                    display += "[O] ";
                    tempTicks -= 2;
                }
                else
                {
                    display += "[X] ";
                    tempTicks -= 1;
                }
            }
            return display.Trim();
        }
    }
}