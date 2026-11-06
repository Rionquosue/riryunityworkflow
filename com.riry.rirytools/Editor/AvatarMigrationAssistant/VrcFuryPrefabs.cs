using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 9: child prefabs are left out of retargeting unless the user includes them,
    /// one checkbox per prefab. In an included prefab, a VRCFury component is changed only
    /// when its animations target the avatar wrongly (a path that is not on the new avatar):
    /// it is copied, the copy is repaired, and the original is removed, as a scene override
    /// of the prefab (nothing is unpacked). A Full Controller gets Path Rewrite Rules
    /// (old path to new path) and VRCFury does the rest; any other VRCFury feature has its
    /// clips duplicated into the migrated folder and repathed.
    /// </summary>
    internal sealed partial class AvatarAnimationRetarget
    {
        private const string VrcFuryType = "VF.Model.VRCFury";
        private const string RewriteList = "rewriteBindings";

        private void RetargetVrcFury()
        {
            if (includedPrefabs.Count == 0) return;
            Type type = AvatarCopyTypes.Find(VrcFuryType);
            if (type == null)
            {
                report.PossibleIssues.Add("VRCFury in the included child prefabs: " + VrcFuryType + " was not found (VRCFury is not " +
                    "installed or was renamed), so it was not checked.");
                return;
            }
            Transform targetRoot = map.TargetRoot;
            foreach (Transform prefab in includedPrefabs)
            {
                if (prefab == null) continue;
                foreach (Component original in prefab.GetComponentsInChildren(type, true))
                {
                    var broken = new Dictionary<string, string>(StringComparer.Ordinal);
                    var clips = new List<AnimationClip>();
                    var serialized = new SerializedObject(original);
                    bool fullController = false;
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.name == RewriteList && property.isArray) fullController = true;
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object value = property.objectReferenceValue;
                        var clip = value as AnimationClip;
                        if (clip != null) clips.Add(clip);
                        var controller = value as RuntimeAnimatorController;
                        if (controller != null) clips.AddRange(controller.animationClips);
                    }
                    foreach (AnimationClip clip in clips)
                    {
                        if (clip == null) continue;
                        foreach (string path in Paths(clip))
                        {
                            string rebuilt;
                            if ((path.Length == 0 ? targetRoot : targetRoot.Find(path)) != null) continue;
                            if (NewPath(path, map.SourceRoot, targetRoot, out rebuilt) && rebuilt != path) broken[path] = rebuilt;
                        }
                    }
                    // Only VRCFury items that target wrongly are changed; the rest are left as they are.
                    if (broken.Count == 0) continue;
                    Repair(original, type, broken, fullController);
                }
            }
        }

        // Whether the rewrite rules have the from and to fields Repair writes (checked on a temporary element).
        private static bool HasRuleFields(SerializedObject serialized)
        {
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.name != RewriteList || !property.isArray) continue;
                property.InsertArrayElementAtIndex(property.arraySize);
                SerializedProperty rule = property.GetArrayElementAtIndex(property.arraySize - 1);
                bool has = rule.FindPropertyRelative("from") != null && rule.FindPropertyRelative("to") != null;
                property.DeleteArrayElementAtIndex(property.arraySize - 1);
                return has;
            }
            return false;
        }

        private void Repair(Component original, Type type, Dictionary<string, string> broken, bool fullController)
        {
            Component copy = Undo.AddComponent(original.gameObject, type);
            EditorUtility.CopySerialized(original, copy);
            var serialized = new SerializedObject(copy);
            string where = AnimationUtility.CalculateTransformPath(original.transform, map.TargetRoot);
            if (fullController && !HasRuleFields(serialized))
            {
                // Without from/to the rules would be empty: the original stays as it was, and the report says so.
                Undo.DestroyObjectImmediate(copy);
                report.NotCopied.Add("VRCFury on " + where + ": this VRCFury version's Path Rewrite Rules have no from/to fields, " +
                    "so its " + broken.Count + " broken path(s) were not repaired; it was left as it was.");
                return;
            }
            if (fullController)
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.name != RewriteList || !property.isArray) continue;
                    // Longest paths first, so a deeper rule wins over a shorter one it starts with.
                    var paths = new List<string>(broken.Keys);
                    paths.Sort((left, right) => right.Length.CompareTo(left.Length));
                    foreach (string path in paths)
                    {
                        property.InsertArrayElementAtIndex(property.arraySize);
                        SerializedProperty rule = property.GetArrayElementAtIndex(property.arraySize - 1);
                        SerializedProperty from = rule.FindPropertyRelative("from");
                        SerializedProperty to = rule.FindPropertyRelative("to");
                        SerializedProperty delete = rule.FindPropertyRelative("delete");
                        if (from != null) from.stringValue = path;
                        if (to != null) to.stringValue = broken[path];
                        if (delete != null) delete.boolValue = false;
                    }
                    break;
                }
            }
            else
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var clip = property.objectReferenceValue as AnimationClip;
                    if (clip != null)
                    {
                        property.objectReferenceValue = DuplicateClip(clip, map.SourceRoot, map.TargetRoot, null);
                        continue;
                    }
                    var controller = property.objectReferenceValue as RuntimeAnimatorController;
                    if (controller != null) property.objectReferenceValue = DuplicateController(controller, map.SourceRoot, map.TargetRoot);
                }
            }
            serialized.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(original);
            report.PossibleIssues.Add("VRCFury on " + AnimationUtility.CalculateTransformPath(copy.transform, map.TargetRoot) + " was repaired (" +
                broken.Count + " path" + (broken.Count == 1 ? "" : "s") + (fullController ? " added as Path Rewrite Rules" : " retargeted in duplicated clips") +
                "), as a scene override of its prefab. Check it in Play mode.");
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant VRCFury (VrcFuryPrefabs.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarAnimationRetarget (this part)
//   RetargetVrcFury()   In each child prefab the user included: each VF.Model.VRCFury whose clips (its own, and its
//                       controllers') have paths not on the new avatar that the map can rebuild.
//   HasRuleFields       Whether the rewrite rules have from/to; without them Repair removes its copy, keeps the original
//                       and lists it under Not copied. A missing VRCFury type with child prefabs included is a Possible issue.
//   Repair(...)         Adds a copy (CopySerialized), repairs it, removes the original (a removed-component override; the
//                       prefab is not unpacked). Full Controller (rewriteBindings): a from/to Path Rewrite Rule per broken
//                       path, longest first. Other features: clips and controllers duplicated and repathed.
