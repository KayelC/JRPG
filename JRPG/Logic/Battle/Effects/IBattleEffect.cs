using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public interface IBattleEffect
    {
        /// <summary>
        /// Applies the logic for a specific skill or item.
        /// </summary>
        /// <param name="user">The user of the action.</param>
        /// <param name="targets">The targets of the action.</param>
        /// <param name="power">Numerical value (Power/EffectValue).</param>
        /// <param name="metadata">String value (Effect/AilmentName).</param>
        /// <param name="messenger">The mediator for logs.</param>
        /// <param name="status">Authority for ailments/buffs.</param>
        /// <param name="knowledge">Authority for affinities.</param>
        List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge);
    }
}