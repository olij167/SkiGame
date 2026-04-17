using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    public static class ProgressionMetricUtility
    {
        public static float ReadMetricValue(PlayerStatsProfile profile, ProgressionMetric metric, string runId = null)
        {
            if (profile == null)
                return 0f;

            profile.Sanitize();

            var s = profile.session;
            var l = profile.lifetime;
            var p = profile.progression;

            switch (metric)
            {
                case ProgressionMetric.SessionDistanceMeters: return s.distanceMeters;
                case ProgressionMetric.SessionTopSpeedMps: return s.topSpeedMps;
                case ProgressionMetric.SessionAirTimeSeconds: return s.airTimeSeconds;
                case ProgressionMetric.SessionAirDistanceMeters: return s.airDistanceMeters;
                case ProgressionMetric.SessionVerticalDescentMeters: return s.verticalDescentMeters;
                case ProgressionMetric.SessionStacks: return s.stacks;
                case ProgressionMetric.SessionRunsCompleted: return s.runsCompleted;
                case ProgressionMetric.SessionLiftsUsed: return s.liftsUsed;
                case ProgressionMetric.SessionPlacesVisited: return profile.sessionVisitedLandmarkIds != null ? profile.sessionVisitedLandmarkIds.Count : 0;
                case ProgressionMetric.SessionGrindTimeSeconds: return s.grindTimeSeconds;
                case ProgressionMetric.SessionGrindDistanceMeters: return s.grindDistanceMeters;
                case ProgressionMetric.SessionVerticalAscentMeters: return s.verticalAscentMeters;
                case ProgressionMetric.SessionAverageSpeedMps: return s.AverageSpeedMps;
                case ProgressionMetric.SessionRunsVisited: return profile.sessionVisitedRunIds != null ? profile.sessionVisitedRunIds.Count : 0;
                case ProgressionMetric.SessionRunsCompletedClean: return s.runsCompletedClean;
                case ProgressionMetric.SessionTopRunSpeedMps: return s.topRunSpeedMps;
                case ProgressionMetric.SessionTricksLanded: return p != null ? p.sessionTricksLanded : 0f;

                case ProgressionMetric.LifetimeDistanceMeters:
                case ProgressionMetric.LifetimeTotalDistanceMeters:
                    return l.totalDistanceMeters;
                case ProgressionMetric.LifetimeTopSpeedMps: return l.topSpeedMps;
                case ProgressionMetric.LifetimeAirTimeSeconds: return l.totalAirTimeSeconds;
                case ProgressionMetric.LifetimeAirDistanceMeters: return l.totalAirDistanceMeters;
                case ProgressionMetric.LifetimeVerticalDescentMeters:
                case ProgressionMetric.LifetimeTotalVerticalDescentMeters:
                    return l.totalVerticalDescentMeters;
                case ProgressionMetric.LifetimeStacks: return l.totalStacks;
                case ProgressionMetric.LifetimeRunsCompleted: return l.totalRunsCompleted;
                case ProgressionMetric.LifetimeLiftsUsed: return l.totalLiftsUsed;
                case ProgressionMetric.LifetimePlacesVisited: return profile.visitedLandmarkIds != null ? profile.visitedLandmarkIds.Count : 0;
                case ProgressionMetric.LifetimeGrindTimeSeconds: return l.totalGrindTimeSeconds;
                case ProgressionMetric.LifetimeGrindDistanceMeters: return l.totalGrindDistanceMeters;
                case ProgressionMetric.LifetimeTotalVerticalAscentMeters: return l.totalVerticalAscentMeters;
                case ProgressionMetric.LifetimeAverageSpeedMps: return l.AverageSpeedMps;
                case ProgressionMetric.LifetimeRunsVisited: return profile.visitedRunIds != null ? profile.visitedRunIds.Count : 0;
                case ProgressionMetric.LifetimeRunsCompletedClean: return l.totalRunsCompletedClean;
                case ProgressionMetric.LifetimeTopRunSpeedMps: return l.topRunSpeedMps;
                case ProgressionMetric.LifetimeRunVisited: return HasId(profile.visitedRunIds, runId) ? 1f : 0f;
                case ProgressionMetric.LifetimeRunCompletedCount: return ReadRunRecordValue(profile, runId, cleanOnly: false);
                case ProgressionMetric.LifetimeRunCompletedCleanCount: return ReadRunRecordValue(profile, runId, cleanOnly: true);

                case ProgressionMetric.LifetimeRaceStarts: return p != null ? p.lifetimeRaceStarts : 0f;
                case ProgressionMetric.LifetimeRaceCompletions: return p != null ? p.lifetimeRaceCompletions : 0f;
                case ProgressionMetric.LifetimeRaceWins: return p != null ? p.lifetimeRaceWins : 0f;
                case ProgressionMetric.LifetimeRacePodiums: return p != null ? p.lifetimeRacePodiums : 0f;
                case ProgressionMetric.LifetimeUniqueRacesCompleted: return p != null && p.completedRaceIds != null ? p.completedRaceIds.Count : 0f;
                case ProgressionMetric.LifetimeRaceChampionshipsCompleted: return profile.completedRaceChampionshipIds != null ? profile.completedRaceChampionshipIds.Count : 0f;
                case ProgressionMetric.LifetimeRacePersonalBestImprovements: return p != null ? p.lifetimeRacePersonalBestImprovements : 0f;

                case ProgressionMetric.LifetimeRescueStarts: return p != null ? p.lifetimeRescueStarts : 0f;
                case ProgressionMetric.LifetimeRescueCompletions: return profile.rescueMissionsCompleted;
                case ProgressionMetric.LifetimeRescueFailures: return profile.rescueMissionsFailed;
                case ProgressionMetric.LifetimeBestRescueCompletionSeconds: return profile.rescueBestCompletionSeconds > 0f ? profile.rescueBestCompletionSeconds : 0f;
                case ProgressionMetric.LifetimeRescueRank: return Mathf.Max(1, profile.rescueCareerRank);
                case ProgressionMetric.LifetimeRescueUtilityUses: return p != null ? p.lifetimeRescueUtilityUses : 0f;

                case ProgressionMetric.LifetimeQuestsAccepted: return p != null ? p.lifetimeQuestsAccepted : 0f;
                case ProgressionMetric.LifetimeQuestsCompleted: return profile.quests != null && profile.quests.completedQuestIds != null ? profile.quests.completedQuestIds.Count : 0f;
                case ProgressionMetric.LifetimeQuestStagesCompleted: return p != null ? p.lifetimeQuestStagesCompleted : 0f;
                case ProgressionMetric.LifetimeTutorialQuestsCompleted: return p != null ? p.lifetimeTutorialQuestsCompleted : 0f;

                case ProgressionMetric.LifetimeTricksLanded: return p != null ? p.lifetimeTricksLanded : 0f;
                case ProgressionMetric.LifetimeNamedTricksLanded: return p != null ? p.lifetimeNamedTricksLanded : 0f;
                case ProgressionMetric.LifetimeUniqueTrickNamesLanded: return p != null && p.landedTrickNames != null ? p.landedTrickNames.Count : 0f;
                case ProgressionMetric.LifetimeTrickFails: return p != null ? p.lifetimeTrickFails : 0f;
                case ProgressionMetric.LifetimeBestTrickTier: return p != null ? p.bestTrickTier : 0f;

                case ProgressionMetric.LifetimePassesPurchased: return p != null ? p.lifetimePassPurchases : 0f;
                case ProgressionMetric.LifetimePassExtensions: return p != null ? p.lifetimePassExtensions : 0f;
                case ProgressionMetric.LifetimePermanentPassesUnlocked: return profile.permanentlyUnlockedPassIds != null ? profile.permanentlyUnlockedPassIds.Count : 0f;
                case ProgressionMetric.LifetimeUniquePassesOwned: return p != null && p.ownedPassIds != null ? p.ownedPassIds.Count : 0f;

                case ProgressionMetric.LifetimeCustomizationPurchases: return p != null ? p.lifetimeCustomizationPurchases : 0f;
                case ProgressionMetric.LifetimeCustomizationUnlocks:
                    return profile.customization != null && profile.customization.unlockedCustomizationIds != null
                        ? profile.customization.unlockedCustomizationIds.Count
                        : 0f;

                case ProgressionMetric.LifetimeCurrencyEarned: return p != null ? p.lifetimeCurrencyEarned : 0f;
                case ProgressionMetric.LifetimeCurrencySpent: return p != null ? p.lifetimeCurrencySpent : 0f;
                case ProgressionMetric.LargestSingleCurrencyReward: return p != null ? p.largestSingleCurrencyReward : 0f;
            }

            return 0f;
        }

        public static string BuildRequirementText(ProgressionMetric metric, float target, string runId = null)
        {
            if (IsCurrencyMetric(metric))
                return $"{GetMetricVerb(metric)} {FormatMetricValue(metric, target)}";

            return metric switch
            {
                ProgressionMetric.SessionDistanceMeters or
                ProgressionMetric.LifetimeDistanceMeters or
                ProgressionMetric.LifetimeTotalDistanceMeters or
                ProgressionMetric.SessionAirDistanceMeters or
                ProgressionMetric.LifetimeAirDistanceMeters or
                ProgressionMetric.SessionVerticalDescentMeters or
                ProgressionMetric.LifetimeVerticalDescentMeters or
                ProgressionMetric.LifetimeTotalVerticalDescentMeters or
                ProgressionMetric.SessionVerticalAscentMeters or
                ProgressionMetric.LifetimeTotalVerticalAscentMeters or
                ProgressionMetric.SessionGrindDistanceMeters or
                ProgressionMetric.LifetimeGrindDistanceMeters
                    => $"Reach {FormatMetricValue(metric, target)}",

                ProgressionMetric.SessionTopSpeedMps or
                ProgressionMetric.LifetimeTopSpeedMps or
                ProgressionMetric.SessionAverageSpeedMps or
                ProgressionMetric.LifetimeAverageSpeedMps or
                ProgressionMetric.SessionTopRunSpeedMps or
                ProgressionMetric.LifetimeTopRunSpeedMps
                    => $"Reach {FormatMetricValue(metric, target)}",

                ProgressionMetric.SessionAirTimeSeconds or
                ProgressionMetric.LifetimeAirTimeSeconds or
                ProgressionMetric.SessionGrindTimeSeconds or
                ProgressionMetric.LifetimeGrindTimeSeconds or
                ProgressionMetric.LifetimeBestRescueCompletionSeconds
                    => $"Reach {FormatMetricValue(metric, target)}",

                ProgressionMetric.LifetimeRunVisited
                    => string.IsNullOrEmpty(runId) ? "Visit run" : $"Visit {runId}",
                ProgressionMetric.LifetimeRunCompletedCount
                    => string.IsNullOrEmpty(runId) ? $"Complete {Mathf.FloorToInt(target)} runs" : $"Complete {runId} x{Mathf.FloorToInt(target)}",
                ProgressionMetric.LifetimeRunCompletedCleanCount
                    => string.IsNullOrEmpty(runId) ? $"Complete {Mathf.FloorToInt(target)} clean runs" : $"Clean-complete {runId} x{Mathf.FloorToInt(target)}",

                _ => $"{GetMetricVerb(metric)} {Mathf.FloorToInt(target)}"
            };
        }

        public static string BuildProgressText(ProgressionMetric metric, float current, float goal)
        {
            current = Mathf.Max(0f, current);
            goal = Mathf.Max(0.0001f, goal);

            if (IsTimeMetric(metric) || IsSpeedMetric(metric) || IsDistanceMetric(metric) || IsCurrencyMetric(metric))
                return $"{FormatMetricValue(metric, current)} / {FormatMetricValue(metric, goal)}";

            return $"{Mathf.FloorToInt(current)}/{Mathf.FloorToInt(goal)}";
        }

        public static string FormatMetricValue(ProgressionMetric metric, float value)
        {
            if (IsDistanceMetric(metric))
            {
                if (value >= 1000f)
                    return $"{value / 1000f:0.0}km";
                return $"{value:0}m";
            }

            if (IsTimeMetric(metric))
                return $"{value:0.0}s";

            if (IsSpeedMetric(metric))
                return $"{value:0.0} m/s";

            if (IsCurrencyMetric(metric))
                return $"${Mathf.FloorToInt(value)}";

            return Mathf.FloorToInt(value).ToString();
        }

        public static string GetMetricDisplayName(ProgressionMetric metric)
        {
            return metric switch
            {
                ProgressionMetric.SessionDistanceMeters or ProgressionMetric.LifetimeDistanceMeters or ProgressionMetric.LifetimeTotalDistanceMeters => "Distance",
                ProgressionMetric.SessionTopSpeedMps or ProgressionMetric.LifetimeTopSpeedMps => "Top Speed",
                ProgressionMetric.SessionAirTimeSeconds or ProgressionMetric.LifetimeAirTimeSeconds => "Air Time",
                ProgressionMetric.SessionAirDistanceMeters or ProgressionMetric.LifetimeAirDistanceMeters => "Air Distance",
                ProgressionMetric.SessionVerticalDescentMeters or ProgressionMetric.LifetimeVerticalDescentMeters or ProgressionMetric.LifetimeTotalVerticalDescentMeters => "Vertical Descent",
                ProgressionMetric.SessionVerticalAscentMeters or ProgressionMetric.LifetimeTotalVerticalAscentMeters => "Vertical Ascent",
                ProgressionMetric.SessionAverageSpeedMps or ProgressionMetric.LifetimeAverageSpeedMps => "Average Speed",
                ProgressionMetric.SessionGrindTimeSeconds or ProgressionMetric.LifetimeGrindTimeSeconds => "Grind Time",
                ProgressionMetric.SessionGrindDistanceMeters or ProgressionMetric.LifetimeGrindDistanceMeters => "Grind Distance",
                ProgressionMetric.SessionStacks or ProgressionMetric.LifetimeStacks => "Stacks",
                ProgressionMetric.SessionRunsCompleted or ProgressionMetric.LifetimeRunsCompleted => "Runs Completed",
                ProgressionMetric.SessionLiftsUsed or ProgressionMetric.LifetimeLiftsUsed => "Lifts Used",
                ProgressionMetric.SessionPlacesVisited or ProgressionMetric.LifetimePlacesVisited => "Places Visited",
                ProgressionMetric.SessionRunsVisited or ProgressionMetric.LifetimeRunsVisited => "Runs Visited",
                ProgressionMetric.SessionRunsCompletedClean or ProgressionMetric.LifetimeRunsCompletedClean => "Clean Runs",
                ProgressionMetric.SessionTopRunSpeedMps or ProgressionMetric.LifetimeTopRunSpeedMps => "Top Run Speed",
                ProgressionMetric.SessionTricksLanded or ProgressionMetric.LifetimeTricksLanded => "Tricks Landed",
                ProgressionMetric.LifetimeRaceStarts => "Race Starts",
                ProgressionMetric.LifetimeRaceCompletions => "Races Completed",
                ProgressionMetric.LifetimeRaceWins => "Race Wins",
                ProgressionMetric.LifetimeRacePodiums => "Race Podiums",
                ProgressionMetric.LifetimeUniqueRacesCompleted => "Unique Races Completed",
                ProgressionMetric.LifetimeRaceChampionshipsCompleted => "Championships Completed",
                ProgressionMetric.LifetimeRacePersonalBestImprovements => "Race PB Improvements",
                ProgressionMetric.LifetimeRescueStarts => "Rescue Starts",
                ProgressionMetric.LifetimeRescueCompletions => "Rescue Missions Completed",
                ProgressionMetric.LifetimeRescueFailures => "Rescue Failures",
                ProgressionMetric.LifetimeBestRescueCompletionSeconds => "Best Rescue Time",
                ProgressionMetric.LifetimeRescueRank => "Rescue Rank",
                ProgressionMetric.LifetimeRescueUtilityUses => "Rescue Utility Uses",
                ProgressionMetric.LifetimeQuestsAccepted => "Quests Accepted",
                ProgressionMetric.LifetimeQuestsCompleted => "Quests Completed",
                ProgressionMetric.LifetimeQuestStagesCompleted => "Quest Stages Completed",
                ProgressionMetric.LifetimeTutorialQuestsCompleted => "Tutorial Quests Completed",
                ProgressionMetric.LifetimeNamedTricksLanded => "Named Tricks Landed",
                ProgressionMetric.LifetimeUniqueTrickNamesLanded => "Unique Trick Names Landed",
                ProgressionMetric.LifetimeTrickFails => "Trick Fails",
                ProgressionMetric.LifetimeBestTrickTier => "Best Trick Tier",
                ProgressionMetric.LifetimePassesPurchased => "Pass Purchases",
                ProgressionMetric.LifetimePassExtensions => "Pass Extensions",
                ProgressionMetric.LifetimePermanentPassesUnlocked => "Permanent Pass Unlocks",
                ProgressionMetric.LifetimeUniquePassesOwned => "Unique Passes Owned",
                ProgressionMetric.LifetimeCustomizationPurchases => "Customization Purchases",
                ProgressionMetric.LifetimeCustomizationUnlocks => "Customization Unlocks",
                ProgressionMetric.LifetimeCurrencyEarned => "Currency Earned",
                ProgressionMetric.LifetimeCurrencySpent => "Currency Spent",
                ProgressionMetric.LargestSingleCurrencyReward => "Largest Reward",
                ProgressionMetric.LifetimeRunVisited => "Run Visited",
                ProgressionMetric.LifetimeRunCompletedCount => "Run Completions",
                ProgressionMetric.LifetimeRunCompletedCleanCount => "Clean Run Completions",
                _ => metric.ToString()
            };
        }

        public static bool HasCompletedQuest(PlayerStatsProfile profile, string questId)
        {
            if (profile != null)
                profile.Sanitize();

            return profile != null &&
                   !string.IsNullOrWhiteSpace(questId) &&
                   profile.quests != null &&
                   profile.quests.completedQuestIds != null &&
                   profile.quests.completedQuestIds.Contains(questId);
        }

        public static bool HasOwnedPass(PlayerStatsProfile profile, string passId)
        {
            if (profile == null || string.IsNullOrWhiteSpace(passId))
                return false;

            profile.Sanitize();
            string normalized = passId.Trim();
            return HasId(profile.progression != null ? profile.progression.ownedPassIds : null, normalized) ||
                   HasId(profile.permanentlyUnlockedPassIds, normalized);
        }

        public static bool HasCompletedRace(PlayerStatsProfile profile, string raceId)
        {
            if (profile != null)
                profile.Sanitize();

            return profile != null &&
                   !string.IsNullOrWhiteSpace(raceId) &&
                   profile.progression != null &&
                   HasId(profile.progression.completedRaceIds, raceId);
        }

        private static bool HasId(List<string> ids, string id)
        {
            return ids != null && !string.IsNullOrWhiteSpace(id) && ids.Contains(id);
        }

        private static bool IsDistanceMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.SessionDistanceMeters ||
                   metric == ProgressionMetric.LifetimeDistanceMeters ||
                   metric == ProgressionMetric.LifetimeTotalDistanceMeters ||
                   metric == ProgressionMetric.SessionAirDistanceMeters ||
                   metric == ProgressionMetric.LifetimeAirDistanceMeters ||
                   metric == ProgressionMetric.SessionVerticalDescentMeters ||
                   metric == ProgressionMetric.LifetimeVerticalDescentMeters ||
                   metric == ProgressionMetric.LifetimeTotalVerticalDescentMeters ||
                   metric == ProgressionMetric.SessionVerticalAscentMeters ||
                   metric == ProgressionMetric.LifetimeTotalVerticalAscentMeters ||
                   metric == ProgressionMetric.SessionGrindDistanceMeters ||
                   metric == ProgressionMetric.LifetimeGrindDistanceMeters;
        }

        private static bool IsTimeMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.SessionAirTimeSeconds ||
                   metric == ProgressionMetric.LifetimeAirTimeSeconds ||
                   metric == ProgressionMetric.SessionGrindTimeSeconds ||
                   metric == ProgressionMetric.LifetimeGrindTimeSeconds ||
                   metric == ProgressionMetric.LifetimeBestRescueCompletionSeconds;
        }

        private static bool IsSpeedMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.SessionTopSpeedMps ||
                   metric == ProgressionMetric.LifetimeTopSpeedMps ||
                   metric == ProgressionMetric.SessionAverageSpeedMps ||
                   metric == ProgressionMetric.LifetimeAverageSpeedMps ||
                   metric == ProgressionMetric.SessionTopRunSpeedMps ||
                   metric == ProgressionMetric.LifetimeTopRunSpeedMps;
        }

        private static bool IsCurrencyMetric(ProgressionMetric metric)
        {
            return metric == ProgressionMetric.LifetimeCurrencyEarned ||
                   metric == ProgressionMetric.LifetimeCurrencySpent ||
                   metric == ProgressionMetric.LargestSingleCurrencyReward;
        }

        private static string GetMetricVerb(ProgressionMetric metric)
        {
            return metric switch
            {
                ProgressionMetric.SessionRunsCompleted or ProgressionMetric.LifetimeRunsCompleted => "Complete",
                ProgressionMetric.LifetimeRaceStarts => "Start",
                ProgressionMetric.LifetimeRaceCompletions => "Complete",
                ProgressionMetric.LifetimeRaceWins => "Win",
                ProgressionMetric.LifetimeRacePodiums => "Podium in",
                ProgressionMetric.LifetimeUniqueRacesCompleted => "Complete",
                ProgressionMetric.LifetimeRaceChampionshipsCompleted => "Complete",
                ProgressionMetric.LifetimeRacePersonalBestImprovements => "Improve",
                ProgressionMetric.LifetimeRescueStarts => "Start",
                ProgressionMetric.LifetimeRescueCompletions => "Complete",
                ProgressionMetric.LifetimeRescueFailures => "Fail",
                ProgressionMetric.LifetimeRescueUtilityUses => "Use",
                ProgressionMetric.LifetimeQuestsAccepted => "Accept",
                ProgressionMetric.LifetimeQuestsCompleted => "Complete",
                ProgressionMetric.LifetimeQuestStagesCompleted => "Complete",
                ProgressionMetric.LifetimeTutorialQuestsCompleted => "Complete",
                ProgressionMetric.SessionTricksLanded or ProgressionMetric.LifetimeTricksLanded => "Land",
                ProgressionMetric.LifetimeNamedTricksLanded => "Land",
                ProgressionMetric.LifetimeUniqueTrickNamesLanded => "Land",
                ProgressionMetric.LifetimeTrickFails => "Fail",
                ProgressionMetric.LifetimePassesPurchased => "Purchase",
                ProgressionMetric.LifetimePassExtensions => "Extend",
                ProgressionMetric.LifetimePermanentPassesUnlocked => "Unlock",
                ProgressionMetric.LifetimeUniquePassesOwned => "Own",
                ProgressionMetric.LifetimeCustomizationPurchases => "Purchase",
                ProgressionMetric.LifetimeCustomizationUnlocks => "Unlock",
                ProgressionMetric.LifetimeCurrencyEarned => "Earn",
                ProgressionMetric.LifetimeCurrencySpent => "Spend",
                ProgressionMetric.LargestSingleCurrencyReward => "Earn",
                _ => "Reach"
            };
        }

        private static float ReadRunRecordValue(PlayerStatsProfile profile, string runId, bool cleanOnly)
        {
            if (profile == null || string.IsNullOrEmpty(runId) || profile.runRecords == null)
                return 0f;

            for (int i = 0; i < profile.runRecords.Count; i++)
            {
                var rr = profile.runRecords[i];
                if (rr == null || rr.runId != runId)
                    continue;

                return cleanOnly ? rr.timesCompletedClean : rr.timesCompleted;
            }

            return 0f;
        }
    }
}
