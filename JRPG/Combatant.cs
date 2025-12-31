using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    public class Combatant
    {
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

        // Buff State (Key: "Attack", "Defense", "Agility" | Value: Turns Remaining)
        public Dictionary<string, int> Buffs { get; set; } = new Dictionary<string, int>();

        // Equipment
        public WeaponData EquippedWeapon { get; set; }
        public Element WeaponElement => EquippedWeapon != null ? ElementHelper.FromCategory(EquippedWeapon.Type) : Element.Strike;
        public bool IsLongRange => EquippedWeapon != null && EquippedWeapon.IsLongRange;

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

        public int GetStat(StatType type)
        {
            int charVal = CharacterStats.ContainsKey(type) ? CharacterStats[type] : 0;

            if (type == StatType.CHA || type == StatType.INT)
                return charVal;

            if (ActivePersona == null || !ActivePersona.StatModifiers.ContainsKey(type))
                return charVal;

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

            // --- BUFF LOGIC ---
            // SMT Logic: Buffs usually give ~1.2x to 1.4x multiplier
            if (type == StatType.STR || type == StatType.MAG)
            {
                if (Buffs.ContainsKey("Attack") && Buffs["Attack"] > 0) finalValue *= 1.4; // Tarukaja
                if (Buffs.ContainsKey("AttackDown") && Buffs["AttackDown"] > 0) finalValue *= 0.6; // Tarunda
            }
            if (type == StatType.END)
            {
                if (Buffs.ContainsKey("Defense") && Buffs["Defense"] > 0) finalValue *= 1.4; // Rakukaja
                if (Buffs.ContainsKey("DefenseDown") && Buffs["DefenseDown"] > 0) finalValue *= 0.6; // Rakunda
            }
            if (type == StatType.AGI)
            {
                if (Buffs.ContainsKey("Agility") && Buffs["Agility"] > 0) finalValue *= 1.4; // Sukukaja
                if (Buffs.ContainsKey("AgilityDown") && Buffs["AgilityDown"] > 0) finalValue *= 0.6; // Sukunda
            }

            return (int)Math.Floor(finalValue);
        }

        public void AddBuff(string buffType, int turns)
        {
            if (Buffs.ContainsKey(buffType)) Buffs[buffType] = Math.Max(Buffs[buffType], turns); // Refresh or Extend
            else Buffs[buffType] = turns;
        }

        public void TickBuffs()
        {
            var keys = Buffs.Keys.ToList();
            foreach (var k in keys)
            {
                if (Buffs[k] > 0)
                {
                    Buffs[k]--;
                    if (Buffs[k] == 0) Console.WriteLine($"{Name}'s {k} effect wore off.");
                }
            }
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

        // --- Progression, Ailment, Damage Logic (Same as before) ---
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
            Console.WriteLine($"\n[LEVEL UP] {Name} reached Lv.{Level}!");
            Console.WriteLine($"-> HP +{hpGain}, SP +{spGain}");
            Console.WriteLine($"-> Gained 3 Stat Points!");
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