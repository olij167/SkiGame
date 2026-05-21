using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentDataSheetTestDataProvider : IPungentDocumentationTestDataProvider
    {
        private const string Id = "data-sheets";

        static PungentDataSheetTestDataProvider()
        {
            PungentDocumentationTestDataRegistry.Register(new PungentDataSheetTestDataProvider());
        }

        public string ProviderId => Id;
        public string DisplayName => "Data Sheets";
        public string DocumentType => "Data Sheets";

        public IEnumerable<PungentDocumentationRecord> ListRecords()
        {
            foreach (PungentDataSheet sheet in PungentDataSheetEditorStorage.Database.sheets ?? new List<PungentDataSheet>())
            {
                if (sheet == null)
                    continue;

                yield return new PungentDocumentationRecord
                {
                    providerId = ProviderId,
                    itemId = sheet.id,
                    kind = PungentAuthoringItemKind.DataSheet,
                    typeLabel = DocumentType,
                    title = sheet.title,
                    summary = sheet.summary,
                    archived = sheet.archived,
                    locked = sheet.locked,
                    generated = IsGenerated(sheet),
                    generatedBy = sheet.generatedBy,
                    generatedTemplateId = sheet.generatedTemplateId,
                    generatedUtc = sheet.generatedUtc
                };
            }
        }

        public IEnumerable<PungentGeneratedDocumentTemplate> ListTemplates()
        {
            foreach (PungentDataSheetTemplateDefinition template in PungentDataSheetTemplates.GetTemplates())
            {
                yield return new PungentGeneratedDocumentTemplate
                {
                    providerId = ProviderId,
                    templateId = TemplateId(template),
                    documentType = DocumentType,
                    title = template.title,
                    summary = template.summary
                };
            }
        }

        public void Generate(string templateId, bool replaceExisting, PungentDocumentationManagementResult result)
        {
            PungentDataSheetTemplateDefinition sourceTemplate = PungentDataSheetTemplates.GetTemplates().FirstOrDefault(item => string.Equals(TemplateId(item), templateId, StringComparison.OrdinalIgnoreCase));
            if (sourceTemplate == null)
            {
                result.skipped++;
                return;
            }

            PungentDataSheet existing = FindGenerated(templateId);
            if (existing != null)
            {
                if (existing.locked)
                {
                    result.skippedLocked++;
                    result.Add("Locked data sheet skipped: " + existing.title);
                    return;
                }

                if (!replaceExisting)
                {
                    result.skipped++;
                    return;
                }

                PungentDataSheetEditorStorage.Database.DeleteSheet(existing.id);
            }

            PungentDataSheet sheet = PungentDataSheetEditorStorage.Database.CreateSheet(sourceTemplate.title);
            PungentDataSheetTemplates.ApplyTemplate(sheet, sourceTemplate);
            sheet.id = existing == null ? SafeGeneratedId(templateId) : existing.id;
            sheet.title = sourceTemplate.title;
            sheet.summary = sourceTemplate.summary;
            sheet.tags = MergeTags(sheet.tags, "generated-test-document", "documentation-test-data");
            sheet.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
            sheet.generatedTemplateId = templateId;
            sheet.generatedUtc = DateTime.UtcNow.ToString("o");
            sheet.NormalizeInPlace();
            PungentDataSheetEditorStorage.Database.UpdateSheet(sheet);
            PungentDataSheetEditorStorage.SaveNow();
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
                PungentDataSheet sheet = Find(itemId);
                if (sheet == null)
                {
                    result.skipped++;
                    continue;
                }

                if (sheet.locked)
                {
                    result.skippedLocked++;
                    continue;
                }

                if (requireGenerated && !IsGenerated(sheet))
                {
                    result.skipped++;
                    continue;
                }

                dirty |= PungentDataSheetEditorStorage.Database.DeleteSheet(sheet.id);
                result.removed++;
            }

            if (dirty)
                PungentDataSheetEditorStorage.SaveNow();
        }

        public void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, sheet =>
            {
                sheet.locked = locked;
                if (locked)
                    result.locked++;
                else
                    result.unlocked++;
            }, result);
        }

        public void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, sheet =>
            {
                sheet.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
                sheet.generatedTemplateId = string.IsNullOrWhiteSpace(sheet.generatedTemplateId) ? "adopted-data-sheet-" + sheet.id : sheet.generatedTemplateId;
                sheet.generatedUtc = DateTime.UtcNow.ToString("o");
                result.adopted++;
            }, result);
        }

        private static void Mutate(IEnumerable<string> itemIds, Action<PungentDataSheet> mutation, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentDataSheet sheet = Find(itemId);
                if (sheet == null)
                {
                    result.skipped++;
                    continue;
                }

                mutation(sheet);
                sheet.Touch();
                PungentDataSheetEditorStorage.Database.UpdateSheet(sheet);
                dirty = true;
            }

            if (dirty)
                PungentDataSheetEditorStorage.SaveNow();
        }

        private static PungentDataSheet Find(string itemId)
        {
            return PungentDataSheetEditorStorage.Database.FindSheet(itemId);
        }

        private static PungentDataSheet FindGenerated(string templateId)
        {
            return (PungentDataSheetEditorStorage.Database.sheets ?? new List<PungentDataSheet>()).FirstOrDefault(item => item != null && string.Equals(item.generatedTemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGenerated(PungentDataSheet sheet)
        {
            return sheet != null &&
                   (!string.IsNullOrWhiteSpace(sheet.generatedTemplateId) ||
                    string.Equals(sheet.generatedBy, PungentDocumentationTestDataRegistry.GeneratedBy, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeGeneratedId(string templateId)
        {
            string stable = PungentDocumentationTestDataRegistry.StableGeneratedId(Id, templateId);
            PungentDataSheet existing = Find(stable);
            return existing == null || IsGenerated(existing) ? stable : PungentAuthoringId.NewValue();
        }

        private static string TemplateId(PungentDataSheetTemplateDefinition template)
        {
            return "data-sheet." + PungentDocumentationTestDataRegistry.StableGeneratedId("template", template == null ? "sheet" : template.title).Replace("generated-template-", string.Empty);
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
