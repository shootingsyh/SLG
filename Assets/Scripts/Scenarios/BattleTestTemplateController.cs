using UnityEngine;
using UnityEngine.SceneManagement;
using SLG.Core;
using SLG.Saves;
using SLG.Shell;

namespace SLG.Scenarios
{
    public sealed class BattleTestTemplateController : MonoBehaviour
    {
        private BattleRuntimeContext context;
        private CampaignFlowService campaignFlow;

        private void Start()
        {
            if (GameShellServices.TryConsumePendingBattleSave(out BattleSaveData battleSave))
            {
                if (BattleSaveSnapshot.TryRestore(battleSave, out context, out string error))
                {
                    return;
                }

                Debug.LogError(error);
            }

            StartCurrentConfiguration();
            WireCampaignFlow();
        }

        private void WireCampaignFlow()
        {
            BattleTestPresetId preset = BattleTestLabSession.CurrentPreset;
            if (preset == BattleTestPresetId.DemoBattle1Eliminate || preset == BattleTestPresetId.DemoBattle2Protect)
            {
                GameFlowService flow = new GameFlowService();
                SaveRepository repository = GameShellServices.Repository;
                campaignFlow = new CampaignFlowService(flow, repository);
                campaignFlow.ConfigureBattle(preset);

#pragma warning disable 0618
                BattleTurnController turns = FindObjectOfType<BattleTurnController>();
#pragma warning restore 0618
                CampaignBatch.RegisterProcessor(turns, campaignFlow);
            }
        }

        public void StartCurrentConfiguration()
        {
            BattleSetupConfiguration config = BattleTestLabSession.CurrentConfiguration ?? BattleTestPresetLibrary.Create(BattleTestPresetId.FullScenarioSmoke);
            context = BattleScenarioRuntimeBuilder.Build(config, transform, true, null, BattleTestLabSession.CurrentPreset);
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
