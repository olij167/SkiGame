using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Task Definition", fileName = "Task_")]
    public sealed class TaskDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;
        [Tooltip("Short display name for Watch/Tile UI.")]
        public string shortTitle;

        [Header("Goal")]
        public ProgressionMetric metric;
        public float target = 100f;

        [Header("Reward")]
        [Tooltip("Currency granted when this task is turned in. If 0, reward will be derived automatically.")]
        public int reward = 0;

        [Header("Display")]
        [TextArea] public string description;

        public float ReadCurrent(PlayerStatsProfile profile)
        {
            if (profile == null) return 0f;

            var s = profile.session;
            var l = profile.lifetime;

            return metric switch
            {
                ProgressionMetric.SessionDistanceMeters => s.distanceMeters,
                ProgressionMetric.SessionTopSpeedMps => s.topSpeedMps,
                ProgressionMetric.SessionAirTimeSeconds => s.airTimeSeconds,
                ProgressionMetric.SessionAirDistanceMeters => s.airDistanceMeters,
                ProgressionMetric.SessionVerticalDescentMeters => s.verticalDescentMeters,
                ProgressionMetric.SessionStacks => s.stacks,
                ProgressionMetric.SessionRunsCompleted => s.runsCompleted,
                ProgressionMetric.SessionLiftsUsed => s.liftsUsed,

                ProgressionMetric.LifetimeDistanceMeters => l.totalDistanceMeters,
                ProgressionMetric.LifetimeTopSpeedMps => l.topSpeedMps,
                ProgressionMetric.LifetimeAirTimeSeconds => l.totalAirTimeSeconds,
                ProgressionMetric.LifetimeAirDistanceMeters => l.totalAirDistanceMeters,
                ProgressionMetric.LifetimeVerticalDescentMeters => l.totalVerticalDescentMeters,
                ProgressionMetric.LifetimeStacks => l.totalStacks,
                ProgressionMetric.LifetimeRunsCompleted => l.totalRunsCompleted,
                ProgressionMetric.LifetimeLiftsUsed => l.totalLiftsUsed,

                _ => 0f
            };
        }
    }
}
