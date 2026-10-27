#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the Blendshape Sync window draws. Its menu entry is in RiryMenus.cs;
    /// state and actions are in Editor/BlendShapeSyncWindow.cs. A glossary is at
    /// the bottom.
    /// </summary>
    internal sealed partial class BlendShapeSyncWindow
    {
        internal static void Open()
        {
            GetWindow<BlendShapeSyncWindow>("Blendshape Sync");
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
                                child.Renderer = selected;
                                CaptureOriginal(selected);
                            }
                            if (GUILayout.Button("Remove", GUILayout.Width(65))) removeChild = i;
                        }
                        child.Expanded = EditorGUILayout.Foldout(child.Expanded, "Manual link", true);
                        if (child.Expanded) DrawManualLinks(child);
                    }
                }
                if (removeChild >= 0) children.RemoveAt(removeChild);
                EnsureChildRow();
                if (EditorGUI.EndChangeCheck() || removeChild >= 0) InvalidatePlan();
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool automatic = EditorGUILayout.ToggleLeft("Run automatically", runAutomatically);
                    if (automatic != runAutomatically)
                    {
                        runAutomatically = automatic;
                        nextTick = EditorApplication.timeSinceStartup + Interval;
                    }
                    bool debug = GUILayout.Toggle(caseSensitive,
                        new GUIContent("Debug", "Use exact, case-sensitive blendshape names."),
                        "Button", GUILayout.Width(65));
                    if (debug != caseSensitive)
                    {
                        caseSensitive = debug;
                        lastAmbiguity = null;
                        InvalidatePlan();
                    }
                }
                EditorGUILayout.LabelField(caseSensitive
                    ? "Debug: exact, case-sensitive names."
                    : "Literal name matching, ignoring case.", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Sync now")) SyncNow();
                    using (new EditorGUI.DisabledScope(originals.Count == 0))
                    {
                        if (GUILayout.Button("Set all to original")) RestoreOriginals();
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
                        link.Source = EditorGUILayout.TextField(link.Source);
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent("Destination", "Name on this child."),
                            EditorStyles.miniLabel);
                        link.Destination = EditorGUILayout.TextField(link.Destination);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0)
            {
                child.Links.RemoveAt(remove);
                InvalidatePlan();
            }
            EnsureLinkRow(child);
        }
    }
}

// ============================================================================
// GLOSSARY: BlendShapeSyncWindow (both files are one partial class)
// ============================================================================
//
// FIELDS (Editor/BlendShapeSyncWindow.cs; [S] = serialized, survives script reloads)
//   master              [S] SkinnedMeshRenderer  Renderer whose weights are copied.
//   children            [S] List<ChildEntry>     Child rows; the last one is always empty.
//   runAutomatically    [S] bool                 Sync every Interval seconds.
//   caseSensitive       [S] bool                 "Debug": exact, case-sensitive names.
//   originals           [S] List<OriginalState>  Weights captured before the first change.
//   plans                   List<ChildPlan>      Name-to-index links, rebuilt when dirty.
//   planDirty               bool                 The plan must be rebuilt.
//   planError               string               Why the plan cannot be used, shown as a warning.
//   result                  string               Outcome of the last pass, shown at the bottom.
//   busy                    bool                 A pass is running; the UI is disabled.
//   nextTick                double               Time of the next automatic pass.
//   lastAmbiguity / ambiguous / dialogPending    Ambiguous-name dialog state.
//   scroll                  Vector2              Scroll position of the child list.
//   Interval                const double         Seconds between automatic passes (0.5).
//   SyncUndoName / RestoreUndoName  const string Undo names; also identify own Undo steps.
//
// TYPES
//   ChildEntry      Renderer, Expanded (Manual link foldout), Links.
//   ManualLink      Source (name on the master), Destination (name on the child).
//   OriginalState   Renderer, Mesh, Names, Weights captured for "Set all to original".
//   ChildPlan / IndexLink / WeightChange   Internal planning and change records.
//
// UI METHODS (this file)
//   Open()                     Opens the window (called from RiryMenus).
//   OnGUI()                    Master field, child rows, toggles, buttons and messages.
//   DrawManualLinks(child)     Source/Destination rows for one child.
//
// ACTIONS AND HELPERS (Editor/BlendShapeSyncWindow.cs)
//   CanAssign(renderer, index) Refuses invalid or duplicate renderers (shows a notification).
//   CaptureOriginal(renderer)  Stores a renderer's weights once, for restoring.
//   EnsureChildRow() / EnsureLinkRow(child)  Keep one empty row at the end.
//   InvalidatePlan()           Marks the plan dirty and repaints.
//   GetPauseReason()           Why syncing is paused (Play Mode, animation preview, Animator editing).
//   SyncNow()                  Copies the master's weights to every child (one Undo step).
//   RestoreOriginals()         Restores every captured weight and stops automatic sync.
//   BuildPlan() / TryBuildLinks(...)  Match blendshape names to indices.
//   EditorTick()               Runs automatic passes.
//   OnUndoRedo(undo) / IsOwnUndo(name)  Stops automatic sync when its own pass is undone.
#endif
