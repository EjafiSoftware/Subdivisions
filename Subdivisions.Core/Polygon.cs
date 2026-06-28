using System.Collections.Generic;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    internal static class Polygon
    {
        public static float ComputeArea(List<float2> ring)
        {
            var area = 0f;
            var n = ring.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                area += (ring[j].x + ring[i].x) * (ring[j].y - ring[i].y);
            }
            return math.abs(area) * 0.5f;
        }

        /// <summary>True if no two non-adjacent edges of the ring cross.</summary>
        public static bool IsSimple(List<float2> ring)
        {
            var n = ring.Count;
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
        public static void CollapseSpikes(List<float2> ring, float duplicateEpsilon)
        {
            var changed = true;
            while (changed && ring.Count >= 3)
            {
                changed = false;

                for (var i = 0; i < ring.Count && ring.Count >= 2;)
                {
                    var j = (i + 1) % ring.Count;
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

                for (var i = 0; i < ring.Count && ring.Count >= 3;)
                {
                    var prev = ring[(i - 1 + ring.Count) % ring.Count];
                    var cur = ring[i];
                    var next = ring[(i + 1) % ring.Count];
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

        public static void DropCollinear(List<float2> ring, float tolerance)
        {
            for (var i = 0; i < ring.Count && ring.Count > 3;)
            {
                var prev = ring[(i - 1 + ring.Count) % ring.Count];
                var cur = ring[i];
                var next = ring[(i + 1) % ring.Count];
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
        public static void ResolveNecks(List<float2> ring, float minWidth)
        {
            var minSq = minWidth * minWidth;
            for (var guard = 0; guard < 32 && ring.Count >= 4; guard++)
            {
                var n = ring.Count;
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

                var loopA = new List<float2>();
                for (var k = bi; k <= bj; k++)
                {
                    loopA.Add(ring[k]);
                }
                var loopB = new List<float2>();
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
            }
        }

        /// <summary>Drops vertices within <paramref name="minSpacing"/> of the previous kept vertex.</summary>
        public static void EnforceMinSpacing(List<float2> ring, float minSpacing)
        {
            var minSq = minSpacing * minSpacing;
            for (var i = 0; i < ring.Count && ring.Count > 3;)
            {
                var prev = (i - 1 + ring.Count) % ring.Count;
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

        public static void CopyInto(List<float2> src, List<float2> dst)
        {
            for (var i = 0; i < src.Count; i++)
            {
                dst.Add(src[i]);
            }
        }
    }
}
