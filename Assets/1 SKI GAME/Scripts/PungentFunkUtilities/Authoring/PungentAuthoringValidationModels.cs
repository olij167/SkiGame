using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Authoring
{
    public enum PungentAuthoringValidationSeverity
    {
        None = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }

    public enum PungentAuthoringValidationStatus
    {
        NotRun = 0,
        Valid = 1,
        ValidWithWarnings = 2,
        Invalid = 3,
        MissingProvider = 4,
        MissingTarget = 5,
        Unsupported = 6
    }

    [Serializable]
    public sealed class PungentAuthoringValidationIssue
    {
        public string providerId = string.Empty;
        public string itemId = string.Empty;
        public PungentAuthoringTarget target;
        public PungentAuthoringValidationSeverity severity = PungentAuthoringValidationSeverity.Info;
        public string message = string.Empty;
        public string suggestedActionLabel = string.Empty;
        public string sourcePathKeyOrId = string.Empty;
        public string issueCode = string.Empty;

        public static PungentAuthoringValidationIssue Create(
            PungentAuthoringValidationSeverity severity,
            string message,
            string providerId = null,
            string itemId = null,
            PungentAuthoringTarget target = null,
            string suggestedActionLabel = null,
            string sourcePathKeyOrId = null,
            string issueCode = null)
        {
            return new PungentAuthoringValidationIssue
            {
                providerId = providerId ?? string.Empty,
                itemId = itemId ?? string.Empty,
                target = target,
                severity = severity,
                message = message ?? string.Empty,
                suggestedActionLabel = suggestedActionLabel ?? string.Empty,
                sourcePathKeyOrId = sourcePathKeyOrId ?? string.Empty,
                issueCode = issueCode ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PungentAuthoringValidationResult
    {
        public string providerId = string.Empty;
        public string itemId = string.Empty;
        public PungentAuthoringValidationStatus status = PungentAuthoringValidationStatus.NotRun;
        public List<PungentAuthoringValidationIssue> issues = new List<PungentAuthoringValidationIssue>();

        public bool IsValid => status == PungentAuthoringValidationStatus.Valid ||
                               status == PungentAuthoringValidationStatus.ValidWithWarnings;

        public void AddIssue(PungentAuthoringValidationIssue issue)
        {
            if (issue == null)
                return;

            issues.Add(issue);
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            bool hasWarning = false;
            bool hasError = false;
            bool missingProvider = false;
            bool missingTarget = false;
            bool unsupported = false;

            for (int i = 0; i < issues.Count; i++)
            {
                PungentAuthoringValidationIssue issue = issues[i];
                if (issue == null)
                    continue;

                hasWarning |= issue.severity == PungentAuthoringValidationSeverity.Warning;
                hasError |= issue.severity == PungentAuthoringValidationSeverity.Error;
                missingProvider |= string.Equals(issue.issueCode, "MISSING_PROVIDER", StringComparison.OrdinalIgnoreCase);
                missingTarget |= string.Equals(issue.issueCode, "MISSING_TARGET", StringComparison.OrdinalIgnoreCase);
                unsupported |= string.Equals(issue.issueCode, "UNSUPPORTED_TARGET", StringComparison.OrdinalIgnoreCase);
            }

            if (missingProvider)
                status = PungentAuthoringValidationStatus.MissingProvider;
            else if (missingTarget)
                status = PungentAuthoringValidationStatus.MissingTarget;
            else if (unsupported)
                status = PungentAuthoringValidationStatus.Unsupported;
            else if (hasError)
                status = PungentAuthoringValidationStatus.Invalid;
            else if (hasWarning)
                status = PungentAuthoringValidationStatus.ValidWithWarnings;
            else
                status = PungentAuthoringValidationStatus.Valid;
        }

        public static PungentAuthoringValidationResult CreateValid(string providerId = null, string itemId = null)
        {
            return new PungentAuthoringValidationResult
            {
                providerId = providerId ?? string.Empty,
                itemId = itemId ?? string.Empty,
                status = PungentAuthoringValidationStatus.Valid
            };
        }
    }
}
