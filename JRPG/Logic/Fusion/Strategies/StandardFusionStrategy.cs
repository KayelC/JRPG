using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Strategies
{
    public class StandardFusionStrategy : IFusionStrategy
    {
        public void Execute(FusionContext context)
        {
            if (context.Owner.Class == ClassType.Operator)
            {
                ExecuteOperatorFusion(context);
            }
            else if (context.Owner.Class == ClassType.WildCard)
            {
                ExecuteWildCardFusion(context);
            }
        }

        private void ExecuteOperatorFusion(FusionContext context)
        {
            List<Combatant> allParticipants = context.Materials.Cast<Combatant>().ToList();
            if (context.Sacrifice is Combatant sacrificialCom)
                allParticipants.Add(sacrificialCom);

            foreach (var participant in allParticipants)
            {
                if (context.Party.ActiveParty.Contains(participant))
                    context.Party.ReturnDemon(context.Owner, participant);
                context.Owner.DemonStock.Remove(participant);
            }

            Combatant child = Combatant.CreatePlayerDemon(context.ResultId,
                Database.Personas[context.ResultId.ToLower()].Level);
            child.ExtraSkills.Clear();
            child.ExtraSkills.AddRange(context.ChosenSkills);

            // Transfer formula (Earned XP / 1.5)
            if (context.Sacrifice is Combatant offer)
            {
                int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                child.GainExp(transferXP);
            }

            child.RecalculateResources();
            child.CurrentHP = child.MaxHP;
            child.CurrentSP = child.MaxSP;

            if (!context.Party.SummonDemon(context.Owner, child))
                context.Owner.DemonStock.Add(child);
            context.Messenger.Publish($"{child.Name} joined!", ConsoleColor.Green);
        }

        private void ExecuteWildCardFusion(FusionContext context)
        {
            List<Persona> materials = context.Materials.Cast<Persona>().ToList();
            foreach (var persona in materials)
            {
                if (context.Owner.ActivePersona == persona) context.Owner.ActivePersona = null;
                context.Owner.PersonaStock.Remove(persona);
            }

            if (context.Sacrifice is Persona sacrificialPer)
            {
                if (context.Owner.ActivePersona == sacrificialPer)
                    context.Owner.ActivePersona = null;
                context.Owner.PersonaStock.Remove(sacrificialPer);
            }

            Persona child = Database.Personas[context.ResultId.ToLower()].ToPersona();
            child.SkillSet.Clear();
            child.SkillSet.AddRange(context.ChosenSkills);

            // Transfer formula (Earned XP / 1.5)
            if (context.Sacrifice is Persona offer)
            {
                int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                child.GainExp(transferXP);
            }

            context.Owner.PersonaStock.Add(child);
            if (context.Owner.ActivePersona == null) context.Owner.ActivePersona = child;
            context.Owner.RecalculateResources();
            context.Messenger.Publish($"{child.Name} manifested!", ConsoleColor.Cyan);
        }
    }
}