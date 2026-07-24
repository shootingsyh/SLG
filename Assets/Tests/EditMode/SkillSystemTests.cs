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
    public sealed class SkillSystemTests
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
        public void FireBoltUsesEffectiveTerrainDefense()
        {
            SkillDefinition fireBolt = Skill("Fire Bolt", SkillEffectType.Damage, SkillTargetType.Unit, SkillAreaShape.Single, 1, 3, 3, false, false, true, false);
            Unit caster = Unit("Mage", UnitFaction.Player, 10, 5, 0);
            Unit target = Unit("Enemy", UnitFaction.Enemy, 10, 2, 1);
            target.PlaceOnTile(Tile(1, 0, Terrain("Forest", 1, 2)));

            Assert.That(SkillResolver.CanTargetUnit(caster, fireBolt, target), Is.True);
            Assert.That(SkillResolver.CalculateDamage(caster, fireBolt, target), Is.EqualTo(5));
        }

        [Test]
        public void HealTargetsDamagedAlliesAndCapsAtMaxHealth()
        {
            SkillDefinition heal = Skill("Heal", SkillEffectType.Heal, SkillTargetType.Unit, SkillAreaShape.Single, 0, 2, 4, true, true, false, false);
            Unit healer = Unit("Healer", UnitFaction.Player, 10, 3, 0);
            Unit ally = Unit("Ally", UnitFaction.Player, 10, 2, 0);
            Unit enemy = Unit("Enemy", UnitFaction.Enemy, 10, 2, 0);
            ally.ReceiveDamage(3);

            Assert.That(SkillResolver.CanTargetUnit(healer, heal, ally), Is.True);
            Assert.That(SkillResolver.CanTargetUnit(healer, heal, enemy), Is.False);
            Assert.That(SkillResolver.CanTargetUnit(healer, heal, healer), Is.False, "Full-health self should not be a valid heal target.");
            Assert.That(SkillResolver.CalculateActualHealing(healer, heal, ally), Is.EqualTo(3));
            ally.ReceiveHealing(SkillResolver.CalculateActualHealing(healer, heal, ally));
            Assert.That(ally.CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void CrossAreaIgnoresTilesOutsideGrid()
        {
            GridSystem grid = Grid(2, 2);
            SkillDefinition flameCross = Skill("Flame Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 3, 2, false, false, true, true);
            grid.TryGetTile(new GridCoordinate(0, 0), out Tile corner);
            List<Tile> results = new List<Tile>();

            SkillResolver.FillAreaTiles(grid, corner, flameCross, results);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(Contains(results, 0, 0), Is.True);
            Assert.That(Contains(results, 1, 0), Is.True);
            Assert.That(Contains(results, 0, 1), Is.True);
        }

        private GridSystem Grid(int width, int height)
        {
            TerrainDefinition plain = Terrain("Plain", 1, 0);
            GameObject gridObject = new GameObject("Skill Test Grid");
            createdObjects.Add(gridObject);
            GridSystem grid = gridObject.AddComponent<GridSystem>();
            GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(tileObject);
            Tile prefab = tileObject.AddComponent<Tile>();

            SerializedObject so = new SerializedObject(grid);
            so.FindProperty("width").intValue = width;
            so.FindProperty("height").intValue = height;
            so.FindProperty("tilePrefab").objectReferenceValue = prefab;
            so.FindProperty("defaultTerrain").objectReferenceValue = plain;
            so.FindProperty("plainTerrain").objectReferenceValue = plain;
            SerializedProperty rows = so.FindProperty("terrainRows");
            rows.arraySize = height;
            for (int i = 0; i < height; i++)
            {
                rows.GetArrayElementAtIndex(i).stringValue = new string('P', width);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            grid.RebuildGrid();
            return grid;
        }

        private Tile Tile(int x, int y, TerrainDefinition terrain)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(go);
            Tile tile = go.AddComponent<Tile>();
            tile.Initialize(null, new GridCoordinate(x, y), terrain, Color.white, Color.white, Color.white, Color.white);
            return tile;
        }

        private Unit Unit(string name, UnitFaction faction, int hp, int attack, int defense)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            createdObjects.Add(definition);
            SerializedObject definitionObject = new SerializedObject(definition);
            definitionObject.FindProperty("displayName").stringValue = name;
            definitionObject.FindProperty("maxHealth").intValue = hp;
            definitionObject.FindProperty("attackPower").intValue = attack;
            definitionObject.FindProperty("defense").intValue = defense;
            definitionObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            createdObjects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            SerializedObject unitObject = new SerializedObject(unit);
            unitObject.FindProperty("unitDefinition").objectReferenceValue = definition;
            unitObject.FindProperty("faction").enumValueIndex = (int)faction;
            unitObject.ApplyModifiedPropertiesWithoutUndo();
            unit.InitializeHealthForBattle();
            unit.PlaceOnTile(Tile(faction == UnitFaction.Player ? 0 : 1, 0, Terrain("Plain", 1, 0)));
            return unit;
        }

        private TerrainDefinition Terrain(string name, int cost, int defense)
        {
            TerrainDefinition terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            createdObjects.Add(terrain);
            SerializedObject so = new SerializedObject(terrain);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("baseMovementCost").intValue = cost;
            so.FindProperty("defenseBonus").intValue = defense;
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
