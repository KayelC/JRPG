using System;

namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// Payload data for Field-based events (Exploration, Shopping, Healing).
    /// Used by the FieldMessenger to communicate narrative and feedback to the UI/Logger.
    /// </summary>
    public class FieldMessageArgs : EventArgs
    {
        public string? Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
        public bool WaitForInput { get; }
        public bool ClearScreen { get; }

        /// <summary>
        /// Initializes a new instance of the FieldMessageArgs.
        /// </summary>
        /// <param name="message">The text to be displayed to the player.</param>
        /// <param name="color">The color of the text (defaults to Gray).</param>
        /// <param name="delay">Optional pause after the message in milliseconds.</param>
        /// <param name="waitForInput">If true, the UI should wait for a keypress after rendering.</param>
        /// <param name="clearScreen">If true, the UI should clear the log area before printing.</param>
        public FieldMessageArgs(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            Message = message;
            Color = color;
            Delay = delay;
            WaitForInput = waitForInput;
            ClearScreen = clearScreen;
        }
    }
}