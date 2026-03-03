using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Strategies
{
    public class RankMutationStrategy : IFusionStrategy
    {
        public void Execute(FusionContext context)
        {
            if (context.Owner.Class == ClassType.Operator)
            {
                Combatant original = (Combatant)context.Materials.First(m => ((Combatant)m).ActivePersona.Race != "Element");
                if (context.Sacrifice is Combatant sacrificialCom) context.Owner.DemonStock.Remove(sacrificialCom);

                Combatant newD = Combatant.CreatePlayerDemon(context.ResultId, Database.Personas[context.ResultId.ToLower()].Level);
                newD.ExtraSkills.Clear();
                newD.ExtraSkills.AddRange(context.ChosenSkills);

                context.Messenger.Publish($"{original.Name} transformed!", ConsoleColor.Magenta);
                ReplaceDemon(context, original, newD);
            }
            else if (context.Owner.Class == ClassType.WildCard)
            {
                Persona original = (Persona)context.Materials.First(m => ((Persona)m).Race != "Element");
                Persona newP = Database.Personas[context.ResultId.ToLower()].ToPersona();
                newP.SkillSet.Clear();
                newP.SkillSet.AddRange(context.ChosenSkills);

                context.Messenger.Publish($"{original.Name} transformed!", ConsoleColor.Magenta);
                ReplacePersona(context, original, newP);
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