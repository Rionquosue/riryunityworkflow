using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Broken References' scrub: removes prefab instances' unused overrides (Unity's PrefabUtility.RemoveUnusedOverrides),
    /// the entries left pointing at model or prefab objects that no longer exist. In a loaded scene and in Prefab Mode it
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
                    PrefabUtility.RemoveUnusedOverrides(instances, InteractionMode.UserAction);
                    scrubbed += instances.Length;
                }, true);
            }
            Undo.CollapseUndoOperations(group);
            return scrubbed;
        }

        private static IEnumerable<GameObject> Instances(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.HasPrefabInstanceUnusedOverrides(go));
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
