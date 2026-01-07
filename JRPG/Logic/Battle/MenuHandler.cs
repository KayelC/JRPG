using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    public class MenuHandler
    {
        private readonly IGameIO _io;
        private readonly InventoryManager _inv;
        private readonly PartyManager _party;

        private int _mainMenuIndex = 0;
        private int _skillMenuIndex = 0;
        private int _itemMenuIndex = 0;

        public MenuHandler(IGameIO io, InventoryManager inv, PartyManager party)
        {
            _io = io;
            _inv = inv;
            _party = party;
        }

        public string GetActionChoice(Combatant actor, string uiContext)
        {
            List<string> options = new List<string> { "Attack", "Guard" };

            if (actor.Class == ClassType.PersonaUser || actor.Class == ClassType.WildCard)
            {
                options.Add("Persona");
            }
            else if (actor.Class == ClassType.Operator)
            {
                options.Add("Command");
                options.Add("COMP");
            }
            else
            {
                options.Add("Skill");
            }

            options.Add("Item");
            options.Add("Tactics");
            options.Add("Pass");

            bool isPanicked = actor.CurrentAilment != null && actor.CurrentAilment.Name == "Panic";
            List<bool> disabledStates = new List<bool>();
            foreach (var opt in options)
            {
                bool isDisabled = false;
                if (isPanicked && (opt == "Persona" || opt == "Skill" || opt == "Command" || opt == "COMP"))
                {
                    isDisabled = true;
                }
                disabledStates.Add(isDisabled);
            }

            // We prepend the uiContext (the table) to the specific command prompt
            int choice = _io.RenderMenu($"{uiContext}\nCommand: {actor.Name}", options, _mainMenuIndex, disabledStates);
            if (choice == -1)
            {
                return "Cancel";
            }

            _mainMenuIndex = choice;
            return options[choice];
        }

        public string GetTacticsChoice(string uiContext, bool isBossBattle, bool canChangeStrategy)
        {
            List<string> options = new List<string> { "Escape", "Strategy", "Back" };
            List<bool> disabled = new List<bool> { isBossBattle, !canChangeStrategy, false };

            int choice = _io.RenderMenu($"{uiContext}\nTACTICS", options, 0, disabled);
            if (choice == -1 || choice == 2) return "Back";

            return options[choice];
        }

        public Combatant SelectStrategyTarget(string uiContext, List<Combatant> party)
        {
            var targets = party.Where(c => c.Class == ClassType.Demon).ToList();
            if (!targets.Any())
            {
                _io.WriteLine("No demons in party to command.");
                _io.Wait(800);
                return null;
            }

            var names = targets.Select(t => $"{t.Name} [{t.BattleControl}]").ToList();
            names.Add("Back");

            int choice = _io.RenderMenu($"{uiContext}\nSELECT DEMON TO COMMAND", names, 0);
            if (choice == -1 || choice == names.Count - 1) return null;

            return targets[choice];
        }

        public SkillData SelectSkill(Combatant actor, string uiContext)
        {
            var skillNames = actor.GetConsolidatedSkills();
            if (skillNames.Count == 0)
            {
                return null;
            }

            List<string> labels = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var sName in skillNames)
            {
                if (Database.Skills.TryGetValue(sName, out var data))
                {
                    var cost = data.ParseCost();
                    bool canAfford = cost.isHP ? actor.CurrentHP > cost.value : actor.CurrentSP >= cost.value;

                    labels.Add($"{sName} ({data.Cost})");
                    disabled.Add(!canAfford);
                }
            }

            labels.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"{uiContext}\nSelect Skill:", labels, _skillMenuIndex, disabled, (idx) =>
            {
                if (idx >= 0 && idx < skillNames.Count)
                {
                    var d = Database.Skills[skillNames[idx]];
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
                }
            });

            if (choice == -1 || choice == labels.Count - 1)
            {
                return null;
            }

            _skillMenuIndex = choice;
            return Database.Skills[skillNames[choice]];
        }

        public ItemData SelectItem(string uiContext)
        {
            var ownedItems = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (ownedItems.Count == 0)
            {
                return null;
            }

            var labels = ownedItems.Select(i => $"{i.Name} x{_inv.GetQuantity(i.Id)}").ToList();
            labels.Add("Back");

            int choice = _io.RenderMenu($"{uiContext}\nItems", labels, _itemMenuIndex, null, (idx) =>
            {
                if (idx >= 0 && idx < ownedItems.Count)
                {
                    _io.WriteLine(ownedItems[idx].Description);
                }
            });

            if (choice == -1 || choice == labels.Count - 1)
            {
                return null;
            }

            _itemMenuIndex = choice;
            return ownedItems[choice];
        }

        public List<Combatant> AcquireTargets(Combatant actor, SkillData skill, List<Combatant> enemies, string uiContext)
        {
            bool targetsAll = skill != null && skill.Effect.Contains("all", StringComparison.OrdinalIgnoreCase);

            bool targetsAllySide = false;
            if (skill != null)
            {
                if (skill.Effect.Contains("ally", StringComparison.OrdinalIgnoreCase) ||
                    skill.Category.Contains("Recovery") ||
                    skill.Category.Contains("Enhance"))
                {
                    targetsAllySide = true;
                }
            }

            var selectionPool = targetsAllySide ? _party.ActiveParty : enemies.Where(e => !e.IsDead).ToList();

            if (targetsAll)
            {
                return selectionPool;
            }

            var targetLabels = selectionPool.Select(t => $"{t.Name} (HP: {t.CurrentHP}/{t.MaxHP})").ToList();
            targetLabels.Add("Back");

            int choice = _io.RenderMenu($"{uiContext}\nSelect Target:", targetLabels, 0);
            if (choice == -1 || choice == targetLabels.Count - 1)
            {
                return null;
            }

            return new List<Combatant> { selectionPool[choice] };
        }

        public (string action, Combatant demon) GetCompAction(Combatant actor, string uiContext)
        {
            List<string> menu = new List<string> { "Summon", "Return", "Back" };
            int choice = _io.RenderMenu($"{uiContext}\nCOMP SYSTEM", menu, 0);

            if (choice == 0) // Summon
            {
                if (actor.DemonStock.Count == 0)
                {
                    _io.WriteLine("No demons available in COMP stock.");
                    _io.Wait(800);
                    return ("None", null);
                }

                var names = actor.DemonStock.Select(d => $"{d.Name} (Lv.{d.Level})").ToList();
                names.Add("Back");
                int subChoice = _io.RenderMenu($"{uiContext}\nSummon Demon:", names, 0);

                if (subChoice == -1 || subChoice == names.Count - 1)
                {
                    return ("None", null);
                }
                return ("Summon", actor.DemonStock[subChoice]);
            }

            if (choice == 1) // Return
            {
                var activeDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                if (activeDemons.Count == 0)
                {
                    _io.WriteLine("No active demons to return.");
                    _io.Wait(800);
                    return ("None", null);
                }

                var names = activeDemons.Select(d => d.Name).ToList();
                names.Add("Back");
                int subChoice = _io.RenderMenu($"{uiContext}\nReturn Demon:", names, 0);

                if (subChoice == -1 || subChoice == names.Count - 1)
                {
                    return ("None", null);
                }
                return ("Return", activeDemons[subChoice]);
            }

            return ("None", null);
        }
    }
}