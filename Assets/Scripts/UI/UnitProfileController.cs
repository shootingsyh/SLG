using System.Text;
using SLG.Core;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.UI
{
    public sealed class UnitProfileController : MonoBehaviour
    {
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private Image factionAccent;
        [SerializeField] private Text profileText;

        private readonly StringBuilder builder = new StringBuilder(256);
        private Unit displayedUnit;

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
        }

        public void Show(Unit unit)
        {
            EnsureUi();
            if (unit == null || !unit.IsAlive || profilePanel == null || profileText == null)
            {
                Hide();
                return;
            }

            displayedUnit = unit;
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

            builder.Clear();
            builder.AppendLine(unit.DisplayName);
            builder.AppendLine($"{unit.ArchetypeName} | {unit.Faction}");
            builder.AppendLine($"HP {unit.CurrentHealth}/{unit.MaxHealth}");
            builder.AppendLine($"Atk {unit.AttackPower}  Def {effectiveDefense} (+{terrainDefense})");
            builder.AppendLine($"Move {unit.MovementRange}  Range {unit.MinimumAttackRange}-{unit.AttackRange}");
            builder.AppendLine($"Terrain {terrainName}  Def +{terrainDefense}");
            builder.Append(unit.HasActed ? "Acted: Yes" : "Acted: No");
            profileText.text = builder.ToString();

            if (factionAccent != null)
            {
                factionAccent.color = GetFactionColor(unit.Faction);
            }
        }

        private void EnsureUi()
        {
            if (profilePanel != null && profileText != null && factionAccent != null)
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("UnitProfileController requires an existing Canvas.", this);
                return;
            }

            if (profilePanel == null)
            {
                profilePanel = new GameObject("Unit Profile Panel", typeof(RectTransform), typeof(Image));
                profilePanel.transform.SetParent(canvas.transform, false);

                Image image = profilePanel.GetComponent<Image>();
                image.color = new Color(0.035f, 0.045f, 0.055f, 0.88f);

                RectTransform rect = profilePanel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(18f, -18f);
                rect.sizeDelta = new Vector2(270f, 156f);
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
                accentRect.offsetMax = new Vector2(7f, 0f);
            }

            if (profileText == null)
            {
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

        private static Color GetFactionColor(UnitFaction faction)
        {
            return faction == UnitFaction.Player ? new Color(0.2f, 0.48f, 1f, 1f) : new Color(1f, 0.28f, 0.18f, 1f);
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
