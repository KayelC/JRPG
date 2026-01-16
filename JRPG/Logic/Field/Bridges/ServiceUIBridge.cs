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
    /// Handles all UI interactions for City Services (Hospital and Shop).
    /// </summary>
    public class ServiceUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly EconomyManager _economy;
        private readonly PartyManager _party;

        public ServiceUIBridge(IGameIO io, FieldUIState uiState, EconomyManager economy, PartyManager party)
        {
            _io = io;
            _uiState = uiState;
            _economy = economy;
            _party = party;
        }

        #region Hospital UI

        /// <summary>
        /// Renders the medical treatment list.
        /// Feature: Sorts injured party/stock members to the top.
        /// </summary>
        public Combatant SelectHospitalPatient(Combatant player)
        {
            // Gather all possible patients: Player + Active Party + Stock
            var patients = new List<Combatant> { player };
            patients.AddRange(_party.ActiveParty.Where(p => p != player));
            patients.AddRange(player.DemonStock);

            // SMT III Requirement: Sort injured (HP/SP < Max) to the top for convenience
            var sortedPatients = patients
                .OrderByDescending(p => (p.CurrentHP < p.MaxHP || p.CurrentSP < p.MaxSP))
                .ToList();

            string header = $"=== HOSPITAL / CLOCK ===\n" +
                            $"Current Macca: {_economy.Macca}\n" +
                            $"Select a member to treat:";

            List<string> labels = new List<string>();
            List<bool> disabledList = new List<bool>();

            foreach (var p in sortedPatients)
            {
                int hpMissing = p.MaxHP - p.CurrentHP;
                int spMissing = p.MaxSP - p.CurrentSP;
                int cost = (hpMissing * 1) + (spMissing * 5);

                bool isHealthy = (hpMissing <= 0 && spMissing <= 0);
                string costDisplay = isHealthy ? "[HEALTHY]" : $"{cost} M";

                labels.Add($"{p.Name,-15} | HP: {p.CurrentHP,3}/{p.MaxHP,3} SP: {p.CurrentSP,3}/{p.MaxSP,3} | {costDisplay}");
                disabledList.Add(isHealthy);
            }

            labels.Add("Leave");
            disabledList.Add(false);

            // Resetting index to 0 for hospital as urgency sorting changes the list context
            int choice = _io.RenderMenu(header, labels, 0, disabledList);

            if (choice == -1 || choice == labels.Count - 1) return null;

            return sortedPatients[choice];
        }

        #endregion

        #region Shop UI

        /// <summary>
        /// Renders the category selection within the City Services menu.
        /// </summary>
        public string ShowCityServicesMenu()
        {
            string header = $"=== CITY SERVICES ===\nMacca: {_economy.Macca}";
            List<string> options = new List<string>
            {
                "Blacksmith (Weapons)",
                "Clothing Store (Armor/Boots)",
                "Jeweler (Accessories)",
                "Pharmacy (Items)",
                "Hospital (Heal)",
                "Back"
            };

            int choice = _io.RenderMenu(header, options, _uiState.CityMenuIndex);
            if (choice == -1 || choice == options.Count - 1) return "Back";

            _uiState.CityMenuIndex = choice;
            return options[choice];
        }

        /// <summary>
        /// Renders the specific list of equipment available for the player to equip.
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
                string name = category switch
                {
                    ShopCategory.Weapon => Database.Weapons[id].Name,
                    ShopCategory.Armor => Database.Armors[id].Name,
                    ShopCategory.Boots => Database.Boots[id].Name,
                    _ => Database.Accessories[id].Name
                };

                bool equipped = category switch
                {
                    ShopCategory.Weapon => player.EquippedWeapon?.Id == id,
                    ShopCategory.Armor => player.EquippedArmor?.Id == id,
                    ShopCategory.Boots => player.EquippedBoots?.Id == id,
                    _ => player.EquippedAccessory?.Id == id
                };

                names.Add($"{name}{(equipped ? " [E]" : "")}");
                disabled.Add(equipped); // Cannot re-select currently equipped items
            }

            names.Add("Back");
            disabled.Add(false);

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

        /// <summary>
        /// Helper to display item stats during selection in the Equip menu.
        /// </summary>
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
    }
}