using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>How a source object was matched to its target counterpart.</summary>
    internal enum AvatarMatchMethod
    {
        None,
        NamePath,
        Humanoid,
        Structure,
        Manual
    }

    /// <summary>One source object and its counterpart on the target (null when unmatched).</summary>
    [Serializable]
    internal sealed class AvatarMatch
    {
        public Transform Source;
        public Transform Target;
        public AvatarMatchMethod Method;
        // The source path from the avatar root, "" for the root.
        public string SourcePath = "";
    }

    /// <summary>
    /// Avatar Migration Assistant feature 1: one map from every Transform under the source
    /// root to its counterpart under the target root, built once before anything changes.
    /// Parents come before children. Each object takes the first method that finds it:
    /// its name path from the root; then, when both avatars are humanoid, the same
    /// HumanBodyBones slot as its nearest humanoid bone followed by the names below it;
    /// then, when its parent is matched, the child of the parent's match at the same
    /// place in avatar-root space (within Tolerance; the closest, none on a tie). An object
    /// whose parent is unmatched stays unmatched.
    /// </summary>
    [Serializable]
    internal sealed class AvatarMigrationMap
    {
        // Two candidates closer to each other than this count as equally close.
        private const float TieEpsilon = 1e-7f;

        public Transform SourceRoot;
        public Transform TargetRoot;
        public List<AvatarMatch> Matches = new List<AvatarMatch>();

        [NonSerialized] private Dictionary<Transform, AvatarMatch> bySource;

        internal static AvatarMigrationMap Build(Transform sourceRoot, Transform targetRoot, float tolerance)
        {
            var map = new AvatarMigrationMap { SourceRoot = sourceRoot, TargetRoot = targetRoot };
            Animator sourceAnimator = HumanoidAnimator(sourceRoot);
            Animator targetAnimator = HumanoidAnimator(targetRoot);
            Dictionary<Transform, HumanBodyBones> sourceBones = sourceAnimator != null && targetAnimator != null
                ? HumanBones(sourceAnimator)
                : null;

            // GetComponentsInChildren lists parents before their children.
            foreach (Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                var match = new AvatarMatch { Source = source, SourcePath = AnimationUtility.CalculateTransformPath(source, sourceRoot) };
                map.Matches.Add(match);
                map.Index()[source] = match;
                if (source == sourceRoot)
                {
                    match.Target = targetRoot;
                    match.Method = AvatarMatchMethod.NamePath;
                    continue;
                }
                AvatarMatch parent;
                if (!map.Index().TryGetValue(source.parent, out parent) || parent.Target == null) continue;

                Transform found = targetRoot.Find(match.SourcePath);
                if (found != null)
                {
                    match.Target = found;
                    match.Method = AvatarMatchMethod.NamePath;
                    continue;
                }
                if (sourceBones != null)
                {
                    found = ByHumanoid(source, sourceRoot, sourceBones, targetAnimator);
                    if (found != null)
                    {
                        match.Target = found;
                        match.Method = AvatarMatchMethod.Humanoid;
                        continue;
                    }
                }
                found = ByStructure(source, sourceRoot, parent.Target, targetRoot, tolerance);
                if (found != null)
                {
                    match.Target = found;
                    match.Method = AvatarMatchMethod.Structure;
                }
            }
            return map;
        }

        private Dictionary<Transform, AvatarMatch> Index()
        {
            if (bySource != null) return bySource;
            bySource = new Dictionary<Transform, AvatarMatch>();
            foreach (AvatarMatch match in Matches)
            {
                if (match.Source != null) bySource[match.Source] = match;
            }
            return bySource;
        }

        /// <summary>The source object's match, or null when it is not under the source root.</summary>
        internal AvatarMatch MatchOf(Transform source)
        {
            AvatarMatch match;
            return source != null && Index().TryGetValue(source, out match) ? match : null;
        }

        /// <summary>The target counterpart of a source object, or null when it is unmatched.</summary>
        internal Transform TargetOf(Transform source)
        {
            AvatarMatch match = MatchOf(source);
            return match != null ? match.Target : null;
        }

        internal List<AvatarMatch> Unmatched()
        {
            return Matches.FindAll(match => match.Target == null);
        }

        // The humanoid Animator on the avatar root or below it, or null.
        private static Animator HumanoidAnimator(Transform root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.isHuman && animator.avatar != null) return animator;
            }
            return null;
        }

        private static Dictionary<Transform, HumanBodyBones> HumanBones(Animator animator)
        {
            var bones = new Dictionary<Transform, HumanBodyBones>();
            for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform transform = animator.GetBoneTransform(bone);
                if (transform != null && !bones.ContainsKey(transform)) bones[transform] = bone;
            }
            return bones;
        }

        // The nearest humanoid bone at or above the object, its slot on the target, then the names below it.
        private static Transform ByHumanoid(Transform source, Transform sourceRoot, Dictionary<Transform, HumanBodyBones> sourceBones,
            Animator targetAnimator)
        {
            var below = new List<string>();
            for (Transform current = source; current != null && current != sourceRoot; current = current.parent)
            {
                HumanBodyBones bone;
                if (sourceBones.TryGetValue(current, out bone))
                {
                    Transform targetBone = targetAnimator.GetBoneTransform(bone);
                    if (targetBone == null) return null;
                    below.Reverse();
                    return below.Count == 0 ? targetBone : targetBone.Find(string.Join("/", below));
                }
                below.Add(current.name);
            }
            return null;
        }

        // The child of the parent's match nearest the object's place in avatar-root space.
        private static Transform ByStructure(Transform source, Transform sourceRoot, Transform targetParent, Transform targetRoot, float tolerance)
        {
            Vector3 wanted = sourceRoot.InverseTransformPoint(source.position);
            Transform best = null;
            float bestDistance = float.MaxValue;
            bool tie = false;
            foreach (Transform child in targetParent)
            {
                float distance = Vector3.Distance(targetRoot.InverseTransformPoint(child.position), wanted);
                if (distance > tolerance) continue;
                if (Mathf.Abs(distance - bestDistance) <= TieEpsilon)
                {
                    tie = true;
                    continue;
                }
                if (distance < bestDistance)
                {
                    best = child;
                    bestDistance = distance;
                    tie = false;
                }
            }
            return tie ? null : best;
        }

        /// <summary>The target object at a path from the target root ("" is the root), or null.</summary>
        internal Transform FindOnTarget(string path)
        {
            if (TargetRoot == null || path == null) return null;
            return path.Length == 0 ? TargetRoot : TargetRoot.Find(path);
        }

        internal string TargetPath(Transform target)
        {
            return target == null || TargetRoot == null ? "" : AnimationUtility.CalculateTransformPath(target, TargetRoot);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant object map (MatchObjects.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMatchMethod        None, NamePath, Humanoid, Structure, Manual: how a source object found its target.
// AvatarMatch              Source, Target (null when unmatched), Method, SourcePath (from the avatar root).
// AvatarMigrationMap (this file)
//   Build(source, target, tolerance)  Every source Transform, parents first: name path, then humanoid slot plus the
//                            names below it (both avatars humanoid), then structure (same avatar-root-space position
//                            among the children of the parent's match, within tolerance; none on a tie). An
//                            unmatched parent leaves its children unmatched.
//   MatchOf / TargetOf / Unmatched()  Look-ups.
//   FindOnTarget(path) / TargetPath(target)  Paths from the target root.
//   TieEpsilon (1e-7)        Distances this close count as a tie.
