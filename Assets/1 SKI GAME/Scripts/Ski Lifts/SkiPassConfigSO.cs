using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkiGame/Ski Pass/Ski Pass Config", fileName = "SkiPassConfig")]
public class SkiPassConfigSO : ScriptableObject
{
    [Serializable]
    public class PassLevel
    {
        [Tooltip("Stable unique ID for this pass. Used by permanent unlocks and lift requirements.")]
        public string passId = "starter";

        [Tooltip("Optional region / grouping identifier for authoring only.")]
        public string regionId = "";

        [Tooltip("Display name for this pass tier.")]
        public string displayName = "Pass";

        [Tooltip("Index / level number for this tier (usually matches array index).")]
        public int levelIndex = 0;

        [Tooltip("If enabled, this pass is treated as the default / basic pass.")]
        public bool isDefaultPass = false;

        [Tooltip("Additional pass IDs that this pass grants access to. Leave empty if it only grants itself.")]
        public List<string> grantedAccessPassIds = new List<string>();

        [Header("Pricing")]
        [Tooltip("Base price for ONE DAY of this pass level.")]
        public int dayPrice = 0;

        [Header("Map / UI")]
        [Tooltip("Colour used on the map for lifts that require this pass level.")]
        public Color mapColor = Color.white;
    }

    [Serializable]
    public class DurationOption
    {
        public string label = "1 Day";
        [Min(1)] public int days = 1;

        [Tooltip("Discount fraction applied to total price for this duration. (0.10 = 10% off)")]
        [Range(0f, 0.75f)]
        public float discount01 = 0f;
    }

    [Header("Levels (array index should match levelIndex)")]
    public PassLevel[] levels;

    [Header("Default / expiry behavior")]
    [Tooltip("Level to revert to if the pass expires.")]
    public int defaultLevelIndex = 0;

    [Header("Duration options (purchase buttons)")]
    public DurationOption[] durations = new DurationOption[]
    {
        new DurationOption(){ label="1 Day", days=1, discount01=0f },
        new DurationOption(){ label="1 Week", days=7, discount01=0.10f },
        new DurationOption(){ label="1 Month", days=30, discount01=0.20f },
    };

    [Header("Upgrade credit")]
    [Tooltip("When upgrading, remaining-time value is credited by this multiplier (1 = full credit, 0.5 = half credit).")]
    [Range(0f, 1f)]
    public float upgradeCreditMultiplier01 = 1f;

    public int ClampLevel(int level) => Mathf.Clamp(level, 0, (levels?.Length ?? 1) - 1);

    public PassLevel Get(int level)
    {
        if (levels == null || levels.Length == 0)
            return null;

        level = ClampLevel(level);
        return levels[level];
    }

    public DurationOption GetDuration(int index)
    {
        if (durations == null || durations.Length == 0)
            return null;

        index = Mathf.Clamp(index, 0, durations.Length - 1);
        return durations[index];
    }

    public PassLevel GetByPassId(string passId)
    {
        if (string.IsNullOrWhiteSpace(passId) || levels == null)
            return null;

        string normalized = passId.Trim();
        for (int i = 0; i < levels.Length; i++)
        {
            PassLevel level = levels[i];
            if (level == null || string.IsNullOrWhiteSpace(level.passId))
                continue;

            if (string.Equals(level.passId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return level;
        }

        return null;
    }

    public int GetLevelIndexByPassId(string passId)
    {
        if (string.IsNullOrWhiteSpace(passId) || levels == null)
            return -1;

        string normalized = passId.Trim();
        for (int i = 0; i < levels.Length; i++)
        {
            PassLevel level = levels[i];
            if (level == null || string.IsNullOrWhiteSpace(level.passId))
                continue;

            if (string.Equals(level.passId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    public string GetPassIdForLevel(int level)
    {
        PassLevel def = Get(level);
        if (def == null || string.IsNullOrWhiteSpace(def.passId))
            return string.Empty;

        return def.passId.Trim();
    }

    public string GetDisplayNameForPassId(string passId)
    {
        PassLevel def = GetByPassId(passId);
        return def != null ? def.displayName : passId;
    }

    public string GetDefaultPassId()
    {
        if (levels == null || levels.Length == 0)
            return string.Empty;

        for (int i = 0; i < levels.Length; i++)
        {
            PassLevel level = levels[i];
            if (level != null && level.isDefaultPass && !string.IsNullOrWhiteSpace(level.passId))
                return level.passId.Trim();
        }

        return GetPassIdForLevel(defaultLevelIndex);
    }

    public HashSet<string> ResolveGrantedAccessPassIds(string sourcePassId)
    {
        HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PassLevel source = GetByPassId(sourcePassId);
        if (source == null)
            return results;

        if (!string.IsNullOrWhiteSpace(source.passId))
            results.Add(source.passId.Trim());

        if (source.grantedAccessPassIds != null)
        {
            for (int i = 0; i < source.grantedAccessPassIds.Count; i++)
            {
                string granted = source.grantedAccessPassIds[i];
                if (!string.IsNullOrWhiteSpace(granted))
                    results.Add(granted.Trim());
            }
        }

        return results;
    }

    public bool PassGrantsAccessTo(string sourcePassId, string requiredPassId)
    {
        if (string.IsNullOrWhiteSpace(requiredPassId))
            return false;

        HashSet<string> grants = ResolveGrantedAccessPassIds(sourcePassId);
        return grants.Contains(requiredPassId.Trim());
    }
}