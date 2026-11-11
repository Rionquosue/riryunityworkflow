using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 5: after every copy step, each object reference in a copied component that
    /// points into the source avatar is pointed at the target. Every serialized property is
    /// walked (constraint sources and worldUpObject, joint connectedBody, SkinnedMeshRenderer
    /// bones, rootBone and probeAnchor, Avatar Descriptor eye bones, eyelid and viseme meshes
    /// and colliders are all among them). A GameObject goes to its match; a Component to its
    /// copy, else to the component of the same type at the same index on the match; anything
    /// that cannot be found is set to none and reported. Asset references and references to
    /// objects outside both avatars are left as they are. Feature 12's duplicate check is here too.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        // Properties that are Unity's own links, never re-pointed or compared.
        private static readonly HashSet<string> LinkProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_Script", "m_GameObject", "m_PrefabInstance", "m_PrefabAsset", "m_CorrespondingSourceObject", "m_PrefabInternal",
            "m_PrefabParentObject", "m_Father", "m_Children", "m_ObjectHideFlags", "m_EditorHideFlags", "m_EditorClassIdentifier"
        };

        private IEnumerable<Component> Written()
        {
            var seen = new HashSet<Component>();
            foreach (Component component in copied.Values)
            {
                if (component != null && seen.Add(component)) yield return component;
            }
            foreach (Component component in made)
            {
                if (component != null && seen.Add(component)) yield return component;
            }
        }

        private void RepointReferences()
        {
            foreach (Component written in new List<Component>(Written()))
            {
                var serialized = new SerializedObject(written);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || LinkProperties.Contains(property.name)) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || !InSource(value)) continue;
                    Object resolved = Resolve(value);
                    property.objectReferenceValue = resolved;
                    changed = true;
                    if (resolved == null)
                        report.ReferencesCleared.Add(Describe(written) + " > " + property.propertyPath + ": " + value.name + " has no match on the target.");
                }
                if (changed) serialized.ApplyModifiedProperties();
            }
        }

        // Whether an object is in the source avatar (or the temporary constraint copy of it).
        private bool InSource(Object value)
        {
            if (alias.ContainsKey(value)) return true;
            Transform transform = value is GameObject ? ((GameObject)value).transform : value is Component ? ((Component)value).transform : null;
            return transform != null && (transform == sourceRoot || transform.IsChildOf(sourceRoot));
        }

        private Object Original(Object value)
        {
            Object original;
            return value != null && alias.TryGetValue(value, out original) ? original : value;
        }

        private Transform OriginalTransform(Transform transform)
        {
            return transform == null ? null : Original(transform) as Transform;
        }

        /// <summary>The target counterpart of a source GameObject or Component, or null.</summary>
        private Object Resolve(Object value)
        {
            Component copy;
            var asComponent = value as Component;
            if (asComponent != null && copied.TryGetValue(asComponent, out copy)) return copy;
            value = Original(value);
            var gameObject = value as GameObject;
            if (gameObject != null)
            {
                Transform match = map.TargetOf(gameObject.transform);
                return match != null ? match.gameObject : null;
            }
            var component = value as Component;
            if (component == null) return value;
            if (copied.TryGetValue(component, out copy)) return copy;
            Transform target = map.TargetOf(component.transform);
            if (target == null) return null;
            if (component is Transform) return target;
            Component[] same = component.GetComponents(component.GetType());
            Component[] there = target.GetComponents(component.GetType());
            int index = Array.IndexOf(same, component);
            return index >= 0 && index < there.Length ? there[index] : null;
        }

        // ---- Feature 12: add, but skip a component the target already had ---------

        /// <summary>
        /// A component this run added is dropped when the same object already had one of the
        /// same type with every serialized value equal, now that references are re-pointed and
        /// values corrected. References to the dropped one go to the one that was there.
        /// </summary>
        private void RemoveDuplicates()
        {
            var duplicates = new Dictionary<Component, Component>();
            foreach (Component added in made)
            {
                if (added == null || OncePerObject(added.GetType())) continue;
                foreach (Component existing in added.GetComponents(added.GetType()))
                {
                    if (existing == added || existing.GetType() != added.GetType() || made.Contains(existing)) continue;
                    if (!SameValues(added, existing)) continue;
                    duplicates[added] = existing;
                    break;
                }
            }
            if (duplicates.Count == 0) return;

            foreach (Component written in new List<Component>(Written()))
            {
                if (duplicates.ContainsKey(written)) continue;
                var serialized = new SerializedObject(written);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || LinkProperties.Contains(property.name)) continue;
                    var value = property.objectReferenceValue as Component;
                    Component kept;
                    if (value == null || !duplicates.TryGetValue(value, out kept)) continue;
                    property.objectReferenceValue = kept;
                    changed = true;
                }
                if (changed) serialized.ApplyModifiedProperties();
            }
            foreach (KeyValuePair<Component, Component> duplicate in duplicates)
            {
                foreach (Component source in new List<Component>(copied.Keys))
                {
                    if (copied[source] == duplicate.Key) copied[source] = duplicate.Value;
                }
                made.Remove(duplicate.Key);
                Undo.DestroyObjectImmediate(duplicate.Key);
                report.SkippedIdentical++;
            }
        }

        private static bool SameValues(Component left, Component right)
        {
            var leftObject = new SerializedObject(left);
            var rightObject = new SerializedObject(right);
            SerializedProperty property = leftObject.GetIterator();
            bool enter = true;
            while (property.Next(enter))
            {
                enter = false;
                if (LinkProperties.Contains(property.name)) continue;
                SerializedProperty other = rightObject.FindProperty(property.propertyPath);
                if (other == null || !SerializedProperty.DataEquals(property, other)) return false;
            }
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant references (AvatarMigratorRepointReferences.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   RepointReferences()   Every object reference of every written component (copied and made) that points into the
//                         source (InSource) is set to Resolve(value); none when it has no match (ReferencesCleared).
//   Resolve(value)        Its copy; a GameObject's match; else the same type at the same index on the match.
//   Original / alias      Objects of the temporary constraint copy stand for the source objects they copy.
//   RemoveDuplicates()    Feature 12 (add): a made component equal in every serialized value (SameValues) to one the
//                         object already had is dropped, and references to it go to the one that was there.
//   LinkProperties        Unity's own link fields, never re-pointed or compared.
