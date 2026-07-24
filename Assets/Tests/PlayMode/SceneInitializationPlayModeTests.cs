using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Tests.Utilities;
using SLG.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class SceneInitializationPlayModeTests
    {
        [UnityTest]
        public IEnumerator TestScenes_LoadAndInitializeFixtures()
        {
            string[] scenes =
            {
                "Test_MovementAndTerrain",
                "Test_MovementRollback",
                "Test_Combat",
                "Test_Skills",
                "Test_TurnAndAI",
                "Test_BattleEnd"
            };

            for (int i = 0; i < scenes.Length; i++)
            {
                BattleTestFixture fixture = null;
                yield return PlayModeTestUtility.LoadFixture(scenes[i], loaded => fixture = loaded);

                Assert.That(Object.FindObjectsByType<BattleTestFixture>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1));
                Assert.That(fixture.Turns.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn), fixture.DumpState());
                Assert.That(fixture.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle), fixture.DumpState());
                Assert.That(fixture.Turns.IsBattleEnded, Is.False, fixture.DumpState());
                Assert.That(fixture.Turns.CountLivingUnits(UnitFaction.Player), Is.GreaterThan(0), fixture.DumpState());
                Assert.That(fixture.Turns.CountLivingUnits(UnitFaction.Enemy), Is.GreaterThan(0), fixture.DumpState());
            }
        }
    }
}
