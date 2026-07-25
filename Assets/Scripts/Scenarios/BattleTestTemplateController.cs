using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Scenarios
{
    public sealed class BattleTestTemplateController : MonoBehaviour
    {
        private BattleRuntimeContext context;

        private void Start()
        {
            StartCurrentConfiguration();
        }

        public void StartCurrentConfiguration()
        {
            BattleSetupConfiguration config = BattleTestLabSession.CurrentConfiguration ?? BattleTestPresetLibrary.Create(BattleTestPresetId.FullScenarioSmoke);
            context = BattleScenarioRuntimeBuilder.Build(config, transform, true);
        }

        public void RestartCurrentConfiguration()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        public void ReturnToLab()
        {
            SceneManager.LoadScene(BattleTestLabSession.LabSceneName, LoadSceneMode.Single);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 260f, 20f, 240f, 180f), GUI.skin.box);
            GUILayout.Label(context != null && context.Scenario != null ? context.Scenario.ObjectiveSummary : "Starting scenario...");
            if (GUILayout.Button("Restart Current Configuration"))
            {
                RestartCurrentConfiguration();
            }

            if (GUILayout.Button("Return to Test Lab"))
            {
                ReturnToLab();
            }

            GUILayout.EndArea();
        }
    }
}
