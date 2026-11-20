using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A mesh's blendshape names, and how an animation curve names a blendshape; used by Avatar Migration Assistant,
    /// Blendshape Sync and Clean Animation Keys.
    /// </summary>
    internal static class BlendshapeNames
    {
        /// <summary>A blendshape curve's property name is this followed by the blendshape's name.</summary>
        internal const string CurvePrefix = "blendShape.";

        /// <summary>The mesh's blendshape names, in index order.</summary>
        internal static string[] Of(Mesh mesh)
        {
            var names = new string[mesh.blendShapeCount];
            for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
            return names;
        }
    }
}

// ============================================================================
// GLOSSARY: blendshape names
// ============================================================================
//
// BlendshapeNames (this file)
//   CurvePrefix              "blendShape.": what a blendshape curve's property name starts with.
//   Of(mesh)                 The mesh's blendshape names, in index order.
