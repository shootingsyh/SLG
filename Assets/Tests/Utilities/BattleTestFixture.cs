using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SLG.Core;
using SLG.Grid;
using SLG.Skills;
using SLG.Terrain;
using SLG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SLG.Tests.Utilities
{
    public sealed class BattleTestFixture : MonoBehaviour
    {
        [SerializeField] private BattleTestScenario scenario;

        private readonly Dictionary<string, Unit> unitsByKey = new Dictionary<string, Unit>();
        private readonly Dictionary<string, SkillDefinition> skillsByKey = new Dictionary<string, SkillDefinition>();

        public BattleTestScenario Scenario => scenario;
        public GridSystem Grid { get; private set; }
        public BattleTurnController Turns { get; private set; }
        public UnitSelectionController Player { get; private set; }
        public bool IsReady { get; private set; }
        public string ValidationError { get; private set; }

        public Unit this[string key] => unitsByKey[key];
        public SkillDefinition Skill(string key) => skillsByKey[key];

        public Tile Tile(int x, int y)
        {
            return Grid != null && Grid.TryGetTile(new GridCoordinate(x, y), out Tile tile) ? tile : null;
        }

        private void Awake()
        {
            Time.timeScale = 8f;
            EnsureSceneServices();
            BuildBattle();
        }

        private IEnumerator Start()
        {
            yield return null;
            Grid.RebuildGrid();
            Player.InitializeUnitsOnGrid();
            yield return null;
            IsReady = ValidateReferences(out string error);
            ValidationError = error;
        }

        public bool ValidateReferences(out string error)
        {
            if (Grid == null || Grid.Pathfinder == null || Grid.Reachability == null)
            {
                error = "Grid, pathfinder, or reachability is missing.";
                return false;
            }

            if (Turns == null || Player == null)
            {
                error = "Turn controller or player interaction controller is missing.";
                return false;
            }

            foreach (KeyValuePair<string, Unit> pair in unitsByKey)
            {
                Unit unit = pair.Value;
                if (unit == null)
                {
                    error = $"Unit '{pair.Key}' is null.";
                    return false;
                }

                if (unit.IsAlive && unit.OccupiedTile == null)
                {
                    error = $"Living unit '{pair.Key}' has no occupied tile.";
                    return false;
                }

                if (unit.IsAlive && unit.OccupiedTile.OccupyingUnit != unit)
                {
                    error = $"Unit '{pair.Key}' tile does not point back to the unit.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public string DumpState()
        {
            var builder = new System.Text.StringBuilder(512);
            builder.AppendLine($"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");
            builder.AppendLine($"Scenario={scenario} Phase={Turns?.CurrentPhase} BattleEnded={Turns?.IsBattleEnded} Result={Turns?.BattleResult} EnemyActing={Turns?.IsEnemyActing}");
            builder.AppendLine($"FSM={Player?.CurrentInteractionState} Selected={Player?.SelectedUnit?.name ?? "None"} Skill={Player?.SelectedSkill?.DisplayName ?? "None"} Original={FormatTile(Player?.OriginalTile)} Current={FormatTile(Player?.CurrentTile)} Provisional={Player?.HasProvisionalMovement}");
            builder.AppendLine($"LivingPlayers={Turns?.CountLivingUnits(UnitFaction.Player)} LivingEnemies={Turns?.CountLivingUnits(UnitFaction.Enemy)}");
            foreach (KeyValuePair<string, Unit> pair in unitsByKey)
            {
                Unit unit = pair.Value;
                builder.AppendLine($"Unit[{pair.Key}] name={unit.name} faction={unit.Faction} alive={unit.IsAlive} hp={unit.CurrentHealth}/{unit.MaxHealth} acted={unit.HasActed} tile={FormatTile(unit.OccupiedTile)} moving={unit.IsMoving}");
            }

            return builder.ToString();
        }

        private static string FormatTile(Tile tile)
        {
            return tile != null ? tile.Coordinate.ToString() : "None";
        }

        private void EnsureSceneServices()
        {
            if (FindAnyObjectByType<Canvas>() == null)
            {
                GameObject canvasObject = new GameObject("Test Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (Camera.main == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 8f, -8f);
                cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }

            if (FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light", typeof(Light));
                lightObject.GetComponent<Light>().type = LightType.Directional;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private void BuildBattle()
        {
            GameObject systems = new GameObject("Battle Systems");
            Player = systems.AddComponent<UnitSelectionController>();
            Turns = systems.AddComponent<BattleTurnController>();

            GameObject gridObject = new GameObject("Test Grid");
            Grid = gridObject.AddComponent<GridSystem>();

            string[] rows = GetRowsForScenario();
            SetField(Grid, "width", rows[0].Length);
            SetField(Grid, "height", rows.Length);
            SetField(Grid, "tilePrefab", CreateTilePrefab());
            TerrainDefinition plain = Terrain("Plain", "plain", 1, 0, true, true);
            TerrainDefinition forest = Terrain("Forest", "forest", 3, 2, true, true);
            TerrainDefinition water = Terrain("Water", "water", 1, 0, false, true);
            TerrainDefinition wall = Terrain("Wall", "wall", 1, 0, false, false);
            SetField(Grid, "defaultTerrain", plain);
            SetField(Grid, "plainTerrain", plain);
            SetField(Grid, "forestTerrain", forest);
            SetField(Grid, "waterTerrain", water);
            SetField(Grid, "wallTerrain", wall);
            SetField(Grid, "terrainRows", rows);
            SetField(Grid, "unitSelectionController", Player);
            SetField(Player, "gridSystem", Grid);
            SetField(Player, "battleTurnController", Turns);
            SetField(Turns, "gridSystem", Grid);
            SetField(Turns, "unitSelectionController", Player);

            BuildScenarioUnits();
        }

        private string[] GetRowsForScenario()
        {
            switch (scenario)
            {
                case BattleTestScenario.MovementAndTerrain:
                    return new[] { "PPPPP", "PFWXP", "PPPPP", "PPPPP" };
                case BattleTestScenario.MovementRollback:
                    return new[] { "PPPP", "PPPP", "PPPP" };
                case BattleTestScenario.Combat:
                    return new[] { "PPPP", "PFFP", "PPPP" };
                case BattleTestScenario.Skills:
                    return new[] { "PPPPP", "PPPPP", "PPPPP", "PPPPP", "PPPPP" };
                case BattleTestScenario.TurnAndAI:
                    return new[] { "PPPPP", "PPPPP", "PPPPP" };
                case BattleTestScenario.BattleEnd:
                    return new[] { "PPPP", "PPPP", "PPPP" };
                default:
                    return new[] { "PPP", "PPP", "PPP" };
            }
        }

        private void BuildScenarioUnits()
        {
            switch (scenario)
            {
                case BattleTestScenario.MovementAndTerrain:
                    AddUnit("ground", "Ground", UnitFaction.Player, MovementProfile.Ground, 0, 1, 10, 4, 1, 4, 1, 1, 10);
                    AddUnit("flyer", "Flyer", UnitFaction.Player, MovementProfile.Flying, 0, 2, 10, 4, 1, 4, 1, 1, 10);
                    AddUnit("enemy", "Enemy", UnitFaction.Enemy, MovementProfile.Ground, 4, 3, 10, 2, 1, 3, 1, 1, 10);
                    break;
                case BattleTestScenario.MovementRollback:
                    AddUnit("mover", "Mover", UnitFaction.Player, MovementProfile.Ground, 0, 1, 10, 4, 1, 4, 1, 1, 10);
                    AddUnit("ally", "Ally", UnitFaction.Player, MovementProfile.Ground, 0, 2, 10, 4, 1, 4, 1, 1, 10);
                    AddUnit("enemy", "Enemy", UnitFaction.Enemy, MovementProfile.Ground, 3, 2, 10, 2, 1, 3, 1, 1, 10);
                    break;
                case BattleTestScenario.Combat:
                    AddUnit("attacker", "Attacker", UnitFaction.Player, MovementProfile.Ground, 0, 1, 12, 6, 1, 3, 1, 1, 12);
                    AddUnit("support", "Support", UnitFaction.Player, MovementProfile.Ground, 0, 0, 10, 2, 1, 3, 1, 1, 10);
                    AddUnit("defender", "Defender", UnitFaction.Enemy, MovementProfile.Ground, 1, 1, 10, 4, 1, 3, 1, 1, 10);
                    AddUnit("farEnemy", "Far Enemy", UnitFaction.Enemy, MovementProfile.Ground, 3, 2, 10, 3, 1, 3, 1, 1, 10);
                    AddUnit("fragileEnemy", "Fragile Enemy", UnitFaction.Enemy, MovementProfile.Ground, 1, 2, 4, 1, 0, 3, 1, 1, 4);
                    break;
                case BattleTestScenario.Skills:
                    SkillDefinition fire = AddSkill("fire", "Fire Bolt", SkillEffectType.Damage, SkillTargetType.Unit, SkillAreaShape.Single, 1, 3, 3, false, false, true, false);
                    SkillDefinition heal = AddSkill("heal", "Heal", SkillEffectType.Heal, SkillTargetType.Unit, SkillAreaShape.Single, 0, 2, 4, true, true, false, false);
                    SkillDefinition cross = AddSkill("cross", "Flame Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 3, 2, false, false, true, true);
                    AddUnit("mage", "Mage", UnitFaction.Player, MovementProfile.Ground, 0, 2, 12, 5, 1, 4, 1, 2, 12, fire, cross);
                    AddUnit("healer", "Healer", UnitFaction.Player, MovementProfile.Ground, 0, 0, 11, 3, 1, 4, 1, 1, 11, heal);
                    AddUnit("damagedAlly", "Damaged Ally", UnitFaction.Player, MovementProfile.Ground, 1, 0, 10, 2, 1, 4, 1, 1, 4);
                    AddUnit("areaAlly", "Area Ally", UnitFaction.Player, MovementProfile.Ground, 3, 3, 10, 2, 1, 4, 1, 1, 10);
                    AddUnit("enemy", "Enemy", UnitFaction.Enemy, MovementProfile.Ground, 2, 2, 10, 2, 1, 3, 1, 1, 10);
                    AddUnit("areaEnemy", "Area Enemy", UnitFaction.Enemy, MovementProfile.Ground, 3, 2, 10, 2, 1, 3, 1, 1, 10);
                    AddUnit("outsideEnemy", "Outside Enemy", UnitFaction.Enemy, MovementProfile.Ground, 4, 4, 10, 2, 1, 3, 1, 1, 10);
                    break;
                case BattleTestScenario.TurnAndAI:
                    AddUnit("playerA", "Player A", UnitFaction.Player, MovementProfile.Ground, 0, 1, 12, 4, 1, 3, 1, 1, 12);
                    AddUnit("playerB", "Player B", UnitFaction.Player, MovementProfile.Ground, 0, 2, 12, 4, 1, 3, 1, 1, 12);
                    AddUnit("enemy", "Enemy", UnitFaction.Enemy, MovementProfile.Ground, 4, 1, 10, 4, 1, 3, 1, 1, 10);
                    break;
                case BattleTestScenario.BattleEnd:
                    AddUnit("hero", "Hero", UnitFaction.Player, MovementProfile.Ground, 0, 2, 10, 20, 0, 3, 1, 1, 10);
                    AddUnit("fragileHero", "Fragile Hero", UnitFaction.Player, MovementProfile.Ground, 0, 1, 2, 1, 0, 3, 1, 1, 2);
                    AddUnit("lastEnemy", "Last Enemy", UnitFaction.Enemy, MovementProfile.Ground, 1, 1, 5, 20, 0, 3, 1, 1, 5);
                    break;
            }
        }

        private SkillDefinition AddSkill(string key, string name, SkillEffectType effect, SkillTargetType target, SkillAreaShape area, int minRange, int maxRange, int power, bool self, bool allies, bool enemies, bool ground)
        {
            SkillDefinition skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.name = name;
            SetField(skill, "skillId", key);
            SetField(skill, "displayName", name);
            SetField(skill, "description", name);
            SetField(skill, "effectType", effect);
            SetField(skill, "targetType", target);
            SetField(skill, "areaShape", area);
            SetField(skill, "areaSize", area == SkillAreaShape.Cross ? 1 : 0);
            SetField(skill, "minimumRange", minRange);
            SetField(skill, "maximumRange", maxRange);
            SetField(skill, "power", power);
            SetField(skill, "canTargetSelf", self);
            SetField(skill, "canTargetAllies", allies);
            SetField(skill, "canTargetEnemies", enemies);
            SetField(skill, "canTargetEmptyGround", ground);
            skillsByKey[key] = skill;
            return skill;
        }

        private Unit AddUnit(string key, string displayName, UnitFaction faction, MovementProfile profile, int x, int y, int hp, int attack, int defense, int move, int minRange, int maxRange, int currentHp, params SkillDefinition[] skills)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.name = displayName;
            SetField(definition, "displayName", displayName);
            SetField(definition, "archetypeName", displayName);
            SetField(definition, "maxHealth", hp);
            SetField(definition, "attackPower", attack);
            SetField(definition, "defense", defense);
            SetField(definition, "movementRange", move);
            SetField(definition, "movementProfile", profile);
            SetField(definition, "minimumAttackRange", minRange);
            SetField(definition, "maximumAttackRange", maxRange);
            SetField(definition, "skills", new List<SkillDefinition>(skills ?? Array.Empty<SkillDefinition>()));

            GameObject unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitObject.name = key;
            Unit unit = unitObject.AddComponent<Unit>();
            SetField(unit, "unitDefinition", definition);
            SetField(unit, "faction", faction);
            SetField(unit, "movementSpeed", 100f);
            SetField(unit, "currentCoordinate", new GridCoordinate(x, y));
            SetField(unit, "currentHealth", currentHp);
            unit.InitializeHealthForBattle();
            unitsByKey[key] = unit;
            return unit;
        }

        private static TerrainDefinition Terrain(string name, string id, int cost, int defense, bool ground, bool flying)
        {
            TerrainDefinition terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            terrain.name = name;
            SetField(terrain, "displayName", name);
            SetField(terrain, "terrainId", id);
            SetField(terrain, "baseMovementCost", cost);
            SetField(terrain, "defenseBonus", defense);
            SetField(terrain, "groundEnterable", ground);
            SetField(terrain, "flyingEnterable", flying);
            return terrain;
        }

        private static Tile CreateTilePrefab()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "Runtime Test Tile Prefab";
            return prefab.AddComponent<Tile>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
