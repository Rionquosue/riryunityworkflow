#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>Finds bone chains and creates empty target chains for a constraint train.</summary>
    internal static class ConstraintTrainChains
    {
        internal const string RestPrefix = "Rest_";
        internal const string EndpointSuffix = "_End";

        internal static string ChainPrefix(int chain)
        {
            return "Phys" + (chain + 1) + "_";
        }

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

        /// <summary>
        /// Follows a target chain down from its start, one bone per real bone.
        /// With several children, the child named after the matching real bone wins.
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
            for (int i = 1; i < realBones.Count; i++)
            {
                Transform next = PickChild(chain[i - 1], realBones[i].name);
                if (next == null)
                {
                    error = "Could not follow the chain below '" + chain[i - 1].name +
                        "'. It needs one child, or one child whose name ends with '" +
                        realBones[i].name + "'.";
                    return false;
                }
                chain.Add(next);
            }
            return true;
        }

        private static Transform PickChild(Transform parent, string realName)
        {
            if (parent.childCount == 1) return parent.GetChild(0);
            Transform match = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!child.name.EndsWith(realName, System.StringComparison.Ordinal) ||
                    child.name.EndsWith(EndpointSuffix, System.StringComparison.Ordinal)) continue;
                if (match != null) return null;
                match = child;
            }
            return match;
        }

        internal static Transform FindExisting(IList<Transform> realBones, string prefix)
        {
            Transform parent = realBones[0].parent;
            string name = prefix + realBones[0].name;
            if (parent != null) return parent.Find(name);
            foreach (GameObject root in realBones[0].gameObject.scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }
            return null;
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
            Transform previous = parent;
            Transform first = null;
            foreach (Transform real in realBones)
            {
                var created = new GameObject(prefix + real.name);
                created.layer = real.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(created, undoName);
                if (previous == null) SceneManager.MoveGameObjectToScene(created, real.gameObject.scene);
                // Each parent already matches the real parent's pose, so local
                // values reproduce the real world position, rotation and scale.
                created.transform.SetParent(previous, false);
                created.transform.localPosition = real.localPosition;
                created.transform.localRotation = real.localRotation;
                created.transform.localScale = real.localScale;
                if (first == null) first = created.transform;
                previous = created.transform;
            }

            if (addEndpoint && realBones.Count >= 2)
            {
                Transform last = realBones[realBones.Count - 1];
                Transform beforeLast = realBones[realBones.Count - 2];
                var endpoint = new GameObject(prefix + last.name + EndpointSuffix);
                endpoint.layer = last.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(endpoint, undoName);
                endpoint.transform.SetParent(previous, false);
                endpoint.transform.position = previous.position + (last.position - beforeLast.position);
                endpoint.transform.localRotation = Quaternion.identity;
                endpoint.transform.localScale = Vector3.one;
            }
            return first;
        }
    }
}
#endif
