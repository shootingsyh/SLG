using System.Collections.Generic;
using System.Text;
using SLG.Core;
using SLG.Grid;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.Scenarios
{
    public sealed class BattleScenarioController : MonoBehaviour
    {
        [SerializeField] private Text objectiveText;
        [SerializeField] private bool enableDevelopmentControls;

        private readonly BattleScenarioRuntimeState state = new BattleScenarioRuntimeState();
        private readonly List<Unit> unitsBuffer = new List<Unit>();
        private BattleSetupConfiguration config;
        private GridSystem grid;
        private UnitSelectionController player;
        private BattleTurnController turns;
        private int spawnedUnitCounter;
        private bool battleEnded;

        public BattleSetupConfiguration Configuration => config;
        public BattleScenarioRuntimeState State => state;
        public int CurrentRound => state.CurrentRound;
        public int CompletedRounds => state.CompletedRounds;
        public bool HasConfiguration => config != null;
        public bool IsAiEnabled => config == null || config.AiEnabled;
        public string LastDiagnostic { get; private set; } = string.Empty;
        public string ObjectiveSummary => BuildObjectiveSummary();

        public void Configure(BattleSetupConfiguration configuration, GridSystem gridSystem, UnitSelectionController playerController, BattleTurnController turnController)
        {
            config = configuration != null ? configuration.Clone() : null;
            grid = gridSystem;
            player = playerController;
            turns = turnController;
            battleEnded = false;
            spawnedUnitCounter = 0;
            state.Initialize(config);
            RefreshRoleLookup();
            UpdateObjectiveUi();
        }

        public void RegisterUnit(BattleUnitRole role, Unit unit)
        {
            if (role != BattleUnitRole.None && unit != null)
            {
                state.UnitsByRole[role] = unit;
            }
        }

        public void NotifyPlayerUnitCommitted(Unit unit)
        {
            BattleObjectiveEvaluator.TryCompleteReachObjective(config, state, unit);
            UpdateObjectiveUi();
        }

        public void NotifyEnemyPhaseStarted(int round)
        {
            if (battleEnded || config == null)
            {
                return;
            }

            state.CurrentRound = round;
            SpawnDueReinforcements(round);
            UpdateObjectiveUi();
        }

        public void NotifyEnemyPhaseCompleted()
        {
            if (battleEnded || config == null)
            {
                return;
            }

            state.CompletedRounds++;
            state.CurrentRound++;
            UpdateObjectiveUi();
        }

        public bool TryEvaluateOutcome(IReadOnlyList<Unit> activeUnits, out string result)
        {
            result = string.Empty;
            if (config == null || battleEnded)
            {
                return false;
            }

            BattleScenarioOutcome outcome = BattleObjectiveEvaluator.Evaluate(config, state, activeUnits);
            if (outcome == BattleScenarioOutcome.None)
            {
                UpdateObjectiveUi();
                return false;
            }

            battleEnded = true;
            result = outcome == BattleScenarioOutcome.Defeat ? "Defeat" : "Victory";
            UpdateObjectiveUi();
            return true;
        }

        public void NotifyBattleEnded()
        {
            battleEnded = true;
        }

        public void RestoreRuntimeState(int currentRound, int completedRounds, IReadOnlyList<int> completedObjectiveIndices, IReadOnlyList<SLG.Saves.ReinforcementRuntimeSaveData> savedReinforcements)
        {
            if (config == null)
            {
                return;
            }

            battleEnded = false;
            state.CurrentRound = Mathf.Max(1, currentRound);
            state.CompletedRounds = Mathf.Max(0, completedRounds);
            state.CompletedObjectives.Clear();
            if (completedObjectiveIndices != null)
            {
                for (int i = 0; i < completedObjectiveIndices.Count; i++)
                {
                    int index = completedObjectiveIndices[i];
                    if (index >= 0 && index < config.Objectives.Count)
                    {
                        state.CompletedObjectives.Add(config.Objectives[index]);
                    }
                }
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                state.ReinforcementStates[wave] = ReinforcementWaveState.Pending;
                if (savedReinforcements == null)
                {
                    continue;
                }

                for (int savedIndex = 0; savedIndex < savedReinforcements.Count; savedIndex++)
                {
                    if (savedReinforcements[savedIndex].WaveId == wave.Key)
                    {
                        state.ReinforcementStates[wave] = savedReinforcements[savedIndex].State;
                        break;
                    }
                }
            }

            UpdateObjectiveUi();
        }

        public int CountPendingRequiredReinforcements()
        {
            int count = 0;
            if (config == null)
            {
                return count;
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                if (wave != null && wave.RequiredForEliminateAllEnemies && state.ReinforcementStates.TryGetValue(wave, out ReinforcementWaveState waveState) && waveState == ReinforcementWaveState.Pending)
                {
                    count++;
                }
            }

            return count;
        }

        private void SpawnDueReinforcements(int round)
        {
            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                if (wave == null || wave.ArrivalRound != round || wave.ArrivalPhase != BattlePhase.EnemyTurn)
                {
                    continue;
                }

                if (!state.ReinforcementStates.TryGetValue(wave, out ReinforcementWaveState waveState) || waveState != ReinforcementWaveState.Pending)
                {
                    continue;
                }

                bool spawnedAll = true;
                for (int spawnIndex = 0; spawnIndex < wave.Count; spawnIndex++)
                {
                    if (!TrySpawnReinforcement(wave, spawnIndex))
                    {
                        spawnedAll = false;
                    }
                }

                state.ReinforcementStates[wave] = spawnedAll ? ReinforcementWaveState.Spawned : ReinforcementWaveState.Failed;
            }
        }

        private bool TrySpawnReinforcement(BattleReinforcementWaveSetup wave, int spawnIndex)
        {
            if (grid == null || player == null || wave.UnitDefinition == null || turns != null && turns.IsBattleEnded)
            {
                LastDiagnostic = $"Reinforcement '{wave?.Key}' cannot spawn because required setup is missing or battle ended.";
                Debug.LogWarning(LastDiagnostic, this);
                return false;
            }

            GameObject unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitObject.name = $"{wave.Key}_{spawnIndex}_{++spawnedUnitCounter}";
            Unit unit = unitObject.AddComponent<Unit>();
            unit.ConfigureRuntime(wave.UnitDefinition, wave.Faction, wave.SpawnCoordinate, wave.UnitDefinition.MaxHealth, 100f);

            if (!ReinforcementPlacement.TryFindSpawnTile(grid, unit, wave.SpawnCoordinate, config.FallbackRadius, out Tile tile))
            {
                LastDiagnostic = $"Reinforcement '{wave.Key}' failed to spawn within radius {config.FallbackRadius} of {wave.SpawnCoordinate}.";
                Debug.LogWarning(LastDiagnostic, this);
                Destroy(unitObject);
                return false;
            }

            unit.PlaceOnTile(tile);
            tile.SetOccupyingUnit(unit);
            unit.Initialize(player, tile.Coordinate, tile);
            RegisterUnit(wave.Role, unit);
            LastDiagnostic = $"Reinforcement '{wave.Key}' spawned at {tile.Coordinate}.";
            return true;
        }

        private void RefreshRoleLookup()
        {
            state.UnitsByRole.Clear();
            if (config == null)
            {
                return;
            }

            unitsBuffer.Clear();
            unitsBuffer.AddRange(FindObjectsByType<Unit>(FindObjectsInactive.Exclude));
            for (int setupIndex = 0; setupIndex < config.Units.Count; setupIndex++)
            {
                BattleUnitSetup setup = config.Units[setupIndex];
                if (setup == null || setup.Role == BattleUnitRole.None)
                {
                    continue;
                }

                for (int unitIndex = 0; unitIndex < unitsBuffer.Count; unitIndex++)
                {
                    Unit unit = unitsBuffer[unitIndex];
                    if (unit != null && unit.name == setup.Key)
                    {
                        state.UnitsByRole[setup.Role] = unit;
                        break;
                    }
                }
            }
        }

        private void UpdateObjectiveUi()
        {
            if (objectiveText != null)
            {
                objectiveText.text = BuildObjectiveSummary();
            }
        }

        private string BuildObjectiveSummary()
        {
            if (config == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(256);
            builder.AppendLine($"Round {state.CurrentRound}");
            builder.AppendLine("Objectives");
            if (config.RequireEliminateAllEnemies)
            {
                builder.AppendLine($"{Check(BattleObjectiveEvaluator.IsEliminateComplete(config, state, ActiveUnits()))} Defeat all enemies");
            }

            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup objective = config.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                switch (objective.Type)
                {
                    case BattleObjectiveType.ReachArea:
                        builder.AppendLine($"{Check(BattleObjectiveEvaluator.IsObjectiveComplete(objective, state))} Reach destination with {objective.UnitRole}");
                        break;
                    case BattleObjectiveType.SurviveRounds:
                        builder.AppendLine($"{Check(BattleObjectiveEvaluator.IsObjectiveComplete(objective, state))} Survive {state.CompletedRounds}/{objective.RequiredRounds} rounds");
                        break;
                    case BattleObjectiveType.ProtectUnit:
                        builder.AppendLine($"{Check(!BattleObjectiveEvaluator.IsProtectedUnitDefeated(config, state))} Protect {objective.UnitRole}{FormatProtectedHp(objective.UnitRole)}");
                        break;
                }
            }

            builder.AppendLine($"Enemies remaining: {BattleObjectiveEvaluator.CountLiving(ActiveUnits(), UnitFaction.Enemy)}");
            int nextRound = NextPendingReinforcementRound();
            if (nextRound > 0)
            {
                builder.AppendLine($"Next reinforcements: Round {nextRound}");
            }

            return builder.ToString();
        }

        private IReadOnlyList<Unit> ActiveUnits()
        {
            if (turns != null)
            {
                return turns.ActiveUnits;
            }

            unitsBuffer.Clear();
            unitsBuffer.AddRange(FindObjectsByType<Unit>(FindObjectsInactive.Exclude));
            return unitsBuffer;
        }

        private string FormatProtectedHp(BattleUnitRole role)
        {
            if (state.UnitsByRole.TryGetValue(role, out Unit unit) && unit != null)
            {
                return $" HP {unit.CurrentHealth}/{unit.MaxHealth}";
            }

            return string.Empty;
        }

        private int NextPendingReinforcementRound()
        {
            int result = int.MaxValue;
            for (int i = 0; config != null && i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                if (wave != null && state.ReinforcementStates.TryGetValue(wave, out ReinforcementWaveState waveState) && waveState == ReinforcementWaveState.Pending && wave.ArrivalRound >= state.CurrentRound)
                {
                    result = Mathf.Min(result, wave.ArrivalRound);
                }
            }

            return result == int.MaxValue ? 0 : result;
        }

        private static string Check(bool complete)
        {
            return complete ? "[x]" : "[ ]";
        }
    }
}
