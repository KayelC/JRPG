using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// The logic engine for the Field Sub-System.
    /// Handles the "Math and State" for services like the Hospital, 
    /// Item/Skill usage, Equipment management, and Dungeon progression.
    /// </summary>
    public class FieldServiceEngine
    {
        private readonly IGameIO _io;
        private readonly EconomyManager _economy;
        private readonly InventoryManager _inventory;
        private readonly PartyManager _party;
        private readonly DungeonState _dungeonState;
        private readonly ShopManager _shopManager;

        public FieldServiceEngine(
            IGameIO io,
            EconomyManager economy,
            InventoryManager inventory,
            PartyManager party,
            DungeonState dungeonState)
        {
            _io = io;
            _economy = economy;
            _inventory = inventory;
            _party = party;
            _dungeonState = dungeonState;

            _shopManager = new ShopManager(_inventory, _economy, _io);
        }

        #region Shop and Equipment

  
        public void OpenShop(Combatant player, ShopType shopType)
        {
            _shopManager.OpenShop(player, shopType);
        }

        public void PerformEquip(Combatant player, string equipId, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Weapon:
                    player.EquippedWeapon = Database.Weapons[equipId];
                    break;
                case ShopCategory.Armor:
                    player.EquippedArmor = Database.Armors[equipId];
                    break;
                case ShopCategory.Boots:
                    player.EquippedBoots = Database.Boots[equipId];
                    break;
                case ShopCategory.Accessory:
                    player.EquippedAccessory = Database.Accessories[equipId];
                    break;
            }

            player.RecalculateResources();
            _io.WriteLine("Equipped successfully!");
            _io.Wait(500);
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
        /// Authoritative logic for using an item on the field.
        /// Handles deduction from inventory and application of effects.
        /// </summary>
        public bool ExecuteItemUsage(ItemData item, Combatant user, Combatant target)
        {
            if (!_inventory.HasItem(item.Id)) return false;

            bool effectApplied = false;

            // Specialized Item: Goho-M (Dungeon Escape)
            if (item.Name == "Goho-M")
            {
                _io.WriteLine("Using Goho-M... A mystical light surrounds the party.");
                _io.Wait(1000);
                _inventory.RemoveItem(item.Id, 1);
                _dungeonState.ResetToEntry();
                return true;
            }

            // Standard Item Categories
            switch (item.Type)
            {
                case "Healing":
                case "Healing_All":
                    if (target.CurrentHP >= target.MaxHP)
                    {
                        _io.WriteLine($"{target.Name}'s HP is already full.");
                    }
                    else
                    {
                        int healAmount = item.EffectValue >= 9999 ? target.MaxHP : item.EffectValue;
                        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                        _io.WriteLine($"{target.Name} recovered health.");
                        effectApplied = true;
                    }
                    break;

                case "Spirit":
                    if (target.CurrentSP >= target.MaxSP)
                    {
                        _io.WriteLine($"{target.Name}'s SP is already full.");
                    }
                    else
                    {
                        target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + item.EffectValue);
                        _io.WriteLine($"{target.Name} recovered SP.");
                        effectApplied = true;
                    }
                    break;

                case "Cure":
                    // Instantiate a transient StatusRegistry to handle the removal logic
                    StatusRegistry sr = new StatusRegistry();
                    if (sr.CheckAndExecuteCure(target, item.Name))
                    {
                        _io.WriteLine($"{target.Name} was cured of their ailment!");
                        effectApplied = true;
                    }
                    else _io.WriteLine("The item had no effect.");
                    break;
            }

            if (effectApplied)
            {
                _inventory.RemoveItem(item.Id, 1);
                _io.Wait(800);
            }
            return effectApplied;
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
                _io.WriteLine($"{user.Name} does not have enough SP.");
                _io.Wait(800);
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
                        _io.WriteLine($"{target.Name} was healed.");
                        applied = true;
                    }
                    else _io.WriteLine($"{target.Name} is already at full health.");
                }
            }

            if (applied)
            {
                user.CurrentSP -= cost.value;
                _io.Wait(800);
            }
            return applied;
        }

        #endregion

        #region Stat Allocation logic

        /// <summary>
        /// Logic Port: Handles the menu loop and selection for stat allocation.
        /// Feature: Preserves the flavor text/bonus descriptions from the monolith.
        /// </summary>
        public StatType? PromptStatAllocation(Combatant player)
        {
            List<string> options = new List<string>();
            var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>().ToList();

            foreach (StatType s in stats)
            {
                options.Add($"{s}: {player.CharacterStats[s]}");
            }
            options.Add("Back");

            int idx = _io.RenderMenu($"=== STAT ALLOCATION (Pts: {player.StatPoints}) ===", options, 0, null, (index) =>
            {
                if (index >= 0 && index < stats.Count)
                {
                    StatType s = stats[index];
                    string bonus = s switch
                    {
                        StatType.STR => "Increases Physical Damage",
                        StatType.MAG => "Increases Magic Damage and +3 Max SP",
                        StatType.END => "Increases Max HP by 5",
                        StatType.AGI => "Increases Hit/Accuracy and Evasion Chance",
                        StatType.LUK => "General Purpose Stat affecting Chances and Shop Prices",
                        _ => ""
                    };
                    _io.WriteLine($"Highlight: {s}");
                    _io.WriteLine($"Current: {player.CharacterStats[s]}");
                    _io.WriteLine($"Bonus: {bonus}"); ;
                }
            });

            if (idx == -1 || idx == options.Count - 1) return null;
            return stats[idx];
        }

        public void AllocateStatPoint(Combatant player, StatType type)
        {
            if (player.StatPoints <= 0) return;

            // Hard Cap Check (Global requirement established earlier)
            if (player.CharacterStats[type] >= 40)
            {
                _io.WriteLine($"{type} has already reached the maximum cap of 40.", ConsoleColor.Yellow);
                _io.Wait(800);
                return;
            }

            player.AllocateStat(type);
            _io.WriteLine($"{type} increased!");
            _io.Wait(300);
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
                _io.WriteLine($"Equipped {newPersona.Name}!");
                player.RecalculateResources();
                _io.Wait(800);
            }
        }

        #endregion

        #region Dungeon and Progression Logic

        // Registers the defeat of a floor boss.
        public void RegisterBossDefeat(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId)) _dungeonState.MarkBossDefeated(bossId);
        }


        /// Persistently unlocks a terminal for future warping.
        public void UnlockTerminal(int floor)
        {
            _dungeonState.UnlockTerminal(floor);
        }

        #endregion
    }
}