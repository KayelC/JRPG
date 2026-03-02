using JRPGPrototype.Services;
using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Subscriber (Observer) for the Battle System.
    /// It listens to the ActionProcessor's broadcasts and renders them to the Console via IGameIO.
    /// This keeps the visual representation completely separate from the mathematical calculation.
    /// </summary>
    public class BattleLogger
    {
        private readonly IGameIO _io;

        public BattleLogger(IGameIO io)
        {
            _io = io;
        }

        // Hooks into the logic engine to start receiving updates.
        public void Subscribe(ActionProcessor processor)
        {
            processor.OnActionPerformed += HandleBattleMessage;
        }

        /// <summary>
        /// Unhooks from the logic engine. 
        /// Important in Game Dev to prevent memory leaks when objects are destroyed (e.g., leaving battle).
        /// </summary>
        public void Unsubscribe(ActionProcessor processor)
        {
            processor.OnActionPerformed -= HandleBattleMessage;
        }

        /// <summary>
        /// The actual event handler. 
        /// Translates the raw data (BattleMessageArgs) into user feedback (Console writes/waits).
        /// </summary>
        private void HandleBattleMessage(object sender, BattleMessageArgs e)
        {
            // 1. Render the text with the requested color
            _io.WriteLine(e.Message, e.Color);

            // 2. Handle dramatic pacing (if requested by logic)
            if (e.Delay > 0)
            {
                _io.Wait(e.Delay);
            }

            // 3. Handle forced pauses (e.g., tutorial prompts or major events)
            if (e.WaitForInput)
            {
                _io.WriteLine("Press any key...");
                _io.ReadKey();
            }
        }
    }
}