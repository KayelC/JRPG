class Program
{
    static void Main(string[] args)
    {
        // 1. Initialize Database
        // In a real app, read the file content using System.IO.File.ReadAllText
        string mockJson = System.IO.File.ReadAllText("skills_by_category.json");
        JRPGPrototype.SkillDatabase.LoadSkills(mockJson);

        // 2. Create Player (Protagonist)
        var player = new JRPGPrototype.Combatant("Protagonist", str: 10, mag: 12, agi: 8, hp: 120, sp: 80);
        player.IsPlayer = true;

        // Give player a Persona
        player.ActivePersona = new JRPGPrototype.Persona { Name = "Orpheus", Level = 1 };
        player.ActivePersona.SkillSet.Add("Agi"); // Fire
        player.ActivePersona.SkillSet.Add("Bash"); // Strike
        player.ActivePersona.Affinities.Add(JRPGPrototype.Element.Elec, JRPGPrototype.Affinity.Weak); // Weak to Elec
        player.ActivePersona.Affinities.Add(JRPGPrototype.Element.Fire, JRPGPrototype.Affinity.Resist); // Resist Fire

        // 3. Create Enemy
        var enemy = new JRPGPrototype.Combatant("Shadow Blob", str: 8, mag: 5, agi: 5, hp: 50, sp: 0);
        // Enemies might not have Personas, but they have affinities directly. 
        // For this prototype, we can assign a "Dummy Persona" to hold affinities.
        enemy.ActivePersona = new JRPGPrototype.Persona { Name = "ShadowSelf" };
        enemy.ActivePersona.Affinities.Add(JRPGPrototype.Element.Fire, JRPGPrototype.Affinity.Weak); // Weak to Fire!

        // 4. Combat Loop
        Console.WriteLine("--- BATTLE START ---");

        while (player.CurrentHP > 0 && enemy.CurrentHP > 0)
        {
            Console.WriteLine($"\n{player.Name}: HP {player.CurrentHP}/{player.MaxHP} | SP {player.CurrentSP}/{player.MaxSP}");
            Console.WriteLine($"{enemy.Name}: HP {enemy.CurrentHP}");

            // --- Player Turn ---
            Console.WriteLine("\nChoose Action: (1) Attack [Slash] (2) Skill");
            var input = Console.ReadLine();

            if (input == "1")
            {
                // Basic Slash
                int dmg = CalculatePhysDmg(player, enemy, 30);
                enemy.ReceiveDamage(dmg, JRPGPrototype.Element.Slash);
            }
            else if (input == "2")
            {
                // List Skills
                Console.WriteLine("Skills:");
                foreach (var sName in player.ActivePersona.SkillSet)
                {
                    var skillData = JRPGPrototype.SkillDatabase.AllSkills[sName];
                    Console.WriteLine($"- {sName} ({skillData.Cost})");
                }

                Console.Write("Type skill name: ");
                string skillChoice = Console.ReadLine();

                if (JRPGPrototype.SkillDatabase.AllSkills.TryGetValue(skillChoice, out var skill))
                {
                    // Deduct Cost (Simplified)
                    var cost = skill.ParseCost();
                    if (cost.isHP) player.CurrentHP -= (int)(player.MaxHP * (cost.value / 100.0f));
                    else player.CurrentSP -= cost.value;

                    // Determine Element (Naive check for prototype)
                    JRPGPrototype.Element elem = JRPGPrototype.Element.Almighty;
                    if (skill.Category.Contains("Fire")) elem = JRPGPrototype.Element.Fire;
                    if (skill.Category.Contains("Strike")) elem = JRPGPrototype.Element.Strike;
                    // ... map other categories

                    int dmg = 0;
                    if (skill.Category.Contains("Physical") || skill.Category.Contains("Slash") || skill.Category.Contains("Strike"))
                        dmg = CalculatePhysDmg(player, enemy, skill.GetPowerVal());
                    else
                        dmg = CalculateMagDmg(player, enemy, skill.GetPowerVal());

                    enemy.ReceiveDamage(dmg, elem);
                }
            }

            // Check if enemy died
            if (enemy.CurrentHP <= 0) break;

            // --- Enemy Turn (Simple AI) ---
            Console.WriteLine($"\n{enemy.Name} attacks!");
            player.ReceiveDamage(10, JRPGPrototype.Element.Strike);
        }

        Console.WriteLine("\n--- BATTLE END ---");
    }

    // Very basic damage formulas
    static int CalculatePhysDmg(JRPGPrototype.Combatant attacker, JRPGPrototype.Combatant defender, int power)
    {
        // Formula: sqrt(STR) * Power ... adjusted for game balance
        double dmg = Math.Sqrt(attacker.Stats[JRPGPrototype.StatType.STR]) * power * 0.5;
        return (int)dmg;
    }

    static int CalculateMagDmg(JRPGPrototype.Combatant attacker, JRPGPrototype.Combatant defender, int power)
    {
        // Formula: sqrt(MAG) * Power ...
        double dmg = Math.Sqrt(attacker.Stats[JRPGPrototype.StatType.MAG]) * power * 0.5;
        return (int)dmg;
    }
}