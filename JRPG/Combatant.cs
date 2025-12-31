using System;
using System.Collections.Generic;

namespace JRPGPrototype
{
    public class Combatant
    {
        public string Name { get; set; }

        // Resource Pools
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxSP { get; set; }
        public int CurrentSP { get; set; }

        // Battle States
        public bool IsDown { get; set; }
        public bool IsDizzy { get; set; }
        public bool IsImmuneToDown { get; set; }

        // Ailment State
        public AilmentData CurrentAilment { get; private set; }
        public int AilmentDuration { get; set; }

        // Weapon Configuration (New)
        public Element WeaponElement { get; set; } = Element.Slash;
        public bool IsLongRange { get; set; } = false;

        // The "Operator" base stats
        public Dictionary<StatType, int> CharacterStats { get; set; } = new Dictionary<StatType, int>();

        // The equipped Persona
        public Persona ActivePersona { get; set; }

        public Combatant(string name)
        {
            Name = name;
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
                CharacterStats[type] = 10;

            // Default Weapon Config
            if (name == "Hero") { WeaponElement = Element.Slash; IsLongRange = false; }
            else { WeaponElement = Element.Strike; IsLongRange = false; }
        }

        public int GetStat(StatType type)
        {
            int charVal = CharacterStats.ContainsKey(type) ? CharacterStats[type] : 0;
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
            return (int)Math.Floor(finalValue);
        }

        public void RecalculateResources()
        {
            int totalEnd = GetStat(StatType.END);
            int totalInt = GetStat(StatType.INT);
            MaxHP = 100 + (totalEnd * 10);
            MaxSP = 40 + (totalInt * 5);
            CurrentHP = MaxHP;
            CurrentSP = MaxSP;
        }

        // Ailment Logic
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

        // Rigid Body Exception: Frozen or Shocked targets are physically locked
        public bool IsRigidBody => CurrentAilment != null &&
                                   (CurrentAilment.Name == "Freeze" || CurrentAilment.Name == "Shock");

        public CombatResult ReceiveDamage(int damage, Element element, bool isCritical)
        {
            Affinity aff = ActivePersona?.GetAffinity(element) ?? Affinity.Normal;
            var result = new CombatResult();
            result.IsCritical = isCritical;

            // Critical Damage Multiplier (1.5x)
            if (isCritical) damage = (int)(damage * 1.5);

            switch (aff)
            {
                case Affinity.Weak:
                    result.Type = HitType.Weakness;
                    result.DamageDealt = (int)(damage * 1.5f);

                    if (IsDown)
                    {
                        IsDizzy = true;
                        result.Message = "!!! DIZZY !!!";
                    }
                    else
                    {
                        // Immunity Logic
                        if (IsImmuneToDown)
                        {
                            result.Message = "Stood Firm!";
                        }
                        // Rigid Body Exception: Do not knock down if Frozen/Shocked
                        else if (IsRigidBody)
                        {
                            result.Message = "CRITICAL HIT! (Rigid)";
                        }
                        else
                        {
                            IsDown = true;
                            result.Message = "WEAKNESS STRUCK!";
                        }
                    }
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

                default: // Normal
                    result.Type = HitType.Normal;
                    result.DamageDealt = damage;

                    // Critical Knockdown Logic (Normal Affinity)
                    if (isCritical)
                    {
                        if (IsDown)
                        {
                            IsDizzy = true;
                            result.Message = "!!! DIZZY (CRIT) !!!";
                        }
                        else if (!IsImmuneToDown && !IsRigidBody)
                        {
                            IsDown = true;
                            result.Message = "CRITICAL HIT! [DOWN]";
                        }
                        else
                        {
                            result.Message = "CRITICAL HIT!";
                        }
                    }
                    break;
            }

            CurrentHP = Math.Max(0, CurrentHP - result.DamageDealt);
            return result;
        }
    }
}