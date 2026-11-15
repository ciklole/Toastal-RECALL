using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ToastalRecall
{
    /// <summary>
    /// URP 3D objects are lit by real lights. A scene with 3D meshes but no
    /// directional light renders them black, which looks like "the model is
    /// invisible". This adds a soft key light at runtime when a scene contains
    /// 3D meshes and no directional light exists.
    ///
    /// The light is only created at runtime, so it never modifies your scenes.
    /// Add your own Directional Light to a scene to disable this behaviour there.
    /// </summary>
    public static class ThreeDLightingBootstrap
    {
        private const string SunName = "Toastal 3D Sun";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply(scene);
        }

        private static void Apply(Scene scene)
        {
            if (!scene.isLoaded)
                return;

            if (!Has3DContent())
                return;

            if (HasDirectionalLight())
                return;

            var sunObject = new GameObject(SunName);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // The 2D template usually has no ambient light configured. Without it
            // unlit faces of a 3D model go pure black even with a key light.
            if (RenderSettings.ambientMode != AmbientMode.Skybox &&
                RenderSettings.ambientLight.maxColorComponent < 0.15f)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.48f);
            }

            Debug.Log("[ToastalRecall] Added a temporary 3D key light because this scene has 3D meshes " +
                      "but no Directional Light. Add your own Directional Light to a scene to control this.");
        }

        private static bool Has3DContent()
        {
            var meshes = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            return meshes.Length > 0;
        }

        private static bool HasDirectionalLight()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var light in lights)
            {
                if (light != null && light.type == LightType.Directional && light.enabled)
                    return true;
            }

            return false;
        }
    }
}
