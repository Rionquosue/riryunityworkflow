#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Weight math for a staggered "train" of parent constraints over a 0..1 value.
    /// Bone b (1-based, chain root first) of n moves from its rest target along
    /// physics chain 1, one chain bone per step, and arrives at chain-1 bone b at
    /// TrainEnd. The tip leads; each bone toward the root starts one step later.
    /// The remaining range blends every bone from chain 1 to 2, then 2 to 3, etc.
    /// </summary>
    internal static class ConstraintTrainPlanner
    {
        internal const int MaxChains = 5;
        private const float SimplifyTolerance = 0.0001f;
        private const float SampleTolerance = 0.002f;
        private const int MaxSubdivisions = 12;

        internal sealed class Settings
        {
            public int BoneCount;
            public int ChainCount;
            public float TrainEnd;
            public AnimationCurve StepCurve;
            public AnimationCurve BlendCurve;
        }

        // Source layout on bone i (0-based):
        //   0            rest target
        //   1 .. i+1     chain 1, bones 0 .. i (the train path)
        //   i+1+c        chain c+1, bone i, for c = 1 .. ChainCount-1
        internal static int SourceCount(int bone, int chainCount)
        {
            return bone + chainCount + 1;
        }

        internal static int ChainSource(int bone, int chain)
        {
            return chain == 0 ? bone + 1 : bone + 1 + chain;
        }

        /// <summary>chain is -1 for the rest target.</summary>
        internal static void DescribeSource(int bone, int source, out int chain, out int chainBone)
        {
            if (source == 0)
            {
                chain = -1;
                chainBone = bone;
            }
            else if (source <= bone + 1)
            {
                chain = 0;
                chainBone = source - 1;
            }
            else
            {
                chain = source - bone - 1;
                chainBone = bone;
            }
        }

        internal static float DefaultTrainEnd(int chainCount)
        {
            return 1f / Mathf.Max(1, chainCount);
        }

        internal static string Validate(Settings settings)
        {
            if (settings.BoneCount < 1) return "The real bone chain needs at least one bone.";
            if (settings.ChainCount < 1 || settings.ChainCount > MaxChains)
                return "Use between 1 and " + MaxChains + " physics chains.";
            if (!(settings.TrainEnd > 0f) || settings.TrainEnd > 1f)
                return "The train end must be above 0 and at most 1.";
            if (settings.ChainCount > 1 && settings.TrainEnd >= 1f)
                return "Leave room after the train end to blend between physics chains.";
            return null;
        }

        /// <summary>Writes the weight of every source on one bone at value t.</summary>
        internal static void EvaluateWeights(Settings settings, int bone, float t, float[] weights)
        {
            int count = SourceCount(bone, settings.ChainCount);
            Array.Clear(weights, 0, count);
            t = Mathf.Clamp01(t);
            int n = settings.BoneCount;
            int b = bone + 1;

            if (t < settings.TrainEnd)
            {
                float step = settings.TrainEnd / n;
                // Train node 0 is rest, node k is chain-1 bone k-1; node index == source index.
                float position = Mathf.Clamp(t / step - (n - b), 0f, b);
                int node = Mathf.Min((int)Math.Floor(position), b);
                if (node >= b)
                {
                    weights[b] = 1f;
                    return;
                }
                float shaped = Shape(settings.StepCurve, position - node);
                weights[node] = 1f - shaped;
                weights[node + 1] = shaped;
                return;
            }

            int chains = settings.ChainCount;
            if (chains == 1)
            {
                weights[ChainSource(bone, 0)] = 1f;
                return;
            }

            float segment = (1f - settings.TrainEnd) / (chains - 1);
            float x = (t - settings.TrainEnd) / segment;
            if (x >= chains - 1)
            {
                weights[ChainSource(bone, chains - 1)] = 1f;
                return;
            }
            int from = (int)Math.Floor(x);
            float blend = Shape(settings.BlendCurve, x - from);
            weights[ChainSource(bone, from)] = 1f - blend;
            weights[ChainSource(bone, from + 1)] = blend;
        }

        /// <summary>Values where a weight can change slope even with linear curves.</summary>
        internal static List<float> BreakTimes(Settings settings)
        {
            var times = new List<float>();
            for (int q = 0; q <= settings.BoneCount; q++)
                times.Add(settings.TrainEnd * q / settings.BoneCount);
            int chains = settings.ChainCount;
            for (int m = 1; m < chains; m++)
                times.Add(settings.TrainEnd + (1f - settings.TrainEnd) * m / (chains - 1));
            times.Add(1f);
            times.Sort();
            for (int i = times.Count - 1; i > 0; i--)
            {
                if (times[i] - times[i - 1] < 1e-6f) times.RemoveAt(i);
            }
            return times;
        }

        /// <summary>
        /// One curve per source of a bone, keyed over 0..1 seconds for a Motion
        /// Time state. Intervals are split until straight keys stay within
        /// <see cref="SampleTolerance"/> of the weights, then collinear keys are
        /// removed, so linear settings produce keys only at the break times.
        /// </summary>
        internal static AnimationCurve[] BuildCurves(Settings settings, int bone)
        {
            int count = SourceCount(bone, settings.ChainCount);
            var times = new List<float>();
            var samples = new List<float[]>();
            List<float> breaks = BreakTimes(settings);
            float[] first = Sample(settings, bone, breaks[0]);
            times.Add(breaks[0]);
            samples.Add(first);
            for (int i = 0; i < breaks.Count - 1; i++)
            {
                float[] last = Sample(settings, bone, breaks[i + 1]);
                Subdivide(settings, bone, breaks[i], samples[samples.Count - 1], breaks[i + 1], last, 0,
                    times, samples);
                times.Add(breaks[i + 1]);
                samples.Add(last);
            }

            var curves = new AnimationCurve[count];
            var values = new float[times.Count];
            for (int s = 0; s < count; s++)
            {
                for (int i = 0; i < times.Count; i++) values[i] = samples[i][s];
                curves[s] = LinearCurve(times, values);
            }
            return curves;
        }

        private static float[] Sample(Settings settings, int bone, float t)
        {
            var weights = new float[SourceCount(bone, settings.ChainCount)];
            EvaluateWeights(settings, bone, t, weights);
            return weights;
        }

        /// <summary>Adds the interior samples of (a, b), in order.</summary>
        private static void Subdivide(Settings settings, int bone, float a, float[] wa, float b, float[] wb,
            int depth, List<float> times, List<float[]> samples)
        {
            float mid = (a + b) * 0.5f;
            float[] wm = Sample(settings, bone, mid);
            // Check quarter points too, so an S-shaped span is not mistaken for a line.
            float[] wq1 = Sample(settings, bone, (a + mid) * 0.5f);
            float[] wq3 = Sample(settings, bone, (mid + b) * 0.5f);
            bool straight = true;
            for (int s = 0; s < wm.Length && straight; s++)
            {
                straight = Mathf.Abs(wm[s] - (wa[s] + wb[s]) * 0.5f) <= SampleTolerance &&
                    Mathf.Abs(wq1[s] - (wa[s] * 0.75f + wb[s] * 0.25f)) <= SampleTolerance &&
                    Mathf.Abs(wq3[s] - (wa[s] * 0.25f + wb[s] * 0.75f)) <= SampleTolerance;
            }
            if (straight || depth >= MaxSubdivisions) return;
            Subdivide(settings, bone, a, wa, mid, wm, depth + 1, times, samples);
            times.Add(mid);
            samples.Add(wm);
            Subdivide(settings, bone, mid, wm, b, wb, depth + 1, times, samples);
        }

        private static AnimationCurve LinearCurve(List<float> times, float[] values)
        {
            List<int> kept = Simplify(times, values);
            var keys = new Keyframe[kept.Count];
            for (int k = 0; k < kept.Count; k++)
            {
                float left = k > 0 ? Slope(times, values, kept[k - 1], kept[k]) : 0f;
                float right = k < kept.Count - 1 ? Slope(times, values, kept[k], kept[k + 1]) : 0f;
                if (k == 0) left = right;
                if (k == kept.Count - 1) right = left;
                keys[k] = new Keyframe(times[kept[k]], values[kept[k]], left, right);
            }
            var curve = new AnimationCurve(keys);
            for (int k = 0; k < keys.Length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        private static float Slope(List<float> times, float[] values, int from, int to)
        {
            float span = times[to] - times[from];
            return span > 0f ? (values[to] - values[from]) / span : 0f;
        }

        private static List<int> Simplify(List<float> times, float[] values)
        {
            var kept = new List<int> { 0 };
            int anchor = 0;
            for (int candidate = 1; candidate < times.Count - 1; candidate++)
            {
                if (FitsLine(times, values, anchor, candidate + 1)) continue;
                kept.Add(candidate);
                anchor = candidate;
            }
            if (times.Count > 1) kept.Add(times.Count - 1);
            return kept;
        }

        private static bool FitsLine(List<float> times, float[] values, int from, int to)
        {
            float span = times[to] - times[from];
            for (int i = from + 1; i < to; i++)
            {
                float u = span > 0f ? (times[i] - times[from]) / span : 0f;
                if (Mathf.Abs(Mathf.Lerp(values[from], values[to], u) - values[i]) > SimplifyTolerance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Evaluates a 0..1 shaping curve, rescaled so it always runs from 0 to 1.
        /// A missing or flat curve is linear.
        /// </summary>
        internal static float Shape(AnimationCurve curve, float u)
        {
            u = Mathf.Clamp01(u);
            if (curve == null || curve.length < 2) return u;
            float start = curve.Evaluate(0f);
            float end = curve.Evaluate(1f);
            if (Mathf.Abs(end - start) < 1e-6f) return u;
            return Mathf.Clamp01((curve.Evaluate(u) - start) / (end - start));
        }

        /// <summary>Exponent that puts a power curve at <paramref name="midpoint"/> halfway.</summary>
        internal static float ExponentForMidpoint(float midpoint)
        {
            midpoint = Mathf.Clamp(midpoint, 0.01f, 0.99f);
            return Mathf.Log(midpoint) / Mathf.Log(0.5f);
        }

        /// <summary>Sampled u^exponent curve with tangents from its derivative.</summary>
        internal static AnimationCurve PowerCurve(float exponent)
        {
            const int segments = 16;
            const float limit = 20f;
            var keys = new Keyframe[segments + 1];
            for (int q = 0; q <= segments; q++)
            {
                float u = (float)q / segments;
                float slope = u > 0f
                    ? exponent * Mathf.Pow(u, exponent - 1f)
                    : (exponent < 1f ? limit : exponent > 1f ? 0f : 1f);
                slope = Mathf.Min(slope, limit);
                keys[q] = new Keyframe(u, Mathf.Pow(u, exponent), slope, slope);
            }
            return new AnimationCurve(keys);
        }

        /// <summary>Animated property for one VRC constraint source weight.</summary>
        internal static string SourceWeightProperty(int source)
        {
            // VRC constraints keep 16 keyable sources, then an overflow list.
            return source < 16
                ? "Sources.source" + source + ".Weight"
                : "Sources.overflowList.Array.data[" + (source - 16) + "].Weight";
        }
    }
}
#endif
