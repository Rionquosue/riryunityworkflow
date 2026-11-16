using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The project's Assets/RiryTools folder, where tools save generated assets when they have no better place. It is
    /// only created when a tool needs it; nothing from the package is copied into the project.
    /// </summary>
    internal static class RiryToolsProjectFolder
    {
        internal const string AssetPath = "Assets/RiryTools";

        /// <summary>Creates Assets/RiryTools when needed and returns its asset path.</summary>
        internal static string Ensure()
        {
            if (!AssetDatabase.IsValidFolder(AssetPath)) AssetDatabase.CreateFolder("Assets", "RiryTools");
            return AssetPath;
        }
    }
}

// ============================================================================
// GLOSSARY: RiryTools project folder
// ============================================================================
//
// RiryToolsProjectFolder (this file)
//   AssetPath            "Assets/RiryTools": assets the tools generate.
//   Ensure()             Creates Assets/RiryTools when a tool needs it; returns its path.
