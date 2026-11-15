using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// Imported FBX/OBJ models often use the Built-in "Standard" shader. Under URP
    /// those materials render magenta. This tool converts common Built-in shaders
    /// on the selected materials (or every material used by the open scene) to
    /// URP/Lit or URP/Unlit and carries over the main texture, color, normal map
    /// and smoothness/metallic values.
    /// </summary>
    public static class MaterialUpgrader
    {
        private static readonly string[] LitShaderNames =
        {
            "Standard",
            "Standard (Specular setup)",
            "Legacy Shaders/Diffuse",
            "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Diffuse",
            "Legacy Shaders/Bumped Specular",
            "Diffuse",
            "Specular",
            "Bumped Diffuse",
            "Bumped Specular"
        };

        private static readonly string[] UnlitShaderNames =
        {
            "Unlit/Texture",
            "Unlit/Color",
            "Legacy Shaders/Unlit/Texture"
        };

        private const string LitShaderPath = "Universal Render Pipeline/Lit";
        private const string UnlitShaderPath = "Universal Render Pipeline/Unlit";

        [MenuItem("Tools/Toastal Recall/Upgrade 3D Materials to URP", priority = 41)]
        private static void UpgradeFromMenu()
        {
            var materials = CollectMaterials();
            if (materials.Count == 0)
            {
                Debug.Log("[ToastalRecall] No 3D materials found to upgrade. " +
                          "Select a material or an object with a MeshRenderer first.");
                return;
            }

            Shader lit = Shader.Find(LitShaderPath);
            Shader unlit = Shader.Find(UnlitShaderPath);
            int upgraded = 0;

            foreach (var material in materials)
            {
                if (TryUpgradeMaterial(material, lit, unlit))
                {
                    EditorUtility.SetDirty(material);
                    upgraded++;
                }
            }

            if (upgraded > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[ToastalRecall] Upgraded {upgraded} material(s) to URP shaders.");
            }
            else
            {
                Debug.Log("[ToastalRecall] All found materials already use URP or an unsupported shader.");
            }
        }

        [MenuItem("Tools/Toastal Recall/Upgrade All 3D Materials in Project", priority = 43)]
        private static void UpgradeWholeProjectFromMenu()
        {
            int upgraded = UpgradeAllBuiltInMaterialsInProject();
            Debug.Log(upgraded > 0
                ? $"[ToastalRecall] Upgraded {upgraded} Built-in material(s) in the project to URP."
                : "[ToastalRecall] No Built-in materials needed upgrading.");
        }

        /// <summary>
        /// Converts every material in Assets/ that still uses a Built-in shader.
        /// Returns how many materials were changed.
        /// </summary>
        public static int UpgradeAllBuiltInMaterialsInProject()
        {
            Shader lit = Shader.Find(LitShaderPath);
            Shader unlit = Shader.Find(UnlitShaderPath);
            if (lit == null)
                return 0;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int upgraded = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                if (TryUpgradeMaterial(material, lit, unlit))
                {
                    EditorUtility.SetDirty(material);
                    upgraded++;
                }
            }

            if (upgraded > 0)
                AssetDatabase.SaveAssets();

            return upgraded;
        }

        /// <summary>
        /// Upgrades one material when it uses a known Built-in shader.
        /// Returns true when the material was changed.
        /// </summary>
        public static bool TryUpgradeMaterial(Material material, Shader lit, Shader unlit)
        {
            if (material == null || material.shader == null)
                return false;

            string shaderName = material.shader.name;
            if (shaderName.StartsWith("Universal Render Pipeline/") ||
                shaderName.StartsWith("Shader Graphs/") ||
                shaderName.StartsWith("Sprites/"))
                return false;

            bool isUnlit = Array.IndexOf(UnlitShaderNames, shaderName) >= 0;
            if (!isUnlit && Array.IndexOf(LitShaderNames, shaderName) < 0)
                return false;

            if (isUnlit)
            {
                if (unlit == null)
                    return false;
                CopyUnlitProperties(material, unlit);
            }
            else
            {
                if (lit == null)
                    return false;
                CopyLitProperties(material, lit);
            }

            return true;
        }

        private static List<Material> CollectMaterials()
        {
            var result = new List<Material>();

            foreach (var selected in Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.Editable))
                AddUnique(result, selected);

            if (result.Count > 0)
                return result;

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (var renderer in renderers)
            {
                if (renderer == null ||
                    renderer is SpriteRenderer ||
                    renderer is ParticleSystemRenderer ||
                    renderer is LineRenderer ||
                    renderer is TrailRenderer)
                    continue;

                foreach (var material in renderer.sharedMaterials)
                    AddUnique(result, material);
            }

            return result;
        }

        private static void AddUnique(List<Material> list, Material material)
        {
            if (material != null && !list.Contains(material))
                list.Add(material);
        }

        private static void CopyLitProperties(Material material, Shader target)
        {
            var upgraded = new Material(target);
            upgraded.name = material.name;

            CopyTexture(material, upgraded, "_MainTex", "_BaseMap");
            CopyTexture(material, upgraded, "_BumpMap", "_BumpMap");
            CopyTexture(material, upgraded, "_MetallicGlossMap", "_MetallicGlossMap");
            CopyTexture(material, upgraded, "_OcclusionMap", "_OcclusionMap");
            CopyTexture(material, upgraded, "_EmissionMap", "_EmissionMap");
            CopyTexture(material, upgraded, "_DetailMask", "_DetailMask");

            CopyVector(material, upgraded, "_MainTex_ST", "_BaseMap_ST");
            CopyColor(material, upgraded, "_Color", "_BaseColor");
            CopyFloat(material, upgraded, "_BumpScale", "_BumpScale");
            CopyFloat(material, upgraded, "_Metallic", "_Metallic");
            CopyFloat(material, upgraded, "_Glossiness", "_Smoothness");

            // When a metallic/smoothness map is present, Standard uses
            // _GlossMapScale as the smoothness multiplier; URP uses _Smoothness.
            bool hasMetallicGloss = material.HasProperty("_MetallicGlossMap") &&
                                    material.GetTexture("_MetallicGlossMap") != null;
            if (hasMetallicGloss)
                CopyFloat(material, upgraded, "_GlossMapScale", "_Smoothness");

            CopyFloat(material, upgraded, "_OcclusionStrength", "_OcclusionStrength");

            if (material.HasProperty("_EmissionColor"))
            {
                upgraded.SetColor("_EmissionColor", material.GetColor("_EmissionColor"));
                upgraded.EnableKeyword("_EMISSION");
            }

            float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : 0f;
            if (Mathf.Approximately(mode, 1f))
            {
                // Alpha cutout keeps leaf/fence holes.
                upgraded.SetFloat("_AlphaClip", 1f);
                upgraded.EnableKeyword("_ALPHATEST_ON");
                upgraded.renderQueue = 2450;
            }
            else if (mode >= 2f)
            {
                // Fade/Transparent.
                upgraded.SetFloat("_Surface", 1f);
                upgraded.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                upgraded.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                upgraded.SetFloat("_ZWrite", 0f);
                upgraded.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                upgraded.renderQueue = 3000;
            }

            // Assign the shader first so URP property names survive the copy.
            material.shader = target;
            material.CopyPropertiesFromMaterial(upgraded);
            material.renderQueue = upgraded.renderQueue;

            EnableTextureKeywords(material, upgraded);
            UnityEngine.Object.DestroyImmediate(upgraded);
        }

        private static void CopyUnlitProperties(Material material, Shader target)
        {
            var upgraded = new Material(target);
            upgraded.name = material.name;

            CopyTexture(material, upgraded, "_MainTex", "_BaseMap");
            CopyVector(material, upgraded, "_MainTex_ST", "_BaseMap_ST");
            CopyColor(material, upgraded, "_Color", "_BaseColor");

            material.shader = target;
            material.CopyPropertiesFromMaterial(upgraded);
            material.renderQueue = upgraded.renderQueue;
            UnityEngine.Object.DestroyImmediate(upgraded);
        }

        /// <summary>
        /// URP Lit only samples normal/metallic/occlusion maps when the matching
        /// keyword is enabled, so enable them based on the copied textures.
        /// </summary>
        private static void EnableTextureKeywords(Material material, Material source)
        {
            if (source.GetTexture("_BumpMap") != null)
                material.EnableKeyword("_NORMALMAP");
            if (source.GetTexture("_MetallicGlossMap") != null)
                material.EnableKeyword("_METALLICGLOSSMAP");
            if (source.GetTexture("_OcclusionMap") != null)
                material.EnableKeyword("_OCCLUSIONMAP");
            if (source.GetTexture("_EmissionMap") != null)
                material.EnableKeyword("_EMISSION");
        }

        private static void CopyTexture(Material from, Material to, string fromName, string toName)
        {
            if (!from.HasProperty(fromName) || !to.HasProperty(toName))
                return;

            Texture texture = from.GetTexture(fromName);
            if (texture != null)
                to.SetTexture(toName, texture);
        }

        private static void CopyVector(Material from, Material to, string fromName, string toName)
        {
            if (!from.HasProperty(fromName) || !to.HasProperty(toName))
                return;

            to.SetVector(toName, from.GetVector(fromName));
        }

        private static void CopyColor(Material from, Material to, string fromName, string toName)
        {
            if (!from.HasProperty(fromName) || !to.HasProperty(toName))
                return;

            to.SetColor(toName, from.GetColor(fromName));
        }

        private static void CopyFloat(Material from, Material to, string fromName, string toName)
        {
            if (!from.HasProperty(fromName) || !to.HasProperty(toName))
                return;

            to.SetFloat(toName, from.GetFloat(fromName));
        }
    }
}
