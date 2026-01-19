namespace SkiGame.Progression
{
    public enum ProgressionMetric
    {
        // Session stats
        SessionDistanceMeters,
        SessionTopSpeedMps,
        SessionAirTimeSeconds,
        SessionAirDistanceMeters,
        SessionVerticalDescentMeters,
        SessionStacks,
        SessionRunsCompleted,
        SessionLiftsUsed,

        // NEW: Session POI + Grind
        SessionPlacesVisited,
        SessionGrindTimeSeconds,
        SessionGrindDistanceMeters,

        // Lifetime stats
        LifetimeDistanceMeters,
        LifetimeTopSpeedMps,
        LifetimeAirTimeSeconds,
        LifetimeAirDistanceMeters,
        LifetimeVerticalDescentMeters,
        LifetimeStacks,
        LifetimeRunsCompleted,
        LifetimeLiftsUsed,

        // NEW: Lifetime POI + Grind
        LifetimePlacesVisited,
        LifetimeGrindTimeSeconds,
        LifetimeGrindDistanceMeters,
    }
}
