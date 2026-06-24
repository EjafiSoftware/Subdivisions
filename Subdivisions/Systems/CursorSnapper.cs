using Colossal.Mathematics;
using Game.Net;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    internal sealed class CursorSnapper
    {
        private const float SnapDistance = 20f;
        private const float NodeSnapDistance = 14f;

        private readonly RoadNetwork _roads;
        private readonly AreaIndex _areas;

        public CursorSnapper(RoadNetwork roads, AreaIndex areas)
        {
            _roads = roads;
            _areas = areas;
        }

        public SnapPoint Snap(float3 hit)
        {
            var free = new SnapPoint { _position = hit, _edge = Entity.Null, _t = 0f };
            var curves = _roads.Curves;
            var edges = _roads.CollectBoundaryEdges(hit, SnapDistance);
            var areas = _areas.CollectAreas(hit, SnapDistance);

            var vertex = FindNearestVertex(hit, curves, edges, areas, free, out var haveVertex);
            if (haveVertex)
            {
                edges.Dispose();
                areas.Dispose();
                return vertex;
            }

            var edge = FindNearestEdge(hit, curves, edges, areas, free);
            edges.Dispose();
            areas.Dispose();
            return edge;
        }

        private SnapPoint FindNearestVertex(
            float3 hit,
            ComponentLookup<Curve> curves,
            NativeList<Entity> edges,
            NativeList<Entity> areas,
            SnapPoint fallback,
            out bool found)
        {
            var best = NodeSnapDistance;
            var result = fallback;
            found = false;

            foreach (var t1 in edges)
            {
                var bezier = curves[t1].m_Bezier;
                for (var end = 0; end < 2; end++)
                {
                    var t = end == 0 ? 0f : 1f;
                    var position = MathUtils.Position(bezier, t);
                    var distance = math.distance(position.xz, hit.xz);
                    if (distance < best)
                    {
                        best = distance;
                        found = true;
                        result = new SnapPoint { _position = position, _edge = t1, _t = t };
                    }
                }
            }

            foreach (var t in areas)
            {
                var ring = _areas.GetRing(t);
                for (var n = 0; n < ring.Length; n++)
                {
                    var position = ring[n].m_Position;
                    var distance = math.distance(position.xz, hit.xz);
                    if (distance < best)
                    {
                        best = distance;
                        found = true;
                        result = new SnapPoint { _position = position, _edge = Entity.Null, _t = 0f, _onArea = true };
                    }
                }
            }

            return result;
        }

        private SnapPoint FindNearestEdge(
            float3 hit,
            ComponentLookup<Curve> curves,
            NativeList<Entity> edges,
            NativeList<Entity> areas,
            SnapPoint fallback)
        {
            var best = SnapDistance;
            var result = fallback;

            foreach (var t1 in edges)
            {
                var bezier = curves[t1].m_Bezier;
                var t = NetSnap.FindNearestT(bezier, hit.xz);
                var position = MathUtils.Position(bezier, t);
                var distance = math.distance(position.xz, hit.xz);
                if (distance < best)
                {
                    best = distance;
                    result = new SnapPoint { _position = position, _edge = t1, _t = t };
                }
            }

            foreach (var t in areas)
            {
                var ring = _areas.GetRing(t);
                for (var n = 0; n < ring.Length; n++)
                {
                    var a = ring[n].m_Position;
                    var b = ring[(n + 1) % ring.Length].m_Position;
                    var position = ProjectOntoSegment(a, b, hit.xz, out var distance);
                    if (distance < best)
                    {
                        best = distance;
                        result = new SnapPoint { _position = position, _edge = Entity.Null, _t = 0f, _onArea = true };
                    }
                }
            }

            return result;
        }

        private static float3 ProjectOntoSegment(float3 a, float3 b, float2 p, out float distance)
        {
            var ab = b.xz - a.xz;
            var lengthSq = math.lengthsq(ab);
            var t = lengthSq > 1e-6f ? math.saturate(math.dot(p - a.xz, ab) / lengthSq) : 0f;
            var position = math.lerp(a, b, t);
            distance = math.distance(position.xz, p);
            return position;
        }
    }
}
