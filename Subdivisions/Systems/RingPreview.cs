using System.Collections.Generic;
using Subdivisions.Core;
using Subdivisions.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Caches the traced border ring and drives its rebuild lifecycle. Traces synchronously on
    /// the main thread (only when the points or snapped hover actually change) and re-emits the
    /// cached ring as a creation definition every frame for the live preview.
    /// </summary>
    internal sealed class RingPreview
    {
        private const float HoverRebuildEpsilon = 0.5f;

        private readonly BorderTracer _tracer = new();
        private readonly List<SnapPoint> _input = new();
        private readonly List<float3> _ring = new();

        private SnapPoint _lastBuiltHover;
        private bool _lastIncludeHover;
        private bool _hasBuild;
        private bool _pointsDirty;
        private bool _valid;

        public bool IsValid => _valid;

        public void MarkPointsDirty() => _pointsDirty = true;

        public void Reset()
        {
            _hasBuild = false;
            _pointsDirty = true;
            _valid = false;
            _ring.Clear();
        }

        public void Update(
            ControlPointRing points,
            SnapPoint hover,
            bool includeHover,
            IBoundaryGraph graph,
            EntityCommandBuffer ecb,
            Entity prefab)
        {
            if (points.Count + (includeHover ? 1 : 0) < 3)
            {
                _hasBuild = false;
                _valid = false;
                return;
            }

            if (NeedsRebuild(hover, includeHover))
            {
                Rebuild(points, hover, includeHover, graph);
                _lastBuiltHover = hover;
                _lastIncludeHover = includeHover;
                _hasBuild = true;
                _pointsDirty = false;
            }

            if (_valid)
            {
                Emit(ecb, prefab);
            }
        }

        private void Rebuild(ControlPointRing points, SnapPoint hover, bool includeHover, IBoundaryGraph graph)
        {
            _input.Clear();
            for (var i = 0; i < points.Count; i++)
            {
                _input.Add(points[i]);
            }
            if (includeHover)
            {
                _input.Add(hover);
            }

            var result = _tracer.Trace(_input, graph);
            _valid = result.IsValid;

            // Copy out: TraceResult.Ring is a view valid only until the next Trace call.
            _ring.Clear();
            if (_valid)
            {
                for (var i = 0; i < result.Ring.Count; i++)
                {
                    _ring.Add(result.Ring[i]);
                }
            }
        }

        private void Emit(EntityCommandBuffer ecb, Entity prefab)
        {
            var entity = ecb.CreateEntity();
            AreaDefinitionCreation.AsDynamicBufferNodes(ecb, entity, _ring);
            AreaDefinitionCreation.WithCreationDefinition(ecb, entity, prefab);
        }

        private bool NeedsRebuild(SnapPoint hover, bool includeHover)
        {
            if (!_hasBuild || _pointsDirty || includeHover != _lastIncludeHover)
            {
                return true;
            }
            if (!includeHover)
            {
                return false;
            }
            return hover.Edge != _lastBuiltHover.Edge
                || math.distance(hover.Position.xz, _lastBuiltHover.Position.xz) > HoverRebuildEpsilon;
        }
    }
}
