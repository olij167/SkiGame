using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public static class PungentNoteBulkActions
    {
        public static int SetStatus(IEnumerable<PungentNote> notes, PungentNoteStatus status) => Mutate(notes, n => n.status = status);
        public static int SetPriority(IEnumerable<PungentNote> notes, PungentNotePriority priority) => Mutate(notes, n => n.priority = priority);
        public static int SetKind(IEnumerable<PungentNote> notes, PungentNoteKind kind) => Mutate(notes, n => n.kind = kind);
        public static int SetVisibility(IEnumerable<PungentNote> notes, PungentNoteVisibility visibility) => Mutate(notes, n => n.visibility = visibility);
        public static int SetDeveloperOnly(IEnumerable<PungentNote> notes, bool value) => Mutate(notes, n => n.developerOnly = value);
        public static int SetLocked(IEnumerable<PungentNote> notes, bool value) => Mutate(notes, n => n.locked = value);
        public static int SetLinkedUtility(IEnumerable<PungentNote> notes, string utilityId) => Mutate(notes, n => n.linkedUtilityId = utilityId ?? string.Empty);
        public static int SetLinkedFutureUtility(IEnumerable<PungentNote> notes, string futureUtilityId) => Mutate(notes, n => n.linkedFutureUtilityId = futureUtilityId ?? string.Empty);
        public static int SetAuditIssueCode(IEnumerable<PungentNote> notes, string code) => Mutate(notes, n => n.auditIssueCode = code ?? string.Empty);
        public static int Archive(IEnumerable<PungentNote> notes, bool archived) => Mutate(notes, n => n.archived = archived);

        public static int AddTag(IEnumerable<PungentNote> notes, string tag)
        {
            string clean = CleanTag(tag);
            if (string.IsNullOrWhiteSpace(clean))
                return 0;

            return Mutate(notes, n =>
            {
                if (n.tags == null)
                    n.tags = new List<string>();
                if (!n.tags.Any(t => string.Equals(t, clean, StringComparison.OrdinalIgnoreCase)))
                    n.tags.Add(clean);
            });
        }

        public static int RemoveTag(IEnumerable<PungentNote> notes, string tag)
        {
            string clean = CleanTag(tag);
            if (string.IsNullOrWhiteSpace(clean))
                return 0;

            return Mutate(notes, n =>
            {
                if (n.tags != null)
                    n.tags.RemoveAll(t => string.Equals(t, clean, StringComparison.OrdinalIgnoreCase));
            });
        }

        public static int Duplicate(IEnumerable<PungentNote> notes)
        {
            List<PungentNote> list = notes?.Where(n => n != null).ToList() ?? new List<PungentNote>();
            foreach (PungentNote note in list)
            {
                PungentNote copy = PungentNoteStorage.Database.Duplicate(note);
                PungentSupportRequestBridge.CopyRequestMetadata(note, copy);
            }
            PungentNoteStorage.Save();
            return list.Count;
        }

        public static int Delete(IEnumerable<PungentNote> notes)
        {
            List<PungentNote> list = notes?.Where(n => n != null && !n.locked).ToList() ?? new List<PungentNote>();
            foreach (PungentNote note in list)
                PungentNoteStorage.Database.notes.Remove(note);
            PungentNoteStorage.Save();
            return list.Count;
        }

        private static int Mutate(IEnumerable<PungentNote> notes, Action<PungentNote> action)
        {
            int count = 0;
            foreach (PungentNote note in notes?.Where(n => n != null) ?? Enumerable.Empty<PungentNote>())
            {
                action(note);
                PungentNoteStorage.Database.Touch(note);
                count++;
            }
            if (count > 0)
                PungentNoteStorage.Save();
            return count;
        }

        private static string CleanTag(string tag) => (tag ?? string.Empty).Trim().TrimStart('#');
    }
#endif
}
