using System;

namespace JRPGPrototype.Logic
{
    /// <summary>
    /// A simple static system to track the lunar cycle.
    /// This is a critical input for the NegotiationEngine.
    /// </summary>
    public static class MoonPhaseSystem
    {
        // 0 = New, 4 = Half, 8 = Full
        private static int _currentPhase = 0;

        public static int CurrentPhase => _currentPhase;

        /// <summary>
        /// Advances the moon phase by one step.
        /// Called by the FieldManager when moving between floors or resting.
        /// </summary>
        public static void Advance()
        {
            _currentPhase = (_currentPhase + 1) % 9; // Cycle from 0 to 8
        }

        /// <summary>
        /// Returns a string representation of the current moon phase.
        /// </summary>
        public static string GetPhaseName()
        {
            return _currentPhase switch
            {
                0 => "New Moon",
                1 => "Waxing Crescent 1/8",
                2 => "Waxing Crescent 2/8",
                3 => "Waxing Crescent 3/8",
                4 => "Half Moon",
                5 => "Waxing Gibbous 5/8",
                6 => "Waxing Gibbous 6/8",
                7 => "Waxing Gibbous 7/8",
                8 => "Full Moon",
                _ => "Unknown Phase"
            };
        }

        /// <summary>
        /// High-Fidelity Rule: Negotiation is impossible on a Full Moon.
        /// </summary>
        public static bool IsNegotiationBlocked()
        {
            return _currentPhase == 8;
        }
    }
}