using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Scenarios;
using SLG.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class ScenarioPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeConfigurationInitialization_AppliesPresetAndStartsBattle()
        {
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.FullScenarioSmoke, c => context = c);

            Assert.That(context.Grid.Width, Is.EqualTo(5));
            Assert.That(context.UnitsByKey.ContainsKey("knight"), Is.True);
            Assert.That(context.UnitsByKey.ContainsKey("healer"), Is.True);
            Assert.That(context.UnitsByKey.ContainsKey("mage"), Is.True);
            Assert.That(context.UnitsByKey["mage"].Skills.Count, Is.GreaterThan(0));
            Assert.That(context.UnitsByKey["knight"].OccupiedTile, Is.Not.Null);
            Assert.That(context.Scenario.Configuration.Objectives.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(context.Scenario.Configuration.Reinforcements.Count, Is.EqualTo(2));
            Assert.That(context.Turns.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
            Assert.That(Object.FindObjectsByType<BattleTurnController>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BattleTestLabAndTemplateScenes_Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/BattleTestLab.unity", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<BattleTestLabController>(), Is.Not.Null);

            BattleTestLabSession.Store(BattleTestPresetId.FullScenarioSmoke, BattleTestPresetLibrary.Create(BattleTestPresetId.FullScenarioSmoke));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/BattleTestTemplate.unity", LoadSceneMode.Single);
            yield return PlayModeTestUtility.WaitUntilOrFail(() => Object.FindAnyObjectByType<BattleScenarioController>() != null, 5f, () => "Scenario controller did not initialize in template scene.");
            BattleScenarioController scenario = Object.FindAnyObjectByType<BattleScenarioController>();
            Assert.That(scenario.HasConfiguration, Is.True);
            Assert.That(scenario.ObjectiveSummary, Does.Contain("Objectives"));
        }

        [UnityTest]
        public IEnumerator LastPlayerAction_AutomaticallyStartsEnemyPhase()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.EliminateNoReinforcements);
            config.AiEnabled = false;
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);

            Assert.That(context.Player.TrySelectUnit(context.UnitsByKey["knight"]), Is.True);
            Assert.That(context.Player.TryChooseStay(), Is.True);
            Assert.That(context.Player.TryWait(), Is.True);
            yield return null;

            Assert.That(context.Scenario.CompletedRounds, Is.EqualTo(1), "The final Player action should auto-resolve the Enemy phase when AI is disabled.");
            Assert.That(context.Turns.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
        }

        [UnityTest]
        public IEnumerator RestartConfiguration_ReconstructsCleanStateWithoutDuplicates()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.EliminateRound3Reinforcements);
            BattleRuntimeContext first = null;
            yield return StartScenario(config, c => first = c);
            Unit knight = first.UnitsByKey["knight"];
            knight.ReceiveDamage(5);
            knight.SetHasActed(true);
            first.Scenario.State.ReinforcementStates[first.Scenario.Configuration.Reinforcements[0]] = ReinforcementWaveState.Failed;

            yield return StartScenario(config, c => first = c);

            Assert.That(Object.FindObjectsByType<BattleTurnController>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1));
            Assert.That(first.UnitsByKey["knight"].CurrentHealth, Is.EqualTo(first.UnitsByKey["knight"].MaxHealth));
            Assert.That(first.UnitsByKey["knight"].HasActed, Is.False);
            Assert.That(first.Scenario.CompletedRounds, Is.EqualTo(0));
            Assert.That(first.Scenario.State.ReinforcementStates[first.Scenario.Configuration.Reinforcements[0]], Is.EqualTo(ReinforcementWaveState.Pending));
        }

        [UnityTest]
        public IEnumerator EliminateObjective_WaitsForRequiredFutureWave()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.EliminateRound3Reinforcements);
            config.AiEnabled = false;
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            context.UnitsByKey["enemy"].ReceiveDamage(99);
            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.IsBattleEnded, Is.False);

            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.CurrentRound, Is.EqualTo(2));
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(0));

            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.CurrentRound, Is.EqualTo(3));
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(0), "Round 3 wave does not spawn until Round 3 Enemy phase starts.");
            yield return AdvanceOneRound(context);
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(1));

            foreach (Unit unit in context.Turns.ActiveUnits)
            {
                if (unit != null && unit.Faction == UnitFaction.Enemy)
                {
                    unit.ReceiveDamage(99);
                }
            }

            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
        }

        [UnityTest]
        public IEnumerator ReachObjective_CompletesOnlyAfterDesignatedCommit()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.ReachAreaWrongUnitPresent);
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            Unit mage = context.UnitsByKey["mage"];
            Unit knight = context.UnitsByKey["knight"];

            context.Grid.TryGetTile(new GridCoordinate(4, 2), out var zone);
            TileClear(mage);
            mage.PlaceOnTile(zone);
            zone.SetOccupyingUnit(mage);
            context.Scenario.NotifyPlayerUnitCommitted(mage);
            Assert.That(context.Turns.IsBattleEnded, Is.False);

            TileClear(mage);
            mage.PlaceOnTile(context.Grid.TryGetTile(new GridCoordinate(0, 1), out var mageStart) ? mageStart : zone);
            mage.OccupiedTile.SetOccupyingUnit(mage);
            Assert.That(context.Player.TrySelectUnit(knight), Is.True);
            Assert.That(context.Player.TryChooseMovementTile(zone), Is.True);
            yield return PlayModeTestUtility.WaitUntilOrFail(() => context.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, () => context.Scenario.ObjectiveSummary);
            Assert.That(context.Turns.IsBattleEnded, Is.False, "Provisional movement does not complete Reach.");
            Assert.That(context.Player.TryCancel(), Is.True);
            yield return PlayModeTestUtility.WaitUntilOrFail(() => context.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingMovement, 5f, () => context.Scenario.ObjectiveSummary);
            Assert.That(context.Turns.IsBattleEnded, Is.False);
            Assert.That(context.Player.TryChooseMovementTile(zone), Is.True);
            yield return PlayModeTestUtility.WaitUntilOrFail(() => context.Player.CurrentInteractionState == UnitSelectionController.PlayerInteractionState.ChoosingAction, 5f, () => context.Scenario.ObjectiveSummary);
            Assert.That(context.Player.TryWait(), Is.True);
            yield return null;
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
        }

        [UnityTest]
        public IEnumerator SurviveAndProtectObjectives_UseRoundBoundaryAndProtectedDeath()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.ProtectAndSurvive);
            config.AiEnabled = false;
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            Unit healer = context.UnitsByKey["healer"];

            healer.ReceiveDamage(2);
            Assert.That(context.Turns.CheckBattleEndAfterSkill(), Is.False);
            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.CompletedRounds, Is.EqualTo(1));
            Assert.That(context.Turns.IsBattleEnded, Is.False);
            healer.ReceiveHealing(2);
            yield return AdvanceOneRound(context);
            yield return AdvanceOneRound(context);
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));

            yield return StartScenario(config, c => context = c);
            context.UnitsByKey["knight"].ReceiveDamage(99);
            Assert.That(context.Turns.CheckBattleEndAfterSkill(), Is.False, "Killing a non-protected Player does not trigger Protect defeat while another Player lives.");
            context.UnitsByKey["healer"].ReceiveDamage(99);
            Assert.That(context.Turns.CheckBattleEndAfterSkill(), Is.True);
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Defeat"));
        }

        [UnityTest]
        public IEnumerator MultipleObjectives_RequireAllCompletionInEitherOrder()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.ReachAndEliminate);
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            context.UnitsByKey["enemy"].ReceiveDamage(99);
            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.IsBattleEnded, Is.False);
            context.Grid.TryGetTile(new GridCoordinate(4, 2), out var zone);
            context.UnitsByKey["knight"].PlaceOnTile(zone);
            zone.SetOccupyingUnit(context.UnitsByKey["knight"]);
            context.Scenario.NotifyPlayerUnitCommitted(context.UnitsByKey["knight"]);
            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.IsBattleEnded, Is.False, "Pending required wave still blocks Eliminate.");
        }

        [UnityTest]
        public IEnumerator ReinforcementTimingFallbackFailureAndBattleEndLocking()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.ReinforcementSpawnOccupiedFallback);
            config.AiEnabled = false;
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(1));
            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.CurrentRound, Is.EqualTo(2));
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(1), "Round 2 wave waits for Round 2 Enemy phase.");
            yield return AdvanceOneRound(context);
            Assert.That(context.Turns.CountLivingUnits(UnitFaction.Enemy), Is.EqualTo(2));
            Unit spawned = null;
            foreach (Unit unit in context.Turns.ActiveUnits)
            {
                if (unit != null && unit.Faction == UnitFaction.Enemy && unit.name.StartsWith("wave"))
                {
                    spawned = unit;
                }
            }

            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.CurrentCoordinate, Is.EqualTo(new GridCoordinate(4, 1)), "Occupied intended tile falls back deterministically to nearest valid coordinate.");
            Assert.That(context.UnitsByKey["blocker"].OccupiedTile.Coordinate, Is.EqualTo(new GridCoordinate(4, 2)));

            yield return StartScenario(FailedPlacementConfig(), c => context = c);
            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.CurrentRound, Is.EqualTo(2));
            yield return AdvanceOneRound(context);
            Assert.That(context.Scenario.LastDiagnostic, Does.Contain("failed to spawn"));
            Assert.That(context.Turns.CheckBattleEndAfterSkill(), Is.False, "Failed required wave blocks EliminateAllEnemies.");
            context.UnitsByKey["enemy"].ReceiveDamage(99);
            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.IsBattleEnded, Is.False);
        }

        [UnityTest]
        public IEnumerator DefeatPrecedence_FinalEnemyAndProtectedUnitDieTogether()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.ProtectHealer);
            BattleRuntimeContext context = null;
            yield return StartScenario(config, c => context = c);
            context.UnitsByKey["enemy"].ReceiveDamage(99);
            context.UnitsByKey["healer"].ReceiveDamage(99);
            context.Turns.CheckBattleEndAfterSkill();
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Defeat"));
            Assert.That(context.Player.TrySelectUnit(context.UnitsByKey["knight"]), Is.False);
            Assert.That(context.Turns.TryEndPlayerTurn(), Is.False);
        }

        private static IEnumerator AdvanceOneRound(BattleRuntimeContext context)
        {
            Assert.That(context.Turns.TryEndPlayerTurn(), Is.True, context.Scenario.ObjectiveSummary);
            yield return PlayModeTestUtility.WaitUntilOrFail(() => context.Turns.CurrentPhase == BattlePhase.PlayerTurn || context.Turns.IsBattleEnded, 5f, () => context.Scenario.ObjectiveSummary);
        }

        private static void TileClear(Unit unit)
        {
            if (unit != null && unit.OccupiedTile != null)
            {
                unit.OccupiedTile.SetOccupyingUnit(null);
            }
        }

        private IEnumerator StartScenario(BattleTestPresetId preset, System.Action<BattleRuntimeContext> loaded)
        {
            yield return StartScenario(BattleTestPresetLibrary.Create(preset), loaded);
        }

        private IEnumerator StartScenario(BattleSetupConfiguration config, System.Action<BattleRuntimeContext> loaded)
        {
            Scene scene = SceneManager.CreateScene("RuntimeScenarioTest" + Time.frameCount);
            SceneManager.SetActiveScene(scene);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.IsValid() && loadedScene != scene)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                    while (unload != null && !unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            BattleRuntimeContext context = BattleScenarioRuntimeBuilder.Build(config, null, true, null, TryGetPreset(config));
            yield return null;
            yield return null;
            Assert.That(context.Turns.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
            loaded(context);
        }

        private static BattleSetupConfiguration FailedPlacementConfig()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.EliminateRound3Reinforcements);
            config.AiEnabled = false;
            config.FallbackRadius = 0;
            config.Reinforcements[0].ArrivalRound = 2;
            config.Reinforcements[0].SpawnCoordinate = config.Units[0].Coordinate;
            return config;
        }

        private static BattleTestPresetId TryGetPreset(BattleSetupConfiguration config)
        {
            foreach (BattleTestPresetMetadata metadata in BattleTestPresetLibrary.Presets)
            {
                if (metadata.DisplayName == config.ScenarioName)
                {
                    return metadata.Id;
                }
            }

            return BattleTestPresetId.FullScenarioSmoke;
        }
    }
}
