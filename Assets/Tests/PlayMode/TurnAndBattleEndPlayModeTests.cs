using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Tests.Utilities;
using SLG.Units;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class TurnAndBattleEndPlayModeTests
    {
        [UnityTest]
        public IEnumerator EndTurn_IsRejectedWhileFSMHasPendingAction_AndAcceptedFromIdle()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementRollback", loaded => fixture = loaded);
            Unit mover = fixture["mover"];

            Assert.That(fixture.Player.TryWait(), Is.False, "Wait is rejected outside ChoosingAction.");
            Assert.That(fixture.Player.TrySelectUnit(mover), Is.True, fixture.DumpState());
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.False, "End turn rejected during movement selection.");
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.False, "End turn rejected during action menu.");
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.False, "End turn rejected during attack targeting.");
            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingMovement, 5f, fixture.DumpState);
            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());

            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.True, fixture.DumpState());
            Assert.That(fixture.Turns.CurrentPhase, Is.EqualTo(BattlePhase.EnemyTurn));
        }

        [UnityTest]
        public IEnumerator TurnFlow_EnemyAI_MovesAttacksOnceAndReturnsToPlayerTurn()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_TurnAndAI", loaded => fixture = loaded);
            Unit playerA = fixture["playerA"];
            Unit playerB = fixture["playerB"];
            Unit enemy = fixture["enemy"];
            var enemyStart = enemy.OccupiedTile;
            int playerABefore = playerA.CurrentHealth;

            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.True, fixture.DumpState());
            Assert.That(fixture.Turns.CurrentPhase, Is.EqualTo(BattlePhase.EnemyTurn));

            yield return PlayModeTestUtility.WaitUntilOrFail(
                () => fixture.Turns.CurrentPhase == BattlePhase.PlayerTurn && !fixture.Turns.IsEnemyActing,
                10f,
                fixture.DumpState);

            Assert.That(enemy.OccupiedTile, Is.Not.SameAs(enemyStart), "Enemy should move toward the player.");
            Assert.That(enemy.OccupiedTile.OccupyingUnit, Is.SameAs(enemy));
            Assert.That(playerA.CurrentHealth, Is.LessThan(playerABefore), "AI should attack the clear nearest target.");
            Assert.That(playerA.HasActed, Is.False, "Living players reset when player turn begins.");
            Assert.That(playerB.HasActed, Is.False);
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
        }

        [UnityTest]
        public IEnumerator Victory_KillingFinalEnemy_EndsBattleAndRejectsCommands()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_BattleEnd", loaded => fixture = loaded);
            Unit hero = fixture["hero"];
            Unit enemy = fixture["lastEnemy"];

            Assert.That(fixture.Player.TrySelectUnit(hero), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(fixture.Tile(1, 2)), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, fixture.DumpState);
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseUnitTarget(enemy), Is.True, fixture.DumpState());

            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Turns.IsBattleEnded, 5f, fixture.DumpState);

            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(fixture.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.BattleEnded));
            Assert.That(fixture.Player.TrySelectUnit(fixture["fragileHero"]), Is.False);
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.False);
        }

        [UnityTest]
        public IEnumerator Defeat_FromCounterattackKillingFinalPlayer_EndsBattle()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_BattleEnd", loaded => fixture = loaded);
            Unit hero = fixture["hero"];
            Unit fragileHero = fixture["fragileHero"];
            Unit enemy = fixture["lastEnemy"];

            hero.ReceiveDamage(99);
            Assert.That(hero.IsAlive, Is.False);
            Assert.That(fixture.Player.TrySelectUnit(fragileHero), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseUnitTarget(enemy), Is.True, fixture.DumpState());

            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Turns.IsBattleEnded, 5f, fixture.DumpState);

            Assert.That(fragileHero.IsAlive, Is.False);
            Assert.That(fixture.Turns.BattleResult, Is.EqualTo("Defeat"));
            Assert.That(fixture.Turns.CountLivingUnits(UnitFaction.Player), Is.EqualTo(0));
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.BattleEnded));
        }

        [UnityTest]
        public IEnumerator LoadingSameSceneRepeatedly_DoesNotDuplicateRegistrationOrState()
        {
            for (int i = 0; i < 2; i++)
            {
                BattleTestFixture fixture = null;
                yield return PlayModeTestUtility.LoadFixture("Test_TurnAndAI", loaded => fixture = loaded);
                Assert.That(fixture.Turns.CountLivingUnits(UnitFaction.Player), Is.EqualTo(2), fixture.DumpState());
                Assert.That(fixture.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(1), fixture.DumpState());
                Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
            }
        }
    }
}
