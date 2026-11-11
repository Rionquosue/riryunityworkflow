using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Unpacked prefabs on the old avatar, found through their skinned meshes: each SkinnedMeshRenderer that has no
    /// counterpart on the new avatar and whose mesh comes from a model in the project (not the avatar's own body model)
    /// marks the object where that model was placed and later unpacked. Plan finds them; the run places each model again
    /// as a child prefab on the new avatar (RepackPrefabs), so it is a prefab instance once more.
    /// </summary>
    internal static class AvatarRepack
    {
        /// <summary>One unpacked model: the source object where its root was, and the model asset.</summary>
        internal sealed class Group
        {
            public Transform SourceRoot;
            public GameObject Model;
            public string ModelPath;
            public readonly List<SkinnedMeshRenderer> Renderers = new List<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// The unpacked models under the source, outermost first; <paramref name="problems"/> gets each skinned mesh that
        /// looks unpacked but can't be placed again, with why.
        /// </summary>
        internal static List<Group> Plan(AvatarMigrationMap map, Func<Transform, bool> inScope, List<string> problems)
        {
            var groups = new List<Group>();
            Transform sourceRoot = map.SourceRoot;
            string sourceBody = BodyModel(sourceRoot), targetBody = BodyModel(map.TargetRoot);
            foreach (SkinnedMeshRenderer renderer in sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!inScope(renderer.transform) || renderer.sharedMesh == null) continue;
                Transform match = map.TargetOf(renderer.transform);
                if (match != null && match.GetComponent<SkinnedMeshRenderer>() != null) continue;
                string path = AssetDatabase.GetAssetPath(renderer.sharedMesh);
                // The body comes with the new avatar; a mesh saved on its own is not a model to place.
                if (string.IsNullOrEmpty(path) || path == sourceBody || path == targetBody) continue;
                var model = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                if (model == null) continue;
                string where = AnimationUtility.CalculateTransformPath(renderer.transform, sourceRoot);
                string inModel = PathInModel(model, renderer.sharedMesh);
                if (inModel == null)
                {
                    problems.Add(where + ": its mesh is no longer in " + path + ", so it can't be placed as a prefab again.");
                    continue;
                }
                Transform root = RootFor(renderer.transform, inModel, sourceRoot);
                if (root == null || Fit(model.transform, root) < LayoutFit)
                {
                    problems.Add(where + ": its objects are no longer laid out as in " + path + " (" + inModel + "), so it can't be " +
                        "placed as a prefab again.");
                    continue;
                }
                if (map.TargetOf(root) != null)
                {
                    problems.Add(where + ": the new avatar already has an object at " + AnimationUtility.CalculateTransformPath(root, sourceRoot) +
                        " without this mesh, so " + path + " was not placed there.");
                    continue;
                }
                Group group = groups.Find(item => item.SourceRoot == root);
                if (group == null)
                {
                    groups.Add(group = new Group { SourceRoot = root, Model = model, ModelPath = path });
                }
                else if (group.Model != model)
                {
                    problems.Add(where + ": " + AnimationUtility.CalculateTransformPath(root, sourceRoot) + " holds meshes from more than " +
                        "one model, so only " + group.ModelPath + " is placed there.");
                    continue;
                }
                group.Renderers.Add(renderer);
            }
            // Outermost first: a model placed inside another unpacked model is placed after it, inside the outer instance.
            groups.Sort((left, right) => Depth(left.SourceRoot).CompareTo(Depth(right.SourceRoot)));
            return groups;
        }

        /// <summary>Whether the object is inside another group's source objects than its own (an inner model).</summary>
        internal static bool InOtherGroup(Transform source, Group own, List<Group> groups)
        {
            return groups.Exists(group => group != own && group.SourceRoot != own.SourceRoot &&
                (source == group.SourceRoot || source.IsChildOf(group.SourceRoot)) && group.SourceRoot.IsChildOf(own.SourceRoot));
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;
            for (Transform current = transform; current != null; current = current.parent) depth++;
            return depth;
        }

        // Share of the model's objects found at their name path under the root, at least this, for the layout to count.
        private const float LayoutFit = 0.5f;

        // The share of the model's objects that are under the root at the same name path (a few renamed or moved ones are fine).
        private static float Fit(Transform model, Transform root)
        {
            Transform[] parts = model.GetComponentsInChildren<Transform>(true);
            int found = 0;
            foreach (Transform part in parts)
            {
                string path = AnimationUtility.CalculateTransformPath(part, model);
                if (path.Length == 0 || root.Find(path) != null) found++;
            }
            return found / (float)parts.Length;
        }

        // The model asset of the avatar's humanoid Avatar (its body), or null.
        internal static string BodyModel(Transform root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar != null) return AssetDatabase.GetAssetPath(animator.avatar);
            }
            return null;
        }

        // The path, in the model, of its renderer that uses this mesh, or null.
        private static string PathInModel(GameObject model, Mesh mesh)
        {
            foreach (SkinnedMeshRenderer original in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (original.sharedMesh == mesh) return AnimationUtility.CalculateTransformPath(original.transform, model.transform);
            }
            return null;
        }

        /// <summary>
        /// The object as many levels above the renderer as its path in the model has parts (names may differ: renamed
        /// objects are the user's changes; Fit checks the layout); null when it would be the avatar root or above.
        /// </summary>
        private static Transform RootFor(Transform renderer, string inModel, Transform sourceRoot)
        {
            Transform current = renderer;
            int levels = inModel.Length == 0 ? 0 : inModel.Split('/').Length;
            for (int i = 0; i < levels && current != null; i++) current = current.parent;
            return current != null && current != sourceRoot && current.IsChildOf(sourceRoot) ? current : null;
        }
    }

    internal sealed partial class AvatarMigratorCopyRun
    {
        /// <summary>
        /// Places each unpacked model (AvatarRepack.Plan) as a child prefab on the new avatar, where its source object's
        /// parent matches, and turns the old avatar's changes into overrides on it: names, transforms, active state, tags
        /// and layers, and every component the model has, pasted from the source (removed when the source lost it).
        /// Objects of the model the source no longer has are removed from the instance (deactivated when Unity can't).
        /// Objects the user added inside it are made on the instance (added-object overrides). Every object is added to
        /// the map, so the rest of the run copies the components the user added onto the instance (added-component
        /// overrides) and re-points references to it.
        /// </summary>
        private void RepackPrefabs()
        {
            var problems = new List<string>();
            List<AvatarRepack.Group> groups = AvatarRepack.Plan(map, InScope, problems);
            report.NotCopied.AddRange(problems);
            foreach (AvatarRepack.Group group in groups)
            {
                string where = AnimationUtility.CalculateTransformPath(group.SourceRoot, sourceRoot);
                Transform parent = TargetFor(group.SourceRoot.parent, true);
                if (parent == null)
                {
                    report.NotCopied.Add(where + ": its parent has no place on the new avatar, so " + group.ModelPath + " was not placed.");
                    continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(group.Model, parent);
                Undo.RegisterCreatedObjectUndo(instance, AvatarMigrationAssistant.Title);
                instance.transform.SetSiblingIndex(Mathf.Min(group.SourceRoot.GetSiblingIndex(), parent.childCount - 1));
                int removed = Pair(group, groups, instance.transform);
                // Objects the user added inside it are added to the instance too (added-object overrides), so their
                // components have somewhere to go; child prefabs inside it are placed by the Prefabs step, inner unpacked
                // models by their own group, after this one.
                foreach (Transform added in group.SourceRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (InScope(added) && map.TargetOf(added) == null && !AvatarRepack.InOtherGroup(added, group, groups)) TargetFor(added, true);
                }
                report.RepackedRoots.Add(instance);
                report.Repacked.Add(where + ": " + group.ModelPath + " (" + group.Renderers.Count + " mesh" + (group.Renderers.Count == 1 ? "" : "es") +
                    (removed > 0 ? "; " + removed + " object(s) the old avatar no longer had removed from the instance" : "") + ")");
            }
        }

        /// <summary>
        /// Pairs the instance with the group's source objects level by level (PairTree); removes the instance objects the
        /// source no longer has. Returns how many were removed.
        /// </summary>
        private int Pair(AvatarRepack.Group group, List<AvatarRepack.Group> groups, Transform instance)
        {
            var gone = new List<Transform>();
            PairTree(group, groups, group.SourceRoot, instance, gone);
            int removed = 0;
            foreach (Transform part in gone)
            {
                string name = AnimationUtility.CalculateTransformPath(part, instance);
                try
                {
                    Undo.DestroyObjectImmediate(part.gameObject);
                    removed++;
                }
                catch (Exception exception)
                {
                    Undo.RecordObject(part.gameObject, AvatarMigrationAssistant.Title);
                    part.gameObject.SetActive(false);
                    report.PossibleIssues.Add(instance.name + "/" + name + ": the old avatar no longer has it, and Unity could not remove it " +
                        "from the prefab instance (" + exception.Message + "), so it was deactivated.");
                }
            }
            return removed;
        }

        /// <summary>
        /// One object and its children: the instance child takes the source child of the same name, else (a renamed one)
        /// the left-over source child of the same make (SameMake); an instance child with neither is gone. A source child
        /// left out of copying (ignored), or in an inner group, leaves the instance child of its name untouched.
        /// </summary>
        private void PairTree(AvatarRepack.Group group, List<AvatarRepack.Group> groups, Transform source, Transform part, List<Transform> gone)
        {
            map.AddCreated(source, part);
            CopyObjectState(source, part);
            PairComponents(source, part);
            var free = new List<Transform>();
            var leftAlone = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform child in source)
            {
                if (InScope(child) && !AvatarRepack.InOtherGroup(child, group, groups)) free.Add(child);
                else leftAlone.Add(child.name);
            }
            var children = new List<Transform>();
            foreach (Transform child in part) children.Add(child);
            var pairs = new List<KeyValuePair<Transform, Transform>>();
            var unnamed = new List<Transform>();
            // Names decide first, before any instance child is renamed.
            foreach (Transform child in children)
            {
                Transform named = free.Find(candidate => candidate.name == child.name);
                if (named != null)
                {
                    free.Remove(named);
                    pairs.Add(new KeyValuePair<Transform, Transform>(named, child));
                }
                else if (!leftAlone.Contains(child.name)) unnamed.Add(child);
            }
            foreach (Transform child in unnamed)
            {
                Transform renamed = free.Find(candidate => SameMake(candidate, child));
                if (renamed == null)
                {
                    gone.Add(child);
                    continue;
                }
                free.Remove(renamed);
                pairs.Add(new KeyValuePair<Transform, Transform>(renamed, child));
            }
            foreach (KeyValuePair<Transform, Transform> pair in pairs) PairTree(group, groups, pair.Key, pair.Value, gone);
        }

        /// <summary>
        /// A renamed object: the source has every component the model gives it (it may have more, the user's own), with the
        /// same mesh on a skinned renderer; an object with nothing but its Transform (a bone) also needs as many children.
        /// </summary>
        private static bool SameMake(Transform source, Transform part)
        {
            var theirs = new List<Component>(source.GetComponents<Component>());
            bool plain = true;
            foreach (Component component in part.GetComponents<Component>())
            {
                if (component == null || component is Transform) continue;
                plain = false;
                Component same = theirs.Find(candidate => candidate != null && candidate.GetType() == component.GetType());
                if (same == null) return false;
                theirs.Remove(same);
                var renderer = component as SkinnedMeshRenderer;
                if (renderer != null && renderer.sharedMesh != ((SkinnedMeshRenderer)same).sharedMesh) return false;
            }
            return !plain || source.childCount == part.childCount;
        }

        // A component on the object whose type requires this one (RequireComponent), so Unity won't remove it; or null.
        private static Component RequiredBy(Component component)
        {
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == null || other == component) continue;
                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    foreach (Type needed in new[] { require.m_Type0, require.m_Type1, require.m_Type2 })
                    {
                        if (needed != null && needed.IsInstanceOfType(component)) return other;
                    }
                }
            }
            return null;
        }

        // Name, local pose, active state, tag and layer as on the source: overrides where they differ from the model.
        private void CopyObjectState(Transform source, Transform part)
        {
            Undo.RecordObject(part.gameObject, AvatarMigrationAssistant.Title);
            Undo.RecordObject(part, AvatarMigrationAssistant.Title);
            part.gameObject.name = source.name;
            part.localPosition = source.localPosition;
            part.localRotation = source.localRotation;
            part.localScale = source.localScale;
            part.gameObject.SetActive(source.gameObject.activeSelf);
            part.gameObject.layer = source.gameObject.layer;
            try
            {
                part.gameObject.tag = source.gameObject.tag;
            }
            catch (UnityException exception)
            {
                report.NotCopied.Add("Tag of " + part.name + ": " + exception.Message);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
        }

        /// <summary>
        /// Each component the model gives the object takes the values of the source's component of the same type and
        /// index (an override where they differ; references are re-pointed later), or is removed when the source has none.
        /// </summary>
        private void PairComponents(Transform source, Transform part)
        {
            foreach (Component component in part.GetComponents<Component>())
            {
                if (component == null || component is Transform) continue;
                Type type = component.GetType();
                Component[] mine = part.GetComponents(type), theirs = source.GetComponents(type);
                int index = Array.IndexOf(mine, component);
                if (index < 0 || index >= theirs.Length)
                {
                    // Unity refuses to remove a component another one needs (logging its own error), so that is checked first.
                    Component needs = RequiredBy(component);
                    if (needs != null)
                    {
                        report.NotCopied.Add(Describe(component) + ": the old avatar removed it, but " + ObjectNames.NicifyVariableName(needs.GetType().Name) +
                            " needs it, so it was kept on the placed prefab.");
                        continue;
                    }
                    string described = Describe(component);
                    try
                    {
                        Undo.DestroyObjectImmediate(component);
                    }
                    catch (Exception exception)
                    {
                        report.NotCopied.Add(described + ": the old avatar removed it, but it could not be removed from the instance (" +
                            exception.Message + ").");
                        continue;
                    }
                    if (component != null)
                        report.NotCopied.Add(described + ": the old avatar removed it, but Unity kept it on the placed prefab.");
                    continue;
                }
                Undo.RecordObject(component, AvatarMigrationAssistant.Title);
                if (!ComponentUtility.CopyComponent(theirs[index]) || !ComponentUtility.PasteComponentValues(component))
                {
                    report.NotCopied.Add(Describe(theirs[index]) + ": its values could not be copied onto the placed prefab.");
                    continue;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                copied[theirs[index]] = component;
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant unpacked prefabs (AvatarMigratorRepackPrefabs.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarRepack (this file)
//   Group                    SourceRoot (where the model's root was), Model and ModelPath (the asset), Renderers.
//   Plan(map, inScope, problems)  Source SkinnedMeshRenderers in scope with no renderer at their match, whose mesh is in a
//                            model in the project other than either avatar's body model (BodyModel: the Animator's
//                            Avatar asset): the source object as many levels up as the mesh's path in the model, the
//                            names on the way matching (RootFor). Skipped, with why: mesh no longer in the model, layout
//                            changed (Fit under LayoutFit, 0.5: share of the model's objects at their name path; the
//                            root is found by depth, RootFor, so renames are fine), an object already at that place on
//                            the target, meshes of two models in one place. Outermost first; inner models are placed
//                            after, inside the outer instance (InOtherGroup).
// AvatarMigratorCopyRun (this part)
//   RepackPrefabs()          The run's first step when the up-front check's "Place them as child prefabs" is Yes:
//                            InstantiatePrefab under the match of the source root's parent (made when missing), then Pair.
//   Pair / PairTree          Level by level: an instance child takes the source child of its name, else a left-over one
//                            of the same make (SameMake: a renamed object), with map.AddCreated, CopyObjectState and
//                            PairComponents; ignored and inner-model children leave theirs alone; instance objects the source no longer has are removed
//                            (deactivated when Unity can't). Objects the user added inside it are made on the instance
//                            (TargetFor). Listed in the run report under Repacked.
//   CopyObjectState / PairComponents  Name, local pose, active, tag, layer; the model's components take the source's
//                            values (same type and index) or are removed (kept and reported when another component needs
//                            it, RequiredBy, or Unity keeps it); all recorded as prefab instance overrides. The placed
//                            roots go in the report (RepackedRoots) and the window ticks them for Retarget animations.
