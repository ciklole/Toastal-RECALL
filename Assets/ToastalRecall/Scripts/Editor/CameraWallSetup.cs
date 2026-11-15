using UnityEditor;
using UnityEngine;

namespace ToastalRecall.EditorTools
{
    /// <summary>
    /// Creates a dedicated "Wall" layer and helps assign scene geometry to it.
    /// The follow camera only avoids colliders on this layer, so props, food and
    /// other small objects between the bread and the camera never push it in.
    /// </summary>
    [InitializeOnLoad]
    public static class CameraWallSetup
    {
        public const string WallLayerName = "Wall";
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        static CameraWallSetup()
        {
            EditorApplication.delayCall += AutoCreateLayer;
        }

        private static void AutoCreateLayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureWallLayer();
        }

        [MenuItem("Tools/Toastal Recall/Camera/Create Wall Layer", priority = 40)]
        public static int EnsureWallLayer()
        {
            int existing = LayerMask.NameToLayer(WallLayerName);
            if (existing >= 0)
                return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[ToastalRecall] Could not open " + TagManagerPath + " to create the Wall layer.");
                return -1;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                Debug.LogWarning("[ToastalRecall] TagManager has no layers array.");
                return -1;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                    continue;

                layer.stringValue = WallLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[ToastalRecall] Created the '" + WallLayerName + "' layer (index " + i + ").");
                return i;
            }

            Debug.LogError("[ToastalRecall] No free layer slot is available for the Wall layer.");
            return -1;
        }

        [MenuItem("Tools/Toastal Recall/Camera/Mark Selected Objects As Walls", priority = 41)]
        public static void MarkSelectionAsWalls()
        {
            int layer = EnsureWallLayer();
            if (layer < 0)
                layer = LayerMask.NameToLayer(WallLayerName);
            if (layer < 0)
                return;

            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[ToastalRecall] Select the wall/room objects first, then run this command.");
                return;
            }

            Undo.SetCurrentGroupName("Mark Camera Walls");
            int group = Undo.GetCurrentGroup();

            foreach (var root in selection)
            {
                if (root == null)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(root, "Mark Camera Walls");
                if (SetLayerRecursively(root, layer))
                    EditorUtility.SetDirty(root);
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log("[ToastalRecall] Marked " + selection.Length +
                      " selected object(s) as '" + WallLayerName + "' for the follow camera.");
        }

        [MenuItem("Tools/Toastal Recall/Camera/Mark Room Objects In Active Scene", priority = 42)]
        public static void MarkRoomObjectsInActiveScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            int marked = MarkRoomObjects(scene);
            Debug.Log("[ToastalRecall] Marked " + marked + " room object(s) as '" + WallLayerName + "'.");
        }

        /// <summary>
        /// Marks room-shell objects (names containing "Room", excluding
        /// "RoomAccessories") in a scene so the camera collides with the room walls
        /// but passes through props. Returns how many roots were marked.
        /// </summary>
        public static int MarkRoomObjects(UnityEngine.SceneManagement.Scene scene)
        {
            int layer = EnsureWallLayer();
            if (layer < 0)
                layer = LayerMask.NameToLayer(WallLayerName);
            if (layer < 0)
                return 0;

            int marked = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null || !IsRoomShell(root.name))
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(root, "Mark Room As Wall");
                if (SetLayerRecursively(root, layer))
                {
                    EditorUtility.SetDirty(root);
                    marked++;
                }
            }

            return marked;
        }

        private static bool IsRoomShell(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName.IndexOf("Room", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                   objectName.IndexOf("Accessor", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool SetLayerRecursively(GameObject go, int layer)
        {
            bool changed = false;
            if (go.layer != layer)
            {
                go.layer = layer;
                changed = true;
            }

            foreach (Transform child in go.transform)
                changed |= SetLayerRecursively(child.gameObject, layer);

            return changed;
        }
    }
}
