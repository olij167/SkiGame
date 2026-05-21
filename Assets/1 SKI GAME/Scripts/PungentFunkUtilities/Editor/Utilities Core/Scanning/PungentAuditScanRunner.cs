namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;

    public enum PungentAuditScanMode
    {
        Immediate,
        BackgroundIdle,
        BackgroundAfterChanges,
        Scheduled,
        FullProject
    }

    public enum PungentAuditBatchState
    {
        Idle,
        Queued,
        Running,
        Paused,
        Cancelling,
        Cancelled,
        Complete,
        Failed,
        CompleteWithIssues
    }

    public enum PungentAuditScanJobState
    {
        Idle,
        Queued,
        Running,
        Paused,
        Cancelling,
        Cancelled,
        Complete,
        Failed,
        NotConfigured,
        Skipped
    }

    public enum PungentAuditScanStepDisposition
    {
        Continue,
        Complete,
        Failed,
        Cancelled,
        NotConfigured,
        Skipped
    }

    [Flags]
    public enum PungentAuditScanJobCapabilities
    {
        None = 0,
        Cooperative = 1 << 0,
        Monolithic = 1 << 1,
        BackgroundSafe = 1 << 2,
        ImmediateOnly = 1 << 3,
        RequiresSceneOpening = 1 << 4,
        UsesAssetDatabase = 1 << 5,
        UsesModalProgress = 1 << 6,
        ScanOnly = 1 << 7,
        CacheOnlyOrManualOnly = 1 << 8
    }

    public readonly struct PungentAuditScanStepResult
    {
        public readonly PungentAuditScanStepDisposition disposition;
        public readonly string message;
        public readonly PungentScanResult result;

        public PungentAuditScanStepResult(PungentAuditScanStepDisposition disposition, string message = null, PungentScanResult result = null)
        {
            this.disposition = disposition;
            this.message = message;
            this.result = result;
        }

        public static PungentAuditScanStepResult Continue(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.Continue, message, result);
        }

        public static PungentAuditScanStepResult Complete(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.Complete, message, result);
        }

        public static PungentAuditScanStepResult Failed(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.Failed, message, result);
        }

        public static PungentAuditScanStepResult Cancelled(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.Cancelled, message, result);
        }

        public static PungentAuditScanStepResult NotConfigured(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.NotConfigured, message, result);
        }

        public static PungentAuditScanStepResult Skipped(string message = null, PungentScanResult result = null)
        {
            return new PungentAuditScanStepResult(PungentAuditScanStepDisposition.Skipped, message, result);
        }
    }

    public sealed class PungentAuditScanProgress
    {
        public string providerId;
        public string label;
        public float progress01;
        public int processed;
        public int total;
        public string currentStepLabel;
        public string message;
        public PungentScanSeverity severity;
        public bool isIndeterminate = true;
    }

    public sealed class PungentAuditScanContext
    {
        private readonly PungentAuditScanJob _job;

        internal PungentAuditScanContext(PungentAuditScanJob job, PungentAuditScanMode mode, int timeBudgetMs)
        {
            _job = job;
            StartedUtc = DateTime.UtcNow;
            Update(mode, timeBudgetMs);
        }

        public string ProviderId => _job != null ? _job.providerId : string.Empty;
        public string DisplayName => _job != null ? _job.displayName : string.Empty;
        public PungentAuditScanMode Mode { get; private set; }
        public bool CancelRequested => _job != null && _job.cancelRequested;
        public bool PauseRequested => _job != null && _job.pauseRequested;
        public bool CanOpenScenes { get; private set; }
        public bool CanUseModalProgress { get; private set; }
        public bool CanMutateAssets { get; private set; }
        public bool CanMutateScenes { get; private set; }
        public bool AllowExpensiveScriptReferenceRefresh { get; private set; }
        public int TimeBudgetMs { get; private set; }
        public DateTime StartedUtc { get; private set; }

        public void Report(float progress01, int processed, int total, string message, bool indeterminate = false, string stepLabel = null)
        {
            _job?.Report(progress01, processed, total, message, indeterminate, stepLabel);
        }

        public bool IsCancellationRequested()
        {
            return CancelRequested;
        }

        public bool IsPauseRequested()
        {
            return PauseRequested;
        }

        internal void Update(PungentAuditScanMode mode, int timeBudgetMs)
        {
            Mode = mode;
            TimeBudgetMs = Math.Max(1, timeBudgetMs);
            CanOpenScenes = mode == PungentAuditScanMode.Immediate;
            CanUseModalProgress = false;
            CanMutateAssets = false;
            CanMutateScenes = false;
            AllowExpensiveScriptReferenceRefresh = false;
        }
    }

    public sealed class PungentAuditScanJob
    {
        private readonly Func<PungentAuditScanJob, PungentAuditScanStepResult> _step;
        private Func<PungentAuditScanContext, PungentAuditScanStepResult> _contextStep;
        private PungentAuditScanContext _context;

        public PungentAuditScanJob(string providerId, string displayName, Func<PungentAuditScanJob, bool> run)
            : this(providerId, displayName, WrapMonolithicRun(run))
        {
        }

        public PungentAuditScanJob(string providerId, string displayName, Func<PungentAuditScanJob, PungentAuditScanStepResult> step)
        {
            this.providerId = string.IsNullOrWhiteSpace(providerId) ? "unknown-provider" : providerId.Trim();
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? this.providerId : displayName.Trim();
            _step = step;
            state = PungentAuditScanJobState.Queued;
            progress.providerId = this.providerId;
            progress.label = this.displayName;
            progress.currentStepLabel = "Queued";
            progress.message = "Queued";
        }

        public static PungentAuditScanJob CreateCooperative(string providerId, string displayName, Func<PungentAuditScanContext, PungentAuditScanStepResult> step)
        {
            PungentAuditScanJob job = new PungentAuditScanJob(providerId, displayName, (Func<PungentAuditScanJob, PungentAuditScanStepResult>)null);
            job._contextStep = step;
            job.capabilities &= ~PungentAuditScanJobCapabilities.Monolithic;
            job.capabilities |= PungentAuditScanJobCapabilities.Cooperative | PungentAuditScanJobCapabilities.ScanOnly;
            return job;
        }

        public readonly string providerId;
        public readonly string displayName;
        public PungentAuditScanMode scanMode = PungentAuditScanMode.Immediate;
        public PungentAuditScanJobState state;
        public readonly PungentAuditScanProgress progress = new PungentAuditScanProgress();
        public string statusMessage = "Queued";
        public string error = string.Empty;
        public string errorMessage = string.Empty;
        public bool canPause = true;
        public bool canCancel = true;
        public bool pauseRequested;
        public bool cancelRequested;
        public long startedTicks;
        public long completedTicks;
        public PungentScanResult result;
        public PungentAuditScanJobCapabilities capabilities = PungentAuditScanJobCapabilities.Monolithic | PungentAuditScanJobCapabilities.ScanOnly;

        public float progress01 => progress.progress01;
        public string currentStepLabel => progress.currentStepLabel;
        public int processedCount => progress.processed;
        public int totalCount => progress.total;
        public bool isCooperative => (capabilities & PungentAuditScanJobCapabilities.Cooperative) != 0;
        public bool isMonolithic => (capabilities & PungentAuditScanJobCapabilities.Monolithic) != 0;
        public bool backgroundSafe => (capabilities & PungentAuditScanJobCapabilities.BackgroundSafe) != 0;
        public bool immediateOnly => (capabilities & PungentAuditScanJobCapabilities.ImmediateOnly) != 0;
        public bool requiresSceneOpening => (capabilities & PungentAuditScanJobCapabilities.RequiresSceneOpening) != 0;
        public bool usesModalProgress => (capabilities & PungentAuditScanJobCapabilities.UsesModalProgress) != 0;
        public bool scanOnly => (capabilities & PungentAuditScanJobCapabilities.ScanOnly) != 0;
        public bool cacheOnlyOrManualOnly => (capabilities & PungentAuditScanJobCapabilities.CacheOnlyOrManualOnly) != 0;

        public bool IsTerminal =>
            state == PungentAuditScanJobState.Cancelled ||
            state == PungentAuditScanJobState.Complete ||
            state == PungentAuditScanJobState.Failed ||
            state == PungentAuditScanJobState.NotConfigured ||
            state == PungentAuditScanJobState.Skipped;

        public void Report(float progress01, int processed, int total, string message, bool indeterminate = false, string stepLabel = null)
        {
            progress.providerId = providerId;
            progress.label = displayName;
            progress.progress01 = (float)Math.Max(0d, Math.Min(1d, progress01));
            progress.processed = Math.Max(0, processed);
            progress.total = Math.Max(0, total);
            progress.currentStepLabel = string.IsNullOrWhiteSpace(stepLabel) ? progress.currentStepLabel : stepLabel.Trim();
            progress.message = string.IsNullOrWhiteSpace(message) ? statusMessage : message.Trim();
            progress.isIndeterminate = indeterminate;
            statusMessage = progress.message;
        }

        public void AttachResult(PungentScanResult scanResult)
        {
            result = scanResult;
        }

        internal bool ExecuteStep(PungentAuditScanMode mode)
        {
            scanMode = mode;

            if (cancelRequested && state != PungentAuditScanJobState.Running && state != PungentAuditScanJobState.Cancelling)
            {
                MarkCancelled("Interrupted before " + displayName + " started.");
                return true;
            }

            if (pauseRequested && state != PungentAuditScanJobState.Running)
            {
                MarkPaused();
                return false;
            }

            if (state == PungentAuditScanJobState.Queued || state == PungentAuditScanJobState.Idle)
            {
                state = PungentAuditScanJobState.Running;
                if (startedTicks == 0)
                    startedTicks = DateTime.UtcNow.Ticks;
                Report(progress.progress01, progress.processed, Math.Max(1, progress.total), "Running " + displayName + "...", true, "Starting");
            }

            try
            {
                PungentAuditScanStepResult stepResult;
                if (_contextStep != null)
                {
                    int budgetMs = mode == PungentAuditScanMode.BackgroundIdle
                        ? PungentAuditScanSettings.BackgroundTimeBudgetMs
                        : PungentAuditScanSettings.ImmediateTimeBudgetMs;
                    if (_context == null)
                        _context = new PungentAuditScanContext(this, mode, budgetMs);
                    else
                        _context.Update(mode, budgetMs);
                    stepResult = _contextStep(_context);
                }
                else
                {
                    stepResult = _step != null
                        ? _step(this)
                        : PungentAuditScanStepResult.Failed("No scan delegate was configured.");
                }

                if (stepResult.result != null)
                    result = stepResult.result;
                if (!string.IsNullOrWhiteSpace(stepResult.message))
                    statusMessage = stepResult.message.Trim();

                switch (stepResult.disposition)
                {
                    case PungentAuditScanStepDisposition.Continue:
                        if (!cancelRequested && !pauseRequested)
                            state = PungentAuditScanJobState.Running;
                        if (!string.IsNullOrWhiteSpace(statusMessage))
                            Report(progress.progress01, progress.processed, Math.Max(progress.total, progress.processed), statusMessage, progress.isIndeterminate);
                        return false;
                    case PungentAuditScanStepDisposition.NotConfigured:
                        MarkTerminal(PungentAuditScanJobState.NotConfigured, string.IsNullOrWhiteSpace(statusMessage) ? displayName + " is not configured." : statusMessage);
                        return true;
                    case PungentAuditScanStepDisposition.Skipped:
                        MarkTerminal(PungentAuditScanJobState.Skipped, string.IsNullOrWhiteSpace(statusMessage) ? displayName + " skipped." : statusMessage);
                        return true;
                    case PungentAuditScanStepDisposition.Failed:
                        MarkFailed(string.IsNullOrWhiteSpace(statusMessage) ? displayName + " failed." : statusMessage);
                        return true;
                    case PungentAuditScanStepDisposition.Cancelled:
                        MarkCancelled(string.IsNullOrWhiteSpace(statusMessage) ? displayName + " interrupted." : statusMessage);
                        return true;
                    default:
                        MarkTerminal(PungentAuditScanJobState.Complete, string.IsNullOrWhiteSpace(statusMessage) ? displayName + " complete." : statusMessage);
                        return true;
                }
            }
            catch (Exception ex)
            {
                MarkFailed(displayName + " failed: " + ex.Message);
                UnityEngine.Debug.LogException(ex);
                return true;
            }
        }

        internal void MarkCancelling(string message = null)
        {
            if (IsTerminal)
                return;
            state = PungentAuditScanJobState.Cancelling;
            cancelRequested = true;
            statusMessage = string.IsNullOrWhiteSpace(message) ? "Interruption requested." : message.Trim();
            Report(progress.progress01, progress.processed, progress.total, statusMessage, progress.isIndeterminate, "Interrupting");
        }

        internal void MarkCancelled(string message)
        {
            MarkTerminal(PungentAuditScanJobState.Cancelled, string.IsNullOrWhiteSpace(message) ? "Interrupted" : message);
        }

        internal void MarkSkipped(string message)
        {
            MarkTerminal(PungentAuditScanJobState.Skipped, string.IsNullOrWhiteSpace(message) ? "Skipped" : message);
        }

        internal void MarkNotConfigured(string message)
        {
            MarkTerminal(PungentAuditScanJobState.NotConfigured, string.IsNullOrWhiteSpace(message) ? "Not configured" : message);
        }

        internal void MarkFailed(string message)
        {
            error = string.IsNullOrWhiteSpace(message) ? "Scan failed." : message.Trim();
            errorMessage = error;
            MarkTerminal(PungentAuditScanJobState.Failed, error);
        }

        private void MarkPaused()
        {
            if (IsTerminal)
                return;
            state = PungentAuditScanJobState.Paused;
            statusMessage = "Paused";
            Report(progress.progress01, progress.processed, progress.total, statusMessage, progress.isIndeterminate, "Paused");
        }

        private void MarkTerminal(PungentAuditScanJobState terminalState, string message)
        {
            state = terminalState;
            completedTicks = DateTime.UtcNow.Ticks;
            statusMessage = string.IsNullOrWhiteSpace(message) ? terminalState.ToString() : message.Trim();
            bool completeLike = terminalState == PungentAuditScanJobState.Complete ||
                                terminalState == PungentAuditScanJobState.NotConfigured ||
                                terminalState == PungentAuditScanJobState.Skipped ||
                                terminalState == PungentAuditScanJobState.Failed;
            Report(completeLike ? 1f : progress.progress01, progress.processed, progress.total, statusMessage, terminalState == PungentAuditScanJobState.Cancelled && progress.isIndeterminate, terminalState.ToString());
        }

        private static Func<PungentAuditScanJob, PungentAuditScanStepResult> WrapMonolithicRun(Func<PungentAuditScanJob, bool> run)
        {
            return job =>
            {
                bool success = run != null && run(job);
                return success
                    ? PungentAuditScanStepResult.Complete(job.statusMessage, job.result)
                    : PungentAuditScanStepResult.Failed(job.statusMessage, job.result);
            };
        }
    }

    public interface IPungentAuditScanProvider
    {
        string ProviderId { get; }
        string DisplayName { get; }
        string Description { get; }
        string OpenButtonLabel { get; }
        string RunButtonLabel { get; }
        bool CanRunImmediate { get; }
        bool CanRunBackground { get; }
        bool CanRunFromCoordinator { get; }
        bool CanPause { get; }
        bool CanCancel { get; }
        bool UsesSceneOpening { get; }
        bool UsesAssetDatabase { get; }
        bool UsesModalProgress { get; }
        bool IsCooperative { get; }
        bool IsMonolithic { get; }
        bool IsScanOnly { get; }
        bool TryGetNotConfiguredReason(out string reason);
        PungentAuditScanJob CreateJob(PungentAuditScanMode mode);
        void OpenWindow();
    }

    public static class PungentAuditScanProviderRegistry
    {
        private static readonly Dictionary<string, IPungentAuditScanProvider> ProvidersById = new Dictionary<string, IPungentAuditScanProvider>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IPungentAuditScanProvider> ProvidersInOrder = new List<IPungentAuditScanProvider>();

        public static IReadOnlyList<IPungentAuditScanProvider> Providers => ProvidersInOrder;

        public static void Clear()
        {
            ProvidersById.Clear();
            ProvidersInOrder.Clear();
        }

        public static void Register(IPungentAuditScanProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            string id = provider.ProviderId.Trim();
            if (ProvidersById.ContainsKey(id))
            {
                ProvidersById[id] = provider;
                for (int i = 0; i < ProvidersInOrder.Count; i++)
                {
                    if (string.Equals(ProvidersInOrder[i].ProviderId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        ProvidersInOrder[i] = provider;
                        break;
                    }
                }
            }
            else
            {
                ProvidersById.Add(id, provider);
                ProvidersInOrder.Add(provider);
            }

            PungentProjectAuditIndex.RegisterProvider(provider.ProviderId, provider.DisplayName);
        }

        public static bool TryGet(string providerId, out IPungentAuditScanProvider provider)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                provider = null;
                return false;
            }

            return ProvidersById.TryGetValue(providerId.Trim(), out provider);
        }
    }

    public sealed class PungentProjectAuditIndexEntry
    {
        public string ProviderId;
        public string DisplayName;
        public long LastCompletedUtcTicks;
        public bool Stale = true;
        public string LastStatus = "No cache";
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public int SuccessCount;
        public bool HasResult;
        public string LastScanMode = "Unknown";
        public double LastDurationSeconds;
    }

    public static class PungentProjectAuditIndex
    {
        private const string PrefPrefix = "PungentFunkUtilities.ProjectAudit.Index.";
        private static readonly Dictionary<string, PungentProjectAuditIndexEntry> Entries = new Dictionary<string, PungentProjectAuditIndexEntry>(StringComparer.OrdinalIgnoreCase);

        public static void RegisterProvider(string providerId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return;

            PungentProjectAuditIndexEntry entry = Get(providerId);
            entry.ProviderId = providerId.Trim();
            entry.DisplayName = string.IsNullOrWhiteSpace(displayName) ? entry.ProviderId : displayName.Trim();
            Save(entry);
        }

        public static PungentProjectAuditIndexEntry Get(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                providerId = "unknown-provider";
            providerId = providerId.Trim();

            if (Entries.TryGetValue(providerId, out PungentProjectAuditIndexEntry cached))
                return cached;

            PungentProjectAuditIndexEntry entry = Load(providerId);
            Entries[providerId] = entry;
            return entry;
        }

        public static void RecordResult(PungentScanResult result, PungentAuditScanMode? mode = null)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.ToolId))
                return;

            PungentProjectAuditIndexEntry entry = Get(result.ToolId);
            entry.ProviderId = result.ToolId;
            entry.DisplayName = result.DisplayName;
            entry.LastCompletedUtcTicks = result.CompletedAtUtc.Ticks;
            entry.Stale = false;
            entry.LastStatus = string.IsNullOrWhiteSpace(result.StatusMessage) ? "Scan complete" : result.StatusMessage;
            entry.ErrorCount = result.Summary.ErrorCount;
            entry.WarningCount = result.Summary.WarningCount;
            entry.InfoCount = result.Summary.InfoCount;
            entry.SuccessCount = result.Summary.SuccessCount;
            entry.HasResult = true;
            entry.LastDurationSeconds = result.DurationSeconds;
            if (mode.HasValue)
                entry.LastScanMode = mode.Value.ToString();
            Save(entry);
        }

        public static void RecordJob(PungentAuditScanJob job, PungentAuditScanMode mode)
        {
            if (job == null)
                return;

            if (job.result != null && job.state == PungentAuditScanJobState.Complete)
            {
                RecordResult(job.result, mode);
                return;
            }

            PungentProjectAuditIndexEntry entry = Get(job.providerId);
            entry.ProviderId = job.providerId;
            entry.DisplayName = job.displayName;
            entry.LastScanMode = mode.ToString();
            entry.LastStatus = string.IsNullOrWhiteSpace(job.statusMessage) ? job.state.ToString() : job.statusMessage;
            entry.LastCompletedUtcTicks = job.completedTicks != 0 ? job.completedTicks : DateTime.UtcNow.Ticks;
            entry.Stale = job.state != PungentAuditScanJobState.NotConfigured && job.state != PungentAuditScanJobState.Skipped;
            if (job.state == PungentAuditScanJobState.Cancelled || job.state == PungentAuditScanJobState.Failed)
                entry.Stale = true;
            Save(entry);
        }

        public static void MarkStale(string providerId, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return;

            PungentProjectAuditIndexEntry entry = Get(providerId);
            entry.Stale = true;
            if (!string.IsNullOrWhiteSpace(reason))
                entry.LastStatus = reason.Trim();
            Save(entry);
        }

        public static void MarkAllRegisteredStale(string reason = null)
        {
            IReadOnlyList<IPungentAuditScanProvider> providers = PungentAuditScanProviderRegistry.Providers;
            for (int i = 0; i < providers.Count; i++)
                MarkStale(providers[i].ProviderId, reason);
        }

        private static PungentProjectAuditIndexEntry Load(string providerId)
        {
            string key = Key(providerId);
            return new PungentProjectAuditIndexEntry
            {
                ProviderId = providerId,
                DisplayName = UtilityWindowPrefs.GetString(key + ".DisplayName", providerId),
                LastCompletedUtcTicks = ParseLong(UtilityWindowPrefs.GetString(key + ".LastCompletedUtcTicks", "0")),
                Stale = UtilityWindowPrefs.GetBool(key + ".Stale", true),
                LastStatus = UtilityWindowPrefs.GetString(key + ".LastStatus", "No cache"),
                ErrorCount = UtilityWindowPrefs.GetInt(key + ".ErrorCount", 0),
                WarningCount = UtilityWindowPrefs.GetInt(key + ".WarningCount", 0),
                InfoCount = UtilityWindowPrefs.GetInt(key + ".InfoCount", 0),
                SuccessCount = UtilityWindowPrefs.GetInt(key + ".SuccessCount", 0),
                HasResult = UtilityWindowPrefs.GetBool(key + ".HasResult", false),
                LastScanMode = UtilityWindowPrefs.GetString(key + ".LastScanMode", "Unknown"),
                LastDurationSeconds = UtilityWindowPrefs.GetFloat(key + ".LastDurationSeconds", 0f)
            };
        }

        private static void Save(PungentProjectAuditIndexEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ProviderId))
                return;

            string key = Key(entry.ProviderId);
            UtilityWindowPrefs.SetString(key + ".DisplayName", entry.DisplayName);
            UtilityWindowPrefs.SetString(key + ".LastCompletedUtcTicks", entry.LastCompletedUtcTicks.ToString());
            UtilityWindowPrefs.SetBool(key + ".Stale", entry.Stale);
            UtilityWindowPrefs.SetString(key + ".LastStatus", entry.LastStatus);
            UtilityWindowPrefs.SetInt(key + ".ErrorCount", entry.ErrorCount);
            UtilityWindowPrefs.SetInt(key + ".WarningCount", entry.WarningCount);
            UtilityWindowPrefs.SetInt(key + ".InfoCount", entry.InfoCount);
            UtilityWindowPrefs.SetInt(key + ".SuccessCount", entry.SuccessCount);
            UtilityWindowPrefs.SetBool(key + ".HasResult", entry.HasResult);
            UtilityWindowPrefs.SetString(key + ".LastScanMode", entry.LastScanMode);
            UtilityWindowPrefs.SetFloat(key + ".LastDurationSeconds", (float)entry.LastDurationSeconds);
        }

        private static string Key(string providerId)
        {
            return PrefPrefix + (providerId ?? "unknown-provider").Trim();
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, out long parsed) ? parsed : 0L;
        }
    }

    public sealed class PungentAuditScanRunner : IDisposable
    {
        private readonly List<PungentAuditScanJob> _jobs = new List<PungentAuditScanJob>();
        private readonly Stopwatch _tickStopwatch = new Stopwatch();
        private readonly PungentAuditProgressBridge _progressBridge = new PungentAuditProgressBridge();
        private int _activeIndex = -1;
        private bool _updateSubscribed;
        private bool _manualPause;
        private bool _backgroundAutoPaused;

        public event Action Changed;
        public event Action Completed;

        public IReadOnlyList<PungentAuditScanJob> Jobs => _jobs;
        public bool IsActive { get; private set; }
        public bool IsRunning => IsActive && !IsPaused && BatchState != PungentAuditBatchState.Cancelling;
        public bool IsPaused => IsActive && (_manualPause || _backgroundAutoPaused || BatchState == PungentAuditBatchState.Paused);
        public bool CancelRequested { get; private set; }
        public PungentAuditBatchState BatchState { get; private set; } = PungentAuditBatchState.Idle;
        public PungentAuditScanMode CurrentMode { get; private set; } = PungentAuditScanMode.Immediate;
        public string StatusMessage { get; private set; } = "Idle";
        public string OverallStatusMessage => StatusMessage;
        public PungentAuditScanJob CurrentJob => _activeIndex >= 0 && _activeIndex < _jobs.Count ? _jobs[_activeIndex] : null;
        public string CurrentProviderId => CurrentJob != null ? CurrentJob.providerId : string.Empty;
        public int TotalCount => _jobs.Count;
        public int TotalJobCount => _jobs.Count;
        public int CompletedCount => CountJobs(PungentAuditScanJobState.Complete);
        public int CompletedJobCount => CompletedCount;
        public int FailedCount => CountJobs(PungentAuditScanJobState.Failed);
        public int CancelledCount => CountJobs(PungentAuditScanJobState.Cancelled);
        public int NotConfiguredCount => CountJobs(PungentAuditScanJobState.NotConfigured);
        public int SkippedCount => CountJobs(PungentAuditScanJobState.Skipped);
        public long LastCompletedTicks { get; private set; }
        public string LastRunSummary { get; private set; } = "No audit run yet.";

        public float OverallProgress01 => Progress01;

        public float Progress01
        {
            get
            {
                if (_jobs.Count == 0)
                    return 0f;
                float units = 0f;
                for (int i = 0; i < _jobs.Count; i++)
                {
                    PungentAuditScanJob job = _jobs[i];
                    if (job.state == PungentAuditScanJobState.Complete ||
                        job.state == PungentAuditScanJobState.Failed ||
                        job.state == PungentAuditScanJobState.Cancelled ||
                        job.state == PungentAuditScanJobState.NotConfigured ||
                        job.state == PungentAuditScanJobState.Skipped)
                    {
                        units += 1f;
                    }
                    else if (job.state == PungentAuditScanJobState.Running ||
                             job.state == PungentAuditScanJobState.Paused ||
                             job.state == PungentAuditScanJobState.Cancelling)
                    {
                        units += (float)Math.Max(0d, Math.Min(1d, job.progress.progress01));
                    }
                }
                return (float)Math.Max(0d, Math.Min(1d, units / _jobs.Count));
            }
        }

        public void StartBatch(IEnumerable<PungentAuditScanJob> jobs)
        {
            StartBatch(jobs, PungentAuditScanMode.Immediate);
        }

        public void StartBatch(IEnumerable<PungentAuditScanJob> jobs, PungentAuditScanMode mode)
        {
            ResetActiveBatch();
            _jobs.Clear();
            if (jobs != null)
                _jobs.AddRange(jobs);

            CurrentMode = mode;
            _activeIndex = -1;
            _manualPause = false;
            _backgroundAutoPaused = false;
            CancelRequested = false;

            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                job.scanMode = mode;
                if (job.state == PungentAuditScanJobState.Idle)
                    job.state = PungentAuditScanJobState.Queued;
            }

            IsActive = HasNonTerminalJob();
            BatchState = IsActive ? PungentAuditBatchState.Queued : PungentAuditBatchState.Idle;
            StatusMessage = IsActive ? GetModeLabel(mode) + " project audit queued." : "No coordinator-runnable providers selected.";

            if (IsActive)
            {
                _progressBridge.StartBatch("PungentFunk Project Audit", StatusMessage, Cancel, false);
                _progressBridge.UpdateBatch(this);
                SubscribeUpdate();
            }
            else
            {
                LastRunSummary = StatusMessage;
            }

            NotifyChanged();
        }

        public void PauseAll()
        {
            if (!IsActive)
                return;

            _manualPause = true;
            BatchState = PungentAuditBatchState.Paused;
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (job.IsTerminal || !job.canPause)
                    continue;
                job.pauseRequested = true;
                if (job.state == PungentAuditScanJobState.Queued)
                    job.state = PungentAuditScanJobState.Paused;
            }
            StatusMessage = "Project audit pause requested. Progress will be preserved.";
            _progressBridge.UpdateBatch(this);
            NotifyChanged();
        }

        public void ResumeAll()
        {
            _manualPause = false;
            _backgroundAutoPaused = false;
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                job.pauseRequested = false;
                if (job.state == PungentAuditScanJobState.Paused)
                    job.state = PungentAuditScanJobState.Queued;
            }
            if (IsActive)
            {
                BatchState = PungentAuditBatchState.Running;
                StatusMessage = "Project audit resumed from preserved progress.";
                SubscribeUpdate();
            }
            _progressBridge.UpdateBatch(this);
            NotifyChanged();
        }

        public void InterruptForRestart(string message = null)
        {
            if (_jobs.Count == 0 && !IsActive)
                return;

            if (IsActive)
                ProcessPauseCheckpoint();

            IsActive = false;
            CancelRequested = false;
            _manualPause = false;
            _backgroundAutoPaused = false;
            BatchState = PungentAuditBatchState.Idle;
            StatusMessage = string.IsNullOrWhiteSpace(message) ? "Project audit interrupted for restart." : message.Trim();
            LastRunSummary = StatusMessage;
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (job == null || job.IsTerminal)
                    continue;

                job.pauseRequested = false;
                job.cancelRequested = false;
                job.statusMessage = "Interrupted for restart. Previous completed cache remains available.";
            }

            _progressBridge.Clear();
            UnsubscribeUpdate();
            NotifyChanged();
        }

        public void PauseForInterruption(string message)
        {
            if (!IsActive)
                return;

            PauseAll();
            StatusMessage = string.IsNullOrWhiteSpace(message) ? "Project audit paused at a safe checkpoint." : message.Trim();
            ProcessPauseCheckpoint();
            StatusMessage = string.IsNullOrWhiteSpace(message) ? "Project audit paused at a safe checkpoint." : message.Trim();
            _progressBridge.UpdateBatch(this);
            NotifyChanged();
        }

        public void Cancel()
        {
            Cancel("Project audit interruption requested.");
        }

        public void Cancel(string message)
        {
            if (_jobs.Count == 0 && !IsActive)
                return;

            CancelRequested = true;
            if (IsActive)
                BatchState = PungentAuditBatchState.Cancelling;
            StatusMessage = string.IsNullOrWhiteSpace(message) ? "Project audit interruption requested." : message.Trim();

            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (job.IsTerminal || !job.canCancel)
                    continue;

                job.cancelRequested = true;
                if (job.state == PungentAuditScanJobState.Queued || job.state == PungentAuditScanJobState.Paused || job.state == PungentAuditScanJobState.Idle)
                    job.MarkCancelled("Interrupted before running.");
                else
                    job.MarkCancelling("Interruption requested. Waiting for the next safe provider checkpoint.");
            }

            _progressBridge.UpdateBatch(this);
            if (IsActive && !HasNonTerminalJob())
                FinishBatch();
            else
                NotifyChanged();
        }

        public void PauseJob(string providerId)
        {
            PungentAuditScanJob job = FindJob(providerId);
            if (job == null || !job.canPause || job.IsTerminal)
                return;

            job.pauseRequested = true;
            if (job.state == PungentAuditScanJobState.Queued)
                job.state = PungentAuditScanJobState.Paused;
            StatusMessage = job.displayName + " pause requested.";
            _progressBridge.UpdateJob(job);
            NotifyChanged();
        }

        public void ResumeJob(string providerId)
        {
            PungentAuditScanJob job = FindJob(providerId);
            if (job == null)
                return;

            job.pauseRequested = false;
            if (job.state == PungentAuditScanJobState.Paused)
                job.state = PungentAuditScanJobState.Queued;
            _manualPause = false;
            if (IsActive)
                SubscribeUpdate();
            StatusMessage = job.displayName + " resumed.";
            _progressBridge.UpdateJob(job);
            NotifyChanged();
        }

        public void CancelJob(string providerId)
        {
            PungentAuditScanJob job = FindJob(providerId);
            if (job == null || job.IsTerminal || !job.canCancel)
                return;

            job.cancelRequested = true;
            if (job.state == PungentAuditScanJobState.Queued || job.state == PungentAuditScanJobState.Paused || job.state == PungentAuditScanJobState.Idle)
                job.MarkCancelled("Interrupted before running.");
            else
                job.MarkCancelling("Interruption requested. Waiting for the next safe provider checkpoint.");
            StatusMessage = job.displayName + " interruption requested.";
            _progressBridge.UpdateJob(job);
            NotifyChanged();
        }

        public PungentAuditScanJob FindJob(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return null;
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (string.Equals(_jobs[i].providerId, providerId, StringComparison.OrdinalIgnoreCase))
                    return _jobs[i];
            }
            return null;
        }

        public void Dispose()
        {
            if (IsActive)
                MarkInterrupted("Project audit interrupted by window close, domain reload, or editor shutdown.");
            UnsubscribeUpdate();
            _progressBridge.Dispose();
        }

        private void Tick()
        {
            if (!IsActive)
            {
                UnsubscribeUpdate();
                return;
            }

            if (_manualPause)
            {
                if (ProcessPauseCheckpoint())
                {
                    NotifyChanged();
                    return;
                }

                if (!HasNonTerminalJob())
                {
                    FinishBatch();
                    return;
                }

                BatchState = PungentAuditBatchState.Paused;
                StatusMessage = "Project audit paused. Progress preserved; resume or restart when ready.";
                _progressBridge.UpdateBatch(this);
                NotifyChanged();
                return;
            }

            if (CurrentMode == PungentAuditScanMode.BackgroundIdle && !PungentAuditScanSettings.CanRunBackgroundScanNow(out string busyReason))
            {
                _backgroundAutoPaused = true;
                BatchState = PungentAuditBatchState.Paused;
                StatusMessage = "Background project audit paused: " + busyReason;
                _progressBridge.UpdateBatch(this);
                NotifyChanged();
                return;
            }

            _backgroundAutoPaused = false;
            BatchState = CancelRequested ? PungentAuditBatchState.Cancelling : PungentAuditBatchState.Running;
            int budgetMs = CurrentMode == PungentAuditScanMode.BackgroundIdle
                ? PungentAuditScanSettings.BackgroundTimeBudgetMs
                : PungentAuditScanSettings.ImmediateTimeBudgetMs;
            budgetMs = Math.Max(1, budgetMs);

            _tickStopwatch.Reset();
            _tickStopwatch.Start();

            bool touchedJob = false;
            do
            {
                PungentAuditScanJob job = NextRunnableJob();
                if (job == null)
                {
                    if (HasPausedPendingJob())
                    {
                        BatchState = PungentAuditBatchState.Paused;
                        StatusMessage = "Project audit paused.";
                        _progressBridge.UpdateBatch(this);
                        NotifyChanged();
                        return;
                    }

                    FinishBatch();
                    return;
                }

                touchedJob = true;
                _activeIndex = _jobs.IndexOf(job);

                if (job.cancelRequested && job.state != PungentAuditScanJobState.Running && job.state != PungentAuditScanJobState.Cancelling)
                {
                    job.MarkCancelled(job.displayName + " interrupted.");
                    _progressBridge.UpdateJob(job);
                    continue;
                }

                StatusMessage = "Running " + job.displayName + "...";
                bool stepFinished = job.ExecuteStep(CurrentMode);
                StatusMessage = job.statusMessage;
                _progressBridge.UpdateJob(job);
                _progressBridge.UpdateBatch(this);
                if (stepFinished)
                    PungentProjectAuditIndex.RecordJob(job, CurrentMode);
                else if (job.pauseRequested && !job.IsTerminal)
                {
                    job.state = PungentAuditScanJobState.Paused;
                    StatusMessage = string.IsNullOrWhiteSpace(job.statusMessage) ? job.displayName + " paused." : job.statusMessage;
                    _progressBridge.UpdateJob(job);
                    _progressBridge.UpdateBatch(this);
                }
                if (!stepFinished)
                    break;
            }
            while (_tickStopwatch.ElapsedMilliseconds < budgetMs);

            _tickStopwatch.Stop();

            if (touchedJob)
                NotifyChanged();
        }

        private PungentAuditScanJob NextRunnableJob()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (job.IsTerminal)
                    continue;
                if (job.state == PungentAuditScanJobState.Running && job.pauseRequested)
                    return job;
                if (job.state == PungentAuditScanJobState.Paused || job.pauseRequested)
                    continue;
                return job;
            }
            return null;
        }

        private bool HasPausedPendingJob()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (!job.IsTerminal && (job.state == PungentAuditScanJobState.Paused || job.pauseRequested))
                    return true;
            }
            return false;
        }

        private bool ProcessPauseCheckpoint()
        {
            PungentAuditScanJob job = FindRunningPauseRequestedJob();
            if (job == null)
                return false;

            bool stepFinished = job.ExecuteStep(CurrentMode);
            StatusMessage = string.IsNullOrWhiteSpace(job.statusMessage) ? job.displayName + " paused." : job.statusMessage;
            _progressBridge.UpdateJob(job);
            _progressBridge.UpdateBatch(this);
            if (stepFinished)
            {
                PungentProjectAuditIndex.RecordJob(job, CurrentMode);
                return true;
            }

            if (job.pauseRequested && !job.IsTerminal)
            {
                job.state = PungentAuditScanJobState.Paused;
                _progressBridge.UpdateJob(job);
            }
            return true;
        }

        private PungentAuditScanJob FindRunningPauseRequestedJob()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (job != null && job.state == PungentAuditScanJobState.Running && job.pauseRequested && !job.IsTerminal)
                    return job;
            }
            return null;
        }

        private bool HasNonTerminalJob()
        {
            for (int i = 0; i < _jobs.Count; i++)
                if (!_jobs[i].IsTerminal)
                    return true;
            return false;
        }

        private void FinishBatch()
        {
            IsActive = false;
            _manualPause = false;
            _backgroundAutoPaused = false;
            for (int i = 0; i < _jobs.Count; i++)
                if (_jobs[i].IsTerminal)
                    PungentProjectAuditIndex.RecordJob(_jobs[i], CurrentMode);

            if (CancelRequested || CancelledCount > 0)
            {
                BatchState = PungentAuditBatchState.Cancelled;
                StatusMessage = "Project audit interrupted. " + BuildCountSummary() + ".";
            }
            else if (FailedCount > 0)
            {
                BatchState = CompletedCount > 0 || NotConfiguredCount > 0 || SkippedCount > 0
                    ? PungentAuditBatchState.CompleteWithIssues
                    : PungentAuditBatchState.Failed;
                StatusMessage = "Project audit completed with failures. " + BuildCountSummary() + ".";
            }
            else if (NotConfiguredCount > 0 || SkippedCount > 0)
            {
                BatchState = PungentAuditBatchState.CompleteWithIssues;
                StatusMessage = "Project audit complete with skipped or not-configured providers. " + BuildCountSummary() + ".";
            }
            else
            {
                BatchState = PungentAuditBatchState.Complete;
                StatusMessage = "Project audit complete. " + BuildCountSummary() + ".";
            }

            LastCompletedTicks = DateTime.UtcNow.Ticks;
            LastRunSummary = StatusMessage;
            _progressBridge.UpdateBatch(this);
            _progressBridge.FinishBatch(BatchState);
            UnsubscribeUpdate();
            NotifyChanged();
            Completed?.Invoke();
        }

        private void ResetActiveBatch()
        {
            if (IsActive)
                MarkInterrupted("Project audit interrupted by a new audit batch.");
            _progressBridge.Clear();
            IsActive = false;
            BatchState = PungentAuditBatchState.Idle;
            CancelRequested = false;
            _manualPause = false;
            _backgroundAutoPaused = false;
            UnsubscribeUpdate();
        }

        private void MarkInterrupted(string message)
        {
            CancelRequested = true;
            for (int i = 0; i < _jobs.Count; i++)
            {
                PungentAuditScanJob job = _jobs[i];
                if (!job.IsTerminal)
                    job.MarkCancelled(message);
            }
            IsActive = false;
            BatchState = PungentAuditBatchState.Cancelled;
            StatusMessage = message;
            LastCompletedTicks = DateTime.UtcNow.Ticks;
            LastRunSummary = message;
            _progressBridge.UpdateBatch(this);
            _progressBridge.FinishBatch(BatchState);
        }

        private string BuildCountSummary()
        {
            return CompletedCount + " complete, " +
                   FailedCount + " failed, " +
                   CancelledCount + " interrupted, " +
                   NotConfiguredCount + " not configured, " +
                   SkippedCount + " skipped";
        }

        private int CountJobs(PungentAuditScanJobState state)
        {
            int count = 0;
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (_jobs[i].state == state)
                    count++;
            }
            return count;
        }

        private void SubscribeUpdate()
        {
            if (_updateSubscribed)
                return;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += Dispose;
            _updateSubscribed = true;
        }

        private void UnsubscribeUpdate()
        {
            if (!_updateSubscribed)
                return;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.quitting -= Dispose;
            _updateSubscribed = false;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (CurrentMode == PungentAuditScanMode.BackgroundIdle && PungentAuditScanSettings.PauseDuringPlayMode)
                {
                    _backgroundAutoPaused = true;
                    BatchState = PungentAuditBatchState.Paused;
                    StatusMessage = "Background project audit paused during Play Mode transition.";
                    NotifyChanged();
                    return;
                }

                PauseForInterruption("Project audit paused before Play Mode transition.");
            }
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static string GetModeLabel(PungentAuditScanMode mode)
        {
            return mode == PungentAuditScanMode.BackgroundIdle ? "Background idle" : "Immediate";
        }
    }

    /// <summary>
    /// Reflection-backed bridge so older Unity versions compile without UnityEditor.Progress references.
    /// </summary>
    internal sealed class PungentAuditProgressBridge : IDisposable
    {
        private readonly Type _progressType;
        private readonly Dictionary<string, int> _jobProgressIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private int _batchProgressId = -1;
        private Action _cancelCallback;
        private bool _allowUserCancel;

        public PungentAuditProgressBridge()
        {
            _progressType = typeof(Editor).Assembly.GetType("UnityEditor.Progress");
        }

        public bool IsAvailable => _progressType != null;

        public void StartBatch(string title, string description, Action cancelCallback, bool allowUserCancel)
        {
            Clear();
            if (!IsAvailable)
                return;

            _allowUserCancel = allowUserCancel;
            _cancelCallback = allowUserCancel ? cancelCallback : null;
            _batchProgressId = StartProgress(title, description, -1);
            if (_allowUserCancel)
                RegisterCancelCallback(_batchProgressId);
        }

        public void UpdateBatch(PungentAuditScanRunner runner)
        {
            if (!IsAvailable || runner == null || _batchProgressId < 0)
                return;

            Report(_batchProgressId, runner.OverallProgress01, runner.OverallStatusMessage);
        }

        public void UpdateJob(PungentAuditScanJob job)
        {
            if (!IsAvailable || job == null || string.IsNullOrWhiteSpace(job.providerId) || _batchProgressId < 0)
                return;

            if (!_jobProgressIds.TryGetValue(job.providerId, out int progressId))
            {
                progressId = StartProgress(job.displayName, job.statusMessage, _batchProgressId);
                _jobProgressIds[job.providerId] = progressId;
                if (_allowUserCancel)
                    RegisterCancelCallback(progressId);
            }

            float progress = job.progress.isIndeterminate ? -1f : job.progress.progress01;
            Report(progressId, progress, job.statusMessage);

            if (job.IsTerminal)
            {
                FinishProgress(progressId, GetProgressStatus(job.state));
                _jobProgressIds.Remove(job.providerId);
            }
        }

        public void FinishBatch(PungentAuditBatchState state)
        {
            if (!IsAvailable)
                return;

            foreach (int id in _jobProgressIds.Values)
                FinishProgress(id, GetProgressStatus(state));
            _jobProgressIds.Clear();

            if (_batchProgressId >= 0)
            {
                FinishProgress(_batchProgressId, GetProgressStatus(state));
                _batchProgressId = -1;
            }
        }

        public void Clear()
        {
            if (!IsAvailable)
                return;

            try
            {
                foreach (int id in _jobProgressIds.Values)
                    RemoveProgress(id);

                if (_batchProgressId >= 0)
                    RemoveProgress(_batchProgressId);
            }
            catch
            {
                // Native Progress is optional polish; cleanup failures must not block scan runner state.
            }
            finally
            {
                _jobProgressIds.Clear();
                _batchProgressId = -1;
                _allowUserCancel = false;
                _cancelCallback = null;
            }
        }

        public void Dispose()
        {
            Clear();
        }

        private int StartProgress(string name, string description, int parentId)
        {
            if (!IsAvailable)
                return -1;

            MethodInfo[] methods = _progressType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Start", StringComparison.Ordinal) || method.ReturnType != typeof(int))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    continue;

                object[] args = new object[parameters.Length];
                int stringIndex = 0;
                bool supported = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    ParameterInfo parameter = parameters[p];
                    if (parameter.ParameterType == typeof(string))
                    {
                        args[p] = stringIndex == 0 ? name : description;
                        stringIndex++;
                    }
                    else if (parameter.ParameterType == typeof(int))
                    {
                        args[p] = parameter.Name != null && parameter.Name.IndexOf("parent", StringComparison.OrdinalIgnoreCase) >= 0 ? parentId : -1;
                    }
                    else if (parameter.ParameterType.IsEnum)
                    {
                        args[p] = GetEnumValue(parameter.ParameterType, "None");
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        args[p] = parameter.DefaultValue;
                    }
                    else
                    {
                        supported = false;
                        break;
                    }
                }

                if (!supported)
                    continue;

                try
                {
                    return (int)method.Invoke(null, args);
                }
                catch
                {
                    // Try another overload.
                }
            }

            return -1;
        }

        private void Report(int id, float progress01, string description)
        {
            if (!IsAvailable || id < 0)
                return;

            MethodInfo[] methods = _progressType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Report", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 3 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(string) && parameters[2].ParameterType == typeof(float))
                    {
                        method.Invoke(null, new object[] { id, description ?? string.Empty, progress01 });
                        return;
                    }

                    if (parameters.Length == 3 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(float) && parameters[2].ParameterType == typeof(string))
                    {
                        method.Invoke(null, new object[] { id, progress01, description ?? string.Empty });
                        return;
                    }

                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(float))
                    {
                        method.Invoke(null, new object[] { id, progress01 });
                        SetDescription(id, description);
                        return;
                    }
                }
                catch
                {
                    // Fallback to internal Design Audit progress state.
                }
            }
        }

        private void SetDescription(int id, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return;

            MethodInfo method = FindProgressMethod("SetDescription", typeof(int), typeof(string));
            if (method == null)
                return;

            try
            {
                method.Invoke(null, new object[] { id, description });
            }
            catch
            {
                // Optional native progress polish only.
            }
        }

        private void FinishProgress(int id, string status)
        {
            if (!IsAvailable || id < 0)
                return;

            MethodInfo[] methods = _progressType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Finish", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType.IsEnum)
                    {
                        method.Invoke(null, new object[] { id, GetEnumValue(parameters[1].ParameterType, status) });
                        return;
                    }

                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    {
                        method.Invoke(null, new object[] { id });
                        return;
                    }
                }
                catch
                {
                    // Try another overload.
                }
            }

            RemoveProgress(id);
        }

        private void RemoveProgress(int id)
        {
            if (!IsAvailable || id < 0)
                return;

            MethodInfo[] methods = _progressType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Remove", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(int))
                    continue;

                object[] args = new object[parameters.Length];
                args[0] = id;
                bool supported = true;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].HasDefaultValue)
                        args[p] = parameters[p].DefaultValue;
                    else
                    {
                        supported = false;
                        break;
                    }
                }

                if (!supported)
                    continue;

                try
                {
                    method.Invoke(null, args);
                    return;
                }
                catch
                {
                    // Try another overload.
                }
            }
        }

        private void RegisterCancelCallback(int id)
        {
            if (id < 0 || _cancelCallback == null)
                return;

            MethodInfo method = FindProgressMethod("RegisterCancelCallback", typeof(int), null);
            if (method == null)
                return;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2)
                return;

            try
            {
                Type callbackType = parameters[1].ParameterType;
                if (callbackType == typeof(Func<bool>))
                {
                    Func<bool> callback = OnNativeCancelRequested;
                    method.Invoke(null, new object[] { id, callback });
                }
                else if (callbackType == typeof(Action))
                {
                    Action callback = () => _cancelCallback?.Invoke();
                    method.Invoke(null, new object[] { id, callback });
                }
            }
            catch
            {
                // Native cancellation is optional; Design Audit normally uses pause/resume/restart.
            }
        }

        private MethodInfo FindProgressMethod(string name, params Type[] parameterTypes)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(name))
                return null;

            MethodInfo[] methods = _progressType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                    continue;

                bool match = true;
                for (int p = 0; p < parameterTypes.Length; p++)
                {
                    Type expected = parameterTypes[p];
                    if (expected != null && parameters[p].ParameterType != expected)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return method;
            }

            return null;
        }

        private bool OnNativeCancelRequested()
        {
            _cancelCallback?.Invoke();
            return true;
        }

        private static object GetEnumValue(Type enumType, string desired)
        {
            if (enumType == null || !enumType.IsEnum)
                return null;

            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], desired, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse(enumType, names[i]);
            }

            if (names.Length > 0)
                return Enum.Parse(enumType, names[0]);
            return Activator.CreateInstance(enumType);
        }

        private static string GetProgressStatus(PungentAuditScanJobState state)
        {
            switch (state)
            {
                case PungentAuditScanJobState.Cancelled:
                    return "Canceled";
                case PungentAuditScanJobState.Failed:
                    return "Failed";
                default:
                    return "Succeeded";
            }
        }

        private static string GetProgressStatus(PungentAuditBatchState state)
        {
            switch (state)
            {
                case PungentAuditBatchState.Cancelled:
                    return "Canceled";
                case PungentAuditBatchState.Failed:
                case PungentAuditBatchState.CompleteWithIssues:
                    return "Failed";
                default:
                    return "Succeeded";
            }
        }
    }
#endif
}
