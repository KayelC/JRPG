using JRPGPrototype.Entities;
using JRPGPrototype.Core;
using System;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// A "Delegator" strategy for the broad 'Recovery' skill category.
    /// It identifies the intent (Cure, Revive, or Heal) and passes the task 
    /// to the specialized atomic strategies.
    /// </summary>
    public class RecoveryEffect : IBattleEffect
    {
        // We reuse the existing strategies to prevent code duplication (DRY Principle)
        private readonly HealEffect _healer = new HealEffect();
        private readonly CureEffect _curer = new CureEffect();
        private readonly ReviveEffect _reviver = new ReviveEffect();

        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            // 1. Route to Revive logic if the skill contains the keyword
            // Priority: Revive must be checked first as some skills heal AND revive.
            if (actionEffect.Contains("Revive", StringComparison.OrdinalIgnoreCase))
            {
                return _reviver.Apply(user, targets, power, actionName, actionEffect, messenger, status, knowledge);
            }

            // 2. Route to Cure logic if the skill contains curing/dispelling keywords
            if (actionEffect.Contains("Cure", StringComparison.OrdinalIgnoreCase) ||
                actionEffect.Contains("Dispel", StringComparison.OrdinalIgnoreCase) ||
                actionEffect.Contains("Patra", StringComparison.OrdinalIgnoreCase))
            {
                // Perform the cure first, then optionally fall through to healing inside the specialized strategy
                return _curer.Apply(user, targets, power, actionName, actionEffect, messenger,
                status, knowledge);
            }

            // 3. Default route: Healing logic
            // Most recovery skills (Dia, Media) are just healing and fall through here.
            return _healer.Apply(user, targets, power, actionName, actionEffect, messenger,
            status, knowledge);
        }
    }
}