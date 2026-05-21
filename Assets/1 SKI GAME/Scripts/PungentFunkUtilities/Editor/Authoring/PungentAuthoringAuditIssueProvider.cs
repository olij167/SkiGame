using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringAuditIssueProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider
    {
        public const string Id = "cached-audit-issues";

        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.AuditIssue };
        private static readonly string[] KnownCachedProviderIds =
        {
            "terrain-usage-scanner",
            "reference-assignment-scanner",
            "audio-setup-coverage",
            "audio-catalog-coverage",
            "coverage-matrix",
            "token-validator",
            "design-validation-audit"
        };

        public string ProviderId => Id;
        public string DisplayName => "Cached Audit Issues";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Copy;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.ProjectAudit;
        public string ExtensionId => PungentAuthoringPackageCapabilities.ProjectAudit;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (CachedIssue issue in EnumerateCachedIssues())
                yield return ToMetadata(issue);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            CachedIssue issue = Find(reference?.itemId);
            if (issue == null)
                return false;

            metadata = ToMetadata(issue);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            yield break;
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            CachedIssue issue = Find(reference?.itemId);
            if (issue == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(issue.issue.Code))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AuditIssueCode, issue.issue.Code, "Issue Code", Id);
            if (!string.IsNullOrWhiteSpace(issue.issue.Path))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.ScriptPath, issue.issue.Path, "Issue Path", Id);
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            CachedIssue issue = Find(reference?.itemId);
            if (issue == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(issue), GetTargets(reference).Count());
            preview.subtitle = issue.providerDisplayName + " - " + issue.issue.Severity;
            preview.bodyPreview = issue.issue.Message;
            preview.warning = issue.issue.Severity == PungentScanSeverity.Warning || issue.issue.Severity == PungentScanSeverity.Error;
            preview.warningLabel = issue.issue.Severity.ToString();
            preview.primaryActionLabels.Add("Open Project Audit");
            preview.primaryActionLabels.Add("Copy Issue");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference?.itemId) == null ? "Cached audit issue could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            CachedIssue issue = Find(reference?.itemId);
            if (issue == null)
                return false;

            PungentUtilityDesignAuditWindow.OpenAndFocusCachedIssue(
                issue.providerId,
                issue.issue != null ? issue.issue.Code : string.Empty,
                issue.issue != null ? issue.issue.Title : string.Empty,
                issue.issue != null ? issue.issue.Path : string.Empty,
                issue.issue != null ? issue.issue.Message : string.Empty);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            reason = "Audit issues are produced by scanners and cannot be edited from the authoring provider.";
            return false;
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return false;
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Audit issue creation is owned by explicit scanner runs.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Audit issue creation is owned by explicit scanner runs.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            CachedIssue issue = Find(reference?.itemId);
            if (issue == null)
            {
                copiedValue = string.Empty;
                error = "Cached audit issue could not be found.";
                return false;
            }

            copiedValue = issue.providerId + " | " + issue.issue.Severity + " | " + issue.issue.Title + " | " + issue.issue.Message;
            if (!string.IsNullOrWhiteSpace(issue.issue.Path))
                copiedValue += " | " + issue.issue.Path;
            EditorGUIUtility.systemCopyBuffer = copiedValue;
            error = string.Empty;
            return true;
        }

        private static PungentAuthoringMetadata ToMetadata(CachedIssue issue)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = issue.id,
                title = string.IsNullOrWhiteSpace(issue.issue.Title) ? issue.issue.Code : issue.issue.Title,
                summary = issue.issue.Message,
                kind = PungentAuthoringItemKind.AuditIssue,
                status = issue.issue.Severity.ToString(),
                priority = issue.issue.Severity == PungentScanSeverity.Error ? "High" : issue.issue.Severity == PungentScanSeverity.Warning ? "Medium" : "Low",
                visibility = "Cached",
                tags = PungentAuthoringMetadata.NormalizeTags(new[] { issue.providerId, issue.issue.Code, issue.issue.Severity.ToString() }),
                updatedUtc = issue.issue.CreatedAtUtc.ToUniversalTime().ToString("o"),
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.ProjectAudit,
                extensionId = PungentAuthoringPackageCapabilities.ProjectAudit
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static CachedIssue Find(string issueId)
        {
            if (string.IsNullOrWhiteSpace(issueId))
                return null;

            return EnumerateCachedIssues().FirstOrDefault(issue => string.Equals(issue.id, issueId, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<CachedIssue> EnumerateCachedIssues()
        {
            foreach (string providerId in KnownCachedProviderIds.Concat(PungentAuditScanProviderRegistry.Providers.Select(provider => provider.ProviderId)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(providerId))
                    continue;
                if (!PungentScanCache.TryGet(providerId, out PungentScanResult result) || result == null || result.Issues == null)
                    continue;

                foreach (PungentScanIssue issue in result.Issues)
                {
                    if (issue == null)
                        continue;

                    yield return new CachedIssue
                    {
                        id = BuildIssueId(providerId, issue),
                        providerId = providerId,
                        providerDisplayName = string.IsNullOrWhiteSpace(result.DisplayName) ? providerId : result.DisplayName,
                        issue = issue
                    };
                }
            }
        }

        private static string BuildIssueId(string providerId, PungentScanIssue issue)
        {
            string seed = (providerId ?? string.Empty) + "|" +
                          (issue?.Code ?? string.Empty) + "|" +
                          (issue?.Title ?? string.Empty) + "|" +
                          (issue?.Path ?? string.Empty) + "|" +
                          (issue?.Message ?? string.Empty);
            return (providerId ?? "audit").Trim() + ":" + StableHash(seed);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        private sealed class CachedIssue
        {
            public string id;
            public string providerId;
            public string providerDisplayName;
            public PungentScanIssue issue;
        }
    }
#endif
}
