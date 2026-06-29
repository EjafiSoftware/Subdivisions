using System.Collections.Generic;
using AutoBogus;
using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Subdivisions.Tests.Fakes;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class BorderTracerTests
    {
        private static ArrayBoundaryGraph EmptyGraph() => new BoundaryGraphBuilder().Build();

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
        public void Trace_FewerThanThreePoints_IsInvalid()
        {
            var points = new List<SnapPoint> { SnapPoints.Free(0, 0), SnapPoints.Free(10, 0) };

            var result = new BorderTracer().Trace(points, EmptyGraph());

            result.IsValid.Should().BeFalse();
            result.Ring.Should().BeEmpty();
        }

        [Test]
        public void Trace_FreePointSquare_IsValidWithAreaMatchingItsSize()
        {
            var spec = new AutoFaker<SquareSpec>()
                .RuleFor(s => s.OriginX, f => f.Random.Float(-200f, 200f))
                .RuleFor(s => s.OriginZ, f => f.Random.Float(-200f, 200f))
                .RuleFor(s => s.Size, f => f.Random.Float(10f, 80f))
                .Generate();

            var points = new List<SnapPoint>
            {
                SnapPoints.Free(spec.OriginX, spec.OriginZ),
                SnapPoints.Free(spec.OriginX + spec.Size, spec.OriginZ),
                SnapPoints.Free(spec.OriginX + spec.Size, spec.OriginZ + spec.Size),
                SnapPoints.Free(spec.OriginX, spec.OriginZ + spec.Size),
            };

            var result = new BorderTracer().Trace(points, EmptyGraph());

            result.IsValid.Should().BeTrue();
            RingArea(result.Ring).Should().BeApproximately(spec.Size * spec.Size, 1f);
        }

        [Test]
        public void Trace_BowtieFreePoints_IsInvalid()
        {
            // Order makes the two diagonals of the square cross: self-intersecting.
            var points = new List<SnapPoint>
            {
                SnapPoints.Free(0, 0), SnapPoints.Free(10, 10), SnapPoints.Free(10, 0), SnapPoints.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, EmptyGraph());

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void Trace_RoadLoopThreePoints_ProducesValidEnclosingRing()
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

            result.IsValid.Should().BeTrue();
            RingArea(result.Ring).Should().BeGreaterThan(100f, "ring should enclose a real block, not a sliver");
        }

        [Test]
        public void Trace_SameKindNeighbors_TracesTheCorner()
        {
            var graph = LShape(NetworkKind.Road, NetworkKind.Road, out var e0, out var e1);
            var points = new List<SnapPoint>
            {
                graph.PointOn(e0, 0.5f), graph.PointOn(e1, 0.5f), SnapPoints.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            result.IsValid.Should().BeTrue();
            RingContains(result.Ring, 10, 0, 1f).Should().BeTrue("same-kind segment should trace through the shared corner");
        }

        [Test]
        public void Trace_DifferentKindNeighbors_ConnectsStraightSkippingCorner()
        {
            var graph = LShape(NetworkKind.Road, NetworkKind.Track, out var e0, out var e1);
            var points = new List<SnapPoint>
            {
                graph.PointOn(e0, 0.5f), graph.PointOn(e1, 0.5f), SnapPoints.Free(0, 10),
            };

            var result = new BorderTracer().Trace(points, graph);

            result.IsValid.Should().BeTrue();
            RingContains(result.Ring, 10, 0, 1f).Should().BeFalse("cross-kind segment should cut straight, not trace the corner");
        }

        [Test]
        public void Trace_CurvedEdge_IsTessellated()
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 20, 0);
            var curve = new Colossal.Mathematics.Bezier4x3(
                new float3(0, 0, 0), new float3(5, 0, 10), new float3(15, 0, 10), new float3(20, 0, 0));
            var e = b.Edge(0, 1, curve, NetworkKind.Road);
            var graph = b.Build();

            var points = new List<SnapPoint>
            {
                graph.PointOn(e, 0.1f), graph.PointOn(e, 0.9f), SnapPoints.Free(10, -10),
            };

            var result = new BorderTracer().Trace(points, graph);

            result.IsValid.Should().BeTrue();
            result.Ring.Count.Should().BeGreaterThan(5, "the bent edge should subdivide into several vertices");
        }

        private static ArrayBoundaryGraph LShape(NetworkKind first, NetworkKind second, out Entity e0, out Entity e1)
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 10, 0).Node(2, 10, 10);
            e0 = b.Edge(0, 1, first);
            e1 = b.Edge(1, 2, second);
            return b.Build();
        }

        public class SquareSpec
        {
            public float OriginX { get; set; }
            public float OriginZ { get; set; }
            public float Size { get; set; }
        }
    }
}
