using UnityEngine;

namespace SLG.Grid
{
    [CreateAssetMenu(fileName = "TileVisualSettings", menuName = "SLG/Tile Visual Settings")]
    public sealed class TileVisualSettings : ScriptableObject
    {
        [Header("Movement Range")]
        [Tooltip("Overlay color for movement range tiles")]
        public Color movementRangeColor = new Color(0.2f, 0.75f, 1f, 0.4f);
        [Tooltip("Overlay scale relative to tile (0-1, broader = more coverage)")]
        [Range(0.5f, 0.95f)]
        public float movementRangeScale = 0.82f;
        [Tooltip("Overlay height above tile center")]
        public float movementRangeHeight = 0.58f;

        [Header("Attack Range")]
        [Tooltip("Overlay color for attack range tiles")]
        public Color attackRangeColor = new Color(1f, 0.35f, 0.15f, 0.4f);
        [Tooltip("Overlay scale (smaller = outlined border effect)")]
        [Range(0.2f, 0.9f)]
        public float attackRangeScale = 0.5f;
        [Tooltip("Overlay height above tile center")]
        public float attackRangeHeight = 0.64f;

        [Header("Skill Target")]
        public Color skillTargetColor = new Color(0.35f, 0.55f, 1f, 0.45f);
        [Range(0.2f, 0.9f)]
        public float skillTargetScale = 0.64f;
        public float skillTargetHeight = 0.68f;

        [Header("Skill Area")]
        public Color skillAreaColor = new Color(0.8f, 0.25f, 1f, 0.45f);
        [Range(0.2f, 0.9f)]
        public float skillAreaScale = 0.42f;
        public float skillAreaHeight = 0.72f;

        [Header("Hover")]
        [Tooltip("Bright border/overlay added on top of existing state")]
        public Color hoverBorderColor = new Color(0.9f, 0.95f, 1f, 0.7f);
        [Tooltip("Hover overlay scale (thin border effect)")]
        [Range(0.2f, 0.9f)]
        public float hoverBorderScale = 0.3f;
        [Tooltip("Hover overlay height (raised slightly)")]
        public float hoverHeight = 0.78f;

        [Header("Selected Destination")]
        [Tooltip("Bright border for clicked destination tile")]
        public Color selectedBorderColor = new Color(1f, 1f, 1f, 0.85f);
        [Range(0.2f, 0.9f)]
        public float selectedBorderScale = 0.35f;
        public float selectedHeight = 0.82f;
        [Tooltip("Pulse animation period in seconds")]
        public float selectedPulsePeriod = 0.8f;
        [Tooltip("Pulse amplitude for scale oscillation")]
        [Range(0f, 0.15f)]
        public float selectedPulseAmplitude = 0.08f;

        [Header("Terrain Base")]
        [Tooltip("Default terrain hover color when no TerrainDefinition assigned")]
        public Color terrainHoverColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        [Tooltip("Default terrain selected color when no TerrainDefinition assigned")]
        public Color terrainSelectedColor = new Color(1f, 0.95f, 0.5f, 1f);
        [Tooltip("Default terrain movement range color when no TerrainDefinition assigned")]
        public Color terrainMovementRangeColor = new Color(0.2f, 0.75f, 1f, 0.4f);

        /// <summary>Singleton default instance</summary>
        private static TileVisualSettings _default;
        public static TileVisualSettings Default
        {
            get
            {
                if (_default != null) return _default;
                _default = CreateInstance<TileVisualSettings>();
                _default.hideFlags = HideFlags.DontSave;
                return _default;
            }
        }
    }
}
