using Unity.Mathematics;

namespace Subdivisions.Core
{
    /// <summary>One cursor-frame snap request: where the cursor hit, the previously committed control point (for continuity), and the settings.</summary>
    public readonly struct SnapQuery
    {
        public readonly float3 Hit;
        public readonly SnapPoint? Previous;
        public readonly SnapSettings Settings;

        public SnapQuery(float3 hit, SnapPoint? previous, SnapSettings settings)
        {
            Hit = hit;
            Previous = previous;
            Settings = settings;
        }
    }
}
