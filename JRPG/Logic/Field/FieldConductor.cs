using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Field.Bridges;

namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// The Root Orchestrator for the Field Sub-System.
    /// Manages the high-level state transitions between City, Dungeon, and Menus.
    /// Coordinates the specialized Bridges and Logic Engines.
    /// </summary>
    public class FieldConductor
    {
        // Infrastructure
        private readonly IGameIO _io;
        private readonly Combatant _player;
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly DungeonState _dungeonState;
        private readonly DungeonManager _dungeonManager;
        private readonly PartyManager _partyManager;
        private readonly BattleKnowledge _playerKnowledge;

        // Sub-Sub-System Components
        private readonly FieldUIState _uiState;
        private readonly ServiceUIBridge _serviceUI;
        private readonly DungeonUIBridge _dungeonUI;
        private readonly StatusUIBridge _statusUI;
        private readonly InventoryUIBridge _inventoryUI;
        private readonly FieldServiceEngine _logicEngine;
        private readonly ExplorationProcessor _explorationProcessor;

        public FieldConductor(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            DungeonState dungeonState,
            IGameIO io,
            BattleKnowledge playerKnowledge)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _dungeonState = dungeonState;
            _io = io;
            _playerKnowledge = playerKnowledge;

            // Initialize Shared State
            _uiState = new FieldUIState();

            // Initialize Core Logic Managers
            _partyManager = new PartyManager(_player);
            _dungeonManager = new DungeonManager(_dungeonState);

            // Initialize Logic Engines
            _logicEngine = new FieldServiceEngine(_io, _economy, _inventory, _partyManager, _dungeonState);

            // Initialize Specialized Bridges
            _serviceUI = new ServiceUIBridge(_io, _uiState, _economy, _partyManager);
            _dungeonUI = new DungeonUIBridge(_io, _uiState);
            _statusUI = new StatusUIBridge(_io, _uiState, _partyManager);
            _inventoryUI = new InventoryUIBridge(_io, _uiState, _inventory, _partyManager);

            // Initialize Exploration Processor
            _explorationProcessor = new ExplorationProcessor(_io, _dungeonManager, _dungeonState, _dungeonUI, _logicEngine);
        }

        /// <summary>
        /// The primary entry point for the Field Sub-System.
        /// Orchestrates the top-level loop between the City, Dungeon, and System menus.
        /// </summary>
        public void NavigateMenus()
        {
            while (true)
            {
                string choice = _serviceUI.ShowFieldMainMenu(_player);

                if (choice == "Cancel") continue;

                switch (choice)
                {
                    case "Explore Tartarus":
                        PrepareDungeonEntry();
                        break;

                    case "City Services":
                        OpenCityMenu();
                        break;

                    case "Inventory":
                        OpenInventoryMenu(inDungeon: false);
                        break;

                    case "Status":
                        OpenSeamlessStatusMenu();
                        break;

                    case "Organize Party":
                        OpenOrganizeMenu();
                        break;

                    case "Exit Game":
                        return;
                }

                // If the player somehow dies in the field (future-proofing for traps/DOT)
                if (_player.CurrentHP <= 0) return;
            }
        }

        #region Dungeon Traversal Logic

        private void PrepareDungeonEntry()
        {
            List<int> terminals = _dungeonManager.GetUnlockedTerminals();

            // If only the entrance is unlocked, skip the warp menu
            if (terminals.Count <= 1)
            {
                _dungeonManager.WarpToFloor(1);
                ExploreDungeon();
                return;
            }

            int? selectedFloor = _dungeonUI.SelectEntryPoint(terminals);
            if (selectedFloor.HasValue)
            {
                _dungeonManager.WarpToFloor(selectedFloor.Value);
                ExploreDungeon();
            }
        }

        private void ExploreDungeon()
        {
            // Initial trigger for the floor we just arrived on
            HandleFloorChange(_dungeonManager.ProcessCurrentFloor());

            while (_player.CurrentHP > 0)
            {
                DungeonFloorResult floorInfo = _dungeonManager.ProcessCurrentFloor();
                string action = _dungeonUI.ShowFloorActionMenu(floorInfo, _player);

                if (action == "Cancel") continue;

                switch (action)
                {
                    case "Ascend Stairs":
                        HandleFloorChange(_explorationProcessor.PerformAscension());
                        break;

                    case "Descend Stairs":
                        HandleFloorChange(_explorationProcessor.PerformDescension());
                        break;

                    case "Clock (Heal)":
                        OpenHospitalMenu();
                        break;

                    case "Terminal (Warp)":
                    case "Access Terminal (Return)":
                        int? destination = _dungeonUI.SelectWarpDestination(_dungeonManager.GetUnlockedTerminals(), floorInfo.FloorNumber);
                        if (destination.HasValue)
                        {
                            HandleFloorChange(_explorationProcessor.PerformWarp(destination.Value));
                        }
                        break;

                    case "Inventory":
                        OpenInventoryMenu(inDungeon: true);
                        break;

                    case "Status":
                        OpenSeamlessStatusMenu();
                        break;

                    case "Organize Party":
                        OpenOrganizeMenu();
                        break;

                    case "Return to City":
                        _dungeonState.ResetToEntry();
                        return;

                    case "Barrier (Cannot Pass)":
                        _dungeonUI.ReportBarrierBlocked();
                        break;
                }

                // If a battle occurred and the player died, exit the loop
                if (_player.CurrentHP <= 0) return;

                // If an item usage (Goho-M) reset the dungeon state to 1, exit to city
                if (_dungeonState.CurrentFloor == 1 && action != "Clock (Heal)" && action != "Terminal (Warp)")
                {
                    // If we are at floor 1 and not interacting with lobby objects, we've likely used a Goho-M
                    if (floorInfo.FloorNumber != 1) return;
                }
            }
        }

        private void HandleFloorChange(DungeonFloorResult floorInfo)
        {
            ExplorationEvent result = _explorationProcessor.ProcessFloorEntry(floorInfo);

            if (result == ExplorationEvent.Encounter || result == ExplorationEvent.BossEncounter)
            {
                bool isBoss = (result == ExplorationEvent.BossEncounter);
                List<Combatant> enemies = _explorationProcessor.PrepareEncounter(floorInfo.EnemyIds);

                // Transition to Battle Sub-System
                BattleConductor battle = new BattleConductor(
                    _partyManager,
                    enemies,
                    _inventory,
                    _economy,
                    _io,
                    _playerKnowledge,
                    isBoss);

                battle.StartBattle();

                // Post-Battle Logic
                if (isBoss && !enemies.Any(e => !e.IsDead))
                {
                    _dungeonUI.ReportBossDefeated();
                    _logicEngine.RegisterBossDefeat(floorInfo.EnemyIds.FirstOrDefault());
                }
            }
        }

        #endregion

        #region City Services Logic

        private void OpenCityMenu()
        {
            while (true)
            {
                string choice = _serviceUI.ShowCityServicesMenu();

                if (choice == "Back") return;

                switch (choice)
                {
                    case "Blacksmith (Weapons)":
                        _logicEngine.OpenShop(_player, ShopType.Weapon); // Note: ShopManager logic preserved via engine
                        break;

                    case "Clothing Store (Armor/Boots)":
                        string clothingType = _io.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots", "Back" }, 0) == 0 ? "Armor" : "Boots";
                        if (clothingType == "Armor") _logicEngine.OpenShop(_player, ShopType.Armor);
                        else _logicEngine.OpenShop(_player, ShopType.Boots);
                        break;

                    case "Jeweler (Accessories)":
                        _logicEngine.OpenShop(_player, ShopType.Accessory);
                        break;

                    case "Pharmacy (Items)":
                        _logicEngine.OpenShop(_player, ShopType.Item);
                        break;

                    case "Hospital (Heal)":
                        OpenHospitalMenu();
                        break;
                }
            }
        }

        private void OpenHospitalMenu()
        {
            while (true)
            {
                Combatant patient = _serviceUI.SelectHospitalPatient(_player);
                if (patient == null) return;

                if (!_logicEngine.TryRestoreCombatant(patient))
                {
                    _io.WriteLine("Could not complete treatment.", ConsoleColor.Red);
                    _io.Wait(1000);
                }
                else
                {
                    _io.WriteLine($"{patient.Name} has been fully restored!", ConsoleColor.Green);
                    _io.Wait(800);
                }
            }
        }

        #endregion

        #region System Menus (Inventory/Status)

        private void OpenInventoryMenu(bool inDungeon)
        {
            while (true)
            {
                string choice = _inventoryUI.ShowInventorySubMenu(_player);
                if (choice == "Back") return;

                switch (choice)
                {
                    case "Use Item":
                        ShowItemMenu(inDungeon);
                        break;
                    case "Use Skill":
                        ShowSkillMenu();
                        break;
                    case "Equipment":
                        ShowEquipSlotMenu();
                        break;
                    case "Demons (COMP)":
                        OpenDemonStockMenu();
                        break;
                }
            }
        }

        private void ShowItemMenu(bool inDungeon)
        {
            ItemData selectedItem = _inventoryUI.SelectItem(_player, inDungeon);
            if (selectedItem == null) return;

            Combatant target = _inventoryUI.SelectFieldTarget(_player, selectedItem.Name);
            if (target == null) return;

            _logicEngine.ExecuteItemUsage(selectedItem, _player, target);
        }

        private void ShowSkillMenu()
        {
            Combatant performer = _inventoryUI.SelectSkillPerformer(_player);
            if (performer == null) return;

            SkillData selectedSkill = _inventoryUI.SelectFieldSkill(performer);
            if (selectedSkill == null) return;

            Combatant target = _inventoryUI.SelectFieldTarget(_player, selectedSkill.Name);
            if (target == null) return;

            _logicEngine.ExecuteSkillUsage(selectedSkill, performer, target);
        }

        private void OpenSeamlessStatusMenu()
        {
            while (true)
            {
                string choice = _statusUI.ShowStatusHub(_player);
                if (choice == "Back") return;

                switch (choice)
                {
                    case "Allocate Stats":
                        OpenStatAllocation();
                        break;
                    case "Change Equipment":
                        ShowEquipSlotMenu();
                        break;
                    case "Persona Stock":
                        OpenPersonaStockMenu();
                        break;
                    case "Demon Stock":
                        OpenDemonStockMenu();
                        break;
                }
            }
        }

        private void OpenStatAllocation()
        {
            // Stat allocation loop logic shifted to logic engine
            while (_player.StatPoints > 0)
            {
                StatType? selected = _logicEngine.PromptStatAllocation(_player); // Logic engine uses RenderMenu internally for this specialized module
                if (selected == null) break;
                _logicEngine.AllocateStatPoint(_player, selected.Value);
            }
        }

        private void ShowEquipSlotMenu()
        {
            while (true)
            {
                string slot = _statusUI.ShowEquipSlotMenu(_player);
                if (slot == "Back") return;

                ShopCategory category = slot switch
                {
                    string s when s.Contains("Weapon") => ShopCategory.Weapon,
                    string s when s.Contains("Armor") => ShopCategory.Armor,
                    string s when s.Contains("Boots") => ShopCategory.Boots,
                    _ => ShopCategory.Accessory
                };

                List<string> ids = category switch
                {
                    ShopCategory.Weapon => _inventory.OwnedWeapons,
                    ShopCategory.Armor => _inventory.OwnedArmor,
                    ShopCategory.Boots => _inventory.OwnedBoots,
                    _ => _inventory.OwnedAccessories
                };

                string selectedId = _serviceUI.SelectEquipmentFromInventory(_player, ids, category);
                if (selectedId != "Back")
                {
                    _logicEngine.PerformEquip(_player, selectedId, category);
                }
            }
        }

        private void OpenPersonaStockMenu()
        {
            while (true)
            {
                Persona selected = _statusUI.SelectPersonaFromStock(_player);
                if (selected == null) return;

                bool isEquipped = (selected == _player.ActivePersona);
                string action = _statusUI.ShowPersonaDetails(selected, isEquipped);

                if (action == "Equip Persona")
                {
                    _logicEngine.PerformPersonaSwap(_player, selected);
                }
            }
        }

        private void OpenDemonStockMenu()
        {
            while (true)
            {
                Combatant selected = _statusUI.SelectDemonFromStock(_player);
                if (selected == null) return;

                _statusUI.ShowDemonDetails(selected);
            }
        }

        private void OpenOrganizeMenu()
        {
            while (true)
            {
                int slotIndex = _statusUI.ShowOrganizationSlots();
                if (slotIndex == -1) return;

                if (slotIndex < _partyManager.ActiveParty.Count)
                {
                    Combatant member = _partyManager.ActiveParty[slotIndex];
                    string action = _statusUI.ShowMemberManagementMenu(member);
                    if (action == "Return to COMP")
                    {
                        if (_partyManager.ReturnDemon(_player, member))
                        {
                            _io.WriteLine($"{member.Name} returned to stock.");
                            _io.Wait(600);
                        }
                    }
                }
                else
                {
                    Combatant target = _statusUI.SelectSummonTarget(_player);
                    if (target != null)
                    {
                        if (_partyManager.SummonDemon(_player, target))
                        {
                            _io.WriteLine($"{target.Name} joined the party!");
                            _io.Wait(800);
                        }
                    }
                }
            }
        }

        #endregion
    }
}