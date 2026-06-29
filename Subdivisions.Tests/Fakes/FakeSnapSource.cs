using System.Collections.Generic;
using Subdivisions.Core;
using Unity.Mathematics;

namespace Subdivisions.Tests.Fakes
{
    /// <summary>
    /// In-memory <see cref="ISnapSource"/> for resolver tests. Candidates are placed
    /// geometrically; the source reports each candidate's planar distance from the hit,
    /// exactly as the production sources do.
    /// </summary>
    internal sealed class FakeSnapSource : ISnapSource
    {
        private readonly List<SnapPoint> _vertices = new();
        private readonly List<SnapPoint> _edges = new();

        public FakeSnapSource Vertex(SnapPoint point)
        {
            _vertices.Add(point);
            return this;
        }

        public FakeSnapSource Edge(SnapPoint point)
        {
            _edges.Add(point);
            return this;
        }

        public void AddVertices(float3 hit, ref SnapAccumulator acc)
        {
            foreach (var v in _vertices)
            {
                acc.Consider(v, math.distance(v.Position.xz, hit.xz));
            }
        }

        public void AddEdges(float3 hit, ref SnapAccumulator acc)
        {
            foreach (var e in _edges)
            {
                acc.Consider(e, math.distance(e.Position.xz, hit.xz));
            }
        }
    }
}
