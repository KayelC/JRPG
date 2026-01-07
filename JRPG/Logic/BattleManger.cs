using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Services;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic
{
    public class BattleManager
    {
        private PartyManager _party;
        private List<Combatant> _enemies;
        private InventoryManager _inv;
        private EconomyManager _eco;
        private IGameIO _io;
        private Random _rnd = new Random();

        // Battle State
        private bool _isBossBattle;
        private int _turnIcons; // 1 Icon = 2 Ticks
        private bool _isPlayerTurn;

        // Knowledge Bases
        private BattleKnowledge _playerKnowledge = new BattleKnowledge();
        private BattleKnowledge _enemyKnowledge = new BattleKnowledge();

        // Flags
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
            foreach (var e in _enemies)
            {
                _io.WriteLine($"Appeared: {e.Name} (Lv.{e.Level})");
            }
            _io.Wait(1000);

            // Initiative Logic
            double pAvgAgi = _party.ActiveParty.Any() ? _party.ActiveParty.Average(c => c.GetStat(StatType.AGI)) : 0;
            double eAvgAgi = _enemies.Any() ? _enemies.Average(c => c.GetStat(StatType.AGI)) : 0;

            // Random variance
            double pRoll = pAvgAgi * (0.9 + (_rnd.NextDouble() * 0.2));
            double eRoll = eAvgAgi * (0.9 + (_rnd.NextDouble() * 0.2));

            _isPlayerTurn = pRoll >= eRoll;

            if (_isPlayerTurn) _io.WriteLine("Player Party attacks first!", ConsoleColor.Cyan);
            else _io.WriteLine("Enemy Party attacks first!", ConsoleColor.Red);
            _io.Wait(1000);

            // Main Battle Loop
            while (!BattleEnded)
            {
                if (TraestoUsed || Escaped) break;

                if (_isPlayerTurn) ExecutePhase(_party.GetAliveMembers(), _enemies.Where(e => !e.IsDead).ToList(), true);
                else ExecutePhase(_enemies.Where(e => !e.IsDead).ToList(), _party.GetAliveMembers(), false);

                CheckBattleEnd();

                if (!BattleEnded)
                {
                    _isPlayerTurn = !_isPlayerTurn;
                }
            }

            ResolveBattleEnd();
        }

        private void ExecutePhase(List<Combatant> actors, List<Combatant> opponents, bool isPlayerPhase)
        {
            int turnIcons = actors.Count;
            int ticks = turnIcons * 2; // 2 Ticks = 1 Full Icon

            ConsoleColor phaseColor = isPlayerPhase ? ConsoleColor.Cyan : ConsoleColor.Red;
            string phaseName = isPlayerPhase ? "PLAYER" : "ENEMY";

            _io.WriteLine($"\n--- {phaseName} TURN (Icons: {turnIcons}) ---", phaseColor);

            int actorIndex = 0;

            while (ticks > 0 && actors.Count > 0 && opponents.Count > 0)
            {
                if (TraestoUsed || Escaped) return;
                if (CheckBattleEndCondition()) return;

                if (actorIndex >= actors.Count) actorIndex = 0;
                Combatant actor = actors[actorIndex];

                if (actor.IsDead)
                {
                    actorIndex++;
                    continue;
                }

                // Process Ailments/Buffs at start of actor's action
                if (!ProcessTurnStart(actor))
                {
                    // Actor incapacitated
                    ticks -= 2; // Lose 1 Icon
                    actorIndex++;
                    ProcessTurnEnd(actor);
                    continue;
                }

                if (isPlayerPhase)
                    _io.WriteLine($"\nActive: {actor.Name} | HP: {actor.CurrentHP} | Icons: {ticks / 2.0:F1}");
                else
                    _io.WriteLine($"\nEnemy: {actor.Name} is acting... | Icons: {ticks / 2.0:F1}");

                // --- ACTION SELECTION ---
                int cost = 0;

                if (actor.Controller == ControllerType.LocalPlayer || (actor.Class == ClassType.Demon && actor.BattleControl == ControlState.DirectControl))
                {
                    cost = ProcessHumanAction(actor, opponents);
                }
                else
                {
                    // Smart AI for both Allies and Enemies
                    var knowledge = isPlayerPhase ? _playerKnowledge : _enemyKnowledge;
                    cost = ProcessSmartAI(actor, actors, opponents, knowledge);
                }

                ticks -= cost;

                ProcessTurnEnd(actor);

                // Refresh opponent list
                opponents = isPlayerPhase ? _enemies.Where(e => !e.IsDead).ToList() : _party.GetAliveMembers();
                if (opponents.Count == 0) return;

                actorIndex++;
            }
        }

        private int ProcessHumanAction(Combatant actor, List<Combatant> opponents)
        {
            if (actor.IsGuarding)
            {
                actor.IsGuarding = false;
                _io.WriteLine($"{actor.Name} lowers guard.");
            }

            while (true)
            {
                // Dynamic Menu Construction based on Class
                List<string> options = new List<string> { "Attack", "Guard" };

                if (actor.Class == ClassType.PersonaUser || actor.Class == ClassType.WildCard)
                {
                    options.Add("Persona");
                    if (actor.ExtraSkills.Count > 0) options.Add("Skill");
                }
                else if (actor.Class == ClassType.Operator)
                {
                    options.Add("Command"); // Operator Skills
                    options.Add("COMP"); // Summon/Return/Analyze
                }
                else
                {
                    options.Add("Skill");
                }

                options.Add("Item");
                options.Add("Tactics");
                options.Add("Pass");

                bool isPanicked = actor.CurrentAilment?.Name == "Panic";
                List<bool> disabled = new List<bool>();

                foreach (var opt in options)
                {
                    if ((opt == "Persona" || opt == "Skill" || opt == "Command" || opt == "COMP") && isPanicked)
                    {
                        disabled.Add(true);
                    }
                    else
                    {
                        disabled.Add(false);
                    }
                }

                int choice = _io.RenderMenu($"Command: {actor.Name}", options, 0, disabled);

                // FIX: Safety check for -1 (Esc) input
                if (choice == -1) continue;

                string selectedOption = options[choice];

                if (selectedOption == "Attack")
                {
                    Combatant target = SelectTarget(opponents);
                    if (target == null) continue;
                    return ExecutePhysicalAttack(actor, target, _playerKnowledge);
                }
                else if (selectedOption == "Guard")
                {
                    actor.IsGuarding = true;
                    _io.WriteLine($"{actor.Name} assumes a defensive stance.");
                    return 2;
                }
                else if (selectedOption == "Persona" || selectedOption == "Skill")
                {
                    if (isPanicked) continue;
                    List<string> skillsToShow;
                    if (selectedOption == "Persona") skillsToShow = actor.ActivePersona?.SkillSet ?? new List<string>();
                    else skillsToShow = actor.ExtraSkills;

                    if (actor.Class == ClassType.Human || actor.Class == ClassType.Demon)
                        skillsToShow = actor.GetConsolidatedSkills();

                    var result = ExecuteSkillMenu(actor, skillsToShow, opponents);
                    if (result.executed) return result.cost;
                }
                else if (selectedOption == "Command")
                {
                    if (isPanicked) continue;
                    var result = ExecuteSkillMenu(actor, actor.ExtraSkills, opponents);
                    if (result.executed) return result.cost;
                }
                else if (selectedOption == "COMP")
                {
                    if (isPanicked) continue;
                    var result = ExecuteCompMenu(actor);
                    if (result.executed) return result.cost;
                }
                else if (selectedOption == "Item")
                {
                    var result = ExecuteItemMenu(actor);
                    if (result.executed) return result.cost;
                }
                else if (selectedOption == "Tactics")
                {
                    if (ExecuteTacticsMenu(actor)) return 0;
                }
                else if (selectedOption == "Pass")
                {
                    _io.WriteLine($"{actor.Name} passes the baton.");
                    return 1;
                }

                if (TraestoUsed || Escaped) return 2;
            }
        }

        private (bool executed, int cost) ExecuteCompMenu(Combatant actor)
        {
            List<string> options = new List<string> { "Summon", "Return", "Analyze", "Cancel" };
            int idx = _io.RenderMenu("COMP SYSTEM", options, 0);
            if (idx == -1 || idx == options.Count - 1) return (false, 0);

            if (idx == 0) return ExecuteSummonMenu(actor);
            if (idx == 1) return ExecuteReturnMenu(actor);
            if (idx == 2) return ExecuteAnalyzeMenu();

            return (false, 0);
        }

        private (bool executed, int cost) ExecuteSummonMenu(Combatant actor)
        {
            if (actor.DemonStock.Count == 0)
            {
                _io.WriteLine("No demons in stock.");
                _io.Wait(500);
                return (false, 0);
            }

            List<string> demonOpts = actor.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList();
            demonOpts.Add("Cancel");

            int idx = _io.RenderMenu("SUMMON DEMON", demonOpts, 0);
            if (idx == -1 || idx == demonOpts.Count - 1) return (false, 0);

            Combatant demon = actor.DemonStock[idx];

            if (_party.SummonDemon(demon))
            {
                actor.DemonStock.RemoveAt(idx);
                demon.OwnerId = actor.SourceId;
                _io.WriteLine($"{actor.Name} summoned {demon.Name}!");
                _io.Wait(1000);
                return (true, 2);
            }
            else
            {
                _io.WriteLine("Party is full! Return a demon first.");
                _io.Wait(1000);
                return (false, 0);
            }
        }

        private (bool executed, int cost) ExecuteReturnMenu(Combatant actor)
        {
            var activeDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (activeDemons.Count == 0)
            {
                _io.WriteLine("No demons currently summoned.");
                _io.Wait(500);
                return (false, 0);
            }

            List<string> demonOpts = activeDemons.Select(d => $"{d.Name} (HP: {d.CurrentHP})").ToList();
            demonOpts.Add("Cancel");

            int idx = _io.RenderMenu("RETURN DEMON", demonOpts, 0);
            if (idx == -1 || idx == demonOpts.Count - 1) return (false, 0);

            Combatant demon = activeDemons[idx];

            if (_party.ReturnDemon(demon))
            {
                actor.DemonStock.Add(demon);
                _io.WriteLine($"{demon.Name} returned to COMP.");
                _io.Wait(1000);
                return (true, 2);
            }
            return (false, 0);
        }

        private (bool executed, int cost) ExecuteAnalyzeMenu()
        {
            Combatant target = SelectTarget(_enemies);
            if (target == null) return (false, 0);

            _io.Clear();
            _io.WriteLine($"=== ANALYSIS: {target.Name} ===");
            _io.WriteLine($"Level: {target.Level}");
            _io.WriteLine($"HP: {target.CurrentHP}/{target.MaxHP} SP: {target.CurrentSP}/{target.MaxSP}");

            _io.WriteLine("\nAffinities:");
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _io.WriteLine($" {elem}: {aff}");
                _playerKnowledge.Learn(target.SourceId, elem, aff);
            }

            _io.WriteLine("\n[Press Any Key]");
            _io.ReadKey();
            return (true, 2);
        }

        private int ProcessSmartAI(Combatant actor, List<Combatant> allies, List<Combatant> opponents, BattleKnowledge knowledge)
        {
            _io.Wait(500);

            if (actor.ActivePersona == null)
            {
                var target = opponents.OrderBy(x => _rnd.Next()).First();
                return ExecutePhysicalAttack(actor, target, knowledge);
            }

            var criticalAlly = allies.FirstOrDefault(a => (double)a.CurrentHP / a.MaxHP < 0.5);
            if (criticalAlly != null)
            {
                var healSkill = FindSkillByKeyword(actor, "Salvation", "Mediarahan", "Diarahan", "Mediarama", "Diarama", "Media", "Dia");
                if (healSkill != null)
                {
                    return ExecuteSkillLogic(actor, healSkill, new List<Combatant> { criticalAlly }, knowledge);
                }
            }

            var ailedAlly = allies.FirstOrDefault(a => a.CurrentAilment != null && (a.CurrentAilment.ActionRestriction != "None"));
            if (ailedAlly != null)
            {
                var cureSkill = FindSkillByKeyword(actor, "Patra", "Cure", "Amrita");
                if (cureSkill != null)
                {
                    return ExecuteSkillLogic(actor, cureSkill, new List<Combatant> { ailedAlly }, knowledge);
                }
            }

            foreach (var target in opponents)
            {
                foreach (var skillName in actor.ActivePersona.SkillSet)
                {
                    if (Database.Skills.TryGetValue(skillName, out var skillData))
                    {
                        if (skillData.Category.Contains("Recovery") || skillData.Category.Contains("Enhance")) continue;
                        Element skillElement = ElementHelper.FromCategory(skillData.Category);
                        if (knowledge.IsWeaknessKnown(target.SourceId, skillElement))
                        {
                            return ExecuteSkillLogic(actor, skillData, new List<Combatant> { target }, knowledge);
                        }
                    }
                }
            }

            if (actor.ActivePersona.SkillSet.Count > 0 && _rnd.Next(100) < 85)
            {
                var offensiveSkills = actor.ActivePersona.SkillSet
                    .Where(s => Database.Skills.ContainsKey(s))
                    .Select(s => Database.Skills[s])
                    .Where(s => !s.Category.Contains("Recovery") && !s.Category.Contains("Enhance"))
                    .ToList();

                if (offensiveSkills.Count > 0)
                {
                    var skill = offensiveSkills[_rnd.Next(offensiveSkills.Count)];
                    var target = opponents[_rnd.Next(opponents.Count)];
                    Element elem = ElementHelper.FromCategory(skill.Category);
                    if (!knowledge.IsResistanceKnown(target.SourceId, elem))
                    {
                        return ExecuteSkillLogic(actor, skill, new List<Combatant> { target }, knowledge);
                    }
                }
            }

            var physTarget = opponents.OrderBy(x => _rnd.Next()).First();
            return ExecutePhysicalAttack(actor, physTarget, knowledge);
        }

        private SkillData FindSkillByKeyword(Combatant actor, params string[] keywords)
        {
            if (actor.ActivePersona == null) return null;
            foreach (var s in actor.ActivePersona.SkillSet)
            {
                if (!Database.Skills.ContainsKey(s)) continue;
                foreach (var k in keywords)
                {
                    if (s.Contains(k)) return Database.Skills[s];
                }
            }
            return null;
        }

        private int ExecutePhysicalAttack(Combatant actor, Combatant target, BattleKnowledge knowledge)
        {
            _io.WriteLine($"{actor.Name} attacks {target.Name}!");

            int power;
            if (actor.EquippedWeapon != null) power = actor.EquippedWeapon.Power;
            else power = actor.Level + (actor.GetStat(StatType.STR) * 2);

            int atk = actor.GetStat(StatType.STR);
            int def = target.GetStat(StatType.END) + target.GetDefense();

            if (actor.CurrentAilment != null) atk = (int)(atk * actor.CurrentAilment.DamageDealMult);

            double ratio = (double)atk / Math.Max(1, def);
            double dmgBase = 5.0 * Math.Sqrt((double)power * ratio);

            if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

            int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));

            bool isCritical = false;
            if (target.IsRigidBody) isCritical = true;
            else
            {
                int critChance = (actor.GetStat(StatType.LUK) - target.GetStat(StatType.LUK)) + 5;
                if (_rnd.Next(0, 100) < critChance) isCritical = true;
            }

            var res = target.ReceiveDamage(damage, actor.WeaponElement, isCritical);
            _io.WriteLine($"{target.Name} takes {res.DamageDealt} dmg. {res.Message}");

            if (res.Type == HitType.Weakness) knowledge.Learn(target.SourceId, actor.WeaponElement, Affinity.Weak);
            if (res.Type == HitType.Repel) knowledge.Learn(target.SourceId, actor.WeaponElement, Affinity.Repel);
            if (res.Type == HitType.Absorb) knowledge.Learn(target.SourceId, actor.WeaponElement, Affinity.Absorb);
            if (res.Type == HitType.Null) knowledge.Learn(target.SourceId, actor.WeaponElement, Affinity.Null);
            if (res.Message.Contains("Resisted")) knowledge.Learn(target.SourceId, actor.WeaponElement, Affinity.Resist);

            return CalculateIconCost(res);
        }

        private int ExecuteSkillLogic(Combatant actor, SkillData skillData, List<Combatant> targets, BattleKnowledge knowledge)
        {
            var costInfo = skillData.ParseCost();
            if (costInfo.isHP) actor.CurrentHP -= costInfo.value; else actor.CurrentSP -= costInfo.value;

            _io.WriteLine($"{actor.Name} uses {skillData.Name}!");

            bool hitWeakness = false;
            bool missed = false;

            foreach (var target in targets)
            {
                if (skillData.Category.Contains("Recovery")) { PerformHealingLogic(skillData, target); continue; }
                if (skillData.Category.Contains("Enhance")) { PerformBuffLogic(skillData.Name, actor, target); continue; }

                int power = skillData.GetPowerVal();
                Element elem = ElementHelper.FromCategory(skillData.Category);
                bool isPhys = (elem == Element.Slash || elem == Element.Strike || elem == Element.Pierce);
                int atk = isPhys ? actor.GetStat(StatType.STR) : actor.GetStat(StatType.MAG);
                int def = target.GetStat(StatType.END) + target.GetDefense();

                double dmgBase = 5.0 * Math.Sqrt((double)power * atk / Math.Max(1, def));
                if (skillData.Effect.Contains("50%") && skillData.Effect.Contains("HP")) dmgBase = target.CurrentHP * 0.5;
                if (target.CurrentAilment != null) dmgBase *= target.CurrentAilment.DamageTakenMult;

                int damage = (int)(dmgBase * (0.95 + _rnd.NextDouble() * 0.1));
                bool isCrit = isPhys && target.IsRigidBody;

                var res = target.ReceiveDamage(damage, elem, isCrit);
                _io.WriteLine($"{target.Name} takes {res.DamageDealt} dmg. {res.Message}");

                if (res.Type == HitType.Weakness) knowledge.Learn(target.SourceId, elem, Affinity.Weak);
                else if (res.Type == HitType.Repel) knowledge.Learn(target.SourceId, elem, Affinity.Repel);
                else if (res.Type == HitType.Absorb) knowledge.Learn(target.SourceId, elem, Affinity.Absorb);
                else if (res.Type == HitType.Null) knowledge.Learn(target.SourceId, elem, Affinity.Null);
                if (res.Message.Contains("Resisted")) knowledge.Learn(target.SourceId, elem, Affinity.Resist);

                ProcessAilmentInfliction(target, skillData);

                if (res.Type == HitType.Weakness || res.IsCritical) hitWeakness = true;
                if (res.Type == HitType.Miss || res.Type == HitType.Null || res.Type == HitType.Repel || res.Type == HitType.Absorb) missed = true;
            }

            if (missed) return 4;
            if (hitWeakness) return 1;
            return 2;
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
                    if (_rnd.Next(0, 100) < chance)
                    {
                        if (target.InflictAilment(ail, 3)) _io.WriteLine($"-> {target.Name} afflicted with {ail.Name}!", ConsoleColor.Magenta);
                    }
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
                target.CurrentHP = target.MaxHP / 2;
                _io.WriteLine($"{target.Name} revived!");
                return;
            }
            if (heal > 0 && !target.IsDead)
            {
                int oldHP = target.CurrentHP;
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                _io.WriteLine($"{target.Name} recovered {target.CurrentHP - oldHP} HP.");
            }
        }

        private void PerformBuffLogic(string skillName, Combatant user, Combatant target)
        {
            if (skillName.Contains("Taru")) { if (skillName.Contains("nda")) { target.AddBuff("AttackDown", 3); _io.WriteLine($"{target.Name} Attack Down."); } else { target.AddBuff("Attack", 3); _io.WriteLine($"{target.Name} Attack Up."); } }
            if (skillName.Contains("Raku")) { if (skillName.Contains("nda")) { target.AddBuff("DefenseDown", 3); _io.WriteLine($"{target.Name} Defense Down."); } else { target.AddBuff("Defense", 3); _io.WriteLine($"{target.Name} Defense Up."); } }
            if (skillName.Contains("Suku")) { if (skillName.Contains("nda")) { target.AddBuff("AgilityDown", 3); _io.WriteLine($"{target.Name} Agility Down."); } else { target.AddBuff("Agility", 3); _io.WriteLine($"{target.Name} Agility Up."); } }
            if (skillName.Contains("Heat Riser"))
            {
                target.AddBuff("Attack", 3);
                target.AddBuff("Defense", 3);
                target.AddBuff("Agility", 3);
                _io.WriteLine($"{target.Name}'s stats rose significantly!");
            }
        }

        private Combatant SelectTarget(List<Combatant> candidates)
        {
            var live = candidates.Where(e => !e.IsDead).ToList();
            if (live.Count == 0) return null;
            if (live.Count == 1) return live[0];

            List<string> names = live.Select(e => $"{e.Name} (HP: {e.CurrentHP})").ToList();
            names.Add("Cancel");
            int idx = _io.RenderMenu("Select Target:", names, 0);
            if (idx == -1 || idx == names.Count - 1) return null;
            return live[idx];
        }

        private Combatant SelectAllyTarget(bool selectDead)
        {
            var targets = selectDead ? _party.ActiveParty.Where(m => m.IsDead).ToList() : _party.ActiveParty.Where(m => !m.IsDead).ToList();
            if (targets.Count == 0) { _io.WriteLine("No valid targets."); _io.Wait(500); return null; }
            List<string> names = targets.Select(e => $"{e.Name} (HP: {e.CurrentHP}/{e.MaxHP})").ToList();
            names.Add("Cancel");
            int idx = _io.RenderMenu("Select Ally:", names, 0);
            if (idx == -1 || idx == names.Count - 1) return null;
            return targets[idx];
        }

        private (bool executed, int cost) ExecuteSkillMenu(Combatant actor, List<string> skills, List<Combatant> opponents)
        {
            if (skills.Count == 0) { _io.WriteLine("No skills."); return (false, 0); }
            List<string> options = new List<string>();
            List<bool> disabled = new List<bool>();
            foreach (var s in skills)
            {
                string label = s;
                bool cannot = false;
                if (Database.Skills.TryGetValue(s, out var d))
                {
                    label = $"{s} ({d.Cost})";
                    var cost = d.ParseCost();
                    if ((cost.isHP && actor.CurrentHP <= cost.value) || (!cost.isHP && actor.CurrentSP < cost.value)) cannot = true;
                }
                options.Add(label);
                disabled.Add(cannot);
            }
            options.Add("Cancel");
            disabled.Add(false);

            int idx = _io.RenderMenu("Select Skill:", options, 0, disabled, (i) =>
            {
                if (i >= 0 && i < skills.Count && Database.Skills.TryGetValue(skills[i], out var d))
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
            });

            if (idx == -1 || idx == options.Count - 1) return (false, 0);

            string skillName = skills[idx];
            if (Database.Skills.TryGetValue(skillName, out var skillData))
            {
                List<Combatant> targets = new List<Combatant>();
                bool isAll = skillData.Effect.Contains("all", StringComparison.OrdinalIgnoreCase);
                bool isAlly = skillData.Effect.Contains("ally", StringComparison.OrdinalIgnoreCase) || skillData.Effect.Contains("party", StringComparison.OrdinalIgnoreCase);
                bool isSelf = skillData.Effect.Contains("self", StringComparison.OrdinalIgnoreCase);

                if (isAlly)
                {
                    if (isAll) targets = _party.GetAliveMembers();
                    else
                    {
                        var t = SelectAllyTarget(skillData.Effect.Contains("Revive"));
                        if (t != null) targets.Add(t); else return (false, 0);
                    }
                }
                else if (isSelf) targets.Add(actor);
                else
                {
                    if (isAll) targets = opponents.Where(e => !e.IsDead).ToList();
                    else
                    {
                        var t = SelectTarget(opponents);
                        if (t != null) targets.Add(t); else return (false, 0);
                    }
                }

                int cost = ExecuteSkillLogic(actor, skillData, targets, _playerKnowledge);
                return (true, cost);
            }
            return (false, 0);
        }

        private (bool executed, int cost) ExecuteItemMenu(Combatant actor)
        {
            var usableItems = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (usableItems.Count == 0) { _io.WriteLine("Empty."); return (false, 0); }

            List<string> options = new List<string>();
            List<bool> disabled = new List<bool>();
            foreach (var item in usableItems)
            {
                string label = $"{item.Name} x{_inv.GetQuantity(item.Id)}";
                bool isDisabled = item.Name == "Goho-M"; // Field only
                options.Add(label + (isDisabled ? " (Field Only)" : ""));
                disabled.Add(isDisabled);
            }
            options.Add("Cancel");
            disabled.Add(false);

            int idx = _io.RenderMenu("Items", options, 0, disabled, (i) => { if (i >= 0 && i < usableItems.Count) _io.WriteLine(usableItems[i].Description); });
            if (idx == -1 || idx == options.Count - 1) return (false, 0);

            ItemData selectedItem = usableItems[idx];
            if (selectedItem.Name == "Traesto Gem")
            {
                _io.WriteLine("Escaping...");
                TraestoUsed = true;
                _inv.RemoveItem(selectedItem.Id, 1);
                return (true, 0);
            }

            Combatant target = SelectAllyTarget(selectedItem.Type == "Revive");
            if (target == null) return (false, 0);

            if (PerformItem(selectedItem, target))
            {
                _inv.RemoveItem(selectedItem.Id, 1);
                return (true, 2);
            }
            return (false, 0);
        }

        private bool PerformItem(ItemData item, Combatant target)
        {
            _io.WriteLine($"\n[ITEM] Used {item.Name}!");
            bool used = false;
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    int h = item.EffectValue;
                    target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + h);
                    _io.WriteLine($"Healed {h} HP."); used = true; break;
                case "Spirit":
                    int s = item.EffectValue;
                    target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + s);
                    _io.WriteLine($"Recovered {s} SP."); used = true; break;
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

            // FIX: Safety check for -1 (Esc) input
            if (c == -1 || c == opts.Count - 1) return false;

            if (c == 0 && !_isBossBattle) return AttemptEscape(actor);
            if (c == 1 && actor.Class == ClassType.Operator) return ExecuteStrategyMenu(actor);

            return false;
        }

        private bool ExecuteStrategyMenu(Combatant actor)
        {
            var ownedDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (ownedDemons.Count == 0)
            {
                _io.WriteLine("No demons to command.");
                _io.Wait(500);
                return false;
            }

            List<string> demonOpts = ownedDemons.Select(d => $"{d.Name} [{d.BattleControl}]").ToList();
            demonOpts.Add("Back");

            int idx = _io.RenderMenu("STRATEGY", demonOpts, 0);
            if (idx == -1 || idx == demonOpts.Count - 1) return false;

            Combatant selectedDemon = ownedDemons[idx];
            selectedDemon.BattleControl = selectedDemon.BattleControl == ControlState.ActFreely ? ControlState.DirectControl : ControlState.ActFreely;

            _io.WriteLine($"{selectedDemon.Name} set to {selectedDemon.BattleControl}.");
            _io.Wait(500);
            return false;
        }

        private bool AttemptEscape(Combatant actor)
        {
            _io.WriteLine("Attempting escape..."); _io.Wait(500);
            int pAgi = actor.GetStat(StatType.AGI);
            double eAvgAgi = Math.Max(1, _enemies.Average(e => e.GetStat(StatType.AGI)));
            double chance = Math.Clamp(10.0 + 40.0 * ((double)pAgi / eAvgAgi) + (actor.GetStat(StatType.LUK) - _enemies.Average(e => e.GetStat(StatType.LUK))) * 0.5, 5.0, 95.0);
            _io.WriteLine($"Chance: {chance:F1}%"); _io.Wait(500);
            if (_rnd.Next(0, 100) < chance) { _io.WriteLine("Escaped!", ConsoleColor.Cyan); Escaped = true; return true; }
            else { _io.WriteLine("Blocked!", ConsoleColor.Red); _io.Wait(1000); return true; }
        }

        private bool ProcessTurnStart(Combatant c)
        {
            if (c.IsGuarding) { c.IsGuarding = false; _io.WriteLine($"{c.Name} drops guard."); }
            if (c.CurrentAilment == null) return true;
            _io.WriteLine($"\n{c.Name} suffers {c.CurrentAilment.Name}...", ConsoleColor.Magenta);

            if (c.CurrentAilment.ActionRestriction == "SkipTurn") { _io.WriteLine("Cannot move!"); return false; }
            if (c.CurrentAilment.ActionRestriction == "ChanceSkip" && _rnd.Next(0, 100) < 50) { _io.WriteLine("Too confused!"); return false; }
            return true;
        }

        private void ProcessTurnEnd(Combatant c)
        {
            var msgs = c.TickBuffs();
            foreach (var m in msgs) _io.WriteLine(m);

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

        private bool CheckEnemiesWiped() => _enemies.All(e => e.IsDead);
        private bool CheckPartyWiped() => _party.IsPartyWiped();

        private bool CheckBattleEndCondition()
        {
            if (CheckEnemiesWiped()) { BattleEnded = true; PlayerWon = true; return true; }
            if (CheckPartyWiped()) { BattleEnded = true; PlayerWon = false; return true; }
            return false;
        }

        private void CheckBattleEnd() => CheckBattleEndCondition();

        private void ResolveBattleEnd()
        {
            if (PlayerWon)
            {
                _io.WriteLine("\nVICTORY!", ConsoleColor.Green);
                int totalExp = 0;
                int totalMacca = 0;
                foreach (var e in _enemies)
                {
                    if (Database.Enemies.TryGetValue(e.SourceId, out var data))
                    {
                        totalExp += data.ExpYield;
                        totalMacca += data.MaccaYield;
                    }
                }

                _io.WriteLine($"Party gained {totalExp} EXP and {totalMacca} Macca.");

                foreach (var member in _party.GetAliveMembers())
                {
                    member.GainExp(totalExp);
                    if (member.ActivePersona != null) member.ActivePersona.GainExp(totalExp, _io);
                }
                _eco.AddMacca(totalMacca);
            }
            else if (!Escaped && !TraestoUsed)
            {
                _io.WriteLine("\nDEFEAT...", ConsoleColor.Red);
            }
            _io.ReadKey();
        }

        private int CalculateIconCost(CombatResult res)
        {
            if (res.Type == HitType.Weakness || res.IsCritical) return 1;
            if (res.Type == HitType.Miss || res.Type == HitType.Null || res.Type == HitType.Repel || res.Type == HitType.Absorb) return 4;
            return 2;
        }
    }
}