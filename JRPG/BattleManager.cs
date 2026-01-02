using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    public class BattleManager
    {
        private Combatant _p;
        private Combatant _e;
        private InventoryManager _inv;
        private EconomyManager _eco;
        private IGameIO _io;
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

        public BattleManager(Combatant player, Combatant enemy, InventoryManager inventory, EconomyManager economy, IGameIO io)
        {
            _p = player;
            _e = enemy;
            _inv = inventory;
            _eco = economy;
            _io = io;
        }

        // --- HUD & UI HELPERS ---

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

            sb.AppendLine($"ENEMY: {_e.Name.ToUpper()} (Lv.{_e.Level}) {eStatus}");
            sb.AppendLine($"HP: {_e.CurrentHP}/{_e.MaxHP}");
            sb.AppendLine("\n vs\n");

            // Player Info
            string pStatus = _p.CurrentAilment != null ? $" [{_p.CurrentAilment.Name}]" : "";
            if (_p.IsDizzy) pStatus += " [DIZZY]";
            else if (_p.IsDown) pStatus += " [DOWN]";
            else if (_p.IsImmuneToDown) pStatus += " [GUARD]";
            foreach (var b in _p.Buffs) if (b.Value > 0) pStatus += $" [{b.Key}]";

            sb.AppendLine($"PLAYER: {_p.Name} [Persona: {_p.ActivePersona?.Name}] {pStatus}");
            sb.AppendLine($"HP: {_p.CurrentHP}/{_p.MaxHP} | SP: {_p.CurrentSP}/{_p.MaxSP}");
            sb.AppendLine("=================================================");

            return sb.ToString();
        }

        private void DrawUI()
        {
            _io.Clear();
            _io.WriteLine(GetBattleStatusString());
        }

        // --- MAIN BATTLE LOOP ---

        public void StartBattle()
        {
            _io.Clear();
            _io.WriteLine("=== BATTLE COMMENCE ===");

            bool playerTurn = _p.GetStat(StatType.AGI) >= _e.GetStat(StatType.AGI);

            while (_p.CurrentHP > 0 && _e.CurrentHP > 0)
            {
                Combatant active = playerTurn ? _p : _e;
                Combatant target = playerTurn ? _e : _p;

                DrawUI();

                bool canAct = ProcessTurnStart(active);

                if (active.IsDizzy)
                {
                    _io.WriteLine($"\n[RECOVERY] {active.Name} is dizzy! They spend the turn standing up...", ConsoleColor.Yellow);
                    active.IsDizzy = false;
                    active.IsDown = false;
                    active.IsImmuneToDown = true;
                    _io.Wait(2000);
                }
                else if (canAct)
                {
                    if (active.IsDown)
                    {
                        _io.WriteLine($"\n{active.Name} gets back on their feet.");
                        active.IsDown = false;
                        _io.Wait(1000);
                    }

                    if (active.IsImmuneToDown) active.IsImmuneToDown = false;

                    // Check for Rage
                    if (active.CurrentAilment?.ActionRestriction == "ForceAttack")
                    {
                        _io.WriteLine($"\n{active.Name} is Enraged! Attacking automatically!", ConsoleColor.Red);

                        int attacks = 1 + active.CurrentAilment.ExtraTurns;
                        for (int i = 0; i < attacks; i++)
                        {
                            if (target.CurrentHP <= 0) break;
                            PerformMove(active, target, "Attack", 30, active.WeaponElement);
                            _io.Wait(800);
                        }
                    }
                    else
                    {
                        bool getOneMore = playerTurn ? ExecutePlayerTurn() : ExecuteEnemyTurn();

                        if (getOneMore && target.CurrentHP > 0)
                        {
                            _io.WriteLine("\n >>> ONE MORE! <<< ", ConsoleColor.Cyan);
                            _io.Wait(1000);
                            DrawUI();
                            if (playerTurn) ExecutePlayerTurn(); else ExecuteEnemyTurn();
                        }
                    }
                }

                ProcessTurnEnd(active);
                playerTurn = !playerTurn;
                _io.Wait(1200);
            }

            // --- END OF BATTLE ---
            if (_p.CurrentHP > 0)
            {
                _io.WriteLine("\n[VICTORY] Shadow Dissipated.", ConsoleColor.Green);

                // --- FIXED MATH: DATA-DRIVEN EXP ---
                int baseExp = 0;
                int maccaGain = 0;

                // Fix: Initialize enemyData explicitly to null to satisfy compiler (CS0165)
                EnemyData enemyData = null;

                // Attempt to load from JSON data to ensure curve compliance
                if (!string.IsNullOrEmpty(_e.SourceId) && Database.Enemies.TryGetValue(_e.SourceId, out var data))
                {
                    enemyData = data;
                    baseExp = enemyData.ExpYield;
                    maccaGain = enemyData.MaccaYield;
                }
                else
                {
                    // Fallback to linear formula only if no JSON data exists
                    baseExp = _e.Level * 10;
                    maccaGain = _e.Level * 40;
                }

                int levelDiff = _e.Level - _p.Level;
                double multiplier = 0.10;
                if (levelDiff >= 10) multiplier = 1.75;
                else if (levelDiff >= 5) multiplier = 1.30;
                else if (levelDiff >= -4) multiplier = 1.00;
                else if (levelDiff >= -9) multiplier = 0.50;

                int totalExp = (int)(baseExp * multiplier);
                int expPerMember = totalExp; // For single player

                _io.WriteLine($"EXP Gained: {expPerMember} (Mult: x{multiplier})");

                _p.GainExp(expPerMember);
                if (_p.ActivePersona != null) _p.ActivePersona.GainExp(expPerMember);

                // Macca Logic
                double maccaMod = Math.Clamp(1 + (_e.Level - _p.Level) * 0.04, 0.9, 1.5);
                int totalMacca = (int)(maccaGain * maccaMod);

                _eco.AddMacca(totalMacca);

                // Drop Logic (Safe to access enemyData now because we initialized it to null)
                if (enemyData?.Drops != null)
                {
                    foreach (var drop in enemyData.Drops)
                    {
                        if (_rnd.NextDouble() < drop.Chance)
                        {
                            _inv.AddItem(drop.ItemId, 1);
                            var dropName = Database.Items.ContainsKey(drop.ItemId) ? Database.Items[drop.ItemId].Name : drop.ItemId;
                            _io.WriteLine($"Found Drop: {dropName}", ConsoleColor.Yellow);
                        }
                    }
                }
            }
            else
            {
                _io.WriteLine("\n[GAMEOVER] The journey ends here...", ConsoleColor.Red);
            }
            _io.ReadKey();
        }

        // --- TURN LOGIC & AILMENTS ---

        private bool ProcessTurnStart(Combatant c)
        {
            if (c.CurrentAilment == null) return true;

            _io.WriteLine($"\n{c.Name} is {c.CurrentAilment.Name}...", ConsoleColor.Magenta);
            bool act = true;

            switch (c.CurrentAilment.ActionRestriction)
            {
                case "SkipTurn":
                    _io.WriteLine($"{c.Name} cannot move!");
                    act = false;
                    break;
                case "ChanceSkipOrFlee":
                    if (_rnd.Next(100) < 30) { _io.WriteLine($"{c.Name} is paralyzed by Fear!"); act = false; }
                    break;
                case "ChanceSkip":
                    if (_rnd.Next(100) < 40) { _io.WriteLine($"{c.Name} is Panicking and does nothing!"); act = false; }
                    break;
                case "ConfusedAction":
                    _io.WriteLine($"{c.Name} is Charmed!");
                    if (_rnd.Next(100) < 50)
                    {
                        _io.WriteLine("...and heals the enemy!");
                        Combatant foe = (c == _p) ? _e : _p;
                        foe.CurrentHP = Math.Min(foe.MaxHP, foe.CurrentHP + 50);
                        act = false;
                    }
                    else
                    {
                        _io.WriteLine("...and attacks themselves!");
                        PerformMove(c, c, "Attack", 20, c.WeaponElement);
                        act = false;
                    }
                    break;
            }

            return act;
        }

        private void ProcessTurnEnd(Combatant c)
        {
            var msgs = c.TickBuffs();
            foreach (var m in msgs) _io.WriteLine(m);

            if (c.CurrentAilment == null) return;

            if (c.CurrentAilment.DotPercent > 0)
            {
                int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent);
                c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                _io.WriteLine($"\n{c.Name} takes {dmg} damage from {c.CurrentAilment.Name}.", ConsoleColor.DarkMagenta);
            }

            c.AilmentDuration--;
            if (c.AilmentDuration <= 0)
            {
                _io.WriteLine($"\n{c.Name} recovered from {c.CurrentAilment.Name}!");
                c.RemoveAilment();
            }
        }

        // --- PLAYER MENU SYSTEM ---

        private bool ExecutePlayerTurn()
        {
            bool isPanicked = _p.CurrentAilment?.Name == "Panic";
            int menuIndex = 0;

            while (true)
            {
                List<string> options = new List<string> { "Attack", "Skill", "Item" };
                List<bool> disabled = new List<bool> { false, isPanicked, false };

                if (isPanicked) options[1] = "Skill (Blocked)";

                string header = GetBattleStatusString() + "\n=== PLAYER TURN ===";

                int choice = _io.RenderMenu(header, options, menuIndex, disabled);
                if (choice != -1) menuIndex = choice;

                if (choice == 0) // Attack
                {
                    int power = _p.EquippedWeapon?.Power ?? 20;
                    return PerformMove(_p, _e, "Attack", power, _p.WeaponElement);
                }
                else if (choice == 1) // Skill
                {
                    if (isPanicked) continue;
                    if (ExecuteSkillMenu()) return PerformMoveFromSkill();
                }
                else if (choice == 2) // Item
                {
                    if (ExecuteItemMenu()) return false;
                }
            }
        }

        private bool ExecuteSkillMenu()
        {
            var skills = _p.ActivePersona.SkillSet;
            if (skills.Count == 0) return false;

            List<string> options = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var s in skills)
            {
                string label = s;
                bool cannotAfford = false;

                if (Database.Skills.TryGetValue(s, out var data))
                {
                    label = $"{s} ({data.Cost})";
                    var cost = data.ParseCost();
                    if ((cost.isHP && _p.CurrentHP <= cost.value) || (!cost.isHP && _p.CurrentSP < cost.value))
                    {
                        cannotAfford = true;
                    }
                }
                options.Add(label);
                disabled.Add(cannotAfford);
            }

            string header = GetBattleStatusString() + "\n=== SKILLS ===";
            int idx = _io.RenderMenu(header, options, 0, disabled, (index) =>
            {
                string sName = skills[index];
                if (Database.Skills.TryGetValue(sName, out var d))
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power} | Acc: {d.Accuracy}");
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
                _io.WriteLine("Inventory is empty!");
                _io.Wait(800);
                return false;
            }

            List<string> options = new List<string>();
            foreach (var item in usableItems)
            {
                options.Add($"{item.Name} x{_inv.GetQuantity(item.Id)}");
            }

            string header = GetBattleStatusString() + "\n=== ITEMS ===";
            int idx = _io.RenderMenu(header, options, 0, null, (index) =>
            {
                _io.WriteLine($"Description: {usableItems[index].Description}");
            });

            if (idx != -1)
            {
                ItemData selectedItem = usableItems[idx];
                if (PerformItem(selectedItem, _p))
                {
                    _inv.RemoveItem(selectedItem.Id, 1);
                    return true;
                }
                else _io.Wait(1000);
            }
            return false;
        }

        // --- ACTION EXECUTION ---

        private bool PerformItem(ItemData item, Combatant target)
        {
            _io.WriteLine($"\n[ITEM] {_p.Name} uses {item.Name}...");
            bool used = false;
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    int heal = item.EffectValue;
                    int oldHp = target.CurrentHP;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                    _io.WriteLine($"-> Restored {target.CurrentHP - oldHp} HP.");
                    used = true;
                    break;
                case "Spirit":
                    int spHeal = item.EffectValue;
                    int oldSp = target.CurrentSP;
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + spHeal);
                    _io.WriteLine($"-> Restored {target.CurrentSP - oldSp} SP.");
                    used = true;
                    break;
                case "Revive":
                    if (target.CurrentHP > 0)
                    {
                        _io.WriteLine("-> No effect! Target is alive.");
                    }
                    else
                    {
                        int reviveHp = (int)(target.MaxHP * (item.EffectValue / 100.0));
                        target.CurrentHP = Math.Max(1, reviveHp);
                        target.IsDown = false;
                        target.IsDizzy = false;
                        _io.WriteLine($"-> {target.Name} revived with {target.CurrentHP} HP!");
                        used = true;
                    }
                    break;
                case "Cure":
                    if (target.CurrentAilment == null)
                    {
                        _io.WriteLine("-> No effect! Target healthy.");
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

                        if (cured) { _io.WriteLine($"-> Cured!"); used = true; }
                        else { _io.WriteLine("-> No effect on this ailment."); }
                    }
                    break;
                case "Barrier":
                case "Utility":
                    _io.WriteLine($"-> {item.Name} used! (Placeholder effect)");
                    used = true;
                    break;
                default:
                    _io.WriteLine("-> Effect not implemented.");
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
            int power = _e.EquippedWeapon?.Power ?? 25;
            return PerformMove(_e, _p, "Attack", power, _e.WeaponElement);
        }

        private bool PerformMove(Combatant user, Combatant target, string name, int power, Element elem)
        {
            _io.WriteLine($"\n{user.Name} invokes {name}!");

            string category = "Physical";
            if (Database.Skills.TryGetValue(name, out var skillDataVal)) category = skillDataVal.Category;

            // --- RECOVERY ---
            if (category.Contains("Recovery"))
            {
                int heal = power == 0 ? 50 : power;
                int oldHp = user.CurrentHP;
                user.CurrentHP = Math.Min(user.MaxHP, user.CurrentHP + heal);
                _io.WriteLine($"-> {user.Name} restored {user.CurrentHP - oldHp} HP to themselves!");
                return false;
            }

            // --- ENHANCE ---
            if (category.Contains("Enhance"))
            {
                if (name.Contains("Taru")) { if (name.Contains("nda")) target.AddBuff("AttackDown", 3); else user.AddBuff("Attack", 3); }
                else if (name.Contains("Raku")) { if (name.Contains("nda")) target.AddBuff("DefenseDown", 3); else user.AddBuff("Defense", 3); }
                else if (name.Contains("Suku")) { if (name.Contains("nda")) target.AddBuff("AgilityDown", 3); else user.AddBuff("Agility", 3); }
                _io.WriteLine("-> Effect applied.");
                return false;
            }

            // --- OFFENSIVE ---
            int baseAcc = 95;
            if (name == "Attack" && user.EquippedWeapon != null) baseAcc = user.EquippedWeapon.Accuracy;
            else if (Database.Skills.TryGetValue(name, out var sd)) int.TryParse(sd.Accuracy?.Replace("%", ""), out baseAcc);

            if (user.IsLongRange) baseAcc -= 20;

            // Target Evasion Logic
            double tEvasionMult = target.CurrentAilment?.EvasionMult ?? 1.0;
            if (target.IsDown || target.IsDizzy || target.IsRigidBody) tEvasionMult = 0.0;

            int uAgi = user.GetStat(StatType.AGI);
            int tAgi = (int)(target.GetStat(StatType.AGI) * tEvasionMult);
            int equipEva = target.GetEvasion();

            int finalHit = Math.Clamp(baseAcc + (uAgi - tAgi) - equipEva, 5, 99);
            if (tEvasionMult == 0.0) finalHit = 100;

            if (_rnd.Next(1, 101) > finalHit)
            {
                _io.WriteLine($"-> MISS! {target.Name} evaded the attack.", ConsoleColor.Yellow);
                if (!user.IsLongRange && (elem == Element.Slash || elem == Element.Strike || elem == Element.Pierce))
                {
                    _io.WriteLine($"-> {user.Name} overextended and fell! [DOWN]");
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
            int def = target.GetStat(StatType.END) + target.GetDefense();

            if (user.CurrentAilment != null) atk = (int)(atk * user.CurrentAilment.DamageDealMult);
            if (target.IsDown || target.IsDizzy) def = (int)(def * 0.5);

            double dmgBase = Math.Sqrt(power) * ((double)atk / Math.Max(1, def)) * 7;
            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            bool wasAlreadyDown = target.IsDown;
            var res = target.ReceiveDamage(damage, elem, isCritical);

            _io.WriteLine($"{target.Name} takes {res.DamageDealt} {elem} dmg. {res.Message}");

            // Infliction
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
                                    _io.WriteLine($"-> {target.Name} is afflicted with {ailmentData.Name}!", ConsoleColor.Magenta);
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
                    _io.WriteLine($"-> {user.Name} hit a vulnerable spot, but {user.CurrentAilment.Name} prevented a One More!");
                    return false;
                }
                return true;
            }

            return false;
        }
    }
}