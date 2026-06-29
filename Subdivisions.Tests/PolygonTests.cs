using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class PolygonTests
    {
        [Test]
        public void ComputeArea_Square_ReturnsSideSquared()
        {
            var ring = Ring((0, 0), (10, 0), (10, 10), (0, 10));

            Polygon.ComputeArea(ring).Should().BeApproximately(100f, 1e-3f);
        }

        [Test]
        public void ComputeArea_IsWindingIndependent()
        {
            var ccw = Ring((0, 0), (1, 0), (1, 1), (0, 1));
            var cw = Ring((0, 0), (0, 1), (1, 1), (1, 0));

            Polygon.ComputeArea(cw).Should().BeApproximately(Polygon.ComputeArea(ccw), 1e-5f);
            Polygon.ComputeArea(ccw).Should().BeApproximately(1f, 1e-5f);
        }

        [Test]
        public void IsSimple_Square_ReturnsTrue()
        {
            Polygon.IsSimple(Ring((0, 0), (10, 0), (10, 10), (0, 10))).Should().BeTrue();
        }

        [Test]
        public void IsSimple_Bowtie_ReturnsFalse()
        {
            Polygon.IsSimple(Ring((0, 0), (10, 10), (10, 0), (0, 10))).Should().BeFalse();
        }

        [Test]
        public void CollapseSpikes_RemovesConsecutiveDuplicateVertices()
        {
            var ring = Ring((0, 0), (0, 0), (10, 0), (10, 10), (0, 10));

            Polygon.CollapseSpikes(ring, 0.5f);

            ring.Should().HaveCount(4);
        }

        [Test]
        public void CollapseSpikes_RemovesOutAndBackStub()
        {
            // (5,15) is a 180-degree reversal tip off the (5,10) edge.
            var ring = Ring((0, 0), (10, 0), (10, 10), (5, 10), (5, 15), (5, 10), (0, 10));

            Polygon.CollapseSpikes(ring, 0.5f);

            ring.Should().NotContain(p => p.y > 10.5f, "the out-and-back tip should be collapsed");
        }

        [Test]
        public void DropCollinear_RemovesAVertexLyingOnAnEdge()
        {
            // (5,0) sits on the (0,0)-(10,0) edge.
            var ring = Ring((0, 0), (5, 0), (10, 0), (10, 10), (0, 10));

            Polygon.DropCollinear(ring, 0.5f);

            ring.Should().HaveCount(4);
            ring.Should().NotContain(p => math.abs(p.x - 5f) < 1e-3f && math.abs(p.y) < 1e-3f);
        }

        [Test]
        public void ResolveNecks_SplitsAtThePinchAndKeepsTheLargerLobe()
        {
            // Big square (area ~100) joined to a small lobe (area ~2) through a 2-wide neck.
            var ring = Ring(
                (0, 0), (10, 0), (10, 10), (6, 10), (6, 11), (4, 11), (4, 10), (0, 10));

            Polygon.ResolveNecks(ring, 6f);

            ring.Should().HaveCount(6);
            Polygon.ComputeArea(ring).Should().BeApproximately(100f, 1f);
        }

        [Test]
        public void EnforceMinSpacing_DropsVerticesTooCloseToTheirPredecessor()
        {
            var ring = Ring((0, 0), (0.5f, 0), (10, 0), (10, 10), (0, 10));

            Polygon.EnforceMinSpacing(ring, 2f);

            ring.Should().HaveCount(4);
        }

        private static List<float2> Ring(params (float x, float y)[] points) =>
            points.Select(p => new float2(p.x, p.y)).ToList();
    }
}
