using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    internal sealed class AreaIndex
    {
        private readonly SearchSystem _search;

        private BufferLookup<Node> _nodes;
        private ComponentLookup<District> _districts;
        private ComponentLookup<MapTile> _mapTiles;
        private ComponentLookup<Temp> _temps;
        private ComponentLookup<Deleted> _deleted;

        public AreaIndex(
            SearchSystem search,
            BufferLookup<Node> nodes,
            ComponentLookup<District> districts,
            ComponentLookup<MapTile> mapTiles,
            ComponentLookup<Temp> temps,
            ComponentLookup<Deleted> deleted)
        {
            _search = search;
            _nodes = nodes;
            _districts = districts;
            _mapTiles = mapTiles;
            _temps = temps;
            _deleted = deleted;
        }

        public void Refresh(SystemBase system)
        {
            _nodes.Update(system);
            _districts.Update(system);
            _mapTiles.Update(system);
            _temps.Update(system);
            _deleted.Update(system);
        }

        /// <summary>The boundary ring of an area collected by <see cref="CollectAreas"/>.</summary>
        public DynamicBuffer<Node> GetRing(Entity area) => _nodes[area];

        /// <summary>Collects snap-eligible districts and map tiles within <paramref name="range"/> metres of the cursor hit.</summary>
        public NativeList<Entity> CollectAreas(float3 hit, float range)
        {
            var tree = _search.GetSearchTree(true, out var deps);
            deps.Complete();

            var query = new Bounds3(
                new float3(hit.x - range, -10000f, hit.z - range),
                new float3(hit.x + range, 10000f, hit.z + range));
            var areas = new NativeList<Entity>(Allocator.Temp);
            var seen = new NativeHashSet<Entity>(16, Allocator.Temp);
            var collector = new NearbyAreaCollector
            {
                _query = query,
                _areas = areas,
                _seen = seen,
                _districtLookup = _districts,
                _mapTileLookup = _mapTiles,
                _tempLookup = _temps,
                _deletedLookup = _deleted,
            };
            tree.Iterate(ref collector);
            seen.Dispose();
            return areas;
        }
    }
}
