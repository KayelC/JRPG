using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Root Orchestrator of the Battle Sub-System.
    /// Manages the high-level flow of the SMT III Press Turn battle loop.
    /// Delegates specific logic to the Math, Turn, Status, AI, and UI sub-modules.
    /// </summary>
    public class BattleConductor
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly List<Combatant> _enemies;
        private readonly InventoryManager _inv;
        private readonly EconomyManager _eco;

        // Sub-System Engines
        private readonly PressTurnEngine _turnEngine;
        private readonly StatusRegistry _statusRegistry;
        private readonly ActionProcessor _processor;
        private readonly BehaviorEngine _ai;
        private readonly InteractionBridge _ui;
        private readonly BattleKnowledge _playerKnowledge;

        private readonly bool _isBossBattle;

        // Battle State Flags
        public bool BattleEnded { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool Escaped { get; private set; }
        public bool TraestoUsed { get; private set; }

        public BattleConductor(
            PartyManager party,
            List<Combatant> enemies,
            InventoryManager inv,
            EconomyManager eco,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            bool isBoss = false)
        {
            _io = io;
            _party = party;
            _enemies = enemies;
            _inv = inv;
            _eco = eco;
            _playerKnowledge = playerKnowledge;
            _isBossBattle = isBoss;

            // Initialize Sub-Systems
            _turnEngine = new PressTurnEngine();
            _statusRegistry = new StatusRegistry();
            _processor = new ActionProcessor(_io, _statusRegistry, _playerKnowledge);
            _ai = new BehaviorEngine(_statusRegistry);
            _ui = new InteractionBridge(_io, _party, _inv, _enemies, _turnEngine, _playerKnowledge);
        }

        /// <summary>
        /// Entry point for the encounter. Handles initiative and the phase loop.
        /// </summary>
        public void StartBattle()
        {
            _io.Clear();
            _io.WriteLine("=== ENEMY ENCOUNTER ===", ConsoleColor.Yellow);
            foreach (var e in _enemies)
            {
                _io.WriteLine($"Appeared: {e.Name} (Lv.{e.Level})");
            }
            _io.Wait(1200);

            // 1. Initiative Roll (Weighted Agility)
            double pAvgAgi = _party.ActiveParty.Average(c => c.GetStat(StatType.AGI));
            double eAvgAgi = _enemies.Average(c => c.GetStat(StatType.AGI));
            bool isPlayerTurn = CombatMath.RollInitiative(pAvgAgi, eAvgAgi);

            _io.WriteLine(isPlayerTurn ? "Player Party attacks first!" : "Enemy Party attacks first!",
                isPlayerTurn ? ConsoleColor.Cyan : ConsoleColor.Red);
            _io.Wait(1000);

            // 2. Main Phase Loop
            while (!BattleEnded)
            {
                ExecutePhase(isPlayerTurn);

                if (CheckEncounterCompletion()) break;

                // Flip the turn for the next cycle
                isPlayerTurn = !isPlayerTurn;
            }

            // 3. Post-Battle Resolution
            ResolveBattleEnd();
        }

        /// <summary>
        /// Orchestrates a single side's phase (Player or Enemy).
        /// </summary>
        private void ExecutePhase(bool isPlayerSide)
        {
            var activeSide = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
            if (activeSide.Count == 0) return;

            // Initialize Icons for the phase
            _turnEngine.StartPhase(activeSide.Count);
            int actorIndex = 0;

            while (_turnEngine.HasTurnsRemaining() && !BattleEnded)
            {
                // Refresh live members list (Live-Reactive Iteration)
                var currentLiveActors = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
                if (currentLiveActors.Count == 0) break;

                // Loop back to start if index is out of bounds (standard SMT rotation)
                if (actorIndex >= currentLiveActors.Count) actorIndex = 0;
                Combatant actor = currentLiveActors[actorIndex];

                // --- 1. TURN START (Ailments & Restrictions) ---
                TurnStartResult turnState = _statusRegistry.ProcessTurnStart(actor);

                if (turnState == TurnStartResult.Skip)
                {
                    _io.WriteLine($"{actor.Name} is unable to move!", ConsoleColor.Magenta);
                    _turnEngine.ConsumeAction(HitType.Normal, false); // Losing turn skips 1 icon
                    _io.Wait(800);
                }
                else if (turnState == TurnStartResult.FleeBattle)
                {
                    _io.WriteLine($"{actor.Name} fled in fear!", ConsoleColor.Red);
                    Escaped = true;
                    BattleEnded = true;
                    return;
                }
                else if (turnState == TurnStartResult.ReturnToCOMP)
                {
                    _io.WriteLine($"{actor.Name} returned to COMP in terror!", ConsoleColor.Red);
                    _party.ReturnDemon(actor);
                    _turnEngine.ConsumeAction(HitType.Normal, false);
                }
                else
                {
                    // Actor is able to perform an action
                    ExecuteAction(actor, isPlayerSide, turnState);
                }

                // --- 2. TURN END (Recovery & Decay) ---
                var endLogs = _statusRegistry.ProcessTurnEnd(actor);
                foreach (var log in endLogs) _io.WriteLine(log, ConsoleColor.Gray);

                if (CheckEncounterCompletion()) return;

                actorIndex++;
            }
        }

        /// <summary>
        /// Orchestrates the selection and execution of a specific action.
        /// Handles the fork between Manual Control, AI, and Forced Ailment actions.
        /// </summary>
        private void ExecuteAction(Combatant actor, bool isPlayerSide, TurnStartResult turnState)
        {
            SkillData skill = null;
            List<Combatant> targets = null;

            // A. Forced Behaviors (Ailments)
            if (turnState == TurnStartResult.ForcedPhysical || turnState == TurnStartResult.ForcedConfusion)
            {
                var forced = _ai.DetermineBestAction(actor, _party.ActiveParty, _enemies, _playerKnowledge);
                skill = forced.skill;
                targets = forced.targets;
            }
            // B. Manual Control
            else if (isPlayerSide && (actor.Controller == ControllerType.LocalPlayer || actor.BattleControl == ControlState.DirectControl))
            {
                string choice = _ui.ShowMainMenu(actor);

                if (choice == "Attack")
                {
                    targets = _ui.SelectTarget(actor);
                    if (targets == null) { ExecuteAction(actor, isPlayerSide, turnState); return; }
                }
                else if (choice == "Guard")
                {
                    actor.IsGuarding = true;
                    _io.WriteLine($"{actor.Name} is guarding.");
                    _turnEngine.ConsumeAction(HitType.Normal, false);
                    return;
                }
                else if (choice == "Persona" || choice == "Skill" || choice == "Command")
                {
                    skill = _ui.SelectSkill(actor, ""); // Bridge handles context
                    if (skill == null) { ExecuteAction(actor, isPlayerSide, turnState); return; }
                    targets = _ui.SelectTarget(actor, skill);
                    if (targets == null) { ExecuteAction(actor, isPlayerSide, turnState); return; }
                }
                else if (choice == "COMP")
                {
                    var comp = _ui.OpenCOMPMenu(actor);
                    if (comp.action == "None") { ExecuteAction(actor, isPlayerSide, turnState); return; }

                    if (comp.action == "Summon")
                    {
                        if (_party.SummonDemon(comp.target))
                        {
                            _io.WriteLine($"{actor.Name} summoned {comp.target.Name}!");
                            _turnEngine.ConsumeAction(HitType.Normal, false);
                        }
                        return;
                    }
                    if (comp.action == "Return")
                    {
                        if (_party.ReturnDemon(comp.target))
                        {
                            _io.WriteLine($"{actor.Name} returned {comp.target.Name} to stock.");
                            _turnEngine.ConsumeAction(HitType.Normal, false);
                        }
                        return;
                    }
                    if (comp.action == "Analyze")
                    {
                        _processor.ExecuteAnalyze(comp.target);
                        _turnEngine.ConsumeAction(HitType.Normal, false);
                        return;
                    }
                }
                else if (choice == "Pass")
                {
                    _turnEngine.Pass();
                    _io.WriteLine($"{actor.Name} passes.");
                    return;
                }
                else if (choice == "Item")
                {
                    var item = _ui.SelectItem(actor);
                    if (item == null) { ExecuteAction(actor, isPlayerSide, turnState); return; }
                    targets = _ui.SelectTarget(actor, null, item);
                    if (targets == null) { ExecuteAction(actor, isPlayerSide, turnState); return; }
                }
            }
            // C. Heuristic AI
            else
            {
                var sideKnowledge = isPlayerSide ? _playerKnowledge : new BattleKnowledge(); // Enemies don't cheat
                var decision = _ai.DetermineBestAction(actor,
                    isPlayerSide ? _party.ActiveParty : _enemies,
                    isPlayerSide ? _enemies : _party.ActiveParty,
                    sideKnowledge);

                skill = decision.skill;
                targets = decision.targets;
            }

            // --- EXECUTION ---
            if (targets != null && targets.Any())
            {
                if (skill == null) // Basic Attack
                {
                    var res = _processor.ExecuteAttack(actor, targets[0]);
                    _turnEngine.ConsumeAction(res.Type, res.IsCritical);
                }
                else // Skill Use
                {
                    var results = _processor.ExecuteSkill(actor, targets, skill);
                    // SMT III: We evaluate the "Worst" hit from a multi-target pool to determine icon cost
                    HitType worst = results.Max(r => r.Type);
                    bool anyCrit = results.Any(r => r.IsCritical);
                    _turnEngine.ConsumeAction(worst, anyCrit);
                }
                _io.Wait(1000);
            }
        }

        private bool CheckEncounterCompletion()
        {
            if (_enemies.All(e => e.IsDead)) { PlayerWon = true; BattleEnded = true; return true; }
            if (_party.IsPartyWiped()) { PlayerWon = false; BattleEnded = true; return true; }
            return false;
        }

        private void ResolveBattleEnd()
        {
            if (PlayerWon)
            {
                _io.WriteLine("\nVICTORY!", ConsoleColor.Green);
                int totalExp = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var d) ? d.ExpYield : 0);
                int totalMacca = _enemies.Sum(e => Database.Enemies.TryGetValue(e.SourceId, out var d) ? d.MaccaYield : 0);

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

            // Reset transient battle states
            foreach (var member in _party.ActiveParty)
            {
                member.IsGuarding = false;
                member.Buffs.Clear();
            }

            _io.WriteLine("Press any key to exit battle...");
            _io.ReadKey();
        }
    }
}