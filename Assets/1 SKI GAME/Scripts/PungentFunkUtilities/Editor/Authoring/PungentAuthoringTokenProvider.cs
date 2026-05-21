using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringTokenProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider
    {
        public const string Id = "token-definitions";
        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.TokenDefinition };

        public string ProviderId => Id;
        public string DisplayName => "Token Definitions";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Copy;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.TokenSystem;
        public string ExtensionId => PungentAuthoringPackageCapabilities.TokenSystem;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentTokenDefinition token in PungentTokenDatabase.instance.tokens ?? new List<PungentTokenDefinition>())
                if (token != null)
                    yield return ToMetadata(token);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentTokenDefinition token = Find(reference?.itemId);
            if (token == null)
                return false;

            metadata = ToMetadata(token);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentTokenDefinition token = Find(reference?.itemId);
            if (token == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(token.documentationNoteId))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, token.documentationNoteId, PungentAuthoringLegacyNoteProvider.Id, "Documentation Note");

            if (!string.IsNullOrWhiteSpace(token.replacementKey))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.TokenDefinition, token.replacementKey, Id, "Replacement Token");

            foreach (PungentTokenBinding binding in GetBindings(token.key))
            {
                if (!string.IsNullOrWhiteSpace(binding.noteId))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, binding.noteId, PungentAuthoringLegacyNoteProvider.Id, "Binding Note");
                if (!string.IsNullOrWhiteSpace(binding.utilityId))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.Utility, binding.utilityId, PungentAuthoringUtilityProvider.Id, "Binding Utility");
                if (!string.IsNullOrWhiteSpace(binding.futureUtilityId))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.FutureUtility, binding.futureUtilityId, PungentAuthoringLegacyNoteProvider.Id, "Future Utility");
            }
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentTokenDefinition token = Find(reference?.itemId);
            if (token == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(token.documentationNoteId))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.NoteId, token.documentationNoteId, "Documentation Note", PungentAuthoringLegacyNoteProvider.Id);

            foreach (PungentTokenBinding binding in GetBindings(token.key))
            {
                PungentAuthoringTarget target = ToTarget(binding);
                if (target != null)
                    yield return target;
            }

            foreach (PungentNote note in PungentNoteDatabase.instance.notes ?? new List<PungentNote>())
            {
                if (note == null || note.linkedTokenKeys == null)
                    continue;
                if (note.linkedTokenKeys.Any(key => string.Equals(PungentTokenDatabase.NormalizeKey(key), PungentTokenDatabase.NormalizeKey(token.key), StringComparison.OrdinalIgnoreCase)))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.NoteId, note.id, "Linked Note", PungentAuthoringLegacyNoteProvider.Id);
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentTokenDefinition token = Find(reference?.itemId);
            if (token == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(token), GetTargets(reference).Count());
            preview.subtitle = "{" + token.key + "} - " + token.category;
            preview.bodyPreview = string.IsNullOrWhiteSpace(token.description) ? token.previewValue : token.description;
            preview.warning = token.deprecated || token.required || token.archived;
            preview.warningLabel = token.deprecated ? "Deprecated" : token.required ? "Required" : token.archived ? "Archived" : string.Empty;
            preview.primaryActionLabels.Add("Open Token Validator");
            preview.primaryActionLabels.Add("Copy Token Key");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference?.itemId) == null ? "Token could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentTokenDefinition token = Find(reference?.itemId);
            if (token == null)
                return false;

            PungentTokenValidatorWindow.OpenAndSelect(token.key);
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
            reason = "Token creation remains in the existing Token Validator.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Token creation remains in the existing Token Validator.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            PungentTokenDefinition token = Find(reference?.itemId);
            copiedValue = token != null ? token.key : reference?.itemId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No token key to copy.";
                return false;
            }

            copiedValue = PungentTokenDatabase.NormalizeKey(copiedValue);
            EditorGUIUtility.systemCopyBuffer = copiedValue;
            error = string.Empty;
            return true;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentTokenDefinition token)
        {
            List<string> tags = PungentAuthoringMetadata.NormalizeTags(token?.tags);
            if (!string.IsNullOrWhiteSpace(token?.category))
                tags.Add(token.category.Trim());
            if (token != null && token.required)
                tags.Add("required");
            if (token != null && token.deprecated)
                tags.Add("deprecated");

            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = token == null ? string.Empty : PungentTokenDatabase.NormalizeKey(token.key),
                title = string.IsNullOrWhiteSpace(token?.displayName) ? PungentTokenDatabase.NormalizeKey(token?.key) : token.displayName,
                summary = token?.description ?? string.Empty,
                kind = PungentAuthoringItemKind.TokenDefinition,
                status = token == null ? string.Empty : token.archived ? "Archived" : token.deprecated ? "Deprecated" : token.required ? "Required" : "Active",
                priority = token != null && token.required ? "Required" : string.Empty,
                visibility = token != null && token.developerOnly ? "DeveloperOnly" : "Project",
                tags = tags,
                createdUtc = token?.createdUtc ?? string.Empty,
                updatedUtc = token?.updatedUtc ?? string.Empty,
                archived = token != null && token.archived,
                developerOnly = token != null && token.developerOnly,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.TokenSystem,
                extensionId = PungentAuthoringPackageCapabilities.TokenSystem
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        public static PungentAuthoringTarget ToTarget(PungentTokenBinding binding)
        {
            if (binding == null)
                return null;

            PungentAuthoringTarget target = new PungentAuthoringTarget
            {
                label = binding.label,
                providerId = Id,
                sourceContext = binding.tokenKey
            };

            switch (binding.targetType)
            {
                case PungentTokenTargetType.Asset:
                    target.targetKind = PungentAuthoringTargetKind.AssetGuid;
                    target.rawValue = binding.assetGuid;
                    break;
                case PungentTokenTargetType.SceneObject:
                    target.targetKind = PungentAuthoringTargetKind.SceneObjectGlobalId;
                    target.rawValue = binding.sceneObjectGlobalId;
                    break;
                case PungentTokenTargetType.ComponentType:
                    target.targetKind = PungentAuthoringTargetKind.ComponentType;
                    target.rawValue = binding.componentType;
                    break;
                case PungentTokenTargetType.ComponentInstance:
                    target.targetKind = PungentAuthoringTargetKind.ComponentInstanceId;
                    target.rawValue = binding.componentInstanceId;
                    target.contextId = binding.sceneObjectGlobalId;
                    break;
                case PungentTokenTargetType.SerializedProperty:
                    target.targetKind = PungentAuthoringTargetKind.SerializedPropertyPath;
                    target.rawValue = binding.propertyPath;
                    target.propertyPath = binding.propertyPath;
                    target.contextId = !string.IsNullOrWhiteSpace(binding.sceneObjectGlobalId) ? binding.sceneObjectGlobalId : binding.assetGuid;
                    break;
                case PungentTokenTargetType.ScriptPath:
                    target.targetKind = PungentAuthoringTargetKind.ScriptPath;
                    target.rawValue = binding.scriptPath;
                    break;
                case PungentTokenTargetType.Note:
                    target.targetKind = PungentAuthoringTargetKind.NoteId;
                    target.rawValue = binding.noteId;
                    target.providerId = PungentAuthoringLegacyNoteProvider.Id;
                    break;
                case PungentTokenTargetType.AuditIssue:
                    target.targetKind = PungentAuthoringTargetKind.AuditIssueCode;
                    target.rawValue = binding.auditIssueCode;
                    target.providerId = PungentAuthoringAuditIssueProvider.Id;
                    break;
                case PungentTokenTargetType.RegisteredUtility:
                    target.targetKind = PungentAuthoringTargetKind.UtilityId;
                    target.rawValue = binding.utilityId;
                    target.providerId = PungentAuthoringUtilityProvider.Id;
                    break;
                case PungentTokenTargetType.FutureUtility:
                    target.targetKind = PungentAuthoringTargetKind.FutureUtilityId;
                    target.rawValue = binding.futureUtilityId;
                    target.providerId = PungentAuthoringLegacyNoteProvider.Id;
                    break;
                case PungentTokenTargetType.ExternalPath:
                    target.targetKind = PungentAuthoringTargetKind.ExternalLocalPath;
                    target.rawValue = binding.externalPath;
                    break;
                default:
                    target.targetKind = PungentAuthoringTargetKind.Unknown;
                    break;
            }

            target.NormalizeInPlace();
            return target;
        }

        private static PungentTokenDefinition Find(string idOrKey)
        {
            string clean = PungentTokenDatabase.NormalizeKey(idOrKey);
            return (PungentTokenDatabase.instance.tokens ?? new List<PungentTokenDefinition>())
                .FirstOrDefault(token => token != null &&
                                         (string.Equals(PungentTokenDatabase.NormalizeKey(token.key), clean, StringComparison.OrdinalIgnoreCase) ||
                                          PungentAuthoringId.EqualsId(token.id, idOrKey)));
        }

        private static IEnumerable<PungentTokenBinding> GetBindings(string key)
        {
            string clean = PungentTokenDatabase.NormalizeKey(key);
            return (PungentTokenDatabase.instance.bindings ?? new List<PungentTokenBinding>())
                .Where(binding => binding != null &&
                                  !binding.archived &&
                                  string.Equals(PungentTokenDatabase.NormalizeKey(binding.tokenKey), clean, StringComparison.OrdinalIgnoreCase));
        }
    }
#endif
}
