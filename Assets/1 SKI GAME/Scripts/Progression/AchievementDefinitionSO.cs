using System.Collections.Generic;
using SkiGame.POI;
using UnityEngine;

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
            CompleteQuest = 30,
            OwnPass = 40,
            CompleteRace = 50,
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

        [Header("Requirement: Specific Content")]
        public string questId;
        public string passId;
        public string raceId;

        [Header("Rewards")]
        [Min(0)] public int currencyReward = 0;

        [Tooltip("If >= 0, grant at least this ski pass level when the reward is claimed.")]
        public int skiPassLevelReward = -1;

        [Tooltip("Customization items unlocked when the reward is claimed.")]
        public List<CustomizationOptionSO> customizationUnlocks = new();

        [Tooltip("If enabled, this reward is claimed immediately when the achievement completes.")]
        public bool autoClaimRewardOnUnlock = false;

        public float ReadCurrent(PlayerStatsProfile profile)
        {
            if (profile == null) return 0f;

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

            if (requirementKind == RequirementKind.CompleteQuest)
                return ProgressionMetricUtility.HasCompletedQuest(profile, questId) ? 1f : 0f;

            if (requirementKind == RequirementKind.OwnPass)
                return ProgressionMetricUtility.HasOwnedPass(profile, passId) ? 1f : 0f;

            if (requirementKind == RequirementKind.CompleteRace)
                return ProgressionMetricUtility.HasCompletedRace(profile, raceId) ? 1f : 0f;


            // Default: Metric
            return ProgressionMetricUtility.ReadMetricValue(profile, metric, runId);
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

            if (requirementKind == RequirementKind.CompleteQuest)
                return string.IsNullOrWhiteSpace(questId) ? "Complete quest" : $"Complete {questId}";

            if (requirementKind == RequirementKind.OwnPass)
                return string.IsNullOrWhiteSpace(passId) ? "Own pass" : $"Own {passId}";

            if (requirementKind == RequirementKind.CompleteRace)
                return string.IsNullOrWhiteSpace(raceId) ? "Complete race" : $"Complete {raceId}";

            return ProgressionMetricUtility.BuildRequirementText(metric, target, runId);
        }

        public MountainHudCategory GetMountainHudCategory()
        {
            if (mountainHudCategory != MountainHudCategory.Auto)
                return mountainHudCategory;

            if (requirementKind == RequirementKind.VisitPOIsInGroup)
                return MountainHudCategory.Exploration;

            if (requirementKind == RequirementKind.VisitRunsInGroup)
                return MountainHudCategory.MountainMastery;

            if (requirementKind == RequirementKind.CompleteQuest ||
                requirementKind == RequirementKind.OwnPass ||
                requirementKind == RequirementKind.CompleteRace)
            {
                return MountainHudCategory.MountainMastery;
            }

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
                requirementKind == RequirementKind.VisitRunsInGroup ||
                requirementKind == RequirementKind.CompleteQuest ||
                requirementKind == RequirementKind.OwnPass ||
                requirementKind == RequirementKind.CompleteRace)
            {
                return $"{Mathf.FloorToInt(current)}/{Mathf.FloorToInt(goal)}";
            }

            return ProgressionMetricUtility.BuildProgressText(metric, current, goal);
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
                   metric == ProgressionMetric.LifetimeRunCompletedCleanCount ||
                   metric == ProgressionMetric.LifetimeRaceStarts ||
                   metric == ProgressionMetric.LifetimeRaceCompletions ||
                   metric == ProgressionMetric.LifetimeRaceWins ||
                   metric == ProgressionMetric.LifetimeRacePodiums ||
                   metric == ProgressionMetric.LifetimeUniqueRacesCompleted ||
                   metric == ProgressionMetric.LifetimeRaceChampionshipsCompleted ||
                   metric == ProgressionMetric.LifetimeRacePersonalBestImprovements ||
                   metric == ProgressionMetric.LifetimeRescueStarts ||
                   metric == ProgressionMetric.LifetimeRescueCompletions ||
                   metric == ProgressionMetric.LifetimeRescueFailures ||
                   metric == ProgressionMetric.LifetimeRescueRank ||
                   metric == ProgressionMetric.LifetimeRescueUtilityUses ||
                   metric == ProgressionMetric.LifetimeQuestsAccepted ||
                   metric == ProgressionMetric.LifetimeQuestsCompleted ||
                   metric == ProgressionMetric.LifetimeQuestStagesCompleted ||
                   metric == ProgressionMetric.LifetimeTutorialQuestsCompleted ||
                   metric == ProgressionMetric.SessionTricksLanded ||
                   metric == ProgressionMetric.LifetimeTricksLanded ||
                   metric == ProgressionMetric.LifetimeNamedTricksLanded ||
                   metric == ProgressionMetric.LifetimeUniqueTrickNamesLanded ||
                   metric == ProgressionMetric.LifetimeTrickFails ||
                   metric == ProgressionMetric.LifetimeBestTrickTier ||
                   metric == ProgressionMetric.LifetimePassesPurchased ||
                   metric == ProgressionMetric.LifetimePassExtensions ||
                   metric == ProgressionMetric.LifetimePermanentPassesUnlocked ||
                   metric == ProgressionMetric.LifetimeUniquePassesOwned ||
                   metric == ProgressionMetric.LifetimeCustomizationPurchases ||
                   metric == ProgressionMetric.LifetimeCustomizationUnlocks ||
                   metric == ProgressionMetric.LifetimeCurrencyEarned ||
                   metric == ProgressionMetric.LifetimeCurrencySpent ||
                   metric == ProgressionMetric.LargestSingleCurrencyReward;
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

        public string GetRewardSummary()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (currencyReward > 0)
                parts.Add($"${currencyReward}");

            if (skiPassLevelReward >= 0)
                parts.Add($"Pass L{skiPassLevelReward}");

            if (customizationUnlocks != null)
            {
                for (int i = 0; i < customizationUnlocks.Count; i++)
                {
                    var opt = customizationUnlocks[i];
                    if (opt == null) continue;
                    parts.Add(string.IsNullOrWhiteSpace(opt.displayName) ? opt.id : opt.displayName);
                }
            }

            if (parts.Count == 0)
                return "No reward";

            return "Reward: " + string.Join(" • ", parts);
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

            if (requirementKind == RequirementKind.CompleteQuest ||
                requirementKind == RequirementKind.OwnPass ||
                requirementKind == RequirementKind.CompleteRace)
            {
                target = 1f;
            }
        }
#endif

    }
}
