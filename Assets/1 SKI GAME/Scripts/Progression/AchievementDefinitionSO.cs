using UnityEngine;
using SkiGame.POI;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Achievement Definition", fileName = "Ach_")]
    public sealed class AchievementDefinitionSO : ScriptableObject
    {
        public enum RequirementKind
        {
            Metric = 0,
            VisitPOIsInGroup = 10,
        }

        public enum VisitScope
        {
            Session = 0,
            Lifetime = 10,
        }

        [Header("Identity")]
        public string id;
        public string title;

        [TextArea] public string description;

        [Header("Requirement")]
        public RequirementKind requirementKind = RequirementKind.Metric;

        [Header("Requirement: Metric")]
        public ProgressionMetric metric;
        public float target = 1f;

        [Header("Requirement: POI Group Visit")]
        public POIGroupDefinitionSO poiGroup;

        [Tooltip("Should POI completion be measured against sessionVisitedLandmarkIds or visitedLandmarkIds.")]
        public VisitScope poiVisitScope = VisitScope.Lifetime;

        [Tooltip("If enabled, target is kept equal to poiGroup.Count in the editor.")]
        public bool autoTargetFromPOIGroup = true;

        public float ReadCurrent(PlayerStatsProfile profile)
        {
            if (profile == null) return 0f;

            var s = profile.session;
            var l = profile.lifetime;

            if (requirementKind == RequirementKind.VisitPOIsInGroup)
            {
                if (poiGroup == null || poiGroup.Count <= 0) return 0f;

                var ids = poiGroup.POIIds;
                if (ids == null || ids.Count == 0) return 0f;

                int count = 0;

                if (poiVisitScope == VisitScope.Session)
                {
                    var visited = profile.sessionVisitedLandmarkIds;
                    if (visited == null) return 0f;

                    for (int i = 0; i < ids.Count; i++)
                        if (visited.Contains(ids[i])) count++;
                }
                else
                {
                    var visited = profile.visitedLandmarkIds;
                    if (visited == null) return 0f;

                    for (int i = 0; i < ids.Count; i++)
                        if (visited.Contains(ids[i])) count++;
                }

                return count;
            }

            // Default: Metric
            return metric switch
            {
                // Session stats
                ProgressionMetric.SessionDistanceMeters => s.distanceMeters,
                ProgressionMetric.SessionTopSpeedMps => s.topSpeedMps,
                ProgressionMetric.SessionAirTimeSeconds => s.airTimeSeconds,
                ProgressionMetric.SessionAirDistanceMeters => s.airDistanceMeters,
                ProgressionMetric.SessionVerticalDescentMeters => s.verticalDescentMeters,
                ProgressionMetric.SessionStacks => s.stacks,
                ProgressionMetric.SessionRunsCompleted => s.runsCompleted,
                ProgressionMetric.SessionLiftsUsed => s.liftsUsed,

                // NEW: Session POI + Grind
                ProgressionMetric.SessionPlacesVisited => profile.sessionVisitedLandmarkIds != null ? profile.sessionVisitedLandmarkIds.Count : 0,
                ProgressionMetric.SessionGrindTimeSeconds => s.grindTimeSeconds,
                ProgressionMetric.SessionGrindDistanceMeters => s.grindDistanceMeters,

                // Lifetime stats
                ProgressionMetric.LifetimeDistanceMeters => l.totalDistanceMeters,
                ProgressionMetric.LifetimeTopSpeedMps => l.topSpeedMps,
                ProgressionMetric.LifetimeAirTimeSeconds => l.totalAirTimeSeconds,
                ProgressionMetric.LifetimeAirDistanceMeters => l.totalAirDistanceMeters,
                ProgressionMetric.LifetimeVerticalDescentMeters => l.totalVerticalDescentMeters,
                ProgressionMetric.LifetimeStacks => l.totalStacks,
                ProgressionMetric.LifetimeRunsCompleted => l.totalRunsCompleted,
                ProgressionMetric.LifetimeLiftsUsed => l.totalLiftsUsed,

                // NEW: Lifetime POI + Grind
                ProgressionMetric.LifetimePlacesVisited => profile.visitedLandmarkIds != null ? profile.visitedLandmarkIds.Count : 0,
                ProgressionMetric.LifetimeGrindTimeSeconds => l.totalGrindTimeSeconds,
                ProgressionMetric.LifetimeGrindDistanceMeters => l.totalGrindDistanceMeters,

                _ => 0f
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!autoTargetFromPOIGroup) return;
            if (requirementKind != RequirementKind.VisitPOIsInGroup) return;
            if (poiGroup == null) return;

            target = Mathf.Max(1f, poiGroup.Count);
        }
#endif
    }
}
