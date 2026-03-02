using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class BuffEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                // metadata is the skill name (e.g., "Matarukaja")
                status.ApplyStatChange(metadata, target);
                messenger.Publish($"{target.Name}'s stats were modified!");
            }
        }
    }
}