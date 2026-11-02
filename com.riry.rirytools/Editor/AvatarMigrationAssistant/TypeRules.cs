using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The rules of each component type: which source components are copied, Transforms
    /// (root-relative global values), active state, tag and layer, MeshRenderers,
    /// SkinnedMeshRenderers (blendshapes by name through Blendshape Sync's matcher) and
    /// the Avatar Descriptor's groups.
    /// </summary>
    internal sealed partial class AvatarCopyRun
    {
        // Names an armature object may have when it is not the parent of the humanoid hips.
        private static readonly string[] ArmatureNames = { "armature", "skeleton", "root", "rig" };

        // The root Animator is copied only with that option on.
        private bool TypeRuleAllows(AvatarCopyType type, Component source)
        {
            if (type.Rule == AvatarCopyTypes.AnimatorsRule && source.transform == sourceRoot && !settings.RootAnimator) return false;
            return true;
        }

        // A Camera is copied only onto an object without one.
        private static bool TargetRuleAllows(AvatarCopyType type, Component source, Transform target)
        {
            if (type.Rule == AvatarCopyTypes.CamerasRule && target.GetComponent<Camera>() != null) return false;
            return true;
        }

        /// <summary>The humanoid hips of an avatar, or null.</summary>
        internal static Transform Hips(Transform root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.isHuman && animator.avatar != null) return animator.GetBoneTransform(HumanBodyBones.Hips);
            }
            return null;
        }

        // The armature object: the parent of the humanoid hips, or an object with an armature name.
        private bool hipsFound;
        private Transform sourceHips;

        private bool IsArmature(Transform source)
        {
            if (!hipsFound)
            {
                hipsFound = true;
                sourceHips = Hips(sourceRoot);
            }
            Transform hips = sourceHips;
            if (hips != null && hips.parent == source) return true;
            return Array.IndexOf(ArmatureNames, source.name.ToLowerInvariant()) >= 0;
        }

        // ---- Transforms -----------------------------------------------------------

        /// <summary>
        /// Position, rotation and scale as global values relative to the avatar root, so they
        /// come out right whatever base scale each avatar has. The root, its direct children
        /// and the armature are left alone.
        /// </summary>
        private void CopyTransforms(AvatarTypeSettings typeSettings)
        {
            if (!settings.TransformPosition && !settings.TransformRotation && !settings.TransformScale) return;
            foreach (Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (source == sourceRoot || source.parent == sourceRoot || IsArmature(source) || !InScope(source)) continue;
                Transform target = TargetFor(source, typeSettings.CreateMissing);
                if (target == null)
                {
                    report.NotCopied.Add("Transform of " + AnimationUtility.CalculateTransformPath(source, sourceRoot) + ": no match on the target.");
                    continue;
                }
                Undo.RecordObject(target, AvatarMigrationAssistant.Title);
                if (settings.TransformPosition) target.position = targetRoot.TransformPoint(sourceRoot.InverseTransformPoint(source.position));
                if (settings.TransformRotation) target.rotation = targetRoot.rotation * (Quaternion.Inverse(sourceRoot.rotation) * source.rotation);
                if (settings.TransformScale && target.parent != null)
                {
                    Vector3 relative = Divide(source.lossyScale, sourceRoot.lossyScale);
                    target.localScale = Divide(Vector3.Scale(relative, targetRoot.lossyScale), target.parent.lossyScale);
                }
            }
        }

        private static Vector3 Divide(Vector3 value, Vector3 by)
        {
            return new Vector3(by.x != 0f ? value.x / by.x : value.x, by.y != 0f ? value.y / by.y : value.y, by.z != 0f ? value.z / by.z : value.z);
        }

        /// <summary>Active state, tag and layer, set on each matched object (not the root).</summary>
        private void CopyActiveTagLayer()
        {
            if (!settings.RuleEnabled(AvatarCopyTypes.TransformsRule)) return;
            if (!settings.TransformTagLayer && !settings.TransformActive) return;
            foreach (Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (source == sourceRoot || !InScope(source)) continue;
                Transform target = map.TargetOf(source);
                if (target == null || target == targetRoot) continue;
                Undo.RecordObject(target.gameObject, AvatarMigrationAssistant.Title);
                if (settings.TransformTagLayer)
                {
                    target.gameObject.layer = source.gameObject.layer;
                    try
                    {
                        target.gameObject.tag = source.gameObject.tag;
                    }
                    catch (UnityException exception)
                    {
                        report.NotCopied.Add("Tag of " + target.name + ": " + exception.Message);
                    }
                }
                if (settings.TransformActive) target.gameObject.SetActive(source.gameObject.activeSelf);
            }
        }

        // ---- Renderers ------------------------------------------------------------

        /// <summary>MeshRenderers that have a MeshFilter, copied with it.</summary>
        private void CopyMeshRenderers(AvatarTypeSettings typeSettings)
        {
            if (typeSettings.Replace) ClearTarget(typeof(MeshRenderer));
            foreach (MeshRenderer source in sourceRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || !InScope(source.transform)) continue;
                Transform target = TargetFor(source.transform, typeSettings.CreateMissing);
                if (target == null)
                {
                    report.NotCopied.Add(Describe(source) + ": its object has no match on the target (Create missing objects is off).");
                    continue;
                }
                CopyComponent(filter, target.gameObject);
                CopyComponent(source, target.gameObject);
            }
        }

        // The SkinnedMeshRenderer settings copied with "settings"; bones and the mesh stay the target's.
        private static readonly string[] SkinnedSettingNames =
        {
            "m_CastShadows", "m_ReceiveShadows", "m_DynamicOccludee", "m_MotionVectors", "m_LightProbeUsage", "m_ReflectionProbeUsage",
            "m_RayTracingMode", "m_RenderingLayerMask", "m_RendererPriority", "m_SortingLayerID", "m_SortingOrder", "m_Quality",
            "m_UpdateWhenOffscreen", "m_SkinnedMotionVectors", "m_ProbeAnchor", "m_LightProbeVolumeOverride"
        };

        /// <summary>
        /// SkinnedMeshRenderers come with their model or prefab, so only the target's own are
        /// written: settings, materials and bounds when the up-front check said to copy them,
        /// and blendshape values matched by name.
        /// </summary>
        private void CopySkinnedMeshRenderers()
        {
            foreach (SkinnedMeshRenderer source in sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!InScope(source.transform)) continue;
                Transform targetObject = map.TargetOf(source.transform);
                SkinnedMeshRenderer target = targetObject != null ? targetObject.GetComponent<SkinnedMeshRenderer>() : null;
                if (target == null)
                {
                    report.PossibleIssues.Add(Describe(source) + ": the target has no SkinnedMeshRenderer there. Skinned meshes come with their " +
                        "model or prefab; check that the mesh is on the new avatar.");
                    continue;
                }
                Undo.RecordObject(target, AvatarMigrationAssistant.Title);
                if (answers.CopySkinnedMeshRenderers)
                {
                    if (settings.SkinnedSettings)
                    {
                        var from = new SerializedObject(source);
                        var to = new SerializedObject(target);
                        foreach (string name in SkinnedSettingNames)
                        {
                            SerializedProperty property = from.FindProperty(name);
                            if (property != null) to.CopyFromSerializedProperty(property);
                        }
                        to.ApplyModifiedProperties();
                    }
                    if (settings.SkinnedMaterials) target.sharedMaterials = source.sharedMaterials;
                    if (settings.SkinnedBounds) target.localBounds = source.localBounds;
                    copied[source] = target;
                }
                if (settings.SkinnedBlendshapes) CopyBlendshapes(source, target);
            }
        }

        // Blendshape values matched by exact name, with Blendshape Sync's matcher.
        private void CopyBlendshapes(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            if (source.sharedMesh == null || target.sharedMesh == null) return;
            string[] sourceNames = BlendshapeNames(source.sharedMesh);
            string[] targetNames = BlendshapeNames(target.sharedMesh);
            var links = new List<BlendshapeSyncWindow.IndexLink>();
            string error;
            bool collision;
            if (!BlendshapeSyncWindow.TryBuildLinks(sourceNames, targetNames, null, true, links, out error, out collision))
            {
                report.NotCopied.Add("Blendshapes of " + Describe(source) + ": " + error);
                return;
            }
            foreach (BlendshapeSyncWindow.IndexLink link in links)
                target.SetBlendShapeWeight(link.Destination, source.GetBlendShapeWeight(link.Source));
        }

        private static string[] BlendshapeNames(Mesh mesh)
        {
            var names = new string[mesh.blendShapeCount];
            for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
            return names;
        }

        // ---- Avatar Descriptor ------------------------------------------------------

        private static readonly string[] LipSyncNames =
        {
            "Name", "Animations", "ScaleIPD", "lipSync", "VisemeSkinnedMesh", "MouthOpenBlendShapeName", "VisemeBlendShapes",
            "portraitCameraPositionOffset", "portraitCameraRotationOffset", "lipSyncJawBone", "lipSyncJawClosed", "lipSyncJawOpen",
            "AnimationPreset", "autoFootsteps", "autoLocomotion"
        };
        private static readonly string[] PlayableLayerNames = { "customizeAnimationLayers", "baseAnimationLayers", "specialAnimationLayers" };
        private static readonly string[] EyeLookNames = { "enableEyeLook", "customEyeLookSettings" };
        private static readonly string[] ExpressionNames = { "customExpressions", "expressionsMenu", "expressionParameters" };
        private static readonly string[] ColliderNames =
        {
            // Field names from the SDK's VRCSDK3A.dll; each ColliderConfig keeps its own isMirrored.
            "collider_head", "collider_torso", "collider_handR", "collider_footR", "collider_fingerIndexR",
            "collider_fingerMiddleR", "collider_fingerRingR", "collider_fingerLittleR", "collider_handL", "collider_footL",
            "collider_fingerIndexL", "collider_fingerMiddleL", "collider_fingerRingL", "collider_fingerLittleL"
        };

        /// <summary>
        /// The VRChat Avatar Descriptor by groups of serialized properties, each with its own
        /// option; its bone, mesh and collider references are re-pointed with the rest. The
        /// blueprint ID is copied only with "Copy original blueprint ID?" on. Avatar scale
        /// copies the root scale and moves the viewpoint to match.
        /// </summary>
        private void CopyAvatarDescriptor()
        {
            Type descriptorType = AvatarCopyTypes.Find(AvatarCopyTypes.DescriptorType);
            if (descriptorType == null) return;
            Component source = sourceRoot.GetComponent(descriptorType);
            if (source == null) return;
            Component target = targetRoot.GetComponent(descriptorType);
            if (target == null)
            {
                target = Undo.AddComponent(targetRoot.gameObject, descriptorType);
                made.Add(target);
            }
            else Undo.RecordObject(target, AvatarMigrationAssistant.Title);

            var names = new List<string>();
            if (settings.DescriptorViewpoint) names.Add("ViewPosition");
            if (settings.DescriptorLipSync) names.AddRange(LipSyncNames);
            if (settings.DescriptorPlayableLayers) names.AddRange(PlayableLayerNames);
            if (settings.DescriptorEyeLook) names.AddRange(EyeLookNames);
            if (settings.DescriptorExpressions) names.AddRange(ExpressionNames);
            if (settings.DescriptorColliders) names.AddRange(ColliderNames);
            var from = new SerializedObject(source);
            var to = new SerializedObject(target);
            foreach (string name in names)
            {
                SerializedProperty property = from.FindProperty(name);
                if (property != null) to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedProperties();
            copied[source] = target;

            if (settings.DescriptorBlueprintId) CopyBlueprintId();
            if (settings.DescriptorAvatarScale)
            {
                Vector3 before = targetRoot.localScale;
                Undo.RecordObject(targetRoot, AvatarMigrationAssistant.Title);
                targetRoot.localScale = sourceRoot.localScale;
                SerializedProperty view = to.FindProperty("ViewPosition");
                if (view != null && before.y != 0f)
                {
                    to.Update();
                    view.vector3Value *= sourceRoot.localScale.y / before.y;
                    to.ApplyModifiedProperties();
                }
            }
        }

        private void CopyBlueprintId()
        {
            Type pipelineType = AvatarCopyTypes.Find(AvatarCopyTypes.PipelineManager);
            if (pipelineType == null) return;
            Component source = sourceRoot.GetComponent(pipelineType);
            if (source == null) return;
            Component target = targetRoot.GetComponent(pipelineType);
            if (target == null)
            {
                target = Undo.AddComponent(targetRoot.gameObject, pipelineType);
                made.Add(target);
            }
            SerializedProperty id = new SerializedObject(source).FindProperty("blueprintId");
            if (id == null) return;
            var to = new SerializedObject(target);
            to.CopyFromSerializedProperty(id);
            to.ApplyModifiedProperties();
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant type rules (TypeRules.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarCopyRun (this part)
//   TypeRuleAllows / TargetRuleAllows  The root Animator only with Root animator on; a Camera only onto an object without one.
//   Hips(root)               The humanoid hips of an avatar, or null.
//   IsArmature(source)       The parent of the humanoid hips, or an object named armature, skeleton, root or rig.
//   CopyTransforms           Root-relative global position, rotation and scale; skips the root, its children, the armature.
//   CopyActiveTagLayer       Tag and layer, active state on each matched object but the root (Transforms sub-options).
//   CopyMeshRenderers        Only MeshRenderers with a MeshFilter, copied with it.
//   CopySkinnedMeshRenderers The target's own: settings (SkinnedSettingNames), materials, bounds when the up-front check
//                            said so; blendshape values by exact name (BlendshapeSyncWindow.TryBuildLinks).
//   CopyAvatarDescriptor     Groups: viewpoint, lip sync and visemes (LipSyncNames), playable layers, eye look, expressions,
//                            colliders; CopyBlueprintId (PipelineManager blueprintId); avatar scale moves the viewpoint.
