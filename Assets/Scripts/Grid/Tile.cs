using SLG.Core;
using SLG.Terrain;
using SLG.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace SLG.Grid
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class Tile : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private int x;
        [SerializeField] private int y;
        [Header("Terrain")]
        [SerializeField] private TerrainDefinition terrainDefinition;
        [SerializeField] private bool useMovementCostOverride;
        [SerializeField] private int movementCostOverride = 1;

        [Header("Runtime")]
        [SerializeField] private Unit occupyingUnit;

        private GridSystem gridSystem;
        private MeshRenderer meshRenderer;
        private MeshRenderer movementOverlayRenderer;
        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock overlayPropertyBlock;
        private Color normalColor;
        private Color hoverColor;
        private Color selectedColor;
        private Color movementRangeColor;
        private bool isHovered;
        private bool isSelected;
        private bool isInMovementRange;

        public int X => x;
        public int Y => y;
        public bool IsWalkable => terrainDefinition == null || terrainDefinition.GroundEnterable || terrainDefinition.FlyingEnterable;
        public int MovementCost => BaseMovementCost;
        public Unit OccupyingUnit => occupyingUnit;
        public GridCoordinate Coordinate => new GridCoordinate(x, y);
        public TerrainDefinition TerrainDefinition => terrainDefinition;
        public string TerrainName => terrainDefinition != null ? terrainDefinition.DisplayName : "Unassigned";
        public int BaseMovementCost => useMovementCostOverride ? Mathf.Max(1, movementCostOverride) : (terrainDefinition != null ? terrainDefinition.BaseMovementCost : 1);
        public bool IsGroundEnterable => terrainDefinition == null || terrainDefinition.GroundEnterable;
        public bool IsFlyingEnterable => terrainDefinition == null || terrainDefinition.FlyingEnterable;

        public void Initialize(GridSystem owner, GridCoordinate coordinate, TerrainDefinition terrain, Color baseColor, Color hover, Color selected, Color movementRange)
        {
            gridSystem = owner;
            x = coordinate.X;
            y = coordinate.Y;
            terrainDefinition = terrain;
            normalColor = baseColor;
            hoverColor = hover;
            selectedColor = selected;
            movementRangeColor = movementRange;

            ApplyTerrainVisuals();
            RefreshVisualState();
        }

        public void SetTerrainDefinition(TerrainDefinition terrain)
        {
            terrainDefinition = terrain;
            ApplyTerrainVisuals();
            RefreshVisualState();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            RefreshVisualState();
        }

        public void SetMovementRangeHighlighted(bool highlighted)
        {
            isInMovementRange = highlighted;
            RefreshVisualState();
        }

        public bool CanEnter(Unit movingUnit)
        {
            if (occupyingUnit != null && occupyingUnit != movingUnit)
            {
                return false;
            }

            if (movingUnit == null)
            {
                return IsWalkable;
            }

            return terrainDefinition == null || terrainDefinition.CanEnter(movingUnit.MovementProfile);
        }

        public int GetMovementCost(Unit movingUnit)
        {
            if (movingUnit == null)
            {
                return BaseMovementCost;
            }

            if (useMovementCostOverride && CanEnter(movingUnit))
            {
                return movingUnit.MovementProfile == MovementProfile.Flying ? 1 : Mathf.Max(1, movementCostOverride);
            }

            return terrainDefinition != null ? terrainDefinition.GetMovementCost(movingUnit.MovementProfile) : 1;
        }

        public int GetDefenseBonus(Unit defender)
        {
            return terrainDefinition != null ? terrainDefinition.DefenseBonus : 0;
        }

        public void SetOccupyingUnit(Unit unit)
        {
            occupyingUnit = unit;
        }

        public void SetWalkable(bool walkable)
        {
            Debug.LogWarning("Tile.SetWalkable is deprecated. Assign a TerrainDefinition instead.", this);
        }

        public void SetMovementCost(int cost)
        {
            useMovementCostOverride = true;
            movementCostOverride = Mathf.Max(1, cost);
        }

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnMouseEnter()
        {
            isHovered = true;
            gridSystem?.HandleTileHoverEntered(this);
            RefreshVisualState();
        }

        private void OnMouseOver()
        {
            gridSystem?.HandleTileHoverStayed(this);
        }

        private void OnMouseExit()
        {
            isHovered = false;
            gridSystem?.HandleTileHoverExited(this);
            RefreshVisualState();
        }

        private void OnMouseDown()
        {
            gridSystem?.HandleTileClicked(this);
        }

        private bool CacheRenderer()
        {
            if (meshRenderer == null)
            {
                if (!TryGetComponent(out meshRenderer))
                {
                    Debug.LogError("Tile requires a MeshRenderer.", this);
                    return false;
                }
            }

            propertyBlock ??= new MaterialPropertyBlock();
            return true;
        }

        private void ApplyColor(Color color)
        {
            if (!CacheRenderer())
            {
                return;
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void SetMovementOverlayVisible(bool visible)
        {
            if (visible && movementOverlayRenderer == null)
            {
                EnsureMovementOverlay();
            }

            if (movementOverlayRenderer != null)
            {
                movementOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                {
                    ApplyMovementOverlayColor(movementRangeColor);
                }
            }
        }

        private void EnsureMovementOverlay()
        {
            if (!CacheRenderer())
            {
                return;
            }

            Transform existing = transform.Find("Movement Range Overlay");
            if (existing != null && existing.TryGetComponent(out movementOverlayRenderer))
            {
                overlayPropertyBlock ??= new MaterialPropertyBlock();
                return;
            }

            if (!TryGetComponent(out MeshFilter sourceMeshFilter) || sourceMeshFilter.sharedMesh == null)
            {
                return;
            }

            GameObject overlay = new GameObject("Movement Range Overlay");
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = new Vector3(0.72f, 0.08f, 0.72f);

            MeshFilter overlayMeshFilter = overlay.AddComponent<MeshFilter>();
            overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            movementOverlayRenderer = overlay.AddComponent<MeshRenderer>();
            movementOverlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
            movementOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            movementOverlayRenderer.receiveShadows = false;
            overlayPropertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyMovementOverlayColor(Color color)
        {
            if (movementOverlayRenderer == null)
            {
                return;
            }

            movementOverlayRenderer.GetPropertyBlock(overlayPropertyBlock);
            overlayPropertyBlock.SetColor(BaseColorId, color);
            overlayPropertyBlock.SetColor(ColorId, color);
            movementOverlayRenderer.SetPropertyBlock(overlayPropertyBlock);
        }

        private void ApplyTerrainVisuals()
        {
            normalColor = terrainDefinition != null ? terrainDefinition.DisplayColor : normalColor;
            if (!CacheRenderer())
            {
                return;
            }

            if (terrainDefinition != null && terrainDefinition.Material != null)
            {
                meshRenderer.sharedMaterial = terrainDefinition.Material;
                if (movementOverlayRenderer != null)
                {
                    movementOverlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
                }
            }

            Vector3 localScale = transform.localScale;
            float heightOffset = terrainDefinition != null ? terrainDefinition.VisualHeightOffset : 0f;
            transform.localPosition = new Vector3(transform.localPosition.x, heightOffset, transform.localPosition.z);
            transform.localScale = localScale;
        }

        private void RefreshVisualState()
        {
            SetMovementOverlayVisible(isInMovementRange);

            if (isSelected)
            {
                ApplyColor(selectedColor);
                return;
            }

            if (isHovered)
            {
                ApplyColor(hoverColor);
                return;
            }

            ApplyColor(normalColor);
        }
    }
}
