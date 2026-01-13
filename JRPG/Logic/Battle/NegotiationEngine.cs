using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// Represents the possible outcomes of a negotiation attempt.
    /// </summary>
    public enum NegotiationResult
    {
        InProgress,
        Success,
        Failure,
        Trick,
        Flee
    }

    /// <summary>
    /// Manages the state and flow of the SMT III-style negotiation mini-game.
    /// Uses Arcana-driven personalities and a global question pool.
    /// </summary>
    public class NegotiationEngine
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly Random _rnd = new Random();

        private static readonly Dictionary<string, PersonalityType> ArcanaToPersonality = new Dictionary<string, PersonalityType>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fool", PersonalityType.Childlike }, { "Magician", PersonalityType.Arrogant },
            { "Priestess", PersonalityType.Timid }, { "Empress", PersonalityType.Sultry },
            { "Emperor", PersonalityType.Arrogant }, { "Hierophant", PersonalityType.Honorable },
            { "Lovers", PersonalityType.Sultry }, { "Chariot", PersonalityType.Childlike },
            { "Justice", PersonalityType.Honorable }, { "Hermit", PersonalityType.Timid },
            { "Fortune", PersonalityType.Upbeat }, { "Strength", PersonalityType.Honorable },
            { "Hanged Man", PersonalityType.Gloomy }, { "Death", PersonalityType.Gloomy },
            { "Temperance", PersonalityType.Formal }, { "Devil", PersonalityType.Sultry },
            { "Tower", PersonalityType.Arrogant }, { "Star", PersonalityType.Upbeat },
            { "Moon", PersonalityType.Timid }, { "Sun", PersonalityType.Upbeat },
            { "Judgement", PersonalityType.Formal }
        };

        public NegotiationEngine(IGameIO io, PartyManager party, InventoryManager inventory, EconomyManager economy)
        {
            _io = io;
            _party = party;
            _inventory = inventory;
            _economy = economy;
        }

        public NegotiationResult StartNegotiation(Combatant actor, Combatant target, List<Combatant> enemies)
        {
            // 1. Check Environmental & State Blockers
            if (MoonPhaseSystem.IsNegotiationBlocked())
            {
                _io.WriteLine($"The {target.Name} is agitated due to the Full Moon and cannot be reasoned with!", ConsoleColor.Red);
                _io.Wait(1000);
                return NegotiationResult.Failure;
            }

            if (!_party.HasOpenDemonStockSlot(actor))
            {
                _io.WriteLine("Your Demon Stock is full! You cannot recruit anyone else.", ConsoleColor.Yellow);
                _io.Wait(1000);
                return NegotiationResult.Failure;
            }

            if (!CheckNegotiationChance(enemies.Count(e => !e.IsDead)))
            {
                _io.WriteLine($"{target.Name} is on guard and refuses to talk!");
                _io.Wait(800);
                return NegotiationResult.Failure;
            }

            // 3. Determine Personality & Fetch Questions
            string arcana = target.ActivePersona?.Arcana ?? "Fool";
            PersonalityType personality = ArcanaToPersonality.GetValueOrDefault(arcana, PersonalityType.Childlike);

            var questionPool = Database.NegotiationQuestions.Questions.GetValueOrDefault(personality, new List<NegotiationQuestion>());
            if (!questionPool.Any())
            {
                _io.WriteLine($"{target.Name} seems unresponsive...");
                _io.Wait(800);
                return NegotiationResult.Failure;
            }

            int moodScore = 0;
            int questionsToAsk = 2;

            // 4. The Conversation Loop
            for (int i = 0; i < questionsToAsk; i++)
            {
                var question = questionPool[_rnd.Next(questionPool.Count)];
                var answerLabels = question.Answers.Select(a => a.Text).ToList();

                int choice = _io.RenderMenu($"{target.Name}: \"{question.Text}\"", answerLabels, 0);

                if (choice == -1)
                {
                    _io.WriteLine($"{target.Name} seems disappointed...");
                    return NegotiationResult.Failure;
                }

                moodScore += question.Answers[choice].Value;
            }

            // 5. The Demand & Resolution Phase
            if (moodScore >= 3)
            {
                _io.WriteLine($"{target.Name} seems pleased with your answers.");
                return ProcessDemands(actor, target);
            }
            else if (moodScore > 0)
            {
                _io.WriteLine($"{target.Name} is considering your words...");
                return NegotiationResult.Flee;
            }
            else
            {
                _io.WriteLine($"{target.Name} grows angry!", ConsoleColor.Red);
                _io.Wait(800);
                return NegotiationResult.Failure;
            }
        }

        private bool CheckNegotiationChance(int livingEnemyCount)
        {
            if (livingEnemyCount <= 1) return true;
            if (livingEnemyCount == 2) return _rnd.Next(0, 100) < 75;
            if (livingEnemyCount == 3) return _rnd.Next(0, 100) < 50;
            if (livingEnemyCount >= 4) return _rnd.Next(0, 100) < 25;
            return false;
        }

        private NegotiationResult ProcessDemands(Combatant actor, Combatant target)
        {
            // FIX: High-Fidelity SMT V Macca Demand Formula
            double baseCost = Math.Pow(target.Level, 2) * 10;
            double luckDiscount = baseCost * ((double)actor.GetStat(StatType.LUK) / 100.0);
            int maccaDemand = (int)Math.Max(target.Level * 5, baseCost - luckDiscount);

            var itemDemand = _inventory.GetAllItemIds().FirstOrDefault(id => Database.Items[id].Type == "Healing");

            _io.WriteLine($"{target.Name}: \"Your words are intriguing. But talk is cheap.\"");

            if (_rnd.Next(0, 100) < 50)
            {
                if (maccaDemand > 0 && _economy.Macca >= maccaDemand)
                {
                    var options = new List<string> { $"Give {maccaDemand} Macca", "Refuse" };
                    int choice = _io.RenderMenu($"{target.Name}: \"A gift of {maccaDemand} Macca should suffice.\"", options, 0);
                    if (choice == 0)
                    {
                        _economy.SpendMacca(maccaDemand);
                        return NegotiationResult.Success;
                    }
                }
            }
            else
            {
                if (itemDemand != null)
                {
                    string itemName = Database.Items[itemDemand].Name;
                    var options = new List<string> { $"Give {itemName}", "Refuse" };
                    int choice = _io.RenderMenu($"{target.Name}: \"A {itemName} would be lovely.\"", options, 0);
                    if (choice == 0)
                    {
                        _inventory.RemoveItem(itemDemand, 1);
                        return NegotiationResult.Success;
                    }
                }
            }

            if (_rnd.Next(0, 100) < 50)
            {
                _io.WriteLine($"{target.Name}: \"Hmph. You waste my time.\"");
                return NegotiationResult.Trick;
            }

            return NegotiationResult.Failure;
        }
    }
}