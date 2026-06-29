namespace Subdivisions.Core
{
    /// <summary>Stable cursor-snapping configuration: the snap radii, the coincidence radius, and the default preference used when there is no continuity signal.</summary>
    public readonly struct SnapSettings
    {
        public readonly float NodeSnapDistance;
        public readonly float SnapDistance;
        public readonly float CoincidenceRadius;
        public readonly SnapPreference Default;

        public SnapSettings(float nodeSnapDistance, float snapDistance, float coincidenceRadius, SnapPreference preferred)
        {
            NodeSnapDistance = nodeSnapDistance;
            SnapDistance = snapDistance;
            CoincidenceRadius = coincidenceRadius;
            Default = preferred;
        }
    }
}
