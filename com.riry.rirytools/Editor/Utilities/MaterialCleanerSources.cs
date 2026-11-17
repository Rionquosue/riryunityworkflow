using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Material Cleaner's materials to scan: those the open scenes use, or every one in the project's Assets.</summary>
    internal static class MaterialCleanerSources
    {
        /// <summary>
        /// Every material a component in the open scenes references (renderers, VRCFury, Modular Avatar and any other), each
        /// once. Materials in packages or inside models (read-only) are left out.
        /// </summary>
        internal static List<Material> InOpenScenes()
        {
            return ReferencedAssets.InOpenScenes<Material>(value => value as Material, ".mat");
        }

        /// <summary>Every material in Assets.</summary>
        internal static List<Material> InProject()
        {
            return ReferencedAssets.InProject<Material>("t:Material", ".mat");
        }
    }
}

// ============================================================================
// GLOSSARY: Material Cleaner sources
// ============================================================================
//
// MaterialCleanerSources (this file)
//   InOpenScenes()           Materials any component in the open scenes references, .mat files in Assets only.
//   InProject()              Every .mat in Assets.
