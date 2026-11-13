using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The numbers behind Constraint Tool's Insertable prop tab, with no Unity objects:
    /// the insert entry point, how long a smoothed slide takes, the joint's damping,
    /// the drop collider and the pose a prop lies in on Android.
    /// </summary>
    internal static class InsertablePropMath
    {
        // VRCPhysBoneBase.MAX_STRETCH in VRC.Dynamics.dll (SDK 3.10.6): Max Stretch can't exceed 5.
        internal const float MaxStretch = 5f;
        // Rest distance from the PhysBone's root to its end: reach is about AnchorLength * (1 + MaxStretch).
        internal const float AnchorLength = 1.7f;
        // How far past the seated depth the entry point sits outside the opening.
        internal const float EntryMargin = 0.03f;
        // Share of the way a smoothed constraint has moved when a slide step ends.
        internal const float SettledFraction = 0.99f;

        /// <summary>
        /// Seconds for VRChat's constraint smoothing to cover <paramref name="fraction"/> of the way:
        /// result = lerp(current, target, 1 - exp(-dt / s)), so the share left is exp(-t / s).
        /// </summary>
        internal static float SmoothedDuration(float smoothing, float fraction)
        {
            if (smoothing <= 0f) return 0f;
            return -smoothing * Mathf.Log(1f - Mathf.Clamp(fraction, 0f, 0.9999f));
        }

        /// <summary>Critical damping of a spring of this stiffness on this mass (or inertia).</summary>
        internal static float CriticalDamping(float stiffness, float mass)
        {
            return 2f * Mathf.Sqrt(Mathf.Max(0f, stiffness) * Mathf.Max(0f, mass));
        }

        /// <summary>Largest principal inertia of a solid box: m (b^2 + c^2) / 12 over its two other sides.</summary>
        internal static float BoxInertia(float mass, Vector3 size)
        {
            float x = size.y * size.y + size.z * size.z;
            float y = size.x * size.x + size.z * size.z;
            float z = size.x * size.x + size.y * size.y;
            return mass * Mathf.Max(x, Mathf.Max(y, z)) / 12f;
        }

        /// <summary>
        /// Where the prop goes before sliding in: the seated pose moved back out along the insertion
        /// axis by how deep the tip sits past the opening, plus EntryMargin. axis points inward.
        /// </summary>
        internal static Vector3 EntryPosition(Vector3 seatPosition, Vector3 seatedTip, Vector3 opening, Vector3 axis)
        {
            Vector3 inward = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.forward;
            float depth = Mathf.Max(0f, Vector3.Dot(seatedTip - opening, inward));
            return seatPosition - inward * (depth + EntryMargin);
        }

        /// <summary>
        /// Android's resting pose relative to the hit point: the bounds' thinnest axis turned to
        /// point up, raised so the lowest corner touches the surface. bounds are in the prop's space.
        /// </summary>
        internal static void LyingPose(Bounds bounds, out Vector3 position, out Quaternion rotation)
        {
            Vector3 size = bounds.size;
            Vector3 thin = Vector3.up;
            if (size.x <= size.y && size.x <= size.z) thin = Vector3.right;
            else if (size.z <= size.x && size.z <= size.y) thin = Vector3.forward;
            rotation = Quaternion.FromToRotation(thin, Vector3.up);
            float lowest = float.MaxValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                lowest = Mathf.Min(lowest, (rotation * point).y);
            }
            position = new Vector3(0f, -lowest, 0f);
        }

        /// <summary>
        /// The grip's inverse as a parent-constraint offset: following a source at the hand with this
        /// offset puts the prop where its grip lands in the hand. gripPosition/gripRotation are the
        /// grip's pose in the prop's space.
        /// </summary>
        internal static void InverseOffset(Vector3 gripPosition, Quaternion gripRotation, out Vector3 position, out Vector3 euler)
        {
            Quaternion inverse = Quaternion.Inverse(gripRotation);
            position = inverse * -gripPosition;
            euler = inverse.eulerAngles;
        }
    }
}

// ============================================================================
// GLOSSARY: Insertable prop math (Constraint Tool's Insertable prop tab)
// ============================================================================
//
// InsertablePropMath            Static math, no Unity objects; used by Editor/VRChat/InsertableProp*.cs.
//   MaxStretch (5)              VRCPhysBoneBase.MAX_STRETCH in the 3.10.6 SDK.
//   AnchorLength (1.7 m)        PhysBone root-to-end rest distance; with MaxStretch, a reach of about 10 m.
//   EntryMargin (0.03 m)        Extra distance outside the opening for the entry point.
//   SettledFraction (0.99)      Share of the way a smoothed step covers before the next one starts.
//   SmoothedDuration(s, f)      Time for constraint smoothing s to cover share f: -s * ln(1 - f).
//   CriticalDamping(k, m)       2 * sqrt(k * m): the joint's damper for stiffness k on mass (or inertia) m.
//   BoxInertia(m, size)         Largest principal inertia of a box, for the joint's rotation drive.
//   EntryPosition(...)          Seat moved back out along the inward axis by the seated tip's depth plus EntryMargin.
//   LyingPose(bounds, ...)      Android rest pose: thinnest axis up, lowest corner on the surface.
//   InverseOffset(...)          The grip's inverse pose, as a parent-constraint source offset (position, euler).
