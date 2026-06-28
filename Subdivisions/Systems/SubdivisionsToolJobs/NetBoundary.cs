using Game.Common;
using Game.Net;
using Game.Prefabs;
using Subdivisions.Core;
using Unity.Collections;
using Unity.Entities;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal struct NetBoundary
    {
        [ReadOnly] public ComponentLookup<Edge> _edgeLookup;
        [ReadOnly] public ComponentLookup<Curve> _curveLookup;
        [ReadOnly] public ComponentLookup<Owner> _ownerLookup;
        [ReadOnly] public ComponentLookup<Deleted> _deletedLookup;
        [ReadOnly] public ComponentLookup<Composition> _compositionLookup;
        [ReadOnly] public ComponentLookup<NetCompositionData> _compositionDataLookup;

        public bool IsBoundary(Entity edge)
        {
            if (_ownerLookup.HasComponent(edge) || _deletedLookup.HasComponent(edge))
            {
                return false;
            }
            if (!_edgeLookup.HasComponent(edge) || !_curveLookup.HasComponent(edge)
                || !_compositionLookup.HasComponent(edge))
            {
                return false;
            }

            var composition = _compositionLookup[edge];
            if (!_compositionDataLookup.HasComponent(composition.m_Edge))
            {
                return false;
            }

            const CompositionState carriesTraffic =
                CompositionState.HasForwardRoadLanes | CompositionState.HasBackwardRoadLanes
                | CompositionState.HasForwardTrackLanes | CompositionState.HasBackwardTrackLanes
                | CompositionState.HasPedestrianLanes | CompositionState.HasSurface;

            var data = _compositionDataLookup[composition.m_Edge];
            return (data.m_State & carriesTraffic) != 0;
        }

        public NetworkKind GetKind(Entity edge)
        {
            if (!_compositionLookup.HasComponent(edge))
            {
                return NetworkKind.None;
            }
            var composition = _compositionLookup[edge];
            if (!_compositionDataLookup.HasComponent(composition.m_Edge))
            {
                return NetworkKind.None;
            }

            var state = _compositionDataLookup[composition.m_Edge].m_State;
            if ((state & (CompositionState.HasForwardRoadLanes | CompositionState.HasBackwardRoadLanes)) != 0)
            {
                return NetworkKind.Road;
            }
            if ((state & (CompositionState.HasForwardTrackLanes | CompositionState.HasBackwardTrackLanes)) != 0)
            {
                return NetworkKind.Track;
            }
            if ((state & CompositionState.HasPedestrianLanes) != 0)
            {
                return NetworkKind.Pedestrian;
            }
            if ((state & CompositionState.HasSurface) != 0)
            {
                return NetworkKind.Surface;
            }
            return NetworkKind.None;
        }
    }
}
