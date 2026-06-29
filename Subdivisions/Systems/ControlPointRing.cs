using System;
using Subdivisions.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Owns the ordered list of boundary control points the user has dropped, and the
    /// close-detection geometry. Holds the persistent native list and its lifetime.
    /// </summary>
    internal sealed class ControlPointRing : IDisposable
    {
        private NativeList<SnapPoint> _points = new(Allocator.Persistent);

        public int Count => _points.Length;

        public SnapPoint this[int index] => _points[index];

        /// <summary>The backing list, for read-only iteration by the renderer and preview.</summary>
        public NativeList<SnapPoint> Points => _points;

        public void Add(SnapPoint point) => _points.Add(point);

        public void Clear() => _points.Clear();

        /// <summary>Removes the most recently added point; returns whether one was removed.</summary>
        public bool RemoveLast()
        {
            if (_points.Length == 0)
            {
                return false;
            }
            _points.RemoveAt(_points.Length - 1);
            return true;
        }

        public bool IsNearStart(float3 position, float radius)
        {
            return _points.Length > 0
                && math.distance(position.xz, _points[0].Position.xz) < radius;
        }

        public bool CanClose(float3 position, float radius)
        {
            return _points.Length >= 3 && IsNearStart(position, radius);
        }

        public void Dispose()
        {
            if (_points.IsCreated)
            {
                _points.Dispose();
            }
        }
    }
}
