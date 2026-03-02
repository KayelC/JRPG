using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// A centralized messenger that handles the distribution of battle events.
    /// This eliminates code duplication by providing a single 'Publish' logic for the whole system.
    /// </summary>
    public class BattleMessenger : IBattleMessenger
    {
        public event EventHandler<BattleMessageArgs> OnMessagePublished;

        public void Publish(string message, ConsoleColor color = ConsoleColor.Gray, int delay = 0, bool waitForInput = false, Combat analysisTarget = null, bool clearScreen = false)
        {
            // Broadcast to anyone listening
            OnMessagePublished?.Invoke(this, new BattleMessageArgs(message, color, delay, waitForInput, analysisTarget, clearScreen));
        }
    }
}