using Colossal.Mathematics;
using Subdivisions.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Snap candidates from boundary-qualified network edges: edge endpoints (vertices) and the
    /// nearest point on each edge curve. Collected once per cursor query, then released.
    /// </summary>
    internal sealed class NetSnapSource : ISnapSource
    {
        private readonly RoadNetwork _roads;
        private NativeList<Entity> _edges;

        public NetSnapSource(RoadNetwork roads)
        {
            _roads = roads;
        }

        public void Collect(float3 hit, float range)
        {
            _edges = _roads.CollectBoundaryEdges(hit, range);
        }

        public void Release()
        {
            if (_edges.IsCreated)
            {
                _edges.Dispose();
            }
        }

        public void AddVertices(float3 hit, ref SnapAccumulator accumulator)
        {
            var curves = _roads.Curves;
            foreach (var edge in _edges)
            {
                var bezier = curves[edge].m_Bezier;
                for (var end = 0; end < 2; end++)
                {
                    var t = end == 0 ? 0f : 1f;
                    var position = MathUtils.Position(bezier, t);
                    accumulator.Consider(
                        new SnapPoint { Position = position, Edge = edge, CurveParameter = t },
                        math.distance(position.xz, hit.xz));
                }
            }
        }

        public void AddEdges(float3 hit, ref SnapAccumulator accumulator)
        {
            var curves = _roads.Curves;
            foreach (var edge in _edges)
            {
                var bezier = curves[edge].m_Bezier;
                var t = NetSnap.FindNearestT(bezier, hit.xz);
                var position = MathUtils.Position(bezier, t);
                accumulator.Consider(
                    new SnapPoint { Position = position, Edge = edge, CurveParameter = t },
                    math.distance(position.xz, hit.xz));
            }
        }
    }
}
