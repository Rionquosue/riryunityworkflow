using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the Blendshape Sync window draws. Its menu entry is in RiryToolsMenuBar.cs;
    /// state is in BlendshapeSyncWindow.cs, actions in Editor/BlendshapeSync/. A glossary is at
    /// the bottom.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow
    {
        internal static void Open()
        {
            GetWindow<BlendshapeSyncWindow>("Blendshape Sync");
        }

        private void OnGUI()
        {
            string paused = GetPauseReason();
            if (!string.IsNullOrEmpty(paused))
                EditorGUILayout.HelpBox("Paused: " + paused, MessageType.Info);

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(paused) || busy))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUI.BeginChangeCheck();
                SkinnedMeshRenderer selected = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                    "Master", master, typeof(SkinnedMeshRenderer), true);
                if (selected != master && CanAssign(selected, -1))
                {
                    Undo.RecordObject(this, SettingsUndo);
                    master = selected;
                    CaptureOriginal(master);
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Children", EditorStyles.boldLabel);
                EnsureChildRow();
                int removeChild = -1;
                // Render the current rows; add a new empty row after this layout.
                int rowCount = children.Count;
                for (int i = 0; i < rowCount; i++)
                {
                    ChildEntry child = children[i];
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            selected = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                                "Child " + (i + 1), child.Renderer, typeof(SkinnedMeshRenderer), true);
                            if (selected != child.Renderer && CanAssign(selected, i))
                            {
                                Undo.RecordObject(this, SettingsUndo);
                                child.Renderer = selected;
                                CaptureOriginal(selected);
                            }
                            if (GUILayout.Button("Remove", GUILayout.Width(65))) removeChild = i;
                        }
                        child.Expanded = EditorGUILayout.Foldout(child.Expanded, "Manual link", true);
                        if (child.Expanded) DrawManualLinks(child);
                    }
                }
                if (removeChild >= 0)
                {
                    Undo.RecordObject(this, SettingsUndo);
                    children.RemoveAt(removeChild);
                }
                EnsureChildRow();
                if (EditorGUI.EndChangeCheck() || removeChild >= 0) InvalidatePlan();
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool automatic = EditorGUILayout.ToggleLeft("Run automatically", runAutomatically);
                    if (automatic != runAutomatically)
                    {
                        Undo.RecordObject(this, SettingsUndo);
                        runAutomatically = automatic;
                        nextTick = EditorApplication.timeSinceStartup + Interval;
                    }
                    bool exact = GUILayout.Toggle(caseSensitive,
                        new GUIContent("Case-sensitive", "Match blendshape names exactly, including case."),
                        "Button", GUILayout.Width(100));
                    if (exact != caseSensitive)
                    {
                        Undo.RecordObject(this, SettingsUndo);
                        caseSensitive = exact;
                        lastAmbiguity = null;
                        InvalidatePlan();
                    }
                }
                EditorGUILayout.LabelField(caseSensitive
                    ? "Exact name matching, including case."
                    : "Literal name matching, ignoring case.", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Sync Now")) SyncNow();
                    using (new EditorGUI.DisabledScope(originals.Count == 0))
                    {
                        if (GUILayout.Button("Set All to Original")) RestoreOriginals();
                    }
                }
            }
            if (!string.IsNullOrEmpty(planError))
                EditorGUILayout.HelpBox(planError, MessageType.Warning);
            EditorGUILayout.HelpBox(result ?? "Assign meshes to begin.", MessageType.Info);
            EditorGUILayout.LabelField(
                "Original values are kept while this window remains open.", EditorStyles.miniLabel);
        }

        private void DrawManualLinks(ChildEntry child)
        {
            if (child.Links == null) child.Links = new List<ManualLink>();
            EnsureLinkRow(child);
            int remove = -1;
            for (int i = 0; i < child.Links.Count; i++)
            {
                ManualLink link = child.Links[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent("Source", "Name on the master."),
                            EditorStyles.miniLabel);
                        link.Source = RiryToolGUI.Set(this, SettingsUndo, link.Source, EditorGUILayout.TextField(link.Source));
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent("Destination", "Name on this child."),
                            EditorStyles.miniLabel);
                        link.Destination = RiryToolGUI.Set(this, SettingsUndo, link.Destination, EditorGUILayout.TextField(link.Destination));
                    }
                    if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0)
            {
                Undo.RecordObject(this, SettingsUndo);
                child.Links.RemoveAt(remove);
                InvalidatePlan();
            }
            EnsureLinkRow(child);
        }
    }
}

// ============================================================================
// GLOSSARY: Blendshape Sync window: drawing
// BlendshapeSyncWindow is one partial class: UI/BlendshapeSync/BlendshapeSyncWindow(.UI).cs and BlendshapeSync/BlendshapeSyncWindow.*.cs, BlendshapeSyncJob.cs.
// ============================================================================
//
// BlendshapeSyncWindow (this part)
//   Open()                     Opens the window (called from RiryToolsMenuBar).
//   OnGUI()                    Master field, child rows, toggles, buttons and messages.
//   DrawManualLinks(child)     Source/Destination rows for one child.
