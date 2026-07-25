using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using SLG.Units;

namespace SLG.Scenarios
{
    public enum BattleScenarioOutcome
    {
        None,
        Victory,
        Defeat
    }

    public enum ReinforcementWaveState
    {
        Pending,
        Spawned,
        Failed
    }

    public sealed class BattleScenarioRuntimeState
    {
        public int CurrentRound = 1;
        public int CompletedRounds;
        public readonly HashSet<BattleObjectiveSetup> CompletedObjectives = new HashSet<BattleObjectiveSetup>();
        public readonly Dictionary<BattleReinforcementWaveSetup, ReinforcementWaveState> ReinforcementStates = new Dictionary<BattleReinforcementWaveSetup, ReinforcementWaveState>();
        public readonly Dictionary<BattleUnitRole, Unit> UnitsByRole = new Dictionary<BattleUnitRole, Unit>();

        public void Initialize(BattleSetupConfiguration config)
        {
            CurrentRound = 1;
            CompletedRounds = 0;
            CompletedObjectives.Clear();
            ReinforcementStates.Clear();
            UnitsByRole.Clear();
            if (config == null)
            {
                return;
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                ReinforcementStates[config.Reinforcements[i]] = ReinforcementWaveState.Pending;
            }
        }
    }

    public static class BattleObjectiveEvaluator
    {
        public static BattleScenarioOutcome Evaluate(BattleSetupConfiguration config, BattleScenarioRuntimeState state, IReadOnlyList<Unit> units)
        {
            if (config == null || state == null)
            {
                return BattleScenarioOutcome.None;
            }

            if (IsDefaultDefeat(units) || IsProtectedUnitDefeated(config, state))
            {
                return BattleScenarioOutcome.Defeat;
            }

            bool allComplete = true;
            if (config.RequireEliminateAllEnemies && !IsEliminateComplete(config, state, units))
            {
                allComplete = false;
            }

            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup objective = config.Objectives[i];
                if (objective == null || objective.Type == BattleObjectiveType.ProtectUnit || objective.Type == BattleObjectiveType.EliminateAllEnemies)
                {
                    continue;
                }

                if (!IsObjectiveComplete(objective, state))
                {
                    allComplete = false;
                }
            }

            return allComplete ? BattleScenarioOutcome.Victory : BattleScenarioOutcome.None;
        }

        public static bool IsDefaultDefeat(IReadOnlyList<Unit> units)
        {
            return CountLiving(units, UnitFaction.Player) == 0;
        }

        public static bool IsEliminateComplete(BattleSetupConfiguration config, BattleScenarioRuntimeState state, IReadOnlyList<Unit> units)
        {
            if (CountLiving(units, UnitFaction.Enemy) > 0)
            {
                return false;
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                if (wave == null || !wave.RequiredForEliminateAllEnemies)
                {
                    continue;
                }

                if (!state.ReinforcementStates.TryGetValue(wave, out ReinforcementWaveState waveState) || waveState != ReinforcementWaveState.Spawned)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsObjectiveComplete(BattleObjectiveSetup objective, BattleScenarioRuntimeState state)
        {
            if (objective == null)
            {
                return false;
            }

            if (objective.Type == BattleObjectiveType.SurviveRounds)
            {
                return state.CompletedRounds >= objective.RequiredRounds;
            }

            return state.CompletedObjectives.Contains(objective);
        }

        public static bool IsProtectedUnitDefeated(BattleSetupConfiguration config, BattleScenarioRuntimeState state)
        {
            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup objective = config.Objectives[i];
                if (objective == null || objective.Type != BattleObjectiveType.ProtectUnit)
                {
                    continue;
                }

                if (!state.UnitsByRole.TryGetValue(objective.UnitRole, out Unit unit) || unit == null || !unit.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryCompleteReachObjective(BattleSetupConfiguration config, BattleScenarioRuntimeState state, Unit unit)
        {
            if (config == null || state == null || unit == null || !unit.IsAlive || unit.OccupiedTile == null)
            {
                return false;
            }

            bool completedAny = false;
            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup objective = config.Objectives[i];
                if (objective == null || objective.Type != BattleObjectiveType.ReachArea || state.CompletedObjectives.Contains(objective))
                {
                    continue;
                }

                if (objective.DesignatedUnitRequired && (!state.UnitsByRole.TryGetValue(objective.UnitRole, out Unit designated) || designated != unit))
                {
                    continue;
                }

                if (Contains(objective.DestinationCoordinates, unit.OccupiedTile.Coordinate))
                {
                    state.CompletedObjectives.Add(objective);
                    completedAny = true;
                }
            }

            return completedAny;
        }

        public static int CountLiving(IReadOnlyList<Unit> units, UnitFaction faction)
        {
            int count = 0;
            if (units == null)
            {
                return count;
            }

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

        private static bool Contains(IReadOnlyList<GridCoordinate> coordinates, GridCoordinate coordinate)
        {
            if (coordinates == null)
            {
                return false;
            }

            for (int i = 0; i < coordinates.Count; i++)
            {
                if (coordinates[i].Equals(coordinate))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
