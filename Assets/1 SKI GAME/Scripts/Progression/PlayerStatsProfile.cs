using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    /// <summary>
    /// Serializable root data for player progression stats.
    /// Uses Lists (JsonUtility-friendly) instead of Dictionary/HashSet.
    /// </summary>
    [Serializable]
    public sealed class PlayerStatsProfile
    {
        public int profileVersion = CurrentVersion;
        public const int CurrentVersion = 2; // bumped due to new fields

        public LifetimeStats lifetime = new LifetimeStats();
        public SessionStats session = new SessionStats();

        // Stored as lists for JsonUtility compatibility.
        public List<string> unlockedAchievementIds = new List<string>();
        public List<ActiveTaskState> activeSessionTasks = new List<ActiveTaskState>();

        // Simple reward currency for Tasks/Achievements (arbitrary for now)
        public int currency = 0;

        // Recent task IDs to avoid repeating the same tasks over and over.
        public List<string> recentTaskIds = new List<string>();

        // POI Visits (lifetime + session)
        // - Lifetime persists across launches
        // - Session clears on runtime start (via ResetSessionOnAwake in PlayerStatsManager)
        public List<string> visitedLandmarkIds = new List<string>();
        public List<string> sessionVisitedLandmarkIds = new List<string>();

        // Run records (lifetime)
        public List<RunRecordEntry> runRecords = new List<RunRecordEntry>();

        public DateTimeUtc createdUtc = DateTimeUtc.Now();
        public DateTimeUtc lastSavedUtc = DateTimeUtc.Now();

        [Serializable]
        public sealed class ActiveTaskState
        {
            public string taskId;
            public bool completed;
            public DateTimeUtc completedUtc;

            // Cached progress for UI (now "progress since activation", not absolute metric value)
            public float lastProgress;
            public float target;

            // Baseline snapshot of the metric when this task became active.
            // Progress is computed as: (currentMetric - baselineValue)
            public float baselineValue;
            public bool baselineCaptured;

            public bool claimed;               // task has been turned in
            public int lastRewardGranted;      // optional: for UI/debug
        }

        // --------- Convenience helpers ---------

        public bool HasAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return unlockedAchievementIds.Contains(id);
        }

        public bool TryAddAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (unlockedAchievementIds.Contains(id)) return false;
            unlockedAchievementIds.Add(id);
            return true;
        }

        public void RememberRecentTask(string taskId, int max = 10)
        {
            if (string.IsNullOrEmpty(taskId)) return;

            recentTaskIds ??= new List<string>();
            recentTaskIds.Remove(taskId);
            recentTaskIds.Add(taskId);

            while (recentTaskIds.Count > max)
                recentTaskIds.RemoveAt(0);
        }

        // ---- POI Visitation (Lifetime) ----

        public bool HasVisitedLandmark(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return visitedLandmarkIds.Contains(id);
        }

        public bool TryAddVisitedLandmark(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (visitedLandmarkIds.Contains(id)) return false;
            visitedLandmarkIds.Add(id);
            return true;
        }

        // ---- POI Visitation (Session) ----

        public bool HasVisitedLandmarkThisSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return sessionVisitedLandmarkIds.Contains(id);
        }

        public bool TryAddVisitedLandmarkThisSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (sessionVisitedLandmarkIds.Contains(id)) return false;
            sessionVisitedLandmarkIds.Add(id);
            return true;
        }

        /// <summary>
        /// Common case: mark POI as visited in session, and also in lifetime.
        /// Returns (addedToSession, addedToLifetime).
        /// </summary>
        public (bool addedSession, bool addedLifetime) TryAddVisitedLandmarkSessionAndLifetime(string id)
        {
            bool a = TryAddVisitedLandmarkThisSession(id);
            bool b = TryAddVisitedLandmark(id);
            return (a, b);
        }

        // ---- Run Records ----

        public RunRecordEntry GetOrCreateRunRecord(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                throw new ArgumentException("runId is null/empty.");

            for (int i = 0; i < runRecords.Count; i++)
            {
                if (runRecords[i] != null && runRecords[i].runId == runId)
                    return runRecords[i];
            }

            var created = new RunRecordEntry { runId = runId };
            runRecords.Add(created);
            return created;
        }

        // ---- Resets ----

        public void ResetSession()
        {
            session = new SessionStats();
            sessionVisitedLandmarkIds?.Clear();
            activeSessionTasks?.Clear(); // causes ProgressionDirector to refill next update
        }

        public void ResetAll()
        {
            lifetime = new LifetimeStats();
            session = new SessionStats();

            unlockedAchievementIds.Clear();
            visitedLandmarkIds.Clear();
            sessionVisitedLandmarkIds.Clear();
            runRecords.Clear();

            currency = 0;
            recentTaskIds.Clear();
            activeSessionTasks.Clear();
        }

        public void Sanitize()
        {
            unlockedAchievementIds ??= new List<string>();
            visitedLandmarkIds ??= new List<string>();
            sessionVisitedLandmarkIds ??= new List<string>();
            runRecords ??= new List<RunRecordEntry>();
            activeSessionTasks ??= new List<ActiveTaskState>();
            lifetime ??= new LifetimeStats();
            session ??= new SessionStats();
            recentTaskIds ??= new List<string>();
        }
    }

    [Serializable]
    public sealed class LifetimeStats
    {
        // Movement
        public float totalDistanceMeters;
        public float totalVerticalAscentMeters;
        public float totalVerticalDescentMeters;

        // Speed
        public float topSpeedMps;          // best ever
        public float speedIntegral;        // sum(speed * dt) for lifetime average
        public float activeTimeSeconds;    // sum(dt) for lifetime average

        // Air
        public float totalAirTimeSeconds;
        public float totalAirDistanceMeters;

        // NEW: Grind
        public float totalGrindTimeSeconds;
        public float totalGrindDistanceMeters;

        // Counts
        public int totalStacks;
        public int totalRunsCompleted;
        public int totalLiftsUsed;

        public float AverageSpeedMps => (activeTimeSeconds > 0.0001f) ? (speedIntegral / activeTimeSeconds) : 0f;
    }

    [Serializable]
    public sealed class SessionStats
    {
        public float distanceMeters;
        public float verticalAscentMeters;
        public float verticalDescentMeters;

        public float topSpeedMps;
        public float speedIntegral;
        public float activeTimeSeconds;

        public float airTimeSeconds;
        public float airDistanceMeters;

        // NEW: Grind
        public float grindTimeSeconds;
        public float grindDistanceMeters;

        public int stacks;
        public int runsCompleted;
        public int liftsUsed;

        public float AverageSpeedMps => (activeTimeSeconds > 0.0001f) ? (speedIntegral / activeTimeSeconds) : 0f;
    }

    [Serializable]
    public sealed class RunRecordEntry
    {
        public string runId;

        public int timesCompleted;
        public float bestTimeSeconds = -1f; // -1 = unset
        public float bestTopSpeedMps = 0f;
        public float totalDistanceMeters = 0f;

        public void RegisterCompletion(float timeSeconds, float topSpeedMps, float distanceMeters)
        {
            timesCompleted++;

            if (timeSeconds > 0f)
            {
                if (bestTimeSeconds < 0f || timeSeconds < bestTimeSeconds)
                    bestTimeSeconds = timeSeconds;
            }

            if (topSpeedMps > bestTopSpeedMps)
                bestTopSpeedMps = topSpeedMps;

            if (distanceMeters > 0f)
                totalDistanceMeters += distanceMeters;
        }
    }

    /// <summary>
    /// JsonUtility cannot serialize System.DateTime reliably; store as Unix seconds.
    /// </summary>
    [Serializable]
    public struct DateTimeUtc
    {
        public long unixSeconds;

        public static DateTimeUtc Now()
        {
            var utc = DateTime.UtcNow;
            var unix = (long)(utc - DateTime.UnixEpoch).TotalSeconds;
            return new DateTimeUtc { unixSeconds = unix };
        }

        public DateTime ToDateTimeUtc()
        {
            return DateTime.UnixEpoch.AddSeconds(unixSeconds);
        }

        public override string ToString()
        {
            return ToDateTimeUtc().ToString("u");
        }
    }
}
