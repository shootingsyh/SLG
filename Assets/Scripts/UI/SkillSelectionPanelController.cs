using System;
using SLG.Skills;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class SkillSelectionPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform listRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Button cancelButton;

        private Action<SkillDefinition> selected;
        private Action cancelled;

        public bool IsVisible => panel != null && panel.activeSelf;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        public void Configure(Action<SkillDefinition> onSelected, Action onCancelled)
        {
            selected = onSelected;
            cancelled = onCancelled;
            EnsureUi();
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => cancelled?.Invoke());
        }

        public void Show(Unit unit)
        {
            EnsureUi();
            ClearList();
            if (panel == null || listRoot == null)
            {
                return;
            }

            titleText.text = unit != null ? $"{unit.DisplayName} Skills" : "Skills";
            if (unit == null || unit.Skills.Count == 0)
            {
                CreateLabel("No Skills");
            }
            else
            {
                for (int i = 0; i < unit.Skills.Count; i++)
                {
                    SkillDefinition skill = unit.Skills[i];
                    if (skill != null)
                    {
                        CreateSkillButton(skill);
                    }
                }
            }

            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void EnsureUi()
        {
            if (panel != null && listRoot != null && titleText != null && cancelButton != null)
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = EnsureSharedCanvas();
            }

            if (panel == null)
            {
                panel = new GameObject("Skill Selection Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvas.transform, false);
                Image image = panel.GetComponent<Image>();
                image.color = new Color(0.035f, 0.04f, 0.055f, 0.92f);
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-18f, -18f);
                rect.sizeDelta = new Vector2(330f, 270f);
            }

            titleText ??= CreateText("Title", panel.transform, new Vector2(12f, -10f), new Vector2(250f, 28f), 16, TextAnchor.MiddleLeft);

            if (listRoot == null)
            {
                GameObject listObject = new GameObject("Skill List", typeof(RectTransform));
                listObject.transform.SetParent(panel.transform, false);
                RectTransform listRect = listObject.GetComponent<RectTransform>();
                listRect.anchorMin = new Vector2(0f, 0f);
                listRect.anchorMax = new Vector2(1f, 1f);
                listRect.offsetMin = new Vector2(10f, 48f);
                listRect.offsetMax = new Vector2(-10f, -44f);
                listRoot = listObject.transform;
            }

            if (cancelButton == null)
            {
                cancelButton = CreateButton("Cancel Button", "Cancel", panel.transform, new Vector2(-10f, 10f), new Vector2(80f, 30f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            }
        }

        private void CreateSkillButton(SkillDefinition skill)
        {
            int index = listRoot.childCount;
            Button button = CreateButton($"Skill {skill.DisplayName}", FormatSkill(skill), listRoot, new Vector2(0f, -index * 54f), new Vector2(300f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            button.onClick.AddListener(() => selected?.Invoke(skill));
        }

        private void CreateLabel(string label)
        {
            Text text = CreateText("No Skills Label", listRoot, Vector2.zero, new Vector2(300f, 40f), 14, TextAnchor.MiddleCenter);
            text.text = label;
        }

        private string FormatSkill(SkillDefinition skill)
        {
            string target = skill.TargetType == SkillTargetType.Ground ? "Ground" : "Unit";
            return $"{skill.DisplayName}\n{skill.Description}  Rng {skill.MinimumRange}-{skill.MaximumRange}  {target}";
        }

        private void ClearList()
        {
            if (listRoot == null)
            {
                return;
            }

            for (int i = listRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(listRoot.GetChild(i).gameObject);
            }
        }

        private Button CreateButton(string name, string label, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMax.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            buttonObject.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 0.94f);

            Text text = CreateText("Label", buttonObject.transform, Vector2.zero, size, 13, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 3f);
            text.rectTransform.offsetMax = new Vector2(-8f, -3f);
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            return text;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private Canvas EnsureSharedCanvas()
        {
            GameObject canvasObject = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
