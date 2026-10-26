#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>What the Broken References window draws. State and actions are in Editor/GuidRepair/BrokenReferenceWindow.cs.</summary>
    internal sealed partial class BrokenReferenceWindow
    {
        private static readonly Color Unresolved = new Color(1f, 0.35f, 0.35f);

        private void OnGUI()
        {
            EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Files: " + string.Join(", ", files), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Rescan")) Rescan();
            if (plan == null) return;
            if (plan.Missing.Count == 0)
            {
                EditorGUILayout.HelpBox("No broken references were found.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (KeyValuePair<string, SortedSet<string>> missing in plan.Missing)
            {
                Object choice;
                choices.TryGetValue(missing.Key, out choice);
                Color previous = GUI.backgroundColor;
                if (choice == null) GUI.backgroundColor = Unresolved;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUI.backgroundColor = previous;
                    string lastPath;
                    plan.LastKnownPaths.TryGetValue(missing.Key, out lastPath);
                    EditorGUILayout.LabelField("Missing " + missing.Key + (lastPath != null ? " (was " + lastPath + ")" : ""),
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Used in " + string.Join(", ", missing.Value), EditorStyles.wordWrappedMiniLabel);
                    choices[missing.Key] = EditorGUILayout.ObjectField("Redirect to", choice, typeof(Object), false);
                    EditorGUILayout.LabelField(Describe(missing.Key, choices[missing.Key]), EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();

            int ready = Redirects().Count;
            using (new EditorGUI.DisabledScope(ready == 0))
            {
                if (GUILayout.Button("Apply " + ready + " Redirect(s)", GUILayout.Height(28f))) ApplyRedirects();
            }
        }

        private string Describe(string guid, Object choice)
        {
            if (choice == null) return "Not found. Pick the asset this reference should use.";
            string suggested;
            string chosen;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(choice, out chosen, out localId) &&
                plan.Suggested.TryGetValue(guid, out suggested) && suggested == chosen)
                return "Suggested from " + plan.Sources[guid] + ".";
            return "Chosen manually.";
        }
    }
}

// ============================================================================
// GLOSSARY: Broken References window
// Opened from GIT > Check for Broken References..., Project right-click > Rirytools >
// Repair Missing GUID References (both in RiryMenus.cs), the GUID Tools window, and
// the AutoSync prompt (BrokenReferenceMonitor).
// ============================================================================
//
// UI (this file)
//   Unresolved            Color   Red tint for a missing GUID without a redirect.
//   OnGUI()               Reason, files, Rescan, one box per missing GUID, Apply.
//   Describe(guid, choice) "Suggested from ...", "Chosen manually" or "Not found".
//
// FIELDS AND ACTIONS (Editor/GuidRepair/BrokenReferenceWindow.cs)
//   files                 List<string>  Files scanned.
//   reason                string        Why the window opened.
//   plan                  RepairPlan    Missing GUIDs, their files and suggestions.
//   choices               Dictionary<string, Object>  Redirect chosen per missing GUID.
//   scroll                Vector2       Scroll position.
//   Open(files, reason)   Opens the window and scans.
//   OpenForOpenScenes(reason)  Scans the open scenes and the prefab in Prefab Mode.
//   OpenPaths()           Paths of the open scenes and that prefab.
//   Rescan()              Scans and fills suggestions.
//   Redirects()           Missing GUID to chosen asset GUID.
//   ApplyRedirects()      Checks, confirms, rewrites the GUIDs and reloads the scenes.
//
// RepairPlan (Editor/GuidRepair/GuidReferenceRepair.cs)
//   Files, References (file to offsets), Missing (GUID to files), Suggested, Sources, LastKnownPaths.
// GuidReferenceRepair: Scan(files, log), Suggest(plan, log), Apply(plan, redirects, log),
//   SelectedFiles(), IsMissing(guid), FindGuidReferences(bytes), RepairSelected().
// BrokenReferenceMonitor (BrokenReferenceWindow.cs): offers the window when a file opens with
//   missing GUIDs or Unity logs several missing-script or missing-prefab messages.
#endif
