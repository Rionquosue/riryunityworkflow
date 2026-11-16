using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if RIRYTOOLS_VRC_BIT_WIDTH
using VRC.SDKBase;
#endif

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Reads an avatar descriptor's native face tracking settings into FTEmulatorNativeConfig. Native face tracking arrived in VRChat Avatars SDK 3.10.6; with an
    /// older SDK (no RIRYTOOLS_VRC_BIT_WIDTH, the symbol for 3.10.6 and newer) nothing here touches the newer members,
    /// so the package still compiles, and Supported is false.
    /// </summary>
    internal static class FTEmulatorNativeDescriptor
    {
        internal static bool Supported
        {
            get
            {
#if RIRYTOOLS_VRC_BIT_WIDTH
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// The descriptor's settings, or null when the SDK has no native face tracking. meshes receives the Face Tracking
        /// Meshes that exist (at most FTEmulatorNativeConfig.MaxMeshes, as the client uses).
        /// </summary>
        internal static FTEmulatorNativeConfig TryRead(VRCAvatarDescriptor descriptor, out SkinnedMeshRenderer[] meshes)
        {
            meshes = new SkinnedMeshRenderer[0];
#if RIRYTOOLS_VRC_BIT_WIDTH
            if (descriptor == null) return null;
            var config = new FTEmulatorNativeConfig
            {
                Enabled = descriptor.enableFaceTracking,
                UseAllTrackingKeys = descriptor.useAllTrackingKeys,
                VisemesRouted = descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking,
                GazeDisabled = descriptor.enableEyeLook &&
                    descriptor.customEyeLookSettings.eyeGazeMode == VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation,
                BlinkDisabled = descriptor.enableEyeLook && descriptor.customEyeLookSettings.eyelidType != VRCAvatarDescriptor.EyelidType.None &&
                    descriptor.customEyeLookSettings.eyelidType != VRCAvatarDescriptor.EyelidType.FaceTracking,
                VisemeDampen = descriptor.visemeDampenMode != VRCAvatarDescriptor.VisemeDampenMode.None,
            };
            var found = new List<SkinnedMeshRenderer>();
            if (descriptor.faceTrackingMeshes != null)
            {
                config.MeshCount = descriptor.faceTrackingMeshes.Count;
                for (int i = 0; i < Math.Min(descriptor.faceTrackingMeshes.Count, FTEmulatorNativeConfig.MaxMeshes); i++)
                {
                    SkinnedMeshRenderer mesh = descriptor.faceTrackingMeshes[i];
                    if (mesh != null && mesh.sharedMesh != null) found.Add(mesh);
                }
            }
            meshes = found.ToArray();
            if (descriptor.faceTrackingBlendshapesSetList != null)
            {
                foreach (VRCAvatarDescriptor.FaceTrackingBlendshapesSet set in descriptor.faceTrackingBlendshapesSetList)
                {
                    if (set == null) continue;
                    config.Sets.Add(new FTEmulatorNativeConfig.Set
                    {
                        Name = set.setName ?? "",
                        DefaultWeight = set.setWeight,
                        KeyBlendshapes = set.faceTrackingBlendshapeNames ?? new string[0],
                        KeyMultipliers = set.faceTrackingBlendshapeMultipliers ?? new float[0],
                        VisemeBlendshapes = set.visemeBlendshapeNames ?? new string[0],
                        VisemeMultipliers = set.visemeBlendshapeMultipliers ?? new float[0],
                    });
                }
            }
            if (descriptor.faceTrackingParameterBehaviors != null)
            {
                foreach (VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior in descriptor.faceTrackingParameterBehaviors)
                {
                    if (behavior == null) continue;
                    config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
                    {
                        TargetMode = (int)behavior.targetMode,
                        OutputParameter = behavior.outputParameterName,
                        TargetSet = behavior.targetBlendSetName,
                        TargetBlendshape = behavior.targetBlendshapeName,
                        WeightSet = behavior.weightBlendSetName,
                        Inputs = behavior.inputTrackingKeys ?? new int[0],
                        Inverted = behavior.inputInverted ?? new bool[0],
                        CombineMode = (int)behavior.combineMode,
                        Expression = behavior.customExpression,
                    });
                }
            }
            return config;
#else
            return null;
#endif
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator native descriptor (compiled against VRChat Avatars SDK 3.10.6+ members only with RIRYTOOLS_VRC_BIT_WIDTH)
// ============================================================================
//
// FTEmulatorNativeDescriptor (this file)
//   Supported                The SDK has native face tracking (3.10.6 or newer).
//   TryRead(descriptor, out meshes)  enableFaceTracking, useAllTrackingKeys, lip sync mode, eye gaze mode, eyelid type,
//                            viseme dampen, faceTrackingMeshes (the first 16 that exist), faceTrackingBlendshapesSetList
//                            and faceTrackingParameterBehaviors as an FTEmulatorNativeConfig; null with an older SDK.
