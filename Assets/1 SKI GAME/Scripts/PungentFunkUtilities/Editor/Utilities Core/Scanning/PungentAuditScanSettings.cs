namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public enum PungentAuditBackgroundScanMode
    {
        Off,
        IdleOnly
    }

    public static class PungentAuditScanSettings
    {
        private const string PrefPrefix = "PungentFunkUtilities.ProjectAudit.ScanRunner.";
        private const string PrefBackgroundMode = PrefPrefix + "BackgroundMode";
        private const string PrefIdleDelaySeconds = PrefPrefix + "IdleDelaySeconds";
        private const string PrefBackgroundTimeBudgetMs = PrefPrefix + "BackgroundTimeBudgetMs";
        private const string PrefImmediateTimeBudgetMs = PrefPrefix + "ImmediateTimeBudgetMs";
        private const string PrefPauseDuringPlayMode = PrefPrefix + "PauseDuringPlayMode";
        private const string PrefPauseDuringCompilation = PrefPrefix + "PauseDuringCompilation";
        private const string PrefPauseDuringAssetImport = PrefPrefix + "PauseDuringAssetImport";
        private const string PrefRunOnlyIncludedProviders = PrefPrefix + "RunOnlyIncludedProviders";
        private const string PrefAutoRefreshCacheAfterScanCompletion = PrefPrefix + "AutoRefreshCacheAfterScanCompletion";
        private const string DesignAuditProviderEnabledPrefix = "PungentFunkUtilities.DesignAudit.ProviderEnabled.";

        public static PungentAuditBackgroundScanMode BackgroundMode
        {
            get => (PungentAuditBackgroundScanMode)Mathf.Clamp(
                UtilityWindowPrefs.GetInt(PrefBackgroundMode, (int)PungentAuditBackgroundScanMode.Off),
                0,
                1);
            set => UtilityWindowPrefs.SetInt(PrefBackgroundMode, (int)value);
        }

        public static bool BackgroundScanEnabled
        {
            get => BackgroundMode == PungentAuditBackgroundScanMode.IdleOnly;
            set => BackgroundMode = value ? PungentAuditBackgroundScanMode.IdleOnly : PungentAuditBackgroundScanMode.Off;
        }

        public static float IdleDelaySeconds
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefIdleDelaySeconds, 45f), 5f, 600f);
            set => UtilityWindowPrefs.SetFloat(PrefIdleDelaySeconds, Mathf.Clamp(value, 5f, 600f));
        }

        public static int BackgroundTimeBudgetMs
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefBackgroundTimeBudgetMs, 2), 1, 20);
            set => UtilityWindowPrefs.SetInt(PrefBackgroundTimeBudgetMs, Mathf.Clamp(value, 1, 20));
        }

        public static int ImmediateTimeBudgetMs
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefImmediateTimeBudgetMs, 12), 2, 50);
            set => UtilityWindowPrefs.SetInt(PrefImmediateTimeBudgetMs, Mathf.Clamp(value, 2, 50));
        }

        public static bool PauseDuringPlayMode
        {
            get => UtilityWindowPrefs.GetBool(PrefPauseDuringPlayMode, true);
            set => UtilityWindowPrefs.SetBool(PrefPauseDuringPlayMode, value);
        }

        public static bool PauseDuringCompilation
        {
            get => UtilityWindowPrefs.GetBool(PrefPauseDuringCompilation, true);
            set => UtilityWindowPrefs.SetBool(PrefPauseDuringCompilation, value);
        }

        public static bool PauseDuringAssetImportOrUpdate
        {
            get => UtilityWindowPrefs.GetBool(PrefPauseDuringAssetImport, true);
            set => UtilityWindowPrefs.SetBool(PrefPauseDuringAssetImport, value);
        }

        public static bool RunOnlyIncludedProviders
        {
            get => UtilityWindowPrefs.GetBool(PrefRunOnlyIncludedProviders, true);
            set => UtilityWindowPrefs.SetBool(PrefRunOnlyIncludedProviders, value);
        }

        public static bool AutoRefreshCacheAfterScanCompletion
        {
            get => UtilityWindowPrefs.GetBool(PrefAutoRefreshCacheAfterScanCompletion, true);
            set => UtilityWindowPrefs.SetBool(PrefAutoRefreshCacheAfterScanCompletion, value);
        }

        public static bool CanRunBackgroundScanNow(out string reason)
        {
            if (!BackgroundScanEnabled)
            {
                reason = "background scanning is off";
                return false;
            }

            if (PauseDuringPlayMode && (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying))
            {
                reason = "Play Mode is active or changing";
                return false;
            }

            if (PauseDuringCompilation && EditorApplication.isCompiling)
            {
                reason = "scripts are compiling";
                return false;
            }

            if (PauseDuringAssetImportOrUpdate && EditorApplication.isUpdating)
            {
                reason = "assets are importing or updating";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool IsProviderIncludedInDesignAudit(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return false;
            return UtilityWindowPrefs.GetBool(DesignAuditProviderEnabledPrefix + providerId.Trim(), true);
        }
    }

    [InitializeOnLoad]
    public static class PungentAuditIdleScanScheduler
    {
        private static readonly PungentAuditScanRunner SchedulerRunner = new PungentAuditScanRunner();
        private static bool _initialized;
        private static bool _updateSubscribed;
        private static DateTime _lastStaleSignalUtc = DateTime.UtcNow;
        private static DateTime _lastAttemptUtc = DateTime.MinValue;

        static PungentAuditIdleScanScheduler()
        {
            EnsureInitialized();
        }

        public static PungentAuditScanRunner Runner => SchedulerRunner;

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
            SchedulerRunner.Completed += OnSchedulerCompleted;
            SubscribeUpdate();
            _initialized = true;
        }

        public static void Shutdown()
        {
            SchedulerRunner.Dispose();
            if (_updateSubscribed)
            {
                EditorApplication.update -= Tick;
                _updateSubscribed = false;
            }
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
            SchedulerRunner.Completed -= OnSchedulerCompleted;
            _initialized = false;
        }

        public static void MarkProviderStale(string providerId, string reason = null)
        {
            PungentProjectAuditIndex.MarkStale(providerId, reason ?? "Provider cache is stale.");
            _lastStaleSignalUtc = DateTime.UtcNow;
            SubscribeUpdate();
        }

        private static void OnProjectChanged()
        {
            PungentProjectAuditIndex.MarkAllRegisteredStale("Project assets changed; cache may be stale.");
            _lastStaleSignalUtc = DateTime.UtcNow;
            SubscribeUpdate();
        }

        private static void OnHierarchyChanged()
        {
            PungentProjectAuditIndex.MarkAllRegisteredStale("Scene hierarchy changed; cache may be stale.");
            _lastStaleSignalUtc = DateTime.UtcNow;
            SubscribeUpdate();
        }

        private static void SubscribeUpdate()
        {
            if (_updateSubscribed)
                return;
            EditorApplication.update += Tick;
            _updateSubscribed = true;
        }

        private static void Tick()
        {
            if (!PungentAuditScanSettings.BackgroundScanEnabled)
                return;

            if (SchedulerRunner.IsActive)
                return;

            if (!PungentAuditScanSettings.CanRunBackgroundScanNow(out _))
                return;

            if ((DateTime.UtcNow - _lastStaleSignalUtc).TotalSeconds < PungentAuditScanSettings.IdleDelaySeconds)
                return;

            if ((DateTime.UtcNow - _lastAttemptUtc).TotalSeconds < Mathf.Max(5f, PungentAuditScanSettings.IdleDelaySeconds))
                return;

            IReadOnlyList<IPungentAuditScanProvider> providers = PungentAuditScanProviderRegistry.Providers;
            if (providers == null || providers.Count == 0)
                return;

            List<PungentAuditScanJob> jobs = new List<PungentAuditScanJob>();
            for (int i = 0; i < providers.Count; i++)
            {
                IPungentAuditScanProvider provider = providers[i];
                if (provider == null || !provider.CanRunBackground)
                    continue;
                if (PungentAuditScanSettings.RunOnlyIncludedProviders && !PungentAuditScanSettings.IsProviderIncludedInDesignAudit(provider.ProviderId))
                    continue;

                PungentProjectAuditIndexEntry entry = PungentProjectAuditIndex.Get(provider.ProviderId);
                if (entry != null && !entry.Stale && entry.HasResult)
                    continue;

                if (provider.TryGetNotConfiguredReason(out _))
                    continue;

                PungentAuditScanJob job = provider.CreateJob(PungentAuditScanMode.BackgroundIdle);
                if (job == null || job.IsTerminal)
                    continue;
                jobs.Add(job);
            }

            _lastAttemptUtc = DateTime.UtcNow;
            if (jobs.Count > 0)
                SchedulerRunner.StartBatch(jobs, PungentAuditScanMode.BackgroundIdle);
        }

        private static void OnSchedulerCompleted()
        {
            _lastAttemptUtc = DateTime.UtcNow;
        }
    }
#endif
}
