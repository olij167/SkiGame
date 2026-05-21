using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Text;
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public sealed class SceneIssueScannerWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.SceneIssueScanner.";
        private const string PrefIncludeInactive = PrefPrefix + "IncludeInactive";
        private const string PrefIncludeAdvisory = PrefPrefix + "IncludeAdvisory";
        private const string PrefIncludePackageScenes = PrefPrefix + "IncludePackageScenes";
        private const string PrefCameraThreshold = PrefPrefix + "CameraThreshold";
        private const string PrefLightThreshold = PrefPrefix + "LightThreshold";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefSeverity = PrefPrefix + "Severity";
        private const string PrefCategory = PrefPrefix + "Category";
        private const string PrefGroupByScene = PrefPrefix + "GroupByScene";

        private readonly SceneIssueScanSettings _settings = new SceneIssueScanSettings();
        private readonly List<PungentScanIssue> _filteredIssues = new List<PungentScanIssue>();
        private Vector2 _scroll;
        private Vector2 _findingsScroll;
        private Vector2 _detailScroll;
        private PungentScanResult _result;
        private PungentScanIssue _selectedIssue;
        private SceneIssueScanSummary _summary;
        private string _sourceBanner;
        private string _search = string.Empty;
        private int _severityFilter;
        private int _categoryFilter;
        private bool _groupByScene = true;
        private bool _thresholdsExpanded;
        private bool _scenePassPlanExpanded;
        private bool _scanQueued;
        private static readonly string[] SeverityFilterLabels = { "All", "Errors", "Warnings", "Info" };
        private static readonly string[] CategoryFilterLabels = { "All", "Missing Script", "Broken Material", "Missing Mesh/Sprite", "Duplicate Service", "Advisory" };

        public static void Open()
        {
            SceneIssueScannerWindow window = GetWindow<SceneIssueScannerWindow>("Scene Issues");
            window.minSize = new Vector2(640f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(640f, 480f);
            titleContent = new GUIContent("Scene Issues");
            _settings.Scope = SceneIssueScanScope.OpenScenesOnly;
            _settings.IncludeInactiveObjects = UtilityWindowPrefs.GetBool(PrefIncludeInactive, true);
            _settings.IncludeAdvisoryFindings = UtilityWindowPrefs.GetBool(PrefIncludeAdvisory, false);
            _settings.IncludePackageScenes = UtilityWindowPrefs.GetBool(PrefIncludePackageScenes, false);
            _settings.ExcessiveCameraThreshold = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefCameraThreshold, 8), 1, 128);
            _settings.ExcessiveRealtimeLightThreshold = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefLightThreshold, 16), 1, 256);
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _severityFilter = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSeverity, 0), 0, SeverityFilterLabels.Length - 1);
            _categoryFilter = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefCategory, 0), 0, CategoryFilterLabels.Length - 1);
            _groupByScene = UtilityWindowPrefs.GetBool(PrefGroupByScene, true);
            RefreshCachedResult();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RunOpenScenesScan;
            SavePrefs();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            DrawScopePanel();
            DrawActions();
            DrawSharedScenePassNotice();
            DrawResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            UtilityWindowTheme.Header(
                "Scene Issue Scanner",
                "Non-destructive scene checks for missing scripts, broken renderer assets, duplicate scene services, and advisory thresholds.",
                BuildHeaderStatus());

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                EditorGUILayout.LabelField("Manual scans stay on currently loaded scenes. Full-project scans run through Design Validation Audit's shared scene pass.", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                PungentUtilityHelpButton.Draw(SceneIssueScanService.ProviderId, "overview", "window-header", "Open help for the Scene Issue Scanner overview.", "Scene Issue Scanner header");
            }
        }

        private string BuildHeaderStatus()
        {
            if (_scanQueued)
                return "Scan queued";
            if (_result == null)
                return "No cached result";
            return PungentScanSnapshotStore.GetFreshnessLabel(SceneIssueScanService.ProviderId) + " / " + PungentScanSnapshotStore.GetLastScanAgeLabel(SceneIssueScanService.ProviderId);
        }

        private static void DrawSectionTitleWithHelp(string title, Color tint, string pill, string sectionId, string topicId, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(pill))
                    UtilityWindowTheme.CountPill(pill, tint);
                PungentUtilityHelpButton.Draw(SceneIssueScanService.ProviderId, sectionId, topicId, tooltip, title);
            }
        }

        private void DrawScopePanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.06f)))
            {
                DrawSectionTitleWithHelp("Scope", UtilityWindowTheme.Blue, _settings.Scope == SceneIssueScanScope.OpenScenesOnly ? "manual safe" : "full audit only", "scan-settings", "scope", "Open help for Scene Issue scan settings.");
                _settings.Scope = (SceneIssueScanScope)EditorGUILayout.EnumPopup(new GUIContent("Scene Scope", "Manual scans run Open Scenes Only. Full-project scanning is available through Design Validation Audit's shared scene pass, using the Terrain Usage scene scope."), _settings.Scope);
                _settings.IncludeInactiveObjects = EditorGUILayout.ToggleLeft(new GUIContent("Include inactive objects", "Includes inactive scene objects in structural checks. Duplicate runtime-service checks still only count active enabled components."), _settings.IncludeInactiveObjects);
                _settings.IncludePackageScenes = EditorGUILayout.ToggleLeft(new GUIContent("Include package/sample scenes", "Allows open package scenes to be inspected. This is off by default for project-facing audits."), _settings.IncludePackageScenes);
                _settings.IncludeAdvisoryFindings = EditorGUILayout.ToggleLeft(new GUIContent("Include advisory performance thresholds", "Adds low-confidence threshold suggestions such as many active cameras or lights. These are informational, not proof of a defect."), _settings.IncludeAdvisoryFindings);

                _thresholdsExpanded = EditorGUILayout.Foldout(_thresholdsExpanded, "Advisory Thresholds", true);
                if (_thresholdsExpanded)
                {
                    using (new EditorGUI.DisabledScope(!_settings.IncludeAdvisoryFindings))
                    {
                        _settings.ExcessiveCameraThreshold = EditorGUILayout.IntSlider(new GUIContent("Active Cameras", "Information threshold for many active cameras in one loaded scene."), _settings.ExcessiveCameraThreshold, 1, 64);
                        _settings.ExcessiveRealtimeLightThreshold = EditorGUILayout.IntSlider(new GUIContent("Active Lights", "Information threshold for many active lights in one loaded scene."), _settings.ExcessiveRealtimeLightThreshold, 1, 128);
                    }
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_settings.Scope != SceneIssueScanScope.OpenScenesOnly || _scanQueued))
                {
                    string scanLabel = _scanQueued ? "Scan Queued" : "Scan Open Scenes";
                    if (UtilityWindowTheme.TintedButton(scanLabel, UtilityWindowTheme.Green, GUILayout.Width(138f), GUILayout.Height(26f)))
                        QueueOpenScenesScan();
                }

                if (UtilityWindowTheme.TintedButton("Refresh Cached Results", UtilityWindowTheme.Teal, GUILayout.Width(156f), GUILayout.Height(26f)))
                    RefreshCachedResult();

                using (new EditorGUI.DisabledScope(_result == null))
                {
                    if (UtilityWindowTheme.TintedButton("Copy Report", UtilityWindowTheme.Neutral, GUILayout.Width(104f), GUILayout.Height(26f)))
                        CopyReport();
                }

                if (UtilityWindowTheme.TintedButton("Open Design Audit", UtilityWindowTheme.Purple, GUILayout.Width(136f), GUILayout.Height(26f)))
                    PungentUtilityRegistry.Open("design-validation-audit");

                GUILayout.FlexibleSpace();
            }

            if (_settings.Scope != SceneIssueScanScope.OpenScenesOnly)
                EditorGUILayout.HelpBox("This manual window never opens project scenes directly. Run Design Validation Audit to scan the configured project scene scope through the shared scene pass.", MessageType.Info);
        }

        private void DrawSharedScenePassNotice()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.10f, 0.05f)))
            {
                _scenePassPlanExpanded = EditorGUILayout.Foldout(_scenePassPlanExpanded, "Shared Scene Pass", true);
                if (_scenePassPlanExpanded)
                    EditorGUILayout.LabelField(SceneIssueSharedSceneScanPlan.Note, UtilityWindowTheme.MutedMiniLabelStyle);
                else
                    EditorGUILayout.LabelField("Full-project scanning is routed through Design Validation Audit's shared scene pass; the manual window remains open-scenes-only.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawResults()
        {
            DrawCompactResultSummary();
            if (_summary != null)
                DrawSummaryLine();
            DrawFilters();
            RebuildFilteredIssues();
            if (_result == null || _result.Issues == null || _result.Issues.Count == 0)
            {
                DrawEmptyResultsPanel("No Scene Issue findings are cached yet. Open one or more scenes, then run Scan Open Scenes, or include this provider in a full Design Validation Audit.");
                return;
            }
            if (_filteredIssues.Count == 0)
            {
                DrawEmptyResultsPanel("No Scene Issue findings match the current filters.");
                return;
            }

            bool stacked = EditorGUIUtility.currentViewWidth < 820f;
            if (stacked)
            {
                DrawFindingsPanel(_filteredIssues, Mathf.Max(180f, position.height * 0.32f));
                DrawDetailPanel();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Min(420f, position.width * 0.46f)), GUILayout.ExpandHeight(true)))
                        DrawFindingsPanel(_filteredIssues, Mathf.Max(220f, position.height - 360f));
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                        DrawDetailPanel();
                }
            }
        }

        private void DrawCompactResultSummary()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.05f)))
            {
                DrawSectionTitleWithHelp("Scan Summary", UtilityWindowTheme.Teal, _result != null ? PungentScanSnapshotStore.GetFreshnessLabel(SceneIssueScanService.ProviderId) : "No cache", "results", "summary", "Open help for Scene Issue scan summaries.");

                string status = _scanQueued
                    ? "Open-scenes scan is queued and will run outside IMGUI."
                    : _result != null
                        ? (string.IsNullOrWhiteSpace(_result.StatusMessage) ? "Cached Scene Issue result is available." : _result.StatusMessage)
                        : "No Scene Issue scan result is cached yet.";
                EditorGUILayout.LabelField(status, UtilityWindowTheme.CardLabelStyle);

                string source = string.IsNullOrWhiteSpace(_sourceBanner)
                    ? (_result == null ? "Run Scan Open Scenes or include Scene Issue Scanner in Design Validation Audit." : "Loaded from the shared scan cache.")
                    : _sourceBanner;
                EditorGUILayout.LabelField(source, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    string scope = _result != null && !string.IsNullOrWhiteSpace(_result.ScopeLabel) ? _result.ScopeLabel : "Open Scenes Only";
                    string age = _result != null ? PungentScanSnapshotStore.GetLastScanAgeLabel(SceneIssueScanService.ProviderId) : "Never";
                    UtilityWindowTheme.CountPill("Scope: " + scope, UtilityWindowTheme.Blue, PillWidth("Scope: " + scope, 112f, 220f));
                    UtilityWindowTheme.CountPill("Last: " + age, UtilityWindowTheme.Neutral, PillWidth("Last: " + age, 92f, 180f));
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
                string nextSearch = EditorGUILayout.TextField(_search, GUILayout.MinWidth(120f));
                if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
                {
                    _search = nextSearch;
                    UtilityWindowPrefs.SetString(PrefSearch, _search);
                }
                GUILayout.FlexibleSpace();
                PungentUtilityHelpButton.Draw(SceneIssueScanService.ProviderId, "results", "filters", "Open help for Scene Issue result filters.", "Scene Issue result filters");
            }

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                EditorGUILayout.LabelField("Severity", GUILayout.Width(54f));
                int nextSeverity = EditorGUILayout.Popup(_severityFilter, SeverityFilterLabels, GUILayout.Width(96f));
                if (nextSeverity != _severityFilter)
                {
                    _severityFilter = nextSeverity;
                    UtilityWindowPrefs.SetInt(PrefSeverity, _severityFilter);
                }
                EditorGUILayout.LabelField("Category", GUILayout.Width(58f));
                int nextCategory = EditorGUILayout.Popup(_categoryFilter, CategoryFilterLabels, GUILayout.Width(146f));
                if (nextCategory != _categoryFilter)
                {
                    _categoryFilter = nextCategory;
                    UtilityWindowPrefs.SetInt(PrefCategory, _categoryFilter);
                }
                bool nextGroupByScene = EditorGUILayout.ToggleLeft(new GUIContent("Group by scene", "Groups findings by scene path for faster triage."), _groupByScene, GUILayout.Width(112f));
                if (nextGroupByScene != _groupByScene)
                {
                    _groupByScene = nextGroupByScene;
                    UtilityWindowPrefs.SetBool(PrefGroupByScene, _groupByScene);
                }
            }
        }

        private void DrawSummaryLine()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Scenes: " + _summary.ScenesScanned, UtilityWindowTheme.Blue, 86f);
                if (_summary.BuildScenesIncluded > 0 || _summary.BuildScenesExcluded > 0)
                    UtilityWindowTheme.CountPill("Build: " + _summary.BuildScenesIncluded + " / " + (_summary.BuildScenesIncluded + _summary.BuildScenesExcluded), UtilityWindowTheme.Purple, 88f);
                UtilityWindowTheme.CountPill("Objects: " + _summary.ObjectsScanned, UtilityWindowTheme.Teal, 96f);
                UtilityWindowTheme.CountPill("Skipped: " + _summary.ScenesSkipped, UtilityWindowTheme.Amber, 92f);
                DrawSummaryFilterChip("Issues", _summary.IssuesFound, 0, _summary.IssuesFound > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 90f);
                if (_result != null)
                {
                    DrawSummaryFilterChip("Errors", _result.Summary.ErrorCount, 1, _result.Summary.ErrorCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral, 88f);
                    DrawSummaryFilterChip("Warnings", _result.Summary.WarningCount, 2, _result.Summary.WarningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 108f);
                    DrawSummaryFilterChip("Info", _result.Summary.InfoCount, 3, _result.Summary.InfoCount > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, 78f);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSummaryFilterChip(string label, int count, int severityIndex, Color tint, float width)
        {
            bool allChip = severityIndex == 0;
            bool active = allChip ? _severityFilter == 0 && _categoryFilter == 0 : _severityFilter == severityIndex && _categoryFilter == 0;
            string text = active ? label + ": " + count + " on" : label + ": " + count;
            string tooltip = allChip
                ? "Show all Scene Issue finding cards and clear severity/category filters."
                : "Toggle the " + label.ToLowerInvariant() + " filter for Scene Issue finding cards.";
            UtilityWindowTheme.ActionPill(new GUIContent(text, tooltip), active ? tint : Color.Lerp(tint, UtilityWindowTheme.Neutral, 0.35f), () => ToggleSummaryFilter(severityIndex), width);
        }

        private void ToggleSummaryFilter(int severityIndex)
        {
            if (severityIndex <= 0)
            {
                _severityFilter = 0;
                _categoryFilter = 0;
            }
            else if (_severityFilter == severityIndex && _categoryFilter == 0)
            {
                _severityFilter = 0;
            }
            else
            {
                _severityFilter = severityIndex;
                _categoryFilter = 0;
            }

            UtilityWindowPrefs.SetInt(PrefSeverity, _severityFilter);
            UtilityWindowPrefs.SetInt(PrefCategory, _categoryFilter);
            RebuildFilteredIssues();
            Repaint();
        }

        private static float PillWidth(string text, float min, float max)
        {
            return Mathf.Clamp((string.IsNullOrEmpty(text) ? 0 : text.Length) * 7f + 22f, min, max);
        }

        private void DrawFindingsPanel(IReadOnlyList<PungentScanIssue> issues, float height)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f)))
            {
                UtilityWindowTheme.SectionTitle("Findings", UtilityWindowTheme.Teal, issues.Count + " shown");
                _findingsScroll = EditorGUILayout.BeginScrollView(_findingsScroll, false, true, GUILayout.Height(Mathf.Max(80f, height)));
                if (_groupByScene)
                    DrawGroupedFindingsByScene(issues);
                else
                {
                    for (int i = 0; i < issues.Count; i++)
                        PungentScanGUI.DrawIssueRowCompact(issues[i], SceneIssueScanService.ProviderId, SceneIssueScanService.DisplayName, SelectIssue);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGroupedFindingsByScene(IReadOnlyList<PungentScanIssue> issues)
        {
            List<PungentScanIssue> ordered = new List<PungentScanIssue>(issues);
            ordered.Sort(CompareIssuesForSceneGrouping);
            string currentScene = null;
            for (int i = 0; i < ordered.Count; i++)
            {
                PungentScanIssue issue = ordered[i];
                string scenePath = ExtractScenePath(issue != null ? issue.Path : null);
                if (string.IsNullOrWhiteSpace(scenePath))
                    scenePath = "Scene not recorded";
                if (!string.Equals(scenePath, currentScene, StringComparison.OrdinalIgnoreCase))
                {
                    currentScene = scenePath;
                    EditorGUILayout.LabelField(currentScene, UtilityWindowTheme.PathLabelStyle);
                }
                PungentScanGUI.DrawIssueRowCompact(issue, SceneIssueScanService.ProviderId, SceneIssueScanService.DisplayName, SelectIssue);
            }
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f), GUILayout.ExpandHeight(true)))
            {
                DrawSectionTitleWithHelp("Issue Detail", UtilityWindowTheme.Neutral, _selectedIssue != null ? _selectedIssue.Severity.ToString() : "No selection", "issue-detail", "selected-issue", "Open help for Scene Issue details and safe actions.");
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MinHeight(160f), GUILayout.ExpandHeight(true));
                PungentScanGUI.DrawIssueDetail(_selectedIssue, SceneIssueScanService.ProviderId, SceneIssueScanService.DisplayName);
                DrawIssueActions();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawIssueActions()
        {
            if (_selectedIssue == null)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_selectedIssue.Context == null))
                {
                    if (GUILayout.Button(new GUIContent("Ping", "Ping the selected scene object, component, material, or asset."), GUILayout.Width(56f)))
                        EditorGUIUtility.PingObject(_selectedIssue.Context);
                    if (GUILayout.Button(new GUIContent("Select", "Select the selected scene object, component, material, or asset when it is still loaded."), GUILayout.Width(64f)))
                        Selection.activeObject = _selectedIssue.Context;
                }
                if (GUILayout.Button(new GUIContent("Ping Scene", "Ping the scene asset referenced by this finding."), GUILayout.Width(88f)))
                    PingSceneAsset(_selectedIssue.Path);
                if (GUILayout.Button(new GUIContent("Open Scene", "Open the scene referenced by this finding after prompting for unsaved scene changes."), GUILayout.Width(88f)))
                    OpenIssueScene(_selectedIssue);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Copy", "Copy this finding to the clipboard."), GUILayout.Width(58f)))
                    CopyIssue(_selectedIssue);
                if (GUILayout.Button(new GUIContent("Copy Brief", "Copy a manual fix brief for this scene issue."), GUILayout.Width(82f)))
                    CopyFixBrief(_selectedIssue);
                if (GUILayout.Button(new GUIContent("Note", "Create or open an audit note for this scene issue."), GUILayout.Width(56f)))
                    CreateAuditNote(_selectedIssue);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawEmptyResultsPanel(string message)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f)))
                EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        private void RebuildFilteredIssues()
        {
            _filteredIssues.Clear();
            if (_result == null || _result.Issues == null)
                return;
            for (int i = 0; i < _result.Issues.Count; i++)
            {
                PungentScanIssue issue = _result.Issues[i];
                if (issue == null)
                    continue;
                if (!MatchesSeverity(issue) || !MatchesCategory(issue) || !MatchesSearch(issue))
                    continue;
                _filteredIssues.Add(issue);
            }
            if (_selectedIssue != null && !_filteredIssues.Contains(_selectedIssue))
                _selectedIssue = _filteredIssues.Count > 0 ? _filteredIssues[0] : null;
        }

        private bool MatchesSeverity(PungentScanIssue issue)
        {
            switch (_severityFilter)
            {
                case 1: return issue.Severity == PungentScanSeverity.Error;
                case 2: return issue.Severity == PungentScanSeverity.Warning;
                case 3: return issue.Severity == PungentScanSeverity.Info;
                default: return true;
            }
        }

        private bool MatchesCategory(PungentScanIssue issue)
        {
            if (_categoryFilter == 0)
                return true;
            string code = issue.Code ?? string.Empty;
            switch (_categoryFilter)
            {
                case 1: return code.IndexOf("MISSING_SCRIPT", StringComparison.OrdinalIgnoreCase) >= 0;
                case 2: return code.IndexOf("MATERIAL", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("SHADER", StringComparison.OrdinalIgnoreCase) >= 0;
                case 3: return code.IndexOf("MESH", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("SPRITE", StringComparison.OrdinalIgnoreCase) >= 0;
                case 4: return code.IndexOf("DUPLICATE", StringComparison.OrdinalIgnoreCase) >= 0;
                case 5: return code.IndexOf("MANY_", StringComparison.OrdinalIgnoreCase) >= 0;
                default: return true;
            }
        }

        private bool MatchesSearch(PungentScanIssue issue)
        {
            return string.IsNullOrWhiteSpace(_search) || (issue.SearchText ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareIssuesForSceneGrouping(PungentScanIssue left, PungentScanIssue right)
        {
            string leftScene = ExtractScenePath(left != null ? left.Path : null);
            string rightScene = ExtractScenePath(right != null ? right.Path : null);
            int sceneCompare = string.Compare(leftScene, rightScene, StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
                return sceneCompare;

            int severityCompare = GetSeverityRank(right != null ? right.Severity : PungentScanSeverity.None).CompareTo(GetSeverityRank(left != null ? left.Severity : PungentScanSeverity.None));
            if (severityCompare != 0)
                return severityCompare;

            return string.Compare(left != null ? left.Title : string.Empty, right != null ? right.Title : string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetSeverityRank(PungentScanSeverity severity)
        {
            switch (severity)
            {
                case PungentScanSeverity.Error: return 4;
                case PungentScanSeverity.Warning: return 3;
                case PungentScanSeverity.Info: return 2;
                case PungentScanSeverity.Success: return 1;
                default: return 0;
            }
        }

        private void QueueOpenScenesScan()
        {
            if (_scanQueued)
                return;
            _scanQueued = true;
            _sourceBanner = "Open-scenes scan queued.";
            EditorApplication.delayCall += RunOpenScenesScan;
            Repaint();
        }

        private void RunOpenScenesScan()
        {
            if (this == null)
                return;
            _scanQueued = false;
            SavePrefs();
            _result = SceneIssueScanService.ScanOpenScenes(_settings, out _summary);
            _sourceBanner = "Direct open-scenes scan - " + PungentScanSnapshotStore.GetLastScanAgeLabel(SceneIssueScanService.ProviderId);
            _selectedIssue = _result != null && _result.Issues.Count > 0 ? _result.Issues[0] : null;
            Repaint();
        }

        private void RefreshCachedResult()
        {
            if (PungentScanCache.TryGet(SceneIssueScanService.ProviderId, out PungentScanResult result) && result != null)
            {
                _result = result;
                _summary = BuildSummaryFromResult(result);
                _sourceBanner = "Loaded cached result - " + PungentScanSnapshotStore.GetLastScanAgeLabel(SceneIssueScanService.ProviderId);
                _selectedIssue = _result.Issues.Count > 0 ? _result.Issues[0] : null;
            }
            else
            {
                _result = null;
                _summary = null;
                _selectedIssue = null;
                _sourceBanner = string.Empty;
            }
        }

        private static SceneIssueScanSummary BuildSummaryFromResult(PungentScanResult result)
        {
            if (result == null)
                return null;
            return new SceneIssueScanSummary
            {
                ScenesScanned = result.TotalScanned,
                ScenesSkipped = result.TotalSkipped,
                IssuesFound = result.TotalMatched,
                Duration = TimeSpan.FromSeconds(Math.Max(0d, result.DurationSeconds)),
                StatusMessage = result.StatusMessage
            };
        }

        private void SelectIssue(PungentScanIssue issue)
        {
            _selectedIssue = issue;
            Repaint();
        }

        private void CopyReport()
        {
            if (_result == null)
                return;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Scene Issue Scanner Report");
            builder.AppendLine(_result.StatusMessage ?? string.Empty);
            builder.AppendLine("Scope: " + _result.ScopeLabel);
            builder.AppendLine("Scanned: " + _result.TotalScanned + " | Issues: " + _result.TotalMatched + " | Skipped: " + _result.TotalSkipped);
            for (int i = 0; i < _result.Issues.Count; i++)
            {
                PungentScanIssue issue = _result.Issues[i];
                builder.AppendLine();
                builder.AppendLine(issue.Severity + ": " + issue.Title);
                builder.AppendLine(issue.Message);
                if (!string.IsNullOrWhiteSpace(issue.Path))
                    builder.AppendLine(issue.Path);
            }
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private static void CopyIssue(PungentScanIssue issue)
        {
            if (issue == null)
                return;
            EditorGUIUtility.systemCopyBuffer =
                issue.Severity + ": " + issue.Title + Environment.NewLine +
                issue.Message + Environment.NewLine +
                issue.Path + Environment.NewLine +
                issue.Code;
        }

        private static void CopyFixBrief(PungentScanIssue issue)
        {
            if (issue == null)
                return;
            EditorGUIUtility.systemCopyBuffer =
                "Scene Issue Fix Brief" + Environment.NewLine +
                "Provider: " + SceneIssueScanService.DisplayName + Environment.NewLine +
                "Severity: " + issue.Severity + Environment.NewLine +
                "Issue: " + issue.Title + Environment.NewLine +
                "Message: " + issue.Message + Environment.NewLine +
                "Context: " + issue.Path + Environment.NewLine +
                "Code: " + issue.Code + Environment.NewLine +
                "Request: Inspect the referenced scene/object and resolve the issue non-destructively. Do not apply automated fixes without preview, Undo, and explicit approval.";
        }

        private static void CreateAuditNote(PungentScanIssue issue)
        {
            if (issue == null)
                return;
            PungentUtilityDesignAudit.Issue auditIssue = new PungentUtilityDesignAudit.Issue(
                ToAuditSeverity(issue.Severity),
                SceneIssueScanService.DisplayName,
                issue.Title,
                issue.Message,
                PungentScanFindingActions.GetRecommendedAction(SceneIssueScanService.ProviderId, issue.Code, issue.Severity),
                ExtractScenePath(issue.Path));
            PungentNote note = PungentNoteAuditIssueBridge.CreateOrOpen(auditIssue);
            if (note != null)
                PungentNotesRoadmapWindow.OpenAndSelect(note.id);
        }

        private static void OpenIssueScene(PungentScanIssue issue)
        {
            string scenePath = ExtractScenePath(issue != null ? issue.Path : null);
            if (string.IsNullOrWhiteSpace(scenePath))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static PungentUtilityDesignAudit.Severity ToAuditSeverity(PungentScanSeverity severity)
        {
            switch (severity)
            {
                case PungentScanSeverity.Error: return PungentUtilityDesignAudit.Severity.Error;
                case PungentScanSeverity.Warning: return PungentUtilityDesignAudit.Severity.Warning;
                default: return PungentUtilityDesignAudit.Severity.Info;
            }
        }

        private static void PingSceneAsset(string path)
        {
            string scenePath = ExtractScenePath(path);
            if (string.IsNullOrWhiteSpace(scenePath))
                return;
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset != null)
                EditorGUIUtility.PingObject(sceneAsset);
        }

        private static string ExtractScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            int separator = path.IndexOf(" :: ", StringComparison.Ordinal);
            return separator >= 0 ? path.Substring(0, separator) : path;
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefIncludeInactive, _settings.IncludeInactiveObjects);
            UtilityWindowPrefs.SetBool(PrefIncludeAdvisory, _settings.IncludeAdvisoryFindings);
            UtilityWindowPrefs.SetBool(PrefIncludePackageScenes, _settings.IncludePackageScenes);
            UtilityWindowPrefs.SetInt(PrefCameraThreshold, _settings.ExcessiveCameraThreshold);
            UtilityWindowPrefs.SetInt(PrefLightThreshold, _settings.ExcessiveRealtimeLightThreshold);
            UtilityWindowPrefs.SetBool(PrefGroupByScene, _groupByScene);
        }
    }
#endif
}
