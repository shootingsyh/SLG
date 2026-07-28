using System.Collections.Generic;
using NUnit.Framework;
using SLG.Core;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Units;
using UnityEngine;

namespace SLG.Tests
{
    public sealed class SaveSystemTests
    {
        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }

            objects.Clear();
        }

        [Test]
        public void CampaignAndBattleSave_RoundTripWithMetadataAndChecksum()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            CampaignSaveData campaign = new CampaignSaveData { LastCompletedChapterId = "chapter-1", NextChapterId = "chapter-2" };
            Assert.That(repo.SaveCampaign(campaign, 1, "2026-01-01T00:00:00.0000000Z").Success, Is.True);
            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(1), out CampaignSaveData loadedCampaign, out SaveSlotInfo campaignInfo), Is.True);
            Assert.That(loadedCampaign.NextChapterId, Is.EqualTo("chapter-2"));
            Assert.That(campaignInfo.Metadata.ProgressLabel, Does.Contain("chapter-1"));

            BattleSaveData battle = new BattleSaveData { BattlePresetId = BattleTestPresetId.EliminateNoReinforcements.ToString(), CurrentRound = 4, CompletedRounds = 3 };
            battle.Units.Add(new UnitRuntimeSaveData { UnitInstanceId = "knight", UnitDefinitionId = "knight", Faction = UnitFaction.Player, X = 0, Y = 0, CurrentHp = 10, MaxHp = 10, IsAlive = true });
            Assert.That(repo.SaveBattle(battle, "2026-01-02T00:00:00.0000000Z").Success, Is.True);
            Assert.That(repo.TryLoadBattle(out BattleSaveData loadedBattle, out SaveSlotInfo battleInfo), Is.True);
            Assert.That(loadedBattle.CurrentRound, Is.EqualTo(4));
            Assert.That(battleInfo.Metadata.ProgressLabel, Does.Contain("Round 4"));
        }

        [Test]
        public void CorruptChecksumUnsupportedAndWrongType_AreRejectedSafely()
        {
            InMemorySaveStorage storage = new InMemorySaveStorage();
            SaveRepository repo = new SaveRepository(storage);
            CampaignSaveData campaign = new CampaignSaveData();
            repo.SaveCampaign(campaign, 1, "2026-01-01T00:00:00.0000000Z");
            storage.TryReadText(SavePathUtility.CampaignSlotFileName(1), out string json, out _);
            storage.WriteRaw(SavePathUtility.CampaignSlotFileName(2), json.Replace("chapter-1", "chapter-x"));
            storage.WriteRaw(SavePathUtility.CampaignSlotFileName(3), "not json");

            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(2), out _, out SaveSlotInfo checksum), Is.False);
            Assert.That(checksum.State, Is.EqualTo(SaveSlotState.ChecksumMismatch));
            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(3), out _, out SaveSlotInfo corrupt), Is.False);
            Assert.That(corrupt.State, Is.EqualTo(SaveSlotState.Corrupt));

            BattleSaveData battle = new BattleSaveData { BattlePresetId = "bad" };
            string wrongType = SaveSerializer.SerializePayload(SaveConstants.BattleSaveSaveType, battle);
            storage.WriteRaw(SavePathUtility.CampaignSlotFileName(4), wrongType);
            Assert.That(repo.TryLoadCampaign(SavePathUtility.CampaignSlotFileName(4), out _, out SaveSlotInfo wrong), Is.False);
            Assert.That(wrong.State, Is.EqualTo(SaveSlotState.MissingContent));
        }

        [Test]
        public void ContinuePriority_UsesValidSaveThenRecentCampaignAndIgnoresInvalid()
        {
            InMemorySaveStorage storage = new InMemorySaveStorage();
            SaveRepository repo = new SaveRepository(storage);
            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.None));

            repo.SaveCampaign(new CampaignSaveData { LastCompletedChapterId = "chapter-1", NextChapterId = "chapter-2" }, 1, "2026-01-01T00:00:00.0000000Z");
            repo.SaveCampaign(new CampaignSaveData { LastCompletedChapterId = "chapter-2", NextChapterId = "chapter-3" }, 2, "2026-01-02T00:00:00.0000000Z");
            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.Campaign));
            Assert.That(repo.ResolveContinue().Slot.SlotId, Is.EqualTo("slot-02"));

            repo.SaveBattle(new BattleSaveData { BattlePresetId = BattleTestPresetId.EliminateNoReinforcements.ToString(), CurrentRound = 2 }, "2026-01-03T00:00:00.0000000Z");
            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.BattleSave));
            storage.WriteRaw(SaveConstants.BattleSaveFileName, "corrupt");
            Assert.That(repo.ResolveContinue().Kind, Is.EqualTo(ContinueKind.Campaign));
        }

        [Test]
        public void SlotPaths_AreDeterministicDistinctAndValidateRange()
        {
            Assert.That(SavePathUtility.CampaignSlotFileName(1), Is.EqualTo("campaign-slot-01.json"));
            Assert.That(SavePathUtility.CampaignSlotFileName(5), Is.EqualTo("campaign-slot-05.json"));
            Assert.That(SaveConstants.CampaignAutosaveFileName, Is.Not.EqualTo(SavePathUtility.CampaignSlotFileName(1)));
            Assert.That(SaveConstants.BattleSaveFileName, Is.Not.EqualTo(SaveConstants.CampaignAutosaveFileName));
            Assert.That(SavePathUtility.SanitizeFileName("../bad.json"), Is.EqualTo("bad.json"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SavePathUtility.CampaignSlotFileName(0));
        }

        [Test]
        public void SaveEligibility_ReturnsSpecificBlockReasons()
        {
            BattleTurnController turns = Controller<BattleTurnController>();
            UnitSelectionController player = Controller<UnitSelectionController>();
            turns.ConfigureRuntime(null, player, null);
            player.ConfigureRuntime(null, turns);
            turns.RestoreLoadedBattleState(1);

            Assert.That(BattleSaveEligibility.Evaluate(turns, player).IsAllowed, Is.True);
            Assert.That(BattleSaveEligibility.Evaluate(turns, player, modalOpen: true).Reason, Is.EqualTo(BattleSaveBlockReason.ModalOpen));
            Assert.That(BattleSaveEligibility.Evaluate(turns, player, transitionInProgress: true).Reason, Is.EqualTo(BattleSaveBlockReason.TransitionInProgress));

            turns.ForcePhaseForTests(BattlePhase.EnemyTurn);
            Assert.That(BattleSaveEligibility.Evaluate(turns, player).Reason, Is.EqualTo(BattleSaveBlockReason.NotPlayerPhase));
            turns.RestoreLoadedBattleState(1);
            player.ForceInteractionStateForTests(UnitSelectionController.PlayerInteractionState.ChoosingMovement);
            Assert.That(BattleSaveEligibility.Evaluate(turns, player).Reason, Is.EqualTo(BattleSaveBlockReason.PlayerInteractionNotIdle));
        }

        [Test]
        public void BattleSaveValidation_RejectsDuplicateCoordinatesInvalidHpAndEnemyPhase()
        {
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(BattleTestPresetId.EliminateNoReinforcements);
            BattleSaveData data = new BattleSaveData { BattlePresetId = BattleTestPresetId.EliminateNoReinforcements.ToString(), CurrentPhase = BattlePhase.PlayerTurn };
            data.Units.Add(new UnitRuntimeSaveData { UnitInstanceId = "a", UnitDefinitionId = "knight", Faction = UnitFaction.Player, X = 0, Y = 0, CurrentHp = 5, MaxHp = 10, IsAlive = true });
            data.Units.Add(new UnitRuntimeSaveData { UnitInstanceId = "b", UnitDefinitionId = "enemy", Faction = UnitFaction.Enemy, X = 0, Y = 0, CurrentHp = 5, MaxHp = 10, IsAlive = true });
            Assert.That(BattleSaveSnapshot.TryValidate(data, config, out string error), Is.False);
            Assert.That(error, Does.Contain("Duplicate living"));

            data.Units[1].X = 2;
            data.Units[0].CurrentHp = 99;
            Assert.That(BattleSaveSnapshot.TryValidate(data, config, out error), Is.False);
            Assert.That(error, Does.Contain("Invalid HP"));

            data.Units[0].CurrentHp = 5;
            data.CurrentPhase = BattlePhase.EnemyTurn;
            Assert.That(BattleSaveSnapshot.TryValidate(data, config, out error), Is.False);
            Assert.That(error, Does.Contain("Player phase"));
        }

        [Test]
        public void CampaignSlotListing_DeleteAndAutosave_AreIndependent()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveCampaign(new CampaignSaveData { LastCompletedChapterId = "chapter-1" }, 1, "2026-01-01T00:00:00.0000000Z");
            repo.SaveCampaignAutosave(new CampaignSaveData { LastCompletedChapterId = "chapter-auto" }, "2026-01-02T00:00:00.0000000Z");
            IReadOnlyList<SaveSlotInfo> slots = repo.ListCampaignSlots();
            Assert.That(slots.Count, Is.EqualTo(6));
            Assert.That(slots[0].CanLoad, Is.True);
            Assert.That(slots[5].CanLoad, Is.True);
            repo.DeleteCampaign(SavePathUtility.CampaignSlotFileName(1));
            slots = repo.ListCampaignSlots();
            Assert.That(slots[0].State, Is.EqualTo(SaveSlotState.Empty));
            Assert.That(slots[5].CanLoad, Is.True);
        }

        [Test]
        public void GlobalBattleSave_PersistsAcrossBattleCompletion()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveBattle(new BattleSaveData { BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString(), CurrentRound = 1 }, "2026-01-01T00:00:00.0000000Z");
            Assert.That(repo.TryLoadBattle(out BattleSaveData data, out _), Is.True);
            Assert.That(data.BattlePresetId, Is.EqualTo(BattleTestPresetId.DemoBattle1Eliminate.ToString()));
        }

        [Test]
        public void CrossBattleLoad_GeneratesWarning()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveBattle(new BattleSaveData { BattlePresetId = BattleTestPresetId.DemoBattle1Eliminate.ToString(), CurrentRound = 1 }, "2026-01-01T00:00:00.0000000Z");
            SaveSlotInfo info = repo.TryLoadBattleWithCrossBattleCheck(BattleTestPresetId.DemoBattle2Protect, out _, out info) ? info : new SaveSlotInfo();
            Assert.That(info, Is.Not.Null);
        }

        [Test]
        public void DeleteBattleSave_RemovesGlobalSave()
        {
            SaveRepository repo = new SaveRepository(new InMemorySaveStorage());
            repo.SaveBattle(new BattleSaveData { BattlePresetId = BattleTestPresetId.EliminateNoReinforcements.ToString(), CurrentRound = 1 }, "2026-01-01T00:00:00.0000000Z");
            Assert.That(repo.TryLoadBattle(out _, out _), Is.True);
            Assert.That(repo.DeleteBattleSave().Success, Is.True);
            Assert.That(repo.TryLoadBattle(out _, out _), Is.False);
        }

        [Test]
        public void DemoFlowState_ValuesExist()
        {
            Assert.That((int)DemoFlowState.NotStarted, Is.EqualTo(0));
            Assert.That((int)DemoFlowState.Battle1Complete, Is.EqualTo(1));
            Assert.That((int)DemoFlowState.DemoComplete, Is.EqualTo(3));
        }

        private T Controller<T>() where T : Component
        {
            GameObject go = new GameObject(typeof(T).Name);
            objects.Add(go);
            return go.AddComponent<T>();
        }
    }
}
