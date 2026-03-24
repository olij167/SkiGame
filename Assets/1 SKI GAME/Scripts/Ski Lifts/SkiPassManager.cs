using System;
using UnityEngine;

public class SkiPassManager : MonoBehaviour
{
    public static SkiPassManager Instance { get; private set; }

    [SerializeField] private SkiPassConfigSO config;

    // Persisted state (lightweight)
    private const string Key_Level = "ski_pass_level";
    private const string Key_ExpiryGameHours = "ski_pass_expiry_game_hours";   // absolute game-hours (dayCount*24 + hour)
    private const string Key_TotalHours = "ski_pass_total_hours";             // for progress bar fraction
    private const string Key_DefaultClaimed = "ski_pass_default_claimed";

    public event Action OnPassChanged;

    public int CurrentLevel { get; private set; }

    /// <summary>Absolute game-hours when pass expires (based on TimeWeather.TimeController). Null = no expiry.</summary>
    public double? ExpiryGameHours { get; private set; }

    /// <summary>Total purchased hours remaining baseline (used for progress bar fraction). Null/0 means no timed pass.</summary>
    public double TotalHours { get; private set; }

    public SkiPassConfigSO Config => config;

    public bool HasClaimedDefaultPass { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        EnforceExpiry();
    }

    private void Update()
    {
        EnforceExpiry();
    }

    // -----------------------------
    // Time integration (TimeWeather)
    // -----------------------------

    private bool TryGetNowGameHours(out double nowHours)
    {
        nowHours = 0;

        // Hard reference is fine: your PhoneHUDController already depends on TimeWeather.
        var t = TimeWeather.TimeController.instance != null
            ? TimeWeather.TimeController.instance
            : FindObjectOfType<TimeWeather.TimeController>();

        if (t == null) return false;

        int day = Mathf.Max(0, t.dayCount);
        int hh = Mathf.Clamp(t.timeHours, 0, 23);
        float mm = Mathf.Clamp((float)t.timeMinutes, 0f, 59f);

        nowHours = (day * 24.0) + hh + (mm / 60.0);
        return true;
    }

    private double GetNowGameHoursOrFallback()
    {
        if (TryGetNowGameHours(out var h)) return h;
        // Fallback: use realtime hours. (Only if TimeController absent.)
        return Time.realtimeSinceStartup / 3600.0;
    }

    // -----------------------------
    // Public helpers
    // -----------------------------

    public string GetCurrentPassDisplayName()
    {
        var p = config != null ? config.Get(CurrentLevel) : null;
        if (p == null) return $"Level {CurrentLevel}";
        return $"{p.displayName} (L{CurrentLevel})";
    }

    public bool CanUseLift(int requiredLevel)
    {
        requiredLevel = Mathf.Max(0, requiredLevel);

        if (!HasClaimedDefaultPass)
            return false;

        return CurrentLevel >= requiredLevel;
    }

    public bool HasTimedPass => ExpiryGameHours.HasValue;

    public double GetRemainingHours()
    {
        if (!ExpiryGameHours.HasValue) return double.PositiveInfinity;
        double now = GetNowGameHoursOrFallback();
        return Math.Max(0.0, ExpiryGameHours.Value - now);
    }

    public float GetRemainingFraction01()
    {
        if (!ExpiryGameHours.HasValue) return 1f;
        if (TotalHours <= 0.0001) return 0f;

        double rem = GetRemainingHours();
        return Mathf.Clamp01((float)(rem / TotalHours));
    }

    public string GetRemainingTimeString()
    {
        if (!ExpiryGameHours.HasValue) return "No expiry";

        double remH = GetRemainingHours();
        if (remH <= 0) return "Expired";

        double remD = remH / 24.0;
        if (remD >= 2.0) return $"Expires in {Math.Ceiling(remD)}d";

        int hours = Mathf.CeilToInt((float)remH);
        return $"Expires in {hours}h";
    }

    // -----------------------------
    // Purchase / Upgrade
    // -----------------------------

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

        // Level 0 / default pass is free and cannot be purchased.
        if (config != null && targetLevel == config.defaultLevelIndex)
        {
            reason = "Basic pass is free.";
            return false;
        }

        // Cannot buy below current active pass level.
        if (targetLevel < CurrentLevel)
        {
            reason = "You can't purchase a pass below your active level.";
            return false;
        }

        var target = config.Get(targetLevel);
        var current = config.Get(CurrentLevel);

        int dayPriceTarget = target != null ? Mathf.Max(0, target.dayPrice) : 0;
        int dayPriceCurrent = current != null ? Mathf.Max(0, current.dayPrice) : 0;

        int days = Mathf.Max(1, dur.days);
        float discount01 = Mathf.Clamp01(dur.discount01);

        int baseCost = Mathf.CeilToInt(dayPriceTarget * days * (1f - discount01));

        double remainingHours = HasTimedPass ? GetRemainingHours() : 0.0;

        int credit = 0;
        bool isUpgrade = targetLevel > CurrentLevel && HasTimedPass && remainingHours > 0.001 && dayPriceCurrent > 0;

        if (isUpgrade)
        {
            double remainingDays = remainingHours / 24.0;
            double rawCredit = remainingDays * dayPriceCurrent;
            rawCredit *= Mathf.Clamp01(config.upgradeCreditMultiplier01);
            credit = Mathf.FloorToInt((float)rawCredit);
            credit = Mathf.Max(0, credit);
        }

        int finalCost = Mathf.Max(0, baseCost - credit);

        q = new Quote
        {
            targetLevel = targetLevel,
            durationDays = days,
            discount01 = discount01,
            baseCost = baseCost,
            credit = credit,
            finalCost = finalCost,
            isUpgrade = isUpgrade,
            isExtend = (targetLevel == CurrentLevel),
            remainingHours = remainingHours,
            addedHours = days * 24.0
        };

        return true;
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

        ApplyPurchase(q.targetLevel, q.addedHours);
        failReason = null;
        return true;
    }

    private void ApplyPurchase(int newLevel, double addedHours)
    {
        newLevel = config != null ? config.ClampLevel(newLevel) : Mathf.Max(0, newLevel);

        double now = GetNowGameHoursOrFallback();

        // Carry remaining time if any: extend from existing expiry if it's still in the future
        double baseExpiry = now;
        if (ExpiryGameHours.HasValue && ExpiryGameHours.Value > now)
            baseExpiry = ExpiryGameHours.Value;

        ExpiryGameHours = baseExpiry + Math.Max(0.0, addedHours);

        // TotalHours is used for the progress bar fraction.
        // If we had remaining time, total should include that + added.
        double remaining = 0.0;
        if (ExpiryGameHours.HasValue && baseExpiry > now)
            remaining = baseExpiry - now;

        TotalHours = Math.Max(0.0, remaining + addedHours);

        CurrentLevel = newLevel;

        Save();
        OnPassChanged?.Invoke();
    }

    public void RevertToDefault()
    {
        int def = config != null ? config.defaultLevelIndex : 0;
        CurrentLevel = Mathf.Max(0, def);

        ExpiryGameHours = null;
        TotalHours = 0;

        Save();
        OnPassChanged?.Invoke();
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

        int def = Mathf.Max(0, config.defaultLevelIndex);
        CurrentLevel = def;
        ExpiryGameHours = null;
        TotalHours = 0;
        HasClaimedDefaultPass = true;

        Save();
        OnPassChanged?.Invoke();
        return true;
    }

    private void EnforceExpiry()
    {
        if (!ExpiryGameHours.HasValue) return;

        double now = GetNowGameHoursOrFallback();
        if (now >= ExpiryGameHours.Value)
            RevertToDefault();
    }

    // -----------------------------
    // Save / Load
    // -----------------------------

    private void Load()
    {
        HasClaimedDefaultPass = PlayerPrefs.GetInt(Key_DefaultClaimed, 0) == 1;

        CurrentLevel = PlayerPrefs.GetInt(Key_Level, 0);
        if (config != null) CurrentLevel = config.ClampLevel(CurrentLevel);

        string expiryStr = PlayerPrefs.GetString(Key_ExpiryGameHours, "");
        if (double.TryParse(expiryStr, out var eg) && eg > 0.0)
            ExpiryGameHours = eg;
        else
            ExpiryGameHours = null;

        string totalStr = PlayerPrefs.GetString(Key_TotalHours, "0");
        if (double.TryParse(totalStr, out var th))
            TotalHours = Math.Max(0.0, th);
        else
            TotalHours = 0.0;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(Key_DefaultClaimed, HasClaimedDefaultPass ? 1 : 0);

        PlayerPrefs.SetInt(Key_Level, CurrentLevel);

        PlayerPrefs.SetString(Key_ExpiryGameHours, ExpiryGameHours.HasValue ? ExpiryGameHours.Value.ToString("R") : "");
        PlayerPrefs.SetString(Key_TotalHours, TotalHours.ToString("R"));

        PlayerPrefs.Save();
    }
}
