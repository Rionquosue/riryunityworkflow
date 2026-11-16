using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Blendshape Sync plan: matches the master's blendshape names to each child's indices.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow
    {
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
                // Jobs saved before this check, or loaded together, may still clash.
                if (error == null)
                {
                    foreach (BlendshapeSyncWindow other in BlendshapeSyncJobs.All())
                    {
                        if (other != this && other.children.Exists(entry => entry.Renderer == plan.Renderer))
                            error = "It is also a child in job " + other.JobNumber + "; two jobs would overwrite each other.";
                    }
                }
                if (error == null && Reaches(plan.Renderer, master)) error = LoopError(master, plan.Renderer);
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
                            link.Destination + "'. Turn on Case-sensitive and use exact names.";
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
                        "'. Turn on Case-sensitive and use exact names.";
                    return false;
                }
                claimedDestinations.Add(destination);
                links.Add(new IndexLink(i, destination));
            }
            return true;
        }

        private static int FindUnique(string[] names, string name, bool exact)
        {
            // Whole-name literal comparison: never a pattern, substring or prefix search.
            StringComparison comparison = exact
                ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int match = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.Equals(names[i], name, comparison)) continue;
                if (match >= 0) return -2;
                match = i;
            }
            return match;
        }

        private static bool IsBlank(ManualLink link)
        {
            return string.IsNullOrEmpty(link.Source) && string.IsNullOrEmpty(link.Destination);
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
    }
}

// ============================================================================
// GLOSSARY: Blendshape Sync plan
// BlendshapeSyncWindow is one partial class: UI/BlendshapeSync/BlendshapeSyncWindow(.UI).cs and BlendshapeSync/BlendshapeSyncWindow.*.cs, BlendshapeSyncJob.cs.
// ============================================================================
//
// BlendshapeSyncWindow (this part)
//   PlanIsCurrent()            The plan still matches the master and children (meshes, names, counts).
//   BuildPlan() / TryBuildLinks(...)  Match blendshape names to indices (manual links first, then names; FindUnique).
//   IsBlank(link)              A manual link row with neither name set.
//   GetNames(mesh) / NamesAreCurrent(mesh, names)  A mesh's blendshape names, and whether they are unchanged.
