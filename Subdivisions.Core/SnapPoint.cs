using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    public struct SnapPoint
    {
        public float3 Position;
        public Entity Edge;
        public float CurveParameter;

        /// <summary>Snapped onto an existing district boundary or map-tile border (a free point, but not arbitrary terrain).</summary>
        public bool OnArea;

        public bool OnNet => Edge != Entity.Null;
    }
}
