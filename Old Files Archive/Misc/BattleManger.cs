using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Services;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Core;
using JRPG.Logic.Battle;

namespace JRPGPrototype.Logic
{
    public class BattleManager
    {
        private readonly PartyManager _party;
        private readonly List<Combatant> _enemies;
        private readonly InventoryManager _inv;
        private readonly EconomyManager _eco;
        private readonly IGameIO _io;
        private readonly Random _rnd = new Random();

        private readonly bool _isBossBattle;
        private bool _isPlayerTurn;
        private readonly BattleKnowledge _playerKnowledge = new BattleKnowledge();
        private readonly BattleKnowledge _enemyKnowledge = new BattleKnowledge();

        public bool BattleEnded { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool TraestoUsed { get; private set; }
        public bool Escaped { get; private set; }

        private readonly Dictionary<string, string> _effectToAilmentMap = new Dictionary<string, string>
        {
            { "Poisons", "Poison" }, { "Instills Fear", "Fear" }, { "Panic", "Panic" },
            { "Distresses", "Distress" }, { "Charms", "Charm" }, { "Enrages", "Rage" },
            { "Shocks", "Shock" }, { "Freezes", "Freeze" }
        };

        private const int TERMINAL_ICON_COST = 99;

        public BattleManager(PartyManager party, List<Combatant> enemies, InventoryManager inventory, EconomyManager economy, IGameIO io, bool isBossBattle = false)
        {
            _party = party;
            _enemies = enemies;
            _inv = inventory;
            _eco = economy;
            _io = io;
            _isBossBattle = isBossBattle;
        }

        public void StartBattle()
        {
            _io.Clear();
            _io.WriteLine("=== ENEMY ENCOUNTER ===");
            foreach (var e in _enemies) _io.WriteLine($"Appeared: {e.Name} (Lv.{e.Level})");
            _io.Wait(1000);

            double pAvgAgi = _party.ActiveParty.Any() ? _party.ActiveParty.Average(c => c.GetStat(StatType.AGI)) : 0;
            double eAvgAgi = _enemies.Any() ? _enemies.Average(c => c.GetStat(StatType.AGI)) : 0;

            _isPlayerTurn = (pAvgAgi * (0.9 + _rnd.NextDouble() * 0.2)) >= (eAvgAgi * (0.9 + _rnd.NextDouble() * 0.2));

            _io.WriteLine(_isPlayerTurn ? "Player Party attacks first!" : "Enemy Party attacks first!", _isPlayerTurn ? ConsoleColor.Cyan : ConsoleColor.Red);
            _io.Wait(1000);

            while (!BattleEnded)
            {
                if (TraestoUsed || Escaped) break;

                // We no longer pass the list here, only the phase type
                ExecutePhase(_isPlayerTurn);

                CheckBattleEnd();
                if (!BattleEnded) _isPlayerTurn = !_isPlayerTurn;
            }
            ResolveBattleEnd();
        }

        private void ExecutePhase(bool isPlayerPhase)
        {
            // Nocturne Logic: Initial turn count is set by the members present at start of phase.
            var initialActors = isPlayerPhase ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
            int turnIcons = initialActors.Count;
            int ticks = turnIcons * 2;
            int actorIndex = 0;

            _io.WriteLine($"\n--- {(isPlayerPhase ? "PLAYER" : "ENEMY")} TURN (Icons: {turnIcons}) ---", isPlayerPhase ? ConsoleColor.Cyan : ConsoleColor.Red);

            // Loop continues as long as we have ticks and at least one side remains
            while (ticks > 0 && _enemies.Any(e => !e.IsDead) && _party.GetAliveMembers().Any())
            {
                if (TraestoUsed || Escaped || CheckBattleEndCondition()) return;

                // Re-fetch the LIVE list of actors every cycle to handle Summons/Returns
                var currentActors = isPlayerPhase ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();

                if (actorIndex >= currentActors.Count) actorIndex = 0;
                Combatant actor = currentActors[actorIndex];

                // Safety: If an actor died or was returned during another's sub-turn, skip them
                if (actor.IsDead) { actorIndex++; continue; }

                if (!ProcessTurnStart(actor)) { ticks -= 2; actorIndex++; ProcessTurnEnd(actor); continue; }

                _io.WriteLine(isPlayerPhase ? $"\nActive: {actor.Name} | HP: {actor.CurrentHP} | Icons: {Math.Ceiling(ticks / 2.0)}" : $"\nEnemy: {actor.Name} is acting...");

                int cost = 0;
                if (actor.Controller == ControllerType.LocalPlayer || (actor.Class == ClassType.Demon && actor.BattleControl == ControlState.DirectControl))
                    cost = ProcessHumanAction(actor, isPlayerPhase ? _enemies.Where(e => !e.IsDead).ToList() : _party.GetAliveMembers());
                else
                    cost = ProcessSmartAI(actor, currentActors, isPlayerPhase ? _enemies.Where(e => !e.IsDead).ToList() : _party.GetAliveMembers(), isPlayerPhase ? _playerKnowledge : _enemyKnowledge);

                // Apply turn consumption
                if (cost >= TERMINAL_ICON_COST) ticks = 0;
                else ticks -= cost;

                ProcessTurnEnd(actor);

                // If a demon was returned, currentActors count drops. 
                // We advance the index, but modulo it against the potentially new size.
                actorIndex++;
            }
        }

        private int ProcessHumanAction(Combatant actor, List<Combatant> opponents)
        {
            if (actor.IsGuarding) { actor.IsGuarding = false; _io.WriteLine($"{actor.Name} lowers guard."); }

            while (true)
            {
                List<string> options = new List<string> { "Attack", "Guard" };
                if (actor.Class == ClassType.PersonaUser || actor.Class == ClassType.WildCard)
                {
                    options.Add("Persona");
                    if (actor.ExtraSkills.Count > 0) options.Add("Skill");
                }
                else if (actor.Class == ClassType.Operator) { options.Add("Command"); options.Add("COMP"); }
                else options.Add("Skill");

                options.AddRange(new[] { "Item", "Tactics", "Pass" });

                bool isPanicked = actor.CurrentAilment?.Name == "Panic";
                List<bool> disabled = options.Select(opt => (opt == "Persona" || opt == "Skill" || opt == "Command" || opt == "COMP") && isPanicked).ToList();

                int choice = _io.RenderMenu($"Command: {actor.Name}", options, 0, disabled);
                if (choice == -1) continue;

                string selected = options[choice];

                if (selected == "Attack")
                {
                    var target = SelectTarget(opponents);
                    if (target == null) continue;
                    return ExecutePhysicalAttack(actor, target, _playerKnowledge);
                }
                if (selected == "Guard") { actor.IsGuarding = true; _io.WriteLine($"{actor.Name} assumes a defensive stance."); return 2; }
                if (selected == "Pass") { _io.WriteLine($"{actor.Name} passes the turn."); return 1; }

                if (selected == "Persona" || selected == "Skill")
                {
                    var skills = (selected == "Persona") ? (actor.ActivePersona?.SkillSet ?? new List<string>()) : actor.ExtraSkills;
                    if (actor.Class == ClassType.Human || actor.Class == ClassType.Demon) skills = actor.GetConsolidatedSkills();
                    var res = ExecuteSkillMenu(actor, skills, opponents);
                    if (res.executed) return res.cost;
                }
                if (selected == "Command")
                {
                    var res = ExecuteSkillMenu(actor, actor.ExtraSkills, opponents);
                    if (res.executed) return res.cost;
                }
                if (selected == "COMP") { var res = ExecuteCompMenu(actor); if (res.executed) return res.cost; }
                if (selected == "Item") { var res = ExecuteItemMenu(actor); if (res.executed) return res.cost; }
                if (selected == "Tactics") { if (ExecuteTacticsMenu(actor)) return 0; }
            }
        }

        private int ExecutePhysicalAttack(Combatant actor, Combatant target, BattleKnowledge knowledge)
        {
            _io.WriteLine($"{actor.Name} attacks {target.Name}!");
            int power = actor.EquippedWeapon != null ? actor.EquippedWeapon.Power : actor.Level + (actor.GetStat(StatType.STR) * 2);
            int atk = (int)(actor.GetStat(StatType.STR) * (actor.CurrentAilment?.DamageDealMult ?? 1.0));
            int def = target.GetStat(StatType.END) + target.GetDefense();

            double dmgBase = 5.0 * Math.Sqrt(power * ((double)atk / Math.Max(1, def)));
            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;
            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            bool isCritical = target.IsRigidBody || (_rnd.Next(100) < (actor.GetStat(StatType.LUK) - target.GetStat(StatType.LUK) + 5));
            var res = target.ReceiveDamage(damage, actor.WeaponElement, isCritical);
            _io.WriteLine($"{target.Name} takes {res.DamageDealt} dmg. {res.Message}");

            UpdateKnowledge(target.SourceId, actor.WeaponElement, res, knowledge);
            return CalculateIconCost(res);
        }

        private int ExecuteSkillLogic(Combatant actor, SkillData skillData, List<Combatant> targets, BattleKnowledge knowledge)
        {
            var costInfo = skillData.ParseCost();
            if (costInfo.isHP) actor.CurrentHP -= costInfo.value; else actor.CurrentSP -= costInfo.value;
            _io.WriteLine($"{actor.Name} uses {skillData.Name}!");

            int maxPenalty = 2;
            foreach (var target in targets)
            {
                if (skillData.Category.Contains("Recovery")) { PerformHealingLogic(skillData, target); continue; }
                if (skillData.Category.Contains("Enhance")) { PerformBuffLogic(skillData.Name, actor, target); continue; }

                Element elem = ElementHelper.FromCategory(skillData.Category);
                bool isPhys = (elem == Element.Slash || elem == Element.Strike || elem == Element.Pierce);
                int atk = isPhys ? actor.GetStat(StatType.STR) : actor.GetStat(StatType.MAG);
                int def = target.GetStat(StatType.END) + target.GetDefense();

                double dmgBase = 5.0 * Math.Sqrt(skillData.GetPowerVal() * ((double)atk / Math.Max(1, def)));
                if (skillData.Effect.Contains("50%") && skillData.Effect.Contains("HP")) dmgBase = target.CurrentHP * 0.5;
                if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

                int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));
                var res = target.ReceiveDamage(damage, elem, isPhys && target.IsRigidBody);
                _io.WriteLine($"{target.Name} takes {res.DamageDealt} dmg. {res.Message}");

                UpdateKnowledge(target.SourceId, elem, res, knowledge);
                ProcessAilmentInfliction(target, skillData);

                int cost = CalculateIconCost(res);
                if (cost > maxPenalty || cost >= TERMINAL_ICON_COST) maxPenalty = cost;
            }
            return maxPenalty;
        }

        private void UpdateKnowledge(string sourceId, Element elem, CombatResult res, BattleKnowledge k)
        {
            Affinity aff = Affinity.Normal;
            if (res.Type == HitType.Weakness) aff = Affinity.Weak;
            else if (res.Type == HitType.Repel) aff = Affinity.Repel;
            else if (res.Type == HitType.Absorb) aff = Affinity.Absorb;
            else if (res.Type == HitType.Null) aff = Affinity.Null;
            else if (res.Message.Contains("Resisted")) aff = Affinity.Resist;
            k.Learn(sourceId, elem, aff);
        }

        private int CalculateIconCost(CombatResult res)
        {
            if (res.Type == HitType.Weakness || res.IsCritical) return 1;
            if (res.Type == HitType.Miss || res.Type == HitType.Null) return 4;
            if (res.Type == HitType.Repel || res.Type == HitType.Absorb) return TERMINAL_ICON_COST;
            return 2;
        }

        private (bool executed, int cost) ExecuteSkillMenu(Combatant actor, List<string> skills, List<Combatant> opponents)
        {
            if (!skills.Any()) { _io.WriteLine("No skills."); return (false, 0); }
            List<string> options = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var s in skills)
            {
                if (Database.Skills.TryGetValue(s, out var d))
                {
                    var cost = d.ParseCost();
                    options.Add($"{s} ({d.Cost})");
                    disabled.Add(cost.isHP ? actor.CurrentHP <= cost.value : actor.CurrentSP < cost.value);
                }
            }
            options.Add("Cancel"); disabled.Add(false);

            int idx = _io.RenderMenu("Select Skill:", options, 0, disabled, (i) => {
                if (i >= 0 && i < skills.Count && Database.Skills.TryGetValue(skills[i], out var d))
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
            });

            if (idx == -1 || idx == options.Count - 1) return (false, 0);

            var skillData = Database.Skills[skills[idx]];
            List<Combatant> targets = new List<Combatant>();
            bool isAll = skillData.Effect.Contains("all", StringComparison.OrdinalIgnoreCase);
            bool isAlly = skillData.Effect.Contains("ally", StringComparison.OrdinalIgnoreCase) || skillData.Effect.Contains("party", StringComparison.OrdinalIgnoreCase);

            if (isAlly)
            {
                if (isAll) targets = _party.GetAliveMembers();
                else { var t = SelectAllyTarget(skillData.Effect.Contains("Revive")); if (t != null) targets.Add(t); else return (false, 0); }
            }
            else if (skillData.Effect.Contains("self", StringComparison.OrdinalIgnoreCase)) targets.Add(actor);
            else
            {
                if (isAll) targets = opponents.Where(e => !e.IsDead).ToList();
                else { var t = SelectTarget(opponents); if (t != null) targets.Add(t); else return (false, 0); }
            }

            return (true, ExecuteSkillLogic(actor, skillData, targets, _playerKnowledge));
        }

        private (bool executed, int cost) ExecuteItemMenu(Combatant actor)
        {
            var usable = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (!usable.Any()) { _io.WriteLine("Empty."); return (false, 0); }

            var opts = usable.Select(i => $"{i.Name} x{_inv.GetQuantity(i.Id)}").ToList();
            var dis = usable.Select(i => i.Name == "Goho-M").ToList();
            opts.Add("Cancel"); dis.Add(false);

            int idx = _io.RenderMenu("Items", opts, 0, dis, (i) => { if (i >= 0 && i < usable.Count) _io.WriteLine(usable[i].Description); });
            if (idx == -1 || idx == opts.Count - 1) return (false, 0);

            var item = usable[idx];
            if (item.Name == "Traesto Gem")
            {
                _io.WriteLine("Escaping..."); TraestoUsed = true;
                _inv.RemoveItem(item.Id, 1); return (true, 0);
            }

            var target = SelectAllyTarget(item.Type == "Revive");
            if (target == null) return (false, 0);

            if (PerformItem(item, target)) { _inv.RemoveItem(item.Id, 1); return (true, 2); }
            return (false, 0);
        }

        private void ProcessAilmentInfliction(Combatant target, SkillData skillData)
        {
            foreach (var kvp in _effectToAilmentMap)
            {
                if (skillData.Effect.Contains(kvp.Key) && Database.Ailments.TryGetValue(kvp.Value, out var ail))
                {
                    int chance = 100;
                    var m = Regex.Match(skillData.Effect, @"\((\d+)% chance\)");
                    if (m.Success) int.TryParse(m.Groups[1].Value, out chance);
                    if (_rnd.Next(100) < chance)
                        if (target.InflictAilment(ail, 3)) _io.WriteLine($"-> {target.Name} afflicted with {ail.Name}!", ConsoleColor.Magenta);
                }
            }
        }

        private void PerformHealingLogic(SkillData skill, Combatant target)
        {
            int heal = skill.GetPowerVal();
            if (heal == 0)
            {
                var match = Regex.Match(skill.Effect, @"\((\d+)\)");
                if (match.Success) int.TryParse(match.Groups[1].Value, out heal);
                else heal = 50;
            }
            if (skill.Effect.Contains("Cure") && target.CheckCure(skill.Effect)) _io.WriteLine($"{target.Name} was cured.");
            if (target.IsDead && skill.Effect.Contains("Revive"))
            {
                target.CurrentHP = target.MaxHP / 2; _io.WriteLine($"{target.Name} revived!"); return;
            }
            if (heal > 0 && !target.IsDead)
            {
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                _io.WriteLine($"{target.Name} recovered HP.");
            }
        }

        private void PerformBuffLogic(string skillName, Combatant user, Combatant target)
        {
            int turns = 3;
            if (skillName.Contains("Taru"))
            {
                if (skillName.Contains("nda")) { target.AddBuff("AttackDown", turns); _io.WriteLine($"{target.Name} Attack Down."); }
                else { target.AddBuff("Attack", turns); _io.WriteLine($"{target.Name} Attack Up."); }
            }
            if (skillName.Contains("Raku"))
            {
                if (skillName.Contains("nda")) { target.AddBuff("DefenseDown", turns); _io.WriteLine($"{target.Name} Defense Down."); }
                else { target.AddBuff("Defense", turns); _io.WriteLine($"{target.Name} Defense Up."); }
            }
            if (skillName.Contains("Suku"))
            {
                if (skillName.Contains("nda")) { target.AddBuff("AgilityDown", turns); _io.WriteLine($"{target.Name} Agility Down."); }
                else { target.AddBuff("Agility", turns); _io.WriteLine($"{target.Name} Agility Up."); }
            }
            if (skillName.Contains("Heat Riser"))
            {
                target.AddBuff("Attack", turns); target.AddBuff("Defense", turns); target.AddBuff("Agility", turns);
                _io.WriteLine($"{target.Name}'s stats rose significantly!");
            }
        }

        private Combatant SelectAllyTarget(bool selectDead)
        {
            var targets = _party.ActiveParty.Where(m => selectDead ? m.IsDead : !m.IsDead).ToList();
            if (!targets.Any()) { _io.WriteLine("No valid targets."); return null; }
            var names = targets.Select(e => $"{e.Name} (HP: {e.CurrentHP}/{e.MaxHP})").ToList();
            names.Add("Cancel");
            int idx = _io.RenderMenu("Select Ally:", names, 0);
            return (idx == -1 || idx == names.Count - 1) ? null : targets[idx];
        }

        private Combatant SelectTarget(List<Combatant> candidates)
        {
            var live = candidates.Where(e => !e.IsDead).ToList();
            if (live.Count <= 1) return live.FirstOrDefault();
            var names = live.Select(e => $"{e.Name} (HP: {e.CurrentHP})").ToList();
            names.Add("Cancel");
            int idx = _io.RenderMenu("Select Target:", names, 0);
            return (idx == -1 || idx == names.Count - 1) ? null : live[idx];
        }

        private int ProcessSmartAI(Combatant actor, List<Combatant> allies, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            _io.Wait(500);
            if (actor.ActivePersona == null) return ExecutePhysicalAttack(actor, opponents.OrderBy(x => _rnd.Next()).First(), knowledge);

            var critAlly = allies.FirstOrDefault(a => (double)a.CurrentHP / a.MaxHP < 0.5);
            if (critAlly != null)
            {
                var healSkill = FindSkillByKeyword(actor, "Salvation", "Mediarahan", "Diarahan", "Mediarama", "Diarama", "Media", "Dia");
                if (healSkill != null) return ExecuteSkillLogic(actor, healSkill, new List<Combatant> { critAlly }, knowledge);
            }

            var ailedAlly = allies.FirstOrDefault(a => a.CurrentAilment != null && a.CurrentAilment.ActionRestriction != "None");
            if (ailedAlly != null)
            {
                var cureSkill = FindSkillByKeyword(actor, "Patra", "Cure", "Amrita");
                if (cureSkill != null) return ExecuteSkillLogic(actor, cureSkill, new List<Combatant> { ailedAlly }, knowledge);
            }

            foreach (var target in opponents)
            {
                foreach (var sName in actor.ActivePersona.SkillSet)
                {
                    if (Database.Skills.TryGetValue(sName, out var sData))
                    {
                        if (sData.Category.Contains("Recovery") || sData.Category.Contains("Enhance")) continue;
                        if (knowledge.IsWeaknessKnown(target.SourceId, ElementHelper.FromCategory(sData.Category)))
                            return ExecuteSkillLogic(actor, sData, new List<Combatant> { target }, knowledge);
                    }
                }
            }

            if (_rnd.Next(100) < 85)
            {
                var offSkills = actor.ActivePersona.SkillSet.Select(s => Database.Skills.ContainsKey(s) ? Database.Skills[s] : null)
                    .Where(s => s != null && !s.Category.Contains("Recovery") && !s.Category.Contains("Enhance")).ToList();
                if (offSkills.Any())
                {
                    var skill = offSkills[_rnd.Next(offSkills.Count)];
                    var target = opponents[_rnd.Next(opponents.Count)];
                    if (!knowledge.IsResistanceKnown(target.SourceId, ElementHelper.FromCategory(skill.Category)))
                        return ExecuteSkillLogic(actor, skill, new List<Combatant> { target }, knowledge);
                }
            }

            return ExecutePhysicalAttack(actor, opponents.OrderBy(x => _rnd.Next()).First(), knowledge);
        }

        private SkillData FindSkillByKeyword(Combatant actor, params string[] keywords)
        {
            if (actor.ActivePersona == null) return null;
            foreach (var s in actor.ActivePersona.SkillSet)
                if (Database.Skills.ContainsKey(s))
                    foreach (var k in keywords) if (s.Contains(k)) return Database.Skills[s];
            return null;
        }

        private bool PerformItem(ItemData item, Combatant target)
        {
            _io.WriteLine($"\n[ITEM] Used {item.Name}!");
            bool used = false;
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + item.EffectValue);
                    _io.WriteLine($"Healed {item.EffectValue} HP."); used = true; break;
                case "Spirit":
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                    _io.WriteLine($"Recovered {item.EffectValue} SP."); used = true; break;
                case "Revive":
                    if (target.CurrentHP > 0) _io.WriteLine("Target alive.");
                    else { target.CurrentHP = target.MaxHP / 2; _io.WriteLine("Revived!"); used = true; }
                    break;
                case "Cure":
                    if (target.CurrentAilment == null) _io.WriteLine("Healthy.");
                    else { target.RemoveAilment(); _io.WriteLine("Cured!"); used = true; }
                    break;
            }
            return used;
        }

        private bool ExecuteTacticsMenu(Combatant actor)
        {
            List<string> opts = new List<string> { "Escape", "Strategy", "Cancel" };
            List<bool> dis = new List<bool> { _isBossBattle, actor.Class != ClassType.Operator, false };
            int c = _io.RenderMenu("Tactics", opts, 0, dis);
            if (c == 0) return AttemptEscape(actor);
            if (c == 1) return ExecuteStrategyMenu(actor);
            return false;
        }

        private bool AttemptEscape(Combatant actor)
        {
            _io.WriteLine("Attempting escape...");
            int pAgi = actor.GetStat(StatType.AGI);
            double eAvgAgi = Math.Max(1, _enemies.Average(e => e.GetStat(StatType.AGI)));
            double chance = Math.Clamp(10.0 + 40.0 * (pAgi / eAvgAgi) + (actor.GetStat(StatType.LUK) - _enemies.Average(e => e.GetStat(StatType.LUK))) * 0.5, 5.0, 95.0);
            if (_rnd.Next(100) < chance) { _io.WriteLine("Escaped!"); Escaped = true; return true; }
            _io.WriteLine("Blocked!"); return true;
        }

        private bool ExecuteStrategyMenu(Combatant actor)
        {
            var demons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (!demons.Any()) return false;
            var opts = demons.Select(d => $"{d.Name} [{d.BattleControl}]").ToList(); opts.Add("Back");
            int idx = _io.RenderMenu("STRATEGY", opts, 0);
            if (idx >= 0 && idx < demons.Count) demons[idx].BattleControl = (demons[idx].BattleControl == ControlState.ActFreely) ? ControlState.DirectControl : ControlState.ActFreely;
            return false;
        }

        private void CheckBattleEnd() => CheckBattleEndCondition();
        private bool CheckBattleEndCondition()
        {
            if (_enemies.All(e => e.IsDead)) { BattleEnded = true; PlayerWon = true; return true; }
            if (_party.IsPartyWiped()) { BattleEnded = true; PlayerWon = false; return true; }
            return false;
        }

        private void ResolveBattleEnd()
        {
            if (PlayerWon)
            {
                _io.WriteLine("\nVICTORY!", ConsoleColor.Green);
                int exp = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var d) ? d.ExpYield : 0);
                int macca = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var d) ? d.MaccaYield : 0);
                _io.WriteLine($"Gained {exp} EXP and {macca} Macca.");
                foreach (var m in _party.GetAliveMembers()) { m.GainExp(exp); m.ActivePersona?.GainExp(exp, _io); }
                _eco.AddMacca(macca);
            }
            else if (!Escaped && !TraestoUsed) _io.WriteLine("\nDEFEAT...", ConsoleColor.Red);
            _io.ReadKey();
        }

        private bool ProcessTurnStart(Combatant c)
        {
            if (c.IsGuarding) { c.IsGuarding = false; _io.WriteLine($"{c.Name} drops guard."); }
            if (c.CurrentAilment == null) return true;
            _io.WriteLine($"\n{c.Name} suffers {c.CurrentAilment.Name}...", ConsoleColor.Magenta);
            if (c.CurrentAilment.ActionRestriction == "SkipTurn") { _io.WriteLine("Cannot move!"); return false; }
            if (c.CurrentAilment.ActionRestriction == "ChanceSkip" && _rnd.Next(100) < 50) { _io.WriteLine("Too confused!"); return false; }
            return true;
        }

        private void ProcessTurnEnd(Combatant c)
        {
            foreach (var m in c.TickBuffs()) _io.WriteLine(m);
            if (c.CurrentAilment != null && c.CurrentAilment.DotPercent > 0)
            {
                int dmg = (int)(c.MaxHP * c.CurrentAilment.DotPercent);
                c.CurrentHP = Math.Max(1, c.CurrentHP - dmg);
                _io.WriteLine($"{c.Name} takes {dmg} damage.", ConsoleColor.DarkMagenta);
            }
            if (c.CurrentAilment != null)
            {
                c.AilmentDuration--;
                if (c.AilmentDuration <= 0) { _io.WriteLine($"{c.Name} recovered.", ConsoleColor.Cyan); c.RemoveAilment(); }
            }
        }

        private (bool executed, int cost) ExecuteCompMenu(Combatant actor)
        {
            List<string> options = new List<string> { "Summon", "Return", "Analyze", "Cancel" };
            int idx = _io.RenderMenu("COMP SYSTEM", options, 0);
            if (idx == 0) return ExecuteSummonMenu(actor);
            if (idx == 1) return ExecuteReturnMenu(actor);
            if (idx == 2) return ExecuteAnalyzeMenu();
            return (false, 0);
        }

        private (bool executed, int cost) ExecuteSummonMenu(Combatant actor)
        {
            if (!actor.DemonStock.Any()) { _io.WriteLine("No demons."); return (false, 0); }
            var opts = actor.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList(); opts.Add("Cancel");
            int idx = _io.RenderMenu("SUMMON", opts, 0);
            if (idx < 0 || idx == opts.Count - 1) return (false, 0);
            if (_party.SummonDemon(actor.DemonStock[idx]))
            {
                _io.WriteLine($"{actor.Name} summoned {actor.DemonStock[idx].Name}!");
                actor.DemonStock.RemoveAt(idx); return (true, 2);
            }
            _io.WriteLine("Party full!"); return (false, 0);
        }

        private (bool executed, int cost) ExecuteReturnMenu(Combatant actor)
        {
            var active = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (!active.Any()) { _io.WriteLine("No demons."); return (false, 0); }
            var opts = active.Select(d => d.Name).ToList(); opts.Add("Cancel");
            int idx = _io.RenderMenu("RETURN", opts, 0);
            if (idx < 0 || idx == opts.Count - 1) return (false, 0);
            if (_party.ReturnDemon(active[idx])) { actor.DemonStock.Add(active[idx]); return (true, 2); }
            return (false, 0);
        }

        private (bool executed, int cost) ExecuteAnalyzeMenu()
        {
            Combatant target = SelectTarget(_enemies); if (target == null) return (false, 0);
            _io.Clear(); _io.WriteLine($"=== ANALYSIS: {target.Name} ===");
            foreach (Element e in Enum.GetValues(typeof(Element)))
            {
                if (e == Element.None) continue;
                var aff = target.ActivePersona?.GetAffinity(e) ?? Affinity.Normal;
                _io.WriteLine($"{e}: {aff}"); _playerKnowledge.Learn(target.SourceId, e, aff);
            }
            _io.WriteLine("\n[Any Key]"); _io.ReadKey(); return (true, 2);
        }
    }
}