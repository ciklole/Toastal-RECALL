using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// The project is built from the URP 2D template and only had a Renderer2D.
    /// A 2D Renderer never draws 3D meshes, so imported models are invisible.
    ///
    /// This idempotently ensures the renderer setup and:
    ///   1. Creates Assets/Settings/Renderer3D.asset (a Universal Renderer).
    ///   2. Registers it on the URP pipeline asset.
    ///   3. Makes it the default renderer so every camera renders 3D meshes.
    ///
    /// The Renderer2D stays in the list and can be selected per-camera for
    /// scenes that need Light2D. It can also be re-run from
    /// Tools > Toastal Recall > Setup 3D Rendering.
    /// </summary>
    [InitializeOnLoad]
    public static class ThreeDRenderingSetup
    {
        private const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";
        private const string RendererAssetPath = "Assets/Settings/Renderer3D.asset";
        private const string PostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        private const string SessionKey = "ToastalRecall.3DRendererSetup.v2";

        static ThreeDRenderingSetup()
        {
            EditorApplication.delayCall += AutoSetup;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (Application.isPlaying || BuildPipeline.isBuildingPlayer)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            bool alreadySetup = SessionState.GetBool(SessionKey, false);
            if (Ensure3DRenderer(false))
            {
                SessionState.SetBool(SessionKey, true);

                if (!alreadySetup)
                {
                    int upgraded = MaterialUpgrader.UpgradeAllBuiltInMaterialsInProject();
                    if (upgraded > 0)
                    {
                        Debug.Log($"[ToastalRecall] Converted {upgraded} Built-in material(s) to URP. " +
                                  "Imported 3D models will now render with their textures instead of magenta.");
                    }
                }
            }
        }

        [MenuItem("Tools/Toastal Recall/Setup 3D Rendering", priority = 40)]
        private static void SetupFromMenu()
        {
            if (Ensure3DRenderer(true))
                SessionState.SetBool(SessionKey, true);
        }

        [MenuItem("Tools/Toastal Recall/Add 3D Light to Open Scene", priority = 42)]
        private static void Add3DLightToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[ToastalRecall] No open scene to add a light to.");
                return;
            }

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var existing in lights)
            {
                if (existing != null && existing.type == LightType.Directional)
                {
                    Debug.Log("[ToastalRecall] This scene already has a Directional Light.");
                    Selection.activeGameObject = existing.gameObject;
                    return;
                }
            }

            var sunObject = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(sunObject, "Add 3D Light");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            if (RenderSettings.ambientMode != AmbientMode.Skybox &&
                RenderSettings.ambientLight.maxColorComponent < 0.15f)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.48f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = sunObject;
            Debug.Log("[ToastalRecall] Added a Directional Light for 3D models. Save the scene to keep it.");
        }

        /// <summary>
        /// Ensures the URP asset has a Universal Renderer and that it is the default.
        /// Returns true when the pipeline is configured for 3D rendering.
        /// </summary>
        public static bool Ensure3DRenderer(bool verbose)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);

            if (pipeline == null)
            {
                Debug.LogWarning("[ToastalRecall] Could not find the URP asset. " +
                                 "Assign one in Project Settings > Graphics/Quality.");
                return false;
            }

            var renderer3D = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            bool created = false;

            if (renderer3D == null)
            {
                string directory = Path.GetDirectoryName(RendererAssetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                try
                {
                    renderer3D = ScriptableObject.CreateInstance<UniversalRendererData>();
                    renderer3D.name = "Renderer3D";
                    renderer3D.postProcessData =
                        AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);

                    AssetDatabase.CreateAsset(renderer3D, RendererAssetPath);
                    ResourceReloader.ReloadAllNullIn(renderer3D, UniversalRenderPipelineAsset.packagePath);
                    EditorUtility.SetDirty(renderer3D);
                    AssetDatabase.SaveAssets();
                    created = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[ToastalRecall] Could not create Renderer3D.asset yet: " +
                                     exception.Message + " Run Tools > Toastal Recall > Setup 3D Rendering again.");
                    if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath) != null)
                        AssetDatabase.DeleteAsset(RendererAssetPath);
                    return false;
                }
            }

            var serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            if (rendererList == null)
            {
                Debug.LogWarning("[ToastalRecall] URP asset layout is unexpected; " +
                                 "add Renderer3D.asset manually under Renderer List.");
                return false;
            }

            int rendererIndex = -1;
            for (int i = 0; i < rendererList.arraySize; i++)
            {
                if (rendererList.GetArrayElementAtIndex(i).objectReferenceValue == renderer3D)
                {
                    rendererIndex = i;
                    break;
                }
            }

            bool changed = false;
            if (rendererIndex < 0)
            {
                rendererList.arraySize++;
                rendererList.GetArrayElementAtIndex(rendererList.arraySize - 1).objectReferenceValue = renderer3D;
                rendererIndex = rendererList.arraySize - 1;
                changed = true;
            }

            SerializedProperty defaultIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex");
            if (defaultIndex != null && defaultIndex.intValue != rendererIndex)
            {
                defaultIndex.intValue = rendererIndex;
                changed = true;
            }

            if (changed)
            {
                serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();
            }

            // GetRenderer detects a stale runtime renderer array after the serialized
            // renderer list changes and rebuilds the cache to the correct length.
            // Keep the 3D renderer as the default while allowing URP to repair that cache.
            if (pipeline.GetRenderer(rendererIndex) == null)
            {
                Debug.LogError("[ToastalRecall] The configured 3D renderer could not be created.", pipeline);
                return false;
            }

            if (verbose || created)
            {
                Debug.Log($"[ToastalRecall] 3D rendering enabled. Renderer3D is renderer index {rendererIndex} " +
                          "and is now the default. Cameras set to 'Default' will render 3D meshes. " +
                          "The 2D Renderer is still available for pure 2D scenes.");
            }

            return true;
        }
    }
}
