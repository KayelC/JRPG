using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    public class InteractionBridge
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly InventoryManager _inv;
        private readonly List<Combatant> _enemies;
        private readonly PressTurnEngine _turnEngine;
        private readonly BattleKnowledge _knowledge;

        private static int _mainMenuIndex = 0;
        private static int _skillMenuIndex = 0;
        private static int _itemMenuIndex = 0;

        public InteractionBridge(IGameIO io, PartyManager party, InventoryManager inventory, List<Combatant> enemies, PressTurnEngine turnEngine, BattleKnowledge knowledge)
        {
            _io = io;
            _party = party;
            _inv = inventory;
            _enemies = enemies;
            _turnEngine = turnEngine;
            _knowledge = knowledge;
        }

        public string ShowMainMenu(Combatant actor)
        {
            string context = GetBattleContext(actor);
            List<string> options = new List<string> { "Attack", "Guard" };

            if (actor.Class == ClassType.PersonaUser || actor.Class == ClassType.WildCard) options.Add("Persona");
            else if (actor.Class == ClassType.Operator)
            {
                options.Add("Command");
                options.Add("COMP");
            }
            else options.Add("Skill");

            options.Add("Item");
            options.Add("Tactics");
            options.Add("Pass");

            bool isPanicked = actor.CurrentAilment != null && actor.CurrentAilment.Name == "Panic";
            List<bool> disabledStates = new List<bool>();

            foreach (var opt in options)
            {
                bool isDisabled = false;
                if (isPanicked && (opt == "Persona" || opt == "Skill" || opt == "Command" || opt == "COMP" || opt == "Item")) isDisabled = true;
                disabledStates.Add(isDisabled);
            }

            int choice = _io.RenderMenu($"{context}\nCommand: {actor.Name}", options, _mainMenuIndex, disabledStates);
            if (choice == -1) return "Cancel";

            _mainMenuIndex = choice;
            return options[choice];
        }

        public List<Combatant> SelectTarget(Combatant actor, SkillData skill = null, ItemData item = null)
        {
            string context = GetBattleContext(actor);
            bool targetsAllies = false;
            bool targetsAll = false;
            Element element = Element.None;

            if (skill != null)
            {
                string effect = skill.Effect.ToLower();
                targetsAllies = skill.Category.Contains("Recovery") || skill.Category.Contains("Enhance") || effect.Contains("ally") || effect.Contains("party");
                targetsAll = skill.Name.ToLower().StartsWith("ma") || skill.Name.ToLower().StartsWith("me") || effect.Contains("all foes") || effect.Contains("all allies") || effect.Contains("party");
                element = ElementHelper.FromCategory(skill.Category);
            }
            else if (item != null)
            {
                targetsAllies = true;
                targetsAll = item.Type == "Healing_All" || item.Name == "Amrita";
            }

            var selectionPool = targetsAllies
                ? (item?.Type == "Revive" || (skill != null && skill.Effect.Contains("Revive")) ? _party.ActiveParty : _party.GetAliveMembers())
                : _enemies.Where(e => !e.IsDead).ToList();

            if (targetsAll) return selectionPool;

            List<string> targetLabels = new List<string>();
            foreach (var t in selectionPool)
            {
                string label = $"{t.Name} (HP: {t.CurrentHP}/{t.MaxHP})";
                if (!targetsAllies && skill != null)
                {
                    Affinity known = _knowledge.GetKnownAffinity(t.SourceId, element);
                    if (_knowledge.HasDiscovery(t.SourceId, element)) label += $" [{known.ToString().ToUpper()}]";
                }
                targetLabels.Add(label);
            }
            targetLabels.Add("Back");

            int choice = _io.RenderMenu($"{context}\nSelect Target:", targetLabels, 0);
            if (choice == -1 || choice == targetLabels.Count - 1) return null;

            return new List<Combatant> { selectionPool[choice] };
        }

        public string GetTacticsChoice(bool isBossBattle, bool isOperator)
        {
            List<string> options = new List<string> { "Escape", "Strategy", "Back" };
            List<bool> disabled = new List<bool> { isBossBattle, !isOperator, false };
            int choice = _io.RenderMenu($"{GetBattleContext(null)}\nTACTICS", options, 0, disabled);
            if (choice == -1 || choice == 2) return "Back";
            return options[choice];
        }

        public Combatant SelectStrategyTarget()
        {
            var targets = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (!targets.Any())
            {
                _io.WriteLine("No demons in party to command.");
                _io.Wait(800);
                return null;
            }
            var names = targets.Select(t => $"{t.Name} [{t.BattleControl}]").ToList();
            names.Add("Back");
            int choice = _io.RenderMenu($"{GetBattleContext(null)}\nSELECT DEMON TO COMMAND", names, 0);
            if (choice == -1 || choice == names.Count - 1) return null;
            return targets[choice];
        }

        public SkillData SelectSkill(Combatant actor, string uiContext)
        {
            var skillNames = actor.GetConsolidatedSkills();
            if (skillNames.Count == 0) return null;

            List<string> labels = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var sName in skillNames)
            {
                if (Database.Skills.TryGetValue(sName, out var data))
                {
                    var cost = data.ParseCost();
                    bool canAfford = cost.isHP ? actor.CurrentHP > (int)(actor.MaxHP * (cost.value / 100.0)) : actor.CurrentSP >= cost.value;
                    labels.Add($"{sName} ({data.Cost})");
                    disabled.Add(!canAfford);
                }
            }
            labels.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nSelect Skill:", labels, _skillMenuIndex, disabled, (idx) =>
            {
                if (idx >= 0 && idx < skillNames.Count)
                {
                    var d = Database.Skills[skillNames[idx]];
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
                }
            });

            if (choice == -1 || choice == labels.Count - 1) return null;
            _skillMenuIndex = choice;
            return Database.Skills[skillNames[choice]];
        }

        public ItemData SelectItem(Combatant actor)
        {
            var ownedItems = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (!ownedItems.Any()) { _io.WriteLine("Inventory is empty."); _io.Wait(800); return null; }

            var labels = ownedItems.Select(i => $"{i.Name} x{_inv.GetQuantity(i.Id)}").ToList();
            labels.Add("Back");

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nItems:", labels, _itemMenuIndex, null, (idx) =>
            {
                if (idx >= 0 && idx < ownedItems.Count) _io.WriteLine(ownedItems[idx].Description);
            });

            if (choice == -1 || choice == labels.Count - 1) return null;
            _itemMenuIndex = choice;
            return ownedItems[choice];
        }

        public (string action, Combatant target) OpenCOMPMenu(Combatant actor)
        {
            List<string> options = new List<string> { "Summon", "Return", "Analyze", "Back" };
            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nCOMP SYSTEM", options, 0);

            if (choice == 0)
            {
                if (!actor.DemonStock.Any()) { _io.WriteLine("No demons in stock."); _io.Wait(800); return ("None", null); }
                var names = actor.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList();
                names.Add("Back");
                int sub = _io.RenderMenu("Summon Demon:", names, 0);
                if (sub == -1 || sub == names.Count - 1) return ("None", null);
                return ("Summon", actor.DemonStock[sub]);
            }

            if (choice == 1)
            {
                var activeDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                if (!activeDemons.Any()) { _io.WriteLine("No active demons to return."); _io.Wait(800); return ("None", null); }
                var names = activeDemons.Select(d => d.Name).ToList();
                names.Add("Back");
                int sub = _io.RenderMenu("Return Demon:", names, 0);
                if (sub == -1 || sub == names.Count - 1) return ("None", null);
                return ("Return", activeDemons[sub]);
            }

            if (choice == 2)
            {
                var targetList = SelectTarget(actor);
                if (targetList == null) return ("None", null);
                return ("Analyze", targetList[0]);
            }
            return ("None", null);
        }

        private string GetBattleContext(Combatant actor)
        {
            string icons = $"Turns: {_turnEngine.GetIconsDisplay()}\n";
            string separator = "==================================================\n";
            string enemyGroup = "ENEMIES:\n";
            foreach (var e in _enemies) enemyGroup += $" {e.Name,-15} {(e.IsDead ? "[DEAD]" : $"HP: {e.CurrentHP}")}\n";
            string partyGroup = "--------------------------------------------------\nPARTY:\n";
            foreach (var p in _party.ActiveParty)
            {
                string status = p.CurrentAilment != null ? $" [{p.CurrentAilment.Name}]" : "";
                partyGroup += $" {p.Name,-15} HP: {p.CurrentHP,4}/{p.MaxHP,4} SP: {p.CurrentSP,4}/{p.MaxSP,4}{(p.IsGuarding ? " (G)" : "")}{status}\n";
            }
            return icons + separator + enemyGroup + partyGroup + separator;
        }
    }
}