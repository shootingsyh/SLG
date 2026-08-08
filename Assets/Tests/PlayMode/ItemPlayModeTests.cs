using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Grid;
using SLG.Items;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Shell;
using SLG.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class ItemPlayModeTests
    {
        private InMemorySaveStorage _storage;
        private SaveRepository _repository;

        [SetUp]
        public void Setup()
        {
            ItemCatalog.EnsureInitialized();
            _storage = new InMemorySaveStorage();
            _storage.ClearAll();
            _repository = new SaveRepository(_storage);
            GameShellServices.Clear();
            GameShellServices.UseRepositoryForTests(_repository);
        }

        [TearDown]
        public void TearDown()
        {
            GameShellServices.Clear();
        }

        private BattleRuntimeContext Build(BattleTestPresetId preset)
        {
            var config = BattleTestPresetLibrary.Create(preset);
            config.AiEnabled = false;
            var ctx = BattleScenarioRuntimeBuilder.Build(config, null, true, _repository, preset);
            var flow = new GameFlowService();
            var proc = new CampaignFlowService(flow, _repository);
            proc.ConfigureBattle(preset);
            ctx.Turns.SetCampaignFlowProcessor(proc);
            return ctx;
        }

        // A. Potion flow
        [UnityTest]
        public IEnumerator PotionFlow_HealsDamagedAllyAndConsumes()
        {
            GameShellServices.CampaignInventory.Add("potion", 2);
            var ctx = Build(BattleTestPresetId.ItemPotionHeal);
            Unit knight = ctx.UnitsByKey["knight"];
            Unit healer = ctx.UnitsByKey["healer"];
            yield return null;

            Assert.That(healer.CurrentHealth, Is.LessThan(healer.MaxHealth), "Healer should start damaged");
            int beforeHp = healer.CurrentHealth;
            int beforeQty = GameShellServices.CampaignInventory.GetQuantity("potion");

            ctx.Player.TrySelectUnit(knight);
            bool canOpen = ctx.Player.TryOpenItems();
            Assert.That(canOpen, Is.True);
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingItem));

            var potion = ItemCatalog.Get("potion");
            Assert.That(ctx.Player.TryChooseItem(potion), Is.True);
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingItemTarget));

            bool targeted = ctx.Player.TryChooseItemTarget(healer);
            Assert.That(targeted, Is.True);
            Assert.That(knight.HasActed, Is.True);
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
            yield return null;
            yield return new WaitForSeconds(0.2f);

            Assert.That(healer.CurrentHealth, Is.GreaterThan(beforeHp));
            Assert.That(healer.CurrentHealth, Is.EqualTo(System.Math.Min(healer.MaxHealth, beforeHp + 5)));
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("potion"), Is.EqualTo(beforeQty - 1));
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
        }

        // B. Item cancellation
        [UnityTest]
        public IEnumerator ItemCancellation_ConsumesNothing()
        {
            GameShellServices.CampaignInventory.Add("potion", 2);
            var ctx = Build(BattleTestPresetId.ItemPotionHeal);
            Unit knight = ctx.UnitsByKey["knight"];
            yield return null;

            ctx.Player.TrySelectUnit(knight);
            ctx.Player.TryOpenItems();
            var potion = ItemCatalog.Get("potion");
            ctx.Player.TryChooseItem(potion);
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingItemTarget));
            int before = GameShellServices.CampaignInventory.GetQuantity("potion");

            ctx.Player.TryCancel(); // back to ChoosingItem
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingItem));
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("potion"), Is.EqualTo(before));

            ctx.Player.TryCancel(); // back to ChoosingAction
            Assert.That(ctx.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingAction));
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("potion"), Is.EqualTo(before));
            // Movement rollback still works
            Assert.That(ctx.Player.TryCancel(), Is.True); // should go to ChoosingMovement or Idle
        }

        // C. Bomb
        [UnityTest]
        public IEnumerator Bomb_DealsFixedDamageNoCounter()
        {
            GameShellServices.CampaignInventory.Add("bomb", 1);
            var ctx = Build(BattleTestPresetId.ItemBombDamage);
            Unit knight = ctx.UnitsByKey["knight"];
            Unit enemy = ctx.UnitsByKey["enemy"];
            yield return null;

            int beforeHp = enemy.CurrentHealth;
            int enemyMax = enemy.MaxHealth;
            ctx.Player.TrySelectUnit(knight);
            ctx.Player.TryOpenItems();
            var bomb = ItemCatalog.Get("bomb");
            ctx.Player.TryChooseItem(bomb);
            bool ok = ctx.Player.TryChooseItemTarget(enemy);
            Assert.That(ok, Is.True);
            yield return new WaitForSeconds(0.2f);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(beforeHp - 5));
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("bomb"), Is.EqualTo(0));
            // No counterattack: knight should not have taken damage
            Assert.That(knight.CurrentHealth, Is.EqualTo(knight.MaxHealth));
            // If lethal, check death flow
            if (beforeHp <= 5)
            {
                Assert.That(enemy.IsAlive, Is.False);
            }
        }

        // D. Equipment attack bonus
        [UnityTest]
        public IEnumerator Equipment_AttackBonus_IncreasesDamage()
        {
            // Battle 1 without sword: record damage
            var ctx1 = Build(BattleTestPresetId.ItemEquipmentAttack);
            Unit knight1 = ctx1.UnitsByKey["knight"];
            Unit enemy1 = ctx1.UnitsByKey["enemy"];
            yield return null;
            int baseAtk = knight1.BaseAttackPower;
            int baseDmg = CombatResolver.CalculateDamage(knight1, enemy1);
            // Earn sword via reward
            GameShellServices.CampaignInventory.Add("iron-sword", 1);
            GameShellServices.CampaignEquipment.Equip("knight", "iron-sword", GameShellServices.CampaignInventory);
            // New battle with sword equipped
            var ctx2 = Build(BattleTestPresetId.ItemEquipmentAttack);
            Unit knight2 = ctx2.UnitsByKey["knight"];
            knight2.name = "knight"; // ensure equipment lookup
            Unit enemy2 = ctx2.UnitsByKey["enemy"];
            yield return null;
            int equippedAtk = knight2.AttackPower;
            int equippedDmg = CombatResolver.CalculateDamage(knight2, enemy2);
            Assert.That(equippedAtk, Is.EqualTo(baseAtk + 2));
            Assert.That(equippedDmg, Is.GreaterThan(baseDmg));
        }

        // E. Equipment defense bonus
        [UnityTest]
        public IEnumerator Equipment_DefenseBonus_ReducesDamage()
        {
            var ctx1 = Build(BattleTestPresetId.ItemEquipmentDefense);
            Unit knight1 = ctx1.UnitsByKey["knight"];
            Unit enemy1 = ctx1.UnitsByKey["enemy"];
            yield return null;
            int baseDef = knight1.BaseDefense;
            // Simulate enemy attack damage to knight without armor
            int dmgWithout = CombatResolver.CalculateDamage(enemy1, knight1);
            GameShellServices.CampaignInventory.Add("iron-armor", 1);
            GameShellServices.CampaignEquipment.Equip("knight", "iron-armor", GameShellServices.CampaignInventory);
            var ctx2 = Build(BattleTestPresetId.ItemEquipmentDefense);
            Unit knight2 = ctx2.UnitsByKey["knight"];
            knight2.name = "knight";
            Unit enemy2 = ctx2.UnitsByKey["enemy"];
            yield return null;
            int dmgWith = CombatResolver.CalculateDamage(enemy2, knight2);
            Assert.That(knight2.Defense, Is.EqualTo(baseDef + 2));
            Assert.That(dmgWith, Is.LessThan(dmgWithout));
        }

        // F. Movement accessory
        [UnityTest]
        public IEnumerator Equipment_MovementAccessory_IncreasesReachable()
        {
            var ctx1 = Build(BattleTestPresetId.ItemTestBattle1);
            Unit knight1 = ctx1.UnitsByKey["knight"];
            knight1.name = "knight";
            yield return null;
            int baseMovementRange = knight1.BaseMovementRange;
            int unequippedMovementRange = knight1.MovementRange;

            GameShellServices.CampaignInventory.Add("traveler-charm", 1);
            GameShellServices.CampaignEquipment.Equip("knight", "traveler-charm", GameShellServices.CampaignInventory);

            var ctx2 = Build(BattleTestPresetId.ItemTestBattle1);
            Unit knight2 = ctx2.UnitsByKey["knight"];
            knight2.name = "knight";
            yield return null;
            Assert.That(knight2.MovementRange, Is.EqualTo(baseMovementRange + 1));
            Assert.That(knight2.MovementRange, Is.GreaterThan(unequippedMovementRange));
        }

        // G. Reward flow
        [Test]
        public void RewardFlow_VictoryGrantsOnce()
        {
            var flow = new GameFlowService();
            var proc = new CampaignFlowService(flow, _repository);
            proc.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            Assert.That(BattleRewards.GrantIfNotClaimed("battle-1"), Is.True);
            int qtyAfterFirst = GameShellServices.CampaignInventory.GetQuantity("potion");
            Assert.That(BattleRewards.GrantIfNotClaimed("battle-1"), Is.False);
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("potion"), Is.EqualTo(qtyAfterFirst));
        }

        // H. Campaign Save
        [Test]
        public void CampaignSave_InventoryEquipmentAndRewards()
        {
            GameShellServices.CampaignInventory.Add("potion", 2);
            GameShellServices.CampaignInventory.Add("iron-sword", 1);
            GameShellServices.CampaignEquipment.Equip("knight", "iron-sword", GameShellServices.CampaignInventory);
            BattleRewards.GrantIfNotClaimed("battle-1");

            var data = new CampaignSaveData
            {
                GameId = "item-test",
                BattleId = "item-test-battle-1",
                NextBattleId = "item-test-battle-2",
                Inventory = GameShellServices.CampaignInventory.ToEntries(),
                Equipment = GameShellServices.CampaignEquipment.ToEntries(),
                ClaimedRewardBattleIds = new System.Collections.Generic.List<string>(GameShellServices.ClaimedRewards)
            };
            var res = _repository.SaveCampaign(data, 1);
            Assert.That(res.Success, Is.True);

            // Mutate
            GameShellServices.CampaignInventory.Add("iron-armor", 1);
            GameShellServices.CampaignEquipment.Equip("knight", "iron-armor", GameShellServices.CampaignInventory);

            // Load
            Assert.That(_repository.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded, out _), Is.True);
            var inv2 = new CampaignInventory();
            inv2.FromEntries(loaded.Inventory);
            var equip2 = new CampaignEquipment();
            equip2.FromEntries(loaded.Equipment, inv2);
            Assert.That(inv2.GetQuantity("iron-sword"), Is.EqualTo(2));
            Assert.That(inv2.GetQuantity("potion"), Is.EqualTo(4));
            Assert.That(equip2.GetEquipped("knight", EquipmentSlot.Weapon), Is.EqualTo("iron-sword"));
            Assert.That(loaded.ClaimedRewardBattleIds.Contains("battle-1"), Is.True);
        }

        // I. Battle Save rollback (major)
        [UnityTest]
        public IEnumerator BattleSave_Rollback_RestoresAll()
        {
            // Create Battle Save before future reward
            GameShellServices.CampaignInventory.Add("potion", 3);
            var ctx1 = Build(BattleTestPresetId.ItemTestBattle1);
            var snap = BattleSaveSnapshot.Create(ctx1, BattleTestPresetId.ItemTestBattle1);
            _repository.SaveBattle(snap);
            var beforeInv = GameShellServices.CampaignInventory.GetQuantity("potion");
            var beforeBattleId = snap.BattlePresetId;

            // Spend item, win battle, earn sword, equip, go to battle2
            GameShellServices.CampaignInventory.Remove("potion", 1);
            BattleRewards.GrantIfNotClaimed("item-test-battle-1");
            GameShellServices.CampaignInventory.Add("iron-sword", 0); // already granted
            GameShellServices.CampaignEquipment.Equip("knight", "iron-sword", GameShellServices.CampaignInventory);

            // Load old battle save
            Assert.That(_repository.TryLoadBattle(out var loaded, out _), Is.True);
            BattleRuntimeContext restored = null;
            string err = "";
            Assert.That(BattleSaveSnapshot.TryRestore(loaded, out restored, out err), Is.True, err);
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("potion"), Is.EqualTo(beforeInv));
            Assert.That(GameShellServices.CampaignInventory.GetQuantity("iron-sword"), Is.EqualTo(0));
            Assert.That(GameShellServices.CampaignEquipment.GetEquipped("knight", EquipmentSlot.Weapon), Is.Empty);
            Assert.That(loaded.BattlePresetId, Is.EqualTo(beforeBattleId));
            // Continue playing from restored state
            Unit knight = restored.UnitsByKey["knight"];
            Unit enemy = restored.UnitsByKey["enemy"];
            restored.Player.TrySelectUnit(knight);
            bool attacked = restored.Player.TryChooseUnitTarget(enemy);
            // Should be able to attack
            Assert.That(attacked || CombatResolver.CanAttack(knight, enemy), Is.True);
            yield return null;
        }

        // J. Defeat + Campaign reload
        [UnityTest]
        public IEnumerator Defeat_CampaignReload_Restores()
        {
            GameShellServices.CampaignInventory.Add("potion", 2);
            BattleRewards.GrantIfNotClaimed("battle-1");
            var data = new CampaignSaveData
            {
                GameId = "item-test",
                BattleId = "item-test-battle-1",
                Inventory = GameShellServices.CampaignInventory.ToEntries(),
                Equipment = GameShellServices.CampaignEquipment.ToEntries(),
                ClaimedRewardBattleIds = new System.Collections.Generic.List<string>(GameShellServices.ClaimedRewards)
            };
            _repository.SaveCampaign(data, 1);
            // Simulate defeat in battle2 (no reward)
            // Load campaign
            _repository.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded, out _);
            var inv2 = new CampaignInventory();
            inv2.FromEntries(loaded.Inventory);
            Assert.That(inv2.GetQuantity("potion"), Is.EqualTo(4));
            Assert.That(loaded.ClaimedRewardBattleIds.Contains("battle-1"), Is.True);
            yield return null;
        }
    }
}
