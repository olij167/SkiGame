using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentPropagationApplySummary
    {
        public int appliedTokens;
        public int appliedBoundValues;
        public int skipped;
        public int disabled;
        public string message = string.Empty;

        public string ToStatus()
        {
            return appliedTokens + " token value(s), " + appliedBoundValues + " bound value(s) applied, " + skipped + " skipped, " + disabled + " disabled.";
        }
    }

    public static class PungentRichDocumentPropagationApplyService
    {
        public static bool CanApplySelected(IEnumerable<PungentRichDocumentExtractedEntry> entries)
        {
            List<PungentRichDocumentExtractedEntry> safeEntries = (entries ?? Enumerable.Empty<PungentRichDocumentExtractedEntry>()).ToList();
            bool tokenApplyAvailable = PungentUtilityRegistry.Find("token-validator") != null &&
                                       safeEntries.Any(entry => entry != null && entry.selected && entry.canApply && IsTokenEntry(entry));
            bool boundApplyAvailable = safeEntries.Any(entry => entry != null && entry.selected && entry.canApply && IsBoundEntry(entry));
            return tokenApplyAvailable || boundApplyAvailable;
        }

        public static PungentRichDocumentPropagationApplySummary ApplySelected(PungentRichDocument document, IEnumerable<PungentRichDocumentExtractedEntry> entries)
        {
            PungentRichDocumentPropagationApplySummary summary = new PungentRichDocumentPropagationApplySummary();
            List<PungentRichDocumentExtractedEntry> selected = (entries ?? Enumerable.Empty<PungentRichDocumentExtractedEntry>())
                .Where(entry => entry != null && entry.selected)
                .ToList();

            summary.disabled = selected.Count(entry => !entry.canApply);
            summary.appliedTokens = ApplySelectedTokenEntries(selected);
            summary.appliedBoundValues = ApplySelectedBoundEntries(document, selected, out int skipped, out string message);
            summary.skipped = skipped;
            summary.message = string.IsNullOrWhiteSpace(message) ? summary.ToStatus() : message;
            return summary;
        }

        public static bool IsTokenEntry(PungentRichDocumentExtractedEntry entry)
        {
            return entry != null && string.Equals(entry.targetType, PungentRichDocumentParser.TargetTokenPreviewValue, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBoundEntry(PungentRichDocumentExtractedEntry entry)
        {
            return entry != null &&
                   entry.target != null &&
                   entry.target.HasTarget &&
                   !string.IsNullOrWhiteSpace(entry.semanticBindingId);
        }

        public static void PingTarget(PungentRichDocumentExtractedEntry entry, Action<string> status = null)
        {
            if (entry == null || entry.target == null)
                return;

            if (PungentRichDocumentBindingApplicationService.TryResolveUnityTarget(entry.target, out UnityEngine.Object targetObject, out string error) && targetObject != null)
            {
                Selection.activeObject = targetObject;
                EditorGUIUtility.PingObject(targetObject);
                status?.Invoke("Pinged " + targetObject.name + ".");
                return;
            }

            status?.Invoke(string.IsNullOrWhiteSpace(error) ? "Target could not be resolved." : error);
        }

        private static int ApplySelectedTokenEntries(List<PungentRichDocumentExtractedEntry> entries)
        {
            if (PungentUtilityRegistry.Find("token-validator") == null)
                return 0;

            PungentTokenStorage.EnsureLoaded();
            int applied = 0;
            foreach (PungentRichDocumentExtractedEntry entry in entries.Where(entry => entry != null && entry.canApply && IsTokenEntry(entry)))
            {
                string key = PungentTokenDatabase.NormalizeKey(entry.key);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(entry.value))
                    continue;

                PungentTokenDefinition token = PungentTokenStorage.Database.FindToken(key) ?? PungentTokenStorage.Database.AddToken(key);
                token.previewValue = entry.value.Trim();
                if (string.IsNullOrWhiteSpace(token.category))
                    token.category = "Rich Documents";
                if (string.IsNullOrWhiteSpace(token.source))
                    token.source = "rich-document-editor";
                if (token.examples == null)
                    token.examples = new List<string>();
                string example = "{" + key + "}";
                if (!token.examples.Any(item => string.Equals(item, example, StringComparison.OrdinalIgnoreCase)))
                    token.examples.Add(example);
                token.updatedUtc = DateTime.UtcNow.ToString("o");
                applied++;
            }

            if (applied > 0)
                PungentTokenStorage.Save();

            return applied;
        }

        private static int ApplySelectedBoundEntries(PungentRichDocument document, List<PungentRichDocumentExtractedEntry> entries, out int skipped, out string message)
        {
            int applied = 0;
            skipped = 0;
            message = string.Empty;
            foreach (PungentRichDocumentExtractedEntry entry in entries.Where(entry => entry != null && entry.canApply && IsBoundEntry(entry)))
            {
                PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindBinding(document, entry.semanticBindingId);
                PungentRichDocumentBindingApplyResult result = PungentRichDocumentBindingApplicationService.Apply(binding, entry.value ?? string.Empty);
                if (result != null && result.applied)
                {
                    applied++;
                    continue;
                }

                skipped++;
                if (result != null && !string.IsNullOrWhiteSpace(result.message))
                    message = result.message;
            }

            return applied;
        }
    }
#endif
}
