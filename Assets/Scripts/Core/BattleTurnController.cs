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
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button waitButton;

        private readonly List<Unit> units = new List<Unit>();
        private readonly List<Tile> reachableTiles = new List<Tile>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private readonly List<Tile> neighborBuffer = new List<Tile>(4);
        private BattlePhase currentPhase = BattlePhase.PlayerTurn;
        private bool isEnemyActing;
        private bool pendingEnemyTurn;
        private bool battleEnded;

        public BattlePhase CurrentPhase => currentPhase;
        public bool IsBattleEnded => battleEnded;
        public bool IsPlayerInputAllowed => !battleEnded && currentPhase == BattlePhase.PlayerTurn && !isEnemyActing && unitSelectionController != null && !unitSelectionController.IsUnitMoving;

        private void Awake()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(EndPlayerTurn);
            }

            if (waitButton != null)
            {
                waitButton.onClick.AddListener(() => unitSelectionController?.WaitSelectedUnit());
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            RefreshUnits();
            InitializeUnitHealth();
            if (resultLabel != null)
            {
                resultLabel.gameObject.SetActive(false);
            }
            BeginPlayerTurn();
        }

        public bool CanSelectUnit(Unit unit)
        {
            return IsPlayerInputAllowed && unit != null && unit.IsAlive && unit.Faction == UnitFaction.Player && !unit.HasActed;
        }

        public void NotifyPlayerUnitActionFinished(Unit unit)
        {
            if (battleEnded || unit == null || unit.Faction != UnitFaction.Player)
            {
                return;
            }

            unitSelectionController?.DeselectCurrentUnit();

            if (CheckBattleEnd())
            {
                return;
            }

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
            if (battleEnded || currentPhase != BattlePhase.PlayerTurn)
            {
                return;
            }

            if (unitSelectionController != null && (unitSelectionController.IsUnitMoving || unitSelectionController.HasPendingAction))
            {
                UpdateTurnUi();
                return;
            }

            StartEnemyTurn();
        }

        private void BeginPlayerTurn()
        {
            if (battleEnded)
            {
                return;
            }

            currentPhase = BattlePhase.PlayerTurn;
            isEnemyActing = false;
            pendingEnemyTurn = false;
            RefreshUnits();
            ResetActedState(UnitFaction.Player);
            unitSelectionController?.DeselectCurrentUnit();
            CheckBattleEnd();
            UpdateTurnUi();
        }

        private void StartEnemyTurn()
        {
            if (battleEnded)
            {
                return;
            }

            currentPhase = BattlePhase.EnemyTurn;
            isEnemyActing = true;
            pendingEnemyTurn = false;
            unitSelectionController?.DeselectCurrentUnit();
            RefreshUnits();
            ResetActedState(UnitFaction.Enemy);
            CheckBattleEnd();
            UpdateTurnUi();
            StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator RunEnemyTurn()
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit enemy = units[i];
                if (battleEnded || enemy == null || !enemy.IsAlive || enemy.Faction != UnitFaction.Enemy || enemy.HasActed)
                {
                    continue;
                }

                yield return MoveEnemyUnit(enemy);
                if (enemy.IsAlive)
                {
                    enemy.SetHasActed(true);
                }

                if (CheckBattleEnd())
                {
                    yield break;
                }
            }

            BeginPlayerTurn();
        }

        private IEnumerator MoveEnemyUnit(Unit enemy)
        {
            if (enemy.OccupiedTile == null)
            {
                yield break;
            }

            Unit target = GetAdjacentAttackTarget(enemy);
            if (target == null && TryChooseEnemyDestination(enemy, out Tile destination) && destination != enemy.OccupiedTile)
            {
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

            target = GetAdjacentAttackTarget(enemy);
            if (target == null)
            {
                yield break;
            }

            bool attackCompleted = false;
            enemy.PlayAttack(target, () =>
            {
                ResolveAttack(enemy, target);
                attackCompleted = true;
            });

            while (!attackCompleted)
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
                if (player == null || !player.IsAlive || player.Faction != UnitFaction.Player || player.OccupiedTile == null)
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

        private Unit GetAdjacentAttackTarget(Unit attacker)
        {
            Unit bestTarget = null;
            gridSystem.FillNeighbors(attacker.OccupiedTile, neighborBuffer);
            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                Unit occupyingUnit = neighborBuffer[i].OccupyingUnit;
                if (occupyingUnit == null || !occupyingUnit.IsAlive || occupyingUnit.Faction == attacker.Faction)
                {
                    continue;
                }

                if (bestTarget == null || IsBetterAttackTarget(occupyingUnit, bestTarget))
                {
                    bestTarget = occupyingUnit;
                }
            }

            return bestTarget;
        }

        private static bool IsBetterAttackTarget(Unit candidate, Unit currentBest)
        {
            if (candidate.CurrentHealth != currentBest.CurrentHealth)
            {
                return candidate.CurrentHealth < currentBest.CurrentHealth;
            }

            if (candidate.CurrentCoordinate.Y != currentBest.CurrentCoordinate.Y)
            {
                return candidate.CurrentCoordinate.Y < currentBest.CurrentCoordinate.Y;
            }

            return candidate.CurrentCoordinate.X < currentBest.CurrentCoordinate.X;
        }

        public int ResolveAttack(Unit attacker, Unit defender)
        {
            if (battleEnded || attacker == null || defender == null || !attacker.IsAlive || !defender.IsAlive || attacker.Faction == defender.Faction)
            {
                return 0;
            }

            int damage = CombatResolver.CalculateDamage(attacker, defender);
            defender.ReceiveDamage(damage);
            CheckBattleEnd();
            return damage;
        }

        private void RefreshUnits()
        {
            units.Clear();
            units.AddRange(FindObjectsByType<Unit>(FindObjectsSortMode.None));
        }

        private void InitializeUnitHealth()
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    units[i].InitializeHealthForBattle();
                }
            }
        }

        private void ResetActedState(UnitFaction faction)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit != null && unit.IsAlive && unit.Faction == faction)
                {
                    unit.SetHasActed(false);
                }
            }
        }

        private bool CheckBattleEnd()
        {
            if (battleEnded)
            {
                return true;
            }

            RefreshUnits();
            bool hasLivingPlayer = false;
            bool hasLivingEnemy = false;

            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                hasLivingPlayer |= unit.Faction == UnitFaction.Player;
                hasLivingEnemy |= unit.Faction == UnitFaction.Enemy;
            }

            if (!hasLivingEnemy)
            {
                EndBattle("Victory");
                return true;
            }

            if (!hasLivingPlayer)
            {
                EndBattle("Defeat");
                return true;
            }

            return false;
        }

        private void EndBattle(string message)
        {
            battleEnded = true;
            isEnemyActing = false;
            pendingEnemyTurn = false;
            unitSelectionController?.DeselectCurrentUnit();

            if (resultLabel != null)
            {
                resultLabel.text = message;
                resultLabel.gameObject.SetActive(true);
            }

            UpdateTurnUi();
        }

        public void UpdateTurnControls()
        {
            UpdateTurnUi();
        }

        private void UpdateTurnUi()
        {
            if (turnLabel != null)
            {
                turnLabel.text = currentPhase == BattlePhase.PlayerTurn ? "Player Turn" : "Enemy Turn";
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(!battleEnded && currentPhase == BattlePhase.PlayerTurn);
                endTurnButton.interactable = IsPlayerInputAllowed && !pendingEnemyTurn && (unitSelectionController == null || !unitSelectionController.HasPendingAction);
            }

            if (waitButton != null)
            {
                waitButton.gameObject.SetActive(!battleEnded && currentPhase == BattlePhase.PlayerTurn);
                waitButton.interactable = IsPlayerInputAllowed && unitSelectionController != null && unitSelectionController.HasPendingAction;
            }
        }
    }
}
