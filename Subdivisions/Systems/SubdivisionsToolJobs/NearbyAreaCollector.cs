using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal struct NearbyAreaCollector : INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>
    {
        public Bounds3 _query;
        public NativeList<Entity> _areas;
        public NativeHashSet<Entity> _seen;
        [ReadOnly] public ComponentLookup<District> _districtLookup;
        [ReadOnly] public ComponentLookup<MapTile> _mapTileLookup;
        [ReadOnly] public ComponentLookup<Temp> _tempLookup;
        [ReadOnly] public ComponentLookup<Deleted> _deletedLookup;

        public bool Intersect(QuadTreeBoundsXZ bounds)
        {
            return Overlaps(_query, bounds.m_Bounds);
        }

        public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem item)
        {
            if (!Overlaps(_query, bounds.m_Bounds))
            {
                return;
            }

            var area = item.m_Area;
            if (_deletedLookup.HasComponent(area) || _tempLookup.HasComponent(area))
            {
                return;
            }
            if (!_districtLookup.HasComponent(area) && !_mapTileLookup.HasComponent(area))
            {
                return;
            }

            if (!_seen.Add(area))
            {
                return;
            }
            _areas.Add(area);
        }

        private static bool Overlaps(Bounds3 a, Bounds3 b)
        {
            return a.min.x <= b.max.x && a.max.x >= b.min.x
                && a.min.z <= b.max.z && a.max.z >= b.min.z;
        }
    }
}
