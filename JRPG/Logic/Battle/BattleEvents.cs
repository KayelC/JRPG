using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// Represents the payload data sent when a battle event occurs.
    /// This acts as the bridge between Logic and UI without coupling them.
    /// </summary>
    public class BattleMessageArgs : EventArgs
    {
        // The text content of the event (e.g., "Pixie attacks!", "150 Damage!").
        public string Message { get; }

        // A hint for how the UI should style this message (e.g., Red for damage, Green for healing).
        public ConsoleColor Color { get; }

        // How long (in milliseconds) the UI should pause after showing this message to create dramatic pacing.
        public int Delay { get; }

        // If true, the UI should halt execution until the player acknowledges the message.
        public bool WaitForInput { get; }

        public BattleMessageArgs(string message, ConsoleColor color = ConsoleColor.Gray, int delay = 0, bool waitForInput = false)
        {
            Message = message;
            Color = color;
            Delay = delay;
            WaitForInput = waitForInput;
        }
    }
}