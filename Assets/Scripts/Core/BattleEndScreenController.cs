using System.Text;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Shell;
using SLG.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SLG.Core
{
    public sealed class BattleEndScreenController : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject overlayPanel;
        private GameObject overlayBackground;
        private Text titleText;
        private Text subtitleText;
        private Text objectivesText;
        private GameObject continueButtonObject;
        private GameObject exitToTitleButtonObject;
        private Button continueButton;
        private Button exitToTitleButton;

        private BattleTurnController turns;
        private BattleScenarioController scenario;

        public bool IsVisible => overlayPanel != null && overlayPanel.activeSelf;

        private void Awake()
        {
            turns = GetComponent<BattleTurnController>();
            scenario = GetComponent<BattleScenarioController>();
        }

        public void Show(string result, BattleScenarioRuntimeState state, BattleSetupConfiguration config)
        {
            if (overlayPanel == null)
            {
                CreateOverlay();
            }

            bool isVictory = result == "Victory";

            overlayBackground.SetActive(true);
            titleText.text = isVictory ? "Victory" : "Defeat";
            titleText.color = isVictory ? new Color(0.9f, 0.85f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            subtitleText.text = isVictory ? "Battle Complete" : "Mission Failed";
            subtitleText.color = isVictory ? Color.white : new Color(0.8f, 0.8f, 0.8f);

            objectivesText.text = BuildEndSummary(isVictory, state, config);

            bool isDemoBattle = turns._campaignFlowProcessor != null;
            continueButtonObject.SetActive(isDemoBattle && isVictory);
            exitToTitleButtonObject.SetActive(true);

            if (isVictory)
                exitToTitleButtonObject.GetComponentInChildren<Text>().text = "Exit to Title";
            else
                exitToTitleButtonObject.GetComponentInChildren<Text>().text = "Return to Title";

            overlayPanel.SetActive(true);
        }

        public void Hide()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
        }

        public void OnContinueClicked()
        {
            if (turns._campaignFlowProcessor != null)
            {
                turns._campaignFlowProcessor.TryProcessVictory();
            }
        }

        public void OnExitToTitleClicked()
        {
            if (turns._campaignFlowProcessor != null)
            {
                turns._campaignFlowProcessor.TryTransitionToTitle();
            }
            else
            {
                SceneManager.LoadScene("Title");
            }
        }

        private void CreateOverlay()
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("EndScreen Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            overlayPanel = new GameObject("EndScreen Overlay", typeof(RectTransform), typeof(Image));
            overlayPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = overlayPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
            Image panelImage = overlayPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);

            overlayBackground = new GameObject("Background Fade", typeof(RectTransform), typeof(Image));
            overlayBackground.transform.SetParent(panelRect, false);
            RectTransform bgRect = overlayBackground.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;
            overlayBackground.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            float cardWidth = 600f;
            float cardHeight = 420f;
            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panelRect, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 20f);
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            card.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardRect, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            titleRect.sizeDelta = new Vector2(cardWidth - 80f, 60f);
            titleText = titleObj.GetComponent<Text>();
            titleText.font = GetDefaultFont();
            titleText.fontSize = 48;
            titleText.alignment = TextAnchor.LowerCenter;
            titleText.supportRichText = true;

            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subObj.transform.SetParent(cardRect, false);
            RectTransform subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -90f);
            subRect.sizeDelta = new Vector2(cardWidth - 80f, 30f);
            subtitleText = subObj.GetComponent<Text>();
            subtitleText.font = GetDefaultFont();
            subtitleText.fontSize = 24;
            subtitleText.alignment = TextAnchor.LowerCenter;

            GameObject objPanel = new GameObject("Objectives", typeof(RectTransform), typeof(Image));
            objPanel.transform.SetParent(cardRect, false);
            RectTransform objRect = objPanel.GetComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0f, 1f);
            objRect.anchorMax = new Vector2(1f, 1f);
            objRect.pivot = new Vector2(0.5f, 1f);
            objRect.anchoredPosition = new Vector2(0f, -120f);
            objRect.sizeDelta = new Vector2(cardWidth - 80f, 180f);
            objPanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            GameObject objTextObj = new GameObject("Objectives Text", typeof(RectTransform), typeof(Text));
            objTextObj.transform.SetParent(objRect, false);
            RectTransform objTextRect = objTextObj.GetComponent<RectTransform>();
            objTextRect.anchorMin = Vector2.zero;
            objTextRect.anchorMax = Vector2.one;
            objTextRect.anchoredPosition = Vector2.zero;
            objTextRect.sizeDelta = new Vector2(-16f, -16f);
            objectivesText = objTextObj.GetComponent<Text>();
            objectivesText.font = GetDefaultFont();
            objectivesText.fontSize = 14;
            objectivesText.alignment = TextAnchor.UpperLeft;
            objectivesText.color = new Color(0.85f, 0.85f, 0.85f);

            float btnWidth = 160f;
            float btnHeight = 40f;
            float bottomOffset = 50f;

            continueButtonObject = CreateButton("Continue", cardRect, new Vector2(-(btnWidth / 2f + 8f), bottomOffset), new Vector2(btnWidth, btnHeight));
            continueButton = continueButtonObject.GetComponent<Button>();
            continueButton.onClick.AddListener(OnContinueClicked);

            exitToTitleButtonObject = CreateButton("Exit to Title", cardRect, new Vector2(btnWidth / 2f + 8f, bottomOffset), new Vector2(btnWidth, btnHeight));
            exitToTitleButton = exitToTitleButtonObject.GetComponent<Button>();
            exitToTitleButton.onClick.AddListener(OnExitToTitleClicked);
        }

        private GameObject CreateButton(string label, RectTransform parent, Vector2 position, Vector2 size)
        {
            GameObject btn = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(parent, false);
            RectTransform rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

            GameObject textObj = new GameObject(label + "Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(rect, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            Text txt = textObj.GetComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 16;
            txt.alignment = TextAnchor.MiddleRight;
            txt.color = Color.white;
            txt.text = label;

            return btn;
        }

        private Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private string BuildEndSummary(bool isVictory, BattleScenarioRuntimeState state, BattleSetupConfiguration config)
        {
            if (config == null)
                return isVictory ? "Mission accomplished." : "Mission failed.";

            StringBuilder sb = new StringBuilder(256);
            sb.AppendLine($"Round {state.CurrentRound} | Rounds survived {state.CompletedRounds}");
            sb.AppendLine();

            if (config.RequireEliminateAllEnemies)
            {
                sb.AppendLine($"[x] Defeat all enemies");
            }

            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup obj = config.Objectives[i];
                if (obj == null)
                    continue;

                bool completed = EvaluateObjectiveCompletion(obj, state);
                string check = completed ? "[x]" : "[ ]";
                switch (obj.Type)
                {
                    case BattleObjectiveType.ReachArea:
                        sb.AppendLine($"{check} Reach destination with {obj.UnitRole}");
                        break;
                    case BattleObjectiveType.SurviveRounds:
                        sb.AppendLine($"{check} Survive {state.CompletedRounds}/{obj.RequiredRounds} rounds");
                        break;
                    case BattleObjectiveType.ProtectUnit:
                        sb.AppendLine($"{check} Protect {obj.UnitRole}");
                        break;
                }
            }

            if (!isVictory)
                sb.AppendLine();
            else
            {
                sb.AppendLine();
                sb.AppendLine("All objectives completed.");
            }

            return sb.ToString();
        }

        private bool EvaluateObjectiveCompletion(BattleObjectiveSetup obj, BattleScenarioRuntimeState state)
        {
            switch (obj.Type)
            {
                case BattleObjectiveType.ReachArea:
                    return state.CompletedObjectives.Contains(obj);
                case BattleObjectiveType.SurviveRounds:
                    return state.CompletedRounds >= obj.RequiredRounds;
                case BattleObjectiveType.ProtectUnit:
                    return state.UnitsByRole.TryGetValue(obj.UnitRole, out Unit unit) && unit != null && unit.IsAlive;
                default:
                    return false;
            }
        }
    }
}
