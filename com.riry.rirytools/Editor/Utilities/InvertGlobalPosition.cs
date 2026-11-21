using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Invert Global Position (Hierarchy right-click): moves objects to the other side of their avatar, mirrored across the
    /// avatar root's left/right axis through its position, with their rotation mirrored the same way, so a pen in the right
    /// hand ends up in the same place relative to the left hand. Also finds the mirrored humanoid bone to move them under
    /// (the prompts are in UI/Utilities/InvertGlobalPositionPrompts.cs).
    /// </summary>
    internal static class InvertGlobalPosition
    {
        internal const string UndoName = "Invert Global Position";
        private const string DescriptorType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        /// <summary>The avatar root: the nearest parent (or the object) with a VRC Avatar Descriptor, else the nearest with an
        /// Animator; null when there is neither.</summary>
        internal static Transform AvatarRoot(Transform target)
        {
            for (Transform each = target; each != null; each = each.parent)
            {
                foreach (Component component in each.GetComponents<Component>())
                {
                    if (component != null && component.GetType().FullName == DescriptorType) return each;
                }
            }
            Animator animator = target.GetComponentInParent<Animator>(true);
            return animator != null ? animator.transform : null;
        }

        /// <summary>
        /// Mirrors each object that has an avatar root (and is not that root) across the root's left/right axis, as part of
        /// the current undo group. Returns the objects moved; the rest are left as they are.
        /// </summary>
        internal static List<Transform> Mirror(IEnumerable<Transform> objects)
        {
            var moved = new List<Transform>();
            foreach (Transform target in objects)
            {
                Transform root = target != null ? AvatarRoot(target) : null;
                if (root == null || root == target) continue;
                Undo.RecordObject(target, UndoName);
                Vector3 local = root.InverseTransformPoint(target.position);
                local.x = -local.x;
                // The rotation in the root's space, reflected across its left/right plane: x stays, y and z change sign.
                Quaternion rotation = Quaternion.Inverse(root.rotation) * target.rotation;
                rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
                target.SetPositionAndRotation(root.TransformPoint(local), root.rotation * rotation);
                moved.Add(target);
            }
            return moved;
        }

        /// <summary>
        /// The bone on the other side for an object under a left or right humanoid bone (its nearest such parent): Right Hand
        /// for one under Left Hand. Null when the avatar is not humanoid, the object is under no sided bone, or the other side
        /// has no bone.
        /// </summary>
        internal static Transform MirroredBone(Transform target)
        {
            Transform root = AvatarRoot(target);
            Animator animator = root != null ? root.GetComponent<Animator>() : null;
            if (animator == null || !animator.isHuman) return null;
            var bones = new Dictionary<Transform, HumanBodyBones>();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform found = animator.GetBoneTransform(bone);
                if (found != null && !bones.ContainsKey(found)) bones[found] = bone;
            }
            for (Transform each = target.parent; each != null && each != root; each = each.parent)
            {
                if (!bones.TryGetValue(each, out HumanBodyBones bone)) continue;
                string name = bone.ToString();
                string other = name.StartsWith("Left", StringComparison.Ordinal) ? "Right" + name.Substring(4)
                    : name.StartsWith("Right", StringComparison.Ordinal) ? "Left" + name.Substring(5) : null;
                if (other == null) continue;
                return animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), other));
            }
            return null;
        }

        /// <summary>Moves the object under the bone, keeping its global position, rotation and scale. False when it is inside a
        /// prefab instance (unpack it first with AttachTo.Unpack).</summary>
        internal static bool MoveUnder(Transform target, Transform bone)
        {
            if (target.parent == bone) return true;
            if (AttachTo.NeedsUnpack(target)) return false;
            Undo.SetTransformParent(target, bone, UndoName);
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: InvertGlobalPosition
//   UndoName                 "Invert Global Position": the undo step and the dialogs' title.
//   AvatarRoot(target)       Nearest parent (or itself) with a VRC Avatar Descriptor (found by type name), else with an Animator.
//   Mirror(objects)          Position mirrored across the avatar root's local X (through its position); rotation reflected the
//                            same way (in the root's space, x kept, y and z negated). Objects without a root, or the root itself,
//                            are skipped. Records undo; the caller groups it.
//   MirroredBone(target)     The nearest Left/Right humanoid bone above the object, swapped to the other side (humanoid only).
//   MoveUnder(target, bone)  Undo.SetTransformParent keeping the global pose; false inside a prefab instance (AttachTo.NeedsUnpack).
// ============================================================================
