using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkiGame.Progression
{
    /// <summary>
    /// Save slot routing + manifest.
    /// Each slot writes to: Application.persistentDataPath/Saves/Slot_##/
    /// </summary>
    public static class GameSaveSystem
    {
        public const int MaxSlots = 3;

        private const string RootFolderName = "Saves";
        private const string ManifestFileName = "manifest.json";
        private const string PrefActiveSlot = "skigame.activeSaveSlot";

        [Serializable]
        public sealed class SaveManifest
        {
            public int version = 1;
            public List<SaveSlotMeta> slots = new();
        }

        [Serializable]
        public sealed class SaveSlotMeta
        {
            public int slotId;
            public string displayName;
            public DateTimeUtc createdUtc;
            public DateTimeUtc lastPlayedUtc;
            public bool hasData;
        }

        public static int ActiveSlotId
        {
            get => PlayerPrefs.GetInt(PrefActiveSlot, 0);
            set
            {
                PlayerPrefs.SetInt(PrefActiveSlot, Mathf.Clamp(value, 0, MaxSlots - 1));
                PlayerPrefs.Save();
            }
        }

        public static string RootPath => Path.Combine(Application.persistentDataPath, RootFolderName);
        public static string ManifestPath => Path.Combine(RootPath, ManifestFileName);

        public static string GetSlotPath(int slotId)
            => Path.Combine(RootPath, $"Slot_{slotId:00}");

        public static string GetProfilePath(int slotId)
            => Path.Combine(GetSlotPath(slotId), "player_stats_profile.json");

        public static string GetTimePath(int slotId)
            => Path.Combine(GetSlotPath(slotId), "time_state.json");

        public static SaveManifest LoadOrCreateManifest()
        {
            try
            {
                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);

                if (!File.Exists(ManifestPath))
                {
                    var created = CreateDefaultManifest();
                    SaveManifestToDisk(created);
                    return created;
                }

                var json = File.ReadAllText(ManifestPath);
                var m = JsonUtility.FromJson<SaveManifest>(json) ?? CreateDefaultManifest();
                SanitizeManifest(m);
                return m;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSaveSystem] Failed to load manifest. Recreating.\n{ex}");
                var created = CreateDefaultManifest();
                SaveManifestToDisk(created);
                return created;
            }
        }

        public static void SaveManifestToDisk(SaveManifest m)
        {
            if (m == null) return;
            try
            {
                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);

                SanitizeManifest(m);
                File.WriteAllText(ManifestPath, JsonUtility.ToJson(m, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSaveSystem] Failed to save manifest.\n{ex}");
            }
        }

        public static SaveSlotMeta GetSlotMeta(SaveManifest m, int slotId)
        {
            if (m == null) return null;
            slotId = Mathf.Clamp(slotId, 0, MaxSlots - 1);

            for (int i = 0; i < m.slots.Count; i++)
                if (m.slots[i] != null && m.slots[i].slotId == slotId)
                    return m.slots[i];

            var created = new SaveSlotMeta
            {
                slotId = slotId,
                displayName = $"Save {slotId + 1}",
                createdUtc = DateTimeUtc.Now(),
                lastPlayedUtc = DateTimeUtc.Now(),
                hasData = false
            };
            m.slots.Add(created);
            return created;
        }

        public static void MarkSlotPlayed(int slotId)
        {
            var m = LoadOrCreateManifest();
            var s = GetSlotMeta(m, slotId);
            s.lastPlayedUtc = DateTimeUtc.Now();
            s.hasData = File.Exists(GetProfilePath(slotId));
            SaveManifestToDisk(m);
        }

        public static void DeleteSlot(int slotId)
        {
            slotId = Mathf.Clamp(slotId, 0, MaxSlots - 1);

            try
            {
                string slotPath = GetSlotPath(slotId);
                if (Directory.Exists(slotPath))
                    Directory.Delete(slotPath, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSaveSystem] Failed deleting slot folder.\n{ex}");
            }

            var m = LoadOrCreateManifest();
            var s = GetSlotMeta(m, slotId);
            s.hasData = false;
            s.lastPlayedUtc = DateTimeUtc.Now();
            SaveManifestToDisk(m);
        }

        private static SaveManifest CreateDefaultManifest()
        {
            var m = new SaveManifest();
            for (int i = 0; i < MaxSlots; i++)
            {
                m.slots.Add(new SaveSlotMeta
                {
                    slotId = i,
                    displayName = $"Save {i + 1}",
                    createdUtc = DateTimeUtc.Now(),
                    lastPlayedUtc = DateTimeUtc.Now(),
                    hasData = File.Exists(GetProfilePath(i))
                });
            }
            return m;
        }

        private static void SanitizeManifest(SaveManifest m)
        {
            m.version = Mathf.Max(1, m.version);
            m.slots ??= new List<SaveSlotMeta>();

            // Ensure all slot metas exist
            for (int i = 0; i < MaxSlots; i++)
            {
                bool found = false;
                for (int j = 0; j < m.slots.Count; j++)
                {
                    if (m.slots[j] != null && m.slots[j].slotId == i) { found = true; break; }
                }
                if (!found)
                    m.slots.Add(new SaveSlotMeta
                    {
                        slotId = i,
                        displayName = $"Save {i + 1}",
                        createdUtc = DateTimeUtc.Now(),
                        lastPlayedUtc = DateTimeUtc.Now(),
                        hasData = File.Exists(GetProfilePath(i))
                    });
            }

            // Refresh hasData flags (cheap)
            for (int i = 0; i < m.slots.Count; i++)
            {
                var s = m.slots[i];
                if (s == null) continue;
                s.slotId = Mathf.Clamp(s.slotId, 0, MaxSlots - 1);
                s.hasData = File.Exists(GetProfilePath(s.slotId));
                if (string.IsNullOrEmpty(s.displayName))
                    s.displayName = $"Save {s.slotId + 1}";
            }
        }
    }
}
