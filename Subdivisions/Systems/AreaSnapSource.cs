using Colossal.Mathematics;
using Subdivisions.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Snap candidates from existing area borders (districts, map tiles): ring vertices and the
    /// nearest point on each ring segment. Collected once per cursor query, then released.
    /// </summary>
    internal sealed class AreaSnapSource
    {
        private readonly AreaIndex _areas;
        private NativeList<Entity> _collected;

        public AreaSnapSource(AreaIndex areas)
        {
            _areas = areas;
        }

        public void Collect(float3 hit, float range)
        {
            _collected = _areas.CollectAreas(hit, range);
        }

        public void Release()
        {
            if (_collected.IsCreated)
            {
                _collected.Dispose();
            }
        }

        public void AddVertices(float3 hit, ref SnapAccumulator accumulator)
        {
            foreach (var area in _collected)
            {
                var ring = _areas.GetRing(area);
                for (var n = 0; n < ring.Length; n++)
                {
                    var position = ring[n].m_Position;
                    accumulator.Consider(
                        new SnapPoint { _position = position, _edge = Entity.Null, _t = 0f, _onArea = true },
                        math.distance(position.xz, hit.xz));
                }
            }
        }

        public void AddEdges(float3 hit, ref SnapAccumulator accumulator)
        {
            foreach (var area in _collected)
            {
                var ring = _areas.GetRing(area);
                for (var n = 0; n < ring.Length; n++)
                {
                    var a = ring[n].m_Position;
                    var b = ring[(n + 1) % ring.Length].m_Position;
                    var distance = MathUtils.Distance(new Line2.Segment(a.xz, b.xz), hit.xz, out var t);
                    var position = math.lerp(a, b, t);
                    accumulator.Consider(
                        new SnapPoint { _position = position, _edge = Entity.Null, _t = 0f, _onArea = true },
                        distance);
                }
            }
        }
    }
}
