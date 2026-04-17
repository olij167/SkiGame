using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    public static class ProgressionEventRecorder
    {
        public static void RecordRaceStart(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeRaceStarts++;
        }

        public static void RecordRaceCompletion(PlayerStatsProfile profile, string raceId, int placement, bool personalBestImproved)
        {
            if (profile == null)
                return;

            profile.Sanitize();

            profile.progression.lifetimeRaceCompletions++;
            if (placement == 1)
                profile.progression.lifetimeRaceWins++;
            if (placement > 0 && placement <= 3)
                profile.progression.lifetimeRacePodiums++;
            if (personalBestImproved)
                profile.progression.lifetimeRacePersonalBestImprovements++;

            RememberUniqueId(profile.progression.completedRaceIds, raceId);
        }

        public static void RecordRescueStart(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeRescueStarts++;
        }

        public static void RecordRescueUtilityUse(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeRescueUtilityUses++;
        }

        public static void RecordQuestAccepted(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeQuestsAccepted++;
        }

        public static void RecordQuestStageCompleted(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeQuestStagesCompleted++;
        }

        public static void RecordQuestCompleted(PlayerStatsProfile profile, bool tutorialQuest)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            if (tutorialQuest)
                profile.progression.lifetimeTutorialQuestsCompleted++;
        }

        public static void RecordTrickResolved(PlayerStatsProfile profile, SkierTrickTracker.TrickResult result)
        {
            if (profile == null)
                return;

            profile.Sanitize();

            if (result.success)
            {
                profile.progression.sessionTricksLanded++;
                profile.progression.lifetimeTricksLanded++;
                profile.progression.bestTrickTier = Mathf.Max(profile.progression.bestTrickTier, result.progressionTier);

                if (!string.IsNullOrWhiteSpace(result.displayName))
                {
                    profile.progression.lifetimeNamedTricksLanded++;
                    RememberUniqueId(profile.progression.landedTrickNames, result.displayName);
                }
            }
            else
            {
                profile.progression.lifetimeTrickFails++;
            }
        }

        public static void RecordPassPurchase(PlayerStatsProfile profile, string passId, bool extension)
        {
            if (profile == null)
                return;

            profile.Sanitize();

            if (extension)
                profile.progression.lifetimePassExtensions++;
            else
                profile.progression.lifetimePassPurchases++;

            RememberUniqueId(profile.progression.ownedPassIds, passId);
        }

        public static void RecordPassOwnership(PlayerStatsProfile profile, string passId)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            RememberUniqueId(profile.progression.ownedPassIds, passId);
        }

        public static int RecordCustomizationUnlocks(PlayerStatsProfile profile, IList<string> unlockedIds)
        {
            if (profile == null || unlockedIds == null || unlockedIds.Count == 0)
                return 0;

            profile.Sanitize();

            int newlyUnlocked = 0;
            for (int i = 0; i < unlockedIds.Count; i++)
            {
                string id = unlockedIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (profile.customization != null &&
                    profile.customization.unlockedCustomizationIds != null &&
                    profile.customization.unlockedCustomizationIds.Contains(id))
                {
                    continue;
                }

                newlyUnlocked++;
            }

            return newlyUnlocked;
        }

        public static void RecordCustomizationPurchase(PlayerStatsProfile profile)
        {
            if (profile == null)
                return;

            profile.Sanitize();
            profile.progression.lifetimeCustomizationPurchases++;
        }

        public static void AddCurrency(PlayerStatsProfile profile, int amount)
        {
            if (profile == null || amount <= 0)
                return;

            profile.Sanitize();
            profile.currency += amount;
            profile.progression.lifetimeCurrencyEarned += amount;
            if (amount > profile.progression.largestSingleCurrencyReward)
                profile.progression.largestSingleCurrencyReward = amount;
        }

        public static bool TrySpendCurrency(PlayerStatsProfile profile, int amount)
        {
            if (profile == null || amount < 0)
                return false;

            profile.Sanitize();

            if (profile.currency < amount)
                return false;

            profile.currency -= amount;
            profile.progression.lifetimeCurrencySpent += amount;
            return true;
        }

        private static void RememberUniqueId(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id))
                return;

            string normalized = id.Trim();
            if (!ids.Contains(normalized))
                ids.Add(normalized);
        }
    }
}
