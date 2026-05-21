namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.ProjectAudit;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentUtilityHelpBrowserWindow : EditorWindow
    {
        private enum ContentTab
        {
            QuickUseGuide,
            FeatureIndex,
            ScriptingIndex,
            Troubleshooting,
            Related
        }

        private enum HelpSidebarMode
        {
            AutoCompact,
            AlwaysOpen,
            Collapsed
        }

        private enum HelpSectionRailMode
        {
            VisibleSlim,
            Hidden,
            LabelsAlways,
            Proximity
        }

        private enum HelpOverlayKind
        {
            None,
            Sections,
            Related,
            Links,
            Notes,
            Requests,
            Developer
        }

        private enum CoverageFilter
        {
            All,
            ActionableOnly,
            MissingHeader,
            MissingSection,
            GeneratedReview,
            WeakTooltips,
            MissingDocs,
            MissingScripting,
            NotApplicable,
            Complete,
            DeveloperOnly
        }

        private enum CoverageSort
        {
            Priority,
            UtilityName,
            UtilityKind,
            GeneratedReview,
            TooltipCount,
            MissingDocs,
            CoverageStatus
        }

        private enum CoverageExportMode
        {
            Summary,
            ActionableIssues,
            GeneratedReviewQueue,
            TooltipReviewQueue,
            ScriptingReviewQueue,
            FullCsv,
            FullMarkdown
        }

        private struct HelpRailAnchor
        {
            public string label;
            public float progress;
            public bool selected;
            public Color tint;
            public Action action;
        }

        private const string PrefPrefix = "PungentFunkUtilities.HelpBrowser.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefLegacyUtility = PrefPrefix + "Utility";
        private const string PrefLegacyTopic = PrefPrefix + "Topic";
        private const string PrefSelection = PrefPrefix + "Selection";
        private const string PrefTab = PrefPrefix + "Tab";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefExpanded = PrefPrefix + "Expanded";
        private const string PrefShowGenerated = PrefPrefix + "ShowGenerated";
        private const string PrefShowHidden = PrefPrefix + "ShowHidden";
        private const string PrefShowDeveloperOnly = PrefPrefix + "ShowDeveloperOnly";
        private const string PrefShowNotes = PrefPrefix + "ShowNotes";
        private const string PrefShowBookmarks = PrefPrefix + "ShowBookmarks";
        private const string PrefShowGenerationDashboard = PrefPrefix + "ShowGenerationDashboard";
        private const string PrefShowDeveloperTools = PrefPrefix + "ShowDeveloperTools";
        private const string PrefCoverageFilter = PrefPrefix + "CoverageFilter";
        private const string PrefCoverageSort = PrefPrefix + "CoverageSort";
        private const string PrefReviewQueue = PrefPrefix + "ReviewQueue";
        private const string PrefCoverageExportMode = PrefPrefix + "CoverageExportMode";
        private const string PrefSidebarMode = PrefPrefix + "SidebarMode";
        private const string PrefSectionRailMode = PrefPrefix + "SectionRailMode";
        private const string PrefSectionFoldouts = PrefPrefix + "SectionFoldouts";
        private const string PrefDashboardSummaryOpen = PrefPrefix + "Dashboard.SummaryOpen";
        private const string PrefDashboardActionsOpen = PrefPrefix + "Dashboard.ActionsOpen";
        private const string PrefDashboardReleaseOpen = PrefPrefix + "Dashboard.ReleaseOpen";
        private const string PrefDashboardOnboardingOpen = PrefPrefix + "Dashboard.OnboardingOpen";
        private const string PrefDashboardCoverageOpen = PrefPrefix + "Dashboard.CoverageOpen";
        private const string PrefDashboardReviewOpen = PrefPrefix + "Dashboard.ReviewOpen";
        private const string PrefDashboardIssuesOpen = PrefPrefix + "Dashboard.IssuesOpen";
        private const string PrefDashboardCoverageHeight = PrefPrefix + "Dashboard.CoverageHeight";
        private const string PrefDashboardReviewHeight = PrefPrefix + "Dashboard.ReviewHeight";
        private const float NarrowBreakpoint = 760f;
        private const float MinLeftWidth = 210f;
        private const float MaxLeftWidth = 430f;
        private const float MinMainWidth = 420f;
        private const float SidebarPeekWidth = 84f;
        private const float SidebarPeekHoldSeconds = 0.34f;
        private const float SectionRailProximityWidth = 190f;
        private const float SectionRailLabelHoldSeconds = 0.28f;
        private const float SectionRailWidth = 28f;
        private const float CollapsedNavigationWidth = 38f;
        private const float NavigationDrawerHoldSeconds = 0.58f;
        private const float SectionRailLabelWidth = 126f;
        private const float HeaderNavigationHoldSeconds = 0.42f;
        private const float HeaderNavigationOverlayWidth = 440f;
        private const float HeaderNavigationOverlayHeight = 440f;

        private string _search = string.Empty;
        private PungentUtilityHelpNavigationSelection _selection = PungentUtilityHelpNavigationSelection.SearchResults();
        private ContentTab _tab = ContentTab.QuickUseGuide;
        private float _leftWidth = 260f;
        private bool _showGenerated = true;
        private bool _showHidden;
        private bool _showDeveloperOnly;
        private bool _showNotes = true;
        private bool _showBookmarks = true;
        private bool _showGenerationDashboard;
        private bool _showDeveloperTools;
        private CoverageFilter _coverageFilter = CoverageFilter.ActionableOnly;
        private CoverageSort _coverageSort = CoverageSort.Priority;
        private PungentUtilityHelpReviewQueueKind _reviewQueue = PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics;
        private CoverageExportMode _coverageExportMode = CoverageExportMode.Summary;
        private HelpSidebarMode _sidebarMode = HelpSidebarMode.AutoCompact;
        private HelpSectionRailMode _sectionRailMode = HelpSectionRailMode.VisibleSlim;
        private HelpOverlayKind _activeHelpOverlay = HelpOverlayKind.None;
        private Vector2 _navScroll;
        private Vector2 _contentScroll;
        private Vector2 _overlayScroll;
        private Vector2 _dashboardScroll;
        private Vector2 _reviewQueueScroll;
        private Rect _helpOverlayAnchorRect;
        private Rect _activeHelpOverlayRect;
        private Rect _lastNavigationAreaRect;
        private Rect _lastCollapsedNavigationRect;
        private Rect _lastNavigationDrawerRect;
        private Rect _lastSectionRailRect;
        private Rect _lastSectionRailLabelRect;
        private Rect _lastRightToolbarRect;
        private Rect _headerNavigationStripRect;
        private Rect _headerNavigationAnchorRect;
        private Rect _activeHeaderNavigationRect;
        private double _navigationPeekUntil;
        private double _headerNavigationOpenUntil;
        private double _sectionRailLabelsUntil;
        private float _contentViewportHeight;
        private float _articleContentHeight;
        private bool _draggingSectionRail;
        private bool _drawingNavigationOverlayDrawer;
        private string _openHeaderNavigationCategoryId = string.Empty;
        private Vector2 _headerNavigationScroll;
        private ContentTab? _hoveredRailSection;
        private string _hoveredRailAnchorLabel = string.Empty;
        private bool _dashboardSummaryOpen = true;
        private bool _dashboardActionsOpen = true;
        private bool _dashboardReleaseOpen = true;
        private bool _dashboardOnboardingOpen = true;
        private bool _dashboardCoverageOpen;
        private bool _dashboardReviewOpen;
        private bool _dashboardIssuesOpen;
        private float _dashboardCoverageHeight = 240f;
        private float _dashboardReviewHeight = 280f;
        private PungentUtilityHelpNavigationModel _navigation = new PungentUtilityHelpNavigationModel();
        private readonly HashSet<string> _expandedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _sectionFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ContentTab, Rect> _sectionRects = new Dictionary<ContentTab, Rect>();
        private readonly HashSet<string> _selectedReviewRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PungentUtilityHelpAnnotation> _annotationCache = new List<PungentUtilityHelpAnnotation>();
        private string _annotationCacheTopicId = string.Empty;
        private int _notesBookmarksCount;
        private bool _cacheDirty = true;
        private bool _annotationCacheDirty = true;
        private bool _notesBookmarksCountDirty = true;
        private bool _notesBookmarksBridgeStale;
        private ContentTab? _pendingSectionScroll;
        private string _status = "Ready.";

        public static void Open()
        {
            Open(null, null, null);
        }

        public static void Open(string utilityId, string sectionId = null, string topicId = null)
        {
            PungentUtilityHelpBrowserWindow window = GetWindow<PungentUtilityHelpBrowserWindow>("Help Browser");
            window.minSize = new Vector2(660f, 440f);
            window.SelectTopic(utilityId, sectionId, topicId);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Help Browser");
            PungentUtilityHelpRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityHelpRegistry.Changed += HandleRegistryChanged;
            PungentUtilityHelpNotesBridge.Changed -= HandleNotesChanged;
            PungentUtilityHelpNotesBridge.Changed += HandleNotesChanged;
            PungentUtilityHelpAnnotationStorage.Changed -= HandleLocalAnnotationsChanged;
            PungentUtilityHelpAnnotationStorage.Changed += HandleLocalAnnotationsChanged;

            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _selection = LoadSelection();
            _tab = LoadTab();
            _leftWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefLeftWidth, 260f), MinLeftWidth, MaxLeftWidth);
            _showGenerated = UtilityWindowPrefs.GetBool(PrefShowGenerated, true);
            _showHidden = UtilityWindowPrefs.GetBool(PrefShowHidden, false);
            _showDeveloperOnly = UtilityWindowPrefs.GetBool(PrefShowDeveloperOnly, false);
            _showNotes = UtilityWindowPrefs.GetBool(PrefShowNotes, true);
            _showBookmarks = UtilityWindowPrefs.GetBool(PrefShowBookmarks, true);
            _showGenerationDashboard = UtilityWindowPrefs.GetBool(PrefShowGenerationDashboard, false);
            _showDeveloperTools = UtilityWindowPrefs.GetBool(PrefShowDeveloperTools, false);
            _coverageFilter = LoadEnum(PrefCoverageFilter, CoverageFilter.ActionableOnly);
            _coverageSort = LoadEnum(PrefCoverageSort, CoverageSort.Priority);
            _reviewQueue = LoadEnum(PrefReviewQueue, PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics);
            _coverageExportMode = LoadEnum(PrefCoverageExportMode, CoverageExportMode.Summary);
            _sidebarMode = LoadEnum(PrefSidebarMode, HelpSidebarMode.AutoCompact);
            _sectionRailMode = LoadEnum(PrefSectionRailMode, HelpSectionRailMode.VisibleSlim);
            _dashboardSummaryOpen = UtilityWindowPrefs.GetBool(PrefDashboardSummaryOpen, true);
            _dashboardActionsOpen = UtilityWindowPrefs.GetBool(PrefDashboardActionsOpen, true);
            _dashboardReleaseOpen = UtilityWindowPrefs.GetBool(PrefDashboardReleaseOpen, true);
            _dashboardOnboardingOpen = UtilityWindowPrefs.GetBool(PrefDashboardOnboardingOpen, true);
            _dashboardCoverageOpen = UtilityWindowPrefs.GetBool(PrefDashboardCoverageOpen, _coverageFilter == CoverageFilter.ActionableOnly || _coverageFilter == CoverageFilter.MissingHeader || _coverageFilter == CoverageFilter.MissingSection || _coverageFilter == CoverageFilter.MissingScripting);
            _dashboardReviewOpen = UtilityWindowPrefs.GetBool(PrefDashboardReviewOpen, false);
            _dashboardIssuesOpen = UtilityWindowPrefs.GetBool(PrefDashboardIssuesOpen, false);
            _dashboardCoverageHeight = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefDashboardCoverageHeight, 240f), 160f, 520f);
            _dashboardReviewHeight = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefDashboardReviewHeight, 280f), 180f, 600f);
            LoadExpandedNodes();
            LoadSectionFoldouts();
            MarkCacheDirty();
        }

        private void OnDisable()
        {
            PungentUtilityHelpRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityHelpNotesBridge.Changed -= HandleNotesChanged;
            PungentUtilityHelpAnnotationStorage.Changed -= HandleLocalAnnotationsChanged;
            SavePrefs();
        }

        private void HandleRegistryChanged()
        {
            MarkCacheDirty();
            Repaint();
        }

        private void HandleNotesChanged()
        {
            _annotationCacheDirty = true;
            _notesBookmarksCountDirty = true;
            Repaint();
        }

        private void HandleLocalAnnotationsChanged()
        {
            _annotationCacheDirty = true;
            _notesBookmarksCountDirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            PungentEditorPerformanceUtility.RecordWindowRepaint(this);
            RefreshCachesIfNeeded();
            HandleActiveHelpOverlayInput();
            HandleHeaderNavigationOverlayInput();

            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "help-browser",
                Title = "Pungent Help Browser",
                Description = "Browse utility documentation by category, module, utility, topic, scripting reference, and related notes.",
                Status = _status,
                HelpSectionId = "overview",
                HelpTopicId = "overview",
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });

            DrawToolbar();

            if (position.width < NarrowBreakpoint)
                DrawNarrowLayout();
            else
                DrawWideLayout();

            DrawHeaderNavigationOverlay();
            DrawHelpOverlayTrays(GetSelectedTopic());
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.075f, 0.032f, 6, 4)))
            {
                bool compact = position.width < 860f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Search", "Filter the documentation directory while preserving visible category and utility context."), GUILayout.Width(54f));
                    EditorGUI.BeginChangeCheck();
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selection = string.IsNullOrWhiteSpace(_search) ? _selection : PungentUtilityHelpNavigationSelection.SearchResults();
                        MarkCacheDirty();
                        SavePrefs();
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear help search text."), EditorStyles.miniButton, GUILayout.Width(50f)))
                        {
                            _search = string.Empty;
                            MarkCacheDirty();
                            SavePrefs();
                        }
                    }

                    GUILayout.Space(8f);
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("help-browser", "overview", "overview", "Open help for the Help Browser overview.", "Help Browser header");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawNotesBookmarksAccessButton(GetSelectedTopic(), compact);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(CountUtilities() + " utilities", UtilityWindowTheme.Cyan, 92f);
                    UtilityWindowTheme.CountPill(_navigation.searchTopics.Count + " topics", UtilityWindowTheme.Teal, 82f);
                }

                DrawHeaderNavigationStrip(compact);
            }
        }

        private void DrawHeaderNavigationStrip(bool compact)
        {
            using (EditorGUILayout.HorizontalScope scope = new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.045f, 0.018f, 4, 2), GUILayout.MinHeight(30f)))
            {
                if (Event.current.type == EventType.Repaint)
                    _headerNavigationStripRect = scope.rect;

                DrawHeaderNavigationLink("Home", "Open the documentation home and current search results.", _selection == null || _selection.kind == PungentUtilityHelpSelectionKind.SearchResults, UtilityWindowTheme.Teal, () =>
                {
                    CloseHeaderNavigationOverlay();
                    SelectIfDifferent(PungentUtilityHelpNavigationSelection.SearchResults());
                }, compact ? 48f : 58f);

                float reservedForActions = GetSelectedTopic() == null ? 128f : (compact ? 230f : 360f);
                int categoryLimit = Mathf.Clamp(Mathf.FloorToInt((position.width - reservedForActions) / (compact ? 92f : 126f)), compact ? 2 : 3, compact ? 4 : 7);
                int shown = 0;
                foreach (PungentUtilityHelpCategoryNode category in _navigation.categories)
                {
                    if (category == null || shown >= categoryLimit)
                        break;

                    shown++;
                    DrawHeaderCategoryLink(category, compact);
                }

                if (_navigation.categories.Count > categoryLimit)
                    DrawHeaderOverflowLink(compact);

                GUILayout.FlexibleSpace();
                DrawHeaderActionToolbar(GetSelectedTopic(), compact);
            }
        }

        private void DrawHeaderCategoryLink(PungentUtilityHelpCategoryNode category, bool compact)
        {
            if (category == null)
                return;

            bool selected = IsSelectionInside(category);
            string label = compact ? ShortenForHeader(category.displayName, 12) : category.displayName;
            float width = Mathf.Clamp(label.Length * 7f + 24f, compact ? 58f : 74f, compact ? 104f : 148f);
            Rect rect = DrawHeaderNavigationLink(label, "Open " + category.displayName + " documentation.", selected, UtilityWindowTheme.Cyan, null, width);

            if (rect.Contains(Event.current.mousePosition))
                OpenHeaderNavigationOverlay(category.id, rect, repaintOnly: true);
            if (HandleClick(rect, "Open " + category.displayName + " documentation."))
                OpenHeaderNavigationOverlay(category.id, rect);
        }

        private void DrawHeaderOverflowLink(bool compact)
        {
            Rect rect = DrawHeaderNavigationLink(compact ? "More" : "More...", "Show the remaining documentation categories.", false, UtilityWindowTheme.Cyan, null, compact ? 58f : 72f);

            if (rect.Contains(Event.current.mousePosition))
                OpenHeaderNavigationOverlay(string.Empty, rect, repaintOnly: true);
            if (HandleClick(rect, "Show the remaining documentation categories."))
                OpenHeaderNavigationOverlay(string.Empty, rect);
        }

        private Rect DrawHeaderNavigationLink(string label, string tooltip, bool selected, Color tint, Action action, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 24f, GUILayout.Width(width), GUILayout.Height(24f));
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (hovered || selected)
                {
                    Color fill = new Color(tint.r, tint.g, tint.b, selected ? 0.105f : 0.052f);
                    EditorGUI.DrawRect(rect, fill);
                }

                Rect underline = new Rect(rect.x + 5f, rect.yMax - 2f, rect.width - 10f, selected ? 2f : hovered ? 1f : 0f);
                if (underline.height > 0f)
                    EditorGUI.DrawRect(underline, new Color(tint.r, tint.g, tint.b, selected ? 0.92f : 0.48f));

                GUIStyle style = new GUIStyle(selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(4, 4, 0, 0)
                };
                GUI.Label(rect, new GUIContent(label, tooltip), style);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (action != null && HandleClick(rect, tooltip))
                action?.Invoke();

            return rect;
        }

        private void DrawHeaderActionToolbar(PungentUtilityHelpTopic topic, bool compact)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(26f)))
            {
                DrawOverlayIconButton(compact ? "Req" : "Request", "Req", "Create or open support requests for this documentation context.", HelpOverlayKind.Requests);
                if (topic != null)
                {
                    DrawOverlayIconButton(compact ? "TOC" : "Sections", "TOC", "Show page sections and rail settings.", HelpOverlayKind.Sections);
                    DrawOverlayIconButton(compact ? "Rel" : "Related", "Rel", "Show related topics and utilities.", HelpOverlayKind.Related);
                    DrawOverlayIconButton(compact ? "Doc" : "Links", "Doc", "Show documentation links for this topic and utility.", HelpOverlayKind.Links);
                    DrawOverlayIconButton(compact ? "Note" : "Notes", "Note", "Show notes and bookmarks for this topic.", HelpOverlayKind.Notes);
                }
            }
        }

        private static string ShortenForHeader(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value ?? string.Empty;
            return value.Substring(0, Mathf.Max(1, maxLength - 1)) + "...";
        }

        private void OpenHeaderNavigationOverlay(string categoryId, Rect anchorRect, bool repaintOnly = false)
        {
            bool changed = !string.Equals(_openHeaderNavigationCategoryId, categoryId ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                           _activeHeaderNavigationRect.width <= 0f;
            _openHeaderNavigationCategoryId = categoryId ?? string.Empty;
            _headerNavigationAnchorRect = anchorRect;
            _headerNavigationOpenUntil = EditorApplication.timeSinceStartup + HeaderNavigationHoldSeconds;
            if (changed)
                _headerNavigationScroll = Vector2.zero;
            if (changed || !repaintOnly)
                Repaint();
        }

        private void CloseHeaderNavigationOverlay()
        {
            _openHeaderNavigationCategoryId = string.Empty;
            _headerNavigationAnchorRect = Rect.zero;
            _activeHeaderNavigationRect = Rect.zero;
            _headerNavigationOpenUntil = 0d;
            Repaint();
        }

        private void HandleHeaderNavigationOverlayInput()
        {
            Event evt = Event.current;
            if (evt == null || string.IsNullOrEmpty(_openHeaderNavigationCategoryId) && _activeHeaderNavigationRect.width <= 0f)
                return;

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CloseHeaderNavigationOverlay();
                evt.Use();
                return;
            }

            Vector2 mouse = evt.mousePosition;
            bool overStrip = _headerNavigationStripRect.width > 0f && _headerNavigationStripRect.Contains(mouse);
            bool overOverlay = _activeHeaderNavigationRect.width > 0f && _activeHeaderNavigationRect.Contains(mouse);
            bool overAnchor = _headerNavigationAnchorRect.width > 0f && _headerNavigationAnchorRect.Contains(mouse);
            double now = EditorApplication.timeSinceStartup;
            bool wasOpen = now <= _headerNavigationOpenUntil;

            if (overStrip || overOverlay || overAnchor)
                _headerNavigationOpenUntil = now + HeaderNavigationHoldSeconds;
            else if (evt.type == EventType.MouseDown && wasOpen)
            {
                CloseHeaderNavigationOverlay();
                evt.Use();
                return;
            }

            bool isOpen = EditorApplication.timeSinceStartup <= _headerNavigationOpenUntil;
            if (wasOpen != isOpen && evt.type != EventType.Layout)
                Repaint();
        }

        private void DrawHeaderNavigationOverlay()
        {
            if (EditorApplication.timeSinceStartup > _headerNavigationOpenUntil)
                return;

            Rect rect = CalculateHeaderNavigationOverlayRect();
            _activeHeaderNavigationRect = rect;
            DrawHelpOverlayChrome(rect);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            try
            {
                DrawHeaderNavigationOverlayContent();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private Rect CalculateHeaderNavigationOverlayRect()
        {
            float width = Mathf.Min(HeaderNavigationOverlayWidth, Mathf.Max(260f, position.width - 24f));
            float height = Mathf.Min(HeaderNavigationOverlayHeight, Mathf.Max(220f, position.height - _headerNavigationAnchorRect.yMax - 18f));
            float x = _headerNavigationAnchorRect.width > 0f ? _headerNavigationAnchorRect.x : 10f;
            float y = _headerNavigationStripRect.height > 0f ? _headerNavigationStripRect.yMax + 4f : 126f;
            x = Mathf.Clamp(x, 8f, Mathf.Max(8f, position.width - width - 8f));
            y = Mathf.Clamp(y, 86f, Mathf.Max(86f, position.height - height - 8f));
            return new Rect(x, y, width, height);
        }

        private void DrawHeaderNavigationOverlayContent()
        {
            PungentUtilityHelpCategoryNode category = string.IsNullOrEmpty(_openHeaderNavigationCategoryId)
                ? null
                : _navigation.FindCategory(_openHeaderNavigationCategoryId);

            using (new EditorGUILayout.HorizontalScope())
            {
                string title = category == null ? "Documentation Directory" : category.displayName;
                EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (DrawTextLink("Close", "Close navigation menu.", false, EditorStyles.miniLabel, GUILayout.Width(46f)))
                    CloseHeaderNavigationOverlay();
            }

            EditorGUILayout.Space(2f);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f)), new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.42f));
            _headerNavigationScroll = EditorGUILayout.BeginScrollView(_headerNavigationScroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.ExpandHeight(true));
            try
            {
                if (category == null)
                {
                    DrawHeaderNavOverlayRow("Directory Home", CountUtilities() + " utilities / " + _navigation.searchTopics.Count + " topics", "Open documentation home.", _selection == null || _selection.kind == PungentUtilityHelpSelectionKind.SearchResults, UtilityWindowTheme.Teal, () =>
                    {
                        SelectIfDifferent(PungentUtilityHelpNavigationSelection.SearchResults());
                        CloseHeaderNavigationOverlay();
                    });

                    foreach (PungentUtilityHelpCategoryNode item in _navigation.categories)
                        DrawHeaderNavOverlayCategorySummary(item);
                    return;
                }

                DrawHeaderNavOverlayRow(category.displayName, CountTopics(category) + " topics across " + category.subcategories.Count + " modules", "Open category.", IsSelectionInside(category), UtilityWindowTheme.Cyan, () =>
                {
                    SelectIfDifferent(PungentUtilityHelpNavigationSelection.Category(category.id));
                    CloseHeaderNavigationOverlay();
                });

                foreach (PungentUtilityHelpSubcategoryNode subcategory in category.subcategories)
                    DrawHeaderNavOverlaySubcategory(subcategory);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHeaderNavOverlayCategorySummary(PungentUtilityHelpCategoryNode category)
        {
            if (category == null)
                return;

            DrawHeaderNavOverlayRow(category.displayName, CountTopics(category) + " topics across " + category.subcategories.Count + " modules", "Open " + category.displayName + ".", IsSelectionInside(category), UtilityWindowTheme.Cyan, () =>
            {
                OpenHeaderNavigationOverlay(category.id, _headerNavigationAnchorRect);
                SelectIfDifferent(PungentUtilityHelpNavigationSelection.Category(category.id));
            });
        }

        private void DrawHeaderNavOverlaySubcategory(PungentUtilityHelpSubcategoryNode subcategory)
        {
            if (subcategory == null)
                return;

            DrawHeaderNavOverlayRow(subcategory.displayName, CountTopics(subcategory) + " topics / " + subcategory.utilities.Count + " utilities", "Open " + subcategory.displayName + ".", IsSelectionInside(subcategory), UtilityWindowTheme.Blue, () =>
            {
                SelectIfDifferent(PungentUtilityHelpNavigationSelection.Subcategory(subcategory.categoryId, subcategory.id));
                CloseHeaderNavigationOverlay();
            });

            foreach (PungentUtilityHelpUtilityNode utility in subcategory.utilities)
            {
                DrawHeaderNavOverlayRow("  " + utility.displayName, utility.topics.Count + " topics", string.IsNullOrWhiteSpace(utility.summary) ? "Open utility help." : utility.summary, IsSelectionInside(utility), UtilityWindowTheme.Teal, () =>
                {
                    SelectIfDifferent(PungentUtilityHelpNavigationSelection.Utility(utility.categoryId, utility.subcategoryId, utility.id));
                    CloseHeaderNavigationOverlay();
                });

                foreach (PungentUtilityHelpTopic topic in utility.topics)
                {
                    PungentUtilityHelpTopic captured = topic;
                    PungentUtilityHelpNavigationSelection target = PungentUtilityHelpNavigationSelection.Topic(captured, utility);
                    DrawHeaderNavOverlayRow("    " + (string.IsNullOrWhiteSpace(captured.title) ? captured.topicId : captured.title), TopicStateLabel(captured), "Open topic.", _selection != null && _selection.SameTarget(target), captured.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, () =>
                    {
                        SelectIfDifferent(target);
                        CloseHeaderNavigationOverlay();
                    }, 24f);
                }
            }
        }

        private void DrawHeaderNavOverlayRow(string title, string meta, string tooltip, bool selected, Color tint, Action action, float height = 30f)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                DrawInteractiveSurface(rect, tint, selected, hovered);
                Rect accent = new Rect(rect.x + 5f, rect.y + 5f, 2f, rect.height - 10f);
                EditorGUI.DrawRect(accent, new Color(tint.r, tint.g, tint.b, selected ? 0.86f : hovered ? 0.46f : 0.22f));
                GUIStyle titleStyle = new GUIStyle(selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                GUIStyle metaStyle = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip
                };
                GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, Mathf.Max(80f, rect.width - 132f), rect.height - 8f), new GUIContent(title, tooltip), titleStyle);
                if (!string.IsNullOrWhiteSpace(meta))
                    GUI.Label(new Rect(rect.xMax - 122f, rect.y + 5f, 112f, rect.height - 10f), new GUIContent(meta, tooltip), metaStyle);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (HandleClick(rect, tooltip))
                action?.Invoke();
        }

        private void DrawNotesBookmarksAccessButton(PungentUtilityHelpTopic topic, bool compact)
        {
            string tooltip = "View all Help Browser notes, bookmarks, and topic annotations.";
            string label = compact ? "Notes" : "Notes & Bookmarks";
            float width = compact ? 58f : 134f;
            if (GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Width(width)))
                OpenNotesBookmarksTray(topic);

            string countLabel = _notesBookmarksBridgeStale ? "!" : GetNotesBookmarksCount().ToString();
            UtilityWindowTheme.InfoPill(new GUIContent(countLabel, _notesBookmarksBridgeStale ? "Notes bridge count could not be refreshed. Local bookmarks remain available." : tooltip), _notesBookmarksBridgeStale ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 38f);
        }

        private void OpenNotesBookmarksTray(PungentUtilityHelpTopic topic = null)
        {
            PungentUtilityHelpNotesBookmarksTray.Show(PopupRect(), BuildNotesBookmarksContext(topic), () =>
            {
                _annotationCacheDirty = true;
                _notesBookmarksCountDirty = true;
                Repaint();
            });
        }

        private PungentUtilityHelpContext BuildNotesBookmarksContext(PungentUtilityHelpTopic topic = null)
        {
            topic = topic ?? GetSelectedTopic();
            string utilityId = topic != null ? topic.utilityId : _selection == null ? string.Empty : _selection.utilityId;
            string sectionId = topic != null ? topic.sectionId : PungentUtilityHelpIds.DefaultSection;
            string topicId = topic != null ? topic.topicId : PungentUtilityHelpIds.DefaultTopic;
            PungentUtilityHelpContext context = new PungentUtilityHelpContext
            {
                utilityId = utilityId,
                sectionId = sectionId,
                topicId = topicId,
                label = topic == null ? "Help Browser Notes & Bookmarks" : topic.title,
                location = "Help Browser"
            };
            context.Normalize();
            return context;
        }

        private int GetNotesBookmarksCount()
        {
            if (!_notesBookmarksCountDirty)
                return _notesBookmarksCount;

            _notesBookmarksCountDirty = false;
            _notesBookmarksBridgeStale = false;
            int localCount = PungentUtilityHelpAnnotationStorage.instance.bookmarks == null
                ? 0
                : PungentUtilityHelpAnnotationStorage.instance.bookmarks.Count(b => b != null);
            int bridgeCount = 0;
            if (PungentUtilityHelpNotesBridge.CountAllHelpNotes != null)
            {
                try
                {
                    bridgeCount = PungentUtilityHelpNotesBridge.CountAllHelpNotes();
                }
                catch
                {
                    _notesBookmarksBridgeStale = true;
                }
            }

            _notesBookmarksCount = localCount + bridgeCount;
            return _notesBookmarksCount;
        }

        private void DrawDeveloperToolsToggle()
        {
            // Developer controls moved to PungentUtilityDeveloperToolsWindow.
        }

        private void DrawDeveloperFilterRow()
        {
            // Developer filters and generation actions moved to PungentUtilityDeveloperToolsWindow.
        }

        private void DrawFilterToggle(ref bool value, string label, string tooltip)
        {
            EditorGUI.BeginChangeCheck();
            value = GUILayout.Toggle(value, new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Width(128f));
            if (EditorGUI.EndChangeCheck())
            {
                MarkCacheDirty();
                SavePrefs();
            }
        }

        private void DrawGenerationDashboard()
        {
            // Generated-help maintenance is owned by PungentUtilityDeveloperToolsWindow.Generation.
        }

        private void DrawDashboardFoldout(string title, ref bool open, Action drawContent, string pill = null)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.03f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    open = EditorGUILayout.Foldout(open, title, true);
                    GUILayout.FlexibleSpace();
                    if (!string.IsNullOrWhiteSpace(pill))
                        UtilityWindowTheme.InfoPill(new GUIContent(pill, "Section count."), UtilityWindowTheme.Cyan, Mathf.Clamp(pill.Length * 7f + 30f, 42f, 92f));
                }

                if (open)
                    drawContent?.Invoke();
            }
        }
        private void DrawDashboardSummaryCards(PungentUtilityHelpGenerationReport report)
        {
            int actionableHeaders = report.coverageRows.Count(r => r != null && r.missingHeaderIsActionable);
            int actionableSections = report.coverageRows.Count(r => r != null && r.missingSectionIsActionable);
            int actionableTooltips = report.coverageRows.Count(r => r != null && r.missingTooltipIsActionable);
            int actionableScripting = report.coverageRows.Count(r => r != null && r.missingScriptingIsActionable);
            int docIssues = report.documentationLinksMissingCurrentTargets + report.staleRelatedDocumentationLinkIds;
            int complete = report.coverageRows.Count(r => r != null && r.coverageStatus == PungentUtilityHelpCoverageStatus.Complete);
            int acceptable = report.coverageRows.Count(r => r != null && (r.coverageStatus == PungentUtilityHelpCoverageStatus.Acceptable || r.coverageExpectation == PungentUtilityHelpCoverageExpectation.NotApplicable));

            DrawDashboardSummaryCard(
                "Required Coverage",
                "Header, section, tooltip, and scripting gaps that still need action or a not-applicable decision.",
                UtilityWindowTheme.Blue,
                new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(actionableHeaders + " header", "Window/popup utilities that still need header help or an accepted N/A decision."), actionableHeaders == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusCoverageFilter(CoverageFilter.MissingHeader)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(actionableSections + " section", "Utilities that likely need section-level contextual help."), actionableSections == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusCoverageFilter(CoverageFilter.MissingSection)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(actionableTooltips + " tooltips", "Window utilities with missing or weak Controls & Tooltips coverage."), actionableTooltips == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusCoverageFilter(CoverageFilter.WeakTooltips)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(actionableScripting + " API", "Scripting-facing utilities/topics still missing reference coverage."), actionableScripting == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusCoverageFilter(CoverageFilter.MissingScripting))
                },
                "Filter coverage",
                () => FocusCoverageFilter(CoverageFilter.ActionableOnly));

            DrawDashboardSummaryCard(
                "Generated Content",
                "Generated topics and entries are draft material until reviewed, promoted, hidden, or ignored.",
                UtilityWindowTheme.Purple,
                new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.generatedDraftTopics + " drafts", "Generated or developer-only topics awaiting curation."), report.generatedDraftTopics == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.generatedEntriesAwaitingReview + " review", "Generated entries awaiting developer review."), report.generatedEntriesAwaitingReview == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.GeneratedTooltipEntries)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.weakTooltipEntries + " weak", "Tooltip entries with low confidence, short wording, or review flags."), report.weakTooltipEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.WeakTooltips)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.staleScriptingEntries + " stale API", "Generated API entries whose source member no longer exists."), report.staleScriptingEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.GeneratedScriptingEntries))
                },
                "Open review",
                () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics));

            DrawDashboardSummaryCard(
                "Documentation Links",
                "Utility-assigned, global, and explicit related documentation links surfaced in Help Browser.",
                UtilityWindowTheme.Cyan,
                new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.documentationLinkCount + " links", "Total Documentation Links records."), report.documentationLinkCount > 0 ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, () => DocumentationLinkEditorPopup.Open()),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.documentationLinksMissingCurrentTargets + " missing target", "Links without a usable current target."), report.documentationLinksMissingCurrentTargets == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => DocumentationLinkEditorPopup.Open()),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.staleRelatedDocumentationLinkIds + " stale refs", "Help topics referencing missing documentation link IDs."), report.staleRelatedDocumentationLinkIds == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.DocumentationLinkIssues)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.utilitiesWithDocumentationLinks + " utilities", "Registered utilities with at least one assigned documentation link."), report.utilitiesWithDocumentationLinks > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, () => DocumentationLinkEditorPopup.Open())
                },
                "Manage links",
                () => DocumentationLinkEditorPopup.Open());

            DrawDashboardSummaryCard(
                "Release Readiness",
                "Readiness separates complete, acceptable/not-applicable, actionable, and relay-backed support systems.",
                UtilityWindowTheme.Teal,
                new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(complete + " complete", "Utilities with complete help coverage by current rules."), complete > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, () => FocusCoverageFilter(CoverageFilter.Complete)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(acceptable + " acceptable", "Utilities accepted as complete enough or not applicable by type/decision."), UtilityWindowTheme.Teal, () => FocusCoverageFilter(CoverageFilter.NotApplicable)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(docIssues + " doc issues", "Documentation Links missing targets or stale topic references."), docIssues == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.DocumentationLinkIssues)),
                    new UtilityWindowTheme.PillSpec(new GUIContent("Bug relay", "Bug report UI posts to the configured Wix relay; Discord delivery stays server-side."), UtilityWindowTheme.Teal, () => PungentUtilityHelpRegistry.Open("help-browser", "bug-reporting", "bug-reporting"))
                },
                "Read details",
                () => { _dashboardReleaseOpen = true; SavePrefs(); });

            DrawDashboardSummaryCard(
                "Review Debt",
                "The shortest path to release hardening: review generated drafts, weak tooltips, stale API entries, and docs issues.",
                UtilityWindowTheme.Amber,
                new[]
                {
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.generatedEntriesAwaitingReview + " generated", "Generated entries still awaiting a review decision."), report.generatedEntriesAwaitingReview == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, () => FocusCoverageFilter(CoverageFilter.GeneratedReview)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.weakTooltipEntries + " weak tooltips", "Weak tooltip rows needing wording or ignore decisions."), report.weakTooltipEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.WeakTooltips)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.hiddenEntries + " hidden", "Hidden generated topics/entries."), report.hiddenEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, () => FocusReviewQueue(PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries)),
                    new UtilityWindowTheme.PillSpec(new GUIContent(report.developerOnlyEntries + " dev-only", "Developer-only help topics/entries not visible in normal help."), report.developerOnlyEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, () => FocusCoverageFilter(CoverageFilter.DeveloperOnly))
                },
                "Export queue",
                () => CopyCoverageExport(report, PungentUtilityHelpReviewQueue.BuildRows(report), CoverageExportMode.ActionableIssues));
        }

        private void DrawDashboardSummaryCard(string title, string summary, Color tint, IReadOnlyList<UtilityWindowTheme.PillSpec> pills, string actionLabel, Action action)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    if (!string.IsNullOrWhiteSpace(actionLabel) && GUILayout.Button(new GUIContent(actionLabel, summary), EditorStyles.miniButton, GUILayout.Width(108f)))
                        action?.Invoke();
                }

                EditorGUILayout.LabelField(summary, UtilityWindowTheme.MutedMiniLabelStyle);
                UtilityWindowTheme.DrawWrappedPillGrid(pills, position.width - 72f);
            }
        }

        private void DrawDashboardMaintenanceActions(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.08f, 0.04f, 5, 3)))
            {
                EditorGUILayout.LabelField("Run explicit generation or export work. These actions are button-driven and do not run from repaint.", UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Generate Registry Overview Topics", "Create generated draft overview topics from utility registry metadata."), EditorStyles.miniButton, GUILayout.Width(210f)))
                    {
                        PungentUtilityHelpTopicGenerator.GenerateRegistryOverviewTopics(out _status);
                        MarkCacheDirty();
                    }

                    if (GUILayout.Button(new GUIContent("Generate Missing Contextual Topic Stubs", "Scan contextual help button usage and create generated draft topics for missing destinations."), EditorStyles.miniButton, GUILayout.Width(240f)))
                    {
                        PungentUtilityHelpTopicGenerator.GenerateMissingContextualTopicStubs(out _status);
                        MarkCacheDirty();
                    }

                    if (GUILayout.Button(new GUIContent("Refresh Tooltip Index", "Scan editor source for GUIContent tooltips and generate Controls & Tooltips draft topics."), EditorStyles.miniButton, GUILayout.Width(150f)))
                    {
                        int count = PungentUtilityHelpTooltipIndexer.RefreshTooltipIndex(out _status);
                        if (count == 0)
                            _status = "Tooltip index refreshed; no GUIContent tooltips matched the current rules.";
                        MarkCacheDirty();
                    }
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(report.staleScriptingEntries == 0))
                    {
                        if (GUILayout.Button(new GUIContent("Remove Stale Generated Entries", "Remove stale generated scripting entries after confirmation."), EditorStyles.miniButton, GUILayout.Width(190f)))
                        {
                            if (EditorUtility.DisplayDialog("Remove stale generated help?", "Remove stale generated scripting entries that no longer match source members?", "Remove", "Cancel"))
                            {
                                PungentUtilityHelpAutoIndexer.RemoveStaleGeneratedEntries(out _status);
                                MarkCacheDirty();
                            }
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Open Documentation Links", "Open the Documentation Links utility."), EditorStyles.miniButton, GUILayout.Width(156f)))
                        DocumentationLinkEditorPopup.Open();

                    _coverageExportMode = (CoverageExportMode)EditorGUILayout.EnumPopup(new GUIContent("Export", "Choose coverage export mode."), _coverageExportMode, GUILayout.Width(210f));
                    if (GUILayout.Button(new GUIContent("Copy Export", "Copy the selected help coverage export to the clipboard."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        CopyCoverageExport(report, reviewRows, _coverageExportMode);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField(PungentUtilityHelpStorage.instance.lastGeneratedStatus, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawDashboardRawIssues(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.03f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _coverageExportMode = (CoverageExportMode)EditorGUILayout.EnumPopup(new GUIContent("Export", "Choose coverage export mode."), _coverageExportMode, GUILayout.Width(230f));
                    if (GUILayout.Button(new GUIContent("Copy Export", "Copy the selected help coverage export to the clipboard."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        CopyCoverageExport(report, reviewRows, _coverageExportMode);
                    GUILayout.FlexibleSpace();
                }

                if (report.issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("No raw coverage issues are currently reported by cached data.", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("Top Coverage Issues", UtilityWindowTheme.MutedMiniLabelStyle);
                foreach (PungentUtilityHelpGenerationIssue issue in report.issues.Take(12))
                    EditorGUILayout.LabelField(issue.kind + ": " + issue.topicStableId + " - " + issue.message, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private int CountOnboardingRows(PungentUtilityHelpGenerationReport report)
        {
            return report == null || report.coverageRows == null ? 0 : report.coverageRows.Count(r => r != null && ShouldShowOnboardingRow(r));
        }

        private void FocusCoverageFilter(CoverageFilter filter)
        {
            _coverageFilter = filter;
            _dashboardCoverageOpen = true;
            SavePrefs();
        }

        private void FocusReviewQueue(PungentUtilityHelpReviewQueueKind queue)
        {
            _reviewQueue = queue;
            _dashboardReviewOpen = true;
            SavePrefs();
        }

        private void CopyCoverageExport(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows, CoverageExportMode mode)
        {
            EditorGUIUtility.systemCopyBuffer = BuildCoverageExport(report, reviewRows, mode);
            _status = "Copied " + mode + " coverage export.";
            SavePrefs();
        }

        private void DrawReleaseReadinessSummary(PungentUtilityHelpGenerationReport report)
        {
            if (report == null)
                return;

            int complete = report.coverageRows.Count(r => r != null && r.coverageStatus == PungentUtilityHelpCoverageStatus.Complete);
            int acceptable = report.coverageRows.Count(r => r != null &&
                (r.coverageStatus == PungentUtilityHelpCoverageStatus.Acceptable ||
                 r.coverageStatus == PungentUtilityHelpCoverageStatus.DeveloperOnly ||
                 r.coverageExpectation == PungentUtilityHelpCoverageExpectation.NotApplicable));
            int actionable = report.coverageRows.Count(r => r != null &&
                r.reviewPriority > 0 &&
                r.coverageStatus != PungentUtilityHelpCoverageStatus.Complete &&
                r.coverageStatus != PungentUtilityHelpCoverageStatus.Acceptable &&
                r.coverageStatus != PungentUtilityHelpCoverageStatus.DeveloperOnly);
            int missingScripting = report.coverageRows.Count(r => r != null && r.missingScriptingIsActionable);
            int docIssues = report.documentationLinksMissingCurrentTargets + report.staleRelatedDocumentationLinkIds;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Release Readiness", "Compact help release-readiness summary using cached coverage data."), UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(PungentBugReportSettings.BackendStatus, UtilityWindowTheme.Teal, 178f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(report.registeredUtilities + " registered", UtilityWindowTheme.Blue, 102f);
                    UtilityWindowTheme.CountPill(complete + " complete", complete > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 92f);
                    UtilityWindowTheme.CountPill(acceptable + " acceptable/N/A", UtilityWindowTheme.Teal, 128f);
                    UtilityWindowTheme.CountPill(actionable + " actionable", actionable == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill(report.generatedEntriesAwaitingReview + " review debt", report.generatedEntriesAwaitingReview == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, 112f);
                    UtilityWindowTheme.CountPill(report.weakTooltipEntries + " weak tooltips", report.weakTooltipEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 118f);
                    UtilityWindowTheme.CountPill(missingScripting + " scripting gaps", missingScripting == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 122f);
                    UtilityWindowTheme.CountPill(docIssues + " doc issues", docIssues == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 98f);
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("Bug reporting posts to the configured Wix relay and queues locally if delivery fails. Discord webhook secrets stay on the website backend.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawCoverageOnboarding(PungentUtilityHelpGenerationReport report)
        {
            if (report == null || report.coverageRows == null)
                return;

            List<PungentUtilityHelpCoverageRow> rows = report.coverageRows
                .Where(r => r != null && ShouldShowOnboardingRow(r))
                .OrderByDescending(r => r.reviewPriority)
                .ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("New Utility Onboarding", "Checklist and safe cached actions for bringing future utilities into Help Coverage."), UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("help-browser", "coverage-report", "coverage-report", "Open help for onboarding utilities into Help Coverage.", "Coverage onboarding");
                }

                if (rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No high-priority onboarding rows are visible in cached coverage. New utilities will appear here when they need overview, contextual, tooltip, scripting, or documentation-link decisions.", MessageType.Info);
                    return;
                }

                foreach (PungentUtilityHelpCoverageRow row in rows)
                    DrawOnboardingRow(row);
            }
        }

        private bool ShouldShowOnboardingRow(PungentUtilityHelpCoverageRow row)
        {
            if (row == null)
                return false;

            return !row.hasOverviewHelp ||
                   row.missingHeaderIsActionable ||
                   row.missingSectionIsActionable ||
                   row.missingTooltipIsActionable ||
                   row.missingScriptingIsActionable ||
                   row.generatedEntriesAwaitingReview > 0 ||
                   row.weakTooltipEntries > 0 ||
                   row.missingDocumentationTargetCount > 0;
        }

        private void DrawOnboardingRow(PungentUtilityHelpCoverageRow row)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(row.reviewPriority > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 0.08f, 0.03f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName, EditorStyles.boldLabel, GUILayout.MinWidth(130f));
                    UtilityWindowTheme.CountPill(row.utilityKind.ToString(), UtilityWindowTheme.Blue, 104f);
                    UtilityWindowTheme.CountPill(row.coverageStatus.ToString(), CoverageStatusTint(row.coverageStatus), 122f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(row.hasOverviewHelp ? "Descriptor + overview" : "Needs overview", row.hasOverviewHelp ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 132f);
                    UtilityWindowTheme.CountPill(row.hasHeaderHelp || !row.missingHeaderIsActionable ? "Header ok/N/A" : "Needs header", row.hasHeaderHelp || !row.missingHeaderIsActionable ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 106f);
                    UtilityWindowTheme.CountPill(row.hasSectionHelp || !row.missingSectionIsActionable ? "Sections ok/N/A" : "Needs sections", row.hasSectionHelp || !row.missingSectionIsActionable ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 116f);
                    UtilityWindowTheme.CountPill(row.hasTooltipTopic || !row.missingTooltipIsActionable ? "Tooltips ok/N/A" : "Needs tooltips", row.hasTooltipTopic || !row.missingTooltipIsActionable ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 118f);
                    UtilityWindowTheme.CountPill(row.hasScriptingEntries || !row.missingScriptingIsActionable ? "Scripting ok/N/A" : "Needs API", row.hasScriptingEntries || !row.missingScriptingIsActionable ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 112f);
                    UtilityWindowTheme.CountPill(row.hasDocumentationLinks ? "Docs linked" : "Docs optional", row.hasDocumentationLinks ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 98f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Generate Overview Topic", "Create or open the overview topic for this utility."), EditorStyles.miniButton, GUILayout.Width(150f)))
                        GenerateForCoverageRow(row);
                    if (GUILayout.Button(new GUIContent("Generate Contextual Topic Stubs", "Regenerate missing contextual topic stubs from cached/scanned HelpButton references."), EditorStyles.miniButton, GUILayout.Width(182f)))
                    {
                        PungentUtilityHelpTopicGenerator.GenerateMissingContextualTopicStubs(out _status);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Generate Controls & Tooltips", "Create a manual Controls & Tooltips topic placeholder for this utility."), EditorStyles.miniButton, GUILayout.Width(166f)))
                        CreateControlsTopic(row.utilityId);
                    if (GUILayout.Button(new GUIContent("Classify Utility Kind", "Copy the current cached classification for review."), EditorStyles.miniButton, GUILayout.Width(124f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = row.utilityId + " => " + row.utilityKind + " / " + row.coverageExpectation + " / " + row.coverageStatus;
                        _status = "Copied utility coverage classification.";
                    }
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Mark Header Help N/A", "Mark header help not applicable for this utility."), EditorStyles.miniButton, GUILayout.Width(132f)))
                        MarkCoverageDecision(row, PungentUtilityHelpCoverageDecisionKind.HeaderHelp, "Header help marked not applicable from onboarding.");
                    if (GUILayout.Button(new GUIContent("Mark Scripting N/A", "Mark scripting coverage not applicable for this utility."), EditorStyles.miniButton, GUILayout.Width(126f)))
                        MarkCoverageDecision(row, PungentUtilityHelpCoverageDecisionKind.ScriptingCoverage, "Scripting coverage marked not applicable from onboarding.");
                    if (GUILayout.Button(new GUIContent("Create Documentation Link", "Open Documentation Links to create a utility-assigned link."), EditorStyles.miniButton, GUILayout.Width(146f)))
                        DocumentationLinkEditorPopup.CreateForUtility(row.utilityId);
                    if (GUILayout.Button(new GUIContent("Open Quick Help", "Open Quick Help for this utility overview."), EditorStyles.miniButton, GUILayout.Width(112f)))
                        PungentUtilityQuickHelpTray.Show(PopupRect(), new PungentUtilityHelpContext { utilityId = row.utilityId, sectionId = PungentUtilityHelpIds.DefaultSection, topicId = PungentUtilityHelpIds.DefaultTopic, label = "Coverage onboarding" });
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void MarkCoverageDecision(PungentUtilityHelpCoverageRow row, PungentUtilityHelpCoverageDecisionKind kind, string notes)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.utilityId))
                return;

            PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, kind, PungentUtilityHelpCoverageDecisionState.NotApplicable, notes);
            _status = "Marked " + kind + " not applicable for " + row.displayName + ".";
            MarkCacheDirty();
        }

        private void DrawHelpCoverageReport(PungentUtilityHelpGenerationReport report)
        {
            if (report == null || report.coverageRows == null || report.coverageRows.Count == 0)
                return;

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Help Coverage", "Cached coverage report from registry, topic cache, contextual help contexts, and generated indexes."), UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("help-browser", "coverage-report", "coverage-report", "Open help for the Help Coverage report.", "Help Coverage panel");
                    if (GUILayout.Button(new GUIContent("Copy Summary", "Copy the current help coverage report summary."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = PungentUtilityHelpTopicGenerator.ExportReportText(report);
                        _status = "Copied help coverage report.";
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _coverageFilter = (CoverageFilter)EditorGUILayout.EnumPopup(new GUIContent("Filter", "Filter Help Coverage rows."), _coverageFilter, GUILayout.Width(220f));
                    _coverageSort = (CoverageSort)EditorGUILayout.EnumPopup(new GUIContent("Sort", "Sort Help Coverage rows."), _coverageSort, GUILayout.Width(220f));
                    if (EditorGUI.EndChangeCheck())
                        SavePrefs();

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Clear Ignored Filter", "Show all rows again."), EditorStyles.miniButton, GUILayout.Width(118f)))
                    {
                        _coverageFilter = CoverageFilter.All;
                        SavePrefs();
                    }
                }

                List<PungentUtilityHelpCoverageRow> rows = SortCoverageRows(report.coverageRows.Where(MatchesCoverageFilter)).Take(18).ToList();
                EditorGUILayout.LabelField(rows.Count + " shown from " + report.coverageRows.Count + " coverage rows.", UtilityWindowTheme.MutedMiniLabelStyle);

                foreach (PungentUtilityHelpCoverageRow row in rows)
                    DrawCoverageRow(row);
            }
        }

        private void DrawCoverageRow(PungentUtilityHelpCoverageRow row)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(row.reviewPriority > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, row.reviewPriority > 0 ? 0.10f : 0.06f, 0.03f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName, EditorStyles.boldLabel, GUILayout.MinWidth(130f));
                    UtilityWindowTheme.CountPill(row.utilityKind.ToString(), UtilityWindowTheme.Blue, 104f);
                    UtilityWindowTheme.CountPill(row.coverageStatus.ToString(), CoverageStatusTint(row.coverageStatus), 122f);
                    UtilityWindowTheme.CountPill("P" + row.reviewPriority, row.reviewPriority > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 46f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(row.hasOverviewHelp ? "Overview" : "No overview", row.hasOverviewHelp ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 88f);
                    UtilityWindowTheme.CountPill(row.hasHeaderHelp ? "Header [?]" : (row.missingHeaderIsActionable ? "Needs header" : "Header N/A"), row.hasHeaderHelp ? UtilityWindowTheme.Green : row.missingHeaderIsActionable ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 96f);
                    UtilityWindowTheme.CountPill(row.hasSectionHelp ? "Section [?]" : (row.missingSectionIsActionable ? "Needs section" : "Section N/A"), row.hasSectionHelp ? UtilityWindowTheme.Green : row.missingSectionIsActionable ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 104f);
                    UtilityWindowTheme.CountPill(row.tooltipEntryCount + " tooltips", row.hasTooltipTopic ? UtilityWindowTheme.Teal : row.missingTooltipIsActionable ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 86f);
                    UtilityWindowTheme.CountPill(row.scriptingEntryCount + " API", row.hasScriptingEntries ? UtilityWindowTheme.Green : row.missingScriptingIsActionable ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 70f);
                    UtilityWindowTheme.CountPill(row.documentationLinkCount + " docs", row.hasDocumentationLinks ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 70f);
                    UtilityWindowTheme.CountPill(row.generatedEntriesAwaitingReview + " review", row.generatedEntriesAwaitingReview == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Purple, 78f);
                    if (row.weakTooltipEntries > 0)
                        UtilityWindowTheme.CountPill(row.weakTooltipEntries + " weak", UtilityWindowTheme.Amber, 68f);
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(row.coverageNotes))
                    EditorGUILayout.LabelField(row.coverageNotes, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(row.utilityId)))
                    {
                        if (GUILayout.Button(new GUIContent("Open Help", "Open this utility overview help."), EditorStyles.miniButton, GUILayout.Width(76f)))
                            PungentUtilityHelpRegistry.Open(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
                        if (GUILayout.Button(new GUIContent("Quick Help", "Open contextual Quick Help for this utility."), EditorStyles.miniButton, GUILayout.Width(76f)))
                            PungentUtilityQuickHelpTray.Show(new Rect(Event.current.mousePosition, Vector2.zero), new PungentUtilityHelpContext { utilityId = row.utilityId, sectionId = PungentUtilityHelpIds.DefaultSection, topicId = PungentUtilityHelpIds.DefaultTopic, label = "Coverage row" });
                        if (GUILayout.Button(new GUIContent("Review", "Switch review queue to this row's most important issue."), EditorStyles.miniButton, GUILayout.Width(62f)))
                            FocusQueueFor(row);
                        if (GUILayout.Button(new GUIContent("Manage Docs", "Open Documentation Links for this utility."), EditorStyles.miniButton, GUILayout.Width(88f)))
                            DocumentationLinkEditorPopup.ManageForUtility(row.utilityId);
                        if (GUILayout.Button(new GUIContent("Generate", "Create overview or Controls & Tooltips topic stubs where useful."), EditorStyles.miniButton, GUILayout.Width(70f)))
                            GenerateForCoverageRow(row);
                        if (GUILayout.Button(new GUIContent("Mark N/A", "Mark the currently actionable missing coverage as not applicable."), EditorStyles.miniButton, GUILayout.Width(72f)))
                            MarkCoverageRowNotApplicable(row);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawReviewQueues(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> rows = null)
        {
            rows = rows ?? PungentUtilityHelpReviewQueue.BuildRows(report);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Generated Review Queues", "Developer-only queues for generated drafts, tooltip entries, scripting entries, coverage gaps, docs issues, and accepted/ignored items."), UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(rows.Count + " rows", UtilityWindowTheme.Purple, 82f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _reviewQueue = (PungentUtilityHelpReviewQueueKind)EditorGUILayout.EnumPopup(new GUIContent("Queue", "Choose a generated-help review queue."), _reviewQueue, GUILayout.Width(300f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedReviewRows.Clear();
                        SavePrefs();
                    }

                    int queueCount = rows.Count(r => r.queueKind == _reviewQueue);
                    UtilityWindowTheme.CountPill(queueCount + " in queue", queueCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Export Queue", "Copy this review queue to the clipboard as Markdown."), EditorStyles.miniButton, GUILayout.Width(96f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildReviewQueueMarkdown(rows.Where(r => r.queueKind == _reviewQueue));
                        _status = "Copied " + _reviewQueue + " review queue.";
                    }
                }

                List<PungentUtilityHelpReviewRow> visibleRows = rows
                    .Where(r => r.queueKind == _reviewQueue)
                    .OrderByDescending(r => r.priority)
                    .ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase)
                    .Take(32)
                    .ToList();

                DrawReviewBulkActions(visibleRows);

                if (visibleRows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No rows in this review queue. Use another queue, refresh generated indexes explicitly, or adjust coverage decisions.", MessageType.Info);
                    return;
                }

                _reviewQueueScroll = EditorGUILayout.BeginScrollView(_reviewQueueScroll, false, false, GUILayout.MinHeight(140f), GUILayout.MaxHeight(360f));
                try
                {
                    foreach (PungentUtilityHelpReviewRow row in visibleRows)
                        DrawReviewRow(row);
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawReviewBulkActions(List<PungentUtilityHelpReviewRow> visibleRows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Select Shown", "Select the visible rows in this queue."), EditorStyles.miniButton, GUILayout.Width(96f)))
                {
                    foreach (PungentUtilityHelpReviewRow row in visibleRows)
                        _selectedReviewRows.Add(row.Key);
                }

                using (new EditorGUI.DisabledScope(_selectedReviewRows.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Clear Selection", "Clear selected review rows."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        _selectedReviewRows.Clear();
                    if (GUILayout.Button(new GUIContent("Mark Selected Reviewed", "Mark selected generated review rows as reviewed."), EditorStyles.miniButton, GUILayout.Width(154f)))
                        ApplyBulkReviewAction(visibleRows, "review");
                    if (GUILayout.Button(new GUIContent("Hide Selected", "Hide selected generated entries from shippable help."), EditorStyles.miniButton, GUILayout.Width(96f)))
                        ApplyBulkReviewAction(visibleRows, "hide");
                    if (GUILayout.Button(new GUIContent("Ignore Selected", "Mark selected rows as false positives or not applicable."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        ApplyBulkReviewAction(visibleRows, "ignore");
                    if (GUILayout.Button(new GUIContent("Promote Selected", "Promote selected generated entries to curated/manual overrides."), EditorStyles.miniButton, GUILayout.Width(112f)))
                        ApplyBulkReviewAction(visibleRows, "promote");
                    if (GUILayout.Button(new GUIContent("Create Controls Topics", "Create missing Controls & Tooltips topics for selected utilities."), EditorStyles.miniButton, GUILayout.Width(142f)))
                        ApplyBulkReviewAction(visibleRows, "controls");
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(_selectedReviewRows.Count + " selected", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(78f));
            }
        }

        private void DrawReviewRow(PungentUtilityHelpReviewRow row)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(ReviewTint(row), 0.10f, 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = _selectedReviewRows.Contains(row.Key);
                    bool nextSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                    if (nextSelected != selected)
                    {
                        if (nextSelected)
                            _selectedReviewRows.Add(row.Key);
                        else
                            _selectedReviewRows.Remove(row.Key);
                    }

                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName, EditorStyles.boldLabel, GUILayout.MinWidth(130f));
                    UtilityWindowTheme.CountPill(row.utilityKind.ToString(), UtilityWindowTheme.Blue, 104f);
                    UtilityWindowTheme.CountPill(row.lifecycleState.ToString(), LifecycleTint(row.lifecycleState), 128f);
                    UtilityWindowTheme.CountPill("P" + row.priority, row.priority > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 46f);
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(row.entryLabel))
                    EditorGUILayout.LabelField(row.entryLabel, UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(row.message))
                    EditorGUILayout.LabelField(row.message, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(row.sourcePath))
                    EditorGUILayout.LabelField(row.sourcePath + (row.sourceLine > 0 ? ":" + row.sourceLine : string.Empty), UtilityWindowTheme.PathLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Open Help", "Open the related help topic."), EditorStyles.miniButton, GUILayout.Width(76f)))
                        OpenReviewRowHelp(row);
                    if (GUILayout.Button(new GUIContent("Copy Source", "Copy source path or topic ID."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        EditorGUIUtility.systemCopyBuffer = string.IsNullOrWhiteSpace(row.sourcePath) ? row.topicStableId : row.sourcePath + (row.sourceLine > 0 ? ":" + row.sourceLine : string.Empty);
                    if (GUILayout.Button(new GUIContent("Promote", "Promote generated content to curated/manual help."), EditorStyles.miniButton, GUILayout.Width(68f)))
                        ApplyReviewAction(row, "promote");
                    if (GUILayout.Button(new GUIContent("Reviewed", "Mark this row reviewed or shippable."), EditorStyles.miniButton, GUILayout.Width(72f)))
                        ApplyReviewAction(row, "review");
                    if (GUILayout.Button(new GUIContent("Hide", "Hide generated entry from shippable help."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        ApplyReviewAction(row, "hide");
                    if (GUILayout.Button(new GUIContent("Ignore", "Ignore false positive or mark not applicable."), EditorStyles.miniButton, GUILayout.Width(58f)))
                        ApplyReviewAction(row, "ignore");
                    if (GUILayout.Button(new GUIContent("Create Stub", "Create a topic stub or Controls & Tooltips topic for this review row."), EditorStyles.miniButton, GUILayout.Width(78f)))
                        ApplyReviewAction(row, "stub");
                    if (GUILayout.Button(new GUIContent("Report Issue", "Open the bug report relay form with this review row as context."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        PungentBugReportOverlay.Show(PopupRect(), BuildReportContext(row));
                    if (GUILayout.Button(new GUIContent("Needs Wording", "Flag tooltip/generated entry for better wording."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        ApplyReviewAction(row, "wording");
                    if (GUILayout.Button(new GUIContent("Reset", "Reset manual decision/override for this generated row."), EditorStyles.miniButton, GUILayout.Width(52f)))
                        ApplyReviewAction(row, "reset");
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private bool MatchesCoverageFilter(PungentUtilityHelpCoverageRow row)
        {
            if (row == null)
                return false;

            switch (_coverageFilter)
            {
                case CoverageFilter.ActionableOnly:
                    return row.reviewPriority > 0 && row.coverageStatus != PungentUtilityHelpCoverageStatus.Complete && row.coverageStatus != PungentUtilityHelpCoverageStatus.Acceptable;
                case CoverageFilter.MissingHeader:
                    return row.missingHeaderIsActionable;
                case CoverageFilter.MissingSection:
                    return row.missingSectionIsActionable;
                case CoverageFilter.GeneratedReview:
                    return row.generatedEntriesAwaitingReview > 0;
                case CoverageFilter.WeakTooltips:
                    return row.weakTooltipEntries > 0 || row.missingTooltipIsActionable;
                case CoverageFilter.MissingDocs:
                    return row.missingDocumentationTargetCount > 0 || !row.hasDocumentationLinks;
                case CoverageFilter.MissingScripting:
                    return row.missingScriptingIsActionable;
                case CoverageFilter.NotApplicable:
                    return row.coverageExpectation == PungentUtilityHelpCoverageExpectation.NotApplicable ||
                           row.tooltipCoverageState.IndexOf("not applicable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           row.scriptingCoverageState.IndexOf("not applicable", StringComparison.OrdinalIgnoreCase) >= 0;
                case CoverageFilter.Complete:
                    return row.coverageStatus == PungentUtilityHelpCoverageStatus.Complete || row.coverageStatus == PungentUtilityHelpCoverageStatus.Acceptable;
                case CoverageFilter.DeveloperOnly:
                    return row.utilityKind == PungentUtilityHelpCoverageUtilityKind.DeveloperOnly || row.coverageStatus == PungentUtilityHelpCoverageStatus.DeveloperOnly;
                default:
                    return true;
            }
        }

        private List<PungentUtilityHelpCoverageRow> SortCoverageRows(IEnumerable<PungentUtilityHelpCoverageRow> rows)
        {
            IEnumerable<PungentUtilityHelpCoverageRow> source = rows ?? Enumerable.Empty<PungentUtilityHelpCoverageRow>();
            switch (_coverageSort)
            {
                case CoverageSort.UtilityName:
                    return source.OrderBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                case CoverageSort.UtilityKind:
                    return source.OrderBy(r => r.utilityKind).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                case CoverageSort.GeneratedReview:
                    return source.OrderByDescending(r => r.generatedEntriesAwaitingReview).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                case CoverageSort.TooltipCount:
                    return source.OrderByDescending(r => r.tooltipEntryCount + r.weakTooltipEntries).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                case CoverageSort.MissingDocs:
                    return source.OrderByDescending(r => r.missingDocumentationTargetCount).ThenBy(r => r.documentationLinkCount).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                case CoverageSort.CoverageStatus:
                    return source.OrderBy(r => r.coverageStatus).ThenByDescending(r => r.reviewPriority).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
                default:
                    return source.OrderByDescending(r => r.reviewPriority).ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        private void FocusQueueFor(PungentUtilityHelpCoverageRow row)
        {
            if (row == null)
                return;
            if (row.missingHeaderIsActionable)
                _reviewQueue = PungentUtilityHelpReviewQueueKind.MissingHeaderHelp;
            else if (row.missingSectionIsActionable)
                _reviewQueue = PungentUtilityHelpReviewQueueKind.MissingSectionHelp;
            else if (row.missingScriptingIsActionable)
                _reviewQueue = PungentUtilityHelpReviewQueueKind.MissingScripting;
            else if (row.weakTooltipEntries > 0 || row.missingTooltipIsActionable)
                _reviewQueue = PungentUtilityHelpReviewQueueKind.WeakTooltips;
            else if (row.generatedEntriesAwaitingReview > 0)
                _reviewQueue = PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics;
            else
                _reviewQueue = PungentUtilityHelpReviewQueueKind.CoverageComplete;
            SavePrefs();
        }

        private void GenerateForCoverageRow(PungentUtilityHelpCoverageRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.utilityId))
                return;

            PungentUtilityHelpTopic overview = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
            if (!row.hasTooltipTopic && (row.missingTooltipIsActionable || row.coverageStatus == PungentUtilityHelpCoverageStatus.NeedsTooltipReview))
            {
                PungentUtilityHelpTopic tooltipTopic = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(row.utilityId, "controls-tooltips", "controls-tooltips");
                if (string.IsNullOrWhiteSpace(tooltipTopic.summary) || tooltipTopic.summary == "This topic has not been written yet.")
                {
                    tooltipTopic.title = "Controls & Tooltips";
                    tooltipTopic.summary = "Manual placeholder for controls and tooltip coverage.";
                    tooltipTopic.quickUseMarkdown = "Refresh Tooltip Index for generated entries, or curate this topic manually when static scanning is not enough.";
                    tooltipTopic.tags = new List<string> { "controls", "tooltips", "coverage" };
                    PungentUtilityHelpStorage.instance.Persist();
                }
            }

            _status = "Prepared coverage topic stubs for " + (string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName) + ".";
            PungentUtilityHelpRegistry.Open(overview.utilityId, overview.sectionId, overview.topicId);
            MarkCacheDirty();
        }

        private void MarkCoverageRowNotApplicable(PungentUtilityHelpCoverageRow row)
        {
            if (row == null)
                return;

            if (!EditorUtility.DisplayDialog("Mark coverage not applicable?", "Mark the current actionable coverage gaps for " + row.displayName + " as not applicable? This is reversible from storage/decisions and the ignored queue.", "Mark N/A", "Cancel"))
                return;

            if (row.missingHeaderIsActionable)
                PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.HeaderHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from Help Coverage.");
            if (row.missingSectionIsActionable)
                PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.SectionHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from Help Coverage.");
            if (row.missingTooltipIsActionable)
                PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.TooltipCoverage, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from Help Coverage.");
            if (row.missingScriptingIsActionable)
                PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.ScriptingCoverage, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from Help Coverage.");
            _status = "Marked selected coverage gaps not applicable.";
            MarkCacheDirty();
        }

        private void ApplyBulkReviewAction(List<PungentUtilityHelpReviewRow> visibleRows, string action)
        {
            List<PungentUtilityHelpReviewRow> selected = visibleRows.Where(r => _selectedReviewRows.Contains(r.Key)).ToList();
            if (selected.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Apply bulk review action?", "Apply '" + action + "' to " + selected.Count + " selected review rows?", "Apply", "Cancel"))
                return;

            int changed = 0;
            int skipped = 0;
            foreach (PungentUtilityHelpReviewRow row in selected)
            {
                if (ApplyReviewAction(row, action, false))
                    changed++;
                else
                    skipped++;
            }
            _selectedReviewRows.Clear();
            _status = "Bulk review action '" + action + "': " + changed + " changed, " + skipped + " skipped.";
            MarkCacheDirty();
        }

        private bool ApplyReviewAction(PungentUtilityHelpReviewRow row, string action, bool announce = true)
        {
            if (row == null)
                return false;

            switch (action)
            {
                case "promote":
                    return PromoteReviewRow(row, announce);
                case "review":
                    SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.Reviewed, "Marked reviewed from Help Coverage.");
                    if (row.featureEntry != null)
                        PungentUtilityHelpDeveloperTools.MarkFeatureShippable(row.topic, row.featureEntry);
                    if (row.scriptingEntry != null)
                        PungentUtilityHelpDeveloperTools.MarkShippable(row.topic, row.scriptingEntry);
                    break;
                case "hide":
                    SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.Hidden, "Hidden from Help Coverage.");
                    if (row.featureEntry != null)
                        PungentUtilityHelpDeveloperTools.HideFeatureFromShippable(row.topic, row.featureEntry);
                    if (row.scriptingEntry != null)
                        PungentUtilityHelpDeveloperTools.HideFromShippable(row.topic, row.scriptingEntry);
                    break;
                case "ignore":
                    if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingHeaderHelp)
                        PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.HeaderHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from review queue.");
                    else if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingSectionHelp)
                        PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.SectionHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked not applicable from review queue.");
                    else if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingScripting)
                        PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.ScriptingCoverage, PungentUtilityHelpCoverageDecisionState.NotApplicable, "Marked scripting not applicable from review queue.");
                    else if (row.featureEntry != null)
                        PungentUtilityHelpDeveloperTools.IgnoreFeatureFalsePositive(row.topic, row.featureEntry);
                    SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.Ignored, "Ignored false positive from Help Coverage.");
                    break;
                case "wording":
                    SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.NeedsWording, "Needs better wording.");
                    if (row.featureEntry != null)
                        PungentUtilityHelpDeveloperTools.MarkFeatureNeedsBetterWording(row.topic, row.featureEntry);
                    break;
                case "stub":
                    CreateReviewStub(row);
                    break;
                case "controls":
                    CreateControlsTopic(row.utilityId);
                    break;
                case "reset":
                    ResetReviewRow(row);
                    break;
            }

            if (announce)
            {
                _status = "Applied review action '" + action + "'.";
                MarkCacheDirty();
            }
            return true;
        }

        private void CreateReviewStub(PungentUtilityHelpReviewRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.utilityId))
                return;

            if (row.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedTooltipEntries ||
                row.queueKind == PungentUtilityHelpReviewQueueKind.WeakTooltips ||
                row.queueKind == PungentUtilityHelpReviewQueueKind.MissingSectionHelp)
            {
                CreateControlsTopic(row.utilityId);
                return;
            }

            PungentUtilityHelpTopic topic = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
            if (string.IsNullOrWhiteSpace(topic.summary) || topic.summary == "This topic has not been written yet.")
            {
                topic.summary = "Manual help stub created from Help Coverage review.";
                topic.quickUseMarkdown = "Curate this stub with concise usage guidance, safety notes, and related topics.";
                topic.tags = new List<string> { "coverage", "manual-stub" };
                PungentUtilityHelpStorage.instance.Persist();
            }
            PungentUtilityHelpRegistry.Open(topic.utilityId, topic.sectionId, topic.topicId);
        }

        private void CreateControlsTopic(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            PungentUtilityHelpTopic topic = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(utilityId, "controls-tooltips", "controls-tooltips");
            if (string.IsNullOrWhiteSpace(topic.summary) || topic.summary == "This topic has not been written yet.")
            {
                topic.title = "Controls & Tooltips";
                topic.summary = "Manual Controls & Tooltips coverage topic created from Help Coverage review.";
                topic.quickUseMarkdown = "Refresh Tooltip Index for generated entries, or curate important controls manually when source scanning is unsupported.";
                topic.tags = new List<string> { "controls", "tooltips", "coverage" };
                PungentUtilityHelpStorage.instance.Persist();
            }
            _status = "Created Controls & Tooltips topic for " + utilityId + ".";
        }

        private bool PromoteReviewRow(PungentUtilityHelpReviewRow row, bool announce)
        {
            if (row.featureEntry != null)
            {
                PungentUtilityHelpDeveloperTools.MarkFeatureShippable(row.topic, row.featureEntry);
                SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.PromotedToCurated, "Promoted feature entry to curated/manual override.");
                return true;
            }

            if (row.scriptingEntry != null)
            {
                PungentUtilityHelpDeveloperTools.MarkShippable(row.topic, row.scriptingEntry);
                SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.PromotedToCurated, "Promoted scripting entry to curated/manual override.");
                return true;
            }

            if (row.topic != null)
            {
                PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(row.topic.utilityId, row.topic.sectionId, row.topic.topicId);
                manual.title = row.topic.title;
                manual.summary = row.topic.summary;
                manual.quickUseMarkdown = row.topic.quickUseMarkdown;
                manual.featureEntries = row.topic.featureEntries == null ? new List<PungentUtilityHelpFeatureEntry>() : row.topic.featureEntries.Select(CloneFeatureEntry).ToList();
                manual.scriptingEntries = row.topic.scriptingEntries == null ? new List<PungentUtilityHelpScriptingEntry>() : row.topic.scriptingEntries.Select(CloneScriptingEntry).ToList();
                manual.troubleshootingEntries = row.topic.troubleshootingEntries == null ? new List<PungentUtilityHelpTroubleshootingEntry>() : row.topic.troubleshootingEntries.Select(CloneTroubleshootingEntry).ToList();
                manual.developerOnly = false;
                manual.hidden = false;
                manual.generated = false;
                manual.sourceOwner = "Manual curated help";
                manual.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
                PungentUtilityHelpStorage.instance.Persist();
                SetReviewDecision(row, PungentUtilityHelpCoverageDecisionState.PromotedToCurated, "Promoted topic to curated/manual override.");
                if (announce)
                    PungentUtilityHelpRegistry.Open(manual.utilityId, manual.sectionId, manual.topicId);
                return true;
            }

            return false;
        }

        private void ResetReviewRow(PungentUtilityHelpReviewRow row)
        {
            if (row.featureEntry != null)
                PungentUtilityHelpDeveloperTools.ResetFeatureEntryToGenerated(row.topic, row.featureEntry);
            if (row.scriptingEntry != null)
                PungentUtilityHelpDeveloperTools.ResetScriptingEntryToGenerated(row.topic, row.scriptingEntry);
            PungentUtilityHelpStorage.instance.ClearCoverageDecision(row.utilityId, row.topicStableId, row.entryId, DecisionKindFor(row));
        }

        private void SetReviewDecision(PungentUtilityHelpReviewRow row, PungentUtilityHelpCoverageDecisionState state, string notes)
        {
            PungentUtilityHelpStorage.instance.SetCoverageDecision(row.utilityId, row.topicStableId, row.entryId, DecisionKindFor(row), state, notes);
        }

        private static PungentUtilityHelpCoverageDecisionKind DecisionKindFor(PungentUtilityHelpReviewRow row)
        {
            if (row == null)
                return PungentUtilityHelpCoverageDecisionKind.Coverage;
            if (row.featureEntry != null)
                return PungentUtilityHelpCoverageDecisionKind.FeatureEntry;
            if (row.scriptingEntry != null)
                return PungentUtilityHelpCoverageDecisionKind.ScriptingEntry;
            if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingHeaderHelp)
                return PungentUtilityHelpCoverageDecisionKind.HeaderHelp;
            if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingSectionHelp)
                return PungentUtilityHelpCoverageDecisionKind.SectionHelp;
            if (row.queueKind == PungentUtilityHelpReviewQueueKind.MissingScripting)
                return PungentUtilityHelpCoverageDecisionKind.ScriptingCoverage;
            if (row.queueKind == PungentUtilityHelpReviewQueueKind.DocumentationLinkIssues)
                return PungentUtilityHelpCoverageDecisionKind.DocumentationLinks;
            if (row.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics)
                return PungentUtilityHelpCoverageDecisionKind.GeneratedTopic;
            return PungentUtilityHelpCoverageDecisionKind.Coverage;
        }

        private void OpenReviewRowHelp(PungentUtilityHelpReviewRow row)
        {
            if (row == null)
                return;
            if (row.topic != null)
                PungentUtilityHelpRegistry.Open(row.topic.utilityId, row.topic.sectionId, row.topic.topicId);
            else if (!string.IsNullOrWhiteSpace(row.utilityId))
                PungentUtilityHelpRegistry.Open(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
        }

        private static Color CoverageStatusTint(PungentUtilityHelpCoverageStatus status)
        {
            switch (status)
            {
                case PungentUtilityHelpCoverageStatus.Complete:
                case PungentUtilityHelpCoverageStatus.Acceptable:
                    return UtilityWindowTheme.Green;
                case PungentUtilityHelpCoverageStatus.DeveloperOnly:
                case PungentUtilityHelpCoverageStatus.NeedsReview:
                    return UtilityWindowTheme.Purple;
                case PungentUtilityHelpCoverageStatus.NeedsHeaderHelp:
                case PungentUtilityHelpCoverageStatus.NeedsSectionHelp:
                case PungentUtilityHelpCoverageStatus.NeedsTooltipReview:
                case PungentUtilityHelpCoverageStatus.NeedsScriptingReview:
                case PungentUtilityHelpCoverageStatus.MissingDocs:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static Color LifecycleTint(PungentUtilityHelpGeneratedLifecycleState state)
        {
            switch (state)
            {
                case PungentUtilityHelpGeneratedLifecycleState.Reviewed:
                case PungentUtilityHelpGeneratedLifecycleState.PromotedToCurated:
                    return UtilityWindowTheme.Green;
                case PungentUtilityHelpGeneratedLifecycleState.Hidden:
                case PungentUtilityHelpGeneratedLifecycleState.Stale:
                    return UtilityWindowTheme.Red;
                case PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive:
                case PungentUtilityHelpGeneratedLifecycleState.NotApplicable:
                    return UtilityWindowTheme.Neutral;
                case PungentUtilityHelpGeneratedLifecycleState.NeedsBetterWording:
                case PungentUtilityHelpGeneratedLifecycleState.NeedsReview:
                case PungentUtilityHelpGeneratedLifecycleState.GeneratedDraft:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Purple;
            }
        }

        private static Color ReviewTint(PungentUtilityHelpReviewRow row)
        {
            return row == null ? UtilityWindowTheme.Neutral : LifecycleTint(row.lifecycleState);
        }

        private static string BuildCoverageExport(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows, CoverageExportMode mode)
        {
            switch (mode)
            {
                case CoverageExportMode.ActionableIssues:
                    return BuildReviewQueueMarkdown(reviewRows.Where(r => r.priority > 0 && r.queueKind != PungentUtilityHelpReviewQueueKind.CoverageComplete && r.queueKind != PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries));
                case CoverageExportMode.GeneratedReviewQueue:
                    return BuildReviewQueueMarkdown(reviewRows.Where(r => r.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics || r.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedTooltipEntries || r.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedScriptingEntries));
                case CoverageExportMode.TooltipReviewQueue:
                    return BuildReviewQueueMarkdown(reviewRows.Where(r => r.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedTooltipEntries || r.queueKind == PungentUtilityHelpReviewQueueKind.WeakTooltips));
                case CoverageExportMode.ScriptingReviewQueue:
                    return BuildReviewQueueMarkdown(reviewRows.Where(r => r.queueKind == PungentUtilityHelpReviewQueueKind.GeneratedScriptingEntries || r.queueKind == PungentUtilityHelpReviewQueueKind.MissingScripting));
                case CoverageExportMode.FullCsv:
                    return BuildCoverageCsv(report, reviewRows);
                case CoverageExportMode.FullMarkdown:
                    return BuildCoverageMarkdown(report, reviewRows);
                default:
                    return PungentUtilityHelpTopicGenerator.ExportReportText(report);
            }
        }

        private static string BuildReviewQueueMarkdown(IEnumerable<PungentUtilityHelpReviewRow> rows)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Pungent Help Review Queue");
            foreach (PungentUtilityHelpReviewRow row in (rows ?? Enumerable.Empty<PungentUtilityHelpReviewRow>()).Take(500))
            {
                builder.AppendLine("- **" + EscapeMarkdown(row.displayName) + "** `" + row.utilityId + "` " + row.queueKind + " / " + row.lifecycleState);
                builder.AppendLine("  - Topic: `" + row.topicStableId + "` Entry: `" + row.entryId + "`");
                builder.AppendLine("  - " + EscapeMarkdown(row.message));
                if (!string.IsNullOrWhiteSpace(row.sourcePath))
                    builder.AppendLine("  - Source: `" + row.sourcePath + (row.sourceLine > 0 ? ":" + row.sourceLine : string.Empty) + "`");
            }
            return builder.ToString();
        }

        private static string BuildCoverageMarkdown(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Pungent Utility Help Coverage");
            builder.AppendLine();
            builder.AppendLine("- Generated UTC: `" + report.generatedUtc + "`");
            builder.AppendLine("- Registered utilities: " + report.registeredUtilities);
            builder.AppendLine("- Generated entries awaiting review: " + report.generatedEntriesAwaitingReview);
            builder.AppendLine("- Weak tooltip entries: " + report.weakTooltipEntries);
            builder.AppendLine("- Documentation links: " + report.documentationLinkCount);
            builder.AppendLine("- Bug report relay: " + PungentBugReportSettings.BackendStatus);
            builder.AppendLine();
            builder.AppendLine("| Utility | Kind | Status | Header | Section | Tooltips | Scripting | Docs | Review |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (PungentUtilityHelpCoverageRow row in report.coverageRows.OrderByDescending(r => r.reviewPriority).ThenBy(r => r.displayName).Take(500))
                builder.AppendLine("| " + EscapeMarkdown(row.displayName) + " | " + row.utilityKind + " | " + row.coverageStatus + " | " + State(row.hasHeaderHelp, row.missingHeaderIsActionable) + " | " + State(row.hasSectionHelp, row.missingSectionIsActionable) + " | " + row.tooltipEntryCount + " | " + State(row.hasScriptingEntries, row.missingScriptingIsActionable) + " | " + row.documentationLinkCount + " | " + row.generatedEntriesAwaitingReview + " |");
            builder.AppendLine();
            builder.AppendLine("## Actionable Queue");
            builder.Append(BuildReviewQueueMarkdown(reviewRows.Where(r => r.priority > 0 && r.queueKind != PungentUtilityHelpReviewQueueKind.CoverageComplete)));
            return builder.ToString();
        }

        private static string BuildCoverageCsv(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> reviewRows)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("utilityId,displayName,utilityKind,coverageStatus,expectation,reviewPriority,overview,header,section,tooltipEntries,weakTooltipEntries,scriptingEntries,docs,missingDocTargets,generatedReview,notes");
            foreach (PungentUtilityHelpCoverageRow row in report.coverageRows.OrderByDescending(r => r.reviewPriority).ThenBy(r => r.displayName))
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    Csv(row.utilityId),
                    Csv(row.displayName),
                    Csv(row.utilityKind.ToString()),
                    Csv(row.coverageStatus.ToString()),
                    Csv(row.coverageExpectation.ToString()),
                    row.reviewPriority.ToString(),
                    Csv(row.hasOverviewHelp.ToString()),
                    Csv(State(row.hasHeaderHelp, row.missingHeaderIsActionable)),
                    Csv(State(row.hasSectionHelp, row.missingSectionIsActionable)),
                    row.tooltipEntryCount.ToString(),
                    row.weakTooltipEntries.ToString(),
                    row.scriptingEntryCount.ToString(),
                    row.documentationLinkCount.ToString(),
                    row.missingDocumentationTargetCount.ToString(),
                    row.generatedEntriesAwaitingReview.ToString(),
                    Csv(row.coverageNotes)
                }));
            }

            builder.AppendLine();
            builder.AppendLine("queue,utilityId,displayName,topicStableId,entryId,lifecycle,priority,message,sourcePath,sourceLine");
            foreach (PungentUtilityHelpReviewRow row in reviewRows.Take(1000))
                builder.AppendLine(string.Join(",", new[] { Csv(row.queueKind.ToString()), Csv(row.utilityId), Csv(row.displayName), Csv(row.topicStableId), Csv(row.entryId), Csv(row.lifecycleState.ToString()), row.priority.ToString(), Csv(row.message), Csv(row.sourcePath), row.sourceLine.ToString() }));
            return builder.ToString();
        }

        private static string State(bool present, bool actionable)
        {
            if (present)
                return "Present";
            return actionable ? "Actionable" : "NotApplicable";
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
        }

        private static PungentUtilityHelpFeatureEntry CloneFeatureEntry(PungentUtilityHelpFeatureEntry entry)
        {
            return entry == null ? null : new PungentUtilityHelpFeatureEntry
            {
                id = entry.id,
                label = entry.label,
                description = entry.description,
                location = entry.location,
                safetyNotes = entry.safetyNotes,
                sourcePath = entry.sourcePath,
                sourceLine = entry.sourceLine,
                sourceConfidence = entry.sourceConfidence,
                needsBetterWording = false,
                ignoredGenerated = false,
                developerOnly = false,
                hidden = false,
                generated = false
            };
        }

        private static PungentUtilityHelpScriptingEntry CloneScriptingEntry(PungentUtilityHelpScriptingEntry entry)
        {
            return entry == null ? null : new PungentUtilityHelpScriptingEntry
            {
                id = entry.id,
                declaringType = entry.declaringType,
                memberName = entry.memberName,
                signature = entry.signature,
                description = entry.description,
                usageNotes = entry.usageNotes,
                minimalExample = entry.minimalExample,
                whereItAppears = entry.whereItAppears,
                developerOnly = false,
                hidden = false,
                generated = false,
                stale = false,
                sourcePath = entry.sourcePath
            };
        }

        private static PungentUtilityHelpTroubleshootingEntry CloneTroubleshootingEntry(PungentUtilityHelpTroubleshootingEntry entry)
        {
            return entry == null ? null : new PungentUtilityHelpTroubleshootingEntry
            {
                id = entry.id,
                symptom = entry.symptom,
                likelyCause = entry.likelyCause,
                nextStep = entry.nextStep
            };
        }

        private void DrawWideLayout()
        {
            PungentUtilityHelpTopic topic = GetSelectedTopic();
            float railWidth = GetRightReadingControlsWidth(topic);
            _lastNavigationAreaRect = Rect.zero;
            _lastCollapsedNavigationRect = Rect.zero;
            _lastNavigationDrawerRect = Rect.zero;

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    DrawMainContent();

                if (railWidth > 0f)
                {
                    using (EditorGUILayout.VerticalScope readingScope = new EditorGUILayout.VerticalScope(GUILayout.Width(railWidth), GUILayout.ExpandHeight(true)))
                    {
                        DrawRightReadingControls(topic);
                        if (Event.current.type == EventType.Repaint)
                            _lastRightToolbarRect = readingScope.rect;
                    }
                }
            }
        }

        private void DrawNarrowLayout()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    DrawMainContent();

                float railWidth = GetRightReadingControlsWidth(GetSelectedTopic());
                if (railWidth > 0f)
                    DrawRightReadingControls(GetSelectedTopic());
            }
        }

        private bool IsTopicPageSelected()
        {
            return _selection != null && _selection.kind == PungentUtilityHelpSelectionKind.Topic && GetSelectedTopic() != null;
        }

        private bool ShouldShowNavigationInLayout(bool readingMode)
        {
            if (!readingMode)
                return true;
            return _sidebarMode == HelpSidebarMode.AlwaysOpen;
        }

        private bool ShouldDrawNavigationOverlay(bool readingMode)
        {
            if (!readingMode || _sidebarMode != HelpSidebarMode.AutoCompact)
                return false;
            UpdateContextualNavigationTimers();
            return EditorApplication.timeSinceStartup <= _navigationPeekUntil;
        }

        private float GetRightReadingControlsWidth(PungentUtilityHelpTopic topic)
        {
            return ShouldShowSectionRail() ? SectionRailWidth : 0f;
        }

        private bool ShouldShowSectionRail()
        {
            return _sectionRailMode != HelpSectionRailMode.Hidden;
        }

        private bool ShouldShowSectionRailLabels()
        {
            if (_sectionRailMode == HelpSectionRailMode.LabelsAlways)
                return true;

            UpdateContextualNavigationTimers();
            return _activeHelpOverlay == HelpOverlayKind.Sections || EditorApplication.timeSinceStartup <= _sectionRailLabelsUntil;
        }

        private void UpdateContextualNavigationTimers()
        {
            Event current = Event.current;
            if (current == null || current.type == EventType.Layout)
                return;

            Vector2 mouse = current.mousePosition;
            double now = EditorApplication.timeSinceStartup;
            bool labelsWereVisible = now <= _sectionRailLabelsUntil;

            bool overRail = _lastSectionRailRect.width > 0f && _lastSectionRailRect.Contains(mouse);
            bool overRailLabels = _lastSectionRailLabelRect.width > 0f && _lastSectionRailLabelRect.Contains(mouse);
            bool overSectionsAnchor = _activeHelpOverlay == HelpOverlayKind.Sections && _helpOverlayAnchorRect.width > 0f && _helpOverlayAnchorRect.Contains(mouse);
            if (overRail || overRailLabels || overSectionsAnchor)
                _sectionRailLabelsUntil = now + SectionRailLabelHoldSeconds;

            bool labelsAreVisible = now <= _sectionRailLabelsUntil;
            if (labelsWereVisible != labelsAreVisible && current.type != EventType.Layout)
                Repaint();
        }

        private void DrawCollapsedNavigationRail(bool horizontal = false)
        {
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.08f, 0.035f, 4, 2), horizontal ? GUILayout.ExpandWidth(true) : GUILayout.Width(36f), horizontal ? GUILayout.Height(32f) : GUILayout.ExpandHeight(true)))
            {
                if (horizontal)
                    DrawHorizontalCollapsedNavigationRail();
                else
                    DrawVerticalCollapsedNavigationRail();

                if (Event.current.type == EventType.Repaint)
                    _lastCollapsedNavigationRect = scope.rect;
                EditorGUIUtility.AddCursorRect(scope.rect, MouseCursor.Link);
                if (HandleClick(scope.rect, "Open documentation navigation."))
                {
                    _sidebarMode = HelpSidebarMode.AlwaysOpen;
                    SavePrefs();
                }
            }
        }

        private void DrawNavigationOverlayDrawer()
        {
            Rect anchor = _lastCollapsedNavigationRect.width > 0f
                ? _lastCollapsedNavigationRect
                : new Rect(0f, 110f, CollapsedNavigationWidth, Mathf.Max(220f, position.height - 122f));
            Rect drawerRect = new Rect(anchor.xMax + 4f, anchor.y, _leftWidth, Mathf.Max(180f, anchor.height));
            drawerRect.x = Mathf.Clamp(drawerRect.x, 6f, Mathf.Max(6f, position.width - drawerRect.width - 72f));
            drawerRect.y = Mathf.Clamp(drawerRect.y, 88f, Mathf.Max(88f, position.height - drawerRect.height - 8f));

            if (Event.current.type == EventType.Repaint)
            {
                _lastNavigationDrawerRect = drawerRect;
                EditorGUI.DrawRect(new Rect(drawerRect.x + 3f, drawerRect.y + 4f, drawerRect.width, drawerRect.height), new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.24f : 0.12f));
                EditorGUI.DrawRect(drawerRect, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.11f, 0.115f, 0.98f) : new Color(0.86f, 0.88f, 0.90f, 0.98f));
            }

            GUILayout.BeginArea(drawerRect, EditorStyles.helpBox);
            _drawingNavigationOverlayDrawer = true;
            try
            {
                DrawNavigation();
            }
            finally
            {
                _drawingNavigationOverlayDrawer = false;
                GUILayout.EndArea();
            }
        }

        private void DrawVerticalCollapsedNavigationRail()
        {
            Rect laneRect = GUILayoutUtility.GetRect(CollapsedNavigationWidth - 8f, 116f, GUILayout.Width(CollapsedNavigationWidth - 8f), GUILayout.Height(116f));
            bool explicitCollapsed = _sidebarMode == HelpSidebarMode.Collapsed;
            bool hovered = laneRect.Contains(Event.current.mousePosition);
            Color tint = explicitCollapsed ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Teal;
            if (Event.current.type == EventType.Repaint)
            {
                DrawStudioBox(laneRect, new Color(tint.r, tint.g, tint.b, hovered ? 0.12f : 0.055f), new Color(tint.r, tint.g, tint.b, hovered ? 0.52f : 0.24f));
                GUIStyle center = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                GUI.Label(new Rect(laneRect.x, laneRect.y + 5f, laneRect.width, 18f), new GUIContent("Docs", "Documentation navigation."), center);
                GUI.Label(new Rect(laneRect.x, laneRect.y + 27f, laneRect.width, 18f), new GUIContent(">", "Open documentation navigation."), center);
                if (!string.IsNullOrWhiteSpace(_search))
                    EditorGUI.DrawRect(new Rect(laneRect.center.x - 4f, laneRect.y + 52f, 8f, 8f), UtilityWindowTheme.Amber);
                if (explicitCollapsed)
                {
                    GUI.Label(new Rect(laneRect.x, laneRect.y + 65f, laneRect.width, 16f), new GUIContent(CompactSelectionMarker(), "Current documentation location."), center);
                    GUI.Label(new Rect(laneRect.x, laneRect.y + 84f, laneRect.width, 16f), new GUIContent(CountUtilities().ToString(), "Visible utilities."), center);
                    GUI.Label(new Rect(laneRect.x, laneRect.y + 101f, laneRect.width, 16f), new GUIContent(_navigation.searchTopics.Count.ToString(), "Visible topics."), center);
                }
            }

            EditorGUIUtility.AddCursorRect(laneRect, MouseCursor.Link);
            if (HandleClick(laneRect, "Open documentation navigation."))
            {
                _sidebarMode = HelpSidebarMode.AlwaysOpen;
                SavePrefs();
            }
            GUILayout.FlexibleSpace();
        }

        private string CompactSelectionMarker()
        {
            if (_selection == null)
                return "H";
            switch (_selection.kind)
            {
                case PungentUtilityHelpSelectionKind.Category: return "C";
                case PungentUtilityHelpSelectionKind.Subcategory: return "M";
                case PungentUtilityHelpSelectionKind.Utility: return "U";
                case PungentUtilityHelpSelectionKind.Topic: return "T";
                case PungentUtilityHelpSelectionKind.MissingTopic: return "?";
                default: return "H";
            }
        }

        private void DrawHorizontalCollapsedNavigationRail()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Docs", "Open documentation navigation."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    _sidebarMode = HelpSidebarMode.AlwaysOpen;
                    SavePrefs();
                }
                string message = string.IsNullOrWhiteSpace(_search) ? "Navigation compacted for reading." : "Search active.";
                if (_sidebarMode == HelpSidebarMode.Collapsed)
                    message += " " + CountUtilities() + " utilities / " + _navigation.searchTopics.Count + " topics.";
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawNavigation()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.14f, 0.07f), GUILayout.ExpandHeight(true)))
            {
                UtilityWindowTheme.SectionTitle("Documentation", UtilityWindowTheme.Cyan, string.IsNullOrWhiteSpace(_search) ? null : "Filtered");
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("help-browser", "navigation", "navigation", "Open help for Help Browser navigation.", "Navigation panel");
                }

                _navScroll = EditorGUILayout.BeginScrollView(_navScroll, GUILayout.ExpandHeight(true));
                try
                {
                    DrawSearchResultsNode();

                    if (_navigation.categories.Count == 0)
                    {
                        DrawEmptyState("No documentation directories match the current search.", "Clear Search or enable Developer Mode filters.");
                        return;
                    }

                    foreach (PungentUtilityHelpCategoryNode category in _navigation.categories)
                        DrawCategoryNode(category);
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawSearchResultsNode()
        {
            string label = string.IsNullOrWhiteSpace(_search) ? "Directory Home" : "Search Results";
            DrawNavigationRow(
                0,
                label,
                _navigation.searchTopics.Count + " topics",
                "Show documentation directory overview and current search results.",
                PungentUtilityHelpNavigationSelection.SearchResults(),
                false,
                false,
                null);
        }

        private void DrawCategoryNode(PungentUtilityHelpCategoryNode category)
        {
            if (category == null)
                return;

            string key = "category:" + category.id;
            bool expanded = IsExpanded(key) || IsSelectionInside(category) || !string.IsNullOrWhiteSpace(_search);
            bool nextExpanded = DrawNavigationRow(
                0,
                category.displayName,
                CountTopics(category) + " topics",
                "Open the " + category.displayName + " help category.",
                PungentUtilityHelpNavigationSelection.Category(category.id),
                true,
                expanded,
                key);

            if (!nextExpanded)
                return;

            foreach (PungentUtilityHelpSubcategoryNode subcategory in category.subcategories)
                DrawSubcategoryNode(subcategory);
        }

        private void DrawSubcategoryNode(PungentUtilityHelpSubcategoryNode subcategory)
        {
            if (subcategory == null)
                return;

            string key = "subcategory:" + subcategory.categoryId + "/" + subcategory.id;
            bool expanded = IsExpanded(key) || IsSelectionInside(subcategory) || !string.IsNullOrWhiteSpace(_search);
            bool nextExpanded = DrawNavigationRow(
                14,
                subcategory.displayName,
                CountTopics(subcategory) + " topics",
                "Open the " + subcategory.displayName + " help subcategory.",
                PungentUtilityHelpNavigationSelection.Subcategory(subcategory.categoryId, subcategory.id),
                true,
                expanded,
                key);

            if (!nextExpanded)
                return;

            foreach (PungentUtilityHelpUtilityNode utility in subcategory.utilities)
                DrawUtilityNode(utility);
        }

        private void DrawUtilityNode(PungentUtilityHelpUtilityNode utility)
        {
            if (utility == null)
                return;

            string key = "utility:" + utility.id;
            bool expanded = IsExpanded(key) || IsSelectionInside(utility) || !string.IsNullOrWhiteSpace(_search);
            bool nextExpanded = DrawNavigationRow(
                28,
                utility.displayName,
                utility.topics.Count + " topics",
                string.IsNullOrWhiteSpace(utility.summary) ? "Open utility help directory." : utility.summary,
                PungentUtilityHelpNavigationSelection.Utility(utility.categoryId, utility.subcategoryId, utility.id),
                utility.topics.Count > 0,
                expanded,
                key);

            if (!nextExpanded)
                return;

            foreach (PungentUtilityHelpTopic topic in utility.topics)
            {
                string topicLabel = string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title;
                DrawNavigationRow(
                    44,
                    topicLabel,
                    TopicStateLabel(topic),
                    topic.summary,
                    PungentUtilityHelpNavigationSelection.Topic(topic, utility),
                    false,
                    false,
                    null);
            }
        }

        private bool DrawNavigationRow(float indent, string label, string pill, string tooltip, PungentUtilityHelpNavigationSelection target, bool hasChildren, bool expanded, string expansionKey)
        {
            bool selected = _selection != null && _selection.SameTarget(target);
            Color tint = selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral;
            bool nextExpanded = expanded;

            Rect rowRect;
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.18f : 0.045f, selected ? 0.08f : 0.025f, 3, 1)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(indent);

                    if (hasChildren)
                    {
                        if (DrawTextLink(expanded ? "v" : ">", expanded ? "Collapse" : "Expand", false, EditorStyles.label, GUILayout.Width(18f), GUILayout.Height(21f)))
                        {
                            nextExpanded = !expanded;
                            SetExpanded(expansionKey, nextExpanded);
                        }
                    }
                    else
                    {
                        GUILayout.Space(20f);
                    }

                    GUIStyle labelStyle = selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle;
                    EditorGUILayout.LabelField(new GUIContent(label, tooltip), labelStyle, GUILayout.MinWidth(88f), GUILayout.Height(21f));

                    if (!string.IsNullOrWhiteSpace(pill) && position.width > 560f)
                        UtilityWindowTheme.InfoPill(new GUIContent(pill, tooltip), selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, Mathf.Clamp(pill.Length * 6f + 22f, 56f, 104f));
                }

                rowRect = scope.rect;
            }

            DrawInteractiveSurface(rowRect, tint, selected, rowRect.Contains(Event.current.mousePosition));
            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            if (HandleClick(rowRect, tooltip))
                SelectIfDifferent(target);

            return nextExpanded;
        }

        private static string TopicStateLabel(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return string.Empty;
            if (topic.hidden)
                return "hidden";
            if (topic.developerOnly)
                return "dev";
            if (topic.generated)
                return "generated";
            return "curated";
        }

        private void DrawMainContent()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f), GUILayout.ExpandHeight(true)))
            {
                DrawBreadcrumbs();

                if (_selection == null || _selection.kind != PungentUtilityHelpSelectionKind.Topic)
                    _sectionRects.Clear();

                _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.ExpandHeight(true));
                Rect contentViewportRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint)
                    _contentViewportHeight = Mathf.Max(1f, contentViewportRect.height);
                try
                {
                    switch (_selection.kind)
                    {
                        case PungentUtilityHelpSelectionKind.Category:
                            DrawCategoryPage(_navigation.FindCategory(_selection.categoryId));
                            break;
                        case PungentUtilityHelpSelectionKind.Subcategory:
                            DrawSubcategoryPage(_navigation.FindSubcategory(_selection.categoryId, _selection.subcategoryId));
                            break;
                        case PungentUtilityHelpSelectionKind.Utility:
                            DrawUtilityPage(_navigation.FindUtility(_selection.utilityId));
                            break;
                        case PungentUtilityHelpSelectionKind.Topic:
                            DrawTopicPage(GetSelectedTopic());
                            break;
                        case PungentUtilityHelpSelectionKind.MissingTopic:
                            DrawMissingTopicPage();
                            break;
                        default:
                            DrawSearchResultsPage();
                            break;
                    }

                    if (Event.current.type == EventType.Repaint && (_selection == null || _selection.kind != PungentUtilityHelpSelectionKind.Topic))
                    {
                        Rect lastRect = GUILayoutUtility.GetLastRect();
                        _articleContentHeight = Mathf.Max(_contentViewportHeight, lastRect.yMax + 24f);
                    }
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawBreadcrumbs()
        {
            List<PungentUtilityHelpBreadcrumbSegment> segments = PungentUtilityHelpBreadcrumbs.Build(_navigation, _selection);
            string compact = PungentUtilityHelpBreadcrumbs.CompactPath(segments);
            string full = string.Join(" > ", segments.Select(s => s.label).ToArray());

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.12f, 0.06f, 5, 2)))
            {
                if (position.width < 1000f)
                {
                    DrawCompactBreadcrumbs(segments, full);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Location", "Current help directory path."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(58f));
                    for (int i = 0; i < segments.Count; i++)
                    {
                        PungentUtilityHelpBreadcrumbSegment segment = segments[i];
                        if (i > 0)
                            EditorGUILayout.LabelField("/", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(10f));

                        float width = Mathf.Clamp(segment.label.Length * 7f + 24f, 74f, 180f);
                        bool selected = i == segments.Count - 1;
                        if (DrawTextLink(segment.label, full + "\n\n" + segment.tooltip, selected, selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle, GUILayout.Width(width), GUILayout.Height(20f)))
                            Select(segment.selection);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCompactBreadcrumbs(List<PungentUtilityHelpBreadcrumbSegment> segments, string fullPath)
        {
            if (segments == null || segments.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Location", fullPath), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(58f));
                PungentUtilityHelpBreadcrumbSegment root = segments[0];
                if (DrawTextLink(CompactBreadcrumbLabel(root.label, 10), fullPath + "\n\n" + root.tooltip, false, UtilityWindowTheme.LinkStyle, GUILayout.Width(62f), GUILayout.Height(20f)))
                    Select(root.selection);

                int start = Mathf.Max(1, segments.Count - 2);
                if (start > 1)
                    EditorGUILayout.LabelField(new GUIContent("...", fullPath), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(22f));

                for (int i = start; i < segments.Count; i++)
                {
                    PungentUtilityHelpBreadcrumbSegment segment = segments[i];
                    EditorGUILayout.LabelField("/", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(10f));

                    string label = CompactBreadcrumbLabel(segment.label, i == segments.Count - 1 ? 24 : 18);
                    bool selected = i == segments.Count - 1;
                    if (DrawTextLink(label, fullPath + "\n\n" + segment.tooltip, selected, selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle, GUILayout.MinWidth(82f), GUILayout.ExpandWidth(true), GUILayout.Height(20f)))
                        Select(segment.selection);
                }
            }
        }

        private static string CompactBreadcrumbLabel(string label, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "Help";

            string trimmed = label.Trim();
            if (trimmed.Length <= maxCharacters)
                return trimmed;

            return trimmed.Substring(0, Mathf.Max(1, maxCharacters - 3)) + "...";
        }

        private void DrawSearchResultsPage()
        {
            string title = string.IsNullOrWhiteSpace(_search) ? "Documentation Directory" : "Search Results";
            string summary = string.IsNullOrWhiteSpace(_search)
                ? "Start with a guided path, search for a utility, or browse the documentation tree."
                : "Results are filtered across help topics and utility metadata while keeping parent directory context visible.";
            DrawDirectoryHeader(title, summary);

            if (_navigation.categories.Count == 0)
            {
                DrawEmptyState("No help content matches the current search or filters.", "Clear Search or enable Developer Mode visibility filters.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_search))
            {
                DrawStartHerePathways();
                DrawNotesBookmarksLandingRow();
                DrawArticleSubheading("Browse By Category");
                foreach (PungentUtilityHelpCategoryNode category in _navigation.categories.Take(8))
                    DrawDirectoryListRow(category.displayName, CountTopics(category) + " topics across " + category.subcategories.Count + " modules.", "Open category", () => Select(PungentUtilityHelpNavigationSelection.Category(category.id)), UtilityWindowTheme.Cyan);
            }
            else
            {
                DrawArticleSubheading("Search Result Context");
                foreach (PungentUtilityHelpCategoryNode category in _navigation.categories)
                    DrawDirectoryListRow(category.displayName, CountTopics(category) + " topics across " + category.subcategories.Count + " modules.", "Open category", () => Select(PungentUtilityHelpNavigationSelection.Category(category.id)), UtilityWindowTheme.Cyan);
            }

            if (!string.IsNullOrWhiteSpace(_search) && _navigation.searchTopics.Count > 0)
            {
                DrawArticleSubheading("Matching Topics");
                foreach (PungentUtilityHelpTopic topic in _navigation.searchTopics.Take(20))
                    DrawTopicLinkCard(topic);
            }
        }

        private void DrawStartHerePathways()
        {
            DrawArticleSubheading("Start Here");
            DrawDirectoryCard(
                "Start with a guide",
                "Read the Help Browser overview as a normal guide before diving into controls, troubleshooting, or generated references.",
                "Open guide",
                () => PungentUtilityHelpRegistry.Open("help-browser", "overview", "overview"),
                UtilityWindowTheme.Teal);
            DrawDirectoryCard(
                "Find a utility",
                "Browse by category/module or search names, tasks, controls, and help text from the top bar.",
                "Browse tree",
                () => { _navScroll = Vector2.zero; _status = "Use the left documentation tree or Search to find a utility."; },
                UtilityWindowTheme.Cyan);
            DrawDirectoryCard(
                "Learn a workflow",
                "Choose a utility topic, then use Guide, Controls, Troubleshooting, Examples/API, and Related to move from usage to detail.",
                "Open navigation",
                () => PungentUtilityHelpRegistry.Open("help-browser", "navigation", "navigation"),
                UtilityWindowTheme.Blue);
            DrawDirectoryCard(
                "Troubleshoot",
                "Search known symptoms and setup issues when a utility is empty, missing data, or not behaving as expected.",
                "Search issues",
                () => { _search = "troubleshooting"; Select(PungentUtilityHelpNavigationSelection.SearchResults()); MarkCacheDirty(); SavePrefs(); },
                UtilityWindowTheme.Amber);

            if (DeveloperToolsVisible)
            {
                DrawDirectoryCard(
                    "Developer documentation maintenance",
                    "Review generated drafts, coverage gaps, tooltips, scripting index, docs links, and bug-report relay setup.",
                    "Open dashboard",
                    () => { _showGenerationDashboard = true; _dashboardSummaryOpen = true; SavePrefs(); },
                    UtilityWindowTheme.Purple);
            }
        }

        private void DrawNotesBookmarksLandingRow()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.08f, 0.03f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Your Notes & Bookmarks", UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.InfoPill(new GUIContent(GetNotesBookmarksCount().ToString(), "Total local Help bookmarks plus Help-linked Notes & Roadmap notes."), UtilityWindowTheme.Amber, 42f);
                }

                EditorGUILayout.LabelField("Review help topics you bookmarked and notes you attached while learning the utilities.", UtilityWindowTheme.BodyStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Open Notes & Bookmarks", "View all Help Browser notes, bookmarks, and topic annotations."), EditorStyles.miniButton, GUILayout.Width(156f)))
                        OpenNotesBookmarksTray();

                    using (new EditorGUI.DisabledScope(PungentUtilityHelpNotesBridge.OpenAllHelpNotes == null))
                    {
                        if (GUILayout.Button(new GUIContent("Open Notes & Roadmap", PungentUtilityHelpNotesBridge.OpenAllHelpNotes == null ? "Notes integration is not available. Local Help bookmarks still work." : "Open the full Notes & Roadmap browser."), EditorStyles.miniButton, GUILayout.Width(148f)))
                            PungentUtilityHelpNotesBridge.OpenAllHelpNotes();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCategoryPage(PungentUtilityHelpCategoryNode category)
        {
            if (category == null)
            {
                DrawEmptyState("This help category is not available.", "Choose another category from the documentation directory.");
                return;
            }

            DrawDirectoryHeader(category.displayName, "Documentation category with " + category.subcategories.Count + " modules and " + CountTopics(category) + " topics.");
            foreach (PungentUtilityHelpSubcategoryNode subcategory in category.subcategories)
                DrawDirectoryCard(subcategory.displayName, subcategory.utilities.Count + " utilities, " + CountTopics(subcategory) + " topics.", "Open module", () => Select(PungentUtilityHelpNavigationSelection.Subcategory(category.id, subcategory.id)), UtilityWindowTheme.Blue);

            DrawDirectoryTopicPreview(category.subcategories.SelectMany(s => s.utilities).SelectMany(u => u.topics), "Category Topics");
        }

        private void DrawSubcategoryPage(PungentUtilityHelpSubcategoryNode subcategory)
        {
            if (subcategory == null)
            {
                DrawEmptyState("This help subcategory is not available.", "Choose another module from the documentation directory.");
                return;
            }

            DrawDirectoryHeader(subcategory.displayName, "Utility documentation module with " + subcategory.utilities.Count + " utilities and " + CountTopics(subcategory) + " topics.");
            foreach (PungentUtilityHelpUtilityNode utility in subcategory.utilities)
                DrawUtilityDirectoryCard(utility);

            DrawDirectoryTopicPreview(subcategory.utilities.SelectMany(u => u.topics), "Module Topics");
        }

        private void DrawUtilityPage(PungentUtilityHelpUtilityNode utility)
        {
            if (utility == null)
            {
                DrawEmptyState("This utility has no help directory yet.", "Choose another utility or use Developer Mode to create topic stubs.");
                return;
            }

            DrawDirectoryHeader(utility.displayName, string.IsNullOrWhiteSpace(utility.summary) ? "Utility help directory." : utility.summary);
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(utility.topics.Count + " topics", UtilityWindowTheme.Teal, 82f);
                int scriptingCount = utility.topics.Sum(t => t.scriptingEntries == null ? 0 : t.scriptingEntries.Count);
                UtilityWindowTheme.CountPill(scriptingCount + " API", scriptingCount > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 72f);
                if (utility.descriptor != null)
                    UtilityWindowTheme.CountPill(PungentUtilityRegistry.GetStatus(utility.descriptor), UtilityWindowTheme.Cyan, 92f);
                GUILayout.FlexibleSpace();
            }

            if (utility.topics.Count == 0)
            {
                DrawUtilityDocumentationLinks(utility.id);
                DrawEmptyState("This utility does not have help topics yet.", "Developer Mode can create a topic stub from a missing help destination.");
                return;
            }

            DrawUtilityDocumentationLinks(utility.id);

            UtilityWindowTheme.SectionTitle("Topics", UtilityWindowTheme.Teal, utility.topics.Count.ToString());
            foreach (PungentUtilityHelpTopic topic in utility.topics)
                DrawTopicLinkCard(topic);
        }

        private void DrawUtilityDocumentationLinks(string utilityId)
        {
            List<PungentUtilityHelpDocumentationLinkRow> rows = PungentUtilityHelpDocumentationLinksProvider.GetLinksForUtility(utilityId, includeGlobal: true);
            if (rows.Count == 0)
            {
                if (!DeveloperToolsVisible)
                    return;

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.04f, 5, 3)))
                {
                    UtilityWindowTheme.SectionTitle("Documentation Links", UtilityWindowTheme.Cyan, "0");
                    DrawEmptyState("No documentation links are assigned to this utility yet.", "Create Link For Utility to connect a guide, PDF, markdown file, local path, Unity asset, or web reference.");
                    DrawDocumentationLinkManagementActions(utilityId);
                }
                return;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.04f, 5, 3)))
            {
                UtilityWindowTheme.SectionTitle("Documentation Links", UtilityWindowTheme.Cyan, rows.Count.ToString());
                foreach (PungentUtilityHelpDocumentationLinkRow row in rows.Take(6))
                    DrawDocumentationLinkRow(null, row, utilityId);

                if (rows.Count > 6)
                    EditorGUILayout.LabelField((rows.Count - 6) + " more documentation links are available in Documentation Links.", UtilityWindowTheme.MutedMiniLabelStyle);

                DrawDocumentationLinkManagementActions(utilityId);
            }
        }

        private void DrawDirectoryTopicPreview(IEnumerable<PungentUtilityHelpTopic> topics, string title)
        {
            List<PungentUtilityHelpTopic> list = topics == null
                ? new List<PungentUtilityHelpTopic>()
                : topics.Where(t => t != null).GroupBy(t => t.StableId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(12).ToList();
            if (list.Count == 0)
                return;

            UtilityWindowTheme.SectionTitle(title, UtilityWindowTheme.Teal, list.Count.ToString());
            foreach (PungentUtilityHelpTopic topic in list)
                DrawTopicLinkCard(topic);
        }

        private void DrawTopicPage(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
            {
                DrawMissingTopicPage();
                return;
            }

            RefreshAnnotationCacheIfNeeded(topic);
            DrawTopicHeader(topic);
            DrawTopicArticleSections(topic);
        }

        private void DrawTopicArticleSections(PungentUtilityHelpTopic topic)
        {
            _sectionRects.Clear();
            DrawArticleSection(topic, ContentTab.QuickUseGuide, "Guide", "Overview, start-here steps, common uses, and immediate next steps.", () => DrawQuickUse(topic));
            DrawArticleSection(topic, ContentTab.FeatureIndex, "Controls", CountVisibleFeatureEntries(topic) + " documented controls and concepts.", () => DrawFeatureIndex(topic));
            DrawArticleSection(topic, ContentTab.Troubleshooting, "Troubleshooting", (topic.troubleshootingEntries == null ? 0 : topic.troubleshootingEntries.Count) + " known fixes.", () => DrawTroubleshooting(topic));
            if (topic.scriptingEntries != null && topic.scriptingEntries.Count > 0)
                DrawArticleSection(topic, ContentTab.ScriptingIndex, "Examples/API", CountVisibleScriptingEntries(topic) + " examples or scripting references.", () => DrawScriptingIndex(topic));
            DrawArticleSection(topic, ContentTab.Related, "Related Resources", "Related topics, utilities, documentation links, notes, and bookmarks.", () => DrawRelatedResourcesInline(topic));
            if (Event.current.type == EventType.Repaint && _sectionRects.Count > 0)
                _articleContentHeight = Mathf.Max(_contentViewportHeight, _sectionRects.Values.Max(rect => rect.yMax) + 24f);
        }

        private void DrawArticleSection(PungentUtilityHelpTopic topic, ContentTab section, string label, string summary, Action drawContent)
        {
            bool open = GetSectionOpen(topic, section);
            Color tint = section == ContentTab.Troubleshooting ? UtilityWindowTheme.Amber :
                section == ContentTab.ScriptingIndex ? UtilityWindowTheme.Blue :
                section == ContentTab.Related ? UtilityWindowTheme.Purple : UtilityWindowTheme.Teal;

            Rect anchorRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            Rect sectionRect;
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, open ? 0.052f : 0.026f, open ? 0.022f : 0.012f, 7, 4)))
            {
                if (DrawArticleSectionHeader(label, open, _tab == section, tint, "Show or hide " + label + "."))
                {
                    open = !open;
                    SetSectionOpen(topic, section, open);
                    _tab = section;
                    SavePrefs();
                }

                if (!string.IsNullOrWhiteSpace(summary))
                    EditorGUILayout.LabelField(summary, UtilityWindowTheme.MutedMiniLabelStyle);

                if (open)
                {
                    drawContent?.Invoke();
                }

                sectionRect = scope.rect;
            }

            if (Event.current.type == EventType.Repaint)
                sectionRect = new Rect(sectionRect.x, anchorRect.y, sectionRect.width, Mathf.Max(1f, sectionRect.yMax - anchorRect.y));

            _sectionRects[section] = sectionRect;
            if (_pendingSectionScroll.HasValue && _pendingSectionScroll.Value == section && Event.current.type == EventType.Repaint)
            {
                _contentScroll.y = Mathf.Max(0f, sectionRect.y - 120f);
                _pendingSectionScroll = null;
                Repaint();
            }
        }

        private bool DrawArticleSectionHeader(string label, bool open, bool selected, Color tint, string tooltip)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawInteractiveSurface(rect, tint, selected, hovered);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            Rect glyphRect = new Rect(rect.x + 8f, rect.y + 4f, 16f, rect.height - 8f);
            Rect labelRect = new Rect(glyphRect.xMax + 4f, rect.y + 3f, Mathf.Max(60f, rect.width - 116f), rect.height - 6f);
            Rect stateRect = new Rect(rect.xMax - 84f, rect.y + 4f, 76f, rect.height - 8f);
            GUIStyle headerStyle = new GUIStyle(UtilityWindowTheme.SectionHeaderStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            GUIStyle miniStyle = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };
            GUI.Label(glyphRect, open ? "v" : ">", headerStyle);
            GUI.Label(labelRect, new GUIContent(label, tooltip), headerStyle);
            GUI.Label(stateRect, open ? "Open" : "Collapsed", miniStyle);
            return HandleClick(rect, tooltip);
        }

        private void DrawRelatedResourcesInline(PungentUtilityHelpTopic topic)
        {
            DrawAnnotationLane(topic, PungentUtilityHelpAnchors.Related, "Related", "Related topics, utilities, notes, and documentation links.", showActions: true);
            DrawRelatedTopics(topic);
            DrawRelatedUtilities(topic);
            DrawRelatedNotes(topic);
            DrawDocumentationLinks(topic);
        }

        private bool GetSectionOpen(PungentUtilityHelpTopic topic, ContentTab section)
        {
            string key = SectionFoldoutKey(topic, section);
            if (_sectionFoldouts.TryGetValue(key, out bool open))
                return open;
            return section == ContentTab.QuickUseGuide || section == _tab;
        }

        private void SetSectionOpen(PungentUtilityHelpTopic topic, ContentTab section, bool open)
        {
            _sectionFoldouts[SectionFoldoutKey(topic, section)] = open;
        }

        private static string SectionFoldoutKey(PungentUtilityHelpTopic topic, ContentTab section)
        {
            return (topic == null ? "global" : topic.StableId) + "::" + section;
        }

        private void FocusTopicSection(PungentUtilityHelpTopic topic, ContentTab section)
        {
            SetSectionOpen(topic, section, true);
            _tab = section;
            _pendingSectionScroll = section;
            SavePrefs();
            Repaint();
        }

        private void DrawMissingTopicPage()
        {
            DrawDirectoryHeader("Missing Help Topic", "This help destination has not been written yet.");
            DrawEmptyState("This topic has not been written yet.", "Developer Mode can create a topic stub.");

            if (DeveloperToolsVisible)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Create Help Topic", "Create a manual help topic stub for this destination."), GUILayout.Width(160f)))
                    {
                        PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.CreateTopicStub(_selection.utilityId, _selection.missingSectionId, _selection.missingTopicId);
                        Select(PungentUtilityHelpNavigationSelection.Topic(topic, _navigation.FindUtilityForTopic(topic)));
                    }

                    if (GUILayout.Button(new GUIContent("Report Issue", "Open the bug report relay form for this missing help route."), GUILayout.Width(104f)))
                        PungentBugReportOverlay.Show(PopupRect(), BuildMissingTopicReportContext());

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDirectoryHeader(string title, string summary)
        {
            DrawArticleHeading(title);
            DrawArticleParagraph(summary);
            DrawArticleDivider();
        }

        private void DrawDirectoryCard(string title, string summary, string actionLabel, Action action, Color tint)
        {
            DrawArticleLinkBlock(title, summary, actionLabel, "Open " + title, action, tint);
        }

        private void DrawArticleHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.TitleStyle);
        }

        private void DrawArticleSubheading(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.SectionHeaderStyle);
        }

        private void DrawArticleParagraph(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.BodyStyle);
        }

        private void DrawArticleDivider()
        {
            EditorGUILayout.Space(4f);
        }

        private void DrawArticleCallout(string title, string body, Color tint)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.03f, 7, 4)))
            {
                DrawArticleSubheading(title);
                DrawArticleParagraph(body);
            }
        }

        private void DrawArticleLinkBlock(string title, string summary, string actionLabel, string tooltip, Action action, Color tint)
        {
            DrawClickableArticleCard(title, summary, actionLabel, tooltip, action, tint, false, 82f);
        }

        private bool DrawClickableArticleCard(string title, string summary, string footerLabel, string tooltip, Action action, Color tint, bool selected, float minHeight)
        {
            Rect rect;
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.14f : 0.06f, selected ? 0.07f : 0.025f, 8, 4), GUILayout.MinHeight(minHeight)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(title, tooltip), selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle, GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                }

                DrawArticleParagraph(summary);

                if (!string.IsNullOrWhiteSpace(footerLabel))
                    DrawCardFooterLink(footerLabel + " ->", tooltip);

                rect = scope.rect;
            }

            DrawInteractiveSurface(rect, tint, selected, rect.Contains(Event.current.mousePosition));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (HandleClick(rect, tooltip))
            {
                action?.Invoke();
                return true;
            }

            return false;
        }

        private bool DrawTextLink(string label, string tooltip, bool selected, GUIStyle baseStyle, params GUILayoutOption[] options)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            GUIStyle style = new GUIStyle(baseStyle ?? UtilityWindowTheme.LinkStyle)
            {
                wordWrap = true,
                normal = { textColor = selected ? UtilityWindowTheme.TitleText : UtilityWindowTheme.Cyan },
                hover = { textColor = UtilityWindowTheme.Teal },
                active = { textColor = UtilityWindowTheme.Green },
                padding = new RectOffset(2, 2, 2, 2)
            };

            GUIContent content = new GUIContent(label, tooltip);
            GUILayoutOption[] resolvedOptions = options == null || options.Length == 0 ? new[] { GUILayout.ExpandWidth(true), GUILayout.MinHeight(22f) } : options;
            Rect rect = GUILayoutUtility.GetRect(content, style, resolvedOptions);
            bool hovered = rect.Contains(Event.current.mousePosition);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (hovered || selected)
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, Mathf.Min(rect.width, Mathf.Max(24f, label.Length * 6f)), selected ? 2f : 1f), selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Cyan);

            if (ClickBlockedByNavigationDrawer(Event.current.mousePosition))
                return false;

            return GUI.Button(rect, content, style);
        }

        private void DrawCardFooterLink(string label, string tooltip)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;

            GUIStyle style = new GUIStyle(UtilityWindowTheme.LinkStyle)
            {
                normal = { textColor = UtilityWindowTheme.Cyan },
                hover = { textColor = UtilityWindowTheme.Teal },
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), style, GUILayout.Height(21f));
        }

        private bool DrawClickableLinkRow(string label, string tooltip, bool selected, Color tint, Action action)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(25f));
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawInteractiveSurface(rect, tint, selected, hovered);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, rect.height - 6f);
            GUIStyle style = new GUIStyle(selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = selected ? UtilityWindowTheme.TitleText : UtilityWindowTheme.Cyan },
                hover = { textColor = UtilityWindowTheme.Teal },
                active = { textColor = UtilityWindowTheme.Green }
            };
            GUI.Label(labelRect, new GUIContent(label, tooltip), style);

            if (!HandleClick(rect, tooltip))
                return false;

            action?.Invoke();
            return true;
        }

        private void DrawInteractiveSurface(Rect rect, Color tint, bool selected, bool hovered)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Color border = new Color(tint.r, tint.g, tint.b, selected ? 0.55f : hovered ? 0.34f : 0.18f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

            if (selected)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), UtilityWindowTheme.Teal);
            else if (hovered)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), new Color(tint.r, tint.g, tint.b, 0.85f));
        }

        private bool HandleClick(Rect rect, string tooltip)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseUp || current.button != 0 || !rect.Contains(current.mousePosition))
                return false;

            if (ClickBlockedByNavigationDrawer(current.mousePosition))
                return false;

            if (!string.IsNullOrEmpty(tooltip))
                GUI.tooltip = tooltip;
            current.Use();
            return true;
        }

        private bool ClickBlockedByNavigationDrawer(Vector2 mousePosition)
        {
            return !_drawingNavigationOverlayDrawer &&
                   _lastNavigationDrawerRect.width > 0f &&
                   EditorApplication.timeSinceStartup <= _navigationPeekUntil &&
                   _lastNavigationDrawerRect.Contains(mousePosition);
        }

        private void DrawDirectoryListRow(string title, string summary, string actionLabel, Action action, Color tint)
        {
            DrawClickableArticleCard(title, summary, actionLabel, "Open " + title, action, tint, false, 58f);
        }

        private void DrawUtilityDirectoryCard(PungentUtilityHelpUtilityNode utility)
        {
            if (utility == null)
                return;

            DrawDirectoryCard(
                utility.displayName,
                (string.IsNullOrWhiteSpace(utility.summary) ? "Utility help directory." : utility.summary) + "\n" + utility.topics.Count + " topics.",
                "Open utility",
                () => Select(PungentUtilityHelpNavigationSelection.Utility(utility.categoryId, utility.subcategoryId, utility.id)),
                UtilityWindowTheme.Teal);
        }

        private void DrawTopicLinkCard(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return;

            PungentUtilityHelpNavigationSelection target = PungentUtilityHelpNavigationSelection.Topic(topic, _navigation.FindUtilityForTopic(topic));
            bool selected = _selection != null && _selection.SameTarget(target);
            Color tint = topic.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal;
            string title = string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title;
            string summary = string.IsNullOrWhiteSpace(topic.summary) ? "Open this help topic." : topic.summary;
            string metadata = DeveloperToolsVisible ? topic.StableId : (topic.generated ? "Generated draft" : "Guide topic");

            Rect rect;
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.16f : 0.10f, selected ? 0.08f : 0.04f, 8, 4), GUILayout.MinHeight(86f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(title, "Open this help topic."), selected ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.LinkStyle, GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                    if (DeveloperToolsVisible)
                        PungentUtilityHelpSourceBadge.DrawTopicBadges(topic);
                    else
                        UtilityWindowTheme.InfoPill(new GUIContent(topic.generated ? "Draft" : "Guide", topic.generated ? "Generated help draft." : "Guide topic."), topic.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, 52f);
                }

                EditorGUILayout.LabelField(summary, UtilityWindowTheme.BodyStyle);
                EditorGUILayout.LabelField(metadata, DeveloperToolsVisible ? UtilityWindowTheme.PathLabelStyle : UtilityWindowTheme.MutedMiniLabelStyle);
                DrawCardFooterLink("Read topic ->", "Open this help topic.");

                rect = scope.rect;
            }

            DrawInteractiveSurface(rect, tint, selected, rect.Contains(Event.current.mousePosition));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (HandleClick(rect, "Open this help topic."))
                SelectIfDifferent(target);
        }

        private void DrawTopicHeader(PungentUtilityHelpTopic topic)
        {
            DrawArticleHeading(string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title);

            if (!string.IsNullOrWhiteSpace(topic.summary))
                DrawArticleParagraph(topic.summary);

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.InfoPill(new GUIContent(topic.generated ? "Generated draft" : "Guide", topic.generated ? "Generated help draft." : "Curated or manual guide content."), topic.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, topic.generated ? 112f : 54f);
                int controlCount = CountVisibleFeatureEntries(topic);
                int troubleCount = topic.troubleshootingEntries == null ? 0 : topic.troubleshootingEntries.Count;
                int apiCount = CountVisibleScriptingEntries(topic);
                UtilityWindowTheme.CountPill(controlCount + " controls", controlCount > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, 86f);
                UtilityWindowTheme.CountPill(troubleCount + " fixes", troubleCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 72f);
                UtilityWindowTheme.CountPill(apiCount + " API", apiCount > 0 ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral, 68f);
                GUILayout.FlexibleSpace();
            }

            DrawAnnotationLane(topic, PungentUtilityHelpAnchors.Summary, "Topic summary", topic.summary, showActions: false);
            DrawTopicAnnotationToolbar(topic);

            DrawArticleDivider();
        }

        private void DrawTopicDeveloperDetails(PungentUtilityHelpTopic topic)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Developer Topic Details", UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpSourceBadge.DrawTopicBadges(topic);
                }

                EditorGUILayout.LabelField(new GUIContent(topic.StableId + " | " + topic.sourceOwner, "Stable help topic ID and source owner."), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawTopicAnnotationToolbar(PungentUtilityHelpTopic topic)
        {
            bool narrow = position.width < 860f;
            Action drawToggles = () =>
            {
                EditorGUI.BeginChangeCheck();
                _showNotes = GUILayout.Toggle(_showNotes, new GUIContent("Show Notes", "Show note markers on help content."), EditorStyles.miniButton, GUILayout.Width(92f));
                _showBookmarks = GUILayout.Toggle(_showBookmarks, new GUIContent("Show Bookmarks", "Show bookmark markers on help content."), EditorStyles.miniButton, GUILayout.Width(116f));
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            };

            Action drawActions = () =>
            {
                using (new EditorGUI.DisabledScope(!PungentUtilityHelpNotesBridge.Available || PungentUtilityHelpNotesBridge.CreateNoteForAnchor == null))
                {
                    if (GUILayout.Button(new GUIContent("Add Topic Note", PungentUtilityHelpNotesBridge.Available ? "Create a personal note linked to this help topic." : "Notes integration is not available."), EditorStyles.miniButton, GUILayout.Width(112f)))
                        CreateNoteForAnchor(topic, PungentUtilityHelpAnchors.Topic, topic.summary);
                }

                string bookmarkLabel = PungentUtilityHelpAnnotationStorage.instance.HasBookmark(topic.StableId, PungentUtilityHelpAnchors.Topic) ? "Bookmarked" : "Bookmark Topic";
                if (GUILayout.Button(new GUIContent(bookmarkLabel, "Toggle a local bookmark for this help topic."), EditorStyles.miniButton, GUILayout.Width(112f)))
                    ToggleBookmark(topic, PungentUtilityHelpAnchors.Topic, "Bookmark: " + topic.title, topic.summary);

                using (new EditorGUI.DisabledScope(!PungentUtilityHelpNotesBridge.Available || PungentUtilityHelpNotesBridge.OpenRelatedNotes == null))
                {
                    if (GUILayout.Button(new GUIContent("Open Notes", PungentUtilityHelpNotesBridge.Available ? "Open related notes for this help topic." : "Notes integration is not available."), EditorStyles.miniButton, GUILayout.Width(88f)))
                        PungentUtilityHelpNotesBridge.OpenRelatedNotes(topic);
                }
            };

            if (narrow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentUtilityHelpButton.Draw("help-browser", "annotations", "annotations", "Open help for notes, bookmarks, and annotation markers.", "Annotation toolbar");
                    GUILayout.Space(4f);
                    drawToggles();
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    drawActions();
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentUtilityHelpButton.Draw("help-browser", "annotations", "annotations", "Open help for notes, bookmarks, and annotation markers.", "Annotation toolbar");
                    GUILayout.Space(4f);
                    drawToggles();
                    GUILayout.Space(8f);
                    drawActions();
                    GUILayout.FlexibleSpace();
                }
            }

            if (!PungentUtilityHelpNotesBridge.Available)
                EditorGUILayout.LabelField("Notes integration is not available. Local bookmarks still work.", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawTopicDeveloperActions(PungentUtilityHelpTopic topic)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Copy Topic ID", "Copy the stable topic ID."), EditorStyles.miniButton, GUILayout.Width(96f)))
                    PungentUtilityHelpDeveloperTools.CopyTopicId(topic);
                if (GUILayout.Button(new GUIContent("Copy Help Link", "Copy a stable help link for this topic."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    PungentUtilityHelpDeveloperTools.CopyHelpLink(topic);
                if (GUILayout.Button(new GUIContent("Create Topic Stub", "Create or open a manual override stub for this topic."), EditorStyles.miniButton, GUILayout.Width(118f)))
                {
                    PungentUtilityHelpDeveloperTools.GetEditableTopic(topic);
                    _status = "Manual topic override ready.";
                }
                if (GUILayout.Button(new GUIContent("Report Issue", "Open the bug report relay form for this help topic."), EditorStyles.miniButton, GUILayout.Width(92f)))
                    PungentBugReportOverlay.Show(PopupRect(), BuildReportContext(topic, "Help Browser topic header"));

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawQuickUse(PungentUtilityHelpTopic topic)
        {
            DrawArticleSubheading("Overview Guide");

            if (string.IsNullOrWhiteSpace(topic.quickUseMarkdown))
            {
                DrawEmptyState(
                    "This guide does not have a written walkthrough yet.",
                    "Use Controls, Troubleshooting, Examples/API, or Related for the available reference material.");
            }
            else
            {
                DrawAnnotationLane(topic, PungentUtilityHelpAnchors.QuickUse, "Guide", topic.quickUseMarkdown, showActions: true);
                DrawGuideStartHere(topic);
            }

            DrawGuideFeaturePreview(topic);
            DrawGuideTroubleshootingPreview(topic);
        }

        private void DrawGuideStartHere(PungentUtilityHelpTopic topic)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.09f, 0.04f, 7, 4)))
            {
                DrawArticleSubheading("Start Here");
                List<string> steps = BuildGuideSteps(topic.quickUseMarkdown);
                if (steps.Count == 0)
                {
                    EditorGUILayout.LabelField(topic.quickUseMarkdown.Trim(), UtilityWindowTheme.BodyStyle);
                    return;
                }

                for (int i = 0; i < steps.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.InfoPill(new GUIContent((i + 1).ToString(), "Step " + (i + 1)), UtilityWindowTheme.Teal, 28f);
                        EditorGUILayout.LabelField(steps[i], UtilityWindowTheme.BodyStyle);
                    }
                }
            }
        }

        private void DrawGuideFeaturePreview(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpFeatureEntry> entries = GetVisibleFeatureEntries(topic).Take(4).ToList();
            if (entries.Count == 0)
                return;

            DrawArticleSubheading("What You Can Do");
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(entries.Count + " shown", UtilityWindowTheme.Teal, 76f);
                GUILayout.FlexibleSpace();
            }
            for (int i = 0; i < entries.Count; i++)
                DrawGuideFeatureCard(topic, entries[i]);

            if (CountVisibleFeatureEntries(topic) > entries.Count)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (DrawTextLink("View all controls ->", "Open the Controls section for every documented control and concept.", false, UtilityWindowTheme.LinkStyle, GUILayout.Width(128f), GUILayout.Height(20f)))
                    {
                        FocusTopicSection(topic, ContentTab.FeatureIndex);
                    }
                }
            }
        }

        private void DrawGuideFeatureCard(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            string anchor = PungentUtilityHelpAnchors.Feature(entry.id);
            DrawAnnotationLane(topic, anchor, entry.label, entry.description, showActions: true);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(entry.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, 0.10f, 0.04f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(entry.label) ? entry.id : entry.label, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (DeveloperToolsVisible)
                        PungentUtilityHelpSourceBadge.DrawFeatureBadges(topic, entry);
                }

                if (!string.IsNullOrWhiteSpace(entry.description))
                    EditorGUILayout.LabelField(entry.description, UtilityWindowTheme.BodyStyle);
                if (!string.IsNullOrWhiteSpace(entry.location))
                    EditorGUILayout.LabelField("Where: " + entry.location, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(entry.safetyNotes))
                    DrawArticleCallout("Safety Note", entry.safetyNotes, UtilityWindowTheme.Amber);
            }
        }

        private void DrawGuideTroubleshootingPreview(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpTroubleshootingEntry> entries = topic.troubleshootingEntries ?? new List<PungentUtilityHelpTroubleshootingEntry>();
            if (entries.Count == 0)
                return;

            PungentUtilityHelpTroubleshootingEntry first = entries.FirstOrDefault(e => e != null);
            if (first == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.08f, 0.03f, 6, 3)))
            {
                EditorGUILayout.LabelField("If Something Looks Wrong", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField(first.symptom, UtilityWindowTheme.BodyStyle);
                if (!string.IsNullOrWhiteSpace(first.nextStep))
                    EditorGUILayout.LabelField("Try: " + first.nextStep, UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (DrawTextLink("Open troubleshooting ->", "Show all troubleshooting entries for this topic.", false, UtilityWindowTheme.LinkStyle, GUILayout.Width(154f), GUILayout.Height(20f)))
                    {
                        FocusTopicSection(topic, ContentTab.Troubleshooting);
                    }
                }
            }
        }

        private List<PungentUtilityHelpFeatureEntry> GetVisibleFeatureEntries(PungentUtilityHelpTopic topic)
        {
            return (topic == null ? new List<PungentUtilityHelpFeatureEntry>() : topic.featureEntries ?? new List<PungentUtilityHelpFeatureEntry>())
                .Where(e => e != null)
                .Where(e => PungentUtilityHelpRegistry.EntryVisible(e, ShowDeveloperEntries, ShowHiddenEntries, ShowGeneratedEntries))
                .ToList();
        }

        private int CountVisibleFeatureEntries(PungentUtilityHelpTopic topic)
        {
            return GetVisibleFeatureEntries(topic).Count;
        }

        private int CountVisibleScriptingEntries(PungentUtilityHelpTopic topic)
        {
            return (topic == null ? new List<PungentUtilityHelpScriptingEntry>() : topic.scriptingEntries ?? new List<PungentUtilityHelpScriptingEntry>())
                .Where(e => e != null)
                .Count(e => PungentUtilityHelpRegistry.EntryVisible(e, ShowDeveloperEntries, ShowHiddenEntries, ShowGeneratedEntries));
        }

        private static List<string> BuildGuideSteps(string quickUse)
        {
            List<string> steps = new List<string>();
            if (string.IsNullOrWhiteSpace(quickUse))
                return steps;

            string[] lines = quickUse
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => CleanGuideStep(line))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length > 1)
                return lines.Take(6).ToList();

            string single = lines.Length == 1 ? lines[0] : CleanGuideStep(quickUse);
            string[] sentences = Regex.Split(single, @"(?<=[.!?])\s+")
                .Select(sentence => CleanGuideStep(sentence))
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .ToArray();

            if (sentences.Length > 1)
                return sentences.Take(6).ToList();

            if (!string.IsNullOrWhiteSpace(single))
                steps.Add(single);

            return steps;
        }

        private static string CleanGuideStep(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            trimmed = Regex.Replace(trimmed, @"^\s*[-*]\s+", string.Empty);
            trimmed = Regex.Replace(trimmed, @"^\s*\d+[\.)]\s+", string.Empty);
            return trimmed.Trim();
        }

        private void DrawFeatureIndex(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpFeatureEntry> entries = GetVisibleFeatureEntries(topic);

            if (entries.Count == 0)
            {
                DrawEmptyState(
                    "No controls or concepts have been documented for this topic yet.",
                    "Use the Guide, Troubleshooting, Examples/API, or Related sections for the available help.");
                return;
            }

            DrawArticleSubheading("Controls and Concepts");
            foreach (PungentUtilityHelpFeatureEntry entry in entries)
                DrawFeatureEntry(topic, entry);
        }

        private void DrawFeatureEntry(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            string anchor = PungentUtilityHelpAnchors.Feature(entry.id);
            DrawAnnotationLane(topic, anchor, entry.label, entry.description, showActions: true);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(entry.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, 0.12f, 0.06f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(entry.label) ? entry.id : entry.label, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (DeveloperToolsVisible)
                        PungentUtilityHelpSourceBadge.DrawFeatureBadges(topic, entry);
                }

                if (!string.IsNullOrWhiteSpace(entry.description))
                    EditorGUILayout.LabelField(entry.description, UtilityWindowTheme.BodyStyle);
                if (!string.IsNullOrWhiteSpace(entry.location))
                    EditorGUILayout.LabelField("Where: " + entry.location, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(entry.safetyNotes))
                    DrawArticleCallout("Safety Note", entry.safetyNotes, UtilityWindowTheme.Amber);
            }
        }

        private void DrawFeatureDeveloperTools(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.04f, 5, 3)))
            {
                EditorGUILayout.LabelField("Developer Curation", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUI.BeginChangeCheck();
                string description = EditorGUILayout.TextField(new GUIContent("Edit Description", "Manual feature/tooltip description override."), entry.description);
                string safety = EditorGUILayout.TextField(new GUIContent("Edit Safety Notes", "Manual feature safety note override."), entry.safetyNotes);
                if (EditorGUI.EndChangeCheck())
                {
                    PungentUtilityHelpFeatureEntry editable = PungentUtilityHelpDeveloperTools.GetEditableFeatureEntry(topic, entry);
                    editable.description = description;
                    editable.safetyNotes = safety;
                    editable.generated = false;
                    editable.needsBetterWording = false;
                    PungentUtilityHelpStorage.instance.Persist();
                    _status = "Saved feature help override.";
                    MarkCacheDirty();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Mark Shippable", "Promote this generated feature/tooltip entry into normal help."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    {
                        PungentUtilityHelpDeveloperTools.MarkFeatureShippable(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Hide", "Hide this entry outside Developer Mode."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    {
                        PungentUtilityHelpDeveloperTools.HideFeatureFromShippable(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Needs Wording", "Keep this entry for review and mark the wording as weak."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    {
                        PungentUtilityHelpDeveloperTools.MarkFeatureNeedsBetterWording(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Ignore", "Mark this generated tooltip candidate as a false positive."), EditorStyles.miniButton, GUILayout.Width(58f)))
                    {
                        PungentUtilityHelpDeveloperTools.IgnoreFeatureFalsePositive(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Reset", "Remove manual curation for this entry."), EditorStyles.miniButton, GUILayout.Width(56f)))
                    {
                        PungentUtilityHelpDeveloperTools.ResetFeatureEntryToGenerated(topic, entry);
                        MarkCacheDirty();
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(entry.sourcePath) && string.IsNullOrWhiteSpace(entry.location)))
                    {
                        if (GUILayout.Button(new GUIContent("Copy Source", "Copy the generated source location."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        {
                            EditorGUIUtility.systemCopyBuffer = !string.IsNullOrWhiteSpace(entry.sourcePath)
                                ? entry.sourcePath + (entry.sourceLine > 0 ? ":" + entry.sourceLine : string.Empty)
                                : entry.location;
                            _status = "Copied feature source.";
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawScriptingIndex(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpScriptingEntry> entries = (topic.scriptingEntries ?? new List<PungentUtilityHelpScriptingEntry>())
                .Where(e => PungentUtilityHelpRegistry.EntryVisible(e, ShowDeveloperEntries, ShowHiddenEntries, ShowGeneratedEntries))
                .OrderBy(e => e.declaringType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.memberName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entries.Count == 0)
            {
                DrawEmptyState("No scripting entries are available for this topic.", "Use the Guide, Controls, Troubleshooting, or Related sections for available help.");
                return;
            }

            DrawArticleSubheading("Examples and Scripting Reference");
            foreach (IGrouping<string, PungentUtilityHelpScriptingEntry> group in entries.GroupBy(e => string.IsNullOrWhiteSpace(e.declaringType) ? "Unknown Type" : e.declaringType))
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.08f, 0.04f, 5, 4)))
                {
                    UtilityWindowTheme.SectionTitle(group.Key, UtilityWindowTheme.Blue, group.Count().ToString());
                    foreach (PungentUtilityHelpScriptingEntry entry in group)
                        DrawScriptingEntry(topic, entry);
                }
            }
        }

        private void DrawScriptingEntry(PungentUtilityHelpTopic topic, PungentUtilityHelpScriptingEntry entry)
        {
            string anchor = PungentUtilityHelpAnchors.Scripting(entry.id);
            DrawAnnotationLane(topic, anchor, entry.memberName, entry.description, showActions: true);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(entry.hidden ? UtilityWindowTheme.Red : entry.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Blue, 0.12f, 0.06f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.memberName, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (DeveloperToolsVisible)
                        PungentUtilityHelpSourceBadge.DrawScriptingBadges(topic, entry);
                }

                if (!string.IsNullOrWhiteSpace(entry.signature))
                    EditorGUILayout.SelectableLabel(entry.signature, EditorStyles.textField, GUILayout.Height(20f));
                if (!string.IsNullOrWhiteSpace(entry.declaringType))
                    EditorGUILayout.LabelField("Type: " + entry.declaringType, UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(entry.description) ? "Placeholder/missing description." : entry.description, UtilityWindowTheme.BodyStyle);
                if (!string.IsNullOrWhiteSpace(entry.usageNotes))
                    DrawArticleCallout("Usage Note", entry.usageNotes, UtilityWindowTheme.Cyan);
                if (!string.IsNullOrWhiteSpace(entry.minimalExample))
                {
                    EditorGUILayout.LabelField("Minimal example", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.TextArea(entry.minimalExample, GUILayout.MinHeight(44f));
                }
            }
        }

        private void DrawScriptingDeveloperTools(PungentUtilityHelpTopic topic, PungentUtilityHelpScriptingEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.04f, 5, 3)))
            {
                EditorGUILayout.LabelField("Developer Curation", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUI.BeginChangeCheck();
                string description = EditorGUILayout.TextField(new GUIContent("Edit Description", "Manual description override."), entry.description);
                string example = EditorGUILayout.TextField(new GUIContent("Edit Example", "Manual minimal example override."), entry.minimalExample);
                if (EditorGUI.EndChangeCheck())
                {
                    PungentUtilityHelpScriptingEntry editable = PungentUtilityHelpDeveloperTools.GetEditableScriptingEntry(topic, entry);
                    editable.description = description;
                    editable.minimalExample = example;
                    editable.generated = false;
                    PungentUtilityHelpStorage.instance.Persist();
                    _status = "Saved scripting help override.";
                    MarkCacheDirty();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Mark Shippable", "Show this entry in normal help."), EditorStyles.miniButton, GUILayout.Width(104f)))
                    {
                        PungentUtilityHelpDeveloperTools.MarkShippable(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Hide From Shippable Help", "Hide this entry outside Developer Mode."), EditorStyles.miniButton, GUILayout.Width(154f)))
                    {
                        PungentUtilityHelpDeveloperTools.HideFromShippable(topic, entry);
                        MarkCacheDirty();
                    }
                    if (GUILayout.Button(new GUIContent("Reset To Generated", "Remove manual curation for this entry."), EditorStyles.miniButton, GUILayout.Width(122f)))
                    {
                        PungentUtilityHelpDeveloperTools.ResetScriptingEntryToGenerated(topic, entry);
                        MarkCacheDirty();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawTroubleshooting(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpTroubleshootingEntry> entries = topic.troubleshootingEntries ?? new List<PungentUtilityHelpTroubleshootingEntry>();
            if (entries.Count == 0)
            {
                DrawEmptyState(
                    "No troubleshooting entries have been written for this topic.",
                    "Check the Guide, Controls, Examples/API, or Related sections for adjacent help.");
                return;
            }

            foreach (PungentUtilityHelpTroubleshootingEntry entry in entries.Where(e => e != null))
            {
                string anchor = PungentUtilityHelpAnchors.Troubleshooting(entry.id);
                DrawAnnotationLane(topic, anchor, entry.symptom, entry.nextStep, showActions: true);
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f, 6, 3)))
                {
                    EditorGUILayout.LabelField(entry.symptom, UtilityWindowTheme.SectionHeaderStyle);
                    if (!string.IsNullOrWhiteSpace(entry.likelyCause))
                        EditorGUILayout.LabelField("Why it happens: " + entry.likelyCause, UtilityWindowTheme.BodyStyle);
                    if (!string.IsNullOrWhiteSpace(entry.nextStep))
                        DrawArticleCallout("Try This", entry.nextStep, UtilityWindowTheme.Amber);
                }
            }
        }

        private void DrawRightReadingControls(PungentUtilityHelpTopic topic)
        {
            if (ShouldShowSectionRail())
                DrawSectionRail(topic);
        }

        private void DrawRightTopicToolbar(PungentUtilityHelpTopic topic)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.055f, 0.025f, 4, 2), GUILayout.Width(36f), GUILayout.ExpandHeight(true)))
            {
                DrawOverlayIconButton("Req", "Req", "Create or open support requests for this topic.", HelpOverlayKind.Requests);
                DrawOverlayIconButton("TOC", "TOC", "Show topic section navigation and rail settings.", HelpOverlayKind.Sections);
                DrawOverlayIconButton("Rel", "Rel", "Show related topics and utilities.", HelpOverlayKind.Related);
                DrawOverlayIconButton("Doc", "Doc", "Show documentation links for this topic and utility.", HelpOverlayKind.Links);
                DrawOverlayIconButton("Note", "Note", "Show notes and bookmarks for this topic.", HelpOverlayKind.Notes);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawCompactTopicToolbar(PungentUtilityHelpTopic topic)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 4, 2)))
            {
                EditorGUILayout.LabelField("Topic tools", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(72f));
                DrawOverlayIconButton("Request", "Req", "Create or open support requests for this topic.", HelpOverlayKind.Requests);
                DrawOverlayIconButton("Sections", "Sections", "Show topic section navigation and rail settings.", HelpOverlayKind.Sections);
                DrawOverlayIconButton("Related", "Related", "Show related topics and utilities.", HelpOverlayKind.Related);
                DrawOverlayIconButton("Links", "Links", "Show documentation links for this topic and utility.", HelpOverlayKind.Links);
                DrawOverlayIconButton("Notes", "Notes", "Show notes and bookmarks for this topic.", HelpOverlayKind.Notes);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawOverlayIconButton(string label, string shortName, string tooltip, HelpOverlayKind kind)
        {
            bool active = _activeHelpOverlay == kind;
            string display = position.width < 760f && !string.IsNullOrWhiteSpace(shortName) ? shortName : label;
            float width = display.Length <= 4 ? 30f : Mathf.Clamp(display.Length * 7f + 18f, 48f, 86f);
            Rect rect = GUILayoutUtility.GetRect(width, 24f, GUILayout.Width(width), GUILayout.Height(24f));
            Color tint = OverlayTint(kind);
            bool hovered = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                float fillAlpha = active ? 0.32f : hovered ? 0.16f : 0.055f;
                float borderAlpha = active ? 0.86f : hovered ? 0.52f : 0.24f;
                DrawStudioBox(rect, new Color(tint.r, tint.g, tint.b, fillAlpha), new Color(tint.r, tint.g, tint.b, borderAlpha));
                GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(2, 2, 0, 0)
                };
                Color previous = GUI.contentColor;
                GUI.contentColor = active
                    ? UtilityWindowTheme.TitleText
                    : (EditorGUIUtility.isProSkin ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.18f, 0.19f, 0.21f));
                GUI.Label(rect, new GUIContent(display, tooltip), style);
                GUI.contentColor = previous;
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
                OpenHelpOverlay(kind, rect);
        }

        private static Color OverlayTint(HelpOverlayKind kind)
        {
            switch (kind)
            {
                case HelpOverlayKind.Related: return UtilityWindowTheme.Cyan;
                case HelpOverlayKind.Links: return UtilityWindowTheme.Blue;
                case HelpOverlayKind.Notes: return UtilityWindowTheme.Green;
                case HelpOverlayKind.Requests: return UtilityWindowTheme.Teal;
                case HelpOverlayKind.Developer: return UtilityWindowTheme.Purple;
                default: return UtilityWindowTheme.Teal;
            }
        }

        private static void DrawStudioBox(Rect rect, Color fill, Color border)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private void DrawSectionRail(PungentUtilityHelpTopic topic)
        {
            List<HelpRailAnchor> anchors = BuildRailAnchors(topic);
            bool showLabels = ShouldShowSectionRailLabels();
            ContentTab? previousHover = _hoveredRailSection;
            _hoveredRailSection = null;
            string previousAnchorHover = _hoveredRailAnchorLabel;
            _hoveredRailAnchorLabel = string.Empty;
            using (EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.035f, 0.018f, 3, 2), GUILayout.Width(SectionRailWidth), GUILayout.ExpandHeight(true)))
            {
                Rect railRect = GUILayoutUtility.GetRect(SectionRailWidth - 8f, Mathf.Max(160f, anchors.Count * 34f), GUILayout.ExpandHeight(true));
                float centerX = railRect.x + 11f;
                float top = railRect.y + 18f;
                float bottom = railRect.yMax - 18f;
                Rect trackRect = new Rect(centerX - 8f, top, 16f, Mathf.Max(1f, bottom - top));
                Rect labelRegionRect = new Rect(centerX - SectionRailLabelWidth - 10f, top - 8f, SectionRailLabelWidth, Mathf.Max(1f, bottom - top + 16f));
                EditorGUIUtility.AddCursorRect(trackRect, MouseCursor.ResizeVertical);
                Color line = new Color(UtilityWindowTheme.Teal.r, UtilityWindowTheme.Teal.g, UtilityWindowTheme.Teal.b, showLabels ? 0.58f : 0.42f);
                EditorGUI.DrawRect(new Rect(centerX - 0.5f, top, 1f, Mathf.Max(1f, bottom - top)), line);
                DrawSectionRailScrollbar(trackRect, top, bottom);

                List<Rect> dotRects = new List<Rect>(anchors.Count);

                for (int i = 0; i < anchors.Count; i++)
                {
                    HelpRailAnchor anchor = anchors[i];
                    float t = Mathf.Clamp01(anchor.progress);
                    float y = Mathf.Lerp(top, bottom, t);
                    Rect dot = new Rect(centerX - 5f, y - 5f, 10f, 10f);
                    dotRects.Add(dot);
                    bool hovered = dot.Contains(Event.current.mousePosition);
                    bool selected = anchor.selected;
                    if (hovered && topic != null && TryGetSectionFromLabel(anchor.label, out ContentTab hoveredSection))
                        _hoveredRailSection = hoveredSection;
                    if (hovered)
                        _hoveredRailAnchorLabel = anchor.label;

                    Color dotColor = selected ? UtilityWindowTheme.Green : hovered ? UtilityWindowTheme.Cyan : anchor.tint;
                    Color dotFill = new Color(dotColor.r, dotColor.g, dotColor.b, selected || hovered ? 0.92f : 0.58f);
                    DrawRailDot(dot, dotFill, new Color(dotColor.r, dotColor.g, dotColor.b, selected ? 0.96f : 0.64f), selected || hovered);
                    EditorGUIUtility.AddCursorRect(dot, MouseCursor.Link);
                    if (HandleClick(dot, "Jump to " + anchor.label))
                        anchor.action?.Invoke();

                    if (showLabels)
                    {
                        Rect labelRect = new Rect(labelRegionRect.x, y - 9f, labelRegionRect.width - 12f, 18f);
                        bool emphasized = selected || hovered;
                        GUIStyle labelStyle = new GUIStyle(emphasized ? UtilityWindowTheme.SectionHeaderStyle : UtilityWindowTheme.MutedMiniLabelStyle)
                        {
                            alignment = TextAnchor.MiddleRight,
                            clipping = TextClipping.Clip
                        };
                        GUI.Label(labelRect, anchor.label, labelStyle);
                    }
                }

                HandleSectionRailScrollInput(trackRect, dotRects, top, bottom);

                if (Event.current.type == EventType.Repaint)
                {
                    _lastSectionRailRect = scope.rect;
                    _lastSectionRailLabelRect = labelRegionRect;
                }
            }

            if (!Equals(previousHover, _hoveredRailSection) || !string.Equals(previousAnchorHover, _hoveredRailAnchorLabel, StringComparison.Ordinal))
                Repaint();
        }

        private List<HelpRailAnchor> BuildRailAnchors(PungentUtilityHelpTopic topic)
        {
            if (topic != null)
            {
                List<ContentTab> sections = GetAvailableSections(topic);
                List<HelpRailAnchor> anchors = new List<HelpRailAnchor>(sections.Count);
                for (int i = 0; i < sections.Count; i++)
                {
                    ContentTab section = sections[i];
                    ContentTab captured = section;
                    anchors.Add(new HelpRailAnchor
                    {
                        label = SectionLabel(section),
                        progress = GetSectionRailProgress(section, i, sections.Count),
                        selected = _tab == section,
                        tint = SectionTint(section),
                        action = () => FocusTopicSection(topic, captured)
                    });
                }

                return anchors;
            }

            float scrollProgress = GetArticleMaxScroll() <= 1f ? 0f : Mathf.Clamp01(_contentScroll.y / GetArticleMaxScroll());
            return new List<HelpRailAnchor>
            {
                new HelpRailAnchor
                {
                    label = "Top",
                    progress = 0f,
                    selected = scrollProgress < 0.18f,
                    tint = UtilityWindowTheme.Teal,
                    action = () => ScrollArticleToProgress(0f)
                },
                new HelpRailAnchor
                {
                    label = PageMiddleRailLabel(),
                    progress = 0.5f,
                    selected = scrollProgress >= 0.18f && scrollProgress < 0.82f,
                    tint = UtilityWindowTheme.Cyan,
                    action = () => ScrollArticleToProgress(0.5f)
                },
                new HelpRailAnchor
                {
                    label = "End",
                    progress = 1f,
                    selected = scrollProgress >= 0.82f,
                    tint = UtilityWindowTheme.Neutral,
                    action = () => ScrollArticleToProgress(1f)
                }
            };
        }

        private static Color SectionTint(ContentTab section)
        {
            switch (section)
            {
                case ContentTab.Troubleshooting: return UtilityWindowTheme.Amber;
                case ContentTab.ScriptingIndex: return UtilityWindowTheme.Blue;
                case ContentTab.Related: return UtilityWindowTheme.Purple;
                default: return UtilityWindowTheme.Teal;
            }
        }

        private string PageMiddleRailLabel()
        {
            if (_selection == null)
                return "Content";

            switch (_selection.kind)
            {
                case PungentUtilityHelpSelectionKind.Category: return "Modules";
                case PungentUtilityHelpSelectionKind.Subcategory: return "Utilities";
                case PungentUtilityHelpSelectionKind.Utility: return "Topics";
                case PungentUtilityHelpSelectionKind.MissingTopic: return "Stub";
                default: return string.IsNullOrWhiteSpace(_search) ? "Directory" : "Results";
            }
        }

        private static bool TryGetSectionFromLabel(string label, out ContentTab section)
        {
            foreach (ContentTab candidate in Enum.GetValues(typeof(ContentTab)))
            {
                if (string.Equals(SectionLabel(candidate), label, StringComparison.OrdinalIgnoreCase))
                {
                    section = candidate;
                    return true;
                }
            }

            section = ContentTab.QuickUseGuide;
            return false;
        }

        private float GetSectionRailProgress(ContentTab section, int index, int count)
        {
            float maxScroll = GetArticleMaxScroll();
            if (maxScroll > 1f && _sectionRects.TryGetValue(section, out Rect rect))
                return Mathf.Clamp01(rect.y / maxScroll);

            return count <= 1 ? 0.5f : index / (float)(count - 1);
        }

        private float GetArticleMaxScroll()
        {
            return Mathf.Max(0f, _articleContentHeight - Mathf.Max(1f, _contentViewportHeight));
        }

        private void DrawSectionRailScrollbar(Rect trackRect, float top, float bottom)
        {
            float maxScroll = GetArticleMaxScroll();
            if (maxScroll <= 1f)
                return;

            float trackHeight = Mathf.Max(1f, bottom - top);
            float thumbHeight = Mathf.Clamp(trackHeight * Mathf.Clamp01(_contentViewportHeight / Mathf.Max(_articleContentHeight, 1f)), 22f, Mathf.Max(22f, trackHeight));
            float progress = Mathf.Clamp01(_contentScroll.y / maxScroll);
            float thumbY = Mathf.Lerp(top, bottom - thumbHeight, progress);
            Color tint = UtilityWindowTheme.Cyan;
            DrawStudioBox(
                new Rect(trackRect.center.x - 3f, thumbY, 6f, thumbHeight),
                new Color(tint.r, tint.g, tint.b, 0.34f),
                new Color(tint.r, tint.g, tint.b, 0.72f));
        }

        private void HandleSectionRailScrollInput(Rect trackRect, List<Rect> dotRects, float top, float bottom)
        {
            Event current = Event.current;
            if (current == null)
                return;

            bool overDot = dotRects != null && dotRects.Any(dot => dot.Contains(current.mousePosition));

            if (current.type == EventType.MouseDown && current.button == 0 && trackRect.Contains(current.mousePosition) && !overDot)
            {
                _draggingSectionRail = true;
                ScrollArticleToRailPosition(current.mousePosition.y, top, bottom);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && _draggingSectionRail)
            {
                ScrollArticleToRailPosition(current.mousePosition.y, top, bottom);
                current.Use();
            }
            else if ((current.type == EventType.MouseUp || current.rawType == EventType.MouseUp) && _draggingSectionRail)
            {
                _draggingSectionRail = false;
                current.Use();
            }
        }

        private void ScrollArticleToRailPosition(float mouseY, float top, float bottom)
        {
            float maxScroll = GetArticleMaxScroll();
            if (maxScroll <= 1f)
                return;

            float progress = Mathf.InverseLerp(top, bottom, Mathf.Clamp(mouseY, top, bottom));
            _contentScroll.y = Mathf.Clamp(maxScroll * progress, 0f, maxScroll);
            GUI.FocusControl(null);
            Repaint();
        }

        private void ScrollArticleToProgress(float progress)
        {
            float maxScroll = GetArticleMaxScroll();
            _contentScroll.y = Mathf.Clamp(maxScroll * Mathf.Clamp01(progress), 0f, maxScroll);
            GUI.FocusControl(null);
            Repaint();
        }

        private static void DrawRailDot(Rect rect, Color fill, Color outline, bool emphasized)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color old = Handles.color;
            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            Handles.color = fill;
            Handles.DrawSolidDisc(center, Vector3.forward, emphasized ? 5.5f : 4.5f);
            Handles.color = outline;
            Handles.DrawWireDisc(center, Vector3.forward, emphasized ? 5.5f : 4.5f);
            Handles.color = old;
        }

        private List<ContentTab> GetAvailableSections(PungentUtilityHelpTopic topic)
        {
            List<ContentTab> sections = new List<ContentTab>
            {
                ContentTab.QuickUseGuide,
                ContentTab.FeatureIndex,
                ContentTab.Troubleshooting
            };
            if (topic != null && topic.scriptingEntries != null && topic.scriptingEntries.Count > 0)
                sections.Add(ContentTab.ScriptingIndex);
            sections.Add(ContentTab.Related);
            return sections;
        }

        private static string SectionLabel(ContentTab section)
        {
            switch (section)
            {
                case ContentTab.FeatureIndex: return "Controls";
                case ContentTab.ScriptingIndex: return "Examples/API";
                case ContentTab.Troubleshooting: return "Troubleshooting";
                case ContentTab.Related: return "Related";
                default: return "Guide";
            }
        }

        private void OpenHelpOverlay(HelpOverlayKind kind, Rect anchorRect)
        {
            _activeHelpOverlay = kind;
            _helpOverlayAnchorRect = anchorRect;
            _activeHelpOverlayRect = Rect.zero;
            _overlayScroll = Vector2.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void CloseHelpOverlay()
        {
            _activeHelpOverlay = HelpOverlayKind.None;
            _helpOverlayAnchorRect = Rect.zero;
            _activeHelpOverlayRect = Rect.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void HandleActiveHelpOverlayInput()
        {
            if (_activeHelpOverlay == HelpOverlayKind.None)
                return;

            CalculateHelpOverlayRect();
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CloseHelpOverlay();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown &&
                !_activeHelpOverlayRect.Contains(evt.mousePosition) &&
                !_helpOverlayAnchorRect.Contains(evt.mousePosition))
            {
                CloseHelpOverlay();
                evt.Use();
            }
        }

        private Rect CalculateHelpOverlayRect()
        {
            float height = _activeHelpOverlay == HelpOverlayKind.Sections ? 260f : Mathf.Min(420f, Mathf.Max(220f, position.height - 128f));
            float width = _activeHelpOverlay == HelpOverlayKind.Developer ? 420f : 360f;
            width = Mathf.Clamp(width, 260f, Mathf.Max(260f, position.width - 48f));
            float x = _helpOverlayAnchorRect.width > 0f ? _helpOverlayAnchorRect.xMin - width - 8f : position.width - width - 46f;
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));
            float y = _helpOverlayAnchorRect.height > 0f ? _helpOverlayAnchorRect.yMin : 82f;
            y = Mathf.Clamp(y, 52f, Mathf.Max(52f, position.height - height - 24f));
            _activeHelpOverlayRect = new Rect(x, y, width, height);
            return _activeHelpOverlayRect;
        }

        private static void DrawHelpOverlayChrome(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.24f));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.98f) : new Color(0.88f, 0.88f, 0.88f, 0.98f));
        }

        private void DrawHelpOverlayTrays(PungentUtilityHelpTopic topic)
        {
            if (_activeHelpOverlay == HelpOverlayKind.None)
                return;

            Rect trayRect = CalculateHelpOverlayRect();
            DrawHelpOverlayChrome(trayRect);
            GUILayout.BeginArea(trayRect, EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(HelpOverlayTitle(_activeHelpOverlay), UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(58f)))
                    CloseHelpOverlay();
            }

            _overlayScroll = EditorGUILayout.BeginScrollView(_overlayScroll, GUILayout.ExpandHeight(true));
            if (_activeHelpOverlay == HelpOverlayKind.Requests)
                DrawSupportRequestsOverlay(topic);
            else if (topic == null)
                DrawEmptyState("No topic selected.", "Choose a utility topic before using topic overlays.");
            else if (_activeHelpOverlay == HelpOverlayKind.Sections)
                DrawSectionsOverlay(topic);
            else if (_activeHelpOverlay == HelpOverlayKind.Related)
            {
                DrawRelatedTopics(topic);
                DrawRelatedUtilities(topic);
            }
            else if (_activeHelpOverlay == HelpOverlayKind.Links)
                DrawDocumentationLinks(topic);
            else if (_activeHelpOverlay == HelpOverlayKind.Notes)
                DrawRelatedNotes(topic);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string HelpOverlayTitle(HelpOverlayKind kind)
        {
            switch (kind)
            {
                case HelpOverlayKind.Related: return "Related";
                case HelpOverlayKind.Links: return "Documentation Links";
                case HelpOverlayKind.Notes: return "Notes & Bookmarks";
                case HelpOverlayKind.Requests: return "Support Requests";
                case HelpOverlayKind.Developer: return "Developer";
                default: return "Sections";
            }
        }

        private void DrawSupportRequestsOverlay(PungentUtilityHelpTopic topic)
        {
            DrawArticleCallout(
                "Support Requests",
                "Bug reports and feature requests are saved as Sticky Notes drafts first. Send them through the Wix relay when the wording and context are ready.",
                UtilityWindowTheme.Teal);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("New Request", "Create a support request draft for the current help context."), EditorStyles.miniButton, GUILayout.Width(108f)))
                {
                    PungentBugReportOverlay.Show(PopupRect(), topic == null ? BuildGenericReportContext("Help Browser support request") : BuildReportContext(topic, "Help Browser support request"));
                    CloseHelpOverlay();
                }

                if (GUILayout.Button(new GUIContent("Open Requests", "Open Support Requests in Sticky Notes."), EditorStyles.miniButton, GUILayout.Width(112f)))
                {
                    PungentSupportRequestBridge.OpenRequests();
                    CloseHelpOverlay();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSectionsOverlay(PungentUtilityHelpTopic topic)
        {
            DrawNavigationPreferenceStrip();

            DrawArticleDivider();
            foreach (ContentTab section in GetAvailableSections(topic))
            {
                if (DrawClickableLinkRow(SectionLabel(section), "Jump to " + SectionLabel(section), _tab == section, UtilityWindowTheme.Teal, () => FocusTopicSection(topic, section)))
                    CloseHelpOverlay();
            }
        }

        private void DrawNavigationPreferenceStrip()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.035f, 0.018f, 5, 2)))
            {
                EditorGUILayout.LabelField("Reading Navigation", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField("The directory lives in the header; the page rail handles reading position.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawRailModeChip(HelpSectionRailMode.VisibleSlim, "Rail", "Show a slim section rail; labels appear near the rail.");
                    DrawRailModeChip(HelpSectionRailMode.Proximity, "Labels Near", "Keep the rail visible and reveal labels only over the rail or label lane.");
                    DrawRailModeChip(HelpSectionRailMode.LabelsAlways, "Labels", "Keep section rail labels visible.");
                    DrawRailModeChip(HelpSectionRailMode.Hidden, "Off", "Hide the section rail.");
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawSidebarModeChip(HelpSidebarMode mode, string label, string tooltip)
        {
            if (DrawPreferenceChip(label, tooltip, _sidebarMode == mode, UtilityWindowTheme.Cyan, 54f))
            {
                _sidebarMode = mode;
                SavePrefs();
                Repaint();
            }
        }

        private void DrawRailModeChip(HelpSectionRailMode mode, string label, string tooltip)
        {
            if (DrawPreferenceChip(label, tooltip, _sectionRailMode == mode, UtilityWindowTheme.Teal, 58f))
            {
                _sectionRailMode = mode;
                SavePrefs();
                Repaint();
            }
        }

        private bool DrawPreferenceChip(string label, string tooltip, bool active, Color tint, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 22f, GUILayout.Width(width), GUILayout.Height(22f));
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                float fillAlpha = active ? 0.26f : hovered ? 0.13f : 0.055f;
                float borderAlpha = active ? 0.78f : hovered ? 0.48f : 0.22f;
                DrawStudioBox(rect, new Color(tint.r, tint.g, tint.b, fillAlpha), new Color(tint.r, tint.g, tint.b, borderAlpha));
                GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                GUI.Label(rect, new GUIContent(label, tooltip), style);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none) && !active;
        }

        private void DrawRelatedTopics(PungentUtilityHelpTopic topic)
        {
            EditorGUILayout.LabelField("Topics", UtilityWindowTheme.SectionHeaderStyle);
            if (topic.relatedTopicIds == null || topic.relatedTopicIds.Count == 0)
            {
                EditorGUILayout.LabelField("No related topics yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (string id in topic.relatedTopicIds)
            {
                PungentUtilityHelpTopic related = PungentUtilityHelpRegistry.FindByStableId(NormalizeRelatedTopicId(topic.utilityId, id));
                string label = related == null ? id : related.title;
                DrawClickableLinkRow(label, "Open related help topic.", false, UtilityWindowTheme.Cyan, () =>
                {
                    if (related != null)
                        SelectIfDifferent(PungentUtilityHelpNavigationSelection.Topic(related, _navigation.FindUtilityForTopic(related)));
                    else
                        SelectIfDifferent(PungentUtilityHelpNavigationSelection.MissingTopic(topic.utilityId, null, id));
                });
            }
        }

        private void DrawRelatedUtilities(PungentUtilityHelpTopic topic)
        {
            EditorGUILayout.LabelField("Utilities", UtilityWindowTheme.SectionHeaderStyle);
            if (topic.relatedUtilityIds == null || topic.relatedUtilityIds.Count == 0)
            {
                EditorGUILayout.LabelField("No related utilities yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (string id in topic.relatedUtilityIds)
            {
                PungentUtilityHelpUtilityNode utility = _navigation.FindUtility(id);
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(id);
                DrawClickableLinkRow(descriptor == null ? id : descriptor.DisplayName, "Open related utility help directory.", false, UtilityWindowTheme.Cyan, () =>
                {
                    if (utility != null)
                        SelectIfDifferent(PungentUtilityHelpNavigationSelection.Utility(utility.categoryId, utility.subcategoryId, utility.id));
                    else
                        PungentUtilityRegistry.Open(id);
                });
            }
        }

        private void DrawRelatedNotes(PungentUtilityHelpTopic topic)
        {
            EditorGUILayout.LabelField("Notes", UtilityWindowTheme.SectionHeaderStyle);
            if (DrawTextLink("View all Notes & Bookmarks ->", "Open the global Help notes and bookmarks tray.", false, UtilityWindowTheme.LinkStyle, GUILayout.ExpandWidth(true), GUILayout.Height(21f)))
                OpenNotesBookmarksTray(topic);

            if (!PungentUtilityHelpNotesBridge.Available)
            {
                DrawArticleCallout("Notes Unavailable", "Notes integration is not installed or not available. Help topics still work normally.", UtilityWindowTheme.Amber);
                return;
            }

            List<PungentUtilityHelpAnnotation> notes = _annotationCache.Where(a => a.kind == PungentUtilityHelpAnnotationKind.Note).ToList();
            if (notes.Count == 0)
            {
                EditorGUILayout.LabelField("No related notes linked yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (PungentUtilityHelpAnnotation note in notes.Take(6))
                DrawAnnotationMarkerButton(note);
        }

        private void DrawDocumentationLinks(PungentUtilityHelpTopic topic)
        {
            EditorGUILayout.LabelField("Documentation Links", UtilityWindowTheme.SectionHeaderStyle);
            List<PungentUtilityHelpDocumentationLinkRow> rows = PungentUtilityHelpDocumentationLinksProvider.GetRelatedLinks(topic, includeUtilityAssigned: true, includeGlobal: true);
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("No documentation links are attached to this help topic or assigned utility yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                DrawDocumentationLinkManagementActions(topic == null ? null : topic.utilityId);
                return;
            }

            foreach (PungentUtilityHelpDocumentationLinkRow row in rows)
                DrawDocumentationLinkRow(topic, row, topic == null ? null : topic.utilityId);

            DrawDocumentationLinkManagementActions(topic == null ? null : topic.utilityId);
        }

        private void DrawDocumentationLinkRow(PungentUtilityHelpTopic topic, PungentUtilityHelpDocumentationLinkRow row, string utilityIdOverride = null)
        {
            if (row == null)
                return;

            PungentUtilityDocumentationLinks.DocumentationLink link = row.link;
            if (link == null)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f, 5, 2)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill("Missing", UtilityWindowTheme.Amber, 78f);
                        UtilityWindowTheme.CountPill(string.IsNullOrWhiteSpace(row.sourceLabel) ? "Related Topic Link" : row.sourceLabel, UtilityWindowTheme.Purple, 128f);
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.id) ? "Empty documentation link ID" : row.id, UtilityWindowTheme.PathLabelStyle);
                    }
                    EditorGUILayout.LabelField("Stale relatedDocumentationLinkIds entry. The documentation link record could not be resolved.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                return;
            }

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = row.status ?? PungentUtilityDocumentationLinks.GetTargetStatus(link);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(TargetTint(status.kind), 0.10f, 0.05f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(PungentUtilityDocumentationLinks.GetDisplayName(link), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(row.sourceLabel, row.source == PungentUtilityHelpDocumentationLinkSource.Global ? UtilityWindowTheme.Blue : UtilityWindowTheme.Cyan, 128f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 104f);
                    if (link.backlog != null && link.backlog.Count > 0)
                        UtilityWindowTheme.CountPill(link.backlog.Count + " backlog", UtilityWindowTheme.Amber, 84f);
                    EditorGUILayout.LabelField(PungentUtilityDocumentationLinks.GetAssignedUtilitySummary(link), UtilityWindowTheme.MutedMiniLabelStyle);
                }

                string targetText = !string.IsNullOrWhiteSpace(status.targetValue) ? status.targetValue : status.message;
                if (!string.IsNullOrWhiteSpace(targetText))
                    EditorGUILayout.LabelField(targetText, status.canCopy ? UtilityWindowTheme.PathLabelStyle : UtilityWindowTheme.MutedMiniLabelStyle);

                DrawDocumentationTargetState(status);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!status.canOpen))
                    {
                        if (GUILayout.Button(new GUIContent("Open", status.canOpen ? "Open documentation target." : status.message), EditorStyles.miniButton, GUILayout.Width(54f)))
                            OpenDocumentationLinkOrWarn(link);
                    }

                    using (new EditorGUI.DisabledScope(!status.canCopy))
                    {
                        if (GUILayout.Button(new GUIContent("Copy", status.canCopy ? "Copy documentation target." : status.message), EditorStyles.miniButton, GUILayout.Width(54f)))
                        CopyDocumentationTargetOrWarn(link);
                    }

                    if (PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink != null &&
                        GUILayout.Button(new GUIContent("+ Note", "Create a Notes & Roadmap note for this documentation link."), EditorStyles.miniButton, GUILayout.Width(58f)))
                    {
                        PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink(link);
                    }

                    if (PungentUtilityHelpNotesBridge.OpenNotesForDocumentationLink != null)
                    {
                        int noteCount = PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink == null ? 0 : PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink(link.id);
                        bool canOpenNotes = PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink == null || noteCount > 0;
                        string label = noteCount > 0 ? "Notes (" + noteCount + ")" : "Open Notes";
                        if (canOpenNotes && GUILayout.Button(new GUIContent(label, "Open notes referencing this documentation link."), EditorStyles.miniButton, GUILayout.Width(noteCount > 0 ? 82f : 86f)))
                            PungentUtilityHelpNotesBridge.OpenNotesForDocumentationLink(link.id);
                    }

                    if (DeveloperToolsVisible &&
                        GUILayout.Button(new GUIContent("Edit", "Open this documentation link in the authoring popup."), EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        DocumentationLinkEditorPopup.OpenForLink(link.id, string.IsNullOrWhiteSpace(utilityIdOverride) && topic != null ? topic.utilityId : utilityIdOverride);
                    }

                    if (DeveloperToolsVisible &&
                        GUILayout.Button(new GUIContent("Report", "Open the bug report relay form for this documentation link row."), EditorStyles.miniButton, GUILayout.Width(58f)))
                    {
                        PungentBugReportOverlay.Show(PopupRect(), BuildDocumentationLinkReportContext(topic, link, utilityIdOverride));
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDocumentationLinkManagementActions(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Manage Links", "Open Documentation Links for this utility."), EditorStyles.miniButton, GUILayout.Width(96f)))
                    DocumentationLinkEditorPopup.ManageForUtility(utilityId);

                if (DeveloperToolsVisible &&
                    GUILayout.Button(new GUIContent("Create Link For Utility", "Create a new documentation link assigned to this utility."), EditorStyles.miniButton, GUILayout.Width(146f)))
                    DocumentationLinkEditorPopup.CreateForUtility(utilityId);

                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawDocumentationTargetState(PungentUtilityDocumentationLinks.DocumentationTargetStatus status)
        {
            if (status == null)
                return;

            switch (status.kind)
            {
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.Empty:
                    EditorGUILayout.HelpBox("This documentation link has no current target. Set a Unity asset, local path, or web URL in Documentation Links.", MessageType.Info);
                    break;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing:
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget:
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.UnsupportedUrlScheme:
                    EditorGUILayout.HelpBox(status.message, MessageType.Warning);
                    break;
            }
        }

        private void DrawAnnotationLane(PungentUtilityHelpTopic topic, string anchorId, string title, string excerpt, bool showActions)
        {
            if (topic == null)
                return;

            List<PungentUtilityHelpAnnotation> annotations = GetVisibleAnnotations(anchorId);
            bool hasMarkers = annotations.Count > 0;
            if (!hasMarkers && !showActions)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (hasMarkers)
                    EditorGUILayout.LabelField(new GUIContent("Markers", "Annotation markers for " + title), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(54f));
                else
                    GUILayout.Space(54f);

                if (hasMarkers)
                {
                    foreach (PungentUtilityHelpAnnotation annotation in annotations)
                        DrawAnnotationMarkerButton(annotation);
                }

                GUILayout.FlexibleSpace();

                if (showActions)
                {
                    using (new EditorGUI.DisabledScope(!PungentUtilityHelpNotesBridge.Available || PungentUtilityHelpNotesBridge.CreateNoteForAnchor == null))
                    {
                        if (GUILayout.Button(new GUIContent("+ Note", PungentUtilityHelpNotesBridge.Available ? "Add a note for this help block." : "Notes integration is not available."), EditorStyles.miniButton, GUILayout.Width(58f)))
                            CreateNoteForAnchor(topic, anchorId, excerpt);
                    }

                    string bookmarkLabel = PungentUtilityHelpAnnotationStorage.instance.HasBookmark(topic.StableId, anchorId) ? "Bookmarked" : "Bookmark";
                    if (GUILayout.Button(new GUIContent(bookmarkLabel, "Toggle a local bookmark for this help block."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        ToggleBookmark(topic, anchorId, "Bookmark: " + title, excerpt);
                }
            }
        }

        private void DrawAnnotationMarkerButton(PungentUtilityHelpAnnotation annotation)
        {
            if (annotation == null)
                return;

            string tooltip = annotation.title + "\n" + annotation.excerpt + "\n" + annotation.sourceLabel;
            Color tint = annotation.kind == PungentUtilityHelpAnnotationKind.Bookmark ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan;
            using (new GuiBackgroundScope(tint))
            {
                if (GUILayout.Button(new GUIContent(annotation.MarkerLabel, tooltip), EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(20f)))
                {
                    if (PungentUtilityHelpNotesBridge.OpenNote != null &&
                        !string.IsNullOrWhiteSpace(annotation.noteId) &&
                        !string.Equals(annotation.sourceLabel, "Local Help Bookmark", StringComparison.OrdinalIgnoreCase))
                        PungentUtilityHelpNotesBridge.OpenNote(annotation.noteId);
                    else
                        _status = annotation.title;
                }
            }
        }

        private List<PungentUtilityHelpAnnotation> GetVisibleAnnotations(string anchorId)
        {
            string anchor = string.IsNullOrWhiteSpace(anchorId) ? PungentUtilityHelpAnchors.Topic : anchorId;
            return _annotationCache
                .Where(a => a != null && string.Equals(a.anchorId, anchor, StringComparison.OrdinalIgnoreCase))
                .Where(a => (a.kind == PungentUtilityHelpAnnotationKind.Note && _showNotes) || (a.kind == PungentUtilityHelpAnnotationKind.Bookmark && _showBookmarks))
                .ToList();
        }

        private void RefreshAnnotationCacheIfNeeded(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return;

            if (!_annotationCacheDirty && string.Equals(_annotationCacheTopicId, topic.StableId, StringComparison.OrdinalIgnoreCase))
                return;

            _annotationCacheDirty = false;
            _annotationCacheTopicId = topic.StableId;
            _annotationCache.Clear();
            _annotationCache.AddRange(PungentUtilityHelpAnnotationStorage.instance.GetAnnotationsForTopic(topic.StableId));

            if (PungentUtilityHelpNotesBridge.GetAnnotationsForTopic == null)
                return;

            try
            {
                List<PungentUtilityHelpAnnotation> bridgeAnnotations = PungentUtilityHelpNotesBridge.GetAnnotationsForTopic(topic.StableId);
                if (bridgeAnnotations != null)
                    _annotationCache.AddRange(bridgeAnnotations);
            }
            catch (Exception ex)
            {
                _status = "Notes annotations unavailable: " + ex.Message;
            }
        }

        private void CreateNoteForAnchor(PungentUtilityHelpTopic topic, string anchorId, string excerpt)
        {
            if (topic == null || PungentUtilityHelpNotesBridge.CreateNoteForAnchor == null)
                return;

            PungentUtilityHelpNotesBridge.CreateNoteForAnchor(topic, anchorId, excerpt ?? string.Empty);
            _annotationCacheDirty = true;
            _status = "Created note for help anchor.";
        }

        private void ToggleBookmark(PungentUtilityHelpTopic topic, string anchorId, string title, string excerpt)
        {
            PungentUtilityHelpAnnotationStorage.instance.ToggleBookmark(topic, anchorId, title, excerpt);
            _annotationCacheDirty = true;
            _status = "Updated help bookmark.";
        }

        private void OpenDocumentationLinkOrWarn(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (PungentUtilityDocumentationLinks.instance.Open(link, out string error))
            {
                _status = "Opened documentation link.";
                return;
            }

            EditorUtility.DisplayDialog("Documentation Link", error, "OK");
            _status = error;
        }

        private void CopyDocumentationTargetOrWarn(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (PungentUtilityDocumentationLinks.TryCopyTarget(link, out string error))
            {
                _status = "Copied documentation target.";
                return;
            }

            EditorUtility.DisplayDialog("Documentation Link", error, "OK");
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

        private void DrawEmptyState(string message, string action)
        {
            DrawArticleCallout(message, "Next: " + action, UtilityWindowTheme.Cyan);
        }

        private PungentUtilityHelpTopic GetSelectedTopic()
        {
            return _selection != null && _selection.kind == PungentUtilityHelpSelectionKind.Topic
                ? PungentUtilityHelpRegistry.FindByStableId(_selection.topicStableId)
                : null;
        }

        private Rect PopupRect()
        {
            Event current = Event.current;
            return new Rect(current == null ? Vector2.zero : current.mousePosition, Vector2.zero);
        }

        private PungentBugReportContext BuildReportContext(PungentUtilityHelpTopic topic, string label)
        {
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = topic == null ? string.Empty : topic.utilityId,
                sectionId = topic == null ? PungentUtilityHelpIds.DefaultSection : topic.sectionId,
                topicId = topic == null ? PungentUtilityHelpIds.DefaultTopic : topic.topicId,
                contextLabel = label ?? string.Empty,
                contextPath = topic == null ? string.Empty : topic.StableId,
                sourceWindow = "Help Browser",
                helpTab = _tab.ToString()
            };
            context.Normalize();
            return context;
        }

        private PungentBugReportContext BuildGenericReportContext(string label)
        {
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = "help-browser",
                sectionId = PungentUtilityHelpIds.DefaultSection,
                topicId = PungentUtilityHelpIds.DefaultTopic,
                contextLabel = string.IsNullOrWhiteSpace(label) ? "Help Browser support request" : label,
                contextPath = _selection == null ? "Help Browser" : _selection.ToString(),
                sourceWindow = "Help Browser",
                helpTab = _tab.ToString()
            };
            context.Normalize();
            return context;
        }

        private PungentBugReportContext BuildMissingTopicReportContext()
        {
            string utilityId = _selection == null ? string.Empty : _selection.utilityId;
            string sectionId = _selection == null ? PungentUtilityHelpIds.DefaultSection : _selection.missingSectionId;
            string topicId = _selection == null ? PungentUtilityHelpIds.DefaultTopic : _selection.missingTopicId;
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = utilityId,
                sectionId = sectionId,
                topicId = topicId,
                contextLabel = "Missing Help Browser topic",
                contextPath = PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId),
                sourceWindow = "Help Browser",
                helpTab = "MissingTopic"
            };
            context.Normalize();
            return context;
        }

        private PungentBugReportContext BuildReportContext(PungentUtilityHelpReviewRow row)
        {
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = row == null ? string.Empty : row.utilityId,
                sectionId = row == null || row.topic == null ? PungentUtilityHelpIds.DefaultSection : row.topic.sectionId,
                topicId = row == null || row.topic == null ? PungentUtilityHelpIds.DefaultTopic : row.topic.topicId,
                contextLabel = row == null ? "Help Coverage review row" : "Help Coverage: " + row.queueKind,
                contextPath = row == null ? string.Empty : row.topicStableId,
                sourceWindow = "Help Browser",
                helpTab = "Generation Dashboard / " + _reviewQueue,
                sourcePath = row == null ? string.Empty : row.sourcePath,
                sourceLine = row == null ? 0 : row.sourceLine,
                generatedEntryId = row == null ? string.Empty : row.entryId
            };
            context.Normalize();
            return context;
        }

        private PungentBugReportContext BuildDocumentationLinkReportContext(PungentUtilityHelpTopic topic, PungentUtilityDocumentationLinks.DocumentationLink link, string utilityIdOverride)
        {
            string utilityId = !string.IsNullOrWhiteSpace(utilityIdOverride) ? utilityIdOverride : topic == null ? string.Empty : topic.utilityId;
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = utilityId,
                sectionId = topic == null ? "related" : topic.sectionId,
                topicId = topic == null ? "documentation-links" : topic.topicId,
                contextLabel = "Documentation link: " + (link == null ? "(missing)" : PungentUtilityDocumentationLinks.GetDisplayName(link)),
                contextPath = link == null ? string.Empty : link.id,
                sourceWindow = "Help Browser",
                helpTab = "Related / Documentation Links",
                selectedContextId = link == null ? string.Empty : link.id,
                selectedContextLabel = link == null ? string.Empty : PungentUtilityDocumentationLinks.GetDisplayName(link)
            };
            context.Normalize();
            return context;
        }

        private void SelectTopic(string utilityId, string sectionId, string topicId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.Find(utilityId, sectionId, topicId);
            if (topic != null)
            {
                EnsureNavigation();
                Select(PungentUtilityHelpNavigationSelection.Topic(topic, _navigation.FindUtilityForTopic(topic)));
                return;
            }

            Select(PungentUtilityHelpNavigationSelection.MissingTopic(utilityId, sectionId, topicId));
        }

        private void Select(PungentUtilityHelpNavigationSelection selection)
        {
            if (selection == null)
                selection = PungentUtilityHelpNavigationSelection.SearchResults();

            _selection = selection;
            _contentScroll = Vector2.zero;
            _activeHelpOverlay = HelpOverlayKind.None;
            _helpOverlayAnchorRect = Rect.zero;
            _activeHelpOverlayRect = Rect.zero;
            _annotationCacheDirty = true;
            EnsureExpandedForSelection();
            SavePrefs();
            Repaint();
        }

        private void SelectIfDifferent(PungentUtilityHelpNavigationSelection selection)
        {
            if (selection == null)
                selection = PungentUtilityHelpNavigationSelection.SearchResults();

            if (_selection != null && _selection.SameTarget(selection))
                return;

            Select(selection);
        }

        private void RefreshCachesIfNeeded()
        {
            if (!_cacheDirty)
                return;

            _cacheDirty = false;
            _navigation = PungentUtilityHelpNavigationModel.Build(_search, ShowDeveloperEntries, ShowHiddenEntries, ShowGeneratedEntries);
            EnsureSelectionContext();
            EnsureExpandedForSelection();
        }

        private void EnsureNavigation()
        {
            if (_navigation == null || _cacheDirty)
                RefreshCachesIfNeeded();
        }

        private void EnsureSelectionContext()
        {
            if (_selection == null)
                _selection = PungentUtilityHelpNavigationSelection.SearchResults();

            if (_selection.kind == PungentUtilityHelpSelectionKind.Topic)
            {
                PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(_selection.topicStableId);
                PungentUtilityHelpUtilityNode utility = _navigation.FindUtilityForTopic(topic);
                if (utility != null)
                {
                    _selection.categoryId = utility.categoryId;
                    _selection.subcategoryId = utility.subcategoryId;
                    _selection.utilityId = utility.id;
                }
            }
            else if (_selection.kind == PungentUtilityHelpSelectionKind.Utility)
            {
                PungentUtilityHelpUtilityNode utility = _navigation.FindUtility(_selection.utilityId);
                if (utility != null)
                {
                    _selection.categoryId = utility.categoryId;
                    _selection.subcategoryId = utility.subcategoryId;
                }
            }
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            _annotationCacheDirty = true;
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefSelection, _selection == null ? string.Empty : _selection.Encode());
            UtilityWindowPrefs.SetString(PrefTab, _tab.ToString());
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetBool(PrefShowGenerated, _showGenerated);
            UtilityWindowPrefs.SetBool(PrefShowHidden, _showHidden);
            UtilityWindowPrefs.SetBool(PrefShowDeveloperOnly, _showDeveloperOnly);
            UtilityWindowPrefs.SetBool(PrefShowNotes, _showNotes);
            UtilityWindowPrefs.SetBool(PrefShowBookmarks, _showBookmarks);
            UtilityWindowPrefs.SetBool(PrefShowGenerationDashboard, _showGenerationDashboard);
            UtilityWindowPrefs.SetBool(PrefShowDeveloperTools, _showDeveloperTools);
            UtilityWindowPrefs.SetString(PrefCoverageFilter, _coverageFilter.ToString());
            UtilityWindowPrefs.SetString(PrefCoverageSort, _coverageSort.ToString());
            UtilityWindowPrefs.SetString(PrefReviewQueue, _reviewQueue.ToString());
            UtilityWindowPrefs.SetString(PrefCoverageExportMode, _coverageExportMode.ToString());
            UtilityWindowPrefs.SetString(PrefSidebarMode, _sidebarMode.ToString());
            UtilityWindowPrefs.SetString(PrefSectionRailMode, _sectionRailMode.ToString());
            UtilityWindowPrefs.SetBool(PrefDashboardSummaryOpen, _dashboardSummaryOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardActionsOpen, _dashboardActionsOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardReleaseOpen, _dashboardReleaseOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardOnboardingOpen, _dashboardOnboardingOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardCoverageOpen, _dashboardCoverageOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardReviewOpen, _dashboardReviewOpen);
            UtilityWindowPrefs.SetBool(PrefDashboardIssuesOpen, _dashboardIssuesOpen);
            UtilityWindowPrefs.SetFloat(PrefDashboardCoverageHeight, _dashboardCoverageHeight);
            UtilityWindowPrefs.SetFloat(PrefDashboardReviewHeight, _dashboardReviewHeight);
            UtilityWindowPrefs.SetString(PrefExpanded, string.Join("|", _expandedNodes.ToArray()));
            UtilityWindowPrefs.SetString(PrefSectionFoldouts, EncodeSectionFoldouts());
        }

        private PungentUtilityHelpNavigationSelection LoadSelection()
        {
            string encoded = UtilityWindowPrefs.GetString(PrefSelection, string.Empty);
            if (!string.IsNullOrWhiteSpace(encoded))
                return PungentUtilityHelpNavigationSelection.Decode(encoded);

            string legacyTopic = UtilityWindowPrefs.GetString(PrefLegacyTopic, string.Empty);
            if (!string.IsNullOrWhiteSpace(legacyTopic))
                return new PungentUtilityHelpNavigationSelection { kind = PungentUtilityHelpSelectionKind.Topic, topicStableId = legacyTopic };

            string legacyUtility = UtilityWindowPrefs.GetString(PrefLegacyUtility, string.Empty);
            if (!string.IsNullOrWhiteSpace(legacyUtility))
                return PungentUtilityHelpNavigationSelection.Utility(string.Empty, string.Empty, legacyUtility);

            return PungentUtilityHelpNavigationSelection.SearchResults();
        }

        private ContentTab LoadTab()
        {
            string stored = UtilityWindowPrefs.GetString(PrefTab, ContentTab.QuickUseGuide.ToString());
            return Enum.TryParse(stored, true, out ContentTab tab) ? tab : ContentTab.QuickUseGuide;
        }

        private static T LoadEnum<T>(string key, T fallback) where T : struct
        {
            string stored = UtilityWindowPrefs.GetString(key, fallback.ToString());
            return Enum.TryParse(stored, true, out T value) ? value : fallback;
        }

        private void LoadExpandedNodes()
        {
            _expandedNodes.Clear();
            string encoded = UtilityWindowPrefs.GetString(PrefExpanded, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
                return;

            foreach (string part in encoded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                _expandedNodes.Add(part);
        }

        private void LoadSectionFoldouts()
        {
            _sectionFoldouts.Clear();
            string encoded = UtilityWindowPrefs.GetString(PrefSectionFoldouts, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
                return;

            foreach (string part in encoded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int split = part.LastIndexOf('=');
                if (split <= 0)
                    continue;
                string key = DecodeFoldoutPart(part.Substring(0, split));
                bool open = string.Equals(part.Substring(split + 1), "1", StringComparison.Ordinal);
                _sectionFoldouts[key] = open;
            }
        }

        private string EncodeSectionFoldouts()
        {
            return string.Join("|", _sectionFoldouts.Select(pair => EncodeFoldoutPart(pair.Key) + "=" + (pair.Value ? "1" : "0")).ToArray());
        }

        private static string EncodeFoldoutPart(string value)
        {
            return (value ?? string.Empty).Replace("%", "%25").Replace("|", "%7C").Replace("=", "%3D");
        }

        private static string DecodeFoldoutPart(string value)
        {
            return (value ?? string.Empty).Replace("%3D", "=").Replace("%7C", "|").Replace("%25", "%");
        }

        private bool IsExpanded(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _expandedNodes.Contains(key);
        }

        private void SetExpanded(string key, bool expanded)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            if (expanded)
                _expandedNodes.Add(key);
            else
                _expandedNodes.Remove(key);
            SavePrefs();
        }

        private void EnsureExpandedForSelection()
        {
            if (_selection == null)
                return;
            if (!string.IsNullOrWhiteSpace(_selection.categoryId))
                _expandedNodes.Add("category:" + _selection.categoryId);
            if (!string.IsNullOrWhiteSpace(_selection.categoryId) && !string.IsNullOrWhiteSpace(_selection.subcategoryId))
                _expandedNodes.Add("subcategory:" + _selection.categoryId + "/" + _selection.subcategoryId);
            if (!string.IsNullOrWhiteSpace(_selection.utilityId))
                _expandedNodes.Add("utility:" + _selection.utilityId);
        }

        private bool IsSelectionInside(PungentUtilityHelpCategoryNode category)
        {
            return category != null && _selection != null && string.Equals(category.id, _selection.categoryId, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectionInside(PungentUtilityHelpSubcategoryNode subcategory)
        {
            return subcategory != null &&
                   _selection != null &&
                   string.Equals(subcategory.categoryId, _selection.categoryId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(subcategory.id, _selection.subcategoryId, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectionInside(PungentUtilityHelpUtilityNode utility)
        {
            return utility != null && _selection != null && string.Equals(utility.id, _selection.utilityId, StringComparison.OrdinalIgnoreCase);
        }

        private int CountUtilities()
        {
            return _navigation.categories.Sum(c => c.subcategories.Sum(s => s.utilities.Count));
        }

        private static int CountTopics(PungentUtilityHelpCategoryNode category)
        {
            return category == null ? 0 : category.subcategories.Sum(CountTopics);
        }

        private static int CountTopics(PungentUtilityHelpSubcategoryNode subcategory)
        {
            return subcategory == null ? 0 : subcategory.utilities.Sum(u => u.topics.Count);
        }

        private bool GlobalDeveloperModeEnabled => PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;
        private bool DeveloperToolsVisible => false;
        private bool ShowGeneratedEntries => false;
        private bool ShowDeveloperEntries => false;
        private bool ShowHiddenEntries => false;

        private static string NormalizeRelatedTopicId(string currentUtilityId, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            string trimmed = id.Trim();
            string[] parts = trimmed.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                return PungentUtilityHelpIds.TopicKey(parts[0], parts[1], parts[2]);
            if (parts.Length == 2)
                return PungentUtilityHelpIds.TopicKey(parts[0], parts[1], parts[1]);
            return PungentUtilityHelpIds.TopicKey(currentUtilityId, trimmed, trimmed);
        }

        private struct GuiBackgroundScope : IDisposable
        {
            private readonly Color _previous;

            public GuiBackgroundScope(Color color)
            {
                _previous = GUI.backgroundColor;
                GUI.backgroundColor = color;
            }

            public void Dispose()
            {
                GUI.backgroundColor = _previous;
            }
        }
    }
#endif
}
