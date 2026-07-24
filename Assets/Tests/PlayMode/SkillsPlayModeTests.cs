using System.Collections;
using NUnit.Framework;
using SLG.Grid;
using SLG.Skills;
using SLG.Tests.Utilities;
using SLG.Units;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class SkillsPlayModeTests
    {
        [UnityTest]
        public IEnumerator DamageSkill_DamagesEnemyWithoutCounterattackAndCommitsAction()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Skills", loaded => fixture = loaded);
            Unit mage = fixture["mage"];
            Unit enemy = fixture["enemy"];
            SkillDefinition fire = fixture.Skill("fire");
            int expectedDamage = SkillResolver.CalculateDamage(mage, fire, enemy);
            int mageBefore = mage.CurrentHealth;

            Assert.That(fixture.Player.TrySelectUnit(mage), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryOpenSkills(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseSkill(fire), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingSkillTarget));
            Assert.That(fixture.Player.TryChooseUnitTarget(enemy), Is.True, fixture.DumpState());

            Assert.That(enemy.CurrentHealth, Is.EqualTo(10 - expectedDamage));
            Assert.That(mage.CurrentHealth, Is.EqualTo(mageBefore), "Skills do not counterattack in this milestone.");
            Assert.That(mage.HasActed, Is.True);
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
        }

        [UnityTest]
        public IEnumerator DamageSkill_InvalidTargetsAndCancellation_DoNotConsumeAction()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Skills", loaded => fixture = loaded);
            Unit mage = fixture["mage"];
            SkillDefinition fire = fixture.Skill("fire");

            Assert.That(fixture.Player.TrySelectUnit(mage), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseMovementTile(fixture.Tile(1, 2)), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, fixture.DumpState);
            Assert.That(fixture.Player.TryOpenSkills(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseSkill(fire), Is.True, fixture.DumpState());

            Assert.That(fixture.Player.TryChooseUnitTarget(fixture["areaAlly"]), Is.False, "Ally is invalid for damage skill.");
            Assert.That(fixture.Player.TryChooseUnitTarget(fixture["outsideEnemy"]), Is.False, "Out-of-range enemy is invalid.");
            Assert.That(mage.HasActed, Is.False);

            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingSkill));
            Assert.That(fixture.Player.SelectedSkill, Is.SameAs(fire));
            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.ChoosingAction));
            Assert.That(fixture.Player.SelectedSkill, Is.Null);
            Assert.That(fixture.Player.TryCancel(), Is.True, fixture.DumpState());
            yield return PlayModeTestUtility.WaitUntilOrFail(() => fixture.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingMovement, 5f, fixture.DumpState);
            Assert.That(mage.HasActed, Is.False);
            Assert.That(mage.OccupiedTile, Is.SameAs(fixture.Tile(0, 2)));
        }

        [UnityTest]
        public IEnumerator HealSkill_RestoresDamagedAllyAndRejectsInvalidTargets()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Skills", loaded => fixture = loaded);
            Unit healer = fixture["healer"];
            Unit ally = fixture["damagedAlly"];
            SkillDefinition heal = fixture.Skill("heal");

            Assert.That(fixture.Player.TrySelectUnit(healer), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryOpenSkills(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseSkill(heal), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseUnitTarget(fixture["enemy"]), Is.False, "Enemy is invalid for heal.");
            Assert.That(fixture.Player.TryChooseUnitTarget(healer), Is.False, "Full-health self is invalid under current healing rule.");
            Assert.That(fixture.Player.TryChooseUnitTarget(ally), Is.True, fixture.DumpState());

            Assert.That(ally.CurrentHealth, Is.EqualTo(10));
            Assert.That(healer.HasActed, Is.True);
            Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
        }

        [UnityTest]
        public IEnumerator CrossAreaSkill_DamagesOnlyEnemies_ClipsEdgesAndReleasesDeadOccupancy()
        {
            BattleTestFixture fixture = null;
            yield return PlayModeTestUtility.LoadFixture("Test_Skills", loaded => fixture = loaded);
            Unit mage = fixture["mage"];
            Unit enemy = fixture["enemy"];
            Unit areaEnemy = fixture["areaEnemy"];
            Unit outside = fixture["outsideEnemy"];
            Unit ally = fixture["areaAlly"];
            SkillDefinition cross = fixture.Skill("cross");
            Tile center = fixture.Tile(3, 2);
            Tile edge = fixture.Tile(4, 4);
            var areaEnemyTile = areaEnemy.OccupiedTile;

            areaEnemy.ReceiveDamage(7);
            Assert.That(fixture.Player.TrySelectUnit(mage), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseStay(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryOpenSkills(), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseSkill(cross), Is.True, fixture.DumpState());
            Assert.That(fixture.Player.TryChooseGroundTarget(edge), Is.False, "Out-of-range center is rejected.");
            Assert.That(fixture.Player.TryChooseGroundTarget(center), Is.True, fixture.DumpState());

            Assert.That(enemy.CurrentHealth, Is.EqualTo(4));
            Assert.That(areaEnemy.IsAlive, Is.False);
            Assert.That(areaEnemyTile.OccupyingUnit, Is.Null);
            Assert.That(outside.CurrentHealth, Is.EqualTo(10));
            Assert.That(ally.CurrentHealth, Is.EqualTo(10));
            Assert.That(mage.HasActed, Is.True);
        }
    }
}
