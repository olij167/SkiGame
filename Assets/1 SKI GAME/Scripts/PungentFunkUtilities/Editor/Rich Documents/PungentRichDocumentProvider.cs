using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider,
        IPungentAuthoringValidator,
        IPungentAuthoringConversionProvider
    {
        public const string Id = "rich-documents";
        public const string UtilityId = "rich-document-editor";
        public const string PackageDisplayName = "Documentation & Authoring";

        private static readonly PungentAuthoringItemKind[] Kinds =
        {
            PungentAuthoringItemKind.RichDocument
        };

        public string ProviderId => Id;
        public string DisplayName => "Rich Documents";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Edit |
            PungentAuthoringProviderCapabilities.Copy |
            PungentAuthoringProviderCapabilities.CreateFromContext |
            PungentAuthoringProviderCapabilities.Validate |
            PungentAuthoringProviderCapabilities.ConversionHooks;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.RichDocuments;
        public string ExtensionId => PungentAuthoringPackageCapabilities.RichDocuments;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentRichDocument document in PungentRichDocumentStorage.Database.documents ?? new List<PungentRichDocument>())
                if (document != null)
                    yield return ToMetadata(document);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentRichDocument document = Find(reference?.itemId);
            if (document == null)
                return false;

            metadata = ToMetadata(document);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentRichDocument document = Find(reference?.itemId);
            if (document == null)
                yield break;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(document.sourceLegacyNoteId))
            {
                PungentAuthoringReference source = PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, document.sourceLegacyNoteId, PungentAuthoringLegacyNoteProvider.Id, "Source Legacy Note");
                seen.Add(ReferenceKey(source));
                yield return source;
            }

            foreach (PungentAuthoringReference item in document.references ?? new List<PungentAuthoringReference>())
            {
                if (item == null)
                    continue;

                item.NormalizeInPlace();
                if (seen.Add(ReferenceKey(item)))
                    yield return item;
            }
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentRichDocument document = Find(reference?.itemId);
            if (document == null)
                yield break;

            foreach (PungentAuthoringTarget target in document.targets ?? new List<PungentAuthoringTarget>())
            {
                if (target == null)
                    continue;

                target.NormalizeInPlace();
                yield return target;
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentRichDocument document = Find(reference?.itemId);
            if (document == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(document), GetTargets(reference).Count());
            preview.subtitle = document.kind + " / " + document.templateId;
            preview.bodyPreview = string.IsNullOrWhiteSpace(document.summary)
                ? PungentRichDocument.BuildPreview(document.bodyText)
                : document.summary;
            preview.primaryActionLabels.Add("Open Editor");
            preview.primaryActionLabels.Add("Copy ID");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference?.itemId) == null ? "Rich document could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentRichDocument document = Find(reference?.itemId);
            if (document == null)
                return false;

            PungentRichDocumentEditorWindow.OpenAndSelect(document.id);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            return CanOpen(reference, out reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return Open(reference);
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = string.Empty;
            if (context == null)
            {
                reason = "Context target is missing.";
                return false;
            }

            if (context.targetKind == PungentAuthoringTargetKind.NoteId && FindLegacyNote(context.rawValue) != null)
                return true;

            reason = "Rich Document creation currently supports legacy note contexts only.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = string.Empty;
            if (!CanCreateFromContext(context, out error))
                return false;

            PungentNote note = FindLegacyNote(context.rawValue);
            if (!TryCreateFromLegacyNote(note, out PungentRichDocument document, out error, true))
                return false;

            createdReference = document.ToReference(Id);
            return true;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference?.itemId ?? string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No rich document ID to copy.";
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = copiedValue;
            return true;
        }

        public PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference)
        {
            return PungentRichDocumentEditorGUI.ValidateLocal(Find(reference?.itemId), Id);
        }

        public PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = Id,
                status = PungentAuthoringValidationStatus.Valid
            };

            if (target == null || !target.HasTarget)
                result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Target is missing.", Id, null, target, "Fill or remove the target.", null, "MISSING_TARGET"));

            result.RefreshStatus();
            return result;
        }

        public IEnumerable<PungentAuthoringAction> GetConversionActions(PungentAuthoringReference reference)
        {
            if (reference != null && reference.itemKind == PungentAuthoringItemKind.RichDocument)
            {
                PungentRichDocument document = Find(reference.itemId);
                if (document != null)
                {
                    yield return new PungentAuthoringAction
                    {
                        kind = PungentAuthoringActionKind.CreateChecklistFromRichDocument,
                        label = "Create Checklist Definition",
                        tooltip = "Create or update a project checklist definition from Markdown headings and checklist lines.",
                        providerId = Id,
                        extensionId = PungentAuthoringPackageCapabilities.Checklists,
                        contextReference = reference,
                        enabled = true
                    };
                }

                yield break;
            }

            if (!IsLegacyNoteReference(reference))
                yield break;

            PungentNote note = FindLegacyNote(reference.itemId);
            if (note == null)
                yield break;

            List<PungentRichDocument> existing = PungentRichDocumentStorage.Database.FindBySourceLegacyNoteId(note.id);
            if (existing.Count > 0)
            {
                yield return new PungentAuthoringAction
                {
                    kind = PungentAuthoringActionKind.Open,
                    label = "Open Converted Document",
                    tooltip = "Open the rich document previously created from this legacy note.",
                    providerId = Id,
                    extensionId = ExtensionId,
                    contextReference = reference,
                    enabled = true
                };
            }

            yield return new PungentAuthoringAction
            {
                kind = PungentAuthoringActionKind.ConvertLegacyNoteToRichDocument,
                label = "Create Rich Document from Legacy Note",
                tooltip = "Create a linked rich document copy. The legacy note body remains unchanged.",
                providerId = Id,
                extensionId = ExtensionId,
                contextReference = reference,
                enabled = true
            };
        }

        public bool TryRunConversionAction(PungentAuthoringAction action, out PungentAuthoringReference result, out string error)
        {
            result = null;
            error = string.Empty;
            if (action == null || !action.enabled)
            {
                error = action == null ? "Conversion action is missing." : action.disabledReason;
                return false;
            }

            PungentAuthoringReference context = action.contextReference;
            if (context != null && context.itemKind == PungentAuthoringItemKind.RichDocument)
            {
                if (action.kind != PungentAuthoringActionKind.CreateChecklistFromRichDocument)
                {
                    error = "Unsupported rich document conversion action.";
                    return false;
                }

                PungentRichDocument sourceDocument = Find(context.itemId);
                if (sourceDocument == null)
                {
                    error = "Rich Document could not be found.";
                    return false;
                }

                if (!PungentRichDocumentChecklistBridge.CreateOrUpdateChecklistDefinition(sourceDocument, out string checklistId, out error))
                    return false;

                result = PungentAuthoringReference.Create(PungentAuthoringItemKind.Checklist, checklistId, PungentChecklistConstants.ProviderId, sourceDocument.title);
                PungentRichDocumentStorage.Save(out _);
                PungentChecklistUtilityWindow.OpenChecklist(checklistId);
                return true;
            }

            if (!IsLegacyNoteReference(context))
            {
                error = "This conversion action requires a legacy note reference.";
                return false;
            }

            PungentNote note = FindLegacyNote(context.itemId);
            if (note == null)
            {
                error = "Legacy note could not be found.";
                return false;
            }

            if (action.kind == PungentAuthoringActionKind.Open)
            {
                PungentRichDocument existing = PungentRichDocumentStorage.Database.FindBySourceLegacyNoteId(note.id).FirstOrDefault();
                if (existing == null)
                {
                    error = "No converted rich document exists for this legacy note.";
                    return false;
                }

                result = existing.ToReference(Id);
                PungentRichDocumentEditorWindow.OpenAndSelect(existing.id);
                return true;
            }

            if (action.kind != PungentAuthoringActionKind.ConvertLegacyNoteToRichDocument)
            {
                error = "Unsupported rich document conversion action.";
                return false;
            }

            if (!TryCreateFromLegacyNote(note, out PungentRichDocument document, out error, true))
                return false;

            result = document.ToReference(Id);
            PungentRichDocumentEditorWindow.OpenAndSelect(document.id);
            return true;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentRichDocument document)
        {
            return document == null ? null : document.ToMetadata(Id);
        }

        public static bool TryCreateFromLegacyNote(PungentNote note, out PungentRichDocument document, out string error, bool saveImmediately)
        {
            return PungentRichDocumentNoteBridge.TryCreateLinkedCopy(note, out document, out error, saveImmediately);
        }

        private static PungentRichDocument Find(string documentId)
        {
            return PungentRichDocumentStorage.Database.Find(documentId);
        }

        private static PungentNote FindLegacyNote(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return null;

            PungentNoteStorage.EnsureLoaded();
            return PungentNoteStorage.Database.notes
                .FirstOrDefault(note => note != null && PungentAuthoringId.EqualsId(note.id, noteId));
        }

        private static bool IsLegacyNoteReference(PungentAuthoringReference reference)
        {
            return reference != null &&
                   !string.IsNullOrWhiteSpace(reference.itemId) &&
                   (reference.itemKind == PungentAuthoringItemKind.LegacyNote ||
                    reference.itemKind == PungentAuthoringItemKind.Task ||
                    string.Equals(reference.providerId, PungentAuthoringLegacyNoteProvider.Id, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReferenceKey(PungentAuthoringReference reference)
        {
            if (reference == null)
                return string.Empty;
            return reference.itemKind + "|" + reference.providerId + "|" + reference.itemId;
        }
    }
#endif
}
