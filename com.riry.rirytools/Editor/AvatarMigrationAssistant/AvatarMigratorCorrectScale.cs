using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 6, always on: collision and constraint values are stored in a bone's local
    /// scale, which differs when one build was exported at the wrong scale. The factor per axis
    /// is sourceBone.lossyScale / targetBone.lossyScale, the bone being the one the value is
    /// stored relative to: the component's own transform for PhysBone, collider and contact
    /// values, the constraint source for per-source offsets, the constrained object's parent
    /// for at-rest values. Vectors are multiplied per axis, single numbers by the largest
    /// absolute axis, and every corrected value is rounded to 8 decimal places. A factor of 1
    /// is skipped. Runs after the rotation correction.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        private const int ScaleDecimals = 8;

        private void CorrectScale()
        {
            foreach (var pair in new List<KeyValuePair<Component, Component>>(copied))
            {
                Component source = pair.Key;
                Component target = pair.Value;
                if (source == null || target == null) continue;
                Vector3 own = Factor(OriginalTransform(source.transform), target.transform);
                Transform sourceParent = OriginalTransform(source.transform.parent);
                Vector3 parentFactor = Factor(sourceParent, target.transform.parent);

                var box = target as BoxCollider;
                var sphere = target as SphereCollider;
                var capsule = target as CapsuleCollider;
                if (box != null || sphere != null || capsule != null)
                {
                    if (IsOne(own)) continue;
                    Undo.RecordObject(target, AvatarMigrationAssistant.Title);
                    if (box != null)
                    {
                        box.center = ScaleVector(box.center, own);
                        box.size = ScaleVector(box.size, own);
                    }
                    if (sphere != null)
                    {
                        sphere.center = ScaleVector(sphere.center, own);
                        sphere.radius = ScaleNumber(sphere.radius, own);
                    }
                    if (capsule != null)
                    {
                        capsule.center = ScaleVector(capsule.center, own);
                        capsule.radius = ScaleNumber(capsule.radius, own);
                        capsule.height = ScaleNumber(capsule.height, own);
                    }
                    continue;
                }
                var parent = target as ParentConstraint;
                if (parent != null)
                {
                    Undo.RecordObject(target, AvatarMigrationAssistant.Title);
                    var from = (ParentConstraint)source;
                    for (int i = 0; i < parent.sourceCount && i < from.sourceCount; i++)
                    {
                        Vector3 factor = Factor(OriginalTransform(from.GetSource(i).sourceTransform), parent.GetSource(i).sourceTransform);
                        if (!IsOne(factor)) parent.SetTranslationOffset(i, ScaleVector(parent.GetTranslationOffset(i), factor));
                    }
                    if (!IsOne(parentFactor)) parent.translationAtRest = ScaleVector(parent.translationAtRest, parentFactor);
                    continue;
                }
                var position = target as PositionConstraint;
                if (position != null)
                {
                    if (IsOne(parentFactor)) continue;
                    Undo.RecordObject(target, AvatarMigrationAssistant.Title);
                    position.translationOffset = ScaleVector(position.translationOffset, parentFactor);
                    position.translationAtRest = ScaleVector(position.translationAtRest, parentFactor);
                    continue;
                }

                string type = target.GetType().FullName ?? "";
                if (type == AvatarMigratorCopyTypes.PhysBone) ScaleSerialized(target, own, new[] { "radius" }, new[] { "endpointPosition" });
                else if (type == AvatarMigratorCopyTypes.PhysBoneCollider) ScaleSerialized(target, own, new[] { "radius", "height" }, new[] { "position" });
                else if (type == AvatarMigratorCopyTypes.ContactSender || type == AvatarMigratorCopyTypes.ContactReceiver)
                    ScaleSerialized(target, own, new[] { "radius", "height" }, new[] { "position" });
                else if (type == "DynamicBone") ScaleSerialized(target, own, new[] { "m_Radius", "m_EndLength" }, new[] { "m_EndOffset" });
                else if (type == "DynamicBoneCollider" || type == "DynamicBonePlaneCollider")
                    ScaleSerialized(target, own, new[] { "m_Radius", "m_Height" }, new[] { "m_Center" });
                else if (type.StartsWith(AvatarMigratorCopyTypes.VrcConstraintPrefix, StringComparison.Ordinal)) ScaleVrcConstraint(source, target, parentFactor);
            }
        }

        private static void ScaleSerialized(Component target, Vector3 factor, string[] numbers, string[] vectors)
        {
            if (IsOne(factor)) return;
            var serialized = new SerializedObject(target);
            foreach (string name in numbers)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property != null && property.propertyType == SerializedPropertyType.Float) property.floatValue = ScaleNumber(property.floatValue, factor);
            }
            foreach (string name in vectors)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property != null && property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = ScaleVector(property.vector3Value, factor);
            }
            serialized.ApplyModifiedProperties();
        }

        // A VRC constraint's PositionOffset and PositionAtRest (the constrained object's parent) and, for a
        // parent constraint, each source's ParentPositionOffset (that source).
        private void ScaleVrcConstraint(Component source, Component target, Vector3 parentFactor)
        {
            var to = new SerializedObject(target);
            if (!IsOne(parentFactor))
            {
                foreach (string name in new[] { "PositionOffset", "PositionAtRest" })
                {
                    SerializedProperty property = to.FindProperty(name);
                    if (property != null && property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = ScaleVector(property.vector3Value, parentFactor);
                }
            }
            if (target.GetType().FullName == VrcParent)
            {
                List<SerializedProperty> fromSources = VrcSources(new SerializedObject(source));
                List<SerializedProperty> toSources = VrcSources(to);
                for (int i = 0; i < fromSources.Count && i < toSources.Count; i++)
                {
                    Transform sourceBone = OriginalTransform(fromSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform);
                    Transform targetBone = toSources[i].FindPropertyRelative("SourceTransform").objectReferenceValue as Transform;
                    Vector3 factor = Factor(sourceBone, targetBone);
                    SerializedProperty offset = toSources[i].FindPropertyRelative("ParentPositionOffset");
                    if (!IsOne(factor) && offset != null) offset.vector3Value = ScaleVector(offset.vector3Value, factor);
                }
            }
            to.ApplyModifiedProperties();
        }

        private static Vector3 Factor(Transform sourceBone, Transform targetBone)
        {
            if (sourceBone == null || targetBone == null) return Vector3.one;
            Vector3 from = sourceBone.lossyScale;
            Vector3 to = targetBone.lossyScale;
            return new Vector3(to.x != 0f ? from.x / to.x : 1f, to.y != 0f ? from.y / to.y : 1f, to.z != 0f ? from.z / to.z : 1f);
        }

        private static bool IsOne(Vector3 factor)
        {
            return Mathf.Approximately(factor.x, 1f) && Mathf.Approximately(factor.y, 1f) && Mathf.Approximately(factor.z, 1f);
        }

        private static Vector3 ScaleVector(Vector3 value, Vector3 factor)
        {
            return new Vector3(Round(value.x * factor.x), Round(value.y * factor.y), Round(value.z * factor.z));
        }

        private static float ScaleNumber(float value, Vector3 factor)
        {
            return Round(value * Mathf.Max(Mathf.Abs(factor.x), Mathf.Abs(factor.y), Mathf.Abs(factor.z)));
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, ScaleDecimals);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant scale (AvatarMigratorCorrectScale.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   CorrectScale()      Factor(sourceBone, targetBone) = source lossyScale / target lossyScale per axis. The component's own
//                       transform: Box/Sphere/Capsule collider center, size, radius, height; VRCPhysBone radius,
//                       endpointPosition; VRCPhysBoneCollider and contacts radius, height, position; DynamicBone m_Radius,
//                       m_EndLength, m_EndOffset; DynamicBone colliders m_Radius, m_Height, m_Center. The constraint source:
//                       Parent constraint translation offsets (VRC: ParentPositionOffset). The constrained object's
//                       parent: translationAtRest, Position constraint translationOffset (VRC: PositionOffset, PositionAtRest).
//   ScaleVector / ScaleNumber  Per axis / by the largest absolute axis; Round to ScaleDecimals (8).
//   IsOne(factor)       A factor of 1 is skipped (Mathf.Approximately).
