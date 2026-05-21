using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using UnityEditor;
    using UnityEngine;

    public static class PungentSceneGizmoPerformancePolicy
    {
        public const int InfoActiveSources = 25;
        public const int WarningActiveSources = 75;
        public const int InfoEnabledRules = 100;
        public const int WarningEnabledRules = 300;
        public const int WarningProviderComponents = 75;
        public const int WarningTrajectorySamples = 1500;
        public const int WarningActiveLabels = 150;
        public const int DefaultProviderDrawBudget = 2000;
        public const int DefaultMaxTrajectorySamplesPerComponent = 128;
        public const int MaxCollisionContactsPerSensor = 16;
        public const int MaxTriggerOverlapsPerSensor = 32;
        public const int MaxPingAnimations = 64;
        public const int HighCostRecordDrawOps = 64;

        private static int _labelFrame = -1;
        private static int _labelsConsumed;
        private static string _lastWarningBucket = string.Empty;
        private static double _nextConsoleWarningTime;

        public static string BuildBrowserWarningText(int activeCount, int drawOps)
        {
            return $"Having {activeCount} active PungentFunk gizmo sources/providers and approximately {drawOps} draw operations in this scene could impact editor performance. Consider disabling, hiding, filtering, or removing some gizmos if the Scene View becomes sluggish.";
        }

        public static bool ShouldShowWarning(
            int activeSources,
            int enabledRules,
            int providerComponents,
            int trajectorySamples,
            int labels,
            int drawOps)
        {
            return activeSources >= WarningActiveSources ||
                   enabledRules >= WarningEnabledRules ||
                   providerComponents >= WarningProviderComponents ||
                   trajectorySamples >= WarningTrajectorySamples ||
                   labels >= WarningActiveLabels ||
                   drawOps >= DefaultProviderDrawBudget;
        }

        public static bool ShouldShowInfo(int activeSources, int enabledRules)
        {
            return activeSources >= InfoActiveSources || enabledRules >= InfoEnabledRules;
        }

        public static bool TryConsumeLabel(bool selected)
        {
            int frame = Time.frameCount;
            if (frame != _labelFrame)
            {
                _labelFrame = frame;
                _labelsConsumed = 0;
            }

            int cap = selected ? WarningActiveLabels + 24 : WarningActiveLabels;
            if (_labelsConsumed >= cap)
                return false;

            _labelsConsumed++;
            return true;
        }

        public static void MaybeLogPerformanceWarning(
            int activeCount,
            int drawOps,
            bool browserOrAuditShowing)
        {
            if (browserOrAuditShowing)
                return;
            if (activeCount < WarningActiveSources && drawOps < DefaultProviderDrawBudget)
                return;

            string bucket = activeCount / 25 + ":" + drawOps / 500;
            double now = EditorApplication.timeSinceStartup;
            if (string.Equals(bucket, _lastWarningBucket, StringComparison.Ordinal) && now < _nextConsoleWarningTime)
                return;

            _lastWarningBucket = bucket;
            _nextConsoleWarningTime = now + 60d;
            Debug.LogWarning("[PungentFunk Utilities] " + BuildBrowserWarningText(activeCount, drawOps));
        }

        public static bool IsHighCost(int estimatedDrawOperations, int estimatedTrajectorySamples)
        {
            return estimatedDrawOperations >= HighCostRecordDrawOps ||
                   estimatedTrajectorySamples >= DefaultMaxTrajectorySamplesPerComponent;
        }
    }
#endif
}
