using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle
{
    public class BattleKnowledge
    {
        // Key: Enemy SourceID + Element. Value: Known Affinity.
        private Dictionary<(string, Element), Affinity> _knownAffinities = new Dictionary<(string, Element), Affinity>();

        public void Learn(string combatantId, Element elem, Affinity aff)
        {
            var key = (combatantId, elem);
            if (!_knownAffinities.ContainsKey(key))
            {
                _knownAffinities[key] = aff;
            }
        }

        public bool IsWeaknessKnown(string combatantId, Element elem)
        {
            var key = (combatantId, elem);
            return _knownAffinities.ContainsKey(key) && _knownAffinities[key] == Affinity.Weak;
        }

        public bool IsResistanceKnown(string combatantId, Element elem)
        {
            var key = (combatantId, elem);
            if (!_knownAffinities.ContainsKey(key)) return false;
            var aff = _knownAffinities[key];
            return aff == Affinity.Null || aff == Affinity.Repel || aff == Affinity.Absorb || aff == Affinity.Resist;
        }
    }
}