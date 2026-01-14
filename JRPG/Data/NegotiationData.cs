using System.Collections.Generic;
using Newtonsoft.Json;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data
{
    /// <summary>
    /// Root object for deserializing the questions.json file.
    /// Maps each PersonalityType to a list of possible questions and familiar dialogues.
    /// </summary>
    public class NegotiationQuestionRoot
    {
        [JsonProperty("questions")]
        public Dictionary<PersonalityType, List<NegotiationQuestion>> Questions { get; set; }

        [JsonProperty("familiar_dialogue")]
        public Dictionary<PersonalityType, List<string>> FamiliarDialogues { get; set; }
    }

    /// <summary>
    /// Represents a single conversational prompt in the negotiation mini-game.
    /// </summary>
    public class NegotiationQuestion
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("answers")]
        public List<NegotiationAnswer> Answers { get; set; }
    }

    /// <summary>
    /// Represents a player's choice in response to a demon's question.
    /// Contains the text and the associated "Mood Score" value.
    /// </summary>
    public class NegotiationAnswer
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; } // e.g., +2 for good, +1 for neutral, -1 for bad
    }
}