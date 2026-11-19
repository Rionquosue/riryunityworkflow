using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Writes native face tracking's blendshape weights to the avatar's Face Tracking Meshes in Play Mode. A blendshape
    /// name is matched on every mesh that has it, as VRChat does. Only the renderers' weights are set
    /// (SetBlendShapeWeight): no mesh and no asset is changed. The weight each blendshape had is captured before the
    /// first write and put back on Restore, unless something else has changed it since the last write.
    /// </summary>
    internal sealed class FTEmulatorBlendshapeWriter : IFTEmulatorMeshOutput
    {
        private sealed class Target
        {
            internal SkinnedMeshRenderer Renderer;
            internal int Index;
            internal float Initial;
            internal float Written;
            internal bool Owned;
        }

        // A weight that differs from the one written by more than this was changed by something else.
        private const float SameWeight = 0.01f;

        private readonly SkinnedMeshRenderer[] meshes;
        private List<Target>[] targets = new List<Target>[0];

        internal FTEmulatorBlendshapeWriter(SkinnedMeshRenderer[] meshes)
        {
            this.meshes = meshes ?? new SkinnedMeshRenderer[0];
        }

        public List<string> Bind(string[] blendshapes)
        {
            Restore();
            var missing = new List<string>();
            targets = new List<Target>[blendshapes.Length];
            for (int i = 0; i < blendshapes.Length; i++)
            {
                targets[i] = new List<Target>();
                foreach (SkinnedMeshRenderer mesh in meshes)
                {
                    if (mesh == null || mesh.sharedMesh == null) continue;
                    int index = mesh.sharedMesh.GetBlendShapeIndex(blendshapes[i]);
                    if (index >= 0) targets[i].Add(new Target { Renderer = mesh, Index = index });
                }
                if (targets[i].Count == 0) missing.Add(blendshapes[i]);
            }
            return missing;
        }

        public void Apply(float[] weights, bool[] owned)
        {
            for (int i = 0; i < targets.Length && i < weights.Length; i++)
            {
                if (!owned[i]) continue;
                foreach (Target target in targets[i])
                {
                    // A mesh destroyed during Play Mode is skipped.
                    if (target.Renderer == null) continue;
                    if (!target.Owned)
                    {
                        target.Initial = target.Renderer.GetBlendShapeWeight(target.Index);
                        target.Owned = true;
                    }
                    target.Renderer.SetBlendShapeWeight(target.Index, weights[i]);
                    target.Written = weights[i];
                }
            }
        }

        public void Restore()
        {
            foreach (List<Target> list in targets)
            {
                foreach (Target target in list)
                {
                    if (!target.Owned) continue;
                    target.Owned = false;
                    if (target.Renderer == null) continue;
                    // Something else wrote it after FTEmulator did: it has a new owner, leave it.
                    if (Mathf.Abs(target.Renderer.GetBlendShapeWeight(target.Index) - target.Written) > SameWeight) continue;
                    target.Renderer.SetBlendShapeWeight(target.Index, target.Initial);
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator blendshape writer
// ============================================================================
//
// FTEmulatorBlendshapeWriter (this file): IFTEmulatorMeshOutput
//   Bind(blendshapes)        Finds each name on every Face Tracking Mesh; returns the names no mesh has. Restores first.
//   Apply(weights, owned)    Sets the owned weights (0..100), capturing each blendshape's weight before its first write.
//                            Called after animation (FTEmulator's PostLateUpdate step).
//   Restore()                Puts the captured weights back, except where the current weight is no longer the one written
//                            (SameWeight, 0.01): something else owns it now.
