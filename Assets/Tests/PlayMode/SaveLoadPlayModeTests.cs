using System.Collections;
using NUnit.Framework;
using SLG.Core;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SLG.Tests.PlayMode
{
    public sealed class SaveLoadPlayModeTests
    {
        [UnityTest]
        public IEnumerator CampaignSlotSaveLoad_OverwriteDeleteAndAutosaveAreIndependent()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            CampaignSaveData slot1 = new CampaignSaveData { LastCompletedChapterId = "chapter-1", NextChapterId = "chapter-2" };
            CampaignSaveData slot2 = new CampaignSaveData { LastCompletedChapterId = "chapter-2", NextChapterId = "chapter-3" };
            Assert.That(repo.SaveCampaign(slot1, 1, "2026-01-01T00:00:00.0000000Z").Success, Is.True);
            Assert.That(repo.SaveCampaign(slot2, 2, "2026-01-02T00:00:00.0000000Z").Success, Is.True);
            repo.SaveCampaignAutosave(new CampaignSaveData { LastCompletedChapterId = "chapter-auto" }, "2026-01-03T00:00:00.0000000Z");

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out CampaignSaveData loaded1, out _), Is.True);
            Assert.That(loaded1.NextChapterId, Is.EqualTo("chapter-2"));
            repo.SaveCampaign(new CampaignSaveData { LastCompletedChapterId = "chapter-1b", NextChapterId = "chapter-2b" }, 1, "2026-01-04T00:00:00.0000000Z");
            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(2), out CampaignSaveData loaded2, out _), Is.True);
            Assert.That(loaded2.NextChapterId, Is.EqualTo("chapter-3"));
            repo.DeleteCampaign(SavePathUtility.CampaignSlotFileName(1));
            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out _, out SaveSlotInfo deleted), Is.False);
            Assert.That(deleted.State, Is.EqualTo(SaveSlotState.Empty));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleSaveBasic_RestoresHpPositionActedRoundAndIdleState()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.EliminateNoReinforcements, repo, c => context = c);
            Unit knight = context.UnitsByKey["knight"];
            Unit enemy = context.UnitsByKey["enemy"];
            enemy.ReceiveDamage(3);
            knight.SetHasActed(true);
            context.Scenario.State.CompletedRounds = 1;
            context.Scenario.State.CurrentRound = 2;

            BattleSaveData save = BattleSaveSnapshot.Create(context, BattleTestPresetId.EliminateNoReinforcements);
            Assert.That(repo.SaveBattle(save).Success, Is.True);
            Assert.That(repo.TryLoadBattle(out BattleSaveData loaded, out _), Is.True);
            Assert.That(BattleSaveSnapshot.TryRestore(loaded, out BattleRuntimeContext restored, out string error), Is.True, error);

            Assert.That(restored.Turns.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
            Assert.That(restored.Player.CurrentInteractionState, Is.EqualTo(UnitSelectionController.PlayerInteractionState.Idle));
            Assert.That(restored.UnitsByKey["enemy"].CurrentHealth, Is.EqualTo(enemy.CurrentHealth));
            Assert.That(restored.UnitsByKey["knight"].HasActed, Is.True);
            Assert.That(restored.Scenario.CurrentRound, Is.EqualTo(2));
            Assert.That(restored.UnitsByKey["knight"].OccupiedTile.OccupyingUnit, Is.SameAs(restored.UnitsByKey["knight"]));
        }

        [UnityTest]
        public IEnumerator SystemMenu_SaveLoadRestartAndTitleCommandsUseCommandSeam()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.SaveLoadBasic, repo, c => context = c);

            Assert.That(context.SystemMenu.TryOpenSystemMenuAtScreenCenter(), Is.True);
            Assert.That(context.Player.TrySelectUnit(context.UnitsByKey["knight"]), Is.False, "Menu blocks unit selection through IsPlayerInputAllowed.");
            Assert.That(context.SystemMenu.TrySaveBattle(), Is.True, context.SystemMenu.LastMessage);
            Assert.That(repo.ReadBattleSaveInfo().CanLoad, Is.True);
            Assert.That(context.SystemMenu.TryRequestLoadBattle(), Is.True);
            Assert.That(context.SystemMenu.TryConfirmLoadBattle(), Is.True, context.SystemMenu.LastMessage);

            Assert.That(context.SystemMenu.TryOpenSystemMenuAtScreenCenter(), Is.True);
            Assert.That(context.SystemMenu.TryRequestRestartBattle(), Is.True);
            Assert.That(context.SystemMenu.TryConfirmRestartBattle(), Is.True);

            Assert.That(context.SystemMenu.TryOpenSystemMenuAtScreenCenter(), Is.True);
            Assert.That(context.SystemMenu.TryRequestReturnToTitle(), Is.True);
            Assert.That(context.SystemMenu.TryConfirmReturnToTitleWithoutSaving(), Is.True);
        }

        [UnityTest]
        public IEnumerator ContinuePriority_UsesBattleSaveBeforeCampaignInPlayMode()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveCampaign(new CampaignSaveData { LastCompletedChapterId = "chapter-1", NextChapterId = "chapter-2" }, 1, "2026-01-01T00:00:00.0000000Z");
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.EliminateNoReinforcements, repo, c => context = c);
            repo.SaveBattle(BattleSaveSnapshot.Create(context, BattleTestPresetId.EliminateNoReinforcements), "2026-01-02T00:00:00.0000000Z");

            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.BattleSave));
            repo.DeleteBattleSave();
            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.Campaign));
        }

        [UnityTest]
        public IEnumerator SaveRejectedDuringUnstableStates_DoesNotOverwriteExistingSave()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.EliminateNoReinforcements, repo, c => context = c);
            repo.SaveBattle(BattleSaveSnapshot.Create(context, BattleTestPresetId.EliminateNoReinforcements), "2026-01-01T00:00:00.0000000Z");
            string before = repo.ReadBattleSaveInfo().Metadata.SavedAtUtc;

            Assert.That(context.Player.TrySelectUnit(context.UnitsByKey["knight"]), Is.True);
            Assert.That(context.SystemMenu.TryOpenSystemMenuAtScreenCenter(), Is.False);
            Assert.That(BattleSaveEligibility.Evaluate(context.Turns, context.Player).IsAllowed, Is.False);
            Assert.That(repo.ReadBattleSaveInfo().Metadata.SavedAtUtc, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator CrossBattleLoad_WarningDisplayed()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveBattle(new BattleSaveData { BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString(), CurrentRound = 1 }, "2026-01-01T00:00:00.0000000Z");
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.DemoBattle2Protect, repo, c => context = c);

            Assert.That(context.SystemMenu.TryOpenSystemMenuAtScreenCenter(), Is.True);
            Assert.That(context.SystemMenu.TryRequestLoadBattle(), Is.True);
            Assert.That(context.SystemMenu.CrossBattleWarning, Is.Not.Null.And.Contains("different battle"));
        }

        [UnityTest]
        public IEnumerator TwoBattleDemoFlow_SavesAndCompletes()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            BattleRuntimeContext context = null;
            yield return StartScenario(BattleTestPresetId.DemoBattle1Eliminate, repo, c => context = c);

            Assert.That(repo.SaveBattle(BattleSaveSnapshot.Create(context, BattleTestPresetId.DemoBattle1Eliminate)), Is.Not.Null);
            Assert.That(repo.TryLoadBattle(out BattleSaveData data, out _), Is.True);
            Assert.That(data.BattlePresetId, Is.EqualTo(BattleTestPresetId.DemoBattle1Eliminate.ToString()));
        }

        private IEnumerator StartScenario(BattleTestPresetId preset, SaveRepository repo, System.Action<BattleRuntimeContext> loaded)
        {
            Scene scene = SceneManager.CreateScene("SaveLoadScenario" + Time.frameCount);
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

            BattleRuntimeContext context = BattleScenarioRuntimeBuilder.Build(BattleTestPresetLibrary.Create(preset), null, true, repo, preset);
            yield return null;
            yield return null;
            loaded(context);
        }
    }
}
