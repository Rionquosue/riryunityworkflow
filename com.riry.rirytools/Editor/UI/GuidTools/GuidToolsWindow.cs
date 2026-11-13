using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>The GUID Tools window. Its menu entries are in RiryToolsMenuBar.cs.</summary>
    internal sealed class GuidToolsWindow : EditorWindow
    {
        internal static void Open()
        {
            GuidToolsWindow window = GetWindow<GuidToolsWindow>();
            window.titleContent = new GUIContent("GUID Tools");
            // Fits every row, down to the Cache and Logs paths, with a little to spare.
            window.minSize = new Vector2(360f, 280f);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("GUID Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Regenerates GUIDs for selected assets while preserving text-serialized references. " +
                "Folders are expanded recursively without changing folder GUIDs.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!GuidToolRegenerate.HasValidSelection()))
            {
                if (GUILayout.Button("Regenerate GUID", GUILayout.Height(30f)))
                {
                    GuidToolRegenerate.RegenerateSelectedGuids();
                    RiryToolGUI.ExitAfterModal();
                }
                if (GUILayout.Button("Repair Missing GUID References", GUILayout.Height(24f)))
                {
                    GuidToolReferenceRepair.RepairSelected();
                    RiryToolGUI.ExitAfterModal();
                }
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Check for Broken References", GUILayout.Height(24f)))
            {
                GuidToolBrokenReferenceWindow.OpenForOpenScenes("Checked from the GUID Tools window.");
                RiryToolGUI.ExitAfterModal();
            }
            if (GUILayout.Button("Delete GUID Reference Cache", GUILayout.Height(24f)))
            {
                GuidToolClearReferenceCache.Run();
                RiryToolGUI.ExitAfterModal();
            }

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Cache: " + GuidToolReferenceCache.CacheDirectoryPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Logs: " + GuidToolRegenerate.LogDirectoryPath, EditorStyles.wordWrappedMiniLabel);
        }

        private void OnSelectionChange()
        {
            Repaint();
        }
    }
}

// ============================================================================
// GLOSSARY: GUID Tools window
// Menu entries (Tools > RiryTools > GUID Tools, and the Project right-click
// Regenerate GUID / Repair Missing GUID References, which run without the window)
// are in RiryToolsMenuBar.cs.
// ============================================================================
//
// GuidToolsWindow (this file)
//   Open()            Opens the window (called from RiryToolsMenuBar).
//   OnGUI()           Regenerate GUID, Repair Missing GUID References, Check for Broken References,
//                     Delete GUID Reference Cache, and the cache and log locations.
//   OnSelectionChange()  Repaints so buttons follow the Project selection.
//
// Called from the window
//   GuidToolRegenerate.HasValidSelection()       Any asset or folder under Assets is selected.
//   GuidToolRegenerate.RegenerateSelectedGuids() New GUIDs for the selection, references updated (Assets, ProjectSettings,
//                                            Packages, UserSettings/RiryTools). ConfirmUnusualFiles lists non-reference
//                                            and Packages files before writing; RenameGuidNamedFiles renames
//                                            UserSettings/RiryTools files named by an old GUID afterwards.
//   GuidToolReferenceRepair.RepairSelected()          Broken References window for the selection.
//   GuidToolClearReferenceCache.Run()       Asks, then deletes Library/RiryTools/GuidReferenceCache (Editor/UI/GuidTools/GuidToolClearReferenceCache.cs).
//
// Locations shown
//   Library/RiryTools/GuidReferenceCache   GuidToolReferenceCache.CacheDirectoryPath
//   Logs/RiryTools/GuidTools               GuidToolRegenerate.LogDirectoryPath
