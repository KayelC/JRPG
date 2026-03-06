using System;

namespace JRPGPrototype.Logic.Fusion.Messaging
{
    /// <summary>
    /// A centralized messenger that handles the distribution of fusion-related events.
    /// Eliminates code duplication by providing a single broadcast point.
    /// </summary>
    public class FusionMessenger : IFusionMessenger
    {
        public event EventHandler<FusionMessageArgs> OnMessagePublished;

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            // Broadcast the packet to all subscribers (Observers)
            OnMessagePublished?.Invoke(this, new FusionMessageArgs(
                message,
                color,
                delay,
                waitForInput,
                clearScreen));
        }
    }
}