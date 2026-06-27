using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Resolves a raycast hit to a <see cref="SnapPoint"/>. Snaps to the nearest network or area
    /// vertex first (within <see cref="NodeSnapDistance"/>), else projects onto the nearest network
    /// curve or area segment (within <see cref="SnapDistance"/>), else returns the free hit.
    /// </summary>
    internal sealed class CursorSnapper
    {
        private const float SnapDistance = 20f;
        private const float NodeSnapDistance = 14f;

        private readonly NetSnapSource _net;
        private readonly AreaSnapSource _areas;

        public CursorSnapper(RoadNetwork roads, AreaIndex areas)
        {
            _net = new NetSnapSource(roads);
            _areas = new AreaSnapSource(areas);
        }

        public SnapPoint Snap(float3 hit)
        {
            var free = new SnapPoint { _position = hit, _edge = Entity.Null, _t = 0f };

            _net.Collect(hit, SnapDistance);
            _areas.Collect(hit, SnapDistance);

            var vertices = new SnapAccumulator(NodeSnapDistance, free);
            _net.AddVertices(hit, ref vertices);
            _areas.AddVertices(hit, ref vertices);
            if (vertices.Found)
            {
                _net.Release();
                _areas.Release();
                return vertices.Result;
            }

            var edges = new SnapAccumulator(SnapDistance, free);
            _net.AddEdges(hit, ref edges);
            _areas.AddEdges(hit, ref edges);

            _net.Release();
            _areas.Release();
            return edges.Result;
        }
    }
}
