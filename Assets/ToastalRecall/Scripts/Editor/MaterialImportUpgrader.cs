using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// Automatically converts newly imported Built-in materials to URP shaders.
    /// Imported model packs almost always ship Standard-shader materials, which
    /// are magenta under URP. Doing this on import keeps the 3D workflow smooth.
    /// </summary>
    public sealed class MaterialImportUpgrader : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool _scheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (BuildPipeline.isBuildingPlayer)
                return;

            bool foundMaterial = false;
            foreach (string path in importedAssets)
            {
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Pending.Add(path))
                    foundMaterial = true;
            }

            if (foundMaterial && !_scheduled)
            {
                _scheduled = true;
                EditorApplication.delayCall += Flush;
            }
        }

        private static void Flush()
        {
            _scheduled = false;

            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                _scheduled = true;
                EditorApplication.delayCall += Flush;
                return;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            int upgraded = 0;

            foreach (string path in Pending)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                if (MaterialUpgrader.TryUpgradeMaterial(material, lit, unlit))
                {
                    EditorUtility.SetDirty(material);
                    upgraded++;
                }
            }

            Pending.Clear();

            if (upgraded > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[ToastalRecall] Converted {upgraded} imported Built-in material(s) to URP so 3D models render correctly.");
            }
        }
    }
}
