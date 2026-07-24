using SLG.Units;
using UnityEngine;

namespace SLG.Terrain
{
    [CreateAssetMenu(menuName = "SLG/Terrain Definition", fileName = "TerrainDefinition")]
    public sealed class TerrainDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Plain";
        [SerializeField] private string terrainId = "plain";
        [TextArea]
        [SerializeField] private string description;

        [Header("Rules")]
        [Min(1)]
        [SerializeField] private int baseMovementCost = 1;
        [SerializeField] private int defenseBonus;
        [SerializeField] private int avoidBonus;
        [SerializeField] private bool groundEnterable = true;
        [SerializeField] private bool flyingEnterable = true;

        [Header("Display")]
        [SerializeField] private Color displayColor = new Color(0.28f, 0.52f, 0.25f, 1f);
        [SerializeField] private Material material;
        [SerializeField] private float visualHeightOffset;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string TerrainId => string.IsNullOrWhiteSpace(terrainId) ? name : terrainId;
        public string Description => description;
        public int BaseMovementCost => Mathf.Max(1, baseMovementCost);
        public int DefenseBonus => defenseBonus;
        public int AvoidBonus => avoidBonus;
        public bool GroundEnterable => groundEnterable;
        public bool FlyingEnterable => flyingEnterable;
        public Color DisplayColor => displayColor;
        public Material Material => material;
        public float VisualHeightOffset => visualHeightOffset;

        public bool CanEnter(MovementProfile profile)
        {
            return profile == MovementProfile.Flying ? flyingEnterable : groundEnterable;
        }

        public int GetMovementCost(MovementProfile profile)
        {
            if (!CanEnter(profile))
            {
                return int.MaxValue;
            }

            return profile == MovementProfile.Flying ? 1 : BaseMovementCost;
        }

        private void OnValidate()
        {
            baseMovementCost = Mathf.Max(1, baseMovementCost);
        }
    }
}
