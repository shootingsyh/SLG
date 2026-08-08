using System;
using System.Collections.Generic;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.Items
{
    public sealed class ItemSelectionPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform listRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Button cancelButton;

        private Action<ItemDefinition> selected;
        private Action cancelled;

        public bool IsVisible => panel != null && panel.activeSelf;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        public void Configure(Action<ItemDefinition> onSelected, Action onCancelled)
        {
            selected = onSelected;
            cancelled = onCancelled;
            EnsureUi();
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => cancelled?.Invoke());
        }

        public void Show(Unit unit, CampaignInventory inventory)
        {
            EnsureUi();
            ClearList();
            if (panel == null || listRoot == null) return;
            titleText.text = unit != null ? $"{unit.DisplayName} Items" : "Items";
            bool any = false;
            if (unit != null && inventory != null)
            {
                foreach (var kv in inventory.Quantities)
                {
                    var def = ItemCatalog.Get(kv.Key);
                    if (def == null || kv.Value <= 0 || !def.UsableInBattle) continue;
                    // Only show if can target something (at least one valid target exists) or is equipment (not for battle)
                    // For battle, show all consumables with quantity; targeting validation happens on selection
                    CreateItemButton(def, kv.Value);
                    any = true;
                }
            }
            if (!any) CreateLabel("No Items");
            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void EnsureUi()
        {
            if (panel != null && listRoot != null && titleText != null && cancelButton != null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) canvas = EnsureSharedCanvas();
            if (panel == null)
            {
                panel = new GameObject("Item Selection Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvas.transform, false);
                var img = panel.GetComponent<Image>();
                img.color = new Color(0.035f, 0.04f, 0.055f, 0.92f);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-18f, -320f);
                rect.sizeDelta = new Vector2(330f, 270f);
            }
            titleText ??= CreateText("Title", panel.transform, new Vector2(12f, -10f), new Vector2(250f, 28f), 16, TextAnchor.MiddleLeft);
            if (listRoot == null)
            {
                var go = new GameObject("Item List", typeof(RectTransform));
                go.transform.SetParent(panel.transform, false);
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(1f, 1f);
                r.offsetMin = new Vector2(10f, 48f);
                r.offsetMax = new Vector2(-10f, -44f);
                listRoot = go.transform;
            }
            if (cancelButton == null)
                cancelButton = CreateButton("Cancel Button", "Cancel", panel.transform, new Vector2(-10f, 10f), new Vector2(80f, 30f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        }

        private void CreateItemButton(ItemDefinition def, int quantity)
        {
            int index = listRoot.childCount;
            string label = $"{def.DisplayName} x{quantity}\n{def.Description}";
            Button btn = CreateButton($"Item {def.DisplayName}", label, listRoot, new Vector2(0f, -index * 54f), new Vector2(300f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            btn.onClick.AddListener(() => selected?.Invoke(def));
            // Disable if insufficient quantity (should not happen) or no valid target? Keep enabled for now
        }

        private void CreateLabel(string label)
        {
            var txt = CreateText("No Items Label", listRoot, Vector2.zero, new Vector2(300f, 40f), 14, TextAnchor.MiddleCenter);
            txt.text = label;
        }

        private void ClearList()
        {
            if (listRoot == null) return;
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
        }

        private Button CreateButton(string name, string label, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(anchorMax.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 0.94f);
            var txt = CreateText("Label", go.transform, Vector2.zero, size, 13, TextAnchor.MiddleLeft);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = new Vector2(8f, 3f); txt.rectTransform.offsetMax = new Vector2(-8f, -3f);
            txt.text = label;
            return go.GetComponent<Button>();
        }

        private Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
            var txt = go.GetComponent<Text>();
            txt.font = GetDefaultFont(); txt.fontSize = fontSize; txt.color = Color.white; txt.alignment = alignment;
            return txt;
        }

        private static Font GetDefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Canvas sharedCanvas;
        private Canvas EnsureSharedCanvas()
        {
            if (sharedCanvas != null) return sharedCanvas;
            var existing = FindAnyObjectByType<Canvas>();
            if (existing != null) { sharedCanvas = existing; return sharedCanvas; }
            var go = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            sharedCanvas = c; return sharedCanvas;
        }
    }
}
