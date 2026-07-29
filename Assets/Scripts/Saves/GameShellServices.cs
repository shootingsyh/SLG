using System;
using System.Threading;

namespace SLG.Saves
{
    public static class GameShellServices
    {
        private static SaveRepository repository;
        private static BattleSaveData pendingBattleSave;
        private static CampaignSaveData pendingCampaignData;
        private static bool isDemoComplete;
        private static DemoFlowState demoState = DemoFlowState.NotStarted;
        private static string activeGameId = string.Empty;
        private static string interGameDestinationScene = string.Empty;

        public static SaveRepository Repository
        {
            get
            {
                repository ??= new SaveRepository(FileSaveStorage.CreateProduction());
                return repository;
            }
        }

        public static void UseRepositoryForTests(SaveRepository testRepository)
        {
            repository = testRepository;
        }

        public static string ActiveGameId
        {
            get => activeGameId;
            set => activeGameId = value ?? string.Empty;
        }

        public static string InterGameDestinationScene
        {
            get => interGameDestinationScene;
            set => interGameDestinationScene = value ?? string.Empty;
        }

        public static DemoFlowState GetDemoState() => demoState;
        public static void SetDemoState(DemoFlowState state) => demoState = state;

        public static void SetPendingBattleSave(BattleSaveData data) => pendingBattleSave = data;

        public static bool TryConsumePendingBattleSave(out BattleSaveData data)
        {
            data = pendingBattleSave;
            pendingBattleSave = null;
            return data != null;
        }

        public static void SetPendingCampaignData(CampaignSaveData data, bool demoComplete, string gameId = null)
        {
            pendingCampaignData = data;
            isDemoComplete = demoComplete;
            if (!string.IsNullOrEmpty(gameId))
                activeGameId = gameId;
        }

        public static CampaignSaveData GetPendingCampaignData() => pendingCampaignData;
        public static bool IsPendingDemoComplete() => isDemoComplete;

        public static void SetInterGameState(string gameId, string completedBattleId, string nextBattleId)
        {
            activeGameId = gameId ?? string.Empty;
            interGameDestinationScene = string.Empty;
            pendingCampaignData = null;
            isDemoComplete = false;
        }

        public static void Clear()
        {
            pendingCampaignData = null;
            pendingBattleSave = null;
            isDemoComplete = false;
            demoState = DemoFlowState.NotStarted;
            activeGameId = string.Empty;
            interGameDestinationScene = string.Empty;
        }

        public static bool TryResetTestGameData()
        {
            string testId = "test-1";
            if (!Shell.GameDefinitions.Has(testId))
                return false;
            Clear();
            ActiveGameId = testId;
            return true;
        }
    }
}
