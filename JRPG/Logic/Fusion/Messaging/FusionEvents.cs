using System;

namespace JRPGPrototype.Logic.Fusion.Messaging
{
    /// <summary>
    /// Represents the payload data sent when a fusion event occurs.
    /// This acts as the bridge between Fusion Logic and UI without coupling them.
    /// </summary>
    public class FusionMessageArgs : EventArgs
    {
        public string? Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
        public bool WaitForInput { get; }
        public bool ClearScreen { get; }

        public FusionMessageArgs(
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