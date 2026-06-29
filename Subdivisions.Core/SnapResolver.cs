using System.Collections.Generic;
using Unity.Entities;

namespace Subdivisions.Core
{
    /// <summary>
    /// Resolves a cursor hit to a single <see cref="SnapPoint"/> across the snap sources:
    /// vertices win over edges, nearest wins within each, and a coincident pair is broken
    /// by continuity with the previous point. Pure - the sources own collection and lifetime.
    /// </summary>
    public static class SnapResolver
    {
        public static SnapPoint Resolve(IReadOnlyList<ISnapSource> sources, in SnapQuery query)
        {
            var settings = query.Settings;
            var free = new SnapPoint { Position = query.Hit, Edge = Entity.Null, CurveParameter = 0f };
            var preference = Preference(query.Previous, settings.Default);

            var vertices = new SnapAccumulator(settings.NodeSnapDistance, free, preference, settings.CoincidenceRadius);
            for (var i = 0; i < sources.Count; i++)
            {
                sources[i].AddVertices(query.Hit, ref vertices);
            }
            if (vertices.Found)
            {
                return vertices.Result;
            }

            var edges = new SnapAccumulator(settings.SnapDistance, free, preference, settings.CoincidenceRadius);
            for (var i = 0; i < sources.Count; i++)
            {
                sources[i].AddEdges(query.Hit, ref edges);
            }
            return edges.Result;
        }

        private static SnapPreference Preference(SnapPoint? previous, SnapPreference fallback)
        {
            if (previous is { } p && p.OnArea && !p.OnNet)
            {
                return SnapPreference.Area;
            }
            return fallback;
        }
    }
}
