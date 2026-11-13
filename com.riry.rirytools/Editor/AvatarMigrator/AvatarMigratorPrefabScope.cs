using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Which source objects belong to a child prefab instance: a prefab instance inside the
    /// avatar other than the avatar's own prefab (the one its bones belong to). Child prefabs
    /// are copied whole as prefabs (feature 11) and never in parts, and nothing is unpacked.
    /// </summary>
    internal sealed class AvatarMigratorPrefabScope
    {
        private readonly Transform root;
        private readonly Transform avatarPrefab;

        internal AvatarMigratorPrefabScope(Transform avatarRoot)
        {
            root = avatarRoot;
            Transform hips = AvatarMigratorCopyRun.Hips(avatarRoot);
            GameObject nearest = PrefabUtility.GetNearestPrefabInstanceRoot((hips != null ? hips : avatarRoot).gameObject);
            avatarPrefab = nearest != null ? nearest.transform : null;
        }

        /// <summary>The root of the child prefab instance the object is part of, or null.</summary>
        internal Transform ChildPrefabRoot(Transform source)
        {
            if (source == null) return null;
            GameObject nearest = PrefabUtility.GetNearestPrefabInstanceRoot(source.gameObject);
            if (nearest == null) return null;
            Transform instance = nearest.transform;
            if (instance == root || !instance.IsChildOf(root)) return null;
            if (avatarPrefab != null && (instance == avatarPrefab || avatarPrefab.IsChildOf(instance))) return null;
            return instance;
        }

        /// <summary>The outermost child prefab instance roots, parents first.</summary>
        internal List<Transform> OutermostChildPrefabs()
        {
            var roots = new List<Transform>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (ChildPrefabRoot(transform) == transform && (transform.parent == null || ChildPrefabRoot(transform.parent) == null)) roots.Add(transform);
            }
            return roots;
        }

        internal static GameObject Asset(Transform instanceRoot)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot.gameObject);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>What a prefab instance carries that is not in its asset, as a "Possible issues with" line, or null.</summary>
        internal static string Overrides(Transform instanceRoot, string label)
        {
            int addedComponents = PrefabUtility.GetAddedComponents(instanceRoot.gameObject).Count;
            int addedObjects = PrefabUtility.GetAddedGameObjects(instanceRoot.gameObject).Count;
            int removedComponents = PrefabUtility.GetRemovedComponents(instanceRoot.gameObject).Count;
            if (addedComponents + addedObjects + removedComponents == 0) return null;
            var parts = new List<string>();
            if (addedComponents > 0) parts.Add(addedComponents + " added component" + (addedComponents == 1 ? "" : "s"));
            if (addedObjects > 0) parts.Add(addedObjects + " added object" + (addedObjects == 1 ? "" : "s"));
            if (removedComponents > 0) parts.Add(removedComponents + " removed component" + (removedComponents == 1 ? "" : "s"));
            return label + ": " + string.Join(", ", parts) + " that are not part of the prefab, so they were not copied (the prefab " +
                "is never unpacked). Investigate and add them on the target by hand.";
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant child prefab scope (AvatarMigratorPrefabScope.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorPrefabScope         Child prefab instances of an avatar (not the avatar's own prefab, found from its hips).
//   ChildPrefabRoot(t) / OutermostChildPrefabs() / Asset(root) / Overrides(root, label)  The last lists added components,
//                             added objects and removed components as a "Possible issues with" line.
