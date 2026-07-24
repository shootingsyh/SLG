using System.Collections.Generic;
using NUnit.Framework;
using SLG.Core;
using SLG.Grid;
using SLG.Skills;
using SLG.Terrain;
using SLG.Units;
using UnityEditor;
using UnityEngine;

namespace SLG.Tests
{
    public sealed class ComprehensiveLogicTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ManhattanDistance_IsZeroSymmetricAndExpected()
        {
            GridCoordinate a = new GridCoordinate(1, 2);
            GridCoordinate b = new GridCoordinate(5, 0);

            Assert.That(GridPathfinder.GetManhattanDistance(a, a), Is.EqualTo(0));
            Assert.That(GridPathfinder.GetManhattanDistance(a, b), Is.EqualTo(GridPathfinder.GetManhattanDistance(b, a)));
            Assert.That(GridPathfinder.GetManhattanDistance(a, b), Is.EqualTo(6));
        }

        [Test]
        public void GridLookup_AndNeighbors_RespectBounds()
        {
            GridSystem grid = Grid(3, 2, new[] { "PPP", "PPP" });
            List<Tile> neighbors = new List<Tile>();

            Assert.That(grid.TryGetTile(new GridCoordinate(2, 1), out Tile tile), Is.True);
            Assert.That(tile.Coordinate, Is.EqualTo(new GridCoordinate(2, 1)));
            Assert.That(grid.TryGetTile(new GridCoordinate(3, 1), out _), Is.False);

            grid.TryGetTile(new GridCoordinate(0, 0), out Tile corner);
            grid.FillNeighbors(corner, neighbors);

            Assert.That(neighbors.Count, Is.EqualTo(2));
            Assert.That(Contains(neighbors, 1, 0), Is.True);
            Assert.That(Contains(neighbors, 0, 1), Is.True);
        }

        [Test]
        public void TerrainMovementRules_HandleCostsBlockingAndFlying()
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0, true, true);
            TerrainDefinition forest = Terrain("Forest", 3, 1, true, true);
            TerrainDefinition water = Terrain("Water", 1, 0, false, true);
            Unit ground = Unit("Ground", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 3);
            Unit flyer = Unit("Flyer", MovementProfile.Flying, UnitFaction.Player, 10, 4, 1, 3);

            Assert.That(Tile(0, 0, plain).GetMovementCost(ground), Is.EqualTo(1));
            Assert.That(Tile(0, 0, forest).GetMovementCost(ground), Is.EqualTo(3));
            Assert.That(Tile(0, 0, water).CanEnter(ground), Is.False);
            Assert.That(Tile(0, 0, water).CanEnter(flyer), Is.True);
            Assert.That(Tile(0, 0, water).GetMovementCost(flyer), Is.EqualTo(1));
        }

        [Test]
        public void TileOccupancy_RejectsOtherUnits_AllowsSelf_AndDeadUnitsReleaseTile()
        {
            Tile tile = Tile(0, 0, Terrain("Plain", 1, 0, true, true));
            Unit unit = Unit("Unit", MovementProfile.Ground, UnitFaction.Player, 3, 4, 1, 3);
            Unit blocker = Unit("Blocker", MovementProfile.Ground, UnitFaction.Player, 3, 4, 1, 3);

            unit.PlaceOnTile(tile);
            tile.SetOccupyingUnit(unit);

            Assert.That(tile.CanEnter(unit), Is.True);
            Assert.That(tile.CanEnter(blocker), Is.False);

            unit.ReceiveDamage(99);

            Assert.That(unit.IsAlive, Is.False);
            Assert.That(tile.OccupyingUnit, Is.Null);
            Assert.That(tile.CanEnter(blocker), Is.True);
        }

        [Test]
        public void Reachability_ZeroMovement_ReturnsOnlyStart()
        {
            GridSystem grid = Grid(3, 3, new[] { "PPP", "PPP", "PPP" });
            Unit unit = Unit("Unit", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 0);
            grid.TryGetTile(new GridCoordinate(1, 1), out Tile start);
            unit.PlaceOnTile(start);
            List<Tile> results = new List<Tile>();

            grid.Reachability.FindReachableTiles(start, unit, 0, results);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.SameAs(start));
            Assert.That(start.OccupyingUnit, Is.Null, "Reachability must not mutate occupancy.");
        }

        [Test]
        public void Reachability_TerrainAndOccupancy_ProduceExpectedReachableSet()
        {
            GridSystem grid = Grid(4, 3, new[] { "PPPP", "PFWP", "PXPP" });
            Unit ground = Unit("Ground", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 3);
            Unit blocker = Unit("Blocker", MovementProfile.Ground, UnitFaction.Enemy, 10, 4, 1, 3);
            grid.TryGetTile(new GridCoordinate(0, 1), out Tile start);
            grid.TryGetTile(new GridCoordinate(0, 2), out Tile occupied);
            ground.PlaceOnTile(start);
            blocker.PlaceOnTile(occupied);
            occupied.SetOccupyingUnit(blocker);
            List<Tile> results = new List<Tile>();

            grid.Reachability.FindReachableTiles(start, ground, ground.MovementRange, results);

            Assert.That(Contains(results, 0, 1), Is.True);
            Assert.That(Contains(results, 1, 1), Is.True, "Forest is reachable but consumes extra cost.");
            Assert.That(Contains(results, 2, 1), Is.False, "Ground cannot enter water.");
            Assert.That(Contains(results, 1, 0), Is.False, "Wall is excluded.");
            Assert.That(Contains(results, 0, 2), Is.False, "Occupied destination is excluded.");
            Assert.That(occupied.OccupyingUnit, Is.SameAs(blocker));
        }

        [Test]
        public void Reachability_FlyingAndGround_DifferAcrossWater()
        {
            GridSystem grid = Grid(4, 1, new[] { "PWWP" });
            Unit ground = Unit("Ground", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 3);
            Unit flyer = Unit("Flyer", MovementProfile.Flying, UnitFaction.Player, 10, 4, 1, 3);
            grid.TryGetTile(new GridCoordinate(0, 0), out Tile start);
            List<Tile> results = new List<Tile>();

            grid.Reachability.FindReachableTiles(start, ground, ground.MovementRange, results);
            Assert.That(Contains(results, 3, 0), Is.False);

            grid.Reachability.FindReachableTiles(start, flyer, flyer.MovementRange, results);
            Assert.That(Contains(results, 3, 0), Is.True);
        }

        [Test]
        public void Pathfinder_StartAdjacentUnreachableAndOccupancy_FollowRules()
        {
            GridSystem grid = Grid(3, 1, new[] { "PPP" });
            Unit mover = Unit("Mover", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 3);
            Unit blocker = Unit("Blocker", MovementProfile.Ground, UnitFaction.Enemy, 10, 4, 1, 3);
            grid.TryGetTile(new GridCoordinate(0, 0), out Tile start);
            grid.TryGetTile(new GridCoordinate(1, 0), out Tile middle);
            grid.TryGetTile(new GridCoordinate(2, 0), out Tile end);
            blocker.PlaceOnTile(end);
            end.SetOccupyingUnit(blocker);
            List<Tile> path = new List<Tile>();

            Assert.That(grid.Pathfinder.TryFindPath(start, start, mover, path), Is.True);
            Assert.That(path, Is.EqualTo(new[] { start }));
            Assert.That(grid.Pathfinder.TryFindPath(start, middle, mover, path), Is.True);
            Assert.That(path, Is.EqualTo(new[] { start, middle }));
            Assert.That(grid.Pathfinder.TryFindPath(start, end, mover, path), Is.False);
        }

        [Test]
        public void Pathfinder_ChoosesLowerCostRoute_AroundExpensiveTerrain()
        {
            GridSystem grid = Grid(3, 2, new[] { "PPP", "PFP" });
            Unit mover = Unit("Mover", MovementProfile.Ground, UnitFaction.Player, 10, 4, 1, 4);
            grid.TryGetTile(new GridCoordinate(0, 0), out Tile start);
            grid.TryGetTile(new GridCoordinate(1, 0), out Tile expensiveDirectTile);
            grid.TryGetTile(new GridCoordinate(2, 0), out Tile end);
            expensiveDirectTile.SetMovementCost(5);
            List<Tile> path = new List<Tile>();

            Assert.That(grid.Pathfinder.TryFindPath(start, end, mover, path), Is.True);
            Assert.That(Contains(path, 1, 0), Is.False, "The direct route crosses forest and costs more than the upper route.");
            Assert.That(PathCost(path, mover), Is.EqualTo(4));
        }

        [Test]
        public void CombatDamage_MinimumTerrainPreviewAndDeathClamping_AreConsistent()
        {
            Unit attacker = Unit("Attacker", MovementProfile.Ground, UnitFaction.Player, 10, 5, 0, 3);
            Unit defender = Unit("Defender", MovementProfile.Ground, UnitFaction.Enemy, 3, 1, 4, 3);
            attacker.PlaceOnTile(Tile(0, 0, Terrain("Plain", 1, 0, true, true)));
            defender.PlaceOnTile(Tile(1, 0, Terrain("Fort", 1, 2, true, true)));

            CombatPreview preview = CombatResolver.BuildPreview(attacker, defender);

            Assert.That(CombatResolver.CalculateDamage(attacker, defender), Is.EqualTo(1));
            Assert.That(preview.AttackerDamage, Is.EqualTo(1));
            Assert.That(preview.DefenderTerrainDefenseBonus, Is.EqualTo(2));
            Assert.That(preview.DefenderEffectiveDefense, Is.EqualTo(6));

            defender.ReceiveDamage(99);

            Assert.That(defender.CurrentHealth, Is.EqualTo(0));
            Assert.That(defender.IsAlive, Is.False);
            Assert.That(CombatResolver.CanAttack(attacker, defender), Is.False);
        }

        [Test]
        public void CounterattackRules_RespectRangeLifeAndKillPrevention()
        {
            Unit attacker = Unit("Attacker", MovementProfile.Ground, UnitFaction.Player, 10, 6, 0, 3, 1, 1);
            Unit defender = Unit("Defender", MovementProfile.Ground, UnitFaction.Enemy, 10, 4, 0, 3, 1, 1);
            attacker.PlaceOnTile(Tile(0, 0, Terrain("Plain", 1, 0, true, true)));
            defender.PlaceOnTile(Tile(1, 0, Terrain("Plain", 1, 0, true, true)));

            CombatPreview preview = CombatResolver.BuildPreview(attacker, defender);
            Assert.That(preview.CanCounter, Is.True);
            Assert.That(preview.CounterDamage, Is.EqualTo(4));

            defender.ReceiveDamage(10);
            Assert.That(CombatResolver.CanCounterAttack(defender, attacker), Is.False);

            Unit fragile = Unit("Fragile", MovementProfile.Ground, UnitFaction.Enemy, 3, 4, 0, 3, 1, 1);
            fragile.PlaceOnTile(Tile(1, 0, Terrain("Plain", 1, 0, true, true)));
            Assert.That(CombatResolver.BuildPreview(attacker, fragile).CanCounter, Is.False);
        }

        [Test]
        public void HealingCalculation_TargetRulesPreviewAndResolution_AreConsistent()
        {
            SkillDefinition heal = Skill("Heal", SkillEffectType.Heal, SkillTargetType.Unit, SkillAreaShape.Single, 0, 2, 4, true, true, false, false);
            Unit healer = Unit("Healer", MovementProfile.Ground, UnitFaction.Player, 10, 3, 1, 3);
            Unit ally = Unit("Ally", MovementProfile.Ground, UnitFaction.Player, 10, 2, 1, 3);
            Unit enemy = Unit("Enemy", MovementProfile.Ground, UnitFaction.Enemy, 10, 2, 1, 3);
            healer.PlaceOnTile(Tile(0, 0, Terrain("Plain", 1, 0, true, true)));
            ally.PlaceOnTile(Tile(1, 0, Terrain("Plain", 1, 0, true, true)));
            enemy.PlaceOnTile(Tile(2, 0, Terrain("Plain", 1, 0, true, true)));
            ally.ReceiveDamage(5);

            Assert.That(SkillResolver.CalculateRawHealing(healer, heal), Is.EqualTo(7));
            Assert.That(SkillResolver.CalculateActualHealing(healer, heal, ally), Is.EqualTo(5));
            Assert.That(SkillResolver.CanTargetUnit(healer, heal, ally), Is.True);
            Assert.That(SkillResolver.CanTargetUnit(healer, heal, enemy), Is.False);
            Assert.That(SkillResolver.CanTargetUnit(healer, heal, healer), Is.False, "Full-health self is invalid under current rules.");

            SkillResolver.Resolve(healer, heal, new[] { ally });

            Assert.That(ally.CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void DamageSkillTargeting_RejectsInvalidTargetsAndRanges()
        {
            SkillDefinition fire = Skill("Fire", SkillEffectType.Damage, SkillTargetType.Unit, SkillAreaShape.Single, 1, 2, 3, false, false, true, false);
            Unit caster = Unit("Caster", MovementProfile.Ground, UnitFaction.Player, 10, 5, 1, 3);
            Unit enemy = Unit("Enemy", MovementProfile.Ground, UnitFaction.Enemy, 10, 2, 1, 3);
            Unit ally = Unit("Ally", MovementProfile.Ground, UnitFaction.Player, 10, 2, 1, 3);
            caster.PlaceOnTile(Tile(0, 0, Terrain("Plain", 1, 0, true, true)));
            enemy.PlaceOnTile(Tile(2, 0, Terrain("Plain", 1, 0, true, true)));
            ally.PlaceOnTile(Tile(1, 0, Terrain("Plain", 1, 0, true, true)));

            Assert.That(SkillResolver.CanTargetUnit(caster, fire, enemy), Is.True);
            Assert.That(SkillResolver.CanTargetUnit(caster, fire, ally), Is.False);
            Assert.That(SkillResolver.CanTargetUnit(caster, fire, caster), Is.False);
            Assert.That(SkillResolver.CanTargetTile(caster, fire, Tile(1, 1, Terrain("Plain", 1, 0, true, true))), Is.False);

            enemy.ReceiveDamage(99);
            Assert.That(SkillResolver.CanTargetUnit(caster, fire, enemy), Is.False);
        }

        [Test]
        public void GroundSkillTargeting_AcceptsEmptyAndOccupiedInRange_RejectsOutOfRange()
        {
            SkillDefinition cross = Skill("Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 2, 2, false, false, true, true);
            Unit caster = Unit("Caster", MovementProfile.Ground, UnitFaction.Player, 10, 5, 1, 3);
            Tile casterTile = Tile(0, 0, Terrain("Plain", 1, 0, true, true));
            Tile empty = Tile(1, 0, Terrain("Plain", 1, 0, true, true));
            Tile far = Tile(3, 0, Terrain("Plain", 1, 0, true, true));
            caster.PlaceOnTile(casterTile);

            Assert.That(SkillResolver.CanTargetTile(caster, cross, empty), Is.True);
            Assert.That(SkillResolver.CanTargetTile(caster, cross, far), Is.False);
            Assert.That(SkillResolver.CanTargetTile(caster, cross, null), Is.False);
        }

        [Test]
        public void CrossAreaCalculation_ContainsOrthogonalTiles_NoDuplicates_AndClipsEdges()
        {
            GridSystem grid = Grid(3, 3, new[] { "PPP", "PPP", "PPP" });
            SkillDefinition cross = Skill("Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 3, 2, false, false, true, true);
            grid.TryGetTile(new GridCoordinate(1, 1), out Tile center);
            List<Tile> area = new List<Tile>();

            SkillResolver.FillAreaTiles(grid, center, cross, area);

            Assert.That(area.Count, Is.EqualTo(5));
            Assert.That(Contains(area, 1, 1), Is.True);
            Assert.That(Contains(area, 1, 2), Is.True);
            Assert.That(Contains(area, 1, 0), Is.True);
            Assert.That(Contains(area, 2, 1), Is.True);
            Assert.That(Contains(area, 0, 1), Is.True);
            Assert.That(Contains(area, 0, 0), Is.False);
            Assert.That(new HashSet<Tile>(area).Count, Is.EqualTo(area.Count));

            grid.TryGetTile(new GridCoordinate(0, 2), out Tile corner);
            SkillResolver.FillAreaTiles(grid, corner, cross, area);
            Assert.That(area.Count, Is.EqualTo(3));
        }

        [Test]
        public void AreaAffectedUnits_IncludeEnemies_ExcludeAlliesAndEmptyTiles()
        {
            GridSystem grid = Grid(3, 3, new[] { "PPP", "PPP", "PPP" });
            SkillDefinition cross = Skill("Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 3, 2, false, false, true, true);
            Unit caster = Unit("Caster", MovementProfile.Ground, UnitFaction.Player, 10, 5, 1, 3);
            Unit enemy = Unit("Enemy", MovementProfile.Ground, UnitFaction.Enemy, 10, 2, 1, 3);
            Unit ally = Unit("Ally", MovementProfile.Ground, UnitFaction.Player, 10, 2, 1, 3);
            grid.TryGetTile(new GridCoordinate(0, 1), out Tile casterTile);
            grid.TryGetTile(new GridCoordinate(1, 1), out Tile center);
            grid.TryGetTile(new GridCoordinate(2, 1), out Tile enemyTile);
            grid.TryGetTile(new GridCoordinate(1, 2), out Tile allyTile);
            caster.PlaceOnTile(casterTile);
            enemy.PlaceOnTile(enemyTile);
            enemyTile.SetOccupyingUnit(enemy);
            ally.PlaceOnTile(allyTile);
            allyTile.SetOccupyingUnit(ally);
            List<Tile> area = new List<Tile>();
            List<Unit> affected = new List<Unit>();

            SkillResolver.FillAreaTiles(grid, center, cross, area);
            SkillResolver.FillAffectedUnits(caster, cross, area, affected);

            Assert.That(affected, Is.EqualTo(new[] { enemy }));
        }

        private GridSystem Grid(int width, int height, string[] rows)
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0, true, true);
            TerrainDefinition forest = Terrain("Forest", 3, 1, true, true);
            TerrainDefinition water = Terrain("Water", 1, 0, false, true);
            TerrainDefinition wall = Terrain("Wall", 1, 0, false, false);
            GameObject gridObject = new GameObject("Test Grid");
            createdObjects.Add(gridObject);
            GridSystem grid = gridObject.AddComponent<GridSystem>();
            Tile prefab = TilePrefab();

            SerializedObject so = new SerializedObject(grid);
            so.FindProperty("width").intValue = width;
            so.FindProperty("height").intValue = height;
            so.FindProperty("tilePrefab").objectReferenceValue = prefab;
            so.FindProperty("defaultTerrain").objectReferenceValue = plain;
            so.FindProperty("plainTerrain").objectReferenceValue = plain;
            so.FindProperty("forestTerrain").objectReferenceValue = forest;
            so.FindProperty("waterTerrain").objectReferenceValue = water;
            so.FindProperty("wallTerrain").objectReferenceValue = wall;
            SerializedProperty rowProperty = so.FindProperty("terrainRows");
            rowProperty.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                rowProperty.GetArrayElementAtIndex(i).stringValue = rows[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            grid.RebuildGrid();
            return grid;
        }

        private Tile TilePrefab()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(go);
            return go.AddComponent<Tile>();
        }

        private Tile Tile(int x, int y, TerrainDefinition terrain)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(go);
            Tile tile = go.AddComponent<Tile>();
            tile.Initialize(null, new GridCoordinate(x, y), terrain, Color.white, Color.white, Color.white, Color.white);
            return tile;
        }

        private Unit Unit(string name, MovementProfile profile, UnitFaction faction, int hp, int attack, int defense, int move, int minRange = 1, int maxRange = 1)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            createdObjects.Add(definition);
            SerializedObject definitionObject = new SerializedObject(definition);
            definitionObject.FindProperty("displayName").stringValue = name;
            definitionObject.FindProperty("maxHealth").intValue = hp;
            definitionObject.FindProperty("attackPower").intValue = attack;
            definitionObject.FindProperty("defense").intValue = defense;
            definitionObject.FindProperty("movementRange").intValue = move;
            definitionObject.FindProperty("movementProfile").enumValueIndex = (int)profile;
            definitionObject.FindProperty("minimumAttackRange").intValue = minRange;
            definitionObject.FindProperty("maximumAttackRange").intValue = maxRange;
            definitionObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            createdObjects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            SerializedObject unitObject = new SerializedObject(unit);
            unitObject.FindProperty("unitDefinition").objectReferenceValue = definition;
            unitObject.FindProperty("faction").enumValueIndex = (int)faction;
            unitObject.ApplyModifiedPropertiesWithoutUndo();
            unit.InitializeHealthForBattle();
            return unit;
        }

        private TerrainDefinition Terrain(string name, int cost, int defense, bool ground, bool flying)
        {
            TerrainDefinition terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            createdObjects.Add(terrain);
            SerializedObject so = new SerializedObject(terrain);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("terrainId").stringValue = name.ToLowerInvariant();
            so.FindProperty("baseMovementCost").intValue = cost;
            so.FindProperty("defenseBonus").intValue = defense;
            so.FindProperty("groundEnterable").boolValue = ground;
            so.FindProperty("flyingEnterable").boolValue = flying;
            so.ApplyModifiedPropertiesWithoutUndo();
            return terrain;
        }

        private SkillDefinition Skill(string name, SkillEffectType effect, SkillTargetType target, SkillAreaShape area, int minRange, int maxRange, int power, bool self, bool allies, bool enemies, bool ground)
        {
            SkillDefinition skill = ScriptableObject.CreateInstance<SkillDefinition>();
            createdObjects.Add(skill);
            SerializedObject so = new SerializedObject(skill);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("effectType").enumValueIndex = (int)effect;
            so.FindProperty("targetType").enumValueIndex = (int)target;
            so.FindProperty("areaShape").enumValueIndex = (int)area;
            so.FindProperty("areaSize").intValue = area == SkillAreaShape.Cross ? 1 : 0;
            so.FindProperty("minimumRange").intValue = minRange;
            so.FindProperty("maximumRange").intValue = maxRange;
            so.FindProperty("power").intValue = power;
            so.FindProperty("canTargetSelf").boolValue = self;
            so.FindProperty("canTargetAllies").boolValue = allies;
            so.FindProperty("canTargetEnemies").boolValue = enemies;
            so.FindProperty("canTargetEmptyGround").boolValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static int PathCost(IReadOnlyList<Tile> path, Unit unit)
        {
            int cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                cost += path[i].GetMovementCost(unit);
            }

            return cost;
        }

        private static bool Contains(IReadOnlyList<Tile> tiles, int x, int y)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].X == x && tiles[i].Y == y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
