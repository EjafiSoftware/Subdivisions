using Subdivisions.Domain;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    /// <summary>
    /// Turns the polygon produced by <see cref="BuildBorderJob"/> into a district
    /// creation definition the game will tessellate and commit.
    /// </summary>
    [BurstCompile]
    public struct CreateDistrictJob : IJob
    {
        [ReadOnly] public NativeList<float3> _points;
        public Entity _prefab;
        public EntityCommandBuffer _ecb;

        public void Execute()
        {
            if (_points.Length < 3)
            {
                return;
            }

            var entity = _ecb.CreateEntity();
            AreaDefinitionCreation.AsDynamicBufferNodes(_ecb, entity, _points);
            AreaDefinitionCreation.WithCreationDefinition(_ecb, entity, _prefab);
        }
    }
}
