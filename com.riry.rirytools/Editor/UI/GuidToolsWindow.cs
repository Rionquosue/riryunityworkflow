using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>The GUID Tools window. Its menu entries are in RiryMenus.cs.</summary>
    internal sealed class GuidToolsWindow : EditorWindow
    {
        internal static void Open()
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
                if (GUILayout.Button("Regenerate GUID", GUILayout.Height(30f)))
                    RegenerateAssetGuid.RegenerateSelectedGuids();
                if (GUILayout.Button("Repair Missing GUID References", GUILayout.Height(24f)))
                    GuidReferenceRepair.RepairSelected();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Delete GUID Reference Cache", GUILayout.Height(24f)))
                GuidReferenceCache.DeleteWithConfirmation();

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Cache: " + RegenerateAssetGuid.CacheDirectoryPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Logs: " + RegenerateAssetGuid.LogDirectoryPath, EditorStyles.wordWrappedMiniLabel);
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
//   Open()            Opens the window (called from RiryMenus).
//   OnGUI()           Buttons and the cache and log locations.
//   OnSelectionChange()  Repaints so buttons follow the Project selection.
//
// Called from the window
//   RegenerateAssetGuid.HasValidSelection()       Any asset or folder under Assets is selected.
//   RegenerateAssetGuid.RegenerateSelectedGuids() New GUIDs for the selection, references updated.
//   GuidReferenceRepair.RepairSelected()          Broken References window for the selection.
//   GuidReferenceCache.DeleteWithConfirmation()  Deletes Library/RiryTools/GuidReferenceCache.
//
// Locations shown
//   Library/RiryTools/GuidReferenceCache   RegenerateAssetGuid.CacheDirectoryPath
//   Logs/RiryTools/GuidTools               RegenerateAssetGuid.LogDirectoryPath
