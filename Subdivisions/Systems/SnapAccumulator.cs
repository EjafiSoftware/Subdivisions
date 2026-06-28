using Subdivisions.Core;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Tracks the nearest snap candidate seen so far.
    /// </summary>
    internal struct SnapAccumulator
    {
        private float _best;

        public bool Found { get; private set; }

        public SnapPoint Result { get; private set; }

        public SnapAccumulator(float maxDistance, SnapPoint fallback)
        {
            _best = maxDistance;
            Result = fallback;
            Found = false;
        }

        public void Consider(SnapPoint candidate, float distance)
        {
            if (!(distance < _best))
            {
                return;
            }

            _best = distance;
            Result = candidate;
            Found = true;
        }
    }
}
