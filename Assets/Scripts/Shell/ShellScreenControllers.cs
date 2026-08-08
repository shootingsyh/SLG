using System;
using System.Collections.Generic;
using SLG.Saves;
using SLG.Scenarios;
using UnityEngine;

namespace SLG.Shell
{
    public sealed class BootController : MonoBehaviour
    {
        private readonly GameFlowService flow = new GameFlowService();
        private bool advanced;

        public bool TryAdvance()
        {
            if (advanced) return false;
            advanced = flow.TryAdvanceBoot();
            return advanced;
        }

        private void Update()
        {
            if (!advanced && HasAdvanceInput()) TryAdvance();
        }

        private static bool HasAdvanceInput()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame || UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown || Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(0, Screen.height * 0.35f, Screen.width, 160f));
            GUILayout.Label("SLG Tactical RPG", GUI.skin.box);
            GUILayout.Label("Press Any Key", GUI.skin.box);
            GUILayout.EndArea();
        }
    }

    public sealed class TitleController : MonoBehaviour
    {
        private MainMenuModel model;
        private readonly GameFlowService flow = new GameFlowService();
        private bool showLoadSlots;
        private bool showNewGameWarning;
        private string newGameWarningText = string.Empty;
        private string message = string.Empty;
#if UNITY_EDITOR
        private bool showGameSelection;
#endif

        private void Awake() => model = new MainMenuModel(GameShellServices.Repository);

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(40f, 40f, 320f, 460f), GUI.skin.box);

            GUI.enabled = model.CanContinue;
            if (GUILayout.Button(model.ContinueLabel)) RunMenuAction(flow.TryContinue);
            GUI.enabled = true;

            if (GUILayout.Button("New Game")) TryStartNewGame();

            GUI.enabled = model.CanLoadGame;
            if (GUILayout.Button("Load Game")) showLoadSlots = !showLoadSlots;
            GUI.enabled = true;
            if (showLoadSlots) DrawLoadSlots();

#if UNITY_EDITOR
            if (GUILayout.Button("[DEV] Game Selection")) showGameSelection = !showGameSelection;
#endif
            if (model.ShowTestLab && GUILayout.Button("Test Lab")) flow.TryLoadTestLab();
            if (GUILayout.Button("Quit"))
            {
#if UNITY_EDITOR
                Debug.Log("Quit requested.");
#else
                Application.Quit();
#endif
            }
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            GUILayout.EndArea();

#if UNITY_EDITOR
            if (showGameSelection) DrawGameSelection();
#endif

            if (showNewGameWarning)
            {
                Rect modalRect = new Rect(Screen.width * 0.25f, Screen.height * 0.35f, 500f, 130f);
                GUILayout.BeginArea(modalRect, GUI.skin.box);
                GUILayout.Label("New Game Warning");
                GUILayout.Label(newGameWarningText);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes"))
                {
                    showNewGameWarning = false;
                    RunMenuAction(flow.TryStartNewGame);
                }
                if (GUILayout.Button("No")) showNewGameWarning = false;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawLoadSlots()
        {
            GUILayout.Label("Load Game");
            foreach (SaveSlotInfo slot in GameShellServices.Repository.ListCampaignSlots())
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = slot.CanLoad;
                string label = slot.CanLoad ? $"{slot.SlotId}: {slot.Metadata.ProgressLabel}" : $"{slot.SlotId}: {slot.State}";
                if (GUILayout.Button(label)) RunMenuAction(() => flow.TryLoadCampaignSave(slot.FileName));
                GUI.enabled = slot.State != SaveSlotState.Empty;
                if (GUILayout.Button("Delete", GUILayout.Width(70f)))
                {
                    SaveOperationResult result = GameShellServices.Repository.DeleteCampaign(slot.FileName);
                    message = result.Message;
                    model.Refresh();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

#if UNITY_EDITOR
        private void DrawGameSelection()
        {
            Rect rect = new Rect(Screen.width * 0.2f, Screen.height * 0.15f, 350f, 280f);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("Game Selection (Development)");
            GUILayout.Label("Select a game to play.");

            // Production game
            GameDefinition prodGame = GameDefinitions.GetDefault();
            if (prodGame != null)
            {
                GUI.enabled = true;
                if (GUILayout.Button($"▶ {prodGame.DisplayName} ({prodGame.BattleCount} battles)"))
                {
                    showGameSelection = false;
                    RunMenuAction(() => flow.TryStartGame(prodGame.GameId));
                }
            }

            // Test games
            GameDefinition testGame = GameDefinitions.Get("test-1");
            if (testGame != null)
            {
                GUI.color = new Color(1f, 0.9f, 0.2f);
                if (GUILayout.Button($"▶ {testGame.DisplayName} ({testGame.BattleCount} battles)"))
                {
                    showGameSelection = false;
                    RunMenuAction(() => flow.TryStartGame(testGame.GameId));
                }
                GUI.color = Color.white;
            }

            if (GUILayout.Button("Back")) showGameSelection = false;
            GUILayout.EndArea();
        }
#endif

        private void RunMenuAction(Func<bool> action)
        {
            if (!action()) { message = flow.LastError; model.Refresh(); }
        }

        private void TryStartNewGame()
        {
            SaveSlotInfo battleSaveInfo = GameShellServices.Repository.ReadBattleSaveInfo();
            if (battleSaveInfo.CanLoad)
            {
                showNewGameWarning = true;
                newGameWarningText = $"Active battle save will be replaced. Battle: {battleSaveInfo.Metadata?.BattleName ?? "Unknown"}, Round {battleSaveInfo.Metadata?.Round}. Continue anyway?";
            }
            else
            {
                RunMenuAction(flow.TryStartNewGame);
            }
        }
    }

    public sealed class ChapterSelectController : MonoBehaviour
    {
        private readonly GameFlowService flow = new GameFlowService();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(40f, 40f, 260f, 160f), GUI.skin.box);
            GUILayout.Label("Chapter Select");
            if (GUILayout.Button("Chapter 1")) flow.TryStartNewGame();
            if (GUILayout.Button("Back")) flow.TryLoadTitleScene();
            GUILayout.EndArea();
        }
    }

    public sealed class ChapterResultController : MonoBehaviour
    {
        private CampaignSaveData campaignData;
        private bool isDemoComplete;
        private string message = string.Empty;
        private bool saved;
        private readonly GameFlowService flow = new GameFlowService();

        private void Awake()
        {
            campaignData = GameShellServices.GetPendingCampaignData();
            isDemoComplete = GameShellServices.IsPendingDemoComplete();
        }

        public bool SaveProgressToSlot(int slot)
        {
            if (campaignData == null)
            {
                message = "No save data available.";
                return false;
            }

            SaveOperationResult result = GameShellServices.Repository.SaveCampaign(campaignData, slot);
            message = result.Message;
            if (result.Success)
            {
                saved = true;
                GameShellServices.Repository.DeleteBattleSave();
            }
            return result.Success;
        }

        private void OnGUI()
        {
            bool centered = Screen.width > 600;
            float areaWidth = centered ? Screen.width * 0.5f : Screen.width - 80f;
            float areaHeight = 300f;
            float x = (Screen.width - areaWidth) * 0.5f;
            float y = (Screen.height - areaHeight) * 0.5f;
            GUILayout.BeginArea(new Rect(x, y, areaWidth, areaHeight), GUI.skin.box);

            if (isDemoComplete)
            {
                GUILayout.Label("Game Complete");
                GUILayout.Label("Congratulations! You have finished the demo.");
            }
            else
            {
                GUILayout.Label("Victory");
                GUILayout.Label("Chapter complete — Battle won!");
            }

            if (campaignData != null && !saved)
            {
                GUILayout.Space(12f);
                GUILayout.Label("Save Progress:");
                GUILayout.BeginHorizontal();
                for (int i = 1; i <= SaveConstants.ManualCampaignSlotCount; i++)
                {
                    if (GUILayout.Button("Slot " + i)) SaveProgressToSlot(i);
                }
                GUILayout.EndHorizontal();
            }
            else if (campaignData != null && saved)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Saved!");
            }

            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);

            GUILayout.FlexibleSpace();
            if (isDemoComplete)
            {
                if (GUILayout.Button("Return to Title")) ReturnToTitle();
            }
            else
            {
                if (GUILayout.Button("Continue to Next Chapter")) ContinueToNextChapter();
            }

            GUILayout.EndArea();
        }

        private void ReturnToTitle()
        {
            flow.TryReturnToTitle();
        }

        private void ContinueToNextChapter()
        {
            flow.TryContinueToNextBattle();
        }

        public bool TryContinueToNextBattle()
        {
            return flow.TryContinueToNextBattle();
        }
    }

}
