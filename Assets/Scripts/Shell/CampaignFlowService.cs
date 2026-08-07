using System;
using System.Collections.Generic;
using SLG.Core;
using SLG.Saves;
using SLG.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Shell
{
    public sealed class CampaignFlowService
    {
        private readonly GameFlowService flow;
        private readonly SaveRepository repository;
        private readonly ISceneLoader sceneLoader;
        private BattleTestPresetId currentBattlePreset;
        private bool resultProcessing;
        private bool resultProcessed;
        private string expectedSceneName;

        public CampaignFlowService(GameFlowService flowService, SaveRepository saveRepository)
            : this(flowService, saveRepository, new UnitySceneLoader())
        {
        }

        public CampaignFlowService(GameFlowService flowService, SaveRepository saveRepository, ISceneLoader loader)
        {
            flow = flowService;
            repository = saveRepository;
            sceneLoader = loader;
        }

        public void ConfigureBattle(BattleTestPresetId preset)
        {
            if (!Enum.IsDefined(typeof(BattleTestPresetId), preset))
            {
                return;
            }

            currentBattlePreset = preset;
            resultProcessing = false;
            resultProcessed = false;
        }

        public string ResolveDestination(DemoFlowState state)
        {
            switch (state)
            {
                case DemoFlowState.Battle1Complete:
                case DemoFlowState.Battle2Complete:
                    return CampaignSceneNames.ChapterResult.ToString();
                default:
                    return CampaignSceneNames.Title.ToString();
            }
        }

        public bool TryProcessVictory()
        {
            return TryProcessVictoryInternal(true);
        }

        public bool TryProcessVictoryStateOnly()
        {
            return TryProcessVictoryInternal(false);
        }

        private bool TryProcessVictoryInternal(bool loadScene)
        {
            if (resultProcessing || resultProcessed)
            {
                return false;
            }

            if (flow.IsTransitionInProgress)
            {
                return false;
            }

            CampaignBattleDefinition definition = ResolveCurrentBattle();
            if (definition == null)
            {
                flow.LastError = "Cannot resolve current battle definition for victory processing.";
                return false;
            }

            resultProcessing = true;

            try
            {
                CampaignSaveData data = BuildCampaignData(definition, DemoResultType.Victory);

                SaveOperationResult saveOperation = repository.SaveCampaign(data, DemoSaveSlotId);
                if (!saveOperation.Success)
                {
                    flow.LastError = $"Campaign save failed: {saveOperation.Message}";
                    return false;
                }

                flow.DemoState = IsFinalBattle(definition) ? DemoFlowState.Battle2Complete : DemoFlowState.Battle1Complete;

                GameShellServices.SetPendingCampaignData(data, IsFinalBattle(definition), data.GameId);

                if (!loadScene)
                {
                    resultProcessed = true;
                    resultProcessing = false;
                    return true;
                }

                string destinationScene = CampaignSceneNames.ChapterResult.ToString();
                bool transitionSucceeded = TryLoadScene(destinationScene, ShellScreen.ChapterResult);

                resultProcessed = true;
                return transitionSucceeded;
            }
            catch (Exception ex)
            {
                flow.LastError = $"Victory processing error: {ex.Message}";
                resultProcessing = false;
                return false;
            }
        }

        public bool TryTransitionToTitle()
        {
            if (resultProcessing || resultProcessed)
            {
                return false;
            }

            if (flow.IsTransitionInProgress)
            {
                return false;
            }

            resultProcessing = true;
            GameShellServices.Clear();
            return TryLoadScene(CampaignSceneNames.Title.ToString(), ShellScreen.MainMenu);
        }

        public bool TryProcessDefeat()
        {
            return TryProcessDefeatInternal(true);
        }

        public bool TryProcessDefeatStateOnly()
        {
            return TryProcessDefeatInternal(false);
        }

        private bool TryProcessDefeatInternal(bool loadScene)
        {
            if (resultProcessing || resultProcessed)
            {
                return false;
            }

            if (flow.IsTransitionInProgress)
            {
                return false;
            }

            if (currentBattlePreset != BattleTestPresetId.DemoBattle1Eliminate &&
                currentBattlePreset != BattleTestPresetId.DemoBattle2Protect)
            {
                return false;
            }

            resultProcessing = true;

            try
            {
                GameShellServices.SetPendingCampaignData(null, false);

                resultProcessed = true;
                resultProcessing = false;

                if (!loadScene)
                    return true;

                return TryLoadScene(CampaignSceneNames.Title.ToString(), ShellScreen.MainMenu);
            }
            catch (Exception ex)
            {
                flow.LastError = $"Defeat processing error: {ex.Message}";
                resultProcessing = false;
                return false;
            }
        }

        private bool TryLoadScene(string sceneName, ShellScreen nextScreen)
        {
            if (flow.IsTransitionInProgress)
            {
                return false;
            }

            if (!sceneLoader.CanLoad(sceneName))
            {
                flow.LastError = $"Scene '{sceneName}' is not available in Build Settings.";
                return false;
            }

            flow.IsTransitionInProgress = true;
            flow.CurrentScreen = nextScreen;
            expectedSceneName = sceneName;

            sceneLoader.Load(sceneName, (success, error) =>
            {
                if (!success && !string.IsNullOrEmpty(error))
                {
                    flow.LastError = $"Scene load failed: {error}";
                }

                SceneManager.sceneLoaded += OnDestinationSceneLoaded;
            });

            return true;
        }

        private void OnDestinationSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, expectedSceneName, StringComparison.Ordinal))
            {
                return;
            }

            SceneManager.sceneLoaded -= OnDestinationSceneLoaded;
            expectedSceneName = null;
            flow.IsTransitionInProgress = false;
        }

        private CampaignBattleDefinition ResolveCurrentBattle()
        {
            return CampaignBattleDefinitions.GetByPreset(currentBattlePreset);
        }

        private static bool IsFinalBattle(CampaignBattleDefinition definition)
        {
            return definition.IsFinalBattle;
        }

        private CampaignSaveData BuildCampaignData(CampaignBattleDefinition definition, DemoResultType result)
        {
            List<string> unlocked = new List<string>();
            unlocked.Add("chapter-1");

            if (definition.BattleId == CampaignBattleIds.Battle2Id || result == DemoResultType.Victory)
            {
                unlocked.Add("chapter-2");
            }

            CampaignSaveData data = new CampaignSaveData();
            data.GameId = string.IsNullOrEmpty(GameShellServices.ActiveGameId) ? "default" : GameShellServices.ActiveGameId;
            data.ChapterId = "chapter-1";
            data.ChapterName = "Demo Campaign";
            data.BattleId = definition.BattleId;
            data.BattleName = definition.BattleName;
            data.LastCompletedChapterId = "chapter-1";
            data.NextChapterId = "chapter-2";
            data.NextBattleId = definition.NextBattleId;
            data.DemoCompleted = definition.IsFinalBattle && result == DemoResultType.Victory;
            data.FlowScreen = GetFlowScreen(definition, result);
            data.UnlockedChapterIds = unlocked;
            data.Roster = new List<CampaignRosterEntry>();
            data.Inventory = new List<CampaignInventoryEntry>();
            data.Metadata = new SaveMetadata();
            data.Metadata.SaveType = SaveConstants.CampaignSaveType;
            data.Metadata.ChapterId = "chapter-1";
            data.Metadata.BattleId = definition.BattleId;
            data.Metadata.FormatVersion = SaveConstants.FormatVersion;
            data.Metadata.SlotId = SavePathUtility.CampaignSlotFileName(DemoSaveSlotId);
            return data;
        }

        private static DemoFlowState GetFlowScreen(CampaignBattleDefinition definition, DemoResultType result)
        {
            if (result != DemoResultType.Victory)
            {
                return DemoFlowState.NotStarted;
            }

            if (definition.IsFinalBattle)
            {
                return DemoFlowState.DemoComplete;
            }

            return DemoFlowState.Battle1Complete;
        }

        private static readonly int DemoSaveSlotId = 1;

        public enum DemoResultType
        {
            None,
            Victory,
            Defeat
        }
    }
}
