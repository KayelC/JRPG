using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Specialized UI Bridge for Inventory management and Field-based utility usage.
    /// Handles the selection of items, skills, performers, and targets.
    /// </summary>
    public class InventoryUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly InventoryManager _inventory;
        private readonly PartyManager _party;

        public InventoryUIBridge(IGameIO io, FieldUIState uiState, InventoryManager inventory, PartyManager party)
        {
            _io = io;
            _uiState = uiState;
            _inventory = inventory;
            _party = party;
        }

        #region Main Inventory Navigation

        /// <summary>
        /// Renders the primary Inventory sub-menu.
        /// </summary>
        public string ShowInventorySubMenu(Combatant player)
        {
            string header = "=== INVENTORY ===";
            List<string> options = new List<string> { "Use Item", "Use Skill", "Equipment" };

            // Operator-specific COMP access carried over from monolith
            if (player.Class == ClassType.Operator)
            {
                options.Add("Demons (COMP)");
            }

            options.Add("Back");

            int choice = _io.RenderMenu(header, options, _uiState.InventoryMenuIndex);

            if (choice == -1 || choice == options.Count - 1) return "Back";

            _uiState.InventoryMenuIndex = choice;
            return options[choice];
        }

        #endregion

        #region Item Selection UI

        /// <summary>
        /// Logic for selecting an item from the player's stock.
        /// Feature: Displays item descriptions and handles context-based disabling (Traesto/Goho-M).
        /// </summary>
        public ItemData SelectItem(Combatant user, bool inDungeon)
        {
            // Only items with quantity > 0 are candidates
            var ownedItems = Database.Items.Values
                .Where(itm => _inventory.GetQuantity(itm.Id) > 0)
                .ToList();

            if (ownedItems.Count == 0)
            {
                _io.WriteLine("No usable items remaining.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            List<string> options = new List<string>();
            List<bool> disabledList = new List<bool>();

            foreach (var item in ownedItems)
            {
                string label = $"{item.Name,-20} x{_inventory.GetQuantity(item.Id)}";
                bool isDisabled = false;

                // Monolith Rule: Traesto Gem is for Battle Escape only
                if (item.Name == "Traesto Gem")
                {
                    label += " [BATTLE ONLY]";
                    isDisabled = true;
                }

                // Monolith Rule: Goho-M is for Dungeon usage only
                if (item.Name == "Goho-M" && !inDungeon)
                {
                    label += " [DUNGEON ONLY]";
                    isDisabled = true;
                }

                options.Add(label);
                disabledList.Add(isDisabled);
            }

            options.Add("Back");
            disabledList.Add(false);

            // Bounds check for persistent index
            if (_uiState.ItemMenuIndex >= options.Count) _uiState.ItemMenuIndex = 0;

            int choice = _io.RenderMenu($"User: {user.Name} | Select Item:", options, _uiState.ItemMenuIndex, disabledList, (index) =>
            {
                if (index >= 0 && index < ownedItems.Count)
                {
                    _io.WriteLine($"Description: {ownedItems[index].Description}");
                }
            });

            if (choice == -1 || choice == options.Count - 1) return null;

            _uiState.ItemMenuIndex = choice;
            return ownedItems[choice];
        }

        #endregion

        #region Skill Selection UI

        /// <summary>
        /// Selects the character that will perform a field skill.
        /// Includes humans and, for Operators, demons in the party or stock.
        /// </summary>
        public Combatant SelectSkillPerformer(Combatant player)
        {
            List<Combatant> candidates = new List<Combatant>();

            // Humans (except demons) can usually use skills if they have them
            if (player.Class != ClassType.Demon)
            {
                candidates.Add(player);
            }

            // Operator logic: Can command demons to use skills even if they are in stock
            if (player.Class == ClassType.Operator)
            {
                candidates.AddRange(_party.ActiveParty.Where(c => c.Class == ClassType.Demon));
                candidates.AddRange(player.DemonStock);
            }

            List<string> performerLabels = candidates.Select(c =>
                $"{c.Name,-15} (SP: {c.CurrentSP,3}/{c.MaxSP,3})").ToList();

            performerLabels.Add("Back");

            int perfIdx = _io.RenderMenu("Who is performing the skill?", performerLabels, 0);

            if (perfIdx == -1 || perfIdx == performerLabels.Count - 1) return null;

            return candidates[perfIdx];
        }

        /// <summary>
        /// Filters and selects a specific skill for field usage.
        /// Logic: Prunes offensive and passive skills, showing only Recovery/Cure types.
        /// </summary>
        public SkillData SelectFieldSkill(Combatant performer)
        {
            var skillPool = performer.GetConsolidatedSkills()
                .Select(s => Database.Skills.TryGetValue(s, out var d) ? d : null)
                .Where(d => d != null && d.Category != "Passive Skills" &&
                       (d.Category.Contains("Recovery") || d.Effect.Contains("Cure")))
                .ToList();

            if (!skillPool.Any())
            {
                _io.WriteLine($"{performer.Name} has no field-usable skills.", ConsoleColor.Red);
                _io.Wait(800);
                return null;
            }

            List<string> skillLabels = skillPool.Select(s => $"{s.Name,-15} ({s.Cost})").ToList();
            skillLabels.Add("Back");

            if (_uiState.SkillMenuIndex >= skillLabels.Count) _uiState.SkillMenuIndex = 0;

            int choice = _io.RenderMenu($"{performer.Name}'s Skills:", skillLabels, _uiState.SkillMenuIndex, null, (index) =>
            {
                if (index >= 0 && index < skillPool.Count)
                {
                    _io.WriteLine($"Effect: {skillPool[index].Effect}");
                }
            });

            if (choice == -1 || choice == skillLabels.Count - 1) return null;

            _uiState.SkillMenuIndex = choice;
            return skillPool[choice];
        }

        #endregion

        #region Target Selection UI

        /// <summary>
        /// General purpose target selection for field items or skills.
        /// </summary>
        public Combatant SelectFieldTarget(Combatant player, string actionName)
        {
            var targetPool = _party.ActiveParty.ToList();

            List<string> targetLabels = targetPool.Select(c =>
                $"{c.Name,-15} (HP: {c.CurrentHP,3}/{c.MaxHP,3} SP: {c.CurrentSP,3}/{c.MaxSP,3})").ToList();

            targetLabels.Add("Back");

            string prompt = !string.IsNullOrEmpty(actionName)
                ? $"Using {actionName}. Select Target:"
                : "Select Target:";

            int choice = _io.RenderMenu(prompt, targetLabels, 0);

            if (choice == -1 || choice == targetLabels.Count - 1) return null;

            return targetPool[choice];
        }

        #endregion
    }
}