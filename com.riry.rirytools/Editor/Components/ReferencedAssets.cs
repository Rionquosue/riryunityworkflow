using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The assets of one type a tool scans: those any component in the open scenes references, or every one in the
    /// project's Assets. Used by Controller Cleaner (ControllerCleanerSources) and Material Cleaner
    /// (MaterialCleanerSources). Only assets in Assets are returned, each once, sorted by path; with an extension, only
    /// files that end with it.
    /// </summary>
    internal static class ReferencedAssets
    {
        /// <summary>
        /// Every asset a component in the open scenes references (any component: VRCFury, Modular Avatar and the rest
        /// included). pick turns a referenced object into the asset to list, or null to leave it out.
        /// </summary>
        internal static List<T> InOpenScenes<T>(Func<Object, T> pick, string extension = null) where T : Object
        {
            var found = new HashSet<T>();
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
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            T asset = pick(property.objectReferenceValue);
                            if (asset != null) found.Add(asset);
                        }
                    }
                }
            }
            return Editable(found, extension);
        }

        /// <summary>Every asset in Assets that an AssetDatabase filter ("t:Material") finds.</summary>
        internal static List<T> InProject<T>(string filter, string extension = null) where T : Object
        {
            return Editable(AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))), extension);
        }

        private static List<T> Editable<T>(IEnumerable<T> assets, string extension) where T : Object
        {
            return assets.Where(a => a != null && AssetDatabase.GetAssetPath(a).StartsWith("Assets/", StringComparison.Ordinal) &&
                    (extension == null || AssetDatabase.GetAssetPath(a).EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                .Distinct().OrderBy(a => AssetDatabase.GetAssetPath(a), StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

// ============================================================================
// GLOSSARY: referenced assets
// ============================================================================
//
// ReferencedAssets (this file)
//   InOpenScenes<T>(pick, extension)  Assets any component in the open scenes references, as pick returns them.
//   InProject<T>(filter, extension)   Every asset in Assets the filter finds.
//   Editable(assets, extension)       In Assets (and ending with the extension), each once, sorted by path.
