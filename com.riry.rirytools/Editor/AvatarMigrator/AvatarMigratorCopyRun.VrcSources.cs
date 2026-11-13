using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The serialized sources of a VRC constraint, and each source on the copied constraint paired with the
    /// one it was copied from. Used by the rotation and scale corrections (AvatarMigratorCopyRun.CorrectRotation.cs,
    /// AvatarMigratorCopyRun.CorrectScale.cs), which each correct the pairs in their own way.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        private const string VrcParent = AvatarMigratorCopyTypes.VrcConstraintPrefix + "ParentConstraint";
        // Sources stored as source0 to source15 before overflowList.
        private const int VrcFlatSources = 16;

        /// <summary>A source on the copied constraint (Target), its bone, and the original bone of the source it was copied from.</summary>
        private struct VrcSourcePair
        {
            internal Transform SourceBone;
            internal Transform TargetBone;
            internal SerializedProperty Target;
        }

        /// <summary>The sources of <paramref name="to"/> paired by index with those of <paramref name="from"/>, as far as both go.</summary>
        private List<VrcSourcePair> VrcSourcePairs(SerializedObject from, SerializedObject to)
        {
            var pairs = new List<VrcSourcePair>();
            List<SerializedProperty> fromSources = VrcSources(from);
            List<SerializedProperty> toSources = VrcSources(to);
            for (int i = 0; i < fromSources.Count && i < toSources.Count; i++)
            {
                pairs.Add(new VrcSourcePair
                {
                    SourceBone = OriginalTransform(fromSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform),
                    TargetBone = toSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform,
                    Target = toSources[i]
                });
            }
            return pairs;
        }

        /// <summary>
        /// A VRC constraint's sources: its Sources list stores the first sixteen as source0 to source15 and the
        /// rest in overflowList, with totalLength the count (VRCConstraintSourceKeyableList in VRC.Dynamics.dll).
        /// </summary>
        private static List<SerializedProperty> VrcSources(SerializedObject serialized)
        {
            var sources = new List<SerializedProperty>();
            SerializedProperty list = serialized.FindProperty("Sources");
            if (list == null) return sources;
            SerializedProperty length = list.FindPropertyRelative("totalLength");
            SerializedProperty overflow = list.FindPropertyRelative("overflowList");
            int count = length != null ? length.intValue : 0;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty item = i < VrcFlatSources
                    ? list.FindPropertyRelative("source" + i)
                    : overflow != null && i - VrcFlatSources < overflow.arraySize ? overflow.GetArrayElementAtIndex(i - VrcFlatSources) : null;
                if (item != null) sources.Add(item);
            }
            return sources;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant VRC constraint sources (AvatarMigratorCopyRun.VrcSources.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   VrcParent / VrcFlatSources  The VRC parent constraint's type name; 16 sources stored as source0 to source15.
//   VrcSourcePair          A copied source (Target), its bone (TargetBone) and the original bone it was copied from (SourceBone).
//   VrcSourcePairs(from, to)  The copied constraint's sources paired by index with the original's.
//   VrcSources(serialized) A VRC constraint's sources: source0 to source15, then overflowList, totalLength in all.
