using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Authoring
{
    [Serializable]
    public sealed class PungentAuthoringMetadata
    {
        public string id = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public PungentAuthoringItemKind kind = PungentAuthoringItemKind.Unknown;
        public string customKind = string.Empty;
        public string status = string.Empty;
        public string priority = string.Empty;
        public string visibility = string.Empty;
        public List<string> tags = new List<string>();
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public bool archived;
        public bool developerOnly;
        public string sourceProviderId = string.Empty;
        public string packageCapabilityId = string.Empty;
        public string extensionId = string.Empty;

        public string KindLabel => PungentAuthoringItemKinds.GetDisplayName(kind, customKind);

        public PungentAuthoringReference ToReference()
        {
            return new PungentAuthoringReference
            {
                providerId = sourceProviderId,
                itemKind = kind,
                customKind = customKind,
                itemId = id,
                label = title,
                sourceContext = packageCapabilityId
            };
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            title = string.IsNullOrWhiteSpace(title) ? id : title.Trim();
            summary = summary == null ? string.Empty : summary.Trim();
            customKind = customKind == null ? string.Empty : customKind.Trim();
            status = status == null ? string.Empty : status.Trim();
            priority = priority == null ? string.Empty : priority.Trim();
            visibility = visibility == null ? string.Empty : visibility.Trim();
            sourceProviderId = sourceProviderId == null ? string.Empty : sourceProviderId.Trim();
            packageCapabilityId = packageCapabilityId == null ? string.Empty : packageCapabilityId.Trim();
            extensionId = extensionId == null ? string.Empty : extensionId.Trim();
            tags = NormalizeTags(tags);
        }

        public static List<string> NormalizeTags(IEnumerable<string> source)
        {
            return (source ?? Enumerable.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim().TrimStart('#'))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static PungentAuthoringMetadata Create(
            string id,
            string title,
            PungentAuthoringItemKind kind,
            string providerId,
            string packageCapabilityId)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = id,
                title = title,
                kind = kind,
                sourceProviderId = providerId,
                packageCapabilityId = packageCapabilityId
            };
            metadata.NormalizeInPlace();
            return metadata;
        }
    }
}
