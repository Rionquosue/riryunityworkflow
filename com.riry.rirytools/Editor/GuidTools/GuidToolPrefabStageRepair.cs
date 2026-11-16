using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Broken References' repair of the prefab open in Prefab Mode, in memory: each empty reference whose missing GUID has
    /// a redirect is pointed at the object with the same file ID in the redirect's asset, as one undo step, and the prefab
    /// is left unsaved for the user to save. The prefab's file on disk is not rewritten.
    /// </summary>
    internal static class GuidToolPrefabStageRepair
    {
        /// <summary>Returns how many references were set; <paramref name="unresolved"/> receives those with no matching object.</summary>
        internal static int Apply(PrefabStage stage, Dictionary<string, string> redirects, List<string> unresolved)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(GuidToolReferenceRepair.Title);
            int set = 0;
            foreach (Component component in stage.prefabContentsRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script" ||
                        property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                        continue;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(property.objectReferenceInstanceIDValue, out string guid, out long localId) ||
                        string.IsNullOrEmpty(guid) || !redirects.TryGetValue(guid, out string target))
                        continue;
                    Object replacement = ObjectWithId(target, localId);
                    if (replacement == null)
                    {
                        unresolved.Add(HierarchyPath.Of(component.transform) + " > " + ObjectNames.NicifyVariableName(component.GetType().Name) + " > " + property.displayName);
                        continue;
                    }
                    property.objectReferenceValue = replacement;
                    changed = true;
                    set++;
                }
                if (!changed) continue;
                Undo.RecordObject(component, GuidToolReferenceRepair.Title);
                serialized.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            if (set > 0) EditorSceneManager.MarkSceneDirty(stage.scene);
            return set;
        }

        // The object in the asset with this GUID whose file ID matches, or null.
        private static Object ObjectWithId(string guid, long localId)
        {
            foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                if (item != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string found, out long id) && id == localId) return item;
            }
            return null;
        }
    }
}
