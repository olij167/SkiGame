using System;
using System.Collections.Generic;
using TimeWeather;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class ProgressionDirector : MonoBehaviour
    {
        public static ProgressionDirector Instance { get; private set; }

        [Header("Definitions")]
        [SerializeField] private ProgressionCatalogSO catalog;

        [Header("Daily Tasks")]
        [SerializeField, Min(0)] private int dailyLadderCount = 6;

        [Tooltip("Optional external day key")]
        [SerializeField] private int externalDayKey = -1;

        [Tooltip("How often to evaluate tasks/achievements. Keeps it cheap and deterministic.")]
        [SerializeField, Range(0.05f, 2f)] private float evaluateIntervalSeconds = 0.25f;

        [Header("Tutorial")]
        [SerializeField] private DailyTaskLadderDefinitionSO tutorialWelcomeLadder;

        public event Action<TaskDefinitionSO> OnTaskCompleted;
        public event Action<AchievementDefinitionSO> OnAchievementUnlocked;

        // Optional: daily tier completion event (for SFX/UI).
        public event Action<ProgressionMetric, int /*tierIndex*/> OnDailyTierCompleted;

        private float _nextEvalTime;

        private readonly Dictionary<string, TaskDefinitionSO> _taskById = new();
        private readonly Dictionary<string, AchievementDefinitionSO> _achById = new();
        private readonly Dictionary<string, DailyTaskLadderDefinitionSO> _dailyById = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildLookups();
        }

        private void Start()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr != null && mgr.Profile != null)
            {
                EnsureDailyTasks(mgr.Profile, mgr);
            }

            _nextEvalTime = Time.unscaledTime + evaluateIntervalSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextEvalTime) return;
            _nextEvalTime = Time.unscaledTime + evaluateIntervalSeconds;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || catalog == null) return;

            profile.Sanitize();

            EnsureDailyTasks(profile, mgr);

            EvaluateDailyTasks(profile, mgr);
            EvaluateAchievements(profile, mgr);
        }

        private void BuildLookups()
        {
            _taskById.Clear();
            _achById.Clear();
            _dailyById.Clear();

            if (catalog == null) return;

            if (catalog.tasks != null)
            {
                for (int i = 0; i < catalog.tasks.Count; i++)
                {
                    var t = catalog.tasks[i];
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    _taskById[t.id] = t;
                }
            }

            if (catalog.achievements != null)
            {
                for (int i = 0; i < catalog.achievements.Count; i++)
                {
                    var a = catalog.achievements[i];
                    if (a == null || string.IsNullOrEmpty(a.id)) continue;
                    _achById[a.id] = a;
                }
            }

            if (catalog.dailyTaskLadders != null)
            {
                for (int i = 0; i < catalog.dailyTaskLadders.Count; i++)
                {
                    var d = catalog.dailyTaskLadders[i];
                    if (d == null) continue;
                    var id = d.SafeId;
                    if (string.IsNullOrEmpty(id)) continue;
                    _dailyById[id] = d;
                }
            }
        }

        // -------------------- Day Key --------------------

        [SerializeField] private TimeController timeController;

        public void SetExternalDayKey(int dayKey)
        {
            externalDayKey = dayKey;
        }

        private int GetCurrentDayKey()
        {

            // 2) Caller-provided day key (optional)
            if (externalDayKey >= 0)
                return externalDayKey;

            // 3) Preferred: in-game calendar day key (midnight driven by TimeController)
            if (timeController == null)
            {
#if UNITY_2023_1_OR_NEWER
                timeController = FindFirstObjectByType<TimeController>();
#else
        timeController = FindObjectOfType<TimeController>();
#endif
            }

            if (timeController != null)
                return ComputeInGameDayKey(timeController);

            // 4) Fallback (should be rare): UTC key if no TimeController exists
            return ComputeFallbackDayKey();
        }

        private static int ComputeInGameDayKey(TimeController tc)
        {
            // Stable integer key: yyyy*1000 + (dayCount+1)
            // dayCount is "total days passed in current year" (starts at 0).
            int year = tc.currentYear;
            int dayOfYear = tc.dayCount + 1;
            return (year * 1000) + dayOfYear;
        }

        private static int ComputeFallbackDayKey()
        {
            // Stable integer day key: yyyy*1000 + dayOfYear
            var now = DateTime.UtcNow;
            return (now.Year * 1000) + now.DayOfYear;
        }

        // -------------------- Achievements --------------------

        private void EvaluateAchievements(PlayerStatsProfile profile, PlayerStatsManager mgr)
        {
            if (catalog == null || catalog.achievements == null) return;

            bool anyUnlocked = false;

            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var def = catalog.achievements[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;

                if (profile.HasAchievement(def.id))
                    continue;

                float cur = def.ReadCurrent(profile);
                float tgt = Mathf.Max(0.0001f, def.target);

                if (cur + 0.0001f >= tgt)
                {
                    if (profile.TryAddAchievement(def.id))
                    {
                        anyUnlocked = true;
                        OnAchievementUnlocked?.Invoke(def);

                        if (def.autoClaimRewardOnUnlock)
                            TryClaimAchievement(def.id, out _);
                    }
                }
            }

            if (anyUnlocked && mgr != null)
                mgr.Save();
        }

        public int GetAchievementsForMetric(ProgressionMetric metric, List<AchievementDefinitionSO> buffer)
        {
            if (buffer != null) buffer.Clear();
            if (catalog == null || catalog.achievements == null) return 0;

            int count = 0;
            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var a = catalog.achievements[i];
                if (a == null) continue;
                if (a.metric != metric) continue;

                count++;
                buffer?.Add(a);
            }
            return count;
        }

        // -------------------- Daily Task Matrix (New) --------------------

        private void EnsureDailyTasks(PlayerStatsProfile profile, PlayerStatsManager mgr)
        {
            if (profile == null) return;
            profile.Sanitize();

            if (catalog == null || catalog.dailyTaskLadders == null)
                return;

            int dayKey = GetCurrentDayKey();

            // If day changed (or never initialised), regenerate.
            bool needsRegen = profile.dailyTasks == null
                              || profile.dailyTasks.dayKey != dayKey
                              || profile.dailyTasks.rows == null
                              || profile.dailyTasks.rows.Count == 0;

            if (needsRegen)
            {
                if (profile.dailyTasks == null)
                    profile.dailyTasks = new PlayerStatsProfile.DailyTaskMatrixState();

                profile.dailyTasks.dayKey = dayKey;
                profile.dailyTasks.rows = new List<PlayerStatsProfile.DailyTaskRowState>();

                var selected = PickDailyLadders(profile, dayKey, dailyLadderCount);

                for (int i = 0; i < selected.Count; i++)
                {
                    var def = selected[i];
                    if (def == null) continue;

                    float current = ReadMetricValue(profile, def.metric);

                    var row = new PlayerStatsProfile.DailyTaskRowState
                    {
                        ladderId = def.SafeId,
                        metric = def.metric,
                        evaluationMode = def.evaluationMode,

                        baselineValue = current,
                        baselineCaptured = true,

                        bestValueSinceStart = current,
                        rowBonusClaimed = false,

                        tiers = new List<PlayerStatsProfile.DailyTaskTierState>()
                    };

                    int tierCount = def.tierTargets != null ? def.tierTargets.Count : 0;
                    for (int t = 0; t < tierCount; t++)
                    {
                        float target = Mathf.Max(0.0001f, def.tierTargets[t]);
                        row.tiers.Add(new PlayerStatsProfile.DailyTaskTierState
                        {
                            target = target,
                            lastProgress = 0f,
                            completed = false,
                            claimed = false,
                            completedUtc = default,
                            lastRewardGranted = 0
                        });
                    }

                    profile.dailyTasks.rows.Add(row);
                    profile.RememberRecentDailyLadder(def.SafeId, max: 12);
                }

                mgr?.Save();
            }
            else
            {
                // Sanity pass: remove rows whose ladders no longer exist
                for (int i = profile.dailyTasks.rows.Count - 1; i >= 0; i--)
                {
                    var row = profile.dailyTasks.rows[i];
                    if (row == null || string.IsNullOrEmpty(row.ladderId) || !_dailyById.ContainsKey(row.ladderId))
                        profile.dailyTasks.rows.RemoveAt(i);
                }

                // Ensure baselines exist (older saves / partial data)
                for (int i = 0; i < profile.dailyTasks.rows.Count; i++)
                {
                    var row = profile.dailyTasks.rows[i];
                    if (row == null) continue;

                    if (!row.baselineCaptured)
                    {
                        float current = ReadMetricValue(profile, row.metric);
                        row.baselineValue = current;
                        row.bestValueSinceStart = current;
                        row.baselineCaptured = true;

                        if (row.tiers != null)
                        {
                            for (int t = 0; t < row.tiers.Count; t++)
                                row.tiers[t].lastProgress = 0f;
                        }
                    }
                }
            }
        }

        private static int GetFirstUnclaimedTierIndex(PlayerStatsProfile.DailyTaskRowState row)
        {
            if (row == null || row.tiers == null || row.tiers.Count == 0)
                return -1;

            for (int i = 0; i < row.tiers.Count; i++)
            {
                var tier = row.tiers[i];
                if (tier == null)
                    continue;

                if (!tier.claimed)
                    return i;
            }

            return -1;
        }
        private void EvaluateDailyTasks(PlayerStatsProfile profile, PlayerStatsManager mgr)
        {
            if (profile.dailyTasks == null || profile.dailyTasks.rows == null || profile.dailyTasks.rows.Count == 0)
                return;

            bool anyCompleted = false;

            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null) continue;

                // Get ladder definition
                if (string.IsNullOrEmpty(row.ladderId) || !_dailyById.TryGetValue(row.ladderId, out var ladder) || ladder == null)
                    continue;

                float absolute = ReadMetricValue(profile, row.metric);

                if (!row.baselineCaptured)
                {
                    row.baselineValue = absolute;
                    row.bestValueSinceStart = absolute;
                    row.baselineCaptured = true;
                }

                float progressForDay;

                switch (row.evaluationMode)
                {
                    case DailyTaskLadderDefinitionSO.EvaluationMode.MaxSinceDayStart:
                        if (absolute > row.bestValueSinceStart)
                            row.bestValueSinceStart = absolute;
                        progressForDay = row.bestValueSinceStart;
                        break;

                    case DailyTaskLadderDefinitionSO.EvaluationMode.DeltaFromBaseline:
                    default:
                        progressForDay = absolute - row.baselineValue;
                        if (progressForDay < 0f) progressForDay = 0f;
                        break;
                }

                // Update only the current active tier.
                // A ladder should not advance to later tiers until the current completed tier is claimed.
                if (row.tiers == null) continue;

                int activeTierIndex = GetFirstUnclaimedTierIndex(row);
                if (activeTierIndex < 0)
                    continue;

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    if (t == activeTierIndex)
                    {
                        tier.lastProgress = progressForDay;

                        if (!tier.completed && progressForDay + 0.0001f >= tier.target)
                        {
                            tier.completed = true;
                            tier.completedUtc = DateTimeUtc.Now();
                            anyCompleted = true;

                            OnDailyTierCompleted?.Invoke(row.metric, t);
                        }
                    }
                    else if (!tier.claimed)
                    {
                        // Keep future tiers visually unprogressed until they become active.
                        tier.lastProgress = 0f;
                        tier.completed = false;
                        tier.completedUtc = default;
                    }
                }

                if (anyCompleted && mgr != null)
                    mgr.Save();
            }
        }

        private List<DailyTaskLadderDefinitionSO> PickDailyLadders(PlayerStatsProfile profile, int dayKey, int desiredCount)
        {
            var result = new List<DailyTaskLadderDefinitionSO>(desiredCount);

            if (catalog == null || catalog.dailyTaskLadders == null || catalog.dailyTaskLadders.Count == 0)
                return result;

            // Filter valid ladders
            var pool = new List<DailyTaskLadderDefinitionSO>(catalog.dailyTaskLadders.Count);
            for (int i = 0; i < catalog.dailyTaskLadders.Count; i++)
            {
                var d = catalog.dailyTaskLadders[i];
                if (d == null) continue;
                if (d.tierTargets == null || d.tierTargets.Count == 0) continue;
                pool.Add(d);
            }

            if (pool.Count == 0) return result;

            // Deterministic shuffle using dayKey
            var rng = new System.Random(dayKey);

            // Prefer not-recent ladders if possible
            bool IsRecent(string id)
            {
                if (profile.recentDailyLadderIds == null) return false;
                return profile.recentDailyLadderIds.Contains(id);
            }

            var nonRecent = new List<DailyTaskLadderDefinitionSO>(pool.Count);
            var recent = new List<DailyTaskLadderDefinitionSO>(pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i];
                if (d == null) continue;
                if (IsRecent(d.SafeId)) recent.Add(d);
                else nonRecent.Add(d);
            }

            void PickFrom(List<DailyTaskLadderDefinitionSO> src)
            {
                // Fisher-Yates partial shuffle
                for (int i = src.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(0, i + 1);
                    (src[i], src[j]) = (src[j], src[i]);
                }

                for (int i = 0; i < src.Count && result.Count < desiredCount; i++)
                    result.Add(src[i]);
            }

            PickFrom(nonRecent);
            if (result.Count < desiredCount)
                PickFrom(recent);

            return result;
        }

        // Metric value reader for daily tasks (uses numeric stats only).
        // If you want daily unique POIs etc., add daily-specific accumulators later.
        private float ReadMetricValue(PlayerStatsProfile profile, ProgressionMetric metric)
        {
            return ProgressionMetricUtility.ReadMetricValue(profile, metric);
        }

        // -------------------- Daily Claiming API --------------------

        public bool TryGetDailyLadderDefinition(string ladderId, out DailyTaskLadderDefinitionSO ladder)
        {
            ladder = null;
            if (string.IsNullOrEmpty(ladderId)) return false;
            return _dailyById != null && _dailyById.TryGetValue(ladderId, out ladder) && ladder != null;
        }

        public bool TryClaimDailyTier(string ladderId, int tierIndex, out int rewardGranted, out bool rowBonusGranted, out int rowBonusAmount)
        {
            rewardGranted = 0;
            rowBonusGranted = false;
            rowBonusAmount = 0;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return false;

            var profile = mgr.Profile;
            if (profile == null) return false;

            profile.Sanitize();
            if (profile.dailyTasks == null || profile.dailyTasks.rows == null) return false;
            if (string.IsNullOrEmpty(ladderId)) return false;

            // Find row
            PlayerStatsProfile.DailyTaskRowState row = null;
            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var rr = profile.dailyTasks.rows[r];
                if (rr != null && rr.ladderId == ladderId)
                {
                    row = rr;
                    break;
                }
            }
            if (row == null) return false;
            if (!_dailyById.TryGetValue(ladderId, out var ladder) || ladder == null) return false;
            if (row.tiers == null) return false;
            if (tierIndex < 0 || tierIndex >= row.tiers.Count) return false;

            var tier = row.tiers[tierIndex];
            if (tier == null) return false;
            if (!tier.completed) return false;
            if (tier.claimed) return false;

            // Reward
            if (!ladder.TryGetTierReward(tierIndex, out rewardGranted))
                rewardGranted = ComputeDerivedReward(row.metric, tier.target, explicitReward: 0);

            ProgressionEventRecorder.AddCurrency(profile, rewardGranted);

            tier.claimed = true;
            tier.lastRewardGranted = rewardGranted;

            // Row bonus if all tiers are completed+claimed
            if (!row.rowBonusClaimed && IsRowFullyClaimed(row))
            {
                int bonus = ladder.rowBonusReward > 0 ? ladder.rowBonusReward : ComputeDerivedRowBonus(row.metric, row.tiers.Count);
                if (bonus > 0)
                {
                    ProgressionEventRecorder.AddCurrency(profile, bonus);
                    row.rowBonusClaimed = true;
                    rowBonusGranted = true;
                    rowBonusAmount = bonus;
                }
            }

            mgr.Save();
            return true;
        }

        public int ClaimAllDailyCompleted(out int rowBonusesGranted)
        {
            rowBonusesGranted = 0;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return 0;

            var profile = mgr.Profile;
            if (profile == null) return 0;

            profile.Sanitize();
            if (profile.dailyTasks == null || profile.dailyTasks.rows == null) return 0;

            int total = 0;

            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null || row.tiers == null) continue;

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;
                    if (!tier.completed || tier.claimed) continue;

                    if (TryClaimDailyTier(row.ladderId, t, out int rw, out bool rowBonus, out int bonusAmt))
                    {
                        total += rw;
                        if (rowBonus)
                        {
                            rowBonusesGranted++;
                            total += bonusAmt;
                        }
                    }
                }
            }

            return total;
        }

        public bool TryClaimAchievement(string achievementId, out string failReason)
        {
            failReason = null;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;

            if (profile == null)
            {
                failReason = "No profile loaded.";
                return false;
            }

            if (catalog == null || catalog.achievements == null)
            {
                failReason = "No achievement catalog.";
                return false;
            }

            AchievementDefinitionSO def = null;
            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var candidate = catalog.achievements[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                    continue;

                if (candidate.id == achievementId)
                {
                    def = candidate;
                    break;
                }
            }

            if (def == null)
            {
                failReason = "Achievement not found.";
                return false;
            }

            if (!profile.HasAchievement(def.id))
            {
                failReason = "Achievement not completed yet.";
                return false;
            }

            if (profile.HasClaimedAchievement(def.id))
            {
                failReason = "Reward already claimed.";
                return false;
            }

            if (def.currencyReward > 0)
                ProgressionEventRecorder.AddCurrency(profile, def.currencyReward);

            if (def.skiPassLevelReward >= 0 && SkiPassManager.Instance != null)
                SkiPassManager.Instance.TryGrantMinimumLevel(def.skiPassLevelReward);

            if (def.customizationUnlocks != null)
            {
                profile.customization ??= new PlayerStatsProfile.CustomizationState();

                for (int i = 0; i < def.customizationUnlocks.Count; i++)
                {
                    var opt = def.customizationUnlocks[i];
                    if (opt == null || string.IsNullOrWhiteSpace(opt.id))
                        continue;

                    profile.customization.Unlock(opt.id);
                }
            }

            if (!profile.TryClaimAchievementReward(def.id))
            {
                failReason = "Failed to mark reward as claimed.";
                return false;
            }

            mgr.Save();
            return true;
        }

        [SerializeField] private int currencyToAdd = 10;
        
        [ContextMenu("Add Currency")]
        public void AddCurrency()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return;

            var profile = mgr.Profile;
            if (profile == null) return;

            ProgressionEventRecorder.AddCurrency(profile, currencyToAdd);
        }

        /// <summary>
        /// Total currency currently claimable from completed-but-unclaimed daily tiers, plus any
        /// row bonus rewards where all tiers are completed and the bonus is unclaimed.
        /// </summary>
        public int GetDailyClaimableCurrency(out int rowBonusesClaimable)
        {
            rowBonusesClaimable = 0;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return 0;

            var profile = mgr.Profile;
            if (profile == null) return 0;

            profile.Sanitize();
            if (profile.dailyTasks == null || profile.dailyTasks.rows == null) return 0;

            int total = 0;

            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null || row.tiers == null) continue;

                _dailyById.TryGetValue(row.ladderId, out var ladder);

                if (ladder != null)
                {
                    // Row bonus claimable?
                    bool allCompleted = row.tiers.Count > 0;
                    for (int i = 0; i < row.tiers.Count; i++)
                    {
                        var tier = row.tiers[i];
                        if (tier == null || !tier.completed) { allCompleted = false; break; }
                    }

                    if (allCompleted && !row.rowBonusClaimed && ladder.rowBonusReward > 0)
                    {
                        rowBonusesClaimable++;
                        total += ladder.rowBonusReward;
                    }

                    // Tier rewards claimable?
                    for (int t = 0; t < row.tiers.Count; t++)
                    {
                        var tier = row.tiers[t];
                        if (tier == null) continue;
                        if (!tier.completed || tier.claimed) continue;

                        if (ladder.TryGetTierReward(t, out int reward))
                            total += reward;
                    }
                }
            }

            return total;
        }

        public readonly struct DailyTierDisplay
        {
            public readonly string ladderId;
            public readonly ProgressionMetric metric;
            public readonly int rowIndex;
            public readonly int tierIndex;
            public readonly float target;
            public readonly float progress;
            public readonly float pct01;
            public readonly bool completed;
            public readonly bool claimed;
            public readonly int reward;

            public DailyTierDisplay(
                string ladderId,
                ProgressionMetric metric,
                int rowIndex,
                int tierIndex,
                float target,
                float progress,
                bool completed,
                bool claimed,
                int reward)
            {
                this.ladderId = ladderId;
                this.metric = metric;
                this.rowIndex = rowIndex;
                this.tierIndex = tierIndex;
                this.target = target;
                this.progress = progress;
                this.completed = completed;
                this.claimed = claimed;
                this.reward = reward;
                this.pct01 = target > 0f ? Mathf.Clamp01(progress / target) : 0f;
            }
        }

        /// <summary>
        /// Builds flattened daily tier displays in row order.
        /// </summary>
        public void GetDailyTierDisplays(List<DailyTierDisplay> buffer)
        {
            buffer.Clear();

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return;

            var profile = mgr.Profile;
            if (profile == null) return;

            profile.Sanitize();
            if (profile.dailyTasks == null || profile.dailyTasks.rows == null) return;

            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null || row.tiers == null) continue;

                _dailyById.TryGetValue(row.ladderId, out var ladder);

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    int reward = 0;
                    if (ladder != null && ladder.TryGetTierReward(t, out int rw))
                        reward = rw;

                    buffer.Add(new DailyTierDisplay(
                        row.ladderId,
                        row.metric,
                        r,
                        t,
                        tier.target,
                        tier.lastProgress,
                        tier.completed,
                        tier.claimed,
                        reward));
                }
            }
        }

        private static bool IsRowFullyClaimed(PlayerStatsProfile.DailyTaskRowState row)
        {
            if (row == null || row.tiers == null || row.tiers.Count == 0) return false;

            for (int i = 0; i < row.tiers.Count; i++)
            {
                var t = row.tiers[i];
                if (t == null) return false;
                if (!t.completed) return false;
                if (!t.claimed) return false;
            }

            return true;
        }

        private int ComputeDerivedReward(ProgressionMetric metric, float target, int explicitReward)
        {
            if (explicitReward > 0) return explicitReward;

            float t = Mathf.Max(1f, target);
            int baseReward = 10;

            int scaled = baseReward + Mathf.Clamp(Mathf.RoundToInt(t / 100f) * 5, 0, 40);

            switch (metric)
            {
                case ProgressionMetric.SessionTopSpeedMps:
                case ProgressionMetric.LifetimeTopSpeedMps:
                    scaled += 10;
                    break;
                case ProgressionMetric.SessionAirTimeSeconds:
                case ProgressionMetric.LifetimeAirTimeSeconds:
                    scaled += 5;
                    break;
                case ProgressionMetric.SessionStacks:
                case ProgressionMetric.LifetimeStacks:
                    scaled -= 5;
                    break;
            }

            return Mathf.Max(1, scaled);
        }

        private int ComputeDerivedRowBonus(ProgressionMetric metric, int tierCount)
        {
            // Conservative default row bonus: small premium for clearing the entire ladder.
            int baseBonus = 25 + Mathf.Clamp(tierCount * 5, 0, 30);

            switch (metric)
            {
                case ProgressionMetric.SessionTopSpeedMps:
                case ProgressionMetric.LifetimeTopSpeedMps:
                    baseBonus += 10;
                    break;
                case ProgressionMetric.SessionStacks:
                case ProgressionMetric.LifetimeStacks:
                    baseBonus -= 10;
                    break;
            }

            return Mathf.Max(0, baseBonus);
        }

        // -------------------- Existing UI Helpers (Session tasks / achievements) --------------------

        public readonly struct TaskDisplay
        {
            public readonly string id;
            public readonly string title;

            public readonly bool completed;
            public readonly bool claimed;

            public readonly float current;
            public readonly float target;
            public readonly float pct01;

            public readonly int reward;

            public TaskDisplay(string id, string title, bool completed, bool claimed, float current, float target, int reward)
            {
                this.id = id;
                this.title = title;
                this.completed = completed;
                this.claimed = claimed;

                this.current = current;
                this.target = Mathf.Max(0.0001f, target);
                this.pct01 = Mathf.Clamp01(current / this.target);

                this.reward = Mathf.Max(0, reward);
            }
        }

        public int GetSessionTaskDisplays(PlayerStatsProfile profile, List<TaskDisplay> buffer)
        {
            buffer.Clear();

            if (profile?.activeSessionTasks == null || profile.activeSessionTasks.Count == 0)
                return 0;

            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var st = profile.activeSessionTasks[i];
                if (st == null) continue;

                if (!_taskById.TryGetValue(st.taskId, out var def) || def == null)
                    continue;

                string name = string.IsNullOrEmpty(def.shortTitle) ? def.title : def.shortTitle;

                float absolute = def.ReadCurrent(profile);
                float cur = st.baselineCaptured ? Mathf.Max(0f, absolute - st.baselineValue) : 0f;
                float tgt = (st.target > 0.0001f) ? st.target : Mathf.Max(0.0001f, def.target);

                int rw = ComputeTaskReward(def);
                buffer.Add(new TaskDisplay(def.id, name, st.completed, st.claimed, cur, tgt, rw));
            }

            buffer.Sort((a, b) =>
            {
                int Rank(TaskDisplay d)
                {
                    if (d.completed && !d.claimed) return 0;
                    if (!d.completed) return 1;
                    return 2;
                }

                int ra = Rank(a);
                int rb = Rank(b);
                int cmp = ra.CompareTo(rb);
                if (cmp != 0) return cmp;

                return b.pct01.CompareTo(a.pct01);
            });

            return buffer.Count;
        }

        public void GetTopTaskLines(PlayerStatsProfile profile, out string line1, out string line2)
        {
            line1 = "(no tasks)";
            line2 = "";

            if (profile?.activeSessionTasks == null || profile.activeSessionTasks.Count == 0) return;

            line1 = FormatTaskLine(profile, 0);
            if (profile.activeSessionTasks.Count > 1)
                line2 = FormatTaskLine(profile, 1);
        }

        private string FormatTaskLine(PlayerStatsProfile profile, int index)
        {
            var st = profile.activeSessionTasks[index];
            if (st == null) return "";

            if (!_taskById.TryGetValue(st.taskId, out var def) || def == null)
                return "(missing task)";

            float absolute = def.ReadCurrent(profile);
            float cur = st.baselineCaptured ? Mathf.Max(0f, absolute - st.baselineValue) : 0f;
            float tgt = Mathf.Max(0.0001f, st.target > 0.0001f ? st.target : def.target);
            float pct = Mathf.Clamp01(cur / tgt) * 100f;

            string name = string.IsNullOrEmpty(def.shortTitle) ? def.title : def.shortTitle;
            if (st.completed)
            {
                if (st.claimed) return $"{name}: Claimed";
                int rw = ComputeTaskReward(def);
                return $"{name}: Turn In (+{rw})";
            }

            return $"{name}: {cur:0}/{tgt:0} ({pct:0}%)";
        }

        public void GetTopAchievementLines(PlayerStatsProfile profile, out string line1, out string line2)
        {
            line1 = "(no achievements)";
            line2 = "";

            if (profile == null || catalog == null || catalog.achievements == null || catalog.achievements.Count == 0)
                return;

            AchievementDefinitionSO bestA = null, bestB = null;
            float bestPctA = -1f, bestPctB = -1f;

            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var def = catalog.achievements[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (profile.HasAchievement(def.id)) continue;

                float cur = def.ReadCurrent(profile);
                float tgt = Mathf.Max(0.0001f, def.target);
                float pct = Mathf.Clamp01(cur / tgt);

                if (pct > bestPctA)
                {
                    bestB = bestA; bestPctB = bestPctA;
                    bestA = def; bestPctA = pct;
                }
                else if (pct > bestPctB)
                {
                    bestB = def; bestPctB = pct;
                }
            }

            if (bestA != null)
                line1 = $"{bestA.title}: {(bestPctA * 100f):0}%";
            if (bestB != null)
                line2 = $"{bestB.title}: {(bestPctB * 100f):0}%";
        }

        private bool TryGetTaskDefinition(string id, out TaskDefinitionSO def)
        {
            def = null;
            if (catalog == null || catalog.tasks == null) return false;

            for (int i = 0; i < catalog.tasks.Count; i++)
            {
                var t = catalog.tasks[i];
                if (t != null && t.id == id)
                {
                    def = t;
                    return true;
                }
            }
            return false;
        }

        private TaskDefinitionSO PickNewTaskDefinition(PlayerStatsProfile profile)
        {
            if (catalog == null || catalog.tasks == null || catalog.tasks.Count == 0)
                return null;

            bool IsActive(string id)
            {
                for (int i = 0; i < profile.activeSessionTasks.Count; i++)
                {
                    var s = profile.activeSessionTasks[i];
                    if (s != null && s.taskId == id) return true;
                }
                return false;
            }

            bool IsRecent(string id)
            {
                if (profile.recentTaskIds == null) return false;
                return profile.recentTaskIds.Contains(id);
            }

            bool MetricAlreadyUsed(ProgressionMetric m)
            {
                for (int i = 0; i < profile.activeSessionTasks.Count; i++)
                {
                    var s = profile.activeSessionTasks[i];
                    if (s == null) continue;
                    if (TryGetTaskDefinition(s.taskId, out var def) && def != null && def.metric == m)
                        return true;
                }
                return false;
            }

            var candidates = new List<TaskDefinitionSO>(catalog.tasks.Count);
            for (int i = 0; i < catalog.tasks.Count; i++)
            {
                var t = catalog.tasks[i];
                if (t == null || string.IsNullOrEmpty(t.id)) continue;
                if (IsActive(t.id)) continue;
                if (IsRecent(t.id)) continue;
                if (MetricAlreadyUsed(t.metric)) continue;
                candidates.Add(t);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < catalog.tasks.Count; i++)
                {
                    var t = catalog.tasks[i];
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    if (IsActive(t.id)) continue;
                    if (IsRecent(t.id)) continue;
                    candidates.Add(t);
                }
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < catalog.tasks.Count; i++)
                {
                    var t = catalog.tasks[i];
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    if (IsActive(t.id)) continue;
                    candidates.Add(t);
                }
            }

            if (candidates.Count == 0) return null;

            int idx = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[idx];
        }

        private int ComputeTaskReward(TaskDefinitionSO def)
        {
            if (def == null) return 0;
            if (def.reward > 0) return def.reward;

            float t = Mathf.Max(1f, def.target);
            int baseReward = 10;

            int scaled = baseReward + Mathf.Clamp(Mathf.RoundToInt(t / 100f) * 5, 0, 40);

            switch (def.metric)
            {
                case ProgressionMetric.SessionTopSpeedMps:
                case ProgressionMetric.LifetimeTopSpeedMps:
                    scaled += 10;
                    break;
                case ProgressionMetric.SessionAirTimeSeconds:
                case ProgressionMetric.LifetimeAirTimeSeconds:
                    scaled += 5;
                    break;
                case ProgressionMetric.SessionStacks:
                case ProgressionMetric.LifetimeStacks:
                    scaled -= 5;
                    break;
            }

            return Mathf.Max(1, scaled);
        }

        public bool TryClaimTask(string taskId, out int rewardGranted)
        {
            rewardGranted = 0;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return false;

            var profile = mgr.Profile;
            if (profile == null) return false;

            profile.Sanitize();

            int index = -1;
            PlayerStatsProfile.ActiveTaskState state = null;

            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var s = profile.activeSessionTasks[i];
                if (s != null && s.taskId == taskId)
                {
                    index = i;
                    state = s;
                    break;
                }
            }

            if (state == null) return false;
            if (!state.completed) return false;
            if (state.claimed) return false;

            if (!TryGetTaskDefinition(taskId, out var def) || def == null)
                return false;

            rewardGranted = ComputeTaskReward(def);

            ProgressionEventRecorder.AddCurrency(profile, rewardGranted);

            state.claimed = true;
            state.lastRewardGranted = rewardGranted;

            profile.RememberRecentTask(taskId, max: 10);

            var newDef = PickNewTaskDefinition(profile);
            if (newDef != null)
            {
                float baseline = newDef.ReadCurrent(profile);

                profile.activeSessionTasks[index] = new PlayerStatsProfile.ActiveTaskState
                {
                    taskId = newDef.id,
                    completed = false,
                    claimed = false,
                    completedUtc = default,

                    lastProgress = 0f,
                    target = Mathf.Max(0.0001f, newDef.target),

                    baselineValue = baseline,
                    baselineCaptured = true,

                    lastRewardGranted = 0
                };
            }

            mgr.Save();
            return true;
        }

        public int ClaimAllCompleted()
        {
            int total = 0;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return 0;

            var profile = mgr.Profile;
            if (profile == null) return 0;

            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var s = profile.activeSessionTasks[i];
                if (s == null) continue;
                if (!s.completed || s.claimed) continue;

                if (TryClaimTask(s.taskId, out int reward))
                    total += reward;
            }

            return total;
        }

        public int GetAllAchievements(List<AchievementDefinitionSO> buffer)
        {
            if (buffer != null) buffer.Clear();
            if (catalog == null || catalog.achievements == null) return 0;

            int count = 0;
            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var a = catalog.achievements[i];
                if (a == null) continue;
                count++;
                buffer?.Add(a);
            }
            return count;
        }

        // Returns today's daily task rows from the current player profile.
        // This is a lightweight UI helper so PhoneHUDController doesn't need to know storage details.
        public IReadOnlyList<PlayerStatsProfile.DailyTaskRowState> GetTodayDailyRows()
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;

            if (profile == null)
                return Array.Empty<PlayerStatsProfile.DailyTaskRowState>();

            profile.Sanitize();

            var daily = profile.dailyTasks;
            if (daily == null || daily.rows == null || daily.rows.Count == 0)
                return Array.Empty<PlayerStatsProfile.DailyTaskRowState>();

            return daily.rows;
        }

        public bool EnsureAtLeastOneClaimableDailyTier(out string ladderId, out int tierIndex)
        {
            ladderId = null;
            tierIndex = -1;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return false;

            var profile = mgr.Profile;
            if (profile == null) return false;

            profile.Sanitize();
            EnsureDailyTasks(profile, mgr);

            if (profile.dailyTasks == null || profile.dailyTasks.rows == null)
                return false;

            // Prefer an already-claimable tutorial ladder tier if one exists.
            if (tutorialWelcomeLadder != null)
            {
                string tutorialId = tutorialWelcomeLadder.SafeId;

                for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
                {
                    var row = profile.dailyTasks.rows[r];
                    if (row == null || row.ladderId != tutorialId || row.tiers == null || row.tiers.Count == 0)
                        continue;

                    int activeTier = GetFirstUnclaimedTierIndex(row);
                    if (activeTier >= 0)
                    {
                        var tier = row.tiers[activeTier];
                        if (tier != null && tier.completed && !tier.claimed)
                        {
                            ladderId = row.ladderId;
                            tierIndex = activeTier;
                            return true;
                        }
                    }
                }
            }

            // Any existing claimable tier.
            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null || row.tiers == null) continue;

                int activeTier = GetFirstUnclaimedTierIndex(row);
                if (activeTier < 0) continue;

                var tier = row.tiers[activeTier];
                if (tier == null) continue;

                if (tier.completed && !tier.claimed)
                {
                    ladderId = row.ladderId;
                    tierIndex = activeTier;
                    return true;
                }
            }

            // Force the tutorial welcome ladder first, if present in today's rows.
            if (tutorialWelcomeLadder != null)
            {
                string tutorialId = tutorialWelcomeLadder.SafeId;

                for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
                {
                    var row = profile.dailyTasks.rows[r];
                    if (row == null || row.ladderId != tutorialId || row.tiers == null || row.tiers.Count == 0)
                        continue;

                    int activeTier = GetFirstUnclaimedTierIndex(row);
                    if (activeTier >= 0)
                    {
                        var tier = row.tiers[activeTier];
                        if (tier == null) continue;

                        tier.completed = true;
                        tier.lastProgress = Mathf.Max(tier.lastProgress, tier.target);
                        tier.completedUtc = DateTimeUtc.Now();
                        tier.lastRewardGranted = 0;

                        ladderId = row.ladderId;
                        tierIndex = activeTier;
                        mgr.Save();
                        return true;
                    }
                }
            }

            // Fall back to the active tier of the first available row.
            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row == null || row.tiers == null || row.tiers.Count == 0) continue;

                int activeTier = GetFirstUnclaimedTierIndex(row);
                if (activeTier < 0) continue;

                var tier = row.tiers[activeTier];
                if (tier == null) continue;

                tier.completed = true;
                tier.lastProgress = Mathf.Max(tier.lastProgress, tier.target);
                tier.completedUtc = DateTimeUtc.Now();
                tier.lastRewardGranted = 0;

                ladderId = row.ladderId;
                tierIndex = activeTier;
                mgr.Save();
                return true;
            }

            return false;
        }

        public int CountClaimedDailyTiers()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr == null || mgr.Profile == null) return 0;

            var profile = mgr.Profile;
            profile.Sanitize();

            if (profile.dailyTasks?.rows == null)
                return 0;

            int count = 0;
            for (int r = 0; r < profile.dailyTasks.rows.Count; r++)
            {
                var row = profile.dailyTasks.rows[r];
                if (row?.tiers == null) continue;

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier != null && tier.claimed)
                        count++;
                }
            }

            return count;
        }
    }
}
