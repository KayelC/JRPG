using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class ShieldEffect : IBattleEffect
    {
        public void Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            foreach (var target in targets)
            {
                if (metadata.Contains("Tetra"))
                {
                    target.PhysKarnActive = true;
                    messenger.Publish("Physical Shield deployed.");
                }
                else
                {
                    target.MagicKarnActive = true;
                    messenger.Publish("Magic Shield deployed.");
                }
            }
        }
    }
}