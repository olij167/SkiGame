using System;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkiResortAccessManager : MonoBehaviour
{
    public static SkiResortAccessManager Instance { get; private set; }

    [SerializeField] private SkiResortConfigSO config;
    [SerializeField] private PlayerStatsManager playerStatsManager;

    public event Action OnResortAccessChanged;

    public SkiResortConfigSO Config => config;
    public IReadOnlyList<PlayerStatsProfile.ActiveResortRentalState> ActiveRentals
        => EnsureProfileState()?.activeResortRentals;

    public struct RentalQuote
    {
        public readonly string resortId;
        public readonly string displayName;
        public readonly int days;
        public readonly int cost;
        public readonly string durationLabel;

        public RentalQuote(string resortId, string displayName, int days, int cost, string durationLabel)
        {
            this.resortId = resortId;
            this.displayName = displayName;
            this.days = days;
            this.cost = cost;
            this.durationLabel = durationLabel;
        }
    }

    public static SkiResortAccessManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SkiResortAccessManager existing = FindObjectOfType<SkiResortAccessManager>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("SkiResortAccessManager");
        return go.AddComponent<SkiResortAccessManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
        ApplyPermanentUnlocksFromProfile();
        EnforceExpiry();
    }

    private void Update()
    {
        EnforceExpiry();
    }

    public void ResetForNewGame()
    {
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return;

        profile.activeResortRentals ??= new List<PlayerStatsProfile.ActiveResortRentalState>();
        profile.activeResortRentals.Clear();
        SaveProfile();
        OnResortAccessChanged?.Invoke();
    }

    public void ApplyPermanentUnlocksFromProfile()
    {
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return;

        profile.permanentlyUnlockedResortIds ??= new List<string>();
        profile.activeResortRentals ??= new List<PlayerStatsProfile.ActiveResortRentalState>();
    }

    public HashSet<string> BuildAccessibleResortIdSet()
    {
        HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config != null && config.Resorts != null)
        {
            for (int i = 0; i < config.Resorts.Count; i++)
            {
                SkiResortConfigSO.ResortDefinition definition = config.Resorts[i];
                if (definition != null && definition.isDefaultResort && !string.IsNullOrWhiteSpace(definition.resortId))
                    results.Add(definition.resortId.Trim());
            }
        }

        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return results;

        AddIds(results, profile.permanentlyUnlockedResortIds);

        if (profile.activeResortRentals != null)
        {
            for (int i = 0; i < profile.activeResortRentals.Count; i++)
            {
                PlayerStatsProfile.ActiveResortRentalState rental = profile.activeResortRentals[i];
                if (rental == null || string.IsNullOrWhiteSpace(rental.resortId))
                    continue;

                results.Add(rental.resortId.Trim());
            }
        }

        return results;
    }

    public bool IsResortAccessible(string resortId)
    {
        if (string.IsNullOrWhiteSpace(resortId))
            return false;

        return BuildAccessibleResortIdSet().Contains(resortId.Trim());
    }

    public bool IsResortPermanentlyUnlocked(string resortId)
    {
        if (string.IsNullOrWhiteSpace(resortId))
            return false;

        if (config != null && config.IsDefaultResort(resortId))
            return true;

        PlayerStatsProfile profile = EnsureProfileState();
        return profile != null &&
               profile.permanentlyUnlockedResortIds != null &&
               profile.permanentlyUnlockedResortIds.Contains(resortId.Trim());
    }

    public bool IsResortCurrentlyRented(string resortId)
    {
        if (string.IsNullOrWhiteSpace(resortId))
            return false;

        return TryGetActiveRental(resortId, out _, out _);
    }

    public bool TryGetRentalTimeRemainingText(string resortId, out string text)
    {
        text = string.Empty;

        if (!TryGetActiveRental(resortId, out PlayerStatsProfile.ActiveResortRentalState rental, out _))
            return false;

        double remainingHours = Math.Max(0.0, rental.expiryGameHours - GetNowGameHoursOrFallback());
        if (remainingHours >= 24.0)
        {
            text = $"{Math.Ceiling(remainingHours / 24.0)}d remaining";
            return true;
        }

        text = $"{Mathf.CeilToInt((float)remainingHours)}h remaining";
        return true;
    }

    public bool CanAccessOrRent(string resortId)
    {
        if (IsResortAccessible(resortId))
            return true;

        SkiResortConfigSO.RentalDurationOption option = config != null ? config.GetDefaultRentalOption(resortId) : null;
        return option != null;
    }

    public bool TryPrepareForEntry(SkiResortZone zone, out string message)
    {
        message = string.Empty;

        if (zone == null || string.IsNullOrWhiteSpace(zone.ResortId))
            return true;

        string resortId = zone.ResortId;
        if (IsResortAccessible(resortId))
            return true;

        if (!TryQuoteRental(resortId, out RentalQuote quote, out string reason))
        {
            message = reason;
            return false;
        }

        if (!TrySpendCurrency(quote.cost))
        {
            message = "Not enough currency to rent this resort.";
            return false;
        }

        ApplyRental(quote.resortId, quote.days * 24.0);
        message = $"Rented {quote.displayName} for {quote.days} day{(quote.days == 1 ? string.Empty : "s")}.";
        return true;
    }

    public bool TryQuoteRental(string resortId, out RentalQuote quote, out string reason)
    {
        quote = default;
        reason = string.Empty;

        if (config == null)
        {
            reason = "Missing SkiResortConfig.";
            return false;
        }

        SkiResortConfigSO.ResortDefinition definition = config.GetByResortId(resortId);
        if (definition == null)
        {
            reason = "Missing resort definition.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.resortId))
        {
            reason = "Resort definition is missing a resortId.";
            return false;
        }

        if (definition.isDefaultResort)
        {
            reason = "Default resort is always free.";
            return false;
        }

        if (IsResortPermanentlyUnlocked(resortId))
        {
            reason = "Resort already permanently unlocked.";
            return false;
        }

        SkiResortConfigSO.RentalDurationOption option = config.GetDefaultRentalOption(resortId);
        if (option == null)
        {
            reason = "This resort does not have rental options configured.";
            return false;
        }

        quote = new RentalQuote(
            definition.resortId.Trim(),
            string.IsNullOrWhiteSpace(definition.displayName) ? definition.resortId.Trim() : definition.displayName.Trim(),
            Mathf.Max(1, option.days),
            Mathf.Max(0, option.cost),
            option.label ?? string.Empty);

        return true;
    }

    public bool TryUnlockPermanentResort(string resortId, out bool newlyUnlocked)
    {
        newlyUnlocked = false;

        if (string.IsNullOrWhiteSpace(resortId))
            return false;

        if (config != null && config.IsDefaultResort(resortId))
            return false;

        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return false;

        profile.permanentlyUnlockedResortIds ??= new List<string>();

        string normalized = resortId.Trim();
        if (profile.permanentlyUnlockedResortIds.Contains(normalized))
            return false;

        profile.permanentlyUnlockedResortIds.Add(normalized);
        RemoveRental(normalized);
        SaveProfile();
        newlyUnlocked = true;
        OnResortAccessChanged?.Invoke();
        return true;
    }

    public bool TryGetClosestActiveResort(Vector3 fromPosition, out SkiResortZone zone, out Transform respawnTransform)
    {
        zone = null;
        respawnTransform = null;

        HashSet<string> accessible = BuildAccessibleResortIdSet();
        if (accessible.Count == 0)
            return false;

        SkiResortZone[] zones = FindObjectsOfType<SkiResortZone>(includeInactive: false);
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < zones.Length; i++)
        {
            SkiResortZone candidate = zones[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.ResortId))
                continue;

            if (!accessible.Contains(candidate.ResortId))
                continue;

            Transform point = candidate.RespawnPoint;
            if (point == null)
                continue;

            float sqr = (point.position - fromPosition).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                zone = candidate;
                respawnTransform = point;
            }
        }

        return zone != null && respawnTransform != null;
    }

    public bool TryGetStartupSpawnTransform(Vector3 fromPosition, out Transform spawnTransform)
    {
        spawnTransform = null;
        if (TryGetClosestActiveResort(fromPosition, out _, out spawnTransform))
            return true;

        string defaultResortId = config != null ? config.GetDefaultResortId() : string.Empty;
        if (TryGetZoneByResortId(defaultResortId, out SkiResortZone zone))
        {
            spawnTransform = zone.RespawnPoint;
            return spawnTransform != null;
        }

        return false;
    }

    public string GetDisplayName(string resortId)
    {
        return config != null ? config.GetDisplayNameForResortId(resortId) : resortId ?? string.Empty;
    }

    public bool TryGetRegionId(string resortId, out string regionId)
    {
        regionId = string.Empty;
        return config != null && config.TryGetRegionIdForResort(resortId, out regionId);
    }

    public bool TryGetZoneByResortId(string resortId, out SkiResortZone zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(resortId))
            return false;

        SkiResortZone[] zones = FindObjectsOfType<SkiResortZone>(includeInactive: true);
        for (int i = 0; i < zones.Length; i++)
        {
            SkiResortZone candidate = zones[i];
            if (candidate != null && candidate.MatchesResortId(resortId))
            {
                zone = candidate;
                return true;
            }
        }

        return false;
    }

    private PlayerStatsProfile EnsureProfileState()
    {
        ResolveReferences();
        if (playerStatsManager == null)
            return null;

        if (playerStatsManager.Profile == null)
            playerStatsManager.Load();

        PlayerStatsProfile profile = playerStatsManager.Profile;
        profile?.Sanitize();
        return profile;
    }

    private void ResolveReferences()
    {
        if (playerStatsManager == null)
            playerStatsManager = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance
                : FindObjectOfType<PlayerStatsManager>();

        if (config == null)
        {
            SkiResortConfigSO[] all = Resources.FindObjectsOfTypeAll<SkiResortConfigSO>();
            if (all != null && all.Length > 0)
                config = all[0];
        }
    }

    private void EnforceExpiry()
    {
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile?.activeResortRentals == null || profile.activeResortRentals.Count == 0)
            return;

        double now = GetNowGameHoursOrFallback();
        bool changed = false;

        for (int i = profile.activeResortRentals.Count - 1; i >= 0; i--)
        {
            PlayerStatsProfile.ActiveResortRentalState rental = profile.activeResortRentals[i];
            if (rental == null || string.IsNullOrWhiteSpace(rental.resortId) || rental.expiryGameHours <= now)
            {
                profile.activeResortRentals.RemoveAt(i);
                changed = true;
            }
        }

        if (!changed)
            return;

        SaveProfile();
        OnResortAccessChanged?.Invoke();
    }

    private bool TryGetActiveRental(string resortId, out PlayerStatsProfile.ActiveResortRentalState rental, out int index)
    {
        rental = null;
        index = -1;

        PlayerStatsProfile profile = EnsureProfileState();
        if (profile?.activeResortRentals == null || string.IsNullOrWhiteSpace(resortId))
            return false;

        string normalized = resortId.Trim();
        for (int i = 0; i < profile.activeResortRentals.Count; i++)
        {
            PlayerStatsProfile.ActiveResortRentalState candidate = profile.activeResortRentals[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.resortId))
                continue;

            if (string.Equals(candidate.resortId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                rental = candidate;
                index = i;
                return true;
            }
        }

        return false;
    }

    private void ApplyRental(string resortId, double addedHours)
    {
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null || string.IsNullOrWhiteSpace(resortId))
            return;

        profile.activeResortRentals ??= new List<PlayerStatsProfile.ActiveResortRentalState>();

        double now = GetNowGameHoursOrFallback();
        string normalized = resortId.Trim();

        if (TryGetActiveRental(normalized, out PlayerStatsProfile.ActiveResortRentalState existing, out int index))
        {
            double currentExpiry = Math.Max(existing.expiryGameHours, now);
            double remaining = Math.Max(0.0, currentExpiry - now);
            existing.startGameHours = now;
            existing.expiryGameHours = currentExpiry + Math.Max(0.0, addedHours);
            existing.totalHoursPurchased = remaining + Math.Max(0.0, addedHours);
            existing.resortId = normalized;
            profile.activeResortRentals[index] = existing;
        }
        else
        {
            profile.activeResortRentals.Add(new PlayerStatsProfile.ActiveResortRentalState
            {
                resortId = normalized,
                startGameHours = now,
                expiryGameHours = now + Math.Max(0.0, addedHours),
                totalHoursPurchased = Math.Max(0.0, addedHours)
            });
        }

        SaveProfile();
        OnResortAccessChanged?.Invoke();
    }

    private void RemoveRental(string resortId)
    {
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile?.activeResortRentals == null || string.IsNullOrWhiteSpace(resortId))
            return;

        for (int i = profile.activeResortRentals.Count - 1; i >= 0; i--)
        {
            PlayerStatsProfile.ActiveResortRentalState rental = profile.activeResortRentals[i];
            if (rental != null && string.Equals(rental.resortId, resortId, StringComparison.OrdinalIgnoreCase))
                profile.activeResortRentals.RemoveAt(i);
        }
    }

    private bool TrySpendCurrency(int cost)
    {
        if (cost <= 0)
            return true;

        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return false;

        if (!ProgressionEventRecorder.TrySpendCurrency(profile, cost))
            return false;

        SaveProfile();
        return true;
    }

    private void SaveProfile()
    {
        if (playerStatsManager != null)
            playerStatsManager.Save();
    }

    private static void AddIds(HashSet<string> results, List<string> source)
    {
        if (results == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            string id = source[i];
            if (!string.IsNullOrWhiteSpace(id))
                results.Add(id.Trim());
        }
    }

    private bool TryGetNowGameHours(out double nowHours)
    {
        nowHours = 0;

        var t = TimeWeather.TimeController.instance != null
            ? TimeWeather.TimeController.instance
            : FindObjectOfType<TimeWeather.TimeController>();

        if (t == null)
            return false;

        int day = Mathf.Max(0, t.dayCount);
        int hh = Mathf.Clamp(t.timeHours, 0, 23);
        float mm = Mathf.Clamp((float)t.timeMinutes, 0f, 59f);

        nowHours = (day * 24.0) + hh + (mm / 60.0);
        return true;
    }

    private double GetNowGameHoursOrFallback()
    {
        if (TryGetNowGameHours(out double h))
            return h;

        return Time.realtimeSinceStartup / 3600.0;
    }
}
