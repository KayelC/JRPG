using System;

namespace JRPGPrototype.Logic.Field.Messaging
{
    /// <summary>
    /// Concrete implementation of the Field Messaging system.
    /// Acts as the Broadcaster for narrative and environmental feedback events.
    /// </summary>
    public class FieldMessenger : IFieldMessenger
    {
        /// <summary>
        /// Event used by observers (like the FieldLogger) to react to new messages.
        /// </summary>
        public event EventHandler<FieldMessageArgs>? OnMessagePublished;

        /// <summary>
        /// Broadcasts a message to all subscribers.
        /// </summary>
        /// <param name="message">The text content of the message.</param>
        /// <param name="color">The color to render the text in.</param>
        /// <param name="delay">A pause in milliseconds after the message is shown.</param>
        /// <param name="waitForInput">Whether to pause the game until a key is pressed.</param>
        /// <param name="clearScreen">Whether to clear previous text before showing this message.</param>
        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            // Create the payload and notify all listeners (Observers).
            FieldMessageArgs args = new FieldMessageArgs(
                message,
                color,
                delay,
                waitForInput,
                clearScreen);

            OnMessagePublished?.Invoke(this, args);
        }
    }
}