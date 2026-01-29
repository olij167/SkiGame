using System;
using System.IO;
using UnityEngine;

namespace SkiGame.Progression
{
    public static class PlayerStatsStorage
    {
        private const string DefaultFileName = "player_stats_profile.json";

        public static string GetDefaultPath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        public static PlayerStatsProfile LoadOrCreate(string path = null)
        {
            path ??= GetDefaultPath();

            if (!File.Exists(path))
            {
                var created = CreateNew();
                Save(created, path);
                return created;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<PlayerStatsProfile>(json);

                if (loaded == null)
                {
                    var created = CreateNew();
                    Save(created, path);
                    return created;
                }

                loaded.Sanitize();
                UpgradeIfNeeded(loaded);
                return loaded;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerStatsStorage] Failed to load profile. Creating new. Path={path}\n{ex}");
                var created = CreateNew();
                Save(created, path);
                return created;
            }
        }

        public static void Save(PlayerStatsProfile profile, string path = null)
        {
            if (profile == null) return;

            path ??= GetDefaultPath();

            try
            {
                profile.Sanitize();
                profile.lastSavedUtc = DateTimeUtc.Now();

                string json = JsonUtility.ToJson(profile, prettyPrint: false);

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerStatsStorage] Failed to save profile. Path={path}\n{ex}");
            }
        }

        public static PlayerStatsProfile CreateNew()
        {
            var p = new PlayerStatsProfile
            {
                profileVersion = PlayerStatsProfile.CurrentVersion,
                createdUtc = DateTimeUtc.Now(),
                lastSavedUtc = DateTimeUtc.Now()
            };

            p.Sanitize();
            return p;
        }

        private static void UpgradeIfNeeded(PlayerStatsProfile profile)
        {
            // Phase 1: only version 1 exists.
            // Future-proofing: if you later add fields or rename them, you can migrate here.
            if (profile.profileVersion < PlayerStatsProfile.CurrentVersion)
            {
                // Example:
                // if (profile.profileVersion == 0) { ... migrate ... }
                profile.profileVersion = PlayerStatsProfile.CurrentVersion;
            }
        }
    }
}
