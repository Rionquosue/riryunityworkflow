using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Bone chains for Constraint Tool's Train and Bezier tabs: collecting a chain from its start and end, picking the
    /// end from a selection, and creating empty chains that match it.
    /// </summary>
    internal static class ConstraintToolChains
    {
        // Name ending of a generated chain's extra endpoint.
        internal const string EndpointSuffix = "_End";

        /// <summary>Collects start..end, root first. End must be start or below it.</summary>
        internal static bool TryCollectChain(Transform start, Transform end, List<Transform> chain,
            out string error)
        {
            chain.Clear();
            error = null;
            if (start == null || end == null)
            {
                error = "Assign the start and end of the real bone chain.";
                return false;
            }
            if (end != start && !end.IsChildOf(start))
            {
                error = "The chain end must be the start bone or one of its descendants.";
                return false;
            }
            for (Transform bone = end; bone != start; bone = bone.parent) chain.Add(bone);
            chain.Add(start);
            chain.Reverse();
            return true;
        }

        /// <summary>The deepest of <paramref name="selected"/> below <paramref name="active"/> (the chain end), or null.</summary>
        internal static Transform DeepestSelectedBelow(Transform active, Transform[] selected)
        {
            Transform end = null;
            foreach (Transform candidate in selected)
            {
                if (candidate != active && candidate.IsChildOf(active) && (end == null || candidate.IsChildOf(end)))
                    end = candidate;
            }
            return end;
        }

        /// <summary>
        /// Creates prefix+name empties that match each real bone's world pose. The
        /// first is a sibling of the real start bone and the rest are nested, so
        /// prefab instances only gain added objects and are never unpacked. The
        /// endpoint repeats the offset between the last two real bones.
        /// </summary>
        internal static Transform CreateChain(IList<Transform> realBones, string prefix,
            bool addEndpoint, string undoName)
        {
            Transform parent = realBones[0].parent;
            // A parentless start would add a second root, which a prefab cannot hold.
            if (parent == null)
                throw new System.InvalidOperationException("The real chain start needs a parent.");
            Transform previous = parent;
            Transform first = null;
            foreach (Transform real in realBones)
            {
                Transform created = ConstraintToolObjects.CreateEmpty(prefix + real.name, previous, real.gameObject.layer, undoName);
                // Each parent already matches the real parent's pose, so local
                // values reproduce the real world position, rotation and scale.
                created.localPosition = real.localPosition;
                created.localRotation = real.localRotation;
                created.localScale = real.localScale;
                if (first == null) first = created;
                previous = created;
            }

            if (addEndpoint && realBones.Count >= 2)
            {
                Transform last = realBones[realBones.Count - 1];
                Transform beforeLast = realBones[realBones.Count - 2];
                Transform endpoint = ConstraintToolObjects.CreateEmpty(prefix + last.name + EndpointSuffix, previous, last.gameObject.layer, undoName);
                endpoint.position = previous.position + (last.position - beforeLast.position);
                endpoint.localRotation = Quaternion.identity;
                endpoint.localScale = Vector3.one;
            }
            return first;
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool chains (shared by Train and Bezier)
// ============================================================================
//
// ConstraintToolChains (this file)
//   EndpointSuffix                "_End": a generated chain's extra endpoint (Train's TryFollowChain skips it).
//   TryCollectChain(start, end, chain, out error)  start..end, root first; end must be start or below it.
//   DeepestSelectedBelow(active, selected)         The chain end picked from a Hierarchy selection (UseSelection).
//   CreateChain(realBones, prefix, addEndpoint, undoName)  Empties named prefix + bone name matching each bone's pose,
//                                 the first a sibling of the start (so prefab instances only gain objects); the
//                                 endpoint repeats the last two bones' offset. Each object is its own Undo record.
