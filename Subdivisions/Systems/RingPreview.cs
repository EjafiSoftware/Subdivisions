using System;
using Game.Prefabs;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Caches the traced border ring and drives its rebuild lifecycle.
    /// </summary>
    internal sealed class RingPreview : IDisposable
    {
        private const float HoverRebuildEpsilon = 0.5f;

        private readonly DistrictBuilder _builder;

        private NativeList<float3> _ring;
        private NativeReference<bool> _ringValid;

        private SnapPoint _lastBuiltHover;
        private bool _lastIncludeHover;
        private bool _hasBuild;
        private bool _pointsDirty;

        public RingPreview(DistrictBuilder builder)
        {
            _builder = builder;
            _ring = new NativeList<float3>(Allocator.Persistent);
            _ringValid = new NativeReference<bool>(Allocator.Persistent);
        }

        public bool IsValid => _ringValid.Value;

        public void MarkPointsDirty() => _pointsDirty = true;

        public void Reset()
        {
            _hasBuild = false;
            _pointsDirty = true;
        }

        public JobHandle Update(ControlPointRing points, SnapPoint hover, bool includeHover, DistrictPrefab prefab, JobHandle inputDeps)
        {
            if (points.Count + (includeHover ? 1 : 0) < 3)
            {
                _hasBuild = false;
                return inputDeps;
            }

            var handle = inputDeps;
            if (NeedsRebuild(hover, includeHover))
            {
                handle = ScheduleBuild(points, hover, includeHover, inputDeps);
                _lastBuiltHover = hover;
                _lastIncludeHover = includeHover;
                _hasBuild = true;
                _pointsDirty = false;
            }

            return _builder.CreateDistrict(_ring, prefab, handle);
        }

        private JobHandle ScheduleBuild(ControlPointRing points, SnapPoint hover, bool includeHover, JobHandle inputDeps)
        {
            var list = new NativeList<SnapPoint>(Allocator.TempJob);
            for (var i = 0; i < points.Count; i++)
            {
                list.Add(points[i]);
            }
            if (includeHover)
            {
                list.Add(hover);
            }

            var handle = _builder.BuildRing(list, _ring, _ringValid, inputDeps);
            list.Dispose(handle);
            return handle;
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
            return hover._edge != _lastBuiltHover._edge
                || math.distance(hover._position.xz, _lastBuiltHover._position.xz) > HoverRebuildEpsilon;
        }

        public void Dispose()
        {
            if (_ring.IsCreated)
            {
                _ring.Dispose();
            }
            if (_ringValid.IsCreated)
            {
                _ringValid.Dispose();
            }
        }
    }
}
