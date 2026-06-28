using Unity.Mathematics;

namespace Subdivisions.Core
{
    public static class Geometry
    {
        public static float Cross(float2 a, float2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        public static bool SegmentsCross(float2 a1, float2 a2, float2 b1, float2 b2)
        {
            var d1 = Cross(b2 - b1, a1 - b1);
            var d2 = Cross(b2 - b1, a2 - b1);
            var d3 = Cross(a2 - a1, b1 - a1);
            var d4 = Cross(a2 - a1, b2 - a1);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        public static float MeasurePerpendicularDistance(float2 p, float2 a, float2 b)
        {
            var ab = b - a;
            var len = math.length(ab);
            if (len < 1e-5f)
            {
                return math.length(p - a);
            }
            var cross = (p.x - a.x) * ab.y - (p.y - a.y) * ab.x;
            return math.abs(cross) / len;
        }

        /// <summary>Closest point on segment a-b to <paramref name="p"/> (xz plane); <paramref name="distance"/> is the gap.</summary>
        public static float3 ProjectOntoSegment(float3 a, float3 b, float2 p, out float distance)
        {
            var ab = b.xz - a.xz;
            var lengthSq = math.lengthsq(ab);
            var t = lengthSq > 1e-6f ? math.saturate(math.dot(p - a.xz, ab) / lengthSq) : 0f;
            var position = math.lerp(a, b, t);
            distance = math.distance(position.xz, p);
            return position;
        }
    }
}
