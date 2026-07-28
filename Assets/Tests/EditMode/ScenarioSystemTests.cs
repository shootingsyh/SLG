using System.Collections.Generic;
using NUnit.Framework;
using SLG.Core;
using SLG.Grid;
using SLG.Scenarios;
using SLG.Skills;
using SLG.Terrain;
using SLG.Units;
using UnityEngine;

namespace SLG.Tests
{
    public sealed class ScenarioSystemTests
    {
        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }

            objects.Clear();
        }

        [Test]
        public void Validation_AcceptsValidBasicAndMultipleObjectives()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.SurviveRounds, RequiredRounds = 2 });

            Assert.That(BattleSetupValidator.Validate(config, new List<string>()), Is.True);
        }

        [Test]
        public void Validation_RejectsMissingPlayerFormationZeroPlayersAndNoVictoryObjective()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.PlayerFormation = BattleFormationPreset.None;
            config.Units.RemoveAll(u => u.Faction == UnitFaction.Player);
            config.RequireEliminateAllEnemies = false;
            config.Objectives.Clear();

            List<string> errors = new List<string>();
            Assert.That(BattleSetupValidator.Validate(config, errors), Is.False);
            Assert.That(string.Join("|", errors), Does.Contain("Player formation"));
            Assert.That(string.Join("|", errors), Does.Contain("Player unit"));
            Assert.That(string.Join("|", errors), Does.Contain("Victory objective"));
        }

        [Test]
        public void Validation_RejectsInvalidObjectiveParameters()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.RequireEliminateAllEnemies = false;
            config.Objectives.Clear();
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.None });
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ReachArea, UnitRole = BattleUnitRole.None, DesignatedUnitRequired = true });
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.SurviveRounds, RequiredRounds = 0 });

            List<string> errors = new List<string>();
            Assert.That(BattleSetupValidator.Validate(config, errors), Is.False);
            string all = string.Join("|", errors);
            Assert.That(all, Does.Contain("Protect objective"));
            Assert.That(all, Does.Contain("destination area"));
            Assert.That(all, Does.Contain("designated unit"));
            Assert.That(all, Does.Contain("positive round"));
        }

        [Test]
        public void Validation_RejectsInvalidReinforcementDuplicateAndTerrain()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.Units.Add(config.Units[0].Clone());
            config.Units.Add(UnitSetup("water", UnitFaction.Player, BattleUnitRole.Ally, GroundDefinition(), 1, 1));
            config.TerrainRows = new[] { "PPP", "PWP", "PPP" };
            config.Reinforcements.Add(new BattleReinforcementWaveSetup { Key = "bad", ArrivalRound = 2, SpawnCoordinate = new GridCoordinate(5, 5), UnitDefinition = null });

            List<string> errors = new List<string>();
            Assert.That(BattleSetupValidator.Validate(config, errors), Is.False);
            string all = string.Join("|", errors);
            Assert.That(all, Does.Contain("Duplicate"));
            Assert.That(all, Does.Contain("invalid terrain"));
            Assert.That(all, Does.Contain("missing a UnitDefinition"));
            Assert.That(all, Does.Contain("outside the grid"));
        }

        [Test]
        public void Presets_AllValidateAndCloneDeterministically()
        {
            foreach (BattleTestPresetMetadata metadata in BattleTestPresetLibrary.Presets)
            {
                BattleSetupConfiguration a = BattleTestPresetLibrary.Create(metadata.Id);
                BattleSetupConfiguration b = BattleTestPresetLibrary.Create(metadata.Id);
                Assert.That(BattleSetupValidator.Validate(a, new List<string>()), Is.True, metadata.DisplayName);
                Assert.That(BattleSetupValidator.Validate(b, new List<string>()), Is.True, metadata.DisplayName);
                Assert.That(a.ScenarioName, Is.EqualTo(b.ScenarioName));
                Assert.That(a.Reinforcements.Count, Is.EqualTo(metadata.ExpectedReinforcements));
                a.Units[0].Key = "mutated";
                Assert.That(b.Units[0].Key, Is.Not.EqualTo("mutated"));
            }
        }

        [Test]
        public void ObjectiveEvaluation_EliminateRespectsEnemiesPendingWavesAndFailedWaves()
        {
            BattleSetupConfiguration config = ValidConfig();
            BattleScenarioRuntimeState state = State(config);
            Unit enemy = Unit("Enemy", UnitFaction.Enemy, 10, 1, 0);

            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit(), enemy }), Is.False);
            enemy.ReceiveDamage(99);
            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit(), enemy }), Is.True);

            BattleReinforcementWaveSetup wave = new BattleReinforcementWaveSetup { Key = "wave", RequiredForEliminateAllEnemies = true, UnitDefinition = GroundDefinition() };
            config.Reinforcements.Add(wave);
            state = State(config);
            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit() }), Is.False);
            state.ReinforcementStates[wave] = ReinforcementWaveState.Failed;
            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit() }), Is.False);
            state.ReinforcementStates[wave] = ReinforcementWaveState.Spawned;
            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit() }), Is.True);
        }

        [Test]
        public void ObjectiveEvaluation_ReachRequiresDesignatedCommittedUnitInZone()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.RequireEliminateAllEnemies = false;
            config.Objectives.Clear();
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ReachArea, UnitRole = BattleUnitRole.Knight, DestinationZone = BattleDestinationZonePreset.EastExit, DestinationCoordinates = new List<GridCoordinate> { new GridCoordinate(2, 0) } });
            BattleScenarioRuntimeState state = State(config);
            Unit knight = Unit("Knight", UnitFaction.Player, 10, 2, 0);
            Unit mage = Unit("Mage", UnitFaction.Player, 10, 2, 0);
            state.UnitsByRole[BattleUnitRole.Knight] = knight;

            BattleObjectiveEvaluator.TryCompleteReachObjective(config, state, mage);
            Assert.That(state.CompletedObjectives.Count, Is.EqualTo(0));

            BattleObjectiveEvaluator.TryCompleteReachObjective(config, state, knight);
            Assert.That(state.CompletedObjectives.Count, Is.EqualTo(1));
        }

        [Test]
        public void ObjectiveEvaluation_SurviveProtectDefaultDefeatCompositionAndPrecedence()
        {
            BattleSetupConfiguration config = ValidConfig();
            config.RequireEliminateAllEnemies = false;
            config.Objectives.Clear();
            BattleObjectiveSetup survive = new BattleObjectiveSetup { Type = BattleObjectiveType.SurviveRounds, RequiredRounds = 2 };
            BattleObjectiveSetup protect = new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.Healer };
            config.Objectives.Add(survive);
            config.Objectives.Add(protect);
            BattleScenarioRuntimeState state = State(config);
            Unit player = PlayerUnit();
            Unit healer = Unit("Healer", UnitFaction.Player, 10, 0, 0);
            state.UnitsByRole[BattleUnitRole.Healer] = healer;

            Assert.That(BattleObjectiveEvaluator.Evaluate(config, state, new[] { player, healer }), Is.EqualTo(BattleScenarioOutcome.None));
            state.CompletedRounds = 2;
            Assert.That(BattleObjectiveEvaluator.Evaluate(config, state, new[] { player, healer }), Is.EqualTo(BattleScenarioOutcome.Victory));
            healer.ReceiveDamage(99);
            Assert.That(BattleObjectiveEvaluator.Evaluate(config, state, new[] { player, healer }), Is.EqualTo(BattleScenarioOutcome.Defeat), "Defeat takes precedence over simultaneous Victory.");
            Unit dead = Unit("Dead", UnitFaction.Player, 1, 0, 0);
            dead.ReceiveDamage(99);
            Assert.That(BattleObjectiveEvaluator.IsDefaultDefeat(new[] { dead }), Is.True);
        }

        [Test]
        public void ReinforcementScheduling_StateRulesAreDeterministic()
        {
            BattleSetupConfiguration config = ValidConfig();
            BattleReinforcementWaveSetup waveA = new BattleReinforcementWaveSetup { Key = "A", ArrivalRound = 2, ArrivalPhase = BattlePhase.EnemyTurn, UnitDefinition = GroundDefinition(), SpawnCoordinate = new GridCoordinate(2, 2) };
            BattleReinforcementWaveSetup waveB = new BattleReinforcementWaveSetup { Key = "B", ArrivalRound = 2, ArrivalPhase = BattlePhase.EnemyTurn, UnitDefinition = GroundDefinition(), SpawnCoordinate = new GridCoordinate(1, 2) };
            config.Reinforcements.Add(waveA);
            config.Reinforcements.Add(waveB);
            BattleScenarioRuntimeState state = State(config);

            Assert.That(waveA.ArrivalRound == 2 && waveA.ArrivalPhase == BattlePhase.EnemyTurn, Is.True);
            Assert.That(state.ReinforcementStates[waveA], Is.EqualTo(ReinforcementWaveState.Pending));
            Assert.That(config.Reinforcements[0], Is.SameAs(waveA));
            Assert.That(config.Reinforcements[1], Is.SameAs(waveB));
            state.ReinforcementStates[waveA] = ReinforcementWaveState.Spawned;
            Assert.That(BattleObjectiveEvaluator.IsEliminateComplete(config, state, new[] { PlayerUnit() }), Is.False, "Second required pending wave still blocks EliminateAllEnemies.");
        }

        [Test]
        public void FallbackPlacement_RejectsInvalidCandidatesAndPreservesOccupancyOnFailure()
        {
            GridSystem grid = Grid(new[] { "PXP", "PWP", "PPP" });
            Unit spawning = Unit("Spawn", UnitFaction.Enemy, 10, 1, 0);
            Unit blocker = Unit("Blocker", UnitFaction.Player, 10, 0, 0);
            grid.TryGetTile(new GridCoordinate(1, 1), out Tile intendedWater);
            grid.TryGetTile(new GridCoordinate(0, 1), out Tile expected);
            blocker.PlaceOnTile(expected);
            expected.SetOccupyingUnit(blocker);

            Assert.That(ReinforcementPlacement.TryFindSpawnTile(grid, spawning, intendedWater.Coordinate, 1, out Tile tile), Is.True);
            Assert.That(tile.Coordinate, Is.EqualTo(new GridCoordinate(1, 0)), "Water, occupied, wall, and out-of-grid candidates are rejected with deterministic tie ordering.");
            Assert.That(expected.OccupyingUnit, Is.SameAs(blocker));

            grid.TryGetTile(new GridCoordinate(1, 0), out Tile lastFree);
            Unit blocker2 = Unit("Blocker2", UnitFaction.Player, 10, 0, 0);
            blocker2.PlaceOnTile(lastFree);
            lastFree.SetOccupyingUnit(blocker2);
            grid.TryGetTile(new GridCoordinate(2, 1), out Tile otherFree);
            Unit blocker3 = Unit("Blocker3", UnitFaction.Player, 10, 0, 0);
            blocker3.PlaceOnTile(otherFree);
            otherFree.SetOccupyingUnit(blocker3);
            Assert.That(ReinforcementPlacement.TryFindSpawnTile(grid, spawning, intendedWater.Coordinate, 1, out _), Is.False);
            Assert.That(lastFree.OccupyingUnit, Is.SameAs(blocker2));
        }

        [Test]
        public void RoundTracking_UsesDocumentedBoundaryAndStopsWhenEnded()
        {
            BattleScenarioRuntimeState state = new BattleScenarioRuntimeState();
            state.Initialize(ValidConfig());
            Assert.That(state.CurrentRound, Is.EqualTo(1));
            Assert.That(state.CompletedRounds, Is.EqualTo(0));
            state.CompletedRounds++;
            state.CurrentRound++;
            Assert.That(state.CompletedRounds, Is.EqualTo(1));
            Assert.That(state.CurrentRound, Is.EqualTo(2));
        }

        [Test]
        public void BattleTestLabModel_PresetsResetValidationAndMutationIsolation()
        {
            BattleTestLabModel model = new BattleTestLabModel();
            Assert.That(model.Presets.Count, Is.GreaterThanOrEqualTo(24));
            model.SelectPreset(BattleTestPresetId.ReachAreaKnight);
            Assert.That(model.Validate(new List<string>()), Is.True);
            model.SetSurviveRounds(-1);
            Assert.That(model.Validate(new List<string>()), Is.True, "Irrelevant survive field is ignored for reach preset.");
            model.RuntimeConfiguration.Units.Clear();
            Assert.That(model.Validate(new List<string>()), Is.False);
            model.ResetToPreset();
            Assert.That(model.Validate(new List<string>()), Is.True);
        }

        private BattleSetupConfiguration ValidConfig()
        {
            BattleSetupConfiguration config = new BattleSetupConfiguration { Width = 3, Height = 3, TerrainRows = new[] { "PPP", "PPP", "PPP" }, RequireEliminateAllEnemies = true };
            config.Units.Add(UnitSetup("knight", UnitFaction.Player, BattleUnitRole.Knight, GroundDefinition(), 0, 0));
            config.Units.Add(UnitSetup("enemy", UnitFaction.Enemy, BattleUnitRole.Enemy, GroundDefinition(), 2, 0));
            config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.EliminateAllEnemies });
            return config;
        }

        private BattleScenarioRuntimeState State(BattleSetupConfiguration config)
        {
            BattleScenarioRuntimeState state = new BattleScenarioRuntimeState();
            state.Initialize(config);
            return state;
        }

        private BattleUnitSetup UnitSetup(string key, UnitFaction faction, BattleUnitRole role, UnitDefinition definition, int x, int y)
        {
            return new BattleUnitSetup { Key = key, DisplayName = key, Faction = faction, Role = role, Definition = definition, Coordinate = new GridCoordinate(x, y), CurrentHealth = definition.MaxHealth };
        }

        private UnitDefinition GroundDefinition()
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            objects.Add(definition);
            definition.ConfigureRuntime("Unit", "Unit", 10, 3, 1, 3, MovementProfile.Ground, 1, 1, null, "unit");
            return definition;
        }

        private Unit PlayerUnit()
        {
            return Unit("Player", UnitFaction.Player, 10, 0, 0);
        }

        private Unit Unit(string name, UnitFaction faction, int hp, int x, int y)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            objects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            unit.ConfigureRuntime(GroundDefinition(), faction, new GridCoordinate(x, y), hp, 100f);
            Tile tile = Tile(x, y, Plain());
            unit.PlaceOnTile(tile);
            tile.SetOccupyingUnit(unit);
            return unit;
        }

        private GridSystem Grid(string[] rows)
        {
            GameObject go = new GameObject("Grid");
            objects.Add(go);
            GridSystem grid = go.AddComponent<GridSystem>();
            grid.ConfigureRuntime(rows[0].Length, rows.Length, rows, TilePrefab(), Plain(), Plain(), Water(), Wall(), null);
            grid.RebuildGrid();
            return grid;
        }

        private Tile TilePrefab()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(go);
            return go.AddComponent<Tile>();
        }

        private Tile Tile(int x, int y, TerrainDefinition terrain)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(go);
            Tile tile = go.AddComponent<Tile>();
            tile.Initialize(null, new GridCoordinate(x, y), terrain, Color.white, Color.white, Color.white, Color.white);
            return tile;
        }

        private TerrainDefinition Plain() => Terrain("Plain", "plain", 1, 0, true, true);
        private TerrainDefinition Water() => Terrain("Water", "water", 1, 0, false, true);
        private TerrainDefinition Wall() => Terrain("Wall", "wall", 1, 0, false, false);

        private TerrainDefinition Terrain(string name, string id, int cost, int defense, bool ground, bool flying)
        {
            TerrainDefinition terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            objects.Add(terrain);
            terrain.ConfigureRuntime(name, id, cost, defense, ground, flying);
            return terrain;
        }
    }
}
