#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Riry.UnityWorkflow.Editor.Tests")]

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>Copies renderer weights; never changes meshes or animation clips.</summary>
    internal sealed class BlendShapeSyncWindow : EditorWindow
    {
        private const double Interval = 0.5;
        private const string MenuPath = "Tools/Rirytools/Blendshape Sync";

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
        [SerializeField] private bool caseSensitive;
        // Window serialization keeps the baseline over assembly reloads.
        [SerializeField] private List<OriginalState> originals = new List<OriginalState>();

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
        [NonSerialized] private string result = "Assign a master and at least one child.";
        [NonSerialized] private string pauseReason;
        [NonSerialized] private double nextTick;
        private Vector2 scroll;

        [MenuItem(MenuPath, false, 2020)]
        private static void Open()
        {
            GetWindow<BlendShapeSyncWindow>("Blendshape Sync");
        }

        private void OnEnable()
        {
            minSize = new Vector2(440, 320);
            plans = new List<ChildPlan>();
            changes = new List<WeightChange>();
            if (children == null) children = new List<ChildEntry>();
            if (originals == null) originals = new List<OriginalState>();
            EnsureChildRow();
            planDirty = true;
            nextTick = EditorApplication.timeSinceStartup + Interval;
            // Remove before adding, so re-enabling cannot duplicate subscriptions.
            Unsubscribe();
            EditorApplication.update += EditorTick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += InvalidatePlan;
            Undo.undoRedoPerformed += OnUndoRedo;
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
            Undo.undoRedoPerformed -= OnUndoRedo;
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

        private void OnUndoRedo()
        {
            // Leave Undo effective: the next automatic tick must not undo the Undo.
            runAutomatically = false;
            InvalidatePlan();
            result = "Automatic sync stopped after Undo/Redo.";
        }

        private void InvalidatePlan()
        {
            planDirty = true;
            Repaint();
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

        private bool CanAssign(SkinnedMeshRenderer renderer, int childIndex)
        {
            if (renderer == null) return true;
            string error = RendererError(renderer);
            if (error == null)
            {
                if (childIndex >= 0 && renderer == master)
                    error = "The master cannot also be a child.";
                for (int i = 0; i < children.Count && error == null; i++)
                {
                    if (i != childIndex && children[i].Renderer == renderer)
                        error = "This renderer is already assigned as a child.";
                }
            }
            if (error == null) return true;
            ShowNotification(new GUIContent(error));
            result = error;
            return false;
        }

        private static string RendererError(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return "Assign a renderer.";
            if (EditorUtility.IsPersistent(renderer) || !renderer.gameObject.scene.IsValid())
                return "Use scene objects or objects open in Prefab Mode.";
            if ((renderer.hideFlags & HideFlags.NotEditable) != 0 ||
                (renderer.gameObject.hideFlags & HideFlags.NotEditable) != 0)
                return "The renderer is not editable.";
            if (renderer.sharedMesh == null) return "The renderer has no shared mesh.";
            if (renderer.sharedMesh.blendShapeCount == 0) return "The mesh has no blendshapes.";
            return null;
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

        private bool PlanIsCurrent()
        {
            if (planDirty || master == null || master.sharedMesh != plannedMasterMesh ||
                plannedMasterMesh == null || !NamesAreCurrent(plannedMasterMesh, plannedMasterNames))
                return false;
            int active = 0;
            foreach (ChildEntry child in children)
            {
                if (child.Renderer == null) continue;
                if (active >= plans.Count) return false;
                ChildPlan plan = plans[active++];
                if (child.Renderer != plan.Renderer || plan.Renderer.sharedMesh != plan.Mesh ||
                    plan.Mesh == null || !NamesAreCurrent(plan.Mesh, plan.Names))
                    return false;
            }
            return active == plans.Count;
        }

        private bool BuildPlan()
        {
            plans.Clear();
            planError = null;
            ambiguous = false;
            planDirty = false;
            plannedMasterMesh = master != null ? master.sharedMesh : null;
            plannedMasterCount = plannedMasterMesh != null ? plannedMasterMesh.blendShapeCount : 0;
            plannedMasterNames = plannedMasterMesh != null ? GetNames(plannedMasterMesh) : null;
            string error = RendererError(master);
            if (error != null)
            {
                planError = "Master: " + error;
                return false;
            }
            string[] sources = plannedMasterNames;
            HashSet<SkinnedMeshRenderer> assigned = new HashSet<SkinnedMeshRenderer>();
            // Keep all participants in the cache, including when a link is invalid.
            // A blocked pass then waits for an edit instead of rebuilding every tick.
            foreach (ChildEntry child in children)
            {
                if (child.Renderer == null) continue;
                Mesh mesh = child.Renderer.sharedMesh;
                plans.Add(new ChildPlan
                {
                    Entry = child, Renderer = child.Renderer, Mesh = mesh,
                    ShapeCount = mesh != null ? mesh.blendShapeCount : 0,
                    Names = mesh != null ? GetNames(mesh) : null
                });
            }
            foreach (ChildPlan plan in plans)
            {
                error = RendererError(plan.Renderer);
                if (error == null && (plan.Renderer == master || !assigned.Add(plan.Renderer)))
                    error = "A renderer may only be assigned once, and cannot sync to itself.";
                if (error != null)
                {
                    planError = "Child " + plan.Renderer.name + ": " + error;
                    return false;
                }
                bool collision;
                if (!TryBuildLinks(sources, plan.Names, plan.Entry.Links, caseSensitive,
                    plan.Links, out error, out collision))
                {
                    ambiguous = collision;
                    planError = "Child " + plan.Renderer.name + ": " + error;
                    QueueAmbiguityDialog();
                    return false;
                }
            }
            if (plans.Count == 0)
            {
                planError = "Assign at least one child.";
                return false;
            }
            lastAmbiguity = null;
            return true;
        }

        internal static bool TryBuildLinks(string[] sources, string[] destinations,
            List<ManualLink> manual, bool exact, List<IndexLink> links,
            out string error, out bool collision)
        {
            links.Clear();
            error = null;
            collision = false;
            HashSet<int> overriddenSources = new HashSet<int>();
            HashSet<int> claimedDestinations = new HashSet<int>();
            if (manual != null)
            {
                foreach (ManualLink link in manual)
                {
                    if (link == null || IsBlank(link)) continue;
                    if (string.IsNullOrEmpty(link.Source) || string.IsNullOrEmpty(link.Destination))
                    {
                        error = "Complete both Source and Destination for each manual link.";
                        return false;
                    }
                    int source = FindUnique(sources, link.Source, exact);
                    int destination = FindUnique(destinations, link.Destination, exact);
                    if (source == -2 || destination == -2)
                    {
                        collision = !exact;
                        error = "Multiple names match manual link '" + link.Source + "' -> '" +
                            link.Destination + "'. Enable Debug and use exact names.";
                        return false;
                    }
                    if (source < 0 || destination < 0)
                    {
                        error = "Manual link not found: '" + link.Source + "' -> '" +
                            link.Destination + "'. Check the names on both meshes.";
                        return false;
                    }
                    if (!claimedDestinations.Add(destination))
                    {
                        error = "More than one manual link writes destination '" +
                            destinations[destination] + "'. Remove the conflicting link.";
                        return false;
                    }
                    overriddenSources.Add(source);
                    links.Add(new IndexLink(source, destination));
                }
            }
            for (int i = 0; i < sources.Length; i++)
            {
                if (overriddenSources.Contains(i)) continue;
                int destination = FindUnique(destinations, sources[i], exact);
                if (destination == -1) continue;
                if (destination >= 0 && claimedDestinations.Contains(destination)) continue;
                if (destination == -2 || FindUnique(sources, sources[i], exact) == -2)
                {
                    collision = !exact;
                    error = "Multiple names match source '" + sources[i] +
                        "'. Enable Debug and use exact names.";
                    return false;
                }
                claimedDestinations.Add(destination);
                links.Add(new IndexLink(i, destination));
            }
            return true;
        }

        private static int FindUnique(string[] names, string name, bool exact)
        {
            // Escape metacharacters and anchor the entire name: typing a name is
            // never an arbitrary regex or a substring/prefix search.
            RegexOptions options = RegexOptions.CultureInvariant;
            if (!exact) options |= RegexOptions.IgnoreCase;
            Regex pattern = exact ? null : new Regex("\\A" + Regex.Escape(name) + "\\z", options,
                TimeSpan.FromMilliseconds(100));
            int match = -1;
            for (int i = 0; i < names.Length; i++)
            {
                bool matches = exact ? string.Equals(names[i], name, StringComparison.Ordinal)
                    : pattern.IsMatch(names[i]);
                if (!matches) continue;
                if (match >= 0) return -2;
                match = i;
            }
            return match;
        }

        private static bool IsBlank(ManualLink link)
        {
            return string.IsNullOrEmpty(link.Source) && string.IsNullOrEmpty(link.Destination);
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
                "Multiple matches found without case sensitivity. Would you like to disable?\n\n" +
                planError, "Disable filtering", "Keep filtering"))
            {
                caseSensitive = true;
                InvalidatePlan();
            }
        }

        private void CaptureOriginal(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMesh == null || GetPauseReason() != null) return;
            Mesh mesh = renderer.sharedMesh;
            foreach (OriginalState state in originals)
            {
                if (state.Renderer == renderer && state.Mesh == mesh) return;
            }
            float[] weights = new float[mesh.blendShapeCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = renderer.GetBlendShapeWeight(i);
            originals.Add(new OriginalState
            {
                Renderer = renderer, Mesh = mesh, Names = GetNames(mesh), Weights = weights
            });
        }

        private static string[] GetNames(Mesh mesh)
        {
            string[] names = new string[mesh.blendShapeCount];
            for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
            return names;
        }

        private static bool NamesAreCurrent(Mesh mesh, string[] names)
        {
            if (names == null || mesh.blendShapeCount != names.Length) return false;
            for (int i = 0; i < names.Length; i++)
                if (mesh.GetBlendShapeName(i) != names[i]) return false;
            return true;
        }

        private void SyncNow()
        {
            if (busy || GetPauseReason() != null) return;
            busy = true;
            try
            {
                if ((!PlanIsCurrent() && !BuildPlan()) || planError != null)
                {
                    result = "Sync paused until the configuration is corrected.";
                    return;
                }
                CaptureOriginal(master);
                float[] sourceWeights = new float[plannedMasterCount];
                for (int i = 0; i < sourceWeights.Length; i++)
                    sourceWeights[i] = master.GetBlendShapeWeight(i);
                changes.Clear();
                int linkCount = 0;
                foreach (ChildPlan plan in plans)
                {
                    CaptureOriginal(plan.Renderer);
                    foreach (IndexLink link in plan.Links)
                    {
                        linkCount++;
                        AddChange(plan.Renderer, link.Destination, sourceWeights[link.Source]);
                    }
                }
                int count = changes.Count;
                ApplyChanges("Sync Blendshape Weights");
                result = "Synced " + linkCount + " links across " + plans.Count +
                    " children; " + count + " weights changed.";
            }
            catch (Exception exception)
            {
                runAutomatically = false;
                planDirty = true;
                result = "Sync stopped: " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                changes.Clear();
                busy = false;
                Repaint();
            }
        }

        private void AddChange(SkinnedMeshRenderer renderer, int index, float value)
        {
            float before = renderer.GetBlendShapeWeight(index);
            // Preserve weights outside 0..100; Unity blendshapes can use other ranges.
            if (before.Equals(value)) return;
            changes.Add(new WeightChange
            {
                Renderer = renderer, Index = index, Before = before, After = value
            });
        }

        private void ApplyChanges(string undoName)
        {
            if (changes.Count == 0) return;
            HashSet<SkinnedMeshRenderer> renderers = new HashSet<SkinnedMeshRenderer>();
            foreach (WeightChange change in changes) renderers.Add(change.Renderer);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            foreach (SkinnedMeshRenderer renderer in renderers) Undo.RecordObject(renderer, undoName);
            int applied = 0;
            try
            {
                foreach (WeightChange change in changes)
                {
                    change.Renderer.SetBlendShapeWeight(change.Index, change.After);
                    applied++;
                }
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorUtility.SetDirty(renderer);
                    EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                // A failed pass must not leave only some children synchronized.
                for (int i = applied - 1; i >= 0; i--)
                {
                    WeightChange change = changes[i];
                    if (change.Renderer != null)
                        change.Renderer.SetBlendShapeWeight(change.Index, change.Before);
                }
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private void RestoreOriginals()
        {
            if (busy || GetPauseReason() != null) return;
            runAutomatically = false;
            busy = true;
            try
            {
                changes.Clear();
                int restored = 0;
                int skipped = 0;
                foreach (OriginalState state in originals)
                {
                    if (!CanRestore(state))
                    {
                        skipped++;
                        continue;
                    }
                    for (int i = 0; i < state.Weights.Length; i++)
                        AddChange(state.Renderer, i, state.Weights[i]);
                    restored++;
                }
                ApplyChanges("Restore Original Blendshape Weights");
                result = "Restored " + restored + " renderers. Automatic sync stopped.";
                if (skipped > 0)
                    result += " Skipped " + skipped + " removed or changed renderer/mesh snapshots.";
            }
            catch (Exception exception)
            {
                result = "Restore stopped: " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                changes.Clear();
                busy = false;
                Repaint();
            }
        }

        private static bool CanRestore(OriginalState state)
        {
            if (state.Renderer == null || state.Mesh == null ||
                state.Renderer.sharedMesh != state.Mesh || RendererError(state.Renderer) != null ||
                state.Weights == null || state.Names == null ||
                state.Mesh.blendShapeCount != state.Weights.Length ||
                state.Names.Length != state.Weights.Length) return false;
            for (int i = 0; i < state.Names.Length; i++)
            {
                if (state.Mesh.GetBlendShapeName(i) != state.Names[i]) return false;
            }
            return true;
        }
    }
}
#endif
