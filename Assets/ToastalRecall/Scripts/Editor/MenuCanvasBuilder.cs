using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// Bakes the menus into real, freely editable Canvas hierarchies inside their
    /// scenes and generates editable Canvas prefabs for the gameplay UI.
    ///
    /// It runs automatically once after the scripts compile, and it is idempotent:
    /// a scene that already contains its marked Canvas is left untouched. Use
    /// Tools > Toastal Recall > UI > Rebuild All UI to replace the baked layout.
    /// </summary>
    [InitializeOnLoad]
    public static class MenuCanvasBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string UiPrefabFolder = "Assets/ToastalRecall/Resources/UI";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        private const string LevelSelectScenePath = ScenesFolder + "/LevelSelect.unity";
        private const string SettingsScenePath = ScenesFolder + "/Settings.unity";
        private const string SampleScenePath = ScenesFolder + "/SampleScene.unity";
        private const string GameplayHudPrefabPath = UiPrefabFolder + "/GameplayHUDCanvas.prefab";
        private const string LevelCompletePrefabPath = UiPrefabFolder + "/LevelCompleteCanvas.prefab";
        private const string RespawnPrefabPath = UiPrefabFolder + "/RespawnCanvas.prefab";

        private const string GameplayHudCanvasName = "Gameplay HUD Canvas";
        private const string LevelCompleteCanvasName = "Level Completed Canvas";
        private const string RespawnCanvasName = "Respawn Canvas";

        private const string MainMenuLayoutId = "main-menu";
        private const string LevelSelectLayoutId = "level-select";
        private const string SettingsLayoutId = "settings";

        private static int _autoBuildRetries;

        static MenuCanvasBuilder()
        {
            EditorApplication.delayCall += AutoBuild;
        }

        private static void AutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (_autoBuildRetries++ < 180)
                    EditorApplication.delayCall += AutoBuild;
                return;
            }

            _autoBuildRetries = 0;
            BuildAll(false);
        }

        [MenuItem("Tools/Toastal Recall/UI/Rebuild All UI (Canvases + Prefabs)", priority = 20)]
        public static void RebuildAll()
        {
            BuildAll(true);
        }

        [MenuItem("Tools/Toastal Recall/UI/Open Main Menu Scene", priority = 21)]
        public static void OpenMainMenuScene()
        {
            if (File.Exists(MainMenuScenePath))
                EditorSceneManager.OpenScene(MainMenuScenePath);
        }

        [MenuItem("Tools/Toastal Recall/UI/Reveal UI Prefabs", priority = 22)]
        public static void RevealUiPrefabs()
        {
            EnsurePrefabFolder();
            EditorUtility.RevealInFinder(UiPrefabFolder);
        }

        [MenuItem("Tools/Toastal Recall/UI/Open Respawn Canvas Prefab", priority = 23)]
        public static void OpenRespawnPrefab()
        {
            BuildRespawnPrefab(false);
            OpenPrefabAsset(RespawnPrefabPath);
        }

        [MenuItem("Tools/Toastal Recall/UI/Open Gameplay HUD Prefab", priority = 24)]
        public static void OpenGameplayHudPrefab()
        {
            BuildGameplayHudPrefab(false);
            OpenPrefabAsset(GameplayHudPrefabPath);
        }

        [MenuItem("Tools/Toastal Recall/UI/Open Level Complete Prefab", priority = 25)]
        public static void OpenLevelCompletePrefab()
        {
            BuildLevelCompletePrefab(false);
            OpenPrefabAsset(LevelCompletePrefabPath);
        }

        [MenuItem("Tools/Toastal Recall/UI/Add Gameplay UI To Active Scene", priority = 26)]
        public static void AddGameplayUiToActiveScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ToastalRecall] Stop Play Mode before adding UI to a scene.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[ToastalRecall] Save the scene before adding gameplay UI.");
                return;
            }

            EnsurePrefabFolder();
            BuildGameplayHudPrefab(false);
            BuildRespawnPrefab(false);
            BuildLevelCompletePrefab(false);

            if (!PlaceGameplayUi(scene))
            {
                Debug.Log("[ToastalRecall] Gameplay UI is already in " + scene.name + ".");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ToastalRecall] Added editable Gameplay HUD, Respawn and Level Complete Canvases to " + scene.name + ".");
        }

        [MenuItem("Tools/Toastal Recall/UI/Add Gameplay UI To All Catalog Levels", priority = 27)]
        public static void AddGameplayUiToAllLevels()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ToastalRecall] Stop Play Mode before adding UI to scenes.");
                return;
            }

            var catalog = FindCatalog();
            if (catalog == null)
            {
                Debug.LogWarning("[ToastalRecall] No LevelCatalog found.");
                return;
            }

            catalog.EnsureEntries();
            EnsurePrefabFolder();
            BuildGameplayHudPrefab(false);
            BuildRespawnPrefab(false);
            BuildLevelCompletePrefab(false);

            int changed = 0;
            foreach (var level in catalog.levels)
            {
                if (level == null || !level.IsAssigned)
                    continue;

                if (PlaceGameplayUiInScene(level.scenePath))
                    changed++;
            }

            Debug.Log("[ToastalRecall] Added gameplay UI to " + changed + " level scene(s).");
        }

        public static void BuildAll(bool force)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ToastalRecall] Stop Play Mode before rebuilding UI.");
                return;
            }

            EnsurePrefabFolder();

            bool changed = false;
            changed |= BuildMenuScene(MainMenuScenePath, MainMenuLayoutId, BuildMainMenu, force);
            changed |= BuildMenuScene(LevelSelectScenePath, LevelSelectLayoutId, BuildLevelSelect, force);
            changed |= BuildMenuScene(SettingsScenePath, SettingsLayoutId, BuildSettings, force);
            changed |= BuildGameplayHudPrefab(force);
            changed |= BuildLevelCompletePrefab(force);
            changed |= BuildRespawnPrefab(force);
            changed |= PlaceGameplayUiInScene(SampleScenePath);

            if (!changed)
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ToastalRecall] Editable UI is ready. Select a Canvas in the Hierarchy to restyle it.");
        }

        // ------------------------------------------------------------------ scenes

        private static bool BuildMenuScene(string scenePath, string layoutId,
            Action<Scene> build, bool force)
        {
            if (!File.Exists(scenePath))
                return false;

            if (!force && !SceneNeedsBake(scenePath, layoutId))
                return false;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            Scene previousActive = SceneManager.GetActiveScene();
            bool changed = false;

            try
            {
                SceneManager.SetActiveScene(scene);

                var existing = FindBakedCanvas(scene, layoutId);
                if (existing != null)
                {
                    if (!force)
                        return false;

                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                EnsureEventSystem(scene);
                build(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed = true;
                Debug.Log("[ToastalRecall] Baked editable Canvas into " + scenePath);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);

                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return changed;
        }

        private static void BuildMainMenu(Scene scene)
        {
            var controller = FindComponent<MainMenuController>(scene);
            string title = ReadString(controller, "gameTitle", "TOASTAL RECALL");
            string subtitle = ReadString(controller, "subtitle", "Launch. Fly. Land.");

            var canvas = CreateCanvas("Main Menu Canvas", 0, MainMenuLayoutId);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            var titleText = UIFactory.CreateText(root, "Title", title, 92, UIFactory.TextColor,
                new Vector2(0f, 330f), new Vector2(1400f, 130f), TextAnchor.MiddleCenter, FontStyle.Bold);

            var subtitleText = UIFactory.CreateText(root, "Subtitle", subtitle, 30, UIFactory.MutedTextColor,
                new Vector2(0f, 240f), new Vector2(1200f, 60f));

            var playButton = UIFactory.CreateButton(root, "PlayButton", "PLAY", new Vector2(0f, 100f),
                new Vector2(420f, 88f), null, 40);

            var levelsButton = UIFactory.CreateButton(root, "LevelsButton", "LEVELS", new Vector2(0f, -20f),
                new Vector2(420f, 88f), null, 40);

            var settingsButton = UIFactory.CreateButton(root, "SettingsButton", "SETTINGS", new Vector2(0f, -140f),
                new Vector2(420f, 88f), null, 40);

            var quitButton = UIFactory.CreateButton(root, "QuitButton", "QUIT", new Vector2(0f, -260f),
                new Vector2(420f, 88f), null, 40);

            var statusText = UIFactory.CreateText(root, "Status", string.Empty, 24, UIFactory.MutedTextColor,
                new Vector2(0f, -390f), new Vector2(1400f, 70f));

            if (controller == null)
                return;

            var so = new SerializedObject(controller);
            SetObject(so, "canvas", canvas);
            SetObject(so, "titleText", titleText);
            SetObject(so, "subtitleText", subtitleText);
            SetObject(so, "playButton", playButton);
            SetObject(so, "levelsButton", levelsButton);
            SetObject(so, "settingsButton", settingsButton);
            SetObject(so, "quitButton", quitButton);
            SetObject(so, "statusText", statusText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLevelSelect(Scene scene)
        {
            var canvas = CreateCanvas("Level Select Canvas", 0, LevelSelectLayoutId);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            var titleText = UIFactory.CreateText(root, "Title", "SELECT A LEVEL", 72, UIFactory.TextColor,
                new Vector2(0f, 390f), new Vector2(1400f, 100f), TextAnchor.MiddleCenter, FontStyle.Bold);

            Vector2[] positions =
            {
                new Vector2(-450f, 190f), new Vector2(0f, 190f), new Vector2(450f, 190f),
                new Vector2(-450f, 0f),   new Vector2(0f, 0f),   new Vector2(450f, 0f),
                new Vector2(-450f, -190f),new Vector2(0f, -190f),new Vector2(450f, -190f)
            };

            var catalog = FindCatalog();
            string[] labels = BuildLevelLabels(catalog);

            var levelButtons = new Button[9];
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i] = UIFactory.CreateButton(root, "Level" + (i + 1), labels[i],
                    positions[i], new Vector2(390f, 125f), null, 32);
            }

            var statusText = UIFactory.CreateText(root, "Status", string.Empty, 25, UIFactory.MutedTextColor,
                new Vector2(0f, -310f), new Vector2(1300f, 60f));

            var backButton = UIFactory.CreateButton(root, "BackButton", "BACK", new Vector2(0f, -410f),
                new Vector2(300f, 76f), null, 32);

            var controller = FindComponent<LevelSelectionController>(scene);
            if (controller == null)
                return;

            var so = new SerializedObject(controller);
            SetObject(so, "canvas", canvas);
            SetObject(so, "titleText", titleText);
            SetObject(so, "statusText", statusText);
            SetObject(so, "backButton", backButton);

            var buttonsProperty = so.FindProperty("levelButtons");
            if (buttonsProperty != null)
            {
                buttonsProperty.arraySize = levelButtons.Length;
                for (int i = 0; i < levelButtons.Length; i++)
                    buttonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = levelButtons[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSettings(Scene scene)
        {
            var canvas = CreateCanvas("Settings Canvas", 0, SettingsLayoutId);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            UIFactory.CreateText(root, "Title", "SETTINGS", 76, UIFactory.TextColor,
                new Vector2(0f, 380f), new Vector2(1200f, 110f), TextAnchor.MiddleCenter, FontStyle.Bold);

            var masterSlider = CreateVolumeSlider(root, "Master Volume", 150f, out var masterValue);
            var musicSlider = CreateVolumeSlider(root, "Music Volume", 20f, out var musicValue);
            var sfxSlider = CreateVolumeSlider(root, "Sound Effects Volume", -110f, out var sfxValue);

            UIFactory.CreateText(root, "Note", "Settings are saved automatically.",
                22, UIFactory.MutedTextColor, new Vector2(0f, -250f), new Vector2(1000f, 50f));

            var backButton = UIFactory.CreateButton(root, "BackButton", "BACK", new Vector2(0f, -370f),
                new Vector2(300f, 76f), null, 32);

            var controller = FindComponent<SettingsMenuController>(scene);
            if (controller == null)
                return;

            var so = new SerializedObject(controller);
            SetObject(so, "canvas", canvas);
            SetObject(so, "backButton", backButton);
            SetObject(so, "masterSlider", masterSlider);
            SetObject(so, "musicSlider", musicSlider);
            SetObject(so, "sfxSlider", sfxSlider);
            SetObject(so, "masterValueLabel", masterValue);
            SetObject(so, "musicValueLabel", musicValue);
            SetObject(so, "sfxValueLabel", sfxValue);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Slider CreateVolumeSlider(Transform root, string label, float y, out Text valueLabel)
        {
            valueLabel = UIFactory.CreateText(root, label + "Value", "100%",
                24, UIFactory.MutedTextColor, new Vector2(310f, y + 28f), new Vector2(120f, 40f),
                TextAnchor.MiddleRight);

            return UIFactory.CreateSlider(root, label + "Slider", label, new Vector2(0f, y),
                new Vector2(720f, 100f), 1f, null);
        }

        // ----------------------------------------------------------------- prefabs

        private static bool BuildGameplayHudPrefab(bool force)
        {
            if (File.Exists(GameplayHudPrefabPath) && !force)
                return false;

            BuildPrefab(GameplayHudPrefabPath, () =>
            {
                var canvas = CreateCanvas("Gameplay HUD Canvas", 50, null);
                var root = canvas.transform;

                var respawnButton = UIFactory.CreateButton(root, "RespawnButton", "RESPAWN",
                    Vector2.zero, new Vector2(210f, 64f), null, 26);
                AnchorTopLeft(respawnButton.GetComponent<RectTransform>(), new Vector2(24f, -24f));

                var menuButton = UIFactory.CreateButton(root, "MenuButton", "MENU",
                    Vector2.zero, new Vector2(150f, 64f), null, 26);
                AnchorTopRight(menuButton.GetComponent<RectTransform>(), new Vector2(-24f, -24f));

                var levelLabel = UIFactory.CreateText(root, "LevelLabel", "TOASTAL RECALL", 24,
                    UIFactory.MutedTextColor, Vector2.zero, new Vector2(400f, 50f));
                AnchorTopCenter(levelLabel.rectTransform, new Vector2(0f, -42f));

                return canvas.gameObject;
            });

            return true;
        }

        private static bool BuildLevelCompletePrefab(bool force)
        {
            if (File.Exists(LevelCompletePrefabPath) && !force)
                return false;

            BuildPrefab(LevelCompletePrefabPath, () =>
            {
                var canvas = CreateCanvas("Level Completed Canvas", 1000, null);                var background = UIFactory.CreatePanel(canvas.transform, "Overlay",
                    new Color(0.05f, 0.03f, 0.02f, 0.86f));

                var panel = UIFactory.CreatePanel(background, "Panel", UIFactory.PanelColor);
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(760f, 560f);

                UIFactory.CreateText(panel, "Title", "LEVEL COMPLETED!", 64,
                    UIFactory.TextColor, new Vector2(0f, 200f), new Vector2(700f, 100f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);

                UIFactory.CreateText(panel, "Subtitle", "Toasty landing!", 28,
                    UIFactory.MutedTextColor, new Vector2(0f, 130f), new Vector2(680f, 50f));

                UIFactory.CreateButton(panel, "ReplayButton", "REPLAY", new Vector2(0f, 35f),
                    new Vector2(500f, 78f), null, 30);

                UIFactory.CreateButton(panel, "NextButton", "NEXT LEVEL", new Vector2(0f, -65f),
                    new Vector2(500f, 78f), null, 30);

                UIFactory.CreateButton(panel, "MenuButton", "MAIN MENU", new Vector2(0f, -165f),
                    new Vector2(500f, 78f), null, 30);

                UIFactory.CreateText(panel, "Status", string.Empty, 22,
                    new Color(1f, 0.78f, 0.35f, 1f), new Vector2(0f, -235f),
                    new Vector2(680f, 50f));

                return canvas.gameObject;
            });

            return true;
        }

        private static bool BuildRespawnPrefab(bool force)
        {
            if (File.Exists(RespawnPrefabPath) && !force)
                return false;

            BuildPrefab(RespawnPrefabPath, () =>
            {
                var canvas = CreateCanvas("Respawn Canvas", 900, null);
                var overlay = UIFactory.CreatePanel(canvas.transform, "Overlay",
                    new Color(0.05f, 0.03f, 0.02f, 0.82f));

                var panel = UIFactory.CreatePanel(overlay, "Panel", UIFactory.PanelColor);
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(760f, 520f);

                UIFactory.CreateText(panel, "Title", "TOASTED!", 72,
                    UIFactory.TextColor, new Vector2(0f, 165f), new Vector2(700f, 110f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);

                UIFactory.CreateText(panel, "Subtitle", "The bread didn't make it.", 28,
                    UIFactory.MutedTextColor, new Vector2(0f, 90f), new Vector2(680f, 50f));

                UIFactory.CreateButton(panel, "RespawnButton", "RESPAWN", new Vector2(0f, -5f),
                    new Vector2(500f, 82f), null, 32);

                UIFactory.CreateButton(panel, "MenuButton", "MAIN MENU", new Vector2(0f, -115f),
                    new Vector2(500f, 82f), null, 30);

                UIFactory.CreateText(panel, "Status", string.Empty, 22,
                    new Color(1f, 0.78f, 0.35f, 1f), new Vector2(0f, -210f),
                    new Vector2(680f, 50f));

                return canvas.gameObject;
            });

            return true;
        }

        private static void BuildPrefab(string path, Func<GameObject> build)
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = null;

            try
            {
                SceneManager.SetActiveScene(temp);
                root = build();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);

                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);

                EditorSceneManager.CloseScene(temp, true);
            }

            AssetDatabase.ImportAsset(path);
            Debug.Log("[ToastalRecall] Created editable UI prefab " + path);
        }

        // -------------------------------------------------------------- level scenes

        /// <summary>
        /// Instantiates the gameplay UI prefabs into SampleScene as real, editable
        /// prefab instances. The Respawn and Level Complete Canvases start inactive
        /// so nothing overlaps until they are needed.
        /// </summary>
        private static bool PlaceGameplayUiInScene(string scenePath)
        {
            if (!File.Exists(scenePath))
                return false;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            Scene previousActive = SceneManager.GetActiveScene();
            bool changed = false;

            try
            {
                SceneManager.SetActiveScene(scene);
                changed = PlaceGameplayUi(scene);
                changed |= CameraWallSetup.MarkRoomObjects(scene) > 0;

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[ToastalRecall] Updated gameplay UI and camera walls in " + scenePath);
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);

                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return changed;
        }

        private static bool PlaceGameplayUi(Scene scene)
        {
            bool changed = false;

            changed |= EnsureScenePrefab(scene, GameplayHudPrefabPath, GameplayHudCanvasName, true);
            changed |= EnsureScenePrefab(scene, RespawnPrefabPath, RespawnCanvasName, false);
            changed |= EnsureScenePrefab(scene, LevelCompletePrefabPath, LevelCompleteCanvasName, false);

            // Even if the Canvases already exist, the scene still needs an EventSystem.
            changed |= EnsureEventSystem(scene);
            return changed;
        }

        private static bool EnsureScenePrefab(Scene scene, string prefabPath,
            string instanceName, bool visible)
        {
            var existing = FindSceneObject(scene, instanceName);
            if (existing != null)
            {
                if (existing.activeSelf == visible)
                    return false;

                existing.SetActive(visible);
                return true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = instanceName;
            instance.SetActive(visible);
            return true;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;

                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.name == objectName)
                        return canvas.gameObject;
                }
            }

            return null;
        }

        private static void OpenPrefabAsset(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[ToastalRecall] Could not find " + path);
                return;
            }

            AssetDatabase.OpenAsset(prefab);
        }

        // ----------------------------------------------------------------- helpers

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ToastalRecall/Resources"))
                AssetDatabase.CreateFolder("Assets/ToastalRecall", "Resources");

            if (!AssetDatabase.IsValidFolder(UiPrefabFolder))
                AssetDatabase.CreateFolder("Assets/ToastalRecall/Resources", "UI");
        }

        private static Canvas CreateCanvas(string name, int sortingOrder, string layoutId)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                go.layer = uiLayer;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (!string.IsNullOrEmpty(layoutId))
            {
                var marker = go.AddComponent<EditableUICanvas>();
                marker.layoutId = layoutId;
            }

            return canvas;
        }

        private static bool SceneNeedsBake(string scenePath, string layoutId)
        {
            try
            {
                return !File.ReadAllText(scenePath).Contains("layoutId: " + layoutId);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static GameObject FindBakedCanvas(Scene scene, string layoutId)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var markers = root.GetComponentsInChildren<EditableUICanvas>(true);
                foreach (var marker in markers)
                {
                    if (marker != null && marker.layoutId == layoutId)
                        return marker.gameObject;
                }
            }

            return null;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static bool EnsureEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var eventSystem = root.GetComponentInChildren<EventSystem>(true);
                if (eventSystem == null)
                    continue;

#if ENABLE_INPUT_SYSTEM
                if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                {
                    var legacy = eventSystem.GetComponent<StandaloneInputModule>();
                    if (legacy != null)
                        legacy.enabled = false;

                    eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    return true;
                }
#endif
                return false;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            SceneManager.MoveGameObjectToScene(go, scene);
            return true;
        }

        private static LevelCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelCatalog");
            foreach (string guid in guids)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null)
                    return catalog;
            }

            return null;
        }

        private static string[] BuildLevelLabels(LevelCatalog catalog)
        {
            var labels = new string[9];
            if (catalog != null)
                catalog.EnsureEntries();

            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = "LEVEL " + (i + 1);

                if (catalog == null || i >= catalog.levels.Length)
                    continue;

                var entry = catalog.levels[i];
                if (entry == null || !entry.IsAssigned)
                {
                    labels[i] += "\n(Not Assigned)";
                }
                else if (!string.IsNullOrWhiteSpace(entry.displayName) &&
                         entry.displayName != "Level " + (i + 1))
                {
                    labels[i] += "\n" + entry.displayName;
                }
            }

            return labels;
        }

        private static string ReadString(UnityEngine.Object target, string property, string fallback)
        {
            if (target == null)
                return fallback;

            var so = new SerializedObject(target);
            var serialized = so.FindProperty(property);
            if (serialized == null || string.IsNullOrEmpty(serialized.stringValue))
                return fallback;

            return serialized.stringValue;
        }

        private static void SetObject(SerializedObject so, string property, UnityEngine.Object value)
        {
            var serialized = so.FindProperty(property);
            if (serialized != null)
                serialized.objectReferenceValue = value;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorTopCenter(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }
    }
}
