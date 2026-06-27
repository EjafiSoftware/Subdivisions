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

        private const float DuplicateEpsilon = 0.5f;
        private const float CollinearTolerance = 0.5f;
        private const float MinNodeSpacing = 2f;

        private const float MinNeckWidth = 6f;

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
            var usedEdges = new NativeHashSet<Entity>(64, Allocator.Temp);

            ring.Add(_points[0]._position.xz);
            for (var i = 0; i < n; i++)
            {
                Segment(_points[i], _points[(i + 1) % n], ring, usedEdges);
            }
            usedEdges.Dispose();

            if (ring.Length > 1 && math.distance(ring[^1], ring[0]) < DuplicateEpsilon)
            {
                ring.RemoveAt(ring.Length - 1);
            }

            Polygon.CollapseSpikes(ring, DuplicateEpsilon);
            Polygon.ResolveNecks(ring, MinNeckWidth);
            Polygon.DropCollinear(ring, CollinearTolerance);
            Polygon.EnforceMinSpacing(ring, MinNodeSpacing);
        }

        private void Segment(SnapPoint a, SnapPoint b, NativeList<float2> ring, NativeHashSet<Entity> usedEdges)
        {
            if (!a.OnNet || !b.OnNet || _boundary.GetKind(a._edge) != _boundary.GetKind(b._edge))
            {
                ring.Add(b._position.xz);
                return;
            }

            var ca = _curveLookup[a._edge].m_Bezier;
            if (a._edge == b._edge)
            {
                CurveTessellator.EmitRange(ca, a._t, b._t, ring);
                return;
            }

            var nodePath = new NativeList<Entity>(Allocator.Temp);
            var edgePath = new NativeList<Entity>(Allocator.Temp);
            var finder = new BoundaryPathFinder
            {
                _edgeLookup = _edgeLookup,
                _curveLookup = _curveLookup,
                _connectedEdges = _connectedEdges,
                _boundary = _boundary,
            };
            if (!finder.FindPath(a, b, usedEdges, nodePath, edgePath))
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

            CurveTessellator.EmitRange(ca, a._t, src == ea.m_Start ? 0f : 1f, ring);

            for (var k = 0; k < edgePath.Length; k++)
            {
                var e = edgePath[k];
                var ce = _edgeLookup[e];
                var bez = _curveLookup[e].m_Bezier;
                var u = nodePath[k];
                var v = nodePath[k + 1];
                var t0 = ce.m_Start == u ? 0f : 1f;
                var t1 = ce.m_Start == v ? 0f : 1f;
                CurveTessellator.EmitRange(bez, t0, t1, ring);
                usedEdges.Add(e);
            }

            CurveTessellator.EmitRange(cb, tgt == eb.m_Start ? 0f : 1f, b._t, ring);

            nodePath.Dispose();
            edgePath.Dispose();
        }
    }
}
