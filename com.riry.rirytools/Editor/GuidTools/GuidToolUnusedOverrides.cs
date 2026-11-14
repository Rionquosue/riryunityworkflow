using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Broken References' scrub: removes prefab instances' unused overrides, the property modifications whose target object
    /// no longer exists in the model or prefab, or whose property path no longer exists on it. In a loaded scene and in Prefab Mode it
    /// is one undo step; a prefab file is opened, scrubbed and saved, which Ctrl+Z can't undo.
    /// </summary>
    internal static class GuidToolUnusedOverrides
    {
        /// <summary>The scenes (loaded) and prefabs among the files that hold an instance with unused overrides.</summary>
        internal static List<string> Find(IEnumerable<string> files)
        {
            var found = new List<string>();
            foreach (string path in files)
            {
                bool any = false;
                Use(path, root => any = Instances(root).Any(), false);
                if (any) found.Add(path);
            }
            return found;
        }

        /// <summary>Scrubs each file; returns how many instances were scrubbed.</summary>
        internal static int Scrub(IEnumerable<string> files)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(GuidToolReferenceRepair.Title + ": scrub unused overrides");
            int scrubbed = 0;
            foreach (string path in files)
            {
                Use(path, root =>
                {
                    GameObject[] instances = Instances(root).ToArray();
                    if (instances.Length == 0) return;
                    foreach (GameObject instance in instances) RemoveUnused(instance);
                    scrubbed += instances.Length;
                }, true);
            }
            Undo.CollapseUndoOperations(group);
            return scrubbed;
        }

        private static IEnumerable<GameObject> Instances(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go) && Modifications(go).Any(Unused));
        }

        private static PropertyModification[] Modifications(GameObject instance)
        {
            return PrefabUtility.GetPropertyModifications(instance) ?? Array.Empty<PropertyModification>();
        }

        // Keeps only the used modifications; recorded on the PrefabInstance object so Ctrl+Z restores them.
        private static void RemoveUnused(GameObject instance)
        {
            PropertyModification[] all = Modifications(instance);
            PropertyModification[] kept = all.Where(m => !Unused(m)).ToArray();
            if (kept.Length == all.Length) return;
            Object handle = PrefabUtility.GetPrefabInstanceHandle(instance);
            if (handle != null) Undo.RegisterCompleteObjectUndo(handle, GuidToolReferenceRepair.Title + ": scrub unused overrides");
            PrefabUtility.SetPropertyModifications(instance, kept);
        }

        // Unused: the target is gone, or the property path is not on it. An array element past the source's size is used
        // when its array exists (the instance's own size override adds it); managed references are left alone.
        private static bool Unused(PropertyModification modification)
        {
            if (modification.target == null) return true;
            string path = modification.propertyPath ?? "";
            if (path.StartsWith("managedReferences", StringComparison.Ordinal)) return false;
            using (var serialized = new SerializedObject(modification.target))
            {
                if (serialized.FindProperty(path) != null) return false;
                int array = path.IndexOf(".Array.", StringComparison.Ordinal);
                if (array < 0) return true;
                SerializedProperty list = serialized.FindProperty(path.Substring(0, array));
                return list == null || !list.isArray;
            }
        }

        // Runs the action on the file's roots: a loaded scene's, the prefab open in Prefab Mode, or a prefab file's contents
        // (saved afterwards when save is true).
        private static void Use(string path, Action<GameObject> action, bool save)
        {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) return;
                foreach (GameObject root in scene.GetRootGameObjects()) action(root);
                if (save) EditorSceneManager.MarkSceneDirty(scene);
                return;
            }
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                action(stage.prefabContentsRoot);
                if (save) EditorSceneManager.MarkSceneDirty(stage.scene);
                return;
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                action(contents);
                if (save) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
