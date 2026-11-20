using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Controller Cleaner's controllers to scan: those the open scenes use, or every one in the project's Assets.</summary>
    internal static class ControllerCleanerSources
    {
        /// <summary>
        /// Every animator controller a component in the open scenes references (Animators, avatar layers, VRCFury, Modular
        /// Avatar and any other), with an override controller's base, each once. Controllers in packages are left out.
        /// </summary>
        internal static List<AnimatorController> InOpenScenes()
        {
            return ReferencedAssets.InOpenScenes<AnimatorController>(value =>
                (value is AnimatorOverrideController overrides ? overrides.runtimeAnimatorController : value) as AnimatorController);
        }

        /// <summary>Every animator controller in Assets.</summary>
        internal static List<AnimatorController> InProject()
        {
            return ReferencedAssets.InProject<AnimatorController>("t:AnimatorController");
        }
    }
}
