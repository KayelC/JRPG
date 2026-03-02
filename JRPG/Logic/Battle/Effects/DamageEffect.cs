using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    public class DamageEffect : IBattleEffect
    {
        private readonly Element _element;
        public DamageEffect(Element element) => _element = element;

        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string metadata, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                Affinity aff = CombatMath.GetEffectiveAffinity(target, _element);

                if (aff == Affinity.Repel)
                {
                    messenger.Publish($"{target.Name} repelled the attack!", ConsoleColor.Red);
                    int refDmg = CombatMath.CalculateReflectedDamage(user, power, _element);
                    var repResult = user.ReceiveDamage(refDmg, _element, false);
                    messenger.Publish($"{user.Name} took {refDmg} reflected damage.");

                    repResult.Type = HitType.Repel;
                    results.Add(repResult);
                    continue;
                }

                int damage = CombatMath.CalculateDamage(user, target, power, _element, out bool isCritical);
                CombatResult result = target.ReceiveDamage(damage, _element, isCritical);

                knowledge.Learn(target.SourceId, _element, aff);
                Report(result, target.Name, messenger);

                if (result.Type != HitType.Null && result.Type != HitType.Absorb)
                {
                    if (status.TryInflict(user, target, metadata))
                    {
                        messenger.Publish($"{target.Name} infected with status!", ConsoleColor.Magenta);
                    }
                }

                results.Add(result);
            }
            return results;
        }

        private void Report(CombatResult result, string targetName, IBattleMessenger messenger)
        {
            if (result.DamageDealt > 0)
            {
                string msg = $"{targetName} took {result.DamageDealt} damage";
                if (result.IsCritical) msg += " (CRITICAL)";
                if (result.Type == HitType.Weakness) msg += " (WEAKNESS)";
                messenger.Publish(msg);
            }
            else if (result.Type == HitType.Absorb) messenger.Publish($"{targetName} absorbed the attack!", ConsoleColor.Green);
            else if (result.Type == HitType.Null) messenger.Publish($"{targetName} blocked the attack!");
        }
    }
}