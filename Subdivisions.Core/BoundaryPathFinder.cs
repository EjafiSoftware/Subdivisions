using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>
    /// A* search over the boundary subgraph between two snapped points. Reusable: the scratch
    /// collections are cleared per call so one instance serves every segment of a trace.
    /// </summary>
    internal sealed class BoundaryPathFinder
    {
        private const float MaxPathCost = 30000f;
        private const int MaxVisited = 4000;

        private struct Link
        {
            public Entity _prev;
            public Entity _via;
        }

        private readonly Dictionary<Entity, float> _dist = new();
        private readonly Dictionary<Entity, Link> _link = new();
        private readonly HashSet<Entity> _settled = new();
        private readonly MinHeap _open = new();
        private readonly List<Entity> _revNodes = new();
        private readonly List<Entity> _revEdges = new();

        public bool FindPath(
            IBoundaryGraph graph,
            SnapPoint a,
            SnapPoint b,
            HashSet<Entity> usedEdges,
            List<Entity> nodePath,
            List<Entity> edgePath)
        {
            var ea = graph.GetEndpoints(a._edge);
            var eb = graph.GetEndpoints(b._edge);
            var ca = graph.GetCurve(a._edge);
            var cb = graph.GetCurve(b._edge);

            _dist.Clear();
            _link.Clear();
            _settled.Clear();
            _open.Clear();

            var goal = b._position.xz;
            Seed(ea.Start, NetSnap.MeasureSubCurve(ca, a._t, 0f), MathUtils.Position(ca, 0f).xz, goal);
            Seed(ea.End, NetSnap.MeasureSubCurve(ca, a._t, 1f), MathUtils.Position(ca, 1f).xz, goal);

            var targetsRemaining = eb.Start == eb.End ? 1 : 2;

            var visited = 0;
            while (_open.Count > 0)
            {
                var top = _open.Pop();
                var u = top._node;
                var bd = top._cost;
                if (_settled.Contains(u))
                {
                    continue;
                }
                if (_dist.TryGetValue(u, out var cur) && bd > cur)
                {
                    continue;
                }
                if (bd > MaxPathCost || ++visited > MaxVisited)
                {
                    break;
                }
                _settled.Add(u);

                if ((u == eb.Start || u == eb.End) && --targetsRemaining <= 0)
                {
                    break;
                }

                var count = graph.GetNeighborCount(u);
                for (var i = 0; i < count; i++)
                {
                    var ce = graph.GetNeighborAt(u, i);
                    if (!graph.IsBoundary(ce) || usedEdges.Contains(ce))
                    {
                        continue;
                    }
                    var e = graph.GetEndpoints(ce);
                    Entity v;
                    if (e.Start == u)
                    {
                        v = e.End;
                    }
                    else if (e.End == u)
                    {
                        v = e.Start;
                    }
                    else
                    {
                        continue;
                    }
                    var bez = graph.GetCurve(ce);
                    var nd = bd + NetSnap.MeasureCurve(bez);
                    if (!_dist.TryGetValue(v, out var old) || nd < old)
                    {
                        _dist[v] = nd;
                        _link[v] = new Link { _prev = u, _via = ce };
                        var vPos = MathUtils.Position(bez, e.Start == v ? 0f : 1f).xz;
                        _open.Push(nd + math.distance(vPos, goal), nd, v);
                    }
                }
            }

            var best = float.MaxValue;
            var tgt = Entity.Null;
            if (_dist.TryGetValue(eb.Start, out var db0))
            {
                var total = db0 + NetSnap.MeasureSubCurve(cb, 0f, b._t);
                if (total < best)
                {
                    best = total;
                    tgt = eb.Start;
                }
            }
            if (_dist.TryGetValue(eb.End, out var db1))
            {
                var total = db1 + NetSnap.MeasureSubCurve(cb, 1f, b._t);
                if (total < best)
                {
                    tgt = eb.End;
                }
            }

            if (tgt == Entity.Null)
            {
                return false;
            }

            _revNodes.Clear();
            _revEdges.Clear();
            var node = tgt;
            while (true)
            {
                _revNodes.Add(node);
                if (!_link.TryGetValue(node, out var lk) || lk._prev == Entity.Null)
                {
                    break;
                }
                _revEdges.Add(lk._via);
                node = lk._prev;
            }
            for (var i = _revNodes.Count - 1; i >= 0; i--)
            {
                nodePath.Add(_revNodes[i]);
            }
            for (var i = _revEdges.Count - 1; i >= 0; i--)
            {
                edgePath.Add(_revEdges[i]);
            }
            return true;
        }

        private void Seed(Entity node, float cost, float2 position, float2 goal)
        {
            if (!_dist.TryGetValue(node, out var old) || cost < old)
            {
                _dist[node] = cost;
                _link[node] = new Link { _prev = Entity.Null, _via = Entity.Null };
                _open.Push(cost + math.distance(position, goal), cost, node);
            }
        }
    }
}
