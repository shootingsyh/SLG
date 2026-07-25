using System.Collections.Generic;
using SLG.Core;

namespace SLG.Scenarios
{
    public sealed class BattleTestLabModel
    {
        private BattleTestPresetId selectedPreset = BattleTestPresetId.MovementBasic;
        private BattleSetupConfiguration presetConfiguration;
        private BattleSetupConfiguration runtimeConfiguration;

        public BattleTestPresetId SelectedPreset => selectedPreset;
        public BattleSetupConfiguration RuntimeConfiguration => runtimeConfiguration;

        public BattleTestLabModel()
        {
            SelectPreset(selectedPreset);
        }

        public IReadOnlyList<BattleTestPresetMetadata> Presets => BattleTestPresetLibrary.Presets;

        public void SelectPreset(BattleTestPresetId preset)
        {
            selectedPreset = preset;
            presetConfiguration = BattleTestPresetLibrary.Create(preset);
            ResetToPreset();
        }

        public void ResetToPreset()
        {
            runtimeConfiguration = presetConfiguration.Clone();
        }

        public void SetObjective(BattleObjectiveType objectiveType)
        {
            runtimeConfiguration.Objectives.Clear();
            runtimeConfiguration.RequireEliminateAllEnemies = objectiveType == BattleObjectiveType.EliminateAllEnemies;
            if (objectiveType == BattleObjectiveType.ReachArea)
            {
                runtimeConfiguration.Objectives.Add(new BattleObjectiveSetup
                {
                    Type = BattleObjectiveType.ReachArea,
                    UnitRole = BattleUnitRole.Knight,
                    DestinationZone = BattleDestinationZonePreset.EastExit,
                    DestinationCoordinates = new List<GridCoordinate> { new GridCoordinate(runtimeConfiguration.Width - 1, runtimeConfiguration.Height / 2) },
                    DesignatedUnitRequired = true
                });
            }
            else if (objectiveType == BattleObjectiveType.SurviveRounds)
            {
                runtimeConfiguration.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.SurviveRounds, RequiredRounds = 3 });
            }
            else if (objectiveType == BattleObjectiveType.ProtectUnit)
            {
                runtimeConfiguration.RequireEliminateAllEnemies = true;
                runtimeConfiguration.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.Healer });
            }
        }

        public void SetSurviveRounds(int rounds)
        {
            for (int i = 0; i < runtimeConfiguration.Objectives.Count; i++)
            {
                if (runtimeConfiguration.Objectives[i].Type == BattleObjectiveType.SurviveRounds)
                {
                    runtimeConfiguration.Objectives[i].RequiredRounds = rounds;
                }
            }
        }

        public void SetAiEnabled(bool enabled)
        {
            runtimeConfiguration.AiEnabled = enabled;
        }

        public bool Validate(List<string> errors)
        {
            return BattleSetupValidator.Validate(runtimeConfiguration, errors);
        }
    }
}
