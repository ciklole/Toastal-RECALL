using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ToastalRecall
{
    /// <summary>
    /// Helpers shared by the menu controllers. Every controller prefers a Canvas
    /// that lives in the scene, so the layout can be edited in the Hierarchy, and
    /// only generates a runtime fallback when that Canvas is missing.
    /// </summary>
    public static class UIReferenceBinder
    {
        public static Canvas FindCanvas(params string[] canvasNames)
        {
            if (canvasNames == null || canvasNames.Length == 0)
                return null;

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var canvas in canvases)
            {
                if (canvas == null)
                    continue;

                foreach (string canvasName in canvasNames)
                {
                    if (canvas.name == canvasName)
                        return canvas;
                }
            }

            return null;
        }

        public static T FindChild<T>(Transform root, string childName) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            var components = root.GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component.name == childName)
                    return component;
            }

            return null;
        }

        /// <summary>Finds a component on a child by relative path, e.g. "Master VolumeSlider/Slider".</summary>
        public static T FindAt<T>(Transform root, string path) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            var direct = root.Find(path);
            if (direct != null)
            {
                var directComponent = direct.GetComponent<T>();
                if (directComponent != null)
                    return directComponent;
            }

            string[] parts = path.Split('/');
            var candidates = root.GetComponentsInChildren<Transform>(true);
            foreach (var candidate in candidates)
            {
                if (candidate.name != parts[0])
                    continue;

                Transform current = candidate;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);

                if (current == null)
                    continue;

                var component = current.GetComponent<T>();
                if (component != null)
                    return component;
            }

            return null;
        }

        public static T Resolve<T>(T assigned, Transform root, string childName) where T : Component
        {
            if (assigned != null)
                return assigned;

            return FindChild<T>(root, childName);
        }

        public static T ResolveAt<T>(T assigned, Transform root, string path) where T : Component
        {
            if (assigned != null)
                return assigned;

            return FindAt<T>(root, path);
        }

        public static Text LabelOf(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        public static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public static void Bind(Slider slider, UnityAction<float> action)
        {
            if (slider == null || action == null)
                return;

            slider.onValueChanged.RemoveListener(action);
            slider.onValueChanged.AddListener(action);
        }
    }
}
