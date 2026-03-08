using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.State;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Specialized UI Bridge for Inventory management and Field-based utility usage.
    /// Handles the selection of items, skills, performers, and targets.
    /// Includes metadata repair logic to handle unhydrated equipment names.
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
        /// </summary>
        public ItemData SelectItem(Combatant user, bool inDungeon)
        {
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

                if (item.Name == "Traesto Gem")
                {
                    label += " [BATTLE ONLY]";
                    isDisabled = true;
                }

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

        #region Equipment Selection UI

        /// <summary>
        /// Renders the specific list of equipment available for the player to equip.
        /// Includes a metadata fallback to ensure names appear even if Database objects are unhydrated.
        /// </summary>
        public string SelectEquipmentFromInventory(Combatant player, List<string> ids, ShopCategory category)
        {
            if (ids == null || ids.Count == 0)
            {
                _io.WriteLine($"No {category} available in inventory.");
                _io.Wait(800);
                return "Back";
            }

            List<string> names = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var id in ids)
            {
                // REPAIR LOGIC: Ensure the name is resolved from the Shop Registry if the object is blank.
                string displayName = GetRobustName(id, category);

                bool equipped = category switch
                {
                    ShopCategory.Weapon => player.EquippedWeapon?.Id == id,
                    ShopCategory.Armor => player.EquippedArmor?.Id == id,
                    ShopCategory.Boots => player.EquippedBoots?.Id == id,
                    _ => player.EquippedAccessory?.Id == id
                };

                names.Add($"{displayName}{(equipped ? " [E]" : "")}");
                disabled.Add(equipped);
            }

            names.Add("Back");
            disabled.Add(false);

            if (_uiState.EquipListIndex >= names.Count) _uiState.EquipListIndex = 0;

            int choice = _io.RenderMenu($"=== EQUIP {category.ToString().ToUpper()} ===", names, _uiState.EquipListIndex, disabled, (index) =>
            {
                if (index >= 0 && index < ids.Count)
                {
                    DisplayEquipmentStats(ids[index], category);
                }
            });

            if (choice == -1 || choice == names.Count - 1) return "Back";

            _uiState.EquipListIndex = choice;
            return ids[choice];
        }

        // Attempts to get the item name from the Database, falls back to Shop Registry if blank.
        private string GetRobustName(string id, ShopCategory category)
        {
            string internalName = category switch
            {
                ShopCategory.Weapon => Database.Weapons.TryGetValue(id, out var w) ? w.Name : "",
                ShopCategory.Armor => Database.Armors.TryGetValue(id, out var a) ? a.Name : "",
                ShopCategory.Boots => Database.Boots.TryGetValue(id, out var b) ? b.Name : "",
                ShopCategory.Accessory => Database.Accessories.TryGetValue(id, out var acc) ? acc.Name : "",
                _ => ""
            };

            if (!string.IsNullOrEmpty(internalName)) return internalName;

            // Absolute Source of Truth: The Shop Registry
            var meta = Database.ShopInventory.FirstOrDefault(x => x.Id == id);
            return meta?.Name ?? id;
        }

        private void DisplayEquipmentStats(string id, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Weapon:
                    var w = Database.Weapons[id];
                    _io.WriteLine($"Type: {w.Type} | Pow: {w.Power} Acc: {w.Accuracy}");
                    break;
                case ShopCategory.Armor:
                    var a = Database.Armors[id];
                    _io.WriteLine($"Def: {a.Defense} Eva: {a.Evasion} | {a.Description}");
                    break;
                case ShopCategory.Boots:
                    var b = Database.Boots[id];
                    _io.WriteLine($"Eva: {b.Evasion} | {b.Description}");
                    break;
                case ShopCategory.Accessory:
                    var acc = Database.Accessories[id];
                    _io.WriteLine($"Mod: {acc.ModifierStat} +{acc.ModifierValue} | {acc.Description}");
                    break;
            }
        }

        #endregion

        #region Skill Selection UI

        public Combatant SelectSkillPerformer(Combatant player)
        {
            List<Combatant> candidates = new List<Combatant>();
            if (player.Class != ClassType.Demon) candidates.Add(player);
            if (player.Class == ClassType.Operator)
            {
                candidates.AddRange(_party.ActiveParty.Where(c => c.Class == ClassType.Demon));
                candidates.AddRange(player.DemonStock);
            }

            List<string> performerLabels = candidates.Select(c => $"{c.Name,-15} (SP: {c.CurrentSP,3}/{c.MaxSP,3})").ToList();
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
                .Where(d => d != null && d.Category != "Passive Skills" && (d.Category.Contains("Recovery") || d.Effect.Contains("Cure")))
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

        // General purpose target selection for field items or skills.
        public Combatant SelectFieldTarget(Combatant player, string actionName)
        {
            var targetPool = _party.ActiveParty.ToList();

            List<string> targetLabels = targetPool.Select(c =>
                $"{c.Name,-15} (HP: {c.CurrentHP,3}/{c.MaxHP,3} SP: {c.CurrentSP,3}/" +
                $"{c.MaxSP,3})").ToList();

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