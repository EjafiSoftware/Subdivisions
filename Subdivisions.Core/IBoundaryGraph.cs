using Colossal.Mathematics;
using Unity.Entities;

namespace Subdivisions.Core
{
    /// <summary>
    /// The seam the border tracer consumes. Adapters decide how an edge is classified
    /// (<see cref="IsBoundary"/>/<see cref="GetKind"/>) and how the graph is stored; the
    /// tracer never sees game components or ECS lookups.
    /// </summary>
    public interface IBoundaryGraph
    {
        EdgeEnds GetEndpoints(Entity edge);

        Bezier4x3 GetCurve(Entity edge);

        bool IsBoundary(Entity edge);

        NetworkKind GetKind(Entity edge);

        /// <summary>
        /// Neighbor edges connected at <paramref name="node"/>, exposed as count + index
        /// rather than an enumerator so iteration allocates nothing across thousands of
        /// path-find visits and no game-typed buffer leaks across the seam. Iterate as
        /// <c>for (var i = 0; i &lt; GetNeighborCount(n); i++) GetNeighborAt(n, i)</c>.
        /// </summary>
        int GetNeighborCount(Entity node);

        Entity GetNeighborAt(Entity node, int index);
    }
}
