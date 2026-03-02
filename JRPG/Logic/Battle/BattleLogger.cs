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

        /// <summary>
        /// Translates BattleMessageArgs into physical output.
        /// Handles Clearing, Writing, Pacing, and Analysis screens.
        /// </summary>
        private void HandleBattleMessage(object sender, BattleMessageArgs e)
        {
            // 1. Handle screen clear requests from Logic
            if (e.ClearScreen)
            {
                _io.Clear();
            }

            // 2. Handle full Analysis stat-sheet rendering
            if (e.AnalysisTarget != null)
            {
                HandleAnalysisDisplay(e.AnalysisTarget);
                return;
            }

            // 3. Standard Logging Logic
            if (!string.IsNullOrEmpty(e.Message))
            {
                _io.WriteLine(e.Message, e.Color);
            }

            // 4. Handle Delay requests
            if (e.Delay > 0)
            {
                _io.Wait(e.Delay);
            }

            // 5. Handle forced user acknowledgments
            if (e.WaitForInput)
            {
                _io.WriteLine("Press any key to continue...", ConsoleColor.Gray);
                _io.ReadKey();
            }
        }

        /// <summary>
        /// Handles the complex multi-line rendering of an enemy's stat sheet.
        /// Moved from ActionProcessor to maintain UI/Logic separation.
        /// </summary>
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