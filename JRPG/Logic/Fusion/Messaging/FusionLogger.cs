using JRPGPrototype.Services;
using System;

namespace JRPGPrototype.Logic.Fusion.Messaging
{
    /// <summary>
    /// The Subscriber (Observer) for the Fusion Sub-System.
    /// It listens to the centralized Messenger and renders data to the Console via IGameIO.
    /// </summary>
    public class FusionLogger
    {
        private readonly IGameIO _io;

        public FusionLogger(IGameIO io)
        {
            _io = io;
        }

        // Hooks into the centralized messenger tower.
        public void Subscribe(IFusionMessenger messenger)
        {
            messenger.OnMessagePublished += HandleFusionMessage;
        }

        // Unhooks to prevent memory leaks.
        public void Unsubscribe(IFusionMessenger messenger)
        {
            messenger.OnMessagePublished -= HandleFusionMessage;
        }

        // Translates FusionMessageArgs into physical Console output.
        private void HandleFusionMessage(object? sender, FusionMessageArgs e)
        {
            // 1. Handle screen clear requests
            if (e.ClearScreen)
            {
                _io.Clear();
            }

            // 2. Standard Logging Logic
            if (!string.IsNullOrEmpty(e.Message))
            {
                _io.WriteLine(e.Message, e.Color);
            }

            // 3. Pacing Logic: Handle dramatic delays (rituals need timing!)
            if (e.Delay > 0)
            {
                _io.Wait(e.Delay);
            }

            // 4. Interaction Logic: Handle forced user acknowledgments
            if (e.WaitForInput)
            {
                _io.WriteLine("\nPress any key to continue...", ConsoleColor.Gray);
                _io.ReadKey();
            }
        }
    }
}