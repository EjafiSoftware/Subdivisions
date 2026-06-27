using Colossal.Mathematics;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    /// <summary>
    /// A* search over the boundary subgraph between two snapped points.
    /// </summary>
    internal struct BoundaryPathFinder
    {
        [ReadOnly] public ComponentLookup<Edge> _edgeLookup;
        [ReadOnly] public ComponentLookup<Curve> _curveLookup;
        [ReadOnly] public BufferLookup<ConnectedEdge> _connectedEdges;
        public NetBoundary _boundary;

        private const float MaxPathCost = 30000f;
        private const int MaxVisited = 4000;

        private struct Link
        {
            public Entity _prev;
            public Entity _via;
        }

        public bool FindPath(SnapPoint a, SnapPoint b, NativeHashSet<Entity> usedEdges, NativeList<Entity> nodePath, NativeList<Entity> edgePath)
        {
            var ea = _edgeLookup[a._edge];
            var eb = _edgeLookup[b._edge];
            var ca = _curveLookup[a._edge].m_Bezier;
            var cb = _curveLookup[b._edge].m_Bezier;

            var dist = new NativeHashMap<Entity, float>(256, Allocator.Temp);
            var link = new NativeHashMap<Entity, Link>(256, Allocator.Temp);
            var settled = new NativeHashSet<Entity>(256, Allocator.Temp);
            var open = MinHeap.Allocate(256, Allocator.Temp);

            var goal = b._position.xz;
            Seed(dist, link, open, ea.m_Start, NetSnap.MeasureSubCurve(ca, a._t, 0f), MathUtils.Position(ca, 0f).xz, goal);
            Seed(dist, link, open, ea.m_End, NetSnap.MeasureSubCurve(ca, a._t, 1f), MathUtils.Position(ca, 1f).xz, goal);

            var targetsRemaining = eb.m_Start == eb.m_End ? 1 : 2;

            var visited = 0;
            while (open.Count > 0)
            {
                var top = open.Pop();
                var u = top._node;
                var bd = top._cost;
                if (settled.Contains(u))
                {
                    continue;
                }
                if (dist.TryGetValue(u, out var cur) && bd > cur)
                {
                    continue;
                }
                if (bd > MaxPathCost || ++visited > MaxVisited)
                {
                    break;
                }
                settled.Add(u);

                if ((u == eb.m_Start || u == eb.m_End) && --targetsRemaining <= 0)
                {
                    break;
                }

                if (!_connectedEdges.HasBuffer(u))
                {
                    continue;
                }
                var buffer = _connectedEdges[u];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var ce = buffer[i].m_Edge;
                    if (!_boundary.IsBoundary(ce) || usedEdges.Contains(ce))
                    {
                        continue;
                    }
                    var e = _edgeLookup[ce];
                    Entity v;
                    if (e.m_Start == u)
                    {
                        v = e.m_End;
                    }
                    else if (e.m_End == u)
                    {
                        v = e.m_Start;
                    }
                    else
                    {
                        continue;
                    }
                    var bez = _curveLookup[ce].m_Bezier;
                    var nd = bd + NetSnap.MeasureCurve(bez);
                    if (!dist.TryGetValue(v, out var old) || nd < old)
                    {
                        dist[v] = nd;
                        link[v] = new Link { _prev = u, _via = ce };
                        var vPos = MathUtils.Position(bez, e.m_Start == v ? 0f : 1f).xz;
                        open.Push(nd + math.distance(vPos, goal), nd, v);
                    }
                }
            }

            var best = float.MaxValue;
            var tgt = Entity.Null;
            if (dist.TryGetValue(eb.m_Start, out var db0))
            {
                var total = db0 + NetSnap.MeasureSubCurve(cb, 0f, b._t);
                if (total < best)
                {
                    best = total;
                    tgt = eb.m_Start;
                }
            }
            if (dist.TryGetValue(eb.m_End, out var db1))
            {
                var total = db1 + NetSnap.MeasureSubCurve(cb, 1f, b._t);
                if (total < best)
                {
                    tgt = eb.m_End;
                }
            }

            var found = false;
            if (tgt != Entity.Null)
            {
                var revNodes = new NativeList<Entity>(Allocator.Temp);
                var revEdges = new NativeList<Entity>(Allocator.Temp);
                var cur = tgt;
                while (true)
                {
                    revNodes.Add(cur);
                    if (!link.TryGetValue(cur, out var lk) || lk._prev == Entity.Null)
                    {
                        break;
                    }
                    revEdges.Add(lk._via);
                    cur = lk._prev;
                }
                for (var i = revNodes.Length - 1; i >= 0; i--)
                {
                    nodePath.Add(revNodes[i]);
                }
                for (var i = revEdges.Length - 1; i >= 0; i--)
                {
                    edgePath.Add(revEdges[i]);
                }
                revNodes.Dispose();
                revEdges.Dispose();
                found = true;
            }

            dist.Dispose();
            link.Dispose();
            settled.Dispose();
            open.Dispose();
            return found;
        }

        private static void Seed(NativeHashMap<Entity, float> dist, NativeHashMap<Entity, Link> link, MinHeap open, Entity node, float cost, float2 position, float2 goal)
        {
            if (!dist.TryGetValue(node, out var old) || cost < old)
            {
                dist[node] = cost;
                link[node] = new Link { _prev = Entity.Null, _via = Entity.Null };
                open.Push(cost + math.distance(position, goal), cost, node);
            }
        }
    }
}
