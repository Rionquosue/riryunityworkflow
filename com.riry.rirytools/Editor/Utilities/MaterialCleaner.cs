using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Material Cleaner's scan and clean: a material's texture, float and color entries (m_SavedProperties m_TexEnvs,
    /// m_Floats, m_Colors) its current shader does not have, left from a shader it used before, with the references they hold. A material whose shader is missing is
    /// left alone. Clean is undone with Ctrl+Z.
    /// </summary>
    internal static class MaterialCleaner
    {
        internal const string Title = "Material Cleaner";

        internal sealed class Result
        {
            internal Material Material;
            // The texture, float and color entries the shader does not have, as "name (kind)".
            internal readonly List<string> Stale = new List<string>();
        }

        // The saved property lists cleaned, with the word shown after each name.
        private static readonly string[] Lists = { "m_SavedProperties.m_TexEnvs", "m_SavedProperties.m_Floats", "m_SavedProperties.m_Colors" };
        private static readonly string[] Kinds = { "texture", "float", "color" };

        internal static Result Scan(Material material)
        {
            var result = new Result { Material = material };
            Shader shader = ShaderOf(material);
            if (shader == null) return result;
            using (var serialized = new SerializedObject(material))
            {
                for (int list = 0; list < Lists.Length; list++)
                {
                    SerializedProperty slots = serialized.FindProperty(Lists[list]);
                    for (int i = 0; slots != null && i < slots.arraySize; i++)
                    {
                        string name = slots.GetArrayElementAtIndex(i).FindPropertyRelative("first")?.stringValue;
                        if (!string.IsNullOrEmpty(name) && shader.FindPropertyIndex(name) < 0) result.Stale.Add(name + " (" + Kinds[list] + ")");
                    }
                }
            }
            return result;
        }

        /// <summary>Removes the stale texture, float and color entries (recorded for Undo); returns them as "name (kind)".</summary>
        internal static List<string> Clean(Result result)
        {
            var removed = new List<string>();
            Shader shader = ShaderOf(result.Material);
            if (shader == null) return removed;
            using (var serialized = new SerializedObject(result.Material))
            {
                for (int list = 0; list < Lists.Length; list++)
                {
                    SerializedProperty slots = serialized.FindProperty(Lists[list]);
                    var fromList = new List<string>();
                    for (int i = slots != null ? slots.arraySize - 1 : -1; i >= 0; i--)
                    {
                        string name = slots.GetArrayElementAtIndex(i).FindPropertyRelative("first")?.stringValue;
                        if (string.IsNullOrEmpty(name) || shader.FindPropertyIndex(name) >= 0) continue;
                        slots.DeleteArrayElementAtIndex(i);
                        fromList.Add(name + " (" + Kinds[list] + ")");
                    }
                    fromList.Reverse();
                    removed.AddRange(fromList);
                }
                serialized.ApplyModifiedProperties();
            }
            result.Stale.Clear();
            if (removed.Count > 0) EditorUtility.SetDirty(result.Material);
            return removed;
        }

        // The material's shader, or null when it is missing (Unity's error shader stands in for it).
        private static Shader ShaderOf(Material material)
        {
            if (material == null || material.shader == null) return null;
            return material.shader.name == "Hidden/InternalErrorShader" ? null : material.shader;
        }
    }
}

// ============================================================================
// GLOSSARY: Material Cleaner
// ============================================================================
//
// MaterialCleaner (this file)
//   Title                    "Material Cleaner": window title, dialogs and Undo name.
//   Result                   A material and its Stale texture slots (names the shader does not have).
//   Scan(material)           The stale slots; none when the shader is missing.
//   Clean(result)            Deletes them (SerializedObject, so Ctrl+Z undoes it); returns the names removed.
