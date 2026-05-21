using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    [Serializable]
    public sealed class PungentAuthoringPreview
    {
        public string title = string.Empty;
        public string subtitle = string.Empty;
        public string bodyPreview = string.Empty;
        public string kindLabel = string.Empty;
        public string statusLabel = string.Empty;
        public List<string> tags = new List<string>();
        public int targetCount;
        public bool warning;
        public bool missing;
        public string warningLabel = string.Empty;
        public List<string> primaryActionLabels = new List<string>();

        public static PungentAuthoringPreview FromMetadata(PungentAuthoringMetadata metadata, int targetCount = 0)
        {
            if (metadata == null)
                return Missing("Missing authoring item.", "The requested authoring item could not be found.");

            return new PungentAuthoringPreview
            {
                title = string.IsNullOrWhiteSpace(metadata.title) ? metadata.id : metadata.title,
                subtitle = metadata.summary ?? string.Empty,
                bodyPreview = metadata.summary ?? string.Empty,
                kindLabel = metadata.KindLabel,
                statusLabel = metadata.status ?? string.Empty,
                tags = PungentAuthoringMetadata.NormalizeTags(metadata.tags),
                targetCount = targetCount,
                warning = metadata.archived || metadata.developerOnly,
                warningLabel = metadata.archived ? "Archived" : metadata.developerOnly ? "Developer Only" : string.Empty
            };
        }

        public static PungentAuthoringPreview Missing(string title, string message)
        {
            return new PungentAuthoringPreview
            {
                title = string.IsNullOrWhiteSpace(title) ? "Missing Authoring Item" : title.Trim(),
                bodyPreview = message ?? string.Empty,
                kindLabel = "Missing",
                statusLabel = "Missing",
                warning = true,
                missing = true,
                warningLabel = message ?? string.Empty
            };
        }
    }
#endif
}
