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
        private InventoryManager _inv; // Reference to Inventory
        private Random _rnd = new Random();

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

        // Updated Constructor to accept InventoryManager
        public BattleManager(Combatant player, Combatant enemy, InventoryManager inventory)
        {
            _p = player;
            _e = enemy;
            _inv = inventory;
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

            Console.WriteLine(_p.CurrentHP > 0 ? "\n[VICTORY] Shadow Dissipated." : "\n[GAMEOVER] The journey ends here...");
        }

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
                    if (_rnd.Next(100) < 40) { Console.WriteLine($"\n{c.Name} is Panicking and does nothing!"); act = false; }
                    break;
                case "ConfusedAction":
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
                        PerformMove(c, c, "Attack", 20, c.WeaponElement);
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
            if (c.CurrentAilment.DotPercent > 0)
            {
                int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent);
                c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n{c.Name} takes {dmg} damage from {c.CurrentAilment.Name}.");
                Console.ResetColor();
            }
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
            Console.ForegroundColor = ConsoleColor.Red;
            string eStatus = _e.CurrentAilment != null ? $" [{_e.CurrentAilment.Name}]" : "";
            if (_e.IsDizzy) eStatus += " [DIZZY]";
            else if (_e.IsDown) eStatus += " [DOWN]";
            else if (_e.IsImmuneToDown) eStatus += " [GUARD]";
            Console.WriteLine($"{_e.Name.ToUpper()} (Lv.{_e.ActivePersona?.Level}){eStatus}");
            Console.WriteLine($"HP: {_e.CurrentHP}/{_e.MaxHP}");
            Console.ResetColor();

            Console.WriteLine("\n vs\n");

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

            while (true)
            {
                Console.WriteLine($"\nActions: [1] Skill {(isPanicked ? "(Blocked)" : "")} [2] Attack [3] Item");
                Console.Write("> ");
                string choice = Console.ReadLine();

                if (choice == "2")
                {
                    int power = _p.EquippedWeapon?.Power ?? 20;
                    return PerformMove(_p, _e, "Attack", power, _p.WeaponElement);
                }
                else if (choice == "3")
                {
                    // If ExecuteItemMenu returns true, an item was used successfully
                    if (ExecuteItemMenu()) return false;
                }
                else if (choice == "1")
                {
                    if (isPanicked) { Console.WriteLine("You are Panicking and cannot use skills!"); continue; }

                    var skills = _p.ActivePersona.SkillSet;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        if (Database.Skills.TryGetValue(skills[i], out var s))
                            Console.WriteLine($"{i + 1}. {skills[i]} ({s.Cost}) - {s.Effect}");
                    }
                    Console.Write("Select Skill # (or 'b' for back): ");
                    string input = Console.ReadLine();
                    if (input.ToLower() == "b") continue;

                    if (int.TryParse(input, out int idx) && idx > 0 && idx <= skills.Count)
                    {
                        string sName = skills[idx - 1];
                        if (Database.Skills.TryGetValue(sName, out var sData))
                        {
                            var cost = sData.ParseCost();
                            if (cost.isHP) { if (_p.CurrentHP > cost.value) _p.CurrentHP -= cost.value; else { Console.WriteLine("Not enough HP!"); continue; } }
                            else { if (_p.CurrentSP >= cost.value) _p.CurrentSP -= cost.value; else { Console.WriteLine("Not enough SP!"); continue; } }

                            _e.CheckCure(sData.Effect);
                            _p.CheckCure(sData.Effect);

                            return PerformMove(_p, _e, sName, sData.GetPowerVal(), ElementHelper.FromCategory(sData.Category));
                        }
                    }
                    Console.WriteLine("Invalid selection.");
                }
            }
        }

        private bool ExecuteItemMenu()
        {
            // Filter: Only Items with Quantity > 0
            var usableItems = Database.Items.Values
                                      .Where(i => _inv.GetQuantity(i.Id) > 0)
                                      .ToList();

            if (usableItems.Count == 0)
            {
                Console.WriteLine("Inventory is empty!");
                return false;
            }

            Console.WriteLine("\n--- INVENTORY ---");
            for (int i = 0; i < usableItems.Count; i++)
            {
                int qty = _inv.GetQuantity(usableItems[i].Id);
                Console.WriteLine($"{i + 1}. {usableItems[i].Name} x{qty} - {usableItems[i].Description}");
            }

            Console.Write("Select Item # (or 'b' for back): ");
            string input = Console.ReadLine();
            if (input.ToLower() == "b") return false;

            if (int.TryParse(input, out int idx) && idx > 0 && idx <= usableItems.Count)
            {
                ItemData selectedItem = usableItems[idx - 1];

                // Double-Check Validation
                if (!_inv.HasItem(selectedItem.Id))
                {
                    Console.WriteLine("Error: Item count is 0.");
                    return false;
                }

                // Try to use item
                bool success = PerformItem(selectedItem, _p);

                if (success)
                {
                    // Item Consumption
                    _inv.RemoveItem(selectedItem.Id, 1);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }

            return false;
        }

        private bool PerformItem(ItemData item, Combatant target)
        {
            Console.WriteLine($"\n[ITEM] {_p.Name} attempts to use {item.Name}...");

            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    int heal = item.EffectValue;
                    int oldHp = target.CurrentHP;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                    Console.WriteLine($"-> Restored {target.CurrentHP - oldHp} HP to {target.Name}.");
                    return true;

                case "Spirit":
                    int spHeal = item.EffectValue;
                    int oldSp = target.CurrentSP;
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + spHeal);
                    Console.WriteLine($"-> Restored {target.CurrentSP - oldSp} SP to {target.Name}.");
                    return true;

                case "Revive":
                    if (target.CurrentHP > 0)
                    {
                        Console.WriteLine("-> No effect! Target is already alive.");
                        return false;
                    }
                    else
                    {
                        int reviveHp = (int)(target.MaxHP * (item.EffectValue / 100.0));
                        target.CurrentHP = Math.Max(1, reviveHp);
                        target.IsDown = false;
                        target.IsDizzy = false;
                        Console.WriteLine($"-> {target.Name} is revived with {target.CurrentHP} HP!");
                        return true;
                    }

                case "Cure":
                    if (target.CurrentAilment == null)
                    {
                        Console.WriteLine("-> No effect! Target has no ailment.");
                        return false;
                    }

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
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("-> No effect on this ailment.");
                        return false;
                    }

                case "Barrier":
                case "Utility":
                    Console.WriteLine($"-> {item.Name} used! (Effect not yet implemented in damage calc)");
                    return true;

                default:
                    Console.WriteLine("-> Effect not implemented.");
                    return false;
            }
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
            int power = _e.EquippedWeapon?.Power ?? 25;
            return PerformMove(_e, _p, "Attack", power, _e.WeaponElement);
        }

        private bool PerformMove(Combatant user, Combatant target, string name, int power, Element elem)
        {
            Console.WriteLine($"\n{user.Name} invokes {name}!");

            int baseAcc = 95;
            if (name == "Attack" && user.EquippedWeapon != null) baseAcc = user.EquippedWeapon.Accuracy;
            else if (Database.Skills.TryGetValue(name, out var sd)) int.TryParse(sd.Accuracy?.Replace("%", ""), out baseAcc);

            if (user.IsLongRange) baseAcc -= 20;

            double tEvasionMult = target.CurrentAilment?.EvasionMult ?? 1.0;
            if (target.IsDown || target.IsDizzy || target.IsRigidBody) tEvasionMult = 0.0;

            int uAgi = user.GetStat(StatType.AGI);
            int tAgi = (int)(target.GetStat(StatType.AGI) * tEvasionMult);
            int finalHit = Math.Clamp(baseAcc + (uAgi - tAgi), 5, 99);
            if (tEvasionMult == 0.0) finalHit = 100;

            if (_rnd.Next(1, 101) > finalHit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"-> MISS! {target.Name} evaded the attack.");
                Console.ResetColor();

                if (!user.IsLongRange && (elem == Element.Slash || elem == Element.Strike || elem == Element.Pierce))
                {
                    Console.WriteLine($"-> {user.Name} overextended and fell! [DOWN]");
                    user.IsDown = true;
                }
                return false;
            }

            bool isPhysical = (elem == Element.Slash || elem == Element.Strike || elem == Element.Pierce);
            bool isCritical = false;

            if (isPhysical)
            {
                int critChance = (user.GetStat(StatType.LUK) - target.GetStat(StatType.LUK)) + 5;
                if (target.CurrentAilment?.Name == "Freeze" || target.CurrentAilment?.Name == "Shock") critChance = 100;
                else if (target.CurrentAilment?.Name == "Distress") critChance += 50;

                if (_rnd.Next(1, 101) <= critChance) isCritical = true;
            }

            int atk = isPhysical ? user.GetStat(StatType.STR) : user.GetStat(StatType.MAG);
            int def = target.GetStat(StatType.END);

            if (user.CurrentAilment != null) atk = (int)(atk * user.CurrentAilment.DamageDealMult);
            if (target.IsDown || target.IsDizzy) def = (int)(def * 0.5);

            double dmgBase = Math.Sqrt(power) * ((double)atk / Math.Max(1, def)) * 7;
            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            bool wasAlreadyDown = target.IsDown;
            var res = target.ReceiveDamage(damage, elem, isCritical);

            Console.WriteLine($"{target.Name} takes {res.DamageDealt} {elem} dmg. {res.Message}");

            if (Database.Skills.TryGetValue(name, out var skillData))
            {
                foreach (var kvp in _effectToAilmentMap)
                {
                    if (skillData.Effect.Contains(kvp.Key))
                    {
                        if (Database.Ailments.TryGetValue(kvp.Value, out var ailmentData))
                        {
                            int chance = 100;
                            var matchChance = Regex.Match(skillData.Effect, @"\((\d+)% chance\)");
                            if (matchChance.Success) int.TryParse(matchChance.Groups[1].Value, out chance);

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

            bool hitCondition = (res.Type == HitType.Weakness || res.IsCritical);
            bool immunityBlock = target.IsImmuneToDown;
            bool userAfflictedBlock = (user.CurrentAilment != null);
            bool loopCheck = target.IsRigidBody || !wasAlreadyDown;

            if (hitCondition && !immunityBlock && loopCheck)
            {
                if (userAfflictedBlock)
                {
                    Console.WriteLine($"-> {user.Name} hit a vulnerable spot, but {user.CurrentAilment.Name} prevented a One More!");
                    return false;
                }
                return true;
            }

            return false;
        }
    }
}