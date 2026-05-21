using System;

namespace PungentFunk.Utilities.Authoring
{
    [Serializable]
    public sealed class PungentAuthoringReference
    {
        public PungentAuthoringItemKind itemKind = PungentAuthoringItemKind.Unknown;
        public string customKind = string.Empty;
        public string itemId = string.Empty;
        public string providerId = string.Empty;
        public string label = string.Empty;
        public string sourceContext = string.Empty;

        public bool HasItemId => PungentAuthoringId.IsValidId(itemId);

        public string KindLabel => PungentAuthoringItemKinds.GetDisplayName(itemKind, customKind);

        public void NormalizeInPlace()
        {
            itemId = PungentAuthoringId.Normalize(itemId);
            providerId = providerId == null ? string.Empty : providerId.Trim();
            label = label == null ? string.Empty : label.Trim();
            customKind = customKind == null ? string.Empty : customKind.Trim();
            sourceContext = sourceContext == null ? string.Empty : sourceContext.Trim();
        }

        public static PungentAuthoringReference Create(PungentAuthoringItemKind kind, string itemId, string providerId = null, string label = null)
        {
            PungentAuthoringReference reference = new PungentAuthoringReference
            {
                itemKind = kind,
                itemId = itemId,
                providerId = providerId ?? string.Empty,
                label = label ?? string.Empty
            };
            reference.NormalizeInPlace();
            return reference;
        }
    }
}
