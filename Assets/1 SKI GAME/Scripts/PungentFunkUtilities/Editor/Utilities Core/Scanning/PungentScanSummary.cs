namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;

    /// <summary>
    /// Lightweight immutable-style summary for a completed scan result.
    /// </summary>
    public sealed class PungentScanSummary
    {
        public int TotalScanned { get; internal set; }
        public int TotalMatched { get; internal set; }
        public int TotalSkipped { get; internal set; }
        public int TotalChanged { get; internal set; }
        public int InfoCount { get; internal set; }
        public int SuccessCount { get; internal set; }
        public int WarningCount { get; internal set; }
        public int ErrorCount { get; internal set; }
        public bool WasCanceled { get; internal set; }
        public double DurationSeconds { get; internal set; }
        public DateTime StartedAtUtc { get; internal set; }
        public DateTime CompletedAtUtc { get; internal set; }

        public bool HasRun => StartedAtUtc != default(DateTime);
        public bool HasIssues => InfoCount + SuccessCount + WarningCount + ErrorCount > 0;
        public bool HasWarningsOrErrors => WarningCount > 0 || ErrorCount > 0;

        public string DurationLabel => DurationSeconds < 1d
            ? string.Format("{0:0} ms", Math.Max(0d, DurationSeconds) * 1000d)
            : string.Format("{0:0.00} s", DurationSeconds);
    }
#endif
}
