namespace JRPGPrototype
{
    public class Combatant
    {
        public string Name { get; set; }
        public bool IsPlayer { get; set; }

        // Resources
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxSP { get; set; }
        public int CurrentSP { get; set; }

        // Base Stats
        public Dictionary<StatType, int> Stats { get; set; }

        // The equipped Persona (Null for basic enemies)
        public Persona ActivePersona { get; set; }

        public Combatant(string name, int str, int mag, int agi, int hp, int sp)
        {
            Name = name;
            Stats = new Dictionary<StatType, int>
            {
                { StatType.STR, str },
                { StatType.MAG, mag },
                { StatType.AGI, agi }
                // Add others (LUK, CHA, etc.)
            };
            MaxHP = hp; CurrentHP = hp;
            MaxSP = sp; CurrentSP = sp;
        }

        // Calculation for taking damage
        public void ReceiveDamage(int damage, Element element)
        {
            Affinity aff = Affinity.Normal;

            // If we have a Persona, check its affinities
            if (ActivePersona != null)
            {
                aff = ActivePersona.GetAffinity(element);
            }

            int finalDamage = damage;
            string message = "";

            switch (aff)
            {
                case Affinity.Weak:
                    finalDamage = (int)(damage * 1.5f);
                    message = "WEAKNESS STRUCK! (One More!)";
                    // TODO: Trigger 'One More' state here
                    break;
                case Affinity.Resist:
                    finalDamage = (int)(damage * 0.5f);
                    message = "Resisted.";
                    break;
                case Affinity.Null:
                    finalDamage = 0;
                    message = "Blocked!";
                    break;
                case Affinity.Repel:
                    message = "Repelled!";
                    // Logic to reflect damage back would go here
                    return;
                case Affinity.Absorb:
                    CurrentHP += damage;
                    if (CurrentHP > MaxHP) CurrentHP = MaxHP;
                    Console.WriteLine($"{Name} absorbed {damage} HP!");
                    return;
            }

            CurrentHP -= finalDamage;
            Console.WriteLine($"{Name} took {finalDamage} damage ({element}). {message}");
            if (CurrentHP <= 0) Console.WriteLine($"{Name} has collapsed.");
        }
    }
}