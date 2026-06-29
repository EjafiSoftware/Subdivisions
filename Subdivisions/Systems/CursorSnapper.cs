using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Resolves a raycast hit to a <see cref="SnapPoint"/>. Owns the snap sources and their native
    /// collection lifetime; the priority rules (vertices over edges, nearest within each, coincident
    /// pairs broken by continuity with the previous point) live in <see cref="SnapResolver"/>.
    /// </summary>
    internal sealed class CursorSnapper
    {
        private const float SnapDistance = 20f;
        private const float NodeSnapDistance = 14f;
        private const float CoincidenceRadius = 0.5f;

        private readonly NetSnapSource _net;
        private readonly AreaSnapSource _areas;
        private readonly ISnapSource[] _sources;
        private readonly SnapSettings _settings;

        public CursorSnapper(RoadNetwork roads, AreaIndex areas)
        {
            _net = new NetSnapSource(roads);
            _areas = new AreaSnapSource(areas);
            _sources = new ISnapSource[] { _net, _areas };
            _settings = new SnapSettings(NodeSnapDistance, SnapDistance, CoincidenceRadius, SnapPreference.Net);
        }

        public SnapPoint Snap(float3 hit, SnapPoint? previous)
        {
            _net.Collect(hit, SnapDistance);
            _areas.Collect(hit, SnapDistance);
            try
            {
                return SnapResolver.Resolve(_sources, new SnapQuery(hit, previous, _settings));
            }
            finally
            {
                _net.Release();
                _areas.Release();
            }
        }
    }
}
