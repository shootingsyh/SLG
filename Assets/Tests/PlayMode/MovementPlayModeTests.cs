using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Grid;
using SLG.Tests.Utilities;
using SLG.Units;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class MovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerSelection_RejectsActedEnemiesAndWrongPhase()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementRollback", loaded => fixture = loaded);
            Unit mover = fixture["mover"];
            Unit ally = fixture["ally"];
            Unit enemy = fixture["enemy"];

            Assert.That(fixture.Player.TrySelectUnit(mover), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingMovement));
            Assert.That(fixture.Player.TrySelectUnit(ally), Is.False, "Cannot select another unit while one is active.");
            fixture.Player.TryCancel();

            Assert.That(fixture.Player.TrySelectUnit(enemy), Is.False, "Enemy selection is rejected during player phase.");
            mover.SetHasActed(true);
            Assert.That(fixture.Player.TrySelectUnit(mover), Is.False, "Acted player unit is rejected.");

            mover.SetHasActed(false);
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TrySelectUnit(mover), Is.False, "Selection is rejected outside player phase.");
        }

        [UnityTest]
        public IEnumerator StayInPlace_ThenWait_CommitsActionWithoutMoving()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementRollback", loaded => fixture = loaded);
            Unit mover = fixture["mover"];
            Tile start = mover.OccupiedTile;

            Assert.That(fixture.Player.TrySelectUnit(mover), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(mover.OccupiedTile, Is.SameAs(start));
            Assert.That(mover.HasActed, Is.False);

            Assert.That(fixture.Player.TryWait(), Is.True, fixture.DumpState());

            Assert.That(mover.HasActed, Is.True);
            Assert.That(mover.OccupiedTile, Is.SameAs(start));
            Assert.That(start.OccupyingUnit, Is.SameAs(mover));
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
            Assert.That(fixture.Player.OriginalTile, Is.Null);
            Assert.That(fixture.Player.HasProvisionalMovement, Is.False);
        }

        [UnityTest]
        public IEnumerator ReachableMovement_ThenWait_CommitsOccupancy()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementRollback", loaded => fixture = loaded);
            Unit mover = fixture["mover"];
            Tile start = mover.OccupiedTile;
            Tile destination = fixture.Tile(2, 1);

            Assert.That(fixture.Player.TrySelectUnit(mover), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(destination), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Moving));

            yield return PlayModeTestUtility.WaitUntilOrFail(
                () => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction,
                5f,
                fixture.DumpState);

            Assert.That(mover.OccupiedTile, Is.SameAs(destination));
            Assert.That(destination.OccupyingUnit, Is.SameAs(mover));
            Assert.That(start.OccupyingUnit, Is.Null);
            Assert.That(fixture.Player.HasProvisionalMovement, Is.True);

            Assert.That(fixture.Player.TryWait(), Is.True, fixture.DumpState());
            Assert.That(mover.HasActed, Is.True);
            Assert.That(destination.OccupyingUnit, Is.SameAs(mover));
            Assert.That(fixture.Player.OriginalTile, Is.Null);
        }

        [UnityTest]
        public IEnumerator InvalidMovement_DoesNotMoveOrAct()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementAndTerrain", loaded => fixture = loaded);
            Unit ground = fixture["ground"];
            Tile start = ground.OccupiedTile;

            Assert.That(fixture.Player.TrySelectUnit(ground), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(fixture.Tile(2, 2)), Is.False, "Ground cannot enter water.");
            Assert.That(fixture.Player.TryChooseMovementTile(fixture.Tile(3, 2)), Is.False, "Ground cannot enter wall.");
            Assert.That(fixture.Player.TryChooseMovementTile(fixture["enemy"].OccupiedTile), Is.False, "Occupied destination is rejected.");

            Assert.That(ground.OccupiedTile, Is.SameAs(start));
            Assert.That(ground.HasActed, Is.False);
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingMovement));
        }

        [UnityTest]
        public IEnumerator ProvisionalMovement_CancelRollsBack_ThenSecondMoveCommits()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_MovementRollback", loaded => fixture = loaded);
            Unit mover = fixture["mover"];
            Tile original = mover.OccupiedTile;
            Tile first = fixture.Tile(2, 1);
            Tile second = fixture.Tile(1, 1);

            Assert.That(fixture.Player.TrySelectUnit(mover), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(first), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, fixture.DumpState);
            Assert.That(fixture.Turns.TryEndPlayerTurn(), Is.False, "End turn is rejected while movement is provisional.");
            Assert.That(fixture.Player.TrySelectUnit(fixture["ally"]), Is.False);

            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ReturningToOriginalTile));
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingMovement, 5f, fixture.DumpState);

            Assert.That(mover.OccupiedTile, Is.SameAs(original));
            Assert.That(original.OccupyingUnit, Is.SameAs(mover));
            Assert.That(first.OccupyingUnit, Is.Null);

            Assert.That(fixture.Player.TryChooseMovementTile(second), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, fixture.DumpState);
            Assert.That(fixture.Player.TryWait(), Is.True, fixture.DumpState());
            Assert.That(second.OccupyingUnit, Is.SameAs(mover));
        }
    }
}
