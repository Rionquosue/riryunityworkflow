using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>How a source object was matched to its target counterpart.</summary>
    internal enum AvatarMigratorMatchMethod
    {
        None,
        NamePath,
        Humanoid,
        Structure,
        Manual,
        // Made on the target by the run (feature 4), or part of a copied prefab (feature 11).
        Created,
        // A SkinnedMeshRenderer object matched by its mesh: the mesh name, then shared bone or blendshape names.
        Mesh
    }

    /// <summary>One source object and its counterpart on the target (null when unmatched).</summary>
    [Serializable]
    internal sealed class AvatarMigratorMatch
    {
        public Transform Source;
        public Transform Target;
        public AvatarMigratorMatchMethod Method;
        // The source path from the avatar root, "" for the root.
        public string SourcePath = "";
    }

    /// <summary>
    /// Avatar Migration Assistant feature 1: one map from every Transform under the source
    /// root to its counterpart under the target root, built once before anything changes.
    /// Parents come before children. Each object takes the first method that finds it:
    /// its name path from the root; then, when both avatars are humanoid, the same
    /// HumanBodyBones slot as its nearest humanoid bone followed by the names below it;
    /// then, for a SkinnedMeshRenderer object, the renderer among the children of its parent's
    /// match with the same mesh name, else the most bone or blendshape names in common (over
    /// half, no tie); then, when its parent is matched, the child of the parent's match at the
    /// same place in avatar-root space (within Tolerance; the closest, none on a tie). An object
    /// whose parent is unmatched stays unmatched.
    /// </summary>
    [Serializable]
    internal sealed class AvatarMigratorMatchObjects
    {
        // Two candidates closer to each other than this count as equally close.
        private const float TieEpsilon = 1e-7f;

        public Transform SourceRoot;
        public Transform TargetRoot;
        public List<AvatarMigratorMatch> Matches = new List<AvatarMigratorMatch>();
        public float Tolerance = AvatarMigratorSettings.DefaultTolerance;

        [NonSerialized] private Dictionary<Transform, AvatarMigratorMatch> bySource;
        // Humanoid look-up, made on first use: the source's bones (empty unless both avatars are humanoid).
        [NonSerialized] private Dictionary<Transform, HumanBodyBones> sourceBones;
        [NonSerialized] private Animator targetAnimator;

        internal static AvatarMigratorMatchObjects Build(Transform sourceRoot, Transform targetRoot, float tolerance)
        {
            var map = new AvatarMigratorMatchObjects { SourceRoot = sourceRoot, TargetRoot = targetRoot, Tolerance = tolerance };
            // GetComponentsInChildren lists parents before their children.
            foreach (Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                var match = new AvatarMigratorMatch { Source = source, SourcePath = AnimationUtility.CalculateTransformPath(source, sourceRoot) };
                map.Matches.Add(match);
                map.Index()[source] = match;
                if (source == sourceRoot)
                {
                    match.Target = targetRoot;
                    match.Method = AvatarMigratorMatchMethod.NamePath;
                    continue;
                }
                AvatarMigratorMatchMethod method;
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
        /// <param name="takenByOther">Whether a target object already belongs to another source object, for the mesh
        /// match; null uses the map's matches (Manual matching passes its rows' current choices).</param>
        internal Transform AutoMatch(Transform source, Transform parentTarget, out AvatarMigratorMatchMethod method,
            Func<Transform, bool> takenByOther = null)
        {
            method = AvatarMigratorMatchMethod.None;
            if (source == null || parentTarget == null || TargetRoot == null) return null;
            Transform found = TargetRoot.Find(AnimationUtility.CalculateTransformPath(source, SourceRoot));
            if (found != null)
            {
                method = AvatarMigratorMatchMethod.NamePath;
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
                    method = AvatarMigratorMatchMethod.Humanoid;
                    return found;
                }
            }
            var renderer = source.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                found = ByMesh(renderer, parentTarget, takenByOther ?? (child => Matches.Exists(match => match.Target == child && match.Source != source)));
                if (found != null)
                {
                    method = AvatarMigratorMatchMethod.Mesh;
                    return found;
                }
            }
            found = ByStructure(source, SourceRoot, parentTarget, TargetRoot, Tolerance);
            if (found != null) method = AvatarMigratorMatchMethod.Structure;
            return found;
        }

        private Dictionary<Transform, AvatarMigratorMatch> Index()
        {
            if (bySource != null) return bySource;
            bySource = new Dictionary<Transform, AvatarMigratorMatch>();
            foreach (AvatarMigratorMatch match in Matches)
            {
                if (match.Source != null) bySource[match.Source] = match;
            }
            return bySource;
        }

        /// <summary>The source object's match, or null when it is not under the source root.</summary>
        internal AvatarMigratorMatch MatchOf(Transform source)
        {
            AvatarMigratorMatch match;
            return source != null && Index().TryGetValue(source, out match) ? match : null;
        }

        /// <summary>The target counterpart of a source object, or null when it is unmatched.</summary>
        internal Transform TargetOf(Transform source)
        {
            AvatarMigratorMatch match = MatchOf(source);
            return match != null ? match.Target : null;
        }

        /// <summary>Adds an object the run made on the target for a source object.</summary>
        internal void AddCreated(Transform source, Transform target)
        {
            AvatarMigratorMatch match = MatchOf(source);
            if (match == null)
            {
                match = new AvatarMigratorMatch { Source = source, SourcePath = AnimationUtility.CalculateTransformPath(source, SourceRoot) };
                Matches.Add(match);
                Index()[source] = match;
            }
            match.Target = target;
            match.Method = AvatarMigratorMatchMethod.Created;
        }

        /// <summary>The source object matched to a target object, or null.</summary>
        internal Transform SourceOf(Transform target)
        {
            if (target == null) return null;
            foreach (AvatarMigratorMatch match in Matches)
            {
                if (match.Target == target) return match.Source;
            }
            return null;
        }

        internal List<AvatarMigratorMatch> Unmatched()
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

        // Shared names over all names of both (bones or blendshapes), at least this, to match by them.
        private const float MeshOverlap = 0.5f;

        // The old avatar's body model (its Animator's Avatar asset), found on first use; "" when it has none.
        [NonSerialized] private string sourceBody;

        /// <summary>
        /// A renamed body mesh: only for a mesh from the old avatar's own body model (clothing shares the body's bones, so
        /// it must never be matched to the body this way). Among the children of the parent's match that have a
        /// SkinnedMeshRenderer, belong to no other source object (takenByOther) and are not the name-path match of another
        /// source object: the one whose mesh has the same name (only one), else the one whose bone or blendshape names
        /// overlap most, counted both ways (shared over all, over MeshOverlap, no tie).
        /// </summary>
        private Transform ByMesh(SkinnedMeshRenderer source, Transform targetParent, Func<Transform, bool> takenByOther)
        {
            if (sourceBody == null) sourceBody = AvatarMigratorRepack.BodyModel(SourceRoot) ?? "";
            if (source.sharedMesh == null || sourceBody.Length == 0 || AssetDatabase.GetAssetPath(source.sharedMesh) != sourceBody) return null;
            var candidates = new List<SkinnedMeshRenderer>();
            foreach (Transform child in targetParent)
            {
                var renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null || takenByOther(child)) continue;
                Transform namesake = SourceRoot.Find(AnimationUtility.CalculateTransformPath(child, TargetRoot));
                if (namesake != null && namesake != source.transform) continue;
                candidates.Add(renderer);
            }
            if (candidates.Count == 0) return null;
            string meshName = source.sharedMesh != null ? source.sharedMesh.name : null;
            if (meshName != null)
            {
                List<SkinnedMeshRenderer> named = candidates.FindAll(candidate => candidate.sharedMesh != null &&
                    string.Equals(candidate.sharedMesh.name, meshName, StringComparison.OrdinalIgnoreCase));
                if (named.Count == 1) return named[0].transform;
            }
            HashSet<string> bones = BoneNames(source), shapes = ShapeNames(source.sharedMesh);
            Transform best = null;
            float bestScore = MeshOverlap;
            bool tie = false;
            foreach (SkinnedMeshRenderer candidate in candidates)
            {
                float score = Mathf.Max(Overlap(bones, BoneNames(candidate)), Overlap(shapes, ShapeNames(candidate.sharedMesh)));
                if (score < MeshOverlap) continue;
                if (best != null && Mathf.Abs(score - bestScore) <= TieEpsilon) tie = true;
                else if (score > bestScore || best == null)
                {
                    best = candidate.transform;
                    bestScore = score;
                    tie = false;
                }
            }
            return tie ? null : best;
        }

        private static HashSet<string> BoneNames(SkinnedMeshRenderer renderer)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform bone in renderer.bones)
            {
                if (bone != null) names.Add(bone.name);
            }
            return names;
        }

        private static HashSet<string> ShapeNames(Mesh mesh)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (mesh == null) return names;
            for (int i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
            return names;
        }

        // Shared names over all names of both (so a subset, such as clothing's bones, scores low); 0 when either has none.
        private static float Overlap(HashSet<string> source, HashSet<string> target)
        {
            if (source.Count == 0 || target.Count == 0) return 0f;
            int shared = 0;
            foreach (string name in source)
            {
                if (target.Contains(name)) shared++;
            }
            return shared / (float)(source.Count + target.Count - shared);
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
// GLOSSARY: Avatar Migration Assistant object map (AvatarMigratorMatchObjects.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorMatchMethod        None, NamePath, Humanoid, Structure, Manual, Created, Mesh: how a source object found its target.
// AvatarMigratorMatch              Source, Target (null when unmatched), Method, SourcePath (from the avatar root).
// AvatarMigratorMatchObjects (this file)
//   Build(source, target, tolerance)  Every source Transform, parents first: name path, then humanoid slot plus the
//                            names below it (both avatars humanoid), then for a SkinnedMeshRenderer object its mesh
//                            (ByMesh, only for a mesh from the old avatar's body model: same mesh name, else bone or
//                            blendshape names shared over all of both above MeshOverlap (0.5), no tie, among renderers
//                            below the parent's match that no other source holds or name-path matches), then structure (same avatar-root-space position
//                            among the children of the parent's match, within tolerance; none on a tie). An
//                            unmatched parent leaves its children unmatched.
//   AutoMatch(source, parentTarget, out method)  One object's automatic match given its parent's target; Build uses it,
//                            and Manual matching calls it again below a parent the user matched.
//   MatchOf / TargetOf / SourceOf / Unmatched()  Look-ups.
//   AddCreated(source, target)  An object the run made on the target (feature 4) or a copied prefab's object.
//   FindOnTarget(path) / TargetPath(target)  Paths from the target root.
//   TieEpsilon (1e-7)        Distances this close count as a tie.
