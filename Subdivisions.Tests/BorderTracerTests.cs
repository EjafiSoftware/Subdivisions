using System.Collections.Generic;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class BorderTracerTests
    {
        private static float RingArea(IReadOnlyList<float3> ring)
        {
            var area = 0f;
            var n = ring.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                area += (ring[j].x + ring[i].x) * (ring[j].z - ring[i].z);
            }
            return math.abs(area) * 0.5f;
        }

        private static bool RingContains(IReadOnlyList<float3> ring, float x, float z, float eps)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                if (math.distance(ring[i].xz, new float2(x, z)) < eps)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void Fewer_than_three_points_is_invalid()
        {
            var graph = new BoundaryGraphBuilder().Build();
            var points = new List<SnapPoint> { Pt.Free(0, 0), Pt.Free(10, 0) };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Ring.Count, Is.EqualTo(0));
        }

        [Test]
        public void Free_points_square_is_valid_with_expected_area()
        {
            var graph = new BoundaryGraphBuilder().Build();
            var points = new List<SnapPoint>
            {
                Pt.Free(0, 0), Pt.Free(10, 0), Pt.Free(10, 10), Pt.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.True);
            Assert.That(RingArea(result.Ring), Is.EqualTo(100f).Within(1f));
        }

        [Test]
        public void Bowtie_free_points_is_invalid()
        {
            var graph = new BoundaryGraphBuilder().Build();
            // Order makes the two diagonals of the square cross: self-intersecting.
            var points = new List<SnapPoint>
            {
                Pt.Free(0, 0), Pt.Free(10, 10), Pt.Free(10, 0), Pt.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void Road_loop_three_points_traces_valid_enclosing_ring()
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 20, 0).Node(2, 20, 20).Node(3, 0, 20);
            var e01 = b.Edge(0, 1, NetworkKind.Road);
            var e12 = b.Edge(1, 2, NetworkKind.Road);
            var e23 = b.Edge(2, 3, NetworkKind.Road);
            b.Edge(3, 0, NetworkKind.Road);
            var graph = b.Build();

            var points = new List<SnapPoint>
            {
                graph.PointOn(e01, 0.5f), graph.PointOn(e12, 0.5f), graph.PointOn(e23, 0.5f),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.True);
            Assert.That(RingArea(result.Ring), Is.GreaterThan(100f), "ring should enclose a real block, not a sliver");
        }

        [Test]
        public void Same_kind_neighbors_trace_the_corner()
        {
            var graph = LShape(NetworkKind.Road, NetworkKind.Road, out var e0, out var e1);
            var points = new List<SnapPoint>
            {
                graph.PointOn(e0, 0.5f), graph.PointOn(e1, 0.5f), Pt.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.True);
            Assert.That(RingContains(result.Ring, 10, 0, 1f), Is.True, "same-kind segment should trace through the shared corner");
        }

        [Test]
        public void Different_kind_neighbors_connect_straight_skipping_the_corner()
        {
            var graph = LShape(NetworkKind.Road, NetworkKind.Track, out var e0, out var e1);
            var points = new List<SnapPoint>
            {
                graph.PointOn(e0, 0.5f), graph.PointOn(e1, 0.5f), Pt.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.True);
            Assert.That(RingContains(result.Ring, 10, 0, 1f), Is.False, "cross-kind segment should cut straight, not trace the corner");
        }

        [Test]
        public void Curved_edge_is_tessellated()
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 20, 0);
            var curve = new Colossal.Mathematics.Bezier4x3(
                new float3(0, 0, 0), new float3(5, 0, 10), new float3(15, 0, 10), new float3(20, 0, 0));
            var e = b.Edge(0, 1, curve, NetworkKind.Road);
            var graph = b.Build();

            var points = new List<SnapPoint>
            {
                graph.PointOn(e, 0.1f), graph.PointOn(e, 0.9f), Pt.Free(10, -10),
            };

            var result = new BorderTracer().Trace(points, graph);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Ring.Count, Is.GreaterThan(5), "the bent edge should subdivide into several vertices");
        }

        private static ArrayBoundaryGraph LShape(NetworkKind first, NetworkKind second, out Unity.Entities.Entity e0, out Unity.Entities.Entity e1)
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 10, 0).Node(2, 10, 10);
            e0 = b.Edge(0, 1, first);
            e1 = b.Edge(1, 2, second);
            return b.Build();
        }
    }
}
