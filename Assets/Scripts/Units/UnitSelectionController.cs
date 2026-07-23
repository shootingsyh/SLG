using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using UnityEngine;

namespace SLG.Units
{
    public sealed class UnitSelectionController : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private BattleTurnController battleTurnController;

        private readonly List<Tile> highlightedTiles = new List<Tile>();
        private readonly HashSet<Tile> reachableTiles = new HashSet<Tile>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private Unit selectedUnit;
        private bool isUnitMoving;

        public bool IsUnitMoving => isUnitMoving;

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
                unit.OccupiedTile?.SetOccupyingUnit(unit);
            }
        }

        public void HandleUnitClicked(Unit unit)
        {
            if (isUnitMoving || battleTurnController == null || !battleTurnController.IsPlayerInputAllowed)
            {
                return;
            }

            if (unit == selectedUnit)
            {
                DeselectCurrentUnit();
                return;
            }

            SelectUnit(unit);
        }

        public bool HandleTileClicked(Tile tile)
        {
            if (battleTurnController != null && !battleTurnController.IsPlayerInputAllowed)
            {
                return true;
            }

            if (isUnitMoving)
            {
                return true;
            }

            if (selectedUnit == null)
            {
                return false;
            }

            if (!reachableTiles.Contains(tile) || tile == selectedUnit.OccupiedTile)
            {
                return false;
            }

            BeginMoveSelectedUnit(tile);
            return true;
        }

        public void SelectUnit(Unit unit)
        {
            if (isUnitMoving || battleTurnController == null || !battleTurnController.CanSelectUnit(unit))
            {
                return;
            }

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
            if (isUnitMoving)
            {
                return;
            }

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

            if (gridSystem == null || gridSystem.Reachability == null)
            {
                Debug.LogError("UnitSelectionController requires a ready GridSystem reference.", this);
                return;
            }

            gridSystem.Reachability.FindReachableTiles(unit.OccupiedTile, unit, unit.MovementRange, highlightedTiles);

            for (int i = 0; i < highlightedTiles.Count; i++)
            {
                Tile tile = highlightedTiles[i];
                tile.SetMovementRangeHighlighted(true);
                reachableTiles.Add(tile);
            }
        }

        private void ClearMovementRangePreview()
        {
            for (int i = 0; i < highlightedTiles.Count; i++)
            {
                highlightedTiles[i].SetMovementRangeHighlighted(false);
            }

            highlightedTiles.Clear();
            reachableTiles.Clear();
        }

        private void BeginMoveSelectedUnit(Tile destination)
        {
            if (gridSystem == null || gridSystem.Pathfinder == null || selectedUnit == null)
            {
                return;
            }

            if (!gridSystem.Pathfinder.TryFindPath(selectedUnit.OccupiedTile, destination, selectedUnit, pathBuffer))
            {
                return;
            }

            ClearMovementRangePreview();

            Tile startTile = selectedUnit.OccupiedTile;
            destination.SetOccupyingUnit(selectedUnit);
            isUnitMoving = true;
            selectedUnit.MoveAlongPath(pathBuffer, (unit, arrivedTile) => CompleteUnitMovement(unit, startTile, arrivedTile));
        }

        private void CompleteUnitMovement(Unit unit, Tile previousTile, Tile arrivedTile)
        {
            if (previousTile != null && previousTile != arrivedTile)
            {
                previousTile.SetOccupyingUnit(null);
            }

            arrivedTile.SetOccupyingUnit(unit);
            isUnitMoving = false;
            battleTurnController?.NotifyPlayerUnitMoved(unit);

            if (selectedUnit == unit)
            {
                RefreshMovementRangePreview(unit);
            }
        }
    }
}
