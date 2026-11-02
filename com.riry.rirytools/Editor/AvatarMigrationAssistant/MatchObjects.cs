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
        Manual,
        // Made on the target by the run (feature 4), or part of a copied prefab (feature 11).
        Created
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
        public float Tolerance = AvatarMigrationSettings.DefaultTolerance;

        [NonSerialized] private Dictionary<Transform, AvatarMatch> bySource;
        // Humanoid look-up, made on first use: the source's bones (empty unless both avatars are humanoid).
        [NonSerialized] private Dictionary<Transform, HumanBodyBones> sourceBones;
        [NonSerialized] private Animator targetAnimator;

        internal static AvatarMigrationMap Build(Transform sourceRoot, Transform targetRoot, float tolerance)
        {
            var map = new AvatarMigrationMap { SourceRoot = sourceRoot, TargetRoot = targetRoot, Tolerance = tolerance };
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
                AvatarMatchMethod method;
                match.Target = map.AutoMatch(source, map.TargetOf(source.parent), out method);
                match.Method = method;
            }
            return map;
        }

        /// <summary>
        /// The automatic match of one source object, given its parent's target (null: the
        /// parent is unmatched, so the object is too): name path, then humanoid, then structure.
        /// Manual matching calls this again for the objects below a parent the user matched.
        /// </summary>
        internal Transform AutoMatch(Transform source, Transform parentTarget, out AvatarMatchMethod method)
        {
            method = AvatarMatchMethod.None;
            if (source == null || parentTarget == null || TargetRoot == null) return null;
            Transform found = TargetRoot.Find(AnimationUtility.CalculateTransformPath(source, SourceRoot));
            if (found != null)
            {
                method = AvatarMatchMethod.NamePath;
                return found;
            }
            if (sourceBones == null)
            {
                Animator sourceAnimator = HumanoidAnimator(SourceRoot);
                targetAnimator = HumanoidAnimator(TargetRoot);
                sourceBones = sourceAnimator != null && targetAnimator != null
                    ? HumanBones(sourceAnimator)
                    : new Dictionary<Transform, HumanBodyBones>();
            }
            if (sourceBones.Count > 0)
            {
                found = ByHumanoid(source, SourceRoot, sourceBones, targetAnimator);
                if (found != null)
                {
                    method = AvatarMatchMethod.Humanoid;
                    return found;
                }
            }
            found = ByStructure(source, SourceRoot, parentTarget, TargetRoot, Tolerance);
            if (found != null) method = AvatarMatchMethod.Structure;
            return found;
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

        /// <summary>Adds an object the run made on the target for a source object.</summary>
        internal void AddCreated(Transform source, Transform target)
        {
            AvatarMatch match = MatchOf(source);
            if (match == null)
            {
                match = new AvatarMatch { Source = source, SourcePath = AnimationUtility.CalculateTransformPath(source, SourceRoot) };
                Matches.Add(match);
                Index()[source] = match;
            }
            match.Target = target;
            match.Method = AvatarMatchMethod.Created;
        }

        /// <summary>The source object matched to a target object, or null.</summary>
        internal Transform SourceOf(Transform target)
        {
            if (target == null) return null;
            foreach (AvatarMatch match in Matches)
            {
                if (match.Target == target) return match.Source;
            }
            return null;
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
// AvatarMatchMethod        None, NamePath, Humanoid, Structure, Manual, Created: how a source object found its target.
// AvatarMatch              Source, Target (null when unmatched), Method, SourcePath (from the avatar root).
// AvatarMigrationMap (this file)
//   Build(source, target, tolerance)  Every source Transform, parents first: name path, then humanoid slot plus the
//                            names below it (both avatars humanoid), then structure (same avatar-root-space position
//                            among the children of the parent's match, within tolerance; none on a tie). An
//                            unmatched parent leaves its children unmatched.
//   AutoMatch(source, parentTarget, out method)  One object's automatic match given its parent's target; Build uses it,
//                            and Manual matching calls it again below a parent the user matched.
//   MatchOf / TargetOf / SourceOf / Unmatched()  Look-ups.
//   AddCreated(source, target)  An object the run made on the target (feature 4) or a copied prefab's object.
//   FindOnTarget(path) / TargetPath(target)  Paths from the target root.
//   TieEpsilon (1e-7)        Distances this close count as a tie.
