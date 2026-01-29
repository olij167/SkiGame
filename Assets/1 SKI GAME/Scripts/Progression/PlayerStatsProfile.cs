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
        public const int CurrentVersion = 4;  // added run visit + run attempt history

        public LifetimeStats lifetime = new LifetimeStats();
        public SessionStats session = new SessionStats();

        // Stored as lists for JsonUtility compatibility.
        public List<string> unlockedAchievementIds = new List<string>();

        // New: achievement unlock timestamps (for UI badges).
        // Kept alongside unlockedAchievementIds for backwards compatible saves.
        public List<AchievementUnlockEntry> achievementUnlocks = new List<AchievementUnlockEntry>();

        // Legacy/session tasks (kept for now).
        public List<ActiveTaskState> activeSessionTasks = new List<ActiveTaskState>();

        // NEW: Daily task matrix state (persists across launches; resets on day rollover).
        public DailyTaskMatrixState dailyTasks = new DailyTaskMatrixState();

        // Simple reward currency for Tasks/Achievements (arbitrary for now)
        public int currency = 0;

        // Recent task IDs to avoid repeating the same tasks over and over.
        public List<string> recentTaskIds = new List<string>();

        // NEW: recent daily ladder IDs to avoid repetition day-to-day.
        public List<string> recentDailyLadderIds = new List<string>();

        // POI Visits (lifetime + session)
        // - Lifetime persists across launches
        // - Session clears on runtime start (via ResetSessionOnAwake in PlayerStatsManager)
        public List<string> visitedLandmarkIds = new List<string>();
        public List<string> sessionVisitedLandmarkIds = new List<string>();

        // Run records (lifetime)
        public List<RunRecordEntry> runRecords = new List<RunRecordEntry>();
        // Run visits (lifetime + session)
        public List<string> visitedRunIds = new List<string>();
        public List<string> sessionVisitedRunIds = new List<string>();
        // Run Completions (session only; lifetime completion is derived from runRecords)
        public List<string> sessionCompletedRunIds = new List<string>();

        public DateTimeUtc createdUtc = DateTimeUtc.Now();
        public DateTimeUtc lastSavedUtc = DateTimeUtc.Now();

        [Serializable]
        public struct IdCountEntry { public string id; public int count; }

        public List<IdCountEntry> landmarkVisitCounts;
        public List<IdCountEntry> sessionLandmarkVisitCounts;

        public List<IdCountEntry> runVisitCounts;
        public List<IdCountEntry> sessionRunVisitCounts;

        public List<IdCountEntry> liftRideCounts;
        public List<IdCountEntry> sessionLiftRideCounts;


        [Serializable]
        public sealed class AchievementUnlockEntry
        {
            public string achievementId;
            public DateTimeUtc unlockedUtc;
        }

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

        // ---------------- Daily Task Matrix (New) ----------------

        [Serializable]
        public sealed class DailyTaskMatrixState
        {
            // Day identifier used to decide when to reset/regenerate.
            // Interpreted by ProgressionDirector; default uses UTC day key.
            public int dayKey = -1;

            // Active ladders for the current day (rows).
            public List<DailyTaskRowState> rows = new List<DailyTaskRowState>();
        }

        [Serializable]
        public sealed class DailyTaskRowState
        {
            public string ladderId;
            public ProgressionMetric metric;

            public DailyTaskLadderDefinitionSO.EvaluationMode evaluationMode;

            // Baseline snapshot at day start (for DeltaFromBaseline)
            public float baselineValue;
            public bool baselineCaptured;

            // For MaxSinceDayStart: best observed since day start.
            public float bestValueSinceStart;

            public bool rowBonusClaimed;

            public List<DailyTaskTierState> tiers = new List<DailyTaskTierState>();
        }

        [Serializable]
        public sealed class DailyTaskTierState
        {
            public float target;
            public float lastProgress; // "current progress for the day" (delta or best value depending on mode)
            public bool completed;
            public bool claimed;
            public DateTimeUtc completedUtc;

            public int lastRewardGranted;
        }

        // --------- Convenience helpers ---------

        public bool HasAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Prefer timestamped list (new).
            if (achievementUnlocks != null)
            {
                for (int i = 0; i < achievementUnlocks.Count; i++)
                {
                    var e = achievementUnlocks[i];
                    if (e != null && e.achievementId == id)
                        return true;
                }
            }

            // Back-compat list (old).
            return unlockedAchievementIds != null && unlockedAchievementIds.Contains(id);
        }

        public bool TryAddAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Already unlocked?
            if (HasAchievement(id)) return false;

            // Ensure lists exist
            unlockedAchievementIds ??= new List<string>();
            achievementUnlocks ??= new List<AchievementUnlockEntry>();

            // Write both (old list + timestamped list)
            unlockedAchievementIds.Add(id);
            achievementUnlocks.Add(new AchievementUnlockEntry
            {
                achievementId = id,
                unlockedUtc = DateTimeUtc.Now()
            });
            return true;
        }

        public bool TryGetAchievementUnlockedUtc(string id, out DateTimeUtc unlockedUtc)
        {
            unlockedUtc = default;
            if (string.IsNullOrEmpty(id)) return false;

            if (achievementUnlocks != null)
            {
                for (int i = 0; i < achievementUnlocks.Count; i++)
                {
                    var e = achievementUnlocks[i];
                    if (e != null && e.achievementId == id)
                    {
                        unlockedUtc = e.unlockedUtc;
                        return true;
                    }
                }
            }

            // Back-compat: if old list says unlocked but no timestamp entry exists,
            // treat as 'unknown' and return false (UI can omit date).
            return false;
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

        public void RememberRecentDailyLadder(string ladderId, int max = 10)
        {
            if (string.IsNullOrEmpty(ladderId)) return;

            recentDailyLadderIds ??= new List<string>();
            recentDailyLadderIds.Remove(ladderId);
            recentDailyLadderIds.Add(ladderId);

            while (recentDailyLadderIds.Count > max)
                recentDailyLadderIds.RemoveAt(0);
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

        public bool TryGetRunRecord(string runId, out RunRecordEntry record)
        {
            record = null;
            if (string.IsNullOrEmpty(runId) || runRecords == null) return false;

            for (int i = 0; i < runRecords.Count; i++)
            {
                if (runRecords[i] != null && runRecords[i].runId == runId)
                {
                    record = runRecords[i];
                    return true;
                }
            }
            return false;
        }

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

        public bool HasVisitedRun(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return visitedRunIds.Contains(id);
        }

        public bool TryAddVisitedRun(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (visitedRunIds.Contains(id)) return false;
            visitedRunIds.Add(id);
            return true;
        }

        public bool HasVisitedRunThisSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return sessionVisitedRunIds.Contains(id);
        }

        public bool TryAddVisitedRunThisSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (sessionVisitedRunIds.Contains(id)) return false;
            sessionVisitedRunIds.Add(id);
            return true;
        }


        // ---- Run Completion (Lifetime derived from runRecords) ----

        public bool HasCompletedRunEver(string runId)
        {
            if (string.IsNullOrEmpty(runId) || runRecords == null) return false;

            for (int i = 0; i < runRecords.Count; i++)
            {
                var r = runRecords[i];
                if (r != null && r.runId == runId)
                    return r.timesCompleted > 0;
            }

            return false;
        }

        // ---- Run Completion (Session list) ----

        public bool HasCompletedRunThisSession(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            return sessionCompletedRunIds != null && sessionCompletedRunIds.Contains(runId);
        }

        public bool TryAddCompletedRunThisSession(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            sessionCompletedRunIds ??= new List<string>();
            if (sessionCompletedRunIds.Contains(runId)) return false;
            sessionCompletedRunIds.Add(runId);
            return true;
        }

        // ---- Resets ----

        public void ResetSession()
        {
            session = new SessionStats();
            sessionVisitedLandmarkIds?.Clear();
            activeSessionTasks?.Clear(); // causes ProgressionDirector to refill next update
            sessionVisitedRunIds?.Clear();
            sessionCompletedRunIds?.Clear();

            sessionLandmarkVisitCounts?.Clear();
            sessionRunVisitCounts?.Clear();
            sessionLiftRideCounts?.Clear();

            // NOTE: dailyTasks intentionally NOT cleared here; daily tasks persist across restarts.
        }

        // -------------------------
        // ID -> Count helpers (visits / rides)
        // -------------------------
        private static int GetIdCount(List<IdCountEntry> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id)
                    return list[i].count;
            return 0;
        }

        private static void IncrementIdCount(List<IdCountEntry> list, string id, int delta)
        {
            if (list == null || string.IsNullOrEmpty(id) || delta == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id)
                {
                    var e = list[i];
                    e.count = Mathf.Max(0, e.count + delta);
                    list[i] = e;
                    return;
                }
            }

            list.Add(new IdCountEntry { id = id, count = Mathf.Max(0, delta) });
        }

        public int GetLandmarkVisitCount(string poiId, bool session)
            => GetIdCount(session ? sessionLandmarkVisitCounts : landmarkVisitCounts, poiId);

        public int GetRunVisitCount(string runId, bool session)
            => GetIdCount(session ? sessionRunVisitCounts : runVisitCounts, runId);

        public int GetLiftRideCount(string liftId, bool session)
            => GetIdCount(session ? sessionLiftRideCounts : liftRideCounts, liftId);

        public void IncrementLandmarkVisitCount(string poiId, bool session, int delta = 1)
            => IncrementIdCount(session ? sessionLandmarkVisitCounts : landmarkVisitCounts, poiId, delta);

        public void IncrementRunVisitCount(string runId, bool session, int delta = 1)
            => IncrementIdCount(session ? sessionRunVisitCounts : runVisitCounts, runId, delta);

        public void IncrementLiftRideCount(string liftId, bool session, int delta = 1)
            => IncrementIdCount(session ? sessionLiftRideCounts : liftRideCounts, liftId, delta);

        public void ResetAll()
        {
            lifetime = new LifetimeStats();
            session = new SessionStats();

            unlockedAchievementIds.Clear();
            visitedLandmarkIds.Clear();
            sessionVisitedLandmarkIds.Clear();
            runRecords.Clear();
            visitedRunIds.Clear();
            sessionVisitedRunIds.Clear();
            sessionCompletedRunIds.Clear();

            currency = 0;
            recentTaskIds.Clear();
            recentDailyLadderIds.Clear();
            activeSessionTasks.Clear();

            landmarkVisitCounts?.Clear();
            runVisitCounts?.Clear();
            liftRideCounts?.Clear();

            dailyTasks = new DailyTaskMatrixState();
        }

        public void Sanitize()
        {
            unlockedAchievementIds ??= new List<string>();
            achievementUnlocks ??= new List<AchievementUnlockEntry>();

            // Back-compat migration: if old list has entries but new list is empty,
            // create entries using createdUtc as a fallback timestamp.
            if (achievementUnlocks.Count == 0 && unlockedAchievementIds.Count > 0)
            {
                for (int i = 0; i < unlockedAchievementIds.Count; i++)
                {
                    var id = unlockedAchievementIds[i];
                    if (string.IsNullOrEmpty(id)) continue;

                    achievementUnlocks.Add(new AchievementUnlockEntry
                    {
                        achievementId = id,
                        unlockedUtc = createdUtc
                    });
                }
            }

            activeSessionTasks ??= new List<ActiveTaskState>();
            recentTaskIds ??= new List<string>();
            recentDailyLadderIds ??= new List<string>();
            visitedLandmarkIds ??= new List<string>();
            sessionVisitedLandmarkIds ??= new List<string>();
            runRecords ??= new List<RunRecordEntry>();
            visitedRunIds ??= new List<string>();
            sessionVisitedRunIds ??= new List<string>();
            sessionCompletedRunIds ??= new List<string>();

            dailyTasks ??= new DailyTaskMatrixState();
            dailyTasks.rows ??= new List<DailyTaskRowState>();

            landmarkVisitCounts ??= new List<IdCountEntry>();
            sessionLandmarkVisitCounts ??= new List<IdCountEntry>();

            runVisitCounts ??= new List<IdCountEntry>();
            sessionRunVisitCounts ??= new List<IdCountEntry>();

            liftRideCounts ??= new List<IdCountEntry>();
            sessionLiftRideCounts ??= new List<IdCountEntry>();

            if (lifetime == null) lifetime = new LifetimeStats();
            if (session == null) session = new SessionStats();
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
        public int totalLiftsUsed;

        // Runs (RunProgressTracker)
        public int totalRunsCompleted;
        public int totalRunsCompletedClean;   // completed with 0 stacks
        public float topRunSpeedMps;          // best topSpeed from a completed run attempt

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
        // Runs (RunProgressTracker)
        public int runsCompletedClean;        // completed with 0 stacks
        public float topRunSpeedMps;          // best topSpeed from a completed run attempt (this session)

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
        public List<RunAttemptEntry> attempts = new List<RunAttemptEntry>();
        public int timesCompletedClean;
        public float bestCleanTimeSeconds = -1f; // -1 = unset

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


        public void RegisterAttempt(RunAttemptEntry attempt, int maxKeep = 20)
        {
            if (attempt != null)
            {
                attempts ??= new List<RunAttemptEntry>();
                attempts.Add(attempt);

                // Trim old entries
                maxKeep = Mathf.Max(1, maxKeep);
                while (attempts.Count > maxKeep)
                    attempts.RemoveAt(0);
            }

            // Update totals + bests
            RegisterCompletion(attempt != null ? attempt.timeSeconds : 0f,
                               attempt != null ? attempt.topSpeedMps : 0f,
                               attempt != null ? (attempt.onRouteDistanceMeters + attempt.offRouteDistanceMeters) : 0f);

            // Clean completion tracking (0 stacks)
            if (attempt != null && attempt.stacks == 0)
            {
                timesCompletedClean++;

                if (attempt.timeSeconds > 0f)
                {
                    if (bestCleanTimeSeconds < 0f || attempt.timeSeconds < bestCleanTimeSeconds)
                        bestCleanTimeSeconds = attempt.timeSeconds;
                }
            }
        }

    }

    [Serializable]
    public sealed class StackEventEntry
    {
        public float timeSinceRunStartSeconds;
        public Vector3 positionWorld;
        public float impactSpeedMps;
        public float severity01;
        public string reason;
    }

    [Serializable]
    public sealed class RunAttemptEntry
    {
        public DateTimeUtc completedUtc;

        public float timeSeconds;
        public float averageSpeedMps;
        public float topSpeedMps;

        public float airTimeSeconds;
        public float airDistanceMeters;

        public float onRouteDistanceMeters;
        public float offRouteDistanceMeters;

        public int stacks;

        // Where on the run they entered (meters along centerline)
        public float startDistanceMeters;

        // Completion fraction over remaining run (entry->end)
        public float completionFraction;

        public List<StackEventEntry> stackEvents;
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
