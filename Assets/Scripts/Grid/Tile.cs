using SLG.Core;
using SLG.Units;
using UnityEngine;

namespace SLG.Grid
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class Tile : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private bool isWalkable = true;
        [SerializeField] private int movementCost = 1;
        [SerializeField] private Unit occupyingUnit;

        private GridSystem gridSystem;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color normalColor;
        private Color hoverColor;
        private Color selectedColor;
        private Color movementRangeColor;
        private bool isHovered;
        private bool isSelected;
        private bool isInMovementRange;

        public int X => x;
        public int Y => y;
        public bool IsWalkable => isWalkable;
        public int MovementCost => Mathf.Max(1, movementCost);
        public Unit OccupyingUnit => occupyingUnit;
        public GridCoordinate Coordinate => new GridCoordinate(x, y);

        public void Initialize(GridSystem owner, GridCoordinate coordinate, Color baseColor, Color hover, Color selected, Color movementRange)
        {
            gridSystem = owner;
            x = coordinate.X;
            y = coordinate.Y;
            normalColor = baseColor;
            hoverColor = hover;
            selectedColor = selected;
            movementRangeColor = movementRange;

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
            return isWalkable && (occupyingUnit == null || occupyingUnit == movingUnit);
        }

        public void SetOccupyingUnit(Unit unit)
        {
            occupyingUnit = unit;
        }

        public void SetWalkable(bool walkable)
        {
            isWalkable = walkable;
        }

        public void SetMovementCost(int cost)
        {
            movementCost = Mathf.Max(1, cost);
        }

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnMouseEnter()
        {
            isHovered = true;
            RefreshVisualState();
        }

        private void OnMouseExit()
        {
            isHovered = false;
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

        private void RefreshVisualState()
        {
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

            ApplyColor(isInMovementRange ? movementRangeColor : normalColor);
        }
    }
}
