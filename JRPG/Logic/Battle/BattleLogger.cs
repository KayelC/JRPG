using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Subscriber (Observer) for the Battle System.
    /// It listens to the centralized Messenger and renders data to the Console.
    /// This keeps the visual representation completely separate from the logic.
    /// </summary>
    public class BattleLogger
    {
        private readonly IGameIO _io;

        public BattleLogger(IGameIO io)
        {
            _io = io;
        }

        public void Subscribe(IBattleMessenger messenger)
        {
            messenger.OnMessagePublished += HandleBattleMessage;
        }

        public void Unsubscribe(IBattleMessenger messenger)
        {
            messenger.OnMessagePublished -= HandleBattleMessage;
        }

        // Global entry point for all battle messages.
        private void HandleBattleMessage(object sender, BattleMessageArgs e)
        {
            // If the message contains a target, perform a full Analysis UI render
            if (e.AnalysisTarget != null)
            {
                HandleAnalysisDisplay(e.AnalysisTarget);
                return;
            }

            // Otherwise, render a standard log line
            if (!string.IsNullOrEmpty(e.Message))
            {
                _io.WriteLine(e.Message, e.Color);
            }

            // 2. Handle dramatic pacing (if requested by logic)
            if (e.Delay > 0)
            {
                _io.Wait(e.Delay);
            }

            // 3. Handle forced pauses (e.g., tutorial prompts or major events)
            if (e.WaitForInput)
            {
                _io.WriteLine("Press any key to continue...", ConsoleColor.Gray);
                _io.ReadKey();
            }
        }

        // Handles the complex multi-line rendering of an enemy's stat sheet.
        private void HandleAnalysisDisplay(Combatant target)
        {
            _io.Clear();
            _io.WriteLine($"=== ANALYSIS: {target.Name} ===", ConsoleColor.Yellow);

            _io.WriteLine($"Level: {target.Level} | HP: {target.CurrentHP}/{target.MaxHP} | SP: {target.CurrentSP}/{target.MaxSP}");
            _io.WriteLine("--------------------------------------------------");
            _io.WriteLine("Affinities:");

            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;

                // The UI reads the data from the ActivePersona
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;

                _io.Write($"  {elem,-10}: ");

                ConsoleColor affColor = aff switch
                {
                    Affinity.Weak => ConsoleColor.Red,
                    Affinity.Resist => ConsoleColor.Green,
                    Affinity.Null => ConsoleColor.Cyan,
                    Affinity.Repel => ConsoleColor.Blue,
                    Affinity.Absorb => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };

                _io.WriteLine($"{aff}", affColor);
            }

            _io.WriteLine("--------------------------------------------------");
            _io.WriteLine("Press any key to return to battle...", ConsoleColor.Gray);
            _io.ReadKey();
        }
    }
}