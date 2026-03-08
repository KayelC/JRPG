using System;

namespace JRPGPrototype.Logic.Field.Messaging
{
    /// <summary>
    /// Centralized contract for broadcasting Field events.
    /// Decouples Field Logic from the Console/UI implementation.
    /// </summary>
    public interface IFieldMessenger
    {
        /// <summary>
        /// Fired whenever a logic engine (ServiceEngine, ExplorationProcessor) 
        /// wants to display a message or narrative feedback to the player.
        /// </summary>
        event EventHandler<FieldMessageArgs> OnMessagePublished;

        /// <summary>
        /// Publishes a new message to any listening observers (e.g., FieldLogger).
        /// </summary>
        /// <param name="message">The text content of the message.</param>
        /// <param name="color">The color to render the text in.</param>
        /// <param name="delay">A pause in milliseconds after the message is shown.</param>
        /// <param name="waitForInput">Whether to pause the game until a key is pressed.</param>
        /// <param name="clearScreen">Whether to clear previous text before showing this message.</param>
        void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false);
    }
}