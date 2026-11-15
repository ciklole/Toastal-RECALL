using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ToastalRecall.EditorTools
{
    /// <summary>Builds the runtime music map from the scene-named folders in Assets/Music.</summary>
    [InitializeOnLoad]
    public static class MusicCatalogBuilder
    {
        private const string MusicRoot = "Assets/Music";
        private const string CatalogPath = "Assets/ToastalRecall/Resources/MusicCatalog.asset";

        static MusicCatalogBuilder()
        {
            EditorApplication.delayCall += CreateCatalogIfMissing;
        }

        private static void CreateCatalogIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CreateCatalogIfMissing;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath) == null)
                RebuildCatalog();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += CreateCatalogIfMissing;
        }

        [MenuItem("Tools/Toastal Recall/Audio/Rebuild Music Catalog")]
        public static void RebuildCatalog()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ToastalRecall/Resources"))
                AssetDatabase.CreateFolder("Assets/ToastalRecall", "Resources");

            MusicCatalog catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MusicCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var menuTracks = new List<AudioClip>();
            var scenePlaylists = new List<MusicCatalog.ScenePlaylist>();

            if (Directory.Exists(MusicRoot))
            {
                foreach (string folderPath in Directory.GetDirectories(MusicRoot).OrderBy(path => path,
                             StringComparer.OrdinalIgnoreCase))
                {
                    string folderName = Path.GetFileName(folderPath);
                    AudioClip[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                        .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                        .Where(clip => clip != null)
                        .ToArray();

                    if (string.Equals(NormalizeSceneName(folderName), catalog.mainMenuSceneName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        menuTracks.AddRange(clips);
                        continue;
                    }

                    scenePlaylists.Add(new MusicCatalog.ScenePlaylist
                    {
                        sceneName = NormalizeSceneName(folderName),
                        tracks = clips
                    });
                }
            }

            catalog.mainMenuTracks = menuTracks.ToArray();
            catalog.mainMenuMusicSceneNames = new[] { "LevelSelect", "Settings" };
            catalog.scenePlaylists = scenePlaylists.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ToastalRecall] Built music catalog: {scenePlaylists.Count} scene folders, " +
                      $"{menuTracks.Count} main menu tracks.");
        }

        private static string NormalizeSceneName(string folderName)
        {
            return new string(folderName.Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
