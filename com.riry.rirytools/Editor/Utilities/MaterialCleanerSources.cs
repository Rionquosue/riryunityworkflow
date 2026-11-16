using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>Material Cleaner's materials to scan: those the open scenes use, or every one in the project's Assets.</summary>
    internal static class MaterialCleanerSources
    {
        /// <summary>
        /// Every material a component in the open scenes references (renderers, VRCFury, Modular Avatar and any other), each
        /// once. Materials in packages or inside models (read-only) are left out.
        /// </summary>
        internal static List<Material> InOpenScenes()
        {
            var found = new HashSet<Material>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) continue;
                        SerializedProperty property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Material material)
                                found.Add(material);
                        }
                    }
                }
            }
            return Editable(found);
        }

        /// <summary>Every material in Assets.</summary>
        internal static List<Material> InProject()
        {
            return Editable(AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))));
        }

        private static List<Material> Editable(IEnumerable<Material> materials)
        {
            return materials.Where(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith("Assets/", StringComparison.Ordinal) &&
                    AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(m => AssetDatabase.GetAssetPath(m), StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

// ============================================================================
// GLOSSARY: Material Cleaner sources
// ============================================================================
//
// MaterialCleanerSources (this file)
//   InOpenScenes()           Materials any component in the open scenes references, .mat files in Assets only.
//   InProject()              Every .mat in Assets.
