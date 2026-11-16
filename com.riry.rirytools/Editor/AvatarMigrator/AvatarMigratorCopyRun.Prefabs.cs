using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 11: each outermost child prefab instance is instantiated again from its asset
    /// under the matched parent (made by feature 4 when needed), with the same name, place and
    /// property overrides. Its objects join the map and its components are paired with the
    /// source's, so references, rotation and scale are corrected like copied ones.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        private void CopyPrefabs(AvatarMigratorTypeSettings typeSettings)
        {
            foreach (Transform source in scope.OutermostChildPrefabs())
            {
                if (ignored.Contains(source)) continue;
                string label = "Prefab " + AnimationUtility.CalculateTransformPath(source, sourceRoot);
                GameObject asset = AvatarMigratorPrefabScope.Asset(source);
                if (asset == null)
                {
                    report.PossibleIssues.Add(label + ": its prefab asset is missing, so it was not copied.");
                    continue;
                }
                Transform existing = map.TargetOf(source);
                if (existing != null && AvatarMigratorPrefabScope.Asset(existing) == asset && PrefabUtility.GetNearestPrefabInstanceRoot(existing.gameObject) == existing.gameObject)
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
                Undo.RegisterCreatedObjectUndo(instance, AvatarMigratorWindow.Title);
                instance.name = source.name;
                instance.transform.localPosition = source.localPosition;
                instance.transform.localRotation = source.localRotation;
                instance.transform.localScale = source.localScale;
                instance.transform.SetSiblingIndex(Mathf.Min(source.GetSiblingIndex(), parent.childCount - 1));
                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(source.gameObject);
                if (modifications != null && modifications.Length > 0) PrefabUtility.SetPropertyModifications(instance, modifications);
                PairPrefab(source, instance.transform, true);
                report.CopiedPrefabs++;
                string issues = AvatarMigratorPrefabScope.Overrides(source, label);
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
// GLOSSARY: Avatar Migration Assistant prefabs (AvatarMigratorCopyRun.Prefabs.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   CopyPrefabs(settings)     Feature 11: each outermost child prefab, unless ignored: a missing asset is reported; one
//                             already on the target is paired (Replace removes it first); else InstantiatePrefab under the
//                             matched parent, same name, place and sibling index, SetPropertyModifications from the source.
//   PairPrefab(source, target, pairComponents)  Objects into the map by path (Created); for a new instance, components
//                             paired by type and index into copied (one already on the target is left as it is).
