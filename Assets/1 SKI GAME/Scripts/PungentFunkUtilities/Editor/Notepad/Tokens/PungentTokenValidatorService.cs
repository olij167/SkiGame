using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Scanning;
    using UnityEditor;
    using UnityEngine;

    public static class PungentTokenValidatorService
    {
        public static List<PungentTokenUsage> LastUsages { get; private set; } = new List<PungentTokenUsage>();

        public static List<PungentTokenUsage> ValidateText(
    string text,
    string sourceLabel,
    string sourcePath = null,
    string noteId = null,
    string assetGuid = null,
    bool remember = true,
    bool includeInvalidFormat = true)
        {
            PungentTokenDatabase db = PungentTokenStorage.Database;
            List<PungentTokenUsage> usages = new List<PungentTokenUsage>();

            foreach (PungentParsedToken parsed in PungentTokenParser.Parse(text))
            {
                string key = PungentTokenParser.NormalizeKey(parsed.key);
                bool validKey = PungentTokenParser.IsValidKey(key);

                if (!validKey && !includeInvalidFormat)
                    continue;

                PungentTokenDefinition token = validKey ? db.FindToken(key) : null;

                PungentTokenUsage usage = new PungentTokenUsage
                {
                    tokenKey = key,
                    sourceLabel = sourceLabel ?? string.Empty,
                    sourcePath = sourcePath ?? string.Empty,
                    noteId = noteId ?? string.Empty,
                    assetGuid = assetGuid ?? string.Empty,
                    occurrenceCount = parsed.occurrenceCount,
                    contexts = parsed.contexts
                };

                if (!validKey)
                {
                    usage.status = PungentTokenUsageStatus.InvalidFormat;
                    usage.message = "Token key has invalid characters.";
                }
                else if (token == null)
                {
                    usage.status = PungentTokenUsageStatus.Unknown;
                    usage.message = "Token is not defined.";
                }
                else if (token.deprecated)
                {
                    usage.status = PungentTokenUsageStatus.Deprecated;
                    usage.message = string.IsNullOrWhiteSpace(token.replacementKey)
                        ? "Token is deprecated."
                        : "Use {" + token.replacementKey + "} instead.";
                }
                else
                {
                    usage.status = PungentTokenUsageStatus.Known;
                    usage.message = "Known token.";
                }

                usages.Add(usage);
            }

            if (remember)
                LastUsages = usages;

            return usages;
        }

        public static void AddUsagesToScan(
    PungentScanResult scan,
    List<PungentTokenUsage> usages,
    Object context = null,
    bool includeKnownTokens = false,
    int maxIssues = 200)
        {
            if (scan == null || usages == null)
                return;

            foreach (PungentTokenUsage usage in usages)
            {
                if (usage == null)
                    continue;

                // Successful token rows are useful for detailed usage views, but they should
                // not flood the shared scan issue list. Large issue lists are expensive to
                // redraw and can make the editor feel like it is repainting forever.
                if (usage.status == PungentTokenUsageStatus.Known && !includeKnownTokens)
                    continue;

                if (maxIssues > 0 && scan.Issues.Count >= maxIssues)
                    return;

                if (maxIssues > 0 && scan.Issues.Count == maxIssues - 1)
                {
                    scan.AddIssue(
                        PungentScanSeverity.Info,
                        "Additional token rows omitted",
                        "The scan found more token issues than are shown here. Narrow the scan filter or validate a smaller selection for full detail.",
                        null,
                        null,
                        "TOKEN_ISSUES_TRUNCATED");
                    return;
                }

                scan.AddIssue(
                    SeverityFor(usage.status),
                    TitleFor(usage.status),
                    usage.sourceLabel + " -> {" + usage.tokenKey + "} " + usage.message,
                    context,
                    usage.sourcePath,
                    "TOKEN_" + usage.status.ToString().ToUpperInvariant());
            }
        }

        public static List<PungentTokenUsage> ValidateNotes(PungentScanResult scan)
        {
            List<PungentTokenUsage> all = new List<PungentTokenUsage>();
            foreach (PungentNote note in PungentNoteStorage.Database.notes.Where(n => n != null && !n.archived))
            {
                List<PungentTokenUsage> usages = ValidateText(note.body, note.title, null, note.id, null, false);
                all.AddRange(usages);
                AddUsagesToScan(scan, usages);
                if (note.linkedTokenKeys != null)
                {
                    foreach (string key in note.linkedTokenKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    {
                        if (!PungentTokenStorage.Database.ContainsToken(key))
                        {
                            PungentTokenUsage usage = new PungentTokenUsage
                            {
                                tokenKey = PungentTokenParser.NormalizeKey(key),
                                sourceLabel = note.title,
                                noteId = note.id,
                                status = PungentTokenUsageStatus.Unknown,
                                message = "Linked token key is not defined.",
                                occurrenceCount = 1
                            };
                            all.Add(usage);
                            scan?.AddIssue(PungentScanSeverity.Warning, "Unknown linked token", note.title + " -> {" + usage.tokenKey + "}", null, null, "TOKEN_LINKED_UNKNOWN");
                        }
                    }
                }
            }
            LastUsages = all;
            return all;
        }

        public static void ValidateDefinitions(PungentScanResult scan)
        {
            PungentTokenDatabase db = PungentTokenStorage.Database;
            HashSet<string> keys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (PungentTokenDefinition token in db.tokens.Where(t => t != null))
            {
                string key = PungentTokenParser.NormalizeKey(token.key);
                if (!PungentTokenParser.IsValidKey(key))
                    scan?.AddIssue(PungentScanSeverity.Error, "Invalid token key", "Token key is empty or invalid: " + token.key, null, null, "TOKEN_INVALID_KEY");
                else if (!keys.Add(key))
                    scan?.AddIssue(PungentScanSeverity.Error, "Duplicate token", "Duplicate token definition: {" + key + "}", null, null, "TOKEN_DUPLICATE");
                if (token.deprecated && !string.IsNullOrWhiteSpace(token.replacementKey) && db.FindToken(token.replacementKey) == null)
                    scan?.AddIssue(PungentScanSeverity.Warning, "Replacement missing", "Deprecated token {" + key + "} references missing replacement {" + token.replacementKey + "}.", null, null, "TOKEN_REPLACEMENT_MISSING");
                if (token.required && string.IsNullOrWhiteSpace(token.previewValue) && (token.examples == null || token.examples.Count == 0))
                    scan?.AddIssue(PungentScanSeverity.Warning, "Required token incomplete", "Required token {" + key + "} has no preview value or examples.", null, null, "TOKEN_REQUIRED_INCOMPLETE");
            }
        }

        public static void AddProjectUsagesToScan(
            PungentScanResult scan,
            List<PungentTokenUsage> usages,
            List<PungentTokenCandidate> candidates,
            PungentTokenScanSettings settings,
            Object context = null,
            int maxIssues = 200)
        {
            if (scan == null)
                return;

            settings = settings ?? new PungentTokenScanSettings();
            settings.EnsureDefaults();

            if (usages != null)
            {
                foreach (PungentTokenUsage usage in usages)
                {
                    if (usage == null)
                        continue;

                    if (usage.status == PungentTokenUsageStatus.Known ||
                        usage.status == PungentTokenUsageStatus.Unknown ||
                        usage.status == PungentTokenUsageStatus.InvalidFormat)
                    {
                        continue;
                    }

                    if (!TryReserveIssueSlot(scan, maxIssues))
                        return;

                    scan.AddIssue(
                        SeverityFor(usage.status),
                        TitleFor(usage.status),
                        usage.sourceLabel + " -> {" + usage.tokenKey + "} " + usage.message,
                        context,
                        usage.sourcePath,
                        "TOKEN_" + usage.status.ToString().ToUpperInvariant());
                }
            }

            if (!settings.reportLikelyUnknownsAsWarnings || candidates == null)
                return;

            foreach (PungentTokenCandidate candidate in candidates)
            {
                if (candidate == null ||
                    candidate.ignored ||
                    candidate.intentLevel != PungentTokenIntentLevel.Likely)
                {
                    continue;
                }

                if (!TryReserveIssueSlot(scan, maxIssues))
                    return;

                scan.AddIssue(
                    PungentScanSeverity.Warning,
                    "Likely unknown token",
                    candidate.sourceLabel + " -> {" + candidate.tokenKey + "} " + candidate.confidence + "% confidence. " + candidate.reason,
                    context,
                    candidate.sourcePath,
                    "TOKEN_UNKNOWN_LIKELY");
            }
        }

        public static void ValidateBindings(PungentScanResult scan)
        {
            foreach (PungentTokenBinding binding in PungentTokenStorage.Database.bindings.Where(b => b != null && !b.archived))
            {
                if (string.IsNullOrWhiteSpace(binding.tokenKey) || !PungentTokenStorage.Database.ContainsToken(binding.tokenKey))
                    scan?.AddIssue(PungentScanSeverity.Warning, "Broken binding", "Binding target '" + binding.label + "' references missing token {" + binding.tokenKey + "}.", null, null, "TOKEN_BINDING_MISSING_TOKEN");
                if (!string.IsNullOrWhiteSpace(binding.assetGuid) && string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(binding.assetGuid)))
                    scan?.AddIssue(PungentScanSeverity.Warning, "Broken binding", "Asset binding no longer resolves: " + binding.label, null, null, "TOKEN_BINDING_ASSET_MISSING");
                if (binding.targetType == PungentTokenTargetType.SerializedProperty && string.IsNullOrWhiteSpace(binding.propertyPath))
                    scan?.AddIssue(PungentScanSeverity.Warning, "Incomplete binding", "Serialized property binding has no property path: " + binding.label, null, null, "TOKEN_BINDING_PROPERTY_MISSING");
            }
        }

        private static PungentScanSeverity SeverityFor(PungentTokenUsageStatus status)
        {
            switch (status)
            {
                case PungentTokenUsageStatus.Known: return PungentScanSeverity.Success;
                case PungentTokenUsageStatus.Deprecated: return PungentScanSeverity.Warning;
                case PungentTokenUsageStatus.Unknown:
                case PungentTokenUsageStatus.InvalidFormat:
                case PungentTokenUsageStatus.BrokenBinding:
                    return PungentScanSeverity.Warning;
                default: return PungentScanSeverity.Info;
            }
        }

        private static string TitleFor(PungentTokenUsageStatus status)
        {
            return status == PungentTokenUsageStatus.Known ? "Token valid" : status.ToString();
        }

        private static bool TryReserveIssueSlot(PungentScanResult scan, int maxIssues)
        {
            if (scan == null)
                return false;

            if (maxIssues <= 0)
                return true;

            if (scan.Issues.Count >= maxIssues)
                return false;

            if (scan.Issues.Count == maxIssues - 1)
            {
                scan.AddIssue(
                    PungentScanSeverity.Info,
                    "Additional token rows omitted",
                    "The scan found more token issues than are shown here. Narrow the scan filter or validate a smaller selection for full detail.",
                    null,
                    null,
                    "TOKEN_ISSUES_TRUNCATED");
                return false;
            }

            return true;
        }
    }
#endif
}
