using SLG.Core;
using SLG.Scenarios;
using UnityEngine;

namespace SLG.Saves
{
    public enum BattleSystemModal
    {
        None,
        Load,
        Restart,
        ReturnToTitle
    }

    public enum BattleSystemLoadModal
    {
        None,
        Standard,
        CrossBattle
    }

    public sealed class BattleSystemMenuController : MonoBehaviour
    {
        private BattleRuntimeContext context;
        private SaveRepository repository;
        private BattleTestPresetId presetId;
        private bool isOpen;
        private bool transitionInProgress;
        private bool saveInProgress;
        private Vector2 anchor;
        private string crossBattleWarning;

        public bool IsOpen => isOpen;
        public BattleSystemModal ActiveModal { get; private set; }
        public BattleSystemLoadModal ActiveLoadModal { get; private set; }
        public string LastMessage { get; private set; } = string.Empty;
        public BattleSaveEligibilityResult LastEligibility { get; private set; }
        public string CrossBattleWarning => crossBattleWarning;

        public void Configure(BattleRuntimeContext context, SaveRepository repository, BattleTestPresetId presetId)
        {
            this.context = context;
            this.repository = repository;
            this.presetId = presetId;
            crossBattleWarning = null;
        }

        public bool TryOpenSystemMenuAtScreenCenter()
        {
            return TryOpenSystemMenu(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        public bool TryOpenSystemMenu(Vector2 screenAnchor)
        {
            LastEligibility = BattleSaveEligibility.Evaluate(context?.Turns, context?.Player, ActiveModal != BattleSystemModal.None, transitionInProgress, saveInProgress);
            if (!LastEligibility.IsAllowed && LastEligibility.Reason != BattleSaveBlockReason.PlayerInteractionNotIdle)
            {
                LastMessage = LastEligibility.UserMessage;
                return false;
            }

            if (context == null || context.Player == null || context.Player.CurrentInteractionState != SLG.Units.UnitSelectionController.PlayerInteractionState.Idle || context.Player.SelectedUnit != null || context.Turns.CurrentPhase != BattlePhase.PlayerTurn || context.Turns.IsBattleEnded)
            {
                LastMessage = "System Menu can open only from Player Idle.";
                return false;
            }

            anchor = screenAnchor;
            isOpen = true;
            ActiveModal = BattleSystemModal.None;
            ActiveLoadModal = BattleSystemLoadModal.None;
            LastMessage = string.Empty;
            crossBattleWarning = null;
            return true;
        }

        public bool TryCloseSystemMenu()
        {
            if (ActiveModal != BattleSystemModal.None || ActiveLoadModal != BattleSystemLoadModal.None)
            {
                return false;
            }

            isOpen = false;
            return true;
        }

        public bool BlocksGameplayInput => isOpen || ActiveModal != BattleSystemModal.None || transitionInProgress || saveInProgress;

        public bool TrySaveBattle()
        {
            LastEligibility = BattleSaveEligibility.Evaluate(context?.Turns, context?.Player, ActiveModal != BattleSystemModal.None, transitionInProgress, saveInProgress);
            if (!LastEligibility.IsAllowed)
            {
                LastMessage = LastEligibility.UserMessage;
                return false;
            }

            saveInProgress = true;
            SaveOperationResult result = repository.SaveBattle(BattleSaveSnapshot.Create(context, presetId));
            saveInProgress = false;
            LastMessage = result.Message;
            return result.Success;
        }

        public bool TryRequestLoadBattle()
        {
            if (!repository.TryLoadBattleWithCrossBattleCheck(presetId, out _, out SaveSlotInfo info))
            {
                LastMessage = "No valid save exists.";
                crossBattleWarning = null;
                return false;
            }

            if (!string.IsNullOrEmpty(info.CrossBattleWarning))
            {
                ActiveLoadModal = BattleSystemLoadModal.CrossBattle;
                crossBattleWarning = info.CrossBattleWarning;
            }
            else
            {
                ActiveLoadModal = BattleSystemLoadModal.Standard;
                crossBattleWarning = null;
            }

            return true;
        }

        public bool TryConfirmLoadBattle()
        {
            if (ActiveLoadModal != BattleSystemLoadModal.Standard && ActiveLoadModal != BattleSystemLoadModal.CrossBattle)
            {
                return false;
            }

            transitionInProgress = true;
            BattleRuntimeContext loaded = null;
            string error = string.Empty;
            bool ok = repository.TryLoadBattle(out BattleSaveData data, out _) && BattleSaveSnapshot.TryRestore(data, out loaded, out error);
            if (ok)
            {
                context = loaded;
                LastMessage = "Battle save loaded.";
            }
            else
            {
                LastMessage = string.IsNullOrEmpty(error) ? "Failed to load battle save." : error;
            }

            transitionInProgress = false;
            ActiveLoadModal = BattleSystemLoadModal.None;
            ActiveModal = BattleSystemModal.None;
            isOpen = false;
            return ok;
        }

        public bool TryCancelLoadBattle()
        {
            ActiveLoadModal = BattleSystemLoadModal.None;
            crossBattleWarning = null;
            return true;
        }

        public bool TryRequestRestartBattle()
        {
            ActiveModal = BattleSystemModal.Restart;
            return true;
        }

        public bool TryConfirmRestartBattle()
        {
            if (ActiveModal != BattleSystemModal.Restart)
            {
                return false;
            }

            transitionInProgress = true;
            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(presetId);
            context = BattleScenarioRuntimeBuilder.Build(config, null, true);
            context.SystemMenu.Configure(context, repository, presetId);
            transitionInProgress = false;
            ActiveModal = BattleSystemModal.None;
            isOpen = false;
            LastMessage = "Battle restarted.";
            return true;
        }

        public bool TryRequestReturnToTitle()
        {
            ActiveModal = BattleSystemModal.ReturnToTitle;
            return true;
        }

        public bool TrySaveAndReturnToTitle()
        {
            if (ActiveModal != BattleSystemModal.ReturnToTitle || !TrySaveBattle())
            {
                return false;
            }

            return TryConfirmReturnToTitleWithoutSaving();
        }

        public bool TryConfirmReturnToTitleWithoutSaving()
        {
            if (ActiveModal != BattleSystemModal.ReturnToTitle)
            {
                return false;
            }

            transitionInProgress = true;
            ActiveModal = BattleSystemModal.None;
            isOpen = false;
            LastMessage = "Returned to title.";
            transitionInProgress = false;
            return true;
        }

        private void OnGUI()
        {
            if (!isOpen)
            {
                return;
            }

            Rect rect = new Rect(Mathf.Clamp(anchor.x - 90f, 8f, Screen.width - 180f), Mathf.Clamp(anchor.y - 90f, 8f, Screen.height - 180f), 180f, 180f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            if (GUILayout.Button("Save")) TrySaveBattle();
            if (GUILayout.Button("Load")) TryRequestLoadBattle();
            if (GUILayout.Button("Restart")) TryRequestRestartBattle();
            if (GUILayout.Button("Title")) TryRequestReturnToTitle();
            if (!string.IsNullOrEmpty(LastMessage)) GUILayout.Label(LastMessage);
            GUILayout.EndArea();

            if (ActiveLoadModal == BattleSystemLoadModal.Standard)
            {
                Rect modalRect = new Rect(Screen.width * 0.3f, Screen.height * 0.35f, 300f, 100f);
                GUILayout.BeginArea(modalRect, GUI.skin.box);
                GUILayout.Label("Load save?");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes")) TryConfirmLoadBattle();
                if (GUILayout.Button("No")) TryCancelLoadBattle();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            if (ActiveLoadModal == BattleSystemLoadModal.CrossBattle)
            {
                Rect modalRect = new Rect(Screen.width * 0.25f, Screen.height * 0.3f, 400f, 150f);
                GUILayout.BeginArea(modalRect, GUI.skin.box);
                GUILayout.Label("Cross-Battle Load");
                GUILayout.Label(crossBattleWarning);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Load")) TryConfirmLoadBattle();
                if (GUILayout.Button("Cancel")) TryCancelLoadBattle();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }
    }
}
