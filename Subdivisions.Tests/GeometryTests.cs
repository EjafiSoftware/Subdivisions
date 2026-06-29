using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class GeometryTests
    {
        [Test]
        public void SegmentsCross_ProperlyCrossingSegments_ReturnsTrue()
        {
            Geometry.SegmentsCross(
                new float2(-5f, 0f), new float2(5f, 0f),
                new float2(0f, -5f), new float2(0f, 5f)).Should().BeTrue();
        }

        [Test]
        public void SegmentsCross_DisjointSegments_ReturnsFalse()
        {
            Geometry.SegmentsCross(
                new float2(0f, 0f), new float2(1f, 0f),
                new float2(0f, 5f), new float2(1f, 5f)).Should().BeFalse();
        }

        [Test]
        public void SegmentsCross_SharingAnEndpoint_ReturnsFalse()
        {
            // Strict proper-crossing semantics: a shared endpoint is not a crossing.
            Geometry.SegmentsCross(
                new float2(0f, 0f), new float2(5f, 0f),
                new float2(0f, 0f), new float2(0f, 5f)).Should().BeFalse();
        }

        [Test]
        public void MeasurePerpendicularDistance_PointOffTheLine_ReturnsThePerpendicularGap()
        {
            Geometry.MeasurePerpendicularDistance(new float2(0f, 5f), new float2(-10f, 0f), new float2(10f, 0f))
                .Should().BeApproximately(5f, 1e-4f);
        }

        [Test]
        public void MeasurePerpendicularDistance_DegenerateSegment_FallsBackToPointDistance()
        {
            Geometry.MeasurePerpendicularDistance(new float2(3f, 4f), new float2(0f, 0f), new float2(0f, 0f))
                .Should().BeApproximately(5f, 1e-4f);
        }

        [Test]
        public void ProjectOntoSegment_PointAboveTheMiddle_ReturnsTheFootAndItsGap()
        {
            var foot = Geometry.ProjectOntoSegment(
                new float3(0f, 0f, 0f), new float3(10f, 0f, 0f), new float2(5f, 4f), out var distance);

            foot.x.Should().BeApproximately(5f, 1e-4f);
            foot.z.Should().BeApproximately(0f, 1e-4f);
            distance.Should().BeApproximately(4f, 1e-4f);
        }

        [Test]
        public void ProjectOntoSegment_PointBeyondTheEnd_ClampsToTheEndpoint()
        {
            var foot = Geometry.ProjectOntoSegment(
                new float3(0f, 0f, 0f), new float3(10f, 0f, 0f), new float2(20f, 0f), out var distance);

            foot.x.Should().BeApproximately(10f, 1e-4f);
            distance.Should().BeApproximately(10f, 1e-4f);
        }
    }
}
