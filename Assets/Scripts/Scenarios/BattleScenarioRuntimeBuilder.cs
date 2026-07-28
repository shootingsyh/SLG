using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using SLG.Saves;
using SLG.Terrain;
using SLG.Units;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace SLG.Scenarios
{
    public sealed class BattleRuntimeContext
    {
        public GridSystem Grid;
        public BattleTurnController Turns;
        public UnitSelectionController Player;
        public BattleScenarioController Scenario;
        public BattleSystemMenuController SystemMenu;
        public BattleEndScreenController EndScreen;
        public readonly Dictionary<string, Unit> UnitsByKey = new Dictionary<string, Unit>();
    }

    public static class BattleScenarioRuntimeBuilder
    {
        public static BattleRuntimeContext Build(BattleSetupConfiguration config, Transform parent = null, bool createUi = true, SaveRepository repository = null, BattleTestPresetId presetId = BattleTestPresetId.FullScenarioSmoke)
        {
            ClearExistingRuntimeBattleObjects();
            List<string> errors = new List<string>();
            if (!BattleSetupValidator.Validate(config, errors))
            {
                throw new System.InvalidOperationException(string.Join("\n", errors));
            }

            if (createUi)
            {
                EnsureSceneServices();
            }

            GameObject systems = new GameObject("Battle Systems");
            if (parent != null)
            {
                systems.transform.SetParent(parent, false);
            }

            UnitSelectionController player = systems.AddComponent<UnitSelectionController>();
            BattleTurnController turns = systems.AddComponent<BattleTurnController>();
            BattleScenarioController scenario = systems.AddComponent<BattleScenarioController>();
            BattleSystemMenuController systemMenu = systems.AddComponent<BattleSystemMenuController>();
            BattleEndScreenController endScreen = systems.AddComponent<BattleEndScreenController>();

            GameObject gridObject = new GameObject("Scenario Grid");
            if (parent != null)
            {
                gridObject.transform.SetParent(parent, false);
            }

            GridSystem grid = gridObject.AddComponent<GridSystem>();
            grid.ConfigureRuntime(config.Width, config.Height, config.TerrainRows, CreateTilePrefab(), Terrain("Plain", "plain", 1, 0, true, true), Terrain("Forest", "forest", 3, 2, true, true), Terrain("Water", "water", 1, 0, false, true), Terrain("Wall", "wall", 1, 0, false, false), player);
            player.ConfigureRuntime(grid, turns);
            turns.ConfigureRuntime(grid, player, scenario);
            turns.ConfigureSystemMenu(systemMenu);

            BattleRuntimeContext context = new BattleRuntimeContext
            {
                Grid = grid,
                Turns = turns,
                Player = player,
                Scenario = scenario,
                SystemMenu = systemMenu,
                EndScreen = endScreen
            };

            systemMenu.Configure(context, repository ?? GameShellServices.Repository, presetId);

            for (int i = 0; i < config.Units.Count; i++)
            {
                BattleUnitSetup setup = config.Units[i];
                Unit unit = CreateUnit(setup, player);
                context.UnitsByKey[setup.Key] = unit;
                scenario.RegisterUnit(setup.Role, unit);
            }

            grid.RebuildGrid();
            player.InitializeUnitsOnGrid();
            scenario.Configure(config, grid, player, turns);
            return context;
        }

        private static Unit CreateUnit(BattleUnitSetup setup, UnitSelectionController player)
        {
            GameObject unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitObject.name = setup.Key;
            Unit unit = unitObject.AddComponent<Unit>();
            unit.ConfigureRuntime(setup.Definition, setup.Faction, setup.Coordinate, setup.CurrentHealth, 100f);
            return unit;
        }

        private static void ClearExistingRuntimeBattleObjects()
        {
            Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null)
                {
                    units[i].gameObject.SetActive(false);
                    Object.Destroy(units[i].gameObject);
                }
            }

            GridSystem[] grids = Object.FindObjectsByType<GridSystem>(FindObjectsInactive.Exclude);
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null)
                {
                    grids[i].gameObject.SetActive(false);
                    Object.Destroy(grids[i].gameObject);
                }
            }

            BattleTurnController[] turns = Object.FindObjectsByType<BattleTurnController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < turns.Length; i++)
            {
                if (turns[i] != null)
                {
                    turns[i].gameObject.SetActive(false);
                    Object.Destroy(turns[i].gameObject);
                }
            }
        }

        private static TerrainDefinition Terrain(string name, string id, int cost, int defense, bool ground, bool flying)
        {
            TerrainDefinition terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            terrain.name = name;
            terrain.ConfigureRuntime(name, id, cost, defense, ground, flying);
            return terrain;
        }

        private static Tile CreateTilePrefab()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "Scenario Tile Prefab";
            return prefab.AddComponent<Tile>();
        }

        private static void EnsureSceneServices()
        {
            if (Object.FindAnyObjectByType<Canvas>() == null)
            {
                GameObject canvasObject = new GameObject("Battle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            EnsureEventSystem();

            if (Camera.main == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 8f, -8f);
                cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light", typeof(Light));
                lightObject.GetComponent<Light>().type = LightType.Directional;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
