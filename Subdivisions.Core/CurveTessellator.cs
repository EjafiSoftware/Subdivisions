using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>
    /// Flattens a bezier sub-curve into ring vertices by adaptive recursive subdivision,
    /// stopping once the midpoint deviates from the chord by less than the tolerance.
    /// Appends the end point of each accepted span; the caller seeds the start point.
    /// </summary>
    internal static class CurveTessellator
    {
        private const float DeviationTol = 1.0f;
        private const int MaxDepth = 7;

        public static void EmitRange(Bezier4x3 bez, float t0, float t1, List<float2> ring)
        {
            Emit(bez, t0, t1, ring, 0);
        }

        private static void Emit(Bezier4x3 bez, float t0, float t1, List<float2> ring, int depth)
        {
            var p0 = MathUtils.Position(bez, t0).xz;
            var p1 = MathUtils.Position(bez, t1).xz;
            var tm = (t0 + t1) * 0.5f;
            var pm = MathUtils.Position(bez, tm).xz;

            if (depth >= MaxDepth || MathUtils.Distance(new Line2(p0, p1), pm, out _) < DeviationTol)
            {
                ring.Add(p1);
                return;
            }
            Emit(bez, t0, tm, ring, depth + 1);
            Emit(bez, tm, t1, ring, depth + 1);
        }
    }
}
