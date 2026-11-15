using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToastalRecall
{
    /// <summary>
    /// Single source of truth for level flow. Auto-creates itself so gameplay
    /// scenes and menu scenes can be played directly from the editor.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        /// <summary>Raised after a scene loads. Value is the matching level index or -1.</summary>
        public static event Action<int> LevelLoaded;

        public LevelCatalog Catalog { get; private set; }
        public int CurrentLevelIndex { get; private set; } = -1;

        public bool HasNextLevel =>
            Catalog != null &&
            CurrentLevelIndex >= 0 &&
            CurrentLevelIndex < Catalog.levels.Length - 1 &&
            Catalog.levels[CurrentLevelIndex + 1] != null &&
            Catalog.levels[CurrentLevelIndex + 1].IsAssigned;

        public bool IsCurrentLevelFinal =>
            CurrentLevelIndex >= 0 &&
            Catalog != null &&
            CurrentLevelIndex >= Catalog.levels.Length - 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static LevelManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindAnyObjectByType<LevelManager>();
            if (existing != null)
                return existing;

            var go = new GameObject("LevelManager");
            return go.AddComponent<LevelManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCatalog();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        private void LoadCatalog()
        {
            Catalog = Resources.Load<LevelCatalog>(LevelCatalog.ResourcePath);

            if (Catalog == null)
            {
                Debug.LogWarning(
                    "[LevelManager] No LevelCatalog found in a Resources folder. " +
                    "Using a temporary catalog. Run Tools > Toastal Recall > Setup Project to create one.");
                Catalog = LevelCatalog.CreateRuntimeDefault();
            }

            Catalog.EnsureEntries();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Catalog == null)
                return;

            if (scene.name == Catalog.mainMenuSceneName ||
                scene.name == Catalog.levelSelectSceneName ||
                scene.name == Catalog.settingsSceneName)
            {
                LevelLoaded?.Invoke(-1);
                return;
            }

            int index = Catalog.FindLevelIndexBySceneName(scene.name);
            if (index >= 0)
                CurrentLevelIndex = index;

            LevelLoaded?.Invoke(index);
        }

        public bool LoadLevel(int index)
        {
            if (Catalog == null)
                return false;

            if (index < 0 || index >= Catalog.levels.Length)
            {
                Debug.LogError($"[LevelManager] Level index {index} is out of range (0-{Catalog.levels.Length - 1}).");
                return false;
            }

            var level = Catalog.levels[index];
            if (level == null || !level.IsAssigned)
            {
                Debug.LogError(
                    $"[LevelManager] Level {index + 1} has no scene assigned. " +
                    "Assign it on the LevelCatalog asset (Resources/LevelCatalog).");
                return false;
            }

            if (!IsSceneUsable(level.scenePath, level.sceneName))
                return false;

            CurrentLevelIndex = index;
            SceneManager.LoadScene(level.sceneName, LoadSceneMode.Single);
            return true;
        }

        public bool TryLoadFirstAssignedLevel(out int index)
        {
            index = -1;
            if (Catalog == null)
                return false;

            for (int i = 0; i < Catalog.levels.Length; i++)
            {
                if (Catalog.levels[i] != null && Catalog.levels[i].IsAssigned)
                {
                    index = i;
                    return LoadLevel(i);
                }
            }

            return false;
        }

        public bool RestartCurrentLevel()
        {
            if (CurrentLevelIndex >= 0 && Catalog != null &&
                CurrentLevelIndex < Catalog.levels.Length)
            {
                return LoadLevel(CurrentLevelIndex);
            }

            // Played a level scene directly without it being in the catalog.
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.name))
            {
                SceneManager.LoadScene(active.name, LoadSceneMode.Single);
                return true;
            }

            return false;
        }

        public bool LoadNextLevel()
        {
            if (!HasNextLevel)
            {
                Debug.LogWarning("[LevelManager] There is no next assigned level.");
                return false;
            }

            return LoadLevel(CurrentLevelIndex + 1);
        }

        public void ReturnToMainMenu()
        {
            LoadMenuScene(Catalog != null ? Catalog.mainMenuSceneName : "MainMenu", "main menu");
        }

        public void OpenLevelSelect()
        {
            LoadMenuScene(Catalog != null ? Catalog.levelSelectSceneName : "LevelSelect", "level select");
        }

        public void OpenSettings()
        {
            LoadMenuScene(Catalog != null ? Catalog.settingsSceneName : "Settings", "settings");
        }

        private void LoadMenuScene(string sceneName, string label)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"[LevelManager] No {label} scene name configured in the LevelCatalog.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[LevelManager] Cannot load the {label} scene '{sceneName}'. " +
                    "Make sure it exists and is enabled in Build Settings.");
                return;
            }

            CurrentLevelIndex = -1;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private static bool IsSceneUsable(string scenePath, string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(scenePath) &&
                SceneUtility.GetBuildIndexByScenePath(scenePath) < 0)
            {
                Debug.LogError(
                    $"[LevelManager] Scene '{sceneName}' is assigned but is not in Build Settings. " +
                    "Add it via File > Build Profiles > Scene List (or the LevelCatalog inspector button).");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[LevelManager] Scene '{sceneName}' cannot be loaded. " +
                    "Make sure it exists and is enabled in Build Settings.");
                return false;
            }

            return true;
        }
    }
}
