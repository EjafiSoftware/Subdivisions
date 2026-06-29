namespace Subdivisions.Core
{
    /// <summary>
    /// Tracks the nearest snap candidate seen so far, resolving a coincident pair by
    /// preference. Keeps the nearest candidate of any kind and the nearest matching the
    /// preference separately, so the outcome does not depend on the order candidates arrive:
    /// the preferred kind wins only when it sits within the coincidence radius of the nearest.
    /// </summary>
    public struct SnapAccumulator
    {
        private readonly float _max;
        private readonly SnapPoint _fallback;
        private readonly SnapPreference _preference;
        private readonly float _coincidence;

        private SnapPoint _bestAny;
        private float _bestAnyDistance;
        private bool _anyFound;

        private SnapPoint _bestPreferred;
        private float _bestPreferredDistance;
        private bool _preferredFound;

        public SnapAccumulator(float maxDistance, SnapPoint fallback, SnapPreference preference, float coincidenceRadius)
        {
            _max = maxDistance;
            _fallback = fallback;
            _preference = preference;
            _coincidence = coincidenceRadius;
            _bestAny = fallback;
            _bestAnyDistance = 0f;
            _anyFound = false;
            _bestPreferred = fallback;
            _bestPreferredDistance = 0f;
            _preferredFound = false;
        }

        public bool Found => _anyFound;

        public SnapPoint Result
        {
            get
            {
                if (!_anyFound)
                {
                    return _fallback;
                }
                if (_preferredFound && _bestPreferredDistance <= _bestAnyDistance + _coincidence)
                {
                    return _bestPreferred;
                }
                return _bestAny;
            }
        }

        public void Consider(SnapPoint candidate, float distance)
        {
            if (distance >= _max)
            {
                return;
            }

            if (!_anyFound || distance < _bestAnyDistance)
            {
                _bestAny = candidate;
                _bestAnyDistance = distance;
                _anyFound = true;
            }

            if (Matches(candidate) && (!_preferredFound || distance < _bestPreferredDistance))
            {
                _bestPreferred = candidate;
                _bestPreferredDistance = distance;
                _preferredFound = true;
            }
        }

        private bool Matches(SnapPoint candidate)
        {
            return _preference == SnapPreference.Net ? candidate.OnNet : candidate.OnArea;
        }
    }
}
