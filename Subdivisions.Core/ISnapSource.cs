using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>
    /// The seam the cursor snapper consumes. A source feeds its snap candidates - edge
    /// endpoints (vertices) and nearest points on edges - into a shared accumulator. How
    /// candidates are collected (ECS lookups, native buffers) stays behind the adapter.
    /// </summary>
    public interface ISnapSource
    {
        void AddVertices(float3 hit, ref SnapAccumulator acc);

        void AddEdges(float3 hit, ref SnapAccumulator acc);
    }
}
