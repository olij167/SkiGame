using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentChecklistDocumentTestDataProvider : IPungentDocumentationTestDataProvider
    {
        private static readonly List<PungentGeneratedDocumentTemplate> Templates = new List<PungentGeneratedDocumentTemplate>
        {
            Template("quality-gate", "Quality Gate Checklist", "Generated QA-style checklist test data."),
            Template("to-do", "To-Do List", "Generated to-do checklist test data."),
            Template("review", "Review Checklist", "Generated review checklist test data."),
            Template("release-readiness", "Release Readiness Checklist", "Generated release checklist test data."),
            Template("bug-triage", "Bug Triage List", "Generated bug triage checklist test data.")
        };

        static PungentChecklistDocumentTestDataProvider()
        {
            PungentDocumentationTestDataRegistry.Register(new PungentChecklistDocumentTestDataProvider());
        }

        public string ProviderId => PungentChecklistConstants.ProviderId;
        public string DisplayName => "Checklists";
        public string DocumentType => "Checklists";

        public IEnumerable<PungentDocumentationRecord> ListRecords()
        {
            foreach (PungentChecklistDefinition checklist in PungentChecklistDefinitionRegistry.ProjectDefinitions)
            {
                if (checklist == null)
                    continue;

                yield return new PungentDocumentationRecord
                {
                    providerId = ProviderId,
                    itemId = checklist.checklistId,
                    kind = PungentAuthoringItemKind.Checklist,
                    typeLabel = DocumentType,
                    title = checklist.title,
                    summary = checklist.description,
                    archived = checklist.archived,
                    locked = checklist.locked,
                    generated = IsGenerated(checklist),
                    generatedBy = checklist.generatedBy,
                    generatedTemplateId = checklist.generatedTemplateId,
                    generatedUtc = checklist.generatedUtc
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

            PungentChecklistDefinition existing = FindGenerated(template.templateId);
            if (existing != null)
            {
                if (existing.locked)
                {
                    result.skippedLocked++;
                    result.Add("Locked checklist skipped: " + existing.title);
                    return;
                }

                if (!replaceExisting)
                {
                    result.skipped++;
                    return;
                }

                PungentChecklistDefinitionRegistry.DeleteProjectDefinition(existing.checklistId, out _);
            }

            PungentChecklistDefinition checklist = PungentChecklistDefinitionEditSession.CreateBlank(template.title, ListKindFor(template.templateId), ProfileFor(template.templateId));
            checklist.checklistId = existing == null ? SafeGeneratedId(template.templateId) : existing.checklistId;
            checklist.description = template.summary;
            checklist.tags = new List<string> { "generated-test-document", "documentation-test-data" };
            checklist.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
            checklist.generatedTemplateId = template.templateId;
            checklist.generatedUtc = DateTime.UtcNow.ToString("o");
            checklist.NormalizeInPlace();

            if (PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, PungentChecklistDefinitionRegistry.AllLinkedStateGroups, out string error))
            {
                if (existing == null)
                    result.created++;
                else
                    result.updated++;
            }
            else
            {
                result.skipped++;
                result.Add(error);
            }
        }

        public void Remove(IEnumerable<string> itemIds, bool requireGenerated, PungentDocumentationManagementResult result)
        {
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.ProjectDefinitions.FirstOrDefault(item => item != null && string.Equals(item.checklistId, itemId, StringComparison.OrdinalIgnoreCase));
                if (checklist == null)
                {
                    result.skipped++;
                    continue;
                }

                if (checklist.locked)
                {
                    result.skippedLocked++;
                    continue;
                }

                if (requireGenerated && !IsGenerated(checklist))
                {
                    result.skipped++;
                    continue;
                }

                if (PungentChecklistDefinitionRegistry.DeleteProjectDefinition(checklist.checklistId, out _))
                    result.removed++;
                else
                    result.skipped++;
            }
        }

        public void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result)
        {
            foreach (string itemId in itemIds ?? new string[0])
                Mutate(itemId, checklist =>
                {
                    checklist.locked = locked;
                    if (locked)
                        result.locked++;
                    else
                        result.unlocked++;
                }, result);
        }

        public void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result)
        {
            foreach (string itemId in itemIds ?? new string[0])
                Mutate(itemId, checklist =>
                {
                    checklist.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
                    checklist.generatedTemplateId = string.IsNullOrWhiteSpace(checklist.generatedTemplateId) ? "adopted-checklist-" + checklist.checklistId : checklist.generatedTemplateId;
                    checklist.generatedUtc = DateTime.UtcNow.ToString("o");
                    result.adopted++;
                }, result);
        }

        private static void Mutate(string checklistId, Action<PungentChecklistDefinition> mutation, PungentDocumentationManagementResult result)
        {
            PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.ProjectDefinitions.FirstOrDefault(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
            if (checklist == null || mutation == null)
            {
                result.skipped++;
                return;
            }

            mutation(checklist);
            checklist.Touch();
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, PungentChecklistDefinitionRegistry.AllLinkedStateGroups, out string error))
            {
                result.skipped++;
                result.Add(error);
            }
        }

        private static PungentChecklistDefinition FindGenerated(string templateId)
        {
            return PungentChecklistDefinitionRegistry.ProjectDefinitions.FirstOrDefault(item => item != null && string.Equals(item.generatedTemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGenerated(PungentChecklistDefinition checklist)
        {
            return checklist != null &&
                   (!string.IsNullOrWhiteSpace(checklist.generatedTemplateId) ||
                    string.Equals(checklist.generatedBy, PungentDocumentationTestDataRegistry.GeneratedBy, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeGeneratedId(string templateId)
        {
            string stable = PungentDocumentationTestDataRegistry.StableGeneratedId(PungentChecklistConstants.ProviderId, templateId);
            PungentChecklistDefinition existing = PungentChecklistDefinitionRegistry.ProjectDefinitions.FirstOrDefault(item => item != null && string.Equals(item.checklistId, stable, StringComparison.OrdinalIgnoreCase));
            return existing == null || IsGenerated(existing) ? stable : PungentChecklistDefinitionRegistry.UniqueProjectChecklistId(stable);
        }

        private static PungentGeneratedDocumentTemplate Template(string id, string title, string summary)
        {
            return new PungentGeneratedDocumentTemplate
            {
                providerId = PungentChecklistConstants.ProviderId,
                templateId = "checklist." + id,
                documentType = "Checklists",
                title = title,
                summary = summary
            };
        }

        private static string ListKindFor(string templateId)
        {
            if (templateId.EndsWith("to-do", StringComparison.OrdinalIgnoreCase))
                return PungentChecklistListKinds.ToDo;
            if (templateId.EndsWith("review", StringComparison.OrdinalIgnoreCase))
                return PungentChecklistListKinds.Review;
            if (templateId.EndsWith("release-readiness", StringComparison.OrdinalIgnoreCase))
                return PungentChecklistListKinds.ReleaseReadiness;
            if (templateId.EndsWith("bug-triage", StringComparison.OrdinalIgnoreCase))
                return PungentChecklistListKinds.BugTriage;
            return PungentChecklistListKinds.QualityGate;
        }

        private static string ProfileFor(string templateId)
        {
            return PungentChecklistProfiles.DefaultProfileIdForListKind(ListKindFor(templateId));
        }
    }
#endif
}
