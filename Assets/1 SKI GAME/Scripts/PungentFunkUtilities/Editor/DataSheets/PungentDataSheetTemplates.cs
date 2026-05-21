using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.DataSheets;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public sealed class PungentDataSheetTemplateDefinition
    {
        public string title;
        public string summary;
        public bool enabled = true;
        public string disabledReason = string.Empty;
        public List<PungentDataSheetColumn> columns = new List<PungentDataSheetColumn>();
        public List<PungentDataSheetRow> rows = new List<PungentDataSheetRow>();
        public Action<PungentDataSheet> afterApply;
    }

    public static class PungentDataSheetTemplates
    {
        public static IReadOnlyList<PungentDataSheetTemplateDefinition> GetTemplates()
        {
            return new[]
            {
                BlankSheet(),
                TokenContentSheet(),
                DialogueLinesSheet(),
                QuestObjectivesSheet(),
                AuditFindingsSheet(),
                UtilityInventorySheet(),
                ChecklistDefinitionSheet(),
                ToDoChecklistDefinitionSheet(),
                ReviewChecklistDefinitionSheet(),
                ReleaseMigrationChecklistDefinitionSheet(),
                BugTriageChecklistDefinitionSheet(),
                ReleaseChecklistSheet()
            };
        }

        public static PungentDataSheetTemplateDefinition GetChecklistDefinitionTemplate()
        {
            return ChecklistDefinitionSheet();
        }

        public static void ApplyTemplate(PungentDataSheet sheet, PungentDataSheetTemplateDefinition template)
        {
            if (sheet == null || template == null)
                return;

            sheet.title = string.IsNullOrWhiteSpace(template.title) ? "Untitled Sheet" : template.title;
            sheet.summary = template.summary ?? string.Empty;
            sheet.columns = CloneColumns(template.columns);
            sheet.rows = CloneRows(template.rows);
            sheet.cells = new List<PungentDataSheetCell>();
            template.afterApply?.Invoke(sheet);
            sheet.Touch();
            sheet.NormalizeInPlace();
        }

        private static PungentDataSheetTemplateDefinition BlankSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Blank Sheet",
                summary = "General-purpose editable data sheet.",
                columns =
                {
                    Column("Name"),
                    Column("Notes")
                },
                rows =
                {
                    Row("Row 1", 0)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition TokenContentSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Token Content Sheet",
                summary = "Token/content planning sheet. Token package integrations can layer explicit actions onto this data-only template later.",
                columns =
                {
                    Column("Token Key", PungentDataSheetDataType.Text),
                    Column("Category", PungentDataSheetDataType.Text),
                    Column("Raw Text", PungentDataSheetDataType.Text, 220f),
                    Column("Preview", PungentDataSheetDataType.Text, 220f),
                    Column("Status", PungentDataSheetDataType.EnumText, 120f, "Draft", "Ready", "Deprecated"),
                    Column("Notes", PungentDataSheetDataType.Text, 180f)
                },
                rows =
                {
                    Row("Token row 1", 0),
                    Row("Token row 2", 1)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition DialogueLinesSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Dialogue Lines Sheet",
                summary = "Structured dialogue line planning sheet.",
                columns =
                {
                    Column("Line ID"),
                    Column("Speaker"),
                    Column("Text", PungentDataSheetDataType.Text, 260f),
                    Column("Choice Group"),
                    Column("Conditions", PungentDataSheetDataType.Text, 180f),
                    Column("Tags", PungentDataSheetDataType.Text, 160f)
                },
                rows =
                {
                    Row("Opening line", 0),
                    Row("Response line", 1)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition QuestObjectivesSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Quest Objectives Sheet",
                summary = "Quest objective tracking sheet.",
                columns =
                {
                    Column("Objective ID"),
                    Column("Description", PungentDataSheetDataType.Text, 240f),
                    Column("Condition", PungentDataSheetDataType.Text, 180f),
                    Column("Reward", PungentDataSheetDataType.Text, 150f),
                    Column("Status", PungentDataSheetDataType.EnumText, 120f, "Draft", "Active", "Complete", "Blocked"),
                    Column("Related Document", PungentDataSheetDataType.AuthoringReference, 190f)
                },
                rows =
                {
                    Row("Objective 1", 0),
                    Row("Objective 2", 1)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition AuditFindingsSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Audit Findings Sheet",
                summary = "Manual audit findings capture sheet. Scanner-generated imports are intentionally deferred.",
                columns =
                {
                    Column("Issue Code"),
                    Column("Severity", PungentDataSheetDataType.EnumText, 120f, "Info", "Warning", "Error"),
                    Column("Location", PungentDataSheetDataType.Text, 180f),
                    Column("Finding", PungentDataSheetDataType.Text, 240f),
                    Column("Action", PungentDataSheetDataType.Text, 180f),
                    Column("Linked Note", PungentDataSheetDataType.AuthoringReference, 180f)
                },
                rows =
                {
                    Row("Finding 1", 0),
                    Row("Finding 2", 1)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition UtilityInventorySheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Utility Inventory Sheet",
                summary = "Manual inventory sheet for utility coverage and ownership.",
                columns =
                {
                    Column("Utility ID"),
                    Column("Package"),
                    Column("Status", PungentDataSheetDataType.EnumText, 120f, "Planned", "Active", "Deprecated", "Removed"),
                    Column("Dependencies", PungentDataSheetDataType.Text, 180f),
                    Column("Docs", PungentDataSheetDataType.AuthoringReference, 150f),
                    Column("Notes", PungentDataSheetDataType.Text, 180f)
                },
                rows =
                {
                    Row("Utility 1", 0),
                    Row("Utility 2", 1)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition ReleaseChecklistSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Release Checklist Sheet",
                summary = "Manual release-readiness checklist.",
                columns =
                {
                    Column("Item"),
                    Column("Owner"),
                    Column("Status", PungentDataSheetDataType.EnumText, 120f, "Todo", "Doing", "Done", "Blocked"),
                    Column("Risk", PungentDataSheetDataType.EnumText, 110f, "Low", "Medium", "High"),
                    Column("Validation", PungentDataSheetDataType.Text, 180f),
                    Column("Notes", PungentDataSheetDataType.Text, 180f)
                },
                rows =
                {
                    Row("Compile clean", 0),
                    Row("Open primary windows", 1),
                    Row("Smoke test import/export", 2)
                }
            };
        }

        private static PungentDataSheetTemplateDefinition ChecklistDefinitionSheet()
        {
            return new PungentDataSheetTemplateDefinition
            {
                title = "Checklist Definition Sheet",
                summary = "Checklist authoring sheet that can create or update a project checklist definition.",
                columns =
                {
                    Column("Checklist ID", PungentDataSheetDataType.Text, 150f),
                    Column("Checklist Title", PungentDataSheetDataType.Text, 180f),
                    Column("List Kind", PungentDataSheetDataType.EnumText, 140f, "quality-gate", "to-do", "review", "release-readiness", "migration", "bug-triage"),
                    Column("State Profile", PungentDataSheetDataType.EnumText, 190f, "quality-gate.pass-partial-fail", "todo.not-started-in-progress-blocked-done", "review.needs-changes-approved-rejected", "release-migration.not-started-in-progress-done-skipped-blocked", "bug-triage.open-investigating-fixed-verified-wont-fix"),
                    Column("Target Utility ID", PungentDataSheetDataType.Text, 150f),
                    Column("Tags", PungentDataSheetDataType.Text, 150f),
                    Column("Section ID", PungentDataSheetDataType.Text, 110f),
                    Column("Section Title", PungentDataSheetDataType.Text, 180f),
                    Column("Guidance Prompt", PungentDataSheetDataType.Text, 240f),
                    Column("Item ID", PungentDataSheetDataType.Text, 120f),
                    Column("Label", PungentDataSheetDataType.Text, 260f),
                    Column("Detail", PungentDataSheetDataType.Text, 260f),
                    Column("Optional", PungentDataSheetDataType.Boolean, 90f),
                    Column("State", PungentDataSheetDataType.Text, 110f),
                    Column("Comment", PungentDataSheetDataType.Text, 180f),
                    Column("Priority", PungentDataSheetDataType.Text, 100f),
                    Column("Owner", PungentDataSheetDataType.Text, 130f),
                    Column("Due Date", PungentDataSheetDataType.Text, 120f),
                    Column("Archived", PungentDataSheetDataType.Boolean, 82f),
                    Column("Linked State Key", PungentDataSheetDataType.Text, 160f),
                    Column("Owner Package", PungentDataSheetDataType.Text, 190f),
                    Column("Canonical Package", PungentDataSheetDataType.Text, 190f),
                    Column("Appears In Packages", PungentDataSheetDataType.Text, 210f),
                    Column("Child Checklist ID", PungentDataSheetDataType.Text, 170f),
                    Column("Child Pass Rule", PungentDataSheetDataType.Text, 150f)
                },
                rows =
                {
                    Row("Setup check", 0),
                    Row("Validation check", 1),
                    Row("Follow-up check", 2)
                },
                afterApply = PungentDataSheetChecklistBridge.PopulateChecklistTemplateCells
            };
        }

        private static PungentDataSheetTemplateDefinition ToDoChecklistDefinitionSheet()
        {
            PungentDataSheetTemplateDefinition template = ChecklistDefinitionSheet();
            template.title = "To-Do Checklist Sheet";
            template.summary = "Checklist authoring sheet prefilled for to-do list workflow states.";
            template.afterApply = sheet =>
            {
                PungentDataSheetChecklistBridge.PopulateChecklistTemplateCells(sheet);
                ConfigureChecklistTemplateCells(sheet, "todo-checklist", "To-Do Checklist", PungentChecklistListKinds.ToDo, PungentChecklistProfileIds.ToDo, "todo, checklist");
                SetValue(sheet, 0, "State", "not-started");
                SetValue(sheet, 1, "State", "not-started");
                SetValue(sheet, 2, "State", "not-started");
            };
            return template;
        }

        private static PungentDataSheetTemplateDefinition ReviewChecklistDefinitionSheet()
        {
            PungentDataSheetTemplateDefinition template = ChecklistDefinitionSheet();
            template.title = "Review Checklist Sheet";
            template.summary = "Checklist authoring sheet prefilled for review and approval workflows.";
            template.afterApply = sheet =>
            {
                PungentDataSheetChecklistBridge.PopulateChecklistTemplateCells(sheet);
                ConfigureChecklistTemplateCells(sheet, "review-checklist", "Review Checklist", PungentChecklistListKinds.Review, PungentChecklistProfileIds.Review, "review, checklist");
                SetValue(sheet, 0, "State", "not-reviewed");
                SetValue(sheet, 1, "State", "not-reviewed");
                SetValue(sheet, 2, "State", "not-reviewed");
            };
            return template;
        }

        private static PungentDataSheetTemplateDefinition ReleaseMigrationChecklistDefinitionSheet()
        {
            PungentDataSheetTemplateDefinition template = ChecklistDefinitionSheet();
            template.title = "Release / Migration Checklist Sheet";
            template.summary = "Checklist authoring sheet prefilled for release readiness and migration workflows.";
            template.afterApply = sheet =>
            {
                PungentDataSheetChecklistBridge.PopulateChecklistTemplateCells(sheet);
                ConfigureChecklistTemplateCells(sheet, "release-migration-checklist", "Release / Migration Checklist", PungentChecklistListKinds.ReleaseReadiness, PungentChecklistProfileIds.ReleaseMigration, "release, migration, checklist");
                SetValue(sheet, 0, "State", "not-started");
                SetValue(sheet, 1, "State", "not-started");
                SetValue(sheet, 2, "State", "not-started");
            };
            return template;
        }

        private static PungentDataSheetTemplateDefinition BugTriageChecklistDefinitionSheet()
        {
            PungentDataSheetTemplateDefinition template = ChecklistDefinitionSheet();
            template.title = "Bug Triage Checklist Sheet";
            template.summary = "Checklist authoring sheet prefilled for bug and issue triage workflows.";
            template.afterApply = sheet =>
            {
                PungentDataSheetChecklistBridge.PopulateChecklistTemplateCells(sheet);
                ConfigureChecklistTemplateCells(sheet, "bug-triage-checklist", "Bug Triage Checklist", PungentChecklistListKinds.BugTriage, PungentChecklistProfileIds.BugTriage, "bug, triage, checklist");
                SetValue(sheet, 0, "State", "open");
                SetValue(sheet, 1, "State", "open");
                SetValue(sheet, 2, "State", "open");
            };
            return template;
        }

        private static PungentDataSheetColumn Column(string displayName, PungentDataSheetDataType dataType = PungentDataSheetDataType.Text, float width = 140f, params string[] enumOptions)
        {
            PungentDataSheetColumn column = PungentDataSheetColumn.Create(displayName, dataType);
            column.width = width;
            column.enumOptions = PungentAuthoringMetadata.NormalizeTags(enumOptions);
            return column;
        }

        private static PungentDataSheetRow Row(string displayName, int order)
        {
            return PungentDataSheetRow.Create(displayName, order);
        }

        private static void ConfigureChecklistTemplateCells(PungentDataSheet sheet, string checklistId, string title, string listKind, string profileId, string tags)
        {
            SetValue(sheet, 0, "Checklist ID", checklistId);
            SetValue(sheet, 0, "Checklist Title", title);
            SetValue(sheet, 0, "List Kind", listKind);
            SetValue(sheet, 0, "State Profile", profileId);
            SetValue(sheet, 0, "Tags", tags);
            SetValue(sheet, 1, "Checklist ID", checklistId);
            SetValue(sheet, 1, "Checklist Title", title);
            SetValue(sheet, 2, "Checklist ID", checklistId);
            SetValue(sheet, 2, "Checklist Title", title);
        }

        private static void SetValue(PungentDataSheet sheet, int rowIndex, string columnName, string value)
        {
            if (sheet == null || rowIndex < 0 || sheet.rows == null || rowIndex >= sheet.rows.Count)
                return;

            PungentDataSheetColumn column = (sheet.columns ?? new List<PungentDataSheetColumn>())
                .Find(item => item != null && string.Equals(item.displayName, columnName, StringComparison.OrdinalIgnoreCase));
            if (column == null)
                return;

            PungentDataSheetCell cell = sheet.GetOrCreateCell(sheet.rows[rowIndex].id, column.id);
            cell.rawValue = value ?? string.Empty;
            cell.displayValue = string.Empty;
        }

        private static List<PungentDataSheetColumn> CloneColumns(List<PungentDataSheetColumn> columns)
        {
            List<PungentDataSheetColumn> result = new List<PungentDataSheetColumn>();
            foreach (PungentDataSheetColumn source in columns ?? new List<PungentDataSheetColumn>())
            {
                PungentDataSheetColumn column = PungentDataSheetColumn.Create(source.displayName, source.dataType);
                column.width = source.width;
                column.hidden = source.hidden;
                column.locked = source.locked;
                column.enumOptions = PungentAuthoringMetadata.NormalizeTags(source.enumOptions);
                result.Add(column);
            }

            return result;
        }

        private static List<PungentDataSheetRow> CloneRows(List<PungentDataSheetRow> rows)
        {
            List<PungentDataSheetRow> result = new List<PungentDataSheetRow>();
            foreach (PungentDataSheetRow source in rows ?? new List<PungentDataSheetRow>())
                result.Add(PungentDataSheetRow.Create(source.displayName, result.Count));

            return result;
        }
    }
#endif
}
