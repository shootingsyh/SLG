using System.Collections.Generic;
using SLG.Core;
using SLG.Units;

namespace SLG.Scenarios
{
    public static class BattleSetupValidator
    {
        public static bool Validate(BattleSetupConfiguration config, List<string> errors)
        {
            errors?.Clear();
            if (config == null)
            {
                Add(errors, "Configuration is missing.");
                return false;
            }

            if (config.TerrainRows == null || config.TerrainRows.Length == 0 || config.Width <= 0 || config.Height <= 0)
            {
                Add(errors, "Missing map preset or terrain layout.");
            }

            if (config.PlayerFormation == BattleFormationPreset.None)
            {
                Add(errors, "Missing Player formation.");
            }

            int playerCount = 0;
            bool hasEnemy = false;
            bool hasVictoryObjective = config.RequireEliminateAllEnemies;
            HashSet<GridCoordinate> occupied = new HashSet<GridCoordinate>();

            for (int i = 0; i < config.Units.Count; i++)
            {
                BattleUnitSetup unit = config.Units[i];
                if (unit == null)
                {
                    Add(errors, "Unit setup is missing.");
                    continue;
                }

                if (unit.Faction == UnitFaction.Player)
                {
                    playerCount++;
                }

                if (unit.Faction == UnitFaction.Enemy)
                {
                    hasEnemy = true;
                }

                if (!IsInside(config, unit.Coordinate))
                {
                    Add(errors, $"Unit '{unit.Key}' starts outside the grid at {unit.Coordinate}.");
                }
                else if (!occupied.Add(unit.Coordinate))
                {
                    Add(errors, $"Duplicate starting coordinate {unit.Coordinate}.");
                }

                if (unit.Definition == null)
                {
                    Add(errors, $"Unit '{unit.Key}' is missing a UnitDefinition.");
                }
                else if (!CanEnterTerrain(config, unit.Coordinate, unit.Definition.MovementProfile))
                {
                    Add(errors, $"Unit '{unit.Key}' starts on invalid terrain at {unit.Coordinate}.");
                }
            }

            if (playerCount == 0)
            {
                Add(errors, "At least one Player unit is required.");
            }

            for (int i = 0; i < config.Objectives.Count; i++)
            {
                BattleObjectiveSetup objective = config.Objectives[i];
                if (objective == null)
                {
                    Add(errors, "Objective setup is missing.");
                    continue;
                }

                if (objective.Type != BattleObjectiveType.ProtectUnit)
                {
                    hasVictoryObjective = true;
                }

                switch (objective.Type)
                {
                    case BattleObjectiveType.ReachArea:
                        if (objective.DestinationCoordinates == null || objective.DestinationCoordinates.Count == 0 || objective.DestinationZone == BattleDestinationZonePreset.None)
                        {
                            Add(errors, "Reach objective requires a destination area.");
                        }

                        if (objective.DesignatedUnitRequired && objective.UnitRole == BattleUnitRole.None)
                        {
                            Add(errors, "Reach objective requires a designated unit role.");
                        }
                        break;
                    case BattleObjectiveType.SurviveRounds:
                        if (objective.RequiredRounds <= 0)
                        {
                            Add(errors, "Survive objective requires a positive round count.");
                        }
                        break;
                    case BattleObjectiveType.ProtectUnit:
                        if (objective.UnitRole == BattleUnitRole.None || !HasPlayerRole(config, objective.UnitRole))
                        {
                            Add(errors, "Protect objective requires a valid protected Player unit role.");
                        }
                        break;
                }
            }

            if (!hasVictoryObjective)
            {
                Add(errors, "At least one Victory objective is required.");
            }

            if (config.RequireEliminateAllEnemies && !hasEnemy && config.Reinforcements.Count == 0)
            {
                Add(errors, "EliminateAllEnemies requires enemies or required reinforcements.");
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                if (wave == null)
                {
                    Add(errors, "Reinforcement wave is missing.");
                    continue;
                }

                if (wave.UnitDefinition == null)
                {
                    Add(errors, $"Reinforcement '{wave.Key}' is missing a UnitDefinition.");
                }

                if (wave.Count <= 0)
                {
                    Add(errors, $"Reinforcement '{wave.Key}' requires a positive count.");
                }

                if (wave.ArrivalRound <= 0 || wave.ArrivalPhase != BattlePhase.EnemyTurn)
                {
                    Add(errors, $"Reinforcement '{wave.Key}' must arrive at a positive Enemy phase round.");
                }

                if (!IsInside(config, wave.SpawnCoordinate))
                {
                    Add(errors, $"Reinforcement '{wave.Key}' spawn is outside the grid at {wave.SpawnCoordinate}.");
                }
                else if (wave.UnitDefinition != null && !CanEnterTerrain(config, wave.SpawnCoordinate, wave.UnitDefinition.MovementProfile))
                {
                    Add(errors, $"Reinforcement '{wave.Key}' spawn terrain is invalid at {wave.SpawnCoordinate}.");
                }
            }

            return errors == null || errors.Count == 0;
        }

        public static bool IsInside(BattleSetupConfiguration config, GridCoordinate coordinate)
        {
            return config != null && coordinate.X >= 0 && coordinate.X < config.Width && coordinate.Y >= 0 && coordinate.Y < config.Height;
        }

        public static bool CanEnterTerrain(BattleSetupConfiguration config, GridCoordinate coordinate, MovementProfile profile)
        {
            char terrain = GetTerrainCode(config, coordinate);
            if (terrain == 'X')
            {
                return false;
            }

            if (terrain == 'W' && profile == MovementProfile.Ground)
            {
                return false;
            }

            return true;
        }

        public static char GetTerrainCode(BattleSetupConfiguration config, GridCoordinate coordinate)
        {
            if (config == null || config.TerrainRows == null || config.TerrainRows.Length == 0)
            {
                return 'P';
            }

            int rowIndex = config.Height - 1 - coordinate.Y;
            if (rowIndex < 0 || rowIndex >= config.TerrainRows.Length || string.IsNullOrEmpty(config.TerrainRows[rowIndex]) || coordinate.X >= config.TerrainRows[rowIndex].Length)
            {
                return 'P';
            }

            return char.ToUpperInvariant(config.TerrainRows[rowIndex][coordinate.X]);
        }

        private static bool HasPlayerRole(BattleSetupConfiguration config, BattleUnitRole role)
        {
            for (int i = 0; i < config.Units.Count; i++)
            {
                BattleUnitSetup unit = config.Units[i];
                if (unit != null && unit.Faction == UnitFaction.Player && unit.Role == role)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Add(List<string> errors, string error)
        {
            errors?.Add(error);
        }
    }
}
