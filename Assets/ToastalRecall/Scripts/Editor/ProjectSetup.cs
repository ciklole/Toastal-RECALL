using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// One-click project scaffolding: creates the three menu scenes, the level
    /// catalog and the build-settings entries. Level scenes are left untouched.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ResourcesFolder = "Assets/ToastalRecall/Resources";
        private const string CatalogPath = ResourcesFolder + "/LevelCatalog.asset";

        [MenuItem("Tools/Toastal Recall/Setup Project (Create Menu Scenes)")]
        public static void CreateCoreScenes()
        {
            Directory.CreateDirectory(ScenesFolder);
            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();

            var previousScene = SceneManager.GetActiveScene();

            CreateMenuScene("MainMenu", typeof(MainMenuController));
            CreateMenuScene("LevelSelect", typeof(LevelSelectionController));
            CreateMenuScene("Settings", typeof(SettingsMenuController));

            if (previousScene.IsValid())
                EditorSceneManager.SetActiveScene(previousScene);

            CreateCatalog();
            AddMenuScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ToastalRecall] Setup complete. Open Assets/ToastalRecall/Resources/LevelCatalog " +
                      "to assign your nine level scenes.");
            ValidateSetup();
        }

        private static void CreateMenuScene(string sceneName, System.Type controllerType)
        {
            string path = ScenesFolder + "/" + sceneName + ".unity";
            if (File.Exists(path))
            {
                Debug.Log("[ToastalRecall] Keeping existing scene " + path);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UIFactory.BackgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var controllerGo = new GameObject(sceneName + " Controller");
            controllerGo.AddComponent(controllerType);

            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);

            Debug.Log("[ToastalRecall] Created scene " + path);
        }

        private static void CreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log("[ToastalRecall] Created " + CatalogPath);
            }

            catalog.mainMenuSceneName = "MainMenu";
            catalog.levelSelectSceneName = "LevelSelect";
            catalog.settingsSceneName = "Settings";
            catalog.EnsureEntries();

            EditorUtility.SetDirty(catalog);
        }

        private static void AddMenuScenesToBuildSettings()
        {
            var ordered = new List<EditorBuildSettingsScene>();
            string[] menuScenes =
            {
                ScenesFolder + "/MainMenu.unity",
                ScenesFolder + "/LevelSelect.unity",
                ScenesFolder + "/Settings.unity"
            };

            foreach (string path in menuScenes)
            {
                if (File.Exists(path))
                    ordered.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (!ordered.Exists(s => s.path == existing.path))
                    ordered.Add(existing);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        [MenuItem("Tools/Toastal Recall/Validate Setup")]
        public static void ValidateSetup()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[ToastalRecall] Validation: LevelCatalog is missing. Run Setup Project.");
                return;
            }

            catalog.EnsureEntries();

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogWarning("[ToastalRecall] Validation: No URP asset is active. " +
                                 "3D models will not render. Run Tools > Toastal Recall > Setup 3D Rendering.");
            }
            else
            {
                bool has3DRenderer = false;
                foreach (var rendererData in pipeline.rendererDataList)
                {
                    if (rendererData is UniversalRendererData)
                    {
                        has3DRenderer = true;
                        break;
                    }
                }

                if (!has3DRenderer)
                    Debug.LogWarning("[ToastalRecall] Validation: URP asset has no Universal Renderer. " +
                                     "Run Tools > Toastal Recall > Setup 3D Rendering.");
            }

            string[] menuScenes =
            {
                ScenesFolder + "/MainMenu.unity",
                ScenesFolder + "/LevelSelect.unity",
                ScenesFolder + "/Settings.unity"
            };

            foreach (string menuScene in menuScenes)
            {
                if (!File.Exists(menuScene))
                    Debug.LogError("[ToastalRecall] Validation: Missing menu scene " + menuScene);
                else if (!IsInBuildSettings(menuScene))
                    Debug.LogWarning("[ToastalRecall] Validation: Menu scene is not in Build Settings: " + menuScene);
            }

            int assigned = 0;
            for (int i = 0; i < catalog.levels.Length; i++)
            {
                var level = catalog.levels[i];
                if (level == null || !level.IsAssigned)
                    continue;

                assigned++;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(level.scenePath) == null)
                {
                    Debug.LogError($"[ToastalRecall] Validation: Level {i + 1} scene '{level.scenePath}' does not exist.");
                }
                else if (!IsInBuildSettings(level.scenePath))
                {
                    Debug.LogWarning($"[ToastalRecall] Validation: Level {i + 1} scene is not in Build Settings.");
                }
            }

            Debug.Log($"[ToastalRecall] Validation complete. {assigned}/9 levels assigned. " +
                      "Menu scenes and build settings are managed automatically.");
        }

        private static bool IsInBuildSettings(string scenePath)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == scenePath && scene.enabled)
                    return true;
            }
            return false;
        }
    }
}
