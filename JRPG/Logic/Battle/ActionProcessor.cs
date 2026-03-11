using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Battle.Effects;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Bridges;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The authoritative coordinator of battle actions.
    /// Manages Costs, Charges, and delegates behavior to the Strategy Registry.
    /// This class serves as the 'Brain' that connects Data, Logic, and UI.
    /// </summary>
    public class ActionProcessor
    {
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;
        private readonly IBattleMessenger _messenger;
        private readonly BattleEffectRegistry _registry;

        public ActionProcessor(StatusRegistry status, BattleKnowledge knowledge,
        IBattleMessenger messenger)
        {
            _status = status;
            _knowledge = knowledge;
            _messenger = messenger;

            // The Registry is our centralized toolbox of the logic patterns.
            _registry = new BattleEffectRegistry();
        }

        /// <summary>
        /// Orchestrates a standard physical weapon attack.
        /// Reuses DamageEffect to ensure melee affinities (Slash/Strike/Pierce) are discovered.
        /// </summary>
        public CombatResult ExecuteAttack(Combatant attacker, Combatant target)
        {
            if (target.IsDead) return new CombatResult { Type = HitType.Miss, DamageDealt = 0 };

            // 1. UI: Report the attempt
            _messenger.Publish($"{attacker.Name} attacks {target.Name}!");

            // 2. Reuses the DamageEffect logic based on current weapon element
            IBattleEffect? strategy = _registry.GetEffect(attacker.WeaponElement.ToString());

            if (strategy == null) return new CombatResult { Type = HitType.Miss };

            // 3. Unarmed / Base Demon Melee attacks have a standard power of 15.
            // We pass "Attack" as the action name so the strategy can handle specific narration.
            var results = strategy.Apply(attacker, new List<Combatant> { target }, 15,
            "Attack", "", _messenger, _status, _knowledge);

            return results.FirstOrDefault() ?? new CombatResult { Type = HitType.Miss };
        }

        /// <summary>
        /// Handles Skill execution: Deducts costs and delegates to the correct strategy.
        /// Includes the Effectiveness Gate to prevent turn wastage.
        /// </summary>
        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            // --- 1. Effectiveness Gate ---
            // Verify if the action is redundant (e.g. Poisoning an already poisoned foe).
            // Damaging skills (like Toxic Sting) bypass this via logic inside StatusRegistry.
            if (_status.IsActionRedundant(attacker, skill, targets))
            {
                _messenger.Publish("That action would have no effect!", ConsoleColor.Yellow);
                // Returning an empty list signals the Conductor that the turn should be preserved/re-picked.
                return new List<CombatResult>();
            }

            // --- 2. Resource Cost Calculation ---
            var cost = skill.ParseCost();
            int costValue = cost.value;
            var passives = attacker.GetConsolidatedSkills();

            // Logic Fidelity: Use the core ElementHelper to verify if the category is Physical
            Element skillElement = ElementHelper.FromCategory(skill.Category);
            bool isPhys = skillElement == Element.Slash || skillElement == Element.Strike || skillElement == Element.Pierce;

            // Arms Master / Spell Master Logic
            if (cost.isHP && isPhys && passives.Contains("Arms Master")) costValue /= 2;
            else if (!cost.isHP && !isPhys && passives.Contains("Spell Master")) costValue /= 2;

            if (cost.isHP)
            {
                int hpCost = (int)(attacker.MaxHP * (costValue / 100.0));
                attacker.CurrentHP = Math.Max(1, attacker.CurrentHP - hpCost);
            }
            else
            {
                attacker.CurrentSP -= costValue;
            }

            _messenger.Publish($"{attacker.Name} uses {skill.Name}!", ConsoleColor.White, 200);

            // --- 3. Strategy Execution ---
            IBattleEffect? strategy = _registry.GetEffect(skill.Category);
            List<CombatResult> results;

            if (strategy != null)
            {
                // Delegation: The 'How' is handled by the specialized Strategy class.
                // We pass the Skill Name as the ActionName.
                results = strategy.Apply(attacker, targets, skill.GetPowerVal(), skill.Name, skill.Effect, _messenger, _status, _knowledge);
            }
            else
            {
                _messenger.Publish($"[Error] No logic found for Category: {skill.Category}", ConsoleColor.Yellow);
                results = new List<CombatResult>();
            }

            return results;
        }

        /// <summary>
        /// Handles Item execution: Delegates behavior to the Registry.
        /// Includes the Effectiveness Gate to prevent turn wastage.
        /// </summary>
        public bool ExecuteItem(Combatant user, List<Combatant> targets, ItemData item)
        {
            _messenger.Publish($"{user.Name} used {item.Name}!", ConsoleColor.White, 200);

            // Logic branch for Traesto
            if (item.Name == "Traesto Gem")
            {
                _messenger.Publish("A blinding light creates a path to safety!", ConsoleColor.White, 800);
                return true;
            }

            // --- Strategy Execution ---
            // Items route to strategies based on their 'Type' (Healing, Cure, etc.)
            IBattleEffect? strategy = _registry.GetEffect(item.Type);

            if (strategy != null)
            {
                // Note: Items use EffectValue instead of Power.
                var results = strategy.Apply(user, targets, item.EffectValue, item.Name,
                item.Description ?? "", _messenger, _status, _knowledge);

                return results.Any();
            }

            _messenger.Publish($"[Error] No logic found for Item Type: {item.Type}", ConsoleColor.Red);
            return false;
        }

        // Orchestrates the Analysis logic and records knowledge discovery.
        public void ExecuteAnalyze(Combatant target)
        {
            // 1. LOGIC: Force record all affinities into player memory (Discover All)
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _knowledge.Learn(target.SourceId, elem, aff);
            }

            // 2. BROADCAST: Send the analysis signal to the Messenger. 
            // The BattleLogger sees that 'analysisTarget' is not null and renders the stat sheet.
            _messenger.Publish(message: null, analysisTarget: target);
        }

        // Utility check for the Conductor/AI to determine if a skill targets multiple people.
        public bool IsMultiTarget(SkillData skill)
        {
            string name = skill.Name.ToLower();
            string effect = skill.Effect.ToLower();

            return name.StartsWith("ma") ||
                   name.StartsWith("me") ||
                   effect.Contains("all foes") ||
                   effect.Contains("all allies") ||
                   effect.Contains("party") ||
                   name == "amrita" ||
                   name == "salvation";
        }
    }
}