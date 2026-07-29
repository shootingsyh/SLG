using System.Collections;
using System.Collections.Generic;
using System.Text;
using SLG.Grid;
using SLG.Saves;
using SLG.Scenarios;
using SLG.Shell;
using SLG.Units;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;

namespace SLG.Core
{
    public enum SceneLoadedReason
    {
        None,
        Victory,
        Defeat
    }

    public enum BattleResultType
    {
        None,
        Victory,
        Defeat
    }

    public static class CampaignBatch
    {
        public static void RegisterProcessor(BattleTurnController controller, CampaignFlowService processor)
        {
            controller?.SetCampaignFlowProcessor(processor);
        }

        public static CampaignFlowService GetProcessor(BattleTurnController controller)
        {
            return controller == null ? null : controller._campaignFlowProcessor;
        }

        public static BattleResultType ResolveResult(BattleTurnController controller)
        {
            if (controller == null)
                return BattleResultType.None;

            string result = controller.BattleResult;
            if (result == "Victory")
                return BattleResultType.Victory;

            if (result == "Defeat")
                return BattleResultType.Defeat;

            return BattleResultType.None;
        }
    }

    public sealed class BattleTurnController : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private UnitSelectionController unitSelectionController;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button waitButton;
        [SerializeField] private GameObject combatPreviewPanel;
        [SerializeField] private Text combatPreviewText;
        [SerializeField] private BattleScenarioController scenarioController;
        [SerializeField] private BattleSystemMenuController systemMenuController;

        [Header("Battle HUD")]
        [Tooltip("Top-center turn/round display panel")]
        [SerializeField] private GameObject hudTurnPanel;
        [SerializeField] private Text hudTurnText;
        [SerializeField] private Text hudRoundText;
        [SerializeField] private GameObject hudObjectivePanel;
        [SerializeField] private Text hudObjectiveText;

        private readonly List<Unit> units = new List<Unit>();
        internal CampaignFlowService _campaignFlowProcessor;
        private SceneLoadedReason _sceneLoadedReason;
        private readonly List<Tile> reachableTiles = new List<Tile>();
        private readonly List<Tile> pathBuffer = new List<Tile>();
        private BattlePhase currentPhase = BattlePhase.PlayerTurn;
        private bool isEnemyActing;
        private bool pendingEnemyTurn;
        private bool battleEnded;
        private bool combatPreviewFollowsMouse;
        private string battleResult = string.Empty;
        private int currentRound = 1;

        // Turn fade animation
        private float turnFadeAlpha = 1f;
        private bool turnFadingOut;
        private bool turnFadingIn;
        private readonly float turnFadeDuration = 0.25f;

        public BattlePhase CurrentPhase => currentPhase;
        public bool IsBattleEnded => battleEnded;
        public bool IsEnemyActing => isEnemyActing;
        public string BattleResult => battleEnded ? battleResult : string.Empty;
        public bool IsPlayerInputAllowed => !battleEnded && currentPhase == BattlePhase.PlayerTurn && !isEnemyActing && unitSelectionController != null && !unitSelectionController.IsUnitMoving && (systemMenuController == null || !systemMenuController.BlocksGameplayInput);
        public bool IsCombatPreviewVisible => combatPreviewPanel != null && combatPreviewPanel.activeSelf;
        public IReadOnlyList<Unit> ActiveUnits => units;
        public int CurrentRound => scenarioController != null && scenarioController.HasConfiguration ? scenarioController.CurrentRound : currentRound;
        public int CompletedRounds => scenarioController != null && scenarioController.HasConfiguration ? scenarioController.CompletedRounds : Mathf.Max(0, currentRound - 1);

        public void ConfigureScenarioController(BattleScenarioController scenario)
        {
            scenarioController = scenario;
        }

        public void ConfigureRuntime(GridSystem gridSystem, UnitSelectionController unitSelectionController, BattleScenarioController scenario = null)
        {
            this.gridSystem = gridSystem;
            this.unitSelectionController = unitSelectionController;
            scenarioController = scenario;
        }

        public void ConfigureSystemMenu(BattleSystemMenuController systemMenu)
        {
            systemMenuController = systemMenu;
        }

        public void RestoreLoadedBattleState(int round)
        {
            battleEnded = false;
            battleResult = string.Empty;
            currentRound = Mathf.Max(1, round);
            currentPhase = BattlePhase.PlayerTurn;
            isEnemyActing = false;
            pendingEnemyTurn = false;
            RefreshUnits();
            unitSelectionController?.DeselectCurrentUnit();
            UpdateTurnUi();
        }

        public void ForcePhaseForTests(BattlePhase phase)
        {
            currentPhase = phase;
            isEnemyActing = phase == BattlePhase.EnemyTurn;
            UpdateTurnUi();
        }

        public void SetCampaignFlowProcessor(CampaignFlowService processor)
        {
            _campaignFlowProcessor = processor;
        }

        public SceneLoadedReason SceneLoadedReason => _sceneLoadedReason;

        private void Awake()
        {
            EnsureCombatPreviewUi();

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

            HideCombatPreview();
            if (scenarioController == null)
            {
                scenarioController = FindAnyObjectByType<BattleScenarioController>();
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
            scenarioController?.NotifyPlayerUnitCommitted(unit);

            if (CheckBattleEnd())
            {
                return;
            }

            if (AreAllLivingUnitsActed(UnitFaction.Player))
            {
                StartEnemyTurn();
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

        public bool TryEndPlayerTurn()
        {
            if (battleEnded || currentPhase != BattlePhase.PlayerTurn || (unitSelectionController != null && (unitSelectionController.IsUnitMoving || unitSelectionController.HasPendingAction)))
            {
                return false;
            }

            EndPlayerTurn();
            return true;
        }

        public int CountLivingUnits(UnitFaction faction)
        {
            RefreshUnits();
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit != null && unit.IsAlive && unit.Faction == faction)
                {
                    count++;
                }
            }

            return count;
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
            scenarioController?.NotifyEnemyPhaseStarted(CurrentRound);
            RefreshUnits();
            CheckBattleEnd();
            UpdateTurnUi();
            StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator RunEnemyTurn()
        {
            if (scenarioController != null && scenarioController.HasConfiguration && !scenarioController.IsAiEnabled)
            {
                scenarioController.NotifyEnemyPhaseCompleted();
                currentRound = scenarioController.CurrentRound;
                RefreshUnits();
                if (!CheckBattleEnd())
                {
                    BeginPlayerTurn();
                }

                yield break;
            }

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

            if (scenarioController != null && scenarioController.HasConfiguration)
            {
                scenarioController.NotifyEnemyPhaseCompleted();
                currentRound = scenarioController.CurrentRound;
            }
            else
            {
                currentRound++;
            }
            RefreshUnits();
            if (CheckBattleEnd())
            {
                yield break;
            }

            BeginPlayerTurn();
        }

        private IEnumerator MoveEnemyUnit(Unit enemy)
        {
            if (enemy.OccupiedTile == null)
            {
                yield break;
            }

            Unit target = GetAttackTarget(enemy);
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

            target = GetAttackTarget(enemy);
            if (target == null)
            {
                yield break;
            }

            yield return ResolveCombatExchange(enemy, target);
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

                if (!TryGetRangeGapToNearestPlayer(candidate, enemy, out int distanceToPlayer))
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

        private bool TryGetRangeGapToNearestPlayer(Tile fromTile, Unit movingEnemy, out int bestDistance)
        {
            bestDistance = int.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                Unit player = units[i];
                if (player == null || !player.IsAlive || player.Faction != UnitFaction.Player || player.OccupiedTile == null)
                {
                    continue;
                }

                int distance = GridPathfinder.GetManhattanDistance(fromTile.Coordinate, player.CurrentCoordinate);
                int rangeGap = GetAttackRangeGap(distance, movingEnemy);
                if (rangeGap < bestDistance)
                {
                    bestDistance = rangeGap;
                }
            }

            return bestDistance < int.MaxValue;
        }

        private static int GetAttackRangeGap(int distance, Unit unit)
        {
            if (distance < unit.MinimumAttackRange)
            {
                return unit.MinimumAttackRange - distance;
            }

            if (distance > unit.AttackRange)
            {
                return distance - unit.AttackRange;
            }

            return 0;
        }

        private int GetPathCost(Tile start, Tile destination, Unit movingUnit)
        {
            if (!gridSystem.Pathfinder.TryFindPath(start, destination, movingUnit, pathBuffer))
            {
                return int.MaxValue;
            }

            return GetPathCost(pathBuffer, movingUnit);
        }

        private static int GetPathCost(IReadOnlyList<Tile> path, Unit movingUnit)
        {
            int cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                cost += path[i].GetMovementCost(movingUnit);
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

            int candidateDefense = candidate.GetDefenseBonus(null);
            int currentDefense = currentBest.GetDefenseBonus(null);
            if (candidateDefense != currentDefense)
            {
                return candidateDefense > currentDefense;
            }

            if (candidate.Y != currentBest.Y)
            {
                return candidate.Y < currentBest.Y;
            }

            return candidate.X < currentBest.X;
        }

        private Unit GetAttackTarget(Unit attacker)
        {
            Unit bestTarget = null;
            for (int i = 0; i < units.Count; i++)
            {
                Unit candidate = units[i];
                if (!CombatResolver.CanAttack(attacker, candidate))
                {
                    continue;
                }

                if (bestTarget == null || IsBetterAttackTarget(candidate, bestTarget))
                {
                    bestTarget = candidate;
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
            if (battleEnded || !CombatResolver.CanAttack(attacker, defender))
            {
                return 0;
            }

            int damage = CombatResolver.CalculateDamage(attacker, defender);
            defender.ReceiveDamage(damage);
            CheckBattleEnd();
            return damage;
        }

        public IEnumerator ResolveCombatExchange(Unit attacker, Unit defender)
        {
            if (battleEnded || !CombatResolver.CanAttack(attacker, defender))
            {
                yield break;
            }

            bool counterWasAvailable = CombatResolver.CanCounterAttack(defender, attacker);
            yield return PlaySingleAttack(attacker, defender);

            if (battleEnded || !counterWasAvailable || !attacker.IsAlive || !defender.IsAlive || !CombatResolver.CanAttack(defender, attacker))
            {
                yield break;
            }

            yield return PlaySingleAttack(defender, attacker);
        }

        public void ShowCombatPreview(CombatPreview preview)
        {
            EnsureCombatPreviewUi();
            if (combatPreviewPanel == null || combatPreviewText == null)
            {
                return;
            }

            combatPreviewFollowsMouse = true;
            combatPreviewPanel.SetActive(true);
            combatPreviewText.text = FormatCombatPreview(preview);
            PositionCombatPreviewAtMouse();
        }

        public void ShowSkillPreview(string previewText)
        {
            EnsureCombatPreviewUi();
            if (combatPreviewPanel == null || combatPreviewText == null)
            {
                return;
            }

            combatPreviewFollowsMouse = true;
            combatPreviewPanel.SetActive(true);
            combatPreviewText.text = previewText;
            PositionCombatPreviewAtMouse();
        }

        public void HideCombatPreview()
        {
            combatPreviewFollowsMouse = false;

            if (combatPreviewPanel != null)
            {
                combatPreviewPanel.SetActive(false);
            }
        }

        public void UpdateCombatPreviewPosition()
        {
            if (combatPreviewFollowsMouse && combatPreviewPanel != null && combatPreviewPanel.activeSelf)
            {
                PositionCombatPreviewAtMouse();
            }
        }

        private IEnumerator PlaySingleAttack(Unit attacker, Unit defender)
        {
            bool completed = false;
            attacker.PlayAttack(defender, () =>
            {
                ResolveAttack(attacker, defender);
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }
        }

        private string FormatCombatPreview(CombatPreview preview)
        {
            StringBuilder builder = new StringBuilder(128);
            string defenderTerrain = preview.Defender != null && preview.Defender.OccupiedTile != null ? preview.Defender.OccupiedTile.TerrainName : "None";
            string attackerTerrain = preview.Attacker != null && preview.Attacker.OccupiedTile != null ? preview.Attacker.OccupiedTile.TerrainName : "None";

            builder.AppendLine($"{preview.Attacker.DisplayName} HP {preview.Attacker.CurrentHealth}/{preview.Attacker.MaxHealth}");
            builder.AppendLine($"Attack: {preview.AttackerDamage} dmg");
            builder.AppendLine($"Vs {preview.Defender.DisplayName} HP {preview.Defender.CurrentHealth}/{preview.Defender.MaxHealth}");
            builder.AppendLine($"Def Terrain: {defenderTerrain} +{preview.DefenderTerrainDefenseBonus} Def ({preview.DefenderEffectiveDefense})");
            builder.Append("Counter: ");
            builder.Append(preview.CanCounter ? preview.CounterDamage.ToString() : "None");
            if (preview.CanCounter)
            {
                builder.Append($" | {attackerTerrain} +{preview.AttackerTerrainDefenseBonus} Def ({preview.AttackerEffectiveDefense})");
            }
            return builder.ToString();
        }

        private void PositionCombatPreviewAtMouse()
        {
            RectTransform panelRect = combatPreviewPanel != null ? combatPreviewPanel.GetComponent<RectTransform>() : null;
            if (panelRect == null)
            {
                return;
            }

            Vector2 size = panelRect.sizeDelta;
            Vector2 position = GetPointerPosition() + new Vector2(18f, -18f);
            position.x = Mathf.Clamp(position.x, 8f, Screen.width - size.x - 8f);
            position.y = Mathf.Clamp(position.y, size.y + 8f, Screen.height - 8f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.position = position;
        }

        private Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#endif
        }

        private void EnsureCombatPreviewUi()
        {
            if (combatPreviewPanel != null && combatPreviewText != null)
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (combatPreviewPanel == null)
            {
                combatPreviewPanel = new GameObject("Combat Preview Panel", typeof(RectTransform), typeof(Image));
                combatPreviewPanel.transform.SetParent(canvas.transform, false);
                Image image = combatPreviewPanel.GetComponent<Image>();
                image.color = new Color(0.05f, 0.05f, 0.05f, 0.86f);

                RectTransform panelRect = combatPreviewPanel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(1f, 0f);
                panelRect.anchorMax = new Vector2(1f, 0f);
                panelRect.pivot = new Vector2(1f, 0f);
                panelRect.anchoredPosition = new Vector2(-20f, 20f);
                panelRect.sizeDelta = new Vector2(360f, 130f);
            }

            if (combatPreviewText == null)
            {
                combatPreviewText = CreatePreviewText("Combat Preview Text", combatPreviewPanel.transform, new Vector2(12f, -12f), new Vector2(336f, 106f));
            }
        }

        private Text CreatePreviewText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            return text;
        }

        private Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void RefreshUnits()
        {
            units.Clear();
            units.AddRange(FindObjectsByType<Unit>(FindObjectsInactive.Exclude));
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

        private bool AreAllLivingUnitsActed(UnitFaction faction)
        {
            RefreshUnits();
            bool hasLivingUnit = false;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Faction != faction)
                {
                    continue;
                }

                hasLivingUnit = true;
                if (!unit.HasActed)
                {
                    return false;
                }
            }

            return hasLivingUnit;
        }

        private bool CheckBattleEnd()
        {
            if (battleEnded)
            {
                return true;
            }

            RefreshUnits();
            if (scenarioController != null && scenarioController.HasConfiguration && scenarioController.TryEvaluateOutcome(units, out string scenarioResult))
            {
                EndBattle(scenarioResult);
                return true;
            }

            if (scenarioController != null && scenarioController.HasConfiguration)
            {
                return false;
            }

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
            battleResult = message;
            isEnemyActing = false;
            pendingEnemyTurn = false;
            scenarioController?.NotifyBattleEnded();
            unitSelectionController?.ClearBattleUiAndSelection();

            if (resultLabel != null)
            {
                resultLabel.text = message;
                resultLabel.gameObject.SetActive(true);
            }

            UpdateTurnUi();

            BattleEndScreenController endScreen = GetComponent<BattleEndScreenController>();
            if (endScreen != null)
            {
                endScreen.Show(message, scenarioController?.State, scenarioController?.Configuration);
            }
            else
            {
                BattleEndScreenController controller = gameObject.AddComponent<BattleEndScreenController>();
                controller.Show(message, scenarioController?.State, scenarioController?.Configuration);
            }
        }

        public void UpdateTurnControls()
        {
            UpdateTurnUi();
        }

        public bool CheckBattleEndAfterSkill()
        {
            return CheckBattleEnd();
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
                waitButton.gameObject.SetActive(false);
                waitButton.interactable = false;
            }

            // Update HUD elements with fade animation
            string turnLabel2 = currentPhase == BattlePhase.PlayerTurn ? "Player Turn" : "Enemy Turn";
            EnsureHUDElements();
            if (hudTurnText != null)
                hudTurnText.text = turnLabel2;
            if (hudRoundText != null)
                hudRoundText.text = $"Round {CurrentRound}";
            if (hudObjectiveText != null)
                UpdateObjectiveText();
        }

        private void UpdateObjectiveText()
        {
            if (hudObjectiveText == null)
                return;

            if (scenarioController != null && scenarioController.HasConfiguration && scenarioController.Configuration != null)
            {
                BattleSetupConfiguration config = scenarioController.Configuration;
                if (config.RequireEliminateAllEnemies)
                {
                    hudObjectiveText.text = "Objective: Defeat all enemies";
                    return;
                }

                if (config.Objectives.Count > 0)
                {
                    BattleObjectiveSetup obj = config.Objectives[0];
                    if (obj != null)
                    {
                        switch (obj.Type)
                        {
                            case BattleObjectiveType.ReachArea:
                                hudObjectiveText.text = $"Objective: {obj.UnitRole} must reach destination";
                                break;
                            case BattleObjectiveType.SurviveRounds:
                                hudObjectiveText.text = $"Objective: Survive {obj.RequiredRounds} rounds";
                                break;
                            case BattleObjectiveType.ProtectUnit:
                                hudObjectiveText.text = $"Objective: Protect {obj.UnitRole}";
                                break;
                        }
                        return;
                    }
                }
            }

            hudObjectiveText.text = "Objective: Defeat all enemies";
        }

        private void EnsureHUDElements()
        {
            if (hudTurnPanel != null && hudTurnText != null && hudRoundText != null && hudObjectivePanel != null && hudObjectiveText != null)
                return;

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Turn/round panel - top center
            if (hudTurnPanel == null)
            {
                hudTurnPanel = new GameObject("HUD Turn Panel", typeof(RectTransform), typeof(Image));
                hudTurnPanel.transform.SetParent(canvas.transform, false);
                hudTurnPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.8f);

                RectTransform panelRect = hudTurnPanel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -16f);
                panelRect.sizeDelta = new Vector2(220f, 44f);

                // Turn text
                GameObject turnObj = new GameObject("Turn Text", typeof(RectTransform), typeof(Text));
                turnObj.transform.SetParent(panelRect, false);
                RectTransform turnRect = turnObj.GetComponent<RectTransform>();
                turnRect.anchorMin = new Vector2(0f, 0f);
                turnRect.anchorMax = new Vector2(1f, 0f);
                turnRect.pivot = new Vector2(0.5f, 0.5f);
                turnRect.anchoredPosition = new Vector2(0f, 6f);
                turnRect.sizeDelta = new Vector2(-8f, 20f);
                hudTurnText = turnObj.GetComponent<Text>();
                hudTurnText.font = GetDefaultFont();
                hudTurnText.fontSize = 18;
                hudTurnText.color = Color.white;
                hudTurnText.alignment = TextAnchor.MiddleCenter;

                // Round text
                GameObject roundObj = new GameObject("Round Text", typeof(RectTransform), typeof(Text));
                roundObj.transform.SetParent(panelRect, false);
                RectTransform roundRect = roundObj.GetComponent<RectTransform>();
                roundRect.anchorMin = new Vector2(0f, 0f);
                roundRect.anchorMax = new Vector2(1f, 0f);
                roundRect.pivot = new Vector2(0.5f, 0.5f);
                roundRect.anchoredPosition = new Vector2(0f, -8f);
                roundRect.sizeDelta = new Vector2(-8f, 16f);
                hudRoundText = roundObj.GetComponent<Text>();
                hudRoundText.font = GetDefaultFont();
                hudRoundText.fontSize = 13;
                hudRoundText.color = new Color(0.7f, 0.7f, 0.8f);
                hudRoundText.alignment = TextAnchor.MiddleCenter;
            }

            // Objective panel - top right
            if (hudObjectivePanel == null)
            {
                hudObjectivePanel = new GameObject("HUD Objective Panel", typeof(RectTransform), typeof(Image));
                hudObjectivePanel.transform.SetParent(canvas.transform, false);
                hudObjectivePanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.7f);

                RectTransform objRect = hudObjectivePanel.GetComponent<RectTransform>();
                objRect.anchorMin = new Vector2(1f, 1f);
                objRect.anchorMax = new Vector2(1f, 1f);
                objRect.pivot = new Vector2(1f, 1f);
                objRect.anchoredPosition = new Vector2(-18f, -18f);
                objRect.sizeDelta = new Vector2(260f, 48f);

                GameObject objTextObj = new GameObject("Objective Text", typeof(RectTransform), typeof(Text));
                objTextObj.transform.SetParent(objRect, false);
                RectTransform objTextRect = objTextObj.GetComponent<RectTransform>();
                objTextRect.anchorMin = Vector2.zero;
                objTextRect.anchorMax = Vector2.one;
                objTextRect.offsetMin = new Vector2(10f, 6f);
                objTextRect.offsetMax = new Vector2(-10f, -6f);
                hudObjectiveText = objTextObj.GetComponent<Text>();
                hudObjectiveText.font = GetDefaultFont();
                hudObjectiveText.fontSize = 13;
                hudObjectiveText.color = new Color(0.8f, 0.85f, 0.9f);
                hudObjectiveText.alignment = TextAnchor.UpperLeft;
                hudObjectiveText.supportRichText = false;
            }
        }

        private void Update()
        {
            UpdateTurnFade();
        }

        private void UpdateTurnFade()
        {
            if (turnFadingOut)
            {
                turnFadeAlpha -= Time.deltaTime / turnFadeDuration;
                if (turnFadeAlpha <= 0f)
                {
                    turnFadeAlpha = 0f;
                    turnFadingOut = false;
                    UpdateTurnUi();
                    turnFadingIn = true;
                }
            }
            else if (turnFadingIn)
            {
                turnFadeAlpha += Time.deltaTime / turnFadeDuration;
                if (turnFadeAlpha >= 1f)
                {
                    turnFadeAlpha = 1f;
                    turnFadingIn = false;
                }
            }

            if (hudTurnPanel != null && (turnFadingOut || turnFadingIn))
            {
                ApplyPanelAlpha(hudTurnPanel, turnFadeAlpha);
            }
        }

        private static void ApplyPanelAlpha(GameObject panel, float alpha)
        {
            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                Color c = image.color;
                c.a = Mathf.Clamp01(alpha) * 0.8f;
                image.color = c;
            }
        }
    }
}
