using Unity.Collections;
using Unity.Mathematics;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal static class Polygon
    {
        public static float ComputeArea(NativeList<float2> ring)
        {
            var area = 0f;
            var n = ring.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                area += (ring[j].x + ring[i].x) * (ring[j].y - ring[i].y);
            }
            return math.abs(area) * 0.5f;
        }

        /// <summary>True if no two non-adjacent edges of the ring cross.</summary>
        public static bool IsSimple(NativeList<float2> ring)
        {
            var n = ring.Length;
            for (var i = 0; i < n; i++)
            {
                var a1 = ring[i];
                var a2 = ring[(i + 1) % n];
                for (var j = i + 1; j < n; j++)
                {
                    if ((j + 1) % n == i || (i + 1) % n == j)
                    {
                        continue;
                    }
                    if (Geometry.SegmentsCross(a1, a2, ring[j], ring[(j + 1) % n]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Removes consecutive near-duplicate vertices and 180° reversal tips (out-and-back stubs).</summary>
        public static void CollapseSpikes(NativeList<float2> ring, float duplicateEpsilon)
        {
            var changed = true;
            while (changed && ring.Length >= 3)
            {
                changed = false;

                for (var i = 0; i < ring.Length && ring.Length >= 2;)
                {
                    var j = (i + 1) % ring.Length;
                    if (math.distance(ring[i], ring[j]) < duplicateEpsilon)
                    {
                        ring.RemoveAt(j);
                        changed = true;
                    }
                    else
                    {
                        i++;
                    }
                }

                for (var i = 0; i < ring.Length && ring.Length >= 3;)
                {
                    var prev = ring[(i - 1 + ring.Length) % ring.Length];
                    var cur = ring[i];
                    var next = ring[(i + 1) % ring.Length];
                    var d1 = math.normalizesafe(cur - prev);
                    var d2 = math.normalizesafe(next - cur);
                    if (math.dot(d1, d2) < -0.999f)
                    {
                        ring.RemoveAt(i);
                        changed = true;
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        public static void DropCollinear(NativeList<float2> ring, float tolerance)
        {
            for (var i = 0; i < ring.Length && ring.Length > 3;)
            {
                var prev = ring[(i - 1 + ring.Length) % ring.Length];
                var cur = ring[i];
                var next = ring[(i + 1) % ring.Length];
                if (Geometry.MeasurePerpendicularDistance(cur, prev, next) < tolerance)
                {
                    ring.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Splits the ring at the closest pair of non-adjacent vertices nearer than
        /// <paramref name="minWidth"/> and keeps the larger-area loop, repeating until none remain.
        /// Drops thin necks and slivers that tessellate into degenerate geometry.
        /// </summary>
        public static void ResolveNecks(NativeList<float2> ring, float minWidth)
        {
            var minSq = minWidth * minWidth;
            for (var guard = 0; guard < 32 && ring.Length >= 4; guard++)
            {
                var n = ring.Length;
                var bi = -1;
                var bj = -1;
                var best = minSq;
                for (var i = 0; i < n; i++)
                {
                    for (var j = i + 2; j < n; j++)
                    {
                        if (i == 0 && j == n - 1)
                        {
                            continue;
                        }
                        var d = math.distancesq(ring[i], ring[j]);
                        if (d < best)
                        {
                            best = d;
                            bi = i;
                            bj = j;
                        }
                    }
                }
                if (bi < 0)
                {
                    break;
                }

                var loopA = new NativeList<float2>(Allocator.Temp);
                for (var k = bi; k <= bj; k++)
                {
                    loopA.Add(ring[k]);
                }
                var loopB = new NativeList<float2>(Allocator.Temp);
                for (var k = bj; k < n; k++)
                {
                    loopB.Add(ring[k]);
                }
                for (var k = 0; k <= bi; k++)
                {
                    loopB.Add(ring[k]);
                }

                ring.Clear();
                CopyInto(ComputeArea(loopA) >= ComputeArea(loopB) ? loopA : loopB, ring);
                loopA.Dispose();
                loopB.Dispose();
            }
        }

        /// <summary>Drops vertices within <paramref name="minSpacing"/> of the previous kept vertex.</summary>
        public static void EnforceMinSpacing(NativeList<float2> ring, float minSpacing)
        {
            var minSq = minSpacing * minSpacing;
            for (var i = 0; i < ring.Length && ring.Length > 3;)
            {
                var prev = (i - 1 + ring.Length) % ring.Length;
                if (math.distancesq(ring[prev], ring[i]) < minSq)
                {
                    ring.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        public static void CopyInto(NativeList<float2> src, NativeList<float2> dst)
        {
            for (var i = 0; i < src.Length; i++)
            {
                dst.Add(src[i]);
            }
        }
    }
}
