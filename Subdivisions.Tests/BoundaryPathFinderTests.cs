using System.Collections.Generic;
using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Subdivisions.Tests.Fakes;
using Unity.Entities;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class BoundaryPathFinderTests
    {
        [Test]
        public void FindPath_WithUsedEdges_TakesTheOtherArc()
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 10, 0).Node(2, 10, 10).Node(3, 0, 10);
            var e0 = b.Edge(0, 1, NetworkKind.Road);
            b.Edge(1, 2, NetworkKind.Road);
            var e2 = b.Edge(2, 3, NetworkKind.Road);
            b.Edge(3, 0, NetworkKind.Road);
            var graph = b.Build();

            var a = graph.PointOn(e0, 0.5f);
            var target = graph.PointOn(e2, 0.5f);

            var finder = new BoundaryPathFinder();

            var nodes1 = new List<Entity>();
            var edges1 = new List<Entity>();
            var found1 = finder.FindPath(graph, a, target, new HashSet<Entity>(), nodes1, edges1);

            found1.Should().BeTrue();
            edges1.Should().HaveCount(1, "opposite edges of a 4-loop are bridged by one intermediate edge");

            var used = new HashSet<Entity> { edges1[0] };
            var nodes2 = new List<Entity>();
            var edges2 = new List<Entity>();
            var found2 = finder.FindPath(graph, a, target, used, nodes2, edges2);

            found2.Should().BeTrue();
            edges2.Should().NotContain(edges1[0], "second path must avoid the claimed edge and take the other arc");
            edges2.Should().HaveCount(1);
        }

        [Test]
        public void FindPath_WhenEveryRouteIsClaimed_ReturnsFalse()
        {
            var b = new BoundaryGraphBuilder();
            b.Node(0, 0, 0).Node(1, 10, 0).Node(2, 10, 10).Node(3, 0, 10);
            var e0 = b.Edge(0, 1, NetworkKind.Road);
            var e1 = b.Edge(1, 2, NetworkKind.Road);
            var e2 = b.Edge(2, 3, NetworkKind.Road);
            var e3 = b.Edge(3, 0, NetworkKind.Road);
            var graph = b.Build();

            var used = new HashSet<Entity> { e1, e3 };
            var found = finder().FindPath(graph, graph.PointOn(e0, 0.5f), graph.PointOn(e2, 0.5f), used,
                new List<Entity>(), new List<Entity>());

            found.Should().BeFalse("both arcs between the opposite edges are claimed");
        }

        private static BoundaryPathFinder finder() => new();
    }
}
