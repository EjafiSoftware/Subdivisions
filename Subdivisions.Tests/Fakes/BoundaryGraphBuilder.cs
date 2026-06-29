using System.Collections.Generic;
using Colossal.Mathematics;
using Subdivisions.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Tests.Fakes
{
    /// <summary>
    /// Fluent construction of an <see cref="ArrayBoundaryGraph"/> from node positions and edges
    /// by node id. Straight edges synthesize a bezier with controls at 1/3 and 2/3; an overload
    /// takes an explicit curve to exercise the tessellator. Edges default to boundary edges.
    /// </summary>
    internal sealed class BoundaryGraphBuilder
    {
        private readonly Dictionary<int, float3> _nodePos = new();
        private readonly Dictionary<Entity, EdgeEnds> _endpoints = new();
        private readonly Dictionary<Entity, Bezier4x3> _curves = new();
        private readonly Dictionary<Entity, bool> _isBoundary = new();
        private readonly Dictionary<Entity, NetworkKind> _kind = new();
        private readonly Dictionary<Entity, List<Entity>> _neighbors = new();
        private int _edgeCounter;

        public static Entity NodeEntity(int id) => new()
            { Index = id + 1, Version = 1 };

        public BoundaryGraphBuilder Node(int id, float x, float z)
        {
            _nodePos[id] = new float3(x, 0f, z);
            return this;
        }

        public Entity Edge(int a, int b, NetworkKind kind, bool boundary = true)
        {
            return Edge(a, b, StraightBezier(_nodePos[a], _nodePos[b]), kind, boundary);
        }

        public Entity Edge(int a, int b, Bezier4x3 curve, NetworkKind kind, bool boundary = true)
        {
            var edge = new Entity { Index = 100000 + _edgeCounter++, Version = 1 };
            var na = NodeEntity(a);
            var nb = NodeEntity(b);
            _endpoints[edge] = new EdgeEnds(na, nb);
            _curves[edge] = curve;
            _isBoundary[edge] = boundary;
            _kind[edge] = kind;
            AddNeighbor(na, edge);
            AddNeighbor(nb, edge);
            return edge;
        }

        public ArrayBoundaryGraph Build()
        {
            return new ArrayBoundaryGraph(_endpoints, _curves, _isBoundary, _kind, _neighbors);
        }

        private void AddNeighbor(Entity node, Entity edge)
        {
            if (!_neighbors.TryGetValue(node, out var list))
            {
                list = new List<Entity>();
                _neighbors[node] = list;
            }
            list.Add(edge);
        }

        private static Bezier4x3 StraightBezier(float3 p0, float3 p3)
        {
            return new Bezier4x3(p0, math.lerp(p0, p3, 1f / 3f), math.lerp(p0, p3, 2f / 3f), p3);
        }
    }
}
