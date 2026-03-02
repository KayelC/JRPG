using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class SpiritEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                int oldSP = target.CurrentSP;
                target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + power);

                messenger.Publish($"{target.Name} recovered {target.CurrentSP - oldSP} SP.");
            }
        }
    }
}