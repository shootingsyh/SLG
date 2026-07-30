using System.Text;
using SLG.Core;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class UnitProfileController : MonoBehaviour
    {
        private const float FadeDuration = 0.12f;

        [SerializeField] private GameObject profilePanel;
        [SerializeField] private Image factionAccent;
        [SerializeField] private Text profileText;

        [SerializeField] private Text nameText;
        [SerializeField] private Text hpText;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private Text statsText;

        private readonly StringBuilder builder = new StringBuilder(256);
        private Unit displayedUnit;
        private float fadeAlpha;
        private bool fadingIn;
        private GameObject panelBackground;

        public bool IsVisible => profilePanel != null && profilePanel.activeSelf;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        private void LateUpdate()
        {
            if (displayedUnit != null && IsVisible)
            {
                Refresh(displayedUnit);
            }

            if (profilePanel != null && panelBackground != null && profilePanel.activeSelf)
            {
                if (fadingIn)
                {
                    fadeAlpha = Mathf.Min(1f, fadeAlpha + Time.deltaTime / FadeDuration);
                    ApplyPanelAlpha(fadeAlpha);
                    if (fadeAlpha >= 1f)
                        fadingIn = false;
                }
            }
        }

        public void Show(Unit unit)
        {
            EnsureUi();
            if (unit == null || !unit.IsAlive || profilePanel == null)
            {
                Hide();
                return;
            }

            if (displayedUnit == unit)
            {
                return;
            }

            displayedUnit = unit;
            fadeAlpha = 0f;
            fadingIn = true;
            Refresh(unit);
            profilePanel.SetActive(true);
        }

        public void Hide()
        {
            displayedUnit = null;
            if (profilePanel != null)
            {
                profilePanel.SetActive(false);
            }
            fadeAlpha = 1f;
            fadingIn = false;
        }

        private void Refresh(Unit unit)
        {
            if (unit == null || !unit.IsAlive)
            {
                Hide();
                return;
            }

            string terrainName = unit.OccupiedTile != null ? unit.OccupiedTile.TerrainName : "None";
            int terrainDefense = CombatResolver.GetTerrainDefenseBonus(unit);
            int effectiveDefense = CombatResolver.GetEffectiveDefense(unit);

            // Build name and faction
            if (nameText != null)
            {
                builder.Clear();
                builder.Append(unit.DisplayName);
                builder.Append(' ');
                builder.Append(unit.Faction == UnitFaction.Player ? "(Player)" : "(Enemy)");
                nameText.text = builder.ToString();
                nameText.fontSize = 16;
                nameText.color = Color.white;
            }

            // HP display
            if (hpText != null)
            {
                hpText.text = $"HP {unit.CurrentHealth}/{unit.MaxHealth}";
                hpText.fontSize = 14;
                float hpRatio = (float)unit.CurrentHealth / unit.MaxHealth;
                hpText.color = hpRatio > 0.5f ? Color.white : (hpRatio > 0.25f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.3f, 0.2f));
            }

            // HP bar
            if (hpBarFill != null)
            {
                float hpRatio = (float)unit.CurrentHealth / unit.MaxHealth;
                hpBarFill.rectTransform.anchorMax = new Vector2(hpRatio, 0.5f);
                hpBarFill.color = hpRatio > 0.5f ? new Color(0.2f, 0.9f, 0.4f, 1f)
                    : (hpRatio > 0.25f ? new Color(0.9f, 0.85f, 0.2f, 1f)
                    : new Color(0.9f, 0.2f, 0.15f, 1f));
            }

            // Stats
            if (statsText != null)
            {
                builder.Clear();
                builder.AppendLine($"Atk {unit.AttackPower}   Def {effectiveDefense} (+{terrainDefense})");
                builder.AppendLine($"Move {unit.MovementRange}   Range {unit.MinimumAttackRange}-{unit.AttackRange}");
                builder.Append($"Terrain: {terrainName}");
                if (terrainDefense > 0)
                    builder.Append($" (Def +{terrainDefense})");
                if (unit.HasActed)
                    builder.Append(" | Acted");
                statsText.text = builder.ToString();
                statsText.fontSize = 13;
                statsText.color = new Color(0.85f, 0.85f, 0.9f);
            }

            // Legacy single-text fallback
            if (profileText != null && nameText == null)
            {
                builder.Clear();
                builder.AppendLine($"{unit.DisplayName} {(unit.Faction == UnitFaction.Player ? "Player" : "Enemy")}");
                builder.AppendLine($"HP {unit.CurrentHealth}/{unit.MaxHealth}");
                builder.AppendLine($"Atk {unit.AttackPower}   Def {effectiveDefense} (+{terrainDefense})");
                builder.AppendLine($"Move {unit.MovementRange}   Range {unit.MinimumAttackRange}-{unit.AttackRange}");
                builder.Append($"Terrain: {terrainName}");
                if (terrainDefense > 0)
                    builder.Append($" (Def +{terrainDefense})");
                if (unit.HasActed)
                    builder.Append(" | Acted");
                profileText.text = builder.ToString();
            }

            if (factionAccent != null)
            {
                factionAccent.color = GetFactionColor(unit.Faction);
            }
        }

        private void EnsureUi()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = EnsureSharedCanvas();
            }

            if (profilePanel == null)
            {
                profilePanel = new GameObject("Unit Profile Panel", typeof(RectTransform), typeof(Image));
                profilePanel.transform.SetParent(canvas.transform, false);

                Image image = profilePanel.GetComponent<Image>();
                image.color = new Color(0.035f, 0.045f, 0.055f, 0.92f);

                RectTransform rect = profilePanel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(18f, -18f);
                rect.sizeDelta = new Vector2(280f, 170f);

                panelBackground = profilePanel;
            }

            if (panelBackground == null)
            {
                panelBackground = profilePanel;
            }

            if (factionAccent == null)
            {
                GameObject accentObject = new GameObject("Faction Accent", typeof(RectTransform), typeof(Image));
                accentObject.transform.SetParent(profilePanel.transform, false);
                factionAccent = accentObject.GetComponent<Image>();

                RectTransform accentRect = accentObject.GetComponent<RectTransform>();
                accentRect.anchorMin = new Vector2(0f, 0f);
                accentRect.anchorMax = new Vector2(0f, 1f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.offsetMin = Vector2.zero;
                accentRect.offsetMax = new Vector2(6f, 0f);
            }

            // Try to use structured fields if they exist, otherwise create legacy text
            if (profileText == null && nameText == null)
            {
                // Create structured layout
                EnsureStructuredUi(canvas);
            }
            else if (profileText == null)
            {
                // Legacy single text fallback
                GameObject textObject = new GameObject("Unit Profile Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(profilePanel.transform, false);

                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(16f, 9f);
                textRect.offsetMax = new Vector2(-10f, -8f);

                profileText = textObject.GetComponent<Text>();
                profileText.font = GetDefaultFont();
                profileText.fontSize = 14;
                profileText.color = Color.white;
                profileText.alignment = TextAnchor.UpperLeft;
            }
        }

        private void EnsureStructuredUi(Canvas canvas)
        {
            // Name line
            GameObject nameObj = new GameObject("Name Text", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(profilePanel.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -10f);
            nameRect.sizeDelta = new Vector2(-24f, 24f);
            nameText = nameObj.GetComponent<Text>();
            nameText.font = GetDefaultFont();
            nameText.fontSize = 16;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleCenter;

            // HP line
            GameObject hpObj = new GameObject("HP Text", typeof(RectTransform), typeof(Text));
            hpObj.transform.SetParent(profilePanel.transform, false);
            RectTransform hpRect = hpObj.GetComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0f, 1f);
            hpRect.anchorMax = new Vector2(1f, 1f);
            hpRect.pivot = new Vector2(0.5f, 1f);
            hpRect.anchoredPosition = new Vector2(0f, -38f);
            hpRect.sizeDelta = new Vector2(-24f, 22f);
            hpText = hpObj.GetComponent<Text>();
            hpText.font = GetDefaultFont();
            hpText.fontSize = 14;
            hpText.color = Color.white;
            hpText.alignment = TextAnchor.MiddleCenter;

            Sprite hpSprite = CreateWhitePixelSprite();

            // HP bar bg + mask
            GameObject hpBarBg = new GameObject("HP Bar BG", typeof(RectTransform), typeof(Image), typeof(Mask));
            hpBarBg.transform.SetParent(profilePanel.transform, false);
            RectTransform hpBarBgRect = hpBarBg.GetComponent<RectTransform>();
            hpBarBgRect.anchorMin = new Vector2(0f, 1f);
            hpBarBgRect.anchorMax = new Vector2(1f, 1f);
            hpBarBgRect.pivot = new Vector2(0.5f, 1f);
            hpBarBgRect.anchoredPosition = new Vector2(0f, -58f);
            hpBarBgRect.sizeDelta = new Vector2(-32f, 8f);
            Image bgImage = hpBarBg.GetComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f);
            bgImage.sprite = hpSprite;
            bgImage.type = Image.Type.Simple;

            GameObject hpBarFillObj = new GameObject("HP Bar Fill", typeof(RectTransform), typeof(Image));
            hpBarFillObj.transform.SetParent(hpBarBg.transform, false);
            RectTransform hpBarFillRect = hpBarFillObj.GetComponent<RectTransform>();
            hpBarFillRect.anchorMin = new Vector2(0f, 0.5f);
            hpBarFillRect.anchorMax = new Vector2(0.5f, 0.5f);
            hpBarFillRect.pivot = new Vector2(0.5f, 0.5f);
            hpBarFillRect.anchoredPosition = Vector2.zero;
            hpBarFillRect.sizeDelta = new Vector2(-2f, 4f);
            hpBarFill = hpBarFillObj.GetComponent<Image>();
            hpBarFill.sprite = hpSprite;
            hpBarFill.type = Image.Type.Simple;
            hpBarFill.color = new Color(0.2f, 0.9f, 0.4f);

            // Stats block
            GameObject statsObj = new GameObject("Stats Text", typeof(RectTransform), typeof(Text));
            statsObj.transform.SetParent(profilePanel.transform, false);
            RectTransform statsRect = statsObj.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0f, 0f);
            statsRect.anchorMax = new Vector2(1f, 0f);
            statsRect.pivot = new Vector2(0.5f, 0f);
            statsRect.anchoredPosition = new Vector2(0f, 12f);
            statsRect.sizeDelta = new Vector2(-24f, 80f);
            statsText = statsObj.GetComponent<Text>();
            statsText.font = GetDefaultFont();
            statsText.fontSize = 13;
            statsText.color = new Color(0.85f, 0.85f, 0.9f);
            statsText.alignment = TextAnchor.LowerLeft;
            statsText.supportRichText = false;
        }

        private void ApplyPanelAlpha(float alpha)
        {
            if (panelBackground == null)
                return;

            Image image = panelBackground.GetComponent<Image>();
            if (image != null)
            {
                Color c = image.color;
                c.a = alpha;
                image.color = c;
            }
        }

        private static Color GetFactionColor(UnitFaction faction)
        {
            return faction == UnitFaction.Player ? new Color(0.2f, 0.48f, 1f, 1f) : new Color(1f, 0.28f, 0.18f, 1f);
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

        private static Sprite CreateWhitePixelSprite()
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        }
    }
}
