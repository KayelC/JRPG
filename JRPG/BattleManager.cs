using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace JRPGPrototype
{
    public class BattleManager
    {
        private Combatant _p;
        private Combatant _e;
        private InventoryManager _inv;
        private EconomyManager _eco;
        private Random _rnd = new Random();

        // Used to store the selected skill during menu navigation
        private string? _selectedSkillName;

        private readonly Dictionary<string, string> _effectToAilmentMap = new Dictionary<string, string>
        {
            { "Poisons", "Poison" },
            { "Instills Fear", "Fear" },
            { "Panic", "Panic" },
            { "Distresses", "Distress" },
            { "Charms", "Charm" },
            { "Enrages", "Rage" },
            { "Shocks", "Shock" },
            { "Freezes", "Freeze" }
        };

        public BattleManager(Combatant player, Combatant enemy, InventoryManager inventory, EconomyManager economy)
        {
            _p = player;
            _e = enemy;
            _inv = inventory;
            _eco = economy;
        }

        // --- HUD & UI HELPERS ---

        /// <summary>
        /// Generates the static Battle HUD string.
        /// Passed to MenuUI as the header so it persists during menu navigation.
        /// </summary>
        private string GetBattleStatusString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=================================================");

            // Enemy Info
            string eStatus = _e.CurrentAilment != null ? $" [{_e.CurrentAilment.Name}]" : "";
            if (_e.IsDizzy) eStatus += " [DIZZY]";
            else if (_e.IsDown) eStatus += " [DOWN]";
            else if (_e.IsImmuneToDown) eStatus += " [GUARD]";
            foreach (var b in _e.Buffs) if (b.Value > 0) eStatus += $" [{b.Key}]";

            sb.AppendLine($"ENEMY: {_e.Name.ToUpper()} (Lv.{_e.ActivePersona?.Level}){eStatus}");
            sb.AppendLine($"HP: {_e.CurrentHP}/{_e.MaxHP}");
            sb.AppendLine("\n vs\n");

            // Player Info
            string pStatus = _p.CurrentAilment != null ? $" [{_p.CurrentAilment.Name}]" : "";
            if (_p.IsDizzy) pStatus += " [DIZZY]";
            else if (_p.IsDown) pStatus += " [DOWN]";
            else if (_p.IsImmuneToDown) pStatus += " [GUARD]";
            foreach (var b in _p.Buffs) if (b.Value > 0) pStatus += $" [{b.Key}]";

            sb.AppendLine($"PLAYER: {_p.Name} [Persona: {_p.ActivePersona?.Name}]{pStatus}");
            sb.AppendLine($"HP: {_p.CurrentHP}/{_p.MaxHP} | SP: {_p.CurrentSP}/{_p.MaxSP}");
            sb.AppendLine("=================================================");

            return sb.ToString();
        }

        // Helper to draw UI during non-interactive phases (AI turn, animations)
        private void DrawUI()
        {
            Console.Clear();
            Console.WriteLine(GetBattleStatusString());
        }

        // --- MAIN BATTLE LOGIC ---

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
                    if (active.IsDown)
                    {
                        Console.WriteLine($"\n{active.Name} gets back on their feet.");
                        active.IsDown = false;
                        Thread.Sleep(1000);
                    }
                    if (active.IsImmuneToDown) active.IsImmuneToDown = false;

                    // Rage Check
                    if (active.CurrentAilment?.ActionRestriction == "ForceAttack")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n{active.Name} is Enraged! Attacking automatically!");
                        Console.ResetColor();

                        int attacks = 1 + active.CurrentAilment.ExtraTurns;
                        for (int i = 0; i < attacks; i++)
                        {
                            if (target.CurrentHP <= 0) break;
                            PerformMove(active, target, "Attack", 30, active.WeaponElement);
                            Thread.Sleep(800);
                        }
                    }
                    else
                    {
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
                }
                ProcessTurnEnd(active);
                playerTurn = !playerTurn;
                Thread.Sleep(1200);
            }

            if (_p.CurrentHP > 0)
            {
                Console.WriteLine("\n[VICTORY] Shadow Dissipated.");

                int levelDiff = _e.Level - _p.Level;
                double multiplier = 0.10;
                if (levelDiff >= 10) multiplier = 1.75;
                else if (levelDiff >= 5) multiplier = 1.30;
                else if (levelDiff >= -4) multiplier = 1.00;
                else if (levelDiff >= -9) multiplier = 0.50;

                int baseExp = _e.Level * 10;
                int totalExp = (int)(baseExp * multiplier);

                Console.WriteLine($"EXP Gained: {totalExp} (Mult: x{multiplier})");
                _p.GainExp(totalExp);
                if (_p.ActivePersona != null) _p.ActivePersona.GainExp(totalExp);

                double maccaMod = Math.Clamp(1 + (_e.Level - _p.Level) * 0.04, 0.9, 1.5);
                int maccaGain = (int)((_e.Level * 40) * maccaMod);
                _eco.AddMacca(maccaGain);
            }
            else
            {
                Console.WriteLine("\n[GAMEOVER] The journey ends here...");
            }
        }

        // --- TURN LOGIC & AILMENTS ---

        private bool ProcessTurnStart(Combatant c)
        {
            if (c.CurrentAilment == null) return true;
            Console.ForegroundColor = ConsoleColor.Magenta;
            bool act = true;

            switch (c.CurrentAilment.ActionRestriction)
            {
                case "SkipTurn":
                    Console.WriteLine($"\n{c.Name} is {c.CurrentAilment.Name} and cannot move!");
                    act = false;
                    break;
                case "ChanceSkipOrFlee":
                    if (_rnd.Next(100) < 30) { Console.WriteLine($"\n{c.Name} is paralyzed by Fear!"); act = false; }
                    break;
                case "ChanceSkip":
                    if (_rnd.Next(100) < 40) { Console.WriteLine($"\n{c.Name} is Panicking!"); act = false; }
                    break;
                case "ConfusedAction":
                    Console.WriteLine($"\n{c.Name} is Charmed!");
                    if (_rnd.Next(100) < 50)
                    {
                        Console.WriteLine("...and heals the enemy!");
                        Combatant foe = (c == _p) ? _e : _p; foe.CurrentHP = Math.Min(foe.MaxHP, foe.CurrentHP + 50); act = false;
                    }
                    else
                    {
                        Console.WriteLine("...and attacks themselves!"); PerformMove(c, c, "Attack", 20, c.WeaponElement); act = false;
                    }
                    break;
            }
            Console.ResetColor();
            return act;
        }

        private void ProcessTurnEnd(Combatant c)
        {
            c.TickBuffs();
            if (c.CurrentAilment == null) return;
            if (c.CurrentAilment.DotPercent > 0)
            {
                int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent); c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                Console.ForegroundColor = ConsoleColor.DarkMagenta; Console.WriteLine($"\n{c.Name} takes {dmg} damage from {c.CurrentAilment.Name}."); Console.ResetColor();
            }
            c.AilmentDuration--;
            if (c.AilmentDuration <= 0)
            {
                Console.WriteLine($"\n{c.Name} recovered from {c.CurrentAilment.Name}!");
                c.RemoveAilment();
            }
        }

        // --- PLAYER MENU SYSTEM ---

        private bool ExecutePlayerTurn()
        {
            bool isPanicked = _p.CurrentAilment?.Name == "Panic";
            int menuIndex = 0; // Remembers cursor position

            while (true)
            {
                // Prepare Menu Options
                List<string> options = new List<string> { "Attack", "Skill", "Item" };
                List<bool> disabled = new List<bool> { false, isPanicked, false };

                if (isPanicked) options[1] = "Skill (Blocked)";

                string header = GetBattleStatusString() + "\n=== PLAYER TURN ===";

                int choice = MenuUI.RenderMenu(header, options, menuIndex, disabled);

                if (choice != -1) menuIndex = choice; // Persist selection

                if (choice == 0) // Attack
                {
                    int power = _p.EquippedWeapon?.Power ?? 20;
                    return PerformMove(_p, _e, "Attack", power, _p.WeaponElement);
                }
                else if (choice == 1) // Skill
                {
                    if (isPanicked) continue;
                    if (ExecuteSkillMenu())
                    {
                        return PerformMoveFromSkill();
                    }
                }
                else if (choice == 2) // Item
                {
                    if (ExecuteItemMenu()) return false; // Used item, turn ends
                }
            }
        }

        private bool ExecuteSkillMenu()
        {
            var skills = _p.ActivePersona.SkillSet;
            if (skills.Count == 0) return false;

            List<string> options = new List<string>();
            List<bool> disabled = new List<bool>();

            // Build list with visual clarity (Cost + Greying out)
            foreach (var s in skills)
            {
                string label = s;
                bool cannotAfford = false;

                if (Database.Skills.TryGetValue(s, out var data))
                {
                    label = $"{s,-15} ({data.Cost})";
                    var cost = data.ParseCost();
                    if ((cost.isHP && _p.CurrentHP <= cost.value) || (!cost.isHP && _p.CurrentSP < cost.value))
                    {
                        cannotAfford = true;
                    }
                }
                options.Add(label);
                disabled.Add(cannotAfford);
            }

            // Render Skill Menu
            string header = GetBattleStatusString() + "\n=== SKILLS ===";
            int idx = MenuUI.RenderMenu(header, options, 0, disabled, (index) =>
            {
                // Footer: Skill Description
                string sName = skills[index];
                if (Database.Skills.TryGetValue(sName, out var d))
                    Console.WriteLine($"Effect: {d.Effect}\nPower: {d.Power} | Acc: {d.Accuracy}");
            });

            if (idx != -1)
            {
                _selectedSkillName = skills[idx];
                return true;
            }
            return false;
        }

        private bool PerformMoveFromSkill()
        {
            if (string.IsNullOrEmpty(_selectedSkillName)) return false;

            if (Database.Skills.TryGetValue(_selectedSkillName, out var sData))
            {
                // Pay Cost
                var cost = sData.ParseCost();
                if (cost.isHP) _p.CurrentHP -= cost.value; else _p.CurrentSP -= cost.value;

                _e.CheckCure(sData.Effect);
                _p.CheckCure(sData.Effect);

                return PerformMove(_p, _e, _selectedSkillName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
            }
            return false;
        }

        private bool ExecuteItemMenu()
        {
            var usableItems = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (usableItems.Count == 0)
            {
                Console.WriteLine("Inventory is empty!");
                Thread.Sleep(800);
                return false;
            }

            List<string> options = new List<string>();
            foreach (var item in usableItems)
            {
                options.Add($"{item.Name} x{_inv.GetQuantity(item.Id)}");
            }

            string header = GetBattleStatusString() + "\n=== ITEMS ===";
            int idx = MenuUI.RenderMenu(header, options, 0, null, (index) =>
            {
                Console.WriteLine($"Description: {usableItems[index].Description}");
            });

            if (idx != -1)
            {
                ItemData selectedItem = usableItems[idx];
                if (PerformItem(selectedItem, _p))
                {
                    _inv.RemoveItem(selectedItem.Id, 1);
                    return true;
                }
                else Thread.Sleep(1000);
            }
            return false;
        }

        // --- ACTION EXECUTION ---

        private bool PerformItem(ItemData item, Combatant target)
        {
            Console.WriteLine($"\n[ITEM] {_p.Name} uses {item.Name}...");
            bool used = false;
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    int heal = item.EffectValue;
                    int oldHp = target.CurrentHP;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                    Console.WriteLine($"-> Restored {target.CurrentHP - oldHp} HP.");
                    used = true;
                    break;
                case "Spirit":
                    int spHeal = item.EffectValue;
                    int oldSp = target.CurrentSP;
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + spHeal);
                    Console.WriteLine($"-> Restored {target.CurrentSP - oldSp} SP.");
                    used = true;
                    break;
                case "Revive":
                    if (target.CurrentHP > 0)
                    {
                        Console.WriteLine("-> No effect! Target is alive.");
                    }
                    else
                    {
                        int reviveHp = (int)(target.MaxHP * (item.EffectValue / 100.0));
                        target.CurrentHP = Math.Max(1, reviveHp);
                        target.IsDown = false;
                        target.IsDizzy = false;
                        Console.WriteLine($"-> {target.Name} revived with {target.CurrentHP} HP!");
                        used = true;
                    }
                    break;
                case "Cure":
                    if (target.CurrentAilment == null)
                    {
                        Console.WriteLine("-> No effect! Target healthy.");
                    }
                    else
                    {
                        bool cured = false;
                        if (item.Name == "Dis-Poison" && target.CurrentAilment.Name == "Poison")
                        {
                            target.RemoveAilment();
                            cured = true;
                        }
                        else if (item.Name == "Patra Card")
                        {
                            if (target.CheckCure("Cure All")) cured = true;
                        }
                        if (cured)
                        {
                            Console.WriteLine($"-> Cured!");
                            used = true;
                        }
                        else
                        {
                            Console.WriteLine("-> No effect on this ailment.");
                        }
                    }
                    break;
                case "Barrier":
                case "Utility":
                    Console.WriteLine($"-> {item.Name} used! (Placeholder effect)");
                    used = true;
                    break;
                default:
                    Console.WriteLine("-> Effect not implemented.");
                    break;
            }
            return used;
        }

        private bool ExecuteEnemyTurn()
        {
            var skills = _e.ActivePersona.SkillSet;
            string sName = (_rnd.Next(100) < 70 && skills.Count > 0) ? skills[_rnd.Next(skills.Count)] : "Attack";
            if (_e.CurrentAilment?.Name == "Panic") sName = "Attack";

            if (sName != "Attack" && Database.Skills.TryGetValue(sName, out var sData))
            {
                _p.CheckCure(sData.Effect);
                _e.CheckCure(sData.Effect);
                return PerformMove(_e, _p, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
            }
            return PerformMove(_e, _p, "Attack", _e.EquippedWeapon?.Power ?? 25, _e.WeaponElement);
        }

        private bool PerformMove(Combatant user, Combatant target, string name, int power, Element elem)
        {
            Console.WriteLine($"\n{user.Name} invokes {name}!");

            string category = "Physical";
            if (Database.Skills.TryGetValue(name, out var skillDataVal)) category = skillDataVal.Category;

            // RECOVERY
            if (category.Contains("Recovery"))
            {
                int h = power == 0 ? 50 : power;
                int old = user.CurrentHP;
                user.CurrentHP = Math.Min(user.MaxHP, user.CurrentHP + h);
                Console.WriteLine($"-> Restored {user.CurrentHP - old} HP.");
                return false;
            }
            // ENHANCE
            if (category.Contains("Enhance"))
            {
                if (name.Contains("Taru"))
                {
                    if (name.Contains("nda")) { target.AddBuff("AttackDown", 3); Console.WriteLine($"-> {target.Name}'s Attack decreased!"); }
                    else { user.AddBuff("Attack", 3); Console.WriteLine($"-> {user.Name}'s Attack increased!"); }
                }
                else if (name.Contains("Raku"))
                {
                    if (name.Contains("nda")) { target.AddBuff("DefenseDown", 3); Console.WriteLine($"-> {target.Name}'s Defense decreased!"); }
                    else { user.AddBuff("Defense", 3); Console.WriteLine($"-> {user.Name}'s Defense increased!"); }
                }
                else if (name.Contains("Suku"))
                {
                    if (name.Contains("nda")) { target.AddBuff("AgilityDown", 3); Console.WriteLine($"-> {target.Name}'s Agility decreased!"); }
                    else { user.AddBuff("Agility", 3); Console.WriteLine($"-> {user.Name}'s Agility increased!"); }
                }
                else Console.WriteLine("-> Effect applied.");
                return false;
            }

            // ATTACK
            int baseAcc = 95;
            if (name == "Attack" && user.EquippedWeapon != null) baseAcc = user.EquippedWeapon.Accuracy;
            else if (Database.Skills.TryGetValue(name, out var sd)) int.TryParse(sd.Accuracy?.Replace("%", ""), out baseAcc);
            if (user.IsLongRange) baseAcc -= 20;

            double tEvasion = target.CurrentAilment?.EvasionMult ?? 1.0;
            if (target.IsDown || target.IsDizzy || target.IsRigidBody) tEvasion = 0.0;
            int finalHit = Math.Clamp(baseAcc + (user.GetStat(StatType.AGI) - (int)(target.GetStat(StatType.AGI) * tEvasion)), 5, 99);
            if (tEvasion == 0.0) finalHit = 100;

            if (_rnd.Next(1, 101) > finalHit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("-> MISS!");
                Console.ResetColor();
                if (!user.IsLongRange && (elem <= Element.Pierce))
                {
                    Console.WriteLine("-> Fell down!");
                    user.IsDown = true;
                }
                return false;
            }

            bool isPhys = (elem <= Element.Pierce);
            bool isCrit = false;
            if (isPhys)
            {
                int critC = (user.GetStat(StatType.LUK) - target.GetStat(StatType.LUK)) + 5;
                if (target.CurrentAilment?.Name == "Freeze" || target.CurrentAilment?.Name == "Shock") critC = 100;
                if (_rnd.Next(1, 101) <= critC) isCrit = true;
            }

            int atk = isPhys ? user.GetStat(StatType.STR) : user.GetStat(StatType.MAG);
            int def = target.GetStat(StatType.END);
            if (user.CurrentAilment != null) atk = (int)(atk * user.CurrentAilment.DamageDealMult);
            if (target.IsDown || target.IsDizzy) def = (int)(def * 0.5);

            double dmgBase = Math.Sqrt(power) * ((double)atk / Math.Max(1, def)) * 7;
            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;
            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            bool wasDown = target.IsDown;
            var res = target.ReceiveDamage(damage, elem, isCrit);
            Console.WriteLine($"{target.Name} takes {res.DamageDealt} {elem} dmg. {res.Message}");

            // Infliction
            if (Database.Skills.TryGetValue(name, out var sInf))
            {
                foreach (var kvp in _effectToAilmentMap)
                {
                    if (sInf.Effect.Contains(kvp.Key))
                    {
                        if (Database.Ailments.TryGetValue(kvp.Value, out var aData))
                        {
                            if (_rnd.Next(100) < 40)
                            {
                                if (target.InflictAilment(aData))
                                {
                                    Console.WriteLine($"-> Afflicted {aData.Name}!");
                                }
                            }
                        }
                    }
                }
            }

            bool canOneMore = (res.Type == HitType.Weakness || res.IsCritical) && !target.IsImmuneToDown && (target.IsRigidBody || !wasDown);
            if (user.CurrentAilment != null && canOneMore) return false;
            return canOneMore;
        }
    }
}