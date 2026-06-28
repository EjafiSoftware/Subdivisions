using System.Collections.Generic;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>
    /// The outcome of <see cref="BorderTracer.Trace"/>. <see cref="Ring"/> is a view over the
    /// tracer's pooled buffer and is only valid until the next call to <c>Trace</c> on the same
    /// <see cref="BorderTracer"/>; copy it if you need to keep it. Empty when not valid.
    /// </summary>
    public readonly struct TraceResult
    {
        public IReadOnlyList<float3> Ring { get; }
        public bool IsValid { get; }

        public TraceResult(IReadOnlyList<float3> ring, bool isValid)
        {
            Ring = ring;
            IsValid = isValid;
        }
    }
}
