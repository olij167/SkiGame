namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Session-local scan cache for editor windows. Results are explicit and never trigger background work.
    /// </summary>
    public static class PungentScanCache
    {
        private static readonly Dictionary<string, PungentScanResult> ResultsByToolId = new Dictionary<string, PungentScanResult>(StringComparer.OrdinalIgnoreCase);

        public static void Store(PungentScanResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.ToolId))
                return;

            ResultsByToolId[result.ToolId] = result;
            PungentProjectAuditIndex.RecordResult(result);
            PungentScanSnapshotStore.Store(result);
        }

        public static bool TryGet(string toolId, out PungentScanResult result)
        {
            return TryGet(toolId, out result, out _);
        }

        public static bool TryGet(string toolId, out PungentScanResult result, out string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(toolId))
            {
                result = null;
                sourceLabel = string.Empty;
                return false;
            }

            string key = toolId.Trim();
            if (ResultsByToolId.TryGetValue(key, out result))
            {
                sourceLabel = "Design Validation Audit cache";
                return true;
            }

            if (PungentScanSnapshotStore.TryRestoreResult(key, out result))
            {
                ResultsByToolId[key] = result;
                sourceLabel = "persisted scan snapshot";
                return true;
            }

            sourceLabel = string.Empty;
            return false;
        }

        public static PungentScanResult Get(string toolId)
        {
            PungentScanResult result;
            return TryGet(toolId, out result) ? result : null;
        }

        public static void Clear(string toolId)
        {
            if (!string.IsNullOrWhiteSpace(toolId))
            {
                ResultsByToolId.Remove(toolId.Trim());
                PungentScanSnapshotStore.Clear(toolId);
            }
        }

        public static void ClearAll()
        {
            ResultsByToolId.Clear();
            PungentScanSnapshotStore.ClearAll();
        }

        public static bool TryHydrateSession(PungentScanSession session, out string sourceBanner)
        {
            sourceBanner = string.Empty;
            return session != null && session.TryLoadLatestCachedOrSnapshot(out sourceBanner);
        }
    }

    [Serializable]
    public sealed class PungentScanSummarySnapshot
    {
        public int totalScanned;
        public int totalMatched;
        public int totalSkipped;
        public int totalChanged;
        public int infoCount;
        public int successCount;
        public int warningCount;
        public int errorCount;
        public bool wasCancelled;
        public double durationSeconds;
    }

    [Serializable]
    public sealed class PungentScanIssueSnapshot
    {
        public string severity;
        public string title;
        public string message;
        public string assetPath;
        public string issueCode;
        public long createdUtcTicks;
        public string contextObjectName;
        public string contextGlobalObjectId;
        public string sourceToolId;
    }

    [Serializable]
    public sealed class PungentScanResultSnapshot
    {
        public string toolId;
        public string displayName;
        public string scope;
        public string scopeLabel;
        public long startedUtcTicks;
        public long completedUtcTicks;
        public double durationSeconds;
        public string statusMessage;
        public int totalScanned;
        public int totalMatched;
        public int totalSkipped;
        public int totalChanged;
        public bool wasCancelled;
        public int totalIssueCount;
        public int storedIssueCount;
        public bool issueListTruncated;
        public PungentScanSummarySnapshot summary = new PungentScanSummarySnapshot();
        public List<PungentScanIssueSnapshot> issues = new List<PungentScanIssueSnapshot>();
    }

    public static class PungentScanSnapshotStore
    {
        private const int MaxPersistedIssuesPerResult = 750;
        private static readonly List<PungentScanResultSnapshot> Snapshots = new List<PungentScanResultSnapshot>();
        private static bool _loaded;

        public static void Store(PungentScanResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.ToolId) || !result.IsComplete)
                return;
            if (result.WasCanceled)
                return;

            EnsureLoaded();
            string toolId = result.ToolId.Trim();
            Snapshots.RemoveAll(snapshot => snapshot != null && string.Equals(snapshot.toolId, toolId, StringComparison.OrdinalIgnoreCase));
            Snapshots.Add(CreateSnapshot(result));
            Save();
        }

        public static bool TryGetSnapshot(string toolId, out PungentScanResultSnapshot snapshot)
        {
            EnsureLoaded();
            snapshot = null;
            if (string.IsNullOrWhiteSpace(toolId))
                return false;
            snapshot = Snapshots.Find(item => item != null && string.Equals(item.toolId, toolId.Trim(), StringComparison.OrdinalIgnoreCase));
            return snapshot != null;
        }

        public static bool TryRestoreResult(string toolId, out PungentScanResult result)
        {
            result = null;
            if (!TryGetSnapshot(toolId, out PungentScanResultSnapshot snapshot))
                return false;

            List<PungentScanIssue> issues = new List<PungentScanIssue>();
            if (snapshot.issues != null)
            {
                for (int i = 0; i < snapshot.issues.Count; i++)
                {
                    PungentScanIssueSnapshot issue = snapshot.issues[i];
                    if (issue == null)
                        continue;
                    issues.Add(PungentScanIssue.Restore(
                        ParseEnum(issue.severity, PungentScanSeverity.Info),
                        issue.title,
                        issue.message,
                        issue.assetPath,
                        issue.issueCode,
                        FromTicks(issue.createdUtcTicks)));
                }
            }

            if (snapshot.issueListTruncated)
            {
                issues.Add(PungentScanIssue.Restore(
                    PungentScanSeverity.Info,
                    "Snapshot issue list truncated",
                    "The persisted snapshot stores the first " + snapshot.storedIssueCount + " of " + snapshot.totalIssueCount + " issue(s). Rerun the scanner for the full live issue list.",
                    null,
                    "SCAN_SNAPSHOT_TRUNCATED",
                    DateTime.UtcNow));
            }

            result = PungentScanResult.Restore(
                snapshot.toolId,
                snapshot.displayName,
                ParseEnum(snapshot.scope, PungentScanScope.Custom),
                snapshot.scopeLabel,
                FromTicks(snapshot.startedUtcTicks),
                FromTicks(snapshot.completedUtcTicks),
                snapshot.durationSeconds,
                snapshot.statusMessage,
                snapshot.totalScanned,
                snapshot.totalMatched,
                snapshot.totalSkipped,
                snapshot.totalChanged,
                snapshot.wasCancelled,
                issues);
            return true;
        }

        public static void Clear(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return;
            EnsureLoaded();
            Snapshots.RemoveAll(snapshot => snapshot != null && string.Equals(snapshot.toolId, toolId.Trim(), StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public static void ClearAll()
        {
            EnsureLoaded();
            Snapshots.Clear();
            Save();
        }

        public static string GetLastScanAgeLabel(string toolId)
        {
            if (!TryGetSnapshot(toolId, out PungentScanResultSnapshot snapshot))
                return "never scanned";
            DateTime completed = FromTicks(snapshot.completedUtcTicks);
            if (completed == default(DateTime))
                return "unknown age";
            TimeSpan age = DateTime.UtcNow - completed.ToUniversalTime();
            if (age.TotalMinutes < 1d)
                return "just now";
            if (age.TotalHours < 1d)
                return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalMinutes)) + " min ago";
            if (age.TotalDays < 1d)
                return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalHours)) + " hr ago";
            return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalDays)) + " day(s) ago";
        }

        public static string GetFreshnessLabel(string toolId)
        {
            PungentProjectAuditIndexEntry entry = PungentProjectAuditIndex.Get(toolId);
            if ((entry == null || !entry.HasResult) && !TryGetSnapshot(toolId, out _))
                return "No cache";
            return entry != null && entry.Stale ? "Stale" : "Fresh";
        }

        internal static DateTime FromTicks(long ticks)
        {
            if (ticks <= 0)
                return default(DateTime);
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch
            {
                return default(DateTime);
            }
        }

        private static PungentScanResultSnapshot CreateSnapshot(PungentScanResult result)
        {
            PungentScanResultSnapshot snapshot = new PungentScanResultSnapshot
            {
                toolId = result.ToolId,
                displayName = result.DisplayName,
                scope = result.Scope.ToString(),
                scopeLabel = result.ScopeLabel,
                startedUtcTicks = result.StartedAtUtc.ToUniversalTime().Ticks,
                completedUtcTicks = result.CompletedAtUtc.ToUniversalTime().Ticks,
                durationSeconds = result.DurationSeconds,
                statusMessage = result.StatusMessage,
                totalScanned = result.TotalScanned,
                totalMatched = result.TotalMatched,
                totalSkipped = result.TotalSkipped,
                totalChanged = result.TotalChanged,
                wasCancelled = result.WasCanceled,
                totalIssueCount = result.Issues != null ? result.Issues.Count : 0,
                summary = new PungentScanSummarySnapshot
                {
                    totalScanned = result.Summary.TotalScanned,
                    totalMatched = result.Summary.TotalMatched,
                    totalSkipped = result.Summary.TotalSkipped,
                    totalChanged = result.Summary.TotalChanged,
                    infoCount = result.Summary.InfoCount,
                    successCount = result.Summary.SuccessCount,
                    warningCount = result.Summary.WarningCount,
                    errorCount = result.Summary.ErrorCount,
                    wasCancelled = result.Summary.WasCanceled,
                    durationSeconds = result.Summary.DurationSeconds
                }
            };

            int issueLimit = Mathf.Min(snapshot.totalIssueCount, MaxPersistedIssuesPerResult);
            snapshot.issueListTruncated = snapshot.totalIssueCount > issueLimit;
            for (int i = 0; i < issueLimit; i++)
            {
                PungentScanIssue issue = result.Issues[i];
                if (issue != null)
                    snapshot.issues.Add(CreateIssueSnapshot(result.ToolId, issue));
            }
            snapshot.storedIssueCount = snapshot.issues.Count;
            return snapshot;
        }

        private static PungentScanIssueSnapshot CreateIssueSnapshot(string toolId, PungentScanIssue issue)
        {
            string globalId = string.Empty;
            if (issue.Context != null && EditorUtility.IsPersistent(issue.Context))
            {
                try
                {
                    globalId = GlobalObjectId.GetGlobalObjectIdSlow(issue.Context).ToString();
                }
                catch
                {
                    globalId = string.Empty;
                }
            }

            return new PungentScanIssueSnapshot
            {
                severity = issue.Severity.ToString(),
                title = issue.Title,
                message = issue.Message,
                assetPath = issue.Path,
                issueCode = issue.Code,
                createdUtcTicks = issue.CreatedAtUtc.ToUniversalTime().Ticks,
                contextObjectName = issue.Context == null ? string.Empty : issue.Context.name,
                contextGlobalObjectId = globalId,
                sourceToolId = toolId
            };
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            Snapshots.Clear();
            if (!File.Exists(StorePath))
                return;
            try
            {
                string json = File.ReadAllText(StorePath);
                SnapshotCollection collection = JsonUtility.FromJson<SnapshotCollection>(json);
                if (collection != null && collection.snapshots != null)
                    Snapshots.AddRange(collection.snapshots);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Pungent scan snapshot store could not be loaded: " + ex.Message);
            }
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(StorePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(StorePath, JsonUtility.ToJson(new SnapshotCollection { snapshots = Snapshots }, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Pungent scan snapshot store could not be saved: " + ex.Message);
            }
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }

        private static string StorePath => Path.Combine(Directory.GetCurrentDirectory(), "Library", "PungentFunkUtilities", "PungentScanSnapshots.json");

        [Serializable]
        private sealed class SnapshotCollection
        {
            public List<PungentScanResultSnapshot> snapshots = new List<PungentScanResultSnapshot>();
        }
    }
#endif
}
