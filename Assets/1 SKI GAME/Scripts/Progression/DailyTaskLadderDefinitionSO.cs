using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Daily Task Ladder", fileName = "DailyTaskLadder")]
    public sealed class DailyTaskLadderDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique stable ID for persistence. If empty, name will be used (not recommended).")]
        public string id = "daily_ladder_id";

        [Header("Metric")]
        public ProgressionMetric metric;

        public enum EvaluationMode
        {
            /// <summary>
            /// Progress = (currentMetric - baselineAtDayStart), clamped to >= 0.
            /// Use for cumulative/monotonic metrics like distance, grind time, etc.
            /// </summary>
            DeltaFromBaseline = 0,

            /// <summary>
            /// Progress = best observed value since day start (max).
            /// Use for "top speed today" style metrics.
            /// </summary>
            MaxSinceDayStart = 1
        }

        [Tooltip("How this ladder evaluates progress for the day.")]
        public EvaluationMode evaluationMode = EvaluationMode.DeltaFromBaseline;

        [Header("Tier Targets (Columns)")]
        [Tooltip("Targets for each column, in ascending difficulty. Eg: 100, 500, 1000, 5000, 10000")]
        public List<float> tierTargets = new List<float> { 100f, 500f, 1000f, 5000f, 10000f };

        [Header("Rewards (Optional)")]
        [Tooltip("If empty or mismatched length, rewards will be derived.")]
        public List<int> tierRewards = new List<int>();

        [Tooltip("Bonus reward granted once when ALL tiers in this row are completed and claimed.")]
        public int rowBonusReward = 0;

        [Header("UI (Optional)")]
        [Tooltip("Optional override label for UI. If empty, UI can derive from metric.")]
        public string displayNameOverride = "";

        [Tooltip("Optional unit suffix override (e.g., 'm', 'km', 'm/s'). If empty, UI derives.")]
        public string unitOverride = "";

        public string SafeId => string.IsNullOrEmpty(id) ? name : id;

        public bool TryGetTierReward(int tierIndex, out int reward)
        {
            reward = 0;
            if (tierRewards == null) return false;
            if (tierIndex < 0 || tierIndex >= tierRewards.Count) return false;
            reward = Mathf.Max(0, tierRewards[tierIndex]);
            return true;
        }

        public float GetTierTarget(int tierIndex)
        {
            if (tierTargets == null || tierTargets.Count == 0) return 0f;
            tierIndex = Mathf.Clamp(tierIndex, 0, tierTargets.Count - 1);
            return Mathf.Max(0.0001f, tierTargets[tierIndex]);
        }
    }
}
