namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentUtilityHelpNotesBookmarksTray
    {
        public static void Show(Rect activatorRect, PungentUtilityHelpContext context, Action onChanged = null)
        {
            context = context ?? new PungentUtilityHelpContext();
            context.Normalize();
            PopupWindow.Show(activatorRect, new PungentUtilityHelpNotesBookmarksPopupContent(context, onChanged));
        }
    }

    internal enum PungentUtilityHelpNotesBookmarksTypeFilter
    {
        All,
        Notes,
        Bookmarks
    }

    internal enum PungentUtilityHelpNotesBookmarksScopeFilter
    {
        AllHelp,
        CurrentTopic,
        CurrentUtility,
        Recent
    }

    internal sealed class PungentUtilityHelpNotesBookmarksPopupContent : PopupWindowContent
    {
        private const float Width = 560f;
        private const float Height = 640f;
        private const int MaxRowsPerSection = 24;
        private const string PrefPrefix = "PungentFunkUtilities.HelpBrowser.NotesBookmarks.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefType = PrefPrefix + "Type";
        private const string PrefScope = PrefPrefix + "Scope";
        private const string PrefIncludeArchived = PrefPrefix + "IncludeArchived";
        private const string PrefIncludeDeveloperOnly = PrefPrefix + "IncludeDeveloperOnly";

        private readonly PungentUtilityHelpContext _context;
        private readonly Action _onChanged;
        private readonly List<PungentUtilityHelpAnnotation> _allRows = new List<PungentUtilityHelpAnnotation>();
        private readonly List<PungentUtilityHelpAnnotation> _filteredRows = new List<PungentUtilityHelpAnnotation>();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private PungentUtilityHelpNotesBookmarksTypeFilter _typeFilter = PungentUtilityHelpNotesBookmarksTypeFilter.All;
        private PungentUtilityHelpNotesBookmarksScopeFilter _scopeFilter = PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp;
        private bool _includeArchived;
        private bool _includeDeveloperOnly;
        private bool _filterDirty = true;
        private string _status = "Ready.";

        public PungentUtilityHelpNotesBookmarksPopupContent(PungentUtilityHelpContext context, Action onChanged)
        {
            _context = context ?? new PungentUtilityHelpContext();
            _context.Normalize();
            _onChanged = onChanged;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(Width, Height);
        }

        public override void OnOpen()
        {
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _typeFilter = LoadEnum(PrefType, PungentUtilityHelpNotesBookmarksTypeFilter.All);
            _scopeFilter = LoadEnum(PrefScope, PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp);
            _includeArchived = UtilityWindowPrefs.GetBool(PrefIncludeArchived, false);
            _includeDeveloperOnly = UtilityWindowPrefs.GetBool(PrefIncludeDeveloperOnly, false);
            PungentUtilityHelpNotesBridge.Changed -= HandleSourceChanged;
            PungentUtilityHelpNotesBridge.Changed += HandleSourceChanged;
            PungentUtilityHelpAnnotationStorage.Changed -= HandleSourceChanged;
            PungentUtilityHelpAnnotationStorage.Changed += HandleSourceChanged;
            RefreshRows();
        }

        public override void OnClose()
        {
            PungentUtilityHelpNotesBridge.Changed -= HandleSourceChanged;
            PungentUtilityHelpAnnotationStorage.Changed -= HandleSourceChanged;
            SavePrefs();
        }

        public override void OnGUI(Rect rect)
        {
            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandHeight(true));
            try
            {
                EnsureFiltered();
                DrawSummary();
                DrawSearchAndFilters();
                DrawCurrentTopicSection();
                DrawBookmarksSection();
                DrawNotesSection();
                DrawRecentSection();
                DrawActions();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                GUILayout.Label(new GUIContent("Notes & Bookmarks", "View all Help Browser notes, bookmarks, and topic annotations."), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.InfoPill(new GUIContent(_filteredRows.Count + " shown", "Rows after the current tray filters."), UtilityWindowTheme.Neutral, 78f);
                if (GUILayout.Button(new GUIContent("Refresh", "Refresh local bookmarks and bridge-provided Help notes."), EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    RefreshRows();
            }
        }

        private void DrawSummary()
        {
            int localBookmarks = _allRows.Count(IsLocalBookmark);
            int helpNotes = _allRows.Count(IsBridgeNote);
            int currentTopic = _allRows.Count(IsCurrentTopic);
            int recent = _allRows.Count(IsRecent);
            bool bridgeAvailable = PungentUtilityHelpNotesBridge.GetAllHelpAnnotations != null || PungentUtilityHelpNotesBridge.CountAllHelpNotes != null;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.04f, 5, 3)))
            {
                UtilityWindowTheme.SectionTitle("Summary", UtilityWindowTheme.Blue);
                UtilityWindowTheme.DrawWrappedPillGrid(new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(localBookmarks + " bookmarks", "Local Help bookmarks stored in Core editor storage."), localBookmarks > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, () => SetFilters(PungentUtilityHelpNotesBookmarksTypeFilter.Bookmarks, PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp), 118f),
                    new UtilityWindowTheme.PillSpec(new GUIContent(helpNotes + " help notes", "Help-linked Notes & Roadmap notes returned through the optional bridge."), helpNotes > 0 ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, () => SetFilters(PungentUtilityHelpNotesBookmarksTypeFilter.Notes, PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp), 118f),
                    new UtilityWindowTheme.PillSpec(new GUIContent(currentTopic + " current", "Notes and bookmarks attached to the current Help topic."), currentTopic > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, () => SetFilters(PungentUtilityHelpNotesBookmarksTypeFilter.All, PungentUtilityHelpNotesBookmarksScopeFilter.CurrentTopic), 112f),
                    new UtilityWindowTheme.PillSpec(new GUIContent(recent + " recent", "Help notes and bookmarks created or updated recently."), recent > 0 ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral, () => SetFilters(PungentUtilityHelpNotesBookmarksTypeFilter.All, PungentUtilityHelpNotesBookmarksScopeFilter.Recent), 98f),
                    new UtilityWindowTheme.PillSpec(new GUIContent(bridgeAvailable ? "Notes ready" : "Notes unavailable", bridgeAvailable ? "Notes & Roadmap bridge is available." : "Notes integration is not available. Local Help bookmarks are still available."), bridgeAvailable ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, bridgeAvailable && PungentUtilityHelpNotesBridge.OpenAllHelpNotes != null ? (Action)(() => PungentUtilityHelpNotesBridge.OpenAllHelpNotes()) : null, 126f)
                }, Width - 42f);

                if (!bridgeAvailable)
                    EditorGUILayout.HelpBox("Notes integration is not available. Local Help bookmarks are still available.", MessageType.Info);
            }
        }

        private void DrawSearchAndFilters()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 5, 3)))
            {
                UtilityWindowTheme.SectionTitle("Search", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _search = EditorGUILayout.TextField(new GUIContent("Search", "Search title, topic ID, anchor, excerpt, source, status, priority, or utility."), _search);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _typeFilter = (PungentUtilityHelpNotesBookmarksTypeFilter)EditorGUILayout.EnumPopup(new GUIContent("Type", "Filter by notes or bookmarks."), _typeFilter);
                    _scopeFilter = (PungentUtilityHelpNotesBookmarksScopeFilter)EditorGUILayout.EnumPopup(new GUIContent("Scope", "Filter by all Help, current topic, current utility, or recent items."), _scopeFilter);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _includeArchived = GUILayout.Toggle(_includeArchived, new GUIContent("Include Archived", "Show archived Notes & Roadmap items when the bridge returns them."), EditorStyles.miniButton, GUILayout.Width(118f));
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled))
                    {
                        _includeDeveloperOnly = GUILayout.Toggle(_includeDeveloperOnly, new GUIContent("Developer Notes", "Show developer-only Help notes. Developer Mode only."), EditorStyles.miniButton, GUILayout.Width(118f));
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_search) && _typeFilter == PungentUtilityHelpNotesBookmarksTypeFilter.All && _scopeFilter == PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp && !_includeArchived && !_includeDeveloperOnly))
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear Notes & Bookmarks filters."), EditorStyles.miniButton, GUILayout.Width(52f)))
                        {
                            _search = string.Empty;
                            _typeFilter = PungentUtilityHelpNotesBookmarksTypeFilter.All;
                            _scopeFilter = PungentUtilityHelpNotesBookmarksScopeFilter.AllHelp;
                            _includeArchived = false;
                            _includeDeveloperOnly = false;
                            MarkFilterDirty();
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                if (EditorGUI.EndChangeCheck())
                    MarkFilterDirty();
            }
        }

        private void DrawCurrentTopicSection()
        {
            if (string.IsNullOrWhiteSpace(CurrentTopicStableId))
                return;

            DrawSection(
                "Current Topic",
                _allRows.Where(IsCurrentTopic).Where(PassesGlobalVisibility).OrderByDescending(RowDate).Take(MaxRowsPerSection).ToList(),
                "No notes or bookmarks are attached to the current topic yet. Use Add Topic Note or Bookmark Topic from the topic toolbar.");
        }

        private void DrawBookmarksSection()
        {
            DrawSection(
                "Help Bookmarks",
                _filteredRows.Where(IsLocalBookmark).Take(MaxRowsPerSection).ToList(),
                "No local Help bookmarks match the current filters.");
        }

        private void DrawNotesSection()
        {
            DrawSection(
                "Help Notes",
                _filteredRows.Where(a => !IsLocalBookmark(a)).Take(MaxRowsPerSection).ToList(),
                PungentUtilityHelpNotesBridge.GetAllHelpAnnotations != null
                    ? "No Help-linked Notes & Roadmap notes match the current filters."
                    : "Notes integration is not available. Local Help bookmarks are still available.");
        }

        private void DrawRecentSection()
        {
            DrawSection(
                "Recent",
                _allRows.Where(IsRecent).Where(PassesGlobalVisibility).OrderByDescending(RowDate).Take(8).ToList(),
                "No recent Help notes or bookmarks were found.");
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.03f, 5, 3)))
            {
                UtilityWindowTheme.SectionTitle("Actions", UtilityWindowTheme.Purple);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(PungentUtilityHelpNotesBridge.OpenAllHelpNotes == null))
                    {
                        if (GUILayout.Button(new GUIContent("Open Notes & Roadmap", PungentUtilityHelpNotesBridge.OpenAllHelpNotes == null ? "Notes integration is not available." : "Open the full Notes & Roadmap browser for deeper editing."), EditorStyles.miniButton, GUILayout.Width(148f)))
                            PungentUtilityHelpNotesBridge.OpenAllHelpNotes();
                    }

                    if (GUILayout.Button(new GUIContent("Copy Summary", "Copy a compact summary of visible Help notes and bookmarks."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildSummaryText();
                        _status = "Copied Notes & Bookmarks summary.";
                    }

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("PFU_AUTHORING_TODO: Route Help note/bookmark listing through Authoring providers once the Authoring Browser shell owns global cross-item search.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawSection(string title, List<PungentUtilityHelpAnnotation> rows, string emptyMessage)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.08f, 0.03f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.InfoPill(new GUIContent(rows.Count.ToString(), "Rows shown in this section."), rows.Count > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, 42f);
                }

                if (rows.Count == 0)
                {
                    EditorGUILayout.LabelField(emptyMessage, UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                foreach (PungentUtilityHelpAnnotation row in rows)
                    DrawRow(row);

                int total = title == "Help Bookmarks"
                    ? _filteredRows.Count(IsLocalBookmark)
                    : title == "Help Notes"
                        ? _filteredRows.Count(a => !IsLocalBookmark(a))
                        : rows.Count;
                if (total > rows.Count)
                    EditorGUILayout.LabelField("Showing " + rows.Count + " of " + total + ". Narrow the search to review more.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawRow(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null)
                return;

            Color tint = IsLocalBookmark(annotation) ? UtilityWindowTheme.Amber : annotation.kind == PungentUtilityHelpAnnotationKind.Bookmark ? UtilityWindowTheme.Purple : UtilityWindowTheme.Cyan;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.03f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(annotation.title) ? "Untitled" : annotation.title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.InfoPill(new GUIContent(annotation.sourceLabel, "Source: " + annotation.sourceLabel), tint, Mathf.Clamp((annotation.sourceLabel ?? string.Empty).Length * 6f + 28f, 92f, 150f));
                }

                string topicLabel = !string.IsNullOrWhiteSpace(annotation.topicTitle) ? annotation.topicTitle : annotation.topicStableId;
                string anchor = !string.IsNullOrWhiteSpace(annotation.anchorLabel) ? annotation.anchorLabel : annotation.anchorId;
                EditorGUILayout.LabelField("Topic: " + (string.IsNullOrWhiteSpace(topicLabel) ? "(unresolved)" : topicLabel), UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(annotation.topicStableId) || !string.IsNullOrWhiteSpace(anchor))
                    EditorGUILayout.LabelField((string.IsNullOrWhiteSpace(annotation.topicStableId) ? string.Empty : annotation.topicStableId) + (string.IsNullOrWhiteSpace(anchor) ? string.Empty : " | " + anchor), UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!string.IsNullOrWhiteSpace(annotation.statusLabel))
                        UtilityWindowTheme.InfoPill(new GUIContent(annotation.statusLabel, "Note status."), UtilityWindowTheme.Neutral, Mathf.Clamp(annotation.statusLabel.Length * 6f + 28f, 64f, 104f));
                    if (!string.IsNullOrWhiteSpace(annotation.priorityLabel))
                        UtilityWindowTheme.InfoPill(new GUIContent(annotation.priorityLabel, "Note priority."), UtilityWindowTheme.Neutral, Mathf.Clamp(annotation.priorityLabel.Length * 6f + 28f, 64f, 128f));
                    string date = ShortDate(annotation.updatedUtc, annotation.createdUtc);
                    if (!string.IsNullOrWhiteSpace(date))
                        EditorGUILayout.LabelField(date, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(108f));
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(annotation.excerpt))
                    EditorGUILayout.LabelField(annotation.excerpt, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(annotation.topicStableId)))
                    {
                        if (GUILayout.Button(new GUIContent("Open Topic", "Open this Help topic in the full Help Browser."), EditorStyles.miniButton, GUILayout.Width(84f)))
                            OpenTopic(annotation);
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(annotation.topicStableId)))
                    {
                        if (GUILayout.Button(new GUIContent("Copy Topic ID", "Copy the stable Help topic ID."), EditorStyles.miniButton, GUILayout.Width(96f)))
                            EditorGUIUtility.systemCopyBuffer = annotation.topicStableId;
                    }

                    if (IsLocalBookmark(annotation))
                    {
                        if (GUILayout.Button(new GUIContent("Remove Bookmark", "Remove this local Help bookmark."), EditorStyles.miniButton, GUILayout.Width(124f)))
                            RemoveBookmark(annotation);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(PungentUtilityHelpNotesBridge.OpenNote == null || string.IsNullOrWhiteSpace(annotation.noteId)))
                        {
                            if (GUILayout.Button(new GUIContent("Open Note", PungentUtilityHelpNotesBridge.OpenNote == null ? "Notes integration is not available." : "Open this note in Notes & Roadmap."), EditorStyles.miniButton, GUILayout.Width(82f)))
                                PungentUtilityHelpNotesBridge.OpenNote(annotation.noteId);
                        }

                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(annotation.noteId)))
                        {
                            if (GUILayout.Button(new GUIContent("Copy Note ID", "Copy the Notes & Roadmap note ID."), EditorStyles.miniButton, GUILayout.Width(92f)))
                                EditorGUIUtility.systemCopyBuffer = annotation.noteId;
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void RefreshRows()
        {
            _allRows.Clear();
            _allRows.AddRange(PungentUtilityHelpAnnotationStorage.instance.GetAllBookmarks());

            if (PungentUtilityHelpNotesBridge.GetAllHelpAnnotations != null)
            {
                try
                {
                    List<PungentUtilityHelpAnnotation> bridgeRows = PungentUtilityHelpNotesBridge.GetAllHelpAnnotations();
                    if (bridgeRows != null)
                        _allRows.AddRange(bridgeRows.Where(a => a != null));
                }
                catch (Exception ex)
                {
                    _status = "Notes bridge unavailable: " + ex.Message;
                }
            }

            DeduplicateRows();
            MarkFilterDirty();
        }

        private void HandleSourceChanged()
        {
            RefreshRows();
            if (editorWindow != null)
                editorWindow.Repaint();
        }

        private void DeduplicateRows()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = _allRows.Count - 1; i >= 0; i--)
            {
                PungentUtilityHelpAnnotation row = _allRows[i];
                string key = (row.sourceLabel ?? string.Empty) + "|" + (row.noteId ?? string.Empty) + "|" + (row.topicStableId ?? string.Empty) + "|" + (row.anchorId ?? string.Empty);
                if (!seen.Add(key))
                    _allRows.RemoveAt(i);
            }
        }

        private void EnsureFiltered()
        {
            if (!_filterDirty)
                return;

            _filterDirty = false;
            _filteredRows.Clear();
            _filteredRows.AddRange(_allRows.Where(PassesFilters).OrderByDescending(RowDate));
        }

        private bool PassesFilters(PungentUtilityHelpAnnotation annotation)
        {
            if (!PassesGlobalVisibility(annotation))
                return false;

            switch (_typeFilter)
            {
                case PungentUtilityHelpNotesBookmarksTypeFilter.Notes:
                    if (annotation.kind != PungentUtilityHelpAnnotationKind.Note)
                        return false;
                    break;
                case PungentUtilityHelpNotesBookmarksTypeFilter.Bookmarks:
                    if (annotation.kind != PungentUtilityHelpAnnotationKind.Bookmark)
                        return false;
                    break;
            }

            switch (_scopeFilter)
            {
                case PungentUtilityHelpNotesBookmarksScopeFilter.CurrentTopic:
                    if (!IsCurrentTopic(annotation))
                        return false;
                    break;
                case PungentUtilityHelpNotesBookmarksScopeFilter.CurrentUtility:
                    if (string.IsNullOrWhiteSpace(_context.utilityId) || !string.Equals(annotation.utilityId, _context.utilityId, StringComparison.OrdinalIgnoreCase))
                        return false;
                    break;
                case PungentUtilityHelpNotesBookmarksScopeFilter.Recent:
                    if (!IsRecent(annotation))
                        return false;
                    break;
            }

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string q = _search.Trim();
            return Contains(annotation.title, q) ||
                   Contains(annotation.topicTitle, q) ||
                   Contains(annotation.topicStableId, q) ||
                   Contains(annotation.utilityId, q) ||
                   Contains(annotation.anchorId, q) ||
                   Contains(annotation.anchorLabel, q) ||
                   Contains(annotation.excerpt, q) ||
                   Contains(annotation.sourceLabel, q) ||
                   Contains(annotation.statusLabel, q) ||
                   Contains(annotation.priorityLabel, q);
        }

        private bool PassesGlobalVisibility(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null)
                return false;
            if (!_includeArchived && annotation.archived)
                return false;
            if ((!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled || !_includeDeveloperOnly) && annotation.developerOnly)
                return false;
            return true;
        }

        private void RemoveBookmark(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null || !IsLocalBookmark(annotation))
                return;

            if (!PungentUtilityHelpAnnotationStorage.instance.RemoveBookmark(annotation.noteId))
                return;

            _status = "Removed local Help bookmark.";
            RefreshRows();
            _onChanged?.Invoke();
        }

        private void OpenTopic(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null || string.IsNullOrWhiteSpace(annotation.topicStableId))
                return;

            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(annotation.topicStableId);
            if (topic != null)
                PungentUtilityHelpRegistry.Open(topic.utilityId, topic.sectionId, topic.topicId);
            else
                PungentUtilityHelpRegistry.Open(annotation.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);

            ClosePopup();
            GUIUtility.ExitGUI();
        }

        private void SetFilters(PungentUtilityHelpNotesBookmarksTypeFilter type, PungentUtilityHelpNotesBookmarksScopeFilter scope)
        {
            _typeFilter = type;
            _scopeFilter = scope;
            MarkFilterDirty();
        }

        private void MarkFilterDirty()
        {
            _filterDirty = true;
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefType, _typeFilter.ToString());
            UtilityWindowPrefs.SetString(PrefScope, _scopeFilter.ToString());
            UtilityWindowPrefs.SetBool(PrefIncludeArchived, _includeArchived);
            UtilityWindowPrefs.SetBool(PrefIncludeDeveloperOnly, _includeDeveloperOnly);
        }

        private string BuildSummaryText()
        {
            EnsureFiltered();
            return "Help Notes & Bookmarks\n" +
                   "Shown: " + _filteredRows.Count + "\n" +
                   "Local bookmarks: " + _allRows.Count(IsLocalBookmark) + "\n" +
                   "Help-linked notes: " + _allRows.Count(IsBridgeNote) + "\n" +
                   "Current topic: " + CurrentTopicStableId + "\n" +
                   "Filter: " + _typeFilter + " / " + _scopeFilter;
        }

        private bool IsCurrentTopic(PungentUtilityHelpAnnotation annotation)
        {
            return annotation != null &&
                   !string.IsNullOrWhiteSpace(CurrentTopicStableId) &&
                   string.Equals(annotation.topicStableId, CurrentTopicStableId, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRecent(PungentUtilityHelpAnnotation annotation)
        {
            return RowDate(annotation) >= DateTime.UtcNow.AddDays(-14d);
        }

        private static bool IsLocalBookmark(PungentUtilityHelpAnnotation annotation)
        {
            return annotation != null &&
                   annotation.kind == PungentUtilityHelpAnnotationKind.Bookmark &&
                   string.Equals(annotation.sourceLabel, "Local Help Bookmark", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBridgeNote(PungentUtilityHelpAnnotation annotation)
        {
            return annotation != null && !IsLocalBookmark(annotation) && annotation.kind == PungentUtilityHelpAnnotationKind.Note;
        }

        private string CurrentTopicStableId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_context.StableTopicId))
                    return _context.StableTopicId;
                return string.Empty;
            }
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DateTime RowDate(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null)
                return DateTime.MinValue;

            if (DateTime.TryParse(annotation.updatedUtc, out DateTime updated))
                return updated.ToUniversalTime();
            if (DateTime.TryParse(annotation.createdUtc, out DateTime created))
                return created.ToUniversalTime();
            return DateTime.MinValue;
        }

        private static string ShortDate(string preferred, string fallback)
        {
            if (DateTime.TryParse(preferred, out DateTime parsed) || DateTime.TryParse(fallback, out parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd");
            return string.Empty;
        }

        private static T LoadEnum<T>(string key, T fallback) where T : struct
        {
            string value = UtilityWindowPrefs.GetString(key, fallback.ToString());
            return Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }

        private void ClosePopup()
        {
            if (editorWindow != null)
                editorWindow.Close();
        }
    }
#endif
}
