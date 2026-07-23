using System.Collections;
using System.Collections.Generic;
using SLG.Grid;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.Core
{
    public sealed class BattleTurnController : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private UnitSelectionController unitSelectionController;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Button endTurnButton;

        private readonly List<Unit> units = new List<Unit>();
        private readonly List<Tile> reachableTiles = new List<Tile>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private readonly List<Tile> neighborBuffer = new List<Tile>(4);
        private BattlePhase currentPhase = BattlePhase.PlayerTurn;
        private bool isEnemyActing;
        private bool pendingEnemyTurn;

        public BattlePhase CurrentPhase => currentPhase;
        public bool IsPlayerInputAllowed => currentPhase == BattlePhase.PlayerTurn && !isEnemyActing && unitSelectionController != null && !unitSelectionController.IsUnitMoving;

        private void Awake()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(EndPlayerTurn);
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            RefreshUnits();
            BeginPlayerTurn();
        }

        public bool CanSelectUnit(Unit unit)
        {
            return IsPlayerInputAllowed && unit != null && unit.Faction == UnitFaction.Player && !unit.HasActed;
        }

        public void NotifyPlayerUnitMoved(Unit unit)
        {
            if (unit == null || unit.Faction != UnitFaction.Player)
            {
                return;
            }

            unit.SetHasActed(true);
            unitSelectionController?.DeselectCurrentUnit();

            if (pendingEnemyTurn)
            {
                pendingEnemyTurn = false;
                StartEnemyTurn();
                return;
            }

            UpdateTurnUi();
        }

        public void EndPlayerTurn()
        {
            if (currentPhase != BattlePhase.PlayerTurn)
            {
                return;
            }

            if (unitSelectionController != null && unitSelectionController.IsUnitMoving)
            {
                pendingEnemyTurn = true;
                UpdateTurnUi();
                return;
            }

            StartEnemyTurn();
        }

        private void BeginPlayerTurn()
        {
            currentPhase = BattlePhase.PlayerTurn;
            isEnemyActing = false;
            pendingEnemyTurn = false;
            RefreshUnits();
            ResetActedState(UnitFaction.Player);
            unitSelectionController?.DeselectCurrentUnit();
            UpdateTurnUi();
        }

        private void StartEnemyTurn()
        {
            currentPhase = BattlePhase.EnemyTurn;
            isEnemyActing = true;
            pendingEnemyTurn = false;
            unitSelectionController?.DeselectCurrentUnit();
            RefreshUnits();
            ResetActedState(UnitFaction.Enemy);
            UpdateTurnUi();
            StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator RunEnemyTurn()
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit enemy = units[i];
                if (enemy == null || enemy.Faction != UnitFaction.Enemy || enemy.HasActed)
                {
                    continue;
                }

                yield return MoveEnemyUnit(enemy);
                enemy.SetHasActed(true);
            }

            BeginPlayerTurn();
        }

        private IEnumerator MoveEnemyUnit(Unit enemy)
        {
            if (enemy.OccupiedTile == null || IsAdjacentToPlayer(enemy.OccupiedTile))
            {
                yield break;
            }

            if (!TryChooseEnemyDestination(enemy, out Tile destination) || destination == enemy.OccupiedTile)
            {
                yield break;
            }

            if (!gridSystem.Pathfinder.TryFindPath(enemy.OccupiedTile, destination, enemy, pathBuffer))
            {
                yield break;
            }

            bool completed = false;
            Tile startTile = enemy.OccupiedTile;
            destination.SetOccupyingUnit(enemy);
            enemy.MoveAlongPath(pathBuffer, (unit, arrivedTile) =>
            {
                if (startTile != null && startTile != arrivedTile)
                {
                    startTile.SetOccupyingUnit(null);
                }

                arrivedTile.SetOccupyingUnit(unit);
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }
        }

        private bool TryChooseEnemyDestination(Unit enemy, out Tile destination)
        {
            destination = null;
            int bestDistance = int.MaxValue;
            int bestPathCost = int.MaxValue;

            gridSystem.Reachability.FindReachableTiles(enemy.OccupiedTile, enemy, enemy.MovementRange, reachableTiles);

            for (int i = 0; i < reachableTiles.Count; i++)
            {
                Tile candidate = reachableTiles[i];
                if (candidate == enemy.OccupiedTile || candidate.OccupyingUnit != null)
                {
                    continue;
                }

                if (!TryGetDistanceToNearestPlayer(candidate, enemy, out int distanceToPlayer))
                {
                    continue;
                }

                int movementCost = GetPathCost(enemy.OccupiedTile, candidate, enemy);
                if (IsBetterEnemyDestination(candidate, destination, distanceToPlayer, movementCost, bestDistance, bestPathCost))
                {
                    destination = candidate;
                    bestDistance = distanceToPlayer;
                    bestPathCost = movementCost;
                }
            }

            return destination != null;
        }

        private bool TryGetDistanceToNearestPlayer(Tile fromTile, Unit movingEnemy, out int bestDistance)
        {
            bestDistance = int.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                Unit player = units[i];
                if (player == null || player.Faction != UnitFaction.Player || player.OccupiedTile == null)
                {
                    continue;
                }

                gridSystem.FillNeighbors(player.OccupiedTile, neighborBuffer);
                for (int j = 0; j < neighborBuffer.Count; j++)
                {
                    Tile adjacentTile = neighborBuffer[j];
                    if (!adjacentTile.CanEnter(movingEnemy))
                    {
                        continue;
                    }

                    if (gridSystem.Pathfinder.TryFindPath(fromTile, adjacentTile, movingEnemy, pathBuffer))
                    {
                        int distance = GetPathCost(pathBuffer);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                        }
                    }
                }
            }

            return bestDistance < int.MaxValue;
        }

        private int GetPathCost(Tile start, Tile destination, Unit movingUnit)
        {
            if (!gridSystem.Pathfinder.TryFindPath(start, destination, movingUnit, pathBuffer))
            {
                return int.MaxValue;
            }

            return GetPathCost(pathBuffer);
        }

        private static int GetPathCost(IReadOnlyList<Tile> path)
        {
            int cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                cost += path[i].MovementCost;
            }

            return cost;
        }

        private static bool IsBetterEnemyDestination(Tile candidate, Tile currentBest, int distance, int pathCost, int bestDistance, int bestPathCost)
        {
            if (distance != bestDistance)
            {
                return distance < bestDistance;
            }

            if (pathCost != bestPathCost)
            {
                return pathCost < bestPathCost;
            }

            if (currentBest == null)
            {
                return true;
            }

            if (candidate.Y != currentBest.Y)
            {
                return candidate.Y < currentBest.Y;
            }

            return candidate.X < currentBest.X;
        }

        private bool IsAdjacentToPlayer(Tile tile)
        {
            gridSystem.FillNeighbors(tile, neighborBuffer);
            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                Unit occupyingUnit = neighborBuffer[i].OccupyingUnit;
                if (occupyingUnit != null && occupyingUnit.Faction == UnitFaction.Player)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshUnits()
        {
            units.Clear();
            units.AddRange(FindObjectsByType<Unit>(FindObjectsSortMode.None));
        }

        private void ResetActedState(UnitFaction faction)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit != null && unit.Faction == faction)
                {
                    unit.SetHasActed(false);
                }
            }
        }

        private void UpdateTurnUi()
        {
            if (turnLabel != null)
            {
                turnLabel.text = currentPhase == BattlePhase.PlayerTurn ? "Player Turn" : "Enemy Turn";
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(currentPhase == BattlePhase.PlayerTurn);
                endTurnButton.interactable = IsPlayerInputAllowed && !pendingEnemyTurn;
            }
        }
    }
}
