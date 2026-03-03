using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Strategies
{
    public class StatBoostStrategy : IFusionStrategy
    {
        public void Execute(FusionContext context)
        {
            string mitamaName = "";
            if (context.Owner.Class == ClassType.Operator)
            {
                Combatant target = (Combatant)context.Materials.First(m => ((Combatant)m).ActivePersona.Race != "Mitama");
                Combatant mitama = (Combatant)context.Materials.First(m => ((Combatant)m).ActivePersona.Race == "Mitama");
                mitamaName = mitama.ActivePersona.Name;

                Combatant boosted = Combatant.CreatePlayerDemon(target.SourceId, target.Level);
                boosted.ExtraSkills.Clear();
                boosted.ExtraSkills.AddRange(context.ChosenSkills);
                foreach (var mod in target.ActivePersona.StatModifiers) boosted.ActivePersona.StatModifiers[mod.Key] = mod.Value;

                ApplyBoosts(boosted.ActivePersona, mitamaName, context.Messenger);
                ReplaceDemon(context, target, boosted);
            }
            else if (context.Owner.Class == ClassType.WildCard)
            {
                Persona target = (Persona)context.Materials.First(m => ((Persona)m).Race != "Mitama");
                Persona mitama = (Persona)context.Materials.First(m => ((Persona)m).Race == "Mitama");
                mitamaName = mitama.Name;

                Persona newP = Database.Personas.Values.First(p => p.Name == target.Name).ToPersona();
                newP.SkillSet.Clear();
                newP.SkillSet.AddRange(context.ChosenSkills);
                foreach (var mod in target.StatModifiers) newP.StatModifiers[mod.Key] = mod.Value;

                ApplyBoosts(newP, mitamaName, context.Messenger);
                ReplacePersona(context, target, newP);
            }
        }

        private void ApplyBoosts(Persona target, string mitamaName, IFusionMessenger messenger)
        {
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();
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
                    target.StatModifiers[entry.Key] = Math.Min(40, current + entry.Value);
                    messenger.Publish($" -> {entry.Key} increased by {entry.Value}!", ConsoleColor.Cyan);
                }
            }
        }

        private void ReplaceDemon(FusionContext context, Combatant oldD, Combatant newD)
        {
            if (context.Party.ActiveParty.Contains(oldD)) context.Party.ActiveParty[oldD.PartySlot] = newD;
            else { context.Owner.DemonStock.Remove(oldD); context.Owner.DemonStock.Add(newD); }
        }

        private void ReplacePersona(FusionContext context, Persona oldP, Persona newP)
        {
            if (context.Owner.ActivePersona == oldP) context.Owner.ActivePersona = newP;
            else { context.Owner.PersonaStock.Remove(oldP); context.Owner.PersonaStock.Add(newP); }
        }
    }
}