using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Copies renderer weights; never changes meshes or animation clips. What the
    /// window draws is in BlendshapeSyncWindow.UI.cs; checks, the plan and the sync passes are in
    /// Editor/BlendshapeSync/BlendshapeSyncWindow.Checks.cs, .Plan.cs and .Sync.cs.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow : EditorWindow
    {
        private const double Interval = 0.5;
        private const string SyncUndoName = "Sync Blendshape Weights";
        private const string RestoreUndoName = "Restore Original Blendshape Weights";
        internal const string SettingsUndo = "Blendshape Sync settings";

        [Serializable]
        internal sealed class ManualLink
        {
            public string Source = "";
            public string Destination = "";
        }

        [Serializable]
        internal sealed class ChildEntry
        {
            public SkinnedMeshRenderer Renderer;
            public bool Expanded;
            public List<ManualLink> Links = new List<ManualLink>();
        }

        [Serializable]
        private sealed class OriginalState
        {
            public SkinnedMeshRenderer Renderer;
            public Mesh Mesh;
            public string[] Names;
            public float[] Weights;
        }

        private sealed class ChildPlan
        {
            public ChildEntry Entry;
            public SkinnedMeshRenderer Renderer;
            public Mesh Mesh;
            public int ShapeCount;
            public string[] Names;
            public readonly List<IndexLink> Links = new List<IndexLink>();
        }

        internal struct IndexLink
        {
            public int Source;
            public int Destination;
            public IndexLink(int source, int destination)
            {
                Source = source;
                Destination = destination;
            }
        }

        private struct WeightChange
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public float Before;
            public float After;
        }

        [SerializeField] private SkinnedMeshRenderer master;
        [SerializeField] private List<ChildEntry> children = new List<ChildEntry>();
        [SerializeField] private bool runAutomatically;
        // The Undo groups this job's own passes made; IsOwnUndo stops only the job whose pass is undone.
        [NonSerialized] private readonly HashSet<int> ownGroups = new HashSet<int>();
        [SerializeField] private bool caseSensitive;
        // Window serialization keeps the baseline over assembly reloads.
        [SerializeField] private List<OriginalState> originals = new List<OriginalState>();
        // The live snapshots: Undo records this window, so after any Undo or Redo they are put back (OnUndoRedo) and an
        // undone setting never wipes snapshots taken after it.
        [NonSerialized] private List<OriginalState> keptOriginals;

        [NonSerialized] private List<ChildPlan> plans;
        [NonSerialized] private List<WeightChange> changes;
        [NonSerialized] private Mesh plannedMasterMesh;
        [NonSerialized] private int plannedMasterCount;
        [NonSerialized] private string[] plannedMasterNames;
        [NonSerialized] private bool planDirty = true;
        [NonSerialized] private bool busy;
        [NonSerialized] private bool dialogPending;
        [NonSerialized] private bool ambiguous;
        [NonSerialized] private string lastAmbiguity;
        [NonSerialized] private string planError;
        // The pause reason last logged, so a lasting one is logged once.
        [NonSerialized] private string loggedPause;
        [NonSerialized] private string result = "Assign a master and at least one child.";
        [NonSerialized] private string pauseReason;
        [NonSerialized] private double nextTick;
        private Vector2 scroll;

        private void OnEnable()
        {
            minSize = new Vector2(440, 320);
            plans = new List<ChildPlan>();
            changes = new List<WeightChange>();
            if (children == null) children = new List<ChildEntry>();
            if (originals == null) originals = new List<OriginalState>();
            keptOriginals = originals;
            EnsureChildRow();
            planDirty = true;
            nextTick = EditorApplication.timeSinceStartup + Interval;
            // Remove before adding, so re-enabling cannot duplicate subscriptions.
            Unsubscribe();
            EditorApplication.update += EditorTick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += InvalidatePlan;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (plans != null) plans.Clear();
            if (changes != null) changes.Clear();
        }

        private void Unsubscribe()
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.projectChanged -= InvalidatePlan;
            Undo.undoRedoEvent -= OnUndoRedo;
            EditorApplication.delayCall -= ShowAmbiguityDialog;
            dialogPending = false;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                runAutomatically = false;
            planDirty = true;
            nextTick = EditorApplication.timeSinceStartup + Interval;
            Repaint();
        }

        private void OnUndoRedo(in UndoRedoInfo undo)
        {
            if (keptOriginals != null) originals = keptOriginals;
            InvalidatePlan();
            // A job edit put back by Undo or Redo is saved like the edit was.
            if (undo.undoName == SettingsUndo) BlendshapeSyncJobs.SaveAll();
            // Undoing another edit (for example a master weight) should simply
            // resync. Undoing this tool's own pass must stay effective, so the
            // next automatic tick may not reapply it.
            if (!IsOwnUndo(undo)) return;
            runAutomatically = false;
            result = "Automatic sync stopped after Undo/Redo.";
        }

        /// <summary>
        /// True for an Undo step this job made: its name is this tool's and its group is one this
        /// job recorded, so undoing another job's pass leaves this job running.
        /// </summary>
        private bool IsOwnUndo(in UndoRedoInfo undo)
        {
            // An unnamed group cannot be identified; stop to be safe.
            if (string.IsNullOrEmpty(undo.undoName)) return true;
            return (undo.undoName == SyncUndoName || undo.undoName == RestoreUndoName) && ownGroups.Contains(undo.undoGroup);
        }

        private void InvalidatePlan()
        {
            planDirty = true;
            Repaint();
        }

        private void EnsureChildRow()
        {
            children.RemoveAll(child => child == null);
            if (children.Count == 0 || children[children.Count - 1].Renderer != null)
                children.Add(new ChildEntry());
        }

        private static void EnsureLinkRow(ChildEntry child)
        {
            child.Links.RemoveAll(link => link == null);
            if (child.Links.Count == 0 || !IsBlank(child.Links[child.Links.Count - 1]))
                child.Links.Add(new ManualLink());
        }

        private void EditorTick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (busy || now < nextTick) return;
            // No catch-up loop after an editor stall, and no worker-thread Unity APIs.
            nextTick = now + Interval;
            string paused = GetPauseReason();
            if (paused != pauseReason)
            {
                pauseReason = paused;
                Repaint();
            }
            if (runAutomatically && paused == null) SyncNow();
        }

        private static bool IsAnimatorWindow(EditorWindow window)
        {
            // Unity's Animator graph window is internal; no reflected members needed.
            return window != null && window.GetType().Name == "AnimatorControllerTool";
        }

        private static string GetPauseReason()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Play Mode or a Play Mode transition.";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "Scripts are compiling or assets are being imported.";
            if (AnimationMode.InAnimationMode()) return "Animation preview or recording is active.";
            AnimationWindow[] windows = Resources.FindObjectsOfTypeAll<AnimationWindow>();
            foreach (AnimationWindow window in windows)
            {
                if (window.previewing || window.playing || window.recording)
                    return "Animation preview, playback, or recording is active.";
            }
            if (IsAnimatorWindow(focusedWindow) || IsAnimatorWindow(mouseOverWindow))
                return "The Animator graph is being edited.";
            UnityEngine.Object selection = Selection.activeObject;
            if (selection is AnimatorState || selection is AnimatorStateMachine ||
                selection is AnimatorTransitionBase)
                return "An Animator graph state or transition is selected for editing.";
            return null;
        }

        private void QueueAmbiguityDialog()
        {
            if (!ambiguous || caseSensitive || dialogPending || lastAmbiguity == planError) return;
            lastAmbiguity = planError;
            dialogPending = true;
            EditorApplication.delayCall += ShowAmbiguityDialog;
        }

        private void ShowAmbiguityDialog()
        {
            dialogPending = false;
            if (this == null || caseSensitive || !ambiguous || GetPauseReason() != null) return;
            if (EditorUtility.DisplayDialog("Blendshape Sync",
                "Several blendshape names match when case is ignored. Match names case-sensitively?\n\n" +
                planError, "Case-sensitive", "Keep Ignoring Case"))
            {
                caseSensitive = true;
                InvalidatePlan();
            }
        }

    }
}

// ============================================================================
// GLOSSARY: Blendshape Sync window: state, lifecycle and automatic passes
// BlendshapeSyncWindow is one partial class: UI/BlendshapeSync/BlendshapeSyncWindow(.UI).cs and BlendshapeSync/BlendshapeSyncWindow.*.cs, BlendshapeSyncJob.cs.
// ============================================================================
//
// FIELDS ([S] = serialized, survives script reloads)
//   master              [S] SkinnedMeshRenderer  Renderer whose weights are copied.
//   children            [S] List<ChildEntry>     Child rows; the last one is always empty.
//   runAutomatically    [S] bool                 Sync every Interval seconds.
//   caseSensitive       [S] bool                 "Case-sensitive": exact names, including case.
//   originals           [S] List<OriginalState>  Weights captured before the first change; keptOriginals (the live list)
//                       is put back after any Undo/Redo, so an undone setting never drops newer snapshots.
//   plans                   List<ChildPlan>      Name-to-index links, rebuilt when dirty.
//   planDirty               bool                 The plan must be rebuilt.
//   planError               string               Why the plan cannot be used, shown as a warning.
//   result                  string               Outcome of the last pass, shown at the bottom.
//   busy                    bool                 A pass is running; the UI is disabled.
//   nextTick                double               Time of the next automatic pass.
//   lastAmbiguity / ambiguous / dialogPending    Ambiguous-name dialog state.
//   loggedPause             string               The pause reason last logged: a job that pauses logs why once (RiryLog), since jobs run unseen.
//   ownGroups               HashSet<int>         Undo groups this job's passes made.
//   scroll                  Vector2              Scroll position of the child list.
//   Interval                const double         Seconds between automatic passes (0.5).
//   SyncUndoName / RestoreUndoName  const string Undo names; also identify own Undo steps.
//   SettingsUndo         "Blendshape Sync settings": Undo name of the job's edits (master, children, Remove, manual
//                        links, Run automatically, Case-sensitive, the Main Menu's Auto); undoing one saves the jobs.
//
// TYPES
//   ChildEntry      Renderer, Expanded (Manual link foldout), Links.
//   ManualLink      Source (name on the master), Destination (name on the child).
//   OriginalState   Renderer, Mesh, Names, Weights captured for "Set All to Original".
//   ChildPlan / IndexLink / WeightChange   Internal planning and change records.
//
// METHODS
//   OnEnable() / OnDisable() / Unsubscribe() / OnPlayModeChanged(state)  Editor events while the job runs.
//   OnUndoRedo(undo) / IsOwnUndo(undo)  Stops automatic sync when this job's own pass is undone (its name and a group in
//                              ownGroups); another job's pass leaves it running.
//   InvalidatePlan()           Marks the plan dirty and repaints.
//   EnsureChildRow() / EnsureLinkRow(child)  Keep one empty row at the end.
//   EditorTick()               Runs automatic passes.
//   GetPauseReason() / IsAnimatorWindow(window)  Why syncing is paused (Play Mode, animation preview, Animator editing).
//   QueueAmbiguityDialog() / ShowAmbiguityDialog()  The ambiguous-name dialog, shown once per new ambiguity.
