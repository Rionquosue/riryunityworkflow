using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>Controller Cleaner's controllers to scan: those the open scenes use, or every one in the project's Assets.</summary>
    internal static class ControllerCleanerSources
    {
        /// <summary>
        /// Every animator controller a component in the open scenes references (Animators, avatar layers, VRCFury, Modular
        /// Avatar and any other), with an override controller's base, each once. Controllers in packages are left out.
        /// </summary>
        internal static List<AnimatorController> InOpenScenes()
        {
            var found = new HashSet<AnimatorController>();
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
                            Object value = property.objectReferenceValue;
                            if (value is AnimatorOverrideController overrides) value = overrides.runtimeAnimatorController;
                            if (value is AnimatorController controller) found.Add(controller);
                        }
                    }
                }
            }
            return Editable(found);
        }

        /// <summary>Every animator controller in Assets.</summary>
        internal static List<AnimatorController> InProject()
        {
            return Editable(AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid))));
        }

        private static List<AnimatorController> Editable(IEnumerable<AnimatorController> controllers)
        {
            return controllers.Where(c => c != null && AssetDatabase.GetAssetPath(c).StartsWith("Assets/", System.StringComparison.Ordinal))
                .Distinct().OrderBy(c => AssetDatabase.GetAssetPath(c), System.StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
