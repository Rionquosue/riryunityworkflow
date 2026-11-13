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
    internal sealed partial class AvatarMigratorCopyRun
    {
        // Names an armature object may have when it is not the parent of the humanoid hips.
        private static readonly string[] ArmatureNames = { "armature", "skeleton", "root", "rig" };

        // The root Animator is copied only with that option on.
        private bool TypeRuleAllows(AvatarMigratorCopyType type, Component source)
        {
            if (type.Rule == AvatarMigratorCopyTypes.AnimatorsRule && source.transform == sourceRoot && !settings.RootAnimator) return false;
            return true;
        }

        // A Camera is copied only onto an object without one.
        private static bool TargetRuleAllows(AvatarMigratorCopyType type, Component source, Transform target)
        {
            if (type.Rule == AvatarMigratorCopyTypes.CamerasRule && target.GetComponent<Camera>() != null) return false;
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
        private void CopyTransforms(AvatarMigratorTypeSettings typeSettings)
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
                Undo.RecordObject(target, AvatarMigratorWindow.Title);
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
            if (!settings.RuleEnabled(AvatarMigratorCopyTypes.TransformsRule)) return;
            if (!settings.TransformTagLayer && !settings.TransformActive) return;
            foreach (Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (source == sourceRoot || !InScope(source)) continue;
                Transform target = map.TargetOf(source);
                if (target == null || target == targetRoot) continue;
                Undo.RecordObject(target.gameObject, AvatarMigratorWindow.Title);
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
                        RiryLog.Exception(RiryLog.AvatarMigration, "Could not copy the tag of " + target.name, exception, target.gameObject);
                    }
                }
                if (settings.TransformActive) target.gameObject.SetActive(source.gameObject.activeSelf);
            }
        }

        // ---- Renderers ------------------------------------------------------------

        /// <summary>MeshRenderers that have a MeshFilter, copied with it.</summary>
        private void CopyMeshRenderers(AvatarMigratorTypeSettings typeSettings)
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
                Undo.RecordObject(target, AvatarMigratorWindow.Title);
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
                            else MissingField("SkinnedMeshRenderer", name);
                        }
                        to.ApplyModifiedProperties();
                    }
                    if (settings.SkinnedMaterials) target.sharedMaterials = PlaceMaterials(source, target);
                    // The old bounds only fit the old mesh: for a changed mesh, only when the up-front check said so.
                    if (settings.SkinnedBounds && (source.sharedMesh == target.sharedMesh || answers.CopyChangedBounds))
                        target.localBounds = source.localBounds;
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
            var linked = new HashSet<int>();
            foreach (BlendshapeSyncWindow.IndexLink link in links)
            {
                target.SetBlendShapeWeight(link.Destination, source.GetBlendShapeWeight(link.Source));
                linked.Add(link.Source);
            }
            // A set value whose blendshape the new mesh doesn't have is lost: listed, with its value.
            var missing = new List<string>();
            for (int i = 0; i < sourceNames.Length; i++)
            {
                float weight = source.GetBlendShapeWeight(i);
                if (!linked.Contains(i) && weight != 0f) missing.Add(sourceNames[i] + " (" + weight + ")");
            }
            if (missing.Count > 0)
                report.PossibleIssues.Add("Blendshapes of " + Describe(source) + " that the new mesh doesn't have, so their values were not copied: " +
                    string.Join(", ", missing) + ".");
        }

        /// <summary>
        /// The target's materials with the source's placed by slot name: each slot is named by the material its model
        /// gives it by default (SlotNames), so a mesh whose slots were reordered, added or removed still gets each
        /// material on the right part (target slots sharing a name all take it). A slot with no name in common takes the
        /// source's material of the same slot number, if that one is not placed yet; a source material with no place left
        /// is reported.
        /// </summary>
        private Material[] PlaceMaterials(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            Material[] from = source.sharedMaterials;
            var placed = (Material[])target.sharedMaterials.Clone();
            string[] sourceSlots = SlotNames(source), targetSlots = SlotNames(target);
            var used = new bool[from.Length];
            var filled = new bool[placed.Length];
            for (int j = 0; j < placed.Length; j++)
            {
                if (targetSlots[j] == null) continue;
                int found = -1;
                for (int i = 0; i < from.Length; i++)
                {
                    // A source slot may fill several target slots of its name (one material on two parts).
                    if (sourceSlots[i] != targetSlots[j]) continue;
                    // Two source slots of the same name: no name match, so the slot numbers decide.
                    if (found >= 0)
                    {
                        found = -2;
                        break;
                    }
                    found = i;
                }
                if (found < 0) continue;
                placed[j] = from[found];
                used[found] = filled[j] = true;
            }
            for (int j = 0; j < placed.Length; j++)
            {
                if (filled[j] || j >= from.Length || used[j]) continue;
                placed[j] = from[j];
                used[j] = filled[j] = true;
            }
            var left = new List<string>();
            for (int i = 0; i < from.Length; i++)
            {
                if (!used[i] && from[i] != null) left.Add(from[i].name + " (slot " + i + (sourceSlots[i] != null ? ", " + sourceSlots[i] : "") + ")");
            }
            if (left.Count > 0)
                report.NotCopied.Add("Materials of " + Describe(source) + " with no slot on the new mesh: " + string.Join(", ", left) + ".");
            return placed;
        }

        /// <summary>
        /// Each material slot's name: the material its model gives that slot by default (the model's renderer with the
        /// same mesh), else the material on it now; null for an empty slot.
        /// </summary>
        private static string[] SlotNames(SkinnedMeshRenderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            Mesh mesh = renderer.sharedMesh;
            string path = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
            var model = !string.IsNullOrEmpty(path) ? AssetDatabase.LoadMainAssetAtPath(path) as GameObject : null;
            if (model != null)
            {
                foreach (SkinnedMeshRenderer original in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (original.sharedMesh != mesh) continue;
                    materials = original.sharedMaterials;
                    break;
                }
            }
            var names = new string[renderer.sharedMaterials.Length];
            for (int i = 0; i < names.Length; i++) names[i] = i < materials.Length && materials[i] != null ? materials[i].name : null;
            return names;
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
            Type descriptorType = RiryToolsTypes.Named(AvatarMigratorCopyTypes.DescriptorType);
            if (descriptorType == null)
            {
                report.NotCopied.Add("Avatar Descriptor: " + AvatarMigratorCopyTypes.DescriptorType + " was not found in this project, so it was not copied.");
                return;
            }
            Component source = sourceRoot.GetComponent(descriptorType);
            if (source == null) return;
            Component target = targetRoot.GetComponent(descriptorType);
            if (target == null)
            {
                target = Undo.AddComponent(targetRoot.gameObject, descriptorType);
                made.Add(target);
            }
            else Undo.RecordObject(target, AvatarMigratorWindow.Title);

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
                else MissingField("Avatar Descriptor", name);
            }
            to.ApplyModifiedProperties();
            copied[source] = target;
            descriptorSource = source;
            descriptorTarget = target;

            if (settings.DescriptorBlueprintId) CopyBlueprintId();
            if (settings.DescriptorAvatarScale)
            {
                Vector3 before = targetRoot.localScale;
                Undo.RecordObject(targetRoot, AvatarMigratorWindow.Title);
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

        private Component descriptorSource, descriptorTarget;

        /// <summary>
        /// After references are re-pointed (the descriptor's meshes are the target's now): the eyelid blendshapes, which
        /// the descriptor keeps as positions in the eyelid mesh's list, are looked up again by name on the new eyelid
        /// mesh, and the viseme and mouth-open names the new viseme mesh doesn't have are reported.
        /// </summary>
        private void FixDescriptorBlendshapes()
        {
            if (descriptorSource == null || descriptorTarget == null) return;
            var from = new SerializedObject(descriptorSource);
            var to = new SerializedObject(descriptorTarget);
            if (settings.DescriptorEyeLook) RemapEyelids(from, to);
            if (settings.DescriptorLipSync) CheckVisemes(to);
            to.ApplyModifiedProperties();
        }

        private void RemapEyelids(SerializedObject from, SerializedObject to)
        {
            const string EyelidMesh = "customEyeLookSettings.eyelidsSkinnedMesh", EyelidShapes = "customEyeLookSettings.eyelidsBlendshapes";
            SerializedProperty fromShapes = from.FindProperty(EyelidShapes), toShapes = to.FindProperty(EyelidShapes);
            if (fromShapes == null || toShapes == null || !fromShapes.isArray || !toShapes.isArray)
            {
                MissingField("Avatar Descriptor", EyelidShapes);
                return;
            }
            Mesh oldMesh = MeshOf(from.FindProperty(EyelidMesh)), newMesh = MeshOf(to.FindProperty(EyelidMesh));
            if (oldMesh == null || newMesh == null || oldMesh == newMesh) return;
            var missing = new List<string>();
            toShapes.arraySize = fromShapes.arraySize;
            for (int i = 0; i < fromShapes.arraySize; i++)
            {
                int index = fromShapes.GetArrayElementAtIndex(i).intValue;
                int now = -1;
                if (index >= 0 && index < oldMesh.blendShapeCount)
                {
                    string name = oldMesh.GetBlendShapeName(index);
                    now = newMesh.GetBlendShapeIndex(name);
                    if (now < 0) missing.Add(name);
                }
                toShapes.GetArrayElementAtIndex(i).intValue = now;
            }
            if (missing.Count > 0)
                report.PossibleIssues.Add("Eyelid blendshapes the new eyelid mesh (" + newMesh.name + ") doesn't have, set to none: " +
                    string.Join(", ", missing) + ".");
        }

        private void CheckVisemes(SerializedObject to)
        {
            Mesh mesh = MeshOf(to.FindProperty("VisemeSkinnedMesh"));
            if (mesh == null) return;
            // Only the names the Lip Sync mode uses (SDK LipSyncStyle): visemes for the viseme modes, the mouth-open
            // shape for jaw flap; Default (auto detect) may use either.
            SerializedProperty style = to.FindProperty("lipSync");
            string mode = style != null && style.propertyType == SerializedPropertyType.Enum && style.enumValueIndex >= 0 &&
                style.enumValueIndex < style.enumNames.Length ? style.enumNames[style.enumValueIndex] : "Default";
            bool useVisemes = mode == "Default" || mode.StartsWith("VisemeBlendShape", StringComparison.Ordinal);
            bool useMouth = mode == "Default" || mode == "JawFlapBlendShape";
            var names = new List<string>();
            SerializedProperty visemes = to.FindProperty("VisemeBlendShapes");
            if (useVisemes && visemes != null && visemes.isArray)
            {
                for (int i = 0; i < visemes.arraySize; i++) names.Add(visemes.GetArrayElementAtIndex(i).stringValue);
            }
            SerializedProperty mouth = to.FindProperty("MouthOpenBlendShapeName");
            if (useMouth && mouth != null) names.Add(mouth.stringValue);
            var missing = new List<string>();
            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(name) && mesh.GetBlendShapeIndex(name) < 0 && !missing.Contains(name)) missing.Add(name);
            }
            if (missing.Count > 0)
                report.PossibleIssues.Add("Viseme and mouth-open blendshapes the new viseme mesh (" + mesh.name + ") doesn't have: " +
                    string.Join(", ", missing) + ". Set them again in the Avatar Descriptor's Lip Sync.");
        }

        private static Mesh MeshOf(SerializedProperty property)
        {
            var renderer = property != null ? property.objectReferenceValue as SkinnedMeshRenderer : null;
            return renderer != null ? renderer.sharedMesh : null;
        }

        // A serialized field this SDK or Unity version doesn't have (renamed, removed): listed once in the report, which is
        // also logged, instead of the copy silently leaving it out.
        private readonly HashSet<string> missingFields = new HashSet<string>(StringComparer.Ordinal);

        private void MissingField(string component, string field)
        {
            if (missingFields.Add(component + "." + field))
                report.NotCopied.Add(component + " field '" + field + "' was not found in this version, so it was not copied.");
        }

        private void CopyBlueprintId()
        {
            Type pipelineType = RiryToolsTypes.Named(AvatarMigratorCopyTypes.PipelineManager);
            if (pipelineType == null)
            {
                report.NotCopied.Add("Blueprint ID: " + AvatarMigratorCopyTypes.PipelineManager + " was not found in this project, so it was not copied.");
                return;
            }
            Component source = sourceRoot.GetComponent(pipelineType);
            if (source == null) return;
            Component target = targetRoot.GetComponent(pipelineType);
            if (target == null)
            {
                target = Undo.AddComponent(targetRoot.gameObject, pipelineType);
                made.Add(target);
            }
            SerializedProperty id = new SerializedObject(source).FindProperty("blueprintId");
            if (id == null)
            {
                MissingField("Pipeline Manager", "blueprintId");
                return;
            }
            var to = new SerializedObject(target);
            to.CopyFromSerializedProperty(id);
            to.ApplyModifiedProperties();
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant type rules (AvatarMigratorCopyRun.TypeRules.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
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
//   FixDescriptorBlendshapes  After re-pointing: eyelid blendshapes (indices) looked up again by name on the new eyelid
//                            mesh (RemapEyelids; missing ones set to none and reported); viseme and mouth-open names the
//                            new viseme mesh lacks reported, those the Lip Sync mode uses (CheckVisemes).
//   PlaceMaterials / SlotNames  Materials by slot name (the model's default material per slot), then by slot number
//                            where no name matches; source materials left without a slot are reported.
//   CopyBlendshapes          Values by exact name; set values the new mesh has no blendshape for are reported.
//   MissingField(component, field)  A field or type not found in this SDK or Unity version goes in the report's Not
//                            copied, once each, instead of being left out silently.
