using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    public static class RaceRescueProgression
    {
        private static readonly int[] RescueRankCompletionThresholds = { 0, 3, 6, 10 };
        private const long RescueDispatchRefreshIntervalSeconds = 7L * 24L * 60L * 60L;
        private const long RescueBeaconLifetimeSeconds = 3L * 24L * 60L * 60L;

        public enum RescueDispatchUtility
        {
            MedicalSupplyDrop = 0,
            RespawnBeacon = 1,
            SnowmobileDispatch = 2
        }

        public static List<string> EnsureList(List<string> list)
        {
            return list ?? new List<string>();
        }

        public static List<int> EnsureIntList(List<int> list)
        {
            return list ?? new List<int>();
        }

        public static bool IsPassIdPermanentlyUnlocked(string passId)
        {
            if (string.IsNullOrWhiteSpace(passId))
                return false;

            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null)
                return false;

            profile.permanentlyUnlockedPassIds = EnsureList(profile.permanentlyUnlockedPassIds);
            return profile.permanentlyUnlockedPassIds.Contains(passId.Trim());
        }

        public static List<string> GetPermanentlyUnlockedPassIds()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null)
                return new List<string>();

            profile.permanentlyUnlockedPassIds = EnsureList(profile.permanentlyUnlockedPassIds);
            return profile.permanentlyUnlockedPassIds;
        }

        public static bool IsPassLevelPermanentlyUnlocked(int passLevel)
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null)
                return false;

            profile.permanentlyUnlockedPassLevels = EnsureIntList(profile.permanentlyUnlockedPassLevels);
            return profile.permanentlyUnlockedPassLevels.Contains(Mathf.Max(0, passLevel));
        }

        public static int GetHighestPermanentlyUnlockedPassLevel()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null || profile.permanentlyUnlockedPassLevels == null || profile.permanentlyUnlockedPassLevels.Count == 0)
                return -1;

            int best = -1;
            for (int i = 0; i < profile.permanentlyUnlockedPassLevels.Count; i++)
                best = Mathf.Max(best, profile.permanentlyUnlockedPassLevels[i]);

            return best;
        }

        public static bool IsRaceChampionshipCompleted(string championshipRaceId)
        {
            if (string.IsNullOrWhiteSpace(championshipRaceId))
                return false;

            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null)
                return false;

            profile.completedRaceChampionshipIds = EnsureList(profile.completedRaceChampionshipIds);
            return profile.completedRaceChampionshipIds.Contains(championshipRaceId.Trim());
        }

        public static bool TryUnlockPermanentPassId(string passId, out bool newlyUnlocked)
        {
            newlyUnlocked = false;

            if (string.IsNullOrWhiteSpace(passId))
                return false;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return false;

            profile.permanentlyUnlockedPassIds = EnsureList(profile.permanentlyUnlockedPassIds);

            string normalizedId = passId.Trim();
            if (profile.permanentlyUnlockedPassIds.Contains(normalizedId))
                return false;

            profile.permanentlyUnlockedPassIds.Add(normalizedId);
            ProgressionEventRecorder.RecordPassOwnership(profile, normalizedId);
            mgr.Save();

            newlyUnlocked = true;
            return true;
        }

        public static bool TryMarkRaceChampionshipCompleted(string championshipRaceId, string rewardedPassId, out bool newlyCompleted)
        {
            newlyCompleted = false;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || string.IsNullOrWhiteSpace(championshipRaceId))
                return false;

            string normalizedId = championshipRaceId.Trim();
            profile.completedRaceChampionshipIds = EnsureList(profile.completedRaceChampionshipIds);
            profile.permanentlyUnlockedPassIds = EnsureList(profile.permanentlyUnlockedPassIds);

            bool changed = false;

            if (!profile.completedRaceChampionshipIds.Contains(normalizedId))
            {
                profile.completedRaceChampionshipIds.Add(normalizedId);
                changed = true;
                newlyCompleted = true;
            }

            if (!string.IsNullOrWhiteSpace(rewardedPassId))
            {
                string normalizedPassId = rewardedPassId.Trim();
                if (!profile.permanentlyUnlockedPassIds.Contains(normalizedPassId))
                {
                    profile.permanentlyUnlockedPassIds.Add(normalizedPassId);
                    ProgressionEventRecorder.RecordPassOwnership(profile, normalizedPassId);
                    changed = true;
                }
            }

            if (changed)
                mgr.Save();

            return changed;
        }

        public static bool TryMarkRaceChampionshipCompleted(string championshipRaceId, int rewardedPassLevel, out bool newlyCompleted)
        {
            newlyCompleted = false;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || string.IsNullOrWhiteSpace(championshipRaceId))
                return false;

            string normalizedId = championshipRaceId.Trim();
            profile.completedRaceChampionshipIds = EnsureList(profile.completedRaceChampionshipIds);
            profile.permanentlyUnlockedPassLevels = EnsureIntList(profile.permanentlyUnlockedPassLevels);

            bool changed = false;

            if (!profile.completedRaceChampionshipIds.Contains(normalizedId))
            {
                profile.completedRaceChampionshipIds.Add(normalizedId);
                changed = true;
                newlyCompleted = true;
            }

            int clampedPassLevel = Mathf.Max(0, rewardedPassLevel);
            if (clampedPassLevel > 0 && !profile.permanentlyUnlockedPassLevels.Contains(clampedPassLevel))
            {
                profile.permanentlyUnlockedPassLevels.Add(clampedPassLevel);
                changed = true;
            }

            if (changed)
                mgr.Save();

            return changed;
        }

        public static int GetRescueCareerRank()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            return profile != null ? Mathf.Max(1, profile.rescueCareerRank) : 1;
        }

        public static int GetRescueMissionCompletionCount()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            return profile != null ? Mathf.Max(0, profile.rescueMissionsCompleted) : 0;
        }

        public static int GetRescueMissionFailureCount()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            return profile != null ? Mathf.Max(0, profile.rescueMissionsFailed) : 0;
        }

        public static float GetRescueBestCompletionSeconds()
        {
            var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
            if (profile == null)
                return -1f;

            float best = profile.rescueBestCompletionSeconds;
            return best > 0f && !float.IsNaN(best) && !float.IsInfinity(best) ? best : -1f;
        }

        public static bool RecordRescueMissionCompleted(int reward, float completionSeconds, out bool rankIncreased, out int newRank)
        {
            rankIncreased = false;
            newRank = 1;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return false;

            int previousRank = Mathf.Max(1, profile.rescueCareerRank);

            profile.rescueMissionsCompleted = Mathf.Max(0, profile.rescueMissionsCompleted) + 1;

            if (completionSeconds > 0f &&
                (profile.rescueBestCompletionSeconds < 0f || completionSeconds < profile.rescueBestCompletionSeconds))
            {
                profile.rescueBestCompletionSeconds = completionSeconds;
            }

            if (reward > 0)
                ProgressionEventRecorder.AddCurrency(profile, reward);

            int resolvedRank = ResolveRescueRank(profile.rescueMissionsCompleted);
            profile.rescueCareerRank = resolvedRank;

            mgr.Save();

            newRank = resolvedRank;
            rankIncreased = resolvedRank > previousRank;
            return true;
        }

        public static bool RecordRescueMissionFailed()
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return false;

            profile.rescueMissionsFailed = Mathf.Max(0, profile.rescueMissionsFailed) + 1;
            mgr.Save();
            return true;
        }

        public static int GetRescueUtilityUsesRemaining(RescueDispatchUtility utility)
        {
            var profile = GetProfileAndRefreshState(saveIfChanged: true, out _);
            if (profile == null)
                return 0;

            return utility switch
            {
                RescueDispatchUtility.MedicalSupplyDrop => Mathf.Max(0, profile.rescueMedicalSupplyUsesRemaining),
                RescueDispatchUtility.RespawnBeacon => Mathf.Max(0, profile.rescueRespawnBeaconUsesRemaining),
                RescueDispatchUtility.SnowmobileDispatch => Mathf.Max(0, profile.rescueSnowmobileDispatchUsesRemaining),
                _ => 0
            };
        }

        public static DateTimeUtc GetRescueDispatchRefreshUtc()
        {
            var profile = GetProfileAndRefreshState(saveIfChanged: true, out _);
            return profile != null ? profile.rescueDispatchRefreshUtc : DateTimeUtc.Now();
        }

        public static bool IsRescueUtilityUnlocked(RescueDispatchUtility utility)
        {
            int rank = GetRescueCareerRank();
            return rank >= GetRequiredRankForUtility(utility);
        }

        public static int GetRequiredRankForUtility(RescueDispatchUtility utility)
        {
            return utility switch
            {
                RescueDispatchUtility.MedicalSupplyDrop => 2,
                RescueDispatchUtility.RespawnBeacon => 3,
                RescueDispatchUtility.SnowmobileDispatch => 4,
                _ => int.MaxValue
            };
        }

        public static bool TryConsumeRescueUtility(RescueDispatchUtility utility, out string failureReason)
        {
            failureReason = string.Empty;

            var mgr = PlayerStatsManager.Instance;
            var profile = GetProfileAndRefreshState(saveIfChanged: true, out _);
            if (profile == null || mgr == null)
            {
                failureReason = "Profile unavailable.";
                return false;
            }

            int requiredRank = GetRequiredRankForUtility(utility);
            if (GetRescueCareerRank() < requiredRank)
            {
                failureReason = $"Requires rescue rank {requiredRank}.";
                return false;
            }

            ref int remaining = ref GetRemainingUsesRef(profile, utility);
            if (remaining <= 0)
            {
                failureReason = "No weekly dispatch uses remaining.";
                return false;
            }

            remaining = Mathf.Max(0, remaining - 1);
            ProgressionEventRecorder.RecordRescueUtilityUse(profile);
            mgr.Save();
            return true;
        }

        public static bool TryGetActiveRescueBeacon(out Vector3 worldPosition, out DateTimeUtc expiryUtc)
        {
            worldPosition = Vector3.zero;
            expiryUtc = new DateTimeUtc();

            var profile = GetProfileAndRefreshState(saveIfChanged: true, out _);
            if (profile == null || !profile.rescueBeaconActive)
                return false;

            if (profile.rescueBeaconExpiryUtc.unixSeconds <= DateTimeUtc.Now().unixSeconds)
            {
                ClearActiveRescueBeacon();
                return false;
            }

            worldPosition = profile.rescueBeaconWorldPosition;
            expiryUtc = profile.rescueBeaconExpiryUtc;
            return true;
        }

        public static bool SetActiveRescueBeacon(Vector3 worldPosition)
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = GetProfileAndRefreshState(saveIfChanged: false, out _);
            if (profile == null || mgr == null)
                return false;

            long now = DateTimeUtc.Now().unixSeconds;
            profile.rescueBeaconActive = true;
            profile.rescueBeaconWorldPosition = worldPosition;
            profile.rescueBeaconExpiryUtc = new DateTimeUtc { unixSeconds = now + RescueBeaconLifetimeSeconds };
            mgr.Save();
            return true;
        }

        public static bool ClearActiveRescueBeacon()
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return false;

            bool hadBeacon = profile.rescueBeaconActive;
            profile.rescueBeaconActive = false;
            profile.rescueBeaconWorldPosition = Vector3.zero;
            profile.rescueBeaconExpiryUtc = new DateTimeUtc();

            if (hadBeacon)
                mgr.Save();

            return hadBeacon;
        }

        private static PlayerStatsProfile GetProfileAndRefreshState(bool saveIfChanged, out bool changed)
        {
            changed = false;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return null;

            long now = DateTimeUtc.Now().unixSeconds;

            if (profile.rescueBeaconActive && profile.rescueBeaconExpiryUtc.unixSeconds > 0 && profile.rescueBeaconExpiryUtc.unixSeconds <= now)
            {
                profile.rescueBeaconActive = false;
                profile.rescueBeaconWorldPosition = Vector3.zero;
                profile.rescueBeaconExpiryUtc = new DateTimeUtc();
                changed = true;
            }

            bool shouldRefreshAllowances = profile.rescueDispatchRefreshUtc.unixSeconds <= 0 ||
                                           profile.rescueDispatchRefreshUtc.unixSeconds <= now;

            if (shouldRefreshAllowances)
            {
                profile.rescueMedicalSupplyUsesRemaining = GetAllowanceForUtility(profile.rescueCareerRank, RescueDispatchUtility.MedicalSupplyDrop);
                profile.rescueRespawnBeaconUsesRemaining = GetAllowanceForUtility(profile.rescueCareerRank, RescueDispatchUtility.RespawnBeacon);
                profile.rescueSnowmobileDispatchUsesRemaining = GetAllowanceForUtility(profile.rescueCareerRank, RescueDispatchUtility.SnowmobileDispatch);
                profile.rescueDispatchRefreshUtc = new DateTimeUtc { unixSeconds = now + RescueDispatchRefreshIntervalSeconds };
                changed = true;
            }

            if (changed && saveIfChanged)
                mgr.Save();

            return profile;
        }

        private static int GetAllowanceForUtility(int rescueRank, RescueDispatchUtility utility)
        {
            rescueRank = Mathf.Max(1, rescueRank);

            return utility switch
            {
                RescueDispatchUtility.MedicalSupplyDrop => rescueRank >= 2 ? 2 : 0,
                RescueDispatchUtility.RespawnBeacon => rescueRank >= 3 ? 1 : 0,
                RescueDispatchUtility.SnowmobileDispatch => rescueRank >= 4 ? 1 : 0,
                _ => 0
            };
        }

        private static ref int GetRemainingUsesRef(PlayerStatsProfile profile, RescueDispatchUtility utility)
        {
            switch (utility)
            {
                case RescueDispatchUtility.MedicalSupplyDrop:
                    return ref profile.rescueMedicalSupplyUsesRemaining;
                case RescueDispatchUtility.RespawnBeacon:
                    return ref profile.rescueRespawnBeaconUsesRemaining;
                default:
                    return ref profile.rescueSnowmobileDispatchUsesRemaining;
            }
        }

        private static int ResolveRescueRank(int completedMissions)
        {
            completedMissions = Mathf.Max(0, completedMissions);

            int rank = 1;
            for (int i = 0; i < RescueRankCompletionThresholds.Length; i++)
            {
                if (completedMissions >= RescueRankCompletionThresholds[i])
                    rank = i + 1;
            }

            return Mathf.Clamp(rank, 1, RescueRankCompletionThresholds.Length);
        }
    }
}
