namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Explicit scan lifecycle helper. It is intentionally passive: it never scans, updates, or repaints by itself.
    /// </summary>
    public sealed class PungentScanSession
    {
        private readonly string _toolId;
        private readonly string _displayName;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public PungentScanSession(string toolId, string displayName = null)
        {
            _toolId = string.IsNullOrWhiteSpace(toolId) ? "unknown-scan" : toolId.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName) ? _toolId : displayName.Trim();
        }

        public bool IsRunning { get; private set; }
        public PungentScanResult Result { get; private set; }
        public bool HasResult => Result != null;
        public string ResultSourceBanner { get; private set; }

        public PungentScanResult Begin(PungentScanScope scope, string scopeLabel = null)
        {
            Result = new PungentScanResult(_toolId, _displayName, scope, scopeLabel);
            ResultSourceBanner = string.Empty;
            IsRunning = true;
            _stopwatch.Reset();
            _stopwatch.Start();
            return Result;
        }

        public PungentScanResult Complete(int totalScanned, int totalMatched, int totalSkipped, int totalChanged, string statusMessage = null)
        {
            if (Result == null)
                Result = new PungentScanResult(_toolId, _displayName, PungentScanScope.Custom, "Unknown");

            _stopwatch.Stop();
            Result.TotalScanned = totalScanned;
            Result.TotalMatched = totalMatched;
            Result.TotalSkipped = totalSkipped;
            Result.TotalChanged = totalChanged;
            Result.Complete(_stopwatch.Elapsed.TotalSeconds, statusMessage, false);
            IsRunning = false;
            PungentScanCache.Store(Result);
            ResultSourceBanner = "Direct scanner run · " + PungentScanSnapshotStore.GetLastScanAgeLabel(_toolId);
            return Result;
        }

        public PungentScanResult Cancel(string statusMessage = null, bool publishToCache = false)
        {
            if (Result == null)
                Result = new PungentScanResult(_toolId, _displayName, PungentScanScope.Custom, "Canceled");

            _stopwatch.Stop();
            Result.Complete(_stopwatch.Elapsed.TotalSeconds, statusMessage ?? "Scan canceled.", true);
            IsRunning = false;
            if (publishToCache)
            {
                PungentScanCache.Store(Result);
                ResultSourceBanner = "Cancelled result cached · " + PungentScanSnapshotStore.GetLastScanAgeLabel(_toolId);
            }
            return Result;
        }

        public PungentScanResult Fail(Exception exception, string statusMessage = null)
        {
            if (Result == null)
                Result = new PungentScanResult(_toolId, _displayName, PungentScanScope.Custom, "Failed");

            _stopwatch.Stop();
            string message = exception == null ? "Unknown scan failure." : exception.Message;
            Result.AddIssue(PungentScanSeverity.Error, "Scan failed", message, null, null, "SCAN_EXCEPTION");
            Result.Complete(_stopwatch.Elapsed.TotalSeconds, statusMessage ?? message, false);
            IsRunning = false;
            PungentScanCache.Store(Result);
            ResultSourceBanner = "Direct scanner run failed · " + PungentScanSnapshotStore.GetLastScanAgeLabel(_toolId);
            return Result;
        }

        public void Clear()
        {
            IsRunning = false;
            Result = null;
            ResultSourceBanner = string.Empty;
            _stopwatch.Reset();
            PungentScanCache.Clear(_toolId);
        }

        public bool TryLoadLatestCachedOrSnapshot(out string sourceBanner)
        {
            sourceBanner = string.Empty;
            if (IsRunning)
                return false;

            if (!PungentScanCache.TryGet(_toolId, out PungentScanResult latest, out string sourceLabel) || latest == null)
                return false;

            DateTime latestStamp = latest.IsComplete ? latest.CompletedAtUtc : latest.StartedAtUtc;
            DateTime currentStamp = Result == null ? default(DateTime) : Result.IsComplete ? Result.CompletedAtUtc : Result.StartedAtUtc;
            if (Result != null && currentStamp != default(DateTime) && latestStamp != default(DateTime) && currentStamp > latestStamp)
                return false;

            Result = latest;
            string age = PungentScanSnapshotStore.GetLastScanAgeLabel(_toolId);
            sourceBanner = "Loaded latest " + sourceLabel + " · " + age;
            ResultSourceBanner = sourceBanner;
            return true;
        }
    }
#endif
}
