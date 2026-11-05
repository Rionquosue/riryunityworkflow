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
                if (root == null)
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
            // A model placed inside another unpacked model goes with the outer one's objects; outermost first.
            groups.RemoveAll(inner => groups.Exists(outer => outer != inner && inner.SourceRoot.IsChildOf(outer.SourceRoot)));
            return groups;
        }

        // The model asset of the avatar's humanoid Avatar (its body), or null.
        private static string BodyModel(Transform root)
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
        /// The object as many levels above the renderer as its path in the model has parts, when the names on the way match
        /// that path (the model's root may have been renamed); null when it would be the avatar root or above.
        /// </summary>
        private static Transform RootFor(Transform renderer, string inModel, Transform sourceRoot)
        {
            Transform current = renderer;
            if (inModel.Length > 0)
            {
                string[] parts = inModel.Split('/');
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    if (current == null || current.name != parts[i]) return null;
                    current = current.parent;
                }
            }
            return current != null && current != sourceRoot && current.IsChildOf(sourceRoot) ? current : null;
        }
    }

    internal sealed partial class AvatarCopyRun
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
                int removed = Pair(group.SourceRoot, instance.transform);
                // Objects the user added inside it are added to the instance too (added-object overrides), so their
                // components have somewhere to go; child prefabs inside it are placed by the Prefabs step.
                foreach (Transform added in group.SourceRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (InScope(added) && map.TargetOf(added) == null) TargetFor(added, true);
                }
                report.Repacked.Add(where + ": " + group.ModelPath + " (" + group.Renderers.Count + " mesh" + (group.Renderers.Count == 1 ? "" : "es") +
                    (removed > 0 ? "; " + removed + " object(s) the old avatar no longer had removed from the instance" : "") + ")");
            }
        }

        // Pairs the instance with its source objects by path; returns how many instance objects were removed.
        private int Pair(Transform sourceGroup, Transform instance)
        {
            var gone = new List<Transform>();
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(part, instance);
                Transform source = path.Length == 0 ? sourceGroup : sourceGroup.Find(path);
                if (source == null)
                {
                    if (!gone.Exists(outer => part.IsChildOf(outer))) gone.Add(part);
                    continue;
                }
                if (gone.Exists(outer => part.IsChildOf(outer))) continue;
                map.AddCreated(source, part);
                CopyObjectState(source, part);
                PairComponents(source, part);
            }
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
                    try
                    {
                        Undo.DestroyObjectImmediate(component);
                    }
                    catch (Exception exception)
                    {
                        report.NotCopied.Add(Describe(component) + ": the old avatar removed it, but it could not be removed from the instance (" +
                            exception.Message + ").");
                    }
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
// GLOSSARY: Avatar Migration Assistant unpacked prefabs (RepackPrefabs.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarRepack (this file)
//   Group                    SourceRoot (where the model's root was), Model and ModelPath (the asset), Renderers.
//   Plan(map, inScope, problems)  Source SkinnedMeshRenderers in scope with no renderer at their match, whose mesh is in a
//                            model in the project other than either avatar's body model (BodyModel: the Animator's
//                            Avatar asset): the source object as many levels up as the mesh's path in the model, the
//                            names on the way matching (RootFor). Skipped, with why: mesh no longer in the model, layout
//                            changed, an object already at that place on the target, meshes of two models in one place.
//                            Models inside another planned model go with the outer one.
// AvatarCopyRun (this part)
//   RepackPrefabs()          The run's first step when the up-front check's "Place them as child prefabs" is Yes:
//                            InstantiatePrefab under the match of the source root's parent (made when missing), then Pair.
//   Pair(sourceGroup, instance)  Every instance object to the source object at the same path (map.AddCreated), with
//                            CopyObjectState and PairComponents; instance objects the source no longer has are removed
//                            (deactivated when Unity can't). Objects the user added inside it are made on the instance
//                            (TargetFor). Listed in the run report under Repacked.
//   CopyObjectState / PairComponents  Name, local pose, active, tag, layer; the model's components take the source's
//                            values (same type and index) or are removed; all recorded as prefab instance overrides.
