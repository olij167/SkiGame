using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.Progression;

public class SkiPassManager : MonoBehaviour
{
    public static SkiPassManager Instance { get; private set; }

    [SerializeField] private SkiPassConfigSO config;

    private const string Key_Level = "ski_pass_level";
    private const string Key_ExpiryGameHours = "ski_pass_expiry_game_hours";
    private const string Key_TotalHours = "ski_pass_total_hours";
    private const string Key_DefaultClaimed = "ski_pass_default_claimed";
    private const string Key_ActivePassesJson = "ski_pass_active_passes_json";

    [Serializable]
    public class ActivePassRecord
    {
        public string passId;
        public double startGameHours;
        public double expiryGameHours;
        public double totalHoursPurchased;
    }

    [Serializable]
    private class ActivePassRecordListWrapper
    {
        public List<ActivePassRecord> items = new List<ActivePassRecord>();
    }

    public event Action OnPassChanged;

    private readonly List<ActivePassRecord> _activePasses = new List<ActivePassRecord>();

    public int CurrentLevel { get; private set; }
    public double? ExpiryGameHours { get; private set; }
    public double TotalHours { get; private set; }

    public SkiPassConfigSO Config => config;
    public bool HasClaimedDefaultPass { get; private set; }
    public bool HasTimedPass => _activePasses.Count > 0;
    public IReadOnlyList<ActivePassRecord> ActivePasses => _activePasses;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyPermanentUnlocksFromProfile(equipBestUnlocked: false);
        EnforceExpiry();
    }

    private void Update()
    {
        EnforceExpiry();
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

    private void AddGrantedPassIds(HashSet<string> results, string sourcePassId)
    {
        if (results == null || string.IsNullOrWhiteSpace(sourcePassId))
            return;

        string normalized = sourcePassId.Trim();

        if (config == null)
        {
            results.Add(normalized);
            return;
        }

        HashSet<string> granted = config.ResolveGrantedAccessPassIds(normalized);
        if (granted == null || granted.Count == 0)
        {
            results.Add(normalized);
            return;
        }

        foreach (string passId in granted)
        {
            if (!string.IsNullOrWhiteSpace(passId))
                results.Add(passId.Trim());
        }
    }

    private void RemoveExpiredActivePasses(double nowHours)
    {
        for (int i = _activePasses.Count - 1; i >= 0; i--)
        {
            ActivePassRecord record = _activePasses[i];
            if (record == null || string.IsNullOrWhiteSpace(record.passId) || record.expiryGameHours <= nowHours)
                _activePasses.RemoveAt(i);
        }
    }

    private int GetPassLevelForId(string passId)
    {
        if (config == null || string.IsNullOrWhiteSpace(passId))
            return -1;

        return config.GetLevelIndexByPassId(passId.Trim());
    }

    private ActivePassRecord GetPrimaryActivePassRecord()
    {
        if (_activePasses.Count == 0)
            return null;

        ActivePassRecord best = null;
        int bestLevel = int.MinValue;

        for (int i = 0; i < _activePasses.Count; i++)
        {
            ActivePassRecord record = _activePasses[i];
            if (record == null || string.IsNullOrWhiteSpace(record.passId))
                continue;

            int level = GetPassLevelForId(record.passId);
            if (best == null || level > bestLevel)
            {
                best = record;
                bestLevel = level;
            }
        }

        return best;
    }

    private bool TryGetActivePassRecord(string passId, out ActivePassRecord record, out int index)
    {
        record = null;
        index = -1;

        if (string.IsNullOrWhiteSpace(passId))
            return false;

        string normalized = passId.Trim();
        for (int i = 0; i < _activePasses.Count; i++)
        {
            ActivePassRecord candidate = _activePasses[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.passId))
                continue;

            if (string.Equals(candidate.passId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                record = candidate;
                index = i;
                return true;
            }
        }

        return false;
    }

    private void RecalculateCompatibilityState()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();

        if (primary != null)
        {
            CurrentLevel = Mathf.Max(0, GetPassLevelForId(primary.passId));
            ExpiryGameHours = primary.expiryGameHours;
            TotalHours = Math.Max(0.0, primary.totalHoursPurchased);
            return;
        }

        int defaultLevel = config != null ? Mathf.Max(0, config.defaultLevelIndex) : 0;
        int highestPermanent = GetHighestPermanentUnlockedLevel();

        CurrentLevel = Mathf.Max(defaultLevel, highestPermanent);
        ExpiryGameHours = null;
        TotalHours = 0.0;
    }

    public bool IsPassActive(string passId)
    {
        if (string.IsNullOrWhiteSpace(passId))
            return false;

        return TryGetActivePassRecord(passId, out _, out _);
    }

    public bool IsPassActive(int level)
    {
        if (config == null)
            return false;

        string passId = config.GetPassIdForLevel(level);
        return IsPassActive(passId);
    }

    public HashSet<string> BuildAccessiblePassIdSet()
    {
        HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config == null)
            return results;

        if (HasClaimedDefaultPass)
            AddGrantedPassIds(results, config.GetDefaultPassId());

        for (int i = 0; i < _activePasses.Count; i++)
        {
            ActivePassRecord record = _activePasses[i];
            if (record == null || string.IsNullOrWhiteSpace(record.passId))
                continue;

            AddGrantedPassIds(results, record.passId);
        }

        foreach (string unlockedId in EnumeratePermanentUnlockedPassIds())
            AddGrantedPassIds(results, unlockedId);

        return results;
    }

    public bool TryGetActiveTimedPassWindow(string passId, out double startGameHours, out double expiryGameHours, out double totalHours)
    {
        startGameHours = 0.0;
        expiryGameHours = 0.0;
        totalHours = 0.0;

        if (!TryGetActivePassRecord(passId, out ActivePassRecord record, out _))
            return false;

        startGameHours = record.startGameHours;
        expiryGameHours = record.expiryGameHours;
        totalHours = record.totalHoursPurchased;
        return true;
    }

    public string GetCurrentPassDisplayName()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary != null && config != null)
        {
            var pass = config.GetByPassId(primary.passId);
            if (pass != null)
                return pass.displayName;
        }

        var p = config != null ? config.Get(CurrentLevel) : null;
        string baseName = p == null ? $"Pass {CurrentLevel}" : p.displayName;

        if (p != null && !string.IsNullOrWhiteSpace(p.passId) && IsPassPermanentlyUnlocked(p.passId))
            return $"{baseName} - Permanent";

        if (IsPassPermanentlyUnlocked(CurrentLevel))
            return $"{baseName} - Permanent";

        return baseName;
    }

    public string GetCurrentPassId()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary != null && !string.IsNullOrWhiteSpace(primary.passId))
            return primary.passId.Trim();

        if (config == null)
            return string.Empty;

        return config.GetPassIdForLevel(CurrentLevel);
    }

    public bool HasAccessToPass(string requiredPassId)
    {
        if (string.IsNullOrWhiteSpace(requiredPassId))
            return HasClaimedDefaultPass || HasAnyPermanentUnlocks() || _activePasses.Count > 0;

        string normalizedRequiredPassId = requiredPassId.Trim();

        if (config != null && config.GetLevelIndexByPassId(normalizedRequiredPassId) >= 0)
        {
            HashSet<string> accessible = BuildAccessiblePassIdSet();
            if (accessible.Contains(normalizedRequiredPassId))
                return true;

            return LegacyLevelFallbackGrantsPassAccess(normalizedRequiredPassId);
        }

        return false;
    }

    public bool CanUseLift(LiftLine lift)
    {
        if (lift == null)
            return false;

        // Prefer pass-id logic when the ID is valid in config.
        // If the ID is stale / missing from config, fall back to legacy level access.
        if (!string.IsNullOrWhiteSpace(lift.RequiredPassId))
        {
            int requiredLevelFromId = config != null
                ? config.GetLevelIndexByPassId(lift.RequiredPassId)
                : -1;

            if (requiredLevelFromId >= 0)
                return HasAccessToPass(lift.RequiredPassId);
        }

        // Legacy numeric fallback
        if (!HasClaimedDefaultPass && !HasAnyPermanentUnlocks())
            return false;

        if (CurrentLevel >= lift.RequiredPassLevel)
            return true;

        return GetHighestPermanentUnlockedLevel() >= lift.RequiredPassLevel;
    }

    public double GetRemainingHours()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary == null)
            return double.PositiveInfinity;

        double now = GetNowGameHoursOrFallback();
        return Math.Max(0.0, primary.expiryGameHours - now);
    }

    public float GetRemainingFraction01()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary == null)
            return 1f;

        if (primary.totalHoursPurchased <= 0.0001)
            return 0f;

        double rem = GetRemainingHours();
        return Mathf.Clamp01((float)(rem / primary.totalHoursPurchased));
    }

    public string GetRemainingTimeString()
    {
        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary == null)
            return "No expiry";

        double remH = GetRemainingHours();
        if (remH <= 0)
            return "Expired";

        double remD = remH / 24.0;
        if (remD >= 2.0)
            return $"Expires in {Math.Ceiling(remD)}d";

        int hours = Mathf.CeilToInt((float)remH);
        return $"Expires in {hours}h";
    }

    private bool HasAnyPermanentUnlocks()
    {
        if (RaceRescueProgression.GetHighestPermanentlyUnlockedPassLevel() >= 0)
            return true;

        List<string> ids = RaceRescueProgression.GetPermanentlyUnlockedPassIds();
        return ids != null && ids.Count > 0;
    }

    private bool LegacyLevelFallbackGrantsPassAccess(string requiredPassId)
    {
        if (config == null || string.IsNullOrWhiteSpace(requiredPassId))
            return false;

        int requiredLevel = config.GetLevelIndexByPassId(requiredPassId);
        if (requiredLevel < 0)
            return false;

        if (CurrentLevel >= requiredLevel)
            return true;

        return GetHighestPermanentUnlockedLevel() >= requiredLevel;
    }

    private bool ActivePassGrantsAccess(string requiredPassId)
    {
        if (config == null || string.IsNullOrWhiteSpace(requiredPassId))
            return false;

        HashSet<string> accessible = BuildAccessiblePassIdSet();
        return accessible.Contains(requiredPassId.Trim());
    }

    private bool PermanentUnlockGrantsAccess(string requiredPassId)
    {
        if (config == null || string.IsNullOrWhiteSpace(requiredPassId))
            return false;

        foreach (string unlockedId in EnumeratePermanentUnlockedPassIds())
        {
            if (config.PassGrantsAccessTo(unlockedId, requiredPassId))
                return true;
        }

        return false;
    }

    private IEnumerable<string> EnumeratePermanentUnlockedPassIds()
    {
        HashSet<string> yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<string> permanentIds = RaceRescueProgression.GetPermanentlyUnlockedPassIds();
        if (permanentIds != null)
        {
            for (int i = 0; i < permanentIds.Count; i++)
            {
                string unlockedId = permanentIds[i];
                if (string.IsNullOrWhiteSpace(unlockedId))
                    continue;

                string normalizedId = unlockedId.Trim();
                if (yielded.Add(normalizedId))
                    yield return normalizedId;
            }
        }

        var profile = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Profile : null;
        List<int> legacyLevels = profile != null ? profile.permanentlyUnlockedPassLevels : null;
        if (legacyLevels == null || config == null)
            yield break;

        for (int i = 0; i < legacyLevels.Count; i++)
        {
            string mappedPassId = config.GetPassIdForLevel(legacyLevels[i]);
            if (string.IsNullOrWhiteSpace(mappedPassId))
                continue;

            string normalizedId = mappedPassId.Trim();
            if (yielded.Add(normalizedId))
                yield return normalizedId;
        }
    }

    private bool TryGetRequiredPassIdForLevel(int requiredLevel, out string requiredPassId)
    {
        requiredPassId = string.Empty;

        if (config == null)
            return false;

        requiredPassId = config.GetPassIdForLevel(requiredLevel);
        return !string.IsNullOrWhiteSpace(requiredPassId);
    }

    public struct Quote
    {
        public int targetLevel;
        public int durationDays;
        public float discount01;

        public int baseCost;
        public int credit;
        public int finalCost;

        public bool isUpgrade;
        public bool isExtend;
        public double remainingHours;
        public double addedHours;
    }

    public bool TryQuotePurchase(int targetLevel, int durationIndex, out Quote q, out string reason)
    {
        q = default;
        reason = null;

        if (config == null || config.levels == null || config.levels.Length == 0)
        {
            reason = "Missing SkiPassConfig.";
            return false;
        }

        targetLevel = config.ClampLevel(targetLevel);
        var dur = config.GetDuration(durationIndex);
        if (dur == null)
        {
            reason = "Missing duration options.";
            return false;
        }

        if (targetLevel == config.defaultLevelIndex)
        {
            reason = "Basic pass is free.";
            return false;
        }

        if (IsPassPermanentlyUnlocked(targetLevel))
        {
            reason = "This pass tier has already been permanently unlocked.";
            return false;
        }

        var target = config.Get(targetLevel);
        if (target == null)
        {
            reason = "Missing target pass config.";
            return false;
        }

        string targetPassId = config.GetPassIdForLevel(targetLevel);
        int days = Mathf.Max(1, dur.days);
        float discount01 = Mathf.Clamp01(dur.discount01);
        int baseCost = Mathf.CeilToInt(Mathf.Max(0, target.dayPrice) * days * (1f - discount01));

        bool isExtend = IsPassActive(targetPassId);
        double remainingHours = 0.0;

        if (isExtend && TryGetActivePassRecord(targetPassId, out ActivePassRecord existing, out _))
        {
            double now = GetNowGameHoursOrFallback();
            remainingHours = Math.Max(0.0, existing.expiryGameHours - now);
        }

        q = new Quote
        {
            targetLevel = targetLevel,
            durationDays = days,
            discount01 = discount01,
            baseCost = baseCost,
            credit = 0,
            finalCost = Mathf.Max(0, baseCost),
            isUpgrade = false,
            isExtend = isExtend,
            remainingHours = remainingHours,
            addedHours = days * 24.0
        };

        return true;
    }

    public void ResetForNewGame()
    {
        int def = config != null ? Mathf.Max(0, config.defaultLevelIndex) : 0;

        _activePasses.Clear();
        CurrentLevel = def;
        ExpiryGameHours = null;
        TotalHours = 0;
        HasClaimedDefaultPass = false;

        Save();
        OnPassChanged?.Invoke();
    }

    public void RevertToDefault()
    {
        _activePasses.Clear();

        int def = config != null ? config.defaultLevelIndex : 0;
        CurrentLevel = Mathf.Max(0, def);

        ExpiryGameHours = null;
        TotalHours = 0;

        Save();
        OnPassChanged?.Invoke();
    }

    public bool TryPurchase(int targetLevel, int durationIndex, Func<int, bool> trySpendCurrency, out Quote q, out string failReason)
    {
        if (!TryQuotePurchase(targetLevel, durationIndex, out q, out failReason))
            return false;

        if (trySpendCurrency != null && !trySpendCurrency(q.finalCost))
        {
            failReason = "Not enough currency.";
            return false;
        }

        string targetPassId = config != null ? config.GetPassIdForLevel(q.targetLevel) : string.Empty;
        ApplyPurchase(targetPassId, q.addedHours);
        failReason = null;
        return true;
    }

    private void ApplyPurchase(string passId, double addedHours)
    {
        if (config == null || string.IsNullOrWhiteSpace(passId))
            return;

        string normalized = passId.Trim();
        double now = GetNowGameHoursOrFallback();
        RemoveExpiredActivePasses(now);

        bool isExtension = TryGetActivePassRecord(normalized, out ActivePassRecord existing, out int index);
        if (isExtension)
        {
            double currentExpiry = Math.Max(existing.expiryGameHours, now);
            double remaining = Math.Max(0.0, currentExpiry - now);
            double newTotal = remaining + Math.Max(0.0, addedHours);

            existing.passId = normalized;
            existing.startGameHours = now;
            existing.expiryGameHours = currentExpiry + Math.Max(0.0, addedHours);
            existing.totalHoursPurchased = newTotal;

            _activePasses[index] = existing;
        }
        else
        {
            _activePasses.Add(new ActivePassRecord
            {
                passId = normalized,
                startGameHours = now,
                expiryGameHours = now + Math.Max(0.0, addedHours),
                totalHoursPurchased = Math.Max(0.0, addedHours)
            });
        }

        HasClaimedDefaultPass = true;
        if (PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Profile != null)
            ProgressionEventRecorder.RecordPassPurchase(PlayerStatsManager.Instance.Profile, normalized, isExtension);
        RecalculateCompatibilityState();
        Save();
        OnPassChanged?.Invoke();
    }

    public bool IsPassPermanentlyUnlocked(string passId)
    {
        return RaceRescueProgression.IsPassIdPermanentlyUnlocked(passId);
    }

    public bool IsPassPermanentlyUnlocked(int level)
    {
        if (RaceRescueProgression.IsPassLevelPermanentlyUnlocked(level))
            return true;

        if (config == null)
            return false;

        string passId = config.GetPassIdForLevel(level);
        return !string.IsNullOrWhiteSpace(passId) && RaceRescueProgression.IsPassIdPermanentlyUnlocked(passId);
    }

    public int GetHighestPermanentUnlockedLevel()
    {
        int legacyHighest = RaceRescueProgression.GetHighestPermanentlyUnlockedPassLevel();
        int idHighest = -1;

        if (config != null)
        {
            List<string> ids = RaceRescueProgression.GetPermanentlyUnlockedPassIds();
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    int idx = config.GetLevelIndexByPassId(ids[i]);
                    if (idx >= 0)
                        idHighest = Mathf.Max(idHighest, idx);
                }
            }
        }

        return Mathf.Max(legacyHighest, idHighest);
    }

    public void ApplyPermanentUnlocksFromProfile(bool equipBestUnlocked)
    {
        bool changed = SyncLegacyPermanentUnlockIdsFromLevels();

        if (HasAnyPermanentUnlocks() && !HasClaimedDefaultPass)
        {
            HasClaimedDefaultPass = true;
            changed = true;
        }

        // Permanent ownership no longer implies auto-equipping the numerically highest pass.
        _ = equipBestUnlocked;

        if (changed)
        {
            Save();
            OnPassChanged?.Invoke();
        }
    }

    private bool SyncLegacyPermanentUnlockIdsFromLevels()
    {
        if (config == null || PlayerStatsManager.Instance == null || PlayerStatsManager.Instance.Profile == null)
            return false;

        PlayerStatsProfile profile = PlayerStatsManager.Instance.Profile;
        profile.permanentlyUnlockedPassIds = RaceRescueProgression.EnsureList(profile.permanentlyUnlockedPassIds);
        profile.permanentlyUnlockedPassLevels = RaceRescueProgression.EnsureIntList(profile.permanentlyUnlockedPassLevels);

        bool changed = false;

        for (int i = 0; i < profile.permanentlyUnlockedPassLevels.Count; i++)
        {
            string mappedPassId = config.GetPassIdForLevel(profile.permanentlyUnlockedPassLevels[i]);
            if (string.IsNullOrWhiteSpace(mappedPassId))
                continue;

            string normalizedId = mappedPassId.Trim();
            if (profile.permanentlyUnlockedPassIds.Contains(normalizedId))
                continue;

            profile.permanentlyUnlockedPassIds.Add(normalizedId);
            ProgressionEventRecorder.RecordPassOwnership(profile, normalizedId);
            changed = true;
        }

        if (changed)
            PlayerStatsManager.Instance.Save();

        return changed;
    }

    public bool TryClaimDefaultPass(out string reason)
    {
        reason = null;

        if (config == null)
        {
            reason = "Missing SkiPassConfig.";
            return false;
        }

        if (HasClaimedDefaultPass)
        {
            reason = "Default pass already claimed.";
            return false;
        }

        HasClaimedDefaultPass = true;
        string defaultPassId = config.GetPassIdForLevel(config.defaultLevelIndex);
        if (PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Profile != null)
            ProgressionEventRecorder.RecordPassOwnership(PlayerStatsManager.Instance.Profile, defaultPassId);
        RecalculateCompatibilityState();

        Save();
        OnPassChanged?.Invoke();
        return true;
    }

    private void EnforceExpiry()
    {
        if (_activePasses.Count == 0)
            return;

        double now = GetNowGameHoursOrFallback();
        int before = _activePasses.Count;

        RemoveExpiredActivePasses(now);

        if (_activePasses.Count != before)
        {
            RecalculateCompatibilityState();
            Save();
            OnPassChanged?.Invoke();
        }
    }

    public bool TryGrantMinimumLevel(int level)
    {
        if (config == null)
            return false;

        int target = config.ClampLevel(level);
        string passId = config.GetPassIdForLevel(target);
        if (string.IsNullOrWhiteSpace(passId))
            return false;

        if (!HasClaimedDefaultPass)
            TryClaimDefaultPass(out _);

        if (IsPassActive(passId))
            return false;

        double now = GetNowGameHoursOrFallback();

        _activePasses.Add(new ActivePassRecord
        {
            passId = passId.Trim(),
            startGameHours = now,
            expiryGameHours = now + (24.0 * 3650.0),
            totalHoursPurchased = 24.0 * 3650.0
        });

        HasClaimedDefaultPass = true;
        if (PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Profile != null)
            ProgressionEventRecorder.RecordPassOwnership(PlayerStatsManager.Instance.Profile, passId);
        RecalculateCompatibilityState();
        Save();
        OnPassChanged?.Invoke();
        return true;
    }

    private void Load()
    {
        HasClaimedDefaultPass = PlayerPrefs.GetInt(Key_DefaultClaimed, 0) == 1;

        _activePasses.Clear();

        string activeJson = PlayerPrefs.GetString(Key_ActivePassesJson, string.Empty);
        if (!string.IsNullOrWhiteSpace(activeJson))
        {
            try
            {
                ActivePassRecordListWrapper wrapper = JsonUtility.FromJson<ActivePassRecordListWrapper>(activeJson);
                if (wrapper != null && wrapper.items != null)
                {
                    for (int i = 0; i < wrapper.items.Count; i++)
                    {
                        ActivePassRecord record = wrapper.items[i];
                        if (record == null || string.IsNullOrWhiteSpace(record.passId))
                            continue;

                        _activePasses.Add(record);
                    }
                }
            }
            catch
            {
                // Ignore malformed save payloads and fall back to legacy migration below.
            }
        }

        // Legacy migration path: convert the old single active pass window into one active pass record.
        if (_activePasses.Count == 0)
        {
            int legacyLevel = PlayerPrefs.GetInt(Key_Level, 0);
            if (config != null)
                legacyLevel = config.ClampLevel(legacyLevel);

            string expiryStr = PlayerPrefs.GetString(Key_ExpiryGameHours, "");
            string totalStr = PlayerPrefs.GetString(Key_TotalHours, "0");

            if (double.TryParse(expiryStr, out double eg) &&
                eg > 0.0 &&
                double.TryParse(totalStr, out double th) &&
                th > 0.0 &&
                config != null)
            {
                string legacyPassId = config.GetPassIdForLevel(legacyLevel);
                if (!string.IsNullOrWhiteSpace(legacyPassId))
                {
                    _activePasses.Add(new ActivePassRecord
                    {
                        passId = legacyPassId.Trim(),
                        startGameHours = eg - th,
                        expiryGameHours = eg,
                        totalHoursPurchased = Math.Max(0.0, th)
                    });
                }
            }
        }

        RemoveExpiredActivePasses(GetNowGameHoursOrFallback());
        RecalculateCompatibilityState();
    }

    private void Save()
    {
        ActivePassRecordListWrapper wrapper = new ActivePassRecordListWrapper();
        wrapper.items.AddRange(_activePasses);

        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(Key_ActivePassesJson, json);

        PlayerPrefs.SetInt(Key_DefaultClaimed, HasClaimedDefaultPass ? 1 : 0);
        PlayerPrefs.SetInt(Key_Level, CurrentLevel);

        ActivePassRecord primary = GetPrimaryActivePassRecord();
        if (primary != null)
        {
            PlayerPrefs.SetString(Key_ExpiryGameHours, primary.expiryGameHours.ToString("R"));
            PlayerPrefs.SetString(Key_TotalHours, primary.totalHoursPurchased.ToString("R"));
        }
        else
        {
            PlayerPrefs.SetString(Key_ExpiryGameHours, "");
            PlayerPrefs.SetString(Key_TotalHours, "0");
        }

        PlayerPrefs.Save();
    }
}
