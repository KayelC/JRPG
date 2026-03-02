using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Logic.Battle.Effects;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Strategy Registry. 
    /// This class maps JSON identifiers to concrete IBattleEffect implementations.
    /// This satisfies the "Open/Closed Principle": To add a new effect, you add it here,
    /// and the rest of the engine updates automatically.
    /// </summary>
    public class BattleEffectRegistry
    {
        private readonly Dictionary<string, IBattleEffect> _effects = new Dictionary<string, IBattleEffect>(StringComparer.OrdinalIgnoreCase);

        public BattleEffectRegistry()
        {
            InitializeRegistry();
        }

        private void InitializeRegistry()
        {
            // 1. Recovery & Utility (Mapping Item 'Type' and Skill 'Category')
            _effects["Healing"] = new HealEffect();
            _effects["Healing_All"] = new HealEffect();
            _effects["Spirit"] = new SpiritEffect();
            _effects["Revive"] = new ReviveEffect();
            _effects["Cure"] = new CureEffect();
            _effects["Enhance"] = new BuffEffect();
            _effects["Dekaja"] = new DekajaEffect();
            _effects["Charge"] = new ChargeEffect();
            _effects["Shield"] = new ShieldEffect();

            // 2. Damage Elements (Mapping Skill 'Category' to specific DamageEffect instances)
            // We pass the Element into the constructor so one class can handle all types.
            _effects["Slash"] = new DamageEffect(Element.Slash);
            _effects["Strike"] = new DamageEffect(Element.Strike);
            _effects["Pierce"] = new DamageEffect(Element.Pierce);
            _effects["Fire"] = new DamageEffect(Element.Fire);
            _effects["Ice"] = new DamageEffect(Element.Ice);
            _effects["Elec"] = new DamageEffect(Element.Elec);
            _effects["Wind"] = new DamageEffect(Element.Wind);
            _effects["Earth"] = new DamageEffect(Element.Earth);
            _effects["Light"] = new DamageEffect(Element.Light);
            _effects["Dark"] = new DamageEffect(Element.Dark);
            _effects["Almighty"] = new DamageEffect(Element.Almighty);
        }

        /// <summary>
        /// Retrieves the strategy for a given key.
        /// </summary>
        public IBattleEffect GetEffect(string effectKey)
        {
            if (_effects.TryGetValue(effectKey, out var effect))
            {
                return effect;
            }

            // Return null or a 'NullEffect' if no match is found
            return null;
        }
    }
}