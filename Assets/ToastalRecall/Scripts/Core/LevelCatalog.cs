using System;
using UnityEngine;

namespace ToastalRecall
{
    /// <summary>
    /// Central list of playable levels. Assign each level scene in the Inspector
    /// (or use the custom editor on this asset). Menu scenes are configured here too.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Toastal Recall/Level Catalog")]
    public class LevelCatalog : ScriptableObject
    {
        public const int LevelCount = 9;
        public const string ResourcePath = "LevelCatalog";

        [Serializable]
        public class LevelDefinition
        {
            [Tooltip("Optional label shown on the level-select button.")]
            public string displayName = "";

            [Tooltip("Scene name used by SceneManager. Filled automatically by the custom inspector.")]
            public string sceneName = "";

            [Tooltip("Full asset path, used to verify the scene is in Build Settings.")]
            public string scenePath = "";

            public bool IsAssigned =>
                !string.IsNullOrWhiteSpace(sceneName) && !string.IsNullOrWhiteSpace(scenePath);
        }

        [Header("Menu Scenes")]
        public string mainMenuSceneName = "MainMenu";
        public string levelSelectSceneName = "LevelSelect";
        public string settingsSceneName = "Settings";

        [Header("Levels (1-9)")]
        public LevelDefinition[] levels = new LevelDefinition[LevelCount];

        private void OnValidate()
        {
            EnsureEntries();
        }

        /// <summary>Guarantees exactly nine entries with sensible defaults.</summary>
        public void EnsureEntries()
        {
            if (levels == null || levels.Length != LevelCount)
            {
                var resized = new LevelDefinition[LevelCount];
                if (levels != null)
                {
                    int copy = Mathf.Min(levels.Length, LevelCount);
                    for (int i = 0; i < copy; i++)
                        resized[i] = levels[i];
                }
                levels = resized;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                    levels[i] = new LevelDefinition();
                if (string.IsNullOrWhiteSpace(levels[i].displayName))
                    levels[i].displayName = "Level " + (i + 1);
            }
        }

        public static LevelCatalog CreateRuntimeDefault()
        {
            var catalog = CreateInstance<LevelCatalog>();
            catalog.EnsureEntries();
            return catalog;
        }

        public int FindLevelIndexBySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || levels == null)
                return -1;

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null && levels[i].sceneName == sceneName)
                    return i;
            }
            return -1;
        }
    }
}
