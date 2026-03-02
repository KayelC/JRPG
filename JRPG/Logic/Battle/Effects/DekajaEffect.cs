using JRPGPrototype.Entities;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class DekajaEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    if (target.Buffs[k] > 0) target.Buffs[k] = 0;
                }
                messenger.Publish($"{target.Name}'s stat bonuses were nullified!");
            }
        }
    }
}