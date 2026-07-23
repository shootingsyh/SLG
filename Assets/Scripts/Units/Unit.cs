using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private float tileHeightOffset = 0.55f;
        [SerializeField] private Color normalColor = new Color(0.8f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.3f, 1f);

        private Renderer unitRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isSelected;
        private bool isMoving;

        public string DisplayName => displayName;
        public GridCoordinate CurrentCoordinate => currentCoordinate;
        public int MovementRange => movementRange;
        public Tile OccupiedTile => occupiedTile;
        public bool IsMoving => isMoving;

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
            transform.position = GetUnitPosition(tile);
        }

        public void MoveAlongPath(IReadOnlyList<Tile> path, Action<Unit, Tile> completed)
        {
            if (isMoving || path == null || path.Count < 2)
            {
                return;
            }

            StartCoroutine(MoveAlongPathRoutine(path, completed));
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
            selectionController?.HandleUnitClicked(this);
        }

        private IEnumerator MoveAlongPathRoutine(IReadOnlyList<Tile> path, Action<Unit, Tile> completed)
        {
            isMoving = true;
            Tile destination = path[path.Count - 1];

            for (int i = 1; i < path.Count; i++)
            {
                Vector3 start = transform.position;
                Vector3 end = GetUnitPosition(path[i]);
                float distance = Vector3.Distance(start, end);
                float duration = distance / Mathf.Max(0.01f, movementSpeed);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                transform.position = end;
            }

            occupiedTile = destination;
            currentCoordinate = destination.Coordinate;
            isMoving = false;
            completed?.Invoke(this, destination);
        }

        private Vector3 GetUnitPosition(Tile tile)
        {
            return tile.transform.position + new Vector3(0f, tileHeightOffset, 0f);
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
