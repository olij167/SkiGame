using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentRichDocumentTestDataProvider : IPungentDocumentationTestDataProvider
    {
        private const string Id = "rich-documents";

        static PungentRichDocumentTestDataProvider()
        {
            PungentDocumentationTestDataRegistry.Register(new PungentRichDocumentTestDataProvider());
        }

        public string ProviderId => Id;
        public string DisplayName => "Rich Documents";
        public string DocumentType => "Rich Documents";

        public IEnumerable<PungentDocumentationRecord> ListRecords()
        {
            foreach (PungentRichDocument document in PungentRichDocumentStorage.Database.documents ?? new List<PungentRichDocument>())
            {
                if (document == null)
                    continue;

                yield return new PungentDocumentationRecord
                {
                    providerId = ProviderId,
                    itemId = document.id,
                    kind = PungentAuthoringItemKind.RichDocument,
                    typeLabel = DocumentType,
                    title = document.title,
                    summary = document.summary,
                    archived = document.archived,
                    locked = document.locked,
                    generated = IsGenerated(document),
                    generatedBy = document.generatedBy,
                    generatedTemplateId = document.generatedTemplateId,
                    generatedUtc = document.generatedUtc
                };
            }
        }

        public IEnumerable<PungentGeneratedDocumentTemplate> ListTemplates()
        {
            foreach (PungentRichDocumentTemplateDefinition template in PungentRichDocumentTemplates.All)
            {
                yield return new PungentGeneratedDocumentTemplate
                {
                    providerId = ProviderId,
                    templateId = "rich-document." + template.id,
                    documentType = DocumentType,
                    title = template.displayName,
                    summary = template.summary
                };
            }
        }

        public void Generate(string templateId, bool replaceExisting, PungentDocumentationManagementResult result)
        {
            PungentGeneratedDocumentTemplate template = ListTemplates().FirstOrDefault(item => string.Equals(item.templateId, templateId, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                result.skipped++;
                return;
            }

            PungentRichDocument existing = FindGenerated(template.templateId);
            if (existing != null)
            {
                if (existing.locked)
                {
                    result.skippedLocked++;
                    result.Add("Locked rich document skipped: " + existing.title);
                    return;
                }

                if (!replaceExisting)
                {
                    result.skipped++;
                    return;
                }

                PungentRichDocumentStorage.Database.Delete(existing.id);
            }

            string sourceTemplateId = template.templateId.Substring("rich-document.".Length);
            PungentRichDocument document = PungentRichDocumentTemplates.CreateDocument(sourceTemplateId);
            document.id = existing == null ? SafeGeneratedId(template.templateId) : existing.id;
            document.title = template.title;
            document.summary = template.summary;
            document.tags = MergeTags(document.tags, "generated-test-document", "documentation-test-data");
            document.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
            document.generatedTemplateId = template.templateId;
            document.generatedUtc = DateTime.UtcNow.ToString("o");
            document.NormalizeInPlace();
            PungentRichDocumentStorage.Database.AddOrUpdate(document);
            PungentRichDocumentStorage.Save(out _);
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
                PungentRichDocument document = Find(itemId);
                if (document == null)
                {
                    result.skipped++;
                    continue;
                }

                if (document.locked)
                {
                    result.skippedLocked++;
                    continue;
                }

                if (requireGenerated && !IsGenerated(document))
                {
                    result.skipped++;
                    continue;
                }

                dirty |= PungentRichDocumentStorage.Database.Delete(document.id);
                result.removed++;
            }

            if (dirty)
                PungentRichDocumentStorage.Save(out _);
        }

        public void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, document =>
            {
                document.locked = locked;
                if (locked)
                    result.locked++;
                else
                    result.unlocked++;
            }, result);
        }

        public void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, document =>
            {
                document.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
                document.generatedTemplateId = string.IsNullOrWhiteSpace(document.generatedTemplateId) ? "adopted-rich-document-" + document.id : document.generatedTemplateId;
                document.generatedUtc = DateTime.UtcNow.ToString("o");
                result.adopted++;
            }, result);
        }

        private static void Mutate(IEnumerable<string> itemIds, Action<PungentRichDocument> mutation, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentRichDocument document = Find(itemId);
                if (document == null)
                {
                    result.skipped++;
                    continue;
                }

                mutation(document);
                document.Touch();
                PungentRichDocumentStorage.Database.AddOrUpdate(document);
                dirty = true;
            }

            if (dirty)
                PungentRichDocumentStorage.Save(out _);
        }

        private static PungentRichDocument Find(string itemId)
        {
            return PungentRichDocumentStorage.Database.Find(itemId);
        }

        private static PungentRichDocument FindGenerated(string templateId)
        {
            return (PungentRichDocumentStorage.Database.documents ?? new List<PungentRichDocument>()).FirstOrDefault(item => item != null && string.Equals(item.generatedTemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGenerated(PungentRichDocument document)
        {
            return document != null &&
                   (!string.IsNullOrWhiteSpace(document.generatedTemplateId) ||
                    string.Equals(document.generatedBy, PungentDocumentationTestDataRegistry.GeneratedBy, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeGeneratedId(string templateId)
        {
            string stable = PungentDocumentationTestDataRegistry.StableGeneratedId(Id, templateId);
            PungentRichDocument existing = Find(stable);
            return existing == null || IsGenerated(existing) ? stable : PungentAuthoringId.NewValue();
        }

        private static List<string> MergeTags(List<string> source, params string[] tags)
        {
            HashSet<string> result = new HashSet<string>(source ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (string tag in tags ?? new string[0])
                if (!string.IsNullOrWhiteSpace(tag))
                    result.Add(tag);
            return result.ToList();
        }
    }
#endif
}
