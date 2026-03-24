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
            VisitRunsInGroup = 20,
        }

        public enum VisitScope
        {
            Session = 0,
            Lifetime = 10,
        }

        public enum MountainHudCategory
        {
            Auto = 0,
            Performance = 10,
            Exploration = 20,
            MountainMastery = 30,
        }

        [Header("Identity")]
        public string id;
        public string title;

        [TextArea] public string description;

        [Header("HUD Presentation")]
        [Tooltip("Auto infers the Mountain HUD tab from the requirement kind / metric. Use an explicit value to override.")]
        public MountainHudCategory mountainHudCategory = MountainHudCategory.Auto;

        [Tooltip("Optional short glyph shown on the Mountain HUD card. Leave blank to use the automatic glyph.")]
        public string mountainHudGlyph;

        [Header("Requirement")]
        public RequirementKind requirementKind = RequirementKind.Metric;

        [Header("Requirement: Metric")]
        public ProgressionMetric metric;
        public float target = 1f;

        [Header("Requirement: Run Target (Optional)")]
        [Tooltip("If set, run-specific metrics evaluate against this run id (SkiRunLine.RunId).")]
        public string runId;

        [Header("Requirement: POI Group Visit")]
        public POIGroupDefinitionSO poiGroup;

        [Tooltip("Should POI completion be measured against sessionVisitedLandmarkIds or visitedLandmarkIds.")]
        public VisitScope poiVisitScope = VisitScope.Lifetime;

        [Tooltip("If enabled, target is kept equal to poiGroup.Count in the editor.")]
        public bool autoTargetFromPOIGroup = true;

        public enum RunGroupMeasure
        {
            Visited = 0,
            Completed = 10,
        }

        [Header("Requirement: Run Group")]
        public RunGroupDefinitionSO runGroup;

        [Tooltip("Should Run completion/visitation be measured against session lists or lifetime lists/records.")]
        public VisitScope runVisitScope = VisitScope.Lifetime;

        public RunGroupMeasure runGroupMeasure = RunGroupMeasure.Completed;

        [Tooltip("If enabled, target is kept equal to runGroup.Count in the editor.")]
        public bool autoTargetFromRunGroup = true;

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

            if (requirementKind == RequirementKind.VisitRunsInGroup)
            {
                if (runGroup == null || runGroup.Count <= 0) return 0f;

                var ids = runGroup.RunIds;
                if (ids == null || ids.Count == 0) return 0f;

                int count = 0;

                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (string.IsNullOrEmpty(id)) continue;

                    if (runGroupMeasure == RunGroupMeasure.Visited)
                    {
                        if (runVisitScope == VisitScope.Session)
                        {
                            if (profile.sessionVisitedRunIds != null && profile.sessionVisitedRunIds.Contains(id))
                                count++;
                        }
                        else
                        {
                            if (profile.visitedRunIds != null && profile.visitedRunIds.Contains(id))
                                count++;
                        }
                    }
                    else // Completed
                    {
                        if (runVisitScope == VisitScope.Session)
                        {
                            if (profile.sessionCompletedRunIds != null && profile.sessionCompletedRunIds.Contains(id))
                                count++;
                        }
                        else
                        {
                            if (profile.HasCompletedRunEver(id))
                                count++;
                        }
                    }
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

                ProgressionMetric.SessionRunsVisited => profile.sessionVisitedRunIds != null ? profile.sessionVisitedRunIds.Count : 0,
                ProgressionMetric.SessionRunsCompletedClean => s.runsCompletedClean,
                ProgressionMetric.SessionTopRunSpeedMps => s.topRunSpeedMps,

                ProgressionMetric.LifetimeRunsVisited => profile.visitedRunIds != null ? profile.visitedRunIds.Count : 0,
                ProgressionMetric.LifetimeRunsCompletedClean => l.totalRunsCompletedClean,
                ProgressionMetric.LifetimeTopRunSpeedMps => l.topRunSpeedMps,

                ProgressionMetric.LifetimeRunVisited => (!string.IsNullOrEmpty(runId) && profile.visitedRunIds != null && profile.visitedRunIds.Contains(runId)) ? 1f : 0f,
                ProgressionMetric.LifetimeRunCompletedCount => ReadRunRecordValue(profile, runId, cleanOnly: false),
                ProgressionMetric.LifetimeRunCompletedCleanCount => ReadRunRecordValue(profile, runId, cleanOnly: true),


                _ => 0f
            };
        }

        public string GetRequirementText()
        {
            // Keep this intentionally short + UI-friendly.
            // (PhoneHUD can still override formatting if needed.)

            if (requirementKind == RequirementKind.VisitPOIsInGroup)
                return $"Visit {Mathf.FloorToInt(target)} places";

            if (requirementKind == RequirementKind.VisitRunsInGroup)
            {
                string verb = (runGroupMeasure == RunGroupMeasure.Completed) ? "Complete" : "Visit";
                return $"{verb} {Mathf.FloorToInt(target)} runs";
            }

            return metric switch
            {
                ProgressionMetric.SessionDistanceMeters or
                ProgressionMetric.LifetimeDistanceMeters or
                ProgressionMetric.SessionAirDistanceMeters or
                ProgressionMetric.LifetimeAirDistanceMeters or
                ProgressionMetric.SessionVerticalDescentMeters or
                ProgressionMetric.LifetimeVerticalDescentMeters
                    => $"Reach {FormatMeters(target)}",

                ProgressionMetric.SessionTopSpeedMps or
                ProgressionMetric.LifetimeTopSpeedMps
                    => $"Reach {target:0.0} m/s",

                ProgressionMetric.SessionAirTimeSeconds or
                ProgressionMetric.LifetimeAirTimeSeconds
                    => $"Reach {target:0.0} s",

                ProgressionMetric.SessionGrindTimeSeconds or
                ProgressionMetric.LifetimeGrindTimeSeconds
                    => $"Reach {target:0.0} s",

                ProgressionMetric.SessionGrindDistanceMeters or
                ProgressionMetric.LifetimeGrindDistanceMeters
                    => $"Reach {FormatMeters(target)}",

                ProgressionMetric.SessionRunsCompleted or
                ProgressionMetric.LifetimeRunsCompleted
                    => $"Complete {Mathf.FloorToInt(target)} runs",

                ProgressionMetric.SessionLiftsUsed or
                ProgressionMetric.LifetimeLiftsUsed
                    => $"Ride {Mathf.FloorToInt(target)} lifts",

                ProgressionMetric.SessionRunsVisited => $"Visit {Mathf.FloorToInt(target)} runs",
                ProgressionMetric.LifetimeRunsVisited => $"Visit {Mathf.FloorToInt(target)} runs",

                ProgressionMetric.SessionRunsCompletedClean => $"Complete {Mathf.FloorToInt(target)} clean runs",
                ProgressionMetric.LifetimeRunsCompletedClean => $"Complete {Mathf.FloorToInt(target)} clean runs",

                ProgressionMetric.SessionTopRunSpeedMps or
                ProgressionMetric.LifetimeTopRunSpeedMps
                    => $"Reach {target:0.0} m/s",

                ProgressionMetric.LifetimeRunVisited
                    => string.IsNullOrEmpty(runId) ? $"Visit run" : $"Visit {runId}",

                ProgressionMetric.LifetimeRunCompletedCount
                    => string.IsNullOrEmpty(runId) ? $"Complete {Mathf.FloorToInt(target)} runs" : $"Complete {runId} x{Mathf.FloorToInt(target)}",

                ProgressionMetric.LifetimeRunCompletedCleanCount
                    => string.IsNullOrEmpty(runId) ? $"Complete {Mathf.FloorToInt(target)} clean runs" : $"Clean-complete {runId} x{Mathf.FloorToInt(target)}",

                _ => $"Target {target:0.##}"
            };
        }

        public MountainHudCategory GetMountainHudCategory()
        {
            if (mountainHudCategory != MountainHudCategory.Auto)
                return mountainHudCategory;

            if (requirementKind == RequirementKind.VisitPOIsInGroup)
                return MountainHudCategory.Exploration;

            if (requirementKind == RequirementKind.VisitRunsInGroup)
                return MountainHudCategory.MountainMastery;

            if (IsExplorationMetric(metric))
                return MountainHudCategory.Exploration;

            if (IsMasteryMetric(metric))
                return MountainHudCategory.MountainMastery;

            return MountainHudCategory.Performance;
        }

        public string GetMountainHudGlyph()
        {
            if (!string.IsNullOrWhiteSpace(mountainHudGlyph))
                return mountainHudGlyph;

            return GetMountainHudCategory() switch
            {
                MountainHudCategory.Exploration => "◎",
                MountainHudCategory.MountainMastery => "▲",
                _ => "★"
            };
        }

        public float GetProgress01(PlayerStatsProfile profile)
        {
            float current = ReadCurrent(profile);
            float goal = Mathf.Max(0.0001f, target);
            return Mathf.Clamp01(current / goal);
        }

        public string GetProgressText(PlayerStatsProfile profile)
        {
            float current = Mathf.Max(0f, ReadCurrent(profile));
            float goal = Mathf.Max(0.0001f, target);

            if (requirementKind == RequirementKind.VisitPOIsInGroup ||
                requirementKind == RequirementKind.VisitRunsInGroup)
            {
                return $"{Mathf.FloorToInt(current)}/{Mathf.FloorToInt(goal)}";
            }

            return metric switch
            {
                ProgressionMetric.SessionDistanceMeters or
                ProgressionMetric.LifetimeDistanceMeters or
                ProgressionMetric.SessionAirDistanceMeters or
                ProgressionMetric.LifetimeAirDistanceMeters or
                ProgressionMetric.SessionVerticalDescentMeters or
                ProgressionMetric.LifetimeVerticalDescentMeters or
                ProgressionMetric.SessionGrindDistanceMeters or
                ProgressionMetric.LifetimeGrindDistanceMeters
                    => $"{FormatMeters(current)} / {FormatMeters(goal)}",

                ProgressionMetric.SessionTopSpeedMps or
                ProgressionMetric.LifetimeTopSpeedMps or
                ProgressionMetric.SessionTopRunSpeedMps or
                ProgressionMetric.LifetimeTopRunSpeedMps
                    => $"{current:0.0} / {goal:0.0} m/s",

                ProgressionMetric.SessionAirTimeSeconds or
                ProgressionMetric.LifetimeAirTimeSeconds or
                ProgressionMetric.SessionGrindTimeSeconds or
                ProgressionMetric.LifetimeGrindTimeSeconds
                    => $"{current:0.0} / {goal:0.0} s",

                _ => $"{Mathf.FloorToInt(current)}/{Mathf.FloorToInt(goal)}"
            };
        }

        private static bool IsExplorationMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.SessionPlacesVisited ||
                   metric == ProgressionMetric.LifetimePlacesVisited ||
                   metric == ProgressionMetric.SessionLiftsUsed ||
                   metric == ProgressionMetric.LifetimeLiftsUsed ||
                   metric == ProgressionMetric.SessionRunsVisited ||
                   metric == ProgressionMetric.LifetimeRunsVisited;
        }

        private static bool IsMasteryMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.SessionRunsCompleted ||
                   metric == ProgressionMetric.LifetimeRunsCompleted ||
                   metric == ProgressionMetric.SessionRunsCompletedClean ||
                   metric == ProgressionMetric.LifetimeRunsCompletedClean ||
                   metric == ProgressionMetric.SessionTopRunSpeedMps ||
                   metric == ProgressionMetric.LifetimeTopRunSpeedMps ||
                   metric == ProgressionMetric.LifetimeRunVisited ||
                   metric == ProgressionMetric.LifetimeRunCompletedCount ||
                   metric == ProgressionMetric.LifetimeRunCompletedCleanCount;
        }

        private static string FormatMeters(float meters)
        {
            if (meters >= 1000f) return $"{meters / 1000f:0.0}km";
            return $"{meters:0}m";
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            // POI group auto-target
            if (autoTargetFromPOIGroup && requirementKind == RequirementKind.VisitPOIsInGroup && poiGroup != null)
            {
                target = Mathf.Max(1f, poiGroup.Count);
            }

            // Run group auto-target
            if (autoTargetFromRunGroup && requirementKind == RequirementKind.VisitRunsInGroup && runGroup != null)
            {
                target = Mathf.Max(1f, runGroup.Count);
            }
        }
#endif

    }
}
