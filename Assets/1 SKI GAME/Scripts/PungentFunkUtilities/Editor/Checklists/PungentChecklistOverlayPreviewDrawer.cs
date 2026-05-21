using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal sealed class PungentChecklistOverlayPreviewDrawer : IPungentAuthoringOverlayPreviewDrawer
    {
        private const double ChecklistCommentAutosaveDelaySeconds = 0.75d;
        private const double ChecklistSearchDebounceSeconds = 0.16d;
        private const string CommentKeySeparator = "\u001F";
        private const float SearchRowHeight = 24f;
        private const float RunSectionHeaderHeight = 24f;
        private const float RunItemBaseHeight = 82f;
        private const float RunItemDetailHeight = 18f;
        private const float EditMetadataHeight = 78f;
        private const float EditFooterHeight = 28f;
        private const float EditSectionHeight = 28f;
        private const float EditItemHeight = 24f;
        private const float VirtualizationOverscan = 96f;

        private static readonly Dictionary<string, PungentChecklistDefinitionEditSession> EditSessions = new Dictionary<string, PungentChecklistDefinitionEditSession>(StringComparer.OrdinalIgnoreCase);
        private static readonly ChecklistOverlayRunCache RunCache = new ChecklistOverlayRunCache();
        private static readonly List<string> DirtyCommentScratch = new List<string>();
        private static readonly List<EditRow> EditRows = new List<EditRow>();
        private static GUIStyle _mutedCenteredStyle;

        private sealed class VisibleChecklistSection
        {
            public PungentChecklistSectionDefinition section;
            public readonly List<PungentChecklistItemDefinition> items = new List<PungentChecklistItemDefinition>();
        }

        private enum RunRowKind
        {
            Section,
            Item
        }

        private sealed class RunRow
        {
            public RunRowKind kind;
            public PungentChecklistSectionDefinition section;
            public PungentChecklistItemDefinition item;
            public float y;
            public float height;
            public int count;
        }

        private sealed class EditRow
        {
            public bool addSection;
            public bool emptySection;
            public PungentChecklistSectionDefinition section;
            public PungentChecklistItemDefinition item;
            public float y;
            public float height;
        }

        private sealed class ChecklistOverlayRunCache
        {
            public string key = string.Empty;
            public PungentChecklistProgressSummary summary = new PungentChecklistProgressSummary();
            public PungentChecklistUtilityStateService.StateSnapshot snapshot;
            public readonly List<VisibleChecklistSection> visibleSections = new List<VisibleChecklistSection>();
            public readonly List<RunRow> rows = new List<RunRow>();
            public readonly Dictionary<string, string> stateByItemId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, PungentChecklistStateOptionDefinition> stateOptionByItemId = new Dictionary<string, PungentChecklistStateOptionDefinition>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> commentByItemId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public int visibleItemCount;
            public float contentHeight = 120f;
            public int lastVisibleDrawCount;

            public void Clear()
            {
                key = string.Empty;
                summary = new PungentChecklistProgressSummary();
                snapshot = null;
                visibleSections.Clear();
                rows.Clear();
                stateByItemId.Clear();
                stateOptionByItemId.Clear();
                commentByItemId.Clear();
                visibleItemCount = 0;
                contentHeight = 120f;
                lastVisibleDrawCount = 0;
            }
        }

        static PungentChecklistOverlayPreviewDrawer()
        {
            PungentAuthoringOverlayPreviewRegistry.Register(new PungentChecklistOverlayPreviewDrawer());
            PungentStickyNoteOverlayController.OverlayUpdate -= HandleOverlayUpdate;
            PungentStickyNoteOverlayController.OverlayUpdate += HandleOverlayUpdate;
            PungentStickyNoteOverlayController.BeforeOverlayReset -= HandleBeforeOverlayReset;
            PungentStickyNoteOverlayController.BeforeOverlayReset += HandleBeforeOverlayReset;
        }

        public bool CanDraw(PungentAuthoringReference reference)
        {
            return reference != null &&
                   (reference.itemKind == PungentAuthoringItemKind.Checklist ||
                    string.Equals(reference.providerId, PungentChecklistConstants.ProviderId, StringComparison.OrdinalIgnoreCase));
        }

        public void Draw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            long drawSample = PungentAuthoringOverlayPerformance.BeginSample();
            EnsureStyles();
            try
            {
                PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.Find(reference.itemId);
                if (checklist == null)
                {
                    EditorGUILayout.LabelField("Checklist missing", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("The linked checklist definition could not be found.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                ChecklistOverlayRunCache cache = GetRunCache(checklist, state);
                DrawToolbar(checklist, state, cache);
                if (state.previewMode)
                    DrawEdit(checklist, state);
                else
                    DrawRun(checklist, state, cache);
            }
            finally
            {
                PungentAuthoringOverlayPerformance.EndSample("Checklist Overlay Draw", drawSample, RunCache.lastVisibleDrawCount);
            }
        }

        private static void DrawToolbar(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state, ChecklistOverlayRunCache cache)
        {
            PungentChecklistProgressSummary summary = cache == null ? new PungentChecklistProgressSummary() : cache.summary;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                state.previewMode = GUILayout.Toggle(state.previewMode, state.previewMode ? "Edit" : "Run", EditorStyles.toolbarButton, GUILayout.Width(52f));
                UtilityWindowTheme.CountPill(summary.complete + "/" + summary.total, UtilityWindowTheme.Green, 54f);
                if (summary.problem > 0)
                    UtilityWindowTheme.CountPill(summary.problem + " attention", UtilityWindowTheme.Red, 86f);
                GUILayout.FlexibleSpace();
                if (state.checklistFilterPending)
                    EditorGUILayout.LabelField("Updating...", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(68f));
                PungentAuthoringOverlayPerformance.DrawToolbarReadout("Checklist Overlay Draw");
                bool nextArchived = GUILayout.Toggle(state.checklistShowArchived, new GUIContent("Archived", "Show archived checklist sections and items."), EditorStyles.toolbarButton, GUILayout.Width(72f));
                if (nextArchived != state.checklistShowArchived)
                {
                    state.checklistShowArchived = nextArchived;
                    RunCache.Clear();
                }
                if (GUILayout.Button("Open Full Tool", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    PungentChecklistUtilityWindow.OpenChecklist(checklist.checklistId);
            }
        }

        private static void DrawRun(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state, ChecklistOverlayRunCache cache)
        {
            DrawRunSearchRow(state);

            Rect listRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.MinHeight(160f),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (listRect.width <= 1f || listRect.height <= 1f)
                return;

            float contentHeight = Mathf.Max(listRect.height, cache == null ? 120f : cache.contentHeight);
            Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, listRect.width - 18f), contentHeight);
            EditorGUI.DrawRect(listRect, EditorGUIUtility.isProSkin ? new Color(0.105f, 0.11f, 0.118f, 1f) : new Color(0.93f, 0.94f, 0.95f, 1f));
            state.checklistRunScroll = GUI.BeginScrollView(listRect, state.checklistRunScroll, contentRect, false, true);
            try
            {
                if (cache != null)
                    cache.lastVisibleDrawCount = 0;

                if (cache == null || cache.visibleItemCount == 0)
                {
                    GUI.Label(new Rect(0f, Mathf.Max(0f, listRect.height * 0.5f - 10f), contentRect.width, 22f), "No checklist items match this view.", _mutedCenteredStyle);
                    return;
                }

                float minY = Mathf.Max(0f, state.checklistRunScroll.y - VirtualizationOverscan);
                float maxY = state.checklistRunScroll.y + listRect.height + VirtualizationOverscan;
                for (int i = 0; i < cache.rows.Count; i++)
                {
                    RunRow row = cache.rows[i];
                    if (row == null || row.y + row.height < minY)
                        continue;
                    if (row.y > maxY)
                        break;

                    Rect rowRect = new Rect(4f, row.y, Mathf.Max(80f, contentRect.width - 8f), row.height);
                    if (row.kind == RunRowKind.Section)
                        DrawRunSectionHeader(rowRect, row.section, row.count);
                    else
                        DrawRunItem(rowRect, checklist, row.item, state, cache);
                    cache.lastVisibleDrawCount++;
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private static void DrawRunSearchRow(PungentStickyNoteOverlayState state)
        {
            Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(SearchRowHeight), GUILayout.ExpandWidth(true));
            float labelWidth = 48f;
            GUI.Label(new Rect(row.x, row.y + 2f, labelWidth, row.height - 4f), "Search", UtilityWindowTheme.PathLabelStyle);
            Rect searchRect = new Rect(row.x + labelWidth, row.y + 2f, Mathf.Max(40f, row.width - labelWidth - 26f), row.height - 4f);
            EditorGUI.BeginChangeCheck();
            string nextSearch = GUI.TextField(searchRect, state.checklistSearch ?? string.Empty, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                state.checklistSearch = nextSearch ?? string.Empty;
                state.checklistLastSearchEditTime = EditorApplication.timeSinceStartup;
                state.checklistFilterPending = true;
            }

            if (!string.IsNullOrWhiteSpace(state.checklistSearch) &&
                GUI.Button(new Rect(searchRect.xMax + 3f, row.y + 2f, 22f, row.height - 4f), "x", EditorStyles.toolbarButton))
            {
                state.checklistSearch = string.Empty;
                state.checklistLastSearchEditTime = EditorApplication.timeSinceStartup;
                state.checklistFilterPending = true;
            }
        }

        private static void DrawRunSectionHeader(Rect rect, PungentChecklistSectionDefinition section, int count)
        {
            Rect titleRect = new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(40f, rect.width - 74f), rect.height - 4f);
            GUI.Label(titleRect, string.IsNullOrWhiteSpace(section.title) ? "Section" : section.title, EditorStyles.miniBoldLabel);
            DrawPill(new Rect(rect.xMax - 64f, rect.y + 3f, 58f, rect.height - 6f), count + " items", UtilityWindowTheme.Green);
        }

        private static void DrawRunItem(Rect rect, PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, PungentStickyNoteOverlayState state, ChecklistOverlayRunCache cache)
        {
            if (item == null)
                return;

            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            string itemId = item.id ?? string.Empty;
            cache.stateOptionByItemId.TryGetValue(itemId, out PungentChecklistStateOptionDefinition current);
            bool computed = PungentChecklistUtilityStateService.IsComputedFromChild(item);

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 6f, Mathf.Max(40f, rect.width - 92f), 18f);
            GUI.Label(labelRect, string.IsNullOrWhiteSpace(item.label) ? itemId : item.label, EditorStyles.miniBoldLabel);
            DrawPill(new Rect(rect.xMax - 78f, rect.y + 5f, 68f, 18f), current == null ? "Open" : current.shortLabel, StateColor(current));

            float y = rect.y + 27f;
            if (!string.IsNullOrWhiteSpace(item.detail))
            {
                GUI.Label(new Rect(rect.x + 8f, y, Mathf.Max(40f, rect.width - 16f), 16f), item.detail, UtilityWindowTheme.MutedMiniLabelStyle);
                y += RunItemDetailHeight;
            }

            if (!computed)
            {
                float x = rect.x + 8f;
                IReadOnlyList<PungentChecklistStateOptionDefinition> options = cache.snapshot == null ? PungentChecklistProfiles.ResolveStateOptions(checklist) : cache.snapshot.StateOptions;
                for (int i = 0; i < options.Count; i++)
                {
                    PungentChecklistStateOptionDefinition option = options[i];
                    if (option == null)
                        continue;

                    string label = cache.snapshot != null && cache.snapshot.IsQualityGate && option.stateId == PungentChecklistProfiles.StateUntested ? "Clear" : option.shortLabel;
                    float width = Mathf.Clamp(44f + label.Length * 5f, 52f, 96f);
                    if (x + width > rect.xMax - 8f)
                        break;

                    bool selected = current != null && string.Equals(option.stateId, current.stateId, StringComparison.OrdinalIgnoreCase);
                    Color old = GUI.backgroundColor;
                    if (selected)
                        GUI.backgroundColor = StateColor(option);
                    if (GUI.Button(new Rect(x, y, width, 19f), label, EditorStyles.miniButton))
                    {
                        CommitCommentDraft(checklist, state, item.id);
                        string next = selected && option.stateId != PungentChecklistProfiles.DefaultStateId(checklist)
                            ? PungentChecklistProfiles.DefaultStateId(checklist)
                            : option.stateId;
                        PungentChecklistUtilityStateService.SetStateId(checklist, item, next);
                        state.checklistOverlayDirtyVersion++;
                        RunCache.Clear();
                    }
                    GUI.backgroundColor = old;
                    x += width + 4f;
                }
            }
            else
            {
                GUI.Label(new Rect(rect.x + 8f, y, Mathf.Max(40f, rect.width - 16f), 18f), "Computed from child checklist.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            y += 24f;
            DrawCommentField(new Rect(rect.x + 8f, y, Mathf.Max(40f, rect.width - 16f), 20f), checklist, item, state, cache);
        }

        private static void DrawCommentField(Rect rect, PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, PungentStickyNoteOverlayState state, ChecklistOverlayRunCache cache)
        {
            string key = CommentDraftKey(checklist, item == null ? string.Empty : item.id);
            string controlName = "PungentChecklistOverlayComment_" + key.GetHashCode().ToString("X8");
            string persisted = string.Empty;
            if (item != null && cache != null)
                cache.commentByItemId.TryGetValue(item.id ?? string.Empty, out persisted);

            string draft = GetCommentDraft(checklist, state, item == null ? string.Empty : item.id, persisted);
            GUI.SetNextControlName(controlName);
            EditorGUI.BeginChangeCheck();
            string next = EditorGUI.TextField(rect, new GUIContent("Comment", "Short follow-up detail for this checklist item."), draft);
            if (EditorGUI.EndChangeCheck())
            {
                state.checklistCommentDrafts[key] = next ?? string.Empty;
                if (state.checklistDirtyCommentKeys == null)
                    state.checklistDirtyCommentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                state.checklistDirtyCommentKeys.Add(key);
                state.checklistFocusedCommentItemId = key;
                state.checklistLastCommentEditTime = EditorApplication.timeSinceStartup;
                state.checklistCommentsDirty = true;
            }

            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            if (focused)
            {
                state.checklistFocusedCommentItemId = key;
            }
            else if (string.Equals(state.checklistFocusedCommentItemId, key, StringComparison.OrdinalIgnoreCase))
            {
                CommitCommentDraft(checklist, state, item == null ? string.Empty : item.id);
                state.checklistFocusedCommentItemId = string.Empty;
            }
        }

        private static void DrawEdit(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state)
        {
            if (PungentChecklistDefinitionRegistry.IsProviderDefinitionId(checklist.checklistId))
            {
                EditorGUILayout.HelpBox("This checklist is package-provided. Clone it to project storage before editing.", MessageType.Info);
                if (GUILayout.Button("Clone to Project", EditorStyles.miniButton, GUILayout.Width(120f)) &&
                    PungentChecklistDefinitionRegistry.CloneToProject(checklist, out PungentChecklistDefinition clone, out string error))
                {
                    state.authoringReference = clone.ToReference();
                    state.authoringStatus = "Cloned checklist.";
                    state.ResetChecklistOverlayState();
                    RunCache.Clear();
                }
                return;
            }

            PungentChecklistDefinitionEditSession session = GetEditSession(checklist);
            PungentChecklistDefinition draft = session.Draft;
            if (draft == null)
            {
                EditorGUILayout.HelpBox("Checklist draft could not be prepared.", MessageType.Warning);
                return;
            }

            DrawEditMetadata(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(EditMetadataHeight), GUILayout.ExpandWidth(true)), draft, session);

            Rect listRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.MinHeight(160f),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawEditList(listRect, draft, session, state);

            DrawEditFooter(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(EditFooterHeight), GUILayout.ExpandWidth(true)), checklist, draft, session, state);
        }

        private static void DrawEditMetadata(Rect rect, PungentChecklistDefinition draft, PungentChecklistDefinitionEditSession session)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            float x = rect.x + 8f;
            float width = Mathf.Max(80f, rect.width - 16f);
            EditorGUI.BeginChangeCheck();
            draft.title = EditorGUI.TextField(new Rect(x, rect.y + 6f, width, 18f), "Title", draft.title);
            draft.description = EditorGUI.TextField(new Rect(x, rect.y + 28f, width, 18f), "Description", draft.description);
            Rect tagsRect = new Rect(x, rect.y + 50f, Mathf.Max(80f, width - 92f), 18f);
            draft.tags = PungentAuthoringMetadata.NormalizeTags((EditorGUI.TextField(tagsRect, "Tags", string.Join(", ", draft.tags ?? new List<string>())) ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            draft.archived = GUI.Toggle(new Rect(rect.xMax - 78f, rect.y + 51f, 70f, 18f), draft.archived, "Archived");
            if (EditorGUI.EndChangeCheck())
                session.Touch();
        }

        private static void DrawEditList(Rect listRect, PungentChecklistDefinition draft, PungentChecklistDefinitionEditSession session, PungentStickyNoteOverlayState state)
        {
            if (listRect.width <= 1f || listRect.height <= 1f)
                return;

            List<EditRow> rows = BuildEditRows(draft, out float editContentHeight);
            float contentHeight = Mathf.Max(listRect.height, editContentHeight);
            Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, listRect.width - 18f), contentHeight);
            EditorGUI.DrawRect(listRect, EditorGUIUtility.isProSkin ? new Color(0.105f, 0.11f, 0.118f, 1f) : new Color(0.93f, 0.94f, 0.95f, 1f));
            state.checklistEditScroll = GUI.BeginScrollView(listRect, state.checklistEditScroll, contentRect, false, true);
            try
            {
                if (draft.sections == null || draft.sections.Count == 0)
                {
                    if (GUI.Button(new Rect(6f, 5f, 92f, 20f), "Add Section", EditorStyles.miniButton))
                    {
                        session.AddSection();
                        RunCache.Clear();
                        GUIUtility.ExitGUI();
                    }
                    GUI.Label(new Rect(6f, 53f, contentRect.width - 12f, 22f), "This checklist has no sections yet.", _mutedCenteredStyle);
                    return;
                }

                RunCache.lastVisibleDrawCount = 0;
                float minY = Mathf.Max(0f, state.checklistEditScroll.y - VirtualizationOverscan);
                float maxY = state.checklistEditScroll.y + listRect.height + VirtualizationOverscan;
                for (int i = 0; i < rows.Count; i++)
                {
                    EditRow row = rows[i];
                    if (row == null || row.y + row.height < minY)
                        continue;
                    if (row.y > maxY)
                        break;

                    DrawEditRow(new Rect(0f, row.y, contentRect.width, row.height), row, session);
                    RunCache.lastVisibleDrawCount++;
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private static void DrawEditFooter(Rect rect, PungentChecklistDefinition source, PungentChecklistDefinition draft, PungentChecklistDefinitionEditSession session, PungentStickyNoteOverlayState state)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            float x = rect.x + 4f;
            if (GUI.Button(new Rect(x, rect.y + 4f, 52f, 20f), "Save", EditorStyles.miniButton))
            {
                if (PungentChecklistDefinitionRegistry.UpsertProjectDefinition(draft, null, out string error))
                {
                    session.MarkClean();
                    state.authoringStatus = "Saved checklist.";
                    state.checklistOverlayDirtyVersion++;
                    RunCache.Clear();
                }
                else
                {
                    state.authoringStatus = error;
                }
            }
            x += 58f;
            if (GUI.Button(new Rect(x, rect.y + 4f, 58f, 20f), "Revert", EditorStyles.miniButton))
            {
                session.Load(source);
                RunCache.Clear();
            }
            x += 64f;
            if (GUI.Button(new Rect(x, rect.y + 4f, 68f, 20f), "Validate", EditorStyles.miniButton))
                state.authoringStatus = PungentChecklistValidation.ValidateDefinition(draft).Summary;

            GUI.Label(new Rect(rect.xMax - 74f, rect.y + 5f, 68f, 18f), session.IsDirty ? "Unsaved" : "Saved", UtilityWindowTheme.PathLabelStyle);
        }

        private static List<EditRow> BuildEditRows(PungentChecklistDefinition draft, out float contentHeight)
        {
            EditRows.Clear();
            float y = 5f;
            EditRows.Add(new EditRow { addSection = true, y = y, height = 24f });
            y += 28f;

            if (draft != null && draft.sections != null)
            {
                for (int s = 0; s < draft.sections.Count; s++)
                {
                    PungentChecklistSectionDefinition section = draft.sections[s];
                    if (section == null)
                        continue;

                    EditRows.Add(new EditRow { section = section, y = y, height = EditSectionHeight });
                    y += EditSectionHeight + 3f;

                    if (section.items == null || section.items.Count == 0)
                    {
                        EditRows.Add(new EditRow { section = section, emptySection = true, y = y, height = EditItemHeight });
                        y += EditItemHeight;
                        continue;
                    }

                    for (int i = 0; i < section.items.Count; i++)
                    {
                        PungentChecklistItemDefinition item = section.items[i];
                        if (item == null)
                            continue;
                        EditRows.Add(new EditRow { section = section, item = item, y = y, height = EditItemHeight });
                        y += EditItemHeight;
                    }

                    y += 4f;
                }
            }

            contentHeight = Mathf.Max(120f, y + 12f);
            return EditRows;
        }

        private static void DrawEditRow(Rect rect, EditRow row, PungentChecklistDefinitionEditSession session)
        {
            if (row == null)
                return;

            if (row.addSection)
            {
                if (GUI.Button(new Rect(rect.x + 6f, rect.y, 92f, 20f), "Add Section", EditorStyles.miniButton))
                {
                    session.AddSection();
                    RunCache.Clear();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            if (row.emptySection)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 2f, rect.width - 28f, 18f), "No items in this section.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            if (row.item == null)
            {
                if (row.section == null)
                    return;

                GUI.Box(new Rect(rect.x + 4f, rect.y, rect.width - 8f, EditSectionHeight), GUIContent.none, EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                row.section.title = EditorGUI.TextField(new Rect(rect.x + 10f, rect.y + 5f, Mathf.Max(80f, rect.width - 100f), 18f), "Section", row.section.title);
                if (EditorGUI.EndChangeCheck())
                    session.Touch();
                if (GUI.Button(new Rect(rect.xMax - 82f, rect.y + 5f, 72f, 18f), "Add Item", EditorStyles.miniButton))
                {
                    session.AddItem(row.section);
                    RunCache.Clear();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            row.item.label = EditorGUI.TextField(new Rect(rect.x + 12f, rect.y + 2f, Mathf.Max(80f, rect.width - 24f), 18f), row.item.id, row.item.label);
            if (EditorGUI.EndChangeCheck())
                session.Touch();
        }

        private static ChecklistOverlayRunCache GetRunCache(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state)
        {
            string key = BuildRunCacheKey(checklist, state);
            if (string.Equals(RunCache.key, key, StringComparison.Ordinal))
                return RunCache;

            long cacheSample = PungentAuthoringOverlayPerformance.BeginSample();
            RunCache.Clear();
            RunCache.key = key;
            RunCache.snapshot = new PungentChecklistUtilityStateService.StateSnapshot(checklist, PungentChecklistDefinitionRegistry.Find);
            RunCache.summary = new PungentChecklistProgressSummary();

            string search = (state.checklistAppliedSearch ?? string.Empty).Trim();
            float y = 4f;
            if (checklist.sections != null)
            {
                for (int s = 0; s < checklist.sections.Count; s++)
                {
                    PungentChecklistSectionDefinition section = checklist.sections[s];
                    if (section == null || (section.archived && !state.checklistShowArchived))
                        continue;

                    VisibleChecklistSection visibleSection = new VisibleChecklistSection { section = section };
                    if (section.items != null)
                    {
                        for (int i = 0; i < section.items.Count; i++)
                        {
                            PungentChecklistItemDefinition item = section.items[i];
                            if (item == null || (item.archived && !state.checklistShowArchived))
                                continue;

                            string comment = PungentChecklistUtilityStateService.GetComment(checklist, item.id);
                            string stateId = RunCache.snapshot.GetStateId(item);
                            PungentChecklistStateOptionDefinition stateOption = RunCache.snapshot.ResolveState(stateId);
                            RunCache.commentByItemId[item.id ?? string.Empty] = comment;
                            RunCache.stateByItemId[item.id ?? string.Empty] = stateId;
                            RunCache.stateOptionByItemId[item.id ?? string.Empty] = stateOption;
                            AddSummaryItem(RunCache.summary, RunCache.snapshot, stateOption);

                            if (!MatchesSearch(item, section, comment, search))
                                continue;

                            visibleSection.items.Add(item);
                            RunCache.visibleItemCount++;
                        }
                    }

                    if (visibleSection.items.Count > 0)
                    {
                        RunCache.visibleSections.Add(visibleSection);
                        RunCache.rows.Add(new RunRow
                        {
                            kind = RunRowKind.Section,
                            section = section,
                            y = y,
                            height = RunSectionHeaderHeight,
                            count = visibleSection.items.Count
                        });
                        y += RunSectionHeaderHeight + 2f;

                        for (int i = 0; i < visibleSection.items.Count; i++)
                        {
                            PungentChecklistItemDefinition item = visibleSection.items[i];
                            float itemHeight = RunItemHeight(item);
                            RunCache.rows.Add(new RunRow
                            {
                                kind = RunRowKind.Item,
                                section = section,
                                item = item,
                                y = y,
                                height = itemHeight
                            });
                            y += itemHeight + 4f;
                        }
                    }
                }
            }

            RunCache.contentHeight = Mathf.Max(120f, y + 8f);
            PungentAuthoringOverlayPerformance.EndSample("Checklist Cache Rebuild", cacheSample, RunCache.visibleItemCount);
            return RunCache;
        }

        private static void AddSummaryItem(PungentChecklistProgressSummary summary, PungentChecklistUtilityStateService.StateSnapshot snapshot, PungentChecklistStateOptionDefinition state)
        {
            if (summary == null || snapshot == null || state == null)
                return;

            summary.total++;
            if (!summary.countsByStateId.ContainsKey(state.stateId))
                summary.countsByStateId[state.stateId] = 0;
            summary.countsByStateId[state.stateId]++;

            if (snapshot.CountsAsStarted(state.stateId))
                summary.started++;
            else
                summary.unstarted++;
            if (state.countsAsComplete)
                summary.complete++;
            if (snapshot.CountsAsProblem(state.stateId))
                summary.problem++;
        }

        private static string BuildRunCacheKey(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state)
        {
            if (checklist == null)
                return "missing";

            return string.Join("|",
                checklist.checklistId ?? string.Empty,
                checklist.updatedUtc ?? string.Empty,
                checklist.stateProfileId ?? string.Empty,
                checklist.sections == null ? "s0" : "s" + checklist.sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (state.checklistAppliedSearch ?? string.Empty).Trim().ToLowerInvariant(),
                state.checklistShowArchived ? "arch1" : "arch0",
                state.checklistOverlayDirtyVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool MatchesSearch(PungentChecklistItemDefinition item, PungentChecklistSectionDefinition section, string comment, string search)
        {
            if (item == null)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return Matches(item.id, search) ||
                   Matches(item.label, search) ||
                   Matches(item.detail, search) ||
                   Matches(item.owner, search) ||
                   Matches(item.priority, search) ||
                   Matches(section == null ? string.Empty : section.title, search) ||
                   Matches(comment, search);
        }

        private static bool Matches(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float CalculateRunContentHeight(ChecklistOverlayRunCache cache)
        {
            if (cache == null || cache.visibleItemCount == 0)
                return 120f;

            float height = 8f;
            for (int s = 0; s < cache.visibleSections.Count; s++)
            {
                VisibleChecklistSection section = cache.visibleSections[s];
                if (section == null || section.items.Count == 0)
                    continue;
                height += RunSectionHeaderHeight + 2f;
                for (int i = 0; i < section.items.Count; i++)
                    height += RunItemHeight(section.items[i]) + 4f;
            }
            return height + 8f;
        }

        private static float RunItemHeight(PungentChecklistItemDefinition item)
        {
            return RunItemBaseHeight + (item != null && !string.IsNullOrWhiteSpace(item.detail) ? RunItemDetailHeight : 0f);
        }

        private static float CalculateEditContentHeight(PungentChecklistDefinition draft)
        {
            float height = 36f;
            if (draft == null || draft.sections == null || draft.sections.Count == 0)
                return 120f;

            for (int s = 0; s < draft.sections.Count; s++)
            {
                PungentChecklistSectionDefinition section = draft.sections[s];
                if (section == null)
                    continue;
                height += EditSectionHeight + 3f;
                height += Mathf.Max(1, section.items == null ? 0 : section.items.Count) * EditItemHeight;
                height += 4f;
            }
            return height + 12f;
        }

        private static string GetCommentDraft(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state, string itemId, string persisted)
        {
            if (state.checklistCommentDrafts == null)
                state.checklistCommentDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string key = CommentDraftKey(checklist, itemId);
            if (!state.checklistCommentDrafts.TryGetValue(key, out string draft))
            {
                draft = persisted ?? PungentChecklistUtilityStateService.GetComment(checklist, itemId);
                state.checklistCommentDrafts[key] = draft ?? string.Empty;
            }
            return draft ?? string.Empty;
        }

        private static bool CommitCommentDraft(PungentChecklistDefinition checklist, PungentStickyNoteOverlayState state, string itemId)
        {
            if (checklist == null || state == null || string.IsNullOrWhiteSpace(itemId) || state.checklistCommentDrafts == null)
                return false;

            string key = CommentDraftKey(checklist, itemId);
            if (!state.checklistCommentDrafts.TryGetValue(key, out string draft))
                return false;

            string persisted = PungentChecklistUtilityStateService.GetComment(checklist, itemId);
            if (string.Equals(persisted ?? string.Empty, draft ?? string.Empty, StringComparison.Ordinal))
                return false;

            PungentChecklistUtilityStateService.SetComment(checklist, itemId, draft);
            if (RunCache.commentByItemId != null)
                RunCache.commentByItemId[itemId] = draft ?? string.Empty;
            if (state.checklistDirtyCommentKeys != null)
                state.checklistDirtyCommentKeys.Remove(key);
            state.checklistCommentsDirty = state.checklistDirtyCommentKeys != null && state.checklistDirtyCommentKeys.Count > 0;
            state.checklistLastCommentEditTime = EditorApplication.timeSinceStartup;
            if (!string.IsNullOrWhiteSpace(state.checklistAppliedSearch))
            {
                state.checklistOverlayDirtyVersion++;
                RunCache.Clear();
            }
            return true;
        }

        private static bool CommitCommentDrafts(PungentStickyNoteOverlayState state)
        {
            PungentChecklistDefinition checklist = ActiveChecklist(state);
            if (checklist == null || state == null || state.checklistCommentDrafts == null || state.checklistCommentDrafts.Count == 0)
                return false;
            if (state.checklistDirtyCommentKeys == null || state.checklistDirtyCommentKeys.Count == 0)
            {
                state.checklistCommentsDirty = false;
                return false;
            }

            bool changed = false;
            string prefix = ChecklistDraftPrefix(checklist);
            DirtyCommentScratch.Clear();
            foreach (string key in state.checklistDirtyCommentKeys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    DirtyCommentScratch.Add(key);
            }

            for (int i = 0; i < DirtyCommentScratch.Count; i++)
            {
                string key = DirtyCommentScratch[i];
                if (!state.checklistCommentDrafts.TryGetValue(key, out string draft))
                    continue;

                string itemId = key.Substring(prefix.Length);
                string persisted = PungentChecklistUtilityStateService.GetComment(checklist, itemId);
                if (string.Equals(persisted ?? string.Empty, draft ?? string.Empty, StringComparison.Ordinal))
                {
                    state.checklistDirtyCommentKeys.Remove(key);
                    continue;
                }

                PungentChecklistUtilityStateService.SetComment(checklist, itemId, draft);
                RunCache.commentByItemId[itemId] = draft ?? string.Empty;
                state.checklistDirtyCommentKeys.Remove(key);
                changed = true;
            }

            if (changed && !string.IsNullOrWhiteSpace(state.checklistAppliedSearch))
            {
                state.checklistOverlayDirtyVersion++;
                RunCache.Clear();
            }

            state.checklistCommentsDirty = state.checklistDirtyCommentKeys.Count > 0;
            DirtyCommentScratch.Clear();
            return changed;
        }

        private static string CommentDraftKey(PungentChecklistDefinition checklist, string itemId)
        {
            return ChecklistDraftPrefix(checklist) + (itemId ?? string.Empty);
        }

        private static string ChecklistDraftPrefix(PungentChecklistDefinition checklist)
        {
            return (checklist == null ? string.Empty : checklist.checklistId ?? string.Empty) + CommentKeySeparator;
        }

        private static PungentChecklistDefinition ActiveChecklist(PungentStickyNoteOverlayState state)
        {
            if (state == null || state.authoringReference == null)
                return null;
            if (state.authoringReference.itemKind != PungentAuthoringItemKind.Checklist &&
                !string.Equals(state.authoringReference.providerId, PungentChecklistConstants.ProviderId, StringComparison.OrdinalIgnoreCase))
                return null;

            return PungentChecklistDefinitionRegistry.Find(state.authoringReference.itemId);
        }

        private static void HandleOverlayUpdate(PungentStickyNoteOverlayState state)
        {
            if (state == null)
                return;

            bool changed = false;
            if (state.checklistFilterPending &&
                EditorApplication.timeSinceStartup - state.checklistLastSearchEditTime >= ChecklistSearchDebounceSeconds)
            {
                state.checklistAppliedSearch = state.checklistSearch ?? string.Empty;
                state.checklistFilterPending = false;
                RunCache.Clear();
                changed = true;
            }

            if (state.checklistCommentsDirty &&
                EditorApplication.timeSinceStartup - state.checklistLastCommentEditTime >= ChecklistCommentAutosaveDelaySeconds)
                changed |= CommitCommentDrafts(state);

            if (changed)
                PungentStickyNoteOverlayTrayWindow.RepaintIfOpen();
        }

        private static void HandleBeforeOverlayReset(PungentStickyNoteOverlayState state)
        {
            CommitCommentDrafts(state);
        }

        private static PungentChecklistDefinitionEditSession GetEditSession(PungentChecklistDefinition checklist)
        {
            string id = checklist == null ? string.Empty : checklist.checklistId;
            if (!EditSessions.TryGetValue(id, out PungentChecklistDefinitionEditSession session))
            {
                session = new PungentChecklistDefinitionEditSession();
                EditSessions[id] = session;
            }
            if (!session.HasDraft || !string.Equals(session.SourceId, id, StringComparison.OrdinalIgnoreCase))
                session.Load(checklist);
            return session;
        }

        private static Color StateColor(PungentChecklistStateOptionDefinition state)
        {
            if (state == null)
                return UtilityWindowTheme.Neutral;
            switch (PungentChecklistThemeTokens.Normalize(state.themeToken))
            {
                case PungentChecklistThemeTokens.Green: return UtilityWindowTheme.Green;
                case PungentChecklistThemeTokens.Amber: return UtilityWindowTheme.Amber;
                case PungentChecklistThemeTokens.Red: return UtilityWindowTheme.Red;
                case PungentChecklistThemeTokens.Blue: return UtilityWindowTheme.Blue;
                case PungentChecklistThemeTokens.Cyan: return UtilityWindowTheme.Cyan;
                case PungentChecklistThemeTokens.Purple: return UtilityWindowTheme.Purple;
                default: return UtilityWindowTheme.Neutral;
            }
        }

        private static void DrawPill(Rect rect, string label, Color tint)
        {
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, 0.92f)
                : new Color(Mathf.Lerp(1f, tint.r, 0.22f), Mathf.Lerp(1f, tint.g, 0.22f), Mathf.Lerp(1f, tint.b, 0.22f), 0.96f);
            EditorGUI.DrawRect(rect, fill);
            GUI.Label(rect, label ?? string.Empty, EditorStyles.centeredGreyMiniLabel);
        }

        private static void EnsureStyles()
        {
            if (_mutedCenteredStyle != null)
                return;

            _mutedCenteredStyle = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
    }
#endif
}
