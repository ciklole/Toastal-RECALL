using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Editable "you got toasted" panel shown when the bread dies. It can bind a
    /// Canvas placed in the level, instantiate the prefab at
    /// Resources/UI/RespawnCanvas, or fall back to a generated Canvas.
    /// Gameplay pauses until the player picks RESPAWN or MAIN MENU.
    /// </summary>
    public class RespawnScreen : MonoBehaviour
    {
        private const string CanvasName = "Respawn Canvas";
        private const string PrefabCanvasName = "RespawnCanvas";
        private const string PrefabResourcePath = "UI/RespawnCanvas";

        private static RespawnScreen _instance;

        public static RespawnScreen Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindAnyObjectByType<RespawnScreen>(FindObjectsInactive.Include);
                if (_instance != null)
                    return _instance;

                var go = new GameObject("RespawnScreen");
                _instance = go.AddComponent<RespawnScreen>();
                return _instance;
            }
        }

        public static bool IsShowing => _instance != null && _instance._showing;

        [Header("Messages")]
        [SerializeField] private string deathTitle = "TOASTED!";
        [SerializeField] private string deathSubtitle = "The bread didn't make it.";

        [Header("Editable UI (optional)")]
        [Tooltip("Canvas to use. If empty, one is found in the scene or generated.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Canvas prefab to instantiate. Falls back to Resources/UI/RespawnCanvas.")]
        [SerializeField] private GameObject canvasPrefab;

        private Text _title;
        private Text _subtitle;
        private Button _respawnButton;
        private Button _menuButton;
        private Text _status;
        private BreadRespawnController _owner;
        private bool _showing;
        private bool _built;
        private float _restoreTimeScale = 1f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUI();

            // The death screen is baked into levels as an inactive Canvas so it
            // never overlaps the HUD until the bread actually dies.
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            if (_showing)
                RestoreTimeScale();

            _showing = false;
        }

        /// <summary>Called by the bread when a hazard or fall kills it.</summary>
        public static void ShowFor(BreadRespawnController owner)
        {
            if (owner == null)
                return;

            Instance.Show(owner);
        }

        /// <summary>Closes the panel without resetting the bread (used when the level ends).</summary>
        public static void HideIfShowing()
        {
            if (_instance != null && _instance._showing)
                _instance.Hide();
        }

        public void Show(BreadRespawnController owner)
        {
            if (!_built)
                BuildUI();

            _owner = owner;
            _showing = true;

            _restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SetText(_title, deathTitle);
            SetText(_subtitle, deathSubtitle);
            SetText(_status, string.Empty);

            if (_respawnButton != null)
            {
                _respawnButton.interactable = true;
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_respawnButton.gameObject);
            }

            if (canvas != null)
                canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (!_showing)
                return;

            _showing = false;
            if (canvas != null)
                canvas.gameObject.SetActive(false);

            RestoreTimeScale();
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = _restoreTimeScale > 0f ? _restoreTimeScale : 1f;
        }

        private void OnRespawnPressed()
        {
            Hide();

            if (_owner != null)
                _owner.RespawnAfterDeath();
        }

        private void OnMenuPressed()
        {
            Hide();
            LevelManager.EnsureInstance().ReturnToMainMenu();
        }

        // -------------------------------------------------------------------- UI

        private void BuildUI()
        {
            if (_built)
                return;

            _built = true;
            UIFactory.EnsureEventSystem();

            if (canvas == null)
                canvas = UIReferenceBinder.FindCanvas(CanvasName, PrefabCanvasName);

            if (canvas == null && !TryBuildFromPrefab())
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
            canvas = instance.GetComponent<Canvas>();
            if (canvas == null)
                canvas = instance.GetComponentInChildren<Canvas>(true);

            if (canvas == null)
            {
                Destroy(instance);
                return false;
            }

            return true;
        }

        private void BindChildren()
        {
            if (canvas == null)
                return;

            var root = canvas.transform;
            _title = UIReferenceBinder.Resolve(_title, root, "Title");
            _subtitle = UIReferenceBinder.Resolve(_subtitle, root, "Subtitle");
            _respawnButton = UIReferenceBinder.Resolve(_respawnButton, root, "RespawnButton");
            _menuButton = UIReferenceBinder.Resolve(_menuButton, root, "MenuButton");
            _status = UIReferenceBinder.Resolve(_status, root, "Status");

            UIReferenceBinder.Bind(_respawnButton, OnRespawnPressed);
            UIReferenceBinder.Bind(_menuButton, OnMenuPressed);
        }

        private void BuildFallbackUI()
        {
            canvas = UIFactory.CreateCanvas(CanvasName, 900);
            var overlay = UIFactory.CreatePanel(canvas.transform, "Overlay",
                new Color(0.05f, 0.03f, 0.02f, 0.82f));

            var panel = UIFactory.CreatePanel(overlay, "Panel", UIFactory.PanelColor);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 520f);

            _title = UIFactory.CreateText(panel, "Title", deathTitle, 72, UIFactory.TextColor,
                new Vector2(0f, 165f), new Vector2(700f, 110f),
                TextAnchor.MiddleCenter, FontStyle.Bold);

            _subtitle = UIFactory.CreateText(panel, "Subtitle", deathSubtitle, 28,
                UIFactory.MutedTextColor, new Vector2(0f, 90f), new Vector2(680f, 50f));

            _respawnButton = UIFactory.CreateButton(panel, "RespawnButton", "RESPAWN",
                new Vector2(0f, -5f), new Vector2(500f, 82f), null, 32);

            _menuButton = UIFactory.CreateButton(panel, "MenuButton", "MAIN MENU",
                new Vector2(0f, -115f), new Vector2(500f, 82f), null, 30);

            _status = UIFactory.CreateText(panel, "Status", string.Empty, 22,
                new Color(1f, 0.78f, 0.35f, 1f), new Vector2(0f, -210f), new Vector2(680f, 50f));
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
