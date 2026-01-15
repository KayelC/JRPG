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
        Flee,
        FamiliarFlee
    }

    /// <summary>
    /// Manages the state and flow of the negotiation mini-game.
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

            // Check against the specific actor initiating the talk
            if (_party.IsDemonOwned(actor, target.SourceId))
            {
                return HandleFamiliarDemon(actor, target);
            }

            if (!_party.HasOpenDemonStockSlot(actor))
            {
                _io.WriteLine("Your Demon Stock is full!");
                _io.Wait(1000);
                return NegotiationResult.Failure;
            }

            if (!CheckNegotiationChance(enemies.Count(e => !e.IsDead)))
            {
                _io.WriteLine($"{target.Name} is on guard and refuses to talk!");
                _io.Wait(800);
                return NegotiationResult.Failure;
            }

            // Determine Personality & Fetch Questions
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
            // Create a session-specific copy of the pool to prevent repeated questions.
            var sessionQuestions = new List<NegotiationQuestion>(questionPool);

            // Increased difficulty by requiring 3 successful rounds instead of 2.
            for (int i = 0; i < 3; i++)
            {
                if (!sessionQuestions.Any()) break;

                // Pick a question and remove it from the session pool immediately.
                int qIdx = _rnd.Next(sessionQuestions.Count);
                var question = sessionQuestions[qIdx];
                sessionQuestions.RemoveAt(qIdx);

                int choice = _io.RenderMenu($"{target.Name}: \"{question.Text}\"", question.Answers.Select(a => a.Text).ToList(), 0);

                if (choice == -1)
                {
                    _io.WriteLine($"{target.Name} seems disappointed...");
                    return NegotiationResult.Failure;
                }

                moodScore += question.Answers[choice].Value;
            }

            // The Demand & Resolution Phase
            // Note: moodScore requirements adjusted for 3 rounds (max 6, success threshold 4).
            if (moodScore >= 4)
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

        private NegotiationResult HandleFamiliarDemon(Combatant actor, Combatant target)
        {
            string dialogue = $"{target.Name} looks at you with a sense of familiarity...";
            if (Database.Personas.TryGetValue(target.SourceId, out var pData) && !string.IsNullOrEmpty(pData.FamiliarDialogue))
            {
                dialogue = $"{target.Name}: \"{pData.FamiliarDialogue}\"";
            }
            else
            {
                string arcana = target.ActivePersona?.Arcana ?? "Fool";
                PersonalityType personality = ArcanaToPersonality.GetValueOrDefault(arcana, PersonalityType.Childlike);
                var dialogues = Database.NegotiationQuestions.FamiliarDialogues.GetValueOrDefault(personality, new List<string>());
                if (dialogues.Any())
                {
                    dialogue = $"{target.Name}: \"{dialogues[_rnd.Next(dialogues.Count)]}\"";
                }
            }

            _io.WriteLine(dialogue, ConsoleColor.Cyan);

            int roll = _rnd.Next(0, 100);
            if (roll < 50)
            {
                _io.WriteLine($"{target.Name} gives you a Medicine and departs.");
                _inventory.AddItem("101", 1);
            }
            else if (roll < 80)
            {
                int macca = target.Level * 20;
                _io.WriteLine($"{target.Name} gives you {macca} Macca and departs.");
                _economy.AddMacca(macca);
            }
            else
            {
                _io.WriteLine($"{target.Name} casts a gentle light upon your party before departing.");
                foreach (var member in _party.GetAliveMembers())
                {
                    member.CurrentHP = (int)Math.Min(member.MaxHP, member.CurrentHP + (member.MaxHP * 0.15));
                }
            }
            return NegotiationResult.FamiliarFlee;
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
            // Macca Demand Formula
            if (target.Level > actor.Level)
            {
                _io.WriteLine($"{target.Name}: \"You have courage, but you are not yet worthy to command me. Perhaps we shall meet again.\"");
                return NegotiationResult.Flee;
            }

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