using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Entities;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic
{
    public class PartyManager
    {
        // The 4 active combatants on the field
        public List<Combatant> ActiveParty { get; private set; } = new List<Combatant>();

        // The reserve stock (Humans/Guests/Demons not currently fighting)
        public List<Combatant> ReserveMembers { get; private set; } = new List<Combatant>();

        private const int MAX_PARTY_SIZE = 4;

        public PartyManager(Combatant protagonist)
        {
            // Protagonist always starts in Slot 0 and is controlled by Local Player
            protagonist.PartySlot = 0;
            protagonist.Controller = ControllerType.LocalPlayer;
            ActiveParty.Add(protagonist);
        }

        public bool AddMember(Combatant member)
        {
            if (ActiveParty.Count < MAX_PARTY_SIZE)
            {
                member.PartySlot = ActiveParty.Count;
                ActiveParty.Add(member);
                return true;
            }
            else
            {
                member.PartySlot = -1;
                ReserveMembers.Add(member);
                return false;
            }
        }

        public void SwapMember(int activeIndex, int reserveIndex)
        {
            if (activeIndex < 0 || activeIndex >= ActiveParty.Count) return;
            if (reserveIndex < 0 || reserveIndex >= ReserveMembers.Count) return;

            Combatant active = ActiveParty[activeIndex];
            Combatant reserve = ReserveMembers[reserveIndex];

            // Perform Swap
            ActiveParty[activeIndex] = reserve;
            ReserveMembers[reserveIndex] = active;

            // Update Indices
            reserve.PartySlot = activeIndex;
            active.PartySlot = -1;
        }

        public bool SummonDemon(Combatant demon)
        {
            if (ActiveParty.Count < MAX_PARTY_SIZE)
            {
                demon.PartySlot = ActiveParty.Count;
                // Default summoned demons to AI control for now (Operator Command logic will handle overrides later)
                demon.BattleControl = ControlState.ActFreely;
                ActiveParty.Add(demon);
                return true;
            }
            return false; // Party full
        }

        public bool ReturnDemon(Combatant demon)
        {
            if (ActiveParty.Contains(demon))
            {
                demon.PartySlot = -1;
                ActiveParty.Remove(demon);
                return true;
            }
            return false;
        }

        public bool IsPartyWiped()
        {
            return ActiveParty.All(m => m.IsDead);
        }

        public List<Combatant> GetAliveMembers()
        {
            return ActiveParty.Where(m => !m.IsDead).ToList();
        }
    }
}