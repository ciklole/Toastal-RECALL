using UnityEngine;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Main menu: PLAY / LEVELS / SETTINGS / QUIT.
    /// The Canvas is authored in the scene (Assets/Scenes/MainMenu.unity) so it can
    /// be restyled freely in the Hierarchy. If no Canvas is present, one is built at
    /// runtime as a fallback.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string CanvasName = "Main Menu Canvas";

        [Header("Canvas UI (leave empty to auto-bind / generate)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text statusText;

        [Header("Fallback Text (only used when the Canvas is missing)")]
        [SerializeField] private string gameTitle = "TOASTAL RECALL";
        [SerializeField] private string subtitle = "Launch. Fly. Land.";

        private void Start()
        {
            Time.timeScale = 1f;
            AudioManager.EnsureInstance();
            UIFactory.EnsureEventSystem();

            if (!BindSceneUI())
                BuildFallbackUI();

            WireButtons();
            SetStatus(string.Empty);
        }

        private bool BindSceneUI()
        {
            if (canvas == null)
                canvas = UIReferenceBinder.FindCanvas(CanvasName);
            if (canvas == null)
                return false;

            var root = canvas.transform;
            titleText = UIReferenceBinder.Resolve(titleText, root, "Title");
            subtitleText = UIReferenceBinder.Resolve(subtitleText, root, "Subtitle");
            playButton = UIReferenceBinder.Resolve(playButton, root, "PlayButton");
            levelsButton = UIReferenceBinder.Resolve(levelsButton, root, "LevelsButton");
            settingsButton = UIReferenceBinder.Resolve(settingsButton, root, "SettingsButton");
            quitButton = UIReferenceBinder.Resolve(quitButton, root, "QuitButton");
            statusText = UIReferenceBinder.Resolve(statusText, root, "Status");
            return true;
        }

        private void BuildFallbackUI()
        {
            canvas = UIFactory.CreateCanvas(CanvasName);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            titleText = UIFactory.CreateText(root, "Title", gameTitle, 92, UIFactory.TextColor,
                new Vector2(0f, 330f), new Vector2(1400f, 130f), TextAnchor.MiddleCenter, FontStyle.Bold);

            subtitleText = UIFactory.CreateText(root, "Subtitle", subtitle, 30, UIFactory.MutedTextColor,
                new Vector2(0f, 240f), new Vector2(1200f, 60f));

            playButton = UIFactory.CreateButton(root, "PlayButton", "PLAY", new Vector2(0f, 100f),
                new Vector2(420f, 88f), null, 40);

            levelsButton = UIFactory.CreateButton(root, "LevelsButton", "LEVELS", new Vector2(0f, -20f),
                new Vector2(420f, 88f), null, 40);

            settingsButton = UIFactory.CreateButton(root, "SettingsButton", "SETTINGS", new Vector2(0f, -140f),
                new Vector2(420f, 88f), null, 40);

            quitButton = UIFactory.CreateButton(root, "QuitButton", "QUIT", new Vector2(0f, -260f),
                new Vector2(420f, 88f), null, 40);

            statusText = UIFactory.CreateText(root, "Status", "", 24, UIFactory.MutedTextColor,
                new Vector2(0f, -390f), new Vector2(1400f, 70f));
        }

        private void WireButtons()
        {
            UIReferenceBinder.Bind(playButton, OnPlayPressed);
            UIReferenceBinder.Bind(levelsButton, OnLevelsPressed);
            UIReferenceBinder.Bind(settingsButton, OnSettingsPressed);
            UIReferenceBinder.Bind(quitButton, OnQuitPressed);
        }

        private void OnPlayPressed()
        {
            var levels = LevelManager.EnsureInstance();
            if (levels.TryLoadFirstAssignedLevel(out _))
                return;

            SetStatus("No levels are assigned yet. Opening level select...");
            Debug.LogWarning("[MainMenu] No level scene assigned. Assign one on the LevelCatalog asset.");
            levels.OpenLevelSelect();
        }

        private void OnLevelsPressed()
        {
            LevelManager.EnsureInstance().OpenLevelSelect();
        }

        private void OnSettingsPressed()
        {
            LevelManager.EnsureInstance().OpenSettings();
        }

        private void OnQuitPressed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
