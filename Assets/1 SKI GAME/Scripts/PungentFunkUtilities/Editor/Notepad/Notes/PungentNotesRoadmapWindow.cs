using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Core.Help;
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentNotesRoadmapWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.NotesRoadmap.";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefRightWidth = PrefPrefix + "RightWidth";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefShowArchived = PrefPrefix + "ShowArchived";
        private const string PrefShowDeveloper = PrefPrefix + "ShowDeveloper";
        private const string PrefView = PrefPrefix + "View";
        private const string PrefSort = PrefPrefix + "Sort";
        private const string PrefGroup = PrefPrefix + "Group";
        private const string PrefSelectedNote = PrefPrefix + "SelectedNote";
        private const string PrefTagsFoldout = PrefPrefix + "TagsFoldout";
        private const string PrefSourcesFoldout = PrefPrefix + "SourcesFoldout";
        private const string PrefRecentNotes = PrefPrefix + "RecentNotes";
        private const string PrefBrowserCollapsed = PrefPrefix + "BrowserCollapsed";
        private const string PrefPropertiesCollapsed = PrefPrefix + "PropertiesCollapsed";
        private const string PrefPreviewMode = PrefPrefix + "PreviewMode";
        private const string PrefShowStickyNotes = PrefPrefix + "ShowStickyNotes";
        private const string PrefShowRichDocuments = PrefPrefix + "ShowRichDocuments";
        private const string PrefShowDataSheets = PrefPrefix + "ShowDataSheets";
        private const string PrefShowNodeGraphs = PrefPrefix + "ShowNodeGraphs";
        private const string PrefShowChecklists = PrefPrefix + "ShowChecklists";
        private const string PrefShowAuthoringCreated = PrefPrefix + "ShowAuthoringCreated";
        private const string PrefShowAuthoringUpdated = PrefPrefix + "ShowAuthoringUpdated";
        private const string PrefShowAuthoringStatus = PrefPrefix + "ShowAuthoringStatus";
        private const string PrefShowAuthoringPriority = PrefPrefix + "ShowAuthoringPriority";
        private const string PrefShowAuthoringKind = PrefPrefix + "ShowAuthoringKind";
        private const string PrefShowAuthoringTags = PrefPrefix + "ShowAuthoringTags";
        private const string PrefShowAuthoringTargets = PrefPrefix + "ShowAuthoringTargets";
        private const float MinLeftWidth = 260f;
        private const float MinWorkspaceWidth = 360f;
        private const float MinRightWidth = 250f;
        private const float SplitterWidth = 9f;
        private const float CollapsedRailWidth = 38f;
        private const float BodyPaddingReserve = 24f;
        private const double DraftAutosaveDelaySeconds = 1.25d;
        private const double InlinePreviewIdleDelaySeconds = 0.5d;
        private const double PreferenceSaveDelaySeconds = 0.45d;
        private const int MaxRecentNotes = 8;
        private static readonly Vector2 SlimBrowserMinSize = new Vector2(320f, 360f);
        private const float SlimToolbarNewWidth = 44f;
        private const float SlimToolbarMoreWidth = 52f;
        private const float SlimToolbarMinSearchWidth = 128f;
        private const float SlimToolbarHelpWidth = 28f;
        private const string SlimSearchControlName = "PungentStickyNotesSlimSearch";
        private const float AuthoringRowHeight = 26f;
        private const float AuthoringActionRowHeight = 28f;
        private const float AuthoringRowOverscan = 72f;

        private enum AuthoringBrowserInterface
        {
            StickyNotes,
            RichDocuments,
            DataSheets,
            NodeGraphs,
            Checklists
        }

        [Flags]
        private enum ToolbarOverflow
        {
            None = 0,
            NewSticky = 1 << 0,
            NewForSelection = 1 << 1,
            Templates = 1 << 2,
            View = 1 << 3,
            Types = 1 << 4,
            Filters = 1 << 5,
            Sort = 1 << 6,
            Settings = 1 << 7,
            Surfaces = 1 << 8,
            Help = 1 << 9,
            Selection = 1 << 10,
            Count = 1 << 11
        }

        private sealed class AuthoringBrowserItem
        {
            public string key = string.Empty;
            public PungentAuthoringMetadata metadata;
            public PungentAuthoringReference reference;
            public PungentAuthoringPreview preview;
            public bool previewResolved;
            public PungentNote note;
            public AuthoringBrowserInterface interfaceKind;
            public string providerDisplayName = string.Empty;

            public string Title => metadata == null || string.IsNullOrWhiteSpace(metadata.title) ? reference?.itemId ?? "Untitled" : metadata.title;
            public string Summary => metadata != null && !string.IsNullOrWhiteSpace(metadata.summary) ? metadata.summary : preview?.bodyPreview ?? string.Empty;
            public string Status => metadata?.status ?? string.Empty;
            public string Priority => metadata?.priority ?? string.Empty;
            public string KindLabel => metadata == null ? "Authoring Item" : metadata.KindLabel;
            public string ItemId => metadata?.id ?? reference?.itemId ?? string.Empty;
            public bool IsStickyNote => note != null;
            public bool IsLegacyAuthoring => interfaceKind == AuthoringBrowserInterface.StickyNotes;
        }

        private sealed class FilterChipSpec
        {
            public string label = string.Empty;
            public Action clear;
            public float width;
        }

        private readonly PungentNoteFilterState _filters = new PungentNoteFilterState();
        private readonly List<PungentBacklogSeedPreviewItem> _seedPreview = new List<PungentBacklogSeedPreviewItem>();
        private readonly List<string> _recentNoteIds = new List<string>();
        private readonly HashSet<string> _selectedNoteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PungentNote> _visibleNotesCache = new List<PungentNote>();
        private readonly List<PungentNote> _selectedNotesCache = new List<PungentNote>();
        private readonly HashSet<string> _visibleNoteIdCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<PungentNotesSavedView, int> _savedViewCountCache = new Dictionary<PungentNotesSavedView, int>();
        private readonly List<AuthoringBrowserItem> _allAuthoringItemsCache = new List<AuthoringBrowserItem>();
        private readonly List<AuthoringBrowserItem> _visibleAuthoringItemsCache = new List<AuthoringBrowserItem>();
        private readonly HashSet<string> _visibleAuthoringItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedAuthoringItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<PungentParsedToken> _bodyTokenCache = new List<PungentParsedToken>();
        private Vector2 _sidebarScroll;
        private Vector2 _listScroll;
        private Vector2 _writingScroll;
        private Vector2 _detailScroll;
        private Vector2 _seedScroll;
        private float _leftWidth = 320f;
        private float _rightWidth = 330f;
        private string _selectedNoteId = string.Empty;
        private int _lastClickedVisibleIndex = -1;
        private string _bulkTag = string.Empty;
        private string _bulkAuditCode = string.Empty;
        private PungentNoteStatus _bulkStatus = PungentNoteStatus.ToDo;
        private PungentNotePriority _bulkPriority = PungentNotePriority.NiceToHave;
        private PungentNoteKind _bulkKind = PungentNoteKind.General;
        private PungentNoteVisibility _bulkVisibility = PungentNoteVisibility.PrivateProject;
        private bool _bulkDeveloperOnly;
        private string _bulkUtilityId = string.Empty;
        private string _bulkFutureUtilityId = string.Empty;
        private string _pendingDocumentationLinkId = string.Empty;
        private string _tagSearch = string.Empty;
        private string _pendingCsvPath = string.Empty;
        private bool _tagsExpanded;
        private bool _sourcesExpanded = true;
        private bool _browserCollapsed;
        private bool _propertiesCollapsed;
        private bool _previewMode;
        private bool _legacyWorkspaceMode;
        private bool _showStickyNotes = true;
        private bool _showRichDocuments = true;
        private bool _showDataSheets = true;
        private bool _showNodeGraphs = true;
        private bool _showChecklists = true;
        private bool _showAuthoringCreatedDate;
        private bool _showAuthoringUpdatedDate = true;
        private bool _showAuthoringStatus;
        private bool _showAuthoringPriority;
        private bool _showAuthoringKind;
        private bool _showAuthoringTags;
        private bool _showAuthoringTargetCount;
        private bool _authoringItemsDirty = true;
        private bool _focusSlimSearch;
        private string _status = "Ready.";
        private PungentFutureUtilityRecord _editingFutureUtility;
        private PungentNotesGroupMode _groupMode = PungentNotesGroupMode.None;
        private string _draftNoteId = string.Empty;
        private string _draftTitle = string.Empty;
        private string _draftBody = string.Empty;
        private bool _draftDirty;
        private double _lastDraftEditTime;
        private string _draftSaveState = "Saved";
        private string _visibleNotesCacheKey = string.Empty;
        private string _visibleAuthoringItemsCacheKey = string.Empty;
        private string _savedViewCountCacheKey = string.Empty;
        private string _selectedNotesCacheKey = string.Empty;
        private string _bodyTokenCacheKey = string.Empty;
        private string _surfaceSelectionCountKey = string.Empty;
        private PungentNote _selectedNoteCache;
        private long _notesCacheFingerprint;
        private long _authoringItemsSourceFingerprint;
        private bool _selectedFilteredOutCache;
        private bool _prefsDirty;
        private double _nextPrefsSaveTime;
        private int _filterCacheHits;
        private int _filterCacheMisses;
        private double _lastFilterRebuildMs;
        private string _lastHoverPreviewStatus = "Idle";
        private string _lastAutosaveTime = "Never";
        private string _selectedAuthoringItemKey = string.Empty;
        private int _surfaceSelectionNoteCount;
        private GUIStyle _documentPageStyle;
        private GUIStyle _documentTitleStyle;
        private GUIStyle _documentBodyStyle;
        private GUIStyle _documentPreviewStyle;

        private static readonly string[] ViewLabels =
        {
            "All Items",
            "Inbox",
            "Future Features",
            "Current Utilities",
            "Future Utilities",
            "Audit Follow-ups",
            "Implementation Tasks",
            "Design Decisions",
            "Token Documentation",
            "Unlinked Notes",
            "Archived",
            "Developer Notes",
            "Needs Attention",
            "Blocked",
            "In Progress",
            "Recently Updated",
            "Future Utilities: Planned",
            "Future Utilities: Implemented"
        };

        public static void Open()
        {
            PungentNotesRoadmapWindow window = GetWindow<PungentNotesRoadmapWindow>("Sticky Notes");
            window.minSize = SlimBrowserMinSize;
            window.Show();
        }

        public static void OpenAndSelect(string noteId)
        {
            PungentNotesRoadmapWindow window = GetWindow<PungentNotesRoadmapWindow>("Sticky Notes");
            window.minSize = SlimBrowserMinSize;
            window.Show();
            window.Focus();

            bool alreadySelected = window.IsOpenOnNoteId(noteId);
            if (!alreadySelected)
            {
                window.CommitDraftIfDirty("Saved draft before opening note.");
                PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before opening note.");
                window._selectedNoteId = noteId ?? string.Empty;
                window._selectedNoteIds.Clear();
                if (!string.IsNullOrWhiteSpace(noteId))
                {
                    window._selectedNoteIds.Add(noteId);
                    window.SelectAuthoringNoteKey(noteId);
                    window.RecordRecentNote(noteId);
                }
                window.LoadDraftForSelectedNote();
                window._editingFutureUtility = null;
                window._filters.view = PungentNotesSavedView.AllNotes;
                window.SavePrefs();
                window.RefreshGuiCaches();
            }

            window.SelectAuthoringNoteKey(noteId);
            window.RefreshAuthoringVisibleItems(true);
            PungentStickyNoteOverlayController.OpenEdit(noteId, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
        }

        public static void OpenRequests()
        {
            PungentNotesRoadmapWindow window = GetWindow<PungentNotesRoadmapWindow>("Sticky Notes");
            window.minSize = SlimBrowserMinSize;
            window.Show();
            window._filters.view = PungentNotesSavedView.AllNotes;
            window._filters.kind = PungentNoteKind.SupportRequest;
            window._filters.search = string.Empty;
            window._filters.showArchived = true;
            window.SavePrefs();
            window.RefreshGuiCaches(true);

            PungentNote first = PungentSupportRequestBridge.GetSupportRequestNotes(true).FirstOrDefault(note => note != null && !note.archived);
            if (first != null)
                window.SelectNote(first);
            window._status = "Showing support requests.";
        }

        public static bool OpenAuthoringReference(PungentAuthoringReference reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return false;

            bool isLegacyNote = reference.itemKind == PungentAuthoringItemKind.LegacyNote ||
                                reference.itemKind == PungentAuthoringItemKind.Task ||
                                string.Equals(reference.providerId, PungentAuthoringLegacyNoteProvider.Id, StringComparison.OrdinalIgnoreCase);
            if (isLegacyNote && PungentNoteDatabase.instance.notes.Any(note => note != null && string.Equals(note.id, reference.itemId, StringComparison.OrdinalIgnoreCase)))
            {
                OpenAndSelect(reference.itemId);
                return true;
            }

            return PungentAuthoringProviderRegistry.TryOpen(reference);
        }

        public static void AttachDocumentationLinkToSelectedOrNew(string documentationLinkId)
        {
            PungentUtilityDocumentationLinks.DocumentationLink documentationLink = PungentUtilityDocumentationLinks.instance.FindById(documentationLinkId);
            PungentNotesRoadmapWindow window = GetWindow<PungentNotesRoadmapWindow>("Sticky Notes");
            window.minSize = SlimBrowserMinSize;
            window.Show();

            if (documentationLink == null)
            {
                window._status = "Documentation link could not be resolved.";
                return;
            }

            PungentNote note = window.SelectedNote;
            if (note == null)
            {
                note = PungentNoteStorage.Database.CreateNote("Note: " + PungentUtilityDocumentationLinks.GetDisplayName(documentationLink), PungentNoteKind.UtilityNote);
                note.body = "Created from documentation link " + documentationLink.id + ".";
                window.SelectNote(note);
            }

            bool added = PungentNoteStorage.AddDocumentationLinkTarget(note, documentationLink);
            window._status = added
                ? "Attached documentation link to note."
                : "Selected note already references this documentation link.";
            window.SelectNote(note);
            window.SavePrefs();
        }

        public static PungentNote OpenForTarget(UnityEngine.Object targetObject, string propertyPath, string propertyName, bool createNote)
        {
            PungentNotesRoadmapWindow window = GetWindow<PungentNotesRoadmapWindow>("Sticky Notes");
            window.minSize = SlimBrowserMinSize;
            window.Show();

            PungentNote note = null;
            if (createNote)
            {
                note = PungentNoteStorage.CreateNoteForTarget(targetObject, propertyPath, propertyName);
                window.SelectNote(note);
                window._status = "Created note for " + (targetObject != null ? targetObject.name : "selection") + ".";
            }
            else
            {
                List<PungentNote> matches = PungentNoteStorage.FindNotesFor(targetObject, true);
                if (matches.Count > 0)
                {
                    window.SelectNote(matches[0]);
                    if (matches.Count > 1)
                        PungentStickyNoteOverlayController.OpenStack(matches, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, targetObject != null ? targetObject.name : "Target Notes");
                }
                window._filters.view = PungentNotesSavedView.AllNotes;
                window._filters.search = targetObject != null ? targetObject.name : string.Empty;
                window._status = matches.Count + " matching note(s) for current context.";
            }

            window.SavePrefs();
            return note;
        }

        public static PungentNote CreateAuditFollowUp(string issueCode = null, string title = null, string body = null)
        {
            PungentNote note = PungentNoteStorage.Database.CreateNote(string.IsNullOrWhiteSpace(title) ? "Audit Follow-up" : title, PungentNoteKind.AuditFollowUp);
            note.auditIssueCode = issueCode ?? string.Empty;
            note.body = body ?? string.Empty;
            note.priority = PungentNotePriority.Important;
            note.status = PungentNoteStatus.ToDo;
            PungentNoteStorage.Save();
            Open();
            return note;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Sticky Notes");
            wantsMouseMove = true;
            _leftWidth = UtilityWindowPrefs.GetFloat(PrefLeftWidth, 320f);
            _rightWidth = UtilityWindowPrefs.GetFloat(PrefRightWidth, 330f);
            _filters.search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _filters.showArchived = UtilityWindowPrefs.GetBool(PrefShowArchived, false);
            _filters.showDeveloperNotes = UtilityWindowPrefs.GetBool(PrefShowDeveloper, false);
            _filters.view = (PungentNotesSavedView)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefView, 0), 0, Enum.GetValues(typeof(PungentNotesSavedView)).Length - 1);
            _filters.sortMode = (PungentNotesSortMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSort, 0), 0, Enum.GetValues(typeof(PungentNotesSortMode)).Length - 1);
            _groupMode = (PungentNotesGroupMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefGroup, 0), 0, Enum.GetValues(typeof(PungentNotesGroupMode)).Length - 1);
            _selectedNoteId = UtilityWindowPrefs.GetString(PrefSelectedNote, string.Empty);
            if (!string.IsNullOrWhiteSpace(_selectedNoteId))
                _selectedNoteIds.Add(_selectedNoteId);
            _tagsExpanded = UtilityWindowPrefs.GetBool(PrefTagsFoldout, false);
            _sourcesExpanded = UtilityWindowPrefs.GetBool(PrefSourcesFoldout, true);
            _browserCollapsed = UtilityWindowPrefs.GetBool(PrefBrowserCollapsed, false);
            _propertiesCollapsed = UtilityWindowPrefs.GetBool(PrefPropertiesCollapsed, false);
            _previewMode = UtilityWindowPrefs.GetBool(PrefPreviewMode, false);
            _showStickyNotes = UtilityWindowPrefs.GetBool(PrefShowStickyNotes, true);
            _showRichDocuments = UtilityWindowPrefs.GetBool(PrefShowRichDocuments, true);
            _showDataSheets = UtilityWindowPrefs.GetBool(PrefShowDataSheets, true);
            _showNodeGraphs = UtilityWindowPrefs.GetBool(PrefShowNodeGraphs, true);
            _showChecklists = UtilityWindowPrefs.GetBool(PrefShowChecklists, true);
            _showAuthoringCreatedDate = UtilityWindowPrefs.GetBool(PrefShowAuthoringCreated, false);
            _showAuthoringUpdatedDate = UtilityWindowPrefs.GetBool(PrefShowAuthoringUpdated, true);
            _showAuthoringStatus = UtilityWindowPrefs.GetBool(PrefShowAuthoringStatus, false);
            _showAuthoringPriority = UtilityWindowPrefs.GetBool(PrefShowAuthoringPriority, false);
            _showAuthoringKind = UtilityWindowPrefs.GetBool(PrefShowAuthoringKind, false);
            _showAuthoringTags = UtilityWindowPrefs.GetBool(PrefShowAuthoringTags, false);
            _showAuthoringTargetCount = UtilityWindowPrefs.GetBool(PrefShowAuthoringTargets, false);
            LoadRecentNotes();
            PungentNoteStorage.EnsureLoaded();
            PungentAuthoringProviderBootstrap.RegisterBuiltInProviders();
            PungentAuthoringProviderRegistry.Changed -= HandleAuthoringProvidersChanged;
            PungentAuthoringProviderRegistry.Changed += HandleAuthoringProvidersChanged;
            RebuildAuthoringItemSourceCache();
            SelectAuthoringNoteKey(_selectedNoteId);
            LoadDraftForSelectedNote();
            EditorApplication.update -= HandleDraftAutosave;
            EditorApplication.update += HandleDraftAutosave;
            EditorApplication.update -= HandleDeferredPrefsSave;
            EditorApplication.update += HandleDeferredPrefsSave;
            AssemblyReloadEvents.beforeAssemblyReload -= CommitDraftBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += CommitDraftBeforeReload;
        }

        private void OnDisable()
        {
            CommitDraftIfDirty("Saved draft on close.");
            PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
            PungentAuthoringProviderRegistry.Changed -= HandleAuthoringProvidersChanged;
            EditorApplication.update -= HandleDraftAutosave;
            EditorApplication.update -= HandleDeferredPrefsSave;
            AssemblyReloadEvents.beforeAssemblyReload -= CommitDraftBeforeReload;
            FlushPrefs();
        }

        private void OnGUI()
        {
            PungentNoteStorage.EnsureLoaded();
            UtilityWindowTheme.Header("Sticky Notes", "Quick contextual notes, reminders, checklists, and inspector/scene-linked annotations.", _status);
            RefreshGuiCaches();
            HandleSlimBrowserKeyboardShortcuts();

            if (_legacyWorkspaceMode)
            {
                DrawLegacyWorkspace();
                return;
            }

            List<PungentNote> visibleNotes = _visibleNotesCache;
            List<AuthoringBrowserItem> visibleItems = _visibleAuthoringItemsCache;
            DrawSlimBrowserToolbar(visibleItems);

            if (_seedPreview.Count > 0)
                DrawSeedPreview();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, margin: 2), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawAuthoringItemList(visibleItems);
                _lastHoverPreviewStatus = PungentStickyNoteOverlayController.LastCacheStatus;
                DrawDeveloperDiagnostics(visibleNotes);
            }

            PungentStickyNoteOverlayController.Draw(
                PungentStickyNoteOverlayOwner.BrowserWindow,
                new Rect(0f, 0f, position.width, position.height));
        }

        private void DrawLegacyWorkspace()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f, 5, 2)))
            {
                EditorGUILayout.LabelField("Legacy advanced workspace", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Use this for backlog, future-utility, and bulk-management compatibility.", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Return to Slim Browser", EditorStyles.miniButton, GUILayout.Width(144f)))
                {
                    CommitDraftIfDirty("Saved draft before returning to slim browser.");
                    _legacyWorkspaceMode = false;
                    PungentStickyNoteOverlayController.OpenEdit(_selectedNoteId, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
                }
            }

            DrawToolbar();
            DrawSurfaceToolbar();
            ClampPanelWidths();

            List<PungentNote> visibleNotes = _visibleNotesCache;
            float centerWidth = GetWorkspaceWidth();

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (_browserCollapsed)
                {
                    DrawBrowserRail(visibleNotes, GUILayout.Width(CollapsedRailWidth), GUILayout.ExpandHeight(true));
                }
                else
                {
                    DrawBrowserPanel(visibleNotes, GUILayout.Width(_leftWidth), GUILayout.ExpandHeight(true));
                    UtilityWindowTheme.HorizontalResizeHandle(
                        ref _leftWidth,
                        MinLeftWidth,
                        MaxLeftWidth(),
                        QueuePrefsSave);
                }

                visibleNotes = _visibleNotesCache;
                DrawWritingPanel(
                    visibleNotes,
                    GUILayout.Width(centerWidth),
                    GUILayout.ExpandHeight(true));

                if (_propertiesCollapsed)
                {
                    DrawPropertiesRail(GUILayout.Width(CollapsedRailWidth), GUILayout.ExpandHeight(true));
                }
                else
                {
                    UtilityWindowTheme.HorizontalResizeHandle(
                        ref _rightWidth,
                        MinRightWidth,
                        MaxRightWidth(),
                        QueuePrefsSave,
                        "Drag to resize metadata tray",
                        true);

                    DrawMetadataTray(GUILayout.Width(_rightWidth), GUILayout.ExpandHeight(true));
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // RDE/STICKY-NOTES MIGRATION NOTE: This window is now the lightweight sticky-note surface. Keep broad roadmap/import tools behind Advanced so Rich Documents owns notebook-style authoring.
                    if (UtilityWindowTheme.TintedButton("New Sticky", UtilityWindowTheme.Green, GUILayout.Width(92f)))
                        SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));

                    if (GUILayout.Button(new GUIContent("Templates", "Create a sticky note from a lightweight template."), GUILayout.Width(88f)))
                        ShowCreateTemplateMenu();

                    if (GUILayout.Button(new GUIContent(_browserCollapsed ? "Show List" : "Hide List", "Collapse or expand the sticky-note list."), GUILayout.Width(82f)))
                    {
                        _browserCollapsed = !_browserCollapsed;
                        SavePrefs();
                    }

                    if (GUILayout.Button(new GUIContent(_propertiesCollapsed ? "Show Properties" : "Hide Properties", "Collapse or expand the note properties sidebar."), GUILayout.Width(118f)))
                    {
                        _propertiesCollapsed = !_propertiesCollapsed;
                        SavePrefs();
                    }

                    EditorGUI.BeginChangeCheck();
                    _filters.sortMode = (PungentNotesSortMode)EditorGUILayout.EnumPopup(_filters.sortMode, GUILayout.Width(112f));
                    _filters.showArchived = GUILayout.Toggle(_filters.showArchived, "Archived", EditorStyles.toolbarButton, GUILayout.Width(78f));
                    _filters.showDeveloperNotes = GUILayout.Toggle(_filters.showDeveloperNotes, "Developer", EditorStyles.toolbarButton, GUILayout.Width(86f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SavePrefs();
                        RefreshGuiCaches(true);
                    }

                    if (GUILayout.Button(new GUIContent("Advanced", "Roadmap/import/diagnostic tools preserved for compatibility."), EditorStyles.toolbarDropDown, GUILayout.Width(84f)))
                        ShowAdvancedStickyNotesMenu();

                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("tooltip-notes", "overview", "overview", "Open help for Sticky Notes.", "Notes header");
                }
            }
        }

        private void DrawSlimBrowserToolbar(List<AuthoringBrowserItem> visibleItems)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                ToolbarOverflow overflow = ToolbarOverflow.None;
                float toolbarWidth = Mathf.Max(240f, Mathf.Min(position.width, EditorGUIUtility.currentViewWidth > 1f ? EditorGUIUtility.currentViewWidth : position.width));
                float available = Mathf.Max(0f, toolbarWidth - SlimToolbarNewWidth - SlimToolbarMoreWidth - SlimToolbarHelpWidth - SlimToolbarMinSearchWidth - 38f);
                bool showNewForSelection = ReserveToolbarSlot(ref available, 116f);
                bool showTypes = ReserveToolbarSlot(ref available, 58f);
                bool showFilters = ReserveToolbarSlot(ref available, 62f);
                bool showSort = ReserveToolbarSlot(ref available, 54f);
                bool showView = ReserveToolbarSlot(ref available, 92f);
                bool showTemplates = ReserveToolbarSlot(ref available, 76f);
                bool showSettings = ReserveToolbarSlot(ref available, 70f);
                bool showSurfaces = ReserveToolbarSlot(ref available, 70f);
                bool showSelection = ReserveToolbarSlot(ref available, _selectedAuthoringItemKeys.Count > 1 ? 168f : 144f);
                bool showCount = ReserveToolbarSlot(ref available, 58f);

                if (!showNewForSelection) overflow |= ToolbarOverflow.NewForSelection;
                if (!showTemplates) overflow |= ToolbarOverflow.Templates;
                if (!showView) overflow |= ToolbarOverflow.View;
                if (!showTypes) overflow |= ToolbarOverflow.Types;
                if (!showFilters) overflow |= ToolbarOverflow.Filters;
                if (!showSort) overflow |= ToolbarOverflow.Sort;
                if (!showSettings) overflow |= ToolbarOverflow.Settings;
                if (!showSurfaces) overflow |= ToolbarOverflow.Surfaces;
                if (!showSelection) overflow |= ToolbarOverflow.Selection;
                if (!showCount) overflow |= ToolbarOverflow.Count;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("New", UtilityWindowTheme.Green, GUILayout.Width(SlimToolbarNewWidth)))
                        SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));

                    DrawSlimSearchField();

                    if (showNewForSelection)
                    {
                        using (new EditorGUI.DisabledScope(Selection.activeObject == null))
                        {
                            if (GUILayout.Button(new GUIContent("New for Selection", "Create a sticky note linked to the selected object."), EditorStyles.toolbarButton, GUILayout.Width(116f)))
                                CreateStickyForCurrentSelection();
                        }
                    }

                    if (showTypes && GUILayout.Button(new GUIContent("Types", "Show or hide authoring interfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
                        ShowAuthoringTypeMenu();
                    if (showFilters && GUILayout.Button(new GUIContent("Filters", "Open compact note filters."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                        ShowFiltersMenu();
                    if (showSort && GUILayout.Button(new GUIContent("Sort", "Choose sort order."), EditorStyles.toolbarDropDown, GUILayout.Width(54f)))
                        ShowSortMenu();
                    if (showView && GUILayout.Button(new GUIContent(ViewLabels[(int)_filters.view], "Choose a saved note view."), EditorStyles.toolbarDropDown, GUILayout.Width(92f)))
                        ShowSavedViewMenu();
                    if (showTemplates && GUILayout.Button(new GUIContent("Templates", "Create a sticky note from a lightweight template."), EditorStyles.toolbarButton, GUILayout.Width(76f)))
                        ShowCreateTemplateMenu();
                    if (showSettings && GUILayout.Button(new GUIContent("Settings", "Choose optional metadata shown in authoring rows."), EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                        ShowAuthoringRowSettingsMenu();
                    if (showSurfaces && GUILayout.Button(new GUIContent("Surfaces", "Configure inspector, context menu, and scene note surfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                        ShowSurfacesMenu();

                    if (showSelection)
                        DrawToolbarAuthoringSelectionActions(visibleItems, position.width < 920f, position.width < 620f);

                    if (showCount)
                        UtilityWindowTheme.CountPill((visibleItems == null ? 0 : visibleItems.Count) + "/" + _allAuthoringItemsCache.Count, UtilityWindowTheme.Teal, 58f);

                    if (GUILayout.Button(new GUIContent("More", "Hidden toolbar controls, compatibility tools, and advanced Sticky Notes actions."), EditorStyles.toolbarDropDown, GUILayout.Width(SlimToolbarMoreWidth)))
                        ShowMoreMenu(overflow);

                    PungentUtilityHelpButton.Draw("tooltip-notes", "overview", "overview", "Open help for Sticky Notes.", "Notes header");
                }
                DrawActiveFilterChips(visibleItems);
            }
        }

        private static bool ReserveToolbarSlot(ref float available, float width)
        {
            float claim = width + 4f;
            if (available < claim)
                return false;

            available -= claim;
            return true;
        }

        private void DrawCreateToolbarGroup()
        {
            if (UtilityWindowTheme.TintedButton("New", UtilityWindowTheme.Green, GUILayout.Width(44f)))
                SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));

            using (new EditorGUI.DisabledScope(Selection.activeObject == null))
            {
                if (GUILayout.Button(new GUIContent("New for Selection", "Create a sticky note linked to the selected object."), GUILayout.Width(116f)))
                    CreateStickyForCurrentSelection();
            }

            if (GUILayout.Button(new GUIContent("Templates", "Create a sticky note from a lightweight template."), GUILayout.Width(76f)))
                ShowCreateTemplateMenu();
        }

        private void ShowCreateActionsMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Sticky"), false, () => SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General)));
            if (Selection.activeObject == null)
                menu.AddDisabledItem(new GUIContent("New for Selection"));
            else
                menu.AddItem(new GUIContent("New for Selection"), false, CreateStickyForCurrentSelection);
            menu.AddItem(new GUIContent("Templates"), false, ShowCreateTemplateMenu);
            menu.ShowAsContext();
        }

        private void CreateStickyForCurrentSelection()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
                return;

            PungentNote note = PungentNoteStorage.CreateNoteForTarget(selected, null, selected.name);
            note.kind = PungentNoteKind.ProjectNote;
            SelectNote(note);
        }

        private void DrawSlimViewActions(List<AuthoringBrowserItem> visibleItems, bool compact, bool tiny)
        {
            if (GUILayout.Button(new GUIContent(tiny ? "View" : ViewLabels[(int)_filters.view], "Choose a saved note view."), EditorStyles.toolbarDropDown, GUILayout.Width(tiny ? 52f : 112f)))
                ShowSavedViewMenu();
            if (GUILayout.Button(new GUIContent("Types", "Show or hide authoring interfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
                ShowAuthoringTypeMenu();
            if (GUILayout.Button(new GUIContent("Filters", "Open compact note filters."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                ShowFiltersMenu();

            if (tiny)
            {
                if (GUILayout.Button(new GUIContent("Sort", "Choose sort order."), EditorStyles.toolbarDropDown, GUILayout.Width(52f)))
                    ShowSortMenu();
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _filters.sortMode = (PungentNotesSortMode)EditorGUILayout.EnumPopup(_filters.sortMode, GUILayout.Width(compact ? 86f : 92f));
                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                    RefreshGuiCaches(true);
                }
            }

            if (!tiny && GUILayout.Button(new GUIContent("Surfaces", "Configure inspector, context menu, and scene note surfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                ShowSurfacesMenu();
            if (GUILayout.Button(new GUIContent(tiny ? "Set" : "Settings", "Choose optional metadata shown in authoring rows."), EditorStyles.toolbarDropDown, GUILayout.Width(tiny ? 44f : 70f)))
                ShowAuthoringRowSettingsMenu();
            if (GUILayout.Button(new GUIContent("More", "Compatibility and advanced Sticky Notes tools."), EditorStyles.toolbarDropDown, GUILayout.Width(52f)))
                ShowMoreMenu();

            GUILayout.FlexibleSpace();
            DrawToolbarAuthoringSelectionActions(visibleItems, compact, tiny);
            if (!tiny)
                UtilityWindowTheme.CountPill((visibleItems == null ? 0 : visibleItems.Count) + "/" + _allAuthoringItemsCache.Count, UtilityWindowTheme.Teal, 58f);
            PungentUtilityHelpButton.Draw("tooltip-notes", "overview", "overview", "Open help for Sticky Notes.", "Notes header");
        }

        private void ShowSortMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentNotesSortMode mode in Enum.GetValues(typeof(PungentNotesSortMode)))
            {
                PungentNotesSortMode captured = mode;
                menu.AddItem(new GUIContent(captured.ToString()), _filters.sortMode == captured, () =>
                {
                    _filters.sortMode = captured;
                    SavePrefs();
                    RefreshGuiCaches(true);
                });
            }
            menu.ShowAsContext();
        }

        private void DrawSlimSearchField()
        {
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName(SlimSearchControlName);
            string nextSearch = EditorGUILayout.TextField(_filters.search, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(78f), GUILayout.ExpandWidth(true));
            if (_focusSlimSearch && Event.current != null && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SlimSearchControlName);
                EditorGUI.FocusTextInControl(SlimSearchControlName);
                _focusSlimSearch = false;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _filters.search = nextSearch;
                QueuePrefsSave();
                RefreshGuiCaches(true);
            }
        }

        private void HandleSlimBrowserKeyboardShortcuts()
        {
            if (_legacyWorkspaceMode || Event.current == null || Event.current.type != EventType.KeyDown)
                return;

            Event evt = Event.current;
            bool actionModifier = evt.control || evt.command;
            if (actionModifier && evt.keyCode == KeyCode.F)
            {
                FocusSlimSearch();
                evt.Use();
                return;
            }

            if (actionModifier && evt.keyCode == KeyCode.K)
            {
                PungentStickyNoteMiniFinderOverlayWindow.ShowOverlay(new Rect(position.xMax, position.y, 0f, 0f));
                evt.Use();
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if (!actionModifier && evt.keyCode == KeyCode.Slash)
            {
                FocusSlimSearch();
                evt.Use();
                return;
            }

            if (actionModifier && evt.keyCode == KeyCode.A)
            {
                SelectAllVisibleAuthoringItems();
                evt.Use();
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    SelectRelativeAuthoringItem(1);
                    evt.Use();
                    break;
                case KeyCode.UpArrow:
                    SelectRelativeAuthoringItem(-1);
                    evt.Use();
                    break;
                case KeyCode.Home:
                    SelectVisibleAuthoringItemAt(0);
                    evt.Use();
                    break;
                case KeyCode.End:
                    SelectVisibleAuthoringItemAt(_visibleAuthoringItemsCache.Count - 1);
                    evt.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenSelectedAuthoringItemFromKeyboard(actionModifier);
                    evt.Use();
                    break;
                case KeyCode.Escape:
                    ClearSearchOrSelectionFromKeyboard();
                    evt.Use();
                    break;
            }
        }

        private void FocusSlimSearch()
        {
            _focusSlimSearch = true;
            Repaint();
        }

        private void SelectRelativeAuthoringItem(int delta)
        {
            if (_visibleAuthoringItemsCache.Count == 0)
                return;

            int current = FindVisibleAuthoringIndex(_selectedAuthoringItemKey);
            int next = current < 0 ? (delta >= 0 ? 0 : _visibleAuthoringItemsCache.Count - 1) : Mathf.Clamp(current + delta, 0, _visibleAuthoringItemsCache.Count - 1);
            SelectVisibleAuthoringItemAt(next);
        }

        private void SelectVisibleAuthoringItemAt(int index)
        {
            if (_visibleAuthoringItemsCache.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, _visibleAuthoringItemsCache.Count - 1);
            AuthoringBrowserItem item = _visibleAuthoringItemsCache[index];
            if (item == null || string.IsNullOrWhiteSpace(item.key))
                return;

            CommitDraftIfDirty("Saved draft before keyboard selection.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before keyboard selection.");
            _selectedAuthoringItemKeys.Clear();
            _selectedAuthoringItemKeys.Add(item.key);
            _selectedAuthoringItemKey = item.key;
            _lastClickedVisibleIndex = index;
            _listScroll.y = Mathf.Max(0f, index * AuthoringRowHeight - AuthoringRowOverscan);
            SyncNoteSelectionFromAuthoringSelection();
            SavePrefs();
            Repaint();
        }

        private int FindVisibleAuthoringIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return -1;

            for (int i = 0; i < _visibleAuthoringItemsCache.Count; i++)
            {
                AuthoringBrowserItem item = _visibleAuthoringItemsCache[i];
                if (item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void OpenSelectedAuthoringItemFromKeyboard(bool edit)
        {
            if (_selectedAuthoringItemKeys.Count != 1)
                return;

            AuthoringBrowserItem item = SelectedAuthoringItems().FirstOrDefault();
            if (item == null)
                return;

            if (item.IsStickyNote)
            {
                if (edit)
                    EditNoteFromBrowser(item.note, Rect.zero);
                else if (PungentSupportRequestBridge.IsSupportRequest(item.note) && !PungentSupportRequestBridge.IsSent(item.note))
                    EditNoteFromBrowser(item.note, Rect.zero);
                else
                    PreviewNoteFromBrowser(item.note, Rect.zero);
                return;
            }

            if (edit)
            {
                if (CanEditAuthoringItem(item))
                    EditAuthoringItem(item);
                else
                    OpenAuthoringItem(item);
            }
            else
            {
                OpenAuthoringPreview(item, Rect.zero);
            }
        }

        private void ClearSearchOrSelectionFromKeyboard()
        {
            if (!string.IsNullOrWhiteSpace(_filters.search))
            {
                _filters.search = string.Empty;
                QueuePrefsSave();
                RefreshGuiCaches(true);
                _status = "Cleared search.";
                Repaint();
                return;
            }

            if (_selectedAuthoringItemKeys.Count > 0)
                ClearAuthoringSelection();
        }

        private void DrawActiveFilterChips(List<AuthoringBrowserItem> visibleItems)
        {
            List<FilterChipSpec> chips = CollectActiveFilterChips();
            if (chips.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.04f, 4, 1)))
            {
                int drawn = 0;
                int maxRows = 2;
                float rowWidth = Mathf.Max(180f, position.width - 46f);
                float reservedWidth = 94f;
                for (int row = 0; row < maxRows && drawn < chips.Count; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        float used = 0f;
                        float available = rowWidth - reservedWidth;
                        while (drawn < chips.Count)
                        {
                            FilterChipSpec chip = chips[drawn];
                            bool mustFitOne = used <= 0.1f;
                            if (!mustFitOne && used + chip.width > available)
                                break;

                            DrawFilterChipButton(chip);
                            used += chip.width + 4f;
                            drawn++;
                        }

                        bool finalRow = row == maxRows - 1 || drawn >= chips.Count;
                        if (finalRow && drawn < chips.Count)
                        {
                            int hiddenCount = chips.Count - drawn;
                            if (GUILayout.Button(new GUIContent("+" + hiddenCount + " filters", "Show hidden filter chips."), EditorStyles.miniButton, GUILayout.Width(82f)))
                                ShowHiddenFilterChipMenu(chips.Skip(drawn).ToList());
                            drawn = chips.Count;
                        }

                        GUILayout.FlexibleSpace();
                        if (finalRow)
                        {
                            UtilityWindowTheme.CountPill((visibleItems == null ? 0 : visibleItems.Count).ToString(), UtilityWindowTheme.Cyan, 38f);
                            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                                ClearFilters();
                        }
                    }
                }
            }
        }

        private List<FilterChipSpec> CollectActiveFilterChips()
        {
            List<FilterChipSpec> chips = new List<FilterChipSpec>();
            AddFilterChip(chips, "View: " + ViewLabels[(int)_filters.view], _filters.view != PungentNotesSavedView.AllNotes, () => _filters.view = PungentNotesSavedView.AllNotes);
            AddFilterChip(chips, "Search: " + _filters.search, !string.IsNullOrWhiteSpace(_filters.search), () => _filters.search = string.Empty);
            AddFilterChip(chips, "Status: " + _filters.status, _filters.status.HasValue, () => _filters.status = null);
            AddFilterChip(chips, "Priority: " + _filters.priority, _filters.priority.HasValue, () => _filters.priority = null);
            AddFilterChip(chips, "Kind: " + _filters.kind, _filters.kind.HasValue, () => _filters.kind = null);
            AddFilterChip(chips, "Utility: " + PungentNoteGUI.UtilityDisplayName(_filters.linkedUtilityId), !string.IsNullOrWhiteSpace(_filters.linkedUtilityId), () => _filters.linkedUtilityId = string.Empty);
            AddFilterChip(chips, "Tag: #" + _filters.tag, !string.IsNullOrWhiteSpace(_filters.tag), () => _filters.tag = string.Empty);
            AddFilterChip(chips, "Source: " + _filters.importSourceId, !string.IsNullOrWhiteSpace(_filters.importSourceId), () => _filters.importSourceId = string.Empty);
            AddFilterChip(chips, "Archived shown", _filters.showArchived, () => _filters.showArchived = false);
            AddFilterChip(chips, "Developer shown", _filters.showDeveloperNotes, () => _filters.showDeveloperNotes = false);
            AddFilterChip(chips, "Sticky hidden", !_showStickyNotes, () => _showStickyNotes = true);
            AddFilterChip(chips, "Docs hidden", !_showRichDocuments, () => _showRichDocuments = true);
            AddFilterChip(chips, "Sheets hidden", !_showDataSheets, () => _showDataSheets = true);
            AddFilterChip(chips, "Graphs hidden", !_showNodeGraphs, () => _showNodeGraphs = true);
            AddFilterChip(chips, "Checklists hidden", !_showChecklists, () => _showChecklists = true);
            return chips;
        }

        private static void AddFilterChip(List<FilterChipSpec> chips, string label, bool active, Action clear)
        {
            if (!active)
                return;

            chips.Add(new FilterChipSpec
            {
                label = label,
                clear = clear,
                width = Mathf.Clamp(42f + (label ?? string.Empty).Length * 7f, 82f, 190f)
            });
        }

        private void DrawFilterChipButton(FilterChipSpec chip)
        {
            if (chip == null)
                return;

            if (GUILayout.Button(new GUIContent(chip.label + "  x", "Clear " + chip.label), EditorStyles.miniButton, GUILayout.Width(chip.width)))
                ClearSingleFilter(chip);
        }

        private void ShowHiddenFilterChipMenu(List<FilterChipSpec> hiddenChips)
        {
            GenericMenu menu = new GenericMenu();
            foreach (FilterChipSpec chip in hiddenChips.Where(chip => chip != null))
            {
                FilterChipSpec captured = chip;
                menu.AddItem(new GUIContent(captured.label), false, () => ClearSingleFilter(captured));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear All"), false, ClearFilters);
            menu.ShowAsContext();
        }

        private void ClearSingleFilter(FilterChipSpec chip)
        {
            chip?.clear?.Invoke();
            SavePrefs();
            RefreshGuiCaches(true);
        }

        private bool HasActiveFilters()
        {
            return _filters.view != PungentNotesSavedView.AllNotes ||
                   !string.IsNullOrWhiteSpace(_filters.search) ||
                   _filters.status.HasValue ||
                   _filters.priority.HasValue ||
                   _filters.kind.HasValue ||
                   !string.IsNullOrWhiteSpace(_filters.linkedUtilityId) ||
                   !string.IsNullOrWhiteSpace(_filters.tag) ||
                   !string.IsNullOrWhiteSpace(_filters.importSourceId);
        }

        private bool HasVisibleFilterChips()
        {
            return HasActiveFilters() ||
                   _filters.showArchived ||
                   _filters.showDeveloperNotes ||
                   !_showStickyNotes ||
                   !_showRichDocuments ||
                   !_showDataSheets ||
                   !_showNodeGraphs ||
                   !_showChecklists;
        }

        private void DrawToolbarAuthoringSelectionActions(List<AuthoringBrowserItem> visibleItems, bool compact, bool tiny)
        {
            int selectedCount = _selectedAuthoringItemKeys.Count;
            if (selectedCount > 1)
            {
                UtilityWindowTheme.CountPill(selectedCount + " selected", UtilityWindowTheme.Cyan, 88f);
                if (compact || tiny)
                {
                    if (GUILayout.Button(new GUIContent("Selection", "Actions for selected authoring items."), EditorStyles.toolbarDropDown, GUILayout.Width(tiny ? 76f : 86f)))
                        ShowAuthoringSelectionActionsMenu();
                    if (GUILayout.Button("Deselect", EditorStyles.miniButton, GUILayout.Width(66f)))
                        ClearAuthoringSelection();
                    return;
                }

                using (new EditorGUI.DisabledScope(SelectedStickyNotesFromAuthoring().Count < 2))
                {
                    if (GUILayout.Button(new GUIContent("Open Stack", "Open selected sticky notes in the floating overlay stack."), EditorStyles.miniButton, GUILayout.Width(82f)))
                        OpenSelectedAuthoringStack();
                }
                if (GUILayout.Button(new GUIContent("Copy IDs", "Copy selected authoring item IDs."), EditorStyles.miniButton, GUILayout.Width(68f)))
                    CopySelectedAuthoringIds();
                using (new EditorGUI.DisabledScope(SelectedStickyNotesFromAuthoring().Count == 0))
                {
                    if (GUILayout.Button("Duplicate Notes", EditorStyles.miniButton, GUILayout.Width(104f)))
                        DuplicateSelectedStickyNotesFromAuthoring();
                    if (GUILayout.Button("Archive Notes", EditorStyles.miniButton, GUILayout.Width(92f)))
                        ArchiveSelectedStickyNotesFromAuthoring(true);
                    if (GUILayout.Button("Delete Notes", EditorStyles.miniButton, GUILayout.Width(84f)))
                    {
                        DeleteSelectedStickyNotesFromAuthoring();
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("Deselect", EditorStyles.miniButton, GUILayout.Width(70f)))
                    ClearAuthoringSelection();
                return;
            }

            if (tiny)
            {
                if (GUILayout.Button(new GUIContent("Select", "Selection controls."), EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
                    ShowAuthoringSelectMenu(visibleItems);
                return;
            }

            using (new EditorGUI.DisabledScope(visibleItems == null || visibleItems.Count == 0))
            {
                if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(70f)))
                    SelectAllVisibleAuthoringItems();
            }
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (GUILayout.Button("Deselect", EditorStyles.miniButton, GUILayout.Width(70f)))
                    ClearAuthoringSelection();
            }
        }

        private void ShowAuthoringSelectionActionsMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy IDs"), false, CopySelectedAuthoringIds);
            int stickyCount = SelectedStickyNotesFromAuthoring().Count;
            if (stickyCount < 2)
                menu.AddDisabledItem(new GUIContent("Open Stack"));
            else
                menu.AddItem(new GUIContent("Open Stack"), false, OpenSelectedAuthoringStack);

            if (stickyCount == 0)
            {
                menu.AddDisabledItem(new GUIContent("Duplicate Notes"));
                menu.AddDisabledItem(new GUIContent("Archive Notes"));
                menu.AddDisabledItem(new GUIContent("Delete Notes"));
            }
            else
            {
                menu.AddItem(new GUIContent("Duplicate Notes"), false, DuplicateSelectedStickyNotesFromAuthoring);
                menu.AddItem(new GUIContent("Archive Notes"), false, () => ArchiveSelectedStickyNotesFromAuthoring(true));
                menu.AddItem(new GUIContent("Delete Notes"), false, DeleteSelectedStickyNotesFromAuthoring);
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Deselect"), false, ClearAuthoringSelection);
            menu.ShowAsContext();
        }

        private void ShowAuthoringSelectMenu(List<AuthoringBrowserItem> visibleItems)
        {
            GenericMenu menu = new GenericMenu();
            if (visibleItems == null || visibleItems.Count == 0)
                menu.AddDisabledItem(new GUIContent("Select All"));
            else
                menu.AddItem(new GUIContent("Select All"), false, SelectAllVisibleAuthoringItems);

            if (_selectedAuthoringItemKeys.Count == 0)
                menu.AddDisabledItem(new GUIContent("Deselect"));
            else
                menu.AddItem(new GUIContent("Deselect"), false, ClearAuthoringSelection);
            menu.ShowAsContext();
        }

        private void ShowAuthoringTypeMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("All Interfaces"), AllAuthoringTypesShown(), () => SetAllAuthoringTypeFilters(true));
            menu.AddItem(new GUIContent("Only Sticky Notes"), false, () => SetOnlyAuthoringType(AuthoringBrowserInterface.StickyNotes));
            AddProviderAwareOnlyTypeItem(menu, "Only Rich Documents", AuthoringBrowserInterface.RichDocuments, PungentAuthoringItemKind.RichDocument);
            AddProviderAwareOnlyTypeItem(menu, "Only Data Sheets", AuthoringBrowserInterface.DataSheets, PungentAuthoringItemKind.DataSheet);
            AddProviderAwareOnlyTypeItem(menu, "Only Node Graphs", AuthoringBrowserInterface.NodeGraphs, PungentAuthoringItemKind.Board);
            AddProviderAwareOnlyTypeItem(menu, "Only Checklists", AuthoringBrowserInterface.Checklists, PungentAuthoringItemKind.Checklist);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Sticky Notes"), _showStickyNotes, () => ToggleAuthoringType(AuthoringBrowserInterface.StickyNotes));
            AddProviderAwareTypeItem(menu, "Rich Documents", AuthoringBrowserInterface.RichDocuments, PungentAuthoringItemKind.RichDocument, _showRichDocuments);
            AddProviderAwareTypeItem(menu, "Data Sheets", AuthoringBrowserInterface.DataSheets, PungentAuthoringItemKind.DataSheet, _showDataSheets);
            AddProviderAwareTypeItem(menu, "Node Graphs", AuthoringBrowserInterface.NodeGraphs, PungentAuthoringItemKind.Board, _showNodeGraphs);
            AddProviderAwareTypeItem(menu, "Checklists", AuthoringBrowserInterface.Checklists, PungentAuthoringItemKind.Checklist, _showChecklists);
            menu.ShowAsContext();
        }

        private void AddProviderAwareOnlyTypeItem(GenericMenu menu, string label, AuthoringBrowserInterface type, PungentAuthoringItemKind providerKind)
        {
            if (PungentAuthoringProviderRegistry.GetProvidersForKind(providerKind).Count == 0)
                menu.AddDisabledItem(new GUIContent(label + " (" + PungentAuthoringItemKinds.GetMissingProviderMessage(providerKind) + ")"));
            else
                menu.AddItem(new GUIContent(label), false, () => SetOnlyAuthoringType(type));
        }

        private void AddProviderAwareTypeItem(GenericMenu menu, string label, AuthoringBrowserInterface type, PungentAuthoringItemKind providerKind, bool enabled)
        {
            if (PungentAuthoringProviderRegistry.GetProvidersForKind(providerKind).Count == 0)
                menu.AddDisabledItem(new GUIContent(label + " (" + PungentAuthoringItemKinds.GetMissingProviderMessage(providerKind) + ")"));
            else
                menu.AddItem(new GUIContent(label), enabled, () => ToggleAuthoringType(type));
        }

        private void ShowAuthoringRowSettingsMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Date/Updated Date"), _showAuthoringUpdatedDate, () => ToggleRowMetadata(ref _showAuthoringUpdatedDate));
            menu.AddItem(new GUIContent("Date/Created Date"), _showAuthoringCreatedDate, () => ToggleRowMetadata(ref _showAuthoringCreatedDate));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Metadata/Kind"), _showAuthoringKind, () => ToggleRowMetadata(ref _showAuthoringKind));
            menu.AddItem(new GUIContent("Metadata/Status"), _showAuthoringStatus, () => ToggleRowMetadata(ref _showAuthoringStatus));
            menu.AddItem(new GUIContent("Metadata/Priority"), _showAuthoringPriority, () => ToggleRowMetadata(ref _showAuthoringPriority));
            menu.AddItem(new GUIContent("Metadata/Tags"), _showAuthoringTags, () => ToggleRowMetadata(ref _showAuthoringTags));
            menu.AddItem(new GUIContent("Metadata/Target Count"), _showAuthoringTargetCount, () => ToggleRowMetadata(ref _showAuthoringTargetCount));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset To Minimal"), false, ResetAuthoringRowMetadata);
            menu.ShowAsContext();
        }

        private void ToggleRowMetadata(ref bool value)
        {
            value = !value;
            SavePrefs();
            RefreshAuthoringVisibleItems(true);
        }

        private void ShowSavedViewMenu()
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < ViewLabels.Length; i++)
            {
                PungentNotesSavedView view = (PungentNotesSavedView)i;
                string label = ViewLabels[i] + " (" + GetSavedViewCount(view) + ")";
                menu.AddItem(new GUIContent(label), _filters.view == view, () =>
                {
                    _filters.view = view;
                    SavePrefs();
                    RefreshGuiCaches(true);
                });
            }
            menu.ShowAsContext();
        }

        private void ShowFiltersMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Archived/Show Archived"), _filters.showArchived, () => ToggleArchivedFilter());
            menu.AddItem(new GUIContent("Developer/Show Developer Notes"), _filters.showDeveloperNotes, () => ToggleDeveloperFilter());
            menu.AddSeparator(string.Empty);
            AddNullableEnumFilter(menu, "Status", _filters.status, value => _filters.status = value);
            menu.AddSeparator(string.Empty);
            AddNullableEnumFilter(menu, "Priority", _filters.priority, value => _filters.priority = value);
            menu.AddSeparator(string.Empty);
            AddNullableEnumFilter(menu, "Kind", _filters.kind, value => _filters.kind = value);
            menu.AddSeparator(string.Empty);
            foreach (string tag in PungentNoteStorage.Database.notes
                         .Where(note => note != null && note.tags != null)
                         .SelectMany(note => note.tags)
                         .Where(tag => !string.IsNullOrWhiteSpace(tag))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(tag => tag)
                         .Take(40))
            {
                string captured = tag;
                menu.AddItem(new GUIContent("Tags/#" + captured), string.Equals(_filters.tag, captured, StringComparison.OrdinalIgnoreCase), () =>
                {
                    _filters.tag = captured;
                    SavePrefs();
                    RefreshGuiCaches(true);
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear Filters"), false, ClearFilters);
            menu.ShowAsContext();
        }

        private void AddNullableEnumFilter<T>(GenericMenu menu, string group, T? current, Action<T?> setValue) where T : struct
        {
            menu.AddItem(new GUIContent(group + "/Any"), !current.HasValue, () =>
            {
                setValue(null);
                SavePrefs();
                RefreshGuiCaches(true);
            });

            foreach (T value in Enum.GetValues(typeof(T)))
            {
                T captured = value;
                menu.AddItem(new GUIContent(group + "/" + value), current.HasValue && EqualityComparer<T>.Default.Equals(current.Value, value), () =>
                {
                    setValue(captured);
                    SavePrefs();
                    RefreshGuiCaches(true);
                });
            }
        }

        private void ToggleArchivedFilter()
        {
            _filters.showArchived = !_filters.showArchived;
            SavePrefs();
            RefreshGuiCaches(true);
        }

        private void ToggleDeveloperFilter()
        {
            _filters.showDeveloperNotes = !_filters.showDeveloperNotes;
            SavePrefs();
            RefreshGuiCaches(true);
        }

        private void ShowSurfacesMenu()
        {
            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            GenericMenu menu = new GenericMenu();
            foreach (PungentNoteSurfaceMode mode in Enum.GetValues(typeof(PungentNoteSurfaceMode)))
            {
                PungentNoteSurfaceMode captured = mode;
                menu.AddItem(new GUIContent("Mode/" + mode), settings.surfaceMode == mode, () =>
                {
                    settings.surfaceMode = captured;
                    PungentNoteStorage.Save();
                    PungentNoteSceneOverlay.InvalidateCache();
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Inspector Notes"), settings.showInspectorNotes, () => ToggleSurfaceSetting(value => settings.showInspectorNotes = value, settings.showInspectorNotes));
            menu.AddItem(new GUIContent("Scene Badges"), settings.showSceneBadges, () => ToggleSurfaceSetting(value => settings.showSceneBadges = value, settings.showSceneBadges));
            menu.AddItem(new GUIContent("Context Menus"), PungentNoteDisplaySettingsService.ContextMenusEnabled, () =>
            {
                bool next = !PungentNoteDisplaySettingsService.ContextMenusEnabled;
                settings.showPropertyContextMenus = next;
                settings.showAssetContextMenus = next;
                settings.showGameObjectContextMenus = next;
                PungentNoteStorage.Save();
                PungentNoteSceneOverlay.InvalidateCache();
            });
            menu.AddItem(new GUIContent("Edit Mode"), settings.editMode, () => ToggleSurfaceSetting(value => settings.editMode = value, settings.editMode));
            menu.AddItem(new GUIContent("Hover/Inspector Hover"), settings.showInspectorHoverPreviews, () => ToggleSurfaceSetting(value => settings.showInspectorHoverPreviews = value, settings.showInspectorHoverPreviews));
            menu.AddItem(new GUIContent("Hover/List Hover"), settings.showBrowserHoverPreviews, () => ToggleSurfaceSetting(value => settings.showBrowserHoverPreviews = value, settings.showBrowserHoverPreviews));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Sticky Notes Overlay"), false, () => PungentStickyNoteMiniFinderOverlayWindow.ShowOverlay(new Rect()));
            menu.AddItem(new GUIContent("Refresh Scene Notes"), false, PungentNoteSceneOverlay.InvalidateCache);
            menu.ShowAsContext();
        }

        private void ToggleSurfaceSetting(Action<bool> setter, bool current)
        {
            setter(!current);
            PungentNoteStorage.Save();
            PungentNoteSceneOverlay.InvalidateCache();
        }

        private void ShowMoreMenu()
        {
            ShowMoreMenu(ToolbarOverflow.None);
        }

        private void ShowMoreMenu(ToolbarOverflow overflow)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Sticky Notes Overlay"), false, () => PungentStickyNoteMiniFinderOverlayWindow.ShowOverlay(new Rect()));
            menu.AddItem(new GUIContent("Legacy Advanced Workspace"), _legacyWorkspaceMode, () => _legacyWorkspaceMode = true);
            menu.AddSeparator(string.Empty);
            if (_visibleNotesCache.Count > 0)
                menu.AddItem(new GUIContent("Selection/Select All Visible"), false, SelectAllVisibleNotes);
            else
                menu.AddDisabledItem(new GUIContent("Selection/Select All Visible"));
            if (_selectedNoteIds.Count > 0)
                menu.AddItem(new GUIContent("Selection/Deselect"), false, ClearSelectedNotes);
            else
                menu.AddDisabledItem(new GUIContent("Selection/Deselect"));
            if (SelectedStickyNotesFromAuthoring().Count > 1)
                menu.AddItem(new GUIContent("Selection/Open Selected Stack"), false, OpenSelectedAuthoringStack);
            else
                menu.AddDisabledItem(new GUIContent("Selection/Open Selected Stack"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Future Utilities/New Future Utility Record"), false, () =>
            {
                _legacyWorkspaceMode = true;
                CreateFutureUtilityRecord();
            });
            menu.AddItem(new GUIContent("Backlog/Seed Curated Backlog"), false, () =>
            {
                _pendingCsvPath = string.Empty;
                LoadSeedPreview(PungentNoteBacklogSeeder.CuratedSeeds(), "Curated backlog preview loaded.");
            });
            menu.AddItem(new GUIContent("Backlog/Import Feature Inventory CSV"), false, ImportCsvPreview);
            if (_seedPreview.Count > 0)
                menu.AddItem(new GUIContent("Backlog/Apply Seed Preview"), false, ApplySeedPreview);
            else
                menu.AddDisabledItem(new GUIContent("Backlog/Apply Seed Preview"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Grouping/None"), _groupMode == PungentNotesGroupMode.None, () => SetGroupMode(PungentNotesGroupMode.None));
            menu.AddItem(new GUIContent("Grouping/Kind"), _groupMode == PungentNotesGroupMode.Kind, () => SetGroupMode(PungentNotesGroupMode.Kind));
            menu.AddItem(new GUIContent("Grouping/Status"), _groupMode == PungentNotesGroupMode.Status, () => SetGroupMode(PungentNotesGroupMode.Status));
            menu.AddItem(new GUIContent("Grouping/Priority"), _groupMode == PungentNotesGroupMode.Priority, () => SetGroupMode(PungentNotesGroupMode.Priority));
            menu.AddItem(new GUIContent("Grouping/Utility"), _groupMode == PungentNotesGroupMode.Utility, () => SetGroupMode(PungentNotesGroupMode.Utility));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Diagnostics/Reload Notes Database"), false, ReloadNotesDatabase);
            menu.AddItem(new GUIContent("Diagnostics/Open Design Audit"), false, PungentUtilityDesignAuditWindow.Open);
            AddToolbarOverflowMenuItems(menu, overflow);
            menu.ShowAsContext();
        }

        private void AddToolbarOverflowMenuItems(GenericMenu menu, ToolbarOverflow overflow)
        {
            if (overflow == ToolbarOverflow.None)
                return;

            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Toolbar"));
            if ((overflow & ToolbarOverflow.NewSticky) != 0)
                menu.AddItem(new GUIContent("New Sticky"), false, () => SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General)));
            if ((overflow & ToolbarOverflow.NewForSelection) != 0)
            {
                if (Selection.activeObject == null)
                    menu.AddDisabledItem(new GUIContent("New for Selection"));
                else
                    menu.AddItem(new GUIContent("New for Selection"), false, CreateStickyForCurrentSelection);
            }
            if ((overflow & ToolbarOverflow.Templates) != 0)
                menu.AddItem(new GUIContent("Templates"), false, ShowCreateTemplateMenu);
            if ((overflow & ToolbarOverflow.Types) != 0)
                menu.AddItem(new GUIContent("Types"), false, ShowAuthoringTypeMenu);
            if ((overflow & ToolbarOverflow.Filters) != 0)
                menu.AddItem(new GUIContent("Filters"), false, ShowFiltersMenu);
            if ((overflow & ToolbarOverflow.Sort) != 0)
                menu.AddItem(new GUIContent("Sort"), false, ShowSortMenu);
            if ((overflow & ToolbarOverflow.View) != 0)
                menu.AddItem(new GUIContent("Saved View"), false, ShowSavedViewMenu);
            if ((overflow & ToolbarOverflow.Settings) != 0)
                menu.AddItem(new GUIContent("Settings"), false, ShowAuthoringRowSettingsMenu);
            if ((overflow & ToolbarOverflow.Surfaces) != 0)
                menu.AddItem(new GUIContent("Surfaces"), false, ShowSurfacesMenu);
            if ((overflow & ToolbarOverflow.Selection) != 0)
            {
                if (_selectedAuthoringItemKeys.Count > 1)
                    menu.AddItem(new GUIContent("Selection Actions"), false, ShowAuthoringSelectionActionsMenu);
                else
                    menu.AddItem(new GUIContent("Select All Visible"), false, SelectAllVisibleAuthoringItems);
                if (_selectedAuthoringItemKeys.Count > 0)
                    menu.AddItem(new GUIContent("Deselect"), false, ClearAuthoringSelection);
            }
            if ((overflow & ToolbarOverflow.Count) != 0)
                menu.AddDisabledItem(new GUIContent("Visible Items: " + _visibleAuthoringItemsCache.Count + "/" + _allAuthoringItemsCache.Count));
        }

        private void ShowAdvancedStickyNotesMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Future Utility"), false, CreateFutureUtilityRecord);
            menu.AddItem(new GUIContent("Open Design Audit"), false, PungentUtilityDesignAuditWindow.Open);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Seed Curated Backlog"), false, () =>
            {
                _pendingCsvPath = string.Empty;
                LoadSeedPreview(PungentNoteBacklogSeeder.CuratedSeeds(), "Curated backlog preview loaded.");
            });
            menu.AddItem(new GUIContent("Import Feature Inventory CSV"), false, ImportCsvPreview);
            if (_seedPreview.Count > 0)
            {
                menu.AddItem(new GUIContent("Seed Preview/Apply Selected"), false, ApplySeedPreview);
                menu.AddItem(new GUIContent("Seed Preview/Clear"), false, () => _seedPreview.Clear());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Seed Preview/Apply Selected"));
                menu.AddDisabledItem(new GUIContent("Seed Preview/Clear"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Grouping/None"), _groupMode == PungentNotesGroupMode.None, () => SetGroupMode(PungentNotesGroupMode.None));
            menu.AddItem(new GUIContent("Grouping/Kind"), _groupMode == PungentNotesGroupMode.Kind, () => SetGroupMode(PungentNotesGroupMode.Kind));
            menu.AddItem(new GUIContent("Grouping/Status"), _groupMode == PungentNotesGroupMode.Status, () => SetGroupMode(PungentNotesGroupMode.Status));
            menu.AddItem(new GUIContent("Grouping/Priority"), _groupMode == PungentNotesGroupMode.Priority, () => SetGroupMode(PungentNotesGroupMode.Priority));
            menu.AddItem(new GUIContent("Grouping/Utility"), _groupMode == PungentNotesGroupMode.Utility, () => SetGroupMode(PungentNotesGroupMode.Utility));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reload Notes Database"), false, ReloadNotesDatabase);
            menu.ShowAsContext();
        }

        private void CreateFutureUtilityRecord()
        {
            CommitDraftIfDirty("Saved draft before editing future utility.");
            _editingFutureUtility = PungentNoteStorage.Database.CreateFutureUtility();
            _selectedNoteId = string.Empty;
            _selectedNoteIds.Clear();
            LoadDraftForSelectedNote();
            SavePrefs();
            RefreshGuiCaches(true);
        }

        private void SetGroupMode(PungentNotesGroupMode groupMode)
        {
            _groupMode = groupMode;
            SavePrefs();
            RefreshGuiCaches(true);
        }

        private void ReloadNotesDatabase()
        {
            CommitDraftIfDirty("Saved draft before reload.");
            PungentNoteStorage.EnsureLoaded();
            LoadDraftForSelectedNote();
            RefreshGuiCaches(true);
            _status = "Reloaded sticky notes database.";
        }

        private void DrawSurfaceToolbar()
        {
            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField("Show Notes", GUILayout.Width(72f));
                    settings.surfaceMode = (PungentNoteSurfaceMode)EditorGUILayout.EnumPopup(settings.surfaceMode, GUILayout.Width(150f));
                    settings.showInspectorNotes = GUILayout.Toggle(settings.showInspectorNotes, "Inspector", EditorStyles.toolbarButton, GUILayout.Width(76f));
                    settings.showSceneBadges = GUILayout.Toggle(settings.showSceneBadges, "Scene", EditorStyles.toolbarButton, GUILayout.Width(58f));
                    bool contexts = PungentNoteDisplaySettingsService.ContextMenusEnabled;
                    bool nextContexts = GUILayout.Toggle(contexts, "Context Menus", EditorStyles.toolbarButton, GUILayout.Width(112f));
                    if (nextContexts != contexts)
                    {
                        settings.showPropertyContextMenus = nextContexts;
                        settings.showAssetContextMenus = nextContexts;
                        settings.showGameObjectContextMenus = nextContexts;
                    }
                    settings.editMode = GUILayout.Toggle(settings.editMode, "Edit Notes", EditorStyles.toolbarButton, GUILayout.Width(82f));
                    settings.showDeveloperNotes = GUILayout.Toggle(settings.showDeveloperNotes, "Developer", EditorStyles.toolbarButton, GUILayout.Width(82f));
                    settings.showInspectorHoverPreviews = GUILayout.Toggle(settings.showInspectorHoverPreviews, "Inspector Hover", EditorStyles.toolbarButton, GUILayout.Width(112f));
                    settings.showBrowserHoverPreviews = GUILayout.Toggle(settings.showBrowserHoverPreviews, "List Hover", EditorStyles.toolbarButton, GUILayout.Width(88f));
                    if (GUILayout.Button("Refresh Scene Notes", EditorStyles.miniButton, GUILayout.Width(132f)))
                        PungentNoteSceneOverlay.InvalidateCache();
                    if (EditorGUI.EndChangeCheck())
                    {
                        PungentNoteStorage.Save();
                        PungentNoteSceneOverlay.InvalidateCache();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UnityEngine.Object selected = Selection.activeObject;
                    int count = GetSurfaceSelectionNoteCount(selected, settings);
                    EditorGUILayout.LabelField(selected != null ? selected.name : "No selection", UtilityWindowTheme.PathLabelStyle);
                    UtilityWindowTheme.CountPill(count.ToString(), UtilityWindowTheme.Cyan, 36f);
                    if (selected != null && GUILayout.Button("Add Note for Selection", EditorStyles.miniButton, GUILayout.Width(138f)))
                        SelectNote(PungentNoteContextMenus.AddNoteFor(selected, null, "Note: " + selected.name, PungentNoteKind.ProjectNote));
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawBrowserPanel(List<PungentNote> visibleNotes, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, margin: 2), options))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Sticky List", UtilityWindowTheme.Teal, visibleNotes.Count + " shown");
                    if (GUILayout.Button(new GUIContent("<", "Collapse sticky-note list."), EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        _browserCollapsed = true;
                        SavePrefs();
                    }
                }
                DrawBrowserSearch();

                _sidebarScroll = EditorGUILayout.BeginScrollView(
                    _sidebarScroll,
                    false,
                    true,
                    GUILayout.MinHeight(190f),
                    GUILayout.MaxHeight(Mathf.Clamp(position.height * 0.38f, 220f, 360f)));

                UtilityWindowTheme.SectionTitle("Saved Views", UtilityWindowTheme.Teal);

                for (int i = 0; i < ViewLabels.Length; i++)
                    DrawViewButton((PungentNotesSavedView)i, ViewLabels[i], GetSavedViewCount((PungentNotesSavedView)i));

                EditorGUILayout.Space(8f);
                UtilityWindowTheme.SectionTitle("Filters", UtilityWindowTheme.Blue);
                EditorGUI.BeginChangeCheck();
                DrawNullableEnum("Status", ref _filters.status);
                DrawNullableEnum("Priority", ref _filters.priority);
                DrawNullableEnum("Kind", ref _filters.kind);
                _filters.linkedUtilityId = DrawUtilityPopup("Linked Utility", _filters.linkedUtilityId, true);
                _filters.tag = EditorGUILayout.TextField("Tag", _filters.tag);
                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                    RefreshGuiCaches(true);
                }

                if (GUILayout.Button("Clear Filters", EditorStyles.miniButton))
                    ClearFilters();

                EditorGUILayout.Space(8f);
                DrawImportedSourcesSidebar();
                DrawTagsSidebar();
                DrawRecentNotes();
                EditorGUILayout.Space(8f);
                DrawFutureUtilityMiniList();
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle("Notes", UtilityWindowTheme.Blue, PungentNoteStorage.Database.notes.Count + " total");
                visibleNotes = _visibleNotesCache;
                DrawWorkspaceSelectionToolbar(visibleNotes);
                DrawNoteList(visibleNotes);
                PungentNoteHoverPreview.DrawPendingHoverOverlay(GUILayoutUtility.GetLastRect(), SelectNote);
                _lastHoverPreviewStatus = PungentNoteHoverPreview.LastCacheStatus;
                DrawDeveloperDiagnostics(visibleNotes);
            }
        }

        private void DrawBrowserRail(List<PungentNote> visibleNotes, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, margin: 2), options))
            {
                if (GUILayout.Button(new GUIContent(">", "Expand sticky-note list."), EditorStyles.miniButton, GUILayout.Width(28f)))
                {
                    _browserCollapsed = false;
                    SavePrefs();
                }
                GUILayout.Space(4f);
                UtilityWindowTheme.CountPill((visibleNotes == null ? 0 : visibleNotes.Count).ToString(), UtilityWindowTheme.Cyan, 30f);
                GUILayout.Space(4f);
                GUI.enabled = !string.IsNullOrWhiteSpace(_filters.search);
                if (GUILayout.Button(new GUIContent("S", "Search filter is active. Click to clear."), EditorStyles.miniButton, GUILayout.Width(28f)))
                {
                    _filters.search = string.Empty;
                    SavePrefs();
                    RefreshGuiCaches(true);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawBrowserSearch()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
                string nextSearch = EditorGUILayout.TextField(_filters.search, UtilityWindowTheme.ToolbarSearchStyle);
                if (!string.Equals(nextSearch, _filters.search, StringComparison.Ordinal))
                {
                    _filters.search = nextSearch;
                    QueuePrefsSave();
                    RefreshGuiCaches(true);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_filters.search)))
                {
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        _filters.search = string.Empty;
                        SavePrefs();
                        RefreshGuiCaches(true);
                    }
                }
            }
        }

        private void DrawNoteList(List<PungentNote> notes)
        {
            _listScroll = EditorGUILayout.BeginScrollView(
                _listScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            if (PungentNoteStorage.Database.notes.Count == 0)
                EditorGUILayout.HelpBox("No sticky notes yet. Create your first reminder, checklist, or context note.", MessageType.Info);
            else if (notes.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching notes. Adjust filters or clear search.", MessageType.Info);
                if (GUILayout.Button("Clear Filters", EditorStyles.miniButton))
                    ClearFilters();
            }

            if (_groupMode == PungentNotesGroupMode.None)
            {
                for (int i = 0; i < notes.Count; i++)
                    DrawNoteRow(notes[i], notes, i);
            }
            else
            {
                foreach (IGrouping<string, PungentNote> group in GroupNotes(notes))
                {
                    EditorGUILayout.Space(4f);
                    UtilityWindowTheme.SectionTitle(group.Key, UtilityWindowTheme.Blue, group.Count() + " notes");
                    foreach (PungentNote note in group)
                        DrawNoteRow(note, notes, notes.IndexOf(note));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAuthoringItemList(List<AuthoringBrowserItem> items)
        {
            _listScroll = EditorGUILayout.BeginScrollView(
                _listScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            if (_allAuthoringItemsCache.Count == 0)
            {
                EditorGUILayout.HelpBox("No authoring items are available from registered providers yet.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Authoring Items", EditorStyles.miniButton, GUILayout.Width(148f)))
                        RefreshAuthoringItemsFromProviders();
                    if (GUILayout.Button("New Sticky", EditorStyles.miniButton, GUILayout.Width(78f)))
                        SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));
                }
            }
            else if (items.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching authoring items. Adjust search, type filters, or note filters.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_filters.search)))
                    {
                        if (GUILayout.Button("Clear Search", EditorStyles.miniButton, GUILayout.Width(92f)))
                            ClearSearchOnly();
                    }
                    if (GUILayout.Button("Clear Filters", EditorStyles.miniButton, GUILayout.Width(86f)))
                        ClearFilters();
                    using (new EditorGUI.DisabledScope(AllAuthoringTypesShown()))
                    {
                        if (GUILayout.Button("Show All Types", EditorStyles.miniButton, GUILayout.Width(98f)))
                            SetAllAuthoringTypeFilters(true);
                    }
                    if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(62f)))
                        RefreshAuthoringItemsFromProviders();
                }
            }

            float scrollTop = _listScroll.y;
            float scrollBottom = scrollTop + Mathf.Max(120f, position.height - 138f);
            float contentY = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                AuthoringBrowserItem item = items[i];
                float rowHeight = AuthoringRowHeight + (IsAuthoringItemSelected(item) ? AuthoringActionRowHeight : 0f);
                bool visible = contentY + rowHeight >= scrollTop - AuthoringRowOverscan &&
                               contentY <= scrollBottom + AuthoringRowOverscan;
                if (visible)
                    DrawAuthoringItemRow(item, i);
                else
                    GUILayout.Space(rowHeight);
                contentY += rowHeight;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAuthoringItemRow(AuthoringBrowserItem item, int visibleIndex)
        {
            if (item == null || item.metadata == null)
                return;

            bool selected = IsAuthoringItemSelected(item);
            Rect rowRect = GUILayoutUtility.GetRect(0f, AuthoringRowHeight, GUILayout.ExpandWidth(true));
            Color tint = selected ? UtilityWindowTheme.Cyan : InterfaceTint(item.interfaceKind);
            if (Event.current.type == EventType.Repaint)
            {
                UtilityWindowTheme.PanelStyle(tint, selected ? 0.20f : 0.10f, selected ? 0.08f : 0.035f, 5, 1)
                    .Draw(rowRect, GUIContent.none, false, false, selected, false);
                if (selected)
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3f, rowRect.height), UtilityWindowTheme.Cyan);
            }

            Rect checkRect = new Rect(rowRect.x + 6f, rowRect.y + 4f, 18f, 18f);
            bool nextSelected = GUI.Toggle(checkRect, selected, GUIContent.none);
            if (nextSelected != selected)
                ToggleAuthoringItemSelection(item, visibleIndex);

            float rowWidth = rowRect.width;
            bool tiny = rowWidth < 430f;
            bool narrow = rowWidth < 560f;
            string meta = tiny ? string.Empty : narrow ? BuildAuthoringDateMeta(item) : BuildAuthoringRowMeta(item);
            GUIStyle metaStyle = UtilityWindowTheme.PathLabelStyle;
            float metaWidth = string.IsNullOrWhiteSpace(meta) ? 0f : Mathf.Min(Mathf.Min(260f, rowWidth * 0.34f), metaStyle.CalcSize(new GUIContent(meta)).x + 10f);
            float rightEdge = rowRect.xMax - 8f;
            Rect metaRect = new Rect(rightEdge - metaWidth, rowRect.y + 5f, metaWidth, 18f);

            GUIStyle interfaceStyle = AuthoringInterfaceLabelStyle();
            string interfaceLabel = narrow ? InterfaceShortLabel(item.interfaceKind) : InterfaceLabel(item.interfaceKind);
            float interfaceWidth = Mathf.Min(narrow ? 58f : 130f, interfaceStyle.CalcSize(new GUIContent(interfaceLabel)).x + 14f);
            Rect interfaceRect = new Rect(metaRect.x - interfaceWidth - 6f, rowRect.y + 5f, interfaceWidth, 18f);
            float titleWidth = interfaceRect.x - checkRect.xMax - 12f;
            if (titleWidth < 116f && metaWidth > 1f)
            {
                meta = string.Empty;
                metaWidth = 0f;
                metaRect = new Rect(rightEdge, rowRect.y + 5f, 0f, 18f);
                interfaceRect = new Rect(metaRect.x - interfaceWidth - 6f, rowRect.y + 5f, interfaceWidth, 18f);
                titleWidth = interfaceRect.x - checkRect.xMax - 12f;
            }

            if (titleWidth < 96f && !tiny)
            {
                interfaceLabel = InterfaceShortLabel(item.interfaceKind);
                interfaceWidth = Mathf.Min(58f, interfaceStyle.CalcSize(new GUIContent(interfaceLabel)).x + 14f);
                interfaceRect = new Rect(rightEdge - interfaceWidth, rowRect.y + 5f, interfaceWidth, 18f);
                titleWidth = interfaceRect.x - checkRect.xMax - 12f;
            }

            if (metaWidth > 1f)
                GUI.Label(metaRect, Ellipsize(meta, metaStyle, metaRect.width), metaStyle);
            GUI.Label(interfaceRect, new GUIContent(interfaceLabel, InterfaceLabel(item.interfaceKind)), interfaceStyle);

            Rect titleRect = new Rect(checkRect.xMax + 6f, rowRect.y + 4f, Mathf.Max(60f, titleWidth), 19f);
            if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none))
                SelectAuthoringItem(item, visibleIndex, Event.current, rowRect);
            GUI.Label(titleRect, new GUIContent(Ellipsize(item.Title, UtilityWindowTheme.CardLabelStyle, titleRect.width), item.Title), UtilityWindowTheme.CardLabelStyle);

            if (Event.current != null && Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
            {
                ShowAuthoringItemMenu(item, rowRect);
                Event.current.Use();
            }

            RequestAuthoringHoverPreview(item, rowRect);

            if (selected)
                DrawAuthoringItemActionRow(item, rowRect);
        }

        private void DrawAuthoringItemActionRow(AuthoringBrowserItem item, Rect anchorRect)
        {
            Rect actionRect = GUILayoutUtility.GetRect(0f, AuthoringActionRowHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 1)
                    .Draw(actionRect, GUIContent.none, false, false, false, false);
            }

            float width = Mathf.Max(220f, actionRect.width);
            bool compact = width < 720f;
            bool tiny = width < 520f;
            float x = actionRect.x + 24f;
            float y = actionRect.y + 4f;
            float height = Mathf.Max(16f, actionRect.height - 8f);
            float right = actionRect.xMax - ((compact || tiny) ? 108f : 8f);

            if (item.IsStickyNote)
            {
                if (PungentSupportRequestBridge.IsSupportRequest(item.note))
                {
                    DrawSupportRequestActionButtons(item.note, anchorRect, ref x, y, height, right, compact, tiny);
                }
                else
                {
                    if (DrawActionButton(ref x, y, height, 62f, right, "Preview", "Open a read-only sticky note preview."))
                        PreviewNoteFromBrowser(item.note, anchorRect);
                    if (DrawActionButton(ref x, y, height, 48f, right, "Edit", "Open this sticky note for editing."))
                        EditNoteFromBrowser(item.note, anchorRect);
                    if (!tiny && DrawActionButton(ref x, y, height, 78f, right, "Duplicate", "Duplicate this sticky note."))
                        DuplicateNoteFromBrowser(item.note, anchorRect);
                    if (!tiny && DrawActionButton(ref x, y, height, 82f, right, item.note.archived ? "Unarchive" : "Archive", item.note.archived ? "Restore this sticky note." : "Archive this sticky note."))
                        ArchiveNoteFromBrowser(item.note);
                    if (!compact && DrawActionButton(ref x, y, height, 58f, right, "Delete", "Delete this sticky note after confirmation."))
                    {
                        DeleteNoteFromBrowser(item.note);
                        GUIUtility.ExitGUI();
                    }
                    if (!compact && DrawActionButton(ref x, y, height, 64f, right, "Copy ID", "Copy this sticky note ID."))
                        CopyNoteId(item.note);
                    if (!compact && DrawActionButton(ref x, y, height, 68f, right, "Rich Doc", "Create or open a linked Rich Document."))
                        RunRichDocumentHandoff(item.note);
                    if (!compact && DrawActionButton(ref x, y, height, 104f, right, "Save and Close", "Save the active overlay draft and close it."))
                    {
                        CommitDraftIfDirty("Saved draft.");
                        PungentStickyNoteOverlayController.DoneEditing(PungentStickyNoteOverlayOwner.BrowserWindow);
                    }
                }
                if ((compact || tiny) && DrawRightActionButton(actionRect, 92f, "More Actions", "More note actions."))
                    ShowAuthoringItemMenu(item, anchorRect);
            }
            else
            {
                if (DrawActionButton(ref x, y, height, 64f, right, "Preview", "Open a read-only authoring preview."))
                    OpenAuthoringPreview(item, anchorRect);
                if (!tiny && DrawActionButton(ref x, y, height, 52f, right, "Open", "Open this item in its owning utility."))
                    OpenAuthoringItem(item);
                if (!compact && CanEditAuthoringItem(item) && DrawActionButton(ref x, y, height, 48f, right, "Edit", "Open this item in its owning editor."))
                    EditAuthoringItem(item);
                if (!compact && DrawActionButton(ref x, y, height, 64f, right, "Copy ID", "Copy this authoring item ID."))
                    CopyAuthoringItemId(item);
                if (!compact && DrawActionButton(ref x, y, height, 126f, right, "Create Linked Note", "Create a sticky note linked to this item."))
                    CreateLinkedNoteForAuthoringItem(item, anchorRect);
                if ((compact || tiny) && DrawRightActionButton(actionRect, 92f, "More Actions", "More authoring item actions."))
                    ShowAuthoringItemMenu(item, anchorRect);
            }
        }

        private static bool DrawActionButton(ref float x, float y, float height, float width, float rightEdge, string label, string tooltip)
        {
            if (x + width > rightEdge)
                return false;

            Rect rect = new Rect(x, y, width, height);
            x += width + 4f;
            return GUI.Button(rect, new GUIContent(label, tooltip), EditorStyles.miniButton);
        }

        private void DrawSupportRequestActionButtons(PungentNote note, Rect anchorRect, ref float x, float y, float height, float rightEdge, bool compact, bool tiny)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            if (sent)
            {
                if (DrawActionButton(ref x, y, height, 62f, rightEdge, "Preview", "Open this sent support request as a read-only record."))
                    PreviewNoteFromBrowser(note, anchorRect);
                if (DrawActionButton(ref x, y, height, 82f, rightEdge, "Follow-up", "Create a follow-up support request linked to this ticket."))
                    CreateSupportRequestFollowUp(note, anchorRect);
                if (!tiny && DrawActionButton(ref x, y, height, 72f, rightEdge, "Archive", "Choose how to archive this support request."))
                    ShowSupportRequestArchiveMenu(note);
                return;
            }

            if (DrawActionButton(ref x, y, height, 48f, rightEdge, "Edit", "Open this support request draft for editing."))
                EditNoteFromBrowser(note, anchorRect);
            if (DrawActionButton(ref x, y, height, 86f, rightEdge, PungentSupportRequestBridge.BrowserSendLabel(note), "Open the review overlay before sending."))
                OpenSupportRequestSendReview(note, anchorRect);
            if (!tiny && DrawActionButton(ref x, y, height, 78f, rightEdge, "Duplicate", "Duplicate this support request draft."))
                DuplicateNoteFromBrowser(note, anchorRect);
            if (!tiny && DrawActionButton(ref x, y, height, 72f, rightEdge, "Archive", "Choose how to archive this support request."))
                ShowSupportRequestArchiveMenu(note);
        }

        private static bool DrawRightActionButton(Rect rowRect, float width, string label, string tooltip)
        {
            Rect rect = new Rect(rowRect.xMax - width - 8f, rowRect.y + 4f, width, Mathf.Max(16f, rowRect.height - 8f));
            return GUI.Button(rect, new GUIContent(label, tooltip), EditorStyles.miniButton);
        }

        private void ShowAuthoringItemMenu(AuthoringBrowserItem item, Rect anchorRect)
        {
            if (item == null)
                return;

            GenericMenu menu = new GenericMenu();
            if (item.IsStickyNote)
            {
                if (PungentSupportRequestBridge.IsSupportRequest(item.note))
                {
                    PopulateSupportRequestMenu(menu, item.note, anchorRect);
                    menu.ShowAsContext();
                    return;
                }

                menu.AddItem(new GUIContent("Preview"), false, () => PreviewNoteFromBrowser(item.note, anchorRect));
                menu.AddItem(new GUIContent("Edit"), false, () => EditNoteFromBrowser(item.note, anchorRect));
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNoteFromBrowser(item.note, anchorRect));
                menu.AddItem(new GUIContent(item.note.archived ? "Unarchive" : "Archive"), false, () => ArchiveNoteFromBrowser(item.note));
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteNoteFromBrowser(item.note));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Copy ID"), false, () => CopyNoteId(item.note));
                menu.AddItem(new GUIContent("Rich Doc"), false, () => RunRichDocumentHandoff(item.note));
                menu.AddItem(new GUIContent("Open in Browser"), false, () => OpenNoteInBrowser(item.note));
                menu.AddItem(new GUIContent("Save and Close"), false, () =>
                {
                    CommitDraftIfDirty("Saved draft.");
                    PungentStickyNoteOverlayController.DoneEditing(PungentStickyNoteOverlayOwner.BrowserWindow);
                });
            }
            else
            {
                menu.AddItem(new GUIContent("Preview"), false, () => OpenAuthoringPreview(item, anchorRect));
                menu.AddItem(new GUIContent("Open"), false, () => OpenAuthoringItem(item));
                if (CanEditAuthoringItem(item))
                    menu.AddItem(new GUIContent("Edit"), false, () => EditAuthoringItem(item));
                else
                    menu.AddDisabledItem(new GUIContent("Edit"));
                menu.AddItem(new GUIContent("Copy ID"), false, () => CopyAuthoringItemId(item));
                menu.AddItem(new GUIContent("Create Linked Note"), false, () => CreateLinkedNoteForAuthoringItem(item, anchorRect));
            }

            menu.ShowAsContext();
        }

        private void RequestAuthoringHoverPreview(AuthoringBrowserItem item, Rect rowRect)
        {
            if (item == null)
                return;

            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            bool suppress = IsDocumentEditorFocused() || IsDraftActivelyChanging();
            if (item.IsStickyNote)
            {
                PungentNoteHoverPreview.RequestNoteCardIfHovered(
                    rowRect,
                    item.note,
                    InterfaceLabel(item.interfaceKind),
                    settings.showBrowserHoverPreviews,
                    suppress,
                    HoverPreviewDelay(settings));
                return;
            }

            PungentStickyNoteOverlayController.RequestAuthoringPreview(
                item.reference,
                item.preview,
                rowRect,
                PungentStickyNoteOverlayOwner.BrowserWindow,
                InterfaceLabel(item.interfaceKind),
                settings.showBrowserHoverPreviews,
                suppress,
                HoverPreviewDelay(settings));
        }

        private void SelectAuthoringItem(AuthoringBrowserItem item, int visibleIndex, Event evt, Rect anchorRect)
        {
            if (item == null)
                return;

            if (evt != null && (evt.type == EventType.ContextClick || (evt.isMouse && evt.button != 0)))
            {
                ShowAuthoringItemMenu(item, anchorRect);
                evt.Use();
                return;
            }

            bool additive = evt != null && (evt.control || evt.command);
            bool shift = evt != null && evt.shift;
            if (shift && _lastClickedVisibleIndex >= 0 && _visibleAuthoringItemsCache.Count > 0)
            {
                CommitDraftIfDirty("Saved draft before multi-select.");
                PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before multi-select.");
                int start = Mathf.Clamp(Mathf.Min(_lastClickedVisibleIndex, visibleIndex), 0, _visibleAuthoringItemsCache.Count - 1);
                int end = Mathf.Clamp(Mathf.Max(_lastClickedVisibleIndex, visibleIndex), 0, _visibleAuthoringItemsCache.Count - 1);
                _selectedAuthoringItemKeys.Clear();
                for (int i = start; i <= end; i++)
                    _selectedAuthoringItemKeys.Add(_visibleAuthoringItemsCache[i].key);
                _selectedAuthoringItemKey = item.key;
                SyncNoteSelectionFromAuthoringSelection();
                SavePrefs();
                Repaint();
                return;
            }

            if (additive)
            {
                ToggleAuthoringItemSelection(item, visibleIndex);
                return;
            }

            CommitDraftIfDirty("Saved draft before selection change.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before selection change.");
            _selectedAuthoringItemKeys.Clear();
            _selectedAuthoringItemKeys.Add(item.key);
            _selectedAuthoringItemKey = item.key;
            _lastClickedVisibleIndex = visibleIndex;
            SyncNoteSelectionFromAuthoringSelection();
            SavePrefs();
            Repaint();
        }

        private void ToggleAuthoringItemSelection(AuthoringBrowserItem item, int visibleIndex)
        {
            if (item == null)
                return;

            CommitDraftIfDirty("Saved draft before selection change.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before selection change.");
            if (_selectedAuthoringItemKeys.Contains(item.key))
                _selectedAuthoringItemKeys.Remove(item.key);
            else
                _selectedAuthoringItemKeys.Add(item.key);
            _selectedAuthoringItemKey = _selectedAuthoringItemKeys.Contains(item.key) ? item.key : _selectedAuthoringItemKeys.FirstOrDefault() ?? string.Empty;
            _lastClickedVisibleIndex = visibleIndex;
            SyncNoteSelectionFromAuthoringSelection();
            SavePrefs();
            Repaint();
        }

        private bool IsAuthoringItemSelected(AuthoringBrowserItem item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.key) && _selectedAuthoringItemKeys.Contains(item.key);
        }

        private void SyncNoteSelectionFromAuthoringSelection()
        {
            _selectedNoteIds.Clear();
            foreach (AuthoringBrowserItem item in SelectedAuthoringItems())
                if (item != null && item.note != null && !string.IsNullOrWhiteSpace(item.note.id))
                    _selectedNoteIds.Add(item.note.id);
            _selectedNoteId = _selectedNoteIds.FirstOrDefault() ?? string.Empty;
            _editingFutureUtility = null;
            LoadDraftForSelectedNote();
            RefreshSelectionCache(true);
        }

        private void SelectAuthoringNoteKey(string noteId)
        {
            _selectedAuthoringItemKeys.Clear();
            _selectedAuthoringItemKey = string.Empty;
            if (string.IsNullOrWhiteSpace(noteId))
                return;

            AuthoringBrowserItem item = _allAuthoringItemsCache.FirstOrDefault(candidate =>
                candidate != null &&
                candidate.note != null &&
                string.Equals(candidate.note.id, noteId, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                RebuildAuthoringItemSourceCache();
                item = _allAuthoringItemsCache.FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.note != null &&
                    string.Equals(candidate.note.id, noteId, StringComparison.OrdinalIgnoreCase));
            }

            if (item == null || string.IsNullOrWhiteSpace(item.key))
                return;

            _selectedAuthoringItemKey = item.key;
            _selectedAuthoringItemKeys.Add(item.key);
        }

        private IEnumerable<AuthoringBrowserItem> SelectedAuthoringItems()
        {
            return _allAuthoringItemsCache.Where(item => item != null && _selectedAuthoringItemKeys.Contains(item.key));
        }

        private List<PungentNote> SelectedStickyNotesFromAuthoring()
        {
            List<PungentNote> notes = new List<PungentNote>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allAuthoringItemsCache.Count; i++)
            {
                AuthoringBrowserItem item = _allAuthoringItemsCache[i];
                if (item == null || item.note == null || string.IsNullOrWhiteSpace(item.note.id) || !_selectedAuthoringItemKeys.Contains(item.key))
                    continue;

                if (seen.Add(item.note.id))
                    notes.Add(item.note);
            }

            return notes;
        }

        private void OpenAuthoringPreview(AuthoringBrowserItem item, Rect anchorRect)
        {
            if (item == null)
                return;

            PungentStickyNoteOverlayController.OpenAuthoringPreview(item.reference, EnsureAuthoringPreview(item), anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, InterfaceLabel(item.interfaceKind));
            _status = "Previewing " + InterfaceLabel(item.interfaceKind) + ".";
            Repaint();
        }

        private PungentAuthoringPreview EnsureAuthoringPreview(AuthoringBrowserItem item)
        {
            if (item == null || item.reference == null)
                return null;

            if (item.previewResolved)
                return item.preview;

            item.previewResolved = true;
            if (PungentAuthoringProviderRegistry.TryGetPreview(item.reference, out PungentAuthoringPreview preview) && preview != null)
                item.preview = preview;
            return item.preview;
        }

        private int GetAuthoringTargetCount(AuthoringBrowserItem item)
        {
            PungentAuthoringPreview preview = EnsureAuthoringPreview(item);
            return preview == null ? 0 : preview.targetCount;
        }

        private void OpenAuthoringItem(AuthoringBrowserItem item)
        {
            if (item == null || item.reference == null)
                return;

            _status = PungentAuthoringProviderRegistry.TryOpen(item.reference)
                ? "Opened " + InterfaceLabel(item.interfaceKind) + "."
                : "No provider could open this authoring item.";
            Repaint();
        }

        private bool CanEditAuthoringItem(AuthoringBrowserItem item)
        {
            if (item == null || item.reference == null)
                return false;

            IPungentAuthoringProvider provider = PungentAuthoringProviderRegistry.FindProvider(item.reference.providerId);
            return provider is IPungentAuthoringEditorLauncher launcher && launcher.CanEdit(item.reference, out _);
        }

        private void EditAuthoringItem(AuthoringBrowserItem item)
        {
            if (item == null || item.reference == null)
                return;

            _status = PungentAuthoringProviderRegistry.TryEdit(item.reference)
                ? "Opened editor for " + InterfaceLabel(item.interfaceKind) + "."
                : "No provider could edit this authoring item.";
            Repaint();
        }

        private void CopyAuthoringItemId(AuthoringBrowserItem item)
        {
            if (item == null || item.reference == null)
                return;

            if (PungentAuthoringProviderRegistry.TryCopy(item.reference, out string copiedValue, out string error))
            {
                EditorGUIUtility.systemCopyBuffer = string.IsNullOrWhiteSpace(copiedValue) ? item.ItemId : copiedValue;
                _status = "Copied authoring item ID.";
            }
            else
            {
                EditorGUIUtility.systemCopyBuffer = item.ItemId;
                _status = string.IsNullOrWhiteSpace(error) ? "Copied fallback authoring item ID." : error;
            }
            Repaint();
        }

        private void CreateLinkedNoteForAuthoringItem(AuthoringBrowserItem item, Rect anchorRect)
        {
            if (item == null || item.reference == null)
                return;

            CommitDraftIfDirty("Saved draft before creating linked note.");
            PungentNote note = PungentNoteStorage.Database.CreateNote("Note: " + item.Title, PungentNoteKind.ProjectNote);
            note.body = "Linked authoring item: " + InterfaceLabel(item.interfaceKind) + "\nProvider: " + item.reference.providerId + "\nID: " + item.reference.itemId;
            note.stableKey = "authoring:" + item.reference.providerId + ":" + item.reference.itemKind + ":" + item.reference.itemId;
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.ExternalPath,
                label = InterfaceLabel(item.interfaceKind) + ": " + item.Title,
                externalPathOrUrl = "authoring://" + item.reference.providerId + "/" + item.reference.itemKind + "/" + item.reference.itemId
            });
            PungentNoteStorage.Save();
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            EditNoteFromBrowser(note, anchorRect);
            _status = "Created linked sticky note.";
        }

        private void CopySelectedAuthoringIds()
        {
            string value = string.Join(Environment.NewLine, SelectedAuthoringItems().Select(item => item.ItemId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray());
            EditorGUIUtility.systemCopyBuffer = value;
            _status = "Copied " + _selectedAuthoringItemKeys.Count + " authoring item ID(s).";
            Repaint();
        }

        private void DuplicateSelectedStickyNotesFromAuthoring()
        {
            List<PungentNote> notes = SelectedStickyNotesFromAuthoring();
            if (notes.Count == 0)
                return;

            CommitDraftIfDirty("Saved draft before duplicate.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before duplicate.");
            ReportBulk(PungentNoteBulkActions.Duplicate(notes), "duplicate");
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            Repaint();
        }

        private void ArchiveSelectedStickyNotesFromAuthoring(bool archived)
        {
            List<PungentNote> notes = SelectedStickyNotesFromAuthoring();
            if (notes.Count == 0)
                return;

            CommitDraftIfDirty("Saved draft before archive change.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before archive change.");
            ReportBulk(PungentNoteBulkActions.Archive(notes, archived), archived ? "archive" : "unarchive");
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            Repaint();
        }

        private void DeleteSelectedStickyNotesFromAuthoring()
        {
            List<PungentNote> notes = SelectedStickyNotesFromAuthoring();
            if (notes.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog("Delete Selected Notes", "Delete " + notes.Count + " selected sticky notes? Non-note authoring items will be left untouched.", "Delete Notes", "Cancel"))
                return;

            CommitDraftIfDirty("Saved draft before delete.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before delete.");
            HashSet<string> deletedIds = new HashSet<string>(notes.Select(note => note.id), StringComparer.OrdinalIgnoreCase);
            ReportBulk(PungentNoteBulkActions.Delete(notes), "delete");
            _selectedAuthoringItemKeys.RemoveWhere(key =>
            {
                AuthoringBrowserItem item = _allAuthoringItemsCache.FirstOrDefault(candidate => candidate != null && string.Equals(candidate.key, key, StringComparison.OrdinalIgnoreCase));
                return item != null && item.note != null && deletedIds.Contains(item.note.id);
            });
            _selectedAuthoringItemKey = _selectedAuthoringItemKeys.FirstOrDefault() ?? string.Empty;
            _authoringItemsDirty = true;
            SyncNoteSelectionFromAuthoringSelection();
            RefreshGuiCaches(true);
            PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
            _status = "Deleted selected sticky notes.";
            Repaint();
        }

        private void OpenSelectedAuthoringStack()
        {
            List<PungentNote> notes = SelectedStickyNotesFromAuthoring();
            if (notes.Count == 0)
                return;

            CommitDraftIfDirty("Saved draft before opening stack.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before opening stack.");
            if (notes.Count == 1)
                PungentStickyNoteOverlayController.OpenEdit(notes[0].id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
            else
                PungentStickyNoteOverlayController.OpenStack(notes, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes", true);
            _status = notes.Count == 1 ? "Opened selected note." : "Opened selected note stack.";
            Repaint();
        }

        private void SelectAllVisibleAuthoringItems()
        {
            CommitDraftIfDirty("Saved draft before multi-select.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before multi-select.");
            _selectedAuthoringItemKeys.Clear();
            foreach (AuthoringBrowserItem item in _visibleAuthoringItemsCache)
                if (item != null && !string.IsNullOrWhiteSpace(item.key))
                    _selectedAuthoringItemKeys.Add(item.key);
            _selectedAuthoringItemKey = _selectedAuthoringItemKeys.FirstOrDefault() ?? string.Empty;
            SyncNoteSelectionFromAuthoringSelection();
            SavePrefs();
            Repaint();
        }

        private void ClearAuthoringSelection()
        {
            CommitDraftIfDirty("Saved draft before clearing selection.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before clearing selection.");
            _selectedAuthoringItemKeys.Clear();
            _selectedAuthoringItemKey = string.Empty;
            SyncNoteSelectionFromAuthoringSelection();
            PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
            SavePrefs();
            Repaint();
        }

        private bool IsBrowserAuthoringKind(PungentAuthoringItemKind kind)
        {
            return kind == PungentAuthoringItemKind.LegacyNote ||
                   kind == PungentAuthoringItemKind.Task ||
                   kind == PungentAuthoringItemKind.FutureUtility ||
                   kind == PungentAuthoringItemKind.RichDocument ||
                   kind == PungentAuthoringItemKind.DataSheet ||
                   kind == PungentAuthoringItemKind.Board ||
                   kind == PungentAuthoringItemKind.Checklist;
        }

        private AuthoringBrowserInterface InterfaceFor(PungentAuthoringItemKind kind)
        {
            switch (kind)
            {
                case PungentAuthoringItemKind.RichDocument:
                    return AuthoringBrowserInterface.RichDocuments;
                case PungentAuthoringItemKind.DataSheet:
                    return AuthoringBrowserInterface.DataSheets;
                case PungentAuthoringItemKind.Board:
                    return AuthoringBrowserInterface.NodeGraphs;
                case PungentAuthoringItemKind.Checklist:
                    return AuthoringBrowserInterface.Checklists;
                default:
                    return AuthoringBrowserInterface.StickyNotes;
            }
        }

        private bool IsAuthoringInterfaceShown(AuthoringBrowserInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case AuthoringBrowserInterface.RichDocuments:
                    return _showRichDocuments;
                case AuthoringBrowserInterface.DataSheets:
                    return _showDataSheets;
                case AuthoringBrowserInterface.NodeGraphs:
                    return _showNodeGraphs;
                case AuthoringBrowserInterface.Checklists:
                    return _showChecklists;
                default:
                    return _showStickyNotes;
            }
        }

        private void ToggleAuthoringType(AuthoringBrowserInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case AuthoringBrowserInterface.RichDocuments:
                    _showRichDocuments = !_showRichDocuments;
                    break;
                case AuthoringBrowserInterface.DataSheets:
                    _showDataSheets = !_showDataSheets;
                    break;
                case AuthoringBrowserInterface.NodeGraphs:
                    _showNodeGraphs = !_showNodeGraphs;
                    break;
                case AuthoringBrowserInterface.Checklists:
                    _showChecklists = !_showChecklists;
                    break;
                default:
                    _showStickyNotes = !_showStickyNotes;
                    break;
            }

            SavePrefs();
            RefreshAuthoringVisibleItems(true);
            Repaint();
        }

        private void SetOnlyAuthoringType(AuthoringBrowserInterface interfaceKind)
        {
            _showStickyNotes = interfaceKind == AuthoringBrowserInterface.StickyNotes;
            _showRichDocuments = interfaceKind == AuthoringBrowserInterface.RichDocuments;
            _showDataSheets = interfaceKind == AuthoringBrowserInterface.DataSheets;
            _showNodeGraphs = interfaceKind == AuthoringBrowserInterface.NodeGraphs;
            _showChecklists = interfaceKind == AuthoringBrowserInterface.Checklists;
            SavePrefs();
            RefreshAuthoringVisibleItems(true);
            Repaint();
        }

        private void SetAllAuthoringTypeFilters(bool shown)
        {
            _showStickyNotes = shown;
            _showRichDocuments = shown;
            _showDataSheets = shown;
            _showNodeGraphs = shown;
            _showChecklists = shown;
            SavePrefs();
            RefreshAuthoringVisibleItems(true);
            Repaint();
        }

        private bool AllAuthoringTypesShown()
        {
            return _showStickyNotes && _showRichDocuments && _showDataSheets && _showNodeGraphs && _showChecklists;
        }

        private void ResetAuthoringRowMetadata()
        {
            _showAuthoringCreatedDate = false;
            _showAuthoringUpdatedDate = true;
            _showAuthoringStatus = false;
            _showAuthoringPriority = false;
            _showAuthoringKind = false;
            _showAuthoringTags = false;
            _showAuthoringTargetCount = false;
            SavePrefs();
            RefreshAuthoringVisibleItems(true);
            Repaint();
        }

        private void RefreshAuthoringItemsFromProviders()
        {
            _authoringItemsDirty = true;
            RebuildAuthoringItemSourceCache();
            RefreshAuthoringVisibleItems(true);
            _status = "Refreshed authoring items.";
            Repaint();
        }

        private static string BuildAuthoringItemKey(PungentAuthoringReference reference)
        {
            if (reference == null)
                return string.Empty;

            return (reference.providerId ?? string.Empty).Trim() + "|" +
                   reference.itemKind + "|" +
                   (reference.itemId ?? string.Empty).Trim();
        }

        private static string InterfaceLabel(AuthoringBrowserInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case AuthoringBrowserInterface.RichDocuments:
                    return "Rich Documents";
                case AuthoringBrowserInterface.DataSheets:
                    return "Data Sheets";
                case AuthoringBrowserInterface.NodeGraphs:
                    return "Node Graphs";
                case AuthoringBrowserInterface.Checklists:
                    return "Checklists";
                default:
                    return "Sticky Notes";
            }
        }

        private static string InterfaceShortLabel(AuthoringBrowserInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case AuthoringBrowserInterface.RichDocuments:
                    return "Doc";
                case AuthoringBrowserInterface.DataSheets:
                    return "Sheet";
                case AuthoringBrowserInterface.NodeGraphs:
                    return "Graph";
                case AuthoringBrowserInterface.Checklists:
                    return "List";
                default:
                    return "Note";
            }
        }

        private static Color InterfaceTint(AuthoringBrowserInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case AuthoringBrowserInterface.RichDocuments:
                    return UtilityWindowTheme.Purple;
                case AuthoringBrowserInterface.DataSheets:
                    return UtilityWindowTheme.Blue;
                case AuthoringBrowserInterface.NodeGraphs:
                    return UtilityWindowTheme.Amber;
                case AuthoringBrowserInterface.Checklists:
                    return UtilityWindowTheme.Green;
                default:
                    return UtilityWindowTheme.Teal;
            }
        }

        private string BuildAuthoringRowMeta(AuthoringBrowserItem item)
        {
            if (item == null || item.metadata == null)
                return string.Empty;

            List<string> parts = new List<string>();
            if (_showAuthoringKind)
                parts.Add(item.KindLabel);
            if (_showAuthoringStatus && !string.IsNullOrWhiteSpace(item.Status))
                parts.Add(item.Status);
            if (_showAuthoringPriority && !string.IsNullOrWhiteSpace(item.Priority))
                parts.Add(item.Priority);
            if (_showAuthoringTags && item.metadata.tags != null && item.metadata.tags.Count > 0)
                parts.Add("#" + string.Join(", #", item.metadata.tags.Take(3).ToArray()));
            if (_showAuthoringTargetCount)
                parts.Add(GetAuthoringTargetCount(item) + " targets");
            if (_showAuthoringCreatedDate)
                parts.Add("Created " + ShortDate(item.metadata.createdUtc));
            if (_showAuthoringUpdatedDate)
                parts.Add("Edited " + ShortDate(item.metadata.updatedUtc));
            return string.Join("  |  ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray());
        }

        private string BuildAuthoringDateMeta(AuthoringBrowserItem item)
        {
            if (item == null || item.metadata == null)
                return string.Empty;

            if (_showAuthoringUpdatedDate && !string.IsNullOrWhiteSpace(item.metadata.updatedUtc))
                return "Edited " + ShortDate(item.metadata.updatedUtc);
            if (_showAuthoringCreatedDate && !string.IsNullOrWhiteSpace(item.metadata.createdUtc))
                return "Created " + ShortDate(item.metadata.createdUtc);
            return string.Empty;
        }

        private static bool AuthoringItemMatchesSearch(AuthoringBrowserItem item, string search)
        {
            if (item == null || string.IsNullOrWhiteSpace(search))
                return true;

            string q = search.Trim();
            return Contains(item.Title, q) ||
                   Contains(item.Summary, q) ||
                   Contains(item.KindLabel, q) ||
                   Contains(InterfaceLabel(item.interfaceKind), q) ||
                   Contains(item.providerDisplayName, q) ||
                   Contains(item.Status, q) ||
                   Contains(item.Priority, q) ||
                   Contains(item.ItemId, q) ||
                   (item.metadata.tags != null && item.metadata.tags.Any(tag => Contains(tag, q)));
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(search) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<AuthoringBrowserItem> SortAuthoringItems(IEnumerable<AuthoringBrowserItem> items)
        {
            IEnumerable<AuthoringBrowserItem> source = items ?? Enumerable.Empty<AuthoringBrowserItem>();
            switch (_filters.sortMode)
            {
                case PungentNotesSortMode.Priority:
                    return source.OrderBy(item => item.Priority).ThenBy(item => item.Title);
                case PungentNotesSortMode.Status:
                    return source.OrderBy(item => item.Status).ThenBy(item => item.Title);
                case PungentNotesSortMode.Kind:
                    return source.OrderBy(item => InterfaceLabel(item.interfaceKind)).ThenBy(item => item.KindLabel).ThenBy(item => item.Title);
                case PungentNotesSortMode.Utility:
                    return source.OrderBy(item => item.providerDisplayName).ThenBy(item => item.Title);
                default:
                    return source.OrderByDescending(item => ParseDateTicks(item.metadata.updatedUtc, item.metadata.createdUtc)).ThenBy(item => item.Title);
            }
        }

        private static long ParseDateTicks(string updatedUtc, string createdUtc)
        {
            DateTime parsed;
            if (DateTime.TryParse(updatedUtc, out parsed))
                return parsed.Ticks;
            if (DateTime.TryParse(createdUtc, out parsed))
                return parsed.Ticks;
            return 0L;
        }

        private static GUIStyle AuthoringInterfaceLabelStyle()
        {
            GUIStyle style = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            return style;
        }

        private static string Ellipsize(string value, GUIStyle style, float width)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
            if (style == null || width <= 12f || style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            const string suffix = "...";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid).TrimEnd() + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }
            return low <= 0 ? suffix : text.Substring(0, low).TrimEnd() + suffix;
        }

        private void DrawDeveloperDiagnostics(List<PungentNote> visibleNotes)
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Visible " + (visibleNotes == null ? 0 : visibleNotes.Count), UtilityWindowTheme.Cyan, 86f);
                UtilityWindowTheme.CountPill("Total " + PungentNoteStorage.Database.notes.Count, UtilityWindowTheme.Neutral, 76f);
                UtilityWindowTheme.CountPill("Filter " + _filterCacheHits + "/" + _filterCacheMisses, UtilityWindowTheme.Teal, 98f);
                UtilityWindowTheme.CountPill(_lastFilterRebuildMs.ToString("0.0") + " ms", UtilityWindowTheme.Purple, 72f);
                UtilityWindowTheme.CountPill("Hover " + _lastHoverPreviewStatus, UtilityWindowTheme.Blue, 104f);
                UtilityWindowTheme.CountPill("Autosave " + _lastAutosaveTime, UtilityWindowTheme.Amber, 118f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawWritingPanel(List<PungentNote> visibleNotes, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, margin: 2), options))
            {
                PungentNote selected = SelectedNote;
                List<PungentNote> selectedNotes = SelectedNotes;
                UtilityWindowTheme.SectionTitle("Sticky Pad", UtilityWindowTheme.Neutral, selectedNotes.Count > 1 ? selectedNotes.Count + " selected" : selected != null ? ShortDate(selected.updatedUtc) : _editingFutureUtility != null ? "Future utility" : "No selection");
                _writingScroll = EditorGUILayout.BeginScrollView(
                    _writingScroll,
                    false,
                    true,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

                if (_seedPreview.Count > 0)
                    DrawSeedPreview();

                if (selectedNotes.Count > 1)
                    DrawBulkEditor(selectedNotes);
                else if (_editingFutureUtility != null)
                    DrawFutureUtilityEditor(_editingFutureUtility);
                else if (selected != null)
                    DrawNoteWritingSurface(selected, _selectedFilteredOutCache);
                else
                    DrawWritingEmptyState(visibleNotes);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawMetadataTray(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, margin: 2), options))
            {
                PungentNote selected = SelectedNote;
                List<PungentNote> selectedNotes = SelectedNotes;
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Properties", UtilityWindowTheme.Purple, selectedNotes.Count > 1 ? selectedNotes.Count + " selected" : selected != null ? selected.kind.ToString() : _editingFutureUtility != null ? "Future utility" : "No selection");
                    if (GUILayout.Button(new GUIContent(">", "Collapse properties sidebar."), EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        _propertiesCollapsed = true;
                        SavePrefs();
                    }
                }
                _detailScroll = EditorGUILayout.BeginScrollView(
                    _detailScroll,
                    false,
                    true,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

                if (selectedNotes.Count > 1)
                    DrawBulkSelectionSummary(selectedNotes);
                else if (_editingFutureUtility != null)
                    DrawFutureUtilityMetadata(_editingFutureUtility);
                else if (selected != null)
                    DrawNoteMetadataTray(selected);
                else
                    EditorGUILayout.HelpBox("Select a sticky note to edit links, targets, tokens, and source metadata.", MessageType.Info);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPropertiesRail(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, margin: 2), options))
            {
                if (GUILayout.Button(new GUIContent("<", "Expand properties sidebar."), EditorStyles.miniButton, GUILayout.Width(28f)))
                {
                    _propertiesCollapsed = false;
                    SavePrefs();
                }
                GUILayout.Space(4f);
                PungentNote selected = SelectedNote;
                if (selected != null)
                    UtilityWindowTheme.CountPill(selected.targets == null ? "0" : selected.targets.Count.ToString(), UtilityWindowTheme.Purple, 30f);
                else if (_editingFutureUtility != null)
                    UtilityWindowTheme.CountPill("F", UtilityWindowTheme.Cyan, 30f);
                else
                    UtilityWindowTheme.CountPill("-", UtilityWindowTheme.Neutral, 30f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawNoteRow(PungentNote note, List<PungentNote> visibleNotes, int visibleIndex)
        {
            bool open = IsSelectedNote(note);
            bool selected = open || _selectedNoteIds.Contains(note.id);
            Color tint = selected ? UtilityWindowTheme.Cyan : PungentNoteGUI.PriorityTint(note.priority);
            Rect rowRect = EditorGUILayout.BeginVertical(UtilityWindowTheme.PanelStyle(tint, selected ? 0.22f : 0.12f, selected ? 0.10f : 0.06f, 6, 3), GUILayout.ExpandWidth(true));
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool overlayBlockingClick = PungentNoteHoverPreview.IsPointerOverActiveOverlay();
                    using (new EditorGUI.DisabledScope(overlayBlockingClick))
                    {
                        if (GUILayout.Button(note.title, UtilityWindowTheme.CardLabelStyle, GUILayout.ExpandWidth(true)))
                            HandleNoteSelection(note, visibleNotes, visibleIndex, Event.current, rowRect);
                    }
                    EditorGUILayout.LabelField(ShortDate(note.updatedUtc), UtilityWindowTheme.PathLabelStyle, GUILayout.Width(112f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(note.kind.ToString(), UtilityWindowTheme.Blue, 100f);
                    UtilityWindowTheme.CountPill(note.status.ToString(), PungentNoteGUI.StatusTint(note.status), 94f);
                    UtilityWindowTheme.CountPill(note.priority.ToString(), PungentNoteGUI.PriorityTint(note.priority), 108f);
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            if (selected && Event.current != null && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 4f, rowRect.height), open ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Blue);

            if (Event.current != null && Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
            {
                ShowNoteRowMenu(note, rowRect);
                Event.current.Use();
            }

            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            PungentNoteHoverPreview.RequestNoteCardIfHovered(
                rowRect,
                note,
                "Sticky Notes",
                settings.showBrowserHoverPreviews,
                IsDocumentEditorFocused() || IsDraftActivelyChanging(),
                HoverPreviewDelay(settings));
        }

        private void DrawNoteWritingSurface(PungentNote note, bool selectedFilteredOut)
        {
            EnsureDocumentStyles();
            EnsureDraftFor(note);
            bool isSupportRequest = PungentSupportRequestBridge.IsSupportRequest(note);
            PungentSupportRequestRecord requestRecord = isSupportRequest ? PungentSupportRequestBridge.GetOrCreateRecord(note) : null;
            bool sentRequest = requestRecord != null && requestRecord.state == PungentSupportRequestRelayState.Sent;

            using (new EditorGUILayout.VerticalScope(_documentPageStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawDocumentToolbar(note);

                using (new EditorGUI.DisabledScope(sentRequest))
                {
                    GUI.SetNextControlName("PungentNoteDocumentTitle");
                    EditorGUI.BeginChangeCheck();
                    string nextTitle = EditorGUILayout.TextField(_draftTitle, _documentTitleStyle, GUILayout.MinHeight(34f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _draftTitle = nextTitle;
                        MarkDraftDirty("Unsaved changes");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(sentRequest))
                    {
                        EditorGUI.BeginChangeCheck();
                        note.kind = (PungentNoteKind)EditorGUILayout.EnumPopup(note.kind, GUILayout.MinWidth(116f));
                        note.status = (PungentNoteStatus)EditorGUILayout.EnumPopup(note.status, GUILayout.MinWidth(112f));
                        note.priority = (PungentNotePriority)EditorGUILayout.EnumPopup(note.priority, GUILayout.MinWidth(126f));
                        if (EditorGUI.EndChangeCheck())
                        {
                            PungentNoteStorage.Database.Touch(note);
                            PungentNoteStorage.Save();
                            RefreshGuiCaches(true);
                            _status = "Saved note properties.";
                        }
                    }

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(_draftSaveState, _draftDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 118f);
                    EditorGUILayout.LabelField(BodyWordCount(_draftBody) + " words | Updated " + ShortDate(note.updatedUtc), UtilityWindowTheme.PathLabelStyle, GUILayout.Width(210f));
                }

                if (isSupportRequest)
                    DrawSupportRequestPanel(note, requestRecord);

                if (selectedFilteredOut)
                {
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f, 5, 2)))
                    {
                        EditorGUILayout.LabelField("Selected sticky note is hidden by the current list filters.", UtilityWindowTheme.MutedMiniLabelStyle);
                        if (GUILayout.Button("Clear Filters", EditorStyles.miniButton, GUILayout.Width(86f)))
                            ClearFilters();
                    }
                }

                float bodyHeight = Mathf.Max(320f, position.height - 430f);
                if (_previewMode || sentRequest)
                {
                    DrawDocumentPreview(_draftBody, bodyHeight);
                }
                else
                {
                    GUI.SetNextControlName("PungentNoteDocumentBody");
                    EditorGUI.BeginChangeCheck();
                    string nextBody = EditorGUILayout.TextArea(_draftBody, _documentBodyStyle, GUILayout.MinHeight(bodyHeight), GUILayout.ExpandHeight(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _draftBody = nextBody;
                        MarkDraftDirty("Unsaved changes");
                    }
                }

                if (!IsDraftActivelyChanging())
                    PungentNoteGUI.DrawInlineLinkPills(_draftBody);
                else
                    EditorGUILayout.LabelField("Token/link preview pauses while typing.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawDocumentToolbar(PungentNote note)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.08f, 0.04f, 4, 2)))
            {
                PungentSupportRequestRecord requestRecord = PungentSupportRequestBridge.IsSupportRequest(note) ? PungentSupportRequestBridge.GetOrCreateRecord(note) : null;
                bool sentRequest = requestRecord != null && requestRecord.state == PungentSupportRequestRelayState.Sent;
                using (new EditorGUI.DisabledScope(!_draftDirty))
                {
                    if (GUILayout.Button(new GUIContent("Save", _draftDirty ? "Commit the current note draft." : "No unsaved note draft changes."), GUILayout.Width(58f)))
                        CommitDraftIfDirty("Saved note.");
                }
                if (sentRequest)
                {
                    UtilityWindowTheme.CountPill("Sent request", UtilityWindowTheme.Green, 112f);
                }
                else
                {
                    bool nextPreview = GUILayout.Toggle(_previewMode, _previewMode ? "Preview" : "Edit", EditorStyles.toolbarButton, GUILayout.Width(74f));
                    if (nextPreview != _previewMode)
                    {
                        _previewMode = nextPreview;
                        SavePrefs();
                    }
                    if (GUILayout.Button("Insert Section", GUILayout.Width(104f)))
                        ShowInsertSnippetMenu();
                }
                GUILayout.FlexibleSpace();
                DrawWritingActions(note);
            }
        }

        private void DrawDocumentPreview(string body, float minHeight)
        {
            using (new EditorGUILayout.VerticalScope(_documentPreviewStyle, GUILayout.MinHeight(minHeight), GUILayout.ExpandHeight(true)))
            {
                string[] lines = (body ?? string.Empty).Replace("\r\n", "\n").Split('\n');
                if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
                {
                    EditorGUILayout.LabelField("Start writing this note...", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                bool codeBlock = false;
                foreach (string raw in lines)
                {
                    string line = raw ?? string.Empty;
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        codeBlock = !codeBlock;
                        EditorGUILayout.Space(2f);
                        continue;
                    }

                    DrawPreviewLine(line, codeBlock);
                }
            }
        }

        private void DrawPreviewLine(string line, bool codeBlock)
        {
            string trimmed = (line ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                EditorGUILayout.Space(6f);
                return;
            }

            if (codeBlock)
            {
                EditorGUILayout.SelectableLabel(line, EditorStyles.helpBox, GUILayout.MinHeight(18f));
                return;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                EditorGUILayout.LabelField(trimmed.Substring(2), EditorStyles.boldLabel);
                return;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                EditorGUILayout.LabelField(trimmed.Substring(3), EditorStyles.boldLabel);
                return;
            }

            if (trimmed.StartsWith("- [ ]", StringComparison.Ordinal) || trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.LabelField(trimmed, UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                EditorGUILayout.LabelField("- " + trimmed.Substring(2), _documentPreviewStyle);
            else
                EditorGUILayout.LabelField(trimmed, _documentPreviewStyle);
        }

        private void ShowInsertSnippetMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Heading"), false, () => AppendDraftSnippet("\n\n## New Section\n\n"));
            menu.AddItem(new GUIContent("Checklist"), false, () => AppendDraftSnippet("\n\n- [ ] First check\n- [ ] Second check\n"));
            menu.AddItem(new GUIContent("Validation Section"), false, () => AppendDraftSnippet("\n\n## Validation\n\n- [ ] Compile cleanly\n- [ ] Exercise active UI path\n- [ ] Check narrow and wide layouts\n"));
            menu.AddItem(new GUIContent("Decision Section"), false, () => AppendDraftSnippet("\n\n## Decision\n\nContext:\n\nOptions:\n\nChosen path:\n\nFollow-up:\n"));
            menu.ShowAsContext();
        }

        private void AppendDraftSnippet(string snippet)
        {
            _draftBody = (_draftBody ?? string.Empty).TrimEnd() + snippet;
            MarkDraftDirty("Unsaved changes");
            Repaint();
        }

        private void DrawWritingEmptyState(List<PungentNote> visibleNotes)
        {
            if (PungentNoteStorage.Database.notes.Count == 0)
            {
                EditorGUILayout.HelpBox("No notes exist yet. Start with a general note or seed the curated backlog.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create First Note", UtilityWindowTheme.Green, GUILayout.Width(132f)))
                        SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));
                    if (GUILayout.Button("Seed Curated Backlog", GUILayout.Width(142f)))
                    {
                        _pendingCsvPath = string.Empty;
                        LoadSeedPreview(PungentNoteBacklogSeeder.CuratedSeeds(), "Curated backlog preview loaded.");
                    }
                }
                return;
            }

            if (visibleNotes != null && visibleNotes.Count == 0)
            {
                EditorGUILayout.HelpBox("Filters are hiding all sticky notes. Clear filters or adjust search to continue.", MessageType.Warning);
                if (GUILayout.Button("Clear Filters", GUILayout.Width(92f)))
                    ClearFilters();
                return;
            }

            EditorGUILayout.HelpBox("Select a sticky note from the list, create a new sticky note, or choose a template.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (UtilityWindowTheme.TintedButton("New Sticky", UtilityWindowTheme.Green, GUILayout.Width(92f)))
                    SelectNote(PungentNoteStorage.Database.CreateNote("New Note", PungentNoteKind.General));
                if (GUILayout.Button("Templates", GUILayout.Width(88f)))
                    ShowCreateTemplateMenu();
            }
        }

        private void DrawWritingActions(PungentNote note)
        {
            if (PungentSupportRequestBridge.IsSupportRequest(note))
            {
                DrawSupportRequestWritingActions(note);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(note.archived ? "Unarchive" : "Archive", GUILayout.Width(82f)))
                {
                    CommitDraftIfDirty("Saved draft before archive change.");
                    PungentNoteStorage.Archive(note, !note.archived);
                    RefreshGuiCaches(true);
                }
                if (GUILayout.Button("Duplicate", GUILayout.Width(82f)))
                {
                    CommitDraftIfDirty("Saved draft before duplicate.");
                    SelectNote(PungentNoteStorage.Database.Duplicate(note));
                }
                if (GUILayout.Button("Create Related", GUILayout.Width(112f)))
                    CreateRelatedNote(note);
                if (GUILayout.Button(new GUIContent("Rich Doc", "Create or open a linked rich document copy. The legacy note body remains unchanged."), GUILayout.Width(78f)))
                    RunRichDocumentHandoff(note);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(note.linkedUtilityId)))
                {
                    if (GUILayout.Button("Open Utility", GUILayout.Width(92f)))
                        PungentUtilityRegistry.Open(note.linkedUtilityId);
                }
                if (GUILayout.Button("Delete", GUILayout.Width(72f)) && EditorUtility.DisplayDialog("Delete Note", "Delete this note?", "Delete", "Cancel"))
                {
                    PungentNoteStorage.Delete(note);
                    _selectedNoteId = string.Empty;
                    _selectedNoteIds.Clear();
                    LoadDraftForSelectedNote();
                    SavePrefs();
                    RefreshGuiCaches(true);
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSupportRequestPanel(PungentNote note, PungentSupportRequestRecord record)
        {
            record = record ?? PungentSupportRequestBridge.GetOrCreateRecord(note);
            if (record == null)
                return;

            bool sent = record.state == PungentSupportRequestRelayState.Sent;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(SupportRequestTint(record), 0.08f, 0.035f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Support Request", UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(record.state.ToString(), SupportRequestTint(record), 92f);
                    if (!string.IsNullOrWhiteSpace(record.remoteReportId))
                        UtilityWindowTheme.CountPill("ID " + record.remoteReportId, UtilityWindowTheme.Green, 118f);
                }

                using (new EditorGUI.DisabledScope(sent))
                {
                    string previousEmail = record.contactEmail;
                    string previousDiscord = record.contactDiscord;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(new GUIContent("Category", "What kind of request this is."), UtilityWindowTheme.PathLabelStyle);
                    int requestKind = GUILayout.Toolbar(PungentSupportRequestBridge.IsFeatureRequest(record) ? 1 : 0, new[] { "Bug Report", "Feature Request" }, GUILayout.Height(22f));
                    record.category = requestKind == 1 ? PungentBugReportCategory.MissingFeature : PungentBugReportCategory.Bug;
                    EditorGUILayout.LabelField(new GUIContent("Priority", "1 is lowest priority; 5 is highest priority."), UtilityWindowTheme.PathLabelStyle);
                    int priority = GUILayout.Toolbar(PungentSupportRequestBridge.PriorityIndex(record.severity), PungentSupportRequestBridge.PriorityLabels, GUILayout.Height(22f));
                    record.severity = PungentSupportRequestBridge.PriorityFromIndex(priority);
                    record.contactEmail = EditorGUILayout.TextField(new GUIContent("Email", "Optional contact email for follow-up."), record.contactEmail);
                    record.contactDiscord = EditorGUILayout.TextField(new GUIContent("Discord", "Optional Discord handle for follow-up."), record.contactDiscord);
                    record.includeDiagnostics = EditorGUILayout.ToggleLeft(new GUIContent("Include safe diagnostics", "Include Unity version, platform, utility/topic IDs, and Developer Mode source context when enabled."), record.includeDiagnostics);
                    record.includeConsoleSummary = EditorGUILayout.ToggleLeft(new GUIContent("Include console summary", "Include the sanitized console summary when available."), record.includeConsoleSummary);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!string.Equals(previousEmail, record.contactEmail, StringComparison.Ordinal) ||
                            !string.Equals(previousDiscord, record.contactDiscord, StringComparison.Ordinal))
                            PungentSupportRequestBridge.UpdateSharedContact(record, record.contactEmail, record.contactDiscord);
                        else
                            PungentBugReportStorage.instance.TouchSupportRequest(record);
                        _status = "Saved support request metadata.";
                    }
                }

                string contextLabel = record.context == null ? string.Empty : record.context.contextLabel;
                string contextPath = record.context == null ? string.Empty : record.context.contextPath;
                EditorGUILayout.LabelField("Context: " + (string.IsNullOrWhiteSpace(contextLabel) ? "(none)" : contextLabel), UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(contextPath))
                    EditorGUILayout.LabelField(contextPath, UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(record.parentNoteId))
                    EditorGUILayout.LabelField("Follow-up to: " + record.parentNoteId + (string.IsNullOrWhiteSpace(record.parentRemoteReportId) ? string.Empty : " / " + record.parentRemoteReportId), UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(record.lastError))
                    EditorGUILayout.HelpBox(record.lastError, MessageType.Warning);
            }
        }

        private void DrawSupportRequestWritingActions(PungentNote note)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (sent)
                {
                    if (GUILayout.Button(new GUIContent("Preview", "Open this sent support request as a read-only record."), GUILayout.Width(74f)))
                        PreviewNoteFromBrowser(note, Rect.zero);
                    if (GUILayout.Button(new GUIContent("Follow-up", "Create a linked follow-up request."), GUILayout.Width(92f)))
                    {
                        CommitDraftIfDirty("Saved request before follow-up.");
                        CreateSupportRequestFollowUp(note, Rect.zero);
                    }
                    if (GUILayout.Button(new GUIContent("Archive", "Choose how to archive this support request."), GUILayout.Width(82f)))
                        ShowSupportRequestArchiveMenu(note);
                }
                else
                {
                    if (GUILayout.Button(new GUIContent(PungentSupportRequestBridge.BrowserSendLabel(note), "Open a review overlay before sending this support request."), GUILayout.Width(86f)))
                    {
                        CommitDraftIfDirty("Saved request before send review.");
                        OpenSupportRequestSendReview(note, Rect.zero);
                    }
                    if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate this support request draft."), GUILayout.Width(82f)))
                    {
                        CommitDraftIfDirty("Saved request before duplicate.");
                        PungentNote copy = PungentNoteStorage.Database.Duplicate(note);
                        PungentSupportRequestBridge.CopyRequestMetadata(note, copy);
                        if (copy != null)
                            SelectNote(copy);
                    }
                    if (GUILayout.Button(new GUIContent("Archive", "Choose how to archive this support request."), GUILayout.Width(82f)))
                        ShowSupportRequestArchiveMenu(note);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(note.linkedUtilityId)))
                {
                    if (GUILayout.Button("Open Utility", GUILayout.Width(92f)))
                        PungentUtilityRegistry.Open(note.linkedUtilityId);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static Color SupportRequestTint(PungentSupportRequestRecord record)
        {
            if (record == null)
                return UtilityWindowTheme.Teal;
            switch (record.state)
            {
                case PungentSupportRequestRelayState.Sent:
                    return UtilityWindowTheme.Green;
                case PungentSupportRequestRelayState.Failed:
                    return UtilityWindowTheme.Amber;
                case PungentSupportRequestRelayState.Queued:
                    return UtilityWindowTheme.Cyan;
                default:
                    return UtilityWindowTheme.Teal;
            }
        }

        private void DrawNoteMetadataTray(PungentNote note)
        {
            EditorGUI.BeginChangeCheck();

            UtilityWindowTheme.SectionTitle("Properties", UtilityWindowTheme.Purple);
            note.visibility = (PungentNoteVisibility)EditorGUILayout.EnumPopup("Visibility", note.visibility);
            note.developerOnly = EditorGUILayout.Toggle("Developer Only", note.developerOnly);
            note.linkedUtilityId = DrawUtilityPopup("Registered Utility", note.linkedUtilityId, true);
            note.linkedFutureUtilityId = DrawFutureUtilityPopup("Future Utility", note.linkedFutureUtilityId, true);
            note.auditIssueCode = EditorGUILayout.TextField("Audit Issue Code", note.auditIssueCode);
            note.tags = PungentNoteGUI.ParseTags(PungentNoteGUI.DrawTagField("Tags", note.tags));

            if (EditorGUI.EndChangeCheck())
            {
                PungentNoteStorage.Database.Touch(note);
                PungentNoteStorage.Save();
                RefreshGuiCaches(true);
                _status = "Saved note properties.";
            }

            DrawUtilityLinksSection(note);
            DrawHelpTopicSection(note);
            DrawAuditSection(note);
            if (DrawTargets(note))
                TouchAndSave(note, "Saved target metadata.");
            if (DrawStringList("Related Notes", note.relatedNoteIds))
                TouchAndSave(note, "Saved related notes.");
            if (DrawLinkedTokenKeys(note))
                TouchAndSave(note, "Saved linked token keys.");
            DrawImportSourceMetadata(note);

            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle("Timeline", UtilityWindowTheme.Neutral);
            EditorGUILayout.LabelField("Created UTC", note.createdUtc, UtilityWindowTheme.PathLabelStyle);
            EditorGUILayout.LabelField("Updated UTC", note.updatedUtc, UtilityWindowTheme.PathLabelStyle);
        }

        private void DrawBulkSelectionSummary(List<PungentNote> notes)
        {
            EditorGUILayout.HelpBox("Bulk actions are in the writing panel. The metadata tray summarizes the current selection.", MessageType.Info);
            UtilityWindowTheme.CountPill(notes.Count + " notes", UtilityWindowTheme.Cyan, 86f);
            foreach (PungentNote note in notes.Take(10))
                EditorGUILayout.LabelField(note.title, UtilityWindowTheme.MutedMiniLabelStyle);
            if (notes.Count > 10)
                EditorGUILayout.LabelField("+" + (notes.Count - 10) + " more", UtilityWindowTheme.PathLabelStyle);
        }

        private void DrawFutureUtilityMetadata(PungentFutureUtilityRecord record)
        {
            if (record == null)
                return;

            UtilityWindowTheme.SectionTitle("Future Utility Links", UtilityWindowTheme.Cyan);
            EditorGUILayout.LabelField(record.id, UtilityWindowTheme.PathLabelStyle);
            if (!string.IsNullOrWhiteSpace(record.relatedRegistryId))
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(record.relatedRegistryId);
                EditorGUILayout.LabelField(descriptor != null ? descriptor.DisplayName : record.relatedRegistryId, EditorStyles.boldLabel);
                if (descriptor != null && GUILayout.Button("Open Related Utility", EditorStyles.miniButton))
                    PungentUtilityRegistry.Open(record.relatedRegistryId);
            }

            int linkedNotes = PungentNoteStorage.Database.notes.Count(note => note != null && string.Equals(note.linkedFutureUtilityId, record.id, StringComparison.OrdinalIgnoreCase));
            UtilityWindowTheme.CountPill(linkedNotes + " linked notes", UtilityWindowTheme.Teal, 104f);
        }

        private void DrawUtilityLinksSection(PungentNote note)
        {
            if (note == null)
                return;

            UtilityWindowTheme.SectionTitle("Utility Links", UtilityWindowTheme.Blue);

            if (string.IsNullOrWhiteSpace(note.linkedUtilityId) && string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
            {
                EditorGUILayout.LabelField("No utility links assigned.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                DrawRegisteredUtilityLink(note);

            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                DrawFutureUtilityLink(note);
        }

        private void DrawRegisteredUtilityLink(PungentNote note)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(note.linkedUtilityId);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.06f, 5, 3)))
            {
                EditorGUILayout.LabelField(descriptor != null ? descriptor.DisplayName : note.linkedUtilityId, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(descriptor != null ? descriptor.Status : "Unregistered utility ID", UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (descriptor != null && GUILayout.Button("Open Utility", GUILayout.Width(92f)))
                        PungentUtilityRegistry.Open(note.linkedUtilityId);
                    List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetLinksForUtility(note.linkedUtilityId).ToList();
                    using (new EditorGUI.DisabledScope(links.Count == 0))
                    {
                        if (GUILayout.Button(new GUIContent("Open Docs", links.Count == 0 ? "No documentation links assigned to this utility." : "Open the first openable documentation link for this utility."), GUILayout.Width(84f)))
                            OpenFirstUtilityDocumentationLink(links);
                    }
                    if (GUILayout.Button("Related Notes", GUILayout.Width(104f)))
                    {
                        _filters.view = PungentNotesSavedView.CurrentUtilities;
                        _filters.linkedUtilityId = note.linkedUtilityId;
                        SavePrefs();
                    }
                    if (GUILayout.Button("Clear Link", GUILayout.Width(82f)))
                    {
                        note.linkedUtilityId = string.Empty;
                        TouchAndSave(note, "Cleared utility link.");
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawFutureUtilityLink(PungentNote note)
        {
            PungentFutureUtilityRecord record = PungentNoteStorage.Database.futureUtilities.FirstOrDefault(item => item != null && string.Equals(item.id, note.linkedFutureUtilityId, StringComparison.OrdinalIgnoreCase));
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.06f, 5, 3)))
            {
                EditorGUILayout.LabelField(record != null ? record.displayName : note.linkedFutureUtilityId, EditorStyles.boldLabel);
                if (record != null)
                    EditorGUILayout.LabelField(record.status + " / " + record.priority, UtilityWindowTheme.MutedMiniLabelStyle);
                else
                    EditorGUILayout.LabelField("Missing future utility record.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(record == null))
                    {
                        if (GUILayout.Button("Open Future", GUILayout.Width(92f)))
                        {
                            CommitDraftIfDirty("Saved draft before opening future utility.");
                            _editingFutureUtility = record;
                            _selectedNoteId = string.Empty;
                            _selectedNoteIds.Clear();
                            LoadDraftForSelectedNote();
                        }
                    }
                    if (GUILayout.Button("Related Notes", GUILayout.Width(104f)))
                    {
                        _filters.view = PungentNotesSavedView.FutureUtilities;
                        _filters.search = record != null ? record.displayName : note.linkedFutureUtilityId;
                        SavePrefs();
                    }
                    if (GUILayout.Button("Clear Link", GUILayout.Width(82f)))
                    {
                        note.linkedFutureUtilityId = string.Empty;
                        TouchAndSave(note, "Cleared future utility link.");
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawHelpTopicSection(PungentNote note)
        {
            if (note == null)
                return;

            if (!TryParseHelpStableKey(note.stableKey, out string topicStableId, out string anchorId))
                return;

            UtilityWindowTheme.SectionTitle("Help Topic", UtilityWindowTheme.Teal);
            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(topicStableId);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(topic == null ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, 0.12f, 0.06f, 5, 3)))
            {
                EditorGUILayout.LabelField(topic != null ? topic.title : topicStableId, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Topic: " + topicStableId, UtilityWindowTheme.PathLabelStyle);
                EditorGUILayout.LabelField("Anchor: " + anchorId, UtilityWindowTheme.PathLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(topic == null))
                    {
                        if (GUILayout.Button("Open Help", EditorStyles.miniButton, GUILayout.Width(82f)))
                            PungentUtilityHelpRegistry.Open(topic.utilityId, topic.sectionId, topic.topicId);
                    }
                    if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(62f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = topicStableId + "#" + anchorId;
                        _status = "Copied help topic anchor.";
                    }
                    if (GUILayout.Button("Remove Link", EditorStyles.miniButton, GUILayout.Width(92f)))
                    {
                        note.stableKey = string.Empty;
                        TouchAndSave(note, "Removed help topic link.");
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawAuditSection(PungentNote note)
        {
            if (note == null)
                return;

            List<PungentNoteTargetLink> auditTargets = note.targets == null
                ? new List<PungentNoteTargetLink>()
                : note.targets.Where(target => target != null && target.type == PungentNoteTargetType.AuditIssue).ToList();
            if (string.IsNullOrWhiteSpace(note.auditIssueCode) && auditTargets.Count == 0)
                return;

            UtilityWindowTheme.SectionTitle("Audit Finding", UtilityWindowTheme.Purple);
            if (!string.IsNullOrWhiteSpace(note.auditIssueCode))
                EditorGUILayout.LabelField(note.auditIssueCode, UtilityWindowTheme.PathLabelStyle);
            foreach (PungentNoteTargetLink target in auditTargets)
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(target.auditIssueCode) ? target.label : target.auditIssueCode, UtilityWindowTheme.MutedMiniLabelStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Design Audit", EditorStyles.miniButton, GUILayout.Width(122f)))
                    PungentUtilityDesignAuditWindow.Open();
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(note.auditIssueCode)))
                {
                    if (GUILayout.Button("Copy Code", EditorStyles.miniButton, GUILayout.Width(78f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = note.auditIssueCode;
                        _status = "Copied audit issue code.";
                    }
                    if (GUILayout.Button("Create Follow-up", EditorStyles.miniButton, GUILayout.Width(116f)))
                    {
                        PungentNote followUp = CreateAuditFollowUp(note.auditIssueCode, "Follow-up: " + note.title, "Follow-up for audit issue " + note.auditIssueCode + ".");
                        SelectNote(followUp);
                    }
                }
            }
        }

        private bool DrawTargets(PungentNote note)
        {
            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();

            EditorGUI.BeginChangeCheck();
            UtilityWindowTheme.SectionTitle("Targets", UtilityWindowTheme.Teal, note.targets.Count.ToString());
            DrawAddTargetBar(note);
            DrawDocumentationLinkAttachBar(note);

            DrawTargetGroup(note, "Documentation Links", UtilityWindowTheme.Cyan, target => target.type == PungentNoteTargetType.DocumentationLink);
            DrawTargetGroup(note, "External Path / Web URL", UtilityWindowTheme.Teal, target => target.type == PungentNoteTargetType.ExternalPath);
            DrawTargetGroup(note, "Assets", UtilityWindowTheme.Green, target => target.type == PungentNoteTargetType.Asset || target.type == PungentNoteTargetType.ScriptPath);
            DrawTargetGroup(note, "Scene Object / Component / Property", UtilityWindowTheme.Blue, target => target.type == PungentNoteTargetType.SceneObject || target.type == PungentNoteTargetType.ComponentType || target.type == PungentNoteTargetType.ComponentInstance || target.type == PungentNoteTargetType.SerializedProperty);
            DrawTargetGroup(note, "Tokens", UtilityWindowTheme.Cyan, target => target.type == PungentNoteTargetType.Token);
            DrawTargetGroup(note, "Audit Issues", UtilityWindowTheme.Purple, target => target.type == PungentNoteTargetType.AuditIssue);
            DrawTargetGroup(note, "Utilities", UtilityWindowTheme.Blue, target => target.type == PungentNoteTargetType.RegisteredUtility || target.type == PungentNoteTargetType.FutureUtility);
            DrawTargetGroup(note, "Related Notes", UtilityWindowTheme.Neutral, target => target.type == PungentNoteTargetType.Note);
            DrawTargetGroup(note, "Other Targets", UtilityWindowTheme.Neutral, target => target.type == PungentNoteTargetType.None);
            return EditorGUI.EndChangeCheck();
        }

        private void DrawAddTargetBar(PungentNote note)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Target", EditorStyles.miniButton, GUILayout.Width(86f)))
                    ShowAddTargetMenu(note);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawTargetGroup(PungentNote note, string label, Color tint, Func<PungentNoteTargetLink, bool> predicate)
        {
            List<int> indexes = Enumerable.Range(0, note.targets.Count)
                .Where(index => note.targets[index] != null && predicate(note.targets[index]))
                .ToList();
            if (indexes.Count == 0)
                return;

            UtilityWindowTheme.SectionTitle(label, tint, indexes.Count.ToString());
            foreach (int index in indexes)
            {
                if (index >= 0 && index < note.targets.Count)
                    DrawTargetEditorCard(note, note.targets[index], index, tint);
            }
        }

        private void DrawTargetEditorCard(PungentNote note, PungentNoteTargetLink target, int targetIndex, Color tint)
        {
            Rect cardRect = EditorGUILayout.BeginVertical(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 4, 2));
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(TargetDisplayName(target), EditorStyles.boldLabel);
                    UtilityWindowTheme.CountPill(TargetGroupLabel(target.type), tint, 116f);
                }
                target.type = (PungentNoteTargetType)EditorGUILayout.EnumPopup("Type", target.type);
                target.label = EditorGUILayout.TextField("Label", target.label);
                DrawTargetSpecificFields(target);
                target.propertyPath = EditorGUILayout.TextField("Property", target.propertyPath);
                target.componentType = EditorGUILayout.TextField("Component", target.componentType);
                DrawTargetActions(note, target, targetIndex);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            DrawTargetHover(cardRect, target);
        }

        private void DrawDocumentationLinkAttachBar(PungentNote note)
        {
            List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetAll().ToList();
            if (links.Count == 0)
            {
                EditorGUILayout.LabelField("No documentation links are available to attach.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _pendingDocumentationLinkId = DrawDocumentationLinkPopup("Attach Doc Link", _pendingDocumentationLinkId, true);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_pendingDocumentationLinkId)))
                {
                    if (GUILayout.Button(new GUIContent("Attach", "Attach the selected documentation link to this note."), EditorStyles.miniButton, GUILayout.Width(64f)))
                    {
                        PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(_pendingDocumentationLinkId);
                        if (PungentNoteStorage.AddDocumentationLinkTarget(note, link))
                            _status = "Attached documentation link.";
                        else
                            _status = "Documentation link was already attached or could not be resolved.";
                    }
                    using (new EditorGUI.DisabledScope(PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink == null))
                    {
                        if (GUILayout.Button(new GUIContent("Create Note", "Create a separate documentation note from the selected link."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        {
                            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(_pendingDocumentationLinkId);
                            string noteId = PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink != null ? PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink(link) : string.Empty;
                            _status = string.IsNullOrWhiteSpace(noteId) ? "Documentation note could not be created." : "Created documentation note.";
                        }
                    }
                }
            }
        }

        private void DrawTargetSpecificFields(PungentNoteTargetLink target)
        {
            if (target == null)
                return;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    target.utilityId = DrawUtilityPopup("Utility", target.utilityId, true);
                    break;
                case PungentNoteTargetType.FutureUtility:
                    target.futureUtilityId = DrawFutureUtilityPopup("Future Utility", target.futureUtilityId, true);
                    break;
                case PungentNoteTargetType.DocumentationLink:
                    target.documentationLinkId = DrawDocumentationLinkPopup("Documentation Link", target.documentationLinkId, true);
                    DrawDocumentationLinkTargetStatus(target.documentationLinkId);
                    break;
                case PungentNoteTargetType.ExternalPath:
                    target.externalPathOrUrl = EditorGUILayout.TextField("External Path / Web URL", target.externalPathOrUrl);
                    DrawExternalTargetStatus(target.externalPathOrUrl);
                    break;
                case PungentNoteTargetType.Asset:
                    target.assetGuid = EditorGUILayout.TextField("Asset GUID", target.assetGuid);
                    break;
                case PungentNoteTargetType.SceneObject:
                case PungentNoteTargetType.ComponentInstance:
                case PungentNoteTargetType.SerializedProperty:
                    target.sceneObjectGlobalId = EditorGUILayout.TextField("Scene Object ID", target.sceneObjectGlobalId);
                    target.assetGuid = EditorGUILayout.TextField("Asset GUID", target.assetGuid);
                    break;
                case PungentNoteTargetType.ScriptPath:
                    target.scriptPath = EditorGUILayout.TextField("Script Path", target.scriptPath);
                    break;
                case PungentNoteTargetType.Token:
                    target.tokenKey = EditorGUILayout.TextField("Token Key", target.tokenKey);
                    break;
                case PungentNoteTargetType.AuditIssue:
                    target.auditIssueCode = EditorGUILayout.TextField("Audit Issue", target.auditIssueCode);
                    break;
                case PungentNoteTargetType.Note:
                    target.noteId = EditorGUILayout.TextField("Note ID", target.noteId);
                    break;
            }
        }

        private void DrawDocumentationLinkTargetStatus(string documentationLinkId)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(documentationLinkId);
            if (link == null)
            {
                if (!string.IsNullOrWhiteSpace(documentationLinkId))
                    EditorGUILayout.HelpBox("Missing documentation link.", MessageType.Warning);
                return;
            }

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 104f);
                EditorGUILayout.LabelField(status.message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
            if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl && !string.IsNullOrWhiteSpace(status.targetValue))
                EditorGUILayout.LabelField((status.normalizedFromInput ? "Normalized URL: " : "URL: ") + status.targetValue, UtilityWindowTheme.PathLabelStyle);
        }

        private void DrawTargetHover(Rect cardRect, PungentNoteTargetLink target)
        {
            if (target == null)
                return;

            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            if (target.type == PungentNoteTargetType.Note)
            {
                PungentNote related = FindNoteById(target.noteId);
                PungentNoteHoverPreview.DrawNoteCardIfHovered(
                    cardRect,
                    related,
                    "Related Note Target",
                    settings.showBrowserHoverPreviews,
                    IsDocumentEditorFocused(),
                    HoverPreviewDelay(settings),
                    SelectNote);
                return;
            }

            if (target.type == PungentNoteTargetType.DocumentationLink)
            {
                PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(target.documentationLinkId);
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                PungentNoteHoverPreview.DrawInfoCardIfHovered(
                    cardRect,
                    "doc:" + target.documentationLinkId,
                    link != null ? PungentUtilityDocumentationLinks.GetDisplayName(link) : "Missing Documentation Link",
                    status.message,
                    status.kindLabel + (string.IsNullOrWhiteSpace(status.targetValue) ? string.Empty : " | " + status.targetValue),
                    settings.showBrowserHoverPreviews,
                    IsDocumentEditorFocused(),
                    HoverPreviewDelay(settings));
                return;
            }

            if (target.type == PungentNoteTargetType.ExternalPath)
            {
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(string.Empty, target.externalPathOrUrl);
                PungentNoteHoverPreview.DrawInfoCardIfHovered(
                    cardRect,
                    "external:" + target.externalPathOrUrl,
                    "External Path / Web URL",
                    status.message,
                    status.kindLabel + (string.IsNullOrWhiteSpace(status.targetValue) ? string.Empty : " | " + status.targetValue),
                    settings.showBrowserHoverPreviews,
                    IsDocumentEditorFocused(),
                    HoverPreviewDelay(settings));
                return;
            }

            if (target.type == PungentNoteTargetType.Token)
            {
                string key = PungentTokenParser.NormalizeKey(target.tokenKey);
                string detail = PungentTokenStorage.Database.ContainsToken(key) ? "Known token" : "Unknown token";
                PungentNoteHoverPreview.DrawInfoCardIfHovered(
                    cardRect,
                    "token:" + key,
                    string.IsNullOrWhiteSpace(key) ? "Token target" : key,
                    "Token target. Open Token Validator for full validation.",
                    detail,
                    settings.showBrowserHoverPreviews,
                    IsDocumentEditorFocused(),
                    HoverPreviewDelay(settings));
            }
        }

        private void DrawExternalTargetStatus(string externalPathOrUrl)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(string.Empty, externalPathOrUrl);
            if (!status.hasTarget)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 104f);
                EditorGUILayout.LabelField(status.message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
            if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl && !string.IsNullOrWhiteSpace(status.targetValue))
                EditorGUILayout.LabelField((status.normalizedFromInput ? "Normalized URL: " : "URL: ") + status.targetValue, UtilityWindowTheme.PathLabelStyle);
        }

        private void DrawTargetActions(PungentNote note, PungentNoteTargetLink target, int targetIndex)
        {
            if (target == null)
                return;

            if (target.type == PungentNoteTargetType.DocumentationLink)
            {
                PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(target.documentationLinkId);
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(link == null || !status.canOpen))
                    {
                        if (GUILayout.Button(new GUIContent("Open Target", status.canOpen ? "Open the documentation link target." : status.message), EditorStyles.miniButton, GUILayout.Width(88f)))
                            OpenDocumentationLinkTarget(link);
                    }
                    using (new EditorGUI.DisabledScope(link == null || !status.canCopy))
                    {
                        if (GUILayout.Button(new GUIContent("Copy Target", status.canCopy ? "Copy the documentation link target." : status.message), EditorStyles.miniButton, GUILayout.Width(88f)))
                            CopyDocumentationLinkTarget(link);
                    }
                    bool developerMode = PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;
                    if (developerMode && link != null && GUILayout.Button(new GUIContent("Edit Link", "Open this documentation link in the authoring popup."), EditorStyles.miniButton, GUILayout.Width(72f)))
                    {
                        string utilityContext = link.utilityIds == null ? null : link.utilityIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
                        DocumentationLinkEditorPopup.OpenForLink(link.id, utilityContext);
                    }
                    DrawRemoveTargetButton(note, targetIndex);
                    GUILayout.FlexibleSpace();
                }
                return;
            }

            if (target.type == PungentNoteTargetType.ExternalPath)
            {
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(string.Empty, target.externalPathOrUrl);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!status.canOpen))
                    {
                        if (GUILayout.Button(new GUIContent("Open Target", status.canOpen ? "Open this local file or web URL." : status.message), EditorStyles.miniButton, GUILayout.Width(88f)))
                            OpenExternalTarget(target.externalPathOrUrl);
                    }
                    using (new EditorGUI.DisabledScope(!status.canCopy))
                    {
                        if (GUILayout.Button(new GUIContent("Copy Target", status.canCopy ? "Copy this local file path or web URL." : status.message), EditorStyles.miniButton, GUILayout.Width(88f)))
                            CopyExternalTarget(target.externalPathOrUrl);
                    }
                    DrawRemoveTargetButton(note, targetIndex);
                    GUILayout.FlexibleSpace();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool canOpen = CanOpenTarget(target);
                using (new EditorGUI.DisabledScope(!canOpen))
                {
                    if (GUILayout.Button(new GUIContent("Open", canOpen ? "Open this target." : "This target cannot be opened from Notes."), EditorStyles.miniButton, GUILayout.Width(54f)))
                        OpenNoteTarget(target);
                }

                bool canPing = CanPingTarget(target);
                using (new EditorGUI.DisabledScope(!canPing))
                {
                    if (GUILayout.Button(new GUIContent("Ping", canPing ? "Ping the Unity object target." : "Ping is only available for Unity object targets."), EditorStyles.miniButton, GUILayout.Width(50f)))
                        PingNoteTarget(target);
                }

                string copyValue = GetTargetCopyValue(target);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(copyValue)))
                {
                    if (GUILayout.Button(new GUIContent("Copy", string.IsNullOrWhiteSpace(copyValue) ? "Nothing to copy for this target." : "Copy this target reference."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = copyValue;
                        _status = "Copied target reference.";
                    }
                }

                DrawRemoveTargetButton(note, targetIndex);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRemoveTargetButton(PungentNote note, int targetIndex)
        {
            if (GUILayout.Button(new GUIContent("Remove", "Detach this target from the note."), EditorStyles.miniButton, GUILayout.Width(64f)))
            {
                if (note != null && note.targets != null && targetIndex >= 0 && targetIndex < note.targets.Count)
                {
                    note.targets.RemoveAt(targetIndex);
                    TouchAndSave(note, "Removed note target.");
                }
                GUIUtility.ExitGUI();
            }
        }

        private bool CanOpenTarget(PungentNoteTargetLink target)
        {
            if (target == null)
                return false;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    return PungentUtilityRegistry.Find(target.utilityId) != null;
                case PungentNoteTargetType.FutureUtility:
                    return PungentNoteStorage.Database.futureUtilities.Any(record => record != null && string.Equals(record.id, target.futureUtilityId, StringComparison.OrdinalIgnoreCase));
                case PungentNoteTargetType.Token:
                    return !string.IsNullOrWhiteSpace(target.tokenKey);
                case PungentNoteTargetType.AuditIssue:
                    return true;
                case PungentNoteTargetType.Note:
                    return PungentNoteStorage.Database.notes.Any(note => note != null && string.Equals(note.id, target.noteId, StringComparison.OrdinalIgnoreCase));
                case PungentNoteTargetType.ScriptPath:
                    return !string.IsNullOrWhiteSpace(target.scriptPath) &&
                           (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(target.scriptPath) != null || File.Exists(target.scriptPath));
                default:
                    return TryResolveUnityTarget(target, out _, out _);
            }
        }

        private bool CanPingTarget(PungentNoteTargetLink target)
        {
            return TryResolveUnityTarget(target, out _, out _);
        }

        private void OpenNoteTarget(PungentNoteTargetLink target)
        {
            if (target == null)
                return;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    PungentUtilityRegistry.Open(target.utilityId);
                    _status = "Opened linked utility.";
                    return;
                case PungentNoteTargetType.FutureUtility:
                    CommitDraftIfDirty("Saved draft before opening future utility.");
                    _editingFutureUtility = PungentNoteStorage.Database.futureUtilities.FirstOrDefault(record => record != null && string.Equals(record.id, target.futureUtilityId, StringComparison.OrdinalIgnoreCase));
                    if (_editingFutureUtility != null)
                    {
                        _selectedNoteId = string.Empty;
                        _selectedNoteIds.Clear();
                        LoadDraftForSelectedNote();
                    }
                    _status = _editingFutureUtility == null ? "Future utility target is missing." : "Opened future utility.";
                    return;
                case PungentNoteTargetType.Token:
                    PungentTokenValidatorWindow.Open();
                    _status = "Opened Token Validator.";
                    return;
                case PungentNoteTargetType.AuditIssue:
                    PungentUtilityDesignAuditWindow.Open();
                    _status = "Opened Design Audit.";
                    return;
                case PungentNoteTargetType.Note:
                    OpenAndSelect(target.noteId);
                    _status = "Opened related note.";
                    return;
                case PungentNoteTargetType.ScriptPath:
                    OpenScriptPathTarget(target.scriptPath);
                    return;
            }

            if (TryResolveUnityTarget(target, out UnityEngine.Object obj, out string error))
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                AssetDatabase.OpenAsset(obj);
                _status = "Opened Unity target.";
                return;
            }

            _status = error;
        }

        private void PingNoteTarget(PungentNoteTargetLink target)
        {
            if (TryResolveUnityTarget(target, out UnityEngine.Object obj, out string error))
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                _status = "Pinged Unity target.";
                return;
            }

            _status = error;
        }

        private string GetTargetCopyValue(PungentNoteTargetLink target)
        {
            if (target == null)
                return string.Empty;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility: return target.utilityId ?? string.Empty;
                case PungentNoteTargetType.FutureUtility: return target.futureUtilityId ?? string.Empty;
                case PungentNoteTargetType.Asset:
                    return string.IsNullOrWhiteSpace(target.assetGuid) ? string.Empty : AssetDatabase.GUIDToAssetPath(target.assetGuid);
                case PungentNoteTargetType.SceneObject:
                case PungentNoteTargetType.ComponentInstance:
                case PungentNoteTargetType.SerializedProperty:
                    return !string.IsNullOrWhiteSpace(target.sceneObjectGlobalId) ? target.sceneObjectGlobalId : target.assetGuid;
                case PungentNoteTargetType.ComponentType: return target.componentType ?? string.Empty;
                case PungentNoteTargetType.ScriptPath: return target.scriptPath ?? string.Empty;
                case PungentNoteTargetType.Token: return PungentTokenParser.NormalizeKey(target.tokenKey);
                case PungentNoteTargetType.AuditIssue: return target.auditIssueCode ?? string.Empty;
                case PungentNoteTargetType.Note: return target.noteId ?? string.Empty;
                default: return target.label ?? string.Empty;
            }
        }

        private bool TryResolveUnityTarget(PungentNoteTargetLink target, out UnityEngine.Object obj, out string error)
        {
            obj = null;
            error = "Target could not be resolved.";
            if (target == null)
                return false;

            if (!string.IsNullOrWhiteSpace(target.sceneObjectGlobalId) && GlobalObjectId.TryParse(target.sceneObjectGlobalId, out GlobalObjectId globalId))
            {
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (obj != null)
                    return true;
                error = "Scene object target is missing.";
            }

            if (!string.IsNullOrWhiteSpace(target.assetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(target.assetGuid);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (obj != null)
                        return true;
                }
                error = "Asset target is missing.";
            }

            if (!string.IsNullOrWhiteSpace(target.scriptPath))
            {
                obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(target.scriptPath);
                if (obj != null)
                    return true;
                error = "Script path target is missing.";
            }

            return false;
        }

        private void OpenScriptPathTarget(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                _status = "Script path target is empty.";
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scriptPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                AssetDatabase.OpenAsset(asset);
                _status = "Opened script target.";
                return;
            }

            if (File.Exists(scriptPath))
            {
                EditorUtility.OpenWithDefaultApp(scriptPath);
                _status = "Opened script path.";
                return;
            }

            _status = "Script path target is missing.";
        }

        private bool DrawStringList(string label, List<string> values, bool readOnly = false)
        {
            UtilityWindowTheme.SectionTitle(label, UtilityWindowTheme.Neutral, values == null ? "0" : values.Count.ToString());
            if (values == null)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = !readOnly;
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(values[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        values[i] = next;
                        changed = true;
                    }
                    GUI.enabled = true;
                    if (!readOnly && GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        values.RemoveAt(i);
                        return true;
                    }
                }
            }
            if (!readOnly && GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                values.Add(string.Empty);
                changed = true;
            }

            return changed;
        }

        private bool DrawLinkedTokenKeys(PungentNote note)
        {
            UtilityWindowTheme.SectionTitle("Linked Token Keys", UtilityWindowTheme.Cyan, note.linkedTokenKeys == null ? "0" : note.linkedTokenKeys.Count.ToString());
            if (note.linkedTokenKeys == null)
                note.linkedTokenKeys = new List<string>();
            bool changed = false;

            List<PungentParsedToken> parsedTokens = GetCachedBodyTokens(note);
            UtilityWindowTheme.SectionTitle("Body Tokens", UtilityWindowTheme.Teal, parsedTokens.Count.ToString());
            if (parsedTokens.Count == 0)
                EditorGUILayout.LabelField("No brace tokens found in the note body.", UtilityWindowTheme.MutedMiniLabelStyle);
            foreach (PungentParsedToken parsed in parsedTokens.Take(12))
                PungentTokenGUI.DrawTokenChip(parsed.key, PungentTokenStorage.Database.ContainsToken(parsed.key));

            UtilityWindowTheme.SectionTitle("Manual Token Links", UtilityWindowTheme.Cyan, note.linkedTokenKeys.Count.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Token Validator", EditorStyles.miniButton, GUILayout.Width(130f)))
                    PungentTokenValidatorWindow.Open();
                if (GUILayout.Button("Validate Note Tokens", EditorStyles.miniButton, GUILayout.Width(132f)))
                {
                    ValidateAndSyncNoteTokens(note);
                }
                GUILayout.FlexibleSpace();
            }

            for (int i = 0; i < note.linkedTokenKeys.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(note.linkedTokenKeys[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        note.linkedTokenKeys[i] = PungentTokenParser.NormalizeKey(next);
                        changed = true;
                    }
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = PungentTokenParser.NormalizeKey(note.linkedTokenKeys[i]);
                        _status = "Copied token key.";
                    }
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        note.linkedTokenKeys.RemoveAt(i);
                        return true;
                    }
                }
            }

            if (GUILayout.Button("Add Token Key", EditorStyles.miniButton, GUILayout.Width(106f)))
            {
                note.linkedTokenKeys.Add(string.Empty);
                changed = true;
            }

            return changed;
        }

        private List<PungentParsedToken> GetCachedBodyTokens(PungentNote note)
        {
            if (note == null)
                return new List<PungentParsedToken>();

            string key = (note.id ?? string.Empty) + "|" + (note.updatedUtc ?? string.Empty) + "|" + ContentFingerprint(note.body);
            if (string.Equals(_bodyTokenCacheKey, key, StringComparison.Ordinal))
                return _bodyTokenCache;

            _bodyTokenCache = PungentTokenParser.Parse(note.body ?? string.Empty);
            _bodyTokenCacheKey = key;
            return _bodyTokenCache;
        }

        private void ValidateAndSyncNoteTokens(PungentNote note)
        {
            if (note == null)
                return;

            if (note.linkedTokenKeys == null)
                note.linkedTokenKeys = new List<string>();

            List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateText(
                note.body,
                note.title,
                null,
                note.id,
                null,
                false);

            List<string> parsedKeys = usages
                .Select(u => PungentTokenParser.NormalizeKey(u.tokenKey))
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            int before = note.linkedTokenKeys.Count;

            foreach (string key in parsedKeys)
            {
                if (!note.linkedTokenKeys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                    note.linkedTokenKeys.Add(key);
            }

            note.linkedTokenKeys = note.linkedTokenKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(PungentTokenParser.NormalizeKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            PungentNoteStorage.Database.Touch(note);
            PungentNoteStorage.Save();

            int added = Mathf.Max(0, note.linkedTokenKeys.Count - before);
            int unknown = parsedKeys.Count(k => !PungentTokenStorage.Database.ContainsToken(k));

            _status =
                "Validated " + usages.Count + " token usage(s). " +
                added + " linked key(s) added. " +
                unknown + " unknown token(s).";
        }

        private void DrawFutureUtilityEditor(PungentFutureUtilityRecord record)
        {
            EditorGUI.BeginChangeCheck();
            record.displayName = EditorGUILayout.TextField("Display Name", record.displayName);
            record.area = EditorGUILayout.TextField("Area", record.area);
            record.status = (PungentNoteStatus)EditorGUILayout.EnumPopup("Status", record.status);
            record.priority = (PungentNotePriority)EditorGUILayout.EnumPopup("Priority", record.priority);
            record.implemented = EditorGUILayout.Toggle("Implemented", record.implemented);
            record.relatedRegistryId = DrawUtilityPopup("Related Registry ID", record.relatedRegistryId, true);
            record.tags = PungentNoteGUI.ParseTags(PungentNoteGUI.DrawTagField("Tags", record.tags));
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            record.description = EditorGUILayout.TextArea(record.description, GUILayout.MinHeight(100f));

            if (EditorGUI.EndChangeCheck())
            {
                PungentNoteStorage.Save();
                _status = "Saved future utility.";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrWhiteSpace(record.relatedRegistryId) && GUILayout.Button("Open Utility", GUILayout.Width(92f)))
                    PungentUtilityRegistry.Open(record.relatedRegistryId);
                if (!record.implemented && GUILayout.Button("Mark Implemented", GUILayout.Width(122f)))
                    MarkFutureUtilityImplemented(record);
                if (GUILayout.Button("Create Linked Note", GUILayout.Width(128f)))
                {
                    PungentNote note = PungentNoteStorage.Database.CreateNote(record.displayName + " Note", PungentNoteKind.FutureUtility);
                    note.linkedFutureUtilityId = record.id;
                    SelectNote(note);
                }
                if (GUILayout.Button("Done", GUILayout.Width(58f)))
                {
                    CommitDraftIfDirty("Saved draft before closing future utility editor.");
                    _editingFutureUtility = null;
                }
            }
        }

        private void DrawFutureUtilityMiniList()
        {
            UtilityWindowTheme.SectionTitle("Future Utilities", UtilityWindowTheme.Cyan, PungentNoteStorage.Database.futureUtilities.Count.ToString());
            foreach (PungentFutureUtilityRecord record in PungentNoteStorage.Database.futureUtilities.Where(f => f != null).Take(12))
            {
                if (GUILayout.Button(record.displayName, EditorStyles.miniButton))
                {
                    CommitDraftIfDirty("Saved draft before opening future utility.");
                    _editingFutureUtility = record;
                    _selectedNoteId = string.Empty;
                    _selectedNoteIds.Clear();
                    LoadDraftForSelectedNote();
                    SavePrefs();
                    RefreshSelectionCache(true);
                }
            }
        }

        private void DrawRecentNotes()
        {
            List<PungentNote> recent = _recentNoteIds
                .Select(id => PungentNoteStorage.Database.notes.FirstOrDefault(note => note != null && string.Equals(note.id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(note => note != null)
                .Take(MaxRecentNotes)
                .ToList();
            if (recent.Count == 0)
                return;

            EditorGUILayout.Space(8f);
            UtilityWindowTheme.SectionTitle("Recent", UtilityWindowTheme.Neutral, recent.Count.ToString());
            foreach (PungentNote note in recent)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(note.title, EditorStyles.miniButton))
                        SelectNote(note);
                    UtilityWindowTheme.CountPill(note.status.ToString(), PungentNoteGUI.StatusTint(note.status), 82f);
                }
            }
        }

        private void DrawViewButton(PungentNotesSavedView view, string label, int count)
        {
            bool active = _filters.view == view;
            using (new EditorGUILayout.HorizontalScope(active ? UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.16f, 0.08f, 3, 1) : GUIStyle.none))
            {
                if (GUILayout.Button(label, active ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    if (_filters.view != view)
                    {
                        _filters.view = view;
                        SavePrefs();
                        RefreshGuiCaches(true);
                    }
                }
                UtilityWindowTheme.CountPill(count.ToString(), UtilityWindowTheme.Neutral, 36f);
            }
        }

        private static void DrawNullableEnum<T>(string label, ref T? value) where T : struct
        {
            Array values = Enum.GetValues(typeof(T));
            string[] labels = new[] { "Any" }.Concat(values.Cast<object>().Select(v => v.ToString())).ToArray();
            int current = value.HasValue ? Array.IndexOf(values, value.Value) + 1 : 0;
            int next = EditorGUILayout.Popup(label, current, labels);
            value = next <= 0 ? (T?)null : (T)values.GetValue(next - 1);
        }

        private static string DrawUtilityPopup(string label, string currentId, bool includeNone)
        {
            List<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All.Where(u => u != null).OrderBy(u => u.DisplayName).ToList();
            List<string> ids = utilities.Select(u => u.Id).ToList();
            List<string> labels = utilities.Select(u => u.DisplayName + " (" + u.Id + ")").ToList();
            if (includeNone)
            {
                ids.Insert(0, string.Empty);
                labels.Insert(0, "None");
            }

            int index = Mathf.Max(0, ids.FindIndex(id => string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(label, index, labels.ToArray());
            return next >= 0 && next < ids.Count ? ids[next] : string.Empty;
        }

        private static string DrawFutureUtilityPopup(string label, string currentId, bool includeNone)
        {
            List<PungentFutureUtilityRecord> records = PungentNoteStorage.Database.futureUtilities.Where(f => f != null).OrderBy(f => f.displayName).ToList();
            List<string> ids = records.Select(f => f.id).ToList();
            List<string> labels = records.Select(f => f.displayName).ToList();
            if (includeNone)
            {
                ids.Insert(0, string.Empty);
                labels.Insert(0, "None");
            }

            int index = Mathf.Max(0, ids.FindIndex(id => string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(label, index, labels.ToArray());
            return next >= 0 && next < ids.Count ? ids[next] : string.Empty;
        }

        private static string DrawDocumentationLinkPopup(string label, string currentId, bool includeNone)
        {
            List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetAll()
                .Where(link => link != null)
                .OrderBy(PungentUtilityDocumentationLinks.GetDisplayName)
                .ToList();
            List<string> ids = links.Select(link => link.id).ToList();
            List<string> labels = links.Select(link =>
            {
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                return PungentUtilityDocumentationLinks.GetDisplayName(link) + " [" + status.kindLabel + "]";
            }).ToList();

            if (includeNone)
            {
                ids.Insert(0, string.Empty);
                labels.Insert(0, "None");
            }

            if (!string.IsNullOrWhiteSpace(currentId) && !ids.Any(id => string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add(currentId);
                labels.Add(currentId + " (missing)");
            }

            int index = Mathf.Max(0, ids.FindIndex(id => string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(label, index, labels.ToArray());
            return next >= 0 && next < ids.Count ? ids[next] : string.Empty;
        }

        private void OpenDocumentationLinkTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (PungentUtilityDocumentationLinks.instance.Open(link, out string error))
            {
                _status = "Opened documentation link target.";
                return;
            }

            EditorUtility.DisplayDialog("Documentation Link", error, "OK");
            _status = error;
        }

        private void CopyDocumentationLinkTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (PungentUtilityDocumentationLinks.TryCopyTarget(link, out string error))
            {
                _status = "Copied documentation link target.";
                return;
            }

            EditorUtility.DisplayDialog("Documentation Link", error, "OK");
            _status = error;
        }

        private void OpenExternalTarget(string externalPathOrUrl)
        {
            if (PungentUtilityDocumentationLinks.TryOpenTarget(string.Empty, externalPathOrUrl, out string error))
            {
                _status = "Opened external target.";
                return;
            }

            EditorUtility.DisplayDialog("External Target", error, "OK");
            _status = error;
        }

        private void CopyExternalTarget(string externalPathOrUrl)
        {
            if (PungentUtilityDocumentationLinks.TryCopyTarget(string.Empty, externalPathOrUrl, out string error))
            {
                _status = "Copied external target.";
                return;
            }

            EditorUtility.DisplayDialog("External Target", error, "OK");
            _status = error;
        }

        private static Color TargetTint(PungentUtilityDocumentationLinks.DocumentationTargetKind kind)
        {
            switch (kind)
            {
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.UnityAsset:
                    return UtilityWindowTheme.Green;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.LocalFile:
                    return UtilityWindowTheme.Teal;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl:
                    return UtilityWindowTheme.Cyan;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.UnsupportedUrlScheme:
                    return UtilityWindowTheme.Red;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing:
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void ShowCreateTemplateMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddTemplate(menu, "Planning/Roadmap Item", "Roadmap Item", PungentNoteKind.FutureFeature, PungentNoteStatus.ToDo, PungentNotePriority.Important, new[] { "roadmap", "planning" }, "## Goal\n\n## Scope\n\n## Out of scope\n\n## Dependencies\n\n## Done when\n\n- [ ] ");
            AddTemplate(menu, "Planning/Debug / Investigation Log", "Investigation Log", PungentNoteKind.ProjectNote, PungentNoteStatus.InProgress, PungentNotePriority.Important, new[] { "debug", "investigation" }, "## Symptom\n\n## Repro steps\n\n1. \n\n## Suspected cause\n\n## Evidence\n\n## Next test\n\n- [ ] ");
            AddTemplate(menu, "Utility Design/Utility Design Brief", "Utility Design Brief", PungentNoteKind.DesignDecision, PungentNoteStatus.InProgress, PungentNotePriority.Important, new[] { "utility-design", "design" }, "## Purpose\n\n## User workflow\n\n## Current behaviour\n\n## Proposed changes\n\n## Safety / performance notes\n\n## Validation\n\n- [ ] ");
            AddTemplate(menu, "Utility Design/Implementation Pass Brief", "Implementation Pass Brief", PungentNoteKind.ImplementationTask, PungentNoteStatus.ToDo, PungentNotePriority.Important, new[] { "implementation", "pass-brief" }, "## Primary objective\n\n## Files likely involved\n\n## Required changes\n\n## Safety requirements\n\n## Performance requirements\n\n## Validation\n\n- [ ] Compile cleanly\n- [ ] Exercise the active UI path\n");
            AddTemplate(menu, "Documentation/Documentation Link Note", "Documentation Link Note", PungentNoteKind.UtilityNote, PungentNoteStatus.ToDo, PungentNotePriority.NiceToHave, new[] { "documentation", "documentation-link" }, "## Linked target\n\n{{documentationLink}}\n\n## Summary\n\n## Source type\n\n## Related utility\n\n{{utility}}\n\n## Follow-up\n\n- [ ] ");
            AddTemplate(menu, "Documentation/Help Topic Draft", "Help Topic Draft", PungentNoteKind.UtilityNote, PungentNoteStatus.ToDo, PungentNotePriority.NiceToHave, new[] { "help", "documentation" }, "## User problem\n\n## Explanation\n\n## Steps\n\n1. \n\n## Related utility\n\n{{utility}}\n\n## Related docs\n\n");
            AddTemplate(menu, "Support/Support Request", "Support Request", PungentNoteKind.SupportRequest, PungentNoteStatus.ToDo, PungentNotePriority.Important, new[] { "support-request" }, "## Request\n\nDescribe the bug, feature request, confusing workflow, or documentation gap.\n\n## Steps / context\n\n1. \n\n## Expected result\n\n## Actual result\n\n");
            AddTemplate(menu, "Audit / Validation/Audit Follow-up", "Audit Follow-up", PungentNoteKind.AuditFollowUp, PungentNoteStatus.ToDo, PungentNotePriority.Important, new[] { "audit", "validation" }, "## Finding\n\n{{auditIssue}}\n\n## Cause\n\n## Risk\n\n## Fix plan\n\n- [ ] \n\n## Validation\n\n- [ ] ");
            AddTemplate(menu, "Tokens / Content/Token Documentation", "Token Documentation", PungentNoteKind.TokenDocumentation, PungentNoteStatus.ToDo, PungentNotePriority.NiceToHave, new[] { "token", "documentation" }, "## Token key\n\n## Meaning\n\n## Example raw text\n\n## Rendered preview expectation\n\n## Validation notes\n\n- [ ] ");
            AddTemplate(menu, "Release/Release Note / Changelog", "Release Note", PungentNoteKind.ReleaseNote, PungentNoteStatus.ToDo, PungentNotePriority.NiceToHave, new[] { "release", "changelog" }, "## Added\n\n## Changed\n\n## Fixed\n\n## Known limitations\n\n## Validation\n\n- [ ] ");
            menu.AddSeparator(string.Empty);
            AddTemplate(menu, "General/Blank Note", "New Note", PungentNoteKind.General, PungentNoteStatus.ToDo, PungentNotePriority.NiceToHave, Array.Empty<string>(), string.Empty);
            menu.ShowAsContext();
        }

        private void AddTemplate(GenericMenu menu, string label, string title, PungentNoteKind kind, PungentNoteStatus status, PungentNotePriority priority, string[] tags, string body)
        {
            menu.AddItem(new GUIContent(label), false, () => CreateNoteFromTemplate(title, kind, status, priority, tags, body));
        }

        private void CreateNoteFromTemplate(string title, PungentNoteKind kind, PungentNoteStatus status, PungentNotePriority priority, IEnumerable<string> tags, string body)
        {
            PungentNote note = PungentNoteStorage.Database.CreateNote(title, kind);
            note.status = status;
            note.priority = priority;
            note.body = ApplyTemplateContext(body ?? string.Empty);
            note.tags = tags == null ? new List<string>() : tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            PungentNote context = SelectedNote;
            if (context != null)
            {
                note.linkedUtilityId = context.linkedUtilityId;
                note.linkedFutureUtilityId = context.linkedFutureUtilityId;
                note.auditIssueCode = context.auditIssueCode;
            }
            else if (!string.IsNullOrWhiteSpace(_filters.linkedUtilityId))
            {
                note.linkedUtilityId = _filters.linkedUtilityId;
            }
            else if (_editingFutureUtility != null)
            {
                note.linkedFutureUtilityId = _editingFutureUtility.id;
            }

            PungentNoteStorage.Database.Touch(note);
            PungentNoteStorage.Save();
            if (kind == PungentNoteKind.SupportRequest)
                PungentSupportRequestBridge.GetOrCreateRecord(note);
            SelectNote(note);
            _status = "Created " + LabelForKind(kind) + ".";
        }

        private string ApplyTemplateContext(string body)
        {
            PungentNote context = SelectedNote;
            string utilityLabel = string.Empty;
            if (context != null && !string.IsNullOrWhiteSpace(context.linkedUtilityId))
                utilityLabel = PungentNoteGUI.UtilityDisplayName(context.linkedUtilityId) + " (" + context.linkedUtilityId + ")";
            else if (!string.IsNullOrWhiteSpace(_filters.linkedUtilityId))
                utilityLabel = PungentNoteGUI.UtilityDisplayName(_filters.linkedUtilityId) + " (" + _filters.linkedUtilityId + ")";
            else if (_editingFutureUtility != null)
                utilityLabel = _editingFutureUtility.displayName;

            string auditIssue = context != null && !string.IsNullOrWhiteSpace(context.auditIssueCode)
                ? context.auditIssueCode
                : _bulkAuditCode;

            string documentationLink = string.Empty;
            if (!string.IsNullOrWhiteSpace(_pendingDocumentationLinkId))
            {
                PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(_pendingDocumentationLinkId);
                documentationLink = link != null
                    ? PungentUtilityDocumentationLinks.GetDisplayName(link) + " (" + link.id + ")"
                    : _pendingDocumentationLinkId;
            }

            return body
                .Replace("{{utility}}", string.IsNullOrWhiteSpace(utilityLabel) ? "Unlinked" : utilityLabel)
                .Replace("{{auditIssue}}", string.IsNullOrWhiteSpace(auditIssue) ? "Audit issue code:" : auditIssue)
                .Replace("{{documentationLink}}", string.IsNullOrWhiteSpace(documentationLink) ? "Documentation link:" : documentationLink);
        }

        private static string LabelForKind(PungentNoteKind kind)
        {
            if (kind == PungentNoteKind.SupportRequest)
                return "support request";
            return ObjectNames.NicifyVariableName(kind.ToString()).ToLowerInvariant();
        }

        private void ShowAddTargetMenu(PungentNote note)
        {
            if (note == null)
                return;

            GenericMenu menu = new GenericMenu();
            foreach (PungentNoteTargetType type in Enum.GetValues(typeof(PungentNoteTargetType)))
            {
                PungentNoteTargetType captured = type;
                menu.AddItem(new GUIContent(TargetGroupLabel(captured)), false, () => AddTarget(note, captured));
            }
            menu.ShowAsContext();
        }

        private void AddTarget(PungentNote note, PungentNoteTargetType type)
        {
            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();

            note.targets.Add(new PungentNoteTargetLink
            {
                type = type,
                label = TargetGroupLabel(type)
            });
            TouchAndSave(note, "Added note target.");
        }

        private static string TargetGroupLabel(PungentNoteTargetType type)
        {
            switch (type)
            {
                case PungentNoteTargetType.RegisteredUtility: return "Utility";
                case PungentNoteTargetType.FutureUtility: return "Future Utility";
                case PungentNoteTargetType.Asset: return "Asset";
                case PungentNoteTargetType.SceneObject: return "Scene Object";
                case PungentNoteTargetType.ComponentType: return "Component Type";
                case PungentNoteTargetType.ComponentInstance: return "Component";
                case PungentNoteTargetType.SerializedProperty: return "Property";
                case PungentNoteTargetType.ScriptPath: return "Script Path";
                case PungentNoteTargetType.Token: return "Token";
                case PungentNoteTargetType.AuditIssue: return "Audit Issue";
                case PungentNoteTargetType.DocumentationLink: return "Documentation Link";
                case PungentNoteTargetType.Note: return "Related Note";
                case PungentNoteTargetType.ExternalPath: return "External Path / Web URL";
                default: return "Unspecified Target";
            }
        }

        private static string TargetDisplayName(PungentNoteTargetLink target)
        {
            if (target == null)
                return "Target";
            if (!string.IsNullOrWhiteSpace(target.label))
                return target.label;
            if (target.type == PungentNoteTargetType.DocumentationLink)
            {
                PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(target.documentationLinkId);
                return link == null ? "Missing documentation link" : PungentUtilityDocumentationLinks.GetDisplayName(link);
            }
            if (target.type == PungentNoteTargetType.ExternalPath)
                return string.IsNullOrWhiteSpace(target.externalPathOrUrl) ? "External Path / Web URL" : target.externalPathOrUrl;
            return TargetGroupLabel(target.type);
        }

        private void ClearFilters()
        {
            _filters.search = string.Empty;
            _filters.view = PungentNotesSavedView.AllNotes;
            _filters.status = null;
            _filters.priority = null;
            _filters.kind = null;
            _filters.linkedUtilityId = string.Empty;
            _filters.tag = string.Empty;
            _filters.importSourceId = string.Empty;
            _filters.showArchived = false;
            _filters.showDeveloperNotes = false;
            _showStickyNotes = true;
            _showRichDocuments = true;
            _showDataSheets = true;
            _showNodeGraphs = true;
            SavePrefs();
            RefreshGuiCaches(true);
            _status = "Cleared authoring browser filters.";
        }

        private void ClearSearchOnly()
        {
            _filters.search = string.Empty;
            QueuePrefsSave();
            RefreshGuiCaches(true);
            _status = "Cleared search.";
            Repaint();
        }

        private void TouchAndSave(PungentNote note, string status)
        {
            PungentNoteStorage.Database.Touch(note);
            PungentNoteStorage.Save();
            RefreshGuiCaches(true);
            _status = status;
        }

        private void EnsureDraftFor(PungentNote note)
        {
            if (note == null)
                return;

            if (!string.Equals(_draftNoteId, note.id, StringComparison.OrdinalIgnoreCase))
                LoadDraft(note);
        }

        private void LoadDraftForSelectedNote()
        {
            PungentNote selected = SelectedNote;
            if (selected == null)
            {
                _draftNoteId = string.Empty;
                _draftTitle = string.Empty;
                _draftBody = string.Empty;
                _draftDirty = false;
                _draftSaveState = "Saved";
                return;
            }

            LoadDraft(selected);
        }

        private void LoadDraft(PungentNote note)
        {
            _draftNoteId = note.id;
            _draftTitle = note.title ?? string.Empty;
            _draftBody = note.body ?? string.Empty;
            _draftDirty = false;
            _draftSaveState = "Saved";
            _lastDraftEditTime = EditorApplication.timeSinceStartup;
            _bodyTokenCacheKey = string.Empty;
        }

        private void MarkDraftDirty(string saveState)
        {
            _draftDirty = true;
            _draftSaveState = saveState;
            _lastDraftEditTime = EditorApplication.timeSinceStartup;
            _status = saveState + ".";
        }

        private void HandleDraftAutosave()
        {
            if (!_draftDirty)
                return;

            if (EditorApplication.timeSinceStartup - _lastDraftEditTime < DraftAutosaveDelaySeconds)
                return;

            if (CommitDraftIfDirty("Autosaved note."))
            {
                _lastAutosaveTime = DateTime.Now.ToString("HH:mm:ss");
                Repaint();
            }
        }

        private void CommitDraftBeforeReload()
        {
            CommitDraftIfDirty("Saved draft before domain reload.");
        }

        private bool CommitDraftIfDirty(string status)
        {
            if (!_draftDirty || string.IsNullOrWhiteSpace(_draftNoteId))
                return false;

            PungentNote note = FindNoteById(_draftNoteId);
            if (note == null)
            {
                _draftDirty = false;
                _draftSaveState = "Draft target missing";
                return true;
            }

            bool changed = !string.Equals(note.title ?? string.Empty, _draftTitle ?? string.Empty, StringComparison.Ordinal) ||
                           !string.Equals(note.body ?? string.Empty, _draftBody ?? string.Empty, StringComparison.Ordinal);
            if (changed)
            {
                note.title = string.IsNullOrWhiteSpace(_draftTitle) ? "Untitled Note" : _draftTitle;
                note.body = _draftBody ?? string.Empty;
                PungentNoteStorage.Database.Touch(note);
                PungentNoteStorage.Save();
                RefreshGuiCaches(true);
            }

            _draftDirty = false;
            _draftSaveState = status.IndexOf("auto", StringComparison.OrdinalIgnoreCase) >= 0 ? "Autosaved" : "Saved";
            _status = status;
            return true;
        }

        private PungentNote FindNoteById(string noteId)
        {
            return PungentNoteStorage.Database.notes.FirstOrDefault(note => note != null && string.Equals(note.id, noteId, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsDraftActivelyChanging()
        {
            return _draftDirty && EditorApplication.timeSinceStartup - _lastDraftEditTime < InlinePreviewIdleDelaySeconds;
        }

        private bool IsDocumentEditorFocused()
        {
            string focusedControl = GUI.GetNameOfFocusedControl();
            return string.Equals(focusedControl, "PungentNoteDocumentBody", StringComparison.Ordinal) ||
                   string.Equals(focusedControl, "PungentNoteDocumentTitle", StringComparison.Ordinal);
        }

        private void EnsureDocumentStyles()
        {
            if (_documentPageStyle != null)
                return;

            _documentPageStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(22, 22, 18, 18),
                margin = new RectOffset(8, 8, 6, 8)
            };
            _documentTitleStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };
            _documentBodyStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 13,
                wordWrap = true,
                padding = new RectOffset(14, 14, 12, 12)
            };
            _documentPreviewStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                wordWrap = true,
                padding = new RectOffset(8, 8, 4, 4)
            };
        }

        private static int BodyWordCount(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return 0;
            return body.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static float HoverPreviewDelay(PungentNoteDisplaySettings settings)
        {
            if (settings == null || settings.hoverPreviewDelaySeconds <= 0f)
                return 0.35f;
            return Mathf.Clamp(settings.hoverPreviewDelaySeconds, 0.05f, 2f);
        }

        private void RunRichDocumentHandoff(PungentNote note)
        {
            if (note == null)
                return;

            CommitDraftIfDirty("Saved note draft before rich document handoff.");
            PungentAuthoringReference reference = PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, note.id, PungentAuthoringLegacyNoteProvider.Id, note.title);
            List<PungentAuthoringAction> actions = PungentAuthoringProviderRegistry.GetConversionActions(reference).ToList();
            PungentAuthoringAction action = actions.FirstOrDefault(item => item != null && item.enabled && item.kind == PungentAuthoringActionKind.Open) ??
                                           actions.FirstOrDefault(item => item != null && item.enabled && item.kind == PungentAuthoringActionKind.ConvertLegacyNoteToRichDocument);

            if (action == null)
            {
                PungentAuthoringAction disabled = actions.FirstOrDefault(item => item != null && !item.enabled);
                _status = disabled != null && !string.IsNullOrWhiteSpace(disabled.disabledReason)
                    ? disabled.disabledReason
                    : "Rich Document Editor provider is not installed.";
                return;
            }

            if (PungentAuthoringProviderRegistry.TryRunConversionAction(action, out _, out string error))
                _status = action.kind == PungentAuthoringActionKind.Open
                    ? "Opened converted rich document."
                    : "Created linked rich document copy. Legacy note body was left unchanged.";
            else
                _status = string.IsNullOrWhiteSpace(error) ? "Rich document handoff failed." : error;
        }

        private void CreateRelatedNote(PungentNote source)
        {
            if (source == null)
                return;

            CommitDraftIfDirty("Saved draft before creating related note.");
            PungentNote related = PungentNoteStorage.Database.CreateNote("Related: " + source.title, source.kind);
            related.status = PungentNoteStatus.ToDo;
            related.priority = source.priority;
            related.linkedUtilityId = source.linkedUtilityId;
            related.linkedFutureUtilityId = source.linkedFutureUtilityId;
            related.tags = new List<string>(source.tags ?? new List<string>());
            related.relatedNoteIds.Add(source.id);
            if (source.relatedNoteIds == null)
                source.relatedNoteIds = new List<string>();
            if (!source.relatedNoteIds.Any(id => string.Equals(id, related.id, StringComparison.OrdinalIgnoreCase)))
                source.relatedNoteIds.Add(related.id);
            TouchAndSave(source, "Created related note.");
            SelectNote(related);
        }

        private void OpenFirstUtilityDocumentationLink(List<PungentUtilityDocumentationLinks.DocumentationLink> links)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = links == null
                ? null
                : links.FirstOrDefault(item => PungentUtilityDocumentationLinks.GetTargetStatus(item).canOpen);
            if (link == null)
            {
                _status = "No openable documentation link found.";
                return;
            }

            OpenDocumentationLinkTarget(link);
        }

        private bool TryParseHelpStableKey(string stableKey, out string topicStableId, out string anchorId)
        {
            topicStableId = string.Empty;
            anchorId = PungentUtilityHelpAnchors.Topic;
            if (string.IsNullOrWhiteSpace(stableKey) || !stableKey.StartsWith("help:", StringComparison.OrdinalIgnoreCase))
                return false;

            string value = stableKey.Substring("help:".Length);
            int hash = value.IndexOf('#');
            if (hash >= 0)
            {
                topicStableId = value.Substring(0, hash);
                anchorId = hash < value.Length - 1 ? value.Substring(hash + 1) : PungentUtilityHelpAnchors.Topic;
            }
            else
            {
                topicStableId = value;
            }

            return !string.IsNullOrWhiteSpace(topicStableId);
        }

        private void DrawImportSourceMetadata(PungentNote note)
        {
            if (note == null || note.importSourceIds == null || note.importSourceIds.Count == 0)
                return;

            UtilityWindowTheme.SectionTitle("Import Source", UtilityWindowTheme.Amber, note.importSourceIds.Count.ToString());
            foreach (string sourceId in note.importSourceIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                PungentNoteImportSource source = PungentNoteStorage.Database.importSources.FirstOrDefault(item => item != null && string.Equals(item.id, sourceId, StringComparison.OrdinalIgnoreCase));
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(source != null && source.missing ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 1)))
                {
                    EditorGUILayout.LabelField(source != null ? source.displayName : sourceId, EditorStyles.boldLabel);
                    if (source != null)
                        EditorGUILayout.LabelField(source.sourcePath, UtilityWindowTheme.PathLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (source != null && GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Width(58f)))
                            PungentNoteImportSources.Reveal(source);
                        if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(64f)))
                        {
                            EditorGUIUtility.systemCopyBuffer = sourceId;
                            _status = "Copied import source ID.";
                        }
                    }
                }
            }
        }

        private void RefreshGuiCaches(bool force = false)
        {
            long notesFingerprint = BuildNotesFingerprint();
            _notesCacheFingerprint = notesFingerprint;
            string visibleKey = BuildVisibleNotesCacheKey(notesFingerprint);
            if (force || !string.Equals(_visibleNotesCacheKey, visibleKey, StringComparison.Ordinal))
            {
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                _visibleNotesCache.Clear();
                _visibleNotesCache.AddRange(PungentNoteFilters.Query(PungentNoteStorage.Database.notes, _filters));
                _visibleNoteIdCache.Clear();
                for (int i = 0; i < _visibleNotesCache.Count; i++)
                {
                    PungentNote note = _visibleNotesCache[i];
                    if (note != null && !string.IsNullOrWhiteSpace(note.id))
                        _visibleNoteIdCache.Add(note.id);
                }
                watch.Stop();

                _visibleNotesCacheKey = visibleKey;
                _filterCacheMisses++;
                _lastFilterRebuildMs = watch.Elapsed.TotalMilliseconds;
            }
            else
            {
                _filterCacheHits++;
            }

            string countKey = BuildSavedViewCountCacheKey(notesFingerprint);
            if (force || !string.Equals(_savedViewCountCacheKey, countKey, StringComparison.Ordinal))
            {
                _savedViewCountCache.Clear();
                Array views = Enum.GetValues(typeof(PungentNotesSavedView));
                for (int i = 0; i < views.Length; i++)
                {
                    PungentNotesSavedView view = (PungentNotesSavedView)views.GetValue(i);
                    _savedViewCountCache[view] = PungentNoteFilters.CountForView(PungentNoteStorage.Database.notes, view, _filters.showArchived, _filters.showDeveloperNotes);
                }
                _savedViewCountCacheKey = countKey;
            }

            if (_authoringItemsDirty || _authoringItemsSourceFingerprint != notesFingerprint)
            {
                RebuildAuthoringItemSourceCache();
                _authoringItemsSourceFingerprint = notesFingerprint;
            }
            else if (force)
            {
                _visibleAuthoringItemsCacheKey = string.Empty;
            }
            RefreshAuthoringVisibleItems(force);
            RefreshSelectionCache(force);
        }

        private void HandleAuthoringProvidersChanged()
        {
            _authoringItemsDirty = true;
            _authoringItemsSourceFingerprint = 0L;
            _visibleAuthoringItemsCacheKey = string.Empty;
            Repaint();
        }

        private void RebuildAuthoringItemSourceCache()
        {
            _allAuthoringItemsCache.Clear();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, PungentNote> notesById = new Dictionary<string, PungentNote>(StringComparer.OrdinalIgnoreCase);
            List<PungentNote> notes = PungentNoteStorage.Database.notes;
            if (notes != null)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    PungentNote note = notes[i];
                    if (note != null && !string.IsNullOrWhiteSpace(note.id) && !notesById.ContainsKey(note.id))
                        notesById.Add(note.id, note);
                }
            }

            foreach (IPungentAuthoringProvider provider in PungentAuthoringProviderRegistry.GetProviders())
            {
                if (provider == null || (provider.Capabilities & PungentAuthoringProviderCapabilities.EnumerateItems) == 0)
                    continue;

                foreach (PungentAuthoringMetadata metadata in provider.EnumerateItems() ?? Enumerable.Empty<PungentAuthoringMetadata>())
                {
                    if (metadata == null || !IsBrowserAuthoringKind(metadata.kind) || string.IsNullOrWhiteSpace(metadata.id))
                        continue;

                    metadata.NormalizeInPlace();
                    AuthoringBrowserInterface interfaceKind = InterfaceFor(metadata.kind);
                    PungentAuthoringReference reference = metadata.ToReference();
                    string key = BuildAuthoringItemKey(reference);
                    if (!seen.Add(key))
                        continue;

                    PungentNote note = null;
                    if (interfaceKind == AuthoringBrowserInterface.StickyNotes)
                        notesById.TryGetValue(metadata.id, out note);
                    _allAuthoringItemsCache.Add(new AuthoringBrowserItem
                    {
                        key = key,
                        metadata = metadata,
                        reference = reference,
                        preview = null,
                        previewResolved = false,
                        note = note,
                        interfaceKind = interfaceKind,
                        providerDisplayName = provider.DisplayName ?? string.Empty
                    });
                }
            }

            _authoringItemsDirty = false;
            _authoringItemsSourceFingerprint = _notesCacheFingerprint != 0L ? _notesCacheFingerprint : BuildNotesFingerprint();
            _visibleAuthoringItemsCacheKey = string.Empty;
        }

        private void RefreshAuthoringVisibleItems(bool force = false)
        {
            string key = BuildVisibleAuthoringItemsCacheKey(_notesCacheFingerprint);
            if (!force && string.Equals(_visibleAuthoringItemsCacheKey, key, StringComparison.Ordinal))
                return;

            HashSet<string> noteViewIds = null;
            if (_filters.view != PungentNotesSavedView.AllNotes)
                noteViewIds = new HashSet<string>(PungentNoteFilters.Query(PungentNoteStorage.Database.notes, _filters).Where(note => note != null).Select(note => note.id), StringComparer.OrdinalIgnoreCase);

            IEnumerable<AuthoringBrowserItem> query = _allAuthoringItemsCache.Where(item => item != null && item.metadata != null);
            query = query.Where(item => IsAuthoringInterfaceShown(item.interfaceKind));
            if (!_filters.showArchived && _filters.view != PungentNotesSavedView.Archived)
                query = query.Where(item => !item.metadata.archived);
            if (!_filters.showDeveloperNotes && _filters.view != PungentNotesSavedView.DeveloperNotes)
                query = query.Where(item => !item.metadata.developerOnly);
            if (noteViewIds != null)
                query = query.Where(item => item.note != null && noteViewIds.Contains(item.note.id));
            if (_filters.status.HasValue)
                query = query.Where(item => string.Equals(item.Status, _filters.status.Value.ToString(), StringComparison.OrdinalIgnoreCase));
            if (_filters.priority.HasValue)
                query = query.Where(item => string.Equals(item.Priority, _filters.priority.Value.ToString(), StringComparison.OrdinalIgnoreCase));
            if (_filters.kind.HasValue)
                query = query.Where(item => item.note != null && item.note.kind == _filters.kind.Value);
            if (!string.IsNullOrWhiteSpace(_filters.linkedUtilityId))
                query = query.Where(item => item.note != null && string.Equals(item.note.linkedUtilityId, _filters.linkedUtilityId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_filters.tag))
                query = query.Where(item => item.metadata.tags != null && item.metadata.tags.Any(tag => string.Equals(tag, _filters.tag, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(_filters.importSourceId))
                query = query.Where(item => item.note != null && item.note.importSourceIds != null && item.note.importSourceIds.Contains(_filters.importSourceId));
            if (!string.IsNullOrWhiteSpace(_filters.search))
                query = query.Where(item => AuthoringItemMatchesSearch(item, _filters.search));

            _visibleAuthoringItemsCache.Clear();
            _visibleAuthoringItemsCache.AddRange(SortAuthoringItems(query));
            _visibleAuthoringItemKeys.Clear();
            foreach (AuthoringBrowserItem item in _visibleAuthoringItemsCache)
                if (item != null && !string.IsNullOrWhiteSpace(item.key))
                    _visibleAuthoringItemKeys.Add(item.key);
            HashSet<string> allKeys = new HashSet<string>(_allAuthoringItemsCache.Where(item => item != null && !string.IsNullOrWhiteSpace(item.key)).Select(item => item.key), StringComparer.OrdinalIgnoreCase);
            _selectedAuthoringItemKeys.RemoveWhere(selectedKey => !allKeys.Contains(selectedKey));
            if (!string.IsNullOrWhiteSpace(_selectedAuthoringItemKey) && !_selectedAuthoringItemKeys.Contains(_selectedAuthoringItemKey))
                _selectedAuthoringItemKey = _selectedAuthoringItemKeys.FirstOrDefault() ?? string.Empty;
            _visibleAuthoringItemsCacheKey = key;
        }

        private void RefreshSelectionCache(bool force = false)
        {
            long notesFingerprint = _notesCacheFingerprint != 0 ? _notesCacheFingerprint : BuildNotesFingerprint();
            string selectionKey = BuildSelectionCacheKey(notesFingerprint);
            if (!force && string.Equals(_selectedNotesCacheKey, selectionKey, StringComparison.Ordinal))
                return;

            _selectedNoteCache = null;
            _selectedNotesCache.Clear();

            List<PungentNote> notes = PungentNoteStorage.Database.notes;
            if (notes != null)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    PungentNote note = notes[i];
                    if (note == null || string.IsNullOrWhiteSpace(note.id))
                        continue;

                    if (_selectedNoteCache == null && string.Equals(note.id, _selectedNoteId, StringComparison.OrdinalIgnoreCase))
                        _selectedNoteCache = note;

                    if (_selectedNoteIds.Contains(note.id))
                        _selectedNotesCache.Add(note);
                }
            }

            _selectedFilteredOutCache = _selectedNoteCache != null &&
                                        !_visibleNoteIdCache.Contains(_selectedNoteCache.id);
            _selectedNotesCacheKey = selectionKey;
        }

        private int GetSavedViewCount(PungentNotesSavedView view)
        {
            if (_savedViewCountCache.TryGetValue(view, out int count))
                return count;

            count = PungentNoteFilters.CountForView(PungentNoteStorage.Database.notes, view, _filters.showArchived, _filters.showDeveloperNotes);
            _savedViewCountCache[view] = count;
            return count;
        }

        private int GetSurfaceSelectionNoteCount(UnityEngine.Object selected, PungentNoteDisplaySettings settings)
        {
            if (selected == null)
                return 0;

            string key = selected.GetInstanceID() + "|" +
                         (settings != null && settings.showArchivedInSurfaces ? "1" : "0") + "|" +
                         (_notesCacheFingerprint != 0 ? _notesCacheFingerprint : BuildNotesFingerprint()).ToString();
            if (string.Equals(_surfaceSelectionCountKey, key, StringComparison.Ordinal))
                return _surfaceSelectionNoteCount;

            _surfaceSelectionNoteCount = PungentNoteContextResolver.FindNotesFor(selected, true, settings != null && settings.showArchivedInSurfaces).Count;
            _surfaceSelectionCountKey = key;
            return _surfaceSelectionNoteCount;
        }

        private string BuildVisibleNotesCacheKey(long notesFingerprint)
        {
            return string.Join("|",
                notesFingerprint.ToString(),
                _filters.search ?? string.Empty,
                ((int)_filters.view).ToString(),
                ((int)_filters.sortMode).ToString(),
                ((int)_groupMode).ToString(),
                _filters.showArchived ? "1" : "0",
                _filters.showDeveloperNotes ? "1" : "0",
                _filters.status.HasValue ? Convert.ToInt32(_filters.status.Value).ToString() : "-",
                _filters.priority.HasValue ? Convert.ToInt32(_filters.priority.Value).ToString() : "-",
                _filters.kind.HasValue ? Convert.ToInt32(_filters.kind.Value).ToString() : "-",
                _filters.linkedUtilityId ?? string.Empty,
                _filters.tag ?? string.Empty,
                _filters.importSourceId ?? string.Empty);
        }

        private string BuildSavedViewCountCacheKey(long notesFingerprint)
        {
            return string.Join("|",
                notesFingerprint.ToString(),
                _filters.showArchived ? "1" : "0",
                _filters.showDeveloperNotes ? "1" : "0");
        }

        private string BuildSelectionCacheKey(long notesFingerprint)
        {
            return notesFingerprint + "|" + (_selectedNoteId ?? string.Empty) + "|" + string.Join(",", _selectedNoteIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray()) + "|" + _visibleNotesCacheKey;
        }

        private string BuildVisibleAuthoringItemsCacheKey(long notesFingerprint)
        {
            return string.Join("|",
                notesFingerprint.ToString(),
                BuildAuthoringItemsFingerprint().ToString(),
                _filters.search ?? string.Empty,
                ((int)_filters.view).ToString(),
                ((int)_filters.sortMode).ToString(),
                _filters.showArchived ? "1" : "0",
                _filters.showDeveloperNotes ? "1" : "0",
                _filters.status.HasValue ? Convert.ToInt32(_filters.status.Value).ToString() : "-",
                _filters.priority.HasValue ? Convert.ToInt32(_filters.priority.Value).ToString() : "-",
                _filters.kind.HasValue ? Convert.ToInt32(_filters.kind.Value).ToString() : "-",
                _filters.linkedUtilityId ?? string.Empty,
                _filters.tag ?? string.Empty,
                _filters.importSourceId ?? string.Empty,
                _showStickyNotes ? "S1" : "S0",
                _showRichDocuments ? "R1" : "R0",
                _showDataSheets ? "D1" : "D0",
                _showNodeGraphs ? "G1" : "G0",
                _showChecklists ? "L1" : "L0",
                _showAuthoringCreatedDate ? "C1" : "C0",
                _showAuthoringUpdatedDate ? "U1" : "U0",
                _showAuthoringStatus ? "T1" : "T0",
                _showAuthoringPriority ? "P1" : "P0",
                _showAuthoringKind ? "K1" : "K0",
                _showAuthoringTags ? "A1" : "A0",
                _showAuthoringTargetCount ? "N1" : "N0");
        }

        private long BuildAuthoringItemsFingerprint()
        {
            unchecked
            {
                long hash = 41;
                hash = hash * 31 + _allAuthoringItemsCache.Count;
                foreach (AuthoringBrowserItem item in _allAuthoringItemsCache)
                {
                    if (item == null || item.metadata == null)
                        continue;

                    hash = hash * 31 + StableStringHash(item.key);
                    hash = hash * 31 + StableStringHash(item.metadata.title);
                    hash = hash * 31 + StableStringHash(item.metadata.summary);
                    hash = hash * 31 + StableStringHash(item.metadata.status);
                    hash = hash * 31 + StableStringHash(item.metadata.priority);
                    hash = hash * 31 + StableStringHash(item.metadata.updatedUtc);
                    hash = hash * 31 + StableStringHash(item.metadata.createdUtc);
                    hash = hash * 31 + (item.metadata.archived ? 1 : 0);
                    hash = hash * 31 + (item.metadata.developerOnly ? 1 : 0);
                }
                return hash;
            }
        }

        private long BuildNotesFingerprint()
        {
            unchecked
            {
                long hash = 17;
                PungentNoteDatabase database = PungentNoteStorage.Database;
                hash = hash * 31 + StableStringHash(database.lastSavedUtc);
                hash = hash * 31 + (database.notes == null ? 0 : database.notes.Count);
                hash = hash * 31 + (database.futureUtilities == null ? 0 : database.futureUtilities.Count);
                hash = hash * 31 + (database.importSources == null ? 0 : database.importSources.Count);

                if (database.notes != null)
                {
                    for (int i = 0; i < database.notes.Count; i++)
                    {
                        PungentNote note = database.notes[i];
                        if (note == null)
                            continue;

                        hash = hash * 31 + StableStringHash(note.id);
                        hash = hash * 31 + StableStringHash(note.updatedUtc);
                        hash = hash * 31 + StableStringHash(note.title);
                        hash = hash * 31 + (note.body == null ? 0 : note.body.Length);
                        hash = hash * 31 + (int)note.kind;
                        hash = hash * 31 + (int)note.status;
                        hash = hash * 31 + (int)note.priority;
                        hash = hash * 31 + (note.archived ? 1 : 0);
                        hash = hash * 31 + (note.developerOnly ? 1 : 0);
                        hash = hash * 31 + StableStringHash(note.linkedUtilityId);
                        hash = hash * 31 + StableStringHash(note.linkedFutureUtilityId);
                        hash = hash * 31 + StableStringHash(note.auditIssueCode);
                        hash = hash * 31 + (note.tags == null ? 0 : note.tags.Count);
                        hash = hash * 31 + (note.targets == null ? 0 : note.targets.Count);
                        hash = hash * 31 + (note.importSourceIds == null ? 0 : note.importSourceIds.Count);
                    }
                }

                return hash;
            }
        }

        private static int StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        private static string ContentFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "0:0";

            unchecked
            {
                int hash = 29;
                int stride = Mathf.Max(1, value.Length / 96);
                for (int i = 0; i < value.Length; i += stride)
                    hash = hash * 31 + value[i];
                if (value.Length > 0)
                    hash = hash * 31 + value[value.Length - 1];
                return value.Length + ":" + hash;
            }
        }

        private PungentNote SelectedNote
        {
            get
            {
                RefreshSelectionCache();
                return _selectedNoteCache;
            }
        }

        private List<PungentNote> SelectedNotes
        {
            get
            {
                RefreshSelectionCache();
                return _selectedNotesCache;
            }
        }

        private void SelectNote(PungentNote note)
        {
            TrySelectNote(note);
        }

        private bool TrySelectNote(PungentNote note, bool force = false)
        {
            string nextId = note != null ? note.id : string.Empty;
            if (!force && IsOnlySelectedNote(note) && _editingFutureUtility == null)
                return false;

            if (!string.Equals(_selectedNoteId, nextId, StringComparison.OrdinalIgnoreCase))
            {
                CommitDraftIfDirty("Saved draft before selection change.");
                PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before selection change.");
            }

            _selectedNoteId = nextId;
            _selectedNoteIds.Clear();
            if (note != null)
            {
                _selectedNoteIds.Add(note.id);
                SelectAuthoringNoteKey(note.id);
                RecordRecentNote(note.id);
            }
            else
            {
                _selectedAuthoringItemKeys.Clear();
                _selectedAuthoringItemKey = string.Empty;
            }
            _editingFutureUtility = null;
            LoadDraftForSelectedNote();
            SavePrefs();
            RefreshGuiCaches();
            if (note != null)
                PungentStickyNoteOverlayController.OpenEdit(note.id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
            Repaint();
            return true;
        }

        private bool IsSelectedNote(PungentNote note)
        {
            return note != null &&
                   !string.IsNullOrWhiteSpace(note.id) &&
                   string.Equals(_selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase) &&
                   _editingFutureUtility == null;
        }

        private bool IsOnlySelectedNote(PungentNote note)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.id))
                return string.IsNullOrWhiteSpace(_selectedNoteId) && _selectedNoteIds.Count == 0;

            return IsSelectedNote(note) &&
                   _selectedNoteIds.Count == 1 &&
                   _selectedNoteIds.Contains(note.id) &&
                   string.Equals(_draftNoteId, note.id, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOpenOnNoteId(string noteId)
        {
            string normalized = noteId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return string.IsNullOrWhiteSpace(_selectedNoteId) &&
                       _selectedNoteIds.Count == 0 &&
                       _editingFutureUtility == null &&
                       string.IsNullOrWhiteSpace(_draftNoteId);

            return _editingFutureUtility == null &&
                   string.Equals(_selectedNoteId, normalized, StringComparison.OrdinalIgnoreCase) &&
                   _selectedNoteIds.Count == 1 &&
                   _selectedNoteIds.Contains(normalized) &&
                   string.Equals(_draftNoteId, normalized, StringComparison.OrdinalIgnoreCase);
        }

        private void SavePrefs()
        {
            WritePrefs();
            _prefsDirty = false;
        }

        private void QueuePrefsSave()
        {
            _prefsDirty = true;
            _nextPrefsSaveTime = EditorApplication.timeSinceStartup + PreferenceSaveDelaySeconds;
        }

        private void HandleDeferredPrefsSave()
        {
            if (!_prefsDirty || EditorApplication.timeSinceStartup < _nextPrefsSaveTime)
                return;

            FlushPrefs();
        }

        private void FlushPrefs()
        {
            if (!_prefsDirty)
                return;

            WritePrefs();
            _prefsDirty = false;
        }

        private void WritePrefs()
        {
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetFloat(PrefRightWidth, _rightWidth);
            UtilityWindowPrefs.SetString(PrefSearch, _filters.search);
            UtilityWindowPrefs.SetBool(PrefShowArchived, _filters.showArchived);
            UtilityWindowPrefs.SetBool(PrefShowDeveloper, _filters.showDeveloperNotes);
            UtilityWindowPrefs.SetInt(PrefView, (int)_filters.view);
            UtilityWindowPrefs.SetInt(PrefSort, (int)_filters.sortMode);
            UtilityWindowPrefs.SetInt(PrefGroup, (int)_groupMode);
            UtilityWindowPrefs.SetString(PrefSelectedNote, _selectedNoteId);
            UtilityWindowPrefs.SetString(PrefRecentNotes, string.Join("|", _recentNoteIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(MaxRecentNotes).ToArray()));
            UtilityWindowPrefs.SetBool(PrefBrowserCollapsed, _browserCollapsed);
            UtilityWindowPrefs.SetBool(PrefPropertiesCollapsed, _propertiesCollapsed);
            UtilityWindowPrefs.SetBool(PrefPreviewMode, _previewMode);
            UtilityWindowPrefs.SetBool(PrefShowStickyNotes, _showStickyNotes);
            UtilityWindowPrefs.SetBool(PrefShowRichDocuments, _showRichDocuments);
            UtilityWindowPrefs.SetBool(PrefShowDataSheets, _showDataSheets);
            UtilityWindowPrefs.SetBool(PrefShowNodeGraphs, _showNodeGraphs);
            UtilityWindowPrefs.SetBool(PrefShowChecklists, _showChecklists);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringCreated, _showAuthoringCreatedDate);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringUpdated, _showAuthoringUpdatedDate);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringStatus, _showAuthoringStatus);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringPriority, _showAuthoringPriority);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringKind, _showAuthoringKind);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringTags, _showAuthoringTags);
            UtilityWindowPrefs.SetBool(PrefShowAuthoringTargets, _showAuthoringTargetCount);
        }

        private void LoadRecentNotes()
        {
            _recentNoteIds.Clear();
            string stored = UtilityWindowPrefs.GetString(PrefRecentNotes, string.Empty);
            if (string.IsNullOrWhiteSpace(stored))
                return;

            _recentNoteIds.AddRange(stored
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentNotes));
        }

        private void RecordRecentNote(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return;

            _recentNoteIds.RemoveAll(id => string.Equals(id, noteId, StringComparison.OrdinalIgnoreCase));
            _recentNoteIds.Insert(0, noteId);
            if (_recentNoteIds.Count > MaxRecentNotes)
                _recentNoteIds.RemoveRange(MaxRecentNotes, _recentNoteIds.Count - MaxRecentNotes);
        }

        private void LoadSeedPreview(IEnumerable<PungentBacklogSeedItem> seeds, string status)
        {
            _seedPreview.Clear();
            _seedPreview.AddRange(PungentNoteBacklogSeeder.BuildPreview(seeds));
            _status = status + " " + _seedPreview.Count + " item(s).";
        }

        private void ImportCsvPreview()
        {
            string path = EditorUtility.OpenFilePanel("Import Feature Inventory CSV", string.Empty, "csv");
            if (string.IsNullOrWhiteSpace(path))
                return;

            _pendingCsvPath = path;
            LoadSeedPreview(PungentNoteInventoryImporter.ParseCsvFile(path), "CSV inventory preview loaded.");
        }

        private void ApplySeedPreview()
        {
            PungentNoteImportSource source = !string.IsNullOrWhiteSpace(_pendingCsvPath) ? PungentNoteImportSources.GetOrCreateCsvSource(_pendingCsvPath) : null;
            PungentBacklogSeedApplyResult result = PungentNoteBacklogSeeder.ApplySelected(_seedPreview, source != null ? source.id : null);
            if (source != null)
                PungentNoteImportSources.RecordApply(source, result);
            _seedPreview.Clear();
            _pendingCsvPath = string.Empty;
            RefreshGuiCaches(true);
            _status = "Created " + result.notesCreated + " note(s), " + result.futureUtilitiesCreated + " future utilit(ies), skipped " + result.skipped + ".";
        }

        private void DrawSeedPreview()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f)))
            {
                UtilityWindowTheme.SectionTitle("Backlog Seed Preview", UtilityWindowTheme.Amber, _seedPreview.Count + " preview rows");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(72f)))
                        SetPreviewSelection(true, false);
                    if (GUILayout.Button("Select None", EditorStyles.miniButton, GUILayout.Width(82f)))
                        SetPreviewSelection(false, true);
                    if (GUILayout.Button("Select Missing Only", EditorStyles.miniButton, GUILayout.Width(124f)))
                        SetPreviewSelection(true, false);
                    if (GUILayout.Button("Apply Selected", EditorStyles.miniButton, GUILayout.Width(104f)))
                        ApplySeedPreview();
                    GUILayout.FlexibleSpace();
                }

                _seedScroll = EditorGUILayout.BeginScrollView(_seedScroll, GUILayout.Height(Mathf.Min(260f, 42f + _seedPreview.Count * 34f)));
                for (int i = 0; i < _seedPreview.Count; i++)
                {
                    PungentBacklogSeedPreviewItem item = _seedPreview[i];
                    PungentBacklogSeedItem seed = item.seed;
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(item.duplicate ? UtilityWindowTheme.Neutral : PungentNoteGUI.PriorityTint(seed.priority), 0.10f, 0.04f, 4, 2)))
                    {
                        GUI.enabled = !item.duplicate;
                        item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(18f));
                        GUI.enabled = true;
                        EditorGUILayout.LabelField(seed.title, EditorStyles.boldLabel, GUILayout.MinWidth(160f));
                        UtilityWindowTheme.CountPill(seed.area, UtilityWindowTheme.Teal, 120f);
                        UtilityWindowTheme.CountPill(seed.status.ToString(), PungentNoteGUI.StatusTint(seed.status), 104f);
                        UtilityWindowTheme.CountPill(seed.priority.ToString(), PungentNoteGUI.PriorityTint(seed.priority), 118f);
                        EditorGUILayout.LabelField(item.duplicate ? item.skipReason : seed.kind.ToString(), UtilityWindowTheme.PathLabelStyle, GUILayout.MinWidth(120f));
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void SetPreviewSelection(bool selected, bool includeDuplicates)
        {
            for (int i = 0; i < _seedPreview.Count; i++)
            {
                if (includeDuplicates || !_seedPreview[i].duplicate)
                    _seedPreview[i].selected = selected;
            }
        }

        private IEnumerable<IGrouping<string, PungentNote>> GroupNotes(List<PungentNote> notes)
        {
            switch (_groupMode)
            {
                case PungentNotesGroupMode.Status:
                    return notes.GroupBy(n => n.status.ToString()).OrderBy(g => g.Key);
                case PungentNotesGroupMode.Priority:
                    return notes.GroupBy(n => n.priority.ToString()).OrderBy(g => g.Key);
                case PungentNotesGroupMode.Kind:
                    return notes.GroupBy(n => n.kind.ToString()).OrderBy(g => g.Key);
                case PungentNotesGroupMode.Utility:
                    return notes.GroupBy(n => !string.IsNullOrWhiteSpace(n.linkedUtilityId) ? PungentNoteGUI.UtilityDisplayName(n.linkedUtilityId) : !string.IsNullOrWhiteSpace(n.linkedFutureUtilityId) ? PungentNoteGUI.FutureUtilityDisplayName(n.linkedFutureUtilityId) : "Unlinked").OrderBy(g => g.Key);
                default:
                    return notes.GroupBy(n => string.Empty);
            }
        }

        private void MarkFutureUtilityImplemented(PungentFutureUtilityRecord record)
        {
            if (record == null)
                return;

            bool updateLinkedNotes = EditorUtility.DisplayDialog("Mark Future Utility Implemented", "Mark linked notes complete as well?", "Mark Linked Complete", "Only Mark Utility");
            record.implemented = true;
            record.status = PungentNoteStatus.Complete;
            if (updateLinkedNotes)
            {
                foreach (PungentNote note in PungentNoteStorage.Database.notes.Where(n => n != null && string.Equals(n.linkedFutureUtilityId, record.id, StringComparison.OrdinalIgnoreCase)))
                {
                    note.status = PungentNoteStatus.Complete;
                    PungentNoteStorage.Database.Touch(note);
                }
            }
            else
            {
                string line = "\n\nLinked future utility marked implemented: " + DateTime.UtcNow.ToString("u");
                foreach (PungentNote note in PungentNoteStorage.Database.notes.Where(n => n != null && string.Equals(n.linkedFutureUtilityId, record.id, StringComparison.OrdinalIgnoreCase)))
                {
                    note.body = (note.body ?? string.Empty) + line;
                    PungentNoteStorage.Database.Touch(note);
                }
            }

            PungentNoteStorage.Save();
            RefreshGuiCaches(true);
            _status = "Marked future utility implemented.";
        }

        private static string Preview(string body)
        {
            string clean = (body ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length > 180 ? clean.Substring(0, 177) + "..." : clean;
        }

        private void DrawSelectedNoteActionStrip()
        {
            List<PungentNote> selectedNotes = SelectedNotes;
            if (selectedNotes.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.05f, 5, 1)))
            {
                UtilityWindowTheme.CountPill(selectedNotes.Count + " selected", UtilityWindowTheme.Cyan, 92f);
                if (selectedNotes.Count == 1)
                {
                    PungentNote note = selectedNotes[0];
                    if (PungentSupportRequestBridge.IsSupportRequest(note))
                    {
                        DrawSelectedSupportRequestActions(note, Rect.zero);
                    }
                    else
                    {
                        if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(62f)))
                            PreviewNoteFromBrowser(note, Rect.zero);
                        if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(50f)))
                            EditNoteFromBrowser(note, Rect.zero);
                        if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(78f)))
                            DuplicateNoteFromBrowser(note, Rect.zero);
                        if (GUILayout.Button(note.archived ? "Unarchive" : "Archive", EditorStyles.miniButton, GUILayout.Width(82f)))
                            ArchiveNoteFromBrowser(note);
                        if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(58f)))
                        {
                            DeleteNoteFromBrowser(note);
                            GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(64f)))
                            CopyNoteId(note);
                        if (GUILayout.Button("Save and Close", EditorStyles.miniButton, GUILayout.Width(104f)))
                        {
                            CommitDraftIfDirty("Saved draft.");
                            PungentStickyNoteOverlayController.DoneEditing(PungentStickyNoteOverlayOwner.BrowserWindow);
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Duplicate Selected", EditorStyles.miniButton, GUILayout.Width(126f)))
                        DuplicateSelectedNotesFromBrowser(selectedNotes);
                    if (GUILayout.Button("Archive Selected", EditorStyles.miniButton, GUILayout.Width(114f)))
                        ArchiveSelectedNotesFromBrowser(selectedNotes, true);
                    if (GUILayout.Button("Unarchive Selected", EditorStyles.miniButton, GUILayout.Width(128f)))
                        ArchiveSelectedNotesFromBrowser(selectedNotes, false);
                    if (GUILayout.Button("Delete Selected", EditorStyles.miniButton, GUILayout.Width(106f)))
                    {
                        DeleteSelectedNotesFromBrowser(selectedNotes);
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("Deselect", EditorStyles.miniButton, GUILayout.Width(70f)))
                        ClearSelectedNotes();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void ShowNoteRowMenu(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;

            GenericMenu menu = new GenericMenu();
            if (PungentSupportRequestBridge.IsSupportRequest(note))
            {
                PopulateSupportRequestMenu(menu, note, anchorRect);
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Preview"), false, () => PreviewNoteFromBrowser(note, anchorRect));
            menu.AddItem(new GUIContent("Edit"), false, () => EditNoteFromBrowser(note, anchorRect));
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNoteFromBrowser(note, anchorRect));
            menu.AddItem(new GUIContent(note.archived ? "Unarchive" : "Archive"), false, () => ArchiveNoteFromBrowser(note));
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteNoteFromBrowser(note));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyNoteId(note));
            menu.AddItem(new GUIContent("Rich Doc"), false, () => RunRichDocumentHandoff(note));
            menu.AddItem(new GUIContent("Open in Browser"), false, () => OpenNoteInBrowser(note));
            menu.ShowAsContext();
        }

        private void DrawSelectedSupportRequestActions(PungentNote note, Rect anchorRect)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            if (sent)
            {
                if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(62f)))
                    PreviewNoteFromBrowser(note, anchorRect);
                if (GUILayout.Button("Follow-up", EditorStyles.miniButton, GUILayout.Width(82f)))
                    CreateSupportRequestFollowUp(note, anchorRect);
                if (GUILayout.Button("Archive", EditorStyles.miniButton, GUILayout.Width(72f)))
                    ShowSupportRequestArchiveMenu(note);
            }
            else
            {
                if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(50f)))
                    EditNoteFromBrowser(note, anchorRect);
                if (GUILayout.Button(PungentSupportRequestBridge.BrowserSendLabel(note), EditorStyles.miniButton, GUILayout.Width(86f)))
                    OpenSupportRequestSendReview(note, anchorRect);
                if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(78f)))
                    DuplicateNoteFromBrowser(note, anchorRect);
                if (GUILayout.Button("Archive", EditorStyles.miniButton, GUILayout.Width(72f)))
                    ShowSupportRequestArchiveMenu(note);
            }
        }

        private void PopulateSupportRequestMenu(GenericMenu menu, PungentNote note, Rect anchorRect)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            if (sent)
            {
                menu.AddItem(new GUIContent("Preview"), false, () => PreviewNoteFromBrowser(note, anchorRect));
                menu.AddItem(new GUIContent("Follow-up"), false, () => CreateSupportRequestFollowUp(note, anchorRect));
            }
            else
            {
                menu.AddItem(new GUIContent("Edit"), false, () => EditNoteFromBrowser(note, anchorRect));
                menu.AddItem(new GUIContent(PungentSupportRequestBridge.BrowserSendLabel(note)), false, () => OpenSupportRequestSendReview(note, anchorRect));
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNoteFromBrowser(note, anchorRect));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Archive/Archive Locally"), false, () => ArchiveSupportRequestLocally(note));
            menu.AddItem(new GUIContent("Archive/Archive Support Request"), false, () => ConfirmAndArchiveSupportTicket(note));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyNoteId(note));
            menu.AddItem(new GUIContent("Open in Browser"), false, () => OpenNoteInBrowser(note));
        }

        private void OpenSupportRequestSendReview(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;
            CommitDraftIfDirty("Saved request before send review.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved request before send review.");
            TrySelectNote(note, true);
            PungentSupportRequestBridge.OpenSendReview(note, anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, "Support Request Review");
            _status = "Review this support request, then press Send in the overlay.";
            Repaint();
        }

        private void CreateSupportRequestFollowUp(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;
            CommitDraftIfDirty("Saved request before follow-up.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved request before follow-up.");
            PungentNote followUp = PungentSupportRequestBridge.CreateFollowUp(note);
            if (followUp == null)
                return;
            _authoringItemsDirty = true;
            TrySelectNote(followUp, true);
            PungentStickyNoteOverlayController.OpenEdit(followUp.id, anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, "Support Request Follow-up");
            RefreshGuiCaches(true);
            _status = "Created support request follow-up.";
            Repaint();
        }

        private void ShowSupportRequestArchiveMenu(PungentNote note)
        {
            if (note == null)
                return;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Archive Locally"), false, () => ArchiveSupportRequestLocally(note));
            menu.AddItem(new GUIContent("Archive Support Request"), false, () => ConfirmAndArchiveSupportTicket(note));
            menu.ShowAsContext();
        }

        private void ArchiveSupportRequestLocally(PungentNote note)
        {
            if (note == null)
                return;
            CommitDraftIfDirty("Saved request before local archive.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved request before local archive.");
            PungentSupportRequestBridge.ArchiveLocally(note);
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            _status = "Archived support request locally.";
            Repaint();
        }

        private void ConfirmAndArchiveSupportTicket(PungentNote note)
        {
            if (note == null)
                return;
            if (!EditorUtility.DisplayDialog(
                    "Archive Support Request",
                    "This will send an automatic follow-up asking support to archive the backend ticket. It is not just hiding the request from this browser.",
                    "Archive Backend Ticket",
                    "Cancel"))
                return;

            CommitDraftIfDirty("Saved request before backend archive.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved request before backend archive.");
            _status = "Sending archive follow-up...";
            PungentSupportRequestBridge.ArchiveSupportTicket(note, (success, message) =>
            {
                _authoringItemsDirty = true;
                RefreshGuiCaches(true);
                _status = message;
                Repaint();
            });
        }

        private void PreviewNoteFromBrowser(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;

            TrySelectNote(note, true);
            PungentStickyNoteOverlayController.OpenPreview(note.id, anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
            _status = "Previewing note.";
            Repaint();
        }

        private void EditNoteFromBrowser(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;

            TrySelectNote(note, true);
            PungentStickyNoteOverlayController.OpenEdit(note.id, anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
            _status = "Editing note.";
            Repaint();
        }

        private void DuplicateNoteFromBrowser(PungentNote note, Rect anchorRect)
        {
            if (note == null)
                return;

            CommitDraftIfDirty("Saved draft before duplicate.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before duplicate.");
            PungentNote copy = PungentNoteStorage.Database.Duplicate(note);
            if (copy == null)
                return;
            PungentSupportRequestBridge.CopyRequestMetadata(note, copy);

            _authoringItemsDirty = true;
            TrySelectNote(copy, true);
            PungentStickyNoteOverlayController.OpenEdit(copy.id, anchorRect, PungentStickyNoteOverlayOwner.BrowserWindow, "Sticky Notes");
            RefreshGuiCaches(true);
            _status = "Duplicated note.";
            Repaint();
        }

        private void ArchiveNoteFromBrowser(PungentNote note)
        {
            if (note == null)
                return;

            CommitDraftIfDirty("Saved draft before archive change.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before archive change.");
            PungentNoteStorage.Archive(note, !note.archived);
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            _status = note.archived ? "Archived note." : "Unarchived note.";
            Repaint();
        }

        private void DeleteNoteFromBrowser(PungentNote note)
        {
            if (note == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete Note", "Delete \"" + (string.IsNullOrWhiteSpace(note.title) ? "this note" : note.title) + "\"?", "Delete", "Cancel"))
                return;

            CommitDraftIfDirty("Saved draft before delete.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before delete.");
            bool wasSelected = _selectedNoteIds.Contains(note.id) || string.Equals(_selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase);
            PungentNoteStorage.Delete(note);
            if (wasSelected)
            {
                _selectedNoteIds.Remove(note.id);
                _selectedAuthoringItemKeys.RemoveWhere(key =>
                {
                    AuthoringBrowserItem item = _allAuthoringItemsCache.FirstOrDefault(candidate => candidate != null && string.Equals(candidate.key, key, StringComparison.OrdinalIgnoreCase));
                    return item != null && item.note != null && string.Equals(item.note.id, note.id, StringComparison.OrdinalIgnoreCase);
                });
                _selectedAuthoringItemKey = _selectedAuthoringItemKeys.FirstOrDefault() ?? string.Empty;
                if (string.Equals(_selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase))
                    _selectedNoteId = _selectedNoteIds.FirstOrDefault() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(_selectedNoteId))
                    PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
                LoadDraftForSelectedNote();
                SavePrefs();
                RefreshSelectionCache(true);
            }
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            _status = "Deleted note.";
            Repaint();
        }

        private void CopyNoteId(PungentNote note)
        {
            if (note == null)
                return;

            EditorGUIUtility.systemCopyBuffer = note.id ?? string.Empty;
            _status = "Copied note ID.";
            Repaint();
        }

        private void OpenNoteInBrowser(PungentNote note)
        {
            if (note == null)
                return;

            OpenAndSelect(note.id);
        }

        private void DuplicateSelectedNotesFromBrowser(List<PungentNote> selectedNotes)
        {
            CommitDraftIfDirty("Saved draft before duplicate.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before duplicate.");
            ReportBulk(PungentNoteBulkActions.Duplicate(selectedNotes), "duplicate");
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            Repaint();
        }

        private void ArchiveSelectedNotesFromBrowser(List<PungentNote> selectedNotes, bool archived)
        {
            CommitDraftIfDirty("Saved draft before archive change.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before archive change.");
            ReportBulk(PungentNoteBulkActions.Archive(selectedNotes, archived), archived ? "archive" : "unarchive");
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            Repaint();
        }

        private void DeleteSelectedNotesFromBrowser(List<PungentNote> selectedNotes)
        {
            if (selectedNotes == null || selectedNotes.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog("Delete Selected Notes", "Delete " + selectedNotes.Count + " selected notes?", "Delete", "Cancel"))
                return;

            CommitDraftIfDirty("Saved draft before delete.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before delete.");
            ReportBulk(PungentNoteBulkActions.Delete(selectedNotes), "delete");
            ClearSelectedNotes();
            PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
            _authoringItemsDirty = true;
            RefreshGuiCaches(true);
            _status = "Deleted selected notes.";
            Repaint();
        }

        private void SelectAllVisibleNotes()
        {
            CommitDraftIfDirty("Saved draft before multi-select.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before multi-select.");
            _selectedNoteIds.Clear();
            foreach (PungentNote note in _visibleNotesCache.Where(note => note != null && !string.IsNullOrWhiteSpace(note.id)))
                _selectedNoteIds.Add(note.id);
            _selectedNoteId = _visibleNotesCache.FirstOrDefault(note => note != null && !string.IsNullOrWhiteSpace(note.id))?.id ?? string.Empty;
            LoadDraftForSelectedNote();
            SavePrefs();
            RefreshSelectionCache(true);
            Repaint();
        }

        private void ClearSelectedNotes()
        {
            CommitDraftIfDirty("Saved draft before clearing selection.");
            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before clearing selection.");
            _selectedNoteIds.Clear();
            _selectedNoteId = string.Empty;
            _selectedAuthoringItemKeys.Clear();
            _selectedAuthoringItemKey = string.Empty;
            LoadDraftForSelectedNote();
            SavePrefs();
            RefreshSelectionCache(true);
            PungentStickyNoteOverlayController.CloseTransient(PungentStickyNoteOverlayOwner.BrowserWindow);
            Repaint();
        }

        private void DrawWorkspaceSelectionToolbar(List<PungentNote> visibleNotes)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(_selectedNoteIds.Count + " selected", UtilityWindowTheme.Cyan, 92f);
                if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(72f)))
                {
                    CommitDraftIfDirty("Saved draft before multi-select.");
                    _selectedNoteIds.Clear();
                    foreach (PungentNote note in visibleNotes)
                        _selectedNoteIds.Add(note.id);
                    _selectedNoteId = visibleNotes.FirstOrDefault()?.id ?? string.Empty;
                    LoadDraftForSelectedNote();
                    SavePrefs();
                    RefreshSelectionCache(true);
                }
                if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(86f)))
                {
                    CommitDraftIfDirty("Saved draft before clearing selection.");
                    _selectedNoteIds.Clear();
                    _selectedNoteId = string.Empty;
                    LoadDraftForSelectedNote();
                    SavePrefs();
                    RefreshSelectionCache(true);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void HandleNoteSelection(PungentNote note, List<PungentNote> visibleNotes, int visibleIndex, Event evt, Rect anchorRect)
        {
            if (note == null)
                return;

            if (evt != null && (evt.type == EventType.ContextClick || (evt.isMouse && evt.button != 0)))
            {
                ShowNoteRowMenu(note, anchorRect);
                evt.Use();
                return;
            }

            bool actionModifier = evt != null && (evt.control || evt.command);
            bool shift = evt != null && evt.shift;
            bool selectionWillChange = actionModifier ||
                                       shift ||
                                       !string.Equals(_selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase) ||
                                       !IsOnlySelectedNote(note) ||
                                       _editingFutureUtility != null;
            if (selectionWillChange)
            {
                CommitDraftIfDirty("Saved draft before selection change.");
                PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before selection change.");
            }

            if (shift && _lastClickedVisibleIndex >= 0 && visibleNotes != null && visibleNotes.Count > 0)
            {
                int start = Mathf.Clamp(Mathf.Min(_lastClickedVisibleIndex, visibleIndex), 0, visibleNotes.Count - 1);
                int end = Mathf.Clamp(Mathf.Max(_lastClickedVisibleIndex, visibleIndex), 0, visibleNotes.Count - 1);
                _selectedNoteIds.Clear();
                for (int i = start; i <= end; i++)
                    _selectedNoteIds.Add(visibleNotes[i].id);
                _selectedNoteId = note.id;
            }
            else if (actionModifier)
            {
                if (_selectedNoteIds.Contains(note.id))
                    _selectedNoteIds.Remove(note.id);
                else
                    _selectedNoteIds.Add(note.id);
                _selectedNoteId = note.id;
            }
            else
            {
                _selectedNoteIds.Clear();
                _selectedNoteIds.Add(note.id);
                _selectedNoteId = note.id;
            }

            _lastClickedVisibleIndex = visibleIndex;
            _editingFutureUtility = null;
            LoadDraftForSelectedNote();
            SavePrefs();
            RefreshSelectionCache(true);
            Repaint();
        }

        private void DrawBulkEditor(List<PungentNote> notes)
        {
            EditorGUILayout.HelpBox("Bulk Edit is limited to safe fields. Text fields are protected during bulk edit. Edit them one note at a time.", MessageType.Info);

            _bulkStatus = (PungentNoteStatus)EditorGUILayout.EnumPopup("Status", _bulkStatus);
            if (GUILayout.Button("Set Status"))
                ReportBulk(PungentNoteBulkActions.SetStatus(notes, _bulkStatus), "status");
            _bulkPriority = (PungentNotePriority)EditorGUILayout.EnumPopup("Priority", _bulkPriority);
            if (GUILayout.Button("Set Priority"))
                ReportBulk(PungentNoteBulkActions.SetPriority(notes, _bulkPriority), "priority");
            _bulkKind = (PungentNoteKind)EditorGUILayout.EnumPopup("Kind", _bulkKind);
            if (GUILayout.Button("Set Kind"))
                ReportBulk(PungentNoteBulkActions.SetKind(notes, _bulkKind), "kind");
            _bulkVisibility = (PungentNoteVisibility)EditorGUILayout.EnumPopup("Visibility", _bulkVisibility);
            if (GUILayout.Button("Set Visibility"))
                ReportBulk(PungentNoteBulkActions.SetVisibility(notes, _bulkVisibility), "visibility");
            _bulkDeveloperOnly = EditorGUILayout.Toggle("Developer Only", _bulkDeveloperOnly);
            if (GUILayout.Button("Set Developer Only"))
                ReportBulk(PungentNoteBulkActions.SetDeveloperOnly(notes, _bulkDeveloperOnly), "developer flag");
            _bulkUtilityId = DrawUtilityPopup("Registered Utility", _bulkUtilityId, true);
            if (GUILayout.Button("Set Utility Link"))
                ReportBulk(PungentNoteBulkActions.SetLinkedUtility(notes, _bulkUtilityId), "utility link");
            _bulkFutureUtilityId = DrawFutureUtilityPopup("Future Utility", _bulkFutureUtilityId, true);
            if (GUILayout.Button("Set Future Utility Link"))
                ReportBulk(PungentNoteBulkActions.SetLinkedFutureUtility(notes, _bulkFutureUtilityId), "future utility link");
            _bulkAuditCode = EditorGUILayout.TextField("Audit Issue Code", _bulkAuditCode);
            if (!string.IsNullOrWhiteSpace(_bulkAuditCode) && GUILayout.Button("Set Audit Issue Code"))
                ReportBulk(PungentNoteBulkActions.SetAuditIssueCode(notes, _bulkAuditCode), "audit code");

            _bulkTag = EditorGUILayout.TextField("Tag", _bulkTag);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Tag"))
                    ReportBulk(PungentNoteBulkActions.AddTag(notes, _bulkTag), "tag add");
                if (GUILayout.Button("Remove Tag"))
                    ReportBulk(PungentNoteBulkActions.RemoveTag(notes, _bulkTag), "tag remove");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Archive Selected"))
                    ReportBulk(PungentNoteBulkActions.Archive(notes, true), "archive");
                if (GUILayout.Button("Unarchive Selected"))
                    ReportBulk(PungentNoteBulkActions.Archive(notes, false), "unarchive");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Duplicate Selected"))
                    ReportBulk(PungentNoteBulkActions.Duplicate(notes), "duplicate");
                if (GUILayout.Button("Delete Selected") && EditorUtility.DisplayDialog("Delete Selected Notes", "Delete " + notes.Count + " selected notes?", "Delete", "Cancel"))
                {
                    ReportBulk(PungentNoteBulkActions.Delete(notes), "delete");
                    _selectedNoteIds.Clear();
                    _selectedNoteId = string.Empty;
                    LoadDraftForSelectedNote();
                    SavePrefs();
                    RefreshGuiCaches(true);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawImportedSourcesSidebar()
        {
            bool nextExpanded = EditorGUILayout.Foldout(_sourcesExpanded, "Imported Sources", true);
            if (nextExpanded != _sourcesExpanded)
            {
                _sourcesExpanded = nextExpanded;
                UtilityWindowPrefs.SetBool(PrefSourcesFoldout, _sourcesExpanded);
            }
            if (!_sourcesExpanded)
                return;

            foreach (PungentNoteImportSource source in PungentNoteStorage.Database.importSources.Where(s => s != null && !s.archived).ToList())
            {
                source.missing = !string.IsNullOrWhiteSpace(source.sourcePath) && !System.IO.File.Exists(source.sourcePath);
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(source.missing ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 1)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(source.displayName, EditorStyles.miniButton))
                        {
                            _filters.importSourceId = source.id;
                            _filters.tag = string.Empty;
                            SavePrefs();
                            RefreshGuiCaches(true);
                        }
                        UtilityWindowTheme.CountPill((source.noteIds == null ? 0 : source.noteIds.Count).ToString(), UtilityWindowTheme.Teal, 34f);
                    }
                    EditorGUILayout.LabelField((source.missing ? "Missing | " : string.Empty) + ShortDate(string.IsNullOrEmpty(source.lastRefreshedUtc) ? source.importedUtc : source.lastRefreshedUtc), UtilityWindowTheme.PathLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Reimport", EditorStyles.miniButton))
                        {
                            _pendingCsvPath = source.sourcePath;
                            _seedPreview.Clear();
                            _seedPreview.AddRange(PungentNoteImportSources.ReimportPreview(source));
                        }
                        if (GUILayout.Button("Reveal", EditorStyles.miniButton))
                            PungentNoteImportSources.Reveal(source);
                        if (GUILayout.Button("Remove", EditorStyles.miniButton) && EditorUtility.DisplayDialog("Remove Source Reference", "Remove the source reference? Notes will remain.", "Remove", "Cancel"))
                            PungentNoteImportSources.RemoveReference(source);
                    }
                }
            }
        }

        private void DrawTagsSidebar()
        {
            bool nextExpanded = EditorGUILayout.Foldout(_tagsExpanded, "Tags", true);
            if (nextExpanded != _tagsExpanded)
            {
                _tagsExpanded = nextExpanded;
                UtilityWindowPrefs.SetBool(PrefTagsFoldout, _tagsExpanded);
            }
            if (!_tagsExpanded)
                return;

            _tagSearch = EditorGUILayout.TextField("Find", _tagSearch);
            var tags = PungentNoteStorage.Database.notes
                .Where(n => n != null && n.tags != null)
                .SelectMany(n => n.tags)
                .Concat(PungentNoteStorage.Database.futureUtilities.Where(f => f != null && f.tags != null).SelectMany(f => f.tags))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Tag = g.Key, Count = g.Count() })
                .Where(g => string.IsNullOrWhiteSpace(_tagSearch) || g.Tag.IndexOf(_tagSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Tag)
                .Take(80);

            foreach (var tag in tags)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("#" + tag.Tag, EditorStyles.miniButton))
                    {
                        _filters.tag = tag.Tag;
                        _filters.importSourceId = string.Empty;
                        SavePrefs();
                        RefreshGuiCaches(true);
                    }
                    UtilityWindowTheme.CountPill(tag.Count.ToString(), UtilityWindowTheme.Teal, 34f);
                    if (_selectedNoteIds.Count > 1 && GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                        ReportBulk(PungentNoteBulkActions.AddTag(SelectedNotes, tag.Tag), "tag add");
                }
            }
        }

        private void ReportBulk(int count, string action)
        {
            _status = "Bulk " + action + " updated " + count + " note(s).";
            RefreshGuiCaches(true);
            PungentNoteSceneOverlay.InvalidateCache();
        }

        private void ClampPanelWidths()
        {
            float available = AvailablePanelWidth();
            float rightReserved = _propertiesCollapsed ? CollapsedRailWidth : MinRightWidth;
            float splitterReserve = SplitterWidth + (_propertiesCollapsed ? 0f : SplitterWidth);

            if (!_browserCollapsed)
            {
                float maxLeft = Mathf.Max(MinLeftWidth, available - MinWorkspaceWidth - rightReserved - splitterReserve);
                _leftWidth = Mathf.Clamp(_leftWidth, MinLeftWidth, maxLeft);
            }

            if (!_propertiesCollapsed)
            {
                float maxRight = Mathf.Max(MinRightWidth, available - EffectiveLeftWidth() - MinWorkspaceWidth - SplitterWidth);
                _rightWidth = Mathf.Clamp(_rightWidth, MinRightWidth, maxRight);
            }
        }

        private float AvailablePanelWidth()
        {
            return Mathf.Max(
                CollapsedRailWidth * 2f + MinWorkspaceWidth,
                position.width - BodyPaddingReserve);
        }

        private float GetWorkspaceWidth()
        {
            float splitters = (_browserCollapsed ? 0f : SplitterWidth) + (_propertiesCollapsed ? 0f : SplitterWidth);
            float width = AvailablePanelWidth() - EffectiveLeftWidth() - EffectiveRightWidth() - splitters;
            return Mathf.Max(MinWorkspaceWidth, width);
        }

        private float MaxLeftWidth()
        {
            float rightReserved = _propertiesCollapsed ? CollapsedRailWidth : MinRightWidth;
            float splitterReserve = SplitterWidth + (_propertiesCollapsed ? 0f : SplitterWidth);
            return Mathf.Max(
                MinLeftWidth,
                AvailablePanelWidth() - rightReserved - MinWorkspaceWidth - splitterReserve);
        }

        private float MaxRightWidth()
        {
            float splitterReserve = (_browserCollapsed ? 0f : SplitterWidth) + SplitterWidth;
            return Mathf.Max(
                MinRightWidth,
                AvailablePanelWidth() - EffectiveLeftWidth() - MinWorkspaceWidth - splitterReserve);
        }

        private float EffectiveLeftWidth()
        {
            return _browserCollapsed ? CollapsedRailWidth : _leftWidth;
        }

        private float EffectiveRightWidth()
        {
            return _propertiesCollapsed ? CollapsedRailWidth : _rightWidth;
        }

        private static string ShortDate(string utc)
        {
            return DateTime.TryParse(utc, out DateTime parsed) ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "unknown";
        }
    }
#endif
}
