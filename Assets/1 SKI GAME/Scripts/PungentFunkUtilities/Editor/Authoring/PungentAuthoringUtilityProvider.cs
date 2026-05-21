using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringUtilityProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider
    {
        public const string Id = "utility-registry";
        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.Utility };

        public string ProviderId => Id;
        public string DisplayName => "Utility Registry";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Copy;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.UtilityCore;
        public string ExtensionId => PungentAuthoringPackageCapabilities.UtilityCore;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentUtilityDescriptor descriptor in PungentUtilityRegistry.All)
                if (descriptor != null)
                    yield return ToMetadata(descriptor);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentUtilityDescriptor descriptor = Find(reference?.itemId);
            if (descriptor == null)
                return false;

            metadata = ToMetadata(descriptor);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentUtilityDescriptor descriptor = Find(reference?.itemId);
            if (descriptor == null)
                yield break;

            foreach (string id in descriptor.RelatedUtilityIds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.Utility, id, Id, "Related Utility");

            foreach (PungentUtilityDocumentationLinks.DocumentationLink link in PungentUtilityDocumentationLinks.instance.GetLinksForUtility(descriptor.Id))
                if (link != null && !string.IsNullOrWhiteSpace(link.id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.DocumentationLink, link.id, PungentAuthoringDocumentationLinkProvider.Id, "Documentation Link");
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            foreach (PungentAuthoringReference related in GetReferences(reference))
                yield return PungentAuthoringTarget.Create(
                    related.itemKind == PungentAuthoringItemKind.DocumentationLink ? PungentAuthoringTargetKind.DocumentationLinkId : PungentAuthoringTargetKind.UtilityId,
                    related.itemId,
                    related.label,
                    related.providerId);
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentUtilityDescriptor descriptor = Find(reference?.itemId);
            if (descriptor == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(descriptor), GetTargets(reference).Count());
            preview.subtitle = PungentUtilityRegistry.GetAreaCategory(descriptor) + " / " + PungentUtilityRegistry.GetModule(descriptor);
            preview.bodyPreview = descriptor.Description ?? string.Empty;
            preview.primaryActionLabels.Add(descriptor.CanRun ? "Open Utility" : "Copy Utility ID");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            PungentUtilityDescriptor descriptor = Find(reference?.itemId);
            if (descriptor == null)
            {
                reason = "Utility could not be found.";
                return false;
            }

            reason = descriptor.CanRun ? string.Empty : descriptor.DisabledReason;
            return descriptor.CanRun;
        }

        public bool Open(PungentAuthoringReference reference)
        {
            return PungentUtilityRegistry.Open(reference?.itemId);
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            reason = "Utility registry metadata editing remains in the existing developer metadata editor.";
            return false;
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return false;
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Utility creation is outside the authoring provider foundation pass.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Utility creation is outside the authoring provider foundation pass.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference?.itemId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No utility ID to copy.";
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = copiedValue;
            error = string.Empty;
            return true;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentUtilityDescriptor descriptor)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = descriptor?.Id ?? string.Empty,
                title = descriptor?.DisplayName ?? string.Empty,
                summary = descriptor?.Description ?? string.Empty,
                kind = PungentAuthoringItemKind.Utility,
                status = descriptor == null ? string.Empty : PungentUtilityRegistry.GetStatus(descriptor),
                priority = descriptor != null && descriptor.IsPrimaryUtility ? "Primary" : string.Empty,
                visibility = descriptor == null ? string.Empty : descriptor.Visibility.ToString(),
                tags = PungentAuthoringMetadata.NormalizeTags((descriptor?.Tags ?? Array.Empty<string>()).Concat(descriptor?.CategoryFacets ?? Array.Empty<string>()).Concat(new[] { descriptor?.Lab, descriptor?.Module, descriptor?.Category })),
                archived = descriptor != null && descriptor.IsArchived,
                developerOnly = descriptor != null && descriptor.IsDeveloperOnly,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.UtilityCore,
                extensionId = PungentAuthoringPackageCapabilities.UtilityCore
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static PungentUtilityDescriptor Find(string utilityId)
        {
            return string.IsNullOrWhiteSpace(utilityId) ? null : PungentUtilityRegistry.Find(utilityId);
        }
    }
#endif
}
