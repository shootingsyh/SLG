using System.Collections;
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
        private readonly List<Unit> highlightedAttackTargets = new List<Unit>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private Unit selectedUnit;
        private Unit previewTarget;
        private bool isUnitMoving;
        private bool isAttackInProgress;
        private bool selectedUnitMoved;

        public bool IsUnitMoving => isUnitMoving || isAttackInProgress;
        public bool HasPendingAction => selectedUnit != null && selectedUnitMoved && !selectedUnit.HasActed;
        public Unit SelectedUnit => selectedUnit;

        public void InitializeUnitsOnGrid()
        {
            Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (!unit.gameObject.activeInHierarchy || !unit.IsAlive)
                {
                    continue;
                }

                if (gridSystem != null && gridSystem.TryGetTile(unit.CurrentCoordinate, out Tile tile))
                {
                    if (!tile.CanEnter(unit))
                    {
                        Debug.LogError($"Unit '{unit.DisplayName}' starts on terrain it cannot enter at {tile.Coordinate}.", unit);
                        continue;
                    }

                    if (tile.OccupyingUnit != null && tile.OccupyingUnit != unit)
                    {
                        Debug.LogError($"Multiple units are assigned to tile {tile.Coordinate}: '{tile.OccupyingUnit.DisplayName}' and '{unit.DisplayName}'.", unit);
                        continue;
                    }

                    unit.PlaceOnTile(tile);
                }

                unit.Initialize(this, unit.CurrentCoordinate, unit.OccupiedTile);
                unit.OccupiedTile?.SetOccupyingUnit(unit);
            }
        }

        public void HandleUnitClicked(Unit unit)
        {
            if (IsUnitMoving || battleTurnController == null || !battleTurnController.IsPlayerInputAllowed || unit == null || !unit.IsAlive)
            {
                return;
            }

            if (highlightedAttackTargets.Contains(unit))
            {
                BeginPlayerAttack(unit);
                return;
            }

            if (unit == selectedUnit)
            {
                if (!selectedUnitMoved)
                {
                    DeselectCurrentUnit();
                }
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

            if (IsUnitMoving)
            {
                return true;
            }

            if (selectedUnit == null)
            {
                return false;
            }

            if (selectedUnitMoved)
            {
                return true;
            }

            if (!reachableTiles.Contains(tile) || tile == selectedUnit.OccupiedTile)
            {
                return false;
            }

            BeginMoveSelectedUnit(tile);
            return true;
        }

        public void HandleUnitHoverEntered(Unit unit)
        {
            if (IsUnitMoving || battleTurnController == null || unit == null || !highlightedAttackTargets.Contains(unit))
            {
                return;
            }

            ShowPlayerAttackPreview(unit);
        }

        public void HandleUnitHoverExited(Unit unit)
        {
            if (unit == null || unit != previewTarget)
            {
                return;
            }

            ClearCombatPreview();
        }

        public void HandleUnitHoverStayed(Unit unit)
        {
            if (unit == null || unit != previewTarget)
            {
                return;
            }

            battleTurnController?.UpdateCombatPreviewPosition();
        }

        public void SelectUnit(Unit unit)
        {
            if (IsUnitMoving || battleTurnController == null || !battleTurnController.CanSelectUnit(unit))
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
            selectedUnitMoved = false;
            selectedUnit.ApplySelectionState(true);
            gridSystem?.ClearSelectedTile();
            RefreshMovementRangePreview(selectedUnit);
            RefreshAttackTargets(selectedUnit);

            GridCoordinate coordinate = selectedUnit.CurrentCoordinate;
            int highlightedCount = highlightedTiles.Count;
            Debug.Log($"Selected Unit: {selectedUnit.DisplayName} at {coordinate}");
            Debug.Log($"Movement Range Tiles: {highlightedCount}");
        }

        public void DeselectCurrentUnit()
        {
            if (IsUnitMoving)
            {
                return;
            }

            ClearMovementRangePreview();
            ClearAttackTargets();
            ClearCombatPreview();
            selectedUnitMoved = false;

            if (selectedUnit == null)
            {
                return;
            }

            selectedUnit.ApplySelectionState(false);
            selectedUnit = null;
        }

        public void WaitSelectedUnit()
        {
            if (IsUnitMoving || selectedUnit == null || selectedUnit.HasActed)
            {
                return;
            }

            FinishSelectedUnitAction();
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

        private void RefreshAttackTargets(Unit attacker)
        {
            ClearAttackTargets();

            Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
            for (int i = 0; i < units.Length; i++)
            {
                Unit target = units[i];
                if (target == null || !target.IsAlive || target.Faction == attacker.Faction || target.OccupiedTile == null)
                {
                    continue;
                }

                if (CombatResolver.CanAttack(attacker, target))
                {
                    target.SetAttackTargetHighlighted(true);
                    highlightedAttackTargets.Add(target);
                }
            }
        }

        private void ClearAttackTargets()
        {
            for (int i = 0; i < highlightedAttackTargets.Count; i++)
            {
                if (highlightedAttackTargets[i] != null)
                {
                    highlightedAttackTargets[i].SetAttackTargetHighlighted(false);
                }
            }

            highlightedAttackTargets.Clear();
        }

        private void ShowPlayerAttackPreview(Unit target)
        {
            if (battleTurnController == null || selectedUnit == null || target == null || !CombatResolver.CanAttack(selectedUnit, target))
            {
                return;
            }

            ClearCombatPreview();
            previewTarget = target;
            previewTarget.SetCombatPreviewHighlighted(true);
            CombatPreview preview = CombatResolver.BuildPreview(selectedUnit, target);
            battleTurnController.ShowCombatPreview(preview);
            battleTurnController?.UpdateTurnControls();
        }

        private void ClearCombatPreview()
        {
            if (previewTarget != null)
            {
                previewTarget.SetCombatPreviewHighlighted(false);
                previewTarget = null;
            }

            battleTurnController?.HideCombatPreview();
        }

        private void BeginPlayerAttack(Unit target)
        {
            if (battleTurnController == null || selectedUnit == null || target == null || !CombatResolver.CanAttack(selectedUnit, target))
            {
                return;
            }

            ClearMovementRangePreview();
            ClearAttackTargets();
            ClearCombatPreview();
            isAttackInProgress = true;
            StartCoroutine(CompletePlayerCombatRoutine(selectedUnit, target));
        }

        private IEnumerator CompletePlayerCombatRoutine(Unit attacker, Unit defender)
        {
            yield return battleTurnController.ResolveCombatExchange(attacker, defender);
            isAttackInProgress = false;
            FinishSelectedUnitAction();
        }

        private void FinishSelectedUnitAction()
        {
            if (selectedUnit == null)
            {
                return;
            }

            ClearMovementRangePreview();
            ClearAttackTargets();
            ClearCombatPreview();
            selectedUnit.SetHasActed(true);
            selectedUnit.ApplySelectionState(false);
            Unit actedUnit = selectedUnit;
            selectedUnit = null;
            selectedUnitMoved = false;
            battleTurnController?.NotifyPlayerUnitActionFinished(actedUnit);
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
            ClearCombatPreview();

            Tile startTile = selectedUnit.OccupiedTile;
            destination.SetOccupyingUnit(selectedUnit);
            isUnitMoving = true;
            ClearAttackTargets();
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
            selectedUnitMoved = true;

            if (selectedUnit == unit && !unit.HasActed)
            {
                RefreshAttackTargets(unit);

                if (highlightedAttackTargets.Count == 0)
                {
                    FinishSelectedUnitAction();
                }
                else
                {
                    battleTurnController?.UpdateTurnControls();
                }
            }
        }
    }
}
