using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Train's chain rules: chain names, following a generated chain, and the checks before animating it. Collecting a
    /// chain and creating one are shared with Bezier (Editor/VRChat/Components/ConstraintToolChains.cs).
    /// </summary>
    internal static class ConstraintTrainChains
    {
        internal const string RestPrefix = "Rest_";

        internal static string ChainPrefix(int chain)
        {
            return "Phys" + (chain + 1) + "_";
        }

        /// <summary>
        /// Follows a target chain down from its start, one bone per real bone, then its end bone (where the last real bone
        /// stops): the child ending "_End" (any case), or the only child.
        /// With several children, the child named like the start (same prefix
        /// plus the real bone's name) wins, then a unique name ending match.
        /// </summary>
        internal static bool TryFollowChain(Transform start, IList<Transform> realBones,
            List<Transform> chain, out string error)
        {
            chain.Clear();
            error = null;
            if (start == null)
            {
                error = "Assign or generate the chain start.";
                return false;
            }
            chain.Add(start);
            string prefix = start.name.EndsWith(realBones[0].name, System.StringComparison.Ordinal)
                ? start.name.Substring(0, start.name.Length - realBones[0].name.Length)
                : null;
            for (int i = 1; i < realBones.Count; i++)
            {
                Transform next = PickChild(chain[i - 1], prefix, realBones[i].name);
                if (next == null)
                {
                    error = "Could not follow the chain below '" + chain[i - 1].name +
                        "'. It needs one child, or one child whose name ends with '" +
                        realBones[i].name + "'.";
                    return false;
                }
                chain.Add(next);
            }
            Transform end = EndBone(chain[chain.Count - 1]);
            if (end == null)
            {
                error = "'" + chain[chain.Count - 1].name + "' needs an end bone below it (named ..." + ConstraintToolChains.EndpointSuffix +
                    ", or its only child), where the last bone stops. Generate the chain with Add endpoints on.";
                return false;
            }
            chain.Add(end);
            return true;
        }

        private static Transform EndBone(Transform last)
        {
            Transform named = null;
            for (int i = 0; i < last.childCount; i++)
            {
                Transform child = last.GetChild(i);
                if (!child.name.EndsWith(ConstraintToolChains.EndpointSuffix, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (named != null) return null;
                named = child;
            }
            return named ?? (last.childCount == 1 ? last.GetChild(0) : null);
        }

        private static Transform PickChild(Transform parent, string prefix, string realName)
        {
            if (prefix != null)
            {
                Transform exact = FindChild(parent, prefix + realName);
                if (exact != null) return exact;
            }
            Transform match = null;
            Transform only = null;
            int candidates = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                // A generated endpoint is never a chain bone: a chain that is one
                // bone short must fail instead of using its endpoint.
                if (IsEndpoint(child, realName)) continue;
                candidates++;
                only = child;
                if (!child.name.EndsWith(realName, System.StringComparison.Ordinal)) continue;
                if (match != null) return null;
                match = child;
            }
            return match ?? (candidates == 1 ? only : null);
        }

        private static bool IsEndpoint(Transform child, string realName)
        {
            return child.name.EndsWith(ConstraintToolChains.EndpointSuffix, System.StringComparison.Ordinal) &&
                !child.name.EndsWith(realName, System.StringComparison.Ordinal);
        }

        internal static Transform FindExisting(IList<Transform> realBones, string prefix)
        {
            Transform parent = realBones[0].parent;
            return parent != null ? FindChild(parent, prefix + realBones[0].name) : null;
        }

        /// <summary>
        /// Exact child-name lookup. Transform.Find would read '/' in a name as a
        /// path separator.
        /// </summary>
        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        /// <summary>
        /// Checks that an animation path from root reaches exactly this bone.
        /// Unity splits paths on '/', and binds a name to the first sibling with
        /// that name, so either problem would animate the wrong object.
        /// </summary>
        internal static string AnimationPathError(Transform bone, Transform root)
        {
            for (Transform current = bone; current != null && current != root; current = current.parent)
            {
                if (current.name.IndexOf('/') >= 0)
                    return "'" + current.name + "' contains '/', which animation paths cannot address. " +
                        "Rename it.";
                Transform parent = current.parent;
                if (parent == null) break;
                if (FindChild(parent, current.name) != current)
                    return "'" + current.name + "' shares its name with an earlier sibling under '" +
                        parent.name + "', so the animation would drive the other object. Rename one of them.";
            }
            return null;
        }

        /// <summary>The first transform used by more than one target chain, or null.</summary>
        internal static Transform FindSharedTarget(IList<List<Transform>> chains)
        {
            var seen = new HashSet<Transform>();
            foreach (List<Transform> chain in chains)
            {
                foreach (Transform target in chain)
                {
                    if (!seen.Add(target)) return target;
                }
            }
            return null;
        }

        /// <summary>The outermost Animator above a bone: the avatar's animator, not a prop's.</summary>
        internal static Transform FindAvatarRoot(Transform bone)
        {
            Transform root = null;
            for (Transform current = bone; current != null; current = current.parent)
            {
                if (current.GetComponent<Animator>() != null) root = current;
            }
            return root;
        }
    }
}
