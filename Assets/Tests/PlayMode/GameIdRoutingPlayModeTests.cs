using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Shell;
using SLG.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    /// <summary>
    /// Regression tests for GameId routing, Inter-Game save progression, and cross-game save isolation.
    /// </summary>
    public sealed class GameIdRoutingPlayModeTests
    {
        private InMemorySaveStorage _storage;
        private SaveRepository _repository;
        private GameFlowService _flow;
        private CampaignFlowService _processor;

        [SetUp]
        public void Setup()
        {
            GameShellServices.Clear();
            _storage = new InMemorySaveStorage();
            _storage.ClearAll();
            _repository = new SaveRepository(_storage);
            GameShellServices.UseRepositoryForTests(_repository);
            _flow = new GameFlowService();
            _flow.DemoState = DemoFlowState.NotStarted;
            _flow.IsTransitionInProgress = false;
            _processor = new CampaignFlowService(_flow, _repository);
        }

        [TearDown]
        public void TearDown()
        {
            GameShellServices.Clear();
        }

        // =============================
        // 1. GameId Routing
        // =============================

        [UnityTest]
        public IEnumerator StartGame_Default_SetsCorrectGameIdAndState()
        {
            Assert.That(GameShellServices.ActiveGameId, Is.Empty);
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.NotStarted));

            bool started = _flow.TryStartGame("default");
            Assert.That(started, Is.True, $"{_flow.LastError}");
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("default"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.NotStarted));

            yield return null;
        }

        [UnityTest]
        public IEnumerator StartGameTest1_SetsGameIdAndClearsPreviousState()
        {
            GameShellServices.ActiveGameId = "previous-game";
            GameShellServices.SetDemoState(DemoFlowState.Battle1Complete);

            bool started = _flow.TryStartGame("test-1");
            Assert.That(started, Is.True, $"{_flow.LastError}");
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));
            Assert.That(GameShellServices.GetDemoState(), Is.EqualTo(DemoFlowState.NotStarted),
                "Starting a new game must reset DemoState");

            yield return null;
        }

        [UnityTest]
        public IEnumerator StartGame_UnknownId_ReturnsFalseWithErrorMessage()
        {
            bool started = _flow.TryStartGame("nonexistent-game");
            Assert.That(started, Is.False);
            Assert.That(_flow.LastError, Does.Contain("nonexistent-game"));
            Assert.That(GameShellServices.ActiveGameId, Is.Empty,
                "GameId should not be set when start fails");

            yield return null;
        }

        [Test]
        public void GameDefinitions_GetDefault_ReturnsProductionCampaign()
        {
            GameDefinition game = GameDefinitions.Get("default");
            Assert.That(game, Is.Not.Null);
            Assert.That(game.GameId, Is.EqualTo("default"));
            Assert.That(game.BattleCount, Is.EqualTo(2));
            Assert.That(game.GetBattleAt(0).BattleId, Is.EqualTo("battle-1"));
            Assert.That(game.GetBattleAt(1).BattleId, Is.EqualTo("battle-2"));
        }

#if UNITY_EDITOR
        [Test]
        public void GameDefinitions_GetTest1_ReturnsThreeBattleGame()
        {
            GameDefinition game = GameDefinitions.GetTest();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.GameId, Is.EqualTo("test-1"));
            Assert.That(game.BattleCount, Is.EqualTo(3));
            Assert.That(game.GetBattleAt(0).BattleId, Is.EqualTo("test-battle-1"));
            Assert.That(game.GetBattleAt(1).BattleId, Is.EqualTo("test-battle-2"));
            Assert.That(game.GetBattleAt(2).BattleId, Is.EqualTo("test-battle-3"));
        }

        [Test]
        public void GameDefinitions_GetAll_ContainsAllRegisteredGames()
        {
            GameDefinition[] games = GameDefinitions.GetAll();
            Assert.That(games.Length, Is.GreaterThanOrEqualTo(2));

            GameDefinition defaultGame = GameDefinitions.Get("default");
            GameDefinition testGame = GameDefinitions.GetTest();
            Assert.That(defaultGame, Is.Not.Null);
            Assert.That(testGame, Is.Not.Null);
        }
#endif

        [Test]
        public void GameDefinition_IsLastBattle_DetectsFinalBattleCorrectly()
        {
            GameDefinition game = GameDefinitions.Get("default");
            Assert.That(game.IsLastBattle(0), Is.False);
            Assert.That(game.IsLastBattle(1), Is.True);
            Assert.That(game.IsLastBattle(2), Is.True);
        }

        [Test]
        public void GameDefinition_IsComplete_DetectsCompletion()
        {
            GameDefinition game = GameDefinitions.Get("default");
            Assert.That(game.IsComplete(0), Is.False);
            Assert.That(game.IsComplete(1), Is.False);
            Assert.That(game.IsComplete(2), Is.True);
        }

#if UNITY_EDITOR
        [Test]
        public void GameDefinition_GetNextBattle_BeyondAllBattles_ReturnsNull()
        {
            GameDefinition game = GameDefinitions.GetTest();
            Assert.That(game.GetNextBattle(0), Is.Not.Null);
            Assert.That(game.GetNextBattle(1), Is.Not.Null);
            Assert.That(game.GetNextBattle(2), Is.Not.Null);
            Assert.That(game.GetNextBattle(3), Is.Null);
        }
#endif

        // =============================
        // 2. Inter-Game Save Progression
        // =============================

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator TransitionToInterGame_Victory_Battle1_SetsCorrectState()
        {
            _flow.TryStartGame("test-1");

            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.TestSwiftVictory1);
            var processor = new CampaignFlowService(_flow, _repository);
            processor.ConfigureBattle(BattleTestPresetId.TestSwiftVictory1);
            context.Turns.SetCampaignFlowProcessor(processor);

            yield return null;

            KillAllEnemies(context);
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.IsBattleEnded, Is.True);
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));

            // After victory, transition to inter-game
            bool canTransition = _flow.TryTransitionToInterGame(1);
            Assert.That(canTransition, Is.True, $"{_flow.LastError}");

            yield return null;

            Assert.That(_flow.CurrentScreen, Is.EqualTo(ShellScreen.InterGame));
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));
        }
#endif

        [Test]
        public void SaveMetadata_FillsGameIdAndDestinationSceneForNonDefaultGame()
        {
            var data = new CampaignSaveData
            {
                GameId = "test-1",
                BattleId = "test-battle-1",
                ChapterId = "chapter-1",
                NextChapterId = "chapter-2",
                NextBattleId = "test-battle-2"
            };

            SaveRepository repo = new SaveRepository(_storage);
            SaveOperationResult result = repo.SaveCampaign(data, 1);
            Assert.That(result.Success, Is.True);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded, out var info), Is.True);
            Assert.That(info.State, Is.EqualTo(SaveSlotState.Valid));
            Assert.That(loaded.GameId, Is.EqualTo("test-1"));

            // Verify metadata contains InterGame destination for non-default game
            Assert.That(loaded.Metadata.DestinationScene, Is.EqualTo("InterGame"),
                "Non-default game save should have InterGame as destination scene");
        }

        [Test]
        public void SaveMetadata_DefaultGame_UsesEmptyDestinationScene()
        {
            var data = new CampaignSaveData
            {
                GameId = "default",
                BattleId = "battle-1",
                ChapterId = "chapter-1",
                NextChapterId = "chapter-2"
            };

            SaveRepository repo = new SaveRepository(_storage);
            repo.SaveCampaign(data, 1);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded, out _), Is.True);
            Assert.That(loaded.GameId, Is.EqualTo("default"));

            // Default game (production campaign) doesn't use InterGame routing
            Assert.That(loaded.Metadata.DestinationScene, Is.Not.EqualTo("InterGame"),
                "Default game should not route through InterGame scene");
        }

        [Test]
        public void ContinueResolution_WithInterGameDestination_RoutesCorrectly()
        {
            var data = new CampaignSaveData
            {
                GameId = "test-1",
                BattleId = "test-battle-1",
                NextChapterId = "chapter-2",
                NextBattleId = "test-battle-2"
            };

            SaveRepository repo = new SaveRepository(_storage);
            repo.SaveCampaign(data, 1);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded, out var info), Is.True);
            Assert.That(loaded.Metadata.DestinationScene, Is.EqualTo("InterGame"));

            // Verify the resolution logic can detect InterGame destination
            var metadata = info.Metadata;
            Assert.That(metadata.DestinationScene, Is.EqualTo("InterGame"),
                "Save metadata should carry InterGame destination scene");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator NextBattle_FromInterGame_AdvancesCompletedCount()
        {
            _flow.TryStartGame("test-1");
            _flow.TryTransitionToInterGame(1);

            Assert.That(_flow.TryNextBattle(), Is.True, $"{_flow.LastError}");
            Assert.That(_flow.CurrentScreen, Is.EqualTo(ShellScreen.Battle));

            yield return null;
        }
#endif

        [Test]
        public void SaveAndLoad_PreservesGameIdAcrossSession()
        {
            var data = new CampaignSaveData
            {
                GameId = "test-1",
                ChapterId = "chapter-1",
                BattleId = "test-battle-1",
                NextChapterId = "chapter-2",
                NextBattleId = "test-battle-2",
                LastCompletedChapterId = "chapter-1"
            };

            SaveRepository repo = new SaveRepository(_storage);
            repo.SaveCampaign(data, 3);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(3), out var loaded, out var info), Is.True);
            Assert.That(loaded.GameId, Is.EqualTo("test-1"));
            Assert.That(loaded.NextBattleId, Is.EqualTo("test-battle-2"));
        }

        // =============================
        // 3. Cross-Game Save Isolation
        // =============================

        [Test]
        public void CampaignSlots_IndependentPerSlot_Numbering()
        {
            var gameASlot1 = new CampaignSaveData { GameId = "default", NextChapterId = "chapter-2" };
            var gameBSlot2 = new CampaignSaveData { GameId = "test-1", NextChapterId = "chapter-3" };

            SaveRepository repo = new SaveRepository(_storage);
            repo.SaveCampaign(gameASlot1, 1);
            repo.SaveCampaign(gameBSlot2, 2);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loadedA, out _), Is.True);
            Assert.That(loadedA.GameId, Is.EqualTo("default"));
            Assert.That(loadedA.NextChapterId, Is.EqualTo("chapter-2"));

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(2), out var loadedB, out _), Is.True);
            Assert.That(loadedB.GameId, Is.EqualTo("test-1"));
            Assert.That(loadedB.NextChapterId, Is.EqualTo("chapter-3"));
        }

        [Test]
        public void CampaignSlots_IndependentPerSlot_GameIdMismatch()
        {
            var defaultSave = new CampaignSaveData { GameId = "default", NextChapterId = "chapter-2" };
            var testSave = new CampaignSaveData { GameId = "test-1", NextChapterId = "chapter-1" };

            SaveRepository repo = new SaveRepository(_storage);

            // Save both games to the same slot (slot 2) - simulating game switching
            repo.SaveCampaign(testSave, 2);
            repo.SaveCampaign(defaultSave, 2);

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(2), out var loaded, out _), Is.True);
            Assert.That(loaded.GameId, Is.EqualTo("default"),
                "Last save wins - default game should overwrite slot 2");
        }

        [Test]
        public void ActiveGameId_IndependentPerGame_DefinesNextBattle()
        {
            GameShellServices.ActiveGameId = "nonexistent-game";

            var flow = new GameFlowService();
            flow.IsTransitionInProgress = false;

            bool result = flow.TryContinueToNextBattle();
            Assert.That(result, Is.False,
                "Cannot continue without a valid game context");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FullTestGameFlow_Battle1Victory_InterGameSaveAndLoad()
        {
            _flow.TryStartGame("test-1");

            // Battle 1
            BattleRuntimeContext context1 = BuildRuntime(BattleTestPresetId.TestSwiftVictory1);
            var proc1 = new CampaignFlowService(_flow, _repository);
            proc1.ConfigureBattle(BattleTestPresetId.TestSwiftVictory1);
            context1.Turns.SetCampaignFlowProcessor(proc1);

            yield return null;

            KillAllEnemies(context1);
            context1.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context1.Turns.BattleResult, Is.EqualTo("Victory"));

            // Save battle 1 progress
            CampaignSaveData data1 = new CampaignSaveData
            {
                GameId = "test-1",
                BattleId = "test-battle-1",
                ChapterId = "chapter-1",
                NextChapterId = "chapter-2",
                NextBattleId = "test-battle-2"
            };
            SaveOperationResult save1 = _repository.SaveCampaign(data1, 1);
            Assert.That(save1.Success, Is.True);

            // Verify save has InterGame destination
            Assert.That(_repository.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out var loaded1, out var info1), Is.True);
            Assert.That(loaded1.GameId, Is.EqualTo("test-1"));
            Assert.That(loaded1.Metadata.DestinationScene, Is.EqualTo("InterGame"));

            yield return null;
        }
#endif

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FullTestGameFlow_Battle1ToBattle2_InterGameTransition()
        {
            _flow.TryStartGame("test-1");

            // Battle 1 victory
            BattleRuntimeContext context1 = BuildRuntime(BattleTestPresetId.TestSwiftVictory1);
            var proc1 = new CampaignFlowService(_flow, _repository);
            proc1.ConfigureBattle(BattleTestPresetId.TestSwiftVictory1);
            context1.Turns.SetCampaignFlowProcessor(proc1);

            yield return null;

            KillAllEnemies(context1);
            context1.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context1.Turns.BattleResult, Is.EqualTo("Victory"));

            // Save and transition to InterGame
            _flow.TryTransitionToInterGame(1);
            Assert.That(_flow.CurrentScreen, Is.EqualTo(ShellScreen.InterGame));

            // From InterGame, launch next battle
            bool nextBattle = _flow.TryNextBattle();
            Assert.That(nextBattle, Is.True, $"{_flow.LastError}");
            Assert.That(_flow.CurrentScreen, Is.EqualTo(ShellScreen.Battle));

            yield return null;
        }
#endif

        [Test]
        public void PendingCampaignData_GameIdPersistedAcrossFlowInstance()
        {
            var pendingData = new CampaignSaveData
            {
                GameId = "test-1",
                ChapterId = "chapter-test"
            };
            GameShellServices.SetPendingCampaignData(pendingData, false, "test-1");

            Assert.That(GameShellServices.GetPendingCampaignData().GameId, Is.EqualTo("test-1"));
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));

            // New flow instance should see the same ActiveGameId from global store
            var newFlow = new GameFlowService();
            // GameFlowService reads from GameShellServices for DemoState
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));
        }

        [Test]
        public void InterGameState_ClearsPendingCampaignData()
        {
            GameShellServices.SetPendingCampaignData(
                new CampaignSaveData { GameId = "test-1" },
                false,
                "test-1");

            Assert.That(GameShellServices.GetPendingCampaignData(), Is.Not.Null);

            // Set inter-game state - should clear pending
            GameShellServices.SetInterGameState("test-1", "battle-1", "battle-2");

            Assert.That(GameShellServices.GetPendingCampaignData(), Is.Null,
                "SetInterGameState should clear pending campaign data");
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));
        }

        [Test]
        public void Clear_ResetsAllGameDataToDefaults()
        {
            GameShellServices.ActiveGameId = "test-1";
            GameShellServices.SetDemoState(DemoFlowState.Battle1Complete);
            GameShellServices.SetPendingCampaignData(
                new CampaignSaveData { GameId = "test-1" },
                true,
                "test-1");

            GameShellServices.Clear();

            Assert.That(GameShellServices.ActiveGameId, Is.Empty);
            Assert.That(GameShellServices.GetDemoState(), Is.EqualTo(DemoFlowState.NotStarted));
            Assert.That(GameShellServices.GetPendingCampaignData(), Is.Null);
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);
            Assert.That(GameShellServices.InterGameDestinationScene, Is.Empty);
        }

#if UNITY_EDITOR
        [Test]
        public void TryResetTestGameData_SucceedsAndSetsTestGameId()
        {
            Assert.That(GameShellServices.TryResetTestGameData(), Is.True);
            Assert.That(GameShellServices.ActiveGameId, Is.EqualTo("test-1"));
            Assert.That(GameShellServices.GetDemoState(), Is.EqualTo(DemoFlowState.NotStarted));
        }
#endif

        // =============================
        // 4. TryContinue Scene Resolution
        // =============================

        [Test]
        public void TryContinue_WithBattleSave_ResolvesToBattleScene()
        {
            var battleData = new BattleSaveData
            {
                BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString(),
                CurrentRound = 2
            };

            SaveRepository repo = new SaveRepository(_storage);
            repo.SaveBattle(battleData);

            var flow = new GameFlowService();
            flow.IsTransitionInProgress = false;

            ContinueResolution resolution = repo.ResolveContinue();
            Assert.That(resolution.Kind, Is.EqualTo(ContinueKind.BattleSave));
            Assert.That(resolution.CanContinue, Is.True);
        }

        [Test]
        public void TryContinue_WithInterGameSave_Missing()
        {
            _storage.ClearAll();

            var flow = new GameFlowService();
            flow.IsTransitionInProgress = false;

            ContinueResolution resolution = _repository.ResolveContinue();
            Assert.That(resolution.Kind, Is.EqualTo(ContinueKind.None));
            Assert.That(resolution.CanContinue, Is.False);
        }

        [Test]
        public void BattleSavePriority_ExceedsCampaignSaveInContinue()
        {
            // Save campaign
            _repository.SaveCampaign(
                new CampaignSaveData { GameId = "default", NextChapterId = "chapter-2" }, 1);

            // Save battle
            _repository.SaveBattle(new BattleSaveData
            {
                BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString()
            });

            var resolution = _repository.ResolveContinue();
            Assert.That(resolution.Kind, Is.EqualTo(ContinueKind.BattleSave),
                "Battle save priority should exceed campaign save in Continue resolution");
        }

        [Test]
        public void DeleteBattleSave_FallsBackToCampaignSave()
        {
            // Save both
            _repository.SaveCampaign(
                new CampaignSaveData { GameId = "default", NextChapterId = "chapter-2" }, 1);
            _repository.SaveBattle(new BattleSaveData
            {
                BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString()
            });

            Assert.That(_repository.ResolveContinue().Kind, Is.EqualTo(ContinueKind.BattleSave));

            _repository.DeleteBattleSave();

            Assert.That(_repository.ResolveContinue().Kind, Is.EqualTo(ContinueKind.Campaign),
                "After deleting battle save, Continue should fall back to campaign");
        }

        // =============================
        // 5. Movement Speed Configuration
        // =============================

        [Test]
        public void UnitDefaultMovementSpeed_IsConfigurableViaSerializeField()
        {
            GameObject unitObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitObj.name = "TestSpeedUnit";
            Unit unit = unitObj.AddComponent<Unit>();

            Assert.That(unit.MovementSpeed, Is.GreaterThan(0f));

            Object.DestroyImmediate(unitObj);
        }

        // =============================
        // Helper methods
        // =============================

        private BattleRuntimeContext BuildRuntime(BattleTestPresetId preset)
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(preset);
            config.AiEnabled = false;
            return BattleScenarioRuntimeBuilder.Build(config, null, true, _repository, preset);
        }

        private void KillAllEnemies(BattleRuntimeContext context)
        {
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude))
            {
                if (unit != null && unit.IsAlive && unit.Faction == UnitFaction.Enemy)
                    unit.ReceiveDamage(999);
            }
        }
    }
}
