using Unity.Entities;

namespace Subdivisions.Core
{
    public readonly struct EdgeEnds
    {
        public Entity Start { get; }
        public Entity End { get; }

        public EdgeEnds(Entity start, Entity end)
        {
            Start = start;
            End = end;
        }
    }
}
