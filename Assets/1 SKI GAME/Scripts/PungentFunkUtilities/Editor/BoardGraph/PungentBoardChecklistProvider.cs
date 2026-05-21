using System.Collections.Generic;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Checklists;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentBoardChecklistProvider : IPungentChecklistDefinitionProvider
    {
        public const string Id = "board-graph-checklists";

        static PungentBoardChecklistProvider()
        {
            PungentChecklistDefinitionRegistry.Register(new PungentBoardChecklistProvider());
        }

        public string ProviderId => Id;

        public IEnumerable<PungentChecklistDefinition> GetChecklistDefinitions()
        {
            yield return CreateBoardGraphChecklist();
        }

        public IEnumerable<PungentLinkedStateGroupDefinition> GetLinkedStateGroups()
        {
            yield break;
        }

        private static PungentChecklistDefinition CreateBoardGraphChecklist()
        {
            PungentChecklistDefinition checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = PungentChecklistConstants.BoardGraphChecklistId,
                title = "Board Graph Feature Test",
                description = "Manual feature test checklist for board creation, node editing, links, layout, persistence, and authoring-reference integration.",
                defaultGuidance = "Use partial or failed items to plan the next Board Graph polish pass.",
                targetUtilityId = "board-editor",
                sourceProviderId = PungentBoardProvider.Id,
                sourceLabel = "Built-in Board Graph checklist",
                tags = new List<string> { "board", "graph", "qa", "checklist" },
                sections = new List<PungentChecklistSectionDefinition>
                {
                    Section("access", "Access And Window Lifecycle", "Fix menu, registry, or window lifecycle failures before testing board data.", new[]
                    {
                        Item("BG-01", "Opens from Utilities Browser."),
                        Item("BG-02", "Opens from Tools/PungentFunk Utilities/Board & Graph/Board Editor."),
                        Item("BG-03", "Window opens with no console errors."),
                        Item("BG-04", "Opens from Asset context menu."),
                        Item("BG-05", "Opens from GameObject context menu."),
                        Item("BG-06", "Checklist button opens this unified checklist.")
                    }),
                    Section("storage", "Board Storage And Templates", "Plan a storage/template/schema persistence fix pass for any failures here.", new[]
                    {
                        Item("BG-07", "Create blank board."),
                        Item("BG-08", "Create board from each enabled template."),
                        Item("BG-09", "Save, close editor, reopen, and board persists."),
                        Item("BG-10", "Duplicate board preserves nodes, edges, groups, and metadata."),
                        Item("BG-11", "Delete board confirms and updates selection."),
                        Item("BG-12", "Archived or closed boards do not break selection.")
                    }),
                    Section("nodes", "Node Creation And Editing", "Node edit failures usually mean inspector, selection, or canvas cache work is needed.", new[]
                    {
                        Item("BG-13", "Add text node from toolbar."),
                        Item("BG-14", "Add task/checklist node from toolbar."),
                        Item("BG-15", "Rename node from inspector."),
                        Item("BG-16", "Edit node body text."),
                        Item("BG-17", "Change node colour/category without losing selection."),
                        Item("BG-18", "Move nodes on canvas and persist positions."),
                        Item("BG-19", "Multi-select nodes and drag as a group.")
                    }),
                    Section("edges", "Connections And Relationships", "Connection issues affect downstream visual planning and authoring-reference maps.", new[]
                    {
                        Item("BG-20", "Create edge between two nodes."),
                        Item("BG-21", "Delete selected edge."),
                        Item("BG-22", "Edge labels can be edited and persist."),
                        Item("BG-23", "Connection handles are reachable at narrow and wide zoom."),
                        Item("BG-24", "Graph repaint stays responsive while dragging connections.")
                    }),
                    Section("layout", "Canvas, Layout, And Navigation", "Use these checks to catch visual regression before package-facing use.", new[]
                    {
                        Item("BG-25", "Pan canvas with expected input."),
                        Item("BG-26", "Zoom in/out and keep content framed."),
                        Item("BG-27", "Frame selected nodes."),
                        Item("BG-28", "Auto-layout or tidy command does not overlap nodes."),
                        Item("BG-29", "Inspector pane resizes and clamps correctly.")
                    }),
                    Section("authoring", "Authoring References And Integrations", "Resolve provider handoff issues before relying on linked planning data.", new[]
                    {
                        Item("BG-30", "Attach authoring reference to node."),
                        Item("BG-31", "Preview linked reference metadata."),
                        Item("BG-32", "Open Linked works when provider supports it."),
                        Item("BG-33", "Missing provider state is explanatory and non-destructive."),
                        Item("BG-34", "Create board from selected notes or references when supported."),
                        Item("BG-35", "Validation summarizes broken references.")
                    }),
                    Section("polish", "Domain Reload And Layout Polish", "These checks catch release-facing workflow friction.", new[]
                    {
                        Item("BG-36", "Domain reload preserves selected board and view state."),
                        Item("BG-37", "Play Mode transition does not lose unsaved board changes."),
                        Item("BG-38", "Toolbar controls remain reachable at narrow window widths."),
                        Item("BG-39", "Empty state offers useful creation actions."),
                        Item("BG-40", "Console remains clean after smoke test.")
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
                ownerPackageId = "com.pungentfunk.utilities.board"
            };
        }
    }
#endif
}
