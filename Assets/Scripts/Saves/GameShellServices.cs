using System;
using System.Collections.Generic;
using System.Threading;

namespace SLG.Saves
{
    public static class GameShellServices
    {
        private static SaveRepository repository;
        private static bool isUsingTestRepository;
        private static BattleSaveData pendingBattleSave;
        private static CampaignSaveData pendingCampaignData;
        private static bool isDemoComplete;
        private static DemoFlowState demoState = DemoFlowState.NotStarted;
        private static string activeGameId = string.Empty;
        private static string interGameDestinationScene = string.Empty;
        private static string interGameCompletedBattleId = string.Empty;
        private static string interGameNextBattleId = string.Empty;
        private static Items.CampaignInventory campaignInventory = new Items.CampaignInventory();
        private static Items.CampaignEquipment campaignEquipment = new Items.CampaignEquipment();
        private static HashSet<string> claimedRewardBattleIds = new HashSet<string>();

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
            isUsingTestRepository = true;
        }

        public static bool IsUsingTestRepository => isUsingTestRepository;

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

        public static string InterGameCompletedBattleId => interGameCompletedBattleId;
        public static string InterGameNextBattleId => interGameNextBattleId;

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
            if (data != null)
            {
                campaignInventory.FromEntries(data.Inventory);
                campaignEquipment.FromEntries(data.Equipment, campaignInventory);
                claimedRewardBattleIds.Clear();
                if (data.ClaimedRewardBattleIds != null)
                    foreach (var id in data.ClaimedRewardBattleIds) claimedRewardBattleIds.Add(id);
            }
        }

        public static CampaignSaveData GetPendingCampaignData() => pendingCampaignData;
        public static bool IsPendingDemoComplete() => isDemoComplete;

        public static Items.CampaignInventory CampaignInventory => campaignInventory;
        public static Items.CampaignEquipment CampaignEquipment => campaignEquipment;
        public static IReadOnlyCollection<string> ClaimedRewards => claimedRewardBattleIds;

        public static bool IsRewardClaimed(string battleId) => !string.IsNullOrEmpty(battleId) && claimedRewardBattleIds.Contains(battleId);
        public static void MarkRewardClaimed(string battleId)
        {
            if (!string.IsNullOrEmpty(battleId)) claimedRewardBattleIds.Add(battleId);
        }
        public static void SetClaimedRewards(IEnumerable<string> ids)
        {
            claimedRewardBattleIds.Clear();
            if (ids == null) return;
            foreach (var id in ids) if (!string.IsNullOrEmpty(id)) claimedRewardBattleIds.Add(id);
        }

        public static CampaignSaveData BuildCampaignSaveSnapshot(string gameId, string battleId, string nextBattleId)
        {
            var data = new CampaignSaveData
            {
                GameId = gameId ?? activeGameId,
                BattleId = battleId ?? string.Empty,
                NextBattleId = nextBattleId ?? string.Empty,
                Inventory = campaignInventory.ToEntries(),
                Equipment = campaignEquipment.ToEntries(),
                ClaimedRewardBattleIds = new List<string>(claimedRewardBattleIds)
            };
            return data;
        }

        public static void ApplyCampaignSaveData(CampaignSaveData data)
        {
            if (data == null) return;
            campaignInventory.FromEntries(data.Inventory);
            campaignEquipment.FromEntries(data.Equipment, campaignInventory);
            claimedRewardBattleIds.Clear();
            if (data.ClaimedRewardBattleIds != null)
                foreach (var id in data.ClaimedRewardBattleIds) claimedRewardBattleIds.Add(id);
            if (!string.IsNullOrEmpty(data.GameId)) activeGameId = data.GameId;
            interGameCompletedBattleId = data.LastCompletedChapterId ?? string.Empty;
            interGameNextBattleId = data.NextBattleId ?? string.Empty;
        }

        public static void SetInterGameState(string gameId, string completedBattleId, string nextBattleId)
        {
            activeGameId = gameId ?? string.Empty;
            interGameCompletedBattleId = completedBattleId ?? string.Empty;
            interGameNextBattleId = nextBattleId ?? string.Empty;
            interGameDestinationScene = string.Empty;
            pendingCampaignData = null;
            isDemoComplete = false;
        }

        public static void Clear(bool preserveTestRepository = false)
        {
            bool wasUsingTestRepository = isUsingTestRepository;
            pendingCampaignData = null;
            pendingBattleSave = null;
            isDemoComplete = false;
            demoState = DemoFlowState.NotStarted;
            activeGameId = string.Empty;
            interGameDestinationScene = string.Empty;
            interGameCompletedBattleId = string.Empty;
            interGameNextBattleId = string.Empty;
            campaignInventory.Clear();
            campaignEquipment = new Items.CampaignEquipment();
            claimedRewardBattleIds.Clear();
            isUsingTestRepository = preserveTestRepository && wasUsingTestRepository;
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
