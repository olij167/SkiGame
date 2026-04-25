using System;
using System.Collections.Generic;
using UnityEngine;
using static SkiGame.Progression.DateTimeUtc;

namespace SkiGame.Progression
{
    /// <summary>
    /// Serializable root data for player progression stats.
    /// Uses Lists (JsonUtility-friendly) instead of Dictionary/HashSet.
    /// </summary>
    [Serializable]
    public sealed partial class PlayerStatsProfile : ISerializationCallbackReceiver
    {
        public int profileVersion = CurrentVersion;
        public const int CurrentVersion = 13;
        public LifetimeStats lifetime = new LifetimeStats();
        public SessionStats session = new SessionStats();
        public ExtendedProgressionStats progression = new ExtendedProgressionStats();
        public PlaythroughState playthrough = new PlaythroughState();
        public TutorialState tutorial = new TutorialState();
        public QuestLogState quests = new QuestLogState();

        // Stored as lists for JsonUtility compatibility.
        public List<string> unlockedAchievementIds = new List<string>();

        public List<string> claimedAchievementIds = new List<string>();

        // New: achievement unlock timestamps (for UI badges).
        // Kept alongside unlockedAchievementIds for backwards compatible saves.
        public List<AchievementUnlockEntry> achievementUnlocks = new List<AchievementUnlockEntry>();

        // Legacy/session tasks (kept for now).
        public List<ActiveTaskState> activeSessionTasks = new List<ActiveTaskState>();

        // NEW: Daily task matrix state (persists across launches; resets on day rollover).
        public DailyTaskMatrixState dailyTasks = new DailyTaskMatrixState();

        // Simple reward currency for Tasks/Achievements (arbitrary for now)
        public int currency = 0;

        // Race / rescue progression
        public List<string> completedRaceChampionshipIds = new List<string>();

        // Legacy sequential unlocks kept for save compatibility / gradual migration.
        public List<int> permanentlyUnlockedPassLevels = new List<int>();

        // New non-sequential permanent pass unlocks.
        public List<string> permanentlyUnlockedPassIds = new List<string>();

        // Resort progression
        public List<string> permanentlyUnlockedResortIds = new List<string>();
        public List<ActiveResortRentalState> activeResortRentals = new List<ActiveResortRentalState>();
        public List<RegionReputationState> regionReputations = new List<RegionReputationState>();
        public List<string> discoveryRewardedPoiIds = new List<string>();

        public int rescueCareerRank = 1;
        public int rescueMissionsCompleted = 0;
        public int rescueMissionsFailed = 0;
        public float rescueBestCompletionSeconds = -1f;
        public int rescueMedicalSupplyUsesRemaining = 0;
        public int rescueRespawnBeaconUsesRemaining = 0;
        public int rescueSnowmobileDispatchUsesRemaining = 0;
        public DateTimeUtc rescueDispatchRefreshUtc = new DateTimeUtc();
        public bool rescueBeaconActive = false;
        public Vector3 rescueBeaconWorldPosition = Vector3.zero;
        public DateTimeUtc rescueBeaconExpiryUtc = new DateTimeUtc();

        // ---------------- Customization (New) ----------------

        public CustomizationState customization = new CustomizationState();

        [Serializable]
        public sealed class CustomizationColorOverride
        {
            public string channelId;
            public Color color = Color.white;
        }

        [Serializable]
        public sealed class CustomizationState
        {
            // All purchased/unlocked customization option IDs (cosmetics + gear unlocks).
            public List<string> unlockedCustomizationIds = new List<string>();

            // ---- Equipped cosmetics ----
            // NOTE: Skin is now colour-only; patterns are repurposed as gear textures.
            // Keep this for legacy save compatibility ONLY (v6 and older).
            public string equippedSkinPatternId;

            public string equippedEyeIconId;
            public string equippedHatId;
            public string equippedJacketId;
            public string equippedGlovesId;
            public string equippedBootsId;
            public string equippedAccessoryId;

            // ---- Equipped gear ----
            public string equippedSkisId;
            public string equippedPolesId;

            // ---- Equipped gear patterns (NEW) ----
            // These IDs refer to your pattern options (we're currently reusing the SkinPattern option type as GearPattern).
            public string equippedSkisPatternId;
            public string equippedPolesPatternId;

            // Pattern ids per target (SkinPattern options are repurposed as gear textures)
            public string equippedHatPatternId;
            public string equippedJacketPatternId; // jacket == cloak
            public string equippedGlovesPatternId;
            public string equippedBootsPatternId;
            public string equippedAccessoryPatternId;

            // ---- Continuous selections ----
            public Color skinColor = Color.white;
            public Color eyeColor = Color.white;
            public Color eyeOutlineColor = new Color(0f, 0f, 0f, 0f);
            public float eyeSize = 3f;
            public Color skisColor = Color.white;
            public Color polesColor = Color.white;

            // Hat/Jacket continuous colours
            public Color hatColor = Color.white;
            public Color jacketColor = Color.white;
            public Color glovesColor = Color.white;
            public Color bootsColor = Color.white;
            public Color accessoryColor = Color.white;

            // Optional extra per-slot colour channels (e.g. trim / strap / lining)
            public List<CustomizationColorOverride> skisExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> polesExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> hatExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> jacketExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> glovesExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> bootsExtraColors = new List<CustomizationColorOverride>();
            public List<CustomizationColorOverride> accessoryExtraColors = new List<CustomizationColorOverride>();

            public bool customizationInitialized;

            // ---- Has user overridden defaults? ----
            public bool hasSetSkinColor;
            public bool hasSetEyeColor;
            public bool hasSetEyeOutlineColor;
            public bool hasSetEyeSize;
            public bool hasSetSkisColor;
            public bool hasSetPolesColor;
            public bool hasSetHatColor;
            public bool hasSetJacketColor;
            public bool hasSetGlovesColor;
            public bool hasSetBootsColor;
            public bool hasSetAccessoryColor;

            // ---- Gear display mode toggles ----
            // True = show equipped gear defaults; False = show saved custom selections.
            public bool skisUseDefaultColor = true;
            public bool polesUseDefaultColor = true;
            public bool hatUseDefaultColor = true;
            public bool jacketUseDefaultColor = true;
            public bool glovesUseDefaultColor = true;
            public bool bootsUseDefaultColor = true;
            public bool accessoryUseDefaultColor = true;

            public bool skisUseDefaultPattern = true;
            public bool polesUseDefaultPattern = true;
            public bool hatUseDefaultPattern = true;
            public bool jacketUseDefaultPattern = true;
            public bool glovesUseDefaultPattern = true;
            public bool bootsUseDefaultPattern = true;
            public bool accessoryUseDefaultPattern = true;


            public bool IsUnlocked(string id)
            {
                if (string.IsNullOrEmpty(id)) return false;
                return unlockedCustomizationIds != null && unlockedCustomizationIds.Contains(id);
            }

            public void Unlock(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                unlockedCustomizationIds ??= new List<string>();
                if (!unlockedCustomizationIds.Contains(id))
                    unlockedCustomizationIds.Add(id);
            }

            /// <summary>
            /// v6 and older used equippedSkinPatternId for a player body pattern.
            /// We now use patterns as gear textures. If old profiles only have equippedSkinPatternId,
            /// default both skis/poles patterns to that value.
            /// </summary>
            public void MigrateLegacySkinPatternToGearPatterns()
            {

                if (!string.IsNullOrEmpty(equippedSkinPatternId))
                {
                    if (string.IsNullOrEmpty(equippedSkisPatternId)) equippedSkisPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedPolesPatternId)) equippedPolesPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedHatPatternId)) equippedHatPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedJacketPatternId)) equippedJacketPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedGlovesPatternId)) equippedGlovesPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedBootsPatternId)) equippedBootsPatternId = equippedSkinPatternId;
                    if (string.IsNullOrEmpty(equippedAccessoryPatternId)) equippedAccessoryPatternId = equippedSkinPatternId;
                }
            }

            public List<CustomizationColorOverride> GetExtraColorList(string slotKey)
            {
                switch (slotKey)
                {
                    case "Skis": return skisExtraColors ??= new List<CustomizationColorOverride>();
                    case "Poles": return polesExtraColors ??= new List<CustomizationColorOverride>();
                    case "Hat": return hatExtraColors ??= new List<CustomizationColorOverride>();
                    case "Jacket": return jacketExtraColors ??= new List<CustomizationColorOverride>();
                    case "Gloves": return glovesExtraColors ??= new List<CustomizationColorOverride>();
                    case "Boots": return bootsExtraColors ??= new List<CustomizationColorOverride>();
                    case "Accessory": return accessoryExtraColors ??= new List<CustomizationColorOverride>();
                    default: return null;
                }
            }

            public bool TryGetExtraColor(string slotKey, string channelId, out Color color)
            {
                color = Color.white;
                if (string.IsNullOrEmpty(slotKey) || string.IsNullOrEmpty(channelId))
                    return false;

                var list = GetExtraColorList(slotKey);
                if (list == null) return false;

                for (int i = 0; i < list.Count; i++)
                {
                    var entry = list[i];
                    if (entry == null || string.IsNullOrEmpty(entry.channelId))
                        continue;

                    if (string.Equals(entry.channelId, channelId, StringComparison.Ordinal))
                    {
                        color = entry.color;
                        return true;
                    }
                }

                return false;
            }

            public void SetExtraColor(string slotKey, string channelId, Color color)
            {
                if (string.IsNullOrEmpty(slotKey) || string.IsNullOrEmpty(channelId))
                    return;

                var list = GetExtraColorList(slotKey);
                if (list == null) return;

                for (int i = 0; i < list.Count; i++)
                {
                    var entry = list[i];
                    if (entry == null || string.IsNullOrEmpty(entry.channelId))
                        continue;

                    if (string.Equals(entry.channelId, channelId, StringComparison.Ordinal))
                    {
                        entry.color = color;
                        return;
                    }
                }

                list.Add(new CustomizationColorOverride
                {
                    channelId = channelId,
                    color = color
                });
            }

            public void ClearExtraColor(string slotKey, string channelId)
            {
                if (string.IsNullOrEmpty(slotKey) || string.IsNullOrEmpty(channelId))
                    return;

                var list = GetExtraColorList(slotKey);
                if (list == null) return;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var entry = list[i];
                    if (entry == null || string.IsNullOrEmpty(entry.channelId))
                        continue;

                    if (string.Equals(entry.channelId, channelId, StringComparison.Ordinal))
                        list.RemoveAt(i);
                }
            }

        }

        [Serializable]
        public sealed class TutorialState
        {
            public bool skiLessonsPending = true;
            public bool skiLessonsCompleted = false;
            public bool skiLessonsAccepted = false;
            public bool skiLessonsDeferred = false;
            public int skiLessonStepIndex = 0;

            public bool IsSkiLessonsActive => skiLessonsPending && skiLessonsAccepted && !skiLessonsCompleted;
            public bool CanOfferSkiLessons => skiLessonsPending && !skiLessonsCompleted && !skiLessonsAccepted && !skiLessonsDeferred;

            public void ResetForNewGame()
            {
                skiLessonsPending = true;
                skiLessonsCompleted = false;
                skiLessonsAccepted = false;
                skiLessonsDeferred = false;
                skiLessonStepIndex = 0;
            }

            public void AcceptLessons()
            {
                skiLessonsPending = true;
                skiLessonsCompleted = false;
                skiLessonsAccepted = true;
                skiLessonsDeferred = false;
            }

            public void DeferLessons()
            {
                skiLessonsPending = true;
                skiLessonsCompleted = false;
                skiLessonsAccepted = false;
                skiLessonsDeferred = true;
                skiLessonStepIndex = 0;
            }

            public void MarkCompleted()
            {
                skiLessonsPending = false;
                skiLessonsCompleted = true;
                skiLessonsAccepted = false;
                skiLessonsDeferred = false;
                skiLessonStepIndex = 0;
            }
        }

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

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Best-effort save migration.
            // JsonUtility typically routes through Unity serialization; this keeps older profiles usable.
            EnsureUpToDate();
        }

        public void EnsureUpToDate()
        {
            if (profileVersion < 8)
            {
                if (customization != null)
                {
                    // If the player had previously set a gear colour, treat it as "custom mode"
                    customization.skisUseDefaultColor = !customization.hasSetSkisColor;
                    customization.polesUseDefaultColor = !customization.hasSetPolesColor;
                    customization.hatUseDefaultColor = !customization.hasSetHatColor;
                    customization.jacketUseDefaultColor = !customization.hasSetJacketColor;

                    // If a pattern id is present, treat it as "custom pattern"
                    customization.skisUseDefaultPattern = string.IsNullOrEmpty(customization.equippedSkisPatternId);
                    customization.polesUseDefaultPattern = string.IsNullOrEmpty(customization.equippedPolesPatternId);
                    customization.hatUseDefaultPattern = string.IsNullOrEmpty(customization.equippedHatPatternId);
                    customization.jacketUseDefaultPattern = string.IsNullOrEmpty(customization.equippedJacketPatternId);
                }

                profileVersion = 8;
            }

            if (profileVersion < 9)
            {
                quests ??= new QuestLogState();
                profileVersion = 9;
            }

            if (profileVersion < 10)
            {
                progression ??= new ExtendedProgressionStats();
                profileVersion = 10;
            }

            if (profileVersion < 11)
            {
                permanentlyUnlockedResortIds ??= new List<string>();
                activeResortRentals ??= new List<ActiveResortRentalState>();
                regionReputations ??= new List<RegionReputationState>();
                discoveryRewardedPoiIds ??= new List<string>();
                profileVersion = 11;
            }

            if (profileVersion < 12)
            {
                if (customization != null)
                {
                    customization.glovesUseDefaultColor = !customization.hasSetGlovesColor;
                    customization.bootsUseDefaultColor = !customization.hasSetBootsColor;
                    customization.accessoryUseDefaultColor = !customization.hasSetAccessoryColor;
                }

                profileVersion = 12;
            }

            if (profileVersion < 13)
            {
                if (customization != null)
                {
                    customization.glovesUseDefaultPattern = string.IsNullOrEmpty(customization.equippedGlovesPatternId);
                    customization.bootsUseDefaultPattern = string.IsNullOrEmpty(customization.equippedBootsPatternId);
                    customization.accessoryUseDefaultPattern = string.IsNullOrEmpty(customization.equippedAccessoryPatternId);
                }

                profileVersion = 13;
            }

        }

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
        public sealed class ExtendedProgressionStats
        {
            public int lifetimeRaceStarts;
            public int lifetimeRaceCompletions;
            public int lifetimeRaceWins;
            public int lifetimeRacePodiums;
            public int lifetimeRacePersonalBestImprovements;
            public List<string> completedRaceIds = new List<string>();

            public int lifetimeRescueStarts;
            public int lifetimeRescueUtilityUses;

            public int lifetimeQuestsAccepted;
            public int lifetimeQuestStagesCompleted;
            public int lifetimeTutorialQuestsCompleted;

            public int sessionTricksLanded;
            public int lifetimeTricksLanded;
            public int lifetimeNamedTricksLanded;
            public int lifetimeTrickFails;
            public int bestTrickTier;
            public List<string> landedTrickNames = new List<string>();

            public int lifetimePassPurchases;
            public int lifetimePassExtensions;
            public List<string> ownedPassIds = new List<string>();

            public int lifetimeCustomizationPurchases;

            public int lifetimeCurrencyEarned;
            public int lifetimeCurrencySpent;
            public int largestSingleCurrencyReward;
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

        public bool HasClaimedAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return claimedAchievementIds != null && claimedAchievementIds.Contains(id);
        }

        public bool TryClaimAchievementReward(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!HasAchievement(id)) return false;

            claimedAchievementIds ??= new List<string>();
            if (claimedAchievementIds.Contains(id))
                return false;

            claimedAchievementIds.Add(id);
            return true;
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

        public bool HasPoiDiscoveryReward(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return discoveryRewardedPoiIds != null && discoveryRewardedPoiIds.Contains(id);
        }

        public bool TryMarkPoiDiscoveryRewarded(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            discoveryRewardedPoiIds ??= new List<string>();
            if (discoveryRewardedPoiIds.Contains(id)) return false;
            discoveryRewardedPoiIds.Add(id);
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
            progression ??= new ExtendedProgressionStats();
            progression.sessionTricksLanded = 0;
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
            progression = new ExtendedProgressionStats();

            unlockedAchievementIds.Clear();
            visitedLandmarkIds.Clear();
            sessionVisitedLandmarkIds.Clear();
            runRecords.Clear();
            visitedRunIds.Clear();
            sessionVisitedRunIds.Clear();
            sessionCompletedRunIds.Clear();

            customization = new CustomizationState();

            currency = 0;
            recentTaskIds.Clear();
            recentDailyLadderIds.Clear();
            activeSessionTasks.Clear();

            landmarkVisitCounts?.Clear();
            runVisitCounts?.Clear();
            liftRideCounts?.Clear();

            completedRaceChampionshipIds.Clear();
            permanentlyUnlockedPassLevels.Clear();
            permanentlyUnlockedPassIds.Clear();
            permanentlyUnlockedResortIds.Clear();
            activeResortRentals.Clear();
            regionReputations.Clear();
            discoveryRewardedPoiIds.Clear();
            rescueCareerRank = 1;
            rescueMissionsCompleted = 0;
            rescueMissionsFailed = 0;
            rescueBestCompletionSeconds = -1f;
            rescueMedicalSupplyUsesRemaining = 0;
            rescueRespawnBeaconUsesRemaining = 0;
            rescueSnowmobileDispatchUsesRemaining = 0;
            rescueDispatchRefreshUtc = new DateTimeUtc();
            rescueBeaconActive = false;
            rescueBeaconWorldPosition = Vector3.zero;
            rescueBeaconExpiryUtc = new DateTimeUtc();

            dailyTasks = new DailyTaskMatrixState();

            tutorial = new TutorialState();
            quests = new QuestLogState();
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
            playthrough ??= new PlaythroughState();

            customization ??= new CustomizationState();
            customization.unlockedCustomizationIds ??= new List<string>();

            dailyTasks ??= new DailyTaskMatrixState();
            dailyTasks.rows ??= new List<DailyTaskRowState>();

            landmarkVisitCounts ??= new List<IdCountEntry>();
            sessionLandmarkVisitCounts ??= new List<IdCountEntry>();

            runVisitCounts ??= new List<IdCountEntry>();
            sessionRunVisitCounts ??= new List<IdCountEntry>();

            liftRideCounts ??= new List<IdCountEntry>();
            sessionLiftRideCounts ??= new List<IdCountEntry>();

            completedRaceChampionshipIds ??= new List<string>();
            permanentlyUnlockedPassLevels ??= new List<int>();
            permanentlyUnlockedPassIds ??= new List<string>();
            permanentlyUnlockedResortIds ??= new List<string>();
            activeResortRentals ??= new List<ActiveResortRentalState>();
            regionReputations ??= new List<RegionReputationState>();
            discoveryRewardedPoiIds ??= new List<string>();
            rescueCareerRank = Mathf.Max(1, rescueCareerRank);
            rescueMissionsCompleted = Mathf.Max(0, rescueMissionsCompleted);
            rescueMissionsFailed = Mathf.Max(0, rescueMissionsFailed);
            rescueMedicalSupplyUsesRemaining = Mathf.Max(0, rescueMedicalSupplyUsesRemaining);
            rescueRespawnBeaconUsesRemaining = Mathf.Max(0, rescueRespawnBeaconUsesRemaining);
            rescueSnowmobileDispatchUsesRemaining = Mathf.Max(0, rescueSnowmobileDispatchUsesRemaining);
            if (float.IsNaN(rescueBestCompletionSeconds) || float.IsInfinity(rescueBestCompletionSeconds))
                rescueBestCompletionSeconds = -1f;
            if (rescueBeaconExpiryUtc.unixSeconds <= 0)
                rescueBeaconActive = false;

            if (lifetime == null) lifetime = new LifetimeStats();
            if (session == null) session = new SessionStats();
            progression ??= new ExtendedProgressionStats();
            progression.completedRaceIds ??= new List<string>();
            progression.landedTrickNames ??= new List<string>();
            progression.ownedPassIds ??= new List<string>();
            progression.lifetimeRaceStarts = Mathf.Max(0, progression.lifetimeRaceStarts);
            progression.lifetimeRaceCompletions = Mathf.Max(0, progression.lifetimeRaceCompletions);
            progression.lifetimeRaceWins = Mathf.Max(0, progression.lifetimeRaceWins);
            progression.lifetimeRacePodiums = Mathf.Max(0, progression.lifetimeRacePodiums);
            progression.lifetimeRacePersonalBestImprovements = Mathf.Max(0, progression.lifetimeRacePersonalBestImprovements);
            progression.lifetimeRescueStarts = Mathf.Max(0, progression.lifetimeRescueStarts);
            progression.lifetimeRescueUtilityUses = Mathf.Max(0, progression.lifetimeRescueUtilityUses);
            progression.lifetimeQuestsAccepted = Mathf.Max(0, progression.lifetimeQuestsAccepted);
            progression.lifetimeQuestStagesCompleted = Mathf.Max(0, progression.lifetimeQuestStagesCompleted);
            progression.lifetimeTutorialQuestsCompleted = Mathf.Max(0, progression.lifetimeTutorialQuestsCompleted);
            progression.sessionTricksLanded = Mathf.Max(0, progression.sessionTricksLanded);
            progression.lifetimeTricksLanded = Mathf.Max(0, progression.lifetimeTricksLanded);
            progression.lifetimeNamedTricksLanded = Mathf.Max(0, progression.lifetimeNamedTricksLanded);
            progression.lifetimeTrickFails = Mathf.Max(0, progression.lifetimeTrickFails);
            progression.bestTrickTier = Mathf.Max(0, progression.bestTrickTier);
            progression.lifetimePassPurchases = Mathf.Max(0, progression.lifetimePassPurchases);
            progression.lifetimePassExtensions = Mathf.Max(0, progression.lifetimePassExtensions);
            progression.lifetimeCustomizationPurchases = Mathf.Max(0, progression.lifetimeCustomizationPurchases);
            progression.lifetimeCurrencyEarned = Mathf.Max(0, progression.lifetimeCurrencyEarned);
            progression.lifetimeCurrencySpent = Mathf.Max(0, progression.lifetimeCurrencySpent);
            progression.largestSingleCurrencyReward = Mathf.Max(0, progression.largestSingleCurrencyReward);

            tutorial ??= new TutorialState();
            quests ??= new QuestLogState();
            quests.questStates ??= new List<QuestRuntimeState>();
            quests.completedQuestIds ??= new List<string>();
            quests.trackedQuestIds ??= new List<string>();
        }

        /// <summary>
        /// Removes non-completion run attempts whose covered fraction is below the threshold.
        /// Returns number of removed attempts.
        /// </summary>
        public int PruneRunSegmentsBelowCoverage(float minCoverageFraction01, bool removeEmptyRecords, bool removeNoCoverageSegments)
        {
            minCoverageFraction01 = Mathf.Clamp01(minCoverageFraction01);

            if (runRecords == null || runRecords.Count == 0)
            {
                Debug.Log("No run records.");
                return 0;
            }
            int removed = 0;

            // Iterate run records
            for (int r = runRecords.Count - 1; r >= 0; r--)
            {
                var rec = runRecords[r];
                if (rec == null)
                {
                    runRecords.RemoveAt(r);
                    continue;
                }

                if (rec.attempts == null || rec.attempts.Count == 0)
                {
                    if (removeEmptyRecords && rec.timesCompleted <= 0)
                        runRecords.RemoveAt(r);

                    continue;
                }

                // Remove attempts under threshold / missing coverage / 0s
                for (int i = rec.attempts.Count - 1; i >= 0; i--)
                {
                    var a = rec.attempts[i];
                    if (a == null)
                    {
                        rec.attempts.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    // NEW: prune "0-second" attempts as they appear in UI (usually < 1s and formats to 00:00)
                    const float minSecondsToKeep = 5.0f; // tune: 0.5f if you want to be less aggressive

                    if (a.timeSeconds < minSecondsToKeep || float.IsNaN(a.timeSeconds) || float.IsInfinity(a.timeSeconds))
                    {
                        rec.attempts.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    // Keep full completions always (after the 0s prune)
                    if (a.isCompletion)
                        continue;

                    bool hasCoverage = TryGetCoveredFraction01Safe(a, out float cov01);

                    // Remove entries that have no progress percentage at all (optional)
                    if (removeNoCoverageSegments && !hasCoverage)
                    {
                        rec.attempts.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    // Remove partial segments below threshold
                    if (hasCoverage && cov01 < minCoverageFraction01)
                    {
                        rec.attempts.RemoveAt(i);
                        removed++;
                        continue;
                    }
                }

                // After pruning, rebuild cached aggregates so UI doesn't show stale best stats / completions.
                rec.RebuildAggregatesFromAttempts();

                // Optionally remove empty record entries (no completions + no attempts)
                if (removeEmptyRecords && rec.timesCompleted <= 0 && (rec.attempts == null || rec.attempts.Count == 0))
                {
                    runRecords.RemoveAt(r);
                }
            }

            return removed;
        }


        public int ResetRunRecords()
        {
            if (runRecords == null || runRecords.Count == 0)
            {
                Debug.Log("[PlayerStatsProfile] Run records are already empty.");
                return 0;
            }

            int removed = 0;

            for (int r = 0; r < runRecords.Count; r++)
            {
                var rec = runRecords[r];
                if (rec?.attempts == null) continue;

                removed += rec.attempts.Count;
                rec.attempts.Clear();

                // Ensure caches are cleared too.
                rec.RebuildAggregatesFromAttempts();
            }

            // If you cleared all attempts, your lifetime totals should also reflect that.
            RecalculateLifetimeRunAggregatesFromRunRecords();

            return removed;
        }

        // --- helpers ---

        public int CountRunAttemptsWhere(System.Func<RunAttemptEntry, bool> predicate)
        {
            if (predicate == null || runRecords == null) return 0;

            int count = 0;
            for (int r = 0; r < runRecords.Count; r++)
            {
                var rec = runRecords[r];
                if (rec?.attempts == null) continue;

                for (int i = 0; i < rec.attempts.Count; i++)
                    if (predicate(rec.attempts[i])) count++;
            }
            return count;
        }

        private static bool TryGetCoveredFraction01Safe(RunAttemptEntry a, out float cov01)
        {
            cov01 = 0f;
            if (a == null) return false;

            // Primary new field
            float cov = a.coveredFraction01;

            // Fallback: derive from min/max meters
            if (cov <= 0f && a.runLengthMeters > 0.001f && a.maxDistanceMeters > a.minDistanceMeters)
                cov = (a.maxDistanceMeters - a.minDistanceMeters) / a.runLengthMeters;

            // Fallback: approximate from traveled distance
            if (cov <= 0f && a.runLengthMeters > 0.001f)
            {
                float dist = Mathf.Max(0f, a.onRouteDistanceMeters + a.offRouteDistanceMeters);
                if (dist > 0f) cov = dist / a.runLengthMeters;
            }

            // Validate
            if (float.IsNaN(cov) || float.IsInfinity(cov))
                return false;

            cov01 = Mathf.Clamp01(cov);

            // Treat effectively-zero as "no coverage" (missing) for pruning purposes
            return cov01 > 0.0001f;
        }

        private static void RemoveAllIdMatches(List<string> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == id)
                    list.RemoveAt(i);
        }

        private static void RemoveIdCountEntry(List<IdCountEntry> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].id == id)
                    list.RemoveAt(i);
        }

        /// <summary>
        /// Recomputes lifetime run aggregates from runRecords/attempts (source of truth).
        /// Call this after pruning/deleting run attempts/records.
        /// </summary>
        public void RecalculateLifetimeRunAggregatesFromRunRecords()
        {
            lifetime ??= new LifetimeStats();

            int totalCompletions = 0;
            int totalCleanCompletions = 0;
            float topRunSpeed = 0f;

            if (runRecords != null)
            {
                for (int r = 0; r < runRecords.Count; r++)
                {
                    var rec = runRecords[r];
                    if (rec == null) continue;

                    // Ensure record caches are consistent with attempts.
                    rec.RebuildAggregatesFromAttempts();

                    if (rec.attempts == null) continue;

                    for (int i = 0; i < rec.attempts.Count; i++)
                    {
                        var a = rec.attempts[i];
                        if (a == null || !a.isCompletion) continue;

                        totalCompletions++;
                        if (a.stacks == 0) totalCleanCompletions++;
                        if (a.topSpeedMps > topRunSpeed) topRunSpeed = a.topSpeedMps;
                    }
                }
            }

            lifetime.totalRunsCompleted = totalCompletions;
            lifetime.totalRunsCompletedClean = totalCleanCompletions;
            lifetime.topRunSpeedMps = topRunSpeed;
        }

        /// <summary>
        /// Removes a single run's record, and optionally also clears visits/counts/session references.
        /// </summary>
        public bool RemoveRunRecordCompletely(
            string runId,
            bool removeVisits = true,
            bool removeCounts = true,
            bool removeSessionRefs = true,
            bool recalcLifetimeRunAggregates = true)
        {
            if (string.IsNullOrEmpty(runId)) return false;

            bool removed = false;

            if (runRecords != null)
            {
                for (int i = runRecords.Count - 1; i >= 0; i--)
                {
                    var rr = runRecords[i];
                    if (rr != null && rr.runId == runId)
                    {
                        runRecords.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }

            if (!removed) return false;

            if (removeVisits)
            {
                RemoveAllIdMatches(visitedRunIds, runId);
                if (removeSessionRefs)
                    RemoveAllIdMatches(sessionVisitedRunIds, runId);
            }

            if (removeCounts)
            {
                RemoveIdCountEntry(runVisitCounts, runId);
                if (removeSessionRefs)
                    RemoveIdCountEntry(sessionRunVisitCounts, runId);
            }

            if (removeSessionRefs)
                RemoveAllIdMatches(sessionCompletedRunIds, runId);

            if (recalcLifetimeRunAggregates)
                RecalculateLifetimeRunAggregatesFromRunRecords();

            return true;
        }

        /// <summary>
        /// Hard clears ALL run history (records + optional visit/count/session caches),
        /// then rebuilds lifetime run aggregates to zero.
        /// </summary>
        public int ClearAllRunHistory(
            bool clearVisits = true,
            bool clearCounts = true,
            bool clearSessionRefs = true,
            bool recalcLifetimeRunAggregates = true)
        {
            int removedRecords = runRecords != null ? runRecords.Count : 0;

            runRecords?.Clear();

            if (clearVisits)
            {
                visitedRunIds?.Clear();
                if (clearSessionRefs) sessionVisitedRunIds?.Clear();
            }

            if (clearCounts)
            {
                runVisitCounts?.Clear();
                if (clearSessionRefs) sessionRunVisitCounts?.Clear();
            }

            if (clearSessionRefs)
                sessionCompletedRunIds?.Clear();

            // Also zero session run aggregates, so the UI doesn't still show “this session” bests.
            if (clearSessionRefs && session != null)
            {
                session.runsCompleted = 0;
                session.runsCompletedClean = 0;
                session.topRunSpeedMps = 0f;
            }

            if (recalcLifetimeRunAggregates)
                RecalculateLifetimeRunAggregatesFromRunRecords();

            return removedRecords;
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

        public int AttemptCount => attempts != null ? attempts.Count : 0;

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

            if (attempt == null) return;

            // Always accumulate distance spent on this run (partials included)
            float attemptDist = attempt.onRouteDistanceMeters + attempt.offRouteDistanceMeters;
            if (attemptDist > 0f)
                totalDistanceMeters += attemptDist;

            // Only count "completion" aggregates when this attempt is a full completion
            if (!attempt.isCompletion)
                return;

            RegisterCompletion(attempt.timeSeconds, attempt.topSpeedMps, attemptDist);

            // Clean completion tracking (0 stacks)
            if (attempt.stacks == 0)
            {
                timesCompletedClean++;

                if (attempt.timeSeconds > 0f)
                {
                    if (bestCleanTimeSeconds < 0f || attempt.timeSeconds < bestCleanTimeSeconds)
                        bestCleanTimeSeconds = attempt.timeSeconds;
                }
            }
        }

        public void RebuildAggregatesFromAttempts()
        {
            timesCompleted = 0;
            timesCompletedClean = 0;

            bestTimeSeconds = -1f;
            bestCleanTimeSeconds = -1f;

            bestTopSpeedMps = 0f;
            totalDistanceMeters = 0f;

            if (attempts == null || attempts.Count == 0)
                return;

            for (int i = 0; i < attempts.Count; i++)
            {
                var a = attempts[i];
                if (a == null) continue;

                float attemptDist = Mathf.Max(0f, a.onRouteDistanceMeters) + Mathf.Max(0f, a.offRouteDistanceMeters);
                if (attemptDist > 0f)
                    totalDistanceMeters += attemptDist;

                if (!a.isCompletion)
                    continue;

                timesCompleted++;

                if (a.timeSeconds > 0f)
                {
                    if (bestTimeSeconds < 0f || a.timeSeconds < bestTimeSeconds)
                        bestTimeSeconds = a.timeSeconds;
                }

                if (a.topSpeedMps > bestTopSpeedMps)
                    bestTopSpeedMps = a.topSpeedMps;

                if (a.stacks == 0)
                {
                    timesCompletedClean++;

                    if (a.timeSeconds > 0f)
                    {
                        if (bestCleanTimeSeconds < 0f || a.timeSeconds < bestCleanTimeSeconds)
                            bestCleanTimeSeconds = a.timeSeconds;
                    }
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

        // In-game date/time snapshot at completion (TimeController)
        public int gameYear;
        public int gameMonthIndex;     // 0..monthPresets.Length-1
        public int gameDayOfMonth;     // 1..daysInMonth
        public int gameDayOfWeek;      // 0=Mon .. 6=Sun
        public float gameTimeOfDay;    // 0..24 (hours)

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
        // --- Partial segment support (v6) ---
        public bool isCompletion;

        // Length snapshot at time of attempt (stable even if authoring changes later)
        public float runLengthMeters;

        // Where on the run they entered/exited (meters along centerline)
        public float entryDistanceMeters;
        public float exitDistanceMeters;

        // Full interval covered during the attempt (meters along centerline)
        public float minDistanceMeters;
        public float maxDistanceMeters;

        // Normalized (0..1) versions for UI
        public float entryFraction01;
        public float exitFraction01;
        public float coveredMinFraction01;
        public float coveredMaxFraction01;
        public float coveredFraction01;


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

    [Serializable]
    public sealed class PlaythroughState
    {
        public int playthroughId = 0;

        // "Week 1 Day 1" baseline for all calendar UIs (in-game day-of-year).
        public int calendarStartDayOfYear = -1;
        public int calendarStartYear = 0;

        public DateTimeUtc startedUtc = DateTimeUtc.Now();
    }

    [Serializable]
    public class DailyShopState
    {
        public int dayKey = -1;
        public List<OfferEntry> offers = new();
    }

    [Serializable]
    public class OfferEntry
    {
        public string optionId;
        public int remaining;
        public int initial;
    }


}

