using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringDocumentationLinkProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider
    {
        public const string Id = "documentation-links";
        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.DocumentationLink };

        public string ProviderId => Id;
        public string DisplayName => "Documentation Links";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Edit |
            PungentAuthoringProviderCapabilities.Copy;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.HelpDocumentation;
        public string ExtensionId => PungentAuthoringPackageCapabilities.HelpDocumentation;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentUtilityDocumentationLinks.DocumentationLink link in PungentUtilityDocumentationLinks.instance.GetAll())
                if (link != null)
                    yield return ToMetadata(link);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null)
                return false;

            metadata = ToMetadata(link);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null || link.utilityIds == null)
                yield break;

            foreach (string utilityId in link.utilityIds)
                if (!string.IsNullOrWhiteSpace(utilityId))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.Utility, utilityId, PungentAuthoringUtilityProvider.Id, "Assigned Utility");
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(link.assetGuid))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AssetGuid, link.assetGuid, "Documentation Asset", Id);

            if (!string.IsNullOrWhiteSpace(link.externalPath))
                yield return MapExternalTarget(link);

            foreach (string utilityId in link.utilityIds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(utilityId))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.UtilityId, utilityId, "Assigned Utility", PungentAuthoringUtilityProvider.Id);
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null)
                return false;

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(link), GetTargets(reference).Count());
            preview.subtitle = PungentUtilityDocumentationLinks.GetAssignedUtilitySummary(link) + " - " + status.kindLabel;
            preview.bodyPreview = string.IsNullOrWhiteSpace(link.description) ? status.message : link.description;
            preview.warning = !status.canOpen;
            preview.missing = !status.hasTarget || status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing || status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget;
            preview.warningLabel = status.message;
            preview.primaryActionLabels.Add(status.canOpen ? "Open Target" : "Edit Link");
            preview.primaryActionLabels.Add("Copy Target");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null)
            {
                reason = "Documentation link could not be found.";
                return false;
            }

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            reason = status.canOpen ? string.Empty : status.message;
            return status.canOpen;
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            return link != null && PungentUtilityDocumentationLinks.TryOpenTarget(link, out _);
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference?.itemId) == null ? "Documentation link could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            if (Find(reference?.itemId) == null)
                return false;
            DocumentationLinkEditorPopup.OpenForLink(reference.itemId);
            return true;
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Use the existing Documentation Links editor to create documentation links.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Use the existing Documentation Links editor to create documentation links.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = string.Empty;
            PungentUtilityDocumentationLinks.DocumentationLink link = Find(reference?.itemId);
            if (link == null)
            {
                error = "Documentation link could not be found.";
                return false;
            }

            if (!PungentUtilityDocumentationLinks.TryCopyTarget(link, out error))
                return false;

            copiedValue = EditorGUIUtility.systemCopyBuffer;
            return true;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = link?.id ?? string.Empty,
                title = link == null ? "Documentation" : PungentUtilityDocumentationLinks.GetDisplayName(link),
                summary = link?.description ?? string.Empty,
                kind = PungentAuthoringItemKind.DocumentationLink,
                status = link == null ? string.Empty : PungentUtilityDocumentationLinks.GetTargetStatus(link).kindLabel,
                priority = string.Empty,
                visibility = link == null || link.utilityIds == null || link.utilityIds.Length == 0 ? "Global" : "Assigned",
                tags = PungentAuthoringMetadata.NormalizeTags(new[] { link?.category, link?.versionLabel }.Concat(link?.utilityIds ?? Array.Empty<string>())),
                updatedUtc = TicksToUtc(link != null ? link.updatedUtcTicks : 0L),
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.HelpDocumentation,
                extensionId = PungentAuthoringPackageCapabilities.HelpDocumentation
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static PungentAuthoringTarget MapExternalTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            PungentAuthoringTargetKind kind = status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl
                ? PungentAuthoringTargetKind.ExternalWebUrl
                : status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.LocalFile || status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing
                    ? PungentAuthoringTargetKind.ExternalLocalPath
                    : PungentAuthoringTargetKind.ExternalReference;

            return PungentAuthoringTarget.Create(kind, status.targetValue, status.kindLabel, Id);
        }

        private static PungentUtilityDocumentationLinks.DocumentationLink Find(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : PungentUtilityDocumentationLinks.instance.FindById(id);
        }

        private static string TicksToUtc(long ticks)
        {
            if (ticks <= 0L)
                return string.Empty;
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc).ToString("o");
            }
            catch
            {
                return string.Empty;
            }
        }
    }
#endif
}
