using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using System.Collections.Generic;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Logic.Core;

namespace JRPGPrototype.Logic.Fusion
{
    // Encapsulates all data required to perform a fusion transaction.
    public class FusionContext
    {
        public Combatant Owner { get; }
        public List<object> Materials { get; }
        public object? Sacrifice { get; }
        public List<string> ChosenSkills { get; }
        public string ResultId { get; }
        public IFusionMessenger Messenger { get; }
        public PartyManager Party { get; }

        public FusionContext(
            Combatant owner,
            List<object> materials,
            object? sacrifice,
            List<string> chosenSkills,
            string resultId,
            IFusionMessenger messenger,
            PartyManager party)
        {
            Owner = owner;
            Materials = materials;
            Sacrifice = sacrifice;
            ChosenSkills = chosenSkills;
            ResultId = resultId;
            Messenger = messenger;
            Party = party;
        }
    }
}