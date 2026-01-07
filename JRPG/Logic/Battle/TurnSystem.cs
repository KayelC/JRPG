using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle
{
    public enum IconState { Solid, Blinking }

    /// <summary>
    /// Implements the high-fidelity SMT III Press Turn System.
    /// O = Solid Icon, X = Blinking Icon.
    /// </summary>
    public class TurnSystem
    {
        private List<IconState> _icons = new List<IconState>();

        /// <summary>
        /// SMT III Rule: Starting icons must match the count of active, alive members.
        /// </summary>
        public void StartPhase(int activeMemberCount)
        {
            _icons.Clear();
            for (int i = 0; i < activeMemberCount; i++)
            {
                _icons.Add(IconState.Solid);
            }
        }

        public bool HasTurnsRemaining() => _icons.Count > 0;

        /// <summary>
        /// Satisfies the orchestrator's phase check.
        /// </summary>
        public bool IsPhaseOver() => _icons.Count == 0;

        public int GetIconCount() => _icons.Count;

        /// <summary>
        /// SMT III Pass Rule:
        /// 1. Passing on a Solid icon (O) flips the current icon to Blinking (X) and moves it to the back.
        /// 2. Passing on a Blinking icon (X) consumes it entirely.
        /// </summary>
        public void Pass()
        {
            if (!HasTurnsRemaining()) return;

            if (_icons[0] == IconState.Solid)
            {
                _icons.RemoveAt(0);
                _icons.Add(IconState.Blinking); // Flip to X and move to end
            }
            else
            {
                _icons.RemoveAt(0); // Consume X
            }
        }

        /// <summary>
        /// SMT III Action Rule:
        /// - Weakness/Crit: Flip a Solid icon to Blinking. If current is already Blinking, consume it.
        /// - Normal: Consume the current icon (Solid or Blinking).
        /// - Miss/Null: Consume current icon + 1 more.
        /// - Repel/Absorb: Lose everything.
        /// </summary>
        public void HandleActionResults(HitType worstResult, bool advantageTriggered)
        {
            if (!HasTurnsRemaining()) return;

            // 1. Phase Termination (Repel/Absorb)
            if (worstResult == HitType.Repel || worstResult == HitType.Absorb)
            {
                _icons.Clear();
                return;
            }

            // 2. Heavy Penalty (Miss/Null)
            if (worstResult == HitType.Miss || worstResult == HitType.Null)
            {
                Consume(2);
                return;
            }

            // 3. Tactical Advantage (Weakness/Critical)
            // BUG FIX: Added explicit check for HitType.Weakness
            if (advantageTriggered || worstResult == HitType.Weakness)
            {
                FlipLeftmostSolid();
            }
            // 4. Standard Consumption
            else
            {
                Consume(1);
            }
        }

        private void Consume(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_icons.Count > 0) _icons.RemoveAt(0);
            }
        }

        private void FlipLeftmostSolid()
        {
            // Search for the first Solid (O) icon to flip it to Blinking (X).
            int solidIdx = _icons.FindIndex(x => x == IconState.Solid);

            if (solidIdx != -1)
            {
                _icons.RemoveAt(solidIdx);
                _icons.Add(IconState.Blinking);
            }
            else
            {
                // If only Blinking (X) icons remain, we must consume one.
                _icons.RemoveAt(0);
            }
        }

        /// <summary>
        /// Returns the visual state of icons for display.
        /// </summary>
        public string GetIconsDisplay()
        {
            if (_icons.Count == 0) return "[EMPTY]";

            string display = "";
            foreach (var state in _icons)
            {
                display += (state == IconState.Solid) ? "[O] " : "[X] ";
            }
            return display.Trim();
        }
    }
}