using System;
using System.Collections.Generic;
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
    internal sealed partial class AvatarMigratorCopyRun
    {
        private const string VrcAim = AvatarMigratorCopyTypes.VrcConstraintPrefix + "AimConstraint";
        private const string VrcParent = AvatarMigratorCopyTypes.VrcConstraintPrefix + "ParentConstraint";
        // VRChat's WorldUpType.ObjectRotationUp (VRC.Dynamics.dll: SceneUp 0, ObjectUp 1, ObjectRotationUp 2, Vector 3, None 4).
        private const int VrcObjectRotationUp = 2;
        // Sources stored as source0 to source15 before overflowList.
        private const int VrcFlatSources = 16;

        private void CorrectRotation()
        {
            foreach (var pair in new List<KeyValuePair<Component, Component>>(copied))
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
                if (target.GetType().FullName != null && target.GetType().FullName.StartsWith(AvatarMigratorCopyTypes.VrcConstraintPrefix, StringComparison.Ordinal))
                    CorrectVrcRotation(source, target, sourceObject, sourceParent, targetParent);
            }
        }

        // The equivalent serialized fields of a VRC constraint (names read from the SDK's VRC.Dynamics.dll):
        // RotationOffset and RotationAtRest on Rotation, Aim and LookAt; PositionAtRest on Parent and Position;
        // AimAxis, UpAxis, WorldUp and WorldUpVector on Aim; per-source ParentPositionOffset and ParentRotationOffset on Parent.
        private void CorrectVrcRotation(Component source, Component target, Transform sourceObject, Transform sourceParent, Transform targetParent)
        {
            var from = new SerializedObject(source);
            var to = new SerializedObject(target);
            Quaternion self = Delta(sourceObject, target.transform);
            Quaternion rest = Delta(sourceParent, targetParent);
            RotateEuler(to, "RotationOffset", self);
            RotateEuler(to, "RotationAtRest", rest);
            RotateVector(to, "PositionAtRest", rest, sourceParent, targetParent);
            string type = target.GetType().FullName;
            if (type == VrcAim)
            {
                RotateVector(to, "AimAxis", self, sourceObject, target.transform);
                RotateVector(to, "UpAxis", self, sourceObject, target.transform);
                SerializedProperty worldUp = to.FindProperty("WorldUp");
                if (worldUp != null && worldUp.intValue == VrcObjectRotationUp)
                {
                    Transform sourceUp = OriginalTransform(Reference(from, "WorldUpTransform"));
                    Transform targetUp = Reference(to, "WorldUpTransform");
                    RotateVector(to, "WorldUpVector", Delta(sourceUp, targetUp), sourceUp, targetUp);
                }
            }
            if (type == VrcParent)
            {
                List<SerializedProperty> fromSources = VrcSources(from);
                List<SerializedProperty> toSources = VrcSources(to);
                for (int i = 0; i < fromSources.Count && i < toSources.Count; i++)
                {
                    Transform sourceBone = OriginalTransform(fromSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform);
                    Transform targetBone = toSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform;
                    Quaternion delta = Delta(sourceBone, targetBone);
                    SerializedProperty offset = toSources[i].FindPropertyRelative("ParentPositionOffset");
                    if (offset != null) offset.vector3Value = delta * Mirror(sourceBone, targetBone, offset.vector3Value);
                    SerializedProperty angles = toSources[i].FindPropertyRelative("ParentRotationOffset");
                    if (angles != null) angles.vector3Value = (delta * Quaternion.Euler(angles.vector3Value)).eulerAngles;
                }
            }
            to.ApplyModifiedProperties();
        }

        private static Transform Reference(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property != null ? property.objectReferenceValue as Transform : null;
        }

        private static void RotateEuler(SerializedObject serialized, string name, Quaternion delta)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
                property.vector3Value = (delta * Quaternion.Euler(property.vector3Value)).eulerAngles;
        }

        private static void RotateVector(SerializedObject serialized, string name, Quaternion delta, Transform sourceBone, Transform targetBone)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
                property.vector3Value = delta * Mirror(sourceBone, targetBone, property.vector3Value);
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
// GLOSSARY: Avatar Migration Assistant rotation (AvatarMigratorCorrectRotation.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   CorrectRotation()      Every copied constraint. Parent: per-source translation and rotation offsets (the source bone),
//                          translation and rotation at rest (the constrained object's parent). Aim: aim and up vectors and
//                          rotation offset (the constrained object), world up vector with ObjectRotationUp (worldUpObject),
//                          rotation at rest (parent). Rotation and LookAt: rotation offset (object), at rest (parent).
//                          Position: translation at rest (parent). VRC constraints: the same serialized fields
//                          (CorrectVrcRotation; VrcSources reads source0 to source15 and overflowList).
//   Delta(sourceBone, targetBone)  Inverse(targetRootRot⁻¹ · targetBoneRot) · (sourceRootRot⁻¹ · sourceBoneRot).
//   Mirror(...)            Negates the axes whose lossyScale sign differs between the two bones.
