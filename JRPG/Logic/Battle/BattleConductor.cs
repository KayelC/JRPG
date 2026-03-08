using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle.Effects;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Bridges;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Root Orchestrator of the Battle Sub-System.
    /// Manages the high-level flow of the Press Turn battle loop.
    /// Delegates specific logic to the Math, Turn, Status, AI, and UI sub-modules.
    /// Utilizes the IBattleMessenger mediator to decouple logic from presentation.
    /// </summary>
    public class BattleConductor
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly List<Combatant> _enemies;
        private readonly InventoryManager _inv;
        private readonly EconomyManager _eco;

        // Shared Communication Mediator
        private readonly IBattleMessenger _messenger;

        // Sub-System Engines
        private readonly PressTurnEngine _turnEngine;
        private readonly StatusRegistry _statusRegistry;
        private readonly ActionProcessor _processor;
        private readonly BattleLogger _logger;
        private readonly BehaviorEngine _ai;
        private readonly InteractionBridge _ui;
        private readonly NegotiationEngine _negotiationEngine;
        private readonly BattleKnowledge _playerKnowledge;

        // Added session-specific list to prevent re-recruiting in same battle
        private readonly HashSet<string> _sessionRecruitedIds = new HashSet<string>();

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

            // 1. Initialize the Mediator (The Transmission Tower)
            _messenger = new BattleMessenger();

            // 2. Initialize Sub-Systems
            _turnEngine = new PressTurnEngine();
            _statusRegistry = new StatusRegistry();
            _statusRegistry.SetMessenger(_messenger);

            // Pass the messenger into the logic processor
            _processor = new ActionProcessor(_statusRegistry, _playerKnowledge, _messenger);

            // 3. Initialize the Observer
            _logger = new BattleLogger(_io);
            _logger.Subscribe(_messenger);

            _ai = new BehaviorEngine(_statusRegistry);
            _ui = new InteractionBridge(_io, _party, _inv, _enemies, _turnEngine, _playerKnowledge);
            _negotiationEngine = new NegotiationEngine(_io, _party, _inv, _eco);
        }

        // Entry point for the encounter. Handles initiative and the phase loop.
        public void StartBattle()
        {
            _messenger.Publish("=== ENEMY ENCOUNTER ===", ConsoleColor.White, 1200, clearScreen: true);

            foreach (var e in _enemies)
            {
                _messenger.Publish($"Appeared: {e.Name} (Lv.{e.Level})");
            }

            // 1. Initiative Roll (Weighted Agility)
            double pAvgAgi = _party.GetAliveMembers().Any() ? _party.GetAliveMembers().Average(c => c.GetStat(StatType.Ag)) : 0;
            double eAvgAgi = _enemies.Any(e => !e.IsDead) ? _enemies.Where(e => !e.IsDead).Average(c => c.GetStat(StatType.Ag)) : 0;

            bool isPlayerTurn = CombatMath.RollInitiative(pAvgAgi, eAvgAgi);

            // Event-driven initiative report
            _messenger.Publish(isPlayerTurn ? "Player Party attacks first!" : "Enemy Party attacks first!",
                isPlayerTurn ? ConsoleColor.Cyan : ConsoleColor.Red, 1000);

            // Auto-Kaja Passives on Turn 1
            if (isPlayerTurn)
            {
                var allies = _party.GetAliveMembers();
                foreach (var actor in allies)
                {
                    _statusRegistry.ProcessInitialPassives(actor, allies);
                }
            }
            else
            {
                var enemies = _enemies.Where(e => !e.IsDead).ToList();
                foreach (var actor in enemies)
                {
                    // For Enemy Passives, allies = entire enemy side
                    _statusRegistry.ProcessInitialPassives(actor, enemies);
                }
            }

            // Show HUD update for turn 1 buffs
            _ui.ForceRefreshHUD();
            _messenger.Publish(string.Empty, delay: 800);

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

            // Cleanup: Always unsubscribe when leaving battle to prevent memory leaks
            _logger.Unsubscribe(_messenger);
        }

        // Orchestrates a single side's phase (Player or Enemy).
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

                // Loop back to start if index is out of bounds
                if (actorIndex >= currentLiveActors.Count) actorIndex = 0;
                Combatant actor = currentLiveActors[actorIndex];

                // --- 1. TURN START (Ailments & Restrictions) ---
                TurnStartResult turnState = _statusRegistry.ProcessTurnStart(actor);
                bool actorRemoved = false; // Prevent index shifting bug

                if (turnState == TurnStartResult.Skip)
                {
                    _messenger.Publish($"{actor.Name} is unable to move!", ConsoleColor.Magenta, 800);
                    _turnEngine.ConsumeAction(HitType.Normal, false); // Losing turn skips 1 icon
                }
                else if (turnState == TurnStartResult.FleeBattle)
                {
                    _messenger.Publish($"{actor.Name} fled in fear!", ConsoleColor.Red, 1000);
                    Escaped = true;
                    BattleEnded = true;
                    return;
                }
                else if (turnState == TurnStartResult.ReturnToCOMP)
                {
                    // Differentiate between Player Demon and Enemy Demon fleeing
                    if (isPlayerSide)
                    {
                        _messenger.Publish($"{actor.Name} returned to COMP in terror!", ConsoleColor.Red, 400);
                        _party.ReturnDemon(actor, actor); // Self-return logic
                    }
                    else
                    {
                        _messenger.Publish($"{actor.Name} has fled!", ConsoleColor.Yellow, 400);
                        _enemies.Remove(actor);
                        actorRemoved = true;
                    }
                    _turnEngine.ConsumeAction(HitType.Normal, false);
                }
                else
                {
                    // Actor is able to perform an action
                    ExecuteAction(actor, isPlayerSide, turnState);
                }

                // Real-time HUD refresh after every action or skip
                _ui.ForceRefreshHUD();

                // --- 2. TURN END (Recovery & Decay) ---
                // StatusRegistry now handles its own publishing directly to the messenger.
                _statusRegistry.ProcessTurnEnd(actor);

                // Handle demons dying and returning to stock
                foreach (var p in _party.ActiveParty.ToList())
                {
                    if (p.IsDead && p.Class == ClassType.Demon)
                    {
                        _messenger.Publish($"{p.Name} faded away and returned to stock...");
                        // Use actor as owner assuming player owns all party demons in current build
                        _party.ReturnDemon(actor, p);
                    }
                }

                // Check for protagonist death immediately after action
                if (CheckEncounterCompletion()) return;

                if (!actorRemoved) actorIndex++;
            }

            // At phase end, dissolve any unused Karn shields
            var sideToEnd = isPlayerSide ? _party.ActiveParty : _enemies;
            foreach (var combatant in sideToEnd) combatant.DissolveShields();
        }

        /// <summary>
        /// Orchestrates the selection and execution of a specific action.
        /// Handles the fork between Manual Control, AI, and Forced Ailment actions.
        /// </summary>
        private void ExecuteAction(Combatant actor, bool isPlayerSide, TurnStartResult turnState)
        {
            SkillData? skill = null;
            ItemData? item = null;
            List<Combatant>? targets = null;
            bool actionCommitted = false;

            // --- A. ACTION SELECTION LOOP ---
            while (!actionCommitted && !BattleEnded)
            {
                // Reset temporary selection state
                skill = null;
                item = null;
                targets = null;

                // 1. Forced Behaviors (Ailments)
                if (turnState == TurnStartResult.ForcedPhysical || turnState == TurnStartResult.ForcedConfusion)
                {
                    var forced = _ai.DetermineBestAction(actor, _party.ActiveParty, _enemies, _playerKnowledge, _turnEngine.FullIcons, _turnEngine.BlinkingIcons);
                    skill = forced.skill;
                    targets = forced.targets;
                    actionCommitted = true;
                }
                // 2. Manual Control
                else if (isPlayerSide && (actor.Controller == ControllerType.LocalPlayer || actor.BattleControl == ControlState.DirectControl))
                {
                    string choice = _ui.ShowMainMenu(actor);
                    if (choice == "Cancel") continue; // Re-render main menu

                    if (choice == "Attack")
                    {
                        targets = _ui.SelectTarget(actor);
                        if (targets == null) continue; // Back to Menu
                        actionCommitted = true;
                    }
                    else if (choice == "Guard")
                    {
                        actor.IsGuarding = true;
                        _messenger.Publish($"{actor.Name} is guarding.");
                        _turnEngine.ConsumeAction(HitType.Normal, false);
                        actionCommitted = true;
                        return; // Turn finished
                    }
                    else if (choice == "Persona" || choice == "Skill" || choice == "Command")
                    {
                        skill = _ui.SelectSkill(actor, "");
                        if (skill == null) continue; // Back to Menu

                        targets = _ui.SelectTarget(actor, skill);
                        if (targets == null) continue; // Back to Menu
                        actionCommitted = true;
                    }
                    else if (choice == "COMP")
                    {
                        var comp = _ui.OpenCOMPMenu(actor);
                        if (comp.action == "None") continue; // Back to Menu

                        if (comp.action == "Summon")
                        {
                            // ATOMIC TRANSACTION: PartyManager handles stock and party state
                            if (comp.target != null && _party.SummonDemon(actor, comp.target))
                            {
                                _messenger.Publish($"{actor.Name} summoned {comp.target.Name}!");
                                _turnEngine.ConsumeAction(HitType.Normal, false);
                                actionCommitted = true;
                                return;
                            }
                        }
                        else if (comp.action == "Return")
                        {
                            // ATOMIC TRANSACTION: PartyManager handles stock and party state
                            if (comp.target != null && _party.ReturnDemon(actor, comp.target))
                            {
                                _messenger.Publish($"{actor.Name} returned {comp.target.Name} to stock.");
                                _turnEngine.ConsumeAction(HitType.Normal, false);
                                actionCommitted = true;
                                return;
                            }
                        }
                        else if (comp.action == "Analyze")
                        {
                            if (comp.target != null) _processor.ExecuteAnalyze(comp.target);
                            _turnEngine.ConsumeAction(HitType.Normal, false);
                            actionCommitted = true;
                            return;
                        }
                    }
                    else if (choice == "Pass")
                    {
                        _turnEngine.Pass();
                        _messenger.Publish($"{actor.Name} passes.");
                        actionCommitted = true;
                        return;
                    }
                    else if (choice == "Item")
                    {
                        item = _ui.SelectItem(actor);
                        if (item == null) continue; // Back to Menu

                        // Traesto Gem should not prompt for targets
                        if (item.Name == "Traesto Gem")
                        {
                            actionCommitted = true;
                        }
                        else
                        {
                            targets = _ui.SelectTarget(actor, null, item);
                            if (targets == null) continue; // Back to Menu
                            actionCommitted = true;
                        }
                    }
                    else if (choice == "Talk")
                    {
                        targets = _ui.SelectTarget(actor, null, null, true);
                        if (targets != null && targets.Count > 0) HandleNegotiation(actor, targets[0]);
                        return;
                    }
                    else if (choice == "Tactics")
                    {
                        HandleTactics(actor);
                        if (BattleEnded) { actionCommitted = true; return; }
                        continue;
                    }
                }
                // 3. Heuristic AI
                else
                {
                    var sideKnowledge = isPlayerSide ? _playerKnowledge : new BattleKnowledge();
                    // Passing current turn engine state to AI
                    var decision = _ai.DetermineBestAction(actor,
                        isPlayerSide ? _party.ActiveParty : _enemies,
                        isPlayerSide ? _enemies : _party.ActiveParty,
                        sideKnowledge,
                        _turnEngine.FullIcons,
                        _turnEngine.BlinkingIcons);

                    skill = decision.skill;
                    targets = decision.targets;
                    actionCommitted = true;
                }
            }

            // --- B. EXECUTION ---
            if (actionCommitted && !BattleEnded)
            {
                // Handle Pass (represented by AI returning null skill and empty targets)
                if (targets != null && targets.Count == 0 && skill == null)
                {
                    _turnEngine.Pass();
                    _messenger.Publish($"{actor.Name} passes.");
                    return;
                }

                if (item != null && targets != null)
                {
                    // Defensive check: Only consume and use icon if the item actually worked
                    if (_processor.ExecuteItem(actor, targets, item))
                    {
                        _inv.RemoveItem(item.Id, 1);

                        if (item.Name == "Traesto Gem")
                        {
                            Escaped = true;
                            BattleEnded = true;
                            return;
                        }

                        _turnEngine.ConsumeAction(HitType.Normal, false);
                    }
                    else
                    {
                        // Reprompt if the item had no effect
                        ExecuteAction(actor, isPlayerSide, turnState);
                }
                }
                else if (skill == null && targets != null && targets.Count > 0)
                {
                    var res = _processor.ExecuteAttack(actor, targets[0]);
                    _turnEngine.ConsumeAction(res.Type, res.IsCritical);
                }
                else if (skill != null && targets != null)
                {
                    var results = _processor.ExecuteSkill(actor, targets, skill);
                    if (results.Any())
                    {
                        HitType worst = results.Max(r => r.Type);
                        _turnEngine.ConsumeAction(worst, results.Any(r => r.IsCritical));
                    }
                    else _turnEngine.ConsumeAction(HitType.Normal, false);
                }

                _messenger.Publish(string.Empty, delay: 1000);
            }
        }

        private void HandleTactics(Combatant actor)
        {
            string tactic = _ui.GetTacticsChoice(_isBossBattle, actor.Class == ClassType.Operator);
            if (tactic == "Escape")
            {
                int pAgi = actor.GetStat(StatType.Ag);
                double eAvgAgi = _enemies.Any() ? _enemies.Average(e => e.GetStat(StatType.Ag)) : 1;

                if (new Random().Next(0, 100) < Math.Clamp(10.0 + 40.0 * (pAgi / eAvgAgi), 5.0, 95.0))
                {
                    Escaped = true;
                    BattleEnded = true;
                    _messenger.Publish("Escaped safely!", ConsoleColor.Cyan, 1000);
                }
                else
                {
                    _messenger.Publish("Failed to escape!", ConsoleColor.Yellow, 1000);
                    _turnEngine.ConsumeAction(HitType.Normal, false);
                }
            }
            else if (tactic == "Strategy")
            {
                var stratTarget = _ui.SelectStrategyTarget();
                if (stratTarget != null)
                {
                    stratTarget.BattleControl = (stratTarget.BattleControl == ControlState.ActFreely) ? ControlState.DirectControl : ControlState.ActFreely;
                    _messenger.Publish($"{stratTarget.Name} is now set to {stratTarget.BattleControl}.", ConsoleColor.Gray, 800);
                }
            }
        }

        private void HandleNegotiation(Combatant actor, Combatant target)
        {
            // Check session-recruited list before starting
            if (_sessionRecruitedIds.Contains(target.SourceId))
            {
                // We treat this as a "Familiar" encounter but simplified
                _messenger.Publish($"{target.Name} has already been spoken to.", ConsoleColor.Gray, 800);
                return; // Does not consume a turn
            }

            NegotiationResult result = _negotiationEngine.StartNegotiation(actor, target, _enemies);
            switch (result)
            {
                case NegotiationResult.Success:
                    _messenger.Publish($"{target.Name} joined your party!", ConsoleColor.Green);
                    // Use the Factory to create the demon to ensure correct stats
                    // We can use the target.SourceId directly as CreateEnemy handles ID resolution
                    var newDemon = Combatant.CreateEnemy(target.SourceId);

                    // Add to player's stock
                    actor.DemonStock.Add(newDemon);
                    _sessionRecruitedIds.Add(target.SourceId); // Track for this battle

                    _enemies.Remove(target);
                    _turnEngine.ConsumeAction(HitType.Normal, false);
                    break;

                case NegotiationResult.Failure:
                    _messenger.Publish("Negotiation failed! Your turn ends.", ConsoleColor.Red);
                    _turnEngine.TerminatePhase();
                    break;

                case NegotiationResult.Trick:
                case NegotiationResult.Flee:
                case NegotiationResult.FamiliarFlee:
                    _messenger.Publish($"{target.Name} left the battle.");
                    _enemies.Remove(target);
                    _turnEngine.ConsumeAction(HitType.Miss, false);
                    break;
            }
        }

        /// <summary>
        /// If the local player (protagonist) dies, 
        /// the battle ends immediately in defeat.
        /// </summary>
        private bool CheckEncounterCompletion()
        {
            // 1. High Priority: Protagonist Check
            if (_party.ActiveParty.Any(p => p.Controller == ControllerType.LocalPlayer && p.IsDead))
            {
                PlayerWon = false;
                BattleEnded = true;
                return true;
            }

            // 2. Enemy Side Check
            if (_enemies.All(e => e.IsDead)) { PlayerWon = true; BattleEnded = true; return true; }

            // 3. Full Party Wipe Check
            if (_party.IsPartyWiped()) { PlayerWon = false; BattleEnded = true; return true; }
            return false;
        }

        private void ResolveBattleEnd()
        {
            if (PlayerWon)
            {
                _messenger.Publish("\nVICTORY!", ConsoleColor.Green, 500);

                // Use CombatMath for dynamic reward calculation
                int totalExp = _enemies.Sum(e => CombatMath.CalculateExpYield(e));
                int totalMacca = _enemies.Sum(e => CombatMath.CalculateMaccaYield(e));

                _messenger.Publish($"Gained {totalExp} EXP and {totalMacca} Macca.", ConsoleColor.Gray, 800);

                foreach (var m in _party.GetAliveMembers())
                {
                    m.GainExp(totalExp);
                    if (m.ActivePersona != null) m.ActivePersona.GainExp(totalExp, _io);
                }
                _eco.AddMacca(totalMacca);
            }
            else if (!Escaped && !TraestoUsed)
            {
                _messenger.Publish("\nDEFEAT...", ConsoleColor.Red, 1000);
            }

            foreach (var member in _party.ActiveParty.Concat(_party.ReserveMembers))
            {
                member.CleanupBattleState();
            }

            _messenger.Publish("Press any key to exit battle...", ConsoleColor.Gray, waitForInput: true);
        }
    }
}