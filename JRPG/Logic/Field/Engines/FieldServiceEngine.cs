using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JRPGPrototype.Logic.Field.Engines
{
    /// <summary>
    /// The logic engine for the Field Sub-System.
    /// Handles the "Math and State" for services like the Hospital, 
    /// Item/Skill usage, Equipment management, and Dungeon progression.
    /// </summary>
    public class FieldServiceEngine
    {
        private readonly IFieldMessenger _messenger;
        private readonly IGameIO _io;
        private readonly EconomyManager _economy;
        private readonly InventoryManager _inventory;
        private readonly PartyManager _party;
        private readonly DungeonState _dungeonState;

        // Shop Components
        private readonly ShopEngine _shopEngine;
        private readonly ShopUIBridge _shopUI;

        public FieldServiceEngine(
            IFieldMessenger messenger,
            IGameIO io,
            EconomyManager economy,
            InventoryManager inventory,
            PartyManager party,
            DungeonState dungeonState)
        {
            _messenger = messenger;
            _io = io;
            _economy = economy;
            _inventory = inventory;
            _party = party;
            _dungeonState = dungeonState;

            // Initialize Shop Components
            _shopEngine = new ShopEngine(_inventory, _economy, _messenger);
            _shopUI = new ShopUIBridge(_io, _messenger, _shopEngine, _economy, _inventory);
        }

        #region Shop and Equipment

        /// <summary>
        /// Global method for adding items to inventory (Shop, Boss Drops, Chests).
        /// Ensures that unhydrated metadata (blank names) is repaired before possession.
        /// </summary>
        public void AddPossession(string id, ShopCategory category, int quantity = 1)
        {
            // 1. Repair metadata before the item is ever seen by the player
            object? dataObject = category switch
            {
                ShopCategory.Weapon => Database.Weapons.TryGetValue(id, out var w) ? w : null,
                ShopCategory.Armor => Database.Armors.TryGetValue(id, out var a) ? a : null,
                ShopCategory.Boots => Database.Boots.TryGetValue(id, out var b) ? b : null,
                ShopCategory.Accessory => Database.Accessories.TryGetValue(id, out var acc) ? acc : null,
                ShopCategory.Item => Database.Items.TryGetValue(id, out var i) ? i : null,
                _ => null
            };

            if (dataObject != null)
            {
                ValidateMetadata(dataObject, id);
            }

            // 2. Add to actual inventory
            if (category == ShopCategory.Item)
            {
                _inventory.AddItem(id, quantity);
            }
            else
            {
                for (int i = 0; i < quantity; i++)
                {
                    _inventory.AddEquipment(id, category);
                }
            }
        }

        #endregion

        #region Shop and Equipment

        public void OpenShop(Combatant player, ShopType shopType)
        {
            _shopUI.OpenShop(player, shopType);
        }

        public void PerformEquip(Combatant player, string equipId, ShopCategory category)
        {
            string id = equipId?.Trim() ?? "";
            bool success = false;

            switch (category)
            {
                case ShopCategory.Weapon:
                    if (Database.Weapons.TryGetValue(id, out var w))
                    {
                        ValidateMetadata(w, id);
                        player.EquippedWeapon = w;
                        success = true;
                    }
                    break;
                case ShopCategory.Armor:
                    if (Database.Armors.TryGetValue(id, out var a))
                    {
                        ValidateMetadata(a, id);
                        player.EquippedArmor = a;
                        success = true;
                    }
                    break;
                case ShopCategory.Boots:
                    if (Database.Boots.TryGetValue(id, out var b))
                    {
                        ValidateMetadata(b, id);
                        player.EquippedBoots = b;
                        success = true;
                    }
                    break;
                case ShopCategory.Accessory:
                    if (Database.Accessories.TryGetValue(id, out var acc))
                    {
                        ValidateMetadata(acc, id);
                        player.EquippedAccessory = acc;
                        success = true;
                    }
                    break;
            }

            if (success)
            {
                player.RecalculateResources();
                _messenger.Publish("Equipped successfully!", ConsoleColor.Gray, 500);
            }
            else
            {
                _messenger.Publish($"Error: ID {id} not found in Database.", ConsoleColor.Red, 1000);
            }
        }

        /// <summary>
        /// Fixes unhydrated Database objects. 
        /// Priority 1: Use existing name if present.
        /// Priority 2: Use Shop Registry metadata.
        /// Priority 3: Fallback to ID-based formatting (Final Safety).
        /// </summary>
        private void ValidateMetadata(object obj, string id)
        {
            var meta = Database.ShopInventory.FirstOrDefault(x => x.Id == id);

            if (obj is WeaponData w)
            {
                if (string.IsNullOrEmpty(w.Name)) w.Name = meta?.Name ?? $"Weapon {id}";
                w.Id = id;
            }
            else if (obj is ArmorData a)
            {
                if (string.IsNullOrEmpty(a.Name)) a.Name = meta?.Name ?? $"Armor {id}";
                a.Id = id;
            }
            else if (obj is BootData b)
            {
                if (string.IsNullOrEmpty(b.Name)) b.Name = meta?.Name ?? $"Boots {id}";
                b.Id = id;
            }
            else if (obj is AccessoryData acc)
            {
                if (string.IsNullOrEmpty(acc.Name)) acc.Name = meta?.Name ?? $"Accessory {id}";
                acc.Id = id;
            }
        }

        #endregion

        #region Restoration Logic

        /// <summary>
        /// Calculates the Macca cost to fully restore a combatant.
        /// Logic: 1 Macca per 1 HP, 5 Macca per 1 SP.
        /// </summary>
        public int CalculateRestorationCost(Combatant patient)
        {
            int hpMissing = patient.MaxHP - patient.CurrentHP;
            int spMissing = patient.MaxSP - patient.CurrentSP;
            return (hpMissing * 1) + (spMissing * 5);
        }

        /// <summary>
        /// Logic for processing a Hospital treatment.
        /// Validates funds and applies the state change.
        /// </summary>
        public bool TryRestoreCombatant(Combatant patient)
        {
            int cost = CalculateRestorationCost(patient);

            if (cost <= 0) return false;

            if (_economy.Macca >= cost)
            {
                if (_economy.SpendMacca(cost))
                {
                    patient.CurrentHP = patient.MaxHP;
                    patient.CurrentSP = patient.MaxSP;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Item Usage Logic

        /// <summary>
        /// Executes item usage and returns an explicit result signal to the Conductor.
        /// </summary>
        public ItemUsageResult ExecuteItemUsage(ItemData item, Combatant user, Combatant target)
        {
            if (!_inventory.HasItem(item.Id)) return ItemUsageResult.Failed;

            bool effectApplied = false;

            // Specialized Item: Goho-M (Explicit Exit Request)
            if (item.Name == "Goho-M")
            {
                _messenger.Publish("Using Goho-M... A mystical light surrounds the party.", ConsoleColor.Gray, 1000);
                _inventory.RemoveItem(item.Id, 1);
                _dungeonState.ResetToEntry();
                return ItemUsageResult.RequestDungeonExit;
            }

            // Standard Item Categories
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    if (target.CurrentHP >= target.MaxHP)
                    {
                        _messenger.Publish($"{target.Name}'s HP is already full.");
                    }
                    else
                    {
                        int healAmount = item.EffectValue >= 9999 ? target.MaxHP : item.EffectValue;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                        _messenger.Publish($"{target.Name} recovered health.");
                        effectApplied = true;
                    }
                    break;

                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP)
                    {
                        _messenger.Publish($"{target.Name}'s SP is already full.");
                    }
                    else
                    {
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        _messenger.Publish($"{target.Name} recovered SP.");
                        effectApplied = true;
                    }
                    break;

                case "Cure":
                    // Instantiate a transient StatusRegistry to handle the removal logic
                    StatusRegistry sr = new StatusRegistry();
                    if (sr.CheckAndExecuteCure(target, item.Name))
                    {
                        _messenger.Publish($"{target.Name} was cured of their ailment!");
                        effectApplied = true;
                    }
                    else
                    {
                        _messenger.Publish("The item had no effect.");
                    }
                    break;
            }

            if (effectApplied)
            {
                _inventory.RemoveItem(item.Id, 1);
                _messenger.Publish(null, ConsoleColor.Gray, 800);
                return ItemUsageResult.Applied;
            }

            return ItemUsageResult.Failed;
        }

        #endregion

        #region Skill Usage Logic

        /// <summary>
        /// Logic for using a character's skill on the field.
        /// Handles SP cost deduction and effect calculation.
        /// </summary>
        public bool ExecuteSkillUsage(SkillData skill, Combatant user, Combatant target)
        {
            var cost = skill.ParseCost();

            if (user.CurrentSP < cost.value)
            {
                _messenger.Publish($"{user.Name} does not have enough SP.", ConsoleColor.Gray, 800);
                return false;
            }

            bool applied = false;
            StatusRegistry sr = new StatusRegistry();

            // Field-usable skills are restricted to Recovery and Cure
            if (skill.Category.Contains("Recovery") || skill.Effect.Contains("Cure"))
            {
                if (skill.Effect.Contains("Cure"))
                {
                    applied = sr.CheckAndExecuteCure(target, skill.Effect);
                }
                else
                {
                    int heal = 0;
                    // Parse the power from the skill effect string if numerical
                    Match m = Regex.Match(skill.Effect, @"\((\d+)\)");
                    if (m.Success) heal = int.Parse(m.Groups[1].Value);

                    // Handle percentage-based field heals
                    if (skill.Effect.Contains("50%")) heal = target.MaxHP / 2;
                    if (skill.Effect.Contains("full")) heal = target.MaxHP;

                    // Apply standard power if neither percentage nor specific value found
                    if (heal == 0) heal = skill.GetPowerVal();

                    if (target.CurrentHP < target.MaxHP)
                    {
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
                        _messenger.Publish($"{target.Name} was healed.");
                        applied = true;
                    }
                    else
                    {
                        _messenger.Publish($"{target.Name} is already at full health.");
                    }
                }
            }

            if (applied)
            {
                user.CurrentSP -= cost.value;
                _messenger.Publish(null, ConsoleColor.Gray, 800);
            }
            return applied;
        }

        #endregion

        #region Stat Allocation logic

        // Handles the menu loop and selection for stat allocation.
        public void AllocateStatPoint(Combatant player, StatType type)
        {
            if (player.StatPoints <= 0) return;

            // Hard Cap Check
            if (player.CharacterStats.ContainsKey(type) && player.CharacterStats[type] >= 40)
            {
                _messenger.Publish($"{type} has already reached the maximum cap of 40.", ConsoleColor.Yellow, 800);
                return;
            }

            player.AllocateStat(type);
            _messenger.Publish($"{type} increased!", ConsoleColor.Gray, 300);
        }

        // Rollback method to revert stats and points to a previous snapshot if player cancels.
        public void RollbackStats(Combatant player, Dictionary<StatType, int> statBackup, int pointBackup)
        {
            foreach (var stat in statBackup)
            {
                player.CharacterStats[stat.Key] = stat.Value;
            }
            player.StatPoints = pointBackup;

            // Crucial: Recalculate to fix HP/SP caps after stats are reverted
            player.RecalculateResources();
        }

        #endregion

        #region Persona Logic

        public void PerformPersonaSwap(Combatant player, Persona newPersona)
        {
            int stockIndex = player.PersonaStock.IndexOf(newPersona);
            if (stockIndex != -1)
            {
                Persona oldActive = player.ActivePersona;
                player.ActivePersona = newPersona;
                player.PersonaStock[stockIndex] = oldActive;
                _messenger.Publish($"Equipped {newPersona.Name}!", ConsoleColor.Gray, 800);
                player.RecalculateResources();
            }
        }

        #endregion

        #region Dungeon and Progression Logic

        // Registers the defeat of a floor boss.
        public void RegisterBossDefeat(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId)) _dungeonState.MarkBossDefeated(bossId);
        }

        // Persistently unlocks a terminal for future warping.
        public void UnlockTerminal(int floor)
        {
            _dungeonState.UnlockTerminal(floor);
        }

        #endregion
    }
}