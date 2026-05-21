using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentNoteDocumentTestDataProvider : IPungentDocumentationTestDataProvider
    {
        private const string Id = "sticky-notes";

        private static readonly List<PungentGeneratedDocumentTemplate> Templates = new List<PungentGeneratedDocumentTemplate>
        {
            Template("general", "Generated Sticky Note", "Generated sticky-note test data."),
            Template("audit-follow-up", "Generated Audit Follow-up Note", "Generated audit note test data."),
            Template("design-decision", "Generated Design Decision Note", "Generated design-decision note test data.")
        };

        static PungentNoteDocumentTestDataProvider()
        {
            PungentDocumentationTestDataRegistry.Register(new PungentNoteDocumentTestDataProvider());
        }

        public string ProviderId => Id;
        public string DisplayName => "Sticky Notes";
        public string DocumentType => "Sticky Notes";

        public IEnumerable<PungentDocumentationRecord> ListRecords()
        {
            PungentNoteStorage.EnsureLoaded();
            foreach (PungentNote note in PungentNoteStorage.Database.notes ?? new List<PungentNote>())
            {
                if (note == null)
                    continue;

                yield return new PungentDocumentationRecord
                {
                    providerId = ProviderId,
                    itemId = note.id,
                    kind = PungentAuthoringItemKind.LegacyNote,
                    typeLabel = DocumentType,
                    title = note.title,
                    summary = note.body,
                    archived = note.archived,
                    locked = note.locked,
                    generated = IsGenerated(note),
                    generatedBy = note.generatedBy,
                    generatedTemplateId = note.generatedTemplateId,
                    generatedUtc = note.generatedUtc
                };
            }
        }

        public IEnumerable<PungentGeneratedDocumentTemplate> ListTemplates()
        {
            return Templates;
        }

        public void Generate(string templateId, bool replaceExisting, PungentDocumentationManagementResult result)
        {
            PungentGeneratedDocumentTemplate template = Templates.FirstOrDefault(item => string.Equals(item.templateId, templateId, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                result.skipped++;
                return;
            }

            PungentNote existing = FindGenerated(template.templateId);
            if (existing != null)
            {
                if (existing.locked)
                {
                    result.skippedLocked++;
                    result.Add("Locked sticky note skipped: " + existing.title);
                    return;
                }

                if (!replaceExisting)
                {
                    result.skipped++;
                    return;
                }

                PungentNoteStorage.Database.notes.Remove(existing);
            }

            PungentNote note = new PungentNote
            {
                id = existing == null ? SafeGeneratedId(template.templateId) : existing.id,
                title = template.title,
                body = template.summary + Environment.NewLine + Environment.NewLine + "Use this note to test search, filters, overlays, locking, and generated cleanup.",
                kind = KindFor(template.templateId),
                status = PungentNoteStatus.ToDo,
                priority = PungentNotePriority.NiceToHave,
                visibility = PungentNoteVisibility.PrivateProject,
                tags = new List<string> { "generated-test-document", "documentation-test-data" },
                generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy,
                generatedTemplateId = template.templateId,
                generatedUtc = DateTime.UtcNow.ToString("o")
            };
            PungentNoteStorage.Database.notes.Add(note);
            PungentNoteStorage.Save();
            if (existing == null)
                result.created++;
            else
                result.updated++;
        }

        public void Remove(IEnumerable<string> itemIds, bool requireGenerated, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentNote note = Find(itemId);
                if (note == null)
                {
                    result.skipped++;
                    continue;
                }

                if (note.locked)
                {
                    result.skippedLocked++;
                    continue;
                }

                if (requireGenerated && !IsGenerated(note))
                {
                    result.skipped++;
                    continue;
                }

                PungentNoteStorage.Database.notes.Remove(note);
                result.removed++;
                dirty = true;
            }

            if (dirty)
                PungentNoteStorage.Save();
        }

        public void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, note =>
            {
                note.locked = locked;
                if (locked)
                    result.locked++;
                else
                    result.unlocked++;
            }, result);
        }

        public void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, note =>
            {
                note.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
                note.generatedTemplateId = string.IsNullOrWhiteSpace(note.generatedTemplateId) ? "adopted-note-" + note.id : note.generatedTemplateId;
                note.generatedUtc = DateTime.UtcNow.ToString("o");
                result.adopted++;
            }, result);
        }

        private static void Mutate(IEnumerable<string> itemIds, Action<PungentNote> mutation, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentNote note = Find(itemId);
                if (note == null)
                {
                    result.skipped++;
                    continue;
                }

                mutation(note);
                note.updatedUtc = DateTime.UtcNow.ToString("o");
                dirty = true;
            }

            if (dirty)
                PungentNoteStorage.Save();
        }

        private static PungentNote Find(string itemId)
        {
            PungentNoteStorage.EnsureLoaded();
            return (PungentNoteStorage.Database.notes ?? new List<PungentNote>()).FirstOrDefault(item => item != null && string.Equals(item.id, itemId, StringComparison.OrdinalIgnoreCase));
        }

        private static PungentNote FindGenerated(string templateId)
        {
            PungentNoteStorage.EnsureLoaded();
            return (PungentNoteStorage.Database.notes ?? new List<PungentNote>()).FirstOrDefault(item => item != null && string.Equals(item.generatedTemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGenerated(PungentNote note)
        {
            return note != null &&
                   (!string.IsNullOrWhiteSpace(note.generatedTemplateId) ||
                    string.Equals(note.generatedBy, PungentDocumentationTestDataRegistry.GeneratedBy, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeGeneratedId(string templateId)
        {
            string stable = PungentDocumentationTestDataRegistry.StableGeneratedId(Id, templateId);
            return Find(stable) == null ? stable : PungentAuthoringId.NewValue();
        }

        private static PungentNoteKind KindFor(string templateId)
        {
            if (templateId.EndsWith("audit-follow-up", StringComparison.OrdinalIgnoreCase))
                return PungentNoteKind.AuditFollowUp;
            if (templateId.EndsWith("design-decision", StringComparison.OrdinalIgnoreCase))
                return PungentNoteKind.DesignDecision;
            return PungentNoteKind.General;
        }

        private static PungentGeneratedDocumentTemplate Template(string id, string title, string summary)
        {
            return new PungentGeneratedDocumentTemplate
            {
                providerId = Id,
                templateId = "note." + id,
                documentType = "Sticky Notes",
                title = title,
                summary = summary
            };
        }
    }
#endif
}
