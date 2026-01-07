using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    public class BattleCore
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly List<Combatant> _enemies;
        private readonly InventoryManager _inv;
        private readonly EconomyManager _eco;
        private readonly Random _rnd = new Random();

        // Core battle systems
        private readonly TurnSystem _turns;
        private readonly StatusSystem _status;
        private readonly ActionExecutor _executor;
        private readonly AILogic _ai;
        private readonly MenuHandler _menus;

        // Knowledge tracking for both sides
        private readonly BattleKnowledge _playerKnowledge = new BattleKnowledge();
        private readonly BattleKnowledge _enemyKnowledge = new BattleKnowledge();

        private readonly bool _isBossBattle;

        public bool BattleEnded { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool TraestoUsed { get; private set; }
        public bool Escaped { get; private set; }

        public BattleCore(PartyManager party, List<Combatant> enemies, InventoryManager inv, EconomyManager eco, IGameIO io, bool isBoss = false)
        {
            _party = party;
            _enemies = enemies;
            _inv = inv;
            _eco = eco;
            _io = io;
            _isBossBattle = isBoss;

            _turns = new TurnSystem();
            _status = new StatusSystem();
            _executor = new ActionExecutor(_status);
            _ai = new AILogic();
            _menus = new MenuHandler(io, inv, party);
        }

        public void StartBattle()
        {
            _io.Clear();
            _io.WriteLine("=== BATTLE START ===", ConsoleColor.Yellow);
            foreach (var e in _enemies)
            {
                _io.WriteLine($"Detected Enemy: {e.Name} (Level {e.Level})");
            }
            _io.Wait(1200);

            double pAvgAgi = _party.ActiveParty.Any() ? _party.ActiveParty.Average(c => c.GetStat(StatType.AGI)) : 0;
            double eAvgAgi = _enemies.Any() ? _enemies.Average(c => c.GetStat(StatType.AGI)) : 0;

            bool isPlayerTurn = (pAvgAgi * (0.9 + _rnd.NextDouble() * 0.2)) >= (eAvgAgi * (0.9 + _rnd.NextDouble() * 0.2));

            _io.WriteLine(isPlayerTurn ? "Player Party attacks first!" : "Enemy Party attacks first!", isPlayerTurn ? ConsoleColor.Cyan : ConsoleColor.Red);
            _io.Wait(1000);

            while (!BattleEnded)
            {
                if (TraestoUsed || Escaped) break;

                ExecuteSidePhase(isPlayerTurn);

                if (CheckEncounterCompletion()) break;

                isPlayerTurn = !isPlayerTurn;
            }

            PerformBattleCleanup();
        }

        private void ExecuteSidePhase(bool isPlayerSide)
        {
            var activeCombatants = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
            if (activeCombatants.Count == 0) return;

            _turns.StartPhase(activeCombatants.Count);
            int currentActorIndex = 0;

            while (!_turns.IsPhaseOver() && !BattleEnded)
            {
                if (TraestoUsed || Escaped) break;

                var liveCombatants = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
                if (liveCombatants.Count == 0) break;

                if (currentActorIndex >= liveCombatants.Count) currentActorIndex = 0;
                Combatant currentActor = liveCombatants[currentActorIndex];

                string uiContext = GetUIContext(isPlayerSide);

                // --- Turn Start Ailment Logic ---
                if (currentActor.CurrentAilment != null)
                {
                    _io.Clear();
                    _io.WriteLine(uiContext);
                    _io.WriteLine($"{currentActor.Name} is suffering from {currentActor.CurrentAilment.Name}...", ConsoleColor.Magenta);

                    if (currentActor.CurrentAilment.ActionRestriction == "SkipTurn")
                    {
                        _io.WriteLine($"{currentActor.Name} cannot move!");
                        _io.Wait(1000);
                        _turns.HandleActionResults(HitType.Normal, false);
                        _status.ProcessTurnEnd(currentActor);
                        currentActorIndex++;
                        continue;
                    }

                    if (currentActor.CurrentAilment.ActionRestriction == "ChanceSkip")
                    {
                        if (_rnd.Next(100) < 50)
                        {
                            _io.WriteLine($"{currentActor.Name} is too confused to act!");
                            _io.Wait(1000);
                            _turns.HandleActionResults(HitType.Normal, false);
                            _status.ProcessTurnEnd(currentActor);
                            currentActorIndex++;
                            continue;
                        }
                    }
                }

                bool actionTaken = false;

                // BUG FIX: Added check for DirectControl state to allow manual demon management
                bool isManuallyControlled = (currentActor.Controller == ControllerType.LocalPlayer) ||
                                            (currentActor.BattleControl == ControlState.DirectControl);

                if (isPlayerSide && isManuallyControlled)
                {
                    actionTaken = HandlePlayerTurn(currentActor, uiContext);
                }
                else
                {
                    _io.Clear();
                    _io.WriteLine(uiContext);
                    _io.WriteLine($"{currentActor.Name} is acting...");
                    _io.Wait(800);
                    actionTaken = HandleAITurn(currentActor, isPlayerSide, uiContext);
                }

                if (actionTaken)
                {
                    _io.Wait(1000);
                    var turnLogs = _status.ProcessTurnEnd(currentActor);
                    foreach (var log in turnLogs) _io.WriteLine(log, ConsoleColor.Gray);

                    currentActor.IsGuarding = false;
                    if (CheckEncounterCompletion()) return;
                    currentActorIndex++;
                }
            }
        }

        private string GetUIContext(bool isPlayerSide)
        {
            string phaseHeader = $"--- {(isPlayerSide ? "PLAYER" : "ENEMY")} PHASE ---\n";
            string iconLine = $"Turns: {_turns.GetIconsDisplay()}\n";
            string separator = "==================================================\n";

            string enemiesPart = "ENEMIES:\n";
            foreach (var e in _enemies)
            {
                string status = e.IsDead ? "[DEAD]" : $"HP: {e.CurrentHP}";
                enemiesPart += $"  {e.Name,-15} {status}\n";
            }

            string partyPart = "--------------------------------------------------\nPARTY:\n";
            foreach (var p in _party.ActiveParty)
            {
                string guard = p.IsGuarding ? " (G)" : "";
                string ailment = p.CurrentAilment != null ? $" [{p.CurrentAilment.Name}]" : "";
                partyPart += $"  {p.Name,-15} HP: {p.CurrentHP,4}/{p.MaxHP,4} SP: {p.CurrentSP,4}/{p.MaxSP,4}{guard}{ailment}\n";
            }

            return phaseHeader + iconLine + separator + enemiesPart + partyPart + separator;
        }

        private bool HandlePlayerTurn(Combatant actor, string uiContext)
        {
            string menuChoice = _menus.GetActionChoice(actor, uiContext);
            if (menuChoice == "Cancel") return false;

            switch (menuChoice)
            {
                case "Attack":
                    var targets = _menus.AcquireTargets(actor, null, _enemies, uiContext);
                    if (targets == null) return false;
                    var target = targets.FirstOrDefault();
                    var res = _executor.ExecuteBasicAttack(actor, target, _playerKnowledge);
                    _io.WriteLine($"{actor.Name} attacks {target.Name}! {res.Message}");
                    _turns.HandleActionResults(res.Type, res.IsCritical);
                    return true;

                case "Guard":
                    actor.IsGuarding = true;
                    _io.WriteLine($"{actor.Name} is guarding.");
                    _turns.HandleActionResults(HitType.Normal, false);
                    return true;

                case "Persona":
                case "Skill":
                case "Command":
                    var skill = _menus.SelectSkill(actor, uiContext);
                    if (skill == null) return false;
                    var skillTargets = _menus.AcquireTargets(actor, skill, _enemies, uiContext);
                    if (skillTargets == null) return false;

                    var outcome = _executor.ExecuteSkill(actor, skillTargets, skill, _playerKnowledge);
                    _io.WriteLine($"{actor.Name} used {skill.Name}!");
                    _turns.HandleActionResults(outcome.worstHit, outcome.advantageTriggered);
                    return true;

                case "COMP":
                    var compData = _menus.GetCompAction(actor, uiContext);
                    if (compData.action == "Summon")
                    {
                        if (_party.SummonDemon(compData.demon))
                        {
                            _io.WriteLine($"{actor.Name} summoned {compData.demon.Name}!");
                            _turns.HandleActionResults(HitType.Normal, false);
                            return true;
                        }
                    }
                    else if (compData.action == "Return")
                    {
                        if (_party.ReturnDemon(compData.demon))
                        {
                            _io.WriteLine($"{actor.Name} returned {compData.demon.Name} to stock.");
                            _turns.HandleActionResults(HitType.Normal, false);
                            return true;
                        }
                    }
                    return false;

                case "Tactics":
                    string tactic = _menus.GetTacticsChoice(uiContext, _isBossBattle, actor.Class == ClassType.Operator);
                    if (tactic == "Back") return false;

                    if (tactic == "Escape")
                    {
                        if (AttemptEscape(actor))
                        {
                            Escaped = true;
                            BattleEnded = true;
                            return true;
                        }
                        _io.WriteLine("Failed to escape!");
                        _io.Wait(800);
                        _turns.HandleActionResults(HitType.Normal, false);
                        return true;
                    }

                    if (tactic == "Strategy")
                    {
                        var stratTarget = _menus.SelectStrategyTarget(uiContext, _party.ActiveParty);
                        if (stratTarget != null)
                        {
                            stratTarget.BattleControl = (stratTarget.BattleControl == ControlState.ActFreely)
                                ? ControlState.DirectControl : ControlState.ActFreely;
                            _io.WriteLine($"{stratTarget.Name} is now set to {stratTarget.BattleControl}.");
                            _io.Wait(800);
                        }
                        return false;
                    }
                    return false;

                case "Pass":
                    _turns.Pass();
                    _io.WriteLine($"{actor.Name} passed.");
                    return true;

                case "Item":
                    var item = _menus.SelectItem(uiContext);
                    if (item == null) return false;
                    _io.WriteLine($"{actor.Name} used {item.Name}.");
                    _turns.HandleActionResults(HitType.Normal, false);
                    return true;
            }

            return false;
        }

        private bool HandleAITurn(Combatant actor, bool isPlayerSide, string uiContext)
        {
            var allies = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
            var foes = isPlayerSide ? _enemies.Where(e => !e.IsDead).ToList() : _party.GetAliveMembers();
            var knowledge = isPlayerSide ? _playerKnowledge : _enemyKnowledge;

            var choice = _ai.DetermineAction(actor, allies, foes, knowledge);

            if (choice.skill == null)
            {
                var res = _executor.ExecuteBasicAttack(actor, choice.targets[0], knowledge);
                _io.WriteLine($"{actor.Name} attacks! {res.Message}");
                _turns.HandleActionResults(res.Type, res.IsCritical);
            }
            else
            {
                _io.WriteLine($"{actor.Name} casts {choice.skill.Name}!");
                var outcome = _executor.ExecuteSkill(actor, choice.targets, choice.skill, knowledge);
                _turns.HandleActionResults(outcome.worstHit, outcome.advantageTriggered);
            }

            return true;
        }

        private bool AttemptEscape(Combatant actor)
        {
            _io.WriteLine("Attempting escape...");
            int pAgi = actor.GetStat(StatType.AGI);
            double eAvgAgi = _enemies.Any() ? _enemies.Average(e => e.GetStat(StatType.AGI)) : 1;
            double chance = Math.Clamp(10.0 + 40.0 * (pAgi / eAvgAgi), 5.0, 95.0);
            return _rnd.Next(100) < chance;
        }

        private bool CheckEncounterCompletion()
        {
            if (_enemies.All(e => e.IsDead)) { PlayerWon = true; BattleEnded = true; return true; }
            if (_party.IsPartyWiped()) { PlayerWon = false; BattleEnded = true; return true; }
            return false;
        }

        private void PerformBattleCleanup()
        {
            if (PlayerWon)
            {
                _io.WriteLine("\nVICTORY!", ConsoleColor.Green);
                int totalExp = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var data) ? data.ExpYield : 0);
                int totalMacca = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var data) ? data.MaccaYield : 0);
                _io.WriteLine($"Gained {totalExp} EXP and {totalMacca} Macca.");
                foreach (var m in _party.GetAliveMembers())
                {
                    m.GainExp(totalExp);
                    if (m.ActivePersona != null) m.ActivePersona.GainExp(totalExp, _io);
                }
                _eco.AddMacca(totalMacca);
            }
            else if (!Escaped && !TraestoUsed)
            {
                _io.WriteLine("\nDEFEAT...", ConsoleColor.Red);
            }

            foreach (var member in _party.ActiveParty)
            {
                member.IsGuarding = false;
                member.Buffs.Clear();
            }

            _io.ReadKey();
        }
    }
}