using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Core
{
    public struct SnapPoint
    {
        public float3 _position;
        public Entity _edge;
        public float _t;

        /// <summary>Snapped onto an existing district boundary or map-tile border (a free point, but not arbitrary terrain).</summary>
        public bool _onArea;

        public bool OnNet => _edge != Entity.Null;
    }
}
