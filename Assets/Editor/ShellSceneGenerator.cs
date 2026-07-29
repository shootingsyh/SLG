using System.Collections.Generic;
using System.Linq;
using SLG.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SLG.EditorTools
{
    public static class ShellSceneGenerator
    {
        private const string SceneFolder = "Assets/Scenes";

        public static void Generate()
        {
            EnsureFolder(SceneFolder);

            CreateScene("Boot", typeof(BootController));
            CreateScene("Title", typeof(TitleController));
            CreateScene("ChapterSelect", typeof(ChapterSelectController));
            CreateScene("ChapterResult", typeof(ChapterResultController));
            CreateScene("InterGame", typeof(InterGameController));
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateScene(string sceneName, System.Type controllerType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject controller = new GameObject($"{sceneName} Controller");
            controller.AddComponent(controllerType);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            cameraObject.transform.rotation = Quaternion.identity;
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.GetComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{sceneName}.unity");
        }

        private static void UpdateBuildSettings()
        {
            string[] shellScenes =
            {
                "Assets/Scenes/Boot.unity",
                "Assets/Scenes/Title.unity",
                "Assets/Scenes/ChapterSelect.unity",
                "Assets/Scenes/ChapterResult.unity",
                "Assets/Scenes/InterGame.unity",
                "Assets/Scenes/BattleTestTemplate.unity",
                "Assets/Scenes/BattleTestLab.unity"
            };

            var existing = EditorBuildSettings.scenes
                .Where(scene => !shellScenes.Contains(scene.path))
                .ToList();

            var ordered = new List<EditorBuildSettingsScene>();
            foreach (string scenePath in shellScenes)
            {
                ordered.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            ordered.AddRange(existing);
            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }
    }
}
