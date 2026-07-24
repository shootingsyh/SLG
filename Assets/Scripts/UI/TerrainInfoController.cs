using System.Text;
using SLG.Grid;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class TerrainInfoController : MonoBehaviour
    {
        [SerializeField] private GameObject terrainInfoPanel;
        [SerializeField] private Text terrainInfoText;

        private readonly StringBuilder builder = new StringBuilder(128);

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        public void Show(Tile tile, Unit selectedUnit)
        {
            EnsureUi();
            if (tile == null || terrainInfoPanel == null || terrainInfoText == null)
            {
                return;
            }

            builder.Clear();
            builder.AppendLine(tile.TerrainName);
            if (selectedUnit != null)
            {
                builder.Append("Move Cost: ");
                builder.AppendLine(tile.CanEnter(selectedUnit) ? tile.GetMovementCost(selectedUnit).ToString() : "Blocked");
            }
            else
            {
                builder.Append("Base Cost: ");
                builder.AppendLine(tile.BaseMovementCost.ToString());
            }

            builder.Append("Defense: +");
            builder.AppendLine(tile.GetDefenseBonus(null).ToString());
            builder.Append("Ground: ");
            builder.AppendLine(tile.IsGroundEnterable ? "Yes" : "Blocked");
            builder.Append("Flying: ");
            builder.Append(tile.IsFlyingEnterable ? "Yes" : "Blocked");

            terrainInfoText.text = builder.ToString();
            terrainInfoPanel.SetActive(true);
        }

        public void Hide()
        {
            if (terrainInfoPanel != null)
            {
                terrainInfoPanel.SetActive(false);
            }
        }

        private void EnsureUi()
        {
            if (terrainInfoPanel != null && terrainInfoText != null)
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (terrainInfoPanel == null)
            {
                terrainInfoPanel = new GameObject("Terrain Info Panel", typeof(RectTransform), typeof(Image));
                terrainInfoPanel.transform.SetParent(canvas.transform, false);
                Image image = terrainInfoPanel.GetComponent<Image>();
                image.color = new Color(0.04f, 0.06f, 0.07f, 0.82f);

                RectTransform rect = terrainInfoPanel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(20f, 20f);
                rect.sizeDelta = new Vector2(220f, 118f);
            }

            if (terrainInfoText == null)
            {
                GameObject textObject = new GameObject("Terrain Info Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(terrainInfoPanel.transform, false);
                RectTransform rect = textObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(10f, 8f);
                rect.offsetMax = new Vector2(-10f, -8f);

                terrainInfoText = textObject.GetComponent<Text>();
                terrainInfoText.font = GetDefaultFont();
                terrainInfoText.fontSize = 14;
                terrainInfoText.color = Color.white;
                terrainInfoText.alignment = TextAnchor.UpperLeft;
            }
        }

        private Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
