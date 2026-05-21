namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;

    public enum PungentUtilityHelpAnnotationKind
    {
        Note,
        Bookmark
    }

    [Serializable]
    public sealed class PungentUtilityHelpAnnotation
    {
        public string noteId = string.Empty;
        public string topicStableId = string.Empty;
        public string utilityId = string.Empty;
        public string anchorId = string.Empty;
        public string anchorLabel = string.Empty;
        public string title = string.Empty;
        public string topicTitle = string.Empty;
        public string excerpt = string.Empty;
        public PungentUtilityHelpAnnotationKind kind = PungentUtilityHelpAnnotationKind.Note;
        public bool editable = true;
        public bool archived;
        public bool developerOnly;
        public string sourceLabel = string.Empty;
        public string statusLabel = string.Empty;
        public string priorityLabel = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;

        public string MarkerLabel => kind == PungentUtilityHelpAnnotationKind.Bookmark ? "B" : "N";
    }

    [Serializable]
    public sealed class PungentUtilityHelpBookmark
    {
        public string id = Guid.NewGuid().ToString("N");
        public string topicStableId = string.Empty;
        public string anchorId = string.Empty;
        public string title = string.Empty;
        public string excerpt = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
    }

    public static class PungentUtilityHelpAnchors
    {
        public const string Topic = "topic";
        public const string Summary = "topic-summary";
        public const string QuickUse = "quick-use";
        public const string Related = "related";

        public static string Feature(string id) => "feature:" + PungentUtilityHelpIds.Normalize(id, "feature");
        public static string Scripting(string id) => "scripting:" + PungentUtilityHelpIds.Normalize(id, "scripting");
        public static string Troubleshooting(string id) => "troubleshooting:" + PungentUtilityHelpIds.Normalize(id, "troubleshooting");
    }

    public static class PungentUtilityHelpAnnotationUtility
    {
        public static List<PungentUtilityHelpAnnotation> Merge(IEnumerable<PungentUtilityHelpAnnotation> primary, IEnumerable<PungentUtilityHelpAnnotation> secondary)
        {
            List<PungentUtilityHelpAnnotation> merged = new List<PungentUtilityHelpAnnotation>();
            if (primary != null)
            {
                foreach (PungentUtilityHelpAnnotation annotation in primary)
                    if (annotation != null)
                        merged.Add(annotation);
            }

            if (secondary != null)
            {
                foreach (PungentUtilityHelpAnnotation annotation in secondary)
                    if (annotation != null)
                        merged.Add(annotation);
            }

            return merged;
        }
    }
#endif
}
