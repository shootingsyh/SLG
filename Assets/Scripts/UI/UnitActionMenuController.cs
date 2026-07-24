using SLG.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class UnitActionMenuController : MonoBehaviour
    {
        private const float EdgePadding = 14f;

        [SerializeField] private RectTransform menuRoot;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button skillsButton;
        [SerializeField] private Button itemsButton;
        [SerializeField] private Button waitButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private float anchorHeight = 1.8f;
        [SerializeField] private float buttonSpacing = 58f;

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

        public void Configure(UnityAction attack, UnityAction wait, UnityAction cancel)
        {
            EnsureUi();
            SetButtonAction(attackButton, attack);
            SetButtonAction(waitButton, wait);
            SetButtonAction(cancelButton, cancel);
        }

        public void Show(Unit unit, bool canCancelSelection)
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
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
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
            menuRoot.position = clamped;
        }

        private Vector2 GetCurrentBoundsSize()
        {
            float width = showingAttackCancelOnly ? 92f : buttonSpacing * 2f + 92f;
            float height = showingAttackCancelOnly ? 42f : buttonSpacing * 2f + 92f;
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
                Debug.LogError("UnitActionMenuController requires an existing Canvas.", this);
                return;
            }

            if (menuRoot == null)
            {
                GameObject rootObject = new GameObject("Unit Action Menu", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                menuRoot = rootObject.GetComponent<RectTransform>();
                menuRoot.anchorMin = new Vector2(0f, 0f);
                menuRoot.anchorMax = new Vector2(0f, 0f);
                menuRoot.pivot = new Vector2(0.5f, 0.5f);
                menuRoot.sizeDelta = new Vector2(220f, 220f);
            }

            attackButton ??= CreateButton("Attack Button", "Attack", new Vector2(0f, buttonSpacing), true);
            skillsButton ??= CreateButton("Skills Button", "Skills", new Vector2(-buttonSpacing, 0f), false);
            itemsButton ??= CreateButton("Items Button", "Items", new Vector2(buttonSpacing, 0f), false);
            waitButton ??= CreateButton("Wait Button", "Wait", new Vector2(0f, -buttonSpacing), true);
            cancelButton ??= CreateButton("Cancel Button", "Cancel", Vector2.zero, true);
        }

        private Button CreateButton(string name, string label, Vector2 anchoredPosition, bool interactable)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(menuRoot, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(74f, 36f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = interactable ? new Color(0.09f, 0.12f, 0.16f, 0.92f) : new Color(0.08f, 0.08f, 0.08f, 0.55f);

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 13;
            text.color = interactable ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
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

            if (visible)
            {
                skillsButton.interactable = false;
                itemsButton.interactable = false;
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
    }
}
