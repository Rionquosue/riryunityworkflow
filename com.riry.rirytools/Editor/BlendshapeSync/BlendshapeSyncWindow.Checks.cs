using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Blendshape Sync checks: which renderers a job may use, within the job and across jobs.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow
    {
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
                if (error == null && childIndex >= 0 && master != null) error = CrossJobError(master, renderer);
                for (int i = 0; i < children.Count && error == null && childIndex < 0; i++)
                {
                    if (children[i].Renderer != null && Reaches(children[i].Renderer, renderer))
                        error = LoopError(renderer, children[i].Renderer);
                }
            }
            if (error == null) return true;
            ShowNotification(new GUIContent(error));
            result = error;
            return false;
        }

        /// <summary>
        /// Why this job may not write from <paramref name="from"/> to <paramref name="to"/> across jobs, or null: another
        /// job already writes to it, or the link would close a loop of jobs syncing each other.
        /// </summary>
        private string CrossJobError(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            foreach (BlendshapeSyncWindow other in BlendshapeSyncJobs.All())
            {
                if (other != this && other.children.Exists(child => child.Renderer == to))
                    return to.name + " is already a child in job " + other.JobNumber + "; two jobs would overwrite each other.";
            }
            return Reaches(to, from) ? LoopError(from, to) : null;
        }

        private static string LoopError(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            return "Syncing " + to.name + " from " + from.name + " would make jobs sync each other in a loop.";
        }

        /// <summary>Whether <paramref name="goal"/> is synced from <paramref name="start"/> through any job (master to children).</summary>
        private static bool Reaches(SkinnedMeshRenderer start, SkinnedMeshRenderer goal)
        {
            List<BlendshapeSyncWindow> jobs = BlendshapeSyncJobs.All();
            var seen = new HashSet<SkinnedMeshRenderer> { start };
            var queue = new Queue<SkinnedMeshRenderer>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                SkinnedMeshRenderer current = queue.Dequeue();
                if (current == goal) return true;
                foreach (BlendshapeSyncWindow job in jobs)
                {
                    if (job.master != current) continue;
                    foreach (ChildEntry child in job.children)
                    {
                        if (child.Renderer != null && seen.Add(child.Renderer)) queue.Enqueue(child.Renderer);
                    }
                }
            }
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
    }
}

// ============================================================================
// GLOSSARY: Blendshape Sync checks
// BlendshapeSyncWindow is one partial class: UI/BlendshapeSync/BlendshapeSyncWindow(.UI).cs and BlendshapeSync/BlendshapeSyncWindow.*.cs, BlendshapeSyncJob.cs.
// ============================================================================
//
// BlendshapeSyncWindow (this part)
//   CanAssign(renderer, index) Refuses invalid or duplicate renderers (shows a notification).
//   RendererError(renderer)    Why a renderer can't be used.
//   CrossJobError / Reaches / LoopError  Across jobs: a renderer may be a child in one job only, and no job may sync a
//                              renderer that (through any jobs) syncs its master. Checked on assigning (CanAssign) and in
//                              BuildPlan, which pauses the job with the reason.
