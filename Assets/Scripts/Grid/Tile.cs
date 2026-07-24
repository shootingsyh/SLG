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
        private MeshRenderer attackRangeOverlayRenderer;
        private MeshRenderer skillTargetOverlayRenderer;
        private MeshRenderer skillAreaOverlayRenderer;
        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock overlayPropertyBlock;
        private MaterialPropertyBlock attackOverlayPropertyBlock;
        private MaterialPropertyBlock skillTargetPropertyBlock;
        private MaterialPropertyBlock skillAreaPropertyBlock;
        private Color normalColor;
        private Color hoverColor;
        private Color selectedColor;
        private Color movementRangeColor;
        private Color attackRangeColor = new Color(0.95f, 0.2f, 0.12f, 1f);
        private Color skillTargetColor = new Color(0.35f, 0.55f, 1f, 1f);
        private Color skillAreaColor = new Color(0.8f, 0.25f, 1f, 1f);
        private bool isHovered;
        private bool isSelected;
        private bool isInMovementRange;
        private bool isInAttackRange;
        private bool isSkillTarget;
        private bool isSkillArea;

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

        public void SetAttackRangeHighlighted(bool highlighted)
        {
            isInAttackRange = highlighted;
            RefreshVisualState();
        }

        public void SetSkillTargetHighlighted(bool highlighted)
        {
            isSkillTarget = highlighted;
            RefreshVisualState();
        }

        public void SetSkillAreaHighlighted(bool highlighted)
        {
            isSkillArea = highlighted;
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
                movementOverlayRenderer = EnsureOverlay("Movement Range Overlay", new Vector3(0f, 0.62f, 0f), new Vector3(0.72f, 0.08f, 0.72f));
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

        private void SetAttackRangeOverlayVisible(bool visible)
        {
            if (visible && attackRangeOverlayRenderer == null)
            {
                attackRangeOverlayRenderer = EnsureOverlay("Attack Range Overlay", new Vector3(0f, 0.68f, 0f), new Vector3(0.44f, 0.1f, 0.44f));
            }

            if (attackRangeOverlayRenderer != null)
            {
                attackRangeOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                {
                    ApplyOverlayColor(attackRangeOverlayRenderer, ref attackOverlayPropertyBlock, attackRangeColor);
                }
            }
        }

        private void SetSkillTargetOverlayVisible(bool visible)
        {
            if (visible && skillTargetOverlayRenderer == null)
            {
                skillTargetOverlayRenderer = EnsureOverlay("Skill Target Overlay", new Vector3(0f, 0.72f, 0f), new Vector3(0.56f, 0.1f, 0.56f));
            }

            if (skillTargetOverlayRenderer != null)
            {
                skillTargetOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                {
                    ApplyOverlayColor(skillTargetOverlayRenderer, ref skillTargetPropertyBlock, skillTargetColor);
                }
            }
        }

        private void SetSkillAreaOverlayVisible(bool visible)
        {
            if (visible && skillAreaOverlayRenderer == null)
            {
                skillAreaOverlayRenderer = EnsureOverlay("Skill Area Overlay", new Vector3(0f, 0.76f, 0f), new Vector3(0.34f, 0.1f, 0.34f));
            }

            if (skillAreaOverlayRenderer != null)
            {
                skillAreaOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                {
                    ApplyOverlayColor(skillAreaOverlayRenderer, ref skillAreaPropertyBlock, skillAreaColor);
                }
            }
        }

        private MeshRenderer EnsureOverlay(string overlayName, Vector3 localPosition, Vector3 localScale)
        {
            if (!CacheRenderer())
            {
                return null;
            }

            Transform existing = transform.Find(overlayName);
            if (existing != null && existing.TryGetComponent(out MeshRenderer existingRenderer))
            {
                return existingRenderer;
            }

            if (!TryGetComponent(out MeshFilter sourceMeshFilter) || sourceMeshFilter.sharedMesh == null)
            {
                return null;
            }

            GameObject overlay = new GameObject(overlayName);
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = localPosition;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = localScale;

            MeshFilter overlayMeshFilter = overlay.AddComponent<MeshFilter>();
            overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            MeshRenderer overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            return overlayRenderer;
        }

        private void ApplyMovementOverlayColor(Color color)
        {
            ApplyOverlayColor(movementOverlayRenderer, ref overlayPropertyBlock, color);
        }

        private void ApplyOverlayColor(MeshRenderer targetRenderer, ref MaterialPropertyBlock targetBlock, Color color)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(targetBlock);
            targetBlock.SetColor(BaseColorId, color);
            targetBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(targetBlock);
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

                if (attackRangeOverlayRenderer != null)
                {
                    attackRangeOverlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
                }

                if (skillTargetOverlayRenderer != null)
                {
                    skillTargetOverlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
                }

                if (skillAreaOverlayRenderer != null)
                {
                    skillAreaOverlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
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
            SetAttackRangeOverlayVisible(isInAttackRange);
            SetSkillTargetOverlayVisible(isSkillTarget);
            SetSkillAreaOverlayVisible(isSkillArea);

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
