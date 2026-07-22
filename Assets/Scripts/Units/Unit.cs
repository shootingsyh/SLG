using SLG.Core;
using SLG.Grid;
using UnityEngine;

namespace SLG.Units
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Renderer))]
    public sealed class Unit : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private string displayName = "Unit";
        [SerializeField] private GridCoordinate currentCoordinate;
        [SerializeField] private int movementRange = 3;
        [SerializeField] private Tile occupiedTile;
        [SerializeField] private UnitSelectionController selectionController;
        [SerializeField] private Color normalColor = new Color(0.8f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.3f, 1f);

        private Renderer unitRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isSelected;

        public string DisplayName => displayName;
        public GridCoordinate CurrentCoordinate => currentCoordinate;
        public int MovementRange => movementRange;
        public Tile OccupiedTile => occupiedTile;

        public void Initialize(UnitSelectionController controller, GridCoordinate coordinate, Tile tile)
        {
            selectionController = controller;
            currentCoordinate = coordinate;
            occupiedTile = tile;
            ApplySelectionState(false);
        }

        public void PlaceOnTile(Tile tile)
        {
            if (tile == null)
            {
                occupiedTile = null;
                return;
            }

            occupiedTile = tile;
            currentCoordinate = tile.Coordinate;
            transform.position = tile.transform.position + new Vector3(0f, 0.55f, 0f);
        }

        public void ApplySelectionState(bool selected)
        {
            isSelected = selected;
            ApplyColor(isSelected ? selectedColor : normalColor);
        }

        private void Awake()
        {
            CacheRenderer();
            ApplySelectionState(isSelected);
        }

        private void OnMouseDown()
        {
            selectionController?.SelectUnit(this);
        }

        private bool CacheRenderer()
        {
            if (unitRenderer == null)
            {
                if (!TryGetComponent(out unitRenderer))
                {
                    Debug.LogError("Unit requires a Renderer.", this);
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

            unitRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            unitRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
