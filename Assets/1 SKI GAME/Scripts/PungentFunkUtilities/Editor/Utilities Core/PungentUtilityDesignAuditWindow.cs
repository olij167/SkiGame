using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.Editor.SceneTools;
using PungentFunk.Utilities.Editor.Audio;
using PungentFunk.Utilities.Editor.Core.Help;
namespace PungentFunk.Utilities.Editor.Core {
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    /// <summary>
    /// Project-facing audit dashboard with an optional Developer Mode package-compliance layer.
    /// </summary>
    public sealed class PungentUtilityDesignAuditWindow : EditorWindow {
        private const string PrefPrefix = "PungentFunkUtilities.DesignAudit.";
        private const string PrefIncludeInfo = PrefPrefix + "IncludeInfo";
        private const string PrefSeverity = PrefPrefix + "Severity";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefGroupByArea = PrefPrefix + "GroupByArea";
        private const string PrefSelectedTab = PrefPrefix + "SelectedTab";
        private const string PrefIssueListWidth = PrefPrefix + "IssueListWidth";
        private const string PrefFindingsStackedListHeight = PrefPrefix + "FindingsStackedListHeight";
        private const string PrefSelectedIssueKey = PrefPrefix + "SelectedIssueKey";
        private const string PrefListScrollY = PrefPrefix + "ListScrollY";
        private const string PrefDetailScrollY = PrefPrefix + "DetailScrollY";
        private const string PrefProjectScrollY = PrefPrefix + "ProjectScrollY";
        private const string PrefPackageScrollY = PrefPrefix + "PackageScrollY";
        private const string PrefProviderEnabledPrefix = PrefPrefix + "ProviderEnabled.";
        private const string PrefCollapsedFindingGroupPrefix = PrefPrefix + "CollapsedFindingGroup.";
        private const string PrefShowDeveloperTools = PrefPrefix + "ShowDeveloperTools";
        private const float IssueRowHeight = 42f;
        private const float FindingsSplitThreshold = 920f;
        private const float FindingsListMinWidth = 280f;
        private const float FindingsListMaxWidth = 560f;
        private const float FindingsDetailMinWidth = 320f;
        private const float FindingsSplitHandleWidth = 8f;
        private const float FindingsSplitGap = 0f;
        private const float FindingsWorkspaceMinHeight = 180f;
        private const float FindingsWorkspaceFallbackWidth = 220f;
        private const float FindingsWorkspaceFallbackHeight = 120f;
        private const float FindingsStackedListMinHeight = 150f;
        private const float FindingsStackedDetailMinHeight = 130f;
        private const float FindingsStackedHandleHeight = 8f;
        private const int RestartAuditMaxDelayAttempts = 12;
        private enum AuditViewTab {
            ProjectAudit = 0,
            Findings = 1,
            DeveloperPackage = 2
        }
        private enum AuditSource {
            Project = 0,
            Package = 1,
            All = 2
        }
        private enum ProviderRunState {
            Pending,
            Queued,
            Running,
            Paused,
            Cancelling,
            Complete,
            NotConfigured,
            RequiresExplicitScannerRun,
            Failed,
            Cancelled,
            Skipped
        }
        private Vector2 _issueListScroll;
        private Vector2 _detailScroll;
        private Vector2 _projectAuditScroll;
        private Vector2 _packageScroll;
        private bool _includeInfo;
        private bool _groupByArea;
        private bool _developerModeAvailable;
        private bool _developerModeEnabled;
        private bool _showDeveloperTools;
        private int _severityIndex;
        private string _search = string.Empty;
        private string _selectedIssueKey = string.Empty;
        private string _status = "Project audit ready. Enable tools, then run or refresh audit results.";
        private float _issueListWidth = 340f;
        private float _findingsStackedListHeight = 320f;
        private float _overviewCardWidth = 220f;
        private AuditViewTab _selectedTab = AuditViewTab.ProjectAudit;
        private int _viewVersion;
        private int _cachedViewVersion = -1;
        private bool _filterCacheDirty = true;
        private bool _rawIssueDetailsExpanded;
        private bool _pendingRestartAudit;
        private int _pendingRestartAuditAttempts;
        private PungentAuditScanMode _pendingRestartAuditMode = PungentAuditScanMode.Immediate;
        private float _lastTabStripBottom;
        private float _pendingTabStripBottom;
        private float _currentTabStripBottom;
        private DateTime _lastProjectAuditUtc;
        private PungentUtilityDesignAudit.Report _packageReport;
        private PungentUtilityReleaseReadiness.Assessment _assessment;
        private readonly List<ProjectAuditProvider> _providers = new List<ProjectAuditProvider>();
        private readonly List<PungentUtilityDesignAudit.Issue> _projectIssues = new List<PungentUtilityDesignAudit.Issue>();
        private readonly List<PungentUtilityDesignAudit.Issue> _filteredIssues = new List<PungentUtilityDesignAudit.Issue>();
        private readonly List<IssueGroup> _filteredIssueGroups = new List<IssueGroup>();
        private readonly PungentAuditScanRunner _scanRunner = new PungentAuditScanRunner();
        private readonly HashSet<string> _runnerCacheAppliedProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeSharedSceneProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PungentNote> _noteByIssueKey = new Dictionary<string, PungentNote>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _providerIdByIssueKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _issueCodeByIssueKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly string[] SeverityLabels = { "All", "Errors", "Warnings", "Info" };
        private PungentAuditScanRunner VisibleScanRunner => _scanRunner.IsActive || !PungentAuditIdleScanScheduler.Runner.IsActive ? _scanRunner : PungentAuditIdleScanScheduler.Runner;
        private bool AnyScanActive => _scanRunner.IsActive || PungentAuditIdleScanScheduler.Runner.IsActive;
        private bool DeveloperToolsVisible => false;
        public static void Open() {
            PungentUtilityDesignAuditWindow window = GetWindow<PungentUtilityDesignAuditWindow>("Design Validation");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }
        public static void OpenAndFocusCachedIssue(string providerId, string issueCode, string issueTitle, string issuePath, string issueMessage) {
            PungentUtilityDesignAuditWindow window = GetWindow<PungentUtilityDesignAuditWindow>("Design Validation");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
            window.Focus();
            window.FocusCachedIssue(providerId, issueCode, issueTitle, issuePath, issueMessage);
        }
        private void OnEnable() {
            minSize = new Vector2(720f, 520f);
            titleContent = new GUIContent("Design Validation");
            _includeInfo = UtilityWindowPrefs.GetBool(PrefIncludeInfo, false);
            _groupByArea = UtilityWindowPrefs.GetBool(PrefGroupByArea, true);
            _severityIndex = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSeverity, 0), 0, SeverityLabels.Length - 1);
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _selectedIssueKey = UtilityWindowPrefs.GetString(PrefSelectedIssueKey, string.Empty);
            _selectedTab = (AuditViewTab)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSelectedTab, (int)AuditViewTab.ProjectAudit), 0, 2);
            _issueListWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefIssueListWidth, 340f), 240f, 520f);
            _findingsStackedListHeight = Mathf.Max(FindingsStackedListMinHeight, UtilityWindowPrefs.GetFloat(PrefFindingsStackedListHeight, 320f));
            _issueListScroll.y = Mathf.Max(0f, UtilityWindowPrefs.GetFloat(PrefListScrollY, 0f));
            _detailScroll.y = Mathf.Max(0f, UtilityWindowPrefs.GetFloat(PrefDetailScrollY, 0f));
            _projectAuditScroll.y = Mathf.Max(0f, UtilityWindowPrefs.GetFloat(PrefProjectScrollY, 0f));
            _packageScroll.y = Mathf.Max(0f, UtilityWindowPrefs.GetFloat(PrefPackageScrollY, 0f));
            _showDeveloperTools = UtilityWindowPrefs.GetBool(PrefShowDeveloperTools, false);
            _developerModeAvailable = PungentUtilityDesignAudit.IsDeveloperModeAvailable();
            _developerModeEnabled = _developerModeAvailable && PungentUtilityDesignAudit.TryGetDeveloperModeEnabled(out bool enabled) && enabled;
            if (!DeveloperToolsVisible && _selectedTab == AuditViewTab.DeveloperPackage)
                _selectedTab = AuditViewTab.ProjectAudit;
            InitializeProjectProviders();
            RefreshProviderAvailability();
            RefreshCachedResults(false);
            _scanRunner.Changed += OnScanRunnerChanged;
            _scanRunner.Completed += OnScanRunnerCompleted;
            PungentAuditIdleScanScheduler.EnsureInitialized();
            PungentAuditIdleScanScheduler.Runner.Changed += OnScanRunnerChanged;
            PungentAuditIdleScanScheduler.Runner.Completed += OnScanRunnerCompleted;
            _status = "Project audit cache loaded. Run Enabled Audits only runs providers with safe coordinator hooks.";
        }
        private void OnDisable() {
            SavePrefs();
            EditorApplication.delayCall -= TryStartPendingRestartAudit;
            _scanRunner.Changed -= OnScanRunnerChanged;
            _scanRunner.Completed -= OnScanRunnerCompleted;
            PungentAuditIdleScanScheduler.Runner.Changed -= OnScanRunnerChanged;
            PungentAuditIdleScanScheduler.Runner.Completed -= OnScanRunnerCompleted;
            _scanRunner.Cancel("Project audit interrupted by Design Validation Audit window close.");
            _scanRunner.Dispose();
        }
        private void OnGUI() {
            ApplyPendingTabStripBottom();
            RefreshDeveloperModeState();
            DrawHeader();
            DrawToolbar();
            DrawTabStrip();

            switch (_selectedTab) {
                case AuditViewTab.Findings:
                    DrawFindingsTab();
                    break;
                case AuditViewTab.DeveloperPackage:
                    if (DeveloperToolsVisible)
                        DrawDeveloperPackageTab();
                    else {
                        SelectTab(AuditViewTab.ProjectAudit);
                        DrawProjectAuditTab();
                    }
                    break;
                default:
                    DrawProjectAuditTab();
                    break;
            }
        }
        private void DrawHeader() {
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "design-validation-audit",
                Title = "Design Validation Audit",
                Description = "Audit project readiness, companion tool findings, and package-facing scanner results.",
                Status = _status,
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });
        }
        private void DrawTabStrip() {
            Rect tabStripRect = Rect.zero;
            using (EditorGUILayout.HorizontalScope tabStrip = new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 5, 3))) {
                DrawTabButton(AuditViewTab.ProjectAudit, "Project Audit", "Project audit dashboard and provider cards.");
                DrawTabButton(AuditViewTab.Findings, "Findings", "List and inspect project and developer findings.");
                using (new EditorGUI.DisabledScope(!DeveloperToolsVisible)) {
                    if (DeveloperToolsVisible)
                        DrawTabButton(AuditViewTab.DeveloperPackage, "Developer Package", "Package-development audit and release readiness.");
                }
                GUILayout.FlexibleSpace();
                PungentUtilityHelpButton.Draw("design-validation-audit", "overview", "overview", "Open help for Design Validation Audit.", "Design Audit header");
                UtilityWindowTheme.CountPill("Project Mode", UtilityWindowTheme.Teal, 116f);
                tabStripRect = tabStrip.rect;
            }
            RecordTabStripBottom(tabStripRect);
        }
        private void RecordTabStripBottom(Rect tabStripRect) {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.Repaint)
                return;
            if (!IsFiniteRect(tabStripRect) || tabStripRect.yMax <= 1f)
                return;
            float nextBottom = tabStripRect.yMax;
            _currentTabStripBottom = nextBottom;
            if (!Mathf.Approximately(_pendingTabStripBottom, nextBottom)) {
                _pendingTabStripBottom = nextBottom;
                Repaint();
            }
        }
        private void ApplyPendingTabStripBottom() {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.Layout || _pendingTabStripBottom <= 1f)
                return;
            _lastTabStripBottom = _pendingTabStripBottom;
        }
        private void DrawTabButton(AuditViewTab tab, string label, string tooltip) {
            bool selected = _selectedTab == tab;
            Color tint = selected ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
            if (UtilityWindowTheme.TintedButton(label, tint, GUILayout.Width(tab == AuditViewTab.DeveloperPackage ? 148f : 112f), GUILayout.Height(24f)))
                SelectTab(tab);
        }
        private void SelectTab(AuditViewTab tab) {
            if (tab == AuditViewTab.DeveloperPackage && !DeveloperToolsVisible)
                tab = AuditViewTab.ProjectAudit;
            if (_selectedTab == tab)
                return;
            _selectedTab = tab;
            UtilityWindowPrefs.SetInt(PrefSelectedTab, (int)_selectedTab);
            MarkFilterDirty();
        }
        private void DrawToolbar() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.08f, 6, 4))) {
                DrawToolbarActionRows();
                using (new EditorGUILayout.HorizontalScope()) {
                    PungentUtilityHelpButton.Draw("design-validation-audit", "scan-pipeline", "scan-pipeline", "Open help for audit scan pipeline controls.", "Audit scan pipeline");
                    GUILayout.Space(4f);
                    bool nextGroupByArea = EditorGUILayout.ToggleLeft(new GUIContent("Group By Source/Area", "Group findings by source or audit area."), _groupByArea, GUILayout.Width(136f));
                    SetBoolFromToggle(ref _groupByArea, nextGroupByArea, PrefGroupByArea, true);
                    EditorGUILayout.LabelField(new GUIContent("Severity", "Filter the issue browser by severity."), GUILayout.Width(54f));
                    int nextSeverityIndex = EditorGUILayout.Popup(_severityIndex, SeverityLabels, GUILayout.Width(104f));
                    if (nextSeverityIndex != _severityIndex) {
                        _severityIndex = nextSeverityIndex;
                        UtilityWindowPrefs.SetInt(PrefSeverity, _severityIndex);
                        MarkFilterDirty();
                    }
                    EditorGUILayout.LabelField(new GUIContent("Search", "Search area, target, message, recommendation, and asset path."), GUILayout.Width(44f));
                    string nextSearch = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(120f));
                    if (!string.Equals(nextSearch, _search, StringComparison.Ordinal)) {
                        _search = nextSearch;
                        UtilityWindowPrefs.SetString(PrefSearch, _search);
                        MarkFilterDirty();
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search))) {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear the current issue search."), EditorStyles.miniButton, GUILayout.Width(52f))) {
                            _search = string.Empty;
                            UtilityWindowPrefs.SetString(PrefSearch, _search);
                            MarkFilterDirty();
                        }
                    }
                    if (DeveloperToolsVisible) {
                        bool nextIncludeInfo = EditorGUILayout.ToggleLeft(new GUIContent("Include Package Info", "Include informational package-development audit rows."), _includeInfo, GUILayout.Width(138f));
                        SetBoolFromToggle(ref _includeInfo, nextIncludeInfo, PrefIncludeInfo, true);
                    }
                }
                DrawOverallScanProgressStrip();
            }
        }
        private void DrawToolbarActionRows() {
            List<ActionSpec> actions = new List<ActionSpec> {
                new ActionSpec("Run Enabled Audits", "Run enabled scanner-backed providers through the cooperative audit runner.", 142f, UtilityWindowTheme.Green, RunProjectAudit, !AnyScanActive && !_pendingRestartAudit),
                new ActionSpec("Start Background Scan", "Run included providers in opt-in Background Idle mode with a smaller update budget.", 154f, UtilityWindowTheme.Teal, RunBackgroundProjectAudit, !AnyScanActive && !_pendingRestartAudit && PungentAuditScanSettings.BackgroundScanEnabled),
                new ActionSpec("Refresh Results", "Read cached scanner outputs without running heavy scans.", 118f, UtilityWindowTheme.Neutral, () => RefreshCachedResults(), !AnyScanActive && !_pendingRestartAudit),
                new ActionSpec("Create Audit Note", "Create a project or package audit note from the current summary.", 126f, UtilityWindowTheme.Purple, CreateAuditSummaryNote, true),
                new ActionSpec("Copy Summary", "Copy the current audit summary.", 112f, UtilityWindowTheme.Cyan, CopySummary, true)
            };
            if (DeveloperToolsVisible) {
                actions.Insert(2, new ActionSpec("Run Package Audit", "Run the package-development audit.", 130f, UtilityWindowTheme.Green, RunPackageAudit, true));
                actions.Add(new ActionSpec("Copy Next Pass", "Copy the recommended package next-pass brief.", 122f, UtilityWindowTheme.Amber, CopyNextPassBrief, true));
            }
            DrawWrappedActions(actions, Mathf.Max(260f, position.width - 28f));
        }
        private void DrawOverallScanProgressStrip() {
            PungentAuditScanRunner runner = VisibleScanRunner;
            if (!runner.IsActive && runner.TotalJobCount == 0)
                return;
            if (!runner.IsActive && runner.BatchState != PungentAuditBatchState.Cancelled && runner.BatchState != PungentAuditBatchState.Failed && runner.BatchState != PungentAuditBatchState.CompleteWithIssues)
                return;

            PungentAuditScanJob current = runner.CurrentJob;
            string label = GetBatchStateLabel();
            string detail = current != null
                ? current.displayName + " - " + current.statusMessage
                : runner.CompletedCount + " of " + runner.TotalJobCount + " providers complete";

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetBatchStateTint(), 0.12f, 0.06f, 5, 3))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.MinWidth(150f));
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(runner.CurrentMode == PungentAuditScanMode.BackgroundIdle ? "Background Idle" : "Immediate", UtilityWindowTheme.Blue, 112f);
                    UtilityWindowTheme.CountPill(runner.CompletedCount + "/" + runner.TotalJobCount + " complete", UtilityWindowTheme.Teal, 104f);
                    if (runner.FailedCount > 0)
                        UtilityWindowTheme.CountPill(runner.FailedCount + " failed", UtilityWindowTheme.Red, 72f);
                    if (runner.CancelledCount > 0)
                        UtilityWindowTheme.CountPill(runner.CancelledCount + " interrupted", UtilityWindowTheme.Neutral, 96f);
                    if (runner.NotConfiguredCount > 0)
                        UtilityWindowTheme.CountPill(runner.NotConfiguredCount + " not configured", UtilityWindowTheme.Amber, 118f);
                    if (runner.SkippedCount > 0)
                        UtilityWindowTheme.CountPill(runner.SkippedCount + " skipped", UtilityWindowTheme.Neutral, 82f);
                }
                Rect rect = GUILayoutUtility.GetRect(1f, 16f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, runner.OverallProgress01, Mathf.RoundToInt(runner.OverallProgress01 * 100f) + "%");
                EditorGUILayout.LabelField(detail, UtilityWindowTheme.MutedMiniLabelStyle);
                if (runner.IsActive) {
                    List<ActionSpec> actions = new List<ActionSpec>();
                    if (runner.IsPaused) {
                        actions.Add(new ActionSpec("Resume All", "Resume the queued project audit.", 86f, UtilityWindowTheme.Green, runner.ResumeAll, true));
                        actions.Add(new ActionSpec("Restart Audit", "Discard paused progress and start enabled providers from the beginning.", 112f, UtilityWindowTheme.Amber, RestartCurrentAudit, true));
                    }
                    else
                        actions.Add(new ActionSpec("Pause All", "Pause before the next safe scan checkpoint.", 78f, UtilityWindowTheme.Amber, runner.PauseAll, true));
                    DrawWrappedActions(actions, Mathf.Max(220f, position.width - 42f));
                }
            }
        }
        private void DrawProviderCard(ProjectAuditProvider provider) {
            Color tint = provider.available ? GetProviderStatusTint(provider.state) : UtilityWindowTheme.Neutral;
            PungentAuditScanJob job = VisibleScanRunner.FindJob(provider.id);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.05f, 5, 3))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(!provider.available)) {
                        bool next = EditorGUILayout.ToggleLeft(new GUIContent(provider.displayName, provider.description), provider.enabled, EditorStyles.boldLabel, GUILayout.MinWidth(220f));
                        if (next != provider.enabled) {
                            provider.enabled = next;
                            UtilityWindowPrefs.SetBool(ProviderEnabledPref(provider.id), provider.enabled);
                            MarkFilterDirty();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(GetProviderPrimaryStatusLabel(provider), tint, 104f);
                    UtilityWindowTheme.CountPill(GetProviderFreshnessLabel(provider), GetProviderFreshnessTint(provider), 78f);
                }
                DrawProviderSummaryStrip(provider);
                DrawProviderFindingDigest(provider);
                DrawProviderProgress(provider, job);
                DrawProviderMetadataFoldout(provider);
                DrawProviderActions(provider, job);
            }
        }

        private void DrawProviderSummaryStrip(ProjectAuditProvider provider) {
            string age = provider.cachedResult != null && provider.cachedResult.IsComplete
                ? PungentScanFindingActions.FormatAge(provider.cachedResult.CompletedAtUtc)
                : PungentScanSnapshotStore.GetLastScanAgeLabel(provider.id);
            DrawCountPillGrid(new[] {
                new PillSpec(provider.errorCount + " errors", provider.errorCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral, 82f),
                new PillSpec(provider.warningCount + " warnings", provider.warningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 102f),
                new PillSpec(provider.infoCount + " info", provider.infoCount > 0 ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 74f),
                new PillSpec("Last " + age, UtilityWindowTheme.Blue, 124f),
                new PillSpec(Shorten(GetProviderStateLabel(provider.state), 20), GetProviderStatusTint(provider.state), 118f)
            }, Mathf.Max(240f, position.width - 58f));
        }

        private void DrawProviderFindingDigest(ProjectAuditProvider provider) {
            PungentScanGUI.DrawFindingDigest(GetProviderDigest(provider));
            DrawSceneIssueProviderContextLine(provider);
        }

        private void DrawProviderMetadataFoldout(ProjectAuditProvider provider) {
            if (provider == null)
                return;
            provider.metadataExpanded = EditorGUILayout.Foldout(provider.metadataExpanded, "More scan metadata", true);
            if (!provider.metadataExpanded)
                return;

            PungentProjectAuditIndexEntry index = PungentProjectAuditIndex.Get(provider.id);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.03f, 4, 2))) {
                DrawDetailText("Provider ID", provider.id, UtilityWindowTheme.PathLabelStyle);
                DrawDetailText("Scan Scope", provider.cachedResult != null ? provider.cachedResult.ScopeLabel : "No cached result");
                DrawDetailText("Scan Mode", index != null ? index.LastScanMode : "Unknown");
                DrawDetailText("Run Capability", provider.canRunFromDashboard ? "Coordinator run available" : "Manual scanner run required");
                DrawDetailText("Execution Shape", provider.adapter != null && provider.adapter.IsCooperative ? "Cooperative provider" : "Monolithic provider");
                DrawDetailText("Background Capability", provider.adapter != null && provider.adapter.CanRunBackground ? "Background-safe" : "Immediate only");
                DrawDetailText("Implementation Flags", BuildProviderCapabilityLine(provider));
                if (IsSceneIssueProvider(provider))
                    DrawDetailText("Shared Scene Pass", "Full audit uses the shared scene scan pass; manual window scans open scenes only; background scans do not open scenes.");
                DrawDetailText("Latest Status", string.IsNullOrWhiteSpace(provider.lastStatus) ? "Unknown" : provider.lastStatus);
            }
        }
        private void DrawProjectAuditTab() {
            _projectAuditScroll = EditorGUILayout.BeginScrollView(_projectAuditScroll, GUILayout.ExpandHeight(true));
            DrawProjectOverview();
            DrawProjectToolsGroup();
            DrawProjectFindingsSummary();
            EditorGUILayout.EndScrollView();
        }
        private void DrawProjectOverview() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(ProjectErrorCount() > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 0.14f, 0.07f, 6, 4))) {
                UtilityWindowTheme.SectionTitle("Project Overview", UtilityWindowTheme.Teal, LastProjectAuditLabel());
                DrawOverviewCardGrid();
            }
        }
        private void DrawOverviewCardGrid() {
            float width = Mathf.Max(320f, position.width - 36f);
            int columns = width >= 1040f ? 4 : width >= 620f ? 2 : 1;
            _overviewCardWidth = Mathf.Max(220f, (width - ((columns - 1) * 6f)) / columns);
            List<Action> cards = new List<Action> {
                DrawProjectHealthOverviewCard,
                DrawScannerCoverageOverviewCard,
                DrawTopFindingDashboardCard,
                DrawAuditStatusOverviewCard
            };
            for (int i = 0; i < cards.Count; i += columns) {
                using (new EditorGUILayout.HorizontalScope()) {
                    for (int c = 0; c < columns; c++) {
                        int index = i + c;
                        if (index < cards.Count)
                            cards[index]();
                        else
                            GUILayout.FlexibleSpace();
                    }
                }
            }
        }
        private void DrawProjectHealthOverviewCard() {
            DrawOverviewCard(
                "Project Health",
                ProjectErrorCount() > 0 ? ProjectErrorCount() + " errors" : ProjectWarningCount() > 0 ? ProjectWarningCount() + " warnings" : "No blocking findings",
                "Project findings from included scanner caches.",
                ProjectErrorCount() > 0 ? UtilityWindowTheme.Red : ProjectWarningCount() > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green,
                new[] {
                    new PillSpec(ProjectErrorCount() + " errors", UtilityWindowTheme.Red, 74f),
                    new PillSpec(ProjectWarningCount() + " warnings", UtilityWindowTheme.Amber, 92f),
                    new PillSpec(ProjectInfoCount() + " info", UtilityWindowTheme.Cyan, 68f),
                    new PillSpec("Last " + LastProjectAuditShortLabel(), UtilityWindowTheme.Neutral, 104f)
                });
        }
        private void DrawScannerCoverageOverviewCard() {
            DrawOverviewCard(
                "Scanner Coverage",
                EnabledAvailableProviderCount() + "/" + AvailableProviderCount() + " included",
                "Cache and coordinator coverage for project providers.",
                UtilityWindowTheme.Teal,
                new[] {
                    new PillSpec(CachedProviderCount() + " cached", UtilityWindowTheme.Green, 78f),
                    new PillSpec(ProvidersNeedingFirstScanCount() + " not scanned", UtilityWindowTheme.Neutral, 102f),
                    new PillSpec(ManualProviderCount() + " manual", UtilityWindowTheme.Amber, 78f),
                    new PillSpec(UnavailableProviderCount() + " unavailable", UtilityWindowTheme.Red, 106f)
                });
        }
        private void DrawAuditStatusOverviewCard() {
            PungentAuditScanRunner runner = VisibleScanRunner;
            string headline = runner.IsActive ? Mathf.RoundToInt(runner.Progress01 * 100f) + "% running" : _status;
            DrawOverviewCard(
                "Audit Status",
                headline,
                runner.IsActive ? runner.StatusMessage : "Run uses the cooperative queue; refresh reads cache only.",
                UtilityWindowTheme.Cyan,
                new[] {
                    new PillSpec(RunnableProviderCount() + " runnable", UtilityWindowTheme.Green, 86f),
                    new PillSpec(CachedProviderCount() + " refreshed", UtilityWindowTheme.Teal, 94f),
                    new PillSpec(ManualProviderCount() + " manual", UtilityWindowTheme.Amber, 78f)
                });
        }
        private void DrawOverviewCard(string title, string headline, string detail, Color tint, IReadOnlyList<PillSpec> pills) {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.11f, 0.05f, 5, 3), GUILayout.Width(_overviewCardWidth), GUILayout.MinHeight(132f), GUILayout.ExpandWidth(true))) {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                DrawWrappedLabel(Shorten(headline, 64), UtilityWindowTheme.CardLabelStyle);
                DrawWrappedLabel(detail, UtilityWindowTheme.MutedMiniLabelStyle);
                DrawCountPillGrid(pills, _overviewCardWidth - 18f);
            }
        }
        private void DrawProjectToolsGroup() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.13f, 0.06f))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Project Audit Tools", UtilityWindowTheme.SectionHeaderStyle);
                    UtilityWindowTheme.CountPill(EnabledAvailableProviderCount() + " enabled / " + AvailableProviderCount() + " available", UtilityWindowTheme.Teal, 152f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Enable All", "Include all available project audit tools."), EditorStyles.miniButton, GUILayout.Width(76f)))
                        SetAllProvidersEnabled(true);
                    if (GUILayout.Button(new GUIContent("Disable All", "Temporarily exclude all project audit tools."), EditorStyles.miniButton, GUILayout.Width(76f)))
                        SetAllProvidersEnabled(false);
                    if (GUILayout.Button(new GUIContent("Refresh Results", "Read cached scan results from included tools."), EditorStyles.miniButton, GUILayout.Width(108f)))
                        RefreshCachedResults();
                }
                EditorGUILayout.LabelField("Included tools contribute provider summaries, cached findings, and issue-detail actions to this workbench.", UtilityWindowTheme.MutedMiniLabelStyle);
                DrawBackgroundScanSettings();
                for (int i = 0; i < _providers.Count; i++)
                    DrawProviderCard(_providers[i]);
            }
        }
        private void DrawBackgroundScanSettings() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Background Project Audit", EditorStyles.boldLabel, GUILayout.Width(168f));
                    PungentAuditBackgroundScanMode nextMode = (PungentAuditBackgroundScanMode)EditorGUILayout.EnumPopup(PungentAuditScanSettings.BackgroundMode, GUILayout.Width(110f));
                    if (nextMode != PungentAuditScanSettings.BackgroundMode)
                        PungentAuditScanSettings.BackgroundMode = nextMode;

                    GUILayout.Space(6f);
                    EditorGUILayout.LabelField("Immediate ms", GUILayout.Width(82f));
                    int immediateBudget = EditorGUILayout.IntField(PungentAuditScanSettings.ImmediateTimeBudgetMs, GUILayout.Width(42f));
                    if (immediateBudget != PungentAuditScanSettings.ImmediateTimeBudgetMs)
                        PungentAuditScanSettings.ImmediateTimeBudgetMs = immediateBudget;

                    EditorGUILayout.LabelField("Background ms", GUILayout.Width(96f));
                    int backgroundBudget = EditorGUILayout.IntField(PungentAuditScanSettings.BackgroundTimeBudgetMs, GUILayout.Width(42f));
                    if (backgroundBudget != PungentAuditScanSettings.BackgroundTimeBudgetMs)
                        PungentAuditScanSettings.BackgroundTimeBudgetMs = backgroundBudget;

                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!PungentAuditScanSettings.BackgroundScanEnabled || AnyScanActive)) {
                        if (GUILayout.Button(new GUIContent("Start Background Scan", "Run the opt-in background/idle audit mode using background-safe providers only."), EditorStyles.miniButton, GUILayout.Width(146f)))
                            RunBackgroundProjectAudit();
                    }
                }
                using (new EditorGUILayout.HorizontalScope()) {
                    bool pausePlay = EditorGUILayout.ToggleLeft(new GUIContent("Pause Play Mode", "Background scans pause during Play Mode or Play Mode transitions."), PungentAuditScanSettings.PauseDuringPlayMode, GUILayout.Width(116f));
                    if (pausePlay != PungentAuditScanSettings.PauseDuringPlayMode)
                        PungentAuditScanSettings.PauseDuringPlayMode = pausePlay;
                    bool pauseCompile = EditorGUILayout.ToggleLeft(new GUIContent("Pause Compile", "Background scans pause while scripts compile."), PungentAuditScanSettings.PauseDuringCompilation, GUILayout.Width(112f));
                    if (pauseCompile != PungentAuditScanSettings.PauseDuringCompilation)
                        PungentAuditScanSettings.PauseDuringCompilation = pauseCompile;
                    bool pauseImport = EditorGUILayout.ToggleLeft(new GUIContent("Pause Import", "Background scans pause while Unity updates or imports assets."), PungentAuditScanSettings.PauseDuringAssetImportOrUpdate, GUILayout.Width(104f));
                    if (pauseImport != PungentAuditScanSettings.PauseDuringAssetImportOrUpdate)
                        PungentAuditScanSettings.PauseDuringAssetImportOrUpdate = pauseImport;
                    bool autoRefresh = EditorGUILayout.ToggleLeft(new GUIContent("Auto-refresh cache", "Refresh Design Audit summaries after runner completion."), PungentAuditScanSettings.AutoRefreshCacheAfterScanCompletion, GUILayout.Width(132f));
                    if (autoRefresh != PungentAuditScanSettings.AutoRefreshCacheAfterScanCompletion)
                        PungentAuditScanSettings.AutoRefreshCacheAfterScanCompletion = autoRefresh;
                    bool runIncluded = EditorGUILayout.ToggleLeft(new GUIContent("Included only", "Background scheduler scans only providers included in Design Audit."), PungentAuditScanSettings.RunOnlyIncludedProviders, GUILayout.Width(104f));
                    if (runIncluded != PungentAuditScanSettings.RunOnlyIncludedProviders)
                        PungentAuditScanSettings.RunOnlyIncludedProviders = runIncluded;
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Idle delay", GUILayout.Width(64f));
                    float idleDelay = EditorGUILayout.FloatField(PungentAuditScanSettings.IdleDelaySeconds, GUILayout.Width(52f));
                    if (!Mathf.Approximately(idleDelay, PungentAuditScanSettings.IdleDelaySeconds))
                        PungentAuditScanSettings.IdleDelaySeconds = idleDelay;
                    EditorGUILayout.LabelField("seconds before automatic stale-provider scan.", UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.LabelField(PungentAuditScanSettings.BackgroundScanEnabled
                    ? "Background idle scans are opt-in. The scheduler waits for stale providers, safe editor state, and the idle delay before starting background-safe jobs."
                    : "Background scans are off by default. Immediate manual scans remain available.",
                    UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawProjectFindingsSummary() {
            List<PungentUtilityDesignAudit.Issue> issues = _projectIssues.Where(IsEnabledProviderIssue).OrderByDescending(issue => issue.severity).ThenBy(issue => issue.area).ToList();
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.11f, 0.05f))) {
                UtilityWindowTheme.SectionTitle("Project Findings Summary", UtilityWindowTheme.Purple, issues.Count + " findings");
                DrawCountPillGrid(new[] {
                    new PillSpec(ProjectErrorCount() + " errors", UtilityWindowTheme.Red, 74f),
                    new PillSpec(ProjectWarningCount() + " warnings", UtilityWindowTheme.Amber, 92f),
                    new PillSpec(ProjectInfoCount() + " info", UtilityWindowTheme.Cyan, 68f)
                });
                EditorGUILayout.LabelField("Top sources", EditorStyles.boldLabel);
                for (int i = 0; i < _providers.Count; i++) {
                    ProjectAuditProvider provider = _providers[i];
                    if (!provider.enabled || !provider.available)
                        continue;
                    string value = provider.cachedResult == null
                        ? (provider.canRunFromDashboard ? "not scanned" : "manual scan required")
                        : provider.issueCount + " finding(s)";
                    EditorGUILayout.LabelField(provider.displayName + ": " + value, UtilityWindowTheme.MutedMiniLabelStyle);
                }
                using (new EditorGUI.DisabledScope(issues.Count == 0)) {
                    if (GUILayout.Button(new GUIContent("Review Project Findings", "Switch to Findings and select the highest-severity project finding."), GUILayout.Width(168f), GUILayout.Height(24f)))
                        SelectFirstProjectFinding();
                }
            }
        }
        private void DrawProviderProgress(ProjectAuditProvider provider, PungentAuditScanJob job) {
            if (job == null || (job.state == PungentAuditScanJobState.Complete && !VisibleScanRunner.IsActive))
                return;
            if (job.state == PungentAuditScanJobState.Idle)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetProviderStatusTint(provider.state), 0.08f, 0.04f, 4, 2))) {
                string state = GetScanJobStateLabel(job.state);
                string message = string.IsNullOrWhiteSpace(job.statusMessage) ? state : job.statusMessage;
                DrawWrappedLabel(state + " - " + message, UtilityWindowTheme.MutedMiniLabelStyle);
                Rect rect = GUILayoutUtility.GetRect(1f, 14f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, job.progress.isIndeterminate ? 0f : job.progress.progress01, job.progress.isIndeterminate ? "Working..." : Mathf.RoundToInt(job.progress.progress01 * 100f) + "%");
                if (job.progress.total > 0)
                    EditorGUILayout.LabelField(job.progress.processed + " / " + job.progress.total, UtilityWindowTheme.PathLabelStyle);
            }
        }
        private void DrawProviderActions(ProjectAuditProvider provider, PungentAuditScanJob job) {
            List<ActionSpec> actions = new List<ActionSpec>();
            bool providerBusy = job != null && !job.IsTerminal;
            PungentAuditScanRunner runner = GetRunnerForJob(job);
            bool canRun = provider.available && provider.canRunFromDashboard && !AnyScanActive && !_pendingRestartAudit && !providerBusy;
            actions.Add(new ActionSpec("Review", "Select this tool's most important finding.", 76f, UtilityWindowTheme.Purple, () => SelectFirstProviderFinding(provider.id), provider.issueCount > 0));
            actions.Add(new ActionSpec(provider.openButtonLabel, provider.description, 154f, UtilityWindowTheme.Teal, () => OpenProvider(provider.id), provider.available));
            actions.Add(new ActionSpec(provider.cachedResult == null ? provider.runButtonLabel : provider.runButtonLabel.Replace("Run ", "Run Again "), "Run this provider through the cooperative audit runner.", 124f, UtilityWindowTheme.Green, () => RunProviderFromDashboard(provider), canRun));
            actions.Add(new ActionSpec("Refresh", "Read this tool's latest cached scan result.", 76f, UtilityWindowTheme.Neutral, () => RefreshProviderCachedResult(provider), provider.available && !providerBusy));
            actions.Add(new ActionSpec("Copy Summary", "Copy this provider's latest audit summary.", 104f, UtilityWindowTheme.Cyan, () => CopyProviderSummary(provider), provider.available));
            actions.Add(new ActionSpec("Copy Brief", "Copy a focused non-destructive review brief for this provider.", 94f, UtilityWindowTheme.Amber, () => CopyProviderFixBrief(provider), provider.issueCount > 0 || provider.cachedResult != null));

            if (job != null && !job.IsTerminal) {
                if (job.state == PungentAuditScanJobState.Paused)
                    actions.Insert(0, new ActionSpec("Resume", "Resume this provider before the next safe checkpoint.", 72f, UtilityWindowTheme.Green, () => runner.ResumeJob(provider.id), job.canPause));
                else if (job.state != PungentAuditScanJobState.Cancelling)
                    actions.Insert(0, new ActionSpec("Pause", "Pause this provider before the next safe checkpoint.", 66f, UtilityWindowTheme.Amber, () => runner.PauseJob(provider.id), job.canPause));
            }

            if (provider.state == ProviderRunState.Failed && !string.IsNullOrWhiteSpace(provider.lastStatus))
                actions.Add(new ActionSpec("Copy Error", "Copy this provider's latest status/error.", 88f, UtilityWindowTheme.Amber, () => CopyProviderError(provider), true));

            DrawWrappedActions(actions, Mathf.Max(240f, position.width - 58f));
        }
        private void DrawFindingsTab() {
            EnsureFilterCache();
            Rect workspace = NormalizeFindingsWorkspaceRect(GetFindingsWorkspaceRect());
            if (!IsUsableFindingsWorkspaceRect(workspace)) {
                DrawFindingsWorkspaceFallback(workspace);
                return;
            }

            if (workspace.width < FindingsSplitThreshold)
                DrawFindingsStackedWorkspace(workspace);
            else
                DrawFindingsSplitWorkspace(workspace);
        }
        private static Rect GetFindingsWorkspaceRect() {
            return GUILayoutUtility.GetRect(
                0f,
                100000f,
                FindingsWorkspaceMinHeight,
                100000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(FindingsWorkspaceMinHeight));
        }
        private Rect NormalizeFindingsWorkspaceRect(Rect workspace) {
            if (!IsFiniteRect(workspace))
                return Rect.zero;

            float tabStripBottom = _lastTabStripBottom;
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.Repaint && _lastTabStripBottom > 1f && _currentTabStripBottom > 1f)
                tabStripBottom = _currentTabStripBottom;

            float tabBodyY = tabStripBottom > 1f ? tabStripBottom + 6f : 0f;
            float reservedY = workspace.y > 1f ? workspace.y : 0f;
            if (tabBodyY <= 1f && reservedY <= 1f)
                return Rect.zero;
            float y = Mathf.Max(tabBodyY, reservedY);
            if (y <= 1f || y >= position.height - 6f)
                return Rect.zero;

            float x = 6f;
            float width = Mathf.Max(0f, position.width - 12f);
            float height = Mathf.Max(0f, position.height - y - 6f);
            if (width <= 1f || height <= 1f)
                return Rect.zero;

            return new Rect(x, Mathf.Floor(y), Mathf.Floor(width), Mathf.Floor(height));
        }
        private static bool IsFiniteRect(Rect rect) {
            return !(float.IsNaN(rect.x) || float.IsNaN(rect.y) || float.IsNaN(rect.width) || float.IsNaN(rect.height)
                || float.IsInfinity(rect.x) || float.IsInfinity(rect.y) || float.IsInfinity(rect.width) || float.IsInfinity(rect.height));
        }
        private static bool IsUsableFindingsWorkspaceRect(Rect workspace) {
            return IsFiniteRect(workspace) && workspace.width >= FindingsWorkspaceFallbackWidth && workspace.height >= FindingsWorkspaceFallbackHeight;
        }
        private static bool IsDrawablePanelRect(Rect rect) {
            return IsFiniteRect(rect) && rect.width >= 1f && rect.height >= 1f;
        }
        private static void DrawFindingsWorkspaceFallback(Rect workspace) {
            if (Event.current == null || Event.current.type != EventType.Repaint || !IsFiniteRect(workspace))
                return;
            Rect fallback = new Rect(
                workspace.x + 8f,
                workspace.y + 8f,
                Mathf.Max(1f, workspace.width - 16f),
                Mathf.Min(72f, Mathf.Max(1f, workspace.height - 16f)));
            if (fallback.width <= 1f || fallback.height <= 1f)
                return;
            EditorGUI.HelpBox(fallback, "Findings workspace is too small to display. Resize the window or undock the panel.", MessageType.Info);
        }
        private void DrawFindingsSplitWorkspace(Rect workspace) {
            workspace = NormalizeFindingsWorkspaceRect(workspace);
            if (!IsUsableFindingsWorkspaceRect(workspace)) {
                DrawFindingsWorkspaceFallback(workspace);
                return;
            }
            float reservedWidth = FindingsSplitHandleWidth + FindingsSplitGap;
            float minList = Mathf.Min(FindingsListMinWidth, Mathf.Max(80f, workspace.width - reservedWidth - 80f));
            float minDetail = Mathf.Min(FindingsDetailMinWidth, Mathf.Max(80f, workspace.width - reservedWidth - minList));
            float listMax = Mathf.Min(FindingsListMaxWidth, workspace.width - minDetail - reservedWidth);
            listMax = Mathf.Max(minList, listMax);

            _issueListWidth = Mathf.Clamp(_issueListWidth, minList, listMax);
            Rect listRect = new Rect(workspace.x, workspace.y, _issueListWidth, workspace.height);
            Rect handleRect = new Rect(listRect.xMax, workspace.y, FindingsSplitHandleWidth, workspace.height);
            Rect detailRect = new Rect(handleRect.xMax + FindingsSplitGap, workspace.y, Mathf.Max(1f, workspace.xMax - handleRect.xMax - FindingsSplitGap), workspace.height);

            DrawFindingsList(listRect);
            DrawFindingsHorizontalSplitHandle(handleRect, minList, listMax);
            DrawIssueDetail(detailRect);
        }
        private void DrawFindingsStackedWorkspace(Rect workspace) {
            workspace = NormalizeFindingsWorkspaceRect(workspace);
            if (!IsUsableFindingsWorkspaceRect(workspace)) {
                DrawFindingsWorkspaceFallback(workspace);
                return;
            }
            float availableHeight = Mathf.Max(1f, workspace.height - FindingsStackedHandleHeight);
            float minList = Mathf.Min(FindingsStackedListMinHeight, availableHeight * 0.72f);
            float minDetail = Mathf.Min(FindingsStackedDetailMinHeight, availableHeight * 0.58f);
            float maxList = Mathf.Max(minList, availableHeight - minDetail);
            if (_findingsStackedListHeight <= 0f)
                _findingsStackedListHeight = availableHeight * 0.62f;
            _findingsStackedListHeight = Mathf.Clamp(_findingsStackedListHeight, minList, maxList);

            float detailHeight = Mathf.Max(1f, availableHeight - _findingsStackedListHeight);
            Rect listRect = new Rect(workspace.x, workspace.y, workspace.width, _findingsStackedListHeight);
            Rect handleRect = new Rect(workspace.x, listRect.yMax, workspace.width, FindingsStackedHandleHeight);
            Rect detailRect = new Rect(workspace.x, handleRect.yMax, workspace.width, detailHeight);

            DrawFindingsList(listRect);
            DrawFindingsVerticalSplitHandle(handleRect, minList, maxList);
            DrawIssueDetail(detailRect);
        }
        private void DrawFindingsHorizontalSplitHandle(Rect rect, float minWidth, float maxWidth) {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            DrawFindingsSplitHandle(rect, false, "Drag to resize Findings and Issue Detail");

            Event evt = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            switch (evt.GetTypeForControl(id)) {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition)) {
                        GUIUtility.hotControl = id;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) {
                        _issueListWidth = Mathf.Clamp(_issueListWidth + evt.delta.x, minWidth, maxWidth);
                        SavePrefs();
                        Repaint();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }
        }
        private void DrawFindingsVerticalSplitHandle(Rect rect, float minHeight, float maxHeight) {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            DrawFindingsSplitHandle(rect, true, "Drag to resize Findings list and Issue Detail");

            Event evt = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            switch (evt.GetTypeForControl(id)) {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition)) {
                        GUIUtility.hotControl = id;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) {
                        _findingsStackedListHeight = Mathf.Clamp(_findingsStackedListHeight + evt.delta.y, minHeight, maxHeight);
                        SavePrefs();
                        Repaint();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }
        }
        private static void DrawFindingsSplitHandle(Rect rect, bool horizontal, string tooltip) {
            if (Event.current.type == EventType.Repaint) {
                Color tint = UtilityWindowTheme.ResizeHandleTint;
                if (horizontal) {
                    Rect line = new Rect(rect.x + 8f, rect.center.y - 0.5f, Mathf.Max(1f, rect.width - 16f), 1f);
                    Rect grip = new Rect(rect.center.x - 18f, rect.center.y - 1.5f, 36f, 3f);
                    EditorGUI.DrawRect(line, new Color(tint.r, tint.g, tint.b, tint.a * 0.45f));
                    EditorGUI.DrawRect(grip, tint);
                }
                else {
                    Rect line = new Rect(rect.center.x - 0.5f, rect.y + 8f, 1f, Mathf.Max(1f, rect.height - 16f));
                    Rect grip = new Rect(rect.center.x - 1.5f, rect.center.y - 18f, 3f, 36f);
                    EditorGUI.DrawRect(line, new Color(tint.r, tint.g, tint.b, tint.a * 0.45f));
                    EditorGUI.DrawRect(grip, tint);
                }
            }
            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }
        private void DrawDeveloperPackageTab() {
            _packageScroll = EditorGUILayout.BeginScrollView(_packageScroll, GUILayout.ExpandHeight(true));
            if (_packageReport == null)
                DrawEmptyState("Package audit has not been run in this session. Run Package Audit to populate structure, registry, performance, release-readiness, and package findings.", "Run Package Audit", RunPackageAudit);
            else {
                DrawPackageStructureCard();
                DrawPackageRegistryCard();
                DrawPackagePerformanceCard();
                DrawPackageReleaseReadinessCard();
                DrawPackageFindingsSummary();
            }
            EditorGUILayout.EndScrollView();
        }
        private void DrawPackageStructureCard() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(_packageReport.asmdefCount == 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 0.11f, 0.05f))) {
                UtilityWindowTheme.SectionTitle("Package Structure", UtilityWindowTheme.Green);
                DrawCountPillGrid(new[] {
                    new PillSpec(_packageReport.scriptCount + " Scripts", UtilityWindowTheme.Blue, 92f),
                    new PillSpec(_packageReport.namespaceDeclarationCount + " Namespaced", UtilityWindowTheme.Teal, 112f),
                    new PillSpec(_packageReport.asmdefCount + " Asmdefs", _packageReport.asmdefCount == 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 92f),
                    new PillSpec(_packageReport.runtimeEditorReferenceCount + " Runtime Editor refs", _packageReport.runtimeEditorReferenceCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 144f)
                });
                EditorGUILayout.LabelField("Runtime/editor separation: " + (_packageReport.runtimeEditorReferenceCount == 0 ? "No unguarded runtime UnityEditor references detected." : _packageReport.runtimeEditorReferenceCount + " runtime/editor separation risk(s) detected."), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawPackageRegistryCard() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(_packageReport.legacyMenuAliasCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, 0.11f, 0.05f))) {
                UtilityWindowTheme.SectionTitle("Registry & Menus", UtilityWindowTheme.Teal);
                DrawCountPillGrid(new[] {
                    new PillSpec(_packageReport.descriptorCount + " Registered", UtilityWindowTheme.Teal, 108f),
                    new PillSpec(CountPackageIssues("Registry", PungentUtilityDesignAudit.Severity.Warning) + " Descriptor warnings", UtilityWindowTheme.Amber, 148f),
                    new PillSpec(_packageReport.legacyMenuAliasCount + " Legacy aliases", _packageReport.legacyMenuAliasCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 120f),
                    new PillSpec(_packageReport.createAssetMenuCount + " CreateAssetMenu", UtilityWindowTheme.Blue, 136f),
                    new PillSpec(CountPackageIssues("Menu", PungentUtilityDesignAudit.Severity.Warning) + " Menu warnings", UtilityWindowTheme.Amber, 124f)
                });
                EditorGUILayout.LabelField("Registry, menu taxonomy, legacy aliases, CreateAssetMenu roots, and descriptor readiness are grouped here for release review.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawPackagePerformanceCard() {
            bool hasWarnings = _packageReport.largeEditorWindowCount > 0 || _packageReport.sceneViewRepaintAllCount > 0;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(hasWarnings ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 0.11f, 0.05f))) {
                UtilityWindowTheme.SectionTitle("Performance Hotspots", hasWarnings ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green);
                DrawCountPillGrid(new[] {
                    new PillSpec(_packageReport.largeEditorWindowCount + " Large windows", _packageReport.largeEditorWindowCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 124f),
                    new PillSpec(_packageReport.sceneViewRepaintAllCount + " RepaintAll", _packageReport.sceneViewRepaintAllCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 108f),
                    new PillSpec(CountPackageIssues("Performance", null) + " Performance rows", UtilityWindowTheme.Blue, 136f)
                });
                EditorGUILayout.LabelField("Package scan highlights FindAssets/reflection/repaint safety as package-development findings only; no package audit runs during repaint.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawPackageReleaseReadinessCard() {
            _assessment = _assessment ?? PungentUtilityReleaseReadiness.Assess(_packageReport);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(_assessment.tint, 0.12f, 0.06f))) {
                UtilityWindowTheme.SectionTitle("Release Readiness", _assessment.tint, _packageReport.developerReleaseBlockerCount + " blockers");
                DrawCountPillGrid(new[] {
                    new PillSpec(_packageReport.ErrorCount + " Errors", _packageReport.ErrorCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 82f),
                    new PillSpec(_packageReport.WarningCount + " Warnings", _packageReport.WarningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 104f),
                    new PillSpec(_packageReport.developerReleaseBlockerCount + " Blockers", _packageReport.developerReleaseBlockerCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 96f)
                });
                EditorGUILayout.LabelField(_assessment.headline, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_assessment.details, UtilityWindowTheme.MutedMiniLabelStyle);
                int count = Mathf.Min(3, _assessment.recommendedPasses.Count);
                for (int i = 0; i < count; i++)
                    DrawUpdatePassInline(_assessment.recommendedPasses[i]);
            }
        }
        private void DrawPackageFindingsSummary() {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.11f, 0.05f))) {
                UtilityWindowTheme.SectionTitle("Package Findings Summary", UtilityWindowTheme.Purple, _packageReport.issues.Count + " findings");
                DrawCountPillGrid(new[] {
                    new PillSpec(_packageReport.ErrorCount + " Errors", UtilityWindowTheme.Red, 82f),
                    new PillSpec(_packageReport.WarningCount + " Warnings", UtilityWindowTheme.Amber, 104f),
                    new PillSpec(_packageReport.InfoCount + " Info", UtilityWindowTheme.Cyan, 74f)
                });
                foreach (IGrouping<string, PungentUtilityDesignAudit.Issue> group in _packageReport.issues.GroupBy(issue => string.IsNullOrWhiteSpace(issue.area) ? "General" : issue.area).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)) {
                    DrawGroupHeader(group.Key, group.Count());
                    PungentUtilityDesignAudit.Issue top = group.OrderByDescending(issue => issue.severity).FirstOrDefault();
                    if (top != null)
                        EditorGUILayout.LabelField(top.severity + " - " + top.target + ": " + Shorten(top.message, 128), UtilityWindowTheme.MutedMiniLabelStyle);
                }
                using (new EditorGUI.DisabledScope(_packageReport.issues.Count == 0)) {
                    if (GUILayout.Button(new GUIContent("Review Package Findings", "Switch to Findings and select the highest-severity package finding."), GUILayout.Width(168f), GUILayout.Height(24f)))
                        SelectFirstPackageFinding();
                }
            }
        }
        private void DrawUpdatePassInline(PungentUtilityReleaseReadiness.UpdatePass pass) {
            using (new EditorGUILayout.VerticalScope()) {
                using (new EditorGUILayout.HorizontalScope()) {
                    UtilityWindowTheme.CountPill(PungentUtilityReleaseReadiness.GetPriorityLabel(pass.priority), PungentUtilityReleaseReadiness.GetPriorityTint(pass.priority), 58f);
                    EditorGUILayout.LabelField(pass.title, EditorStyles.boldLabel);
                    if (GUILayout.Button(new GUIContent("Copy Brief", "Copy this recommended pass as a Codex-ready brief."), EditorStyles.miniButton, GUILayout.Width(78f)))
                        CopyUpdatePassBrief(pass);
                }
                EditorGUILayout.LabelField(pass.summary, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawFindingsList(Rect rect) {
            rect = NormalizeFindingsPanelRect(rect);
            if (!IsDrawablePanelRect(rect))
                return;
            GUILayout.BeginArea(rect);
            try {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.13f, 0.06f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))) {
                    UtilityWindowTheme.SectionTitle(GetBrowserTitle(), UtilityWindowTheme.Teal, _filteredIssues.Count + " shown");
                    _issueListScroll = EditorGUILayout.BeginScrollView(_issueListScroll, GUILayout.ExpandHeight(true));
                    try {
                        if (_filteredIssues.Count == 0) {
                            DrawEmptyState(GetFindingsEmptyState(), GetFindingsEmptyActionLabel(), GetFindingsEmptyAction());
                            GUILayout.FlexibleSpace();
                        }
                        else if (_groupByArea) {
                            for (int groupIndex = 0; groupIndex < _filteredIssueGroups.Count; groupIndex++) {
                                IssueGroup group = _filteredIssueGroups[groupIndex];
                                if (DrawFindingsGroupHeader(group))
                                    continue;
                                for (int issueIndex = 0; issueIndex < group.issues.Count; issueIndex++)
                                    DrawIssueRow(group.issues[issueIndex], rect.width);
                            }
                        }
                        else {
                            for (int i = 0; i < _filteredIssues.Count; i++)
                                DrawIssueRow(_filteredIssues[i], rect.width);
                        }
                    }
                    finally {
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            finally {
                GUILayout.EndArea();
            }
        }
        private static Rect NormalizeFindingsPanelRect(Rect rect) {
            rect.width = Mathf.Max(1f, rect.width);
            rect.height = Mathf.Max(1f, rect.height);
            return rect;
        }
        private bool DrawFindingsGroupHeader(IssueGroup group) {
            if (group == null)
                return false;
            bool expanded = !group.collapsed;
            bool selectedInside = GroupContainsSelectedIssue(group);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(selectedInside ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral, selectedInside ? 0.14f : 0.08f, selectedInside ? 0.07f : 0.04f, 4, 1))) {
                bool nextExpanded = EditorGUILayout.Foldout(expanded, new GUIContent(group.area, "Collapse or expand this scanner/source finding group."), true);
                if (nextExpanded != expanded) {
                    SetFindingGroupCollapsed(group, !nextExpanded);
                    expanded = nextExpanded;
                }
                GUILayout.FlexibleSpace();
                if (selectedInside)
                    UtilityWindowTheme.CountPill("Selected inside", UtilityWindowTheme.Blue, 104f);
                UtilityWindowTheme.CountPill(group.errorCount + " E", group.errorCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral, 44f);
                UtilityWindowTheme.CountPill(group.warningCount + " W", group.warningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 48f);
                UtilityWindowTheme.CountPill(group.infoCount + " I", group.infoCount > 0 ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 42f);
                UtilityWindowTheme.CountPill(group.issues.Count + " findings", UtilityWindowTheme.Neutral, 92f);
            }
            return !expanded;
        }
        private bool GroupContainsSelectedIssue(IssueGroup group) {
            if (group == null || string.IsNullOrWhiteSpace(_selectedIssueKey))
                return false;
            for (int i = 0; i < group.issues.Count; i++) {
                if (string.Equals(BuildIssueKey(group.issues[i]), _selectedIssueKey, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
        private void SetFindingGroupCollapsed(IssueGroup group, bool collapsed) {
            if (group == null || group.collapsed == collapsed)
                return;
            group.collapsed = collapsed;
            UtilityWindowPrefs.SetBool(FindingGroupCollapsedPref(group.key), collapsed);
            Repaint();
        }
        private void DrawIssueRow(PungentUtilityDesignAudit.Issue issue, float width) {
            string key = BuildIssueKey(issue);
            bool selected = string.Equals(key, _selectedIssueKey, StringComparison.Ordinal);
            Color tint = selected ? UtilityWindowTheme.Blue : GetTint(issue.severity);
            PungentNote note = GetCachedNote(issue);
            bool isProject = IsProjectIssue(issue);
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.24f : 0.11f, selected ? 0.12f : 0.05f, 5, 2), GUILayout.MinHeight(IssueRowHeight))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(new GUIContent(issue.severity.ToString(), "Select this finding."), EditorStyles.miniButton, GUILayout.Width(76f)))
                        SelectIssue(key);
                    if (GUILayout.Button(new GUIContent(Shorten(issue.target, width < 360f ? 44 : 72), issue.target), UtilityWindowTheme.CardLabelStyle, GUILayout.MinWidth(80f)))
                        SelectIssue(key);
                    GUILayout.FlexibleSpace();
                    if (note != null)
                        UtilityWindowTheme.CountPill(note.archived ? "Archived note" : "Note linked", note.archived ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Purple, note.archived ? 96f : 88f);
                }
                DrawWrappedLabel(BuildIssueMetadataLine(issue, provider, isProject, note), UtilityWindowTheme.MutedMiniLabelStyle);
                if (GUILayout.Button(new GUIContent(Shorten(issue.message, width < 360f ? 96 : 150), issue.message), UtilityWindowTheme.MutedMiniLabelStyle))
                    SelectIssue(key);
                DrawWrappedLabel("Next: " + GetIssueRecommendedAction(issue), UtilityWindowTheme.PathLabelStyle);
                DrawIssueRowActions(issue, provider, note, key, width);
            }
        }

        private void DrawIssueRowActions(PungentUtilityDesignAudit.Issue issue, ProjectAuditProvider provider, PungentNote note, string key, float width) {
            List<ActionSpec> actions = new List<ActionSpec> {
                new ActionSpec("Details", "Select this finding and show Project Issue Detail.", 64f, UtilityWindowTheme.Neutral, () => SelectIssue(key), true)
            };
            if (provider != null)
                actions.Add(new ActionSpec("Open Tool", provider.description, 82f, UtilityWindowTheme.Teal, () => OpenProvider(provider.id), provider.available));
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                actions.Add(new ActionSpec("Ping", "Ping the referenced asset or context.", 52f, UtilityWindowTheme.Neutral, () => PingAsset(issue.assetPath), true));
            actions.Add(new ActionSpec(note == null ? "Note" : "Open Note", note == null ? "Create a follow-up note for this finding." : "Open the linked audit note.", note == null ? 58f : 82f, UtilityWindowTheme.Purple, () => OpenOrCreateIssueNote(issue, note), true));
            actions.Add(new ActionSpec("Copy", "Copy this issue to the clipboard.", 54f, UtilityWindowTheme.Neutral, () => CopyIssue(issue), true));
            DrawWrappedActions(actions, Mathf.Max(180f, width - 18f));
        }
        private void DrawIssueDetail(Rect rect) {
            PungentUtilityDesignAudit.Issue selected = GetSelectedIssue();
            rect = NormalizeFindingsPanelRect(rect);
            if (!IsDrawablePanelRect(rect))
                return;
            GUILayout.BeginArea(rect);
            try {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))) {
                    UtilityWindowTheme.SectionTitle(GetDetailTitle(), UtilityWindowTheme.Neutral, selected != null ? selected.severity.ToString() : "No selection");
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));
                    try {
                        if (selected == null) {
                            DrawEmptyState("Select a finding to inspect details, create an audit note, copy a fix brief, or open the owning scanner.", GetFindingsEmptyActionLabel(), GetFindingsEmptyAction());
                            GUILayout.FlexibleSpace();
                        }
                        else {
                            DrawIssueDetailFields(selected);
                            EditorGUILayout.Space(6f);
                            DrawIssueActions(selected, rect.width);
                            EditorGUILayout.Space(6f);
                            DrawIssueRawDetails(selected);
                        }
                    }
                    finally {
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            finally {
                GUILayout.EndArea();
            }
        }
        private void DrawIssueDetailFields(PungentUtilityDesignAudit.Issue issue) {
            PungentNote note = GetCachedNote(issue);
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            string source = provider != null ? provider.displayName : issue.area;
            using (new EditorGUILayout.HorizontalScope()) {
                UtilityWindowTheme.CountPill(issue.severity.ToString(), GetTint(issue.severity), 82f);
                EditorGUILayout.LabelField(issue.target, UtilityWindowTheme.CardLabelStyle);
                GUILayout.FlexibleSpace();
            }
            string freshness = provider != null ? " | " + GetIssueFreshnessLabel(provider) + " | scanned " + GetIssueAgeLabel(provider) : string.Empty;
            DrawWrappedLabel(source + " | " + GetIssueTypeLabel(issue) + freshness, UtilityWindowTheme.MutedMiniLabelStyle);

            DrawDetailText("What happened", issue.message);
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                DrawDetailText("Affected asset / context", issue.assetPath, UtilityWindowTheme.PathLabelStyle);
            DrawDetailText("Why it matters", GetIssueImpact(issue), UtilityWindowTheme.MutedMiniLabelStyle);
            DrawDetailText("Recommended next step", GetIssueRecommendedAction(issue), UtilityWindowTheme.MutedMiniLabelStyle);
            string noteState = note == null ? "No follow-up note linked" : (note.archived ? "Archived note linked: " : "Linked note: ") + note.title;
            DrawDetailText("Related Note", noteState);
        }
        private void DrawIssueRawDetails(PungentUtilityDesignAudit.Issue issue) {
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            _rawIssueDetailsExpanded = EditorGUILayout.Foldout(_rawIssueDetailsExpanded, "Raw Details", true);
            if (_rawIssueDetailsExpanded) {
                DrawDetailText("Issue Code", GetIssueCode(issue));
                DrawDetailText("Source Area", issue.area);
                if (!string.IsNullOrWhiteSpace(issue.assetPath))
                    DrawDetailText("Asset Path", issue.assetPath, UtilityWindowTheme.PathLabelStyle);
                DrawDetailText("Scan Scope", provider != null && provider.cachedResult != null ? provider.cachedResult.ScopeLabel : "Unknown");
                DrawDetailText("Scan Timestamp", provider != null && provider.cachedResult != null && provider.cachedResult.IsComplete ? provider.cachedResult.CompletedAtUtc.ToLocalTime().ToString("f") : "Unknown");
                if (provider != null) {
                    PungentProjectAuditIndexEntry index = PungentProjectAuditIndex.Get(provider.id);
                    DrawDetailText("Scan Mode", index != null ? index.LastScanMode : "Unknown");
                    DrawDetailText("Cache State", provider.lastStatus);
                    DrawDetailText("Cache Freshness", GetIssueFreshnessLabel(provider));
                    DrawDetailText("Provider ID", provider.id, UtilityWindowTheme.PathLabelStyle);
                }
            }
        }
        private void DrawIssueActions(PungentUtilityDesignAudit.Issue issue, float width) {
            PungentNote note = GetCachedNote(issue);
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            List<ActionSpec> actions = new List<ActionSpec>();
            if (provider != null) {
                actions.Add(new ActionSpec(provider.openButtonLabel, provider.description, 154f, UtilityWindowTheme.Teal, () => OpenProvider(provider.id), provider.available));
                actions.Add(new ActionSpec("Run Again", "Run this provider again through the cooperative audit runner.", 92f, UtilityWindowTheme.Green, () => RunProviderFromDashboard(provider), provider.available && provider.canRunFromDashboard && !AnyScanActive && !_pendingRestartAudit));
            }
            else if (DeveloperToolsVisible && !IsProjectIssue(issue))
                actions.Add(new ActionSpec("Run Package Audit", "Run the package-development audit.", 132f, UtilityWindowTheme.Teal, RunPackageAudit, true));

            if (note == null) {
                actions.Add(new ActionSpec("Create Audit Note", "Create a follow-up note for this finding.", 126f, UtilityWindowTheme.Purple, () => {
                    try {
                        PungentNote created = PungentNoteAuditIssueBridge.CreateOrOpen(issue);
                        _noteByIssueKey[BuildIssueKey(issue)] = created;
                        PungentNotesRoadmapWindow.OpenAndSelect(created.id);
                        _status = "Created audit issue note.";
                    }
                    catch (Exception ex) {
                        _status = "Could not create audit note: " + ex.Message;
                    }
                }, true));
            }
            else
                actions.Add(new ActionSpec(note.archived ? "Open Archived" : "Open Note", "Open the linked audit note.", note.archived ? 118f : 96f, UtilityWindowTheme.Purple, () => PungentNotesRoadmapWindow.OpenAndSelect(note.id), true));
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                actions.Add(new ActionSpec("Ping Asset", "Ping the script or asset referenced by this finding.", 88f, UtilityWindowTheme.Neutral, () => PingAsset(issue.assetPath), true));
            actions.Add(new ActionSpec("Copy Issue", "Copy the selected issue fields to the clipboard.", 88f, UtilityWindowTheme.Neutral, () => CopyIssue(issue), true));
            actions.Add(new ActionSpec("Copy Fix Brief", "Copy a focused implementation brief for this finding.", 112f, UtilityWindowTheme.Amber, () => CopyFixBrief(issue), true));
            DrawWrappedActions(actions, Mathf.Max(220f, width - 18f));
        }
        private void DrawDeveloperToolsToggle() {
            if (!_developerModeAvailable || !_developerModeEnabled)
                return;
            bool next = EditorGUILayout.ToggleLeft(new GUIContent("Developer Mode", "Show or hide package-development compliance, release-readiness passes, documentation checks, and developer blockers. Enable Developer Tools in Utilities Browser settings first."), _showDeveloperTools, GUILayout.Width(128f));
            if (next != _showDeveloperTools) {
                _showDeveloperTools = next;
                UtilityWindowPrefs.SetBool(PrefShowDeveloperTools, _showDeveloperTools);
                if (!DeveloperToolsVisible && _selectedTab == AuditViewTab.DeveloperPackage)
                    SelectTab(AuditViewTab.ProjectAudit);
                MarkFilterDirty();
            }
        }
        private void InitializeProjectProviders() {
            _providers.Clear();
            RegisterBuiltInProjectAuditProviders();
            IReadOnlyList<IPungentAuditScanProvider> providers = PungentAuditScanProviderRegistry.Providers;
            for (int i = 0; i < providers.Count; i++)
                AddProvider(providers[i]);
        }
        internal static void RegisterBuiltInProjectAuditProviders() {
            PungentAuditScanProviderRegistry.Register(new TerrainUsageAuditProvider());
            PungentAuditScanProviderRegistry.Register(new SceneIssueAuditProvider());
            PungentAuditScanProviderRegistry.Register(new PungentSceneGizmoAuditProvider());
            PungentAuditScanProviderRegistry.Register(new ReferenceAssignmentAuditProvider());
            PungentAuditScanProviderRegistry.Register(new AudioSetupCoverageAuditProvider());
            PungentAuditScanProviderRegistry.Register(new AudioCatalogCoverageAuditProvider());
            PungentAuditScanProviderRegistry.Register(new CoverageMatrixAuditProvider());
            PungentAuditScanProviderRegistry.Register(new TokenValidatorAuditProvider());
        }
        private void AddProvider(IPungentAuditScanProvider adapter) {
            if (adapter == null)
                return;
            bool defaultEnabled = adapter.CanRunFromCoordinator && (!string.Equals(adapter.ProviderId, "token-validator", StringComparison.OrdinalIgnoreCase) || adapter.CanRunBackground);
            _providers.Add(new ProjectAuditProvider {
                id = adapter.ProviderId,
                displayName = adapter.DisplayName,
                description = adapter.Description,
                openButtonLabel = adapter.OpenButtonLabel,
                canRunFromDashboard = adapter.CanRunFromCoordinator,
                runButtonLabel = string.IsNullOrWhiteSpace(adapter.RunButtonLabel) ? "Run Check" : adapter.RunButtonLabel,
                enabled = UtilityWindowPrefs.GetBool(ProviderEnabledPref(adapter.ProviderId), defaultEnabled),
                adapter = adapter,
                state = ProviderRunState.Pending,
                lastStatus = "Pending"
            });
        }
        private void RefreshProviderAvailability() {
            for (int i = 0; i < _providers.Count; i++) {
                ProjectAuditProvider provider = _providers[i];
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(provider.id);
                provider.descriptor = descriptor;
                provider.available = (descriptor != null && descriptor.CanOpen) || provider.adapter != null;
                provider.canRunFromDashboard = provider.adapter != null && provider.adapter.CanRunFromCoordinator;
                provider.notConfiguredReason = string.Empty;
                if (provider.adapter != null && provider.adapter.TryGetNotConfiguredReason(out string reason))
                    provider.notConfiguredReason = reason;
                if (!provider.available) {
                    provider.state = ProviderRunState.Failed;
                    provider.lastStatus = "Unavailable";
                }
                else if (provider.state == ProviderRunState.Failed && string.Equals(provider.lastStatus, "Unavailable", StringComparison.Ordinal)) {
                    provider.state = ProviderRunState.Pending;
                    provider.lastStatus = "Pending";
                }
            }
        }
        private void RunProjectAudit() {
            RefreshProviderAvailability();
            if (_pendingRestartAudit) {
                _status = "Restart is preparing a fresh project audit. Wait for cleanup to finish before starting another scan.";
                return;
            }
            if (AnyScanActive) {
                _status = "Project audit is already running.";
                return;
            }
            List<ProjectAuditProvider> included = _providers.Where(provider => provider.enabled && provider.available).ToList();
            List<ProjectAuditProvider> runnable = included.Where(provider => CanProviderRunInMode(provider, PungentAuditScanMode.Immediate) && !TryGetProviderNotConfiguredReason(provider, out _)).ToList();
            List<ProjectAuditProvider> manual = _providers.Where(provider => provider.enabled && provider.available && !provider.canRunFromDashboard).ToList();
            List<ProviderSkipInfo> skipped = BuildProviderSkipInfo(included);
            if (!ConfirmRunEnabledAudits(runnable, manual, skipped)) {
                _status = "Run Enabled Audits was not started.";
                return;
            }

            StartRunnerBatch(included, PungentAuditScanMode.Immediate);
            if (runnable.Count == 0)
                RefreshCachedResults(false);
            _lastProjectAuditUtc = DateTime.UtcNow;
            MarkViewChanged(included.Count == 0 ? "No project audit providers selected; refreshed cached results." : "Queued " + included.Count + " project audit provider(s).");
        }
        private void RunBackgroundProjectAudit() {
            RefreshProviderAvailability();
            if (_pendingRestartAudit) {
                _status = "Restart is preparing a fresh project audit. Wait for cleanup to finish before starting another scan.";
                return;
            }
            if (AnyScanActive) {
                _status = "Project audit is already running.";
                return;
            }
            if (!PungentAuditScanSettings.BackgroundScanEnabled) {
                _status = "Background Project Audit is off. Set it to IdleOnly before starting a background scan.";
                return;
            }
            if (!PungentAuditScanSettings.CanRunBackgroundScanNow(out string reason)) {
                _status = "Background Project Audit cannot start yet: " + reason + ".";
                return;
            }

            List<ProjectAuditProvider> included = _providers.Where(provider => provider.enabled && provider.available && CanProviderRunInMode(provider, PungentAuditScanMode.BackgroundIdle)).ToList();
            StartRunnerBatch(included, PungentAuditScanMode.BackgroundIdle);
            _lastProjectAuditUtc = DateTime.UtcNow;
            MarkViewChanged(included.Count == 0 ? "No included providers selected for background scan." : "Queued " + included.Count + " provider(s) in Background Idle mode.");
        }
        private bool ConfirmRunEnabledAudits(IReadOnlyList<ProjectAuditProvider> runnable, IReadOnlyList<ProjectAuditProvider> manual, IReadOnlyList<ProviderSkipInfo> skipped) {
            if ((runnable == null || runnable.Count == 0) && (skipped == null || skipped.Count == 0))
                return true;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("The following providers will scan project data:");
            sb.AppendLine();
            if (runnable != null && runnable.Count > 0) {
                for (int i = 0; i < runnable.Count; i++)
                    sb.AppendLine("- " + runnable[i].displayName);
            }
            else {
                sb.AppendLine("- None");
            }

            if ((manual != null && manual.Count > 0) || (skipped != null && skipped.Count > 0)) {
                sb.AppendLine();
                sb.AppendLine("These providers will be skipped or refreshed from cache only:");
                if (manual != null) {
                    for (int i = 0; i < manual.Count; i++)
                        sb.AppendLine("- " + manual[i].displayName + ": no safe coordinator hook");
                }
                if (skipped != null) {
                    for (int i = 0; i < skipped.Count; i++)
                        sb.AppendLine("- " + skipped[i].displayName + ": " + skipped[i].reason);
                }
            }
            sb.AppendLine();
            sb.AppendLine("This will not apply changes, but it may take time in large projects. Progress can be paused and resumed. If paused, you can restart the audit from the beginning.");
            return EditorUtility.DisplayDialog("Run Enabled Project Audits?", sb.ToString(), "Run Audits", "Cancel");
        }

        private void RestartCurrentAudit() {
            PungentAuditScanRunner runner = VisibleScanRunner;
            if (runner == null || !runner.IsActive || !runner.IsPaused) {
                _status = "Restart is available only while a project audit is paused.";
                return;
            }

            bool restart = EditorUtility.DisplayDialog(
                "Restart Project Audit?",
                "This will discard the current paused audit progress and start the selected enabled providers from the beginning.",
                "Restart Audit",
                "Keep Paused");
            if (!restart) {
                _status = "Project audit remains paused.";
                return;
            }

            PungentAuditScanMode mode = runner.CurrentMode == PungentAuditScanMode.BackgroundIdle
                ? PungentAuditScanMode.BackgroundIdle
                : PungentAuditScanMode.Immediate;
            runner.InterruptForRestart("Paused audit restarted from the beginning. Previous completed cache remains available until replaced by new scan results.");
            SchedulePendingRestartAudit(mode);
            MarkViewChanged("Restart requested. Waiting for previous audit cleanup before starting a fresh batch.");
        }
        private void SchedulePendingRestartAudit(PungentAuditScanMode mode) {
            _pendingRestartAudit = true;
            _pendingRestartAuditAttempts = 0;
            _pendingRestartAuditMode = mode;
            EditorApplication.delayCall -= TryStartPendingRestartAudit;
            EditorApplication.delayCall += TryStartPendingRestartAudit;
        }
        private void TryStartPendingRestartAudit() {
            if (!_pendingRestartAudit)
                return;

            if (_scanRunner.IsActive || PungentAuditIdleScanScheduler.Runner.IsActive) {
                RetryPendingRestartAudit("previous audit cleanup is still in progress");
                return;
            }

            RefreshProviderAvailability();
            List<ProjectAuditProvider> included = _providers
                .Where(provider => provider.enabled && provider.available && (_pendingRestartAuditMode != PungentAuditScanMode.BackgroundIdle || CanProviderRunInMode(provider, PungentAuditScanMode.BackgroundIdle)))
                .ToList();

            _pendingRestartAudit = false;
            _pendingRestartAuditAttempts = 0;
            _runnerCacheAppliedProviderIds.Clear();
            StartRunnerBatch(included, _pendingRestartAuditMode);
            _lastProjectAuditUtc = DateTime.UtcNow;
            MarkViewChanged(included.Count == 0
                ? "Paused audit restart completed; no enabled providers were runnable in the current mode."
                : "Paused audit restarted from the beginning. Previous completed cache remains available until replaced by new scan results.");
        }
        private void RetryPendingRestartAudit(string reason) {
            _pendingRestartAuditAttempts++;
            if (_pendingRestartAuditAttempts > RestartAuditMaxDelayAttempts) {
                _pendingRestartAudit = false;
                _pendingRestartAuditAttempts = 0;
                MarkViewChanged("Restart could not begin because " + reason + ". The previous cache is still available; try Run Enabled Audits again.");
                return;
            }

            _status = "Restart is waiting because " + reason + ".";
            EditorApplication.delayCall -= TryStartPendingRestartAudit;
            EditorApplication.delayCall += TryStartPendingRestartAudit;
            Repaint();
        }
        private void StartRunnerBatch(IReadOnlyList<ProjectAuditProvider> included, PungentAuditScanMode mode) {
            _runnerCacheAppliedProviderIds.Clear();
            _activeSharedSceneProviderIds.Clear();
            List<PungentAuditScanJob> jobs = new List<PungentAuditScanJob>();
            List<PungentSceneScanProviderBinding> sceneBindings = new List<PungentSceneScanProviderBinding>();
            int sceneJobIndex = -1;
            for (int i = 0; i < included.Count; i++) {
                ProjectAuditProvider provider = included[i];
                if (TryCreateSceneScanBinding(provider, mode, out PungentSceneScanProviderBinding binding)) {
                    sceneBindings.Add(binding);
                    _activeSharedSceneProviderIds.Add(provider.id);
                    if (sceneJobIndex < 0)
                        sceneJobIndex = jobs.Count;
                    provider.state = ProviderRunState.Queued;
                    provider.lastStatus = "Queued in shared scene scan pass";
                    continue;
                }

                PungentAuditScanJob job = CreateProviderJob(provider, mode);
                jobs.Add(job);
                provider.state = MapJobState(job.state);
                provider.lastStatus = GetScanJobStateLabel(job.state);
            }
            if (sceneBindings.Count > 0) {
                PungentAuditScanJob sceneJob = PungentSceneScanCoordinator.CreateJob(sceneBindings, mode);
                int insertIndex = Mathf.Clamp(sceneJobIndex < 0 ? jobs.Count : sceneJobIndex, 0, jobs.Count);
                jobs.Insert(insertIndex, sceneJob);
            }
            _scanRunner.StartBatch(jobs, mode);
        }
        private bool TryCreateSceneScanBinding(ProjectAuditProvider provider, PungentAuditScanMode mode, out PungentSceneScanProviderBinding binding) {
            binding = null;
            if (provider == null || provider.adapter == null || !provider.available || !provider.canRunFromDashboard)
                return false;
            if (!CanProviderRunInMode(provider, mode))
                return false;
            if (TryGetProviderNotConfiguredReason(provider, out _))
                return false;
            IPungentSceneScanProvider sceneProvider = provider.adapter as IPungentSceneScanProvider;
            return sceneProvider != null && sceneProvider.TryCreateSceneScanBinding(mode, out binding) && binding != null;
        }
        private PungentAuditScanJob CreateProviderJob(ProjectAuditProvider provider, PungentAuditScanMode mode) {
            if (provider == null)
                return new PungentAuditScanJob("unknown-provider", "Unknown Provider", job => PungentAuditScanStepResult.Skipped("Provider was missing."));

            if (!provider.available) {
                return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                    job.Report(1f, 0, 0, "Provider is unavailable.", false, "Skipped");
                    return PungentAuditScanStepResult.Skipped("Provider is unavailable.");
                }) {
                    scanMode = mode,
                    canPause = false,
                    canCancel = false
                };
            }

            if (!provider.canRunFromDashboard) {
                return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                    job.Report(1f, 0, 0, "Manual scanner run required; cached result was not changed.", false, "Skipped");
                    return PungentAuditScanStepResult.Skipped("Manual scanner run required; cached result was not changed.");
                }) {
                    scanMode = mode,
                    canPause = false,
                    canCancel = false,
                    capabilities = PungentAuditScanJobCapabilities.CacheOnlyOrManualOnly
                };
            }

            if (!CanProviderRunInMode(provider, mode)) {
                string message = mode == PungentAuditScanMode.BackgroundIdle
                    ? "Provider is immediate-only and was skipped by Background Idle mode."
                    : "Provider cannot run in this scan mode.";
                return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                    job.Report(1f, 0, 0, message, false, "Skipped");
                    return PungentAuditScanStepResult.Skipped(message);
                }) {
                    scanMode = mode,
                    canPause = false,
                    canCancel = false,
                    capabilities = PungentAuditScanJobCapabilities.ImmediateOnly | PungentAuditScanJobCapabilities.ScanOnly
                };
            }

            if (TryGetProviderNotConfiguredReason(provider, out string reason)) {
                return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                    job.Report(1f, 0, 0, reason, false, "Not configured");
                    return PungentAuditScanStepResult.NotConfigured(reason);
                }) {
                    scanMode = mode,
                    canPause = false,
                    canCancel = false,
                    capabilities = PungentAuditScanJobCapabilities.ScanOnly
                };
            }

            try {
                PungentAuditScanJob scanJob = provider.adapter != null ? provider.adapter.CreateJob(mode) : null;
                if (scanJob == null) {
                    return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                        job.Report(1f, 0, 0, "Provider did not create a scan job.", false, "Skipped");
                        return PungentAuditScanStepResult.Skipped("Provider did not create a scan job.");
                    }) {
                        scanMode = mode,
                        canPause = false,
                        canCancel = false
                    };
                }
                scanJob.scanMode = mode;
                scanJob.canPause = provider.adapter.CanPause;
                scanJob.canCancel = provider.adapter.CanCancel;
                return scanJob;
            }
            catch (Exception ex) {
                return new PungentAuditScanJob(provider.id, provider.displayName, job => {
                    PungentScanResult result = PublishCoordinatorStatus(provider, PungentScanSeverity.Error, "Scan job failed", ex.Message, "SCAN_EXCEPTION");
                    job.AttachResult(result);
                    job.Report(1f, 0, 0, ex.Message, false, "Failed");
                    return PungentAuditScanStepResult.Failed(ex.Message, result);
                }) {
                    scanMode = mode,
                    canPause = false,
                    canCancel = false
                };
            }
        }
        private List<ProviderSkipInfo> BuildProviderSkipInfo(IReadOnlyList<ProjectAuditProvider> providers) {
            List<ProviderSkipInfo> skipped = new List<ProviderSkipInfo>();
            if (providers == null)
                return skipped;
            for (int i = 0; i < providers.Count; i++) {
                ProjectAuditProvider provider = providers[i];
                if (provider == null || !provider.canRunFromDashboard)
                    continue;
                if (TryGetProviderNotConfiguredReason(provider, out string reason))
                    skipped.Add(new ProviderSkipInfo(provider.displayName, reason));
            }
            return skipped;
        }
        private bool TryGetProviderNotConfiguredReason(ProjectAuditProvider provider, out string reason) {
            reason = string.Empty;
            if (provider == null)
                return false;
            if (!string.IsNullOrWhiteSpace(provider.notConfiguredReason)) {
                reason = provider.notConfiguredReason;
                return true;
            }
            return provider.adapter != null && provider.adapter.TryGetNotConfiguredReason(out reason);
        }
        private static bool CanProviderRunInMode(ProjectAuditProvider provider, PungentAuditScanMode mode) {
            if (provider == null || provider.adapter == null || !provider.canRunFromDashboard)
                return false;
            return mode == PungentAuditScanMode.BackgroundIdle ? provider.adapter.CanRunBackground : provider.adapter.CanRunImmediate;
        }
        private static PungentScanResult PublishCoordinatorStatus(ProjectAuditProvider provider, PungentScanSeverity severity, string title, string message, string code) {
            if (provider == null)
                return null;
            PungentScanSession session = new PungentScanSession(provider.id, provider.displayName);
            PungentScanResult result = session.Begin(PungentScanScope.Custom, "Coordinator");
            result.AddIssue(severity, title, message, null, null, code);
            return session.Complete(0, 0, 0, 0, message);
        }
        private void OnScanRunnerChanged() {
            ApplyRunnerStateToProviders();
            Repaint();
        }
        private void OnScanRunnerCompleted() {
            PungentAuditScanRunner completedRunner = PungentAuditIdleScanScheduler.Runner.LastCompletedTicks > _scanRunner.LastCompletedTicks
                ? PungentAuditIdleScanScheduler.Runner
                : _scanRunner;
            if (PungentAuditScanSettings.AutoRefreshCacheAfterScanCompletion)
                RefreshCachedResults(false);
            ApplyRunnerStateToProviders();
            _lastProjectAuditUtc = DateTime.UtcNow;
            _status = completedRunner.StatusMessage;
            PungentUtilityDesignAudit.Issue top = _projectIssues.Where(IsEnabledProviderIssue).OrderByDescending(issue => issue.severity).FirstOrDefault();
            if (top != null && completedRunner.CancelledCount == 0)
                SelectIssueAndShowFindings(top);
            else
                MarkViewChanged(_status);
        }
        private void ApplyRunnerStateToProviders() {
            ApplyRunnerStateToProviders(_scanRunner);
            ApplyRunnerStateToProviders(PungentAuditIdleScanScheduler.Runner);
        }
        private void ApplyRunnerStateToProviders(PungentAuditScanRunner runner) {
            if (runner == null)
                return;
            for (int i = 0; i < runner.Jobs.Count; i++) {
                PungentAuditScanJob job = runner.Jobs[i];
                if (string.Equals(job.providerId, PungentSceneScanCoordinator.ProviderId, StringComparison.OrdinalIgnoreCase)) {
                    ApplySharedSceneRunnerState(job);
                    continue;
                }
                ProjectAuditProvider provider = FindProvider(job.providerId);
                if (provider == null)
                    continue;
                provider.state = MapJobState(job.state);
                provider.lastStatus = GetScanJobStateLabel(job.state);
                if (!string.IsNullOrWhiteSpace(job.statusMessage))
                    provider.lastStatus = provider.lastStatus + " - " + Shorten(job.statusMessage, 48);
                if (job.IsTerminal && !_runnerCacheAppliedProviderIds.Contains(provider.id)) {
                    _runnerCacheAppliedProviderIds.Add(provider.id);
                    if (job.state == PungentAuditScanJobState.Complete || job.state == PungentAuditScanJobState.Failed || job.state == PungentAuditScanJobState.NotConfigured)
                        RefreshProviderCachedResult(provider);
                }
            }
        }
        private void ApplySharedSceneRunnerState(PungentAuditScanJob job) {
            if (job == null || _activeSharedSceneProviderIds.Count == 0)
                return;
            foreach (string providerId in _activeSharedSceneProviderIds) {
                ProjectAuditProvider provider = FindProvider(providerId);
                if (provider == null)
                    continue;
                provider.state = MapJobState(job.state);
                provider.lastStatus = GetScanJobStateLabel(job.state);
                if (!string.IsNullOrWhiteSpace(job.statusMessage))
                    provider.lastStatus = provider.lastStatus + " - " + Shorten(job.statusMessage, 48);
                if (job.IsTerminal && !_runnerCacheAppliedProviderIds.Contains(provider.id)) {
                    _runnerCacheAppliedProviderIds.Add(provider.id);
                    if (job.state == PungentAuditScanJobState.Complete || job.state == PungentAuditScanJobState.Failed || job.state == PungentAuditScanJobState.NotConfigured)
                        RefreshProviderCachedResult(provider);
                }
            }
        }
        private static ProviderRunState MapJobState(PungentAuditScanJobState state) {
            switch (state) {
                case PungentAuditScanJobState.Queued:
                    return ProviderRunState.Queued;
                case PungentAuditScanJobState.Running:
                    return ProviderRunState.Running;
                case PungentAuditScanJobState.Cancelling:
                    return ProviderRunState.Cancelling;
                case PungentAuditScanJobState.Paused:
                    return ProviderRunState.Paused;
                case PungentAuditScanJobState.Complete:
                    return ProviderRunState.Complete;
                case PungentAuditScanJobState.Failed:
                    return ProviderRunState.Failed;
                case PungentAuditScanJobState.Cancelled:
                    return ProviderRunState.Cancelled;
                case PungentAuditScanJobState.NotConfigured:
                    return ProviderRunState.NotConfigured;
                case PungentAuditScanJobState.Skipped:
                    return ProviderRunState.Skipped;
                default:
                    return ProviderRunState.Pending;
            }
        }
        private static string GetScanJobStateLabel(PungentAuditScanJobState state) {
            switch (state) {
                case PungentAuditScanJobState.Queued: return "Queued";
                case PungentAuditScanJobState.Running: return "Running";
                case PungentAuditScanJobState.Paused: return "Paused";
                case PungentAuditScanJobState.Cancelling: return "Interrupting";
                case PungentAuditScanJobState.Cancelled: return "Interrupted";
                case PungentAuditScanJobState.Complete: return "Complete";
                case PungentAuditScanJobState.Failed: return "Failed";
                case PungentAuditScanJobState.NotConfigured: return "Not Configured";
                case PungentAuditScanJobState.Skipped: return "Skipped";
                default: return "Not Scanned";
            }
        }
        private string GetBatchStateLabel() {
            PungentAuditScanRunner runner = VisibleScanRunner;
            switch (runner.BatchState) {
                case PungentAuditBatchState.Queued:
                    return runner.CurrentMode == PungentAuditScanMode.BackgroundIdle ? "Background Project Audit Queued" : "Project Audit Queued";
                case PungentAuditBatchState.Running:
                    return runner.CurrentMode == PungentAuditScanMode.BackgroundIdle ? "Running Background Project Audit" : "Running Project Audit";
                case PungentAuditBatchState.Paused:
                    return "Project Audit Paused";
                case PungentAuditBatchState.Cancelling:
                    return "Project Audit Interrupting";
                case PungentAuditBatchState.Cancelled:
                    return "Project Audit Interrupted";
                case PungentAuditBatchState.Failed:
                    return "Project Audit Failed";
                case PungentAuditBatchState.CompleteWithIssues:
                    return "Project Audit Completed with issues";
                case PungentAuditBatchState.Complete:
                    return "Project Audit Complete";
                default:
                    return "Project Audit";
            }
        }
        private Color GetBatchStateTint() {
            switch (VisibleScanRunner.BatchState) {
                case PungentAuditBatchState.Cancelled:
                case PungentAuditBatchState.Paused:
                    return UtilityWindowTheme.Neutral;
                case PungentAuditBatchState.Failed:
                    return UtilityWindowTheme.Red;
                case PungentAuditBatchState.CompleteWithIssues:
                    return UtilityWindowTheme.Amber;
                case PungentAuditBatchState.Complete:
                    return UtilityWindowTheme.Green;
                default:
                    return UtilityWindowTheme.Teal;
            }
        }
        private void RefreshCachedResults(bool updateTimestamp = true) {
            RefreshProviderAvailability();
            _projectIssues.Clear();
            _providerIdByIssueKey.Clear();
            _issueCodeByIssueKey.Clear();
            for (int i = 0; i < _providers.Count; i++) {
                ProjectAuditProvider provider = _providers[i];
                provider.issueCount = 0;
                provider.warningCount = 0;
                provider.errorCount = 0;
                provider.infoCount = 0;
                provider.successCount = 0;
                provider.cachedResult = null;
                provider.topFinding = string.Empty;
                if (!provider.available)
                    continue;
                if (PungentScanCache.TryGet(provider.id, out PungentScanResult result) && result != null) {
                    if (!provider.enabled && provider.canRunFromDashboard)
                        continue;
                    IngestCachedResult(provider, result);
                }
                else {
                    if (!provider.enabled)
                        continue;
                    AddNoCacheFinding(provider);
                }
            }
            if (updateTimestamp)
                _lastProjectAuditUtc = DateTime.UtcNow;
            MarkViewChanged("Cached project audit results refreshed.");
        }
        private void IngestCachedResult(ProjectAuditProvider provider, PungentScanResult result) {
            provider.cachedResult = result;
            provider.state = ResolveProviderStateFromResult(result);
            provider.lastStatus = result.IsComplete
                ? GetProviderStateLabel(provider.state)
                : "Running";
            provider.successCount = result.Summary.SuccessCount;
            for (int i = 0; i < result.Issues.Count; i++) {
                PungentScanIssue scanIssue = result.Issues[i];
                if (scanIssue == null || scanIssue.Severity == PungentScanSeverity.Success || scanIssue.Severity == PungentScanSeverity.None)
                    continue;
                PungentUtilityDesignAudit.Issue issue = CreateProjectIssue(
                    MapSeverity(scanIssue.Severity),
                    provider,
                    string.IsNullOrWhiteSpace(scanIssue.Title) ? provider.displayName : scanIssue.Title,
                    scanIssue.Message,
                    BuildCachedRecommendation(provider, scanIssue),
                    scanIssue.Path);
                AddProjectIssue(provider, issue, scanIssue);
                if (string.IsNullOrWhiteSpace(provider.topFinding))
                    provider.topFinding = issue.message;
            }
            if (provider.issueCount == 0) {
                provider.topFinding = "Cached scan completed with no warnings or errors.";
                provider.successCount = Mathf.Max(1, provider.successCount);
            }
        }
        private void AddNoCacheFinding(ProjectAuditProvider provider) {
            if (!string.IsNullOrWhiteSpace(provider.notConfiguredReason)) {
                provider.state = ProviderRunState.NotConfigured;
                provider.lastStatus = "Not Configured";
            }
            else {
                provider.state = provider.canRunFromDashboard ? ProviderRunState.Pending : ProviderRunState.RequiresExplicitScannerRun;
                provider.lastStatus = provider.canRunFromDashboard ? "Not Scanned" : "Manual Scan Required";
            }
            provider.topFinding = provider.description;
        }
        private static ProviderRunState ResolveProviderStateFromResult(PungentScanResult result) {
            if (result == null)
                return ProviderRunState.Pending;
            if (!result.IsComplete)
                return ProviderRunState.Running;
            if (result.WasCanceled)
                return ProviderRunState.Cancelled;
            for (int i = 0; i < result.Issues.Count; i++) {
                PungentScanIssue issue = result.Issues[i];
                if (issue == null)
                    continue;
                if (string.Equals(issue.Code, "SCAN_EXCEPTION", StringComparison.OrdinalIgnoreCase) || Contains(issue.Message, "scan failed"))
                    return ProviderRunState.Failed;
                if (Contains(issue.Code, "NOT_CONFIGURED") || Contains(issue.Title, "Not configured") || Contains(issue.Message, "No profile assigned") || Contains(issue.Message, "No AudioCoverageProfileSO") || Contains(issue.Message, "No catalog asset"))
                    return ProviderRunState.NotConfigured;
            }
            return ProviderRunState.Complete;
        }
        private static string GetProviderStateLabel(ProviderRunState state) {
            switch (state) {
                case ProviderRunState.Queued:
                    return "Queued";
                case ProviderRunState.Complete:
                    return "Complete";
                case ProviderRunState.NotConfigured:
                    return "Not Configured";
                case ProviderRunState.RequiresExplicitScannerRun:
                    return "Manual Scan Required";
                case ProviderRunState.Failed:
                    return "Failed";
                case ProviderRunState.Paused:
                    return "Paused";
                case ProviderRunState.Cancelled:
                    return "Interrupted";
                case ProviderRunState.Cancelling:
                    return "Interrupting";
                case ProviderRunState.Skipped:
                    return "Skipped";
                case ProviderRunState.Running:
                    return "Running";
                default:
                    return "Not Scanned";
            }
        }
        private static PungentUtilityDesignAudit.Severity MapSeverity(PungentScanSeverity severity) {
            switch (severity) {
                case PungentScanSeverity.Error:
                    return PungentUtilityDesignAudit.Severity.Error;
                case PungentScanSeverity.Warning:
                    return PungentUtilityDesignAudit.Severity.Warning;
                default:
                    return PungentUtilityDesignAudit.Severity.Info;
            }
        }
        private static string BuildCachedRecommendation(ProjectAuditProvider provider, PungentScanIssue issue) {
            if (provider != null && string.Equals(provider.id, "terrain-usage-scanner", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(issue.Path))
                return "Open Terrain Usage Scanner to review scope, then decide whether this TerrainData asset should be reused, archived, or removed.";
            if (IsSceneIssueProvider(provider))
                return "Open Scene Issue Scanner or review this finding in Project Findings, then inspect the referenced scene/object before making manual changes.";
            return "Open " + (provider != null ? provider.displayName : "the related scanner") + " to review the cached finding in context.";
        }
        private PungentUtilityDesignAudit.Issue CreateProjectIssue(PungentUtilityDesignAudit.Severity severity, ProjectAuditProvider provider, string target, string message, string recommendation, string assetPath) {
            return new PungentUtilityDesignAudit.Issue(severity, "Project Audit / " + provider.displayName, target, message, recommendation, assetPath);
        }
        private void AddProjectIssue(ProjectAuditProvider provider, PungentUtilityDesignAudit.Issue issue) {
            AddProjectIssue(provider, issue, null);
        }
        private void AddProjectIssue(ProjectAuditProvider provider, PungentUtilityDesignAudit.Issue issue, PungentScanIssue scanIssue) {
            _projectIssues.Add(issue);
            string key = BuildIssueKey(issue);
            _providerIdByIssueKey[key] = provider.id;
            if (scanIssue != null && !string.IsNullOrWhiteSpace(scanIssue.Code))
                _issueCodeByIssueKey[key] = scanIssue.Code;
            provider.issueCount++;
            switch (issue.severity) {
                case PungentUtilityDesignAudit.Severity.Error:
                    provider.errorCount++;
                    break;
                case PungentUtilityDesignAudit.Severity.Warning:
                    provider.warningCount++;
                    break;
                default:
                    provider.infoCount++;
                    break;
            }
        }
        private void EnsureFilterCache() {
            if (!_filterCacheDirty && _cachedViewVersion == _viewVersion)
                return;
            _filteredIssues.Clear();
            _filteredIssueGroups.Clear();
            _noteByIssueKey.Clear();
            List<PungentUtilityDesignAudit.Issue> source = BuildVisibleIssueSource();
            PungentUtilityDesignAudit.Severity? severity = GetSeverityFilter();
            for (int i = 0; i < source.Count; i++) {
                PungentUtilityDesignAudit.Issue issue = source[i];
                if (severity.HasValue && issue.severity != severity.Value)
                    continue;
                if (!MatchesSearch(issue, _search))
                    continue;
                _filteredIssues.Add(issue);
            }
            _filteredIssues.Sort(CompareIssues);
            BuildFilteredGroups();
            for (int i = 0; i < _filteredIssues.Count; i++) {
                PungentUtilityDesignAudit.Issue issue = _filteredIssues[i];
                _noteByIssueKey[BuildIssueKey(issue)] = PungentNoteAuditIssueBridge.FindExisting(issue, true);
            }
            _cachedViewVersion = _viewVersion;
            _filterCacheDirty = false;
            PreserveSelectionAfterFilter();
        }
        private List<PungentUtilityDesignAudit.Issue> BuildVisibleIssueSource() {
            List<PungentUtilityDesignAudit.Issue> issues = new List<PungentUtilityDesignAudit.Issue>();
            AuditSource source = EffectiveSource();
            if (source == AuditSource.Project || source == AuditSource.All)
                issues.AddRange(_projectIssues.Where(IsEnabledProviderIssue));
            if (DeveloperToolsVisible && (source == AuditSource.Package || source == AuditSource.All) && _packageReport != null)
                issues.AddRange(_packageReport.issues);
            return issues;
        }
        private void BuildFilteredGroups() {
            SortedDictionary<string, IssueGroup> groups = new SortedDictionary<string, IssueGroup>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _filteredIssues.Count; i++) {
                PungentUtilityDesignAudit.Issue issue = _filteredIssues[i];
                string area = string.IsNullOrWhiteSpace(issue.area) ? "General" : issue.area;
                if (!groups.TryGetValue(area, out IssueGroup group)) {
                    string groupKey = BuildFindingGroupKey(area);
                    group = new IssueGroup(area, groupKey, UtilityWindowPrefs.GetBool(FindingGroupCollapsedPref(groupKey), false));
                    groups.Add(area, group);
                }
                group.Add(issue, string.Equals(BuildIssueKey(issue), _selectedIssueKey, StringComparison.Ordinal));
            }
            _filteredIssueGroups.AddRange(groups.Values);
        }
        private static string FindingGroupCollapsedPref(string groupKey) {
            return PrefCollapsedFindingGroupPrefix + (string.IsNullOrWhiteSpace(groupKey) ? "general" : groupKey);
        }
        private static string BuildFindingGroupKey(string area) {
            string source = string.IsNullOrWhiteSpace(area) ? "General" : area.Trim();
            StringBuilder builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++) {
                char c = char.ToLowerInvariant(source[i]);
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return builder.Length == 0 ? "general" : builder.ToString();
        }
        private void PreserveSelectionAfterFilter() {
            if (_filteredIssues.Count == 0)
                return;
            if (_filteredIssues.Any(issue => string.Equals(BuildIssueKey(issue), _selectedIssueKey, StringComparison.Ordinal)))
                return;
            _selectedIssueKey = BuildIssueKey(_filteredIssues[0]);
            UtilityWindowPrefs.SetString(PrefSelectedIssueKey, _selectedIssueKey);
            _detailScroll = Vector2.zero;
        }
        private void RunPackageAudit() {
            if (!DeveloperToolsVisible)
                return;
            string previousSelection = _selectedIssueKey;
            _packageReport = PungentUtilityDesignAudit.Run(_includeInfo);
            _assessment = PungentUtilityReleaseReadiness.Assess(_packageReport);
            _selectedIssueKey = previousSelection;
            MarkViewChanged(_packageReport.ErrorCount + " package errors - " + _packageReport.WarningCount + " package warnings - " + _packageReport.InfoCount + " package info");
            SelectTab(AuditViewTab.DeveloperPackage);
        }
        private void CopySummary() {
            AuditSource source = EffectiveSource();
            if (source == AuditSource.Package) {
                CopyPackageSummary();
                return;
            }
            if (source == AuditSource.All) {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(BuildProjectSummary());
                if (DeveloperToolsVisible && _packageReport != null) {
                    sb.AppendLine();
                    sb.AppendLine(_packageReport.ToMarkdownSummary());
                }
                EditorGUIUtility.systemCopyBuffer = sb.ToString();
                _status = "Copied mixed audit summary to clipboard.";
                return;
            }
            CopyProjectSummary();
        }
        private void CopyProjectSummary() {
            EditorGUIUtility.systemCopyBuffer = BuildProjectSummary();
            _status = "Copied project audit summary to clipboard.";
        }
        private void CopyPackageSummary() {
            if (_packageReport == null)
                RunPackageAudit();
            EditorGUIUtility.systemCopyBuffer = _packageReport != null ? _packageReport.ToMarkdownSummary() : string.Empty;
            _status = "Copied package audit summary to clipboard.";
        }
        private void CopyNextPassBrief() {
            if (!DeveloperToolsVisible)
                return;
            if (_packageReport == null)
                RunPackageAudit();
            EditorGUIUtility.systemCopyBuffer = PungentUtilityReleaseReadiness.BuildNextPassBrief(_packageReport);
            _status = "Copied package next-pass brief to clipboard.";
        }
        private void CopyUpdatePassBrief(PungentUtilityReleaseReadiness.UpdatePass pass) {
            var sb = new StringBuilder();
            sb.AppendLine("Update the PungentFunk Utilities Unity package.");
            sb.AppendLine();
            sb.AppendLine("Recommended pass: " + pass.title);
            sb.AppendLine("Priority: " + PungentUtilityReleaseReadiness.GetPriorityLabel(pass.priority));
            sb.AppendLine("Summary: " + pass.summary);
            if (pass.fileTargets.Length > 0) {
                sb.AppendLine();
                sb.AppendLine("Primary files:");
                for (int i = 0; i < pass.fileTargets.Length; i++)
                    sb.AppendLine("- " + pass.fileTargets[i]);
            }
            if (pass.gates.Length > 0) {
                sb.AppendLine();
                sb.AppendLine("Validation gates:");
                for (int i = 0; i < pass.gates.Length; i++)
                    sb.AppendLine("- " + pass.gates[i]);
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _status = "Copied update-pass brief to clipboard.";
        }
        private void CopyIssue(PungentUtilityDesignAudit.Issue issue) {
            EditorGUIUtility.systemCopyBuffer = BuildIssueCopy(issue);
            _status = "Copied selected issue to clipboard.";
        }
        private void CopyFixBrief(PungentUtilityDesignAudit.Issue issue) {
            EditorGUIUtility.systemCopyBuffer = BuildFixBrief(issue);
            _status = "Copied issue-specific fix brief to clipboard.";
        }
        private void CopyProviderError(ProjectAuditProvider provider) {
            if (provider == null)
                return;
            EditorGUIUtility.systemCopyBuffer = provider.displayName + "\n" + provider.lastStatus + "\n" + (provider.cachedResult != null ? provider.cachedResult.StatusMessage : string.Empty);
            _status = "Copied " + provider.displayName + " status to clipboard.";
        }
        private void CopyProviderSummary(ProjectAuditProvider provider) {
            if (provider == null)
                return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(provider.displayName);
            sb.AppendLine("Status: " + provider.lastStatus);
            sb.AppendLine("Freshness: " + PungentScanSnapshotStore.GetFreshnessLabel(provider.id));
            sb.AppendLine("Last scanned: " + (provider.cachedResult != null && provider.cachedResult.IsComplete ? PungentScanFindingActions.FormatAge(provider.cachedResult.CompletedAtUtc) : PungentScanSnapshotStore.GetLastScanAgeLabel(provider.id)));
            sb.AppendLine("Summary: " + GetProviderSummary(provider));
            if (provider.cachedResult != null) {
                sb.AppendLine("Scope: " + provider.cachedResult.ScopeLabel);
                sb.AppendLine("Scanned: " + provider.cachedResult.TotalScanned);
                sb.AppendLine("Matched: " + provider.cachedResult.TotalMatched);
                sb.AppendLine("Skipped: " + provider.cachedResult.TotalSkipped);
                sb.AppendLine("Errors: " + provider.errorCount + ", Warnings: " + provider.warningCount + ", Info: " + provider.infoCount);
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _status = "Copied " + provider.displayName + " summary to clipboard.";
        }
        private void CopyProviderFixBrief(ProjectAuditProvider provider) {
            if (provider == null)
                return;
            PungentUtilityDesignAudit.Issue topIssue = _projectIssues
                .Where(issue => string.Equals(GetProviderForIssue(issue)?.id, provider.id, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(issue => issue.severity)
                .FirstOrDefault();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Review this PungentFunk project audit provider finding.");
            sb.AppendLine();
            sb.AppendLine("Provider: " + provider.displayName);
            sb.AppendLine("Status: " + provider.lastStatus);
            sb.AppendLine("This brief is non-destructive: open the owning scanner, inspect findings, and do not apply fixes from Design Validation Audit.");
            if (topIssue != null) {
                sb.AppendLine();
                sb.AppendLine(BuildIssueCopy(topIssue));
            }
            else {
                sb.AppendLine("No warnings or errors are currently cached for this provider.");
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _status = "Copied " + provider.displayName + " fix brief to clipboard.";
        }
        private void CreateAuditSummaryNote() {
            try {
                string title = EffectiveSource() == AuditSource.Package ? "Package Design Audit Note" : "Project Audit Note";
                string body = EffectiveSource() == AuditSource.Package ? PungentNoteAuditIssueBridge.BuildAuditSummaryBody(_packageReport) : BuildProjectSummary();
                PungentNote note = PungentNotesRoadmapWindow.CreateAuditFollowUp(
                    "DESIGN_AUDIT_SUMMARY_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                    title,
                    body);
                note.tags = new List<string> { "audit", "design-validation", EffectiveSource() == AuditSource.Package ? "package" : "project" };
                PungentNoteStorage.Save();
                PungentNotesRoadmapWindow.OpenAndSelect(note.id);
                _status = "Created audit note.";
            }
            catch (Exception ex) {
                _status = "Could not create audit note: " + ex.Message;
            }
        }
        private void SelectIssue(string key) {
            if (string.Equals(_selectedIssueKey, key, StringComparison.Ordinal))
                return;
            _selectedIssueKey = key ?? string.Empty;
            _detailScroll = Vector2.zero;
            UtilityWindowPrefs.SetString(PrefSelectedIssueKey, _selectedIssueKey);
            Repaint();
        }
        private void SelectIssueAndShowFindings(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return;
            PungentUtilityDesignAudit.Severity? severity = GetSeverityFilter();
            if (severity.HasValue && issue.severity != severity.Value) {
                _severityIndex = 0;
                UtilityWindowPrefs.SetInt(PrefSeverity, _severityIndex);
            }
            if (!MatchesSearch(issue, _search)) {
                _search = string.Empty;
                UtilityWindowPrefs.SetString(PrefSearch, _search);
            }
            SelectIssue(BuildIssueKey(issue));
            SelectTab(AuditViewTab.Findings);
        }
        private void FocusCachedIssue(string providerId, string issueCode, string issueTitle, string issuePath, string issueMessage) {
            RefreshCachedResults(false);
            PungentUtilityDesignAudit.Issue match = _projectIssues.FirstOrDefault(issue => MatchesCachedIssue(issue, providerId, issueCode, issueTitle, issuePath, issueMessage));
            if (match == null) {
                _status = "Opened Design Validation Audit. The linked cached issue was not found in the current audit cache.";
                SelectTab(AuditViewTab.Findings);
                Repaint();
                return;
            }

            SelectIssueAndShowFindings(match);
            _status = "Focused linked audit issue.";
        }
        private bool MatchesCachedIssue(PungentUtilityDesignAudit.Issue issue, string providerId, string issueCode, string issueTitle, string issuePath, string issueMessage) {
            if (issue == null)
                return false;

            string key = BuildIssueKey(issue);
            if (!string.IsNullOrWhiteSpace(providerId) &&
                (!_providerIdByIssueKey.TryGetValue(key, out string cachedProviderId) ||
                 !string.Equals(cachedProviderId, providerId, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (!string.IsNullOrWhiteSpace(issueCode) &&
                _issueCodeByIssueKey.TryGetValue(key, out string cachedCode) &&
                !string.Equals(cachedCode, issueCode, StringComparison.OrdinalIgnoreCase))
                return false;

            bool pathMatches = string.IsNullOrWhiteSpace(issuePath) || string.Equals(issue.assetPath ?? string.Empty, issuePath, StringComparison.OrdinalIgnoreCase);
            bool messageMatches = string.IsNullOrWhiteSpace(issueMessage) || string.Equals(issue.message ?? string.Empty, issueMessage, StringComparison.Ordinal);
            bool titleMatches = string.IsNullOrWhiteSpace(issueTitle) || string.Equals(issue.target ?? string.Empty, issueTitle, StringComparison.OrdinalIgnoreCase);
            return pathMatches && messageMatches && titleMatches;
        }
        private void RunProviderFromDashboard(ProjectAuditProvider provider) {
            if (provider == null || !provider.available || !provider.canRunFromDashboard)
                return;
            if (!CanProviderRunInMode(provider, PungentAuditScanMode.Immediate)) {
                _status = provider.displayName + " cannot run in Immediate mode.";
                return;
            }
            if (AnyScanActive) {
                _status = "Project audit is already running. Pause or restart it before starting a separate provider scan.";
                return;
            }
            if (_pendingRestartAudit) {
                _status = "Restart is preparing a fresh project audit. Wait for cleanup to finish before starting a provider scan.";
                return;
            }
            bool notConfigured = TryGetProviderNotConfiguredReason(provider, out string reason);
            ProjectAuditProvider[] runnableForDialog = notConfigured ? Array.Empty<ProjectAuditProvider>() : new[] { provider };
            ProviderSkipInfo[] skipped = notConfigured
                ? new[] { new ProviderSkipInfo(provider.displayName, reason) }
                : Array.Empty<ProviderSkipInfo>();
            if (!ConfirmRunEnabledAudits(runnableForDialog, Array.Empty<ProjectAuditProvider>(), skipped)) {
                _status = provider.displayName + " scan was not started.";
                return;
            }
            StartRunnerBatch(new[] { provider }, PungentAuditScanMode.Immediate);
        }
        private PungentUtilityDesignAudit.Issue GetSelectedIssue() {
            EnsureFilterCache();
            return _filteredIssues.FirstOrDefault(issue => string.Equals(BuildIssueKey(issue), _selectedIssueKey, StringComparison.Ordinal));
        }
        private PungentNote GetCachedNote(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return null;
            _noteByIssueKey.TryGetValue(BuildIssueKey(issue), out PungentNote note);
            return note;
        }
        private ProjectAuditProvider GetProviderForIssue(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return null;
            if (!_providerIdByIssueKey.TryGetValue(BuildIssueKey(issue), out string providerId))
                return null;
            return _providers.FirstOrDefault(provider => string.Equals(provider.id, providerId, StringComparison.OrdinalIgnoreCase));
        }
        private PungentUtilityDesignAudit.Severity? GetSeverityFilter() {
            switch (_severityIndex) {
                case 1: return PungentUtilityDesignAudit.Severity.Error;
                case 2: return PungentUtilityDesignAudit.Severity.Warning;
                case 3: return PungentUtilityDesignAudit.Severity.Info;
                default: return null;
            }
        }
        private void MarkFilterDirty() {
            _filterCacheDirty = true;
            Repaint();
        }
        private void MarkViewChanged(string status) {
            _status = status ?? _status;
            _viewVersion++;
            _filterCacheDirty = true;
            Repaint();
        }
        private void SavePrefs() {
            UtilityWindowPrefs.SetBool(PrefIncludeInfo, _includeInfo);
            UtilityWindowPrefs.SetBool(PrefGroupByArea, _groupByArea);
            UtilityWindowPrefs.SetInt(PrefSelectedTab, (int)_selectedTab);
            UtilityWindowPrefs.SetInt(PrefSeverity, _severityIndex);
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefSelectedIssueKey, _selectedIssueKey);
            UtilityWindowPrefs.SetFloat(PrefIssueListWidth, _issueListWidth);
            UtilityWindowPrefs.SetFloat(PrefFindingsStackedListHeight, _findingsStackedListHeight);
            UtilityWindowPrefs.SetFloat(PrefListScrollY, _issueListScroll.y);
            UtilityWindowPrefs.SetFloat(PrefDetailScrollY, _detailScroll.y);
            UtilityWindowPrefs.SetFloat(PrefProjectScrollY, _projectAuditScroll.y);
            UtilityWindowPrefs.SetFloat(PrefPackageScrollY, _packageScroll.y);
            UtilityWindowPrefs.SetBool(PrefShowDeveloperTools, _showDeveloperTools);
            for (int i = 0; i < _providers.Count; i++)
                UtilityWindowPrefs.SetBool(ProviderEnabledPref(_providers[i].id), _providers[i].enabled);
        }
        private void SetBoolFromToggle(ref bool current, bool next, string prefKey, bool filterAffects) {
            if (next == current)
                return;
            current = next;
            UtilityWindowPrefs.SetBool(prefKey, current);
            if (prefKey == PrefIncludeInfo)
                RunPackageAudit();
            else if (filterAffects)
                MarkFilterDirty();
        }
        private void RefreshDeveloperModeState() {
            if (!_developerModeAvailable) {
                _developerModeEnabled = false;
                return;
            }
            bool enabled = PungentUtilityDesignAudit.TryGetDeveloperModeEnabled(out bool value) && value;
            if (enabled == _developerModeEnabled)
                return;
            _developerModeEnabled = enabled;
            if (!DeveloperToolsVisible) {
                if (_selectedTab == AuditViewTab.DeveloperPackage)
                    SelectTab(AuditViewTab.ProjectAudit);
            }
            MarkFilterDirty();
        }
        private AuditSource EffectiveSource() {
            if (!DeveloperToolsVisible)
                return AuditSource.Project;
            if (_selectedTab == AuditViewTab.DeveloperPackage)
                return AuditSource.Package;
            if (_selectedTab == AuditViewTab.Findings)
                return AuditSource.All;
            return AuditSource.Project;
        }
        private bool IsProjectIssue(PungentUtilityDesignAudit.Issue issue) {
            return issue != null && !string.IsNullOrWhiteSpace(issue.area) && issue.area.StartsWith("Project Audit /", StringComparison.Ordinal);
        }
        private bool IsEnabledProviderIssue(PungentUtilityDesignAudit.Issue issue) {
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            return provider == null || (provider.available && (provider.enabled || ShouldExposeManualProviderCache(provider)));
        }
        private bool MatchesSearch(PungentUtilityDesignAudit.Issue issue, string search) {
            if (issue == null)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;
            string q = search.Trim();
            return Contains(issue.area, q) || Contains(issue.target, q) || Contains(issue.message, q) || Contains(issue.recommendation, q) || Contains(issue.assetPath, q);
        }
        private static int CompareIssues(PungentUtilityDesignAudit.Issue a, PungentUtilityDesignAudit.Issue b) {
            int severity = b.severity.CompareTo(a.severity);
            if (severity != 0)
                return severity;
            int area = string.Compare(a.area, b.area, StringComparison.OrdinalIgnoreCase);
            if (area != 0)
                return area;
            int target = string.Compare(a.target, b.target, StringComparison.OrdinalIgnoreCase);
            if (target != 0)
                return target;
            return string.Compare(a.message, b.message, StringComparison.OrdinalIgnoreCase);
        }
        private int AvailableProviderCount() {
            return _providers.Count(provider => provider.available);
        }
        private int UnavailableProviderCount() {
            return _providers.Count(provider => !provider.available);
        }
        private int EnabledAvailableProviderCount() {
            return _providers.Count(provider => provider.available && provider.enabled);
        }
        private int RunnableProviderCount() {
            return _providers.Count(provider => provider.available && provider.enabled && provider.canRunFromDashboard);
        }
        private int ProjectErrorCount() {
            return _projectIssues.Count(issue => IsEnabledProviderIssue(issue) && issue.severity == PungentUtilityDesignAudit.Severity.Error);
        }
        private int ProjectWarningCount() {
            return _projectIssues.Count(issue => IsEnabledProviderIssue(issue) && issue.severity == PungentUtilityDesignAudit.Severity.Warning);
        }
        private int ProjectInfoCount() {
            return _projectIssues.Count(issue => IsEnabledProviderIssue(issue) && issue.severity == PungentUtilityDesignAudit.Severity.Info);
        }
        private string LastProjectAuditLabel() {
            return _lastProjectAuditUtc == default(DateTime) ? "Not run yet" : _lastProjectAuditUtc.ToString("u");
        }
        private string LastProjectAuditShortLabel() {
            return _lastProjectAuditUtc == default(DateTime) ? "not run" : _lastProjectAuditUtc.ToLocalTime().ToShortTimeString();
        }
        private string PackageLastRunLabel() {
            return _packageReport == null ? "Package audit not run" : "Package last run " + _packageReport.generatedUtc.ToLocalTime().ToShortTimeString();
        }
        private ProjectAuditProvider FindProvider(string id) {
            return _providers.FirstOrDefault(provider => string.Equals(provider.id, id, StringComparison.OrdinalIgnoreCase));
        }
        private PungentAuditScanRunner GetRunnerForJob(PungentAuditScanJob job) {
            if (job == null)
                return VisibleScanRunner;
            PungentAuditScanJob schedulerJob = PungentAuditIdleScanScheduler.Runner.FindJob(job.providerId);
            return ReferenceEquals(schedulerJob, job) ? PungentAuditIdleScanScheduler.Runner : _scanRunner;
        }
        private void SetAllProvidersEnabled(bool enabled) {
            for (int i = 0; i < _providers.Count; i++) {
                ProjectAuditProvider provider = _providers[i];
                if (!provider.available)
                    continue;
                provider.enabled = enabled;
                UtilityWindowPrefs.SetBool(ProviderEnabledPref(provider.id), provider.enabled);
            }
            MarkFilterDirty();
        }
        private void RefreshProviderCachedResult(ProjectAuditProvider provider) {
            if (provider == null || !provider.available)
                return;
            _projectIssues.RemoveAll(issue => string.Equals(GetProviderIdForIssue(issue), provider.id, StringComparison.OrdinalIgnoreCase));
            RemoveProviderIssueKeys(provider.id);
            provider.issueCount = 0;
            provider.warningCount = 0;
            provider.errorCount = 0;
            provider.infoCount = 0;
            provider.successCount = 0;
            provider.cachedResult = null;
            provider.topFinding = string.Empty;
            if (PungentScanCache.TryGet(provider.id, out PungentScanResult result) && result != null)
                IngestCachedResult(provider, result);
            else
                AddNoCacheFinding(provider);
            MarkViewChanged("Refreshed cached result for " + provider.displayName + ".");
        }
        private void RefreshProviderCachedResultAfterRun(ProjectAuditProvider provider) {
            if (provider == null || !provider.available)
                return;
            _projectIssues.RemoveAll(issue => string.Equals(GetProviderIdForIssue(issue), provider.id, StringComparison.OrdinalIgnoreCase));
            RemoveProviderIssueKeys(provider.id);
            provider.issueCount = 0;
            provider.warningCount = 0;
            provider.errorCount = 0;
            provider.infoCount = 0;
            provider.successCount = 0;
            provider.cachedResult = null;
            provider.topFinding = string.Empty;
            if (PungentScanCache.TryGet(provider.id, out PungentScanResult result) && result != null)
                IngestCachedResult(provider, result);
            else
                AddNoCacheFinding(provider);
            MarkViewChanged("Ran " + provider.displayName + " and refreshed cached result.");
        }
        private void RemoveProviderIssueKeys(string providerId) {
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, string> pair in _providerIdByIssueKey) {
                if (string.Equals(pair.Value, providerId, StringComparison.OrdinalIgnoreCase))
                    remove.Add(pair.Key);
            }
            for (int i = 0; i < remove.Count; i++)
                _providerIdByIssueKey.Remove(remove[i]);
        }
        private string GetProviderIdForIssue(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return string.Empty;
            return _providerIdByIssueKey.TryGetValue(BuildIssueKey(issue), out string id) ? id : string.Empty;
        }
        private void SelectFirstProviderFinding(string providerId) {
            PungentUtilityDesignAudit.Issue issue = _projectIssues
                .Where(item => string.Equals(GetProviderIdForIssue(item), providerId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.severity)
                .FirstOrDefault();
            if (issue != null)
                SelectIssueAndShowFindings(issue);
        }
        private void SelectFirstProjectFinding() {
            PungentUtilityDesignAudit.Issue issue = _projectIssues
                .Where(IsEnabledProviderIssue)
                .OrderByDescending(item => item.severity)
                .ThenBy(item => item.area)
                .FirstOrDefault();
            if (issue != null)
                SelectIssueAndShowFindings(issue);
        }
        private void SelectFirstPackageFinding() {
            if (_packageReport == null || _packageReport.issues == null || _packageReport.issues.Count == 0)
                return;
            SelectIssueAndShowFindings(_packageReport.issues.OrderByDescending(issue => issue.severity).First());
        }
        private string GetProviderSummary(ProjectAuditProvider provider) {
            if (provider == null)
                return string.Empty;
            if (!provider.available)
                return provider.description + " This utility is not registered or cannot be opened in the current package.";
            if (provider.state == ProviderRunState.Queued || provider.state == ProviderRunState.Running || provider.state == ProviderRunState.Paused || provider.state == ProviderRunState.Cancelling)
                return provider.description + " " + provider.lastStatus;
            if (provider.state == ProviderRunState.Cancelled)
                return "Interrupted. Previous cached result remains available when present.";
            if (provider.state == ProviderRunState.Skipped)
                return "Skipped. Previous cached result remains available when present.";
            if (provider.cachedResult != null) {
                if (provider.state == ProviderRunState.NotConfigured)
                    return "Not configured. " + (string.IsNullOrWhiteSpace(provider.cachedResult.StatusMessage) ? provider.description : provider.cachedResult.StatusMessage) + " Open the scanner to assign the required setup.";
                if (string.Equals(provider.id, "terrain-usage-scanner", StringComparison.OrdinalIgnoreCase))
                    return provider.cachedResult.TotalSkipped + " unused TerrainData assets found. Top: " + (string.IsNullOrWhiteSpace(provider.topFinding) ? provider.cachedResult.StatusMessage : provider.topFinding);
                if (IsSceneIssueProvider(provider))
                    return BuildSceneIssueProviderSummary(provider);
                return (string.IsNullOrWhiteSpace(provider.cachedResult.StatusMessage) ? "Cached scanner result is available." : provider.cachedResult.StatusMessage)
                    + " Top: " + (string.IsNullOrWhiteSpace(provider.topFinding) ? "No warnings or errors in the cached result." : provider.topFinding);
            }
            if (!string.IsNullOrWhiteSpace(provider.notConfiguredReason))
                return "Not configured. " + provider.notConfiguredReason + " Open the scanner to assign the required setup.";
            if (provider.canRunFromDashboard)
                return "Not scanned yet. " + provider.description;
            return "Manual scan required. " + provider.description + " This scanner does not yet expose a coordinator-safe run hook.";
        }
        private static string GetProviderMetricLine(ProjectAuditProvider provider) {
            if (provider == null || provider.cachedResult == null)
                return string.Empty;
            if (string.Equals(provider.id, "terrain-usage-scanner", StringComparison.OrdinalIgnoreCase))
                return "Scenes/assets scanned: " + provider.cachedResult.TotalScanned + " | Used TerrainData: " + provider.cachedResult.TotalMatched + " | Unused TerrainData: " + provider.cachedResult.TotalSkipped;
            if (IsSceneIssueProvider(provider))
                return "Scenes scanned: " + provider.cachedResult.TotalScanned + " | Issues: " + provider.cachedResult.TotalMatched + " | Skipped: " + provider.cachedResult.TotalSkipped + " | Scope: " + provider.cachedResult.ScopeLabel;
            return "Scanned: " + provider.cachedResult.TotalScanned + " | Matched: " + provider.cachedResult.TotalMatched + " | Skipped: " + provider.cachedResult.TotalSkipped + " | Scope: " + provider.cachedResult.ScopeLabel;
        }
        private int CachedProviderCount() {
            return _providers.Count(provider => provider.enabled && provider.available && provider.cachedResult != null);
        }
        private string BuildProjectScopeStatus() {
            int runnable = _providers.Count(provider => provider.enabled && provider.available && provider.canRunFromDashboard);
            int manual = ManualProviderCount();
            int cached = CachedProviderCount();
            return "Runnable here: " + runnable + "\nCache refreshed: " + cached + "\nManual-required: " + manual + "\nTerrain prompts before scanning; refresh reads cache only.";
        }
        private int CountPackageIssues(string areaContains, PungentUtilityDesignAudit.Severity? severity) {
            if (_packageReport == null || _packageReport.issues == null)
                return 0;
            return _packageReport.issues.Count(issue =>
                (string.IsNullOrWhiteSpace(areaContains) || Contains(issue.area, areaContains)) &&
                (!severity.HasValue || issue.severity == severity.Value));
        }
        private int ProvidersNeedingFirstScanCount() {
            return _providers.Count(provider => provider.enabled && provider.available && provider.cachedResult == null);
        }
        private int ManualProviderCount() {
            return _providers.Count(provider => provider.enabled && provider.available && !provider.canRunFromDashboard);
        }
        private void OpenProvider(string id) {
            if (string.IsNullOrWhiteSpace(id))
                return;
            if (PungentUtilityRegistry.Open(id)) {
                ProjectAuditProvider provider = FindProvider(id);
                _status = "Opened " + (provider != null ? provider.displayName : id) + ".";
                return;
            }
            ProjectAuditProvider fallbackProvider = FindProvider(id);
            if (fallbackProvider != null && fallbackProvider.adapter != null) {
                fallbackProvider.adapter.OpenWindow();
                _status = "Opened " + fallbackProvider.displayName + ".";
                return;
            }
            _status = "Could not open project audit tool.";
        }
        private string GetBrowserTitle() {
            switch (EffectiveSource()) {
                case AuditSource.Package:
                    return "Package Findings";
                case AuditSource.All:
                    return "All Findings";
                default:
                    return "Project Findings";
            }
        }
        private string GetDetailTitle() {
            switch (EffectiveSource()) {
                case AuditSource.Package:
                    return "Package Issue Detail";
                case AuditSource.All:
                    return "Issue Detail";
                default:
                    return "Project Issue Detail";
            }
        }
        private string GetFindingsEmptyState() {
            if (BuildVisibleIssueSource().Count > 0)
                return "No findings match the current search/filter. Clear search or broaden severity filters.";
            if (EffectiveSource() == AuditSource.Package)
                return DeveloperToolsVisible ? "No package-development findings match the current filters." : "Package-development findings are available in Developer Mode.";
            return "No findings yet. Run Enabled Audits or Refresh Results from the Project Audit tab to populate findings from included audit tools.";
        }
        private string GetFindingsEmptyActionLabel() {
            return EffectiveSource() == AuditSource.Package ? "Run Package Audit" : "Run Enabled Audits";
        }
        private Action GetFindingsEmptyAction() {
            return EffectiveSource() == AuditSource.Package ? (Action)RunPackageAudit : RunProjectAudit;
        }
        private string BuildProjectSummary() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# PungentFunk Project Audit");
            sb.AppendLine();
            sb.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("u"));
            sb.AppendLine("Last project audit run: " + LastProjectAuditLabel());
            sb.AppendLine("Tools available: " + AvailableProviderCount());
            sb.AppendLine("Tools enabled: " + EnabledAvailableProviderCount());
            sb.AppendLine("Project issues: " + ProjectErrorCount() + " errors, " + ProjectWarningCount() + " warnings, " + ProjectInfoCount() + " info");
            sb.AppendLine();
            sb.AppendLine("Providers:");
            for (int i = 0; i < _providers.Count; i++) {
                ProjectAuditProvider provider = _providers[i];
                sb.AppendLine("- " + provider.displayName + ": " + (provider.available ? provider.lastStatus : "Unavailable") + (provider.enabled ? "" : " (disabled)"));
            }
            sb.AppendLine();
            sb.AppendLine("Findings:");
            List<PungentUtilityDesignAudit.Issue> issues = _projectIssues.Where(IsEnabledProviderIssue).OrderByDescending(issue => issue.severity).ThenBy(issue => issue.area).ToList();
            if (issues.Count == 0)
                sb.AppendLine("- No project audit findings yet. Run Enabled Audits, Refresh Results, or open a companion scanner.");
            for (int i = 0; i < issues.Count; i++) {
                PungentUtilityDesignAudit.Issue issue = issues[i];
                sb.AppendLine("- **" + issue.severity + "** - " + issue.area + " - " + issue.target + ": " + issue.message);
                if (!string.IsNullOrWhiteSpace(issue.recommendation))
                    sb.AppendLine("  - Recommendation: " + issue.recommendation);
            }
            return sb.ToString();
        }
        private static void DrawEmptyState(string message, string actionLabel, Action action) {
            EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
            if (!string.IsNullOrWhiteSpace(actionLabel) && action != null) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(actionLabel, GUILayout.Width(Mathf.Clamp(52f + actionLabel.Length * 6f, 92f, 190f))))
                        action();
                    GUILayout.FlexibleSpace();
                }
            }
        }
        private PungentScanFindingDigest GetProviderDigest(ProjectAuditProvider provider) {
            if (provider == null)
                return null;
            PungentProjectAuditIndexEntry index = PungentProjectAuditIndex.Get(provider.id);
            string cacheKey = string.Join("|", new[] {
                provider.id ?? string.Empty,
                provider.cachedResult != null ? provider.cachedResult.CompletedAtUtc.Ticks.ToString() : "0",
                provider.state.ToString(),
                provider.issueCount.ToString(),
                provider.errorCount.ToString(),
                provider.warningCount.ToString(),
                provider.infoCount.ToString(),
                provider.notConfiguredReason ?? string.Empty,
                index != null && index.Stale ? "stale" : "fresh",
                index != null && index.HasResult ? "has-result" : "no-result"
            });
            if (provider.cachedDigest != null && string.Equals(provider.cachedDigestKey, cacheKey, StringComparison.Ordinal))
                return provider.cachedDigest;

            PungentScanFindingDigest digest = PungentScanFindingDigestBuilder.Build(provider.id, provider.displayName, provider.cachedResult, index);
            if (!string.IsNullOrWhiteSpace(provider.notConfiguredReason) && provider.cachedResult == null) {
                digest.headline = "Not configured.";
                digest.impact = provider.notConfiguredReason;
                digest.recommendedAction = "Open " + provider.displayName + " and assign the required configuration.";
                digest.dominantSeverity = PungentScanSeverity.Warning;
                digest.isNotConfigured = true;
                digest.issueCount = Math.Max(1, digest.issueCount);
            }
            else if (provider.cachedResult == null) {
                digest.headline = provider.canRunFromDashboard ? "No scan result yet." : "Manual scan required.";
                digest.impact = provider.description;
                digest.recommendedAction = provider.canRunFromDashboard ? "Run this provider or refresh cache." : "Open the scanner and run its native scan.";
            }
            provider.cachedDigest = digest;
            provider.cachedDigestKey = cacheKey;
            return digest;
        }
        private string GetProviderPrimaryStatusLabel(ProjectAuditProvider provider) {
            if (provider == null)
                return "Unknown";
            if (!provider.available)
                return "Unavailable";
            if (!provider.enabled && ShouldExposeManualProviderCache(provider))
                return "Manual Cache";
            if (!provider.enabled)
                return "Excluded";
            return GetProviderStateLabel(provider.state);
        }
        private string GetProviderFreshnessLabel(ProjectAuditProvider provider) {
            if (provider == null)
                return "No cache";
            return PungentScanSnapshotStore.GetFreshnessLabel(provider.id);
        }
        private Color GetProviderFreshnessTint(ProjectAuditProvider provider) {
            string label = GetProviderFreshnessLabel(provider);
            return label == "Fresh" ? UtilityWindowTheme.Green : label == "Stale" ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;
        }
        private static string BuildProviderCapabilityLine(ProjectAuditProvider provider) {
            if (provider == null || provider.adapter == null)
                return "No adapter metadata.";
            List<string> flags = new List<string>();
            if (provider.adapter.CanRunImmediate)
                flags.Add("Immediate");
            if (provider.adapter.CanRunBackground)
                flags.Add("Background");
            if (provider.adapter.UsesSceneOpening)
                flags.Add("Opens scenes");
            if (provider.adapter.UsesAssetDatabase)
                flags.Add("AssetDatabase");
            if (provider.adapter.UsesModalProgress)
                flags.Add("Modal progress");
            if (provider.adapter.IsScanOnly)
                flags.Add("Scan-only");
            if (!provider.adapter.CanRunFromCoordinator)
                flags.Add("Manual-only");
            return flags.Count == 0 ? "No flags declared." : string.Join(", ", flags);
        }
        private static bool IsSceneIssueProvider(ProjectAuditProvider provider) {
            return provider != null && string.Equals(provider.id, "scene-issue-scanner", StringComparison.OrdinalIgnoreCase);
        }
        private void DrawSceneIssueProviderContextLine(ProjectAuditProvider provider) {
            if (!IsSceneIssueProvider(provider))
                return;
            string scope = provider.cachedResult != null && !string.IsNullOrWhiteSpace(provider.cachedResult.ScopeLabel)
                ? provider.cachedResult.ScopeLabel
                : "Shared scene pass / open scenes";
            DrawWrappedLabel("Scope: " + scope + ". Scene Issue findings are cached under this provider and can be reviewed here or in the scanner window.", UtilityWindowTheme.MutedMiniLabelStyle);
        }
        private static string BuildSceneIssueProviderSummary(ProjectAuditProvider provider) {
            if (provider == null || provider.cachedResult == null)
                return "Scene Issue Scanner has no cached result yet.";
            string top = string.IsNullOrWhiteSpace(provider.topFinding)
                ? "No warnings or errors in the cached result."
                : provider.topFinding;
            return (string.IsNullOrWhiteSpace(provider.cachedResult.StatusMessage) ? "Cached Scene Issue result is available." : provider.cachedResult.StatusMessage)
                + " Scope: " + provider.cachedResult.ScopeLabel
                + ". Top: " + top;
        }
        private static bool ShouldExposeManualProviderCache(ProjectAuditProvider provider) {
            return provider != null && provider.available && !provider.canRunFromDashboard && provider.cachedResult != null;
        }
        private string BuildIssueMetadataLine(PungentUtilityDesignAudit.Issue issue, ProjectAuditProvider provider, bool isProject, PungentNote note) {
            List<string> parts = new List<string>();
            parts.Add(provider != null ? provider.displayName : (string.IsNullOrWhiteSpace(issue.area) ? "General" : issue.area));
            parts.Add(GetIssueTypeLabel(issue));
            parts.Add(isProject ? "Project" : "Package");
            if (provider != null)
                parts.Add(GetIssueFreshnessLabel(provider));
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                parts.Add("asset context");
            if (note != null)
                parts.Add(note.archived ? "archived note" : "note linked");
            return string.Join(" | ", parts);
        }
        private void OpenOrCreateIssueNote(PungentUtilityDesignAudit.Issue issue, PungentNote note) {
            if (issue == null)
                return;
            if (note == null) {
                PungentNote created = PungentNoteAuditIssueBridge.CreateOrOpen(issue);
                _noteByIssueKey[BuildIssueKey(issue)] = created;
                PungentNotesRoadmapWindow.OpenAndSelect(created.id);
                _status = "Created audit issue note.";
            }
            else {
                PungentNotesRoadmapWindow.OpenAndSelect(note.id);
                _status = "Opened linked audit issue note.";
            }
        }
        private static void DrawCountPillGrid(IReadOnlyList<PillSpec> pills) {
            DrawCountPillGrid(pills, Mathf.Max(240f, EditorGUIUtility.currentViewWidth - 38f));
        }
        private static void DrawCountPillGrid(IReadOnlyList<PillSpec> pills, float rowWidth) {
            rowWidth = Mathf.Max(120f, rowWidth);
            float used = 0f;
            bool rowOpen = false;
            void BeginRow() {
                EditorGUILayout.BeginHorizontal();
                rowOpen = true;
            }
            void EndRow() {
                if (!rowOpen)
                    return;
                try {
                    GUILayout.FlexibleSpace();
                }
                finally {
                    EditorGUILayout.EndHorizontal();
                    rowOpen = false;
                }
            }

            BeginRow();
            try {
                for (int i = 0; i < pills.Count; i++) {
                    PillSpec pill = pills[i];
                    if (used > 0f && used + pill.width > rowWidth) {
                        EndRow();
                        BeginRow();
                        used = 0f;
                    }
                    UtilityWindowTheme.CountPill(pill.label, pill.tint, pill.width);
                    used += pill.width + 4f;
                }
            }
            finally {
                EndRow();
            }
        }
        private static void DrawWrappedActions(IReadOnlyList<ActionSpec> actions, float rowWidth) {
            rowWidth = Mathf.Max(180f, rowWidth);
            float used = 0f;
            bool rowOpen = false;
            void BeginRow() {
                EditorGUILayout.BeginHorizontal();
                rowOpen = true;
            }
            void EndRow() {
                if (!rowOpen)
                    return;
                try {
                    GUILayout.FlexibleSpace();
                }
                finally {
                    EditorGUILayout.EndHorizontal();
                    rowOpen = false;
                }
            }

            BeginRow();
            try {
                for (int i = 0; i < actions.Count; i++) {
                    ActionSpec action = actions[i];
                    float width = Mathf.Clamp(action.width, 56f, rowWidth);
                    if (used > 0f && used + width > rowWidth) {
                        EndRow();
                        BeginRow();
                        used = 0f;
                    }
                    using (new EditorGUI.DisabledScope(!action.enabled)) {
                        bool clicked = action.tint == UtilityWindowTheme.Neutral
                            ? GUILayout.Button(new GUIContent(action.label, action.tooltip), EditorStyles.miniButton, GUILayout.Width(width), GUILayout.Height(24f))
                            : UtilityWindowTheme.TintedButton(action.label, action.tint, GUILayout.Width(width), GUILayout.Height(24f));
                        if (clicked && action.enabled && action.action != null)
                            action.action();
                    }
                    used += width + 4f;
                }
            }
            finally {
                EndRow();
            }
        }
        private static void DrawWrappedLabel(string text, GUIStyle style) {
            GUIStyle wrapped = new GUIStyle(style ?? EditorStyles.wordWrappedLabel) {
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            EditorGUILayout.LabelField(text ?? string.Empty, wrapped, GUILayout.ExpandWidth(true));
        }
        private static void DrawDashboardCard(string title, string body, Color tint) {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.11f, 0.05f, 5, 3), GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true))) {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(body ?? string.Empty, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }
        private void DrawTopFindingDashboardCard() {
            PungentUtilityDesignAudit.Issue issue = _projectIssues.Where(IsEnabledProviderIssue).OrderByDescending(item => item.severity).FirstOrDefault();
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(issue == null ? UtilityWindowTheme.Neutral : GetTint(issue.severity), 0.11f, 0.05f, 5, 3), GUILayout.Width(_overviewCardWidth), GUILayout.MinHeight(132f), GUILayout.ExpandWidth(true))) {
                EditorGUILayout.LabelField("Top Finding", EditorStyles.boldLabel);
                ProjectAuditProvider provider = GetProviderForIssue(issue);
                string source = provider != null ? provider.displayName : issue != null ? issue.area : string.Empty;
                DrawWrappedLabel(issue == null ? "No findings yet" : issue.severity + " - " + source, UtilityWindowTheme.CardLabelStyle);
                DrawWrappedLabel(issue == null ? "Refresh cached results or run an enabled audit tool." : Shorten(issue.target + ": " + issue.message, 96), UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUI.DisabledScope(issue == null)) {
                    if (GUILayout.Button(new GUIContent("Review Finding", "Select the most severe current finding."), EditorStyles.miniButton, GUILayout.Width(108f)))
                        SelectIssueAndShowFindings(issue);
                }
            }
        }
        private static void DrawGroupHeader(string area, int count) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(area, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(count + " findings", UtilityWindowTheme.Neutral, 92f);
            }
        }
        private static void DrawDetailRow(string label, string value, Color tint) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(112f));
                UtilityWindowTheme.CountPill(value, tint, Mathf.Clamp(48f + value.Length * 6f, 80f, 180f));
                GUILayout.FlexibleSpace();
            }
        }
        private static void DrawDetailText(string label, string value, GUIStyle style = null) {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            DrawWrappedLabel(value ?? string.Empty, style ?? UtilityWindowTheme.BodyStyle);
        }
        private string GetIssueCode(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return string.Empty;
            return _issueCodeByIssueKey.TryGetValue(BuildIssueKey(issue), out string code) ? code : string.Empty;
        }
        private string GetIssueTypeLabel(PungentUtilityDesignAudit.Issue issue) {
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            return PungentScanFindingActions.GetIssueTypeBadge(GetIssueCode(issue), issue != null ? issue.target : string.Empty, provider != null ? provider.id : string.Empty);
        }
        private string GetIssueFreshnessLabel(ProjectAuditProvider provider) {
            if (provider == null)
                return "Unknown";
            return PungentScanSnapshotStore.GetFreshnessLabel(provider.id);
        }
        private Color GetIssueFreshnessTint(ProjectAuditProvider provider) {
            string label = GetIssueFreshnessLabel(provider);
            return label == "Fresh" ? UtilityWindowTheme.Green : label == "Stale" ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;
        }
        private string GetIssueAgeLabel(ProjectAuditProvider provider) {
            if (provider == null)
                return "unknown";
            if (provider.cachedResult != null && provider.cachedResult.IsComplete)
                return PungentScanFindingActions.FormatAge(provider.cachedResult.CompletedAtUtc);
            return PungentScanSnapshotStore.GetLastScanAgeLabel(provider.id);
        }
        private string GetIssueImpact(PungentUtilityDesignAudit.Issue issue) {
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            return PungentScanFindingActions.GetImpact(provider != null ? provider.id : string.Empty, GetIssueCode(issue), MapSeverity(issue != null ? issue.severity : PungentUtilityDesignAudit.Severity.Info));
        }
        private string GetIssueRecommendedAction(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return "Review the finding in context.";
            ProjectAuditProvider provider = GetProviderForIssue(issue);
            string resolved = PungentScanFindingActions.GetRecommendedAction(provider != null ? provider.id : string.Empty, GetIssueCode(issue), MapSeverity(issue.severity));
            return string.IsNullOrWhiteSpace(resolved) ? (string.IsNullOrWhiteSpace(issue.recommendation) ? "Review the finding in context." : issue.recommendation) : resolved;
        }
        private static PungentScanSeverity MapSeverity(PungentUtilityDesignAudit.Severity severity) {
            switch (severity) {
                case PungentUtilityDesignAudit.Severity.Error:
                    return PungentScanSeverity.Error;
                case PungentUtilityDesignAudit.Severity.Warning:
                    return PungentScanSeverity.Warning;
                default:
                    return PungentScanSeverity.Info;
            }
        }
        private static string BuildIssueCopy(PungentUtilityDesignAudit.Issue issue) {
            var sb = new StringBuilder();
            sb.AppendLine("Target: " + issue.target);
            sb.AppendLine("Severity: " + issue.severity);
            sb.AppendLine("Area: " + issue.area);
            sb.AppendLine("Message: " + issue.message);
            sb.AppendLine("Recommendation: " + issue.recommendation);
            if (!string.IsNullOrWhiteSpace(issue.assetPath))
                sb.AppendLine("Asset Path: " + issue.assetPath);
            return sb.ToString();
        }
        private static string BuildFixBrief(PungentUtilityDesignAudit.Issue issue) {
            var sb = new StringBuilder();
            sb.AppendLine("Fix this PungentFunk Utilities design audit issue.");
            sb.AppendLine();
            sb.AppendLine("Preserve existing public/static entry points and keep scan/audit work explicit and outside repaint.");
            sb.AppendLine();
            sb.AppendLine(BuildIssueCopy(issue));
            sb.AppendLine("After implementation, verify runtime/shared scripts do not reference UnityEditor and the affected window remains usable at small sizes.");
            return sb.ToString();
        }
        private static string BuildIssueKey(PungentUtilityDesignAudit.Issue issue) {
            if (issue == null)
                return string.Empty;
            return issue.severity + "|" + (issue.area ?? string.Empty) + "|" + (issue.target ?? string.Empty) + "|" + (issue.message ?? string.Empty) + "|" + (issue.assetPath ?? string.Empty);
        }
        private static Color GetTint(PungentUtilityDesignAudit.Severity severity) {
            switch (severity) {
                case PungentUtilityDesignAudit.Severity.Error:
                    return UtilityWindowTheme.Red;
                case PungentUtilityDesignAudit.Severity.Warning:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Cyan;
            }
        }
        private static Color GetProviderStatusTint(ProviderRunState state) {
            switch (state) {
                case ProviderRunState.Complete:
                    return UtilityWindowTheme.Green;
                case ProviderRunState.RequiresExplicitScannerRun:
                case ProviderRunState.NotConfigured:
                case ProviderRunState.Paused:
                    return UtilityWindowTheme.Amber;
                case ProviderRunState.Failed:
                    return UtilityWindowTheme.Red;
                case ProviderRunState.Cancelled:
                case ProviderRunState.Skipped:
                    return UtilityWindowTheme.Neutral;
                case ProviderRunState.Cancelling:
                case ProviderRunState.Queued:
                case ProviderRunState.Running:
                    return UtilityWindowTheme.Cyan;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }
        private static string Shorten(string value, int max) {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
                return value ?? string.Empty;
            return value.Substring(0, Mathf.Max(1, max - 1)) + "...";
        }
        private static bool Contains(string value, string query) {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static void PingAsset(string path) {
            if (string.IsNullOrWhiteSpace(path))
                return;
            string assetPath = ExtractAssetPath(path);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        private static string ExtractAssetPath(string path) {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            int separator = path.IndexOf(" :: ", StringComparison.Ordinal);
            return separator >= 0 ? path.Substring(0, separator) : path;
        }
        private static string ProviderEnabledPref(string id) {
            return PrefProviderEnabledPrefix + id;
        }
        private struct PillSpec {
            public readonly string label;
            public readonly Color tint;
            public readonly float width;
            public PillSpec(string label, Color tint, float width) {
                this.label = label;
                this.tint = tint;
                this.width = width;
            }
        }
        private struct ActionSpec {
            public readonly string label;
            public readonly string tooltip;
            public readonly float width;
            public readonly Color tint;
            public readonly Action action;
            public readonly bool enabled;
            public ActionSpec(string label, string tooltip, float width, Color tint, Action action, bool enabled) {
                this.label = label;
                this.tooltip = tooltip;
                this.width = width;
                this.tint = tint;
                this.action = action;
                this.enabled = enabled;
            }
        }
        private struct ProviderSkipInfo {
            public readonly string displayName;
            public readonly string reason;
            public ProviderSkipInfo(string displayName, string reason) {
                this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Provider" : displayName;
                this.reason = string.IsNullOrWhiteSpace(reason) ? "not configured" : reason;
            }
        }
        private sealed class IssueGroup {
            public readonly string area;
            public readonly string key;
            public readonly List<PungentUtilityDesignAudit.Issue> issues = new List<PungentUtilityDesignAudit.Issue>();
            public int errorCount;
            public int warningCount;
            public int infoCount;
            public bool selectedInside;
            public bool collapsed;
            public IssueGroup(string area, string key, bool collapsed) {
                this.area = string.IsNullOrWhiteSpace(area) ? "General" : area;
                this.key = string.IsNullOrWhiteSpace(key) ? "general" : key;
                this.collapsed = collapsed;
            }
            public void Add(PungentUtilityDesignAudit.Issue issue, bool selected) {
                if (issue == null)
                    return;
                issues.Add(issue);
                selectedInside |= selected;
                switch (issue.severity) {
                    case PungentUtilityDesignAudit.Severity.Error:
                        errorCount++;
                        break;
                    case PungentUtilityDesignAudit.Severity.Warning:
                        warningCount++;
                        break;
                    default:
                        infoCount++;
                        break;
                }
            }
        }
        private sealed class ProjectAuditProvider {
            public string id;
            public string displayName;
            public string description;
            public string openButtonLabel;
            public bool enabled;
            public bool available;
            public bool canRunFromDashboard;
            public ProviderRunState state;
            public string lastStatus;
            public string runButtonLabel;
            public string notConfiguredReason;
            public int issueCount;
            public int warningCount;
            public int errorCount;
            public int infoCount;
            public int successCount;
            public PungentUtilityDescriptor descriptor;
            public IPungentAuditScanProvider adapter;
            public PungentScanResult cachedResult;
            public string topFinding;
            public bool metadataExpanded;
            public string cachedDigestKey;
            public PungentScanFindingDigest cachedDigest;
        }
    }
    [InitializeOnLoad]
    internal static class PungentAuditBuiltInProviderBootstrap {
        static PungentAuditBuiltInProviderBootstrap() {
            PungentUtilityDesignAuditWindow.RegisterBuiltInProjectAuditProviders();
        }
    }
#endif
}
