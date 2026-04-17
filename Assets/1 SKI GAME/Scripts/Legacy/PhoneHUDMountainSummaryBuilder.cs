using System;
using UnityEngine;
using SkiGame.POI;
using SkiGame.Runs;

namespace SkiGame.Progression
{
    public readonly struct PhoneHUDHomeSummary
    {
        public readonly string statsLine;
        public readonly string achievementLine1;
        public readonly string achievementLine2;
        public readonly string skiPassLine;
        public readonly string runIdleLine;
        public readonly string statusHint;

        public PhoneHUDHomeSummary(
            string statsLine,
            string achievementLine1,
            string achievementLine2,
            string skiPassLine,
            string runIdleLine,
            string statusHint)
        {
            this.statsLine = statsLine ?? string.Empty;
            this.achievementLine1 = achievementLine1 ?? string.Empty;
            this.achievementLine2 = achievementLine2 ?? string.Empty;
            this.skiPassLine = skiPassLine ?? string.Empty;
            this.runIdleLine = runIdleLine ?? string.Empty;
            this.statusHint = statusHint ?? string.Empty;
        }
    }

    public readonly struct PhoneHUDRunMapSummary
    {
        public readonly string body;
        public readonly bool hasAttempts;

        public PhoneHUDRunMapSummary(string body, bool hasAttempts)
        {
            this.body = body ?? string.Empty;
            this.hasAttempts = hasAttempts;
        }
    }

    public static class PhoneHUDMountainSummaryBuilder
    {
        public static PhoneHUDHomeSummary BuildHomeSummary(
            PlayerStatsProfile profile,
            ProgressionDirector progression,
            PointOfInterestRegistry poiRegistry,
            SkiPassManager skiPassMgr,
            TimeWeather.TimeController time,
            TimeWeather.WeatherController weather)
        {
            if (profile == null)
            {
                return new PhoneHUDHomeSummary(
                    statsLine: "Today • No profile",
                    achievementLine1: "Achievements unavailable",
                    achievementLine2: string.Empty,
                    skiPassLine: skiPassMgr != null ? skiPassMgr.GetCurrentPassDisplayName() : "No pass data",
                    runIdleLine: "Explore the mountain",
                    statusHint: "No profile loaded");
            }

            int totalRuns = 0;
            int completedRuns = 0;
            int discoveredRuns = 0;
            int lockedLifts = 0;
            int accessibleLifts = 0;
            int discoveredPois = profile.visitedLandmarkIds != null ? profile.visitedLandmarkIds.Count : 0;
            int totalPois = 0;
            string nearestIncompleteRun = null;

            if (poiRegistry != null && poiRegistry.Current != null)
            {
                for (int i = 0; i < poiRegistry.Current.Count; i++)
                {
                    var poi = poiRegistry.Current[i];
                    switch (poi.type)
                    {
                        case POIType.SkiRun:
                            totalRuns++;
                            if (profile.HasVisitedRun(poi.id)) discoveredRuns++;
                            if (profile.HasCompletedRunEver(poi.id))
                                completedRuns++;
                            else if (string.IsNullOrEmpty(nearestIncompleteRun))
                                nearestIncompleteRun = string.IsNullOrWhiteSpace(poi.displayName) ? "Unfinished run" : poi.displayName;
                            break;

                        case POIType.SkiLift:
                            if (poi.source is LiftLine lift)
                            {
                                bool canUse = skiPassMgr == null || skiPassMgr.CanUseLift(lift);
                                if (canUse) accessibleLifts++;
                                else lockedLifts++;
                            }
                            break;

                        case POIType.Custom:
                            totalPois++;
                            break;
                    }
                }
            }

            if (string.IsNullOrEmpty(nearestIncompleteRun))
                nearestIncompleteRun = totalRuns > 0 && completedRuns >= totalRuns ? "All mapped runs completed" : "Find a new run";

            string statsLine =
                $"Today • {profile.session.runsCompleted} runs • {FormatMetersCompact(profile.session.distanceMeters)} • {profile.session.liftsUsed} lifts";

            string ach1 = "Achievements underway";
            string ach2 = string.Empty;
            if (progression != null)
                progression.GetTopAchievementLines(profile, out ach1, out ach2);

            string skiPassLine;
            if (skiPassMgr == null)
            {
                skiPassLine = "No pass data";
            }
            else
            {
                string current = skiPassMgr.GetCurrentPassDisplayName();
                if (lockedLifts > 0)
                    skiPassLine = $"{current} • {accessibleLifts} lifts open • {lockedLifts} locked";
                else
                    skiPassLine = $"{current} • All mapped lifts open";
            }

            string runIdleLine =
                totalRuns > 0
                    ? $"Explore • {completedRuns}/{totalRuns} runs complete • Next: {nearestIncompleteRun}"
                    : $"Explore • {discoveredPois} landmarks found";

            string statusHint = BuildStatusHint(time, weather, totalRuns, discoveredRuns, totalPois, discoveredPois);

            return new PhoneHUDHomeSummary(statsLine, ach1, ach2, skiPassLine, runIdleLine, statusHint);
        }

        public static PhoneHUDRunMapSummary BuildRunMapSummary(
            PlayerStatsProfile profile,
            string runId,
            string displayName,
            string difficultyLabel)
        {
            if (profile == null)
                return new PhoneHUDRunMapSummary("No stats profile loaded.", false);

            bool discovered = profile.HasVisitedRun(runId);
            bool completed = profile.HasCompletedRunEver(runId);
            bool sessionCompleted = profile.HasCompletedRunThisSession(runId);
            int visits = profile.GetRunVisitCount(runId, session: false);

            RunRecordEntry record = null;
            profile.TryGetRunRecord(runId, out record);

            string status = completed
                ? (record != null && record.timesCompletedClean > 0 ? "Completed • Clean logged" : "Completed")
                : discovered ? "Discovered • Incomplete" : "Undiscovered";

            string bestTime = (record != null && record.bestTimeSeconds > 0f)
                ? FormatSeconds(record.bestTimeSeconds)
                : "--";

            string bestProgress = "--";
            if (!completed && record != null && record.attempts != null && record.attempts.Count > 0)
            {
                float best = 0f;
                for (int i = 0; i < record.attempts.Count; i++)
                {
                    var a = record.attempts[i];
                    if (a == null) continue;
                    best = Mathf.Max(best, Mathf.Clamp01(a.coveredFraction01));
                }
                bestProgress = $"{Mathf.RoundToInt(best * 100f)}%";
            }

            string body =
                $"Status: {status}\n" +
                $"Difficulty: {difficultyLabel}\n" +
                $"Visits: {visits}\n" +
                $"Completions: {(record != null ? record.timesCompleted : 0)}\n" +
                $"Best Time: {bestTime}";

            if (!completed)
                body += $"\nBest Progress: {bestProgress}";
            else if (record != null && record.timesCompletedClean > 0)
                body += $"\nBest Clean: {(record.bestCleanTimeSeconds > 0f ? FormatSeconds(record.bestCleanTimeSeconds) : "--")}";

            if (sessionCompleted)
                body += "\nCompleted this session";

            bool hasAttempts = record != null && record.attempts != null && record.attempts.Count > 0;
            return new PhoneHUDRunMapSummary(body, hasAttempts);
        }

        public static string BuildLiftMapSummary(PlayerStatsProfile profile, LiftLine lift, string displayName, SkiPassManager skiPassMgr)
        {
            if (lift == null)
                return "Lift unavailable.";

            int rides = profile != null ? profile.GetLiftRideCount(lift.gameObject.name, session: false) : 0;
            string passRequirement = GetLiftPassRequirementLabel(lift, skiPassMgr);
            bool accessible = skiPassMgr == null || skiPassMgr.CanUseLift(lift);

            string body =
                $"Pass: {passRequirement}\n" +
                $"Status: {(accessible ? "Accessible" : "Locked")}\n" +
                $"Rides: {rides}";

            if (!accessible)
                body += "\nUpgrade your pass to open this route.";

            return body;
        }

        public static string BuildPoiMapSummary(PlayerStatsProfile profile, string poiId, string fallbackBody)
        {
            string status = profile != null && profile.HasVisitedLandmark(poiId) ? "Discovered" : "Undiscovered";
            if (string.IsNullOrWhiteSpace(fallbackBody))
                return $"Status: {status}";

            return $"Status: {status}\n{fallbackBody.Trim()}";
        }

        private static string GetLiftPassRequirementLabel(LiftLine lift, SkiPassManager skiPassMgr)
        {
            if (lift == null)
                return "--";

            return lift.GetRequiredPassDisplayName();
        }

        private static string BuildStatusHint(
            TimeWeather.TimeController time,
            TimeWeather.WeatherController weather,
            int totalRuns,
            int discoveredRuns,
            int totalPois,
            int discoveredPois)
        {
            int hour = time != null ? Mathf.Clamp(time.timeHours, 0, 23) : 12;
            string cond = weather != null && weather.currentWeatherPreset != null
                ? weather.currentWeatherPreset.weatherCondition
                : string.Empty;

            string timeHint = hour < 10
                ? "Morning for exploration"
                : hour < 16
                    ? "Prime time for completions"
                    : "Wrap nearby goals before dark";

            string weatherHint = string.IsNullOrWhiteSpace(cond) ? string.Empty : $" • {cond}";
            string progressHint;

            if (totalRuns > 0 && discoveredRuns < totalRuns)
                progressHint = $" • {totalRuns - discoveredRuns} runs still undiscovered";
            else if (totalPois > 0 && discoveredPois < totalPois)
                progressHint = $" • {Mathf.Max(0, totalPois - discoveredPois)} landmarks still unvisited";
            else
                progressHint = " • Mountain checklist advancing";

            return timeHint + weatherHint + progressHint;
        }

        private static string FormatMetersCompact(float meters)
        {
            if (meters >= 1000f)
                return $"{meters / 1000f:0.0} km";
            return $"{meters:0} m";
        }

        private static string FormatSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.RoundToInt(seconds);
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins:00}:{secs:00}";
        }
    }
}