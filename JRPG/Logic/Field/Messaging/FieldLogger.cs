using System;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Field.Messaging
{
    /// <summary>
    /// The Observer/Subscriber for Field-based events.
    /// Responsible for taking abstract FieldMessageArgs and rendering them via IGameIO.
    /// This decouples the Logic Engines from the physical Console.
    /// </summary>
    public class FieldLogger
    {
        private readonly IGameIO _io;
        private readonly IFieldMessenger _messenger;

        /// <summary>
        /// Initializes the FieldLogger and subscribes to the messenger's events.
        /// </summary>
        /// <param name="io">The Hardware Driver for Console I/O.</param>
        /// <param name="messenger">The Mediator/Broadcaster for Field events.</param>
        public FieldLogger(IGameIO io, IFieldMessenger messenger)
        {
            _io = io;
            _messenger = messenger;

            // Subscribe to the message event
            _messenger.OnMessagePublished += HandleMessagePublished;
        }

        // Core Event Handler. Translates FieldMessageArgs into sequence of IGameIO calls.
        private void HandleMessagePublished(object? sender, FieldMessageArgs e)
        {
            // 1. Handle Screen Clearing
            if (e.ClearScreen)
            {
                _io.Clear();
            }

            // 2. Handle Text Output
            if (!string.IsNullOrEmpty(e.Message))
            {
                _io.WriteLine(e.Message, e.Color);
            }

            // 3. Handle Timing/Delays
            if (e.Delay > 0)
            {
                _io.Wait(e.Delay);
            }

            // 4. Handle Interaction Blocks
            if (e.WaitForInput)
            {
                _io.ReadKey(intercept: true);
            }
        }

        /// <summary>
        /// Unsubscribes the logger from the messenger to prevent memory leaks.
        /// Should be called when the Field Conductor is being shut down.
        /// </summary>
        public void Deactivate()
        {
            _messenger.OnMessagePublished -= HandleMessagePublished;
        }
    }
}