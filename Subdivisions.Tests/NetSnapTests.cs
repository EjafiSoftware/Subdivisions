using AwesomeAssertions;
using Colossal.Mathematics;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class NetSnapTests
    {
        // Controls at 1/3 and 2/3 make the cubic reduce to the straight chord, so lengths are exact.
        private static Bezier4x3 Straight(float3 a, float3 b) =>
            new Bezier4x3(a, math.lerp(a, b, 1f / 3f), math.lerp(a, b, 2f / 3f), b);

        [Test]
        public void MeasureCurve_StraightBezier_ReturnsChordLength()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.MeasureCurve(bez).Should().BeApproximately(10f, 1e-3f);
        }

        [Test]
        public void MeasureSubCurve_HalfSpans_SplitTheLength()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.MeasureSubCurve(bez, 0f, 0.5f).Should().BeApproximately(5f, 1e-3f);
            NetSnap.MeasureSubCurve(bez, 0.5f, 1f).Should().BeApproximately(5f, 1e-3f);
        }

        [Test]
        public void MeasureSubCurve_EmptySpan_IsZero()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.MeasureSubCurve(bez, 0.3f, 0.3f).Should().BeApproximately(0f, 1e-5f);
        }

        [Test]
        public void FindNearestT_PointOppositeTheMidpoint_ReturnsHalf()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.FindNearestT(bez, new float2(5f, 7f)).Should().BeApproximately(0.5f, 0.02f);
        }

        [Test]
        public void FindNearestT_PointBeforeTheStart_ClampsToZero()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.FindNearestT(bez, new float2(-5f, 0f)).Should().BeApproximately(0f, 1e-3f);
        }

        [Test]
        public void FindNearestT_PointAfterTheEnd_ClampsToOne()
        {
            var bez = Straight(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));

            NetSnap.FindNearestT(bez, new float2(15f, 0f)).Should().BeApproximately(1f, 1e-3f);
        }
    }
}
