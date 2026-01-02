using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    public class Combatant
    {
        // Link back to JSON Data
        public string SourceId { get; set; }

        public string Name { get; set; } = string.Empty;

        // Progression
        public int Level { get; set; } = 1;
        public int Exp { get; set; }
        public int StatPoints { get; set; }

        // Growth Base Values
        public int BaseHP { get; set; }
        public int BaseSP { get; set; }

        // Resource Pools
        public int MaxHP { get; private set; }
        public int CurrentHP { get; set; }
        public int MaxSP { get; private set; }
        public int CurrentSP { get; set; }

        // Battle States
        public bool IsDown { get; set; }
        public bool IsDizzy { get; set; }
        public bool IsImmuneToDown { get; set; }

        // Ailment State
        public AilmentData CurrentAilment { get; private set; }
        public int AilmentDuration { get; set; }

        // Buff State
        public Dictionary<string, int> Buffs { get; set; } = new Dictionary<string, int>();

        // --- EQUIPMENT SLOTS ---
        public WeaponData EquippedWeapon { get; set; }
        public ArmorData EquippedArmor { get; set; }
        public BootData EquippedBoots { get; set; }
        public AccessoryData EquippedAccessory { get; set; }

        // Computed Properties
        public Element WeaponElement => EquippedWeapon != null ? ElementHelper.FromCategory(EquippedWeapon.Type) : Element.Strike;
        public bool IsLongRange => EquippedWeapon != null && EquippedWeapon.IsLongRange;

        // Derived Stats
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

        public Combatant(string name)
        {
            Name = name;
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
                CharacterStats[type] = 10;

            BaseHP = 100;
            BaseSP = 40;
        }

        // Factory Constructor from EnemyData
        public static Combatant CreateFromData(EnemyData data)
        {
            Combatant c = new Combatant(data.Name);
            c.SourceId = data.Id;
            c.Level = data.Level;

            // Map JSON stats (String keys) to Enum keys
            foreach (var kvp in data.Stats)
            {
                if (Enum.TryParse(kvp.Key, true, out StatType stat))
                {
                    c.CharacterStats[stat] = kvp.Value;
                }
            }

            // Create Enemy Persona (Shell) if referenced
            // Note: Enemy pool JSON usually defines persona properties directly on the enemy in simpler games,
            // but here we align with the PersonaData architecture.
            if (!string.IsNullOrEmpty(data.PersonaId) && Database.Personas.TryGetValue(data.PersonaId, out var pData))
            {
                c.ActivePersona = pData.ToPersona();
                // Override Persona level with Enemy level
                c.ActivePersona.Level = c.Level;
                // Add enemy specific skills if any
                if (data.Skills != null)
                {
                    foreach (var s in data.Skills)
                    {
                        if (!c.ActivePersona.SkillSet.Contains(s))
                            c.ActivePersona.SkillSet.Add(s);
                    }
                }
            }

            // Set Resources based on new stats
            c.RecalculateResources();
            c.CurrentHP = c.MaxHP;
            c.CurrentSP = c.MaxSP;

            return c;
        }

        public int GetStat(StatType type)
        {
            int charVal = CharacterStats.ContainsKey(type) ? CharacterStats[type] : 0;

            // 1. Accessory Modifier
            if (EquippedAccessory != null)
            {
                if (Enum.TryParse(EquippedAccessory.ModifierStat, true, out StatType accStat))
                {
                    if (accStat == type) charVal += EquippedAccessory.ModifierValue;
                }
            }

            if (type == StatType.CHA || type == StatType.INT)
                return charVal;

            if (ActivePersona == null || !ActivePersona.StatModifiers.ContainsKey(type))
                return charVal;

            int personaVal = ActivePersona.StatModifiers[type];

            // Standard stat formula merging Base + Persona
            double finalValue = type switch
            {
                StatType.STR => charVal + (personaVal * 0.4),
                StatType.MAG => charVal + (personaVal * 0.4),
                StatType.END => charVal + (personaVal * 0.25),
                StatType.AGI => charVal + (personaVal * 0.25),
                StatType.LUK => charVal + (personaVal * 0.5),
                _ => charVal
            };

            // Buff Logic
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

        // Returns a list of log messages instead of printing directly
        public List<string> TickBuffs()
        {
            var messages = new List<string>();
            var keys = Buffs.Keys.ToList();
            foreach (var k in keys)
            {
                if (Buffs[k] > 0)
                {
                    Buffs[k]--;
                    if (Buffs[k] == 0) messages.Add($"{Name}'s {k} effect wore off.");
                }
            }
            return messages;
        }

        public void RecalculateResources()
        {
            int totalEnd = GetStat(StatType.END);
            int totalInt = GetStat(StatType.INT);

            MaxHP = BaseHP + (totalEnd * 5);
            MaxSP = BaseSP + (totalInt * 3);

            CurrentHP = Math.Min(CurrentHP, MaxHP);
            CurrentSP = Math.Min(CurrentSP, MaxSP);

            if (CurrentHP <= 0 && !Name.Contains("Shadow")) CurrentHP = 1;
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
            StatPoints += 3;
            Random rnd = new Random();
            int hpGain = rnd.Next(6, 11);
            int spGain = rnd.Next(3, 8);
            BaseHP += hpGain;
            BaseSP += spGain;
            RecalculateResources();
            CurrentHP = MaxHP;
            CurrentSP = MaxSP;
        }

        public void AllocateStat(StatType type)
        {
            if (StatPoints <= 0) return;
            CharacterStats[type]++;
            StatPoints--;
            RecalculateResources();
        }

        public bool InflictAilment(AilmentData ailment, int duration = 3)
        {
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

        public bool IsRigidBody => CurrentAilment != null && (CurrentAilment.Name == "Freeze" || CurrentAilment.Name == "Shock");

        public CombatResult ReceiveDamage(int damage, Element element, bool isCritical)
        {
            Affinity aff = ActivePersona?.GetAffinity(element) ?? Affinity.Normal;
            var result = new CombatResult();
            result.IsCritical = isCritical;

            if (isCritical) damage = (int)(damage * 1.5);

            switch (aff)
            {
                case Affinity.Weak:
                    result.Type = HitType.Weakness;
                    result.DamageDealt = (int)(damage * 1.5f);
                    if (IsDown) { IsDizzy = true; result.Message = "!!! DIZZY !!!"; }
                    else if (IsImmuneToDown) result.Message = "Stood Firm!";
                    else if (IsRigidBody) result.Message = "CRITICAL (Rigid)!";
                    else { IsDown = true; result.Message = "WEAKNESS STRUCK!"; }
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
                    int heal = damage;
                    CurrentHP = Math.Min(MaxHP, CurrentHP + heal);
                    result.DamageDealt = 0;
                    result.Message = $"Absorbed {heal} HP!";
                    return result;
                default:
                    result.Type = HitType.Normal;
                    result.DamageDealt = damage;
                    if (isCritical)
                    {
                        if (IsDown) { IsDizzy = true; result.Message = "!!! DIZZY (CRIT) !!!"; }
                        else if (!IsImmuneToDown && !IsRigidBody) { IsDown = true; result.Message = "CRITICAL HIT! [DOWN]"; }
                        else result.Message = "CRITICAL HIT!";
                    }
                    break;
            }
            CurrentHP = Math.Max(0, CurrentHP - result.DamageDealt);
            return result;
        }
    }
}