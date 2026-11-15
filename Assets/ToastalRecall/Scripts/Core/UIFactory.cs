using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Lightweight runtime UI builder. Visuals are intentionally simple and all
    /// layout is centralized here so artwork/theme changes stay in one place.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color BackgroundColor = new Color(0.13f, 0.09f, 0.06f, 1f);
        public static readonly Color PanelColor = new Color(0.22f, 0.15f, 0.10f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.85f, 0.52f, 0.20f, 1f);
        public static readonly Color ButtonHighlight = new Color(0.98f, 0.64f, 0.28f, 1f);
        public static readonly Color ButtonPressed = new Color(0.68f, 0.38f, 0.12f, 1f);
        public static readonly Color DisabledColor = new Color(0.35f, 0.30f, 0.26f, 1f);
        public static readonly Color TextColor = new Color(1f, 0.95f, 0.86f, 1f);
        public static readonly Color MutedTextColor = new Color(0.80f, 0.72f, 0.62f, 1f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font != null)
                    return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            EnsureEventSystem();

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
#if ENABLE_INPUT_SYSTEM
                if (existing.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                {
                    var legacy = existing.GetComponent<StandaloneInputModule>();
                    if (legacy != null)
                        legacy.enabled = false;
                    existing.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
#endif
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            UnityAction onClick,
            int fontSize = 34)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = ButtonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonHighlight;
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            if (onClick != null)
                button.onClick.AddListener(onClick);

            var text = CreateText(rect, "Label", label, fontSize, TextColor,
                Vector2.zero, size - new Vector2(24f, 12f));
            text.fontStyle = FontStyle.Bold;

            return button;
        }

        public static Slider CreateSlider(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            float initialValue,
            UnityAction<float> onValueChanged)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = size;

            var labelText = CreateText(rootRect, "Label", label, 26, TextColor,
                new Vector2(0f, size.y * 0.30f), new Vector2(size.x, 40f),
                TextAnchor.MiddleLeft);
            labelText.fontStyle = FontStyle.Bold;

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.SetParent(rootRect, false);
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(0f, 34f);
            sliderRect.anchoredPosition = new Vector2(0f, -size.y * 0.16f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = initialValue;
            slider.direction = Slider.Direction.LeftToRight;

            var background = CreateImage(sliderRect, "Background", new Color(0.10f, 0.07f, 0.05f, 1f));
            background.rectTransform.anchorMin = new Vector2(0f, 0.30f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.70f);
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            var fillArea = CreateUIObject("Fill Area", sliderRect);
            fillArea.anchorMin = new Vector2(0f, 0.30f);
            fillArea.anchorMax = new Vector2(1f, 0.70f);
            fillArea.offsetMin = new Vector2(12f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);

            var fill = CreateImage(fillArea, "Fill", ButtonColor);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = CreateUIObject("Handle Slide Area", sliderRect);
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);

            var handle = CreateImage(handleArea, "Handle", TextColor);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(26f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;

            var navigation = slider.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            slider.navigation = navigation;

            if (onValueChanged != null)
                slider.onValueChanged.AddListener(onValueChanged);

            return slider;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);

            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
