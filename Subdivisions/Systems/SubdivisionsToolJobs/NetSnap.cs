using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal struct SnapPoint
    {
        public float3 _position;
        public Entity _edge;
        public float _t;

        /// <summary>Snapped onto an existing district boundary or map-tile border (a free point, but not arbitrary terrain).</summary>
        public bool _onArea;

        public bool OnNet => _edge != Entity.Null;
    }

    internal static class NetSnap
    {
        /// <summary>Curve parameter of the point on <paramref name="bez"/> nearest <paramref name="p"/> (xz plane).</summary>
        public static float FindNearestT(Bezier4x3 bez, float2 p)
        {
            const int samples = 16;
            var bestT = 0f;
            var bestD = float.MaxValue;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var d = math.distancesq(MathUtils.Position(bez, t).xz, p);
                if (d < bestD)
                {
                    bestD = d;
                    bestT = t;
                }
            }

            var step = 1f / samples;
            for (var it = 0; it < 4; it++)
            {
                step *= 0.5f;
                var tl = math.max(0f, bestT - step);
                var tr = math.min(1f, bestT + step);
                var dl = math.distancesq(MathUtils.Position(bez, tl).xz, p);
                var dr = math.distancesq(MathUtils.Position(bez, tr).xz, p);
                if (dl < bestD)
                {
                    bestD = dl;
                    bestT = tl;
                }
                if (dr < bestD)
                {
                    bestD = dr;
                    bestT = tr;
                }
            }
            return bestT;
        }

        /// <summary>Approximate length of the whole curve (xz), by uniform sampling.</summary>
        public static float MeasureCurve(Bezier4x3 bez)
        {
            return MeasureSubCurve(bez, 0f, 1f);
        }

        /// <summary>Approximate length of the curve between two parameters (xz), by uniform sampling.</summary>
        public static float MeasureSubCurve(Bezier4x3 bez, float t0, float t1)
        {
            const int samples = 8;
            var length = 0f;
            var prev = MathUtils.Position(bez, t0).xz;
            for (var k = 1; k <= samples; k++)
            {
                var t = math.lerp(t0, t1, k / (float)samples);
                var cur = MathUtils.Position(bez, t).xz;
                length += math.distance(prev, cur);
                prev = cur;
            }
            return length;
        }
    }
}
