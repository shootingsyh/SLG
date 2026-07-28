using System;
using System.Collections.Generic;
using SLG.Saves;
using SLG.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Shell
{
    public enum CampaignDestination
    {
        None,
        ChapterResult,
        Title
    }

    public static class CampaignSceneNames
    {
        public const string Title = "Title";
        public const string ChapterResult = "ChapterResult";
        public const string BattleTemplate = "BattleTestTemplate";
    }

    public static class CampaignBattleIds
    {
        public const string Battle1Id = "battle-1";
        public const string Battle2Id = "battle-2";
        public const string ChapterResultSaveLabel = "Chapter Complete";
        public const string GameCompleteSaveLabel = "Game Complete";
    }

    public sealed class CampaignFlowService
    {
        private readonly GameFlowService flow;
        private readonly SaveRepository repository;
        private bool victoryProcessed;
        private bool defeatProcessed;
        private BattleTestPresetId currentBattlePreset;

        public CampaignFlowService(GameFlowService flow, SaveRepository repository)
        {
            this.flow = flow;
            this.repository = repository;
        }

        public void ConfigureBattle(BattleTestPresetId preset)
        {
            currentBattlePreset = preset;
            victoryProcessed = false;
            defeatProcessed = false;
        }

        public CampaignDestination ResolveDestination()
        {
            return ResolveDestination(flow.DemoState);
        }

        public static CampaignDestination ResolveDestination(DemoFlowState demoState)
        {
            return demoState switch
            {
                DemoFlowState.Battle1Complete => CampaignDestination.ChapterResult,
                DemoFlowState.Battle2Complete => CampaignDestination.ChapterResult,
                _ => CampaignDestination.Title
            };
        }

        public bool TryProcessVictory()
        {
            if (victoryProcessed)
            {
                return true;
            }

            victoryProcessed = true;

            if (currentBattlePreset != BattleTestPresetId.DemoBattle1Eliminate &&
                currentBattlePreset != BattleTestPresetId.DemoBattle2Protect)
            {
                return false;
            }

            DemoFlowState nextState = currentBattlePreset == BattleTestPresetId.DemoBattle1Eliminate
                ? DemoFlowState.Battle1Complete
                : DemoFlowState.Battle2Complete;

            flow.DemoState = nextState;

            string battleId = currentBattlePreset == BattleTestPresetId.DemoBattle1Eliminate
                ? CampaignBattleIds.Battle1Id
                : CampaignBattleIds.Battle2Id;
            string battleName = currentBattlePreset == BattleTestPresetId.DemoBattle1Eliminate
                ? "Demo Battle 1 - Eliminate"
                : "Demo Battle 2 - Protect";

            CampaignSaveData data = BuildCampaignData(battleId, battleName, nextState);

            // Store data for ChapterResultController to handle user-initiated save
            GameShellServices.SetPendingCampaignData(data, nextState == DemoFlowState.Battle2Complete);

            return TryTransitionToDestination(ResolveDestination(nextState));
        }

        public bool TryProcessDefeat()
        {
            if (defeatProcessed)
            {
                return true;
            }

            defeatProcessed = true;
            return TryTransitionToTitle();
        }

        private CampaignSaveData BuildCampaignData(string battleId, string battleName, DemoFlowState nextState)
        {
            CampaignSaveData data = new CampaignSaveData
            {
                SlotId = "slot-01",
                ChapterId = "chapter-1",
                ChapterName = "Demo Campaign",
                BattleId = battleId,
                BattleName = battleName,
                LastCompletedChapterId = "chapter-1",
                NextChapterId = "chapter-2",
                NextBattleId = battleId,
                LastSelectedChapterId = "chapter-1",
                FlowScreen = nextState,
                UnlockedChapterIds = new List<string> { "chapter-1", "chapter-2" },
                DemoCompleted = nextState == DemoFlowState.Battle2Complete
            };

            data.Metadata = new SaveMetadata
            {
                SaveType = SaveConstants.CampaignSaveType,
                SlotId = "slot-01",
                ChapterId = "chapter-1",
                ChapterName = "Demo Campaign",
                BattleId = battleId,
                BattleName = battleName,
                ProgressLabel = nextState == DemoFlowState.Battle1Complete
                    ? CampaignBattleIds.ChapterResultSaveLabel
                    : CampaignBattleIds.GameCompleteSaveLabel,
                SavedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                FormatVersion = SaveConstants.FormatVersion
            };

            return data;
        }

        private bool TryTransitionToDestination(CampaignDestination destination)
        {
            switch (destination)
            {
                case CampaignDestination.ChapterResult:
                    return TryLoadScene(CampaignSceneNames.ChapterResult, ShellScreen.ChapterResult);
                case CampaignDestination.Title:
                    return TryLoadScene(CampaignSceneNames.Title, ShellScreen.MainMenu);
                default:
                    return false;
            }
        }

        public bool TryTransitionToTitle()
        {
            return TryLoadScene(CampaignSceneNames.Title, ShellScreen.MainMenu);
        }

        private bool TryLoadScene(string sceneName, ShellScreen nextScreen)
        {
            if (flow.IsTransitionInProgress)
            {
                return false;
            }

            flow.IsTransitionInProgress = true;
            try
            {
                flow.CurrentScreen = nextScreen;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception ex)
            {
                flow.LastError = ex.Message;
                return false;
            }
            finally
            {
                flow.IsTransitionInProgress = false;
            }
        }
    }
}
