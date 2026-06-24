using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Unity.Collections;
using Unity.Entities;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal struct NearbyNetEdgeCollector : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
    {
        public Bounds3 _query;
        public NetBoundary _boundary;
        public NativeList<Entity> _edges;

        public bool Intersect(QuadTreeBoundsXZ bounds)
        {
            return Overlaps(_query, bounds.m_Bounds);
        }

        public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
        {
            if (!Overlaps(_query, bounds.m_Bounds) || !_boundary.IsBoundary(item))
            {
                return;
            }
            _edges.Add(item);
        }

        private static bool Overlaps(Bounds3 a, Bounds3 b)
        {
            return a.min.x <= b.max.x && a.max.x >= b.min.x
                && a.min.z <= b.max.z && a.max.z >= b.min.z;
        }
    }
}
