using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Tests.Utilities;
using SLG.Units;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class CombatPlayModeTests
    {
        [UnityTest]
        public IEnumerator NormalAttack_UsesTerrainDefenseCounterattackAndCommitsAction()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Combat", loaded => fixture = loaded);
            Unit attacker = fixture["attacker"];
            Unit defender = fixture["defender"];
            int defenderBefore = defender.CurrentHealth;
            int attackerBefore = attacker.CurrentHealth;
            CombatPreview preview = CombatResolver.BuildPreview(attacker, defender);

            Assert.That(fixture.Player.TrySelectUnit(attacker), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingAttackTarget));
            Assert.That(fixture.Player.TryChooseUnitTarget(defender), Is.True, fixture.DumpState());

            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.Idle, 5f, fixture.DumpState);

            Assert.That(defender.CurrentHealth, Is.EqualTo(defenderBefore - preview.AttackerDamage));
            Assert.That(attacker.CurrentHealth, Is.EqualTo(attackerBefore - preview.CounterDamage));
            Assert.That(preview.DefenderTerrainDefenseBonus, Is.EqualTo(2));
            Assert.That(attacker.HasActed, Is.True);
            Assert.That(fixture.Player.OriginalTile, Is.Null);
        }

        [UnityTest]
        public IEnumerator InvalidAttackTargets_DoNotConsumeActionOrCommitMovement()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Combat", loaded => fixture = loaded);
            Unit attacker = fixture["attacker"];
            Unit farEnemy = fixture["farEnemy"];
            Unit defender = fixture["defender"];

            Assert.That(fixture.Player.TrySelectUnit(attacker), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());

            Assert.That(fixture.Player.TryChooseUnitTarget(attacker), Is.False, "Ally/self target is rejected.");
            Assert.That(fixture.Player.TryChooseUnitTarget(farEnemy), Is.False, "Out-of-range target is rejected.");
            defender.ReceiveDamage(99);
            Assert.That(fixture.Player.TryChooseUnitTarget(defender), Is.False, "Dead target is rejected.");

            Assert.That(attacker.HasActed, Is.False);
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingAttackTarget));
        }

        [UnityTest]
        public IEnumerator UnitDeath_FromNormalAttack_ReleasesOccupancy()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Combat", loaded => fixture = loaded);
            Unit attacker = fixture["attacker"];
            Unit fragile = fixture["fragileEnemy"];
            var fragileTile = fragile.OccupiedTile;

            Assert.That(fixture.Player.TrySelectUnit(attacker), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(fixture.Tile(0, 2)), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, fixture.DumpState);
            Assert.That(fixture.Player.TryChooseAttack(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseUnitTarget(fragile), Is.True, fixture.DumpState());

            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.Idle, 5f, fixture.DumpState);

            Assert.That(fragile.IsAlive, Is.False);
            Assert.That(fragileTile.OccupyingUnit, Is.Null);
            Assert.That(attacker.HasActed, Is.True);
        }
    }
}
