using UnityEngine;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Gameplay HUD with a manual respawn button and a quick return-to-menu button.
    /// It uses the editable Canvas prefab at Resources/UI/GameplayHUDCanvas when
    /// present, can bind a Canvas placed in the level, and falls back to a generated
    /// Canvas as a last resort.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        private const string CanvasName = "Gameplay HUD Canvas";
        private const string PrefabCanvasName = "GameplayHUDCanvas";
        private const string PrefabResourcePath = "UI/GameplayHUDCanvas";

        private static GameplayHUD _instance;

        [SerializeField] private BreadRespawnController respawnController;

        [Header("Editable UI (optional)")]
        [Tooltip("Canvas prefab to instantiate. Falls back to Resources/UI/GameplayHUDCanvas.")]
        [SerializeField] private GameObject canvasPrefab;

        private Canvas _canvas;
        private Button _respawnButton;
        private Button _menuButton;
        private Text _levelLabel;

        public static GameplayHUD EnsureFor(BreadRespawnController controller)
        {
            if (_instance != null)
            {
                _instance.respawnController = controller;
                return _instance;
            }

            var existing = FindAnyObjectByType<GameplayHUD>();
            if (existing != null)
            {
                _instance = existing;
                existing.respawnController = controller;
                return existing;
            }

            var go = new GameObject("GameplayHUD");
            var hud = go.AddComponent<GameplayHUD>();
            hud.respawnController = controller;
            _instance = hud;
            return hud;
        }

        public static void SetVisible(bool visible)
        {
            if (_instance != null && _instance._canvas != null)
                _instance._canvas.gameObject.SetActive(visible);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void BuildUI()
        {
            UIFactory.EnsureEventSystem();

            if (canvasPrefab == null)
                _canvas = UIReferenceBinder.FindCanvas(CanvasName, PrefabCanvasName);

            if (_canvas == null && !TryBuildFromPrefab())
                BuildFallbackUI();

            BindChildren();
            UpdateLevelLabel();

            // A Canvas baked into the level starts inactive (or was inactive in the
            // prefab) so it is editable in the Hierarchy without overlapping other UI.
            if (_canvas != null)
                _canvas.gameObject.SetActive(true);
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
            _respawnButton = UIReferenceBinder.Resolve(_respawnButton, root, "RespawnButton");
            _menuButton = UIReferenceBinder.Resolve(_menuButton, root, "MenuButton");
            _levelLabel = UIReferenceBinder.Resolve(_levelLabel, root, "LevelLabel");

            UIReferenceBinder.Bind(_respawnButton, OnRespawnPressed);
            UIReferenceBinder.Bind(_menuButton, OnMenuPressed);
        }

        private void UpdateLevelLabel()
        {
            if (_levelLabel == null)
                return;

            var levelManager = LevelManager.EnsureInstance();
            _levelLabel.text = levelManager.CurrentLevelIndex >= 0
                ? "LEVEL " + (levelManager.CurrentLevelIndex + 1)
                : "TOASTAL RECALL";
        }

        private void BuildFallbackUI()
        {
            _canvas = UIFactory.CreateCanvas(CanvasName, 50);
            var root = _canvas.transform;

            _respawnButton = UIFactory.CreateButton(root, "RespawnButton", "RESPAWN",
                Vector2.zero, new Vector2(210f, 64f), null, 26);
            AnchorToTopLeft(_respawnButton.GetComponent<RectTransform>(), new Vector2(24f, -24f));

            _menuButton = UIFactory.CreateButton(root, "MenuButton", "MENU",
                Vector2.zero, new Vector2(150f, 64f), null, 26);
            AnchorToTopRight(_menuButton.GetComponent<RectTransform>(), new Vector2(-24f, -24f));

            _levelLabel = UIFactory.CreateText(root, "LevelLabel", "TOASTAL RECALL", 24,
                UIFactory.MutedTextColor, Vector2.zero, new Vector2(400f, 50f));
            var labelRect = _levelLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, -42f);
        }

        private void OnRespawnPressed()
        {
            if (respawnController != null)
                respawnController.Respawn();
        }

        private void OnMenuPressed()
        {
            LevelManager.EnsureInstance().ReturnToMainMenu();
        }

        private static void AnchorToTopLeft(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorToTopRight(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
        }
    }
}
