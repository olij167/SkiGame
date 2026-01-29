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

        //Session POI + Grind
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

        //Lifetime POI + Grind
        LifetimePlacesVisited,
        LifetimeGrindTimeSeconds,
        LifetimeGrindDistanceMeters,

        //  Daily Task Matrix
        SessionVerticalAscentMeters,
        SessionAverageSpeedMps,

        LifetimeTotalDistanceMeters,
        LifetimeTotalVerticalAscentMeters,
        LifetimeTotalVerticalDescentMeters,
        LifetimeAverageSpeedMps,

        // --- Run Progress (generic + run-specific) ---
        SessionRunsVisited,
        SessionRunsCompletedClean,
        SessionTopRunSpeedMps,

        LifetimeRunsVisited,
        LifetimeRunsCompletedClean,
        LifetimeTopRunSpeedMps,

        // Run-specific (requires Task/Achievement runId field)
        LifetimeRunVisited,
        LifetimeRunCompletedCount,
        LifetimeRunCompletedCleanCount,

    }
}
