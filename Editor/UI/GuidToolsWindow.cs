#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    internal sealed class GuidToolsWindow : EditorWindow
    {
        internal static void OpenWindow()
        {
            GuidToolsWindow window = GetWindow<GuidToolsWindow>();
            window.titleContent = new GUIContent("GUID Tools");
            window.minSize = new Vector2(360f, 220f);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Asset GUID Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Regenerates GUIDs for selected assets while preserving text-serialized references. " +
                "Folders are expanded recursively without changing folder GUIDs.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!RegenerateAssetGuid.HasValidSelection()))
            {
                if (GUILayout.Button("Regenerate Selected GUIDs", GUILayout.Height(30f)))
                    RegenerateAssetGuid.RegenerateSelectedGuids();
            }

            using (new EditorGUI.DisabledScope(!RegenerateAssetGuid.HasValidSelection()))
            {
                if (GUILayout.Button("Repair Missing GUID References", GUILayout.Height(24f)))
                    GuidReferenceRepair.RepairSelected();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Delete Reference Cache", GUILayout.Height(24f)))
                ClearRegenerateGuidCache.DeleteCacheWithConfirmation();

            GUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Cache: Library/RegenerateGuidCache",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "Logs: Logs/RiryGuidTools",
                EditorStyles.miniLabel);
        }

        private void OnSelectionChange()
        {
            Repaint();
        }
    }
}

// ============================================================================
// GLOSSARY: GUID Tools window
// Menu entries (Tools > GUID Tools, Delete GUID Reference Cache, and the Project
// right-click Regenerate GUID / Repair Missing GUID References) are in RiryMenus.cs.
// ============================================================================
//
// GuidToolsWindow (this file)
//   OpenWindow()      Opens the window (called from RiryMenus).
//   OnGUI()           Buttons and the cache and log locations.
//   OnSelectionChange()  Repaints so buttons follow the Project selection.
//
// Called from the window
//   RegenerateAssetGuid.HasValidSelection()       Any asset or folder under Assets is selected.
//   RegenerateAssetGuid.RegenerateSelectedGuids() New GUIDs for the selection, references updated.
//   GuidReferenceRepair.RepairSelected()          Broken References window for the selection.
//   ClearRegenerateGuidCache.DeleteCacheWithConfirmation()  Deletes Library/RegenerateGuidCache.
//
// Locations shown
//   Library/RegenerateGuidCache   RegenerateAssetGuid.CacheDirectoryPath
//   Logs/RiryGuidTools            RegenerateAssetGuid.LogDirectoryPath
#endif
