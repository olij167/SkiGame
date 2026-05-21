namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Serializable-by-convention scan issue row used by shared scanner result views.
    /// </summary>
    public sealed class PungentScanIssue
    {
        public PungentScanIssue(
            PungentScanSeverity severity,
            string title,
            string message,
            Object context = null,
            string path = null,
            string code = null)
        {
            Severity = severity;
            Title = string.IsNullOrWhiteSpace(title) ? severity.ToString() : title.Trim();
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            Context = context;
            Path = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
            Code = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
            CreatedAtUtc = DateTime.UtcNow;
        }

        public PungentScanSeverity Severity { get; private set; }
        public string Title { get; private set; }
        public string Message { get; private set; }
        public Object Context { get; private set; }
        public string Path { get; private set; }
        public string Code { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }

        public string SearchText
        {
            get
            {
                string contextName = Context == null ? string.Empty : Context.name;
                return string.Concat(Severity, " ", Title, " ", Message, " ", contextName, " ", Path, " ", Code);
            }
        }

        internal static PungentScanIssue Restore(
            PungentScanSeverity severity,
            string title,
            string message,
            string path,
            string code,
            DateTime createdAtUtc)
        {
            PungentScanIssue issue = new PungentScanIssue(severity, title, message, null, path, code);
            issue.CreatedAtUtc = createdAtUtc == default(DateTime) ? DateTime.UtcNow : createdAtUtc;
            return issue;
        }
    }
#endif
}
