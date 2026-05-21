using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Checklists;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetChecklistBridge
    {
        private const string ChecklistIdColumn = "Checklist ID";
        private const string ChecklistTitleColumn = "Checklist Title";
        private const string ListKindColumn = "List Kind";
        private const string StateProfileColumn = "State Profile";
        private const string TargetUtilityIdColumn = "Target Utility ID";
        private const string TagsColumn = "Tags";
        private const string SectionIdColumn = "Section ID";
        private const string SectionTitleColumn = "Section Title";
        private const string GuidancePromptColumn = "Guidance Prompt";
        private const string ItemIdColumn = "Item ID";
        private const string LabelColumn = "Label";
        private const string DetailColumn = "Detail";
        private const string OptionalColumn = "Optional";
        private const string StateColumn = "State";
        private const string CommentColumn = "Comment";
        private const string PriorityColumn = "Priority";
        private const string OwnerColumn = "Owner";
        private const string DueDateColumn = "Due Date";
        private const string ArchivedColumn = "Archived";
        private const string LinkedStateKeyColumn = "Linked State Key";
        private const string OwnerPackageColumn = "Owner Package";
        private const string CanonicalPackageColumn = "Canonical Package";
        private const string AppearsInPackagesColumn = "Appears In Packages";
        private const string ChildChecklistIdColumn = "Child Checklist ID";
        private const string ChildPassRuleColumn = "Child Pass Rule";

        public static void PopulateChecklistTemplateCells(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            List<PungentDataSheetRow> rows = (sheet.rows ?? new List<PungentDataSheetRow>()).OrderBy(row => row.order).ToList();
            if (rows.Count == 0)
                return;

            SetValue(sheet, rows[0], ChecklistIdColumn, "example-checklist");
            SetValue(sheet, rows[0], ChecklistTitleColumn, "Example Checklist");
            SetValue(sheet, rows[0], ListKindColumn, PungentChecklistListKinds.QualityGate);
            SetValue(sheet, rows[0], StateProfileColumn, PungentChecklistProfileIds.QualityGate);
            SetValue(sheet, rows[0], TargetUtilityIdColumn, PungentChecklistConstants.UtilityId);
            SetValue(sheet, rows[0], TagsColumn, "example, checklist");
            SetValue(sheet, rows[0], SectionIdColumn, "setup");
            SetValue(sheet, rows[0], SectionTitleColumn, "Setup");
            SetValue(sheet, rows[0], GuidancePromptColumn, "Resolve setup issues before continuing.");
            SetValue(sheet, rows[0], ItemIdColumn, "setup-01");
            SetValue(sheet, rows[0], LabelColumn, "Open the target workflow.");
            SetValue(sheet, rows[0], DetailColumn, "Name the exact window, inspector, scene, or asset state to verify.");
            SetValue(sheet, rows[0], StateColumn, PungentChecklistProfiles.StateUntested);
            SetValue(sheet, rows[0], PriorityColumn, "Normal");
            SetValue(sheet, rows[0], OwnerColumn, "Author");
            SetValue(sheet, rows[0], OwnerPackageColumn, "com.pungentfunk.utilities.core");

            if (rows.Count > 1)
            {
                SetValue(sheet, rows[1], ChecklistIdColumn, "example-checklist");
                SetValue(sheet, rows[1], ChecklistTitleColumn, "Example Checklist");
                SetValue(sheet, rows[1], SectionIdColumn, "validation");
                SetValue(sheet, rows[1], SectionTitleColumn, "Validation");
                SetValue(sheet, rows[1], GuidancePromptColumn, "Capture failed validation as follow-up notes.");
                SetValue(sheet, rows[1], ItemIdColumn, "validation-01");
                SetValue(sheet, rows[1], LabelColumn, "Run the main validation path.");
                SetValue(sheet, rows[1], DetailColumn, "Mark partial if the workflow succeeds but needs polish.");
                SetValue(sheet, rows[1], StateColumn, PungentChecklistProfiles.StateUntested);
                SetValue(sheet, rows[1], PriorityColumn, "High");
                SetValue(sheet, rows[1], OwnerPackageColumn, "com.pungentfunk.utilities.core");
            }

            if (rows.Count > 2)
            {
                SetValue(sheet, rows[2], ChecklistIdColumn, "example-checklist");
                SetValue(sheet, rows[2], ChecklistTitleColumn, "Example Checklist");
                SetValue(sheet, rows[2], SectionIdColumn, "follow-up");
                SetValue(sheet, rows[2], SectionTitleColumn, "Follow-Up");
                SetValue(sheet, rows[2], GuidancePromptColumn, "Review follow-up items before release.");
                SetValue(sheet, rows[2], ItemIdColumn, "follow-up-01");
                SetValue(sheet, rows[2], LabelColumn, "Copy results or guidance for unresolved work.");
                SetValue(sheet, rows[2], DetailColumn, "Use exported results when handing work to another person.");
                SetValue(sheet, rows[2], StateColumn, PungentChecklistProfiles.StateUntested);
            }
        }

        public static bool TryBuildChecklistDefinition(
            PungentDataSheet sheet,
            out PungentChecklistDefinition checklist,
            out List<PungentLinkedStateGroupDefinition> linkedStateGroups,
            out string error)
        {
            checklist = null;
            linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
            error = string.Empty;
            if (sheet == null)
            {
                error = "Data Sheet is missing.";
                return false;
            }

            sheet.NormalizeInPlace();
            Dictionary<string, PungentDataSheetColumn> columns = BuildColumnMap(sheet);
            if (!columns.ContainsKey(LabelColumn))
            {
                error = "Checklist Definition Sheet is missing the '" + LabelColumn + "' column.";
                return false;
            }

            List<PungentDataSheetRow> rows = (sheet.rows ?? new List<PungentDataSheetRow>()).Where(row => row != null).OrderBy(row => row.order).ToList();
            string checklistId = FirstValue(sheet, rows, columns, ChecklistIdColumn);
            if (string.IsNullOrWhiteSpace(checklistId))
                checklistId = PungentChecklistSerialization.Slug(sheet.title, sheet.id);
            string checklistTitle = FirstValue(sheet, rows, columns, ChecklistTitleColumn);
            if (string.IsNullOrWhiteSpace(checklistTitle))
                checklistTitle = string.IsNullOrWhiteSpace(sheet.title) ? "Checklist Definition" : sheet.title;

            checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = checklistId,
                title = checklistTitle,
                description = string.IsNullOrWhiteSpace(sheet.summary) ? "Checklist generated from Data Sheet '" + sheet.title + "'." : sheet.summary,
                listKind = FirstValue(sheet, rows, columns, ListKindColumn),
                stateProfileId = FirstValue(sheet, rows, columns, StateProfileColumn),
                targetUtilityId = FirstValue(sheet, rows, columns, TargetUtilityIdColumn),
                sourceProviderId = PungentDataSheetProvider.Id,
                sourceItemId = sheet.id,
                sourceLabel = sheet.title,
                tags = SplitTags(FirstValue(sheet, rows, columns, TagsColumn)),
                sections = new List<PungentChecklistSectionDefinition>()
            };

            Dictionary<string, PungentChecklistSectionDefinition> sections = new Dictionary<string, PungentChecklistSectionDefinition>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedSectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedLinkedStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PungentDataSheetRow row in rows)
            {
                string label = Value(sheet, row, columns, LabelColumn);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                string sectionTitle = Value(sheet, row, columns, SectionTitleColumn);
                if (string.IsNullOrWhiteSpace(sectionTitle))
                    sectionTitle = "Checklist";
                string sectionKey = Value(sheet, row, columns, SectionIdColumn);
                if (string.IsNullOrWhiteSpace(sectionKey))
                    sectionKey = sectionTitle;

                if (!sections.TryGetValue(sectionKey, out PungentChecklistSectionDefinition section))
                {
                    string explicitSectionId = Value(sheet, row, columns, SectionIdColumn);
                    string sectionId = string.IsNullOrWhiteSpace(explicitSectionId)
                        ? PungentChecklistSerialization.UniqueId(sectionTitle, usedSectionIds, "section")
                        : PungentChecklistSerialization.UniqueId(explicitSectionId, usedSectionIds, "section");
                    section = new PungentChecklistSectionDefinition
                    {
                        id = sectionId,
                        title = sectionTitle,
                        guidancePrompt = Value(sheet, row, columns, GuidancePromptColumn),
                        archived = ParseBool(Value(sheet, row, columns, ArchivedColumn)),
                        priority = Value(sheet, row, columns, PriorityColumn),
                        owner = Value(sheet, row, columns, OwnerColumn),
                        dueUtc = Value(sheet, row, columns, DueDateColumn),
                        items = new List<PungentChecklistItemDefinition>()
                    };
                    sections[sectionKey] = section;
                    checklist.sections.Add(section);
                }
                else if (string.IsNullOrWhiteSpace(section.guidancePrompt))
                {
                    section.guidancePrompt = Value(sheet, row, columns, GuidancePromptColumn);
                }

                string itemId = Value(sheet, row, columns, ItemIdColumn);
                if (string.IsNullOrWhiteSpace(itemId))
                    itemId = section.id + "-" + label;

                string childChecklistId = Value(sheet, row, columns, ChildChecklistIdColumn);
                string linkedStateKey = Value(sheet, row, columns, LinkedStateKeyColumn);
                PungentChecklistItemDefinition item = new PungentChecklistItemDefinition
                {
                    id = PungentChecklistSerialization.UniqueId(itemId, usedItemIds, "item"),
                    label = label,
                    detail = Value(sheet, row, columns, DetailColumn),
                    isOptional = ParseBool(Value(sheet, row, columns, OptionalColumn)),
                    archived = ParseBool(Value(sheet, row, columns, ArchivedColumn)),
                    priority = Value(sheet, row, columns, PriorityColumn),
                    owner = Value(sheet, row, columns, OwnerColumn),
                    dueUtc = Value(sheet, row, columns, DueDateColumn),
                    linkedStateKey = linkedStateKey,
                    ownerPackageId = Value(sheet, row, columns, OwnerPackageColumn),
                    canonicalOwnerPackageId = Value(sheet, row, columns, CanonicalPackageColumn),
                    appearsInPackageIds = SplitTags(Value(sheet, row, columns, AppearsInPackagesColumn)),
                    childChecklistId = childChecklistId,
                    childPassRule = Value(sheet, row, columns, ChildPassRuleColumn)
                };
                if (!string.IsNullOrWhiteSpace(childChecklistId))
                    item.parentStateMode = PungentChecklistConstants.ParentStateComputedFromChild;
                if (string.IsNullOrWhiteSpace(item.childPassRule))
                    item.childPassRule = PungentChecklistConstants.ChildPassRuleAllRequiredPass;

                section.items.Add(item);

                if (!string.IsNullOrWhiteSpace(linkedStateKey) && usedLinkedStateKeys.Add(linkedStateKey))
                {
                    linkedStateGroups.Add(new PungentLinkedStateGroupDefinition
                    {
                        linkedStateKey = linkedStateKey,
                        label = label,
                        ownerPackageId = item.ownerPackageId,
                        sourceProviderId = PungentDataSheetProvider.Id,
                        sourceItemId = sheet.id
                    });
                }
            }

            checklist.NormalizeInPlace();
            if (!PungentChecklistSerialization.ValidateChecklist(checklist, out error))
                return false;

            return true;
        }

        public static bool CreateOrUpdateChecklistDefinition(PungentDataSheet sheet, out string checklistId, out string error)
        {
            checklistId = string.Empty;
            if (!TryBuildChecklistDefinition(sheet, out PungentChecklistDefinition checklist, out List<PungentLinkedStateGroupDefinition> linkedStateGroups, out error))
                return false;

            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, linkedStateGroups, out error))
                return false;

            checklistId = checklist.checklistId;
            EnsureChecklistReference(sheet, checklist);
            return true;
        }

        public static bool TryCreateSheetFromChecklist(string checklistId, out PungentDataSheet sheet, out string error)
        {
            sheet = null;
            error = string.Empty;
            PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.Find(checklistId);
            if (checklist == null)
            {
                error = "Checklist '" + checklistId + "' could not be found.";
                return false;
            }

            sheet = PungentDataSheetEditorStorage.CreateSheet((string.IsNullOrWhiteSpace(checklist.title) ? checklist.checklistId : checklist.title) + " Sheet");
            SyncChecklistToSheet(sheet, checklist);
            PungentDataSheetEditorStorage.UpsertSheet(sheet, true);
            return true;
        }

        public static bool TrySyncLinkedChecklistToSheet(PungentDataSheet sheet, out string checklistId, out string error)
        {
            checklistId = string.Empty;
            error = string.Empty;
            if (!TryFindLinkedChecklist(sheet, out PungentChecklistDefinition checklist, out error))
                return false;

            SyncChecklistToSheet(sheet, checklist);
            checklistId = checklist.checklistId;
            PungentDataSheetEditorStorage.UpsertSheet(sheet, true);
            return true;
        }

        public static bool TryExportResultsSheet(string checklistId, out PungentDataSheet sheet, out string error)
        {
            return TryCreateSheetFromChecklist(checklistId, out sheet, out error);
        }

        public static bool TryFindLinkedChecklist(PungentDataSheet sheet, out PungentChecklistDefinition checklist, out string error)
        {
            checklist = null;
            error = string.Empty;
            PungentAuthoringReference reference = sheet?.references == null
                ? null
                : sheet.references.FirstOrDefault(item => item != null && item.itemKind == PungentAuthoringItemKind.Checklist && !string.IsNullOrWhiteSpace(item.itemId));
            if (reference == null)
            {
                error = "Data Sheet has no linked checklist reference.";
                return false;
            }

            checklist = PungentChecklistDefinitionRegistry.Find(reference.itemId);
            if (checklist == null)
            {
                error = "Linked checklist '" + reference.itemId + "' could not be found.";
                return false;
            }

            return true;
        }

        public static void SyncChecklistToSheet(PungentDataSheet sheet, PungentChecklistDefinition checklist)
        {
            if (sheet == null || checklist == null)
                return;

            PungentDataSheetTemplates.ApplyTemplate(sheet, PungentDataSheetTemplates.GetChecklistDefinitionTemplate());
            sheet.title = (string.IsNullOrWhiteSpace(checklist.title) ? checklist.checklistId : checklist.title) + " Sheet";
            sheet.summary = "Checklist definition and current local results for '" + checklist.checklistId + "'.";
            sheet.tags = PungentAuthoringMetadata.NormalizeTags(checklist.tags);
            sheet.rows = new List<PungentDataSheetRow>();
            sheet.cells = new List<PungentDataSheetCell>();

            int order = 0;
            foreach (PungentChecklistSectionDefinition section in checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null)
                    continue;

                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null)
                        continue;

                    PungentDataSheetRow row = PungentDataSheetRow.Create(string.IsNullOrWhiteSpace(item.label) ? item.id : item.label, order++);
                    sheet.rows.Add(row);
                    SetValue(sheet, row, ChecklistIdColumn, checklist.checklistId);
                    SetValue(sheet, row, ChecklistTitleColumn, checklist.title);
                    SetValue(sheet, row, ListKindColumn, checklist.listKind);
                    SetValue(sheet, row, StateProfileColumn, checklist.stateProfileId);
                    SetValue(sheet, row, TargetUtilityIdColumn, checklist.targetUtilityId);
                    SetValue(sheet, row, TagsColumn, string.Join(", ", (checklist.tags ?? new List<string>()).ToArray()));
                    SetValue(sheet, row, SectionIdColumn, section.id);
                    SetValue(sheet, row, SectionTitleColumn, section.title);
                    SetValue(sheet, row, GuidancePromptColumn, section.guidancePrompt);
                    SetValue(sheet, row, ItemIdColumn, item.id);
                    SetValue(sheet, row, LabelColumn, item.label);
                    SetValue(sheet, row, DetailColumn, item.detail);
                    SetValue(sheet, row, OptionalColumn, item.isOptional ? "true" : "false");
                    SetValue(sheet, row, StateColumn, PungentChecklistUtilityStateService.GetStateId(checklist, item, PungentChecklistDefinitionRegistry.Find));
                    SetValue(sheet, row, CommentColumn, PungentChecklistUtilityStateService.GetComment(checklist, item.id));
                    SetValue(sheet, row, PriorityColumn, item.priority);
                    SetValue(sheet, row, OwnerColumn, item.owner);
                    SetValue(sheet, row, DueDateColumn, item.dueUtc);
                    SetValue(sheet, row, ArchivedColumn, item.archived ? "true" : "false");
                    SetValue(sheet, row, LinkedStateKeyColumn, item.linkedStateKey);
                    SetValue(sheet, row, OwnerPackageColumn, item.ownerPackageId);
                    SetValue(sheet, row, CanonicalPackageColumn, item.canonicalOwnerPackageId);
                    SetValue(sheet, row, AppearsInPackagesColumn, string.Join(", ", (item.appearsInPackageIds ?? new List<string>()).ToArray()));
                    SetValue(sheet, row, ChildChecklistIdColumn, item.childChecklistId);
                    SetValue(sheet, row, ChildPassRuleColumn, item.childPassRule);
                }
            }

            EnsureChecklistReference(sheet, checklist);
            sheet.Touch();
            sheet.NormalizeInPlace();
        }

        private static void EnsureChecklistReference(PungentDataSheet sheet, PungentChecklistDefinition checklist)
        {
            if (sheet == null || checklist == null)
                return;

            if (sheet.references == null)
                sheet.references = new List<PungentAuthoringReference>();
            if (sheet.references.Any(reference => reference != null &&
                                                  reference.itemKind == PungentAuthoringItemKind.Checklist &&
                                                  PungentAuthoringId.EqualsId(reference.itemId, checklist.checklistId)))
                return;

            sheet.references.Add(checklist.ToReference());
        }

        private static Dictionary<string, PungentDataSheetColumn> BuildColumnMap(PungentDataSheet sheet)
        {
            Dictionary<string, PungentDataSheetColumn> columns = new Dictionary<string, PungentDataSheetColumn>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                if (column == null || string.IsNullOrWhiteSpace(column.displayName) || columns.ContainsKey(column.displayName.Trim()))
                    continue;
                columns.Add(column.displayName.Trim(), column);
            }

            return columns;
        }

        private static string FirstValue(PungentDataSheet sheet, IEnumerable<PungentDataSheetRow> rows, Dictionary<string, PungentDataSheetColumn> columns, string columnName)
        {
            foreach (PungentDataSheetRow row in rows ?? Enumerable.Empty<PungentDataSheetRow>())
            {
                string value = Value(sheet, row, columns, columnName);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string Value(PungentDataSheet sheet, PungentDataSheetRow row, Dictionary<string, PungentDataSheetColumn> columns, string columnName)
        {
            if (sheet == null || row == null || columns == null || !columns.TryGetValue(columnName, out PungentDataSheetColumn column))
                return string.Empty;

            PungentDataSheetCell cell = (sheet.cells ?? new List<PungentDataSheetCell>())
                .FirstOrDefault(item => item != null &&
                                        PungentAuthoringId.EqualsId(item.rowId, row.id) &&
                                        PungentAuthoringId.EqualsId(item.columnId, column.id));
            return cell == null ? string.Empty : (cell.VisibleValue ?? string.Empty).Trim();
        }

        private static void SetValue(PungentDataSheet sheet, PungentDataSheetRow row, string columnName, string value)
        {
            PungentDataSheetColumn column = (sheet.columns ?? new List<PungentDataSheetColumn>())
                .FirstOrDefault(item => item != null && string.Equals(item.displayName, columnName, StringComparison.OrdinalIgnoreCase));
            if (column == null || row == null)
                return;

            PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
            cell.rawValue = value ?? string.Empty;
            cell.displayValue = string.Empty;
        }

        private static List<string> SplitTags(string value)
        {
            return PungentAuthoringMetadata.NormalizeTags((value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()));
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string clean = value.Trim();
            return string.Equals(clean, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "y", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "optional", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "1", StringComparison.OrdinalIgnoreCase);
        }
    }
#endif
}
