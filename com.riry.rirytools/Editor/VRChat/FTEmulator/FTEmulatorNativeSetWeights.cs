using System;
using UnityEngine;
#if RIRYTOOLS_VRC_BIT_WIDTH
using VRC.SDKBase;
#endif

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Follows the Animator Face Tracking Blendshapes Set state behavior on one avatar: when a state with it is entered,
    /// the SDK raises VRC_AnimatorFaceTrackingBlendshapesSet.OnApplySettings (a public hook), and each of its blend sets
    /// gives a Blendshapes Set a new weight, constant or sampled from an animator parameter at that moment. The weight
    /// is handed to apply (set name, weight, lerp speed); the session moves the set's weight there.
    /// </summary>
    internal sealed class FTEmulatorNativeSetWeights
    {
        private readonly GameObject avatar;
        private readonly Func<string, float?> readParameter;
        private readonly Action<string, float, float> apply;
        private bool bound;

        internal FTEmulatorNativeSetWeights(GameObject avatar, Func<string, float?> readParameter, Action<string, float, float> apply)
        {
            this.avatar = avatar;
            this.readParameter = readParameter;
            this.apply = apply;
        }

        internal void Bind()
        {
            if (bound) return;
            bound = true;
#if RIRYTOOLS_VRC_BIT_WIDTH
            VRC_AnimatorFaceTrackingBlendshapesSet.OnApplySettings += OnStateEntered;
#endif
        }

        internal void Release()
        {
            if (!bound) return;
            bound = false;
#if RIRYTOOLS_VRC_BIT_WIDTH
            VRC_AnimatorFaceTrackingBlendshapesSet.OnApplySettings -= OnStateEntered;
#endif
        }

#if RIRYTOOLS_VRC_BIT_WIDTH
        private void OnStateEntered(VRC_AnimatorFaceTrackingBlendshapesSet behaviour, Animator animator)
        {
            // Other avatars in the scene raise the same hook.
            if (behaviour == null || animator == null || avatar == null || animator.gameObject != avatar || behaviour.blendSets == null) return;
            foreach (VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet blend in behaviour.blendSets)
            {
                if (blend == null || string.IsNullOrEmpty(blend.blendshapesSetName)) continue;
                float weight = blend.weight;
                if (blend.type == VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter)
                {
                    float? sampled = readParameter(blend.sourceParameter);
                    if (!sampled.HasValue) continue;
                    weight = sampled.Value;
                    if (blend.convertRange)
                    {
                        float span = blend.sourceMax - blend.sourceMin;
                        float t = span != 0f ? Mathf.Clamp01((weight - blend.sourceMin) / span) : 0f;
                        weight = blend.destMin + (blend.destMax - blend.destMin) * t;
                    }
                }
                apply(blend.blendshapesSetName, weight, blend.lerpSpeed);
            }
        }
#endif
    }
}

// ============================================================================
// GLOSSARY: FTEmulator native set weights (the SDK hook is used only with RIRYTOOLS_VRC_BIT_WIDTH, SDK 3.10.6+)
// ============================================================================
//
// FTEmulatorNativeSetWeights (this file)
//   Bind() / Release()       Adds and removes its handler on VRC_AnimatorFaceTrackingBlendshapesSet.OnApplySettings.
//   OnStateEntered           For the bound avatar's animator only: each blend set's weight (Value, or Parameter read through
//                            readParameter, with Convert Range) is passed to apply(set, weight, lerp speed).
