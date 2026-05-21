using System.Collections.Generic;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Checklists;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentDataSheetChecklistProvider : IPungentChecklistDefinitionProvider
    {
        public const string Id = "data-sheet-checklists";

        static PungentDataSheetChecklistProvider()
        {
            PungentChecklistDefinitionRegistry.Register(new PungentDataSheetChecklistProvider());
        }

        public string ProviderId => Id;

        public IEnumerable<PungentChecklistDefinition> GetChecklistDefinitions()
        {
            yield return CreateDataSheetChecklist();
        }

        public IEnumerable<PungentLinkedStateGroupDefinition> GetLinkedStateGroups()
        {
            yield break;
        }

        private static PungentChecklistDefinition CreateDataSheetChecklist()
        {
            PungentChecklistDefinition checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = PungentChecklistConstants.DataSheetChecklistId,
                title = "Data Sheet Current QA",
                description = "Manual checklist for the current Data Sheet editor workflow, persistence, CSV, validation, and authoring-reference integration.",
                defaultGuidance = "Use partial or failed items to plan the next Data Sheet hardening pass.",
                targetUtilityId = "data-sheet-editor",
                sourceProviderId = PungentDataSheetProvider.Id,
                sourceLabel = "Built-in Data Sheet checklist",
                tags = new List<string> { "data-sheet", "qa", "checklist", "authoring" },
                sections = new List<PungentChecklistSectionDefinition>
                {
                    Section("A", "A. Access And Safety", "Fix access, window, or repaint issues before deeper Data Sheet testing.", new[]
                    {
                        Item("A1", "Tools menu opens the Data Sheet Editor."),
                        Item("A2", "Utilities browser opens the Data Sheet Editor card."),
                        Item("A3", "Data Sheet toolbar QA button opens this checklist."),
                        Item("A4", "Assets/GameObject context menu Open Data Sheet Editor works."),
                        Item("A5", "Window opens with no console errors."),
                        Item("A6", "Opening/drawing the editor does not trigger noticeable project-wide scanning.")
                    }),
                    Section("B", "B. Sheet Lifecycle", "Resolve persistence and selection issues before relying on sheet-authored checklist definitions.", new[]
                    {
                        Item("B1", "Create Blank Sheet creates a usable sheet."),
                        Item("B2", "Template menu creates every enabled template without errors."),
                        Item("B3", "Rename sheet persists after save/reopen."),
                        Item("B4", "Duplicate sheet preserves rows, columns, and cell values."),
                        Item("B5", "Close/reopen sheet tab preserves selected sheet state."),
                        Item("B6", "Delete sheet asks for confirmation and updates selection."),
                        Item("B7", "Save, close, reopen, and confirm sheet persists.")
                    }),
                    Section("C", "C. Core Grid Editing", "Capture any editing or selection regressions before testing import/export.", new[]
                    {
                        Item("C1", "Edit a text cell and commit by Enter."),
                        Item("C2", "Cancel an edit by Escape."),
                        Item("C3", "Paste multi-cell values into the grid."),
                        Item("C4", "Copy selected cells back to the clipboard."),
                        Item("C5", "Insert/delete rows through row context actions."),
                        Item("C6", "Insert/delete columns through column context actions."),
                        Item("C7", "Drag selection range without layout jitter.")
                    }),
                    Section("D", "D. Headers, Sorting, Resizing, Reordering", "Header issues usually point to grid layout or cached-column invalidation problems.", new[]
                    {
                        Item("D1", "Resize columns and confirm widths persist."),
                        Item("D2", "Reorder columns by drag."),
                        Item("D3", "Reorder rows by drag."),
                        Item("D4", "Hide/show columns through the inspector or context actions."),
                        Item("D5", "Sort by a column and confirm rows remain editable."),
                        Item("D6", "Horizontal tab scrolling works without vertical tab overflow.")
                    }),
                    Section("E", "E. Spreadsheet Operations", "Record workflow friction before checklist-sheet authoring depends on this path.", new[]
                    {
                        Item("E1", "Fill down selected cells."),
                        Item("E2", "Fill right selected cells."),
                        Item("E3", "Clear selected cells."),
                        Item("E4", "Duplicate row preserves existing cell values."),
                        Item("E5", "Duplicate column preserves data type and values."),
                        Item("E6", "Large sheet scrolling remains responsive.")
                    }),
                    Section("F", "F. Data Types, CSV, Validation, Links", "Address CSV, validation, typed cells, and authoring link issues before propagation polish.", new[]
                    {
                        Item("F1", "Enum columns show and persist allowed options."),
                        Item("F2", "Boolean cells toggle cleanly."),
                        Item("F3", "Authoring Reference cells can pick a provider item."),
                        Item("F4", "Validation reports missing or broken references."),
                        Item("F5", "Export CSV creates expected file through one settings tray."),
                        Item("F6", "Import CSV creates a new sheet."),
                        Item("F7", "Import CSV append mode uses one settings tray and does not overwrite unexpectedly."),
                        Item("F8", "CSV preview reports row/column counts before import."),
                        Item("F9", "Open linked item works when provider supports it."),
                        Item("F10", "Open/Create Sheet Sticky Note works and opens Sticky Notes.")
                    }),
                    Section("G", "G. Checklist Definition Sheet", "Confirm the Data Sheet bridge can author reusable checklist definitions.", new[]
                    {
                        Item("G1", "Checklist Definition Sheet template appears in the New menu."),
                        Item("G2", "Template creates checklist-format columns and example rows."),
                        Item("G3", "Validate Checklist Definition reports missing section/item values."),
                        Item("G4", "Create/Update Checklist Definition saves to project checklist storage."),
                        Item("G5", "Created checklist opens in the Checklist Utility after domain reload.")
                    }),
                    Section("H", "H. Binding And Layout Polish", "Use these checks to catch high-friction editor states after the core paths pass.", new[]
                    {
                        Item("H1", "Inspector pane resizes and clamps correctly."),
                        Item("H2", "Toolbar controls remain reachable at narrow window widths."),
                        Item("H3", "Binding profile creation and preview do not mutate assets until Apply."),
                        Item("H4", "Save-before-reload protects pending Data Sheet edits.")
                    })
                }
            };
            checklist.NormalizeInPlace();
            return checklist;
        }

        private static PungentChecklistSectionDefinition Section(string id, string title, string guidance, PungentChecklistItemDefinition[] items)
        {
            return new PungentChecklistSectionDefinition
            {
                id = id,
                title = title,
                guidancePrompt = guidance,
                items = new List<PungentChecklistItemDefinition>(items ?? new PungentChecklistItemDefinition[0])
            };
        }

        private static PungentChecklistItemDefinition Item(string id, string label, string detail = "")
        {
            return new PungentChecklistItemDefinition
            {
                id = id,
                label = label,
                detail = detail,
                ownerPackageId = "com.pungentfunk.utilities.datasheet"
            };
        }
    }
#endif
}
