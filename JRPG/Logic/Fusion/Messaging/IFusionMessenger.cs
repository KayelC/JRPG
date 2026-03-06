using System;

namespace JRPGPrototype.Logic.Fusion.Messaging
{
    // Defines a centralized contract for broadcasting fusion events.
    public interface IFusionMessenger
    {
        // The central event that observers (like the FusionLogger) subscribe to.
        event EventHandler<FusionMessageArgs> OnMessagePublished;

        // Common method to send a message into the fusion event pipeline.
        void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false);
    }
}