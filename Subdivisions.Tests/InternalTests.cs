using System.Collections.Generic;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    /// <summary>
    /// Scalpel tests reaching past <see cref="BorderTracer"/> into internals via
    /// InternalsVisibleTo, for invariants that are hard to localize from a ring assertion.
    /// </summary>
    [TestFixture]
    public class InternalTests
    {
        [Test]
        public void PathFinder_avoids_used_edges_and_takes_the_other_arc()
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

            Assert.That(found1, Is.True);
            Assert.That(edges1.Count, Is.EqualTo(1), "opposite edges of a 4-loop are bridged by one intermediate edge");

            var used = new HashSet<Entity> { edges1[0] };
            var nodes2 = new List<Entity>();
            var edges2 = new List<Entity>();
            var found2 = finder.FindPath(graph, a, target, used, nodes2, edges2);

            Assert.That(found2, Is.True);
            Assert.That(edges2, Does.Not.Contain(edges1[0]), "second path must avoid the claimed edge and take the other arc");
            Assert.That(edges2.Count, Is.EqualTo(1));
        }

        [Test]
        public void Polygon_IsSimple_true_for_square()
        {
            var ring = new List<float2>
            {
                new(0, 0), new(10, 0), new(10, 10), new(0, 10),
            };

            Assert.That(Polygon.IsSimple(ring), Is.True);
        }

        [Test]
        public void Polygon_IsSimple_false_for_bowtie()
        {
            var ring = new List<float2>
            {
                new(0, 0), new(10, 10), new(10, 0), new(0, 10),
            };

            Assert.That(Polygon.IsSimple(ring), Is.False);
        }
    }
}
