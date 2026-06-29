using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Subdivisions.Tests.Fakes;
using Unity.Mathematics;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class SnapResolverTests
    {
        private static SnapSettings Settings =>
            new SnapSettings(nodeSnapDistance: 14f, snapDistance: 20f, coincidenceRadius: 0.5f, preferred: SnapPreference.Net);

        private static SnapQuery Query(float x, float z, SnapPoint? previous = null) =>
            new SnapQuery(new float3(x, 0f, z), previous, Settings);

        [Test]
        public void Resolve_NoCandidates_ReturnsFreeHit()
        {
            var sources = new ISnapSource[] { new FakeSnapSource() };

            var result = SnapResolver.Resolve(sources, Query(5f, 7f));

            result.OnNet.Should().BeFalse();
            result.OnArea.Should().BeFalse();
            result.Position.Should().Be(new float3(5f, 0f, 7f));
        }

        [Test]
        public void Resolve_SingleEdgeCandidateWithinRange_SnapsToIt()
        {
            var sources = new ISnapSource[] { new FakeSnapSource().Edge(Pt.Net(5f, 5f)) };

            var result = SnapResolver.Resolve(sources, Query(8f, 5f));

            result.OnNet.Should().BeTrue();
            result.Position.Should().Be(new float3(5f, 0f, 5f));
        }

        [Test]
        public void Resolve_CandidateBeyondSnapDistance_ReturnsFreeHit()
        {
            var sources = new ISnapSource[] { new FakeSnapSource().Edge(Pt.Net(100f, 100f)) };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f));

            result.OnNet.Should().BeFalse();
            result.Position.Should().Be(new float3(0f, 0f, 0f));
        }

        [Test]
        public void Resolve_CoincidentNetAndArea_PreviousOnArea_PicksArea()
        {
            var sources = new ISnapSource[]
            {
                new FakeSnapSource().Vertex(Pt.Net(0f, 0f)).Vertex(Pt.Area(0.2f, 0f))
            };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f, previous: Pt.Area(50f, 50f)));

            result.OnArea.Should().BeTrue();
            result.OnNet.Should().BeFalse();
        }

        [Test]
        public void Resolve_CoincidentNetAndArea_PreviousOnNet_PicksNet()
        {
            var sources = new ISnapSource[]
            {
                new FakeSnapSource().Vertex(Pt.Area(0f, 0f)).Vertex(Pt.Net(0.2f, 0f))
            };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f, previous: Pt.Net(50f, 50f)));

            result.OnNet.Should().BeTrue();
        }

        [Test]
        public void Resolve_PreferredKindBeyondCoincidence_NearestWins()
        {
            var sources = new ISnapSource[]
            {
                new FakeSnapSource().Vertex(Pt.Net(0f, 0f)).Vertex(Pt.Area(5f, 0f))
            };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f, previous: Pt.Area(50f, 50f)));

            result.OnNet.Should().BeTrue();
        }

        [Test]
        public void Resolve_FirstPoint_CoincidentNetAndArea_DefaultsToNet()
        {
            var sources = new ISnapSource[]
            {
                new FakeSnapSource().Vertex(Pt.Area(0f, 0f)).Vertex(Pt.Net(0.2f, 0f))
            };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f, previous: null));

            result.OnNet.Should().BeTrue();
        }

        [Test]
        public void Resolve_VertexWithinNodeRange_BeatsCloserEdge()
        {
            var sources = new ISnapSource[]
            {
                new FakeSnapSource().Vertex(Pt.Net(10f, 0f)).Edge(Pt.Net(3f, 0f))
            };

            var result = SnapResolver.Resolve(sources, Query(0f, 0f));

            result.Position.Should().Be(new float3(10f, 0f, 0f));
        }

        [Test]
        public void Resolve_CoincidentTie_IsIndependentOfSourceOrder()
        {
            var net = new FakeSnapSource().Vertex(Pt.Net(0.2f, 0f));
            var area = new FakeSnapSource().Vertex(Pt.Area(0f, 0f));
            var query = Query(0f, 0f, previous: Pt.Area(50f, 50f));

            var netFirst = SnapResolver.Resolve(new ISnapSource[] { net, area }, query);
            var areaFirst = SnapResolver.Resolve(new ISnapSource[] { area, net }, query);

            netFirst.OnArea.Should().BeTrue();
            areaFirst.OnArea.Should().BeTrue();
        }
    }
}
