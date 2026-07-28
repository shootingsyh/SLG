namespace SLG.Saves
{
    public static class GameShellServices
    {
        private static SaveRepository repository;
        private static BattleSaveData pendingBattleSave;
        private static CampaignSaveData pendingCampaignData;
        private static bool isDemoComplete;
        private static DemoFlowState demoState = DemoFlowState.NotStarted;

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

        public static DemoFlowState GetDemoState()
        {
            return demoState;
        }

        public static void SetDemoState(DemoFlowState state)
        {
            demoState = state;
        }

        public static void SetPendingBattleSave(BattleSaveData data)
        {
            pendingBattleSave = data;
        }

        public static bool TryConsumePendingBattleSave(out BattleSaveData data)
        {
            data = pendingBattleSave;
            pendingBattleSave = null;
            return data != null;
        }

        public static void SetPendingCampaignData(CampaignSaveData data, bool demoComplete)
        {
            pendingCampaignData = data;
            isDemoComplete = demoComplete;
        }

        public static CampaignSaveData GetPendingCampaignData()
        {
            return pendingCampaignData;
        }

        public static bool IsPendingDemoComplete()
        {
            return isDemoComplete;
        }

        public static void Clear()
        {
            pendingCampaignData = null;
            isDemoComplete = false;
            demoState = DemoFlowState.NotStarted;
        }
    }
}
