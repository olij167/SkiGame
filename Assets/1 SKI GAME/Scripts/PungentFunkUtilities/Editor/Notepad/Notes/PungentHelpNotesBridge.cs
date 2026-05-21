using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    [InitializeOnLoad]
    public static class PungentHelpNotesBridge
    {
        static PungentHelpNotesBridge()
        {
            PungentUtilityHelpNotesBridge.CreateNoteFromTopic = CreateNoteFromTopic;
            PungentUtilityHelpNotesBridge.CreateNoteForAnchor = CreateNoteForAnchor;
            PungentUtilityHelpNotesBridge.CreateBookmark = CreateBookmark;
            PungentUtilityHelpNotesBridge.LinkExistingNote = LinkExistingNote;
            PungentUtilityHelpNotesBridge.OpenRelatedNotes = OpenRelatedNotes;
            PungentUtilityHelpNotesBridge.OpenNote = OpenNote;
            PungentUtilityHelpNotesBridge.GetAnnotationsForTopic = GetAnnotationsForTopic;
            PungentUtilityHelpNotesBridge.GetAllHelpAnnotations = GetAllHelpAnnotations;
            PungentUtilityHelpNotesBridge.GetHelpAnnotationsForUtility = GetHelpAnnotationsForUtility;
            PungentUtilityHelpNotesBridge.CountAllHelpNotes = CountAllHelpNotes;
            PungentUtilityHelpNotesBridge.OpenAllHelpNotes = OpenAllHelpNotes;
            PungentUtilityHelpNotesBridge.PromoteNoteToHelpTopic = PromoteNoteToHelpTopic;
            PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink = CreateNoteFromDocumentationLink;
            PungentUtilityHelpNotesBridge.AttachDocumentationLinkToSelectedNote = AttachDocumentationLinkToSelectedNote;
            PungentUtilityHelpNotesBridge.OpenNotesForDocumentationLink = OpenNotesForDocumentationLink;
            PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink = CountNotesForDocumentationLink;
        }

        private static string CreateNoteFromTopic(PungentUtilityHelpTopic topic)
        {
            return CreateNoteForAnchor(topic, PungentUtilityHelpAnchors.Topic, topic == null ? string.Empty : topic.summary);
        }

        private static string CreateNoteForAnchor(PungentUtilityHelpTopic topic, string anchorId, string selectedText)
        {
            if (topic == null)
                return string.Empty;

            string anchor = NormalizeAnchor(anchorId);
            string titleSuffix = anchor == PungentUtilityHelpAnchors.Topic ? string.Empty : " / " + anchor;
            PungentNote note = PungentNoteStorage.Database.CreateNote((string.IsNullOrWhiteSpace(topic.title) ? "Help Topic Note" : topic.title) + titleSuffix, PungentNoteKind.UtilityNote);
            note.body = (selectedText ?? topic.summary ?? string.Empty) + "\n\nHelp topic: " + topic.StableId + "\nAnchor: " + anchor;
            note.visibility = PungentNoteVisibility.PrivateProject;
            note.priority = PungentNotePriority.NiceToHave;
            note.status = PungentNoteStatus.ToDo;
            note.linkedUtilityId = topic.utilityId;
            note.stableKey = BuildStableKey(topic.StableId, anchor);
            EnsureCollections(note);
            AddTag(note, "help");
            AddTag(note, "documentation");
            AddTag(note, "annotation");
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.RegisteredUtility,
                utilityId = topic.utilityId,
                label = topic.utilityId
            });

            PungentNoteStorage.Save();
            PungentUtilityHelpNotesBridge.NotifyChanged();
            PungentNotesRoadmapWindow.OpenAndSelect(note.id);
            return note.id;
        }

        private static string CreateBookmark(PungentUtilityHelpTopic topic, string anchorId)
        {
            if (topic == null)
                return string.Empty;

            string anchor = NormalizeAnchor(anchorId);
            PungentNote note = PungentNoteStorage.Database.CreateNote("Bookmark: " + (string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title), PungentNoteKind.UtilityNote);
            note.body = "Bookmark for help topic " + topic.StableId + " at " + anchor + ".";
            note.visibility = PungentNoteVisibility.PrivateProject;
            note.priority = PungentNotePriority.Low;
            note.status = PungentNoteStatus.Complete;
            note.linkedUtilityId = topic.utilityId;
            note.stableKey = BuildStableKey(topic.StableId, anchor);
            EnsureCollections(note);
            AddTag(note, "help");
            AddTag(note, "bookmark");
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.RegisteredUtility,
                utilityId = topic.utilityId,
                label = topic.utilityId
            });
            PungentNoteStorage.Save();
            PungentUtilityHelpNotesBridge.NotifyChanged();
            return note.id;
        }

        private static void LinkExistingNote(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return;

            PungentNotesRoadmapWindow.Open();
            EditorUtility.DisplayDialog("Link Existing Note", "Open Notes & Roadmap, copy the note ID, then add it to this help topic's related notes from developer storage.", "OK");
        }

        private static void OpenRelatedNotes(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
            {
                PungentNotesRoadmapWindow.Open();
                return;
            }

            List<PungentUtilityHelpAnnotation> annotations = GetAnnotationsForTopic(topic.StableId);
            string noteId = annotations.FirstOrDefault(a => a.kind == PungentUtilityHelpAnnotationKind.Note && !string.IsNullOrWhiteSpace(a.noteId))?.noteId;
            if (string.IsNullOrWhiteSpace(noteId) && topic.relatedNoteIds != null)
                noteId = topic.relatedNoteIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

            if (string.IsNullOrWhiteSpace(noteId))
                PungentNotesRoadmapWindow.Open();
            else
                PungentNotesRoadmapWindow.OpenAndSelect(noteId);
        }

        private static void OpenNote(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                PungentNotesRoadmapWindow.Open();
            else
                PungentNotesRoadmapWindow.OpenAndSelect(noteId);
        }

        private static string CreateNoteFromDocumentationLink(PungentUtilityDocumentationLinks.DocumentationLink documentationLink)
        {
            if (documentationLink == null)
                return string.Empty;

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(documentationLink);
            PungentNote note = PungentNoteStorage.Database.CreateNote(PungentUtilityDocumentationLinks.GetDisplayName(documentationLink), PungentNoteKind.UtilityNote);
            note.body = BuildDocumentationLinkNoteBody(documentationLink, status);
            note.visibility = PungentNoteVisibility.PrivateProject;
            note.priority = PungentNotePriority.NiceToHave;
            note.status = PungentNoteStatus.ToDo;
            note.linkedUtilityId = documentationLink.utilityIds == null ? string.Empty : documentationLink.utilityIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
            EnsureCollections(note);
            AddTag(note, "documentation");
            AddTag(note, "documentation-link");
            PungentNoteStorage.AddDocumentationLinkTarget(note, documentationLink);
            PungentUtilityHelpNotesBridge.NotifyChanged();
            PungentNotesRoadmapWindow.OpenAndSelect(note.id);
            return note.id;
        }

        private static void AttachDocumentationLinkToSelectedNote(string documentationLinkId)
        {
            if (string.IsNullOrWhiteSpace(documentationLinkId))
                return;

            PungentNotesRoadmapWindow.AttachDocumentationLinkToSelectedOrNew(documentationLinkId);
            PungentUtilityHelpNotesBridge.NotifyChanged();
        }

        private static void OpenNotesForDocumentationLink(string documentationLinkId)
        {
            List<PungentNote> notes = PungentNoteStorage.GetNotesReferencingDocumentationLink(documentationLinkId);
            if (notes.Count == 0)
                PungentNotesRoadmapWindow.Open();
            else
                PungentNotesRoadmapWindow.OpenAndSelect(notes[0].id);
        }

        private static int CountNotesForDocumentationLink(string documentationLinkId)
        {
            return PungentNoteStorage.GetNotesReferencingDocumentationLink(documentationLinkId).Count;
        }

        private static List<PungentUtilityHelpAnnotation> GetAnnotationsForTopic(string topicStableId)
        {
            List<PungentUtilityHelpAnnotation> annotations = new List<PungentUtilityHelpAnnotation>();
            if (string.IsNullOrWhiteSpace(topicStableId))
                return annotations;

            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(topicStableId);
            HashSet<string> relatedNoteIds = topic != null && topic.relatedNoteIds != null
                ? new HashSet<string>(topic.relatedNoteIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string prefix = "help:" + topicStableId;
            foreach (PungentNote note in PungentNoteStorage.Database.notes)
            {
                if (note == null || note.archived)
                    continue;

                bool stableMatch = !string.IsNullOrWhiteSpace(note.stableKey) && note.stableKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                bool relatedMatch = relatedNoteIds.Contains(note.id);
                if (!stableMatch && !relatedMatch)
                    continue;

                string anchor = stableMatch ? ParseAnchor(note.stableKey) : PungentUtilityHelpAnchors.Topic;
                bool bookmark = note.tags != null && note.tags.Any(t => string.Equals(t, "bookmark", StringComparison.OrdinalIgnoreCase));
                annotations.Add(new PungentUtilityHelpAnnotation
                {
                    noteId = note.id,
                    topicStableId = topicStableId,
                    anchorId = anchor,
                    title = string.IsNullOrWhiteSpace(note.title) ? (bookmark ? "Bookmark" : "Note") : note.title,
                    excerpt = MakeExcerpt(note.body),
                    kind = bookmark ? PungentUtilityHelpAnnotationKind.Bookmark : PungentUtilityHelpAnnotationKind.Note,
                    editable = true,
                    sourceLabel = "Notes & Roadmap"
                });
            }

            return annotations;
        }

        private static List<PungentUtilityHelpAnnotation> GetAllHelpAnnotations()
        {
            Dictionary<string, List<PungentUtilityHelpTopic>> topicsByRelatedNoteId = BuildRelatedNoteTopicMap();
            List<PungentUtilityHelpAnnotation> annotations = new List<PungentUtilityHelpAnnotation>();
            foreach (PungentNote note in PungentNoteStorage.Database.notes)
            {
                if (note == null || !IsHelpLinkedNote(note, topicsByRelatedNoteId))
                    continue;

                annotations.AddRange(ToHelpAnnotations(note, topicsByRelatedNoteId));
            }

            return annotations;
        }

        private static List<PungentUtilityHelpAnnotation> GetHelpAnnotationsForUtility(string utilityId)
        {
            string normalized = (utilityId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return GetAllHelpAnnotations();

            return GetAllHelpAnnotations()
                .Where(a => a != null && string.Equals(a.utilityId, normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static int CountAllHelpNotes()
        {
            return GetAllHelpAnnotations().Count(a => a != null && !a.archived);
        }

        private static void OpenAllHelpNotes()
        {
            PungentNotesRoadmapWindow.Open();
        }

        private static PungentUtilityHelpTopic PromoteNoteToHelpTopic(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return null;

            PungentNote note = PungentNoteStorage.Database.notes.FirstOrDefault(n => n != null && string.Equals(n.id, noteId, StringComparison.OrdinalIgnoreCase));
            if (note == null)
                return null;

            string utilityId = string.IsNullOrWhiteSpace(note.linkedUtilityId) ? "help-browser" : note.linkedUtilityId;
            string topicId = string.IsNullOrWhiteSpace(note.stableKey) ? note.id : note.stableKey.Replace("help:", string.Empty).Replace("/", "-");
            PungentUtilityHelpTopic topic = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(utilityId, topicId, topicId);
            topic.title = note.title;
            topic.summary = note.body;
            if (topic.relatedNoteIds == null)
                topic.relatedNoteIds = new System.Collections.Generic.List<string>();
            if (topic.tags == null)
                topic.tags = new System.Collections.Generic.List<string>();
            topic.relatedNoteIds.Add(note.id);
            topic.tags.AddRange(note.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
            topic.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
            PungentUtilityHelpStorage.instance.Persist();
            PungentUtilityHelpNotesBridge.NotifyChanged();
            return topic;
        }

        private static string BuildDocumentationLinkNoteBody(PungentUtilityDocumentationLinks.DocumentationLink documentationLink, PungentUtilityDocumentationLinks.DocumentationTargetStatus status)
        {
            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(documentationLink.description))
                lines.Add(documentationLink.description.Trim());

            lines.Add("Documentation link: " + documentationLink.id);
            lines.Add("Target kind: " + status.kindLabel);
            if (!string.IsNullOrWhiteSpace(status.targetValue))
                lines.Add("Target: " + status.targetValue);
            if (!string.IsNullOrWhiteSpace(status.message))
                lines.Add("Status: " + status.message);

            return string.Join("\n", lines.ToArray());
        }

        private static string BuildStableKey(string topicStableId, string anchorId)
        {
            return "help:" + topicStableId + "#" + NormalizeAnchor(anchorId);
        }

        private static Dictionary<string, List<PungentUtilityHelpTopic>> BuildRelatedNoteTopicMap()
        {
            Dictionary<string, List<PungentUtilityHelpTopic>> map = new Dictionary<string, List<PungentUtilityHelpTopic>>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentUtilityHelpTopic topic in PungentUtilityHelpRegistry.AllTopics)
            {
                if (topic == null || topic.relatedNoteIds == null)
                    continue;

                foreach (string noteId in topic.relatedNoteIds)
                {
                    if (string.IsNullOrWhiteSpace(noteId))
                        continue;

                    if (!map.TryGetValue(noteId, out List<PungentUtilityHelpTopic> topics))
                    {
                        topics = new List<PungentUtilityHelpTopic>();
                        map[noteId] = topics;
                    }

                    topics.Add(topic);
                }
            }

            return map;
        }

        private static bool IsHelpLinkedNote(PungentNote note, Dictionary<string, List<PungentUtilityHelpTopic>> topicsByRelatedNoteId)
        {
            if (note == null)
                return false;

            if (!string.IsNullOrWhiteSpace(note.stableKey) && note.stableKey.StartsWith("help:", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(note.id) && topicsByRelatedNoteId != null && topicsByRelatedNoteId.ContainsKey(note.id))
                return true;

            return note.tags != null &&
                   note.tags.Any(tag => string.Equals(tag, "help", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(tag, "documentation", StringComparison.OrdinalIgnoreCase) &&
                                         !string.IsNullOrWhiteSpace(note.linkedUtilityId));
        }

        private static IEnumerable<PungentUtilityHelpAnnotation> ToHelpAnnotations(PungentNote note, Dictionary<string, List<PungentUtilityHelpTopic>> topicsByRelatedNoteId)
        {
            if (note == null)
                yield break;

            if (TryParseHelpStableKey(note.stableKey, out string topicStableId, out string anchorId))
            {
                PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(topicStableId);
                yield return ToAnnotation(note, topic, topicStableId, anchorId);
            }
            else if (topicsByRelatedNoteId != null && !string.IsNullOrWhiteSpace(note.id) && topicsByRelatedNoteId.TryGetValue(note.id, out List<PungentUtilityHelpTopic> topics))
            {
                foreach (PungentUtilityHelpTopic topic in topics)
                    yield return ToAnnotation(note, topic, topic == null ? string.Empty : topic.StableId, PungentUtilityHelpAnchors.Topic);
            }
            else
            {
                string utilityId = note.linkedUtilityId ?? string.Empty;
                PungentUtilityHelpTopic overview = string.IsNullOrWhiteSpace(utilityId) ? null : PungentUtilityHelpRegistry.Find(utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
                yield return ToAnnotation(note, overview, overview == null ? string.Empty : overview.StableId, PungentUtilityHelpAnchors.Topic);
            }
        }

        private static PungentUtilityHelpAnnotation ToAnnotation(PungentNote note, PungentUtilityHelpTopic topic, string topicStableId, string anchorId)
        {
            bool bookmark = note.tags != null && note.tags.Any(t => string.Equals(t, "bookmark", StringComparison.OrdinalIgnoreCase));
            string anchor = NormalizeAnchor(anchorId);
            return new PungentUtilityHelpAnnotation
            {
                noteId = note.id,
                topicStableId = topicStableId ?? string.Empty,
                utilityId = topic == null ? note.linkedUtilityId ?? string.Empty : topic.utilityId,
                anchorId = anchor,
                anchorLabel = ObjectNames.NicifyVariableName(anchor.Replace(":", " ")),
                title = string.IsNullOrWhiteSpace(note.title) ? (bookmark ? "Bookmark" : "Note") : note.title,
                topicTitle = topic == null ? topicStableId ?? string.Empty : topic.title,
                excerpt = MakeExcerpt(note.body),
                kind = bookmark ? PungentUtilityHelpAnnotationKind.Bookmark : PungentUtilityHelpAnnotationKind.Note,
                editable = true,
                archived = note.archived,
                developerOnly = note.developerOnly || note.visibility == PungentNoteVisibility.DeveloperOnly,
                sourceLabel = "Notes & Roadmap",
                statusLabel = note.status.ToString(),
                priorityLabel = note.priority.ToString(),
                createdUtc = note.createdUtc,
                updatedUtc = note.updatedUtc
            };
        }

        private static bool TryParseHelpStableKey(string stableKey, out string topicStableId, out string anchorId)
        {
            topicStableId = string.Empty;
            anchorId = PungentUtilityHelpAnchors.Topic;
            if (string.IsNullOrWhiteSpace(stableKey) || !stableKey.StartsWith("help:", StringComparison.OrdinalIgnoreCase))
                return false;

            string value = stableKey.Substring("help:".Length);
            int hash = value.IndexOf('#');
            if (hash >= 0)
            {
                topicStableId = value.Substring(0, hash);
                if (hash < value.Length - 1)
                    anchorId = NormalizeAnchor(value.Substring(hash + 1));
            }
            else
            {
                topicStableId = value;
            }

            return !string.IsNullOrWhiteSpace(topicStableId);
        }

        private static string ParseAnchor(string stableKey)
        {
            if (string.IsNullOrWhiteSpace(stableKey))
                return PungentUtilityHelpAnchors.Topic;

            int hash = stableKey.IndexOf('#');
            if (hash < 0 || hash >= stableKey.Length - 1)
                return PungentUtilityHelpAnchors.Topic;

            return NormalizeAnchor(stableKey.Substring(hash + 1));
        }

        private static string NormalizeAnchor(string anchorId)
        {
            return string.IsNullOrWhiteSpace(anchorId) ? PungentUtilityHelpAnchors.Topic : anchorId.Trim();
        }

        private static void EnsureCollections(PungentNote note)
        {
            if (note.tags == null)
                note.tags = new List<string>();
            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();
        }

        private static void AddTag(PungentNote note, string tag)
        {
            EnsureCollections(note);
            if (!note.tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                note.tags.Add(tag);
        }

        private static string MakeExcerpt(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            string compact = body.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 140 ? compact : compact.Substring(0, 137) + "...";
        }
    }
#endif
}
