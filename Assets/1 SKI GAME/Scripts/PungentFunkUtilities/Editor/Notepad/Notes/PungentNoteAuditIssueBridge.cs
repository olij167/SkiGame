using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;

    public static class PungentNoteAuditIssueBridge
    {
        public static string GetIssueCode(PungentUtilityDesignAudit.Issue issue)
        {
            if (issue == null)
                return "DESIGN_AUDIT_UNKNOWN";

            string input = (issue.area ?? string.Empty) + "|" + (issue.target ?? string.Empty) + "|" + (issue.message ?? string.Empty) + "|" + (issue.assetPath ?? string.Empty);
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return "DESIGN_AUDIT_" + BitConverter.ToString(hash, 0, 5).Replace("-", string.Empty);
            }
        }

        public static PungentNote FindExisting(PungentUtilityDesignAudit.Issue issue, bool includeArchived = true)
        {
            string code = GetIssueCode(issue);
            return PungentNoteStorage.Database.notes.FirstOrDefault(n => n != null && (includeArchived || !n.archived) && string.Equals(n.auditIssueCode, code, StringComparison.OrdinalIgnoreCase));
        }

        public static PungentNote CreateOrOpen(PungentUtilityDesignAudit.Issue issue)
        {
            PungentNote existing = FindExisting(issue, true);
            if (existing != null && !existing.archived)
                return existing;

            PungentNote note = PungentNoteStorage.Database.CreateNote(BuildTitle(issue), PungentNoteKind.AuditFollowUp);
            note.auditIssueCode = GetIssueCode(issue);
            note.stableKey = note.auditIssueCode;
            note.status = PungentNoteStatus.ToDo;
            note.priority = PriorityFor(issue);
            note.tags = BuildTags(issue);
            note.body = BuildBody(issue);
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.AuditIssue,
                label = issue != null ? issue.target : "Audit issue",
                auditIssueCode = note.auditIssueCode
            });

            if (issue != null && !string.IsNullOrWhiteSpace(issue.assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(issue.assetPath);
                note.targets.Add(new PungentNoteTargetLink
                {
                    type = issue.assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? PungentNoteTargetType.ScriptPath : PungentNoteTargetType.Asset,
                    label = issue.assetPath,
                    assetGuid = guid,
                    scriptPath = issue.assetPath
                });
            }

            PungentNoteStorage.Save();
            return note;
        }

        public static string BuildAuditSummaryBody(PungentUtilityDesignAudit.Report report)
        {
            if (report == null)
                return "Audit summary was created before a report was available.";

            IEnumerable<IGrouping<string, PungentUtilityDesignAudit.Issue>> topAreas = report.issues
                .GroupBy(i => i.area)
                .OrderByDescending(g => g.Count())
                .Take(5);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Design Validation Audit summary");
            sb.AppendLine("Generated UTC: " + report.generatedUtc.ToString("u"));
            sb.AppendLine("Errors: " + report.ErrorCount);
            sb.AppendLine("Warnings: " + report.WarningCount);
            sb.AppendLine("Info: " + report.InfoCount);
            sb.AppendLine("Developer blockers: " + report.developerReleaseBlockerCount);
            sb.AppendLine();
            sb.AppendLine("Top issue areas:");
            foreach (IGrouping<string, PungentUtilityDesignAudit.Issue> area in topAreas)
                sb.AppendLine("- " + area.Key + ": " + area.Count());

            PungentUtilityReleaseReadiness.Assessment assessment = PungentUtilityReleaseReadiness.Assess(report);
            if (assessment != null)
            {
                sb.AppendLine();
                sb.AppendLine("Suggested next pass:");
                sb.AppendLine(assessment.headline);
                sb.AppendLine(assessment.details);
            }

            return sb.ToString();
        }

        private static string BuildTitle(PungentUtilityDesignAudit.Issue issue)
        {
            if (issue == null)
                return "Design Audit Follow-up";

            return string.IsNullOrWhiteSpace(issue.target)
                ? issue.area + " Follow-up"
                : issue.target + " - " + issue.area;
        }

        private static string BuildBody(PungentUtilityDesignAudit.Issue issue)
        {
            if (issue == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Issue Area: " + issue.area);
            sb.AppendLine("Issue Target: " + issue.target);
            sb.AppendLine("Severity: " + issue.severity);
            sb.AppendLine("Message: " + issue.message);
            if (!string.IsNullOrWhiteSpace(issue.recommendation))
                sb.AppendLine("Recommendation: " + issue.recommendation);
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                sb.AppendLine("Asset Path: " + issue.assetPath);
            sb.AppendLine("Audit Issue Code: " + GetIssueCode(issue));
            return sb.ToString();
        }

        private static List<string> BuildTags(PungentUtilityDesignAudit.Issue issue)
        {
            string severity = issue == null ? "unknown" : issue.severity.ToString().ToLowerInvariant();
            string area = issue == null ? "audit" : Slug(issue.area);
            return new List<string> { "audit", "design-validation", "severity-" + severity, area };
        }

        private static PungentNotePriority PriorityFor(PungentUtilityDesignAudit.Issue issue)
        {
            if (issue == null)
                return PungentNotePriority.Important;
            switch (issue.severity)
            {
                case PungentUtilityDesignAudit.Severity.Error: return PungentNotePriority.Crucial;
                case PungentUtilityDesignAudit.Severity.Warning: return PungentNotePriority.Important;
                default: return PungentNotePriority.NiceToHave;
            }
        }

        private static string Slug(string value)
        {
            string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
            char[] chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            return new string(chars).Trim('-').Replace("--", "-");
        }
    }
#endif
}
