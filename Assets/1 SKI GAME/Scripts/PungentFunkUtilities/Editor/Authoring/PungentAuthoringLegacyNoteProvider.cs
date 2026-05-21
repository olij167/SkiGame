using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.ProjectAudit;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringLegacyNoteProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider,
        IPungentAuthoringTargetResolver
    {
        public const string Id = "legacy-notes";

        private static readonly PungentAuthoringItemKind[] Kinds =
        {
            PungentAuthoringItemKind.LegacyNote,
            PungentAuthoringItemKind.Task,
            PungentAuthoringItemKind.FutureUtility
        };

        public string ProviderId => Id;
        public string DisplayName => "Legacy Notes";
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
            PungentAuthoringProviderCapabilities.ResolveTarget |
            PungentAuthoringProviderCapabilities.ConversionHooks;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.NotesBrowser;
        public string ExtensionId => PungentAuthoringPackageCapabilities.NotesBrowser;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentNote note in PungentNoteDatabase.instance.notes ?? new List<PungentNote>())
                if (note != null)
                    yield return ToMetadata(note);

            foreach (PungentFutureUtilityRecord record in PungentNoteDatabase.instance.futureUtilities ?? new List<PungentFutureUtilityRecord>())
                if (record != null)
                    yield return ToMetadata(record);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return false;

            PungentNote note = FindNote(reference.itemId);
            if (note != null)
            {
                metadata = ToMetadata(note);
                return true;
            }

            PungentFutureUtilityRecord record = FindFutureUtility(reference.itemId);
            if (record != null)
            {
                metadata = ToMetadata(record);
                return true;
            }

            return false;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentNote note = reference == null ? null : FindNote(reference.itemId);
            if (note == null)
                yield break;

            foreach (string noteId in note.relatedNoteIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(noteId))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, noteId, Id, "Related Note");

            foreach (string tokenKey in note.linkedTokenKeys ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(tokenKey))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.TokenDefinition, tokenKey, PungentAuthoringTokenProvider.Id, "Linked Token");

            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.Utility, note.linkedUtilityId, PungentAuthoringUtilityProvider.Id, "Linked Utility");

            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.FutureUtility, note.linkedFutureUtilityId, Id, "Future Utility");

            if (!string.IsNullOrWhiteSpace(note.auditIssueCode))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.AuditIssue, note.auditIssueCode, PungentAuthoringAuditIssueProvider.Id, "Audit Issue");
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentNote note = reference == null ? null : FindNote(reference.itemId);
            if (note == null)
                yield break;

            foreach (PungentNoteTargetLink target in note.targets ?? new List<PungentNoteTargetLink>())
            {
                PungentAuthoringTarget mapped = ToTarget(target);
                if (mapped != null)
                    yield return mapped;
            }

            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.UtilityId, note.linkedUtilityId, "Linked Utility", PungentAuthoringUtilityProvider.Id);
            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.FutureUtilityId, note.linkedFutureUtilityId, "Future Utility", Id);
            if (!string.IsNullOrWhiteSpace(note.auditIssueCode))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AuditIssueCode, note.auditIssueCode, "Audit Issue", PungentAuthoringAuditIssueProvider.Id);
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            if (!TryGetMetadata(reference, out PungentAuthoringMetadata metadata))
                return false;

            preview = PungentAuthoringPreview.FromMetadata(metadata, GetTargets(reference).Count());
            if (FindNote(reference.itemId) != null)
            {
                preview.bodyPreview = PreviewText(FindNote(reference.itemId).body);
                preview.primaryActionLabels.Add("Open Note");
            }
            else
            {
                preview.primaryActionLabels.Add("Open Notes");
            }

            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = string.Empty;
            if (reference == null || !TryGetMetadata(reference, out _))
            {
                reason = "Legacy note could not be found.";
                return false;
            }

            return true;
        }

        public bool Open(PungentAuthoringReference reference)
        {
            if (FindNote(reference?.itemId) != null)
                PungentNotesRoadmapWindow.OpenAndSelect(reference.itemId);
            else
                PungentNotesRoadmapWindow.Open();
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
            reason = "Legacy note creation from authoring context is deferred to existing note context menus.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Legacy note creation from authoring context is deferred to existing note context menus.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference?.itemId ?? string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No note ID to copy.";
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = copiedValue;
            return true;
        }

        public bool TryResolveTarget(PungentAuthoringTarget target, out UnityEngine.Object unityObject, out string error)
        {
            unityObject = null;
            error = string.Empty;
            if (target == null)
            {
                error = "Target is missing.";
                return false;
            }

            if (target.targetKind == PungentAuthoringTargetKind.AssetGuid)
            {
                string path = AssetDatabase.GUIDToAssetPath(target.rawValue);
                unityObject = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                error = unityObject == null ? "Asset target could not be resolved." : string.Empty;
                return unityObject != null;
            }

            if (target.targetKind == PungentAuthoringTargetKind.SceneObjectGlobalId ||
                target.targetKind == PungentAuthoringTargetKind.ComponentInstanceId ||
                target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath)
            {
                string globalIdValue = target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath ? target.contextId : target.rawValue;
                if (GlobalObjectId.TryParse(globalIdValue, out GlobalObjectId globalId))
                    unityObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                error = unityObject == null ? "Scene object target could not be resolved." : string.Empty;
                return unityObject != null;
            }

            error = "Legacy note provider does not resolve this target kind.";
            return false;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentNote note)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = note?.id ?? string.Empty,
                title = string.IsNullOrWhiteSpace(note?.title) ? "Untitled Note" : note.title,
                summary = PreviewText(note?.body),
                kind = MapKind(note),
                status = note != null ? note.status.ToString() : string.Empty,
                priority = note != null ? note.priority.ToString() : string.Empty,
                visibility = note != null ? note.visibility.ToString() : string.Empty,
                tags = PungentAuthoringMetadata.NormalizeTags(note?.tags),
                createdUtc = note?.createdUtc ?? string.Empty,
                updatedUtc = note?.updatedUtc ?? string.Empty,
                archived = note != null && note.archived,
                developerOnly = note != null && note.developerOnly,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.NotesBrowser,
                extensionId = PungentAuthoringPackageCapabilities.NotesBrowser
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentFutureUtilityRecord record)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = record?.id ?? string.Empty,
                title = string.IsNullOrWhiteSpace(record?.displayName) ? "Future Utility" : record.displayName,
                summary = record?.description ?? string.Empty,
                kind = PungentAuthoringItemKind.FutureUtility,
                status = record != null ? (record.implemented ? "Implemented" : record.status.ToString()) : string.Empty,
                priority = record != null ? record.priority.ToString() : string.Empty,
                visibility = "Project",
                tags = PungentAuthoringMetadata.NormalizeTags(record?.tags),
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.NotesBrowser,
                extensionId = PungentAuthoringPackageCapabilities.NotesBrowser
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        public static PungentAuthoringTarget ToTarget(PungentNoteTargetLink link)
        {
            if (link == null)
                return null;

            PungentAuthoringTarget target = new PungentAuthoringTarget
            {
                label = link.label ?? string.Empty,
                providerId = Id
            };

            switch (link.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    target.targetKind = PungentAuthoringTargetKind.UtilityId;
                    target.rawValue = link.utilityId;
                    target.providerId = PungentAuthoringUtilityProvider.Id;
                    break;
                case PungentNoteTargetType.FutureUtility:
                    target.targetKind = PungentAuthoringTargetKind.FutureUtilityId;
                    target.rawValue = link.futureUtilityId;
                    break;
                case PungentNoteTargetType.Asset:
                    target.targetKind = PungentAuthoringTargetKind.AssetGuid;
                    target.rawValue = link.assetGuid;
                    break;
                case PungentNoteTargetType.SceneObject:
                    target.targetKind = PungentAuthoringTargetKind.SceneObjectGlobalId;
                    target.rawValue = link.sceneObjectGlobalId;
                    break;
                case PungentNoteTargetType.ComponentType:
                    target.targetKind = PungentAuthoringTargetKind.ComponentType;
                    target.rawValue = link.componentType;
                    break;
                case PungentNoteTargetType.ComponentInstance:
                    target.targetKind = PungentAuthoringTargetKind.ComponentInstanceId;
                    target.rawValue = link.sceneObjectGlobalId;
                    target.contextId = link.componentType;
                    break;
                case PungentNoteTargetType.SerializedProperty:
                    target.targetKind = PungentAuthoringTargetKind.SerializedPropertyPath;
                    target.rawValue = link.propertyPath;
                    target.propertyPath = link.propertyPath;
                    target.contextId = !string.IsNullOrWhiteSpace(link.sceneObjectGlobalId) ? link.sceneObjectGlobalId : link.assetGuid;
                    break;
                case PungentNoteTargetType.ScriptPath:
                    target.targetKind = PungentAuthoringTargetKind.ScriptPath;
                    target.rawValue = link.scriptPath;
                    break;
                case PungentNoteTargetType.Token:
                    target.targetKind = PungentAuthoringTargetKind.TokenKey;
                    target.rawValue = link.tokenKey;
                    target.providerId = PungentAuthoringTokenProvider.Id;
                    break;
                case PungentNoteTargetType.AuditIssue:
                    target.targetKind = PungentAuthoringTargetKind.AuditIssueCode;
                    target.rawValue = link.auditIssueCode;
                    target.providerId = PungentAuthoringAuditIssueProvider.Id;
                    break;
                case PungentNoteTargetType.DocumentationLink:
                    target.targetKind = PungentAuthoringTargetKind.DocumentationLinkId;
                    target.rawValue = link.documentationLinkId;
                    target.providerId = PungentAuthoringDocumentationLinkProvider.Id;
                    break;
                case PungentNoteTargetType.Note:
                    target.targetKind = PungentAuthoringTargetKind.NoteId;
                    target.rawValue = link.noteId;
                    break;
                case PungentNoteTargetType.ExternalPath:
                    target = MapExternalTarget(link.externalPathOrUrl, link.label);
                    break;
                default:
                    target.targetKind = PungentAuthoringTargetKind.Unknown;
                    break;
            }

            target.NormalizeInPlace();
            return target;
        }

        private static PungentAuthoringTarget MapExternalTarget(string externalPathOrUrl, string label)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(string.Empty, externalPathOrUrl);
            PungentAuthoringTargetKind kind = status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl
                ? PungentAuthoringTargetKind.ExternalWebUrl
                : status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.LocalFile || status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing
                    ? PungentAuthoringTargetKind.ExternalLocalPath
                    : PungentAuthoringTargetKind.ExternalReference;

            return PungentAuthoringTarget.Create(kind, status.targetValue, label, Id);
        }

        private static PungentAuthoringItemKind MapKind(PungentNote note)
        {
            if (note == null)
                return PungentAuthoringItemKind.LegacyNote;

            switch (note.kind)
            {
                case PungentNoteKind.ImplementationTask:
                case PungentNoteKind.AuditFollowUp:
                case PungentNoteKind.SupportRequest:
                    return PungentAuthoringItemKind.Task;
                case PungentNoteKind.FutureUtility:
                    return PungentAuthoringItemKind.FutureUtility;
                default:
                    return PungentAuthoringItemKind.LegacyNote;
            }
        }

        private static PungentNote FindNote(string noteId)
        {
            return (PungentNoteDatabase.instance.notes ?? new List<PungentNote>())
                .FirstOrDefault(note => note != null && PungentAuthoringId.EqualsId(note.id, noteId));
        }

        private static PungentFutureUtilityRecord FindFutureUtility(string id)
        {
            return (PungentNoteDatabase.instance.futureUtilities ?? new List<PungentFutureUtilityRecord>())
                .FirstOrDefault(record => record != null && PungentAuthoringId.EqualsId(record.id, id));
        }

        private static string PreviewText(string text, int maxLength = 220)
        {
            string clean = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().Replace("\r", " ").Replace("\n", " ");
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength - 3) + "...";
        }
    }
#endif
}
