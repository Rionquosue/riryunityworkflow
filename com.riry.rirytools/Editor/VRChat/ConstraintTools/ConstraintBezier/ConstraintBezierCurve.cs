using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The numbers behind Constraint Tool's Bezier tab: one cubic Bezier segment
    /// B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3. Its four weights are
    /// never negative and add up to 1, so a VRC Position Constraint with P0..P3 as sources
    /// at these weights holds an object on the curve. The curve is fitted to a bone chain
    /// at rest (P0 on the first bone, P3 on the last), and each node's place along it comes
    /// from the distances between nodes. No Unity objects are touched here.
    /// </summary>
    internal static class ConstraintBezierCurve
    {
        // Steps of the table that turns a distance along the curve into t.
        internal const int ArcSteps = 256;
        // Rounds of fitting the curve, then measuring the nodes' t along it again.
        internal const int FitRounds = 4;
        // Pulls P1 and P2 slightly towards the straight-line handles, so two or three nodes still fit.
        private const double Regularization = 1e-6;

        /// <summary>The fitted curve and where each node sits on it.</summary>
        internal sealed class Fit
        {
            /// <summary>P0 (first node), P1, P2 and P3 (last node), in world space.</summary>
            public Vector3[] Controls = new Vector3[4];
            /// <summary>Each node's t on the curve.</summary>
            public float[] T;
            /// <summary>Each node's place on the curve, B(T[i]).</summary>
            public Vector3[] Points;
            /// <summary>Distance from each node's rest position to its place on the curve.</summary>
            public float[] Error;
            public float Length;
        }

        /// <summary>The four Bernstein weights at t, in source order P0..P3.</summary>
        internal static void Weights(float t, float[] weights)
        {
            float u = 1f - t;
            weights[0] = u * u * u;
            weights[1] = 3f * u * u * t;
            weights[2] = 3f * u * t * t;
            weights[3] = t * t * t;
        }

        internal static Vector3 Point(Vector3[] controls, float t)
        {
            var weights = new float[4];
            Weights(t, weights);
            return weights[0] * controls[0] + weights[1] * controls[1] + weights[2] * controls[2] + weights[3] * controls[3];
        }

        /// <summary>Existing spacing: each node's distance along the chain over the chain's length.</summary>
        internal static float[] ExistingFractions(IList<Vector3> nodes)
        {
            var fractions = new float[nodes.Count];
            float total = 0f;
            for (int i = 1; i < nodes.Count; i++)
            {
                total += Vector3.Distance(nodes[i - 1], nodes[i]);
                fractions[i] = total;
            }
            for (int i = 1; i < nodes.Count; i++) fractions[i] = total > 0f ? fractions[i] / total : (float)i / (nodes.Count - 1);
            fractions[nodes.Count - 1] = 1f;
            return fractions;
        }

        /// <summary>Even spacing: the nodes at equal distances along the curve.</summary>
        internal static float[] EvenFractions(int count)
        {
            var fractions = new float[count];
            for (int i = 0; i < count; i++) fractions[i] = count > 1 ? (float)i / (count - 1) : 0f;
            return fractions;
        }

        /// <summary>
        /// Fits the curve to the nodes (two or more, with some length), then places each node
        /// at its fraction of the curve's length: ExistingFractions or EvenFractions.
        /// </summary>
        internal static Fit FitTo(IList<Vector3> nodes, bool evenSpacing)
        {
            int count = nodes.Count;
            float[] existing = ExistingFractions(nodes);
            var fit = new Fit();
            Vector3 first = nodes[0];
            Vector3 last = nodes[count - 1];
            // Start from straight-line handles and the nodes' chord fractions as t.
            fit.Controls[0] = first;
            fit.Controls[1] = Vector3.Lerp(first, last, 1f / 3f);
            fit.Controls[2] = Vector3.Lerp(first, last, 2f / 3f);
            fit.Controls[3] = last;
            float[] t = (float[])existing.Clone();
            for (int round = 0; round < FitRounds; round++)
            {
                SolveHandles(nodes, t, fit.Controls);
                // Measure again where the nodes' existing fractions fall on the new curve.
                t = ParametersAt(fit.Controls, existing, out fit.Length);
            }
            fit.T = evenSpacing ? ParametersAt(fit.Controls, EvenFractions(count), out fit.Length) : t;
            fit.Points = new Vector3[count];
            fit.Error = new float[count];
            for (int i = 0; i < count; i++)
            {
                fit.Points[i] = Point(fit.Controls, fit.T[i]);
                fit.Error[i] = Vector3.Distance(fit.Points[i], nodes[i]);
            }
            return fit;
        }

        /// <summary>
        /// Least squares for P1 and P2 with P0 and P3 fixed: the curve at each node's t as close
        /// as possible to that node.
        /// </summary>
        private static void SolveHandles(IList<Vector3> nodes, float[] t, Vector3[] controls)
        {
            double a = 0, b = 0, c = 0;
            var r1 = new double[3];
            var r2 = new double[3];
            var weights = new float[4];
            Vector3 line1 = Vector3.Lerp(controls[0], controls[3], 1f / 3f);
            Vector3 line2 = Vector3.Lerp(controls[0], controls[3], 2f / 3f);
            for (int i = 0; i < nodes.Count; i++)
            {
                Weights(t[i], weights);
                Vector3 rest = nodes[i] - weights[0] * controls[0] - weights[3] * controls[3];
                a += weights[1] * weights[1];
                b += weights[1] * weights[2];
                c += weights[2] * weights[2];
                for (int axis = 0; axis < 3; axis++)
                {
                    r1[axis] += weights[1] * rest[axis];
                    r2[axis] += weights[2] * rest[axis];
                }
            }
            double lambda = Regularization * System.Math.Max(1.0, a + c);
            a += lambda;
            c += lambda;
            double determinant = a * c - b * b;
            for (int axis = 0; axis < 3; axis++)
            {
                double x1 = r1[axis] + lambda * line1[axis];
                double x2 = r2[axis] + lambda * line2[axis];
                controls[1][axis] = (float)((c * x1 - b * x2) / determinant);
                controls[2][axis] = (float)((a * x2 - b * x1) / determinant);
            }
        }

        /// <summary>The t at each fraction of the curve's length (0 at P0, 1 at P3).</summary>
        internal static float[] ParametersAt(Vector3[] controls, float[] fractions, out float length)
        {
            var lengths = new float[ArcSteps + 1];
            Vector3 previous = controls[0];
            for (int step = 1; step <= ArcSteps; step++)
            {
                Vector3 point = Point(controls, (float)step / ArcSteps);
                lengths[step] = lengths[step - 1] + Vector3.Distance(previous, point);
                previous = point;
            }
            length = lengths[ArcSteps];
            var result = new float[fractions.Length];
            for (int i = 0; i < fractions.Length; i++)
            {
                float wanted = Mathf.Clamp01(fractions[i]) * length;
                int step = 1;
                while (step < ArcSteps && lengths[step] < wanted) step++;
                float span = lengths[step] - lengths[step - 1];
                float within = span > 0f ? (wanted - lengths[step - 1]) / span : 0f;
                result[i] = length > 0f ? Mathf.Clamp01((step - 1 + within) / ArcSteps) : fractions[i];
            }
            result[0] = 0f;
            result[fractions.Length - 1] = 1f;
            return result;
        }
    }
}

// ============================================================================
// GLOSSARY: Bezier curve math (Constraint Tool's Bezier tab)
// ============================================================================
//
// ConstraintBezierCurve           Static math, no Unity objects; used by Editor/VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs.
//   ArcSteps (256)                Steps of the length table that turns a distance along the curve into t.
//   FitRounds (4)                 Rounds of fit handles, then measure the nodes' t again.
//   Regularization (1e-6)         Pulls P1/P2 slightly to the straight-line handles, so 2 or 3 nodes still solve.
//   Fit                           Controls (P0..P3), T (each node's t), Points (B(T)), Error (rest distance), Length.
//   Weights(t, weights)           The Bernstein weights (1-t)^3, 3(1-t)^2 t, 3(1-t) t^2, t^3: the sample constraint's source weights.
//   Point(controls, t)            B(t).
//   ExistingFractions(nodes)      Existing spacing: each node's distance along the chain over its length.
//   EvenFractions(count)          Even spacing: i / (count - 1).
//   FitTo(nodes, evenSpacing)     Fits P1/P2 (P0, P3 on the first and last node), then places the nodes at their fractions.
//   SolveHandles(...)             Least squares for P1 and P2 with P0 and P3 fixed.
//   ParametersAt(controls, fractions, out length)  t at each fraction of the curve's length.
