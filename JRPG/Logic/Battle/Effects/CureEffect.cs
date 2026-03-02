using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class CureEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (target.IsDead || target.CurrentAilment == null) continue;

                if (status.CheckAndExecuteCure(target, metadata))
                {
                    messenger.Publish($"{target.Name} was cured of their ailment!");
                }
            }
        }
    }
}