using Game.Prefabs;
using Game.Tools;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    internal sealed class DistrictBuilder
    {
        private readonly ToolOutputBarrier _barrier;
        private readonly PrefabSystem _prefabs;
        private readonly RoadNetwork _roads;

        public DistrictBuilder(ToolOutputBarrier barrier, PrefabSystem prefabs, RoadNetwork roads)
        {
            _barrier = barrier;
            _prefabs = prefabs;
            _roads = roads;
        }

        /// <summary>
        /// Traces the road-following ring for the given control points into <paramref name="ring"/>
        /// and reports through <paramref name="valid"/> whether it is a usable closed polygon.
        /// Does not create anything; used for the live preview and to gate apply.
        /// </summary>
        public JobHandle BuildRing(
            NativeList<SnapPoint> points,
            NativeList<float3> ring,
            NativeReference<bool> valid,
            JobHandle dependencies)
        {
            var buildJob = new BuildBorderJob
            {
                _points = points,
                _edgeLookup = _roads.Edges,
                _curveLookup = _roads.Curves,
                _connectedEdges = _roads.ConnectedEdges,
                _boundary = _roads.BuildBoundary(),
                _result = ring,
                _valid = valid,
            };
            return buildJob.Schedule(dependencies);
        }

        /// <summary>Emits the district creation definition for an already-built ring.</summary>
        public JobHandle CreateDistrict(NativeList<float3> ring, DistrictPrefab prefab, JobHandle dependencies)
        {
            var createJob = new CreateDistrictJob
            {
                _points = ring,
                _prefab = _prefabs.GetEntity(prefab),
                _ecb = _barrier.CreateCommandBuffer(),
            };
            var createHandle = createJob.Schedule(dependencies);
            _barrier.AddJobHandleForProducer(createHandle);
            return createHandle;
        }
    }
}
