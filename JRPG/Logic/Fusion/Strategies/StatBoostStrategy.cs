using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Strategies
{
    /// <summary>
    /// Strategy for Mitama consumption to boost specific stats of a target.
    /// Enforces a hard cap of 40 and handles sacrifice EXP bonuses.
    /// </summary>
    public class StatBoostStrategy : IFusionStrategy
    {
        public void Execute(FusionContext context)
        {
            string mitamaName = "";

            if (context.Owner.Class == ClassType.Operator)
            {
                // Identify parents
                Combatant target = (Combatant)context.Materials.First(m => ((Combatant)m).ActivePersona.Race != "Mitama");
                Combatant mitama = (Combatant)context.Materials.First(m => ((Combatant)m).ActivePersona.Race == "Mitama");
                mitamaName = mitama.ActivePersona.Name;

                // Handle sacrifice consumption
                if (context.Sacrifice is Combatant sacrificialCom)
                {
                    if (context.Party.ActiveParty.Contains(sacrificialCom))
                        context.Party.ReturnDemon(context.Owner, sacrificialCom);
                    context.Owner.DemonStock.Remove(sacrificialCom);
                }

                // Create the boosted instance
                Combatant boosted = Combatant.CreatePlayerDemon(target.SourceId, target.Level);
                boosted.Exp = target.Exp;
                boosted.ExtraSkills.Clear();
                boosted.ExtraSkills.AddRange(context.ChosenSkills);

                // Transfer existing modifiers
                foreach (var mod in target.ActivePersona.StatModifiers)
                    boosted.ActivePersona.StatModifiers[mod.Key] = mod.Value;

                // Apply Mitama math
                ApplyBoosts(boosted.ActivePersona, mitamaName, context.Messenger);

                // Apply Transfer formula (Earned XP / 1.5)
                if (context.Sacrifice is Combatant offer)
                {
                    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                    boosted.GainExp(transferXP);
                }

                boosted.RecalculateResources();
                context.Messenger.Publish($"{target.Name}'s stats have been enhanced!", ConsoleColor.Magenta);
                ReplaceDemon(context, target, boosted);
            }
            else if (context.Owner.Class == ClassType.WildCard)
            {
                Persona target = (Persona)context.Materials.First(m => ((Persona)m).Race != "Mitama");
                Persona mitama = (Persona)context.Materials.First(m => ((Persona)m).Race == "Mitama");
                mitamaName = mitama.Name;

                // Remove Mitama mask from stock
                context.Owner.PersonaStock.Remove(mitama);

                // Handle sacrifice consumption
                if (context.Sacrifice is Persona sacrificialPer)
                    context.Owner.PersonaStock.Remove(sacrificialPer);

                // Clone base template and restore state
                var template = Database.Personas.Values.First(p => p.Name == target.Name);
                Persona newP = template.ToPersona();
                newP.Level = target.Level;
                newP.Exp = target.Exp;
                newP.SkillSet.Clear();
                newP.SkillSet.AddRange(context.ChosenSkills);

                foreach (var mod in target.StatModifiers) newP.StatModifiers[mod.Key] = mod.Value;

                // Apply Mitama math
                ApplyBoosts(newP, mitamaName, context.Messenger);

                // Apply Transfer formula (Earned XP / 1.5)
                if (context.Sacrifice is Persona offer)
                {
                    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                    newP.GainExp(transferXP);
                }

                context.Messenger.Publish($"{target.Name}'s stats have been enhanced!", ConsoleColor.Magenta);
                ReplacePersona(context, target, newP);
            }
        }

        private void ApplyBoosts(Persona target, string mitamaName, IFusionMessenger messenger)
        {
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();

            // Standardize spelling just in case of JSON variants
            switch (mitamaName)
            {
                case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
                case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
                case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
                case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
            }

            foreach (var entry in boosts)
            {
                int current = target.StatModifiers.GetValueOrDefault(entry.Key, 0);
                if (current < 40)
                {
                    // Hard Cap of 40 logic
                    target.StatModifiers[entry.Key] = Math.Min(40, current + entry.Value);
                    messenger.Publish($" -> {entry.Key} increased by {entry.Value}!", ConsoleColor.Cyan);
                }
                else
                {
                    messenger.Publish($" -> {entry.Key} is already at its maximum!", ConsoleColor.Yellow);
                }
            }
        }

        private void ReplaceDemon(FusionContext context, Combatant oldD, Combatant newD)
        {
            newD.OwnerId = oldD.OwnerId; newD.Controller = oldD.Controller; newD.BattleControl = oldD.BattleControl;
            if (context.Party.ActiveParty.Contains(oldD)) { int s = oldD.PartySlot; context.Party.ActiveParty[s] = newD; newD.PartySlot = s; }
            else { context.Owner.DemonStock.Remove(oldD); context.Owner.DemonStock.Add(newD); }
            context.Owner.RecalculateResources();
        }

        private void ReplacePersona(FusionContext context, Persona oldP, Persona newP)
        {
            if (context.Owner.ActivePersona == oldP) context.Owner.ActivePersona = newP;
            else { context.Owner.PersonaStock.Remove(oldP); context.Owner.PersonaStock.Add(newP); }
            context.Owner.RecalculateResources();
        }
    }
}