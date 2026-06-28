using Colossal.Mathematics;
using Subdivisions.Core;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Entities;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Production <see cref="IBoundaryGraph"/> adapter: serves the tracer from the game's ECS
    /// lookups via <see cref="RoadNetwork"/>, and classifies edges through <see cref="NetBoundary"/>.
    /// Refresh once per frame after the lookups update.
    /// </summary>
    internal sealed class EcsBoundaryGraph : IBoundaryGraph
    {
        private readonly RoadNetwork _roads;
        private NetBoundary _boundary;

        public EcsBoundaryGraph(RoadNetwork roads)
        {
            _roads = roads;
        }

        public void Refresh()
        {
            _boundary = _roads.BuildBoundary();
        }

        public EdgeEnds GetEndpoints(Entity edge)
        {
            var e = _roads.Edges[edge];
            return new EdgeEnds(e.m_Start, e.m_End);
        }

        public Bezier4x3 GetCurve(Entity edge) => _roads.Curves[edge].m_Bezier;

        public bool IsBoundary(Entity edge) => _boundary.IsBoundary(edge);

        public NetworkKind GetKind(Entity edge) => _boundary.GetKind(edge);

        public int GetNeighborCount(Entity node)
        {
            return _roads.ConnectedEdges.HasBuffer(node) ? _roads.ConnectedEdges[node].Length : 0;
        }

        public Entity GetNeighborAt(Entity node, int index) => _roads.ConnectedEdges[node][index].m_Edge;
    }
}
