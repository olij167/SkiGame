using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public enum PungentNotesSavedView
    {
        AllNotes,
        Inbox,
        FutureFeatures,
        CurrentUtilities,
        FutureUtilities,
        AuditFollowUps,
        ImplementationTasks,
        DesignDecisions,
        TokenDocumentation,
        UnlinkedNotes,
        Archived,
        DeveloperNotes,
        NeedsAttention,
        Blocked,
        InProgress,
        RecentlyUpdated,
        FutureUtilitiesNotImplemented,
        ImplementedFutureUtilities
    }

    public enum PungentNotesSortMode
    {
        Updated,
        Priority,
        Status,
        Kind,
        Utility
    }

    public enum PungentNotesGroupMode
    {
        None,
        Status,
        Priority,
        Kind,
        Utility
    }

    public sealed class PungentNoteFilterState
    {
        public string search = string.Empty;
        public PungentNotesSavedView view = PungentNotesSavedView.AllNotes;
        public PungentNotesSortMode sortMode = PungentNotesSortMode.Updated;
        public bool showArchived;
        public bool showDeveloperNotes;
        public PungentNoteStatus? status;
        public PungentNotePriority? priority;
        public PungentNoteKind? kind;
        public string linkedUtilityId = string.Empty;
        public string tag = string.Empty;
        public string importSourceId = string.Empty;
    }

    public static class PungentNoteFilters
    {
        public static List<PungentNote> Query(IEnumerable<PungentNote> notes, PungentNoteFilterState filters)
        {
            IEnumerable<PungentNote> query = notes ?? Enumerable.Empty<PungentNote>();
            filters = filters ?? new PungentNoteFilterState();

            query = query.Where(n => n != null);
            if (!filters.showArchived && filters.view != PungentNotesSavedView.Archived)
                query = query.Where(n => !n.archived);
            if (!filters.showDeveloperNotes && filters.view != PungentNotesSavedView.DeveloperNotes)
                query = query.Where(n => !n.developerOnly);

            query = ApplySavedView(query, filters.view);

            if (filters.status.HasValue)
                query = query.Where(n => n.status == filters.status.Value);
            if (filters.priority.HasValue)
                query = query.Where(n => n.priority == filters.priority.Value);
            if (filters.kind.HasValue)
                query = query.Where(n => n.kind == filters.kind.Value);
            if (!string.IsNullOrWhiteSpace(filters.linkedUtilityId))
                query = query.Where(n => string.Equals(n.linkedUtilityId, filters.linkedUtilityId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(filters.tag))
                query = query.Where(n => HasTag(n, filters.tag));
            if (!string.IsNullOrWhiteSpace(filters.importSourceId))
                query = query.Where(n => n.importSourceIds != null && n.importSourceIds.Contains(filters.importSourceId));
            if (!string.IsNullOrWhiteSpace(filters.search))
                query = query.Where(n => MatchesSearch(n, filters.search));

            return Sort(query, filters.sortMode).ToList();
        }

        public static int CountForView(IEnumerable<PungentNote> notes, PungentNotesSavedView view, bool showArchived, bool showDeveloperNotes)
        {
            PungentNoteFilterState filters = new PungentNoteFilterState
            {
                view = view,
                showArchived = showArchived,
                showDeveloperNotes = showDeveloperNotes
            };
            return Query(notes, filters).Count;
        }

        public static bool MatchesSearch(PungentNote note, string search)
        {
            if (note == null || string.IsNullOrWhiteSpace(search))
                return true;

            string q = search.Trim();
            return Contains(note.title, q) ||
                   Contains(note.body, q) ||
                   Contains(note.linkedUtilityId, q) ||
                   Contains(note.linkedFutureUtilityId, q) ||
                   Contains(note.auditIssueCode, q) ||
                   note.kind.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   note.status.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   note.priority.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (note.tags != null && note.tags.Any(t => Contains(t, q))) ||
                   (note.targets != null && note.targets.Any(t =>
                       Contains(t.label, q) ||
                       Contains(t.propertyPath, q) ||
                       Contains(t.componentType, q) ||
                       Contains(t.documentationLinkId, q) ||
                       Contains(t.externalPathOrUrl, q)));
        }

        public static IEnumerable<PungentNote> ApplySavedView(IEnumerable<PungentNote> query, PungentNotesSavedView view)
        {
            switch (view)
            {
                case PungentNotesSavedView.Inbox:
                    return query.Where(n => string.IsNullOrWhiteSpace(n.linkedUtilityId) && string.IsNullOrWhiteSpace(n.linkedFutureUtilityId) && n.kind == PungentNoteKind.General);
                case PungentNotesSavedView.FutureFeatures:
                    return query.Where(n => n.kind == PungentNoteKind.FutureFeature || n.kind == PungentNoteKind.OutOfScopeIdea);
                case PungentNotesSavedView.CurrentUtilities:
                    return query.Where(n => !string.IsNullOrWhiteSpace(n.linkedUtilityId));
                case PungentNotesSavedView.FutureUtilities:
                    return query.Where(n => n.kind == PungentNoteKind.FutureUtility || !string.IsNullOrWhiteSpace(n.linkedFutureUtilityId));
                case PungentNotesSavedView.AuditFollowUps:
                    return query.Where(n => n.kind == PungentNoteKind.AuditFollowUp || !string.IsNullOrWhiteSpace(n.auditIssueCode));
                case PungentNotesSavedView.ImplementationTasks:
                    return query.Where(n => n.kind == PungentNoteKind.ImplementationTask);
                case PungentNotesSavedView.DesignDecisions:
                    return query.Where(n => n.kind == PungentNoteKind.DesignDecision);
                case PungentNotesSavedView.TokenDocumentation:
                    return query.Where(n => n.kind == PungentNoteKind.TokenDocumentation || (n.linkedTokenKeys != null && n.linkedTokenKeys.Count > 0));
                case PungentNotesSavedView.UnlinkedNotes:
                    return query.Where(n => string.IsNullOrWhiteSpace(n.linkedUtilityId) && string.IsNullOrWhiteSpace(n.linkedFutureUtilityId) && string.IsNullOrWhiteSpace(n.auditIssueCode) && (n.targets == null || n.targets.Count == 0));
                case PungentNotesSavedView.Archived:
                    return query.Where(n => n.archived);
                case PungentNotesSavedView.DeveloperNotes:
                    return query.Where(n => n.developerOnly || n.visibility == PungentNoteVisibility.DeveloperOnly);
                case PungentNotesSavedView.NeedsAttention:
                    return query.Where(n => (n.priority == PungentNotePriority.Crucial || n.priority == PungentNotePriority.Important) && n.status != PungentNoteStatus.Complete && n.status != PungentNoteStatus.OutOfScope);
                case PungentNotesSavedView.Blocked:
                    return query.Where(n => n.status == PungentNoteStatus.Blocked);
                case PungentNotesSavedView.InProgress:
                    return query.Where(n => n.status == PungentNoteStatus.InProgress);
                case PungentNotesSavedView.RecentlyUpdated:
                    return query.Where(n => ParseDate(n.updatedUtc) >= DateTime.UtcNow.AddDays(-14d));
                case PungentNotesSavedView.FutureUtilitiesNotImplemented:
                    return query.Where(n => n.kind == PungentNoteKind.FutureUtility && n.status != PungentNoteStatus.Complete && n.status != PungentNoteStatus.OutOfScope);
                case PungentNotesSavedView.ImplementedFutureUtilities:
                    return query.Where(n => n.kind == PungentNoteKind.FutureUtility && n.status == PungentNoteStatus.Complete);
                default:
                    return query;
            }
        }

        private static IEnumerable<PungentNote> Sort(IEnumerable<PungentNote> query, PungentNotesSortMode sortMode)
        {
            switch (sortMode)
            {
                case PungentNotesSortMode.Priority:
                    return query.OrderBy(n => n.priority).ThenByDescending(n => ParseDate(n.updatedUtc));
                case PungentNotesSortMode.Status:
                    return query.OrderBy(n => n.status).ThenBy(n => n.priority);
                case PungentNotesSortMode.Kind:
                    return query.OrderBy(n => n.kind).ThenBy(n => n.title, StringComparer.OrdinalIgnoreCase);
                case PungentNotesSortMode.Utility:
                    return query.OrderBy(n => string.IsNullOrEmpty(n.linkedUtilityId) ? n.linkedFutureUtilityId : n.linkedUtilityId, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.title, StringComparer.OrdinalIgnoreCase);
                default:
                    return query.OrderByDescending(n => ParseDate(n.updatedUtc));
            }
        }

        private static bool HasTag(PungentNote note, string tag)
        {
            return note != null &&
                   note.tags != null &&
                   note.tags.Any(t => string.Equals(t, tag.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase));
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
#endif
}
