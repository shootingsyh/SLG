using SLG.Saves;
using SLG.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Shell
{
    public enum ShellScreen
    {
        Boot,
        MainMenu,
        LoadGame,
        ChapterSelect,
        ChapterResult,
        Battle,
        TestLab
    }

    public sealed class GameFlowService
    {
        public bool IsTransitionInProgress { get; set; }
        public ShellScreen CurrentScreen { get; set; } = ShellScreen.Boot;
        public DemoFlowState DemoState
        {
            get => GameShellServices.GetDemoState();
            set => GameShellServices.SetDemoState(value);
        }
        public string LastError { get; set; } = string.Empty;

        public bool TryAdvanceBoot()
        {
            if (IsTransitionInProgress || CurrentScreen != ShellScreen.Boot)
            {
                return false;
            }

            return TryLoadScene("Title", ShellScreen.MainMenu);
        }

        public bool TryLoadTitleScene()
        {
            return TryLoadScene("Title", ShellScreen.MainMenu);
        }

        public bool TryLoadChapterSelect()
        {
            return TryLoadScene("ChapterSelect", ShellScreen.ChapterSelect);
        }

        public bool TryStartNewGame()
        {
            DemoState = DemoFlowState.NotStarted;
            return TryStartDemoBattle1();
        }

        public bool TryStartDemoBattle1()
        {
            if (IsTransitionInProgress)
            {
                return false;
            }

            BattleTestLabSession.Store(BattleTestPresetId.DemoBattle1Eliminate, BattleTestPresetLibrary.Create(BattleTestPresetId.DemoBattle1Eliminate));
            return TryLoadScene("BattleTestTemplate", ShellScreen.Battle);
        }

        public bool TryContinueToChapterResult()
        {
            GameShellServices.Repository.DeleteBattleSave();
            return TryLoadScene("ChapterResult", ShellScreen.ChapterResult);
        }

        public bool TryContinueToNextBattle()
        {
            if (IsTransitionInProgress || DemoState != DemoFlowState.Battle1Complete)
            {
                return false;
            }

            DemoState = DemoFlowState.Battle2Complete;
            return TryStartDemoBattle2();
        }

        public bool TryReturnToTitle()
        {
            GameShellServices.Repository.DeleteBattleSave();
            GameShellServices.Clear();
            return TryLoadTitleScene();
        }

        public bool TryStartDemoBattle2()
        {
            if (IsTransitionInProgress)
            {
                return false;
            }

            BattleTestLabSession.Store(BattleTestPresetId.DemoBattle2Protect, BattleTestPresetLibrary.Create(BattleTestPresetId.DemoBattle2Protect));
            return TryLoadScene("BattleTestTemplate", ShellScreen.Battle);
        }

        public bool TryCompleteDemo()
        {
            if (IsTransitionInProgress || DemoState != DemoFlowState.Battle2Complete)
            {
                return false;
            }

            DemoState = DemoFlowState.DemoComplete;
            GameShellServices.Repository.DeleteBattleSave();
            return TryLoadTitleScene();
        }

        public bool TryContinue()
        {
            ContinueResolution resolution = GameShellServices.Repository.ResolveContinue();
            if (resolution == null || !resolution.CanContinue)
            {
                LastError = "No valid save is available to continue.";
                return false;
            }

            if (resolution.Kind == ContinueKind.BattleSave)
            {
                if (!GameShellServices.Repository.TryLoadBattle(out BattleSaveData battle, out _))
                {
                    LastError = "Battle save could not be loaded.";
                    return false;
                }

                GameShellServices.SetPendingBattleSave(battle);
                return TryLoadScene("BattleTestTemplate", ShellScreen.Battle);
            }

            if (resolution.Slot != null && resolution.Slot.Metadata != null)
            {
                DemoFlowState savedDemoState = resolution.Slot.Metadata.FlowScreen;
                if (savedDemoState == DemoFlowState.Battle1Complete)
                {
                    DemoState = DemoFlowState.Battle1Complete;
                    return TryLoadScene("ChapterResult", ShellScreen.ChapterResult);
                }

                if (savedDemoState == DemoFlowState.Battle2Complete || savedDemoState == DemoFlowState.DemoComplete)
                {
                    DemoState = DemoFlowState.Battle2Complete;
                    GameShellServices.SetPendingCampaignData(null, true);
                    return TryLoadScene("ChapterResult", ShellScreen.ChapterResult);
                }
            }

            return TryLoadCampaignSave(resolution.Slot.FileName);
        }

        public bool TryLoadCampaignSave(string fileName)
        {
            if (!GameShellServices.Repository.TryLoadCampaign(fileName, out CampaignSaveData campaign, out SaveSlotInfo info))
            {
                LastError = info != null && !string.IsNullOrEmpty(info.Error) ? info.Error : "Campaign save could not be loaded.";
                return false;
            }

            BattleTestPresetId preset = string.IsNullOrEmpty(campaign.NextChapterId) || campaign.NextChapterId == "chapter-1" ? BattleTestPresetId.EliminateNoReinforcements : BattleTestPresetId.FullScenarioSmoke;
            BattleTestLabSession.Store(preset, BattleTestPresetLibrary.Create(preset));
            return TryLoadScene("BattleTestTemplate", ShellScreen.Battle);
        }

        public bool TryLoadTestLab()
        {
            return TryLoadScene("BattleTestLab", ShellScreen.TestLab);
        }

        public bool TryOpenChapterResult(bool victory)
        {
            return TryLoadScene("ChapterResult", ShellScreen.ChapterResult);
        }

        private bool TryLoadScene(string sceneName, ShellScreen nextScreen)
        {
            if (IsTransitionInProgress)
            {
                LastError = "Scene transition already in progress.";
                return false;
            }

            IsTransitionInProgress = true;
            try
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                CurrentScreen = nextScreen;
                LastError = string.Empty;
                return true;
            }
            catch (System.Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
            finally
            {
                IsTransitionInProgress = false;
            }
        }
    }

    public sealed class MainMenuModel
    {
        private readonly SaveRepository repository;

        public MainMenuModel(SaveRepository repository)
        {
            this.repository = repository;
            Refresh();
        }

        public ContinueResolution Continue { get; private set; }
        public bool CanContinue => Continue != null && Continue.CanContinue;
        public bool CanLoadGame { get; private set; }
        public bool ShowTestLab { get; set; } = true;
        public string ContinueLabel { get; private set; } = "Continue";

        public void Refresh()
        {
            Continue = repository.ResolveContinue();
            CanLoadGame = false;
            foreach (SaveSlotInfo slot in repository.ListCampaignSlots())
            {
                CanLoadGame |= slot.CanLoad;
            }

            if (!CanContinue)
            {
                ContinueLabel = "Continue";
            }
            else if (Continue.Kind == ContinueKind.BattleSave)
            {
                ContinueLabel = $"Continue\n{Continue.DestinationLabel}";
            }
            else
            {
                ContinueLabel = $"Continue\n{Continue.DestinationLabel}";
            }
        }
    }
}
