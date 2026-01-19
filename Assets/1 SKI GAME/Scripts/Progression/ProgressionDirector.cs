using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class ProgressionDirector : MonoBehaviour
    {
        public static ProgressionDirector Instance { get; private set; }

        [Header("Definitions")]
        [SerializeField] private ProgressionCatalogSO catalog;

        [Header("Session Tasks")]
        [SerializeField, Min(0)] private int sessionTaskCount = 2;

        [Tooltip("How often to evaluate tasks/achievements. Keeps it cheap and deterministic.")]
        [SerializeField, Range(0.05f, 2f)] private float evaluateIntervalSeconds = 0.25f;

        public event Action<TaskDefinitionSO> OnTaskCompleted;
        public event Action<AchievementDefinitionSO> OnAchievementUnlocked;

        private float _nextEvalTime;

        private readonly Dictionary<string, TaskDefinitionSO> _taskById = new();
        private readonly Dictionary<string, AchievementDefinitionSO> _achById = new();

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
            // Ensure tasks exist for this runtime session.
            var mgr = PlayerStatsManager.Instance;
            if (mgr != null && mgr.Profile != null)
            {
                EnsureSessionTasks(mgr.Profile);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextEvalTime) return;
            _nextEvalTime = Time.unscaledTime + evaluateIntervalSeconds;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || catalog == null) return;

            // Keep the session task list healthy (fills missing slots, trims extras, removes invalid defs).
            // This is cheap because sessionTaskCount is small.
            EnsureSessionTasks(profile);


            EvaluateTasks(profile, mgr);
            EvaluateAchievements(profile, mgr);
        }

        private void BuildLookups()
        {
            _taskById.Clear();
            _achById.Clear();

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
        }

        private void EnsureSessionTasks(PlayerStatsProfile profile)
        {
            if (profile == null) return;
            profile.Sanitize();

            profile.activeSessionTasks ??= new System.Collections.Generic.List<PlayerStatsProfile.ActiveTaskState>();

            // Remove tasks that no longer exist in the catalog
            for (int i = profile.activeSessionTasks.Count - 1; i >= 0; i--)
            {
                var s = profile.activeSessionTasks[i];
                if (s == null || string.IsNullOrEmpty(s.taskId) || !TryGetTaskDefinition(s.taskId, out _))
                    profile.activeSessionTasks.RemoveAt(i);
            }

            // Trim if too many
            while (profile.activeSessionTasks.Count > sessionTaskCount)
                profile.activeSessionTasks.RemoveAt(profile.activeSessionTasks.Count - 1);

            // Fill missing slots
            while (profile.activeSessionTasks.Count < sessionTaskCount)
            {
                var def = PickNewTaskDefinition(profile);
                if (def == null) break;
                float baseline = def.ReadCurrent(profile);

                profile.activeSessionTasks.Add(new PlayerStatsProfile.ActiveTaskState
                {
                    taskId = def.id,
                    completed = false,
                    claimed = false,
                    completedUtc = default,

                    // task progress starts at 0 (because baseline == current at activation)
                    lastProgress = 0f,
                    target = Mathf.Max(0.0001f, def.target),

                    baselineValue = baseline,
                    baselineCaptured = true,

                    lastRewardGranted = 0
                });

            }

            // Ensure cached targets are populated (in case tasks were created before we cached target)
            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var s = profile.activeSessionTasks[i];
                if (s == null) continue;

                if (TryGetTaskDefinition(s.taskId, out var def))
                {
                    if (s.target <= 0.0001f)
                        s.target = Mathf.Max(0.0001f, def.target);
                }
            }
            // Ensure baselines are captured (handles older saves created before baseline fields existed).
            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var s = profile.activeSessionTasks[i];
                if (s == null) continue;

                if (!TryGetTaskDefinition(s.taskId, out var def) || def == null)
                    continue;

                if (!s.baselineCaptured)
                {
                    s.baselineValue = def.ReadCurrent(profile);
                    s.baselineCaptured = true;
                    s.lastProgress = 0f; // treat progress prior to activation as 0
                }
            }

        }

        private void EvaluateTasks(PlayerStatsProfile profile, PlayerStatsManager mgr)
        {
            if (profile.activeSessionTasks == null) return;

            bool anyCompleted = false;

            for (int i = 0; i < profile.activeSessionTasks.Count; i++)
            {
                var state = profile.activeSessionTasks[i];
                if (state == null) continue;
                if (state.completed) continue;

                if (!_taskById.TryGetValue(state.taskId, out var def) || def == null)
                    continue;

                float absolute = def.ReadCurrent(profile);

                // Safety: if somehow not captured yet, capture now.
                if (!state.baselineCaptured)
                {
                    state.baselineValue = absolute;
                    state.baselineCaptured = true;
                }

                // Progress is how much MORE the player has done since this task became active.
                float progress = absolute - state.baselineValue;
                if (progress < 0f) progress = 0f; // handles metric resets gracefully

                state.lastProgress = progress;
                state.target = Mathf.Max(0.0001f, def.target);

                if (progress + 0.0001f >= state.target)
                {
                    state.completed = true;
                    state.completedUtc = DateTimeUtc.Now();
                    anyCompleted = true;

                    OnTaskCompleted?.Invoke(def);
                }

            }

            // Persist completion promptly so completed tasks survive a crash/quit even before "Turn In".
            if (anyCompleted && mgr != null)
                mgr.Save();
        }

        private void EvaluateAchievements(PlayerStatsProfile profile, PlayerStatsManager mgr)
        {
            if (catalog.achievements == null) return;

            for (int i = 0; i < catalog.achievements.Count; i++)
            {
                var def = catalog.achievements[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;

                if (profile.HasAchievement(def.id))
                    continue;

                float current = def.ReadCurrent(profile);
                if (current + 0.0001f >= def.target)
                {
                    if (profile.TryAddAchievement(def.id))
                    {
                        OnAchievementUnlocked?.Invoke(def);

                        // Persist achievement unlock promptly.
                        if (mgr != null) mgr.Save();
                    }
                }
            }
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

        // ---------- UI helpers (formatted for your current Watch/Phone labels) ----------
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

        /// <summary>
        /// Copies current session tasks into a UI-friendly list (title, current, target, pct, completed).
        /// Does not allocate if caller reuses the provided buffer.
        /// </summary>
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
                // Display progress since activation (do not mutate baseline here).
                float absolute = def.ReadCurrent(profile);
                float cur = st.baselineCaptured ? Mathf.Max(0f, absolute - st.baselineValue) : 0f;
                float tgt = (st.target > 0.0001f) ? st.target : Mathf.Max(0.0001f, def.target);

                int rw = ComputeTaskReward(def);
                buffer.Add(new TaskDisplay(def.id, name, st.completed, st.claimed, cur, tgt, rw));

            }

            // Order:
            // 1) completed & not claimed (actionable)
            // 2) in progress
            // 3) claimed (done)
            buffer.Sort((a, b) =>
            {
                int Rank(TaskDisplay d)
                {
                    if (d.completed && !d.claimed) return 0;
                    if (!d.completed) return 1;
                    return 2; // claimed
                }

                int ra = Rank(a);
                int rb = Rank(b);
                int cmp = ra.CompareTo(rb);
                if (cmp != 0) return cmp;

                // Within same rank, higher progress first.
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

            // Pick “closest” locked achievements by percent progress.
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

            // Build a small exclusion set: currently active + recent
            // (Lists are fine here; sessionTaskCount is tiny.)
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

            // Collect which metrics are already represented in active tasks, so we can diversify.
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

            // Pass 1: not active, not recent, and metric not already used
            var candidates = new System.Collections.Generic.List<TaskDefinitionSO>(catalog.tasks.Count);
            for (int i = 0; i < catalog.tasks.Count; i++)
            {
                var t = catalog.tasks[i];
                if (t == null || string.IsNullOrEmpty(t.id)) continue;
                if (IsActive(t.id)) continue;
                if (IsRecent(t.id)) continue;
                if (MetricAlreadyUsed(t.metric)) continue;
                candidates.Add(t);
            }

            // Pass 2: not active, not recent (allow metric duplicates)
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

            // Pass 3: not active (allow repeats if the catalog is small)
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

            // Random pick
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[idx];
        }

        private int ComputeTaskReward(TaskDefinitionSO def)
        {
            if (def == null) return 0;

            // Prefer explicit tuning
            if (def.reward > 0) return def.reward;

            // Otherwise derive a simple reward based on target magnitude & metric type.
            // Keep it conservative and predictable.
            float t = Mathf.Max(1f, def.target);
            int baseReward = 10;

            // Slight scaling
            int scaled = baseReward + Mathf.Clamp(Mathf.RoundToInt(t / 100f) * 5, 0, 40);

            // Small bonus for “harder to do” metrics
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
                    // “Stacks” tasks are generally undesirable, reward less
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

            // Apply reward
            profile.currency += rewardGranted;

            // Mark claimed
            state.claimed = true;
            state.lastRewardGranted = rewardGranted;

            // Remember to avoid repetition
            profile.RememberRecentTask(taskId, max: 10);

            // Replace task immediately (cycling)
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

        /// <summary>
        /// Copies all achievement definitions from the catalog into the provided buffer.
        /// Intended for UI (avoids exposing the serialized list directly).
        /// Caller should reuse the buffer to avoid allocations.
        /// </summary>
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

    }
}
