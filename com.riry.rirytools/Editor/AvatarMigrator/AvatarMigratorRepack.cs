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
    internal static class AvatarMigratorRepack
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
        internal static List<Group> Plan(AvatarMigratorMatchObjects map, Func<Transform, bool> inScope, List<string> problems)
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
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant unpacked prefab plan (AvatarMigratorRepack.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorRepack
//   Group                    SourceRoot (where the model's root was), Model and ModelPath (the asset), Renderers.
//   Plan(map, inScope, problems)  Source SkinnedMeshRenderers in scope with no renderer at their match, whose mesh is in a
//                            model in the project other than either avatar's body model (BodyModel: the Animator's
//                            Avatar asset): the source object as many levels up as the mesh's path in the model, the
//                            names on the way matching (RootFor). Skipped, with why: mesh no longer in the model, layout
//                            changed (Fit under LayoutFit, 0.5: share of the model's objects at their name path; the
//                            root is found by depth, RootFor, so renames are fine), an object already at that place on
//                            the target, meshes of two models in one place. Outermost first; inner models are placed
//                            after, inside the outer instance (InOtherGroup).
