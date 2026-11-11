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
    internal sealed class AvatarPrefabScope
    {
        private readonly Transform root;
        private readonly Transform avatarPrefab;

        internal AvatarPrefabScope(Transform avatarRoot)
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

    /// <summary>
    /// Feature 11: each outermost child prefab instance is instantiated again from its asset
    /// under the matched parent (made by feature 4 when needed), with the same name, place and
    /// property overrides. Its objects join the map and its components are paired with the
    /// source's, so references, rotation and scale are corrected like copied ones.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        private void CopyPrefabs(AvatarTypeSettings typeSettings)
        {
            foreach (Transform source in scope.OutermostChildPrefabs())
            {
                if (ignored.Contains(source)) continue;
                string label = "Prefab " + AnimationUtility.CalculateTransformPath(source, sourceRoot);
                GameObject asset = AvatarPrefabScope.Asset(source);
                if (asset == null)
                {
                    report.PossibleIssues.Add(label + ": its prefab asset is missing, so it was not copied.");
                    continue;
                }
                Transform existing = map.TargetOf(source);
                if (existing != null && AvatarPrefabScope.Asset(existing) == asset && PrefabUtility.GetNearestPrefabInstanceRoot(existing.gameObject) == existing.gameObject)
                {
                    if (!typeSettings.Replace)
                    {
                        // Already on the target: its parts are matched, nothing is copied twice.
                        PairPrefab(source, existing, false);
                        report.PrefabsAlreadyThere++;
                        continue;
                    }
                    try
                    {
                        Undo.DestroyObjectImmediate(existing.gameObject);
                    }
                    catch (Exception exception)
                    {
                        report.NotCopied.Add(label + ": the target's copy could not be removed for Replace (" + exception.Message + ").");
                        RiryLog.Exception(RiryLog.AvatarMigration, label + ": Replace could not remove the target's copy", exception, existing.gameObject);
                        continue;
                    }
                }
                Transform parent = TargetFor(source.parent, typeSettings.CreateMissing);
                if (parent == null)
                {
                    report.NotCopied.Add(label + ": its parent has no match on the target (Create missing objects is off).");
                    continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                if (instance == null)
                {
                    report.NotCopied.Add(label + ": it could not be instantiated.");
                    continue;
                }
                Undo.RegisterCreatedObjectUndo(instance, AvatarMigrationAssistant.Title);
                instance.name = source.name;
                instance.transform.localPosition = source.localPosition;
                instance.transform.localRotation = source.localRotation;
                instance.transform.localScale = source.localScale;
                instance.transform.SetSiblingIndex(Mathf.Min(source.GetSiblingIndex(), parent.childCount - 1));
                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(source.gameObject);
                if (modifications != null && modifications.Length > 0) PrefabUtility.SetPropertyModifications(instance, modifications);
                PairPrefab(source, instance.transform, true);
                report.CopiedPrefabs++;
                string issues = AvatarPrefabScope.Overrides(source, label);
                if (issues != null) report.PossibleIssues.Add(issues);
            }
        }

        // The prefab's objects join the map by path; for a new instance its components are also
        // paired by type and index, so they are corrected like copied ones.
        private void PairPrefab(Transform source, Transform target, bool pairComponents)
        {
            foreach (Transform part in source.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(part, source);
                Transform counterpart = path.Length == 0 ? target : target.Find(path);
                if (counterpart == null) continue;
                map.AddCreated(part, counterpart);
                if (!pairComponents) continue;
                var counts = new Dictionary<Type, int>();
                foreach (Component component in part.GetComponents<Component>())
                {
                    if (component == null || component is Transform) continue;
                    int index;
                    counts.TryGetValue(component.GetType(), out index);
                    counts[component.GetType()] = index + 1;
                    Component[] there = counterpart.GetComponents(component.GetType());
                    if (index < there.Length) copied[component] = there[index];
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant prefabs (AvatarMigratorCopyPrefabs.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarPrefabScope         Child prefab instances of an avatar (not the avatar's own prefab, found from its hips).
//   ChildPrefabRoot(t) / OutermostChildPrefabs() / Asset(root) / Overrides(root, label)  The last lists added components,
//                             added objects and removed components as a "Possible issues with" line.
// AvatarMigratorCopyRun (this part)
//   CopyPrefabs(settings)     Feature 11: each outermost child prefab, unless ignored: a missing asset is reported; one
//                             already on the target is paired (Replace removes it first); else InstantiatePrefab under the
//                             matched parent, same name, place and sibling index, SetPropertyModifications from the source.
//   PairPrefab(source, target, pairComponents)  Objects into the map by path (Created); for a new instance, components
//                             paired by type and index into copied (one already on the target is left as it is).
