namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    [FilePath("ProjectSettings/PungentFunkUtilities/UtilityHelpAnnotations.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityHelpAnnotationStorage : ScriptableSingleton<PungentUtilityHelpAnnotationStorage>
    {
        public List<PungentUtilityHelpBookmark> bookmarks = new List<PungentUtilityHelpBookmark>();

        public static string StorageLocation => "ProjectSettings/PungentFunkUtilities/UtilityHelpAnnotations.asset";
        public static event Action Changed;

        public List<PungentUtilityHelpAnnotation> GetAllBookmarks()
        {
            if (bookmarks == null)
                bookmarks = new List<PungentUtilityHelpBookmark>();

            return bookmarks
                .Where(b => b != null)
                .Select(ToAnnotation)
                .ToList();
        }

        public List<PungentUtilityHelpAnnotation> GetAnnotationsForTopic(string topicStableId)
        {
            if (bookmarks == null)
                bookmarks = new List<PungentUtilityHelpBookmark>();

            return bookmarks
                .Where(b => b != null && string.Equals(b.topicStableId, topicStableId, StringComparison.OrdinalIgnoreCase))
                .Select(ToAnnotation)
                .ToList();
        }

        public bool HasBookmark(string topicStableId, string anchorId)
        {
            if (bookmarks == null)
                return false;

            string anchor = NormalizeAnchor(anchorId);
            return bookmarks.Any(b => b != null &&
                                      string.Equals(b.topicStableId, topicStableId, StringComparison.OrdinalIgnoreCase) &&
                                      string.Equals(NormalizeAnchor(b.anchorId), anchor, StringComparison.OrdinalIgnoreCase));
        }

        public void ToggleBookmark(PungentUtilityHelpTopic topic, string anchorId, string title, string excerpt)
        {
            if (topic == null)
                return;

            if (bookmarks == null)
                bookmarks = new List<PungentUtilityHelpBookmark>();

            string anchor = NormalizeAnchor(anchorId);
            int existing = bookmarks.FindIndex(b => b != null &&
                                                    string.Equals(b.topicStableId, topic.StableId, StringComparison.OrdinalIgnoreCase) &&
                                                    string.Equals(NormalizeAnchor(b.anchorId), anchor, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                bookmarks.RemoveAt(existing);
            else
                bookmarks.Add(new PungentUtilityHelpBookmark
                {
                    topicStableId = topic.StableId,
                    anchorId = anchor,
                    title = string.IsNullOrWhiteSpace(title) ? "Bookmark: " + topic.title : title.Trim(),
                    excerpt = excerpt ?? string.Empty,
                    createdUtc = DateTime.UtcNow.ToString("o")
                });

            Save(true);
            Changed?.Invoke();
        }

        public bool RemoveBookmark(string bookmarkId)
        {
            if (bookmarks == null || string.IsNullOrWhiteSpace(bookmarkId))
                return false;

            int index = bookmarks.FindIndex(b => b != null && string.Equals(b.id, bookmarkId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            bookmarks.RemoveAt(index);
            Save(true);
            Changed?.Invoke();
            return true;
        }

        private static string NormalizeAnchor(string anchorId)
        {
            return string.IsNullOrWhiteSpace(anchorId) ? PungentUtilityHelpAnchors.Topic : anchorId.Trim();
        }

        private static PungentUtilityHelpAnnotation ToAnnotation(PungentUtilityHelpBookmark bookmark)
        {
            string topicStableId = bookmark == null ? string.Empty : bookmark.topicStableId;
            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(topicStableId);
            string anchor = NormalizeAnchor(bookmark == null ? string.Empty : bookmark.anchorId);
            return new PungentUtilityHelpAnnotation
            {
                noteId = bookmark == null ? string.Empty : bookmark.id,
                topicStableId = topicStableId,
                utilityId = topic == null ? ExtractUtilityId(topicStableId) : topic.utilityId,
                anchorId = anchor,
                anchorLabel = ObjectNames.NicifyVariableName(anchor.Replace(":", " ")),
                title = bookmark == null || string.IsNullOrWhiteSpace(bookmark.title) ? "Bookmark" : bookmark.title,
                topicTitle = topic == null ? topicStableId : topic.title,
                excerpt = bookmark == null ? string.Empty : bookmark.excerpt,
                kind = PungentUtilityHelpAnnotationKind.Bookmark,
                editable = true,
                sourceLabel = "Local Help Bookmark",
                createdUtc = bookmark == null ? string.Empty : bookmark.createdUtc
            };
        }

        private static string ExtractUtilityId(string topicStableId)
        {
            if (string.IsNullOrWhiteSpace(topicStableId))
                return string.Empty;

            int slash = topicStableId.IndexOf('/');
            return slash > 0 ? topicStableId.Substring(0, slash) : string.Empty;
        }
    }
#endif
}
