using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkiGame/Ski Resort/Ski Resort Config", fileName = "SkiResortConfig")]
public sealed class SkiResortConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class RentalDurationOption
    {
        public string label = "1 Day";
        [Min(1)] public int days = 1;
        [Min(0)] public int cost = 0;
    }

    [Serializable]
    public sealed class ResortDefinition
    {
        [Tooltip("Stable unique resort ID used by save data and scene zones.")]
        public string resortId = "main_resort";

        [Tooltip("Stable map region ID that feeds resort reputation.")]
        public string regionId = string.Empty;

        [Tooltip("Display name used in UI.")]
        public string displayName = "Ski Resort";

        [Tooltip("Default resort is always accessible, even with no rental or reputation progress.")]
        public bool isDefaultResort = false;

        [Tooltip("Timed rental options available when the player enters an unowned resort.")]
        public List<RentalDurationOption> rentalOptions = new List<RentalDurationOption>();
    }

    [SerializeField] private List<ResortDefinition> resorts = new List<ResortDefinition>();

    public IReadOnlyList<ResortDefinition> Resorts => resorts;

    public ResortDefinition GetByResortId(string resortId)
    {
        if (string.IsNullOrWhiteSpace(resortId) || resorts == null)
            return null;

        string normalized = resortId.Trim();
        for (int i = 0; i < resorts.Count; i++)
        {
            ResortDefinition definition = resorts[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.resortId))
                continue;

            if (string.Equals(definition.resortId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return definition;
        }

        return null;
    }

    public string GetDefaultResortId()
    {
        if (resorts == null || resorts.Count == 0)
            return string.Empty;

        for (int i = 0; i < resorts.Count; i++)
        {
            ResortDefinition definition = resorts[i];
            if (definition != null && definition.isDefaultResort && !string.IsNullOrWhiteSpace(definition.resortId))
                return definition.resortId.Trim();
        }

        ResortDefinition first = resorts[0];
        return first != null && !string.IsNullOrWhiteSpace(first.resortId) ? first.resortId.Trim() : string.Empty;
    }

    public string GetDisplayNameForResortId(string resortId)
    {
        ResortDefinition definition = GetByResortId(resortId);
        return definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName.Trim()
            : resortId ?? string.Empty;
    }

    public bool IsDefaultResort(string resortId)
    {
        ResortDefinition definition = GetByResortId(resortId);
        return definition != null && definition.isDefaultResort;
    }

    public bool TryGetRegionIdForResort(string resortId, out string regionId)
    {
        regionId = string.Empty;
        ResortDefinition definition = GetByResortId(resortId);
        if (definition == null || string.IsNullOrWhiteSpace(definition.regionId))
            return false;

        regionId = definition.regionId.Trim();
        return true;
    }

    public RentalDurationOption GetDefaultRentalOption(string resortId)
    {
        ResortDefinition definition = GetByResortId(resortId);
        if (definition?.rentalOptions == null || definition.rentalOptions.Count == 0)
            return null;

        for (int i = 0; i < definition.rentalOptions.Count; i++)
        {
            RentalDurationOption option = definition.rentalOptions[i];
            if (option != null)
                return option;
        }

        return null;
    }
}
