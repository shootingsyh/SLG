using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Scenarios
{
    public sealed class BattleTestLabController : MonoBehaviour
    {
        [SerializeField] private string battleSceneName = "BattleTestTemplate";

        private BattleTestLabModel model;
        private readonly List<string> validationErrors = new List<string>();
        private Vector2 scroll;
        private int presetIndex;
        private int objectiveIndex;
        private int surviveRounds = 3;

        public BattleTestLabModel Model => model;
        public string ValidationText { get; private set; } = string.Empty;

        private void Awake()
        {
            model = new BattleTestLabModel();
        }

        private void Start()
        {
            BattleTestLabSession.LabSceneName = SceneManager.GetActiveScene().name;
            BattleTestLabSession.BattleSceneName = battleSceneName;
        }

        public bool TryStartBattle()
        {
            validationErrors.Clear();
            if (!model.Validate(validationErrors))
            {
                ValidationText = string.Join("\n", validationErrors);
                return false;
            }

            BattleTestLabSession.Store(model.SelectedPreset, model.RuntimeConfiguration);
            SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
            return true;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, 420f, Screen.height - 40f), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Battle Test Lab");

            IReadOnlyList<BattleTestPresetMetadata> presets = model.Presets;
            string[] names = new string[presets.Count];
            for (int i = 0; i < presets.Count; i++)
            {
                names[i] = presets[i].DisplayName;
            }

            int newPresetIndex = GUILayout.SelectionGrid(presetIndex, names, 1);
            if (newPresetIndex != presetIndex)
            {
                presetIndex = newPresetIndex;
                model.SelectPreset(presets[presetIndex].Id);
                ValidationText = string.Empty;
            }

            GUILayout.Space(8f);
            DrawConfigurationControls();
            GUILayout.Label(BuildSummary());
            if (GUILayout.Button("Reset to Preset"))
            {
                model.ResetToPreset();
                ValidationText = string.Empty;
            }

            if (GUILayout.Button("Start Battle"))
            {
                TryStartBattle();
            }

            if (!string.IsNullOrWhiteSpace(ValidationText))
            {
                GUILayout.Label(ValidationText);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawConfigurationControls()
        {
            GUILayout.Label("Configuration");
            string[] objectives = { "Eliminate All", "Reach Area", "Survive Rounds", "Protect Unit" };
            int nextObjective = GUILayout.SelectionGrid(objectiveIndex, objectives, 1);
            if (nextObjective != objectiveIndex)
            {
                objectiveIndex = nextObjective;
                model.SetObjective(ToObjectiveType(objectiveIndex));
                ValidationText = string.Empty;
            }

            bool aiEnabled = GUILayout.Toggle(model.RuntimeConfiguration.AiEnabled, "AI Enabled");
            if (aiEnabled != model.RuntimeConfiguration.AiEnabled)
            {
                model.SetAiEnabled(aiEnabled);
            }

            if (ToObjectiveType(objectiveIndex) == BattleObjectiveType.SurviveRounds)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Survive Rounds: {surviveRounds}");
                if (GUILayout.Button("-"))
                {
                    surviveRounds = Mathf.Max(1, surviveRounds - 1);
                    model.SetSurviveRounds(surviveRounds);
                }

                if (GUILayout.Button("+"))
                {
                    surviveRounds++;
                    model.SetSurviveRounds(surviveRounds);
                }

                GUILayout.EndHorizontal();
            }
        }

        private static BattleObjectiveType ToObjectiveType(int index)
        {
            switch (index)
            {
                case 1:
                    return BattleObjectiveType.ReachArea;
                case 2:
                    return BattleObjectiveType.SurviveRounds;
                case 3:
                    return BattleObjectiveType.ProtectUnit;
                default:
                    return BattleObjectiveType.EliminateAllEnemies;
            }
        }

        private string BuildSummary()
        {
            BattleSetupConfiguration config = model.RuntimeConfiguration;
            return $"Preset: {config.ScenarioName}\nMap: {config.MapPreset}\nPlayer Formation: {config.PlayerFormation}\nEnemy Formation: {config.EnemyFormation}\nSkills: {config.SkillLoadout}\nObjectives: {config.Objectives.Count + (config.RequireEliminateAllEnemies ? 1 : 0)}\nReinforcements: {config.Reinforcements.Count}\n\nVerify:\n{config.ManualVerificationNotes}";
        }
    }
}
