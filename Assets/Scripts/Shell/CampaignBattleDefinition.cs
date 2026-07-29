using SLG.Scenarios;

namespace SLG.Shell
{
    public sealed class CampaignBattleDefinition
    {
        public BattleTestPresetId Preset;
        public string BattleId;
        public string BattleName;
        public string SceneName;
        public string NextBattleId;
        public bool IsFinalBattle;
    }

    public static class CampaignBattleDefinitions
    {
        static readonly CampaignBattleDefinition[] definitions =
        {
            new CampaignBattleDefinition
            {
                Preset = BattleTestPresetId.DemoBattle1Eliminate,
                BattleId = CampaignBattleIds.Battle1Id,
                BattleName = "Eliminate All Enemies",
                SceneName = "BattleTestTemplate",
                NextBattleId = CampaignBattleIds.Battle2Id,
                IsFinalBattle = false
            },
            new CampaignBattleDefinition
            {
                Preset = BattleTestPresetId.DemoBattle2Protect,
                BattleId = CampaignBattleIds.Battle2Id,
                BattleName = "Protect The Healer",
                SceneName = "BattleTestTemplate",
                NextBattleId = null,
                IsFinalBattle = true
            }
        };

        public static CampaignBattleDefinition GetByPreset(BattleTestPresetId preset)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].Preset == preset)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        public static CampaignBattleDefinition GetByBattleId(string battleId)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].BattleId == battleId)
                {
                    return definitions[i];
                }
            }

            return null;
        }
    }
}
