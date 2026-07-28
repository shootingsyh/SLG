using System;
using NUnit.Framework;
using SLG.Core;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Shell;

namespace SLG.Tests
{
    public sealed class CampaignFlowTests
    {
        private InMemorySaveStorage storage;
        private SaveRepository repository;
        private GameFlowService flow;

        [SetUp]
        public void Setup()
        {
            storage = new InMemorySaveStorage();
            repository = new SaveRepository(storage);
            flow = new GameFlowService();
            flow.DemoState = DemoFlowState.NotStarted;
            flow.IsTransitionInProgress = false;
            GameShellServices.Clear();
        }

        // Destination resolution
        [Test]
        public void ResolveDestination_Battle1Complete_Returns_ChapterResult()
        {
            Assert.That(CampaignFlowService.ResolveDestination(DemoFlowState.Battle1Complete), Is.EqualTo(CampaignDestination.ChapterResult));
        }

        [Test]
        public void ResolveDestination_Battle2Complete_Returns_ChapterResult()
        {
            Assert.That(CampaignFlowService.ResolveDestination(DemoFlowState.Battle2Complete), Is.EqualTo(CampaignDestination.ChapterResult));
        }

        [Test]
        public void ResolveDestination_OtherState_Returns_Title()
        {
            Assert.That(CampaignFlowService.ResolveDestination(DemoFlowState.NotStarted), Is.EqualTo(CampaignDestination.Title));
            Assert.That(CampaignFlowService.ResolveDestination(DemoFlowState.DemoComplete), Is.EqualTo(CampaignDestination.Title));
        }

        // Victory processing
        [Test]
        public void VictoryProcessed_Battle1_UpdatesDemoState()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);

            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.NotStarted));
            processor.TryProcessVictory();
            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));
        }

        [Test]
        public void VictoryProcessed_Battle2_UpdatesDemoState()
        {
            flow.DemoState = DemoFlowState.Battle1Complete;
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);

            processor.TryProcessVictory();
            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));
        }

        [Test]
        public void VictoryProcessed_IsIdempotent()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);

            processor.TryProcessVictory();

            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));
        }

        [Test]
        public void DefeatProcessed_IsIdempotent()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);

            processor.TryProcessDefeat();

            Assert.That(processor.TryProcessDefeat(), Is.True);
        }

        // Pending data for ChapterResult
        [Test]
        public void VictoryStored_PendingData_HasCorrectChapterFields()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.ChapterId, Is.EqualTo("chapter-1"));
            Assert.That(pending.ChapterName, Is.EqualTo("Demo Campaign"));
            Assert.That(pending.LastCompletedChapterId, Is.EqualTo("chapter-1"));
            Assert.That(pending.NextChapterId, Is.EqualTo("chapter-2"));
        }

        [Test]
        public void VictoryStored_PendingData_HasCorrectBattleFields_Battle1()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.BattleId, Is.EqualTo(CampaignBattleIds.Battle1Id));
            Assert.That(pending.BattleName, Does.Contain("Eliminate"));
        }

        [Test]
        public void VictoryStored_PendingData_HasCorrectBattleFields_Battle2()
        {
            flow.DemoState = DemoFlowState.Battle1Complete;
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.BattleId, Is.EqualTo(CampaignBattleIds.Battle2Id));
            Assert.That(pending.BattleName, Does.Contain("Protect"));
        }

        [Test]
        public void VictoryStored_PendingData_Metadata_HasRequiredFields()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();

            Assert.That(pending.Metadata.SaveType, Is.EqualTo(SaveConstants.CampaignSaveType));
            Assert.That(pending.Metadata.ChapterId, Is.Not.Null);
            Assert.That(pending.Metadata.BattleId, Is.Not.Null);
            Assert.That(pending.Metadata.FormatVersion, Is.EqualTo(SaveConstants.FormatVersion));
        }

        [Test]
        public void VictoryStored_Battle1_NotDemoComplete()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);
        }

        [Test]
        public void VictoryStored_Battle2_IsDemoComplete()
        {
            flow.DemoState = DemoFlowState.Battle1Complete;
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);
            processor.TryProcessVictory();

            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.True);
        }

        [Test]
        public void VictoryStored_NoAutoSave()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            // Verify no autosave was auto-created
            Assert.That(repository.TryLoadCampaign(SaveConstants.CampaignAutosaveFileName, out _, out _), Is.False);
        }

        // User-initiated save (simulates ChapterResultController behavior)
        [Test]
        public void UserInitiatedSave_WritesToSlot()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            SaveOperationResult result = repository.SaveCampaign(pending, 1);

            Assert.That(result.Success, Is.True);
            Assert.That(repository.TryLoadCampaign("campaign-slot-01.json", out CampaignSaveData saved, out SaveSlotInfo info), Is.True);
            Assert.That(saved.BattleId, Is.EqualTo(CampaignBattleIds.Battle1Id));
            Assert.That(info.State, Is.EqualTo(SaveSlotState.Valid));
        }

        // Scene names
        [Test]
        public void CampaignSceneNames_HasAllSceneNames()
        {
            Assert.That(CampaignSceneNames.Title, Is.EqualTo("Title"));
            Assert.That(CampaignSceneNames.ChapterResult, Is.EqualTo("ChapterResult"));
            Assert.That(CampaignSceneNames.BattleTemplate, Is.EqualTo("BattleTestTemplate"));
        }

        // Battle IDs
        [Test]
        public void CampaignBattleIds_HasAllIds()
        {
            Assert.That(CampaignBattleIds.Battle1Id, Is.EqualTo("battle-1"));
            Assert.That(CampaignBattleIds.Battle2Id, Is.EqualTo("battle-2"));
        }

        // Destination enum
        [Test]
        public void CampaignDestination_Enum_HasAllValues()
        {
            Assert.That(Enum.IsDefined(typeof(CampaignDestination), 0), Is.True);
            Assert.That(Enum.IsDefined(typeof(CampaignDestination), 1), Is.True);
            Assert.That(Enum.IsDefined(typeof(CampaignDestination), 2), Is.True);
        }

        // Unlocked chapters
        [Test]
        public void VictoryStored_UnlockedChapters_IncludesDemoChapters()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.UnlockedChapterIds.Contains("chapter-1"), Is.True);
            Assert.That(pending.UnlockedChapterIds.Contains("chapter-2"), Is.True);
        }

        // Roster and inventory
        [Test]
        public void VictoryStored_RosterAndInventory_AreInitialized()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.Roster, Is.Not.Null);
            Assert.That(pending.Inventory, Is.Not.Null);
        }

        // Round trip
        [Test]
        public void VictoryStored_RoundTrip_PreservesFields()
        {
            CampaignFlowService processor = new CampaignFlowService(flow, repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor.TryProcessVictory();

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();

            SaveOperationResult saveResult = repository.SaveCampaign(pending, 1);
            Assert.That(saveResult.Success, Is.True);

            Assert.That(repository.TryLoadCampaign("campaign-slot-01.json", out CampaignSaveData loaded, out SaveSlotInfo info), Is.True);

            Assert.That(loaded.ChapterId, Is.EqualTo("chapter-1"));
            Assert.That(loaded.ChapterName, Is.EqualTo("Demo Campaign"));
            Assert.That(loaded.BattleId, Is.EqualTo(CampaignBattleIds.Battle1Id));
            Assert.That(loaded.BattleName, Does.Contain("Eliminate"));
            Assert.That(loaded.LastCompletedChapterId, Is.EqualTo("chapter-1"));
            Assert.That(loaded.NextChapterId, Is.EqualTo("chapter-2"));
            Assert.That(info.State, Is.EqualTo(SaveSlotState.Valid));
        }

        // Full demo flow
        [Test]
        public void FullDemoFlow_Battle1Victory_Battle2Victory_CompletesDemo()
        {
            CampaignFlowService processor1 = new CampaignFlowService(flow, repository);
            processor1.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            processor1.TryProcessVictory();
            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);

            CampaignFlowService processor2 = new CampaignFlowService(flow, repository);
            processor2.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);
            processor2.TryProcessVictory();
            Assert.That(flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.True);

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending.BattleId, Is.EqualTo(CampaignBattleIds.Battle2Id));
        }

        [Test]
        public void BattleResultType_Enum_HasCorrectValues()
        {
            Assert.That(Enum.IsDefined(typeof(BattleResultType), 0), Is.True);
            Assert.That(Enum.IsDefined(typeof(BattleResultType), 1), Is.True);
            Assert.That(Enum.IsDefined(typeof(BattleResultType), 2), Is.True);
        }

        [Test]
        public void SceneLoadedReason_Enum_HasCorrectValues()
        {
            Assert.That(Enum.IsDefined(typeof(SceneLoadedReason), 0), Is.True);
            Assert.That(Enum.IsDefined(typeof(SceneLoadedReason), 1), Is.True);
            Assert.That(Enum.IsDefined(typeof(SceneLoadedReason), 2), Is.True);
        }
    }
}