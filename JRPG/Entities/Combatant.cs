using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Logic;

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
                CharacterStats[t] = 10;
            BaseHP = 100;
            BaseSP = 40;
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
                    c.CharacterStats[stat] = kvp.Value;
            }

            if (!string.IsNullOrEmpty(data.PersonaId) && Database.Personas.TryGetValue(data.PersonaId, out var pData))
            {
                c.ActivePersona = pData.ToPersona();
                c.ActivePersona.Level = c.Level;
                if (data.Skills != null)
                {
                    foreach (var s in data.Skills)
                        if (!c.ActivePersona.SkillSet.Contains(s)) c.ActivePersona.SkillSet.Add(s);
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

            foreach (StatType t in Enum.GetValues(typeof(StatType)))
                c.CharacterStats[t] = 0;

            c.ActivePersona = pData.ToPersona();

            // Scale persona to target level to get correct stats
            c.ActivePersona.ScaleToLevel(level);

            // CORRECTED: Calculate Base HP/SP from the scaled Persona stats
            // SMT Logic: HP ~= (Lvl + END) * Multiplier
            int end = c.GetStat(StatType.END);
            int mag = c.GetStat(StatType.MAG);

            c.BaseHP = (int)((level * 5) + (end * 4));
            c.BaseSP = (int)((level * 2) + (mag * 2));

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
            // --- DEMON LOGIC ---
            // Demons are physical manifestations of Personas.
            // They do not have "Base Stats" + "Modifiers". The Persona stats ARE their stats.
            if (Class == ClassType.Demon)
            {
                if (ActivePersona == null) return 0;
                return ActivePersona.StatModifiers.ContainsKey(type) ? ActivePersona.StatModifiers[type] : 0;
            }

            // --- HUMAN/OPERATOR LOGIC ---
            int charVal = CharacterStats.ContainsKey(type) ? CharacterStats[type] : 0;
            if (EquippedAccessory != null)
            {
                if (Enum.TryParse(EquippedAccessory.ModifierStat, true, out StatType accStat))
                    if (accStat == type) charVal += EquippedAccessory.ModifierValue;
            }

            if (Class == ClassType.Operator) return charVal;

            if (type == StatType.CHA || type == StatType.INT) return charVal;
            if (ActivePersona == null || !ActivePersona.StatModifiers.ContainsKey(type)) return charVal;

            int personaVal = ActivePersona.StatModifiers[type];
            double finalValue = type switch
            {
                StatType.STR => charVal + (personaVal * 0.4),
                StatType.MAG => charVal + (personaVal * 0.4),
                StatType.END => charVal + (personaVal * 0.25),
                StatType.AGI => charVal + (personaVal * 0.25),
                StatType.LUK => charVal + (personaVal * 0.5),
                _ => charVal
            };

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

            // Demons rely on MAG for SP calculation if INT is missing/zero
            if (Class == ClassType.Demon) totalInt = GetStat(StatType.MAG);

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
            int oldMaxHP = MaxHP;
            int oldMaxSP = MaxSP;

            if (Class != ClassType.Demon)
            {
                BaseHP += rnd.Next(6, 11);
                BaseSP += rnd.Next(3, 8);
            }
            else
            {
                // Demons get HP/SP boost from stats increasing via Persona scaling
                // BaseHP here acts as the "Race Bonus"
                BaseHP += rnd.Next(3, 6);
                BaseSP += rnd.Next(2, 4);
            }

            RecalculateResources();

            int hpGain = MaxHP - oldMaxHP;
            int spGain = MaxSP - oldMaxSP;
            CurrentHP += hpGain;
            CurrentSP += spGain;
        }

        public void CleanupBattleState()
        {
            IsGuarding = false;
            Buffs.Clear();
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