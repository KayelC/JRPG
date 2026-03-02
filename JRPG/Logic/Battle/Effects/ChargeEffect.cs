using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class ChargeEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (metadata.Contains("Power"))
                {
                    target.IsCharged = true;
                    messenger.Publish($"{target.Name} is focusing physical power!");
                }
                else
                {
                    target.IsMindCharged = true;
                    messenger.Publish($"{target.Name} is focusing spiritual energy!");
                }
            }
        }
    }
}