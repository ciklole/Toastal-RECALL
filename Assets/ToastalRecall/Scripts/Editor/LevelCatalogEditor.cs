using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// Friendly scene pickers for the nine level slots. Scene names and paths are
    /// filled automatically; no scene names are hardcoded anywhere.
    /// </summary>
    [CustomEditor(typeof(LevelCatalog))]
    public class LevelCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (LevelCatalog)target;
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Assign your nine level scenes below. Every scene must also be in " +
                "File > Build Profiles > Scene List to load in a build. " +
                "Use the button at the bottom to add assigned scenes automatically.",
                MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("mainMenuSceneName"),
                new GUIContent("Main Menu Scene"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelSelectSceneName"),
                new GUIContent("Level Select Scene"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settingsSceneName"),
                new GUIContent("Settings Scene"));

            EditorGUILayout.Space(8f);

            var levelsProperty = serializedObject.FindProperty("levels");
            if (levelsProperty == null || !levelsProperty.isArray)
            {
                EditorGUILayout.HelpBox("Level list is missing. Re-save this asset.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            for (int i = 0; i < levelsProperty.arraySize; i++)
            {
                var element = levelsProperty.GetArrayElementAtIndex(i);
                var displayName = element.FindPropertyRelative("displayName");
                var sceneName = element.FindPropertyRelative("sceneName");
                var scenePath = element.FindPropertyRelative("scenePath");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Level " + (i + 1), EditorStyles.boldLabel);
                displayName.stringValue = EditorGUILayout.TextField("Button Label", displayName.stringValue);

                var current = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.stringValue);
                var picked = (SceneAsset)EditorGUILayout.ObjectField("Scene", current, typeof(SceneAsset), false);
                if (picked != current)
                {
                    if (picked == null)
                    {
                        sceneName.stringValue = "";
                        scenePath.stringValue = "";
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(picked);
                        scenePath.stringValue = path;
                        sceneName.stringValue = Path.GetFileNameWithoutExtension(path);
                        if (string.IsNullOrWhiteSpace(displayName.stringValue) ||
                            displayName.stringValue == "Level " + (i + 1))
                            displayName.stringValue = "Level " + (i + 1);
                    }
                }

                if (!string.IsNullOrWhiteSpace(scenePath.stringValue))
                {
                    bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == scenePath.stringValue && s.enabled);
                    if (!inBuild)
                    {
                        EditorGUILayout.HelpBox("This scene is not in Build Settings.", MessageType.Warning);
                        if (GUILayout.Button("Add This Scene to Build Settings"))
                            AddSceneToBuildSettings(scenePath.stringValue);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Build Settings", "OK");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Not assigned - the level button will show a warning.", MessageType.None);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Add All Assigned Levels to Build Settings", GUILayout.Height(30f)))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var level in catalog.levels)
                {
                    if (level != null && level.IsAssigned)
                        AddSceneToBuildSettings(level.scenePath);
                }
                AssetDatabase.SaveAssets();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                return;

            var scenes = EditorBuildSettings.scenes.ToList();
            bool found = false;

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != scenePath)
                    continue;

                found = true;
                if (!scenes[i].enabled)
                    scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                break;
            }

            if (!found)
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Toastal Recall/Select Level Catalog")]
        public static void SelectCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("LevelCatalog t:LevelCatalog");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[ToastalRecall] No LevelCatalog found. Run Tools > Toastal Recall > Setup Project.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }
    }
}
