using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Task Definition", fileName = "Task_")]
    public sealed class TaskDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;
        [Tooltip("Short display name for Watch/Tile UI.")]
        public string shortTitle;

        [Header("Goal")]
        public ProgressionMetric metric;
        public float target = 100f;

        [Header("Optional Run Target")]
        [Tooltip("If set, run-specific metrics evaluate against this run id (SkiRunLine.RunId).")]
        public string runId;

        [Header("Reward")]
        [Tooltip("Currency granted when this task is turned in. If 0, reward will be derived automatically.")]
        public int reward = 0;

        [Header("Display")]
        [TextArea] public string description;

        public float ReadCurrent(PlayerStatsProfile profile)
        {
            if (profile == null) return 0f;

            var s = profile.session;
            var l = profile.lifetime;

            return metric switch
            {
                // --- Session stats ---
                ProgressionMetric.SessionDistanceMeters => s.distanceMeters,
                ProgressionMetric.SessionTopSpeedMps => s.topSpeedMps,
                ProgressionMetric.SessionAirTimeSeconds => s.airTimeSeconds,
                ProgressionMetric.SessionAirDistanceMeters => s.airDistanceMeters,
                ProgressionMetric.SessionVerticalDescentMeters => s.verticalDescentMeters,
                ProgressionMetric.SessionStacks => s.stacks,
                ProgressionMetric.SessionRunsCompleted => s.runsCompleted,
                ProgressionMetric.SessionLiftsUsed => s.liftsUsed,

                // --- Session POI + Grind ---
                ProgressionMetric.SessionPlacesVisited => profile.sessionVisitedLandmarkIds != null ? profile.sessionVisitedLandmarkIds.Count : 0,
                ProgressionMetric.SessionGrindTimeSeconds => s.grindTimeSeconds,
                ProgressionMetric.SessionGrindDistanceMeters => s.grindDistanceMeters,

                // --- Session Run Progress ---
                ProgressionMetric.SessionRunsVisited => profile.sessionVisitedRunIds != null ? profile.sessionVisitedRunIds.Count : 0,
                ProgressionMetric.SessionRunsCompletedClean => s.runsCompletedClean,
                ProgressionMetric.SessionTopRunSpeedMps => s.topRunSpeedMps,

                // --- Lifetime stats ---
                ProgressionMetric.LifetimeDistanceMeters => l.totalDistanceMeters,
                ProgressionMetric.LifetimeTopSpeedMps => l.topSpeedMps,
                ProgressionMetric.LifetimeAirTimeSeconds => l.totalAirTimeSeconds,
                ProgressionMetric.LifetimeAirDistanceMeters => l.totalAirDistanceMeters,
                ProgressionMetric.LifetimeVerticalDescentMeters => l.totalVerticalDescentMeters,
                ProgressionMetric.LifetimeStacks => l.totalStacks,
                ProgressionMetric.LifetimeRunsCompleted => l.totalRunsCompleted,
                ProgressionMetric.LifetimeLiftsUsed => l.totalLiftsUsed,

                // --- Lifetime POI + Grind ---
                ProgressionMetric.LifetimePlacesVisited => profile.visitedLandmarkIds != null ? profile.visitedLandmarkIds.Count : 0,
                ProgressionMetric.LifetimeGrindTimeSeconds => l.totalGrindTimeSeconds,
                ProgressionMetric.LifetimeGrindDistanceMeters => l.totalGrindDistanceMeters,

                // --- Lifetime Run Progress ---
                ProgressionMetric.LifetimeRunsVisited => profile.visitedRunIds != null ? profile.visitedRunIds.Count : 0,
                ProgressionMetric.LifetimeRunsCompletedClean => l.totalRunsCompletedClean,
                ProgressionMetric.LifetimeTopRunSpeedMps => l.topRunSpeedMps,

                // --- Run-specific (requires runId) ---
                ProgressionMetric.LifetimeRunVisited => (!string.IsNullOrEmpty(runId) && profile.visitedRunIds != null && profile.visitedRunIds.Contains(runId)) ? 1f : 0f,

                ProgressionMetric.LifetimeRunCompletedCount => ReadRunRecordValue(profile, runId, cleanOnly: false),
                ProgressionMetric.LifetimeRunCompletedCleanCount => ReadRunRecordValue(profile, runId, cleanOnly: true),

                _ => 0f
            };
        }

        private static float ReadRunRecordValue(PlayerStatsProfile profile, string runId, bool cleanOnly)
        {
            if (profile == null || string.IsNullOrEmpty(runId) || profile.runRecords == null) return 0f;

            for (int i = 0; i < profile.runRecords.Count; i++)
            {
                var rr = profile.runRecords[i];
                if (rr == null) continue;
                if (rr.runId != runId) continue;

                return cleanOnly ? rr.timesCompletedClean : rr.timesCompleted;
            }

            return 0f;
        }

    }
}
