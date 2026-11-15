using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Reusable level-completed overlay. Uses the editable Canvas prefab at
    /// Resources/UI/LevelCompleteCanvas when present, can bind a Canvas placed in
    /// the level, and falls back to a generated Canvas as a last resort.
    /// </summary>
    public class LevelCompletionManager : MonoBehaviour
    {
        private const string CanvasName = "Level Completed Canvas";
        private const string PrefabCanvasName = "LevelCompleteCanvas";
        private const string PrefabResourcePath = "UI/LevelCompleteCanvas";

        private static LevelCompletionManager _instance;

        public static LevelCompletionManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindAnyObjectByType<LevelCompletionManager>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject("LevelCompletionManager");
                _instance = go.AddComponent<LevelCompletionManager>();
                return _instance;
            }
        }

        [Header("Messages")]
        [SerializeField] private string completedTitle = "LEVEL COMPLETED!";
        [SerializeField] private string finalTitle = "ALL LEVELS COMPLETE!";
        [SerializeField] private string completedSubtitle = "Toasty landing!";
        [SerializeField] private string finalSubtitle = "You cleared every level.";

        [Header("Editable UI (optional)")]
        [Tooltip("Canvas prefab to instantiate. Falls back to Resources/UI/LevelCompleteCanvas.")]
        [SerializeField] private GameObject canvasPrefab;

        private Canvas _canvas;
        private Text _title;
        private Text _subtitle;
        private Text _status;
        private Button _replayButton;
        private Button _nextButton;
        private Button _menuButton;
        private Text _nextLabel;
        private bool _completed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUI();
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void CompleteLevel()
        {
            BeginCompletion(0f);
        }

        /// <summary>
        /// Starts the completion flow. When <paramref name="settleSeconds"/> is
        /// greater than zero the bread keeps simulating as a normal Rigidbody for
        /// that long, then gameplay freezes and the overlay appears.
        /// </summary>
        public void BeginCompletion(float settleSeconds)
        {
            if (_completed)
                return;

            _completed = true;

            if (settleSeconds <= 0.01f)
                ShowCompletion();
            else
                StartCoroutine(CompleteAfterDelay(settleSeconds));
        }

        private IEnumerator CompleteAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ShowCompletion();
        }

        private void ShowCompletion()
        {
            RespawnScreen.HideIfShowing();
            StopBread();

            var levelManager = LevelManager.EnsureInstance();
            bool isFinal = levelManager.IsCurrentLevelFinal;
            bool hasNext = levelManager.HasNextLevel;

            SetText(_title, isFinal ? finalTitle : completedTitle);
            SetText(_subtitle, isFinal ? finalSubtitle : completedSubtitle);
            SetText(_status, string.Empty);

            if (_nextButton != null)
                _nextButton.interactable = isFinal || hasNext;

            SetText(_nextLabel, isFinal ? "MAIN MENU" : (hasNext ? "NEXT LEVEL" : "NEXT LEVEL (NOT ASSIGNED)"));
            if (!isFinal && !hasNext)
                SetText(_status, "Assign the next level scene on the LevelCatalog asset.");

            _canvas.gameObject.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void StopBread()
        {
            var respawn = FindAnyObjectByType<BreadRespawnController>();
            if (respawn != null)
            {
                respawn.DisableGameplay();
                return;
            }

            var launch = FindAnyObjectByType<BreadLaunchController>();
            if (launch != null)
                launch.DisableGameplay();

            GameplayHUD.SetVisible(false);
        }

        private void BuildUI()
        {
            UIFactory.EnsureEventSystem();

            if (canvasPrefab == null)
                _canvas = UIReferenceBinder.FindCanvas(CanvasName, PrefabCanvasName);

            if (_canvas == null && !TryBuildFromPrefab())
                BuildFallbackUI();

            BindChildren();
        }

        private bool TryBuildFromPrefab()
        {
            if (canvasPrefab == null)
                canvasPrefab = Resources.Load<GameObject>(PrefabResourcePath);

            if (canvasPrefab == null)
                return false;

            var instance = Instantiate(canvasPrefab, transform);
            instance.name = CanvasName;
            _canvas = instance.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = instance.GetComponentInChildren<Canvas>(true);

            if (_canvas == null)
            {
                Destroy(instance);
                return false;
            }

            return true;
        }

        private void BindChildren()
        {
            if (_canvas == null)
                return;

            var root = _canvas.transform;
            _title = UIReferenceBinder.Resolve(_title, root, "Title");
            _subtitle = UIReferenceBinder.Resolve(_subtitle, root, "Subtitle");
            _status = UIReferenceBinder.Resolve(_status, root, "Status");
            _replayButton = UIReferenceBinder.Resolve(_replayButton, root, "ReplayButton");
            _nextButton = UIReferenceBinder.Resolve(_nextButton, root, "NextButton");
            _menuButton = UIReferenceBinder.Resolve(_menuButton, root, "MenuButton");
            _nextLabel = UIReferenceBinder.LabelOf(_nextButton);

            UIReferenceBinder.Bind(_replayButton, OnReplayPressed);
            UIReferenceBinder.Bind(_nextButton, OnNextPressed);
            UIReferenceBinder.Bind(_menuButton, OnMenuPressed);
        }

        private void BuildFallbackUI()
        {
            _canvas = UIFactory.CreateCanvas(CanvasName, 1000);
            var background = UIFactory.CreatePanel(_canvas.transform, "Overlay",
                new Color(0.05f, 0.03f, 0.02f, 0.86f));

            var panel = UIFactory.CreatePanel(background, "Panel", UIFactory.PanelColor);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 560f);

            _title = UIFactory.CreateText(panel, "Title", completedTitle, 64,
                UIFactory.TextColor, new Vector2(0f, 200f), new Vector2(700f, 100f),
                TextAnchor.MiddleCenter, FontStyle.Bold);

            _subtitle = UIFactory.CreateText(panel, "Subtitle", completedSubtitle, 28,
                UIFactory.MutedTextColor, new Vector2(0f, 130f), new Vector2(680f, 50f));

            _replayButton = UIFactory.CreateButton(panel, "ReplayButton", "REPLAY", new Vector2(0f, 35f),
                new Vector2(500f, 78f), null, 30);

            _nextButton = UIFactory.CreateButton(panel, "NextButton", "NEXT LEVEL",
                new Vector2(0f, -65f), new Vector2(500f, 78f), null, 30);
            _nextLabel = UIReferenceBinder.LabelOf(_nextButton);

            _menuButton = UIFactory.CreateButton(panel, "MenuButton", "MAIN MENU", new Vector2(0f, -165f),
                new Vector2(500f, 78f), null, 30);

            _status = UIFactory.CreateText(panel, "Status", "", 22,
                new Color(1f, 0.78f, 0.35f, 1f), new Vector2(0f, -235f),
                new Vector2(680f, 50f));
        }

        private void OnReplayPressed()
        {
            if (!LevelManager.EnsureInstance().RestartCurrentLevel())
                ShowError("Could not restart the level. Check that its scene is in Build Settings.");
        }

        private void OnNextPressed()
        {
            var levelManager = LevelManager.EnsureInstance();
            if (levelManager.IsCurrentLevelFinal)
            {
                levelManager.ReturnToMainMenu();
                return;
            }

            if (!levelManager.LoadNextLevel())
                ShowError("The next level scene is not assigned yet.");
        }

        private void OnMenuPressed()
        {
            LevelManager.EnsureInstance().ReturnToMainMenu();
        }

        private void ShowError(string message)
        {
            SetText(_status, message);
            Debug.LogWarning("[LevelCompletion] " + message);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
