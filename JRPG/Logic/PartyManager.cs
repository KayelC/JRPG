using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Entities;
using JRPGPrototype.Core;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic
{
    public class PartyManager
    {
        // The 4 active combatants on the field
        public List<Combatant> ActiveParty { get; private set; } = new List<Combatant>();

        // The reserve stock (Humans/Guests/Demons not currently fighting)
        public List<Combatant> ReserveMembers { get; private set; } = new List<Combatant>();

        private const int MAX_PARTY_SIZE = 4;

        public PartyManager(Combatant initialPlayer)
        {
            // The first character added is designated as the initial local player
            initialPlayer.PartySlot = 0;
            initialPlayer.Controller = ControllerType.LocalPlayer;
            ActiveParty.Add(initialPlayer);
        }

        /// <summary>
        /// Calculates max stock size based on character level.
        /// Unlocks slots at specific level thresholds.
        /// </summary>
        private int CalculateMaxStock(int level)
        {
            if (level < 10) return 2;
            if (level < 20) return 4;
            if (level < 30) return 6;
            return 8;
        }

        /// <summary>
        /// Checks if a specific actor has an open slot in their Demon Stock.
        /// </summary>
        public bool HasOpenDemonStockSlot(Combatant actor)
        {
            int maxStock = CalculateMaxStock(actor.Level);
            return actor.DemonStock.Count < maxStock;
        }

        /// <summary>
        /// Checks if a specific actor has an open slot in their Persona Stock.
        /// </summary>
        public bool HasOpenPersonaStockSlot(Combatant actor)
        {
            int maxStock = CalculateMaxStock(actor.Level);
            return actor.PersonaStock.Count < maxStock;
        }

        /// <summary>
        /// Checks if a demon with a given SourceId is already owned by the actor,
        /// either in their active party or in their stock.
        /// </summary>
        public bool IsDemonOwned(Combatant owner, string sourceId)
        {
            // Check the specific owner's demon stock
            if (owner.DemonStock.Any(d => d.SourceId == sourceId)) return true;

            // Check if the demon is in the active party under anyone's control
            if (ActiveParty.Any(c => c.SourceId == sourceId && c.Class == ClassType.Demon)) return true;

            return false;
        }

        /// <summary>
        /// Checks if a persona with a given Id is already owned by the actor.
        /// </summary>
        public bool IsPersonaOwned(Combatant owner, string personaId)
        {
            if (owner.ActivePersona?.Name == personaId) return true;
            if (owner.PersonaStock.Any(p => p.Name == personaId)) return true;
            return false;
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

        /// <summary>
        /// Robust Summoning Logic: Moves a demon from the owner's stock to the active party.
        /// This is an atomic transaction to prevent duplication.
        /// </summary>
        public bool SummonDemon(Combatant owner, Combatant demon)
        {
            if (ActiveParty.Count < MAX_PARTY_SIZE)
            {
                // 1. Remove from owner stock first to ensure atomicity
                if (owner.DemonStock.Contains(demon))
                {
                    owner.DemonStock.Remove(demon);
                }

                // 2. Add to active battlefield
                demon.PartySlot = ActiveParty.Count;
                demon.BattleControl = ControlState.DirectControl;
                ActiveParty.Add(demon);
                return true;
            }
            return false; // Party full
        }

        /// <summary>
        /// Robust Return Logic: Moves a demon from the battlefield back to the owner's stock.
        /// </summary>
        public bool ReturnDemon(Combatant owner, Combatant demon)
        {
            if (ActiveParty.Contains(demon))
            {
                // 1. Remove from battlefield
                demon.PartySlot = -1;
                ActiveParty.Remove(demon);

                // 2. Return to owner's stock
                if (!owner.DemonStock.Contains(demon))
                {
                    owner.DemonStock.Add(demon);
                }
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