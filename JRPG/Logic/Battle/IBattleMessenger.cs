using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// Defines a centralized contract for broadcasting battle events.
    /// This satisfies the "Interfaces" requirement from your professor.
    /// </summary>
    public interface IBattleMessenger
    {
        // The central event that observers (like the Logger) subscribe to.
        event EventHandler<BattleMessageArgs> OnMessagePublished;

        // Common method to send a message into the event pipeline.
        void Publish(string message, ConsoleColor color = ConsoleColor.Gray, int delay = 0, bool waitForInput = false, Combatant analysisTarget = null, bool clearScreen = false);
    }
}