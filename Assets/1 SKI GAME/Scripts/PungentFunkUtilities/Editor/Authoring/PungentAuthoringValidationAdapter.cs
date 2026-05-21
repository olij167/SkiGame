using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.ProjectAudit;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public static class PungentAuthoringValidationAdapter
    {
        public static PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = reference != null ? reference.providerId ?? string.Empty : string.Empty,
                itemId = reference != null ? reference.itemId ?? string.Empty : string.Empty
            };

            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    "Authoring reference has an empty ID.",
                    result.providerId,
                    result.itemId,
                    null,
                    "Set ID",
                    result.itemId,
                    "EMPTY_ID"));
                return result;
            }

            if (!PungentAuthoringProviderRegistry.TryGetMetadata(reference, out _))
            {
                IReadOnlyList<IPungentAuthoringProvider> providers = string.IsNullOrWhiteSpace(reference.providerId)
                    ? PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind)
                    : new[] { PungentAuthoringProviderRegistry.FindProvider(reference.providerId) }.Where(provider => provider != null).ToArray();

                bool providerMissing = providers.Count == 0;
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    providerMissing ? PungentAuthoringValidationSeverity.Error : PungentAuthoringValidationSeverity.Warning,
                    providerMissing ? PungentAuthoringProviderRegistry.MissingProviderMessage(reference) : "Authoring item could not be found.",
                    result.providerId,
                    result.itemId,
                    null,
                    providerMissing ? "Install provider" : "Refresh source",
                    reference.itemId,
                    providerMissing ? "MISSING_PROVIDER" : "MISSING_TARGET"));
                return result;
            }

            result.status = PungentAuthoringValidationStatus.Valid;
            return result;
        }

        public static PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = target != null ? target.providerId ?? string.Empty : string.Empty,
                itemId = target != null ? target.rawValue ?? string.Empty : string.Empty
            };

            if (target == null || !target.HasTarget)
            {
                AddMissingTarget(result, target, "Authoring target is empty.", "EMPTY_TARGET");
                return result;
            }

            target.NormalizeInPlace();
            switch (target.targetKind)
            {
                case PungentAuthoringTargetKind.NoteId:
                    ValidateNoteId(result, target);
                    break;
                case PungentAuthoringTargetKind.TokenKey:
                    ValidateTokenKey(result, target);
                    break;
                case PungentAuthoringTargetKind.UtilityId:
                    ValidateUtilityId(result, target);
                    break;
                case PungentAuthoringTargetKind.FutureUtilityId:
                    ValidateFutureUtilityId(result, target);
                    break;
                case PungentAuthoringTargetKind.DocumentationLinkId:
                    ValidateDocumentationLinkId(result, target);
                    break;
                case PungentAuthoringTargetKind.HelpTopicId:
                    ValidateHelpTopicId(result, target);
                    break;
                case PungentAuthoringTargetKind.AssetGuid:
                    ValidateAssetGuid(result, target);
                    break;
                case PungentAuthoringTargetKind.ScriptPath:
                case PungentAuthoringTargetKind.ExternalLocalPath:
                    ValidateLocalPath(result, target);
                    break;
                case PungentAuthoringTargetKind.ExternalWebUrl:
                    ValidateWebUrl(result, target);
                    break;
                case PungentAuthoringTargetKind.RichDocumentId:
                    ValidateProviderBackedTarget(result, target, PungentAuthoringItemKind.RichDocument);
                    break;
                case PungentAuthoringTargetKind.BoardId:
                    ValidateProviderBackedTarget(result, target, PungentAuthoringItemKind.Board);
                    break;
                case PungentAuthoringTargetKind.DataSheetId:
                    ValidateProviderBackedTarget(result, target, PungentAuthoringItemKind.DataSheet);
                    break;
                default:
                    result.status = PungentAuthoringValidationStatus.Valid;
                    break;
            }

            result.RefreshStatus();
            return result;
        }

        public static PungentAuthoringValidationResult ValidateLocal(IEnumerable<PungentAuthoringReference> references, IEnumerable<PungentAuthoringTarget> targets)
        {
            PungentAuthoringValidationResult aggregate = PungentAuthoringValidationResult.CreateValid();
            foreach (PungentAuthoringReference reference in references ?? Enumerable.Empty<PungentAuthoringReference>())
            {
                PungentAuthoringValidationResult result = ValidateReference(reference);
                foreach (PungentAuthoringValidationIssue issue in result.issues)
                    aggregate.AddIssue(issue);
            }

            foreach (PungentAuthoringTarget target in targets ?? Enumerable.Empty<PungentAuthoringTarget>())
            {
                PungentAuthoringValidationResult result = ValidateTarget(target);
                foreach (PungentAuthoringValidationIssue issue in result.issues)
                    aggregate.AddIssue(issue);
            }

            aggregate.RefreshStatus();
            return aggregate;
        }

        private static void ValidateNoteId(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            List<PungentNote> notes = PungentNoteDatabase.instance.notes ?? new List<PungentNote>();
            if (!notes.Any(note => note != null && PungentAuthoringId.EqualsId(note.id, target.rawValue)))
                AddMissingTarget(result, target, "Missing note ID '" + target.rawValue + "'.", "MISSING_NOTE_ID");
        }

        private static void ValidateFutureUtilityId(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            List<PungentFutureUtilityRecord> records = PungentNoteDatabase.instance.futureUtilities ?? new List<PungentFutureUtilityRecord>();
            if (!records.Any(record => record != null && PungentAuthoringId.EqualsId(record.id, target.rawValue)))
                AddMissingTarget(result, target, "Missing future utility ID '" + target.rawValue + "'.", "MISSING_FUTURE_UTILITY_ID");
        }

        private static void ValidateTokenKey(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            string clean = PungentTokenDatabase.NormalizeKey(target.rawValue);
            List<PungentTokenDefinition> tokens = PungentTokenDatabase.instance.tokens ?? new List<PungentTokenDefinition>();
            if (!tokens.Any(token => token != null && string.Equals(PungentTokenDatabase.NormalizeKey(token.key), clean, StringComparison.OrdinalIgnoreCase)))
                AddMissingTarget(result, target, "Missing token key '{" + clean + "}'.", "MISSING_TOKEN_KEY");
        }

        private static void ValidateUtilityId(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            if (PungentUtilityRegistry.Find(target.rawValue) == null)
                AddMissingTarget(result, target, "Missing registered utility ID '" + target.rawValue + "'.", "MISSING_UTILITY_ID");
        }

        private static void ValidateDocumentationLinkId(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            if (PungentUtilityDocumentationLinks.instance.FindById(target.rawValue) == null)
                AddMissingTarget(result, target, "Missing documentation link ID '" + target.rawValue + "'.", "STALE_DOCUMENTATION_LINK_ID");
        }

        private static void ValidateHelpTopicId(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            if (PungentUtilityHelpRegistry.FindByStableId(target.rawValue) == null)
                AddMissingTarget(result, target, "Missing help topic ID '" + target.rawValue + "'.", "MISSING_HELP_TOPIC_ID");
        }

        private static void ValidateAssetGuid(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            if (string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(target.rawValue)))
                AddMissingTarget(result, target, "Missing asset GUID '" + target.rawValue + "'.", "MISSING_ASSET_GUID");
        }

        private static void ValidateLocalPath(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            string path = target.rawValue;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                AddMissingTarget(result, target, "Missing local path '" + path + "'.", "MISSING_LOCAL_PATH");
        }

        private static void ValidateWebUrl(PungentAuthoringValidationResult result, PungentAuthoringTarget target)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(string.Empty, target.rawValue);
            if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.UnsupportedUrlScheme)
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    status.message,
                    result.providerId,
                    result.itemId,
                    target,
                    "Use http:// or https://",
                    target.rawValue,
                    "UNSUPPORTED_TARGET"));
            }
            else if (!status.canOpen)
            {
                AddMissingTarget(result, target, string.IsNullOrWhiteSpace(status.message) ? "Invalid web URL." : status.message, "MISSING_TARGET");
            }
        }

        private static void ValidateProviderBackedTarget(PungentAuthoringValidationResult result, PungentAuthoringTarget target, PungentAuthoringItemKind kind)
        {
            PungentAuthoringReference reference = PungentAuthoringReference.Create(kind, target.rawValue, target.providerId, target.label);
            PungentAuthoringValidationResult referenceResult = ValidateReference(reference);
            foreach (PungentAuthoringValidationIssue issue in referenceResult.issues)
                result.AddIssue(issue);
        }

        private static void AddMissingTarget(PungentAuthoringValidationResult result, PungentAuthoringTarget target, string message, string issueCode)
        {
            result.AddIssue(PungentAuthoringValidationIssue.Create(
                PungentAuthoringValidationSeverity.Error,
                message,
                result.providerId,
                result.itemId,
                target,
                "Refresh link",
                target != null ? target.rawValue : string.Empty,
                string.IsNullOrWhiteSpace(issueCode) ? "MISSING_TARGET" : issueCode));
        }
    }
#endif
}
