using System.Collections.Generic;
using Colossal.Mathematics;
using Subdivisions.Core;
using Unity.Entities;

namespace Subdivisions.Tests.Fakes
{
    /// <summary>
    /// In-memory <see cref="IBoundaryGraph"/> adapter for tests. The second adapter that makes
    /// the seam real; built by <see cref="BoundaryGraphBuilder"/>.
    /// </summary>
    internal sealed class ArrayBoundaryGraph : IBoundaryGraph
    {
        private readonly Dictionary<Entity, EdgeEnds> _endpoints;
        private readonly Dictionary<Entity, Bezier4x3> _curves;
        private readonly Dictionary<Entity, bool> _isBoundary;
        private readonly Dictionary<Entity, NetworkKind> _kind;
        private readonly Dictionary<Entity, List<Entity>> _neighbors;

        public ArrayBoundaryGraph(
            Dictionary<Entity, EdgeEnds> endpoints,
            Dictionary<Entity, Bezier4x3> curves,
            Dictionary<Entity, bool> isBoundary,
            Dictionary<Entity, NetworkKind> kind,
            Dictionary<Entity, List<Entity>> neighbors)
        {
            _endpoints = endpoints;
            _curves = curves;
            _isBoundary = isBoundary;
            _kind = kind;
            _neighbors = neighbors;
        }

        public EdgeEnds GetEndpoints(Entity edge) => _endpoints[edge];

        public Bezier4x3 GetCurve(Entity edge) => _curves[edge];

        public bool IsBoundary(Entity edge) => _isBoundary.TryGetValue(edge, out var b) && b;

        public NetworkKind GetKind(Entity edge) => _kind.TryGetValue(edge, out var k) ? k : NetworkKind.None;

        public int GetNeighborCount(Entity node) => _neighbors.TryGetValue(node, out var list) ? list.Count : 0;

        public Entity GetNeighborAt(Entity node, int index) => _neighbors[node][index];

        /// <summary>A control point snapped onto <paramref name="edge"/> at curve parameter <paramref name="t"/>.</summary>
        public SnapPoint PointOn(Entity edge, float t)
        {
            return new SnapPoint
            {
                Position = MathUtils.Position(_curves[edge], t),
                Edge = edge,
                CurveParameter = t,
            };
        }
    }
}
