using System;
using System.Threading;

namespace JRPGPrototype
{
    public class BattleManager
    {
        private Combatant _p;
        private Combatant _e;
        private Random _rnd = new Random();

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

                if (active.IsDizzy)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[RECOVERY] {active.Name} is dizzy! They spend the turn standing up...");
                    Console.ResetColor();
                    active.IsDizzy = false;
                    active.IsDown = false;
                    Thread.Sleep(2000);
                }
                else
                {
                    active.IsDown = false; // Normal stand up

                    bool getOneMore = playerTurn ? ExecutePlayerTurn() : ExecuteEnemyTurn();

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

                playerTurn = !playerTurn;
                Thread.Sleep(1200);
            }

            Console.WriteLine(_p.CurrentHP > 0 ? "\n[VICTORY] Shadow Dissipated." : "\n[GAMEOVER] The journey ends here...");
        }

        private void DrawUI()
        {
            Console.Clear();
            Console.WriteLine("=================================================");
            // Enemy
            Console.ForegroundColor = ConsoleColor.Red;
            string eStatus = _e.IsDizzy ? " [DIZZY]" : (_e.IsDown ? " [DOWN]" : "");
            Console.WriteLine($"{_e.Name.ToUpper()} (Lv.{_e.ActivePersona?.Level}){eStatus}");
            Console.WriteLine($"HP: {_e.CurrentHP}/{_e.MaxHP}");
            Console.ResetColor();

            Console.WriteLine("\n        vs\n");

            // Player
            Console.ForegroundColor = ConsoleColor.Cyan;
            string pStatus = _p.IsDizzy ? " [DIZZY]" : (_p.IsDown ? " [DOWN]" : "");
            Console.WriteLine($"{_p.Name} [Persona: {_p.ActivePersona?.Name}]{pStatus}");
            Console.WriteLine($"HP: {_p.CurrentHP}/{_p.MaxHP} | SP: {_p.CurrentSP}/{_p.MaxSP}");
            Console.ResetColor();
            Console.WriteLine("=================================================");
        }

        private bool ExecutePlayerTurn()
        {
            Console.WriteLine("\nActions: [1] Skill  [2] Attack");
            Console.Write("> ");
            string choice = Console.ReadLine();

            if (choice == "2") return PerformMove(_p, _e, "Attack", 30, Element.Slash);

            var skills = _p.ActivePersona.SkillSet;
            for (int i = 0; i < skills.Count; i++)
            {
                if (Database.Skills.TryGetValue(skills[i], out var s))
                    Console.WriteLine($"{i + 1}. {skills[i]} ({s.Cost}) - {s.Effect}");
            }

            Console.Write("Select Skill # (or 'b' for back): ");
            string input = Console.ReadLine();
            if (input.ToLower() == "b") return true;

            if (int.TryParse(input, out int idx) && idx > 0 && idx <= skills.Count)
            {
                string sName = skills[idx - 1];
                if (Database.Skills.TryGetValue(sName, out var sData))
                {
                    var cost = sData.ParseCost();
                    if (cost.isHP) { _p.CurrentHP -= cost.value; } else { _p.CurrentSP -= cost.value; }
                    return PerformMove(_p, _e, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
                }
            }
            return false;
        }

        private bool ExecuteEnemyTurn()
        {
            var skills = _e.ActivePersona.SkillSet;
            string sName = (_rnd.Next(100) < 70 && skills.Count > 0) ? skills[_rnd.Next(skills.Count)] : "Attack";

            if (sName != "Attack" && Database.Skills.TryGetValue(sName, out var sData))
                return PerformMove(_e, _p, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));

            return PerformMove(_e, _p, "Attack", 25, Element.Strike);
        }

        private bool PerformMove(Combatant user, Combatant target, string name, int power, Element elem)
        {
            Console.WriteLine($"\n{user.Name} invokes {name}!");

            // 1. Accuracy Check
            int baseAcc = 95;
            if (Database.Skills.TryGetValue(name, out var sd)) int.TryParse(sd.Accuracy?.Replace("%", ""), out baseAcc);

            int uAgi = user.GetStat(StatType.AGI);
            int tAgi = target.GetStat(StatType.AGI);
            int finalHit = Math.Clamp(baseAcc + (uAgi - tAgi), 5, 99);

            Console.WriteLine($"[DEBUG] Acc Check: Base({baseAcc}) + AgiDiff({uAgi - tAgi}) = {finalHit}%");

            if (_rnd.Next(1, 101) > finalHit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"-> MISS! {target.Name} evaded the attack.");
                Console.ResetColor();
                return false;
            }

            // 2. Damage Math
            bool isPhys = (elem <= Element.Pierce);
            int atk = isPhys ? user.GetStat(StatType.STR) : user.GetStat(StatType.MAG);
            int def = target.GetStat(StatType.END);

            double dmgBase = Math.Sqrt(power) * ((double)atk / def) * 7;
            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            Console.WriteLine($"[DEBUG] Damage: Sqrt({power}) * (Atk:{atk}/Def:{def}) * 7 = {damage}");

            // 3. Resolve
            bool wasAlreadyDown = target.IsDown;
            var res = target.ReceiveDamage(damage, elem);

            Console.WriteLine($"{target.Name} takes {res.DamageDealt} {elem} dmg. {res.Message}");

            // One More condition
            return (res.Type == HitType.Weakness && !wasAlreadyDown);
        }
    }
}