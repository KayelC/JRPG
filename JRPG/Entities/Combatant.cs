using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Logic;
using JRPGPrototype.Logic.Battle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Entities
{
    public class Combatant
    {
        public string SourceId { get; set; }
        public string Name { get; set; } = string.Empty;

        // Identity & Control
        public ClassType Class { get; set; } = ClassType.Human;
        public ControllerType Controller { get; set; } = ControllerType.AI;
        public ControlState BattleControl { get; set; } = ControlState.ActFreely;
        public int PartySlot { get; set; } = -1;
        public string OwnerId { get; set; }

        // Progression
        public int Level { get; set; } = 1;
        public int Exp { get; set; }
        public int StatPoints { get; set; }
        public int BaseHP { get; set; }
        public int BaseSP { get; set; }

        // Resource Pools
        public int MaxHP { get; private set; }
        public int CurrentHP { get; set; }
        public int MaxSP { get; private set; }
        public int CurrentSP { get; set; }

        // Battle States
        public bool IsGuarding { get; set; }
        public bool IsDead => CurrentHP <= 0;
        public bool IsRigidBody => CurrentAilment != null && (CurrentAilment.Name == "Freeze" || CurrentAilment.Name == "Shock");

        // Advanced Battle States (SMT III Fidelity)
        public bool IsCharged { get; set; } // Physical 1.9x
        public bool IsMindCharged { get; set; } // Magic 1.9x

        public bool PhysKarnActive { get; set; } // Tetrakarn
        public bool MagicKarnActive { get; set; } // Makarakarn

        // Tracks active "Breaks" (Elemental resistance removal) and their turn durations
        public Dictionary<Element, int> BrokenAffinities { get; set; } = new Dictionary<Element, int>();

        public AilmentData CurrentAilment { get; private set; }
        public int AilmentDuration { get; set; }
        public Dictionary<string, int> Buffs { get; set; } = new Dictionary<string, int>();

        // --- STOCKS ---
        public List<Persona> PersonaStock { get; set; } = new List<Persona>();
        public List<Combatant> DemonStock { get; set; } = new List<Combatant>();

        // --- SKILLS ---
        public List<string> ExtraSkills { get; set; } = new List<string>();

        // --- EQUIPMENT SLOTS ---
        public WeaponData EquippedWeapon { get; set; }
        public ArmorData EquippedArmor { get; set; }
        public BootData EquippedBoots { get; set; }
        public AccessoryData EquippedAccessory { get; set; }

        // Computed Properties
        public Element WeaponElement => EquippedWeapon != null ? ElementHelper.FromCategory(EquippedWeapon.Type) : Element.Strike;
        public bool IsLongRange => EquippedWeapon != null && EquippedWeapon.IsLongRange;

        public int GetDefense() => EquippedArmor?.Defense ?? 0;
        public int GetEvasion()
        {
            int eva = 0;
            if (EquippedArmor != null) eva += EquippedArmor.Evasion;
            if (EquippedBoots != null) eva += EquippedBoots.Evasion;
            return eva;
        }

        // Stats
        public Dictionary<StatType, int> CharacterStats { get; set; } = new Dictionary<StatType, int>();
        public Persona ActivePersona { get; set; }

        public Combatant(string name, ClassType type = ClassType.Human)
        {
            Name = name;
            Class = type;

            foreach (StatType t in Enum.GetValues(typeof(StatType)))
                CharacterStats[t] = 2; // Initial stats at 2

            BaseHP = 20; // 20 + (2*5) = 30HP at start
            BaseSP = 6;  // 6 + (2*3) = 12SP at start
        }

        public static Combatant CreateFromData(EnemyData data)
        {
            Combatant c = new Combatant(data.Name, ClassType.Demon);
            c.SourceId = data.Id;
            c.Level = data.Level;
            c.Controller = ControllerType.AI;

            foreach (var kvp in data.Stats)
            {
                if (Enum.TryParse(kvp.Key, true, out StatType stat))
                    // Clamping enemy stats at 40
                    c.CharacterStats[stat] = Math.Min(40, kvp.Value);
            }

            if (!string.IsNullOrEmpty(data.PersonaId) && Database.Personas.TryGetValue(data.PersonaId, out var pData))
            {
                c.ActivePersona = pData.ToPersona();
                c.ActivePersona.Level = c.Level;
                if (data.Skills != null)
                {
                    foreach (var s in data.Skills)
                        if (!c.ActivePersona.SkillSet.Contains(s))
                            c.ActivePersona.SkillSet.Add(s);
                }
            }
            if (c.ActivePersona == null && data.Skills != null)
            {
                c.ExtraSkills.AddRange(data.Skills);
            }

            c.RecalculateResources();
            c.CurrentHP = c.MaxHP;
            c.CurrentSP = c.MaxSP;
            return c;
        }

        public static Combatant CreateDemon(string personaId, int level)
        {
            if (!Database.Personas.TryGetValue(personaId, out var pData))
                return new Combatant("Glitch", ClassType.Demon);

            Combatant c = new Combatant(pData.Name, ClassType.Demon);
            c.SourceId = personaId;
            c.Level = level;
            c.Controller = ControllerType.AI;
            c.BattleControl = ControlState.ActFreely;

            // Reset base stats to 0 for demons so they rely solely on Persona scaling
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
                c.CharacterStats[t] = 0;

            c.ActivePersona = pData.ToPersona();

            // Scale persona to target level to get correct stats
            c.ActivePersona.ScaleToLevel(level);

            // SMT Logic for Demon Base Pools: Floors are slightly lower than humans
            int end = c.GetStat(StatType.END);
            int mag = c.GetStat(StatType.MAG);

            c.BaseHP = (int)((level * 4) + (end * 2));
            c.BaseSP = (int)((level * 1.5) + (mag * 1.5));

            c.RecalculateResources();
            c.CurrentHP = c.MaxHP;
            c.CurrentSP = c.MaxSP;

            return c;
        }

        public List<string> GetConsolidatedSkills()
        {
            List<string> skills = new List<string>();
            if (ActivePersona != null) skills.AddRange(ActivePersona.SkillSet);
            skills.AddRange(ExtraSkills);
            return skills.Distinct().ToList();
        }

        public int GetStat(StatType type)
        {
            int rawStat = 0;
            // --- DEMON LOGIC ---
            // Demons are physical manifestations of Personas.
            // They do not have "Base Stats" + "Modifiers". The Persona stats ARE their stats.
            if (Class == ClassType.Demon)
            {
                rawStat = (ActivePersona == null) ? 0 : (ActivePersona.StatModifiers.ContainsKey(type) ? ActivePersona.StatModifiers[type] : 0);
            }

            // --- HUMAN/OPERATOR LOGIC ---
            else
            {
                int charVal = CharacterStats.ContainsKey(type) ? CharacterStats[type] : 0;
                if (EquippedAccessory != null)
                {
                    if (Enum.TryParse(EquippedAccessory.ModifierStat, true, out StatType accStat))
                        if (accStat == type) charVal += EquippedAccessory.ModifierValue;
                }

                if (Class == ClassType.Operator) rawStat = charVal;
                else if (ActivePersona == null || !ActivePersona.StatModifiers.ContainsKey(type)) rawStat = charVal;
                else
                {
                    int personaVal = ActivePersona.StatModifiers[type];
                    rawStat = (int)Math.Floor(type switch
                    {
                        StatType.STR => charVal + (personaVal * 0.4),
                        StatType.MAG => charVal + (personaVal * 0.4),
                        StatType.END => charVal + (personaVal * 0.25),
                        StatType.AGI => charVal + (personaVal * 0.25),
                        StatType.LUK => charVal + (personaVal * 0.5),
                        _ => charVal
                    });
                }
            }

            // Apply Global Hard Cap of 40 to any returned stat
            int cappedStat = Math.Min(40, rawStat);

            double finalValue = cappedStat;
            if (type == StatType.STR || type == StatType.MAG)
            {
                if (Buffs.ContainsKey("Attack") && Buffs["Attack"] > 0) finalValue *= 1.4;
                if (Buffs.ContainsKey("AttackDown") && Buffs["AttackDown"] > 0) finalValue *= 0.6;
            }
            if (type == StatType.END)
            {
                if (Buffs.ContainsKey("Defense") && Buffs["Defense"] > 0) finalValue *= 1.4;
                if (Buffs.ContainsKey("DefenseDown") && Buffs["DefenseDown"] > 0) finalValue *= 0.6;
            }
            if (type == StatType.AGI)
            {
                if (Buffs.ContainsKey("Agility") && Buffs["Agility"] > 0) finalValue *= 1.4;
                if (Buffs.ContainsKey("AgilityDown") && Buffs["AgilityDown"] > 0) finalValue *= 0.6;
            }

            return (int)Math.Floor(finalValue);
        }

        public void AddBuff(string buffType, int turns)
        {
            if (Buffs.ContainsKey(buffType)) Buffs[buffType] = Math.Max(Buffs[buffType], turns);
            else Buffs[buffType] = turns;
        }

        /// <summary>
        /// Handles turn-based decay for Buffs, Elemental Breaks, and Karn Shields.
        /// </summary>
        public List<string> TickBuffs()
        {
            var messages = new List<string>();

            // 1. Tick down Stat Buffs
            var keys = Buffs.Keys.ToList();
            foreach (var k in keys)
            {
                if (Buffs[k] > 0)
                {
                    Buffs[k]--;
                    if (Buffs[k] == 0) messages.Add($"{Name}'s {k} effect wore off.");
                }
            }

            // 2. Tick down Elemental Breaks
            var breakKeys = BrokenAffinities.Keys.ToList();
            foreach (var b in breakKeys)
            {
                if (BrokenAffinities[b] > 0)
                {
                    BrokenAffinities[b]--;
                    if (BrokenAffinities[b] == 0)
                    {
                        BrokenAffinities.Remove(b);
                        messages.Add($"{Name}'s {b} resistance returned.");
                    }
                }
            }

            // 3. Karn Shields Dissolve (Manual Phase Trigger logic)
            // Note: Karns are typically cleared by the Conductor at phase-end 
            // if they weren't used. This method acts as a backup sync.

            return messages;
        }

        /// <summary>
        /// Specifically used to dissolve shields if they weren't triggered by the end of a phase.
        /// </summary>
        public void DissolveShields()
        {
            PhysKarnActive = false;
            MagicKarnActive = false;
        }

        public void RecalculateResources()
        {
            int totalEnd = GetStat(StatType.END);
            int totalMag = GetStat(StatType.MAG);

            // Implementation of 666 HP and 333 SP caps
            MaxHP = Math.Min(666, BaseHP + (totalEnd * 5));
            MaxSP = Math.Min(333, BaseSP + (totalMag * 3));

            CurrentHP = Math.Min(CurrentHP, MaxHP);
            CurrentSP = Math.Min(CurrentSP, MaxSP);
        }

        public int ExpRequired => (int)(1.5 * Math.Pow(Level, 3));

        public void GainExp(int amount)
        {
            Exp += amount;
            while (Exp >= ExpRequired)
            {
                Exp -= ExpRequired;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            Level++;
            StatPoints += 1;
            Random rnd = new Random();
            int oldMaxHP = MaxHP;
            int oldMaxSP = MaxSP;

            // Base growth remains steady
            BaseHP += rnd.Next(6, 11);
            BaseSP += rnd.Next(3, 8);

            RecalculateResources();

            int hpGain = MaxHP - oldMaxHP;
            int spGain = MaxSP - oldMaxSP;
            CurrentHP += hpGain;
            CurrentSP += spGain;
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
        }

        public void AllocateStat(StatType type)
        {
            if (StatPoints <= 0) return;
            // Clamped at 40
            if (CharacterStats[type] >= 40) return;
            CharacterStats[type]++;
            StatPoints--;
            RecalculateResources();
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
            if (!string.IsNullOrEmpty(CurrentAilment.CureKeyword) && skillEffect.Contains(CurrentAilment.CureKeyword))
            {
                RemoveAilment();
                return true;
            }
            return false;
        }

        public CombatResult ReceiveDamage(int damage, Element element, bool isCritical)
        {
            Affinity aff = ActivePersona?.GetAffinity(element) ?? Affinity.Normal;
            var result = new CombatResult();

            if (IsGuarding)
            {
                damage = (int)(damage * 0.5);
                isCritical = false;
                if (aff == Affinity.Weak) aff = Affinity.Normal;
            }

            if (IsRigidBody && (element == Element.Slash || element == Element.Strike || element == Element.Pierce))
            {
                isCritical = true;
            }

            result.IsCritical = isCritical;
            if (isCritical) damage = (int)(damage * 1.5);

            switch (aff)
            {
                case Affinity.Weak:
                    result.Type = HitType.Weakness;
                    result.DamageDealt = (int)(damage * 1.5f);
                    result.Message = "WEAKNESS STRUCK!";
                    break;
                case Affinity.Resist:
                    result.Type = HitType.Normal;
                    result.DamageDealt = (int)(damage * 0.5f);
                    result.Message = isCritical ? "CRITICAL (Resisted)!" : "Resisted.";
                    break;
                case Affinity.Null:
                    result.Type = HitType.Null;
                    result.DamageDealt = 0;
                    result.Message = "Blocked!";
                    break;
                case Affinity.Repel:
                    result.Type = HitType.Repel;
                    result.DamageDealt = 0;
                    result.Message = "Repelled!";
                    break;
                case Affinity.Absorb:
                    result.Type = HitType.Absorb;
                    CurrentHP = Math.Min(MaxHP, CurrentHP + damage);
                    result.DamageDealt = 0;
                    result.Message = $"Absorbed {damage} HP!";
                    return result;
                default:
                    result.Type = HitType.Normal;
                    result.DamageDealt = damage;
                    if (isCritical) result.Message = "CRITICAL HIT!";
                    break;
            }

            CurrentHP = Math.Max(0, CurrentHP - result.DamageDealt);
            return result;
        }
    }
}