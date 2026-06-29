using Subdivisions.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Tests.Fakes
{
    /// <summary>Snapped and free control points for tests.</summary>
    internal static class SnapPoints
    {
        public static SnapPoint Free(float x, float z)
        {
            return new SnapPoint { Position = new float3(x, 0f, z), Edge = Entity.Null, CurveParameter = 0f };
        }

        public static SnapPoint Net(float x, float z, int edge = 1, float t = 0f)
        {
            return new SnapPoint { Position = new float3(x, 0f, z), Edge = new Entity { Index = edge, Version = 1 }, CurveParameter = t };
        }

        public static SnapPoint Area(float x, float z)
        {
            return new SnapPoint { Position = new float3(x, 0f, z), Edge = Entity.Null, CurveParameter = 0f, OnArea = true };
        }
    }
}
