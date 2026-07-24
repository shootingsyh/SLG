using System.Collections;
using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using SLG.Skills;
using SLG.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SLG.Units
{
    public sealed class UnitSelectionController : MonoBehaviour
    {
        public enum PlayerInteractionState
        {
            Idle,
            ChoosingMovement,
            Moving,
            ChoosingAction,
            ChoosingSkill,
            ChoosingSkillTarget,
            ChoosingAttackTarget,
            ReturningToOriginalTile,
            ResolvingCombat,
            ResolvingSkill,
            BattleEnded
        }

        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private BattleTurnController battleTurnController;
        [SerializeField] private UnitProfileController unitProfileController;
        [SerializeField] private UnitActionMenuController actionMenuController;
        [SerializeField] private SkillSelectionPanelController skillSelectionPanelController;

        private readonly List<Tile> highlightedMovementTiles = new List<Tile>();
        private readonly HashSet<Tile> reachableTiles = new HashSet<Tile>();
        private readonly List<Tile> highlightedAttackRangeTiles = new List<Tile>();
        private readonly List<Unit> highlightedAttackTargets = new List<Unit>();
        private readonly List<Tile> highlightedSkillRangeTiles = new List<Tile>();
        private readonly List<Tile> highlightedSkillTargetTiles = new List<Tile>();
        private readonly List<Tile> highlightedSkillAreaTiles = new List<Tile>();
        private readonly List<Unit> highlightedSkillAffectedUnits = new List<Unit>();
        private readonly List<Tile> skillAreaBuffer = new List<Tile>();
        private readonly List<Unit> skillAffectedUnitBuffer = new List<Unit>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private readonly List<Tile> provisionalMovementPath = new List<Tile>();
        private readonly List<Tile> returnPathBuffer = new List<Tile>();

        private Unit selectedUnit;
        private Unit hoveredUnit;
        private Unit previewTarget;
        private Tile originalTile;
        private Tile currentTile;
        private Unit currentAttackTarget;
        private SkillDefinition selectedSkill;
        private Tile currentSkillTargetTile;
        private PlayerInteractionState interactionState = PlayerInteractionState.Idle;
        private bool hasDisplacedProvisionalMove;

        public PlayerInteractionState CurrentInteractionState => interactionState;
        public bool IsInteractionIdle => interactionState == PlayerInteractionState.Idle;
        public bool IsUnitMoving => interactionState == PlayerInteractionState.Moving || interactionState == PlayerInteractionState.ReturningToOriginalTile || interactionState == PlayerInteractionState.ResolvingCombat || interactionState == PlayerInteractionState.ResolvingSkill;
        public bool HasPendingAction => interactionState != PlayerInteractionState.Idle && interactionState != PlayerInteractionState.BattleEnded;
        public Unit SelectedUnit => selectedUnit;

        private void Awake()
        {
            EnsureUiControllers();
        }

        private void Update()
        {
            HandleKeyboardInput();
            UpdateProfileVisibility();
        }

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
            if (battleTurnController == null || !battleTurnController.IsPlayerInputAllowed || unit == null || !unit.IsAlive)
            {
                return;
            }

            switch (interactionState)
            {
                case PlayerInteractionState.Idle:
                    SelectUnit(unit);
                    break;
                case PlayerInteractionState.ChoosingMovement:
                    if (unit == selectedUnit)
                    {
                        ChooseStayInPlace();
                    }
                    break;
                case PlayerInteractionState.ChoosingAttackTarget:
                    if (highlightedAttackTargets.Contains(unit))
                    {
                        BeginPlayerAttack(unit);
                    }
                    break;
                case PlayerInteractionState.ChoosingSkillTarget:
                    HandleSkillTargetUnitClicked(unit);
                    break;
            }
        }

        public bool HandleTileClicked(Tile tile)
        {
            if (battleTurnController != null && !battleTurnController.IsPlayerInputAllowed)
            {
                return true;
            }

            switch (interactionState)
            {
                case PlayerInteractionState.Idle:
                    return false;
                case PlayerInteractionState.ChoosingMovement:
                    HandleMovementChoiceTileClicked(tile);
                    return true;
                case PlayerInteractionState.ChoosingSkillTarget:
                    HandleSkillTargetTileClicked(tile);
                    return true;
                default:
                    return true;
            }
        }

        public void HandleTileHoverEntered(Tile tile)
        {
            if (interactionState == PlayerInteractionState.ChoosingSkillTarget)
            {
                ShowSkillTargetPreview(tile);
            }
        }

        public void HandleTileHoverStayed(Tile tile)
        {
            if (interactionState == PlayerInteractionState.ChoosingSkillTarget)
            {
                battleTurnController?.UpdateCombatPreviewPosition();
            }
        }

        public void HandleTileHoverExited(Tile tile)
        {
            if (interactionState == PlayerInteractionState.ChoosingSkillTarget && tile == currentSkillTargetTile)
            {
                ClearSkillHoverPreview();
            }
        }

        public void HandleUnitHoverEntered(Unit unit)
        {
            hoveredUnit = unit != null && unit.IsAlive ? unit : null;

            if (interactionState == PlayerInteractionState.ChoosingAttackTarget && unit != null && highlightedAttackTargets.Contains(unit))
            {
                ShowPlayerAttackPreview(unit);
            }

            if (interactionState == PlayerInteractionState.ChoosingSkillTarget && unit != null && unit.OccupiedTile != null)
            {
                ShowSkillTargetPreview(unit.OccupiedTile);
            }

            UpdateProfileVisibility();
        }

        public void HandleUnitHoverExited(Unit unit)
        {
            if (unit != null && unit == previewTarget)
            {
                ClearCombatPreview();
            }

            if (interactionState == PlayerInteractionState.ChoosingSkillTarget && unit != null && unit.OccupiedTile == currentSkillTargetTile)
            {
                ClearSkillHoverPreview();
            }

            if (unit != null && unit == hoveredUnit)
            {
                hoveredUnit = null;
            }

            UpdateProfileVisibility();
        }

        public void HandleUnitHoverStayed(Unit unit)
        {
            if (unit != null && unit.IsAlive)
            {
                hoveredUnit = unit;
            }

            if (unit != null && unit == previewTarget)
            {
                battleTurnController?.UpdateCombatPreviewPosition();
            }

            if (interactionState == PlayerInteractionState.ChoosingSkillTarget && unit != null && unit.OccupiedTile == currentSkillTargetTile)
            {
                battleTurnController?.UpdateCombatPreviewPosition();
            }
        }

        public void SelectUnit(Unit unit)
        {
            if (interactionState != PlayerInteractionState.Idle || battleTurnController == null || !battleTurnController.CanSelectUnit(unit))
            {
                return;
            }

            selectedUnit = unit;
            originalTile = unit.OccupiedTile;
            currentTile = originalTile;
            currentAttackTarget = null;
            hasDisplacedProvisionalMove = false;
            provisionalMovementPath.Clear();
            selectedUnit.ApplySelectionState(true);
            gridSystem?.ClearSelectedTile();
            SetInteractionState(PlayerInteractionState.ChoosingMovement);

            Debug.Log($"Selected Unit: {selectedUnit.DisplayName} at {selectedUnit.CurrentCoordinate}");
            Debug.Log($"Movement Range Tiles: {highlightedMovementTiles.Count}");
        }

        public void DeselectCurrentUnit()
        {
            if (interactionState == PlayerInteractionState.Moving || interactionState == PlayerInteractionState.ReturningToOriginalTile || interactionState == PlayerInteractionState.ResolvingCombat)
            {
                return;
            }

            ClearSelectionAndRuntimeData(false);
            SetInteractionState(PlayerInteractionState.Idle);
        }

        public void ClearBattleUiAndSelection()
        {
            ClearSelectionAndRuntimeData(true);
            SetInteractionState(PlayerInteractionState.BattleEnded);
        }

        public void WaitSelectedUnit()
        {
            if (interactionState != PlayerInteractionState.ChoosingAction || selectedUnit == null || selectedUnit.HasActed)
            {
                return;
            }

            CommitSelectedUnitAction();
        }

        public void BeginAttackTargeting()
        {
            if (interactionState != PlayerInteractionState.ChoosingAction || selectedUnit == null || selectedUnit.HasActed)
            {
                return;
            }

            SetInteractionState(PlayerInteractionState.ChoosingAttackTarget);
        }

        public void BeginSkillSelection()
        {
            if (interactionState != PlayerInteractionState.ChoosingAction || selectedUnit == null || selectedUnit.HasActed || !HasUsableSkills(selectedUnit))
            {
                return;
            }

            selectedSkill = null;
            SetInteractionState(PlayerInteractionState.ChoosingSkill);
        }

        public void SelectSkill(SkillDefinition skill)
        {
            if (interactionState != PlayerInteractionState.ChoosingSkill || selectedUnit == null || skill == null)
            {
                return;
            }

            selectedSkill = skill;
            SetInteractionState(PlayerInteractionState.ChoosingSkillTarget);
        }

        public void CancelCurrentAction()
        {
            switch (interactionState)
            {
                case PlayerInteractionState.ChoosingAttackTarget:
                    SetInteractionState(PlayerInteractionState.ChoosingAction);
                    break;
                case PlayerInteractionState.ChoosingSkillTarget:
                    SetInteractionState(PlayerInteractionState.ChoosingSkill);
                    break;
                case PlayerInteractionState.ChoosingSkill:
                    selectedSkill = null;
                    SetInteractionState(PlayerInteractionState.ChoosingAction);
                    break;
                case PlayerInteractionState.ChoosingAction:
                    BeginReturnToOriginalTile();
                    break;
                case PlayerInteractionState.ChoosingMovement:
                    DeselectCurrentUnit();
                    break;
            }
        }

        private void SetInteractionState(PlayerInteractionState newState)
        {
            if (interactionState == newState)
            {
                ApplyStateUi(newState);
                return;
            }

            ExitState(interactionState);
            interactionState = newState;
            EnterState(newState);
            battleTurnController?.UpdateTurnControls();
            UpdateProfileVisibility();
        }

        private void ExitState(PlayerInteractionState oldState)
        {
            if (oldState == PlayerInteractionState.ChoosingAttackTarget)
            {
                ClearAttackTargetingHighlights();
                ClearCombatPreview();
            }

            if (oldState == PlayerInteractionState.ChoosingSkillTarget)
            {
                ClearSkillTargetingHighlights();
                ClearSkillHoverPreview();
            }

            if (oldState == PlayerInteractionState.ChoosingSkill)
            {
                skillSelectionPanelController?.Hide();
            }
        }

        private void EnterState(PlayerInteractionState newState)
        {
            switch (newState)
            {
                case PlayerInteractionState.Idle:
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearCombatPreview();
                    actionMenuController?.Hide();
                    break;
                case PlayerInteractionState.ChoosingMovement:
                    ValidateSelectedUnitForState(newState);
                    actionMenuController?.Hide();
                    ClearAttackTargetingHighlights();
                    ClearCombatPreview();
                    RefreshMovementRangePreview(selectedUnit);
                    break;
                case PlayerInteractionState.Moving:
                case PlayerInteractionState.ReturningToOriginalTile:
                case PlayerInteractionState.ResolvingCombat:
                case PlayerInteractionState.ResolvingSkill:
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearSkillTargetingHighlights();
                    ClearCombatPreview();
                    skillSelectionPanelController?.Hide();
                    actionMenuController?.Hide();
                    break;
                case PlayerInteractionState.ChoosingAction:
                    ValidateSelectedUnitForState(newState);
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearSkillTargetingHighlights();
                    ClearCombatPreview();
                    skillSelectionPanelController?.Hide();
                    actionMenuController?.Show(selectedUnit, true, HasUsableSkills(selectedUnit));
                    break;
                case PlayerInteractionState.ChoosingSkill:
                    ValidateSelectedUnitForState(newState);
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearSkillTargetingHighlights();
                    ClearCombatPreview();
                    actionMenuController?.Hide();
                    skillSelectionPanelController?.Show(selectedUnit);
                    break;
                case PlayerInteractionState.ChoosingSkillTarget:
                    ValidateSelectedUnitForState(newState);
                    skillSelectionPanelController?.Hide();
                    actionMenuController?.ShowAttackCancel(selectedUnit);
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearCombatPreview();
                    RefreshSkillTargetingHighlights();
                    break;
                case PlayerInteractionState.ChoosingAttackTarget:
                    ValidateSelectedUnitForState(newState);
                    actionMenuController?.ShowAttackCancel(selectedUnit);
                    skillSelectionPanelController?.Hide();
                    ClearMovementRangePreview();
                    ClearCombatPreview();
                    RefreshAttackRangePreview(selectedUnit);
                    RefreshAttackTargets(selectedUnit);
                    break;
                case PlayerInteractionState.BattleEnded:
                    ClearMovementRangePreview();
                    ClearAttackTargetingHighlights();
                    ClearSkillTargetingHighlights();
                    ClearCombatPreview();
                    actionMenuController?.Hide();
                    skillSelectionPanelController?.Hide();
                    unitProfileController?.Hide();
                    break;
            }
        }

        private void ApplyStateUi(PlayerInteractionState state)
        {
            EnterState(state);
            battleTurnController?.UpdateTurnControls();
            UpdateProfileVisibility();
        }

        private void HandleMovementChoiceTileClicked(Tile tile)
        {
            if (selectedUnit == null || tile == null)
            {
                return;
            }

            if (tile == selectedUnit.OccupiedTile || tile == originalTile)
            {
                ChooseStayInPlace();
                return;
            }

            if (!reachableTiles.Contains(tile))
            {
                return;
            }

            BeginMoveSelectedUnit(tile);
        }

        private void ChooseStayInPlace()
        {
            if (selectedUnit == null || interactionState != PlayerInteractionState.ChoosingMovement)
            {
                return;
            }

            currentTile = originalTile;
            hasDisplacedProvisionalMove = false;
            provisionalMovementPath.Clear();
            SetInteractionState(PlayerInteractionState.ChoosingAction);
            Debug.Log($"{selectedUnit.DisplayName} stays at {selectedUnit.CurrentCoordinate}");
        }

        private void BeginMoveSelectedUnit(Tile destination)
        {
            if (gridSystem == null || gridSystem.Pathfinder == null || selectedUnit == null || destination == null || originalTile == null)
            {
                Debug.LogError("Cannot start movement without a selected unit, grid, destination, and original tile.", this);
                return;
            }

            if (!destination.CanEnter(selectedUnit) || destination.OccupyingUnit != null)
            {
                Debug.LogWarning($"Invalid movement destination for {selectedUnit.DisplayName}: {destination.Coordinate}.", destination);
                return;
            }

            if (!gridSystem.Pathfinder.TryFindPath(selectedUnit.OccupiedTile, destination, selectedUnit, pathBuffer))
            {
                Debug.LogWarning($"No valid path for {selectedUnit.DisplayName} to {destination.Coordinate}.", destination);
                return;
            }

            provisionalMovementPath.Clear();
            provisionalMovementPath.AddRange(pathBuffer);
            Tile startTile = selectedUnit.OccupiedTile;
            destination.SetOccupyingUnit(selectedUnit);
            SetInteractionState(PlayerInteractionState.Moving);
            selectedUnit.MoveAlongPath(provisionalMovementPath, (unit, arrivedTile) => CompleteProvisionalMovement(unit, startTile, arrivedTile));
        }

        private void CompleteProvisionalMovement(Unit unit, Tile previousTile, Tile arrivedTile)
        {
            if (interactionState != PlayerInteractionState.Moving || unit == null)
            {
                return;
            }

            if (previousTile != null && previousTile != arrivedTile)
            {
                previousTile.SetOccupyingUnit(null);
            }

            arrivedTile.SetOccupyingUnit(unit);
            currentTile = arrivedTile;
            hasDisplacedProvisionalMove = arrivedTile != originalTile;

            if (!ValidateOccupancy("provisional movement complete"))
            {
                return;
            }

            if (selectedUnit == unit && unit.IsAlive && !unit.HasActed)
            {
                SetInteractionState(PlayerInteractionState.ChoosingAction);
            }
            else
            {
                ClearSelectionAndRuntimeData(false);
                SetInteractionState(PlayerInteractionState.Idle);
            }
        }

        private void BeginReturnToOriginalTile()
        {
            if (selectedUnit == null || originalTile == null)
            {
                Debug.LogError("Cannot return to original tile without selected unit and original tile.", this);
                ClearSelectionAndRuntimeData(false);
                SetInteractionState(PlayerInteractionState.Idle);
                return;
            }

            if (originalTile.OccupyingUnit != null && originalTile.OccupyingUnit != selectedUnit)
            {
                Debug.LogError($"Cannot return {selectedUnit.DisplayName}; original tile {originalTile.Coordinate} is occupied by {originalTile.OccupyingUnit.DisplayName}.", originalTile.OccupyingUnit);
                SetInteractionState(PlayerInteractionState.ChoosingAction);
                return;
            }

            if (selectedUnit.OccupiedTile == originalTile)
            {
                currentTile = originalTile;
                hasDisplacedProvisionalMove = false;
                provisionalMovementPath.Clear();
                SetInteractionState(PlayerInteractionState.ChoosingMovement);
                return;
            }

            if (!TryBuildReturnPath(returnPathBuffer))
            {
                Debug.LogError($"Could not find a valid return path for {selectedUnit.DisplayName} to original tile {originalTile.Coordinate}.", selectedUnit);
                SetInteractionState(PlayerInteractionState.ChoosingAction);
                return;
            }

            Tile displacedTile = selectedUnit.OccupiedTile;
            originalTile.SetOccupyingUnit(selectedUnit);
            SetInteractionState(PlayerInteractionState.ReturningToOriginalTile);
            selectedUnit.MoveAlongPath(returnPathBuffer, (unit, arrivedTile) => CompleteReturnToOriginalTile(unit, displacedTile, arrivedTile));
        }

        private bool TryBuildReturnPath(List<Tile> results)
        {
            results.Clear();

            if (provisionalMovementPath.Count > 1 && provisionalMovementPath[0] == originalTile && provisionalMovementPath[provisionalMovementPath.Count - 1] == selectedUnit.OccupiedTile)
            {
                for (int i = provisionalMovementPath.Count - 1; i >= 0; i--)
                {
                    results.Add(provisionalMovementPath[i]);
                }
                return true;
            }

            return gridSystem != null && gridSystem.Pathfinder != null && gridSystem.Pathfinder.TryFindPath(selectedUnit.OccupiedTile, originalTile, selectedUnit, results);
        }

        private void CompleteReturnToOriginalTile(Unit unit, Tile displacedTile, Tile arrivedTile)
        {
            if (interactionState != PlayerInteractionState.ReturningToOriginalTile || unit == null)
            {
                return;
            }

            if (displacedTile != null && displacedTile != arrivedTile)
            {
                displacedTile.SetOccupyingUnit(null);
            }

            arrivedTile.SetOccupyingUnit(unit);
            currentTile = arrivedTile;
            hasDisplacedProvisionalMove = false;
            provisionalMovementPath.Clear();

            if (!ValidateOccupancy("rollback complete"))
            {
                return;
            }

            SetInteractionState(PlayerInteractionState.ChoosingMovement);
        }

        private void BeginPlayerAttack(Unit target)
        {
            if (interactionState != PlayerInteractionState.ChoosingAttackTarget || battleTurnController == null || selectedUnit == null || target == null || !CombatResolver.CanAttack(selectedUnit, target))
            {
                return;
            }

            currentAttackTarget = target;
            SetInteractionState(PlayerInteractionState.ResolvingCombat);
            StartCoroutine(CompletePlayerCombatRoutine(selectedUnit, target));
        }

        private void HandleSkillTargetUnitClicked(Unit unit)
        {
            if (selectedSkill == null || selectedSkill.TargetType != SkillTargetType.Unit || unit == null || !SkillResolver.CanTargetUnit(selectedUnit, selectedSkill, unit))
            {
                return;
            }

            CastSelectedSkill(unit.OccupiedTile);
        }

        private void HandleSkillTargetTileClicked(Tile tile)
        {
            if (selectedSkill == null || tile == null || !SkillResolver.CanTargetTile(selectedUnit, selectedSkill, tile))
            {
                return;
            }

            CastSelectedSkill(tile);
        }

        private void CastSelectedSkill(Tile targetTile)
        {
            if (selectedUnit == null || selectedSkill == null || targetTile == null)
            {
                return;
            }

            SkillResolver.FillAreaTiles(gridSystem, targetTile, selectedSkill, skillAreaBuffer);
            SkillResolver.FillAffectedUnits(selectedUnit, selectedSkill, skillAreaBuffer, skillAffectedUnitBuffer);
            if (selectedSkill.TargetType == SkillTargetType.Unit && targetTile.OccupyingUnit != null && SkillResolver.CanTargetUnit(selectedUnit, selectedSkill, targetTile.OccupyingUnit) && !skillAffectedUnitBuffer.Contains(targetTile.OccupyingUnit))
            {
                skillAffectedUnitBuffer.Add(targetTile.OccupyingUnit);
            }

            if (skillAffectedUnitBuffer.Count == 0)
            {
                return;
            }

            List<Unit> affectedUnits = new List<Unit>(skillAffectedUnitBuffer);
            currentSkillTargetTile = targetTile;
            SetInteractionState(PlayerInteractionState.ResolvingSkill);
            CompleteSkill(selectedUnit, selectedSkill, affectedUnits);
        }

        private void CompleteSkill(Unit caster, SkillDefinition skill, List<Unit> affectedUnits)
        {
            SkillResolver.Resolve(caster, skill, affectedUnits);

            if (battleTurnController != null && battleTurnController.CheckBattleEndAfterSkill())
            {
                ClearSelectionAndRuntimeData(true);
                SetInteractionState(PlayerInteractionState.BattleEnded);
                return;
            }

            CommitSelectedUnitAction();
        }

        private IEnumerator CompletePlayerCombatRoutine(Unit attacker, Unit defender)
        {
            yield return battleTurnController.ResolveCombatExchange(attacker, defender);

            if (battleTurnController != null && battleTurnController.IsBattleEnded)
            {
                ClearSelectionAndRuntimeData(true);
                SetInteractionState(PlayerInteractionState.BattleEnded);
                yield break;
            }

            CommitSelectedUnitAction();
        }

        private void CommitSelectedUnitAction()
        {
            if (selectedUnit == null)
            {
                Debug.LogError("Cannot commit an action without a selected unit.", this);
                ClearSelectionAndRuntimeData(false);
                SetInteractionState(PlayerInteractionState.Idle);
                return;
            }

            ClearMovementRangePreview();
            ClearAttackTargetingHighlights();
            ClearCombatPreview();
            actionMenuController?.Hide();

            Unit actedUnit = selectedUnit;
            if (selectedUnit.IsAlive)
            {
                selectedUnit.SetHasActed(true);
                selectedUnit.ApplySelectionState(false);
            }

            ClearSelectionAndRuntimeData(false);
            SetInteractionState(PlayerInteractionState.Idle);
            battleTurnController?.NotifyPlayerUnitActionFinished(actedUnit);
        }

        private void RefreshMovementRangePreview(Unit unit)
        {
            ClearMovementRangePreview();

            if (unit == null || gridSystem == null || gridSystem.Reachability == null)
            {
                return;
            }

            gridSystem.Reachability.FindReachableTiles(unit.OccupiedTile, unit, unit.MovementRange, highlightedMovementTiles);

            for (int i = 0; i < highlightedMovementTiles.Count; i++)
            {
                Tile tile = highlightedMovementTiles[i];
                tile.SetMovementRangeHighlighted(true);
                reachableTiles.Add(tile);
            }
        }

        private void ClearMovementRangePreview()
        {
            for (int i = 0; i < highlightedMovementTiles.Count; i++)
            {
                if (highlightedMovementTiles[i] != null)
                {
                    highlightedMovementTiles[i].SetMovementRangeHighlighted(false);
                }
            }

            highlightedMovementTiles.Clear();
            reachableTiles.Clear();
        }

        private void RefreshAttackRangePreview(Unit attacker)
        {
            ClearAttackRangePreview();
            if (gridSystem == null || attacker == null || attacker.OccupiedTile == null)
            {
                return;
            }

            gridSystem.FillTilesInAttackRange(attacker, highlightedAttackRangeTiles);
            for (int i = 0; i < highlightedAttackRangeTiles.Count; i++)
            {
                highlightedAttackRangeTiles[i].SetAttackRangeHighlighted(true);
            }
        }

        private void ClearAttackRangePreview()
        {
            for (int i = 0; i < highlightedAttackRangeTiles.Count; i++)
            {
                if (highlightedAttackRangeTiles[i] != null)
                {
                    highlightedAttackRangeTiles[i].SetAttackRangeHighlighted(false);
                }
            }

            highlightedAttackRangeTiles.Clear();
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

        private void ClearAttackTargetingHighlights()
        {
            ClearAttackRangePreview();
            ClearAttackTargets();
            currentAttackTarget = null;
        }

        private void RefreshSkillTargetingHighlights()
        {
            ClearSkillTargetingHighlights();
            if (selectedUnit == null || selectedSkill == null || gridSystem == null)
            {
                return;
            }

            gridSystem.FillTilesInRange(selectedUnit, selectedSkill.MinimumRange, selectedSkill.MaximumRange, highlightedSkillRangeTiles);
            for (int i = 0; i < highlightedSkillRangeTiles.Count; i++)
            {
                Tile tile = highlightedSkillRangeTiles[i];
                tile.SetAttackRangeHighlighted(true);
                if (SkillResolver.CanTargetTile(selectedUnit, selectedSkill, tile))
                {
                    tile.SetSkillTargetHighlighted(true);
                    highlightedSkillTargetTiles.Add(tile);
                }
            }
        }

        private void ClearSkillTargetingHighlights()
        {
            for (int i = 0; i < highlightedSkillRangeTiles.Count; i++)
            {
                if (highlightedSkillRangeTiles[i] != null)
                {
                    highlightedSkillRangeTiles[i].SetAttackRangeHighlighted(false);
                }
            }

            for (int i = 0; i < highlightedSkillTargetTiles.Count; i++)
            {
                if (highlightedSkillTargetTiles[i] != null)
                {
                    highlightedSkillTargetTiles[i].SetSkillTargetHighlighted(false);
                }
            }

            ClearSkillHoverPreview();
            highlightedSkillRangeTiles.Clear();
            highlightedSkillTargetTiles.Clear();
        }

        private void ShowSkillTargetPreview(Tile tile)
        {
            if (selectedUnit == null || selectedSkill == null || tile == null || !SkillResolver.CanTargetTile(selectedUnit, selectedSkill, tile))
            {
                ClearSkillHoverPreview();
                return;
            }

            ClearSkillHoverPreview();
            currentSkillTargetTile = tile;
            SkillResolver.FillAreaTiles(gridSystem, tile, selectedSkill, skillAreaBuffer);
            SkillResolver.FillAffectedUnits(selectedUnit, selectedSkill, skillAreaBuffer, skillAffectedUnitBuffer);

            for (int i = 0; i < skillAreaBuffer.Count; i++)
            {
                skillAreaBuffer[i].SetSkillAreaHighlighted(true);
                highlightedSkillAreaTiles.Add(skillAreaBuffer[i]);
            }

            for (int i = 0; i < skillAffectedUnitBuffer.Count; i++)
            {
                skillAffectedUnitBuffer[i].SetCombatPreviewHighlighted(true);
                highlightedSkillAffectedUnits.Add(skillAffectedUnitBuffer[i]);
            }

            battleTurnController?.ShowSkillPreview(SkillResolver.BuildPreview(selectedUnit, selectedSkill, tile, skillAreaBuffer, skillAffectedUnitBuffer));
        }

        private void ClearSkillHoverPreview()
        {
            for (int i = 0; i < highlightedSkillAreaTiles.Count; i++)
            {
                if (highlightedSkillAreaTiles[i] != null)
                {
                    highlightedSkillAreaTiles[i].SetSkillAreaHighlighted(false);
                }
            }

            for (int i = 0; i < highlightedSkillAffectedUnits.Count; i++)
            {
                if (highlightedSkillAffectedUnits[i] != null)
                {
                    highlightedSkillAffectedUnits[i].SetCombatPreviewHighlighted(false);
                }
            }

            highlightedSkillAreaTiles.Clear();
            highlightedSkillAffectedUnits.Clear();
            skillAreaBuffer.Clear();
            skillAffectedUnitBuffer.Clear();
            currentSkillTargetTile = null;
            battleTurnController?.HideCombatPreview();
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
            battleTurnController.UpdateTurnControls();
            UpdateProfileVisibility();
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

        private void ClearSelectionAndRuntimeData(bool forceProfileHide)
        {
            ClearMovementRangePreview();
            ClearAttackTargetingHighlights();
            ClearSkillTargetingHighlights();
            ClearCombatPreview();
            actionMenuController?.Hide();
            skillSelectionPanelController?.Hide();

            if (selectedUnit != null)
            {
                selectedUnit.ApplySelectionState(false);
            }

            selectedUnit = null;
            originalTile = null;
            currentTile = null;
            currentAttackTarget = null;
            selectedSkill = null;
            currentSkillTargetTile = null;
            hasDisplacedProvisionalMove = false;
            provisionalMovementPath.Clear();
            pathBuffer.Clear();
            returnPathBuffer.Clear();

            if (forceProfileHide)
            {
                unitProfileController?.Hide();
            }
        }

        private bool ValidateSelectedUnitForState(PlayerInteractionState state)
        {
            if (selectedUnit != null && selectedUnit.IsAlive)
            {
                return true;
            }

            Debug.LogError($"Cannot enter {state} without a living selected unit.", this);
            return false;
        }

        private bool ValidateOccupancy(string context)
        {
            if (selectedUnit == null)
            {
                return false;
            }

            Tile occupiedTile = selectedUnit.OccupiedTile;
            if (occupiedTile == null)
            {
                Debug.LogError($"Occupancy validation failed after {context}: selected unit has no occupied tile.", selectedUnit);
                return false;
            }

            if (occupiedTile.OccupyingUnit != selectedUnit)
            {
                Debug.LogError($"Occupancy validation failed after {context}: {occupiedTile.Coordinate} does not contain {selectedUnit.DisplayName}.", selectedUnit);
                return false;
            }

            if (originalTile != null && originalTile != occupiedTile && originalTile.OccupyingUnit == selectedUnit)
            {
                Debug.LogError($"Occupancy validation failed after {context}: both original tile {originalTile.Coordinate} and current tile {occupiedTile.Coordinate} contain {selectedUnit.DisplayName}.", selectedUnit);
                return false;
            }

            return true;
        }

        private static bool HasUsableSkills(Unit unit)
        {
            if (unit == null || unit.Skills == null)
            {
                return false;
            }

            for (int i = 0; i < unit.Skills.Count; i++)
            {
                if (unit.Skills[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateProfileVisibility()
        {
            if (unitProfileController == null)
            {
                return;
            }

            if (interactionState == PlayerInteractionState.BattleEnded || (battleTurnController != null && battleTurnController.IsBattleEnded))
            {
                unitProfileController.Hide();
                return;
            }

            if (battleTurnController != null && battleTurnController.IsCombatPreviewVisible)
            {
                unitProfileController.Hide();
                return;
            }

            if (selectedUnit != null && selectedUnit.IsAlive)
            {
                unitProfileController.Show(selectedUnit);
                return;
            }

            if (hoveredUnit != null && hoveredUnit.IsAlive)
            {
                unitProfileController.Show(hoveredUnit);
                return;
            }

            unitProfileController.Hide();
        }

        private void HandleKeyboardInput()
        {
            if (interactionState == PlayerInteractionState.Moving || interactionState == PlayerInteractionState.ReturningToOriginalTile || interactionState == PlayerInteractionState.ResolvingCombat || interactionState == PlayerInteractionState.BattleEnded)
            {
                return;
            }

            if (WasCancelPressed())
            {
                CancelCurrentAction();
                return;
            }

            if (interactionState != PlayerInteractionState.ChoosingAction || selectedUnit == null || selectedUnit.HasActed)
            {
                return;
            }

            if (WasAttackShortcutPressed())
            {
                BeginAttackTargeting();
                return;
            }

            if (WasWaitShortcutPressed())
            {
                WaitSelectedUnit();
            }
        }

        private void EnsureUiControllers()
        {
            if (unitProfileController == null)
            {
                unitProfileController = FindAnyObjectByType<UnitProfileController>();
            }

            if (unitProfileController == null)
            {
                unitProfileController = gameObject.AddComponent<UnitProfileController>();
            }

            if (actionMenuController == null)
            {
                actionMenuController = FindAnyObjectByType<UnitActionMenuController>();
            }

            if (actionMenuController == null)
            {
                actionMenuController = gameObject.AddComponent<UnitActionMenuController>();
            }

            if (skillSelectionPanelController == null)
            {
                skillSelectionPanelController = FindAnyObjectByType<SkillSelectionPanelController>();
            }

            if (skillSelectionPanelController == null)
            {
                skillSelectionPanelController = gameObject.AddComponent<SkillSelectionPanelController>();
            }

            actionMenuController.Configure(BeginAttackTargeting, BeginSkillSelection, WaitSelectedUnit, CancelCurrentAction);
            skillSelectionPanelController.Configure(SelectSkill, CancelCurrentAction);
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private static bool WasAttackShortcutPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#else
            return false;
#endif
        }

        private static bool WasWaitShortcutPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#else
            return false;
#endif
        }
    }
}
