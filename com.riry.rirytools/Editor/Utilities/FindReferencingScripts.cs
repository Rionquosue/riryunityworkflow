using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Find scripts that reference this (Hierarchy right-click): the components in the same avatar whose fields point at the
    /// object or one of its components, such as a constraint's source or a joint's connected body. The popup is
    /// UI/Utilities/FindReferencingScriptsWindow.cs.
    /// </summary>
    internal static class FindReferencingScripts
    {
        internal const string Title = "Find scripts that reference this";

        internal sealed class Result
        {
            internal Component Source;
            // The field's path, such as "Sources[0].SourceTransform".
            internal string Field;
            internal Object Target;
        }

        /// <summary>
        /// Every field, in the components under the object's avatar root (InvertGlobalPosition.AvatarRoot; the top of its
        /// hierarchy when it is in no avatar), that points at the object or one of its components. A component's own
        /// GameObject and Transform hierarchy links are not counted.
        /// </summary>
        internal static List<Result> Find(GameObject target)
        {
            var results = new List<Result>();
            if (target == null) return results;
            Transform root = InvertGlobalPosition.AvatarRoot(target.transform);
            if (root == null) root = target.transform.root;
            var targets = new HashSet<Object> { target };
            foreach (Component component in target.GetComponents<Component>())
            {
                if (component != null) targets.Add(component);
            }
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                // Missing scripts are null; a Transform's references are its parent and children.
                if (component == null || component is Transform) continue;
                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_GameObject") continue;
                        Object value = property.objectReferenceValue;
                        if (value == null || !targets.Contains(value)) continue;
                        results.Add(new Result { Source = component, Field = property.propertyPath.Replace(".Array.data[", "["), Target = value });
                    }
                }
            }
            return results;
        }
    }
}

// ============================================================================
// GLOSSARY: FindReferencingScripts
//   Title                    "Find scripts that reference this": the menu entry, popup title and log name.
//   Result                   Source (the component holding the field), Field (its path, ".Array.data[" shortened to "["), Target
//                            (the object or component it points at).
//   Find(target)             Walks every serialized object reference of every component under the avatar root (or the top of
//                            the hierarchy), skipping Transforms and m_GameObject.
// ============================================================================
