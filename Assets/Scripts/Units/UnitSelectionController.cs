using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using UnityEngine;

namespace SLG.Units
{
    public sealed class UnitSelectionController : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem;

        private readonly List<Tile> highlightedTiles = new List<Tile>();
        private Unit selectedUnit;

        public void InitializeUnitsOnGrid()
        {
            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (gridSystem != null && gridSystem.TryGetTile(unit.CurrentCoordinate, out Tile tile))
                {
                    unit.PlaceOnTile(tile);
                }

                unit.Initialize(this, unit.CurrentCoordinate, unit.OccupiedTile);
            }
        }

        public void SelectUnit(Unit unit)
        {
            if (unit == null)
            {
                DeselectCurrentUnit();
                return;
            }

            if (selectedUnit != null && selectedUnit != unit)
            {
                selectedUnit.ApplySelectionState(false);
            }

            selectedUnit = unit;
            selectedUnit.ApplySelectionState(true);
            gridSystem?.ClearSelectedTile();
            RefreshMovementRangePreview(selectedUnit);

            GridCoordinate coordinate = selectedUnit.CurrentCoordinate;
            int highlightedCount = highlightedTiles.Count;
            Debug.Log($"Selected Unit: {selectedUnit.DisplayName} at {coordinate}");
            Debug.Log($"Movement Range Tiles: {highlightedCount}");
        }

        public void DeselectCurrentUnit()
        {
            ClearMovementRangePreview();

            if (selectedUnit == null)
            {
                return;
            }

            selectedUnit.ApplySelectionState(false);
            selectedUnit = null;
        }

        private void RefreshMovementRangePreview(Unit unit)
        {
            ClearMovementRangePreview();

            if (gridSystem == null)
            {
                Debug.LogError("UnitSelectionController requires a GridSystem reference.", this);
                return;
            }

            GridCoordinate origin = unit.CurrentCoordinate;
            int range = unit.MovementRange;

            for (int y = 0; y < gridSystem.Height; y++)
            {
                for (int x = 0; x < gridSystem.Width; x++)
                {
                    int distance = Mathf.Abs(x - origin.X) + Mathf.Abs(y - origin.Y);
                    if (distance > range)
                    {
                        continue;
                    }

                    if (gridSystem.TryGetTile(new GridCoordinate(x, y), out Tile tile))
                    {
                        tile.SetMovementRangeHighlighted(true);
                        highlightedTiles.Add(tile);
                    }
                }
            }
        }

        private void ClearMovementRangePreview()
        {
            for (int i = 0; i < highlightedTiles.Count; i++)
            {
                highlightedTiles[i].SetMovementRangeHighlighted(false);
            }

            highlightedTiles.Clear();
        }
    }
}
