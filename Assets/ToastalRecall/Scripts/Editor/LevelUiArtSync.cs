using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// One-shot sync of the Level 1 completion design into the shared prefab,
    /// followed by updating the respawn screen artwork and level instances.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelUiArtSync
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string SourceScenePath = ScenesFolder + "/Level1.unity";
        private const string CompletionPrefabPath = "Assets/ToastalRecall/Resources/UI/LevelCompleteCanvas.prefab";
        private const string RespawnPrefabPath = "Assets/ToastalRecall/Resources/UI/RespawnCanvas.prefab";
        private const string SentinelPath = "Temp/ToastalRecallUiArtSync.done";
        private const string CompletionCanvasName = "Level Completed Canvas";

        private static bool _queued;

        static LevelUiArtSync()
        {
            if (!_queued)
            {
                _queued = true;
                EditorApplication.delayCall += TryRun;
            }
        }

        private static void TryRun()
        {
            if (File.Exists(SentinelPath))
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            try
            {
                if (Run())
                {
                    Directory.CreateDirectory("Temp");
                    File.WriteAllText(SentinelPath, DateTime.Now.ToString("s"));
                    Debug.Log("[ToastalRecall] UI art sync finished.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ToastalRecall] UI art sync failed: " + e);
            }
        }

        private static bool Run()
        {
            if (!File.Exists(SourceScenePath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(CompletionPrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(RespawnPrefabPath) == null)
            {
                Debug.LogError("[ToastalRecall] UI art sync could not find its source scene or prefabs.");
                return false;
            }

            Scene previousActive = SceneManager.GetActiveScene();
            Scene source = OpenSceneAdditive(SourceScenePath, out bool sourceOpened);
            try
            {
                GameObject completion = FindCompletionInstance(source);
                if (completion == null ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(completion) != CompletionPrefabPath)
                {
                    Debug.LogError("[ToastalRecall] Level 1 has no instance of the expected completion prefab.");
                    return false;
                }

                // Apply all Level 1 design overrides, including added and removed children.
                completion.SetActive(true);
                PrefabUtility.ApplyPrefabInstance(completion, InteractionMode.AutomatedAction);
                completion.name = CompletionCanvasName;
                completion.SetActive(false);
                EditorSceneManager.MarkSceneDirty(source);
                EditorSceneManager.SaveScene(source);
            }
            finally
            {
                RestoreActiveScene(previousActive);
                if (sourceOpened)
                    EditorSceneManager.CloseScene(source, true);
            }

            ApplyRespawnArt();
            UpdateLevelInstances();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static void ApplyRespawnArt()
        {
            Sprite respawnSprite = LoadSprite("Assets/Prefabs/Respawn_button-removebg-preview.png");
            Sprite menuSprite = LoadSprite("Assets/Prefabs/main_menu_button_lol-removebg-preview.png");
            Sprite backgroundSprite = LoadSprite("Assets/Prefabs/bacckground__kinda_eh-removebg-preview.png");
            if (respawnSprite == null || menuSprite == null || backgroundSprite == null)
                throw new InvalidOperationException("One or more Toastal Recall UI sprites could not be loaded.");

            GameObject contents = PrefabUtility.LoadPrefabContents(RespawnPrefabPath);
            try
            {
                Transform panel = FindChild(contents.transform, "Panel");
                if (panel == null)
                    throw new InvalidOperationException("Respawn prefab has no Panel.");

                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.sprite = backgroundSprite;
                    panelImage.color = new Color(0.5245f, 0f, 0f, 1f);
                    panelImage.preserveAspect = false;
                }
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                if (panelRect != null)
                    panelRect.sizeDelta = new Vector2(965f, 770f);

                ConfigureButton(contents.transform, "RespawnButton", respawnSprite,
                    new Vector2(0f, -20f), new Vector2(314f, 129f));
                ConfigureButton(contents.transform, "MenuButton", menuSprite,
                    new Vector2(0f, -170f), new Vector2(354f, 132f));

                SetRect(contents.transform, "Title", new Vector2(0f, 250f), new Vector2(700f, 100f));
                SetRect(contents.transform, "Subtitle", new Vector2(0f, 160f), new Vector2(680f, 50f));
                SetRect(contents.transform, "Status", new Vector2(0f, -280f), new Vector2(680f, 50f));

                PrefabUtility.SaveAsPrefabAsset(contents, RespawnPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureButton(Transform root, string name, Sprite sprite,
            Vector2 position, Vector2 size)
        {
            Transform child = FindChild(root, name);
            if (child == null)
                throw new InvalidOperationException("Respawn prefab has no " + name + ".");

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
            }

            Button button = child.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = image;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                button.colors = colors;
            }

            Transform label = FindChild(child, "Label");
            if (label != null)
                label.gameObject.SetActive(false);

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
        }

        private static void SetRect(Transform root, string name, Vector2 position, Vector2 size)
        {
            Transform child = FindChild(root, name);
            if (child == null)
                return;
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
        }

        private static void UpdateLevelInstances()
        {
            string[] paths = Directory.GetFiles(ScenesFolder, "Level*.unity");
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            Scene previousActive = SceneManager.GetActiveScene();
            foreach (string path in paths)
            {
                Scene scene = OpenSceneAdditive(path, out bool opened);
                try
                {
                    GameObject instance = FindCompletionInstance(scene);
                    if (instance == null ||
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != CompletionPrefabPath)
                        continue;

                    PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
                    instance = FindCompletionInstance(scene);
                    if (instance != null)
                    {
                        instance.name = CompletionCanvasName;
                        instance.SetActive(false);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    RestoreActiveScene(previousActive);
                    if (opened)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Scene OpenSceneAdditive(string path, out bool opened)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        private static void RestoreActiveScene(Scene previous)
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }

        private static GameObject FindCompletionInstance(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == CompletionCanvasName)
                    return root;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.name == CompletionCanvasName)
                        return canvas.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        private static Sprite LoadSprite(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                    return sprite;
            return null;
        }
    }
}
