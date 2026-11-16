using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    internal sealed partial class AvatarMigratorCopyRun
    {
        /// <summary>
        /// Places each unpacked model (AvatarMigratorRepack.Plan) as a child prefab on the new avatar, where its source object's
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
            List<AvatarMigratorRepack.Group> groups = AvatarMigratorRepack.Plan(map, InScope, problems);
            report.NotCopied.AddRange(problems);
            foreach (AvatarMigratorRepack.Group group in groups)
            {
                string where = AnimationUtility.CalculateTransformPath(group.SourceRoot, sourceRoot);
                Transform parent = TargetFor(group.SourceRoot.parent, true);
                if (parent == null)
                {
                    report.NotCopied.Add(where + ": its parent has no place on the new avatar, so " + group.ModelPath + " was not placed.");
                    continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(group.Model, parent);
                Undo.RegisterCreatedObjectUndo(instance, AvatarMigratorWindow.Title);
                instance.transform.SetSiblingIndex(Mathf.Min(group.SourceRoot.GetSiblingIndex(), parent.childCount - 1));
                int removed = Pair(group, groups, instance.transform);
                // Objects the user added inside it are added to the instance too (added-object overrides), so their
                // components have somewhere to go; child prefabs inside it are placed by the Prefabs step, inner unpacked
                // models by their own group, after this one.
                foreach (Transform added in group.SourceRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (InScope(added) && map.TargetOf(added) == null && !AvatarMigratorRepack.InOtherGroup(added, group, groups)) TargetFor(added, true);
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
        private int Pair(AvatarMigratorRepack.Group group, List<AvatarMigratorRepack.Group> groups, Transform instance)
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
                    Undo.RecordObject(part.gameObject, AvatarMigratorWindow.Title);
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
        private void PairTree(AvatarMigratorRepack.Group group, List<AvatarMigratorRepack.Group> groups, Transform source, Transform part, List<Transform> gone)
        {
            map.AddCreated(source, part);
            CopyObjectState(source, part);
            PairComponents(source, part);
            var free = new List<Transform>();
            var leftAlone = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform child in source)
            {
                if (InScope(child) && !AvatarMigratorRepack.InOtherGroup(child, group, groups)) free.Add(child);
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
            Undo.RecordObject(part.gameObject, AvatarMigratorWindow.Title);
            Undo.RecordObject(part, AvatarMigratorWindow.Title);
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
                Undo.RecordObject(component, AvatarMigratorWindow.Title);
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
// GLOSSARY: Avatar Migration Assistant unpacked prefabs (AvatarMigratorCopyRun.RepackPrefabs.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
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
