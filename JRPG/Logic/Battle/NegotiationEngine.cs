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
        Trick, // Demon took items/Macca but didn't join
        Flee, // Demon ran away
        FamiliarFlee // Familiar demon gave gift and ran
    }

    /// <summary>
    /// Manages the state and flow of the negotiation mini-game.
    /// Uses Race-driven personalities and a global question pool.
    /// </summary>
    public class NegotiationEngine
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly Random _rnd = new Random();

        // Mapped all 32 Races to the 8 Personality Types
        private static readonly Dictionary<string, PersonalityType> RaceToPersonality =
            new Dictionary<string, PersonalityType>(StringComparer.OrdinalIgnoreCase)
        {
            // --- 1. Dark ---
            { "Foul", PersonalityType.Gloomy },
            { "Haunt", PersonalityType.Gloomy },
            { "Raptor", PersonalityType.Childlike },
            { "Tyrant", PersonalityType.Arrogant },
            { "Vile", PersonalityType.Arrogant },
            { "Wilder", PersonalityType.Timid },

            // --- 2. Light ---
            { "Avatar", PersonalityType.Honorable },
            { "Avian", PersonalityType.Upbeat },
            { "Deity", PersonalityType.Arrogant },
            { "Dragon", PersonalityType.Arrogant },
            { "Element", PersonalityType.Formal },
            { "Mitama", PersonalityType.Childlike },
            { "Entity", PersonalityType.Formal },
            { "Fury", PersonalityType.Arrogant },
            { "Genma", PersonalityType.Honorable },
            { "Holy", PersonalityType.Formal },
            { "Kishin", PersonalityType.Honorable },
            { "Lady", PersonalityType.Sultry },
            { "Megami", PersonalityType.Formal },
            { "Seraph", PersonalityType.Honorable },
            { "Wargod", PersonalityType.Upbeat },

            // --- 3. Neutral ---
            { "Beast", PersonalityType.Upbeat },
            { "Brute", PersonalityType.Timid },
            { "Divine", PersonalityType.Honorable },
            { "Fairy", PersonalityType.Childlike },
            { "Fallen", PersonalityType.Gloomy },
            { "Femme", PersonalityType.Sultry },
            { "Jirae", PersonalityType.Gloomy },
            { "Night", PersonalityType.Sultry },
            { "Snake", PersonalityType.Gloomy },
            { "Yoma", PersonalityType.Upbeat },

            // --- 4. Unclassified ---
            { "Fiend", PersonalityType.Arrogant }
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

            // Check global negotiation chance based on number of enemies
            if (!CheckNegotiationChance(enemies.Count(e => !e.IsDead)))
            {
                _io.WriteLine($"{target.Name} is on guard and refuses to talk!");
                _io.Wait(1000);
                return NegotiationResult.Failure;
            }

            // Determine Personality & Fetch Questions
            // Uses the "Race" property from the active persona/demon template
            string race = target.ActivePersona?.Race ?? "Fairy";
            PersonalityType personality = RaceToPersonality.GetValueOrDefault(race, PersonalityType.Childlike);

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
            else // Negative mood
            {
                _io.WriteLine($"{target.Name} grows angry!", ConsoleColor.Red);
                _io.Wait(800);
                return NegotiationResult.Failure;
            }
        }

        private NegotiationResult HandleFamiliarDemon(Combatant actor, Combatant target)
        {
            string dialogue = $"{target.Name} looks at you with a sense of familiarity...";

            // Check for specific familiar dialogue in PersonaData
            string lookupId = target.SourceId.ToLower();

            if (Database.Personas.TryGetValue(lookupId, out var pData) &&
                !string.IsNullOrEmpty(pData.FamiliarDialogue))
            {
                dialogue = $"{target.Name}: \"{pData.FamiliarDialogue}\"";
            }
            else
            {
                // Fallback to personality-based familiar dialogue
                string race = target.ActivePersona?.Race ?? "Fairy";
                PersonalityType personality = RaceToPersonality.GetValueOrDefault(race, PersonalityType.Childlike);

                var dialogues = Database.NegotiationQuestions
                    .FamiliarDialogues.GetValueOrDefault(personality, new List<string>());

                if (dialogues.Any())
                {
                    dialogue = $"{target.Name}: \"{dialogues[_rnd.Next(dialogues.Count)]}\"";
                }
            }

            _io.WriteLine(dialogue, ConsoleColor.Cyan);

            // Familiar demons usually give something and then leave
            int roll = _rnd.Next(0, 100);
            if (roll < 50) // 50% chance for a gift item
            {
                _io.WriteLine($"{target.Name} gives you a Medicine and departs.");
                _inventory.AddItem("101", 1); // "101" is Medicine ID
            }
            else if (roll < 80) // 20% chance for Macca
            {
                int macca = target.Level * 20;
                _io.WriteLine($"{target.Name} gives you {macca} Macca and departs.");
                _economy.AddMacca(macca);
            }
            else // 20% chance to heal party
            {
                _io.WriteLine($"{target.Name} casts a gentle light upon your party before departing.");
                foreach (var member in _party.GetAliveMembers())
                {
                    member.CurrentHP = (int)Math.Min(member.MaxHP, member.CurrentHP + (member.MaxHP * 0.15)); // Heal 15% HP
                }
            }
            return NegotiationResult.FamiliarFlee;
        }

        private bool CheckNegotiationChance(int livingEnemyCount)
        {
            if (livingEnemyCount <= 1) return true; // Higher chance with fewer enemies
            if (livingEnemyCount == 2) return _rnd.Next(0, 100) < 75;
            if (livingEnemyCount == 3) return _rnd.Next(0, 100) < 50;
            if (livingEnemyCount >= 4) return _rnd.Next(0, 100) < 25; // Very hard with many enemies
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
            double luckDiscount = baseCost * ((double)actor.GetStat(StatType.Lu) / 100.0);
            int maccaDemand = (int)Math.Max(target.Level * 5, baseCost - luckDiscount);

            string itemDemandId = _inventory.GetAllItemIds().FirstOrDefault(id => Database.Items[id].Type == "Healing");
            bool demandsItem = itemDemandId != null && _rnd.Next(0, 100) < 50; // 50% chance to demand item

            _io.WriteLine($"{target.Name}: \"Your words are intriguing. But talk is cheap.\"");
            _io.Wait(800);

            // --- Macca Demand ---
            if (maccaDemand > 0)
            {
                if (_economy.Macca < maccaDemand)
                {
                    _io.WriteLine($"The required donation of {maccaDemand} Macca is missing.", ConsoleColor.Red);
                    _io.Wait(1000);
                    return NegotiationResult.Failure;
                }

                var options = new List<string> { $"Give {maccaDemand} Macca", "Refuse" };
                int choice = _io.RenderMenu($"{target.Name}: \"A gift of {maccaDemand} Macca should suffice.\"", options, 0);
                if (choice == 0) // Player accepts macca demand
                {
                    _economy.SpendMacca(maccaDemand);
                    // If no item is demanded, then success
                    if (!demandsItem || itemDemandId == null) return NegotiationResult.Success;
                }
                else return NegotiationResult.Failure; // Player refused Macca
            }

            // --- Item Demand ---
            if (demandsItem && itemDemandId != null)
            {
                ItemData item = Database.Items[itemDemandId];
                List<string> options = new List<string> { $"Give {item.Name}", "Refuse" };
                int choice = _io.RenderMenu($"{target.Name}: \"A {item.Name} would be lovely.\"", options, 0);

                if (choice == 0) // Player accepts item demand
                {
                    _inventory.RemoveItem(itemDemandId, 1);
                    return NegotiationResult.Success;
                }
                else return NegotiationResult.Failure; // Player refused Item
            }

            // If no demands were made (unlikely but possible) or passed all checks
            if (_rnd.Next(0, 100) < 50)
            {
                return NegotiationResult.Success; // Demon joins
            }
            else
            {
                _io.WriteLine($"{target.Name}: \"Hmph. You waste my time.\"");
                return NegotiationResult.Trick; // Demon takes payment and flees
            }
        }
    }
}