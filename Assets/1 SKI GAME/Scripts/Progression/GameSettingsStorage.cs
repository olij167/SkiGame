using System;
using System.IO;
using UnityEngine;

namespace SkiGame.Progression
{
    public static class GameSettingsStorage
    {
        private const string DefaultFileName = "game_settings.json";

        public static string GetDefaultPath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        public static GameSettingsProfile LoadOrCreate(string path = null)
        {
            path ??= GetDefaultPath();

            if (!File.Exists(path))
            {
                var created = GameSettingsProfile.Defaults();
                Save(created, path);
                return created;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<GameSettingsProfile>(json);

                if (loaded == null)
                {
                    var created = GameSettingsProfile.Defaults();
                    Save(created, path);
                    return created;
                }

                loaded.Sanitize();
                UpgradeIfNeeded(loaded);
                return loaded;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSettingsStorage] Failed to load settings. Creating defaults. Path={path}\n{ex}");
                var created = GameSettingsProfile.Defaults();
                Save(created, path);
                return created;
            }
        }

        public static void Save(GameSettingsProfile profile, string path = null)
        {
            if (profile == null) return;
            path ??= GetDefaultPath();

            try
            {
                profile.Sanitize();
                var json = JsonUtility.ToJson(profile, prettyPrint: false);

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSettingsStorage] Failed to save settings. Path={path}\n{ex}");
            }
        }

        private static void UpgradeIfNeeded(GameSettingsProfile p)
        {
            if (p.version < GameSettingsProfile.CurrentVersion)
            {
                // Future migration hooks go here.
                p.version = GameSettingsProfile.CurrentVersion;
            }
        }
    }
}
