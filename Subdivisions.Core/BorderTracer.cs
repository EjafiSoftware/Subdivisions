using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>
    /// Turns ordered snapped control points into a closed border ring, tracing along the
    /// boundary subgraph between points that sit on the same network kind and connecting
    /// everything else with a straight line. The one deep entry point of the tracing core;
    /// the path-finder, tessellator, and polygon cleanup behind it are implementation detail.
    ///
    /// Holds pooled working buffers and reuses them across calls, so a single instance serves
    /// every rebuild. The returned <see cref="TraceResult.Ring"/> is a view over those buffers,
    /// valid only until the next <see cref="Trace"/> call.
    /// </summary>
    public sealed class BorderTracer
    {
        private const float DuplicateEpsilon = 0.5f;
        private const float CollinearTolerance = 0.5f;
        private const float MinNodeSpacing = 2f;
        private const float MinNeckWidth = 6f;

        private readonly BoundaryPathFinder _finder = new();
        private readonly List<float2> _ring = new();
        private readonly List<float3> _result = new();
        private readonly HashSet<Entity> _usedEdges = new();
        private readonly List<Entity> _nodePath = new();
        private readonly List<Entity> _edgePath = new();

        public TraceResult Trace(IReadOnlyList<SnapPoint> points, IBoundaryGraph graph)
        {
            _result.Clear();
            if (points.Count < 3)
            {
                return new TraceResult(_result, false);
            }

            _ring.Clear();
            TryBuildRing(points, graph);

            if (_ring.Count >= 3 && Polygon.IsSimple(_ring))
            {
                var height = points[0].Position.y;
                for (var i = 0; i < _ring.Count; i++)
                {
                    _result.Add(new float3(_ring[i].x, height, _ring[i].y));
                }
                return new TraceResult(_result, true);
            }
            return new TraceResult(_result, false);
        }

        private void TryBuildRing(IReadOnlyList<SnapPoint> points, IBoundaryGraph graph)
        {
            var n = points.Count;
            _usedEdges.Clear();

            _ring.Add(points[0].Position.xz);
            for (var i = 0; i < n; i++)
            {
                Segment(points[i], points[(i + 1) % n], graph);
            }

            if (_ring.Count > 1 && math.distance(_ring[_ring.Count - 1], _ring[0]) < DuplicateEpsilon)
            {
                _ring.RemoveAt(_ring.Count - 1);
            }

            Polygon.CollapseSpikes(_ring, DuplicateEpsilon);
            Polygon.ResolveNecks(_ring, MinNeckWidth);
            Polygon.DropCollinear(_ring, CollinearTolerance);
            Polygon.EnforceMinSpacing(_ring, MinNodeSpacing);
        }

        private void Segment(SnapPoint a, SnapPoint b, IBoundaryGraph graph)
        {
            if (!a.OnNet || !b.OnNet || graph.GetKind(a.Edge) != graph.GetKind(b.Edge))
            {
                _ring.Add(b.Position.xz);
                return;
            }

            var ca = graph.GetCurve(a.Edge);
            if (a.Edge == b.Edge)
            {
                CurveTessellator.EmitRange(ca, a.CurveParameter, b.CurveParameter, _ring);
                return;
            }

            _nodePath.Clear();
            _edgePath.Clear();
            if (!_finder.FindPath(graph, a, b, _usedEdges, _nodePath, _edgePath))
            {
                _ring.Add(b.Position.xz);
                return;
            }

            var ea = graph.GetEndpoints(a.Edge);
            var eb = graph.GetEndpoints(b.Edge);
            var cb = graph.GetCurve(b.Edge);
            var src = _nodePath[0];
            var tgt = _nodePath[_nodePath.Count - 1];

            CurveTessellator.EmitRange(ca, a.CurveParameter, src == ea.Start ? 0f : 1f, _ring);

            for (var k = 0; k < _edgePath.Count; k++)
            {
                var e = _edgePath[k];
                var ce = graph.GetEndpoints(e);
                var bez = graph.GetCurve(e);
                var u = _nodePath[k];
                var v = _nodePath[k + 1];
                var t0 = ce.Start == u ? 0f : 1f;
                var t1 = ce.Start == v ? 0f : 1f;
                CurveTessellator.EmitRange(bez, t0, t1, _ring);
                _usedEdges.Add(e);
            }

            CurveTessellator.EmitRange(cb, tgt == eb.Start ? 0f : 1f, b.CurveParameter, _ring);
        }
    }
}
