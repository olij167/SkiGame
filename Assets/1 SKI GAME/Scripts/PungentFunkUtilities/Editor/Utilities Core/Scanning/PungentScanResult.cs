namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Cached result model shared by explicit project scanners and validation windows.
    /// </summary>
    public sealed class PungentScanResult
    {
        private readonly List<PungentScanIssue> _issues = new List<PungentScanIssue>();
        private readonly PungentScanSummary _summary = new PungentScanSummary();

        public PungentScanResult(string toolId, string displayName, PungentScanScope scope, string scopeLabel)
        {
            ToolId = string.IsNullOrWhiteSpace(toolId) ? "unknown-scan" : toolId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ToolId : displayName.Trim();
            Scope = scope;
            ScopeLabel = string.IsNullOrWhiteSpace(scopeLabel) ? scope.ToString() : scopeLabel.Trim();
            StartedAtUtc = DateTime.UtcNow;
            StatusMessage = "Running scan...";
            _summary.StartedAtUtc = StartedAtUtc;
        }

        public string ToolId { get; private set; }
        public string DisplayName { get; private set; }
        public PungentScanScope Scope { get; private set; }
        public string ScopeLabel { get; private set; }
        public DateTime StartedAtUtc { get; private set; }
        public DateTime CompletedAtUtc { get; private set; }
        public double DurationSeconds { get; private set; }
        public bool IsComplete { get; private set; }
        public bool WasCanceled { get; private set; }
        public string StatusMessage { get; set; }
        public int TotalScanned { get; set; }
        public int TotalMatched { get; set; }
        public int TotalSkipped { get; set; }
        public int TotalChanged { get; set; }
        public IReadOnlyList<PungentScanIssue> Issues => _issues;
        public PungentScanSummary Summary => _summary;

        public void AddIssue(PungentScanSeverity severity, string title, string message, Object context = null, string path = null, string code = null)
        {
            _issues.Add(new PungentScanIssue(severity, title, message, context, path, code));
        }

        public void AddIssue(PungentScanIssue issue)
        {
            if (issue != null)
                _issues.Add(issue);
        }

        public int CountIssues(PungentScanSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i] != null && _issues[i].Severity == severity)
                    count++;
            }
            return count;
        }

        public void Complete(double durationSeconds, string statusMessage = null, bool canceled = false)
        {
            DurationSeconds = Math.Max(0d, durationSeconds);
            CompletedAtUtc = DateTime.UtcNow;
            IsComplete = true;
            WasCanceled = canceled;
            if (!string.IsNullOrWhiteSpace(statusMessage))
                StatusMessage = statusMessage.Trim();
            else if (string.IsNullOrWhiteSpace(StatusMessage))
                StatusMessage = canceled ? "Scan canceled." : "Scan complete.";

            RefreshSummary();
        }

        private void RefreshSummary()
        {
            _summary.TotalScanned = TotalScanned;
            _summary.TotalMatched = TotalMatched;
            _summary.TotalSkipped = TotalSkipped;
            _summary.TotalChanged = TotalChanged;
            _summary.InfoCount = CountIssues(PungentScanSeverity.Info);
            _summary.SuccessCount = CountIssues(PungentScanSeverity.Success);
            _summary.WarningCount = CountIssues(PungentScanSeverity.Warning);
            _summary.ErrorCount = CountIssues(PungentScanSeverity.Error);
            _summary.WasCanceled = WasCanceled;
            _summary.DurationSeconds = DurationSeconds;
            _summary.StartedAtUtc = StartedAtUtc;
            _summary.CompletedAtUtc = CompletedAtUtc;
        }

        internal static PungentScanResult Restore(
            string toolId,
            string displayName,
            PungentScanScope scope,
            string scopeLabel,
            DateTime startedAtUtc,
            DateTime completedAtUtc,
            double durationSeconds,
            string statusMessage,
            int totalScanned,
            int totalMatched,
            int totalSkipped,
            int totalChanged,
            bool wasCanceled,
            IEnumerable<PungentScanIssue> issues)
        {
            PungentScanResult result = new PungentScanResult(toolId, displayName, scope, scopeLabel);
            result.StartedAtUtc = startedAtUtc == default(DateTime) ? DateTime.UtcNow : startedAtUtc;
            result.CompletedAtUtc = completedAtUtc == default(DateTime) ? result.StartedAtUtc : completedAtUtc;
            result.DurationSeconds = Math.Max(0d, durationSeconds);
            result.IsComplete = true;
            result.WasCanceled = wasCanceled;
            result.StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? (wasCanceled ? "Restored cancelled scan snapshot." : "Restored scan snapshot.") : statusMessage.Trim();
            result.TotalScanned = totalScanned;
            result.TotalMatched = totalMatched;
            result.TotalSkipped = totalSkipped;
            result.TotalChanged = totalChanged;

            if (issues != null)
            {
                foreach (PungentScanIssue issue in issues)
                    result.AddIssue(issue);
            }

            result.RefreshSummary();
            return result;
        }
    }
#endif
}
