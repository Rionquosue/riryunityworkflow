using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Blendshape Sync passes: copies the master's weights to the children, and restores the captured originals.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow
    {
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

        private void SyncNow()
        {
            if (busy || GetPauseReason() != null) return;
            busy = true;
            try
            {
                if ((!PlanIsCurrent() && !BuildPlan()) || planError != null)
                {
                    result = "Sync paused until the configuration is corrected.";
                    // Jobs run unseen, so the pause is logged, once per reason.
                    if (planError != loggedPause)
                    {
                        loggedPause = planError;
                        RiryLog.Warn(RiryLog.BlendshapeSync, "Job " + JobNumber + " (" + (master != null ? master.name : "no master") +
                            ") paused: " + planError, master);
                    }
                    return;
                }
                loggedPause = null;
                CaptureOriginal(master);
                float[] sourceWeights = new float[plannedMasterCount];
                for (int i = 0; i < sourceWeights.Length; i++)
                    sourceWeights[i] = master.GetBlendShapeWeight(i);
                changes.Clear();
                int linkCount = 0;
                int invalid = 0;
                foreach (ChildPlan plan in plans)
                {
                    CaptureOriginal(plan.Renderer);
                    foreach (IndexLink link in plan.Links)
                    {
                        // NaN or Infinity on the master must not spread to every child.
                        if (float.IsNaN(sourceWeights[link.Source]) || float.IsInfinity(sourceWeights[link.Source]))
                        {
                            invalid++;
                            continue;
                        }
                        linkCount++;
                        AddChange(plan.Renderer, link.Destination, sourceWeights[link.Source]);
                    }
                }
                int count = changes.Count;
                ApplyChanges(SyncUndoName);
                result = "Synced " + linkCount + " links across " + plans.Count +
                    " children; " + count + " weights changed.";
                if (invalid > 0)
                    result += " Skipped " + invalid + " links whose master weight is NaN or Infinity.";
            }
            catch (Exception exception)
            {
                runAutomatically = false;
                planDirty = true;
                result = "Sync stopped: " + exception.Message;
                RiryLog.Exception(RiryLog.BlendshapeSync, "Job " + JobNumber + " sync stopped", exception, master);
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
            // While a control is held (a slider being dragged), the sync joins that Undo step, so
            // the drag and its sync undo together instead of being split every Interval.
            bool joinHeld = GUIUtility.hotControl != 0;
            if (!joinHeld) Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            if (!joinHeld)
            {
                Undo.SetCurrentGroupName(undoName);
                ownGroups.Add(group);
            }
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
                if (!joinHeld) Undo.CollapseUndoOperations(group);
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
                // A joined step also holds the user's own change, so only this pass is put back (above).
                if (!joinHeld) Undo.RevertAllDownToGroup(group);
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
                ApplyChanges(RestoreUndoName);
                result = "Restored " + restored + " renderers. Automatic sync stopped.";
                if (skipped > 0)
                    result += " Skipped " + skipped + " removed or changed renderer/mesh snapshots.";
            }
            catch (Exception exception)
            {
                result = "Restore stopped: " + exception.Message;
                RiryLog.Exception(RiryLog.BlendshapeSync, "Job " + JobNumber + " restore stopped", exception, master);
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

// ============================================================================
// GLOSSARY: Blendshape Sync passes
// BlendshapeSyncWindow is one partial class: UI/BlendshapeSync/BlendshapeSyncWindow(.UI).cs and BlendshapeSync/BlendshapeSyncWindow.*.cs, BlendshapeSyncJob.cs.
// ============================================================================
//
// BlendshapeSyncWindow (this part)
//   CaptureOriginal(renderer)  Stores a renderer's weights once, for restoring.
//   SyncNow()                  Copies the master's weights to every child (one Undo step).
//   AddChange / ApplyChanges(undoName)  Collects weight changes, then applies them; while a control is held (a slider
//                              drag), the sync joins that Undo step instead of its own (joinHeld).
//   RestoreOriginals() / CanRestore(state)  Restores every captured weight that still fits its mesh and stops automatic sync.
