using UnityEngine;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Master / Music / SFX volume sliders backed by the persistent AudioManager.
    /// The Canvas lives in Assets/Scenes/Settings.unity so it can be restyled
    /// freely; a runtime Canvas is generated only when the scene has none.
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        private const string CanvasName = "Settings Canvas";

        [Header("Canvas UI (leave empty to auto-bind / generate)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Button backButton;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterValueLabel;
        [SerializeField] private Text musicValueLabel;
        [SerializeField] private Text sfxValueLabel;

        private AudioManager _audio;

        private void Start()
        {
            Time.timeScale = 1f;
            _audio = AudioManager.EnsureInstance();
            UIFactory.EnsureEventSystem();

            if (!BindSceneUI())
                BuildFallbackUI();

            BindSliders();
        }

        private bool BindSceneUI()
        {
            if (canvas == null)
                canvas = UIReferenceBinder.FindCanvas(CanvasName);
            if (canvas == null)
                return false;

            var root = canvas.transform;
            backButton = UIReferenceBinder.Resolve(backButton, root, "BackButton");
            masterSlider = UIReferenceBinder.ResolveAt(masterSlider, root, "Master VolumeSlider/Slider");
            musicSlider = UIReferenceBinder.ResolveAt(musicSlider, root, "Music VolumeSlider/Slider");
            sfxSlider = UIReferenceBinder.ResolveAt(sfxSlider, root, "Sound Effects VolumeSlider/Slider");
            masterValueLabel = UIReferenceBinder.Resolve(masterValueLabel, root, "Master VolumeValue");
            musicValueLabel = UIReferenceBinder.Resolve(musicValueLabel, root, "Music VolumeValue");
            sfxValueLabel = UIReferenceBinder.Resolve(sfxValueLabel, root, "Sound Effects VolumeValue");
            return true;
        }

        private void BuildFallbackUI()
        {
            canvas = UIFactory.CreateCanvas(CanvasName);
            var root = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.BackgroundColor);

            UIFactory.CreateText(root, "Title", "SETTINGS", 76, UIFactory.TextColor,
                new Vector2(0f, 380f), new Vector2(1200f, 110f), TextAnchor.MiddleCenter, FontStyle.Bold);

            masterSlider = CreateVolumeSlider(root, "Master Volume", 150f, _audio.MasterVolume,
                out masterValueLabel);
            musicSlider = CreateVolumeSlider(root, "Music Volume", 20f, _audio.MusicVolume,
                out musicValueLabel);
            sfxSlider = CreateVolumeSlider(root, "Sound Effects Volume", -110f, _audio.SfxVolume,
                out sfxValueLabel);

            UIFactory.CreateText(root, "Note", "Settings are saved automatically.",
                22, UIFactory.MutedTextColor, new Vector2(0f, -250f), new Vector2(1000f, 50f));

            backButton = UIFactory.CreateButton(root, "BackButton", "BACK", new Vector2(0f, -370f),
                new Vector2(300f, 76f), null, 32);
        }

        private static Slider CreateVolumeSlider(Transform root, string label, float y,
            float initialValue, out Text valueLabel)
        {
            valueLabel = UIFactory.CreateText(root, label + "Value", FormatPercent(initialValue),
                24, UIFactory.MutedTextColor, new Vector2(310f, y + 28f), new Vector2(120f, 40f),
                TextAnchor.MiddleRight);

            return UIFactory.CreateSlider(root, label + "Slider", label, new Vector2(0f, y),
                new Vector2(720f, 100f), initialValue, null);
        }

        private void BindSliders()
        {
            SetSliderValue(masterSlider, _audio.MasterVolume, masterValueLabel);
            SetSliderValue(musicSlider, _audio.MusicVolume, musicValueLabel);
            SetSliderValue(sfxSlider, _audio.SfxVolume, sfxValueLabel);

            UIReferenceBinder.Bind(masterSlider, value =>
            {
                _audio.SetMasterVolume(value);
                UpdateValueLabel(masterValueLabel, value);
            });

            UIReferenceBinder.Bind(musicSlider, value =>
            {
                _audio.SetMusicVolume(value);
                UpdateValueLabel(musicValueLabel, value);
            });

            UIReferenceBinder.Bind(sfxSlider, value =>
            {
                _audio.SetSfxVolume(value);
                UpdateValueLabel(sfxValueLabel, value);
            });

            UIReferenceBinder.Bind(backButton, OnBackPressed);
        }

        private static void SetSliderValue(Slider slider, float value, Text label)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);

            UpdateValueLabel(label, value);
        }

        private static void UpdateValueLabel(Text label, float value)
        {
            if (label != null)
                label.text = FormatPercent(value);
        }

        private static string FormatPercent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        private void OnBackPressed()
        {
            LevelManager.EnsureInstance().ReturnToMainMenu();
        }
    }
}
