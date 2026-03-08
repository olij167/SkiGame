using System;
using System.IO;
using UnityEngine;
using TimeWeather;

namespace SkiGame.Progression
{
    [Serializable]
    public sealed class TimeState
    {
        public int year;
        public int monthIndex;
        public int dayOfMonth;
        public int dayCount;
        public float timeOfDay;
    }

    public static class TimeStateStorage
    {
        public static TimeState Capture(TimeController tc)
        {
            if (tc == null) return null;

            int monthIndex = 0;
            if (tc.monthPresets != null && tc.currentMonthData != null)
            {
                for (int i = 0; i < tc.monthPresets.Length; i++)
                    if (tc.monthPresets[i] == tc.currentMonthData) { monthIndex = i; break; }
            }

            return new TimeState
            {
                year = tc.currentYear,
                monthIndex = monthIndex,
                dayOfMonth = tc.dayOfMonth,
                dayCount = tc.dayCount,
                timeOfDay = tc.timeOfDay
            };
        }

        public static void Apply(TimeController tc, TimeState s)
        {
            if (tc == null || s == null) return;

            tc.currentYear = s.year;

            if (tc.monthPresets != null && tc.monthPresets.Length > 0)
            {
                int mi = Mathf.Clamp(s.monthIndex, 0, tc.monthPresets.Length - 1);
                tc.currentMonthData = tc.monthPresets[mi];
            }

            tc.dayOfMonth = Mathf.Max(1, s.dayOfMonth);
            tc.dayCount = Mathf.Max(0, s.dayCount);
            tc.timeOfDay = Mathf.Repeat(s.timeOfDay, 24f);

            // Keep internal caches consistent (your TimeController already calls this in Start,
            // but for loads we need it immediately).
            tc.SyncDayCountFromDate();
        }

        public static TimeState LoadOrNull(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<TimeState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimeStateStorage] Failed to load: {path}\n{ex}");
                return null;
            }
        }

        public static void Save(TimeState s, string path)
        {
            if (s == null) return;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, JsonUtility.ToJson(s, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimeStateStorage] Failed to save: {path}\n{ex}");
            }
        }
    }
}
