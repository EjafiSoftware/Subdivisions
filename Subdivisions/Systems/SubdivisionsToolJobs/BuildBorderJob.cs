using Colossal.Mathematics;
using Game.Net;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    [BurstCompile]
    internal struct BuildBorderJob : IJob
    {
        [ReadOnly] public NativeList<SnapPoint> _points;

        [ReadOnly] public ComponentLookup<Edge> _edgeLookup;
        [ReadOnly] public ComponentLookup<Curve> _curveLookup;
        [ReadOnly] public BufferLookup<ConnectedEdge> _connectedEdges;
        public NetBoundary _boundary;

        public NativeList<float3> _result;
        public NativeReference<bool> _valid;

        private const float DeviationTol = 1.0f;
        private const int MaxDepth = 7;

        private const float MaxPathCost = 30000f;
        private const int MaxVisited = 4000;

        private const float DuplicateEpsilon = 0.5f;
        private const float CollinearTolerance = 0.5f;
        private const float MinNodeSpacing = 2f;

        private const float MinNeckWidth = 6f;

        private struct Link
        {
            public Entity _prev;
            public Entity _via;
        }

        private struct HeapItem
        {
            public float _cost;
            public Entity _node;
        }

        public void Execute()
        {
            _result.Clear();
            _valid.Value = false;
            if (_points.Length < 3)
            {
                return;
            }

            var ring = new NativeList<float2>(Allocator.Temp);
            TryBuildRing(ring);

            if (ring.Length >= 3 && Polygon.IsSimple(ring))
            {
                var height = _points[0]._position.y;
                for (var i = 0; i < ring.Length; i++)
                {
                    _result.Add(new float3(ring[i].x, height, ring[i].y));
                }
                _valid.Value = true;
            }
            ring.Dispose();
        }

        private void TryBuildRing(NativeList<float2> ring)
        {
            var n = _points.Length;
            var straight = new NativeArray<bool>(n, Allocator.Temp);
            var edgeOwner = new NativeList<int>(Allocator.Temp);

            for (var attempt = 0; attempt < n; attempt++)
            {
                ring.Clear();
                edgeOwner.Clear();
                var usedEdges = new NativeHashSet<Entity>(64, Allocator.Temp);

                ring.Add(_points[0]._position.xz);
                for (var i = 0; i < n; i++)
                {
                    var before = ring.Length;
                    Segment(_points[i], _points[(i + 1) % n], ring, usedEdges, straight[i]);
                    for (var k = before; k < ring.Length; k++)
                    {
                        edgeOwner.Add(i);
                    }
                }
                usedEdges.Dispose();

                if (ring.Length > 1 && math.distance(ring[^1], ring[0]) < DuplicateEpsilon)
                {
                    ring.RemoveAt(ring.Length - 1);
                }
                while (edgeOwner.Length < ring.Length)
                {
                    edgeOwner.Add(n - 1);
                }

                if (!StraightenFirstCrossing(ring, edgeOwner, straight))
                {
                    break;
                }
            }

            edgeOwner.Dispose();
            straight.Dispose();

            Polygon.CollapseSpikes(ring, DuplicateEpsilon);
            Polygon.ResolveNecks(ring, MinNeckWidth);
            Polygon.DropCollinear(ring, CollinearTolerance);
            Polygon.EnforceMinSpacing(ring, MinNodeSpacing);
        }

        private static bool StraightenFirstCrossing(NativeList<float2> ring, NativeList<int> edgeOwner, NativeArray<bool> straight)
        {
            var n = ring.Length;
            for (var i = 0; i < n; i++)
            {
                var a1 = ring[i];
                var a2 = ring[(i + 1) % n];
                for (var j = i + 1; j < n; j++)
                {
                    if ((j + 1) % n == i || (i + 1) % n == j)
                    {
                        continue;
                    }
                    if (!Geometry.SegmentsCross(a1, a2, ring[j], ring[(j + 1) % n]))
                    {
                        continue;
                    }
                    var o1 = edgeOwner[i];
                    var o2 = edgeOwner[j];
                    if (o1 >= 0 && !straight[o1])
                    {
                        straight[o1] = true;
                        return true;
                    }
                    if (o2 >= 0 && !straight[o2])
                    {
                        straight[o2] = true;
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        private void Segment(SnapPoint a, SnapPoint b, NativeList<float2> ring, NativeHashSet<Entity> usedEdges, bool straightOnly)
        {
            if (straightOnly || !a.OnNet || !b.OnNet || _boundary.GetKind(a._edge) != _boundary.GetKind(b._edge))
            {
                ring.Add(b._position.xz);
                return;
            }

            var ca = _curveLookup[a._edge].m_Bezier;
            if (a._edge == b._edge)
            {
                EmitRange(ca, a._t, b._t, ring, 0);
                return;
            }

            var nodePath = new NativeList<Entity>(Allocator.Temp);
            var edgePath = new NativeList<Entity>(Allocator.Temp);
            if (!FindPath(a, b, nodePath, edgePath, usedEdges))
            {
                ring.Add(b._position.xz);
                nodePath.Dispose();
                edgePath.Dispose();
                return;
            }

            var ea = _edgeLookup[a._edge];
            var eb = _edgeLookup[b._edge];
            var cb = _curveLookup[b._edge].m_Bezier;
            var src = nodePath[0];
            var tgt = nodePath[^1];

            EmitRange(ca, a._t, src == ea.m_Start ? 0f : 1f, ring, 0);

            for (var k = 0; k < edgePath.Length; k++)
            {
                var e = edgePath[k];
                var ce = _edgeLookup[e];
                var bez = _curveLookup[e].m_Bezier;
                var u = nodePath[k];
                var v = nodePath[k + 1];
                var t0 = ce.m_Start == u ? 0f : 1f;
                var t1 = ce.m_Start == v ? 0f : 1f;
                EmitRange(bez, t0, t1, ring, 0);
                usedEdges.Add(e);
            }

            EmitRange(cb, tgt == eb.m_Start ? 0f : 1f, b._t, ring, 0);

            nodePath.Dispose();
            edgePath.Dispose();
        }

        private void EmitRange(Bezier4x3 bez, float t0, float t1, NativeList<float2> ring, int depth)
        {
            var p0 = MathUtils.Position(bez, t0).xz;
            var p1 = MathUtils.Position(bez, t1).xz;
            var tm = (t0 + t1) * 0.5f;
            var pm = MathUtils.Position(bez, tm).xz;

            if (depth >= MaxDepth || Geometry.MeasurePerpendicularDistance(pm, p0, p1) < DeviationTol)
            {
                ring.Add(p1);
                return;
            }
            EmitRange(bez, t0, tm, ring, depth + 1);
            EmitRange(bez, tm, t1, ring, depth + 1);
        }

        private bool FindPath(SnapPoint a, SnapPoint b, NativeList<Entity> nodePath, NativeList<Entity> edgePath, NativeHashSet<Entity> usedEdges)
        {
            var ea = _edgeLookup[a._edge];
            var eb = _edgeLookup[b._edge];
            var ca = _curveLookup[a._edge].m_Bezier;
            var cb = _curveLookup[b._edge].m_Bezier;

            var dist = new NativeHashMap<Entity, float>(256, Allocator.Temp);
            var link = new NativeHashMap<Entity, Link>(256, Allocator.Temp);
            var settled = new NativeHashSet<Entity>(256, Allocator.Temp);
            var open = new NativeList<HeapItem>(256, Allocator.Temp);

            Seed(dist, link, open, ea.m_Start, NetSnap.MeasureSubCurve(ca, a._t, 0f));
            Seed(dist, link, open, ea.m_End, NetSnap.MeasureSubCurve(ca, a._t, 1f));

            var targetsRemaining = eb.m_Start == eb.m_End ? 1 : 2;

            var visited = 0;
            while (open.Length > 0)
            {
                var top = HeapPop(open);
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
                    var nd = bd + NetSnap.MeasureCurve(_curveLookup[ce].m_Bezier);
                    if (!dist.TryGetValue(v, out var old) || nd < old)
                    {
                        dist[v] = nd;
                        link[v] = new Link { _prev = u, _via = ce };
                        HeapPush(open, nd, v);
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

        private static void Seed(NativeHashMap<Entity, float> dist, NativeHashMap<Entity, Link> link, NativeList<HeapItem> open, Entity node, float cost)
        {
            if (!dist.TryGetValue(node, out var old) || cost < old)
            {
                dist[node] = cost;
                link[node] = new Link { _prev = Entity.Null, _via = Entity.Null };
                HeapPush(open, cost, node);
            }
        }

        private static void HeapPush(NativeList<HeapItem> heap, float cost, Entity node)
        {
            heap.Add(new HeapItem { _cost = cost, _node = node });
            var i = heap.Length - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (heap[parent]._cost <= heap[i]._cost)
                {
                    break;
                }
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static HeapItem HeapPop(NativeList<HeapItem> heap)
        {
            var root = heap[0];
            var last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            var n = heap.Length;
            var i = 0;
            while (true)
            {
                var left = 2 * i + 1;
                var right = 2 * i + 2;
                var smallest = i;
                if (left < n && heap[left]._cost < heap[smallest]._cost)
                {
                    smallest = left;
                }
                if (right < n && heap[right]._cost < heap[smallest]._cost)
                {
                    smallest = right;
                }
                if (smallest == i)
                {
                    break;
                }
                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }
            return root;
        }
    }
}
