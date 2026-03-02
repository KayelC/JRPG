using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class HealEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, IBattleMessenger messenger)
        {
            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                int oldHP = target.CurrentHP;
                
                // If power is 9999 (data convention), full heal. Otherwise use value.
                int actualHeal = (power >= 9999) ? target.MaxHP : power;
                
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + actualHeal);
                
                messenger.Publish($"{target.Name} recovered {target.CurrentHP - oldHP} HP.");
            }
            // Utility actions always count as "Normal" hits for Press Turn purposes
            return new List<CombatResult> { new CombatResult { Type = HitType.Normal } };
        }
    }
}