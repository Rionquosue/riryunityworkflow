using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Riry.RiryTools.Editor
{
    /// <summary>The answers given in the up-front check, read by the run.</summary>
    internal sealed class AvatarMigratorPreFlightAnswers
    {
        internal bool CopySkinnedMeshRenderers;
        // Bounds of renderers whose mesh is a different one on the target.
        internal bool CopyChangedBounds;
        // Unpacked prefabs placed again as child prefabs of their models (AvatarMigratorCopyRun.RepackPrefabs.cs).
        internal bool RepackPrefabs;
        internal bool CopyMeshRenderers;
        internal bool UpgradeConstraints;
    }
}
