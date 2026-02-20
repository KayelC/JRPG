using System;
using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Memory Layer of the Battle Sub-System.
    /// Tracks elemental affinities discovered through actions to inform AI decisions
    /// and update the Player UI.
    /// 
    /// Refinement: Designed to be stored in SaveData or SessionData for the Player,
    /// but initialized fresh for Enemies to prevent "Cheating" AI.
    /// </summary>
    public class BattleKnowledge
    {
        /// <summary>
        /// Key: (string SourceId, Element element)
        /// Value: The discovered Affinity.
        /// Using SourceId (e.g., "E_pixie") ensures knowledge persists across different 
        /// encounters with the same species.
        /// </summary>
        private readonly Dictionary<(string, Element), Affinity> _registry = new Dictionary<(string, Element), Affinity>();

        /// <summary>
        /// Records an affinity discovery. 
        /// Hitting a target with an element reveals that affinity forever.
        /// </summary>
        /// <param name="sourceId">The unique ID of the enemy species.</param>
        /// <param name="element">The element used in the attack.</param>
        /// <param name="affinity">The result determined by the Math Kernel.</param>
        public void Learn(string sourceId, Element element, Affinity affinity)
        {
            // SMT III Logic: Almighty and None do not have variable affinities to track.
            if (element == Element.Almighty || element == Element.None || element == Element.Almighty)
            {
                return;
            }

            // If we are learning an affinity for an ID, we save the "Base Truth".
            // Even if the target was Guarding (which makes them 'Normal'), we store 
            // the discovery of their actual weakness/resistance if the hit confirmed it.
            var key = (sourceId, element);

            if (_registry.ContainsKey(key))
            {
                _registry[key] = affinity;
            }
            else
            {
                _registry.Add(key, affinity);
            }
        }

        /// <summary>
        /// Queries the memory to see if a specific weakness has been discovered.
        /// Used by the BehaviorEngine to choose optimal attacks.
        /// </summary>
        public bool IsWeaknessKnown(string sourceId, Element element)
        {
            var key = (sourceId, element);
            return _registry.TryGetValue(key, out var knownAffinity) && knownAffinity == Affinity.Weak;
        }

        /// <summary>
        /// Queries the memory to see if an element is known to be dangerous (Null, Repel, Absorb).
        /// Used by the BehaviorEngine for risk aversion to avoid losing Press Turn icons.
        /// </summary>
        public bool IsResistanceKnown(string sourceId, Element element)
        {
            var key = (sourceId, element);
            if (_registry.TryGetValue(key, out var knownAffinity))
            {
                return knownAffinity == Affinity.Null ||
                       knownAffinity == Affinity.Repel ||
                       knownAffinity == Affinity.Absorb;
            }
            return false;
        }

        /// <summary>
        /// Returns the exact known affinity. 
        /// Returns Affinity.Normal if the element hasn't been tested against this ID yet.
        /// </summary>
        public Affinity GetKnownAffinity(string sourceId, Element element)
        {
            var key = (sourceId, element);
            if (_registry.TryGetValue(key, out var knownAffinity))
            {
                return knownAffinity;
            }
            return Affinity.Normal;
        }

        /// <summary>
        /// UI Support: Checks if an enemy type has been "Scanned" or fully 
        /// interacted with for a specific element.
        /// </summary>
        public bool HasDiscovery(string sourceId, Element element)
        {
            return _registry.ContainsKey((sourceId, element));
        }

        /// <summary>
        /// Used during Save/Load or when merging "Library Data" into the current session.
        /// </summary>
        public void ImportKnowledge(Dictionary<(string, Element), Affinity> externalData)
        {
            foreach (var kvp in externalData)
            {
                Learn(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
            }
        }

        /// <summary>
        /// Returns a copy of the current registry for persistence (Saving to JSON).
        /// </summary>
        public Dictionary<(string, Element), Affinity> ExportKnowledge()
        {
            return new Dictionary<(string, Element), Affinity>(_registry);
        }
    }
}
