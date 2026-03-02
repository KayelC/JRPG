using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Battle.Effects;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The authoritative coordinator of battle actions.
    /// This class no longer contains specific damage/heal math.
    /// It manages Costs, Charges, and delegates behavior to the Strategy Registry.
    /// </summary>
    public class ActionProcessor
    {
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;
        private readonly IBattleMessenger _messenger;
        private readonly BattleEffectRegistry _registry;

        public ActionProcessor(StatusRegistry status, BattleKnowledge knowledge, IBattleMessenger messenger)
        {
            _status = status;
            _knowledge = knowledge;
            _messenger = messenger;

            // This registry acts as the "Toolbox" containing all logic strategies.
            _registry = new BattleEffectRegistry();
        }

        /// <summary>
        /// Orchestrates a standard weapon attack.
        /// Reuses DamageEffect to ensure weapon elemental affinities are respected.
        /// </summary>
        public CombatResult ExecuteAttack(Combatant attacker, Combatant target)
        {
            if (target.IsDead) return new CombatResult { Type = HitType.Miss, DamageDealt = 0 };

            _messenger.Publish($"{attacker.Name} attacks {target.Name}!");

            // Standard attacks are just 'Slash/Strike/Pierce' DamageEffects with a base power of 15.
            IBattleEffect strategy = _registry.GetEffect(attacker.WeaponElement.ToString());

            if (strategy == null) return new CombatResult { Type = HitType.Miss };

            var results = strategy.Apply(attacker, new List<Combatant> { target }, 15, "", _messenger, _status, _knowledge);

            // Fulfills the "Missing/Attacking consumes physical charge" requirement
            attacker.IsCharged = false;

            return results.FirstOrDefault() ?? new CombatResult { Type = HitType.Miss };
        }

        // Handles Skill execution: Deducts costs and delegates to the correct strategy.
        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            // --- 1. LEGACY LOGIC: Resource Cost Calculation ---
            var cost = skill.ParseCost();
            int costValue = cost.value;
            var passives = attacker.GetConsolidatedSkills();

            // Determine if it's physical for cost and charge logic
            bool isPhys = skill.Category == "Slash" || skill.Category == "Strike" || skill.Category == "Pierce";

            // Arms Master / Spell Master logic
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

            // --- 2. DELEGATION: Get the Strategy from the Toolbox ---
            IBattleEffect strategy = _registry.GetEffect(skill.Category);
            List<CombatResult> results;

            if (strategy != null)
            {
                // Logic is in the specific Effect class (e.g., DamageEffect, HealEffect)
                results = strategy.Apply(attacker, targets, skill.GetPowerVal(), skill.Effect, _messenger, _status, _knowledge);
            }
            else
            {
                _messenger.Publish($"[Error] No logic found for {skill.Category}", ConsoleColor.Yellow);
                results = new List<CombatResult>();
            }

            // --- 3. LEGACY LOGIC: Charge Management ---
            // Physical skills consume Physical Charge, Magic consumes Mind Charge
            if (isPhys) attacker.IsCharged = false;
            else attacker.IsMindCharged = false;

            return results;
        }

        // Handles Item execution: Delegates behavior to the Registry.
        public bool ExecuteItem(Combatant user, List<Combatant> targets, ItemData item)
        {
            _messenger.Publish($"{user.Name} used {item.Name}!", ConsoleColor.White, 200);

            // Logic branch for Traesto preserved
            if (item.Name == "Traesto Gem")
            {
                _messenger.Publish("A blinding light creates a path to safety!", ConsoleColor.White, 800);
                return true;
            }

            // Get the Strategy (e.g. Type "Healing" maps to HealEffect)
            IBattleEffect strategy = _registry.GetEffect(item.Type);

            if (strategy != null)
            {
                var results = strategy.Apply(user, targets, item.EffectValue, item.Name, _messenger, _status, _knowledge);
                return results.Any();
            }

            return false;
        }

        // Orchestrates the Analysis logic and records knowledge discovery.
        public void ExecuteAnalyze(Combatant target)
        {
            // 1. LOGIC: Force the recording of all current affinities into player knowledge
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _knowledge.Learn(target.SourceId, elem, aff);
            }

            // 2. BROADCAST: Request that the Observer renders the Analysis Target
            // The BattleLogger sees 'analysisTarget' is not null and triggers its HandleAnalysisDisplay
            _messenger.Publish(message: null, analysisTarget: target);
        }
    }
}