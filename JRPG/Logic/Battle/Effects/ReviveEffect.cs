using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class ReviveEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (!target.IsDead) continue;

                // Percentage or flat
                int revVal = (power >= 100 || metadata.Contains("fully")) ? target.MaxHP : target.MaxHP / 2;
                target.CurrentHP = revVal;

                messenger.Publish($"{target.Name} was revived!", System.ConsoleColor.Green);
            }
        }
    }
}