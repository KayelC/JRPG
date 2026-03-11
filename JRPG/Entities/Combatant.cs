using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Entities
{
    /// <summary>
    /// The Core Shell for all entities in the game.
    /// Acts as a data container for identity, progression, and state.
    /// Delegates heavy logic (math, growth, damage) to specialized Logic Components.
    /// </summary>
    public class Combatant
    {
        #region Identity & Control

        public string SourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ClassType Class { get; set; } = ClassType.Human;
        public ControllerType Controller { get; set; } = ControllerType.AI;
        public ControlState BattleControl { get; set; } = ControlState.ActFreely;
        public int PartySlot { get; set; } = -1;
        public string OwnerId { get; set; } = string.Empty;

        #endregion

        #region Progression Data

        public int Level { get; set; } = 1;
        public int Exp { get; set; }
        public int StatPoints { get; set; }
        public int BaseHP { get; set; }
        public int BaseSP { get; set; }

        // Total experience gained through gameplay. Used for Fusion inheritance math.
        public int LifetimeEarnedExp { get; set; } = 0;

        // Calculated property based on the Cubic curve defined in GrowthProcessor.
        public int ExpRequired => GrowthProcessor.GetExpRequired(Level);

        #endregion

        #region Resource Pools

        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxSP { get; set; }
        public int CurrentSP { get; set; }

        #endregion

        #region Battle States & Ailments

        public bool IsGuarding { get; set; }
        public bool IsDead => CurrentHP <= 0;

        /// <summary>
        /// Technical check for Physical 100% Critical state.
        /// A body is 'Rigid' if it is immobilized by elemental or physical forces.
        /// </summary>
        public bool IsRigidBody => CurrentAilment != null && (
            CurrentAilment.Name.Equals("Freeze", StringComparison.OrdinalIgnoreCase) ||
            CurrentAilment.Name.Equals("Shock", StringComparison.OrdinalIgnoreCase) ||
            CurrentAilment.Name.Equals("Bind", StringComparison.OrdinalIgnoreCase) ||
            CurrentAilment.Name.Equals("Stun", StringComparison.OrdinalIgnoreCase));

        public bool IsCharged { get; set; } // Physical multiplier flag
        public bool IsMindCharged { get; set; } // Magic multiplier flag

        public bool PhysKarnActive { get; set; } // Tetrakarn shield
        public bool MagicKarnActive { get; set; } // Makarakarn shield

        public AilmentData? CurrentAilment { get; private set; }
        public int AilmentDuration { get; set; }

        // Tracks active "Breaks" (elemental resistance removal) and their durations.
        public Dictionary<Element, int> BrokenAffinities { get; set; } = new Dictionary<Element, int>();

        // Tracks Kaja/Nda stat modifications [-4 to +4].
        public Dictionary<string, int> Buffs { get; set; } = new Dictionary<string, int>();

        #endregion

        #region Stocks & Skills

        public List<Persona> PersonaStock { get; set; } = new List<Persona>();
        public List<Combatant> DemonStock { get; set; } = new List<Combatant>();
        public List<string> ExtraSkills { get; set; } = new List<string>();
        public Persona? ActivePersona { get; set; }

        #endregion

        #region Equipment Slots

        public WeaponData? EquippedWeapon { get; set; }
        public ArmorData? EquippedArmor { get; set; }
        public BootData? EquippedBoots { get; set; }
        public AccessoryData? EquippedAccessory { get; set; }

        #endregion

        #region Computed Logic Properties

        public Element WeaponElement => EquippedWeapon != null ? ElementHelper.FromCategory(EquippedWeapon.Type) : Element.Strike;
        public bool IsLongRange => EquippedWeapon != null && EquippedWeapon.IsLongRange;

        #endregion

        #region Stats

        // The raw points allocated to this combatant, prior to Persona or Equipment modification.
        public Dictionary<StatType, int> CharacterStats { get; set; } = new Dictionary<StatType, int>();

        // Proxy to the StatProcessor. Calculates the final usable value including all modifiers.
        public int GetStat(StatType type) => StatProcessor.GetStat(this, type);

        public int GetDefense() => EquippedArmor?.Defense ?? 0;

        public int GetEvasion()
        {
            int eva = 0;
            if (EquippedArmor != null) eva += EquippedArmor.Evasion;
            if (EquippedBoots != null) eva += EquippedBoots.Evasion;
            return eva;
        }

        #endregion

        #region Constructor

        public Combatant(string name, ClassType type = ClassType.Human)
        {
            Name = name;
            Class = type;

            // Initialize standard stat array at baseline (2) for Humanoids
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
            {
                CharacterStats[t] = 2;
            }

            BaseHP = 20;
            BaseSP = 6;
        }

        #endregion

        #region Logic Proxies (Wrappers for established Processors)

        // Proxy to GrowthProcessor. Syncs Max pools based on current level and Vitality/Magic.
        public void RecalculateResources() => GrowthProcessor.RecalculateResources(this);

        // Proxy to GrowthProcessor. Handles experience gain and level-up randomized growth.
        public void GainExp(int amount) => GrowthProcessor.GainExp(this, amount);

        // Proxy to GrowthProcessor. Handles manual stat point allocation.
        public void AllocateStat(StatType type) => GrowthProcessor.AllocateStat(this, type);


        // Proxy to DamageHandler. Processes affinities and applies HP/SP changes.
        public CombatResult ReceiveDamage(int damage, Element element, bool isCritical)
            => DamageHandler.ApplyDamage(this, damage, element, isCritical);

        #endregion

        #region State Management

        public List<string> GetConsolidatedSkills()
        {
            List<string> skills = new List<string>();
            if (ActivePersona != null) skills.AddRange(ActivePersona.SkillSet);
            skills.AddRange(ExtraSkills);
            return skills.Distinct().ToList();
        }

        public void AddBuff(string buffType, int turns)
        {
            if (Buffs.ContainsKey(buffType)) Buffs[buffType] = Math.Max(Buffs[buffType], turns);
            else Buffs[buffType] = turns;
        }

        public bool InflictAilment(AilmentData ailment, int duration = 3)
        {
            if (IsGuarding) return false;
            if (CurrentAilment != null) return false;
            CurrentAilment = ailment;
            AilmentDuration = duration;
            return true;
        }

        public void RemoveAilment()
        {
            CurrentAilment = null;
            AilmentDuration = 0;
        }

        public bool CheckCure(string skillEffect)
        {
            if (CurrentAilment == null) return false;
            if (skillEffect.Contains("Cure All", StringComparison.OrdinalIgnoreCase))
            {
                RemoveAilment();
                return true;
            }
            if (!string.IsNullOrEmpty(CurrentAilment.CureKeyword) &&
                skillEffect.Contains(CurrentAilment.CureKeyword, StringComparison.OrdinalIgnoreCase))
            {
                RemoveAilment();
                return true;
            }
            return false;
        }

        public void CleanupBattleState()
        {
            IsGuarding = false;
            IsCharged = false;
            IsMindCharged = false;
            PhysKarnActive = false;
            MagicKarnActive = false;
            BrokenAffinities.Clear();
            Buffs.Clear();
            CurrentAilment = null;
            AilmentDuration = 0;
        }

        // Handles turn-based decay for Buffs, Elemental Breaks, and Karn Shields.
        public List<string> TickBuffs()
        {
            var messages = new List<string>();

            // 1. Tick down Stat Buffs
            var keys = Buffs.Keys.ToList();
            foreach (var k in keys)
            {
                if (Buffs.ContainsKey(k) && Buffs[k] > 0)
                {
                    Buffs[k]--;
                    if (Buffs[k] == 0) messages.Add($"{Name}'s {k} effect wore off.");
                }
            }

            // 2. Tick down Elemental Breaks
            var breakKeys = BrokenAffinities.Keys.ToList();
            foreach (var b in breakKeys)
            {
                if (BrokenAffinities.ContainsKey(b) && BrokenAffinities[b] > 0)
                {
                    BrokenAffinities[b]--;
                    if (BrokenAffinities[b] == 0)
                    {
                        BrokenAffinities.Remove(b);
                        messages.Add($"{Name}'s {b} resistance returned.");
                    }
                }
            }
            return messages;
        }

        // Specifically used to dissolve shields if they weren't triggered by the end of a phase.
        public void DissolveShields()
        {
            PhysKarnActive = false;
            MagicKarnActive = false;
        }

        #endregion
    }
}