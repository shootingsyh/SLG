using SLG.Core;
using SLG.Terrain;
using SLG.Units;
using SLG.Visuals;
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

        [Header("Visual Settings")]
        [SerializeField] private TileVisualSettings visualSettings;

        private GridSystem gridSystem;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color normalColor;

        private MeshRenderer movementOverlayRenderer;
        private MaterialPropertyBlock movementOverlayBlock;
        private MeshRenderer attackRangeOverlayRenderer;
        private MaterialPropertyBlock attackOverlayBlock;
        private MeshRenderer skillTargetOverlayRenderer;
        private MaterialPropertyBlock skillTargetBlock;
        private MeshRenderer skillAreaOverlayRenderer;
        private MaterialPropertyBlock skillAreaBlock;
        private MeshRenderer hoverOverlayRenderer;
        private MaterialPropertyBlock hoverOverlayBlock;
        private MeshRenderer selectedOverlayRenderer;
        private MaterialPropertyBlock selectedOverlayBlock;

        private bool isHovered;
        private bool isSelected;
        private bool isInMovementRange;
        private bool isInAttackRange;
        private bool isSkillTarget;
        private bool isSkillArea;
        private float selectedPulseTime;

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

        private TileVisualSettings Settings => visualSettings ?? TileVisualSettings.Default;

        public void Initialize(GridSystem owner, GridCoordinate coordinate, TerrainDefinition terrain, Color baseColor, Color hover, Color selected, Color movementRange)
        {
            gridSystem = owner;
            x = coordinate.X;
            y = coordinate.Y;
            terrainDefinition = terrain;
            normalColor = baseColor;

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
            if (selected) selectedPulseTime = 0f;
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

        private void Update()
        {
            if (isSelected && selectedOverlayRenderer != null)
            {
                selectedPulseTime += Time.deltaTime;
                float settingsPulsePeriod = Settings.selectedPulsePeriod;
                float period = settingsPulsePeriod > 0.01f ? settingsPulsePeriod : 0.8f;
                float pulse = Mathf.PingPong(selectedPulseTime, period) / period;
                float settingsAmplitude = Settings.selectedPulseAmplitude;
                float amplitude = settingsAmplitude > 0f ? settingsAmplitude : 0.08f;
                float scaleMultiplier = 1f + pulse * amplitude;
                Vector3 baseScale = Settings.selectedBorderScale * 0.8f * Vector3.one;
                selectedOverlayRenderer.transform.localScale = baseScale * scaleMultiplier;
            }
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

        private void ApplyTerrainVisuals()
        {
            if (!CacheRenderer())
            {
                return;
            }

            normalColor = terrainDefinition != null ? terrainDefinition.DisplayColor : normalColor;

            meshRenderer.sharedMaterial = terrainDefinition != null && terrainDefinition.Material != null
                ? terrainDefinition.Material
                : RuntimeVisualMaterials.TileMaterial;

            ApplyOverlayColor(meshRenderer, ref propertyBlock, normalColor);

            Vector3 localScale = transform.localScale;
            float heightOffset = terrainDefinition != null ? terrainDefinition.VisualHeightOffset : 0f;
            transform.localPosition = new Vector3(transform.localPosition.x, heightOffset, transform.localPosition.z);
            transform.localScale = localScale;
        }

        private void RefreshVisualState()
        {
            TileVisualSettings s = Settings;

            SetMovementOverlayVisible(isInMovementRange, s);
            SetAttackRangeOverlayVisible(isInAttackRange, s);
            SetSkillTargetOverlayVisible(isSkillTarget, s);
            SetSkillAreaOverlayVisible(isSkillArea, s);
            SetHoverOverlayVisible(isHovered, s);
            SetSelectedOverlayVisible(isSelected, s);
        }

        private void SetMovementOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && movementOverlayRenderer == null)
            {
                movementOverlayRenderer = EnsureOverlay("Movement Range Overlay",
                    new Vector3(0f, s.movementRangeHeight, 0f),
                    s.movementRangeScale, false);
            }

            if (movementOverlayRenderer != null)
            {
                movementOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(movementOverlayRenderer, ref movementOverlayBlock, s.movementRangeColor);
            }
        }

        private void SetAttackRangeOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && attackRangeOverlayRenderer == null)
            {
                attackRangeOverlayRenderer = EnsureOverlay("Attack Range Overlay",
                    new Vector3(0f, s.attackRangeHeight, 0f),
                    s.attackRangeScale, false);
            }

            if (attackRangeOverlayRenderer != null)
            {
                attackRangeOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(attackRangeOverlayRenderer, ref attackOverlayBlock, s.attackRangeColor);
            }
        }

        private void SetSkillTargetOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && skillTargetOverlayRenderer == null)
            {
                skillTargetOverlayRenderer = EnsureOverlay("Skill Target Overlay",
                    new Vector3(0f, s.skillTargetHeight, 0f),
                    s.skillTargetScale, false);
            }

            if (skillTargetOverlayRenderer != null)
            {
                skillTargetOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(skillTargetOverlayRenderer, ref skillTargetBlock, s.skillTargetColor);
            }
        }

        private void SetSkillAreaOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && skillAreaOverlayRenderer == null)
            {
                skillAreaOverlayRenderer = EnsureOverlay("Skill Area Overlay",
                    new Vector3(0f, s.skillAreaHeight, 0f),
                    s.skillAreaScale, false);
            }

            if (skillAreaOverlayRenderer != null)
            {
                skillAreaOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(skillAreaOverlayRenderer, ref skillAreaBlock, s.skillAreaColor);
            }
        }

        private void SetHoverOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && hoverOverlayRenderer == null)
            {
                hoverOverlayRenderer = EnsureOverlay("Hover Overlay",
                    new Vector3(0f, s.hoverHeight, 0f),
                    s.hoverBorderScale, false);
            }

            if (hoverOverlayRenderer != null)
            {
                hoverOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(hoverOverlayRenderer, ref hoverOverlayBlock, s.hoverBorderColor);
            }
        }

        private void SetSelectedOverlayVisible(bool visible, TileVisualSettings s)
        {
            if (visible && selectedOverlayRenderer == null)
            {
                selectedOverlayRenderer = EnsureOverlay("Selected Overlay",
                    new Vector3(0f, s.selectedHeight, 0f),
                    s.selectedBorderScale, false);
            }

            if (selectedOverlayRenderer != null)
            {
                selectedOverlayRenderer.gameObject.SetActive(visible);
                if (visible)
                    ApplyOverlayColor(selectedOverlayRenderer, ref selectedOverlayBlock, s.selectedBorderColor);
            }
        }

        private MeshRenderer EnsureOverlay(string overlayName, Vector3 localPosition, float scale, bool hideInEditMode)
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
            overlay.transform.localScale = Vector3.one * scale;

            MeshFilter overlayMeshFilter = overlay.AddComponent<MeshFilter>();
            overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            MeshRenderer overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = meshRenderer.sharedMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            return overlayRenderer;
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
    }
}
