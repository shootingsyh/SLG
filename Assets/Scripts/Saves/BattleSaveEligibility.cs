using SLG.Core;
using SLG.Units;

namespace SLG.Saves
{
    public enum BattleSaveBlockReason
    {
        None,
        MissingBattle,
        BattleEnded,
        NotPlayerPhase,
        EnemyActing,
        PlayerInteractionNotIdle,
        SelectedUnit,
        ProvisionalMovement,
        TransitionInProgress,
        SaveOperationInProgress,
        ModalOpen
    }

    public readonly struct BattleSaveEligibilityResult
    {
        public readonly bool IsAllowed;
        public readonly BattleSaveBlockReason Reason;
        public readonly string UserMessage;

        public BattleSaveEligibilityResult(bool isAllowed, BattleSaveBlockReason reason, string userMessage)
        {
            IsAllowed = isAllowed;
            Reason = reason;
            UserMessage = userMessage;
        }

        public static BattleSaveEligibilityResult Allow() => new BattleSaveEligibilityResult(true, BattleSaveBlockReason.None, "Save allowed.");
        public static BattleSaveEligibilityResult Block(BattleSaveBlockReason reason, string message) => new BattleSaveEligibilityResult(false, reason, message);
    }

    public static class BattleSaveEligibility
    {
        public static BattleSaveEligibilityResult Evaluate(BattleTurnController turns, UnitSelectionController player, bool modalOpen = false, bool transitionInProgress = false, bool saveInProgress = false)
        {
            if (turns == null || player == null)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.MissingBattle, "No active battle is available.");
            }

            if (saveInProgress)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.SaveOperationInProgress, "A save is already in progress.");
            }

            if (transitionInProgress)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.TransitionInProgress, "A scene transition is in progress.");
            }

            if (modalOpen)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.ModalOpen, "Close the confirmation dialog first.");
            }

            if (turns.IsBattleEnded)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.BattleEnded, "Battle has ended.");
            }

            if (turns.CurrentPhase != BattlePhase.PlayerTurn)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.NotPlayerPhase, "You can suspend only during Player phase.");
            }

            if (turns.IsEnemyActing)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.EnemyActing, "Enemy action is resolving.");
            }

            if (player.CurrentInteractionState != UnitSelectionController.PlayerInteractionState.Idle)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.PlayerInteractionNotIdle, $"Cannot suspend during {player.CurrentInteractionState}.");
            }

            if (player.SelectedUnit != null)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.SelectedUnit, "Deselect the current unit first.");
            }

            if (player.HasProvisionalMovement)
            {
                return BattleSaveEligibilityResult.Block(BattleSaveBlockReason.ProvisionalMovement, "Commit or cancel movement before suspending.");
            }

            return BattleSaveEligibilityResult.Allow();
        }
    }
}
