using System.Collections.Generic;

namespace SLG.Items
{
    public sealed class RewardEntry
    {
        public string ItemId;
        public int Quantity;
        public RewardEntry(string itemId, int quantity) { ItemId = itemId; Quantity = quantity; }
    }

    public static class BattleRewards
    {
        private static readonly Dictionary<string, List<RewardEntry>> rewards = new Dictionary<string, List<RewardEntry>>
        {
            { "battle-1", new List<RewardEntry> { new RewardEntry("potion", 2), new RewardEntry("iron-sword", 1) } },
            { "battle-2", new List<RewardEntry> { new RewardEntry("large-potion", 1), new RewardEntry("iron-armor", 1) } },
            // Also support test battles
            { "test-battle-1", new List<RewardEntry> { new RewardEntry("iron-sword", 1) } },
            { "test-battle-2", new List<RewardEntry> { new RewardEntry("iron-armor", 1) } },
            { "test-battle-3", new List<RewardEntry> { new RewardEntry("traveler-charm", 1) } },
            { "item-test-battle-1", new List<RewardEntry> { new RewardEntry("iron-sword", 1) } },
            { "item-test-battle-2", new List<RewardEntry> { new RewardEntry("iron-armor", 1) } },
            { "item-test-battle-3", new List<RewardEntry> { new RewardEntry("traveler-charm", 1) } },
        };

        public static IReadOnlyList<RewardEntry> GetRewards(string battleId)
        {
            if (string.IsNullOrEmpty(battleId)) return null;
            rewards.TryGetValue(battleId, out var list);
            return list;
        }

        public static bool GrantIfNotClaimed(string battleId)
        {
            if (SLG.Saves.GameShellServices.IsRewardClaimed(battleId)) return false;
            var list = GetRewards(battleId);
            if (list == null) return false;
            foreach (var r in list)
            {
                SLG.Saves.GameShellServices.CampaignInventory.Add(r.ItemId, r.Quantity);
            }
            SLG.Saves.GameShellServices.MarkRewardClaimed(battleId);
            return true;
        }
    }
}
