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
    public sealed class CampaignFlowPlayModeTests
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

        [UnityTest]
        public IEnumerator DemoBattle1Eliminate_Victory_ProcessedEndToEnd()
        {
            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle1Eliminate);
            _processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            context.Turns.SetCampaignFlowProcessor(_processor);

            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.NotStarted));

            yield return null;

            Assert.That(context.Turns.IsBattleEnded, Is.False, "Battle should not be ended at start.");

            KillAllEnemies();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.IsBattleEnded, Is.True, context.Scenario.ObjectiveSummary);
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
        }

        [UnityTest]
        public IEnumerator DemoBattle1Eliminate_Defeat_ProcessedEndToEnd()
        {
            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle1Eliminate);
            _processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            context.Turns.SetCampaignFlowProcessor(_processor);

            yield return null;

            KillAllPlayers();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.IsBattleEnded, Is.True, context.Scenario.ObjectiveSummary);
            Assert.That(context.Turns.BattleResult, Is.EqualTo("Defeat"));
        }

        [UnityTest]
        public IEnumerator Battle2Protect_Victory_GoToGameComplete()
        {
            _flow.DemoState = DemoFlowState.Battle1Complete;

            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle2Protect);
            _processor.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);
            context.Turns.SetCampaignFlowProcessor(_processor);

            yield return null;

            for (int r = 0; r < 2; r++)
            {
                context.Scenario.NotifyEnemyPhaseCompleted();
            }

            KillAllEnemies();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.BattleId, Is.EqualTo(CampaignBattleIds.Battle2Id));
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.True);

            SaveOperationResult saveResult = _repository.SaveCampaign(pending, 1);
            Assert.That(saveResult.Success, Is.True);
        }

        [UnityTest]
        public IEnumerator Battle1Eliminate_Victory_PendingDataReadyForChapterResult()
        {
            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle1Eliminate);
            _processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            context.Turns.SetCampaignFlowProcessor(_processor);

            yield return null;

            KillAllEnemies();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.ChapterId, Is.EqualTo("chapter-1"));
            Assert.That(pending.BattleId, Is.EqualTo(CampaignBattleIds.Battle1Id));
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);

            SaveOperationResult saveResult = _repository.SaveCampaign(pending, 1);
            Assert.That(saveResult.Success, Is.True);
            Assert.That(_repository.TryLoadCampaign("campaign-slot-01.json", out CampaignSaveData saved, out var savedInfo), Is.True);
            Assert.That(savedInfo.State, Is.EqualTo(SaveSlotState.Valid));
            Assert.That(saved.BattleId, Is.EqualTo(CampaignBattleIds.Battle1Id));

            Assert.That(_flow.TryContinueToNextBattle(), Is.True);
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));
        }

        [UnityTest]
        public IEnumerator ChapterResultStyle_Battle1Victory_NewFlowInstanceContinues()
        {
            _flow.DemoState = DemoFlowState.NotStarted;
            _flow.IsTransitionInProgress = false;

            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle1Eliminate);
            _processor.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            context.Turns.SetCampaignFlowProcessor(_processor);

            yield return null;

            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.NotStarted));

            KillAllEnemies();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending, Is.Not.Null);
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);

            _repository.SaveCampaign(pending, 1);

            // Simulate ChapterResultController creating a NEW GameFlowService instance
            var chapterResultFlow = new GameFlowService();
            Assert.That(chapterResultFlow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete),
                "New instance must see Battle1Complete from global store");

            Assert.That(chapterResultFlow.TryContinueToNextBattle(), Is.True,
                "New flow instance must successfully continue to next chapter");
            Assert.That(chapterResultFlow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));
        }

        [UnityTest]
        public IEnumerator ChapterResultStyle_Battle2Victory_NewFlowInstanceReturnsToTitle()
        {
            _flow.DemoState = DemoFlowState.Battle1Complete;
            _flow.IsTransitionInProgress = false;

            var processor = new CampaignFlowService(_flow, _repository);
            processor.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);

            BattleRuntimeContext context = BuildRuntime(BattleTestPresetId.DemoBattle2Protect);
            context.Turns.SetCampaignFlowProcessor(processor);

            yield return null;

            for (int r = 0; r < 2; r++)
            {
                context.Scenario.NotifyEnemyPhaseCompleted();
            }

            KillAllEnemies();
            context.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(context.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));

            CampaignSaveData pending = GameShellServices.GetPendingCampaignData();
            Assert.That(pending, Is.Not.Null);
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.True);

            _repository.SaveCampaign(pending, 1);

            // Simulate ChapterResultController creating a NEW GameFlowService instance
            var chapterResultFlow = new GameFlowService();
            Assert.That(chapterResultFlow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete),
                "New instance must see Battle2Complete from global store");

            Assert.That(chapterResultFlow.TryReturnToTitle(), Is.True,
                "New flow instance must successfully return to title");

            Assert.That(GameShellServices.GetDemoState(), Is.EqualTo(DemoFlowState.NotStarted),
                "DemoState should reset after returning to title");
        }

        [UnityTest]
        public IEnumerator ChapterFullCampaign_Battle1ThroughBattle2ToTitle()
        {
            _flow.DemoState = DemoFlowState.NotStarted;
            _flow.IsTransitionInProgress = false;

            BattleRuntimeContext ctx1 = BuildRuntime(BattleTestPresetId.DemoBattle1Eliminate);
            var proc1 = new CampaignFlowService(_flow, _repository);
            proc1.ConfigureBattle(BattleTestPresetId.DemoBattle1Eliminate);
            ctx1.Turns.SetCampaignFlowProcessor(proc1);

            yield return null;

            KillAllEnemies();
            ctx1.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(ctx1.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));

            CampaignSaveData pending1 = GameShellServices.GetPendingCampaignData();
            Assert.That(pending1, Is.Not.Null);
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.False);
            SaveOperationResult save1 = _repository.SaveCampaign(pending1, 1);
            Assert.That(save1.Success, Is.True);

            var crAfterBattle1 = new GameFlowService();
            Assert.That(crAfterBattle1.DemoState, Is.EqualTo(DemoFlowState.Battle1Complete));
            Assert.That(crAfterBattle1.TryContinueToNextBattle(), Is.True);
            Assert.That(crAfterBattle1.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));

            _flow.DemoState = DemoFlowState.Battle1Complete;
            _flow.IsTransitionInProgress = false;

            var proc2 = new CampaignFlowService(_flow, _repository);
            proc2.ConfigureBattle(BattleTestPresetId.DemoBattle2Protect);

            BattleRuntimeContext ctx2 = BuildRuntime(BattleTestPresetId.DemoBattle2Protect);
            ctx2.Turns.SetCampaignFlowProcessor(proc2);

            yield return null;

            for (int r = 0; r < 2; r++)
            {
                ctx2.Scenario.NotifyEnemyPhaseCompleted();
            }

            KillAllEnemies();
            ctx2.Turns.CheckBattleEndAfterSkill();
            yield return null;

            Assert.That(ctx2.Turns.BattleResult, Is.EqualTo("Victory"));
            Assert.That(_flow.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));

            CampaignSaveData pending2 = GameShellServices.GetPendingCampaignData();
            Assert.That(pending2, Is.Not.Null);
            Assert.That(GameShellServices.IsPendingDemoComplete(), Is.True);
            SaveOperationResult save2 = _repository.SaveCampaign(pending2, 2);
            Assert.That(save2.Success, Is.True);

            var crReturn = new GameFlowService();
            Assert.That(crReturn.DemoState, Is.EqualTo(DemoFlowState.Battle2Complete));
            Assert.That(crReturn.TryReturnToTitle(), Is.True);

            Assert.That(GameShellServices.GetDemoState(), Is.EqualTo(DemoFlowState.NotStarted),
                "DemoState should reset after full campaign completion and title return");
        }

        private BattleRuntimeContext BuildRuntime(BattleTestPresetId preset)
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(preset);
            config.AiEnabled = false;
            return BattleScenarioRuntimeBuilder.Build(config, null, true, _repository, preset);
        }

        private void KillAllEnemies()
        {
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude))
            {
                if (unit != null && unit.IsAlive && unit.Faction == UnitFaction.Enemy)
                    unit.ReceiveDamage(999);
            }
        }

        private void KillAllPlayers()
        {
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude))
            {
                if (unit != null && unit.IsAlive && unit.Faction == UnitFaction.Player)
                    unit.ReceiveDamage(999);
            }
        }
    }
}
