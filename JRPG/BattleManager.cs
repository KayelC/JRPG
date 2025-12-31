using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace JRPGPrototype
{
    public class BattleManager
    {
        private Combatant _p;
        private Combatant _e;
        private Random _rnd = new Random();

        // Robust mapping for parsing Skill Descriptions -> Ailments
        private readonly Dictionary<string, string> _effectToAilmentMap = new Dictionary<string, string>
        {
            { "Poisons", "Poison" },
            { "Instills Fear", "Fear" },
            { "Panic", "Panic" }, // "Makes 1 foe Panic"
            { "Distresses", "Distress" },
            { "Charms", "Charm" },
            { "Enrages", "Rage" },
            { "Shocks", "Shock" },
            { "Freezes", "Freeze" }
        };

        public BattleManager(Combatant player, Combatant enemy)
        {
            _p = player;
            _e = enemy;
        }

        public void StartBattle()
        {
            Console.Clear();
            Console.WriteLine("=== BATTLE COMMENCE ===");

            bool playerTurn = _p.GetStat(StatType.AGI) >= _e.GetStat(StatType.AGI);

            while (_p.CurrentHP > 0 && _e.CurrentHP > 0)
            {
                Combatant active = playerTurn ? _p : _e;
                Combatant target = playerTurn ? _e : _p;

                DrawUI();

                // 1. Process Ailment Start (DoTs, Restrictions)
                bool canAct = ProcessTurnStart(active);

                if (active.IsDizzy)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[RECOVERY] {active.Name} is dizzy! They spend the turn standing up...");
                    Console.ResetColor();
                    active.IsDizzy = false;
                    active.IsDown = false;
                    active.IsImmuneToDown = true;
                    Thread.Sleep(2000);
                }
                else if (canAct)
                {
                    active.IsDown = false;

                    if (active.IsDown)
                    {
                        Console.WriteLine($"\n{active.Name} spends the turn standing up.");
                        active.IsDown = false;
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        if (active.IsImmuneToDown) active.IsImmuneToDown = false;

                        // Check for Rage (Forced Action)
                        if (active.CurrentAilment?.ActionRestriction == "ForceAttack")
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\n{active.Name} is Enraged! Attacking automatically!");
                            Console.ResetColor();

                            // Rage Extra Turns Logic (JSON says extra_turns: 1)
                            int attacks = 1 + active.CurrentAilment.ExtraTurns;
                            for (int i = 0; i < attacks; i++)
                            {
                                if (target.CurrentHP <= 0) break;
                                PerformMove(active, target, "Attack", 30, Element.Strike);
                                Thread.Sleep(800);
                            }
                        }
                        else
                        {
                            // Normal Turn
                            bool getOneMore = playerTurn ? ExecutePlayerTurn() : ExecuteEnemyTurn();

                            // One More Logic
                            if (getOneMore && target.CurrentHP > 0)
                            {
                                Console.BackgroundColor = ConsoleColor.DarkBlue;
                                Console.WriteLine("\n >>> ONE MORE! <<< ");
                                Console.ResetColor();
                                Thread.Sleep(1000);
                                DrawUI();
                                if (playerTurn) ExecutePlayerTurn(); else ExecuteEnemyTurn();
                            }
                        }
                    }
                }

                // 2. Process Turn End (Poison damage, duration tick)
                ProcessTurnEnd(active);

                playerTurn = !playerTurn;
                Thread.Sleep(1200);
            }

            Console.WriteLine(_p.CurrentHP > 0 ? "\n[VICTORY] Shadow Dissipated." : "\n[GAMEOVER] The journey ends here...");
        }

        private bool ProcessTurnStart(Combatant c)
        {
            if (c.CurrentAilment == null) return true;

            Console.ForegroundColor = ConsoleColor.Magenta;
            bool act = true;

            switch (c.CurrentAilment.ActionRestriction)
            {
                case "SkipTurn": // Freeze, Shock
                    Console.WriteLine($"\n{c.Name} is {c.CurrentAilment.Name} and cannot move!");
                    act = false;
                    break;
                case "ChanceSkipOrFlee": // Fear
                    int roll = _rnd.Next(100);
                    if (roll < 30) { Console.WriteLine($"\n{c.Name} is paralyzed by Fear!"); act = false; }
                    else if (roll < 50) { Console.WriteLine($"\n{c.Name} runs away in Fear! (Battle End - Not Impl)"); act = false; }
                    break;
                case "ChanceSkip": // Panic
                    // Panic blocks skills (Menu) AND has a chance to skip turn entirely
                    if (_rnd.Next(100) < 40) { Console.WriteLine($"\n{c.Name} is Panicking and does nothing!"); act = false; }
                    break;
                case "ConfusedAction": // Charm
                    Console.WriteLine($"\n{c.Name} is Charmed!");
                    if (_rnd.Next(100) < 50)
                    {
                        Console.WriteLine("...and heals the enemy!");
                        Combatant foe = (c == _p) ? _e : _p;
                        foe.CurrentHP = Math.Min(foe.MaxHP, foe.CurrentHP + 50);
                        act = false;
                    }
                    else
                    {
                        Console.WriteLine("...and attacks themselves!");
                        PerformMove(c, c, "Attack", 20, Element.Strike);
                        act = false;
                    }
                    break;
            }

            Console.ResetColor();
            return act;
        }

        private void ProcessTurnEnd(Combatant c)
        {
            if (c.CurrentAilment == null) return;

            // Dot Damage
            if (c.CurrentAilment.DotPercent > 0)
            {
                int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent);
                c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n{c.Name} takes {dmg} damage from {c.CurrentAilment.Name}.");
                Console.ResetColor();
            }

            // Duration and Removal
            c.AilmentDuration--;
            if (c.AilmentDuration <= 0)
            {
                Console.WriteLine($"\n{c.Name} recovered from {c.CurrentAilment.Name}!");
                c.RemoveAilment();
            }
        }

        private void DrawUI()
        {
            Console.Clear();
            Console.WriteLine("=================================================");
            // Enemy
            Console.ForegroundColor = ConsoleColor.Red;
            string eStatus = _e.CurrentAilment != null ? $" [{_e.CurrentAilment.Name}]" : "";
            if (_e.IsDizzy) eStatus += " [DIZZY]";
            else if (_e.IsDown) eStatus += " [DOWN]";
            else if (_e.IsImmuneToDown) eStatus += " [GUARD]";

            Console.WriteLine($"{_e.Name.ToUpper()} (Lv.{_e.ActivePersona?.Level}){eStatus}");
            Console.WriteLine($"HP: {_e.CurrentHP}/{_e.MaxHP}");
            Console.ResetColor();

            Console.WriteLine("\n vs\n");

            // Player
            Console.ForegroundColor = ConsoleColor.Cyan;
            string pStatus = _p.CurrentAilment != null ? $" [{_p.CurrentAilment.Name}]" : "";
            if (_p.IsDizzy) pStatus += " [DIZZY]";
            else if (_p.IsDown) pStatus += " [DOWN]";
            else if (_p.IsImmuneToDown) pStatus += " [GUARD]";

            Console.WriteLine($"{_p.Name} [Persona: {_p.ActivePersona?.Name}]{pStatus}");
            Console.WriteLine($"HP: {_p.CurrentHP}/{_p.MaxHP} | SP: {_p.CurrentSP}/{_p.MaxSP}");
            Console.ResetColor();
            Console.WriteLine("=================================================");
        }

        private bool ExecutePlayerTurn()
        {
            bool isPanicked = _p.CurrentAilment?.Name == "Panic";

            while (true) // Menu Loop
            {
                Console.WriteLine($"\nActions: [1] Skill {(isPanicked ? "(Blocked)" : "")} [2] Attack");
                Console.Write("> ");
                string choice = Console.ReadLine();

                if (choice == "2")
                {
                    return PerformMove(_p, _e, "Attack", 30, Element.Slash);
                }
                else if (choice == "1")
                {
                    if (isPanicked)
                    {
                        Console.WriteLine("You are Panicking and cannot use skills!");
                        continue; // Loop back
                    }

                    var skills = _p.ActivePersona.SkillSet;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        if (Database.Skills.TryGetValue(skills[i], out var s))
                            Console.WriteLine($"{i + 1}. {skills[i]} ({s.Cost}) - {s.Effect}");
                    }

                    Console.Write("Select Skill # (or 'b' for back): ");
                    string input = Console.ReadLine();

                    if (input.ToLower() == "b")
                    {
                        continue; // Loop back to [1] Skill [2] Attack
                    }

                    if (int.TryParse(input, out int idx) && idx > 0 && idx <= skills.Count)
                    {
                        string sName = skills[idx - 1];
                        if (Database.Skills.TryGetValue(sName, out var sData))
                        {
                            var cost = sData.ParseCost();
                            if (cost.isHP)
                            {
                                if (_p.CurrentHP > cost.value) _p.CurrentHP -= cost.value;
                                else { Console.WriteLine("Not enough HP!"); continue; }
                            }
                            else
                            {
                                if (_p.CurrentSP >= cost.value) _p.CurrentSP -= cost.value;
                                else { Console.WriteLine("Not enough SP!"); continue; }
                            }

                            // Check Cures (Skills that cure ailments)
                            _e.CheckCure(sData.Effect);
                            _p.CheckCure(sData.Effect);

                            return PerformMove(_p, _e, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
                        }
                    }
                    Console.WriteLine("Invalid selection.");
                }
                else
                {
                    Console.WriteLine("Invalid choice.");
                }
            }
        }

        private bool ExecuteEnemyTurn()
        {
            var skills = _e.ActivePersona.SkillSet;
            string sName = (_rnd.Next(100) < 70 && skills.Count > 0) ? skills[_rnd.Next(skills.Count)] : "Attack";

            // Enemy Panic Check
            if (_e.CurrentAilment?.Name == "Panic") sName = "Attack";

            if (sName != "Attack" && Database.Skills.TryGetValue(sName, out var sData))
            {
                _p.CheckCure(sData.Effect);
                _e.CheckCure(sData.Effect);
                return PerformMove(_e, _p, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
            }

            return PerformMove(_e, _p, "Attack", 25, Element.Strike);
        }

        private bool PerformMove(Combatant user, Combatant target, string name, int power, Element elem)
        {
            Console.WriteLine($"\n{user.Name} invokes {name}!");

            // --- 1. Accuracy Check ---
            int baseAcc = 95;
            if (Database.Skills.TryGetValue(name, out var sd))
                int.TryParse(sd.Accuracy?.Replace("%", ""), out baseAcc);

            double tEvasionMult = target.CurrentAilment?.EvasionMult ?? 1.0;

            if (target.IsDown || target.IsDizzy) tEvasionMult = 0.0;

            int uAgi = user.GetStat(StatType.AGI);
            int tAgi = (int)(target.GetStat(StatType.AGI) * tEvasionMult);

            int finalHit = Math.Clamp(baseAcc + (uAgi - tAgi), 5, 99);
            if (tEvasionMult == 0.0) finalHit = 100; // Guaranteed hit if 0 evasion

            if (_rnd.Next(1, 101) > finalHit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"-> MISS! {target.Name} evaded the attack.");
                Console.ResetColor();

                // Melee Miss Penalty
                if (elem == Element.Slash || elem == Element.Strike)
                {
                    Console.WriteLine($"-> {user.Name} lost balance and fell!");
                    user.IsDown = true;
                }
                return false;
            }

            // --- 2. Damage Math ---
            bool isPhys = (elem <= Element.Pierce);
            int atk = isPhys ? user.GetStat(StatType.STR) : user.GetStat(StatType.MAG);
            int def = target.GetStat(StatType.END);

            if (user.CurrentAilment != null) atk = (int)(atk * user.CurrentAilment.DamageDealMult);
            if (target.IsDown || target.IsDizzy) def = (int)(def * 0.5);

            double dmgBase = Math.Sqrt(power) * ((double)atk / Math.Max(1, def)) * 7;

            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            // --- 3. Resolve & Infliction ---
            bool wasAlreadyDown = target.IsDown;
            var res = target.ReceiveDamage(damage, elem);

            Console.WriteLine($"{target.Name} takes {res.DamageDealt} {elem} dmg. {res.Message}");

            // Robust Infliction Logic
            if (Database.Skills.TryGetValue(name, out var skillData))
            {
                foreach (var kvp in _effectToAilmentMap)
                {
                    if (skillData.Effect.Contains(kvp.Key))
                    {
                        if (Database.Ailments.TryGetValue(kvp.Value, out var ailmentData))
                        {
                            // Parse Chance
                            int chance = 100;
                            var matchChance = Regex.Match(skillData.Effect, @"\((\d+)% chance\)");
                            if (matchChance.Success) int.TryParse(matchChance.Groups[1].Value, out chance);

                            // Apply Ailment Boost (if passive implemented later, add here)

                            if (_rnd.Next(100) < chance)
                            {
                                if (target.InflictAilment(ailmentData, 3))
                                {
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine($"-> {target.Name} is afflicted with {ailmentData.Name}!");
                                    Console.ResetColor();
                                }
                            }
                        }
                    }
                }
            }

            // One More condition: Weakness AND Not Already Down AND Not Immune AND User is Not Afflicted
            bool canOneMore = (res.Type == HitType.Weakness && !wasAlreadyDown && !target.IsImmuneToDown);
            if (user.CurrentAilment != null && canOneMore)
            {
                Console.WriteLine($"-> {user.Name} hit a weakness, but suffering from {user.CurrentAilment.Name} prevented a One More!");
                return false;
            }

            return canOneMore;
        }
    }
}