using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 7: rotation and direction values of Unity and VRC constraints are stored in a
    /// bone's space, and bone roll can change between builds. For each bone a value is stored
    /// relative to, delta = Inverse(targetRootRot⁻¹ · targetBoneRot) · (sourceRootRot⁻¹ ·
    /// sourceBoneRot); vectors become delta · v and rotations delta · q. A vector stored in a
    /// bone whose lossyScale sign differs on an axis (a mirrored axis) has that component
    /// negated first. Runs after references are re-pointed and before scale is corrected.
    /// </summary>
    internal sealed partial class AvatarCopyRun
    {
        private const string VrcAim = AvatarCopyTypes.VrcConstraintPrefix + "AimConstraint";
        private const string VrcParent = AvatarCopyTypes.VrcConstraintPrefix + "ParentConstraint";
        // VRChat's WorldUpType.ObjectRotationUp.
        private const int VrcObjectRotationUp = 2;

        private void CorrectRotation()
        {
            foreach (var pair in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Component, Component>>(copied))
            {
                Component source = pair.Key;
                Component target = pair.Value;
                if (source == null || target == null) continue;
                Transform sourceObject = OriginalTransform(source.transform);
                Transform sourceParent = OriginalTransform(source.transform.parent);
                Transform targetParent = target.transform.parent;
                if (target is IConstraint) Undo.RecordObject(target, AvatarMigrationAssistant.Title);

                var parent = target as ParentConstraint;
                if (parent != null)
                {
                    var from = (ParentConstraint)source;
                    for (int i = 0; i < parent.sourceCount && i < from.sourceCount; i++)
                    {
                        Transform sourceBone = OriginalTransform(from.GetSource(i).sourceTransform);
                        Transform targetBone = parent.GetSource(i).sourceTransform;
                        Quaternion delta = Delta(sourceBone, targetBone);
                        parent.SetTranslationOffset(i, delta * Mirror(sourceBone, targetBone, parent.GetTranslationOffset(i)));
                        parent.SetRotationOffset(i, (delta * Quaternion.Euler(parent.GetRotationOffset(i))).eulerAngles);
                    }
                    Quaternion rest = Delta(sourceParent, targetParent);
                    parent.translationAtRest = rest * Mirror(sourceParent, targetParent, parent.translationAtRest);
                    parent.rotationAtRest = (rest * Quaternion.Euler(parent.rotationAtRest)).eulerAngles;
                    continue;
                }
                var aim = target as AimConstraint;
                if (aim != null)
                {
                    Quaternion self = Delta(sourceObject, aim.transform);
                    aim.aimVector = self * Mirror(sourceObject, aim.transform, aim.aimVector);
                    aim.upVector = self * Mirror(sourceObject, aim.transform, aim.upVector);
                    if (aim.worldUpType == AimConstraint.WorldUpType.ObjectRotationUp)
                    {
                        Transform sourceUp = OriginalTransform(((AimConstraint)source).worldUpObject);
                        aim.worldUpVector = Delta(sourceUp, aim.worldUpObject) * Mirror(sourceUp, aim.worldUpObject, aim.worldUpVector);
                    }
                    aim.rotationOffset = (self * Quaternion.Euler(aim.rotationOffset)).eulerAngles;
                    aim.rotationAtRest = (Delta(sourceParent, targetParent) * Quaternion.Euler(aim.rotationAtRest)).eulerAngles;
                    continue;
                }
                var rotation = target as RotationConstraint;
                if (rotation != null)
                {
                    rotation.rotationOffset = (Delta(sourceObject, rotation.transform) * Quaternion.Euler(rotation.rotationOffset)).eulerAngles;
                    rotation.rotationAtRest = (Delta(sourceParent, targetParent) * Quaternion.Euler(rotation.rotationAtRest)).eulerAngles;
                    continue;
                }
                var lookAt = target as LookAtConstraint;
                if (lookAt != null)
                {
                    lookAt.rotationOffset = (Delta(sourceObject, lookAt.transform) * Quaternion.Euler(lookAt.rotationOffset)).eulerAngles;
                    lookAt.rotationAtRest = (Delta(sourceParent, targetParent) * Quaternion.Euler(lookAt.rotationAtRest)).eulerAngles;
                    continue;
                }
                var position = target as PositionConstraint;
                if (position != null)
                {
                    position.translationAtRest = Delta(sourceParent, targetParent) * Mirror(sourceParent, targetParent, position.translationAtRest);
                    continue;
                }
                if (target.GetType().FullName != null && target.GetType().FullName.StartsWith(AvatarCopyTypes.VrcConstraintPrefix, StringComparison.Ordinal))
                    CorrectVrcRotation(source, target, sourceObject, sourceParent, targetParent);
            }
        }

        // The equivalent fields of a VRC constraint, by name.
        private void CorrectVrcRotation(Component source, Component target, Transform sourceObject, Transform sourceParent, Transform targetParent)
        {
            Undo.RecordObject(target, AvatarMigrationAssistant.Title);
            string type = target.GetType().FullName;
            Quaternion self = Delta(sourceObject, target.transform);
            Quaternion rest = Delta(sourceParent, targetParent);
            RotateEuler(target, "RotationOffset", self);
            RotateEuler(target, "RotationAtRest", rest);
            RotateVector(target, "PositionAtRest", rest, sourceParent, targetParent);
            if (type == VrcAim)
            {
                RotateVector(target, "AimAxis", self, sourceObject, target.transform);
                RotateVector(target, "UpAxis", self, sourceObject, target.transform);
                object worldUp = GetMember(target, "WorldUp");
                if (worldUp != null && Convert.ToInt32(worldUp) == VrcObjectRotationUp)
                {
                    Transform sourceUp = OriginalTransform(GetMember(source, "WorldUpTransform") as Transform);
                    Transform targetUp = GetMember(target, "WorldUpTransform") as Transform;
                    RotateVector(target, "WorldUpVector", Delta(sourceUp, targetUp), sourceUp, targetUp);
                }
            }
            if (type == VrcParent) ForEachVrcSource(source, target, (sourceBone, targetBone, item) =>
            {
                Quaternion delta = Delta(sourceBone, targetBone);
                object offset = GetMember(item, "ParentPositionOffset");
                if (offset is Vector3) SetMember(item, "ParentPositionOffset", delta * Mirror(sourceBone, targetBone, (Vector3)offset));
                object angles = GetMember(item, "ParentRotationOffset");
                if (angles is Vector3) SetMember(item, "ParentRotationOffset", (delta * Quaternion.Euler((Vector3)angles)).eulerAngles);
            });
            ApplyVrcChanges(target);
        }

        private static void RotateEuler(Component owner, string name, Quaternion delta)
        {
            object value = GetMember(owner, name);
            if (value is Vector3) SetMember(owner, name, (delta * Quaternion.Euler((Vector3)value)).eulerAngles);
        }

        private static void RotateVector(Component owner, string name, Quaternion delta, Transform sourceBone, Transform targetBone)
        {
            object value = GetMember(owner, name);
            if (value is Vector3) SetMember(owner, name, delta * Mirror(sourceBone, targetBone, (Vector3)value));
        }

        /// <summary>
        /// Calls change(sourceBone, targetBone, boxedSource) for each source of a VRC constraint
        /// and writes the changed source back with SetSource.
        /// </summary>
        private void ForEachVrcSource(Component source, Component target, Action<Transform, Transform, object> change)
        {
            object count = GetMember(target, "SourceCount");
            var getSource = target.GetType().GetMethod("GetSource", new[] { typeof(int) });
            var sourceGetSource = source.GetType().GetMethod("GetSource", new[] { typeof(int) });
            if (!(count is int) || getSource == null || sourceGetSource == null) return;
            object sourceCount = GetMember(source, "SourceCount");
            for (int i = 0; i < (int)count && sourceCount is int && i < (int)sourceCount; i++)
            {
                object item = getSource.Invoke(target, new object[] { i });
                object fromItem = sourceGetSource.Invoke(source, new object[] { i });
                Transform sourceBone = OriginalTransform(GetMember(fromItem, "SourceTransform") as Transform);
                Transform targetBone = GetMember(item, "SourceTransform") as Transform;
                change(sourceBone, targetBone, item);
                var setSource = target.GetType().GetMethod("SetSource", new[] { typeof(int), item.GetType() });
                if (setSource != null) setSource.Invoke(target, new[] { (object)i, item });
            }
        }

        private static void ApplyVrcChanges(Component target)
        {
            var apply = target.GetType().GetMethod("ApplyConfigurationChanges", Type.EmptyTypes);
            if (apply != null) apply.Invoke(target, null);
            EditorUtility.SetDirty(target);
        }

        /// <summary>The rotation from a bone's space on the source to its counterpart's on the target, in avatar-root space.</summary>
        private Quaternion Delta(Transform sourceBone, Transform targetBone)
        {
            if (sourceBone == null || targetBone == null) return Quaternion.identity;
            Quaternion sourceRelative = Quaternion.Inverse(sourceRoot.rotation) * sourceBone.rotation;
            Quaternion targetRelative = Quaternion.Inverse(targetRoot.rotation) * targetBone.rotation;
            return Quaternion.Inverse(targetRelative) * sourceRelative;
        }

        // A vector stored in a bone with a mirrored axis has that component negated.
        private static Vector3 Mirror(Transform sourceBone, Transform targetBone, Vector3 value)
        {
            if (sourceBone == null || targetBone == null) return value;
            Vector3 from = sourceBone.lossyScale;
            Vector3 to = targetBone.lossyScale;
            if (Mathf.Sign(from.x) != Mathf.Sign(to.x)) value.x = -value.x;
            if (Mathf.Sign(from.y) != Mathf.Sign(to.y)) value.y = -value.y;
            if (Mathf.Sign(from.z) != Mathf.Sign(to.z)) value.z = -value.z;
            return value;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant rotation (CorrectRotation.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarCopyRun (this part)
//   CorrectRotation()      Every copied constraint. Parent: per-source translation and rotation offsets (the source bone),
//                          translation and rotation at rest (the constrained object's parent). Aim: aim and up vectors and
//                          rotation offset (the constrained object), world up vector with ObjectRotationUp (worldUpObject),
//                          rotation at rest (parent). Rotation and LookAt: rotation offset (object), at rest (parent).
//                          Position: translation at rest (parent). VRC constraints: the same by field name
//                          (CorrectVrcRotation, ForEachVrcSource, ApplyVrcChanges).
//   Delta(sourceBone, targetBone)  Inverse(targetRootRot⁻¹ · targetBoneRot) · (sourceRootRot⁻¹ · sourceBoneRot).
//   Mirror(...)            Negates the axes whose lossyScale sign differs between the two bones.
