using UnityEngine;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Nine level buttons driven entirely by the LevelCatalog. Unassigned entries
    /// are tinted and explain themselves when clicked instead of crashing.
    /// The Canvas lives in Assets/Scenes/LevelSelect.unity and can be restyled
    /// freely; a runtime Canvas is generated only when the scene has none.
    /// </summary>
    public class LevelSelectionController : MonoBehaviour
    {
        private const string CanvasName = "Level Select Canvas";
        private const int LevelCount = 9;

        [Header("Canvas UI (leave empty to auto-bind / generate)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button[] levelButtons = new Button[LevelCount];

        private readonly ColorBlock[] _authoredColors = new ColorBlock[LevelCount];
        private readonly Color[] _authoredImageColors = new Color[LevelCount];
        private readonly bool[] _hasAuthoredColors = new bool[LevelCount];

        private void Start()
        {
            Time.timeScale = 1f;
            AudioManager.EnsureInstance();
            UIFactory.EnsureEventSystem();

            if (!BindSceneUI())
                BuildFallbackUI();

            RefreshLevelButtons();
            WireButtons();
            ClearStatus();
        }

        private bool BindSceneUI()
        {
            if (canvas == null)
                canvas = UIReferenceBinder.FindCanvas(CanvasName);
            if (canvas == null)
                return false;

            var root = canvas.transform;
            titleText = UIReferenceBinder.Resolve(titleText, root, "Title");
            statusText = UIReferenceBinder.Resolve(statusText, root, "Status");
            backButton = UIReferenceBinder.Resolve(backButton, root, "BackButton");

            if (levelButtons == null || levelButtons.Length != LevelCount)
            {
                var resized = new Button[LevelCount];
                if (levelButtons != null)
                {
                    for (int i = 0; i < levelButtons.Length && i < resized.Length; i++)
                        resized[i] = levelButtons[i];
                }
                levelButtons = resized;
            }

            for (int i = 0; i < LevelCount; i++)
            {
                if (levelButtons[i] == null)
                    levelButtons[i] = UIReferenceBinder.FindChild<Button>(root, "Level" + (i + 1));
            }

            return true;
        }

        private void BuildFallbackUI()
        {
            canvas = UIFactory.CreateCanvas(CanvasName);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            titleText = UIFactory.CreateText(root, "Title", "SELECT A LEVEL", 72, UIFactory.TextColor,
                new Vector2(0f, 390f), new Vector2(1400f, 100f), TextAnchor.MiddleCenter, FontStyle.Bold);

            var levelManager = LevelManager.EnsureInstance();
            var catalog = levelManager.Catalog;

            if (catalog == null)
            {
                UIFactory.CreateText(root, "NoCatalog", "No level catalog found.", 30,
                    UIFactory.MutedTextColor, Vector2.zero, new Vector2(1200f, 80f));
            }
            else
            {
                Vector2[] positions =
                {
                    new Vector2(-450f, 190f), new Vector2(0f, 190f), new Vector2(450f, 190f),
                    new Vector2(-450f, 0f),   new Vector2(0f, 0f),   new Vector2(450f, 0f),
                    new Vector2(-450f, -190f),new Vector2(0f, -190f),new Vector2(450f, -190f)
                };

                levelButtons = new Button[LevelCount];
                for (int i = 0; i < LevelCount; i++)
                {
                    levelButtons[i] = UIFactory.CreateButton(root, "Level" + (i + 1), "LEVEL " + (i + 1),
                        positions[i], new Vector2(390f, 125f), null, 32);
                }
            }

            statusText = UIFactory.CreateText(root, "Status", "", 25, UIFactory.MutedTextColor,
                new Vector2(0f, -310f), new Vector2(1300f, 60f));

            backButton = UIFactory.CreateButton(root, "BackButton", "BACK", new Vector2(0f, -410f),
                new Vector2(300f, 76f), null, 32);
        }

        private void RefreshLevelButtons()
        {
            var levelManager = LevelManager.EnsureInstance();
            var catalog = levelManager.Catalog;

            if (catalog == null)
            {
                SetStatus("No level catalog found.", false);
                return;
            }

            catalog.EnsureEntries();
            for (int i = 0; i < LevelCount; i++)
            {
                var button = levelButtons != null && i < levelButtons.Length ? levelButtons[i] : null;
                if (button == null)
                    continue;

                var entry = i < catalog.levels.Length ? catalog.levels[i] : null;
                bool assigned = entry != null && entry.IsAssigned;

                string label = "LEVEL " + (i + 1);
                if (!assigned)
                    label += "\n(Not Assigned)";
                else if (!string.IsNullOrWhiteSpace(entry.displayName) &&
                         entry.displayName != "Level " + (i + 1))
                    label += "\n" + entry.displayName;

                var text = UIReferenceBinder.LabelOf(button);
                if (text != null)
                    text.text = label;

                ApplyButtonState(button, assigned);
            }
        }

        private void ApplyButtonState(Button button, bool assigned)
        {
            int index = System.Array.IndexOf(levelButtons, button);
            if (index < 0)
                return;

            var image = button.targetGraphic as Image;

            if (!_hasAuthoredColors[index])
            {
                _authoredColors[index] = button.colors;
                _authoredImageColors[index] = image != null ? image.color : Color.white;
                _hasAuthoredColors[index] = true;
            }

            var colors = button.colors;
            if (!assigned)
            {
                colors.normalColor = UIFactory.DisabledColor;
                colors.highlightedColor = new Color(0.43f, 0.37f, 0.32f, 1f);
                colors.pressedColor = new Color(0.30f, 0.26f, 0.22f, 1f);
                button.colors = colors;

                if (image != null)
                    image.color = UIFactory.DisabledColor;
            }
            else
            {
                button.colors = _authoredColors[index];
                if (image != null)
                    image.color = _authoredImageColors[index];
            }
        }

        private void WireButtons()
        {
            if (levelButtons != null)
            {
                for (int i = 0; i < levelButtons.Length; i++)
                {
                    int index = i;
                    UIReferenceBinder.Bind(levelButtons[i], () => OnLevelPressed(index));
                }
            }

            UIReferenceBinder.Bind(backButton, OnBackPressed);
        }

        private void OnLevelPressed(int index)
        {
            var catalog = LevelManager.EnsureInstance().Catalog;
            if (catalog == null || index < 0 || index >= catalog.levels.Length)
                return;

            var entry = catalog.levels[index];
            if (entry == null || !entry.IsAssigned)
            {
                string message = "Level " + (index + 1) +
                                 " has no scene assigned. Assign it on the LevelCatalog asset.";
                SetStatus(message, true);
                Debug.LogWarning("[LevelSelect] " + message);
                return;
            }

            LevelManager.Instance.LoadLevel(index);
        }

        private void OnBackPressed()
        {
            LevelManager.EnsureInstance().ReturnToMainMenu();
        }

        private void ClearStatus()
        {
            if (LevelManager.EnsureInstance().Catalog != null)
                SetStatus(string.Empty, false);
        }

        private void SetStatus(string message, bool warning)
        {
            if (statusText == null)
                return;

            statusText.text = message;
            statusText.color = warning
                ? new Color(1f, 0.78f, 0.35f, 1f)
                : UIFactory.MutedTextColor;
        }
    }
}
