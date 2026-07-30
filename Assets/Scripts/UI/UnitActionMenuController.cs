using SLG.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class UnitActionMenuController : MonoBehaviour
    {
        private const float EdgePadding = 16f;

        [SerializeField] private RectTransform menuRoot;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button skillsButton;
        [SerializeField] private Button itemsButton;
        [SerializeField] private Button waitButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private float anchorHeight = 1.8f;
        [SerializeField] private float buttonSpacing = 64f;

        private Canvas canvas;
        private Camera targetCamera;
        private Unit anchoredUnit;
        private bool showingAttackCancelOnly;

        public bool IsVisible => menuRoot != null && menuRoot.gameObject.activeSelf;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                UpdatePosition();
            }
        }

        public void Configure(UnityAction attack, UnityAction skills, UnityAction wait, UnityAction cancel)
        {
            EnsureUi();
            SetButtonAction(attackButton, attack);
            SetButtonAction(skillsButton, skills);
            SetButtonAction(waitButton, wait);
            SetButtonAction(cancelButton, cancel);
        }

        public void Show(Unit unit, bool canCancelSelection, bool skillsAvailable)
        {
            EnsureUi();
            if (unit == null || !unit.IsAlive || menuRoot == null)
            {
                Hide();
                return;
            }

            anchoredUnit = unit;
            showingAttackCancelOnly = false;
            SetActionButtonsVisible(true);
            attackButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, buttonSpacing);
            cancelButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            SetButtonEnabled(skillsButton, skillsAvailable);
            SetButtonEnabled(itemsButton, false);
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = canCancelSelection;
            menuRoot.gameObject.SetActive(true);
            UpdatePosition();
        }

        public void ShowAttackCancel(Unit unit)
        {
            EnsureUi();
            if (unit == null || !unit.IsAlive || menuRoot == null)
            {
                Hide();
                return;
            }

            anchoredUnit = unit;
            showingAttackCancelOnly = true;
            SetActionButtonsVisible(false);
            attackButton.gameObject.SetActive(true);
            attackButton.interactable = true;
            attackButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 20f);
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
            cancelButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -20f);
            menuRoot.gameObject.SetActive(true);
            UpdatePosition();
        }

        public void Hide()
        {
            anchoredUnit = null;
            if (menuRoot != null)
            {
                menuRoot.gameObject.SetActive(false);
            }
        }

        private void UpdatePosition()
        {
            if (menuRoot == null || anchoredUnit == null || !anchoredUnit.IsAlive)
            {
                Hide();
                return;
            }

            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                Hide();
                return;
            }

            Vector3 worldAnchor = anchoredUnit.transform.position + Vector3.up * anchorHeight;
            Vector3 screenPoint = camera.WorldToScreenPoint(worldAnchor);
            if (screenPoint.z <= 0f)
            {
                Hide();
                return;
            }

            Vector2 size = GetCurrentBoundsSize();
            Vector2 clamped = new Vector2(
                Mathf.Clamp(screenPoint.x, EdgePadding + size.x * 0.5f, Screen.width - EdgePadding - size.x * 0.5f),
                Mathf.Clamp(screenPoint.y, EdgePadding + size.y * 0.5f, Screen.height - EdgePadding - size.y * 0.5f));
            
            // Avoid placing directly on top of the unit
            Vector2 unitScreenPos = camera.WorldToScreenPoint(anchoredUnit.transform.position);
            if (Mathf.Abs(clamped.x - unitScreenPos.x) < size.x * 0.4f && unitScreenPos.y < Screen.height * 0.5f)
            {
                clamped.y = Mathf.Max(clamped.y, unitScreenPos.y - size.y - 20f);
            }
            
            // Use anchoredPosition for ScreenSpaceOverlay canvas (screen-space coordinates)
            // For ScreenSpaceCamera, we'd need to convert to local space
            if (canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
            {
                menuRoot.anchoredPosition = clamped;
            }
            else
            {
                // ScreenSpaceCamera: convert screen point to local space
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    menuRoot, new Vector3(clamped.x, clamped.y, 0f), 
                    canvas.worldCamera ?? Camera.main, out localPoint))
                {
                    menuRoot.anchoredPosition = localPoint;
                }
            }
        }

        private Vector2 GetCurrentBoundsSize()
        {
            float width = showingAttackCancelOnly ? 88f : buttonSpacing * 2f + 88f;
            float height = showingAttackCancelOnly ? 80f : buttonSpacing * 2f + 88f;
            return new Vector2(width, height);
        }

        private void EnsureUi()
        {
            if (menuRoot != null && attackButton != null && skillsButton != null && itemsButton != null && waitButton != null && cancelButton != null)
            {
                return;
            }

            canvas = FindAnyObjectByType<Canvas>();
            targetCamera = Camera.main;
            if (canvas == null)
            {
                canvas = EnsureSharedCanvas();
            }

            if (menuRoot == null)
            {
                GameObject rootObject = new GameObject("Unit Action Menu", typeof(RectTransform), typeof(Image));
                rootObject.transform.SetParent(canvas.transform, false);
                menuRoot = rootObject.GetComponent<RectTransform>();
                menuRoot.anchorMin = new Vector2(0f, 0f);
                menuRoot.anchorMax = new Vector2(0f, 0f);
                menuRoot.pivot = new Vector2(0.5f, 0.5f);
                menuRoot.sizeDelta = new Vector2(240f, 240f);
                rootObject.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.6f);
            }

            attackButton ??= CreateButton("Attack Button", "Attack", new Vector2(0f, buttonSpacing), true, ButtonRole.Primary);
            skillsButton ??= CreateButton("Skills Button", "Skills", new Vector2(-buttonSpacing, 0f), false, ButtonRole.Normal);
            itemsButton ??= CreateButton("Items Button", "Items", new Vector2(buttonSpacing, 0f), false, ButtonRole.Normal);
            waitButton ??= CreateButton("Wait Button", "Wait", new Vector2(0f, -buttonSpacing), true, ButtonRole.Normal);
            cancelButton ??= CreateButton("Cancel Button", "Cancel", Vector2.zero, true, ButtonRole.Secondary);
        }

        private enum ButtonRole
        {
            Primary,
            Normal,
            Secondary
        }

        private Button CreateButton(string name, string label, Vector2 anchoredPosition, bool interactable, ButtonRole role)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(menuRoot, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(88f, 40f);

            Image image = buttonObject.GetComponent<Image>();
            Color bgColor = role switch
            {
                ButtonRole.Primary => new Color(0.15f, 0.25f, 0.45f, 0.95f),
                ButtonRole.Secondary => new Color(0.08f, 0.08f, 0.12f, 0.85f),
                _ => new Color(0.1f, 0.13f, 0.18f, 0.92f)
            };
            image.color = interactable ? bgColor : new Color(0.06f, 0.06f, 0.06f, 0.5f);

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;

            // Setup button state colors
            ColorBlock colors = button.colors;
            if (interactable)
            {
                colors.normalColor = bgColor;
                colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.15f);
                colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f);
                colors.disabledColor = new Color(0.06f, 0.06f, 0.06f, 0.5f);
            }
            else
            {
                colors.normalColor = new Color(0.06f, 0.06f, 0.06f, 0.5f);
                colors.highlightedColor = colors.normalColor;
                colors.pressedColor = colors.normalColor;
                colors.disabledColor = colors.normalColor;
            }
            button.colors = colors;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 14;
            
            if (interactable)
            {
                text.color = role switch
                {
                    ButtonRole.Secondary => new Color(0.8f, 0.8f, 0.85f, 1f),
                    _ => Color.white
                };
            }
            else
            {
                text.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            }
            
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return button;
        }

        private void SetActionButtonsVisible(bool visible)
        {
            attackButton.gameObject.SetActive(visible);
            skillsButton.gameObject.SetActive(visible);
            itemsButton.gameObject.SetActive(visible);
            waitButton.gameObject.SetActive(visible);
        }

        private void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = enabled ? new Color(0.1f, 0.13f, 0.18f, 0.92f) : new Color(0.06f, 0.06f, 0.06f, 0.5f);
            }

            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = enabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        private static void SetButtonAction(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
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
