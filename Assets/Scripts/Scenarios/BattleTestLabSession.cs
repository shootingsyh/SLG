namespace SLG.Scenarios
{
    public static class BattleTestLabSession
    {
        public static BattleSetupConfiguration CurrentConfiguration { get; private set; }
        public static BattleTestPresetId CurrentPreset { get; private set; }
        public static string LabSceneName { get; set; } = "BattleTestLab";
        public static string BattleSceneName { get; set; } = "BattleTestTemplate";

        public static void Store(BattleTestPresetId preset, BattleSetupConfiguration configuration)
        {
            CurrentPreset = preset;
            CurrentConfiguration = configuration != null ? configuration.Clone() : null;
        }
    }
}
