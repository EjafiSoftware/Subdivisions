using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    internal sealed class RoadNetwork
    {
        private readonly SearchSystem _search;

        private ComponentLookup<Edge> _edges;
        private ComponentLookup<Curve> _curves;
        private BufferLookup<ConnectedEdge> _connectedEdges;
        private ComponentLookup<Owner> _owners;
        private ComponentLookup<Deleted> _deleted;
        private ComponentLookup<Composition> _compositions;
        private ComponentLookup<NetCompositionData> _compositionData;

        public RoadNetwork(
            SearchSystem search,
            ComponentLookup<Edge> edges,
            ComponentLookup<Curve> curves,
            BufferLookup<ConnectedEdge> connectedEdges,
            ComponentLookup<Owner> owners,
            ComponentLookup<Deleted> deleted,
            ComponentLookup<Composition> compositions,
            ComponentLookup<NetCompositionData> compositionData)
        {
            _search = search;
            _edges = edges;
            _curves = curves;
            _connectedEdges = connectedEdges;
            _owners = owners;
            _deleted = deleted;
            _compositions = compositions;
            _compositionData = compositionData;
        }

        public ComponentLookup<Edge> Edges => _edges;
        public ComponentLookup<Curve> Curves => _curves;
        public BufferLookup<ConnectedEdge> ConnectedEdges => _connectedEdges;

        public void Refresh(SystemBase system)
        {
            _edges.Update(system);
            _curves.Update(system);
            _connectedEdges.Update(system);
            _owners.Update(system);
            _deleted.Update(system);
            _compositions.Update(system);
            _compositionData.Update(system);
        }

        public NetBoundary BuildBoundary()
        {
            return new NetBoundary
            {
                _edgeLookup = _edges,
                _curveLookup = _curves,
                _ownerLookup = _owners,
                _deletedLookup = _deleted,
                _compositionLookup = _compositions,
                _compositionDataLookup = _compositionData,
            };
        }

        /// <summary>Collects boundary-qualified edges within <paramref name="range"/> metres of the cursor hit.</summary>
        public NativeList<Entity> CollectBoundaryEdges(float3 hit, float range)
        {
            var tree = _search.GetNetSearchTree(true, out var deps);
            deps.Complete();

            var query = new Bounds3(
                new float3(hit.x - range, -10000f, hit.z - range),
                new float3(hit.x + range, 10000f, hit.z + range));
            var edges = new NativeList<Entity>(Allocator.Temp);
            var collector = new NearbyNetEdgeCollector
            {
                _query = query,
                _boundary = BuildBoundary(),
                _edges = edges,
            };
            tree.Iterate(ref collector);
            return edges;
        }
    }
}
