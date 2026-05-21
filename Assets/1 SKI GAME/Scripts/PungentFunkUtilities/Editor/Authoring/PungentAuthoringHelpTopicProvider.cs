using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Core.Help;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringHelpTopicProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher
    {
        public const string Id = "help-topics";
        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.HelpTopic };

        public string ProviderId => Id;
        public string DisplayName => "Help Topics";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.HelpDocumentation;
        public string ExtensionId => PungentAuthoringPackageCapabilities.HelpDocumentation;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentUtilityHelpTopic topic in PungentUtilityHelpRegistry.AllTopics)
                if (topic != null)
                    yield return ToMetadata(topic);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentUtilityHelpTopic topic = Find(reference?.itemId);
            if (topic == null)
                return false;

            metadata = ToMetadata(topic);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentUtilityHelpTopic topic = Find(reference?.itemId);
            if (topic == null)
                yield break;

            foreach (string id in topic.relatedDocumentationLinkIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.DocumentationLink, id, PungentAuthoringDocumentationLinkProvider.Id, "Related Documentation");

            foreach (string id in topic.relatedNoteIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, id, PungentAuthoringLegacyNoteProvider.Id, "Related Note");

            foreach (string id in topic.relatedUtilityIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.Utility, id, PungentAuthoringUtilityProvider.Id, "Related Utility");

            foreach (string id in topic.relatedTopicIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id))
                    yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.HelpTopic, id, Id, "Related Topic");
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentUtilityHelpTopic topic = Find(reference?.itemId);
            if (topic == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(topic.utilityId))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.UtilityId, topic.utilityId, "Help Utility", PungentAuthoringUtilityProvider.Id);

            foreach (PungentAuthoringReference related in GetReferences(reference))
            {
                PungentAuthoringTargetKind kind = related.itemKind == PungentAuthoringItemKind.DocumentationLink
                    ? PungentAuthoringTargetKind.DocumentationLinkId
                    : related.itemKind == PungentAuthoringItemKind.LegacyNote
                        ? PungentAuthoringTargetKind.NoteId
                        : related.itemKind == PungentAuthoringItemKind.Utility
                            ? PungentAuthoringTargetKind.UtilityId
                            : PungentAuthoringTargetKind.HelpTopicId;
                yield return PungentAuthoringTarget.Create(kind, related.itemId, related.label, related.providerId);
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentUtilityHelpTopic topic = Find(reference?.itemId);
            if (topic == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(topic), GetTargets(reference).Count());
            preview.subtitle = topic.utilityId + " / " + topic.sectionId;
            preview.bodyPreview = string.IsNullOrWhiteSpace(topic.summary) ? topic.quickUseMarkdown : topic.summary;
            preview.warning = topic.hidden || topic.developerOnly || topic.generated;
            preview.warningLabel = topic.hidden ? "Hidden" : topic.developerOnly ? "Developer Only" : topic.generated ? "Generated" : string.Empty;
            preview.primaryActionLabels.Add("Open Help");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference?.itemId) == null ? "Help topic could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentUtilityHelpTopic topic = Find(reference?.itemId);
            if (topic == null)
                return false;

            PungentUtilityHelpRegistry.Open(topic.utilityId, topic.sectionId, topic.topicId);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            reason = "Help topic editing remains in the existing Help Browser developer tools.";
            return false;
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return false;
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Help topic creation remains in the existing Help Browser developer tools.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Help topic creation remains in the existing Help Browser developer tools.";
            return false;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentUtilityHelpTopic topic)
        {
            List<string> tags = PungentAuthoringMetadata.NormalizeTags(topic?.tags);
            if (topic != null && topic.generated)
                tags.Add("generated");

            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = topic?.StableId ?? string.Empty,
                title = string.IsNullOrWhiteSpace(topic?.title) ? topic?.StableId ?? "Help Topic" : topic.title,
                summary = topic?.summary ?? string.Empty,
                kind = PungentAuthoringItemKind.HelpTopic,
                status = topic == null ? string.Empty : topic.hidden ? "Hidden" : topic.generated ? "Generated" : "Curated",
                visibility = topic != null && topic.developerOnly ? "DeveloperOnly" : "Project",
                tags = tags,
                updatedUtc = topic?.lastUpdatedUtc ?? string.Empty,
                archived = topic != null && topic.hidden,
                developerOnly = topic != null && topic.developerOnly,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.HelpDocumentation,
                extensionId = PungentAuthoringPackageCapabilities.HelpDocumentation
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static PungentUtilityHelpTopic Find(string stableId)
        {
            return string.IsNullOrWhiteSpace(stableId) ? null : PungentUtilityHelpRegistry.FindByStableId(stableId);
        }
    }
#endif
}
