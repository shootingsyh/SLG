using System.Collections.Generic;
using NUnit.Framework;
using SLG.Core;
using SLG.Grid;
using SLG.Terrain;
using SLG.Units;
using UnityEditor;
using UnityEngine;

namespace SLG.Tests
{
    public sealed class TerrainMovementCombatTests
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
        public void TerrainMovementRulesMatchProfiles()
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0, true, true);
            TerrainDefinition forest = Terrain("Forest", 2, 1, true, true);
            TerrainDefinition water = Terrain("Water", 1, 0, false, true);
            TerrainDefinition wall = Terrain("Wall", 1, 0, false, false);
            Unit ground = Unit("Ground", Definition("Ground", MovementProfile.Ground, 10, 4, 1, 1, 1, 4), UnitFaction.Player, 0, 0);
            Unit flying = Unit("Flying", Definition("Flying", MovementProfile.Flying, 10, 4, 1, 1, 1, 4), UnitFaction.Player, 0, 0);

            Assert.That(Tile(0, 0, plain).CanEnter(ground), Is.True);
            Assert.That(Tile(0, 0, water).CanEnter(ground), Is.False);
            Assert.That(Tile(0, 0, water).CanEnter(flying), Is.True);
            Assert.That(Tile(0, 0, wall).CanEnter(ground), Is.False);
            Assert.That(Tile(0, 0, wall).CanEnter(flying), Is.False);
            Assert.That(Tile(0, 0, forest).GetMovementCost(ground), Is.EqualTo(2));
            Assert.That(Tile(0, 0, forest).GetMovementCost(flying), Is.EqualTo(1));
        }

        [Test]
        public void ReachabilityRespectsTerrainCostAndBlockedTiles()
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0, true, true);
            TerrainDefinition forest = Terrain("Forest", 2, 1, true, true);
            TerrainDefinition water = Terrain("Water", 1, 0, false, true);
            TerrainDefinition wall = Terrain("Wall", 1, 0, false, false);
            GridSystem grid = Grid(4, 3, new[] { "PPPP", "PFWP", "PXPP" }, plain, null, forest, null, water, wall);
            Unit ground = Unit("Ground", Definition("Ground", MovementProfile.Ground, 10, 4, 1, 1, 1, 3), UnitFaction.Player, 0, 1);
            Unit flying = Unit("Flying", Definition("Flying", MovementProfile.Flying, 10, 4, 1, 1, 1, 3), UnitFaction.Player, 0, 1);
            grid.TryGetTile(new GridCoordinate(0, 1), out Tile start);
            ground.PlaceOnTile(start);
            flying.PlaceOnTile(start);
            List<Tile> results = new List<Tile>();

            grid.Reachability.FindReachableTiles(start, ground, ground.MovementRange, results);
            Assert.That(Contains(results, 2, 1), Is.False, "Ground cannot cross water.");
            Assert.That(Contains(results, 1, 0), Is.False, "Wall blocks all movement.");
            Assert.That(Contains(results, 1, 1), Is.True, "Forest is reachable but costs 2.");

            grid.Reachability.FindReachableTiles(start, flying, flying.MovementRange, results);
            Assert.That(Contains(results, 2, 1), Is.True, "Flying crosses water.");
            Assert.That(Contains(results, 1, 0), Is.False, "Flying still cannot cross wall.");
        }

        [Test]
        public void PathfindingRoutesGroundAroundWaterAndFlyingAcrossWater()
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0, true, true);
            TerrainDefinition water = Terrain("Water", 1, 0, false, true);
            GridSystem grid = Grid(4, 3, new[] { "PPPP", "PWWP", "PPPP" }, plain, null, null, null, water, null);
            Unit ground = Unit("Ground", Definition("Ground", MovementProfile.Ground, 10, 4, 1, 1, 1, 8), UnitFaction.Player, 0, 1);
            Unit flying = Unit("Flying", Definition("Flying", MovementProfile.Flying, 10, 4, 1, 1, 1, 8), UnitFaction.Player, 0, 1);
            grid.TryGetTile(new GridCoordinate(0, 1), out Tile start);
            grid.TryGetTile(new GridCoordinate(3, 1), out Tile end);
            List<Tile> path = new List<Tile>();

            Assert.That(grid.Pathfinder.TryFindPath(start, end, ground, path), Is.True);
            Assert.That(Contains(path, 1, 1), Is.False);
            Assert.That(Contains(path, 2, 1), Is.False);

            Assert.That(grid.Pathfinder.TryFindPath(start, end, flying, path), Is.True);
            Assert.That(Contains(path, 1, 1), Is.True);
            Assert.That(Contains(path, 2, 1), Is.True);
        }

        [Test]
        public void CombatUsesTerrainDefenseAndPreviewMatchesDamage()
        {
            TerrainDefinition mountain = Terrain("Mountain", 3, 2, true, true);
            UnitDefinition attackerDefinition = Definition("Attacker", MovementProfile.Ground, 10, 5, 0, 1, 1, 4);
            UnitDefinition defenderDefinition = Definition("Defender", MovementProfile.Ground, 10, 3, 2, 1, 1, 4);
            Unit attacker = Unit("Attacker", attackerDefinition, UnitFaction.Player, 0, 0);
            Unit defender = Unit("Defender", defenderDefinition, UnitFaction.Enemy, 1, 0);
            Tile attackerTile = Tile(0, 0, Terrain("Plain", 1, 0, true, true));
            Tile defenderTile = Tile(1, 0, mountain);
            attacker.PlaceOnTile(attackerTile);
            defender.PlaceOnTile(defenderTile);

            CombatPreview preview = CombatResolver.BuildPreview(attacker, defender);

            Assert.That(preview.DefenderTerrainDefenseBonus, Is.EqualTo(2));
            Assert.That(preview.DefenderEffectiveDefense, Is.EqualTo(4));
            Assert.That(preview.AttackerDamage, Is.EqualTo(1));
            Assert.That(CombatResolver.CalculateDamage(attacker, defender), Is.EqualTo(preview.AttackerDamage));
        }

        [Test]
        public void SharedUnitDefinitionDoesNotShareRuntimeHealth()
        {
            UnitDefinition definition = Definition("Shared", MovementProfile.Ground, 10, 4, 1, 1, 1, 4);
            Unit first = Unit("First", definition, UnitFaction.Player, 0, 0);
            Unit second = Unit("Second", definition, UnitFaction.Player, 1, 0);

            first.InitializeHealthForBattle();
            second.InitializeHealthForBattle();
            first.ReceiveDamage(3);

            Assert.That(first.CurrentHealth, Is.EqualTo(7));
            Assert.That(second.CurrentHealth, Is.EqualTo(10));
            Assert.That(definition.MaxHealth, Is.EqualTo(10));
        }

        private GridSystem Grid(int width, int height, string[] rows, TerrainDefinition plain, TerrainDefinition road, TerrainDefinition forest, TerrainDefinition mountain, TerrainDefinition water, TerrainDefinition wall)
        {
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
            so.FindProperty("roadTerrain").objectReferenceValue = road;
            so.FindProperty("forestTerrain").objectReferenceValue = forest;
            so.FindProperty("mountainTerrain").objectReferenceValue = mountain;
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
            go.name = "Tile Prefab";
            Tile tile = go.AddComponent<Tile>();
            createdObjects.Add(go);
            return tile;
        }

        private Tile Tile(int x, int y, TerrainDefinition terrain)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(go);
            Tile tile = go.AddComponent<Tile>();
            tile.Initialize(null, new GridCoordinate(x, y), terrain, Color.white, Color.white, Color.white, Color.white);
            return tile;
        }

        private Unit Unit(string name, UnitDefinition definition, UnitFaction faction, int x, int y)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            createdObjects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            SerializedObject so = new SerializedObject(unit);
            so.FindProperty("unitDefinition").objectReferenceValue = definition;
            so.FindProperty("faction").enumValueIndex = (int)faction;
            SerializedProperty coordinate = so.FindProperty("currentCoordinate");
            coordinate.FindPropertyRelative("x").intValue = x;
            coordinate.FindPropertyRelative("y").intValue = y;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private UnitDefinition Definition(string name, MovementProfile profile, int hp, int attack, int defense, int minRange, int maxRange, int move)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            createdObjects.Add(definition);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("maxHealth").intValue = hp;
            so.FindProperty("attackPower").intValue = attack;
            so.FindProperty("defense").intValue = defense;
            so.FindProperty("minimumAttackRange").intValue = minRange;
            so.FindProperty("maximumAttackRange").intValue = maxRange;
            so.FindProperty("movementRange").intValue = move;
            so.FindProperty("movementProfile").enumValueIndex = (int)profile;
            so.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private bool Contains(List<Tile> tiles, int x, int y)
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
