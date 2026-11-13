using UnityEditor;
using UnityEngine;
using VRC.Dynamics;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// VRC constraint sources for Constraint Tool's Train, Bezier and Insertable prop tabs: adding sources, the settings
    /// every generated constraint shares, and the animated property of a source's weight. Each caller keeps its own
    /// extra settings (such as FreezeToWorld), Undo grouping and prefab recording.
    /// </summary>
    internal static class ConstraintToolSources
    {
        /// <summary>Animated property for one VRC constraint source weight.</summary>
        internal static string SourceWeightProperty(int source)
        {
            // VRC constraints keep 16 keyable sources, then an overflow list.
            return source < 16
                ? "Sources.source" + source + ".Weight"
                : "Sources.overflowList.Array.data[" + (source - 16) + "].Weight";
        }

        /// <summary>Adds a source per transform at its weight, with no offsets.</summary>
        internal static void AddSources(VRCConstraintBase constraint, Transform[] sources, float[] weights)
        {
            // The source list is a struct: edit a copy and assign it back.
            var list = constraint.Sources;
            for (int s = 0; s < sources.Length; s++)
            {
                list.Add(new VRCConstraintSource
                {
                    SourceTransform = sources[s],
                    Weight = weights[s],
                    ParentPositionOffset = Vector3.zero,
                    ParentRotationOffset = Vector3.zero
                });
            }
            constraint.Sources = list;
        }

        /// <summary>The settings every generated constraint shares: full weight, world space, locked and active.</summary>
        internal static void ApplyDefaults(VRCConstraintBase constraint)
        {
            constraint.GlobalWeight = 1f;
            constraint.SolveInLocalSpace = false;
            constraint.Locked = true;
            constraint.IsActive = true;
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool sources (shared by Train, Bezier and Insertable prop)
// ============================================================================
//
// ConstraintToolSources (this file)
//   SourceWeightProperty(source)  "Sources.source<n>.Weight", or the overflow list's entry past 16: what a clip animates.
//   AddSources(constraint, sources, weights)  One source per transform at its weight, offsets zero.
//   ApplyDefaults(constraint)     GlobalWeight 1, SolveInLocalSpace off, Locked, IsActive.
