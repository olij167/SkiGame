using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
    internal sealed class TerrainUsageSceneTiming
    {
        public string scenePath;
        public string sceneName;
        public double sceneOpenDurationSeconds;
        public double terrainScanDurationSeconds;
        public double dependencyPrefilterDurationSeconds;
        public double restoreDurationSeconds;
        public double totalSceneStepDurationSeconds;
        public string disposition;
    }

    internal sealed class TerrainUsageScanDiagnostics
    {
        public readonly List<TerrainUsageSceneTiming> sceneTimings = new List<TerrainUsageSceneTiming>();
        public double totalDurationSeconds;
        public int scenesDiscovered;
        public int scenesOpened;
        public int scenesSkipped;
        public int scenesSkippedByDependencyPrefilter;
        public double averageSceneOpenDurationSeconds;
        public string slowestScenePath;
        public double slowestSceneDurationSeconds;
        public double durationBeforePauseSeconds;
        public double durationAfterResumeSeconds;
        public bool resumed;
        public bool dependencyPrefilterEnabled;
        public double restoreDurationSeconds;
        public DateTime completedUtc;
    }

    public class TerrainUsageScannerWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtilities.TerrainUsageScanner.";

        private struct TerrainUseRecord
        {
            public string scenePath;
            public string sceneName;
            public string terrainObjectPath;
            public Terrain terrainComponent;
        }

        private readonly Dictionary<string, List<TerrainUseRecord>> _usedTerrainDataByAssetPath = new();
        private readonly List<string> _allTerrainDataPaths = new();
        private readonly List<string> _unusedTerrainDataPaths = new();
        private readonly List<string> _scenePathsScanned = new();
        private readonly List<string> _cachedUsedTerrainPaths = new();
        private readonly List<string> _cachedFilteredUnusedTerrainPaths = new();
        private readonly PungentScanSession _scanSession = new PungentScanSession("terrain-usage-scanner", "Terrain Usage Scanner");

        private Vector2 _usedScroll;
        private Vector2 _unusedScroll;
        private Vector2 _scopeScroll;
        private float _scopePanelHeight = 310f;
        private float _usedPanelWidth = 470f;
        private string _resultSourceBanner = string.Empty;

        private bool _includeDisabledGameObjects = true;
        private bool _autoPingSelection = false;
        private bool _isScanning = false;
        private bool _resultCacheDirty = true;
        private bool _diagnosticsExpanded = false;

        // Scene scan scope
        private bool _scanScenesProjectWide = true;
        private DefaultAsset _sceneFolderAsset;
        private string _sceneFolderPath = "Assets";
        private bool _skipPackageScenes = true;
        private bool _includeSceneSubfolders = true;
        private bool _prefilterScenesByTerrainDataDependencies = false;

        // Terrain asset scope
        private bool _scanTerrainAssetsProjectWide = true;
        private DefaultAsset _terrainFolderAsset;
        private string _terrainFolderPath = "Assets";
        private bool _skipPackageTerrainAssets = true;
        private bool _includeTerrainSubfolders = true;

        // Unused quick filter
        private bool _showOnlyUnusedInSelectedFolder = false;
        private DefaultAsset _unusedFilterFolderAsset;
        private string _unusedFilterFolderPath = "Assets";
        private bool _unusedFilterIncludeSubfolders = true;

        private string _resultSearch = string.Empty;
        private string _status = "Idle";
        private double _lastScanTimeSeconds = 0d;

        public static void ShowWindow()
        {
            var window = GetWindow<TerrainUsageScannerWindow>("Terrain Usage Scanner");
            window.minSize = new Vector2(980f, 560f);
            window.Show();
        }

        public static bool RunCoordinatorScanWithWarning()
        {
            bool run = EditorUtility.DisplayDialog(
                "Run Terrain Usage Scan",
                "Terrain Usage Scanner may save/open scenes as part of its scan flow. It will not modify terrain assets, but large projects can take time. Continue?",
                "Run Terrain Scan",
                "Cancel");
            if (!run)
                return false;

            var window = GetWindow<TerrainUsageScannerWindow>("Terrain Usage Scanner");
            window.minSize = new Vector2(980f, 560f);
            window.Show();
            window.Scan();
            return true;
        }

        public static bool RunCoordinatorScan(out string status)
        {
            status = "Terrain Usage scan did not run.";
            TerrainUsageScannerWindow window = null;
            bool destroyWhenDone = false;
            try
            {
                window = GetCoordinatorInstance(out destroyWhenDone);
                window.Scan();
                status = window._status;
                return true;
            }
            catch (Exception ex)
            {
                status = "Terrain Usage scan failed: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (destroyWhenDone && window != null)
                    DestroyImmediate(window);
            }
        }

        public static PungentAuditScanJob CreateAuditJob(PungentAuditScanMode mode)
        {
            return TerrainUsageScanService.CreateJob(mode);
        }

        private static TerrainUsageScannerWindow GetCoordinatorInstance(out bool destroyWhenDone)
        {
            TerrainUsageScannerWindow[] existing = Resources.FindObjectsOfTypeAll<TerrainUsageScannerWindow>();
            if (existing != null && existing.Length > 0)
            {
                destroyWhenDone = false;
                return existing[0];
            }
            destroyWhenDone = true;
            return CreateInstance<TerrainUsageScannerWindow>();
        }

        //// Legacy compatibility alias. Keep until the final public menu root is locked for release.
        //public static void ShowLegacyWindow()
        //{
        //    ShowWindow();
        //}

        private void OnEnable()
        {
            LoadPrefs();
            HydrateLatestScanResult();
            MarkResultCacheDirty();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            RebuildResultCachesIfNeeded();

            UtilityWindowTheme.Header(
                "Terrain Usage Scanner",
                "Scans scene-assigned Terrain components and compares referenced TerrainData assets against a chosen asset scope. It does not analyse runtime-created TerrainData.",
                _status);

            DrawActionToolbar();
            DrawScopePanel();
            UtilityWindowTheme.VerticalResizeHandle(
                ref _scopePanelHeight,
                138f,
                Mathf.Max(138f, position.height - 260f),
                SavePrefs,
                "Drag to resize the scan configuration panel.");

            DrawStatusPanel();
            DrawResultsPanels();
        }

        private void DrawScopePanel()
        {
            using (new EditorGUI.DisabledScope(_isScanning))
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f), GUILayout.Height(_scopePanelHeight)))
                {
                    UtilityWindowTheme.SectionTitle("Scan Configuration", UtilityWindowTheme.Neutral, "Resizable");

                    _scopeScroll = EditorGUILayout.BeginScrollView(_scopeScroll);
                    DrawSceneScopeSection();
                    DrawTerrainScopeSection();
                    DrawUnusedFilterSection();
                    DrawGeneralOptionsSection();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawResultsPanels()
        {
            float minPanelWidth = 300f;
            float maxLeftWidth = Mathf.Max(minPanelWidth, position.width - 335f);
            _usedPanelWidth = Mathf.Clamp(_usedPanelWidth, minPanelWidth, maxLeftWidth);

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawUsedPanel(GUILayout.Width(_usedPanelWidth), GUILayout.ExpandHeight(true));
                UtilityWindowTheme.HorizontalResizeHandle(
                    ref _usedPanelWidth,
                    minPanelWidth,
                    maxLeftWidth,
                    SavePrefs,
                    "Drag to resize the used/unused result panels.");
                DrawUnusedPanel(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            }
        }

        private void DrawActionToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.20f, 0.10f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_isScanning))
                    {
                        if (UtilityWindowTheme.TintedButton("Scan", UtilityWindowTheme.Green, GUILayout.Height(30f), GUILayout.Width(110f)))
                            QueueScan();

                        if (UtilityWindowTheme.TintedButton("Clear Results", UtilityWindowTheme.Neutral, GUILayout.Height(30f), GUILayout.Width(120f)))
                            ClearResults();
                    }

                    using (new EditorGUI.DisabledScope(_usedTerrainDataByAssetPath.Count == 0 && _unusedTerrainDataPaths.Count == 0))
                    {
                        if (UtilityWindowTheme.TintedButton("Export CSV", UtilityWindowTheme.Teal, GUILayout.Height(30f), GUILayout.Width(110f)))
                            ExportCsv();
                    }

                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField("Search", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(44f));

                    string newSearch = GUILayout.TextField(_resultSearch, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(180f));
                    if (newSearch != _resultSearch)
                    {
                        _resultSearch = newSearch;
                        SavePrefs();
                        MarkResultCacheDirty();
                    }

                    if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? EditorStyles.toolbarButton, GUILayout.Width(20f)))
                    {
                        if (!string.IsNullOrEmpty(_resultSearch))
                        {
                            _resultSearch = string.Empty;
                            GUI.FocusControl(null);
                            SavePrefs();
                            MarkResultCacheDirty();
                        }
                    }

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"Scenes: {_scenePathsScanned.Count}", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill($"Used: {_usedTerrainDataByAssetPath.Count}", UtilityWindowTheme.Green);
                    UtilityWindowTheme.CountPill($"Unused: {_unusedTerrainDataPaths.Count}", UtilityWindowTheme.Amber);
                }
            }
        }

        private void DrawSceneScopeSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.08f)))
            {
                UtilityWindowTheme.SectionTitle("Scene Scan Scope", UtilityWindowTheme.Blue);

                EditorGUI.BeginChangeCheck();
                _scanScenesProjectWide = EditorGUILayout.ToggleLeft(new GUIContent("Scan scenes project-wide", "Find all Scene assets in the project."), _scanScenesProjectWide);

                using (new EditorGUI.DisabledScope(_scanScenesProjectWide))
                {
                    DrawFolderField("Scene Folder", ref _sceneFolderAsset, ref _sceneFolderPath, "Assets");
                    _includeSceneSubfolders = EditorGUILayout.ToggleLeft(new GUIContent("Include subfolders", "Include child folders under the selected scene folder."), _includeSceneSubfolders);
                }

                _skipPackageScenes = EditorGUILayout.ToggleLeft(new GUIContent("Skip scenes under Packages/", "Usually safer and faster; package scenes are rarely part of project TerrainData ownership."), _skipPackageScenes);
                _prefilterScenesByTerrainDataDependencies = EditorGUILayout.ToggleLeft(new GUIContent("Skip scenes with no TerrainData dependency", "Checks serialized scene dependencies before opening the scene. This can greatly speed up full scans by skipping scenes that do not reference TerrainData. Disable for the most exhaustive verification."), _prefilterScenesByTerrainDataDependencies);

                string effectiveSceneScope = _scanScenesProjectWide
                    ? (_skipPackageScenes ? "All project scenes excluding Packages/" : "All scenes including Packages/")
                    : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);

                EditorGUILayout.LabelField($"Effective scene scope: {effectiveSceneScope}", UtilityWindowTheme.MutedMiniLabelStyle);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }
        }

        private void DrawTerrainScopeSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.16f, 0.08f)))
            {
                UtilityWindowTheme.SectionTitle("Terrain Asset Search Scope", UtilityWindowTheme.Teal);

                EditorGUI.BeginChangeCheck();
                _scanTerrainAssetsProjectWide = EditorGUILayout.ToggleLeft(new GUIContent("Search terrain assets project-wide", "Find all TerrainData assets in the project."), _scanTerrainAssetsProjectWide);

                using (new EditorGUI.DisabledScope(_scanTerrainAssetsProjectWide))
                {
                    DrawFolderField("Terrain Asset Folder", ref _terrainFolderAsset, ref _terrainFolderPath, "Assets");
                    _includeTerrainSubfolders = EditorGUILayout.ToggleLeft(new GUIContent("Include subfolders", "Include child folders under the selected TerrainData folder."), _includeTerrainSubfolders);
                }

                _skipPackageTerrainAssets = EditorGUILayout.ToggleLeft(new GUIContent("Skip terrain assets under Packages/", "Excludes TerrainData assets stored in packages."), _skipPackageTerrainAssets);

                string effectiveTerrainScope = _scanTerrainAssetsProjectWide
                    ? (_skipPackageTerrainAssets ? "All TerrainData assets excluding Packages/" : "All TerrainData assets including Packages/")
                    : GetScopeDescription(_terrainFolderPath, _includeTerrainSubfolders);

                EditorGUILayout.LabelField($"Effective terrain asset scope: {effectiveTerrainScope}", UtilityWindowTheme.MutedMiniLabelStyle);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }
        }

        private void DrawUnusedFilterSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.16f, 0.08f)))
            {
                UtilityWindowTheme.SectionTitle("Unused Results Filter", UtilityWindowTheme.Amber, $"Filtered: {_cachedFilteredUnusedTerrainPaths.Count}");

                EditorGUI.BeginChangeCheck();
                _showOnlyUnusedInSelectedFolder = EditorGUILayout.ToggleLeft(new GUIContent("Show only unused in selected folder", "Narrow the unused list to a folder so cleanup can happen in batches."), _showOnlyUnusedInSelectedFolder);

                using (new EditorGUI.DisabledScope(!_showOnlyUnusedInSelectedFolder))
                {
                    DrawFolderField("Unused Filter Folder", ref _unusedFilterFolderAsset, ref _unusedFilterFolderPath, "Assets");
                    _unusedFilterIncludeSubfolders = EditorGUILayout.ToggleLeft(new GUIContent("Include subfolders", "Include child folders in the unused-results filter."), _unusedFilterIncludeSubfolders);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                    MarkResultCacheDirty();
                }

                using (new EditorGUI.DisabledScope(_cachedFilteredUnusedTerrainPaths.Count == 0))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Select All Filtered Unused", UtilityWindowTheme.Amber, GUILayout.Height(24f)))
                            SelectUnusedAssets(_cachedFilteredUnusedTerrainPaths);

                        if (UtilityWindowTheme.TintedButton("Select All Unused In Same Folder", UtilityWindowTheme.Neutral, GUILayout.Height(24f)))
                            SelectAllUnusedInSameFolder();
                    }
                }
            }
        }

        private void DrawGeneralOptionsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.14f, 0.07f)))
            {
                UtilityWindowTheme.SectionTitle("General Options", UtilityWindowTheme.Neutral);

                EditorGUI.BeginChangeCheck();
                _includeDisabledGameObjects = EditorGUILayout.ToggleLeft(new GUIContent("Include disabled GameObjects while scanning scenes", "Uses GetComponentsInChildren<Terrain>(true) so inactive Terrain objects are counted."), _includeDisabledGameObjects);
                _autoPingSelection = EditorGUILayout.ToggleLeft(new GUIContent("Ping selected asset when clicking result rows", "Pings the asset in the Project window when selecting from the results list."), _autoPingSelection);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }
        }

        private void DrawStatusPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.12f, 0.06f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    if (_lastScanTimeSeconds > 0d)
                        UtilityWindowTheme.CountPill($"{_lastScanTimeSeconds:F2}s", UtilityWindowTheme.Purple);
                }

                EditorGUILayout.Space(4f);
                PungentScanGUI.DrawResultHeader(_scanSession.Result, _resultSourceBanner);
                DrawTimingDiagnostics();
            }
        }

        private void DrawTimingDiagnostics()
        {
            if (!TerrainUsageScanService.TryGetLastDiagnostics(out TerrainUsageScanDiagnostics diagnostics) || diagnostics == null)
                return;

            _diagnosticsExpanded = EditorGUILayout.Foldout(_diagnosticsExpanded, "Timing Diagnostics", true);
            if (!_diagnosticsExpanded)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"{diagnostics.totalDurationSeconds:F2}s total", UtilityWindowTheme.Purple, 96f);
                    UtilityWindowTheme.CountPill($"{diagnostics.scenesOpened} opened", UtilityWindowTheme.Blue, 82f);
                    UtilityWindowTheme.CountPill($"{diagnostics.scenesSkipped} skipped", UtilityWindowTheme.Amber, 82f);
                    if (diagnostics.scenesSkippedByDependencyPrefilter > 0)
                        UtilityWindowTheme.CountPill($"{diagnostics.scenesSkippedByDependencyPrefilter} dependency-skipped", UtilityWindowTheme.Teal, 142f);
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField(
                    $"Discovered {diagnostics.scenesDiscovered} scene(s). Average scene open {diagnostics.averageSceneOpenDurationSeconds:F2}s. Slowest scene: {ShortenPath(diagnostics.slowestScenePath, 96)} ({diagnostics.slowestSceneDurationSeconds:F2}s).",
                    UtilityWindowTheme.MutedMiniLabelStyle);
                if (diagnostics.resumed)
                    EditorGUILayout.LabelField($"Before pause: {diagnostics.durationBeforePauseSeconds:F2}s. After resume: {diagnostics.durationAfterResumeSeconds:F2}s.", UtilityWindowTheme.MutedMiniLabelStyle);
                if (diagnostics.restoreDurationSeconds > 0d)
                    EditorGUILayout.LabelField($"Scene setup restore: {diagnostics.restoreDurationSeconds:F2}s.", UtilityWindowTheme.MutedMiniLabelStyle);

                int visibleRows = Mathf.Min(5, diagnostics.sceneTimings.Count);
                for (int i = 0; i < visibleRows; i++)
                {
                    TerrainUsageSceneTiming timing = diagnostics.sceneTimings[i];
                    EditorGUILayout.LabelField(
                        $"{timing.disposition}: {ShortenPath(timing.scenePath, 90)} | open {timing.sceneOpenDurationSeconds:F2}s | scan {timing.terrainScanDurationSeconds:F2}s | prefilter {timing.dependencyPrefilterDurationSeconds:F2}s | total {timing.totalSceneStepDurationSeconds:F2}s",
                        UtilityWindowTheme.PathLabelStyle);
                }
            }
        }

        private void HydrateLatestScanResult()
        {
            if (!PungentScanCache.TryHydrateSession(_scanSession, out _resultSourceBanner))
                return;

            if (_scanSession.Result != null)
            {
                _status = _scanSession.Result.StatusMessage;
                _lastScanTimeSeconds = _scanSession.Result.DurationSeconds;
            }
            RebuildUnusedListFromSharedResult();
        }

        private void RebuildUnusedListFromSharedResult()
        {
            if (_scanSession.Result == null || _scanSession.Result.Issues == null)
                return;
            _unusedTerrainDataPaths.Clear();
            for (int i = 0; i < _scanSession.Result.Issues.Count; i++)
            {
                PungentScanIssue issue = _scanSession.Result.Issues[i];
                if (issue == null || string.IsNullOrWhiteSpace(issue.Path))
                    continue;
                if (string.Equals(issue.Code, "TERRAIN_USAGE_UNUSED_ASSET", StringComparison.OrdinalIgnoreCase))
                    _unusedTerrainDataPaths.Add(issue.Path);
            }
            _unusedTerrainDataPaths.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void DrawFolderField(string label, ref DefaultAsset folderAsset, ref string folderPath, string fallbackPath)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);

                var newFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(folderAsset, typeof(DefaultAsset), false);
                if (newFolderAsset != folderAsset)
                {
                    folderAsset = newFolderAsset;
                    if (folderAsset != null)
                    {
                        string selectedPath = AssetDatabase.GetAssetPath(folderAsset);
                        if (AssetDatabase.IsValidFolder(selectedPath))
                            folderPath = selectedPath;
                        else
                            folderAsset = null;
                    }
                }

                if (GUILayout.Button("Use Assets", GUILayout.Width(90f)))
                {
                    folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
                    folderPath = "Assets";
                }
            }

            folderPath = NormalizeFolderPath(folderPath, fallbackPath);
            EditorGUILayout.LabelField("Path", folderPath, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawUsedPanel(params GUILayoutOption[] layoutOptions)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green, 0.14f, 0.07f), layoutOptions))
            {
                UtilityWindowTheme.SectionTitle("Used TerrainData Assets", UtilityWindowTheme.Green, $"{_cachedUsedTerrainPaths.Count} visible");
                EditorGUILayout.Space(4f);

                _usedScroll = EditorGUILayout.BeginScrollView(_usedScroll);

                if (_usedTerrainDataByAssetPath.Count == 0)
                {
                    EditorGUILayout.HelpBox("No used TerrainData assets found yet. Run a scan.", MessageType.None);
                }
                else if (_cachedUsedTerrainPaths.Count == 0)
                {
                    EditorGUILayout.HelpBox("No used TerrainData assets match the active search.", MessageType.None);
                }
                else
                {
                    foreach (string assetPath in _cachedUsedTerrainPaths)
                    {
                        List<TerrainUseRecord> uses = _usedTerrainDataByAssetPath[assetPath];
                        DrawAssetHeader(assetPath, isUnused: false, useCount: uses.Count);

                        EditorGUI.indentLevel++;
                        foreach (TerrainUseRecord use in uses.OrderBy(u => u.scenePath, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.terrainObjectPath, StringComparer.OrdinalIgnoreCase))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField($"Scene: {use.sceneName}", GUILayout.Width(220f));
                                EditorGUILayout.SelectableLabel(use.terrainObjectPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                            }
                        }
                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space(6f);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawUnusedPanel(params GUILayoutOption[] layoutOptions)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.14f, 0.07f), layoutOptions))
            {
                string label = _showOnlyUnusedInSelectedFolder || !string.IsNullOrWhiteSpace(_resultSearch)
                    ? $"{_cachedFilteredUnusedTerrainPaths.Count} visible / {_unusedTerrainDataPaths.Count} total"
                    : $"{_unusedTerrainDataPaths.Count} total";

                UtilityWindowTheme.SectionTitle("Unused TerrainData Assets", UtilityWindowTheme.Amber, label);
                EditorGUILayout.Space(4f);

                using (new EditorGUI.DisabledScope(_cachedFilteredUnusedTerrainPaths.Count == 0))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Select All Visible", UtilityWindowTheme.Amber, GUILayout.Width(140f)))
                            SelectUnusedAssets(_cachedFilteredUnusedTerrainPaths);

                        if (UtilityWindowTheme.TintedButton("Select Same Folder", UtilityWindowTheme.Neutral, GUILayout.Width(140f)))
                            SelectAllUnusedInSameFolder();

                        GUILayout.FlexibleSpace();
                    }
                }

                EditorGUILayout.Space(4f);
                _unusedScroll = EditorGUILayout.BeginScrollView(_unusedScroll);

                if (_cachedFilteredUnusedTerrainPaths.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        _unusedTerrainDataPaths.Count == 0
                            ? "No unused TerrainData assets found yet, or a scan has not been run."
                            : "No unused TerrainData assets match the active folder/search filter.",
                        MessageType.None);
                }
                else
                {
                    foreach (string assetPath in _cachedFilteredUnusedTerrainPaths)
                    {
                        DrawAssetHeader(assetPath, isUnused: true, useCount: 0);
                        EditorGUILayout.Space(6f);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssetHeader(string assetPath, bool isUnused, int useCount)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIStyle labelStyle = new GUIStyle(UtilityWindowTheme.CardLabelStyle)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };

                if (GUILayout.Button(asset != null ? AssetPreview.GetMiniThumbnail(asset) : null, GUILayout.Width(22f), GUILayout.Height(18f)))
                    SelectAndPing(assetPath);

                if (GUILayout.Button(assetPath, labelStyle, GUILayout.ExpandWidth(true)))
                    SelectAndPing(assetPath);

                if (!isUnused)
                    UtilityWindowTheme.CountPill($"Uses: {useCount}", UtilityWindowTheme.Green, 70f);

                if (asset != null && GUILayout.Button("Select", GUILayout.Width(60f)))
                    SelectAndPing(assetPath);

                if (GUILayout.Button("Copy", GUILayout.Width(54f)))
                {
                    EditorGUIUtility.systemCopyBuffer = assetPath;
                    _status = "Copied asset path.";
                }

                if (isUnused && asset != null)
                {
                    if (GUILayout.Button("Same Folder", GUILayout.Width(90f)))
                        SelectUnusedInFolder(GetParentFolder(assetPath), includeSubfolders: false);

                    if (GUILayout.Button("Reveal", GUILayout.Width(60f)))
                        RevealProjectAsset(assetPath);
                }
            }
        }

        private void SelectAndPing(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            if (_autoPingSelection)
                EditorGUIUtility.PingObject(asset);
        }

        private void RevealProjectAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private void SelectUnusedAssets(IReadOnlyList<string> assetPaths)
        {
            UnityEngine.Object[] assets = assetPaths
                .Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>)
                .Where(obj => obj != null)
                .ToArray();

            Selection.objects = assets;
            _status = $"Selected {assets.Length} unused TerrainData asset(s).";

            if (_autoPingSelection && assets.Length > 0)
                EditorGUIUtility.PingObject(assets[0]);
        }

        private void SelectAllUnusedInSameFolder()
        {
            string folderPath = NormalizeFolderPath(_unusedFilterFolderPath, "Assets");
            SelectUnusedInFolder(folderPath, _unusedFilterIncludeSubfolders);
        }

        private void SelectUnusedInFolder(string folderPath, bool includeSubfolders)
        {
            List<string> matches = _unusedTerrainDataPaths
                .Where(path => IsPathWithinFolderScope(path, folderPath, includeSubfolders))
                .Where(MatchesResultSearch)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SelectUnusedAssets(matches);
        }

        private void ClearResults()
        {
            ClearResultCollections();
            _scanSession.Clear();
            _status = "Idle";
            _lastScanTimeSeconds = 0d;
            MarkResultCacheDirty();
            Repaint();
        }

        private void ClearResultCollections()
        {
            _usedTerrainDataByAssetPath.Clear();
            _allTerrainDataPaths.Clear();
            _unusedTerrainDataPaths.Clear();
            _scenePathsScanned.Clear();
            _cachedUsedTerrainPaths.Clear();
            _cachedFilteredUnusedTerrainPaths.Clear();
        }

        private void QueueScan()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Scan();
            };
        }

        private void Scan()
        {
            if (_isScanning)
                return;

            _isScanning = true;
            ClearResultCollections();
            _status = "Preparing terrain usage scan...";

            DateTime start = DateTime.UtcNow;
            PungentScanResult scanResult = _scanSession.Begin(GetCurrentScanScope(), GetCurrentScanScopeLabel());

            try
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    _status = "Scan cancelled before scene changes were saved.";
                    _scanSession.Cancel(_status);
                    return;
                }

                List<string> scenePathsToScan = GetScenePathsToScan();
                List<string> terrainDataPathsToConsider = GetTerrainDataPathsToConsider();
                int scenesSkippedByDependencyPrefilter = ApplyWindowTerrainDependencyPrefilter(scenePathsToScan, terrainDataPathsToConsider, scanResult);

                _allTerrainDataPaths.AddRange(terrainDataPathsToConsider);

                if (scenePathsToScan.Count == 0)
                {
                    scanResult.AddIssue(
                        PungentScanSeverity.Warning,
                        "No scenes found",
                        "The active scene scan scope did not resolve any Scene assets. Check the scene folder and package-skip options.",
                        null,
                        _scanScenesProjectWide ? string.Empty : _sceneFolderPath,
                        "TERRAIN_USAGE_NO_SCENES");
                }

                if (terrainDataPathsToConsider.Count == 0)
                {
                    scanResult.AddIssue(
                        PungentScanSeverity.Warning,
                        "No TerrainData assets found",
                        "The active TerrainData asset scope did not resolve any TerrainData assets. Check the terrain folder and package-skip options.",
                        null,
                        _scanTerrainAssetsProjectWide ? string.Empty : _terrainFolderPath,
                        "TERRAIN_USAGE_NO_TERRAIN_DATA");
                }

                SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
                try
                {
                    for (int i = 0; i < scenePathsToScan.Count; i++)
                    {
                        string scenePath = scenePathsToScan[i];
                        float progress = (i + 1f) / Mathf.Max(1, scenePathsToScan.Count);
                        bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                            "Scanning Scenes For Terrain Usage",
                            $"Scanning {scenePath}",
                            progress);

                        if (cancelled)
                        {
                            _status = $"Scan cancelled after {i} scene(s).";
                            scanResult.AddIssue(
                                PungentScanSeverity.Info,
                                "Scan cancelled",
                                _status,
                                null,
                                scenePath,
                                "TERRAIN_USAGE_CANCELLED");
                            _scanSession.Cancel(_status);
                            return;
                        }

                        _scenePathsScanned.Add(scenePath);
                        ScanScene(scenePath, scanResult);
                    }

                    _unusedTerrainDataPaths.AddRange(
                        _allTerrainDataPaths
                            .Where(path => !_usedTerrainDataByAssetPath.ContainsKey(path))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

                    AddTerrainUsageSummaryIssues(scanResult, scenePathsToScan.Count, terrainDataPathsToConsider.Count);
                    AddUnusedTerrainAssetIssues(scanResult);

                    _status =
                        $"Scan complete. Scenes scanned: {scenePathsToScan.Count}, " +
                        $"Terrain candidates: {_allTerrainDataPaths.Count}, " +
                        $"Used TerrainData: {_usedTerrainDataByAssetPath.Count}, " +
                        $"Unused TerrainData: {_unusedTerrainDataPaths.Count}" +
                        (scenesSkippedByDependencyPrefilter > 0 ? $", Dependency-skipped scenes: {scenesSkippedByDependencyPrefilter}" : string.Empty);

                    int totalScanned = scenePathsToScan.Count + terrainDataPathsToConsider.Count;
                    _scanSession.Complete(totalScanned, _usedTerrainDataByAssetPath.Count, _unusedTerrainDataPaths.Count, 0, _status);
                }
                finally
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                    EditorUtility.ClearProgressBar();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _status = $"Scan failed: {ex.Message}";
                _scanSession.Fail(ex, _status);
            }
            finally
            {
                _lastScanTimeSeconds = (DateTime.UtcNow - start).TotalSeconds;
                _isScanning = false;
                MarkResultCacheDirty();
                Repaint();
            }
        }

        private PungentScanScope GetCurrentScanScope()
        {
            if (_scanScenesProjectWide && _scanTerrainAssetsProjectWide)
                return PungentScanScope.ProjectAssets;

            if (_scanScenesProjectWide)
                return PungentScanScope.ProjectScenes;

            return PungentScanScope.Custom;
        }

        private string GetCurrentScanScopeLabel()
        {
            string sceneScope = _scanScenesProjectWide
                ? (_skipPackageScenes ? "Project scenes" : "Project + package scenes")
                : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);

            string terrainScope = _scanTerrainAssetsProjectWide
                ? (_skipPackageTerrainAssets ? "Project TerrainData" : "Project + package TerrainData")
                : GetScopeDescription(_terrainFolderPath, _includeTerrainSubfolders);

            return sceneScope + " / " + terrainScope;
        }

        private void AddTerrainUsageSummaryIssues(PungentScanResult scanResult, int sceneCount, int terrainCandidateCount)
        {
            if (scanResult == null)
                return;

            if (_usedTerrainDataByAssetPath.Count == 0 && sceneCount > 0)
            {
                scanResult.AddIssue(
                    PungentScanSeverity.Info,
                    "No scene TerrainData references found",
                    "The scanned scenes did not reference any TerrainData assets in the active scope.",
                    null,
                    null,
                    "TERRAIN_USAGE_NO_USED_REFERENCES");
            }

            if (_unusedTerrainDataPaths.Count > 0)
            {
                scanResult.AddIssue(
                    PungentScanSeverity.Warning,
                    "Unused TerrainData assets found",
                    $"{_unusedTerrainDataPaths.Count} TerrainData asset(s) in the selected asset scope were not referenced by scanned scene Terrain components.",
                    null,
                    null,
                    "TERRAIN_USAGE_UNUSED_ASSETS");
            }
            else if (terrainCandidateCount > 0)
            {
                scanResult.AddIssue(
                    PungentScanSeverity.Success,
                    "No unused TerrainData assets found",
                    "Every TerrainData asset in the selected asset scope was referenced by at least one scanned scene Terrain component.",
                    null,
                    null,
                    "TERRAIN_USAGE_NO_UNUSED_ASSETS");
            }
        }

        private void AddUnusedTerrainAssetIssues(PungentScanResult scanResult)
        {
            if (scanResult == null || _unusedTerrainDataPaths.Count == 0)
                return;

            for (int i = 0; i < _unusedTerrainDataPaths.Count; i++)
            {
                string assetPath = _unusedTerrainDataPaths[i];
                scanResult.AddIssue(
                    PungentScanSeverity.Warning,
                    "Unused TerrainData asset",
                    "This TerrainData asset was not referenced by scanned scene Terrain components.",
                    null,
                    assetPath,
                    "TERRAIN_USAGE_UNUSED_ASSET");
            }
        }

        private List<string> GetScenePathsToScan()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

            IEnumerable<string> scenePaths = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path));

            if (_skipPackageScenes)
                scenePaths = scenePaths.Where(path => !IsPackagePath(path));

            if (!_scanScenesProjectWide)
            {
                string folder = NormalizeFolderPath(_sceneFolderPath, "Assets");
                scenePaths = scenePaths.Where(path => IsPathWithinFolderScope(path, folder, _includeSceneSubfolders));
            }

            return scenePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetTerrainDataPathsToConsider()
        {
            string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData");

            IEnumerable<string> terrainPaths = terrainDataGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path));

            if (_skipPackageTerrainAssets)
                terrainPaths = terrainPaths.Where(path => !IsPackagePath(path));

            if (!_scanTerrainAssetsProjectWide)
            {
                string folder = NormalizeFolderPath(_terrainFolderPath, "Assets");
                terrainPaths = terrainPaths.Where(path => IsPathWithinFolderScope(path, folder, _includeTerrainSubfolders));
            }

            return terrainPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private int ApplyWindowTerrainDependencyPrefilter(List<string> scenePaths, List<string> terrainDataPaths, PungentScanResult scanResult)
        {
            if (!_prefilterScenesByTerrainDataDependencies ||
                scenePaths == null ||
                scenePaths.Count == 0 ||
                terrainDataPaths == null ||
                terrainDataPaths.Count == 0)
            {
                return 0;
            }

            HashSet<string> terrainCandidatePaths = new HashSet<string>(terrainDataPaths, StringComparer.OrdinalIgnoreCase);
            int skipped = scenePaths.RemoveAll(path => !SceneHasTerrainDataDependency(path, terrainCandidatePaths));
            if (skipped > 0)
            {
                scanResult?.AddIssue(
                    PungentScanSeverity.Info,
                    "Scenes skipped by TerrainData dependency prefilter",
                    skipped + " scene(s) were skipped because their serialized dependencies did not include TerrainData in the active scope. Disable the prefilter for exhaustive verification.",
                    null,
                    null,
                    "TERRAIN_USAGE_SCENE_DEPENDENCY_PREFILTER");
            }
            return skipped;
        }

        private IReadOnlyList<string> GetFilteredUnusedTerrainPaths()
        {
            RebuildResultCachesIfNeeded();
            return _cachedFilteredUnusedTerrainPaths;
        }

        private void MarkResultCacheDirty()
        {
            _resultCacheDirty = true;
        }

        private void RebuildResultCachesIfNeeded()
        {
            if (!_resultCacheDirty)
                return;

            _cachedUsedTerrainPaths.Clear();
            _cachedFilteredUnusedTerrainPaths.Clear();

            IEnumerable<string> usedPaths = _usedTerrainDataByAssetPath.Keys;
            if (!string.IsNullOrWhiteSpace(_resultSearch))
                usedPaths = usedPaths.Where(path => MatchesResultSearch(path) || _usedTerrainDataByAssetPath[path].Any(MatchesResultSearch));

            _cachedUsedTerrainPaths.AddRange(usedPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

            IEnumerable<string> unusedPaths = _unusedTerrainDataPaths;
            if (_showOnlyUnusedInSelectedFolder)
            {
                string folder = NormalizeFolderPath(_unusedFilterFolderPath, "Assets");
                unusedPaths = unusedPaths.Where(path => IsPathWithinFolderScope(path, folder, _unusedFilterIncludeSubfolders));
            }

            if (!string.IsNullOrWhiteSpace(_resultSearch))
                unusedPaths = unusedPaths.Where(MatchesResultSearch);

            _cachedFilteredUnusedTerrainPaths.AddRange(
                unusedPaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

            _resultCacheDirty = false;
        }

        private bool MatchesResultSearch(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(_resultSearch))
                return true;

            if (string.IsNullOrEmpty(assetPath))
                return false;

            return assetPath.IndexOf(_resultSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesResultSearch(TerrainUseRecord record)
        {
            if (string.IsNullOrWhiteSpace(_resultSearch))
                return true;

            string query = _resultSearch.Trim();
            return (!string.IsNullOrEmpty(record.scenePath) && record.scenePath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrEmpty(record.sceneName) && record.sceneName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrEmpty(record.terrainObjectPath) && record.terrainObjectPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ScanScene(string scenePath, PungentScanResult scanResult)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scanResult?.AddIssue(
                    PungentScanSeverity.Warning,
                    "Scene could not be loaded",
                    "The scene was skipped because Unity did not load it successfully.",
                    null,
                    scenePath,
                    "TERRAIN_USAGE_SCENE_LOAD_FAILED");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Terrain[] terrains = root.GetComponentsInChildren<Terrain>(_includeDisabledGameObjects);
                foreach (Terrain terrain in terrains)
                {
                    if (terrain == null)
                        continue;

                    TerrainData terrainData = terrain.terrainData;
                    if (terrainData == null)
                    {
                        scanResult?.AddIssue(
                            PungentScanSeverity.Warning,
                            "Terrain has no TerrainData",
                            $"Terrain component '{GetHierarchyPath(terrain.transform)}' has no TerrainData assigned.",
                            terrain,
                            scenePath,
                            "TERRAIN_USAGE_MISSING_TERRAIN_DATA");
                        continue;
                    }

                    string terrainDataPath = AssetDatabase.GetAssetPath(terrainData);
                    if (string.IsNullOrEmpty(terrainDataPath))
                    {
                        scanResult?.AddIssue(
                            PungentScanSeverity.Info,
                            "Runtime or unsaved TerrainData",
                            $"Terrain component '{GetHierarchyPath(terrain.transform)}' references TerrainData with no AssetDatabase path.",
                            terrain,
                            scenePath,
                            "TERRAIN_USAGE_UNSAVED_TERRAIN_DATA");
                        continue;
                    }

                    if (_skipPackageTerrainAssets && IsPackagePath(terrainDataPath))
                        continue;

                    if (!_usedTerrainDataByAssetPath.TryGetValue(terrainDataPath, out List<TerrainUseRecord> list))
                    {
                        list = new List<TerrainUseRecord>();
                        _usedTerrainDataByAssetPath.Add(terrainDataPath, list);
                    }

                    list.Add(new TerrainUseRecord
                    {
                        scenePath = scenePath,
                        sceneName = scene.name,
                        terrainObjectPath = GetHierarchyPath(terrain.transform),
                        terrainComponent = terrain
                    });
                }
            }
        }

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Terrain Usage CSV", Application.dataPath, "terrain_usage_report.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Status,TerrainDataPath,ScenePath,SceneName,TerrainObjectPath,UseCount");

                foreach (string assetPath in _usedTerrainDataByAssetPath.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    List<TerrainUseRecord> uses = _usedTerrainDataByAssetPath[assetPath];
                    foreach (TerrainUseRecord use in uses.OrderBy(u => u.scenePath, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.terrainObjectPath, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine(string.Join(",",
                            Csv("Used"),
                            Csv(assetPath),
                            Csv(use.scenePath),
                            Csv(use.sceneName),
                            Csv(use.terrainObjectPath),
                            Csv(uses.Count.ToString())));
                    }
                }

                foreach (string assetPath in _unusedTerrainDataPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine(string.Join(",",
                        Csv("Unused"),
                        Csv(assetPath),
                        Csv(string.Empty),
                        Csv(string.Empty),
                        Csv(string.Empty),
                        Csv("0")));
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                _status = $"CSV exported: {path}";
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _status = $"CSV export failed: {ex.Message}";
            }
        }

        private static string Csv(string value)
        {
            if (value == null)
                value = string.Empty;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string ShortenPath(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value ?? string.Empty;
            return "..." + value.Substring(Mathf.Max(0, value.Length - Math.Max(4, maxLength - 3)));
        }

        private static bool SceneHasTerrainDataDependency(string scenePath, HashSet<string> terrainCandidatePaths)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || terrainCandidatePaths == null || terrainCandidatePaths.Count == 0)
                return false;

            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (terrainCandidatePaths.Contains(dependencies[i]))
                    return true;
            }

            return false;
        }

        private static string NormalizeFolderPath(string path, string fallbackPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallbackPath;

            path = path.Replace("\\", "/").Trim().TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(path))
                return fallbackPath;

            return path;
        }

        private static string GetParentFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";

            assetPath = assetPath.Replace("\\", "/");
            int lastSlash = assetPath.LastIndexOf('/');
            if (lastSlash <= 0)
                return "Assets";

            string folder = assetPath.Substring(0, lastSlash);
            return AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
        }

        private static bool IsPackagePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.Replace("\\", "/").StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinFolderScope(string assetPath, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath))
                return false;

            assetPath = assetPath.Replace("\\", "/").Trim();
            folderPath = folderPath.Replace("\\", "/").Trim().TrimEnd('/');

            if (includeSubfolders)
            {
                return assetPath.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(assetPath, folderPath, StringComparison.OrdinalIgnoreCase);
            }

            string parentFolder = GetParentFolder(assetPath);
            return string.Equals(parentFolder, folderPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetScopeDescription(string folderPath, bool includeSubfolders)
        {
            string normalized = NormalizeFolderPath(folderPath, "Assets");
            return includeSubfolders ? $"{normalized} (including subfolders)" : $"{normalized} (top-level only)";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            Stack<string> names = new Stack<string>();
            Transform current = transform;

            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private void LoadPrefs()
        {
            _includeDisabledGameObjects = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeDisabled", _includeDisabledGameObjects);
            _autoPingSelection = UtilityWindowPrefs.GetBool(PrefPrefix + "AutoPing", _autoPingSelection);
            _scanScenesProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanScenesProjectWide", _scanScenesProjectWide);
            _sceneFolderPath = UtilityWindowPrefs.GetString(PrefPrefix + "SceneFolderPath", _sceneFolderPath);
            _skipPackageScenes = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageScenes", _skipPackageScenes);
            _includeSceneSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeSceneSubfolders", _includeSceneSubfolders);
            _prefilterScenesByTerrainDataDependencies = UtilityWindowPrefs.GetBool(PrefPrefix + "PrefilterScenesByTerrainDataDependencies", _prefilterScenesByTerrainDataDependencies);
            _scanTerrainAssetsProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanTerrainProjectWide", _scanTerrainAssetsProjectWide);
            _terrainFolderPath = UtilityWindowPrefs.GetString(PrefPrefix + "TerrainFolderPath", _terrainFolderPath);
            _skipPackageTerrainAssets = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageTerrain", _skipPackageTerrainAssets);
            _includeTerrainSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeTerrainSubfolders", _includeTerrainSubfolders);
            _showOnlyUnusedInSelectedFolder = UtilityWindowPrefs.GetBool(PrefPrefix + "FilterUnusedByFolder", _showOnlyUnusedInSelectedFolder);
            _unusedFilterFolderPath = UtilityWindowPrefs.GetString(PrefPrefix + "UnusedFilterFolder", _unusedFilterFolderPath);
            _unusedFilterIncludeSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "UnusedFilterSubfolders", _unusedFilterIncludeSubfolders);
            _resultSearch = UtilityWindowPrefs.GetString(PrefPrefix + "ResultSearch", _resultSearch);
            _scopePanelHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "ScopePanelHeight", _scopePanelHeight);
            _usedPanelWidth = UtilityWindowPrefs.GetFloat(PrefPrefix + "UsedPanelWidth", _usedPanelWidth);

            _sceneFolderPath = NormalizeFolderPath(_sceneFolderPath, "Assets");
            _terrainFolderPath = NormalizeFolderPath(_terrainFolderPath, "Assets");
            _unusedFilterFolderPath = NormalizeFolderPath(_unusedFilterFolderPath, "Assets");
            _sceneFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_sceneFolderPath);
            _terrainFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_terrainFolderPath);
            _unusedFilterFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_unusedFilterFolderPath);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeDisabled", _includeDisabledGameObjects);
            UtilityWindowPrefs.SetBool(PrefPrefix + "AutoPing", _autoPingSelection);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ScanScenesProjectWide", _scanScenesProjectWide);
            UtilityWindowPrefs.SetString(PrefPrefix + "SceneFolderPath", _sceneFolderPath);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SkipPackageScenes", _skipPackageScenes);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeSceneSubfolders", _includeSceneSubfolders);
            UtilityWindowPrefs.SetBool(PrefPrefix + "PrefilterScenesByTerrainDataDependencies", _prefilterScenesByTerrainDataDependencies);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ScanTerrainProjectWide", _scanTerrainAssetsProjectWide);
            UtilityWindowPrefs.SetString(PrefPrefix + "TerrainFolderPath", _terrainFolderPath);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SkipPackageTerrain", _skipPackageTerrainAssets);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeTerrainSubfolders", _includeTerrainSubfolders);
            UtilityWindowPrefs.SetBool(PrefPrefix + "FilterUnusedByFolder", _showOnlyUnusedInSelectedFolder);
            UtilityWindowPrefs.SetString(PrefPrefix + "UnusedFilterFolder", _unusedFilterFolderPath);
            UtilityWindowPrefs.SetBool(PrefPrefix + "UnusedFilterSubfolders", _unusedFilterIncludeSubfolders);
            UtilityWindowPrefs.SetString(PrefPrefix + "ResultSearch", _resultSearch);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "ScopePanelHeight", _scopePanelHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "UsedPanelWidth", _usedPanelWidth);
        }
    }

    internal sealed class TerrainUsageAuditProvider : IPungentAuditScanProvider, IPungentSceneScanProvider
    {
        public string ProviderId => "terrain-usage-scanner";
        public string DisplayName => "Terrain Usage Scanner";
        public string Description => "Checks TerrainData usage and reports unused TerrainData assets.";
        public string OpenButtonLabel => "Open Terrain Scanner";
        public string RunButtonLabel => "Run Terrain Scan";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => true;
        public bool CanRunFromCoordinator => true;
        public bool CanPause => true;
        public bool CanCancel => true;
        public bool UsesSceneOpening => true;
        public bool UsesAssetDatabase => true;
        public bool UsesModalProgress => false;
        public bool IsCooperative => true;
        public bool IsMonolithic => false;
        public bool IsScanOnly => true;

        public bool TryGetNotConfiguredReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            return TerrainUsageScannerWindow.CreateAuditJob(mode);
        }

        public bool TryCreateSceneScanBinding(PungentAuditScanMode mode, out PungentSceneScanProviderBinding binding)
        {
            binding = new PungentSceneScanProviderBinding
            {
                ProviderId = ProviderId,
                DisplayName = DisplayName,
                CreateAnalyzer = scanMode => new TerrainUsageSceneScanAnalyzer(scanMode)
            };
            return mode == PungentAuditScanMode.Immediate || mode == PungentAuditScanMode.BackgroundIdle;
        }

        public void OpenWindow()
        {
            TerrainUsageScannerWindow.ShowWindow();
        }
    }

    internal sealed class TerrainUsageSceneScanAnalyzer : IPungentSceneScanAnalyzer
    {
        private const string PrefPrefix = "GenericUtilities.TerrainUsageScanner.";

        private struct TerrainUseRecord
        {
            public string scenePath;
            public string sceneName;
            public string terrainObjectPath;
        }

        private readonly PungentAuditScanMode _mode;
        private readonly Dictionary<string, List<TerrainUseRecord>> _usedTerrainDataByAssetPath = new Dictionary<string, List<TerrainUseRecord>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _allTerrainDataPaths = new List<string>();
        private readonly List<string> _unusedTerrainDataPaths = new List<string>();
        private readonly HashSet<string> _terrainCandidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly PungentScanSession _session = new PungentScanSession("terrain-usage-scanner", "Terrain Usage Scanner");
        private readonly TerrainUsageScanDiagnostics _diagnostics = new TerrainUsageScanDiagnostics();
        private bool _includeDisabledGameObjects = true;
        private bool _scanScenesProjectWide = true;
        private string _sceneFolderPath = "Assets";
        private bool _skipPackageScenes = true;
        private bool _includeSceneSubfolders = true;
        private bool _prefilterScenesByTerrainDataDependencies = false;
        private bool _scanTerrainAssetsProjectWide = true;
        private string _terrainFolderPath = "Assets";
        private bool _skipPackageTerrainAssets = true;
        private bool _includeTerrainSubfolders = true;
        private PungentScanResult _result;
        private int _scenesScanned;
        private int _scenesSkippedByDependencyPrefilter;

        public TerrainUsageSceneScanAnalyzer(PungentAuditScanMode mode)
        {
            _mode = mode;
            LoadConfig();
        }

        public string ProviderId => "terrain-usage-scanner";
        public string DisplayName => "Terrain Usage Scanner";

        public void Begin(PungentSceneScanBatchContext context)
        {
            _result = _session.Begin(GetScanScope(), GetScanScopeLabel());
            _result.StatusMessage = "Terrain Usage is running through the shared scene scan pass.";
            _diagnostics.dependencyPrefilterEnabled = _prefilterScenesByTerrainDataDependencies;
            _diagnostics.completedUtc = DateTime.UtcNow;
            _diagnostics.scenesDiscovered = context != null && context.SceneTargets != null ? context.SceneTargets.Count : 0;
            GatherTerrainDataAssets();
            _terrainCandidatePaths.Clear();
            for (int i = 0; i < _allTerrainDataPaths.Count; i++)
                _terrainCandidatePaths.Add(_allTerrainDataPaths[i]);
            if (_allTerrainDataPaths.Count == 0)
                _result.AddIssue(PungentScanSeverity.Warning, "No TerrainData assets found", "The active TerrainData asset scope did not resolve any TerrainData assets.", null, _scanTerrainAssetsProjectWide ? string.Empty : _terrainFolderPath, "TERRAIN_USAGE_NO_TERRAIN_DATA");
        }

        public bool ShouldAnalyzeScene(PungentSceneScanTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Path))
                return false;
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return target.LoadedScene.IsValid() && target.LoadedScene.isLoaded;
            if (!_prefilterScenesByTerrainDataDependencies || _allTerrainDataPaths.Count == 0)
                return true;
            return SceneHasTerrainDataDependency(target.Path, _terrainCandidatePaths);
        }

        public void RecordSkippedScene(PungentSceneScanTarget target, string reason)
        {
            if (_prefilterScenesByTerrainDataDependencies && _mode != PungentAuditScanMode.BackgroundIdle && target != null)
            {
                _scenesSkippedByDependencyPrefilter++;
                _diagnostics.scenesSkipped++;
                _diagnostics.sceneTimings.Add(new TerrainUsageSceneTiming
                {
                    scenePath = target.Path,
                    sceneName = target.Name,
                    disposition = "Dependency-prefiltered"
                });
            }
        }

        public void AnalyzeScene(PungentSceneScanContext context)
        {
            Stopwatch scanStopwatch = Stopwatch.StartNew();
            bool scanned = false;
            try
            {
                if (context == null || !context.Scene.IsValid() || !context.Scene.isLoaded)
                {
                    string path = context != null && context.Target != null ? context.Target.Path : string.Empty;
                    _result.AddIssue(PungentScanSeverity.Warning, "Scene could not be loaded", "The scene was skipped because Unity did not load it successfully.", null, path, "TERRAIN_USAGE_SCENE_LOAD_FAILED");
                    _diagnostics.scenesSkipped++;
                    return;
                }

                string scenePath = context.Target != null ? context.Target.Path : context.Scene.path;
                string sceneName = context.Target != null ? context.Target.Name : context.Scene.name;
                GameObject[] roots = context.Scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(_includeDisabledGameObjects);
                    for (int i = 0; i < terrains.Length; i++)
                    {
                        Terrain terrain = terrains[i];
                        if (terrain == null)
                            continue;
                        TerrainData terrainData = terrain.terrainData;
                        if (terrainData == null)
                        {
                            _result.AddIssue(PungentScanSeverity.Warning, "Terrain has no TerrainData", "Terrain component '" + GetHierarchyPath(terrain.transform) + "' has no TerrainData assigned.", terrain, scenePath, "TERRAIN_USAGE_MISSING_TERRAIN_DATA");
                            continue;
                        }

                        string terrainDataPath = AssetDatabase.GetAssetPath(terrainData);
                        if (string.IsNullOrEmpty(terrainDataPath))
                        {
                            _result.AddIssue(PungentScanSeverity.Info, "Runtime or unsaved TerrainData", "Terrain component '" + GetHierarchyPath(terrain.transform) + "' references TerrainData with no AssetDatabase path.", terrain, scenePath, "TERRAIN_USAGE_UNSAVED_TERRAIN_DATA");
                            continue;
                        }
                        if (_skipPackageTerrainAssets && IsPackagePath(terrainDataPath))
                            continue;

                        if (!_usedTerrainDataByAssetPath.TryGetValue(terrainDataPath, out List<TerrainUseRecord> list))
                        {
                            list = new List<TerrainUseRecord>();
                            _usedTerrainDataByAssetPath[terrainDataPath] = list;
                        }
                        list.Add(new TerrainUseRecord
                        {
                            scenePath = scenePath,
                            sceneName = sceneName,
                            terrainObjectPath = GetHierarchyPath(terrain.transform)
                        });
                    }
                }

                scanned = true;
                _scenesScanned++;
            }
            finally
            {
                scanStopwatch.Stop();
                if (context != null && context.Mode != PungentAuditScanMode.BackgroundIdle)
                    _diagnostics.scenesOpened++;
                _diagnostics.sceneTimings.Add(new TerrainUsageSceneTiming
                {
                    scenePath = context != null && context.Target != null ? context.Target.Path : string.Empty,
                    sceneName = context != null && context.Target != null ? context.Target.Name : string.Empty,
                    sceneOpenDurationSeconds = context != null ? context.SceneOpenDurationSeconds : 0d,
                    terrainScanDurationSeconds = scanStopwatch.Elapsed.TotalSeconds,
                    totalSceneStepDurationSeconds = (context != null ? context.SceneOpenDurationSeconds : 0d) + scanStopwatch.Elapsed.TotalSeconds,
                    disposition = scanned ? "Scanned" : "Skipped"
                });
            }
        }

        public PungentScanResult Complete(PungentSceneScanBatchContext context)
        {
            _unusedTerrainDataPaths.AddRange(_allTerrainDataPaths
                .Where(path => !_usedTerrainDataByAssetPath.ContainsKey(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            AddSummaryIssues();
            AddUnusedTerrainAssetIssues();
            if (_scenesSkippedByDependencyPrefilter > 0)
                _result.AddIssue(PungentScanSeverity.Info, "Scenes skipped by TerrainData dependency prefilter", _scenesSkippedByDependencyPrefilter + " scene(s) were skipped for Terrain Usage because their serialized dependencies did not include TerrainData in the active scope.", null, null, "TERRAIN_USAGE_SCENE_DEPENDENCY_PREFILTER");
            string status = "Scan complete. Scenes scanned: " + _scenesScanned +
                            ", Terrain candidates: " + _allTerrainDataPaths.Count +
                            ", Used TerrainData: " + _usedTerrainDataByAssetPath.Count +
                            ", Unused TerrainData: " + _unusedTerrainDataPaths.Count +
                            (_scenesSkippedByDependencyPrefilter > 0 ? ", Dependency-skipped scenes: " + _scenesSkippedByDependencyPrefilter : string.Empty);
            PungentScanResult completed = _session.Complete(_scenesScanned + _allTerrainDataPaths.Count, _usedTerrainDataByAssetPath.Count, _unusedTerrainDataPaths.Count, 0, status);
            CompleteDiagnostics(completed.DurationSeconds);
            return completed;
        }

        public PungentScanResult Cancel(string status)
        {
            return _session.Cancel(status, false);
        }

        public PungentScanResult Fail(Exception exception, string status)
        {
            return _session.Fail(exception, status);
        }

        private void LoadConfig()
        {
            _includeDisabledGameObjects = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeDisabled", _includeDisabledGameObjects);
            _scanScenesProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanScenesProjectWide", _scanScenesProjectWide);
            _sceneFolderPath = NormalizeFolderPath(UtilityWindowPrefs.GetString(PrefPrefix + "SceneFolderPath", _sceneFolderPath), "Assets");
            _skipPackageScenes = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageScenes", _skipPackageScenes);
            _includeSceneSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeSceneSubfolders", _includeSceneSubfolders);
            _prefilterScenesByTerrainDataDependencies = UtilityWindowPrefs.GetBool(PrefPrefix + "PrefilterScenesByTerrainDataDependencies", _prefilterScenesByTerrainDataDependencies);
            _scanTerrainAssetsProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanTerrainProjectWide", _scanTerrainAssetsProjectWide);
            _terrainFolderPath = NormalizeFolderPath(UtilityWindowPrefs.GetString(PrefPrefix + "TerrainFolderPath", _terrainFolderPath), "Assets");
            _skipPackageTerrainAssets = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageTerrain", _skipPackageTerrainAssets);
            _includeTerrainSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeTerrainSubfolders", _includeTerrainSubfolders);
        }

        private void GatherTerrainDataAssets()
        {
            _allTerrainDataPaths.Clear();
            string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData");
            IEnumerable<string> terrainPaths = terrainDataGuids.Select(AssetDatabase.GUIDToAssetPath).Where(path => !string.IsNullOrEmpty(path));
            if (_skipPackageTerrainAssets)
                terrainPaths = terrainPaths.Where(path => !IsPackagePath(path));
            if (!_scanTerrainAssetsProjectWide)
                terrainPaths = terrainPaths.Where(path => IsPathWithinFolderScope(path, _terrainFolderPath, _includeTerrainSubfolders));
            _allTerrainDataPaths.AddRange(terrainPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private void AddSummaryIssues()
        {
            if (_usedTerrainDataByAssetPath.Count == 0 && _scenesScanned > 0)
                _result.AddIssue(PungentScanSeverity.Info, "No scene TerrainData references found", "The scanned scenes did not reference any TerrainData assets in the active scope.", null, null, "TERRAIN_USAGE_NO_USED_REFERENCES");
            if (_unusedTerrainDataPaths.Count > 0)
                _result.AddIssue(PungentScanSeverity.Warning, "Unused TerrainData assets found", _unusedTerrainDataPaths.Count + " TerrainData asset(s) in the selected asset scope were not referenced by scanned scene Terrain components.", null, null, "TERRAIN_USAGE_UNUSED_ASSETS");
            else if (_allTerrainDataPaths.Count > 0)
                _result.AddIssue(PungentScanSeverity.Success, "No unused TerrainData assets found", "Every TerrainData asset in the selected asset scope was referenced by at least one scanned scene Terrain component.", null, null, "TERRAIN_USAGE_NO_UNUSED_ASSETS");
        }

        private void AddUnusedTerrainAssetIssues()
        {
            for (int i = 0; i < _unusedTerrainDataPaths.Count; i++)
                _result.AddIssue(PungentScanSeverity.Warning, "Unused TerrainData asset", "This TerrainData asset was not referenced by scanned scene Terrain components.", null, _unusedTerrainDataPaths[i], "TERRAIN_USAGE_UNUSED_ASSET");
        }

        private void CompleteDiagnostics(double totalDurationSeconds)
        {
            _diagnostics.totalDurationSeconds = totalDurationSeconds;
            _diagnostics.scenesSkippedByDependencyPrefilter = _scenesSkippedByDependencyPrefilter;
            double openTotal = 0d;
            int openCount = 0;
            for (int i = 0; i < _diagnostics.sceneTimings.Count; i++)
            {
                TerrainUsageSceneTiming timing = _diagnostics.sceneTimings[i];
                if (timing.sceneOpenDurationSeconds > 0d)
                {
                    openTotal += timing.sceneOpenDurationSeconds;
                    openCount++;
                }
                if (timing.totalSceneStepDurationSeconds > _diagnostics.slowestSceneDurationSeconds)
                {
                    _diagnostics.slowestSceneDurationSeconds = timing.totalSceneStepDurationSeconds;
                    _diagnostics.slowestScenePath = timing.scenePath;
                }
            }
            _diagnostics.averageSceneOpenDurationSeconds = openCount > 0 ? openTotal / openCount : 0d;
            _diagnostics.completedUtc = DateTime.UtcNow;
            TerrainUsageScanService.StoreSharedDiagnostics(_diagnostics);
        }

        private PungentScanScope GetScanScope()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return PungentScanScope.OpenScenes;
            if (_scanScenesProjectWide && _scanTerrainAssetsProjectWide)
                return PungentScanScope.ProjectAssets;
            if (_scanScenesProjectWide)
                return PungentScanScope.ProjectScenes;
            return PungentScanScope.Custom;
        }

        private string GetScanScopeLabel()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return "Open scenes / " + (_skipPackageTerrainAssets ? "Project TerrainData" : "Project + package TerrainData");
            string sceneScope = _scanScenesProjectWide ? (_skipPackageScenes ? "Project scenes" : "Project + package scenes") : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);
            string terrainScope = _scanTerrainAssetsProjectWide ? (_skipPackageTerrainAssets ? "Project TerrainData" : "Project + package TerrainData") : GetScopeDescription(_terrainFolderPath, _includeTerrainSubfolders);
            return sceneScope + " / " + terrainScope + (_prefilterScenesByTerrainDataDependencies ? " / dependency prefilter" : string.Empty);
        }

        private static bool SceneHasTerrainDataDependency(string scenePath, HashSet<string> terrainCandidatePaths)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || terrainCandidatePaths == null || terrainCandidatePaths.Count == 0)
                return false;
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (terrainCandidatePaths.Contains(dependencies[i]))
                    return true;
            }
            return false;
        }

        private static bool IsPackagePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinFolderScope(string path, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string normalizedPath = NormalizeSlashes(path);
            string normalizedFolder = NormalizeFolderPath(folderPath, "Assets");
            if (includeSubfolders)
                return normalizedPath.StartsWith(normalizedFolder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalizedPath, normalizedFolder, StringComparison.OrdinalIgnoreCase);
            return string.Equals(Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/'), normalizedFolder.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFolderPath(string path, string fallbackPath)
        {
            string normalized = NormalizeSlashes(path);
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = fallbackPath;
            return normalized.TrimEnd('/');
        }

        private static string NormalizeSlashes(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string GetScopeDescription(string folderPath, bool includeSubfolders)
        {
            return NormalizeFolderPath(folderPath, "Assets") + (includeSubfolders ? " + subfolders" : " only");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }

    internal sealed class TerrainUsageScanService
    {
        private const string PrefPrefix = "GenericUtilities.TerrainUsageScanner.";

        private enum Stage
        {
            Begin,
            PrepareScenes,
            GatherTerrainData,
            ScanScenes,
            Publish,
            Restore,
            Done
        }

        private struct SceneTarget
        {
            public string path;
            public string name;
            public Scene loadedScene;
        }

        private struct TerrainUseRecord
        {
            public string scenePath;
            public string sceneName;
            public string terrainObjectPath;
        }

        private readonly PungentAuditScanMode _mode;
        private readonly Dictionary<string, List<TerrainUseRecord>> _usedTerrainDataByAssetPath = new Dictionary<string, List<TerrainUseRecord>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _allTerrainDataPaths = new List<string>();
        private readonly List<string> _unusedTerrainDataPaths = new List<string>();
        private readonly List<SceneTarget> _scenes = new List<SceneTarget>();
        private bool _includeDisabledGameObjects = true;
        private bool _scanScenesProjectWide = true;
        private string _sceneFolderPath = "Assets";
        private bool _skipPackageScenes = true;
        private bool _includeSceneSubfolders = true;
        private bool _prefilterScenesByTerrainDataDependencies = false;
        private bool _scanTerrainAssetsProjectWide = true;
        private string _terrainFolderPath = "Assets";
        private bool _skipPackageTerrainAssets = true;
        private bool _includeTerrainSubfolders = true;
        private Stage _stage = Stage.Begin;
        private PungentScanSession _session;
        private PungentScanResult _result;
        private SceneSetup[] _originalSetup;
        private bool _restoreSceneSetup;
        private bool _sceneSetupDisplaced;
        private int _sceneIndex;
        private bool _resumeMessagePending;
        private bool _hasResumed;
        private double _resumeStartedAtSeconds;
        private bool _sceneOpeningAnnounced;
        private bool _sceneReadyForScan;
        private SceneTarget _activeTarget;
        private Scene _activeScene;
        private TerrainUsageSceneTiming _activeSceneTiming;
        private readonly Stopwatch _scanStopwatch = new Stopwatch();
        private readonly Stopwatch _sceneStepStopwatch = new Stopwatch();
        private readonly Dictionary<string, double> _dependencyPrefilterDurations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private TerrainUsageScanDiagnostics _diagnostics = new TerrainUsageScanDiagnostics();
        private string _publishStatus = string.Empty;
        private int _publishTotalScanned;
        private int _scenesSkippedByDependencyPrefilter;
        private static TerrainUsageScanDiagnostics s_lastDiagnostics;

        private TerrainUsageScanService(PungentAuditScanMode mode)
        {
            _mode = mode;
            LoadConfig();
        }

        public static PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            TerrainUsageScanService service = new TerrainUsageScanService(mode);
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("terrain-usage-scanner", "Terrain Usage Scanner", service.Step);
            job.canPause = true;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.RequiresSceneOpening |
                               PungentAuditScanJobCapabilities.UsesAssetDatabase |
                               PungentAuditScanJobCapabilities.ScanOnly;
            job.Report(0f, 0, 1, mode == PungentAuditScanMode.BackgroundIdle ? "Queued open-scene Terrain Usage scan." : "Queued project Terrain Usage scan.", false, "Queued");
            return job;
        }

        public static bool TryGetLastDiagnostics(out TerrainUsageScanDiagnostics diagnostics)
        {
            diagnostics = s_lastDiagnostics;
            return diagnostics != null;
        }

        internal static void StoreSharedDiagnostics(TerrainUsageScanDiagnostics diagnostics)
        {
            s_lastDiagnostics = diagnostics;
        }

        public PungentAuditScanStepResult Step(PungentAuditScanContext context)
        {
            if (context.IsCancellationRequested())
                return Cancel("Terrain Usage scan interrupted before the next scene checkpoint.");
            if (context.IsPauseRequested())
                return PauseAtCheckpoint(context);

            try
            {
                switch (_stage)
                {
                    case Stage.Begin:
                        _session = new PungentScanSession("terrain-usage-scanner", "Terrain Usage Scanner");
                        _result = _session.Begin(GetScanScope(), GetScanScopeLabel());
                        BeginDiagnostics();
                        _stage = Stage.PrepareScenes;
                        context.Report(0.04f, 0, 1, "Preparing terrain usage scan.", false, "Prepare");
                        return PungentAuditScanStepResult.Continue("Preparing terrain usage scan.");

                    case Stage.PrepareScenes:
                        if (_mode == PungentAuditScanMode.BackgroundIdle)
                        {
                            GatherLoadedScenes();
                            if (_scenes.Count == 0)
                                _result.AddIssue(PungentScanSeverity.Warning, "No open scenes found", "Background Terrain Usage scans only inspect already-open scenes in this pass. Run Immediate mode for project-wide scene opening.", null, null, "TERRAIN_USAGE_BACKGROUND_NO_OPEN_SCENES");
                        }
                        else
                        {
                            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                return Cancel("Terrain Usage scan interrupted before scene changes were saved.");
                            _originalSetup = EditorSceneManager.GetSceneManagerSetup();
                            _restoreSceneSetup = true;
                            _sceneSetupDisplaced = false;
                            GatherSceneAssets();
                        }
                        if (_scenes.Count == 0)
                            _result.AddIssue(PungentScanSeverity.Warning, "No scenes found", "The active scene scan scope did not resolve any Scene assets.", null, _scanScenesProjectWide ? string.Empty : _sceneFolderPath, "TERRAIN_USAGE_NO_SCENES");
                        _stage = Stage.GatherTerrainData;
                        context.Report(0.15f, 0, Math.Max(1, _scenes.Count), "Prepared " + _scenes.Count + " scene(s).", false, "Prepare scenes");
                        return PungentAuditScanStepResult.Continue("Prepared " + _scenes.Count + " scene(s).");

                    case Stage.GatherTerrainData:
                        GatherTerrainDataAssets();
                        _scenesSkippedByDependencyPrefilter = ApplyTerrainDependencyPrefilter();
                        if (_scenesSkippedByDependencyPrefilter > 0)
                            _result.AddIssue(PungentScanSeverity.Info, "Scenes skipped by TerrainData dependency prefilter", _scenesSkippedByDependencyPrefilter + " scene(s) were skipped because their serialized dependencies did not include TerrainData in the active scope. Disable the prefilter for exhaustive verification.", null, null, "TERRAIN_USAGE_SCENE_DEPENDENCY_PREFILTER");
                        if (_allTerrainDataPaths.Count == 0)
                            _result.AddIssue(PungentScanSeverity.Warning, "No TerrainData assets found", "The active TerrainData asset scope did not resolve any TerrainData assets.", null, _scanTerrainAssetsProjectWide ? string.Empty : _terrainFolderPath, "TERRAIN_USAGE_NO_TERRAIN_DATA");
                        _stage = Stage.ScanScenes;
                        context.Report(0.24f, 0, Math.Max(1, _scenes.Count), "Gathered " + _allTerrainDataPaths.Count + " TerrainData candidate(s).", false, "TerrainData assets");
                        return PungentAuditScanStepResult.Continue("TerrainData candidates gathered.");

                    case Stage.ScanScenes:
                        if (_resumeMessagePending)
                        {
                            _resumeMessagePending = false;
                            _hasResumed = true;
                            _resumeStartedAtSeconds = _scanStopwatch.Elapsed.TotalSeconds;
                            string resumeStatus = "Resuming Terrain Usage scan...";
                            context.Report(GetSceneProgress(), _sceneIndex, Math.Max(1, _scenes.Count), resumeStatus, false, "Resume");
                            return PungentAuditScanStepResult.Continue(resumeStatus);
                        }

                        if (_sceneIndex < _scenes.Count)
                        {
                            SceneTarget target = _scenes[_sceneIndex];
                            int sceneNumber = _sceneIndex + 1;
                            if (!_sceneOpeningAnnounced && _mode != PungentAuditScanMode.BackgroundIdle)
                            {
                                BeginSceneTiming(target);
                                _sceneOpeningAnnounced = true;
                                string openingStatus = "Opening next scan scene " + sceneNumber + " / " + _scenes.Count + ": " + target.name;
                                context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, openingStatus, false, "Open scene");
                                return PungentAuditScanStepResult.Continue(openingStatus);
                            }

                            if (!_sceneReadyForScan)
                            {
                                if (_activeSceneTiming == null)
                                    BeginSceneTiming(target);
                                _activeTarget = target;
                                _activeScene = OpenSceneTarget(target);
                                _sceneReadyForScan = true;
                                string scanningStatus = "Scanning scene " + sceneNumber + " / " + _scenes.Count + ": " + target.name;
                                context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, scanningStatus, false, "Scan scene");
                                return PungentAuditScanStepResult.Continue(scanningStatus);
                            }

                            bool scanned = ScanPreparedSceneTarget(_activeTarget, _activeScene);
                            CompleteSceneTiming(scanned ? "Scanned" : "Skipped");
                            _sceneIndex++;
                            float progress = 0.24f + (0.60f * _sceneIndex / Mathf.Max(1, _scenes.Count));
                            string sceneStatus = (scanned ? "Scanned scene " : "Skipped scene ") + _sceneIndex + " / " + _scenes.Count + ": " + target.name;
                            ResetActiveSceneStep();
                            context.Report(progress, _sceneIndex, _scenes.Count, sceneStatus, false, "Scan scenes");
                            return PungentAuditScanStepResult.Continue(sceneStatus);
                        }
                        _stage = Stage.Publish;
                        return PungentAuditScanStepResult.Continue("Calculating unused TerrainData assets.");

                    case Stage.Publish:
                        _unusedTerrainDataPaths.AddRange(_allTerrainDataPaths
                            .Where(path => !_usedTerrainDataByAssetPath.ContainsKey(path))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
                        AddSummaryIssues();
                        AddUnusedTerrainAssetIssues();
                        string status = "Scan complete. Scenes scanned: " + _sceneIndex +
                                        ", Terrain candidates: " + _allTerrainDataPaths.Count +
                                        ", Used TerrainData: " + _usedTerrainDataByAssetPath.Count +
                                        ", Unused TerrainData: " + _unusedTerrainDataPaths.Count +
                                        (_scenesSkippedByDependencyPrefilter > 0 ? ", Dependency-skipped scenes: " + _scenesSkippedByDependencyPrefilter : string.Empty);
                        _publishStatus = status;
                        _publishTotalScanned = _sceneIndex + _allTerrainDataPaths.Count;
                        _stage = Stage.Restore;
                        context.Report(0.94f, _sceneIndex, Math.Max(1, _scenes.Count), "Restoring original scene setup...", false, "Restore scenes");
                        return PungentAuditScanStepResult.Continue("Restoring original scene setup...");

                    case Stage.Restore:
                        RestoreSceneSetup();
                        string completedStatus = _publishStatus;
                        if (_mode != PungentAuditScanMode.BackgroundIdle)
                            completedStatus += " Original scene setup restored.";
                        PungentScanResult completed = _session.Complete(_publishTotalScanned, _usedTerrainDataByAssetPath.Count, _unusedTerrainDataPaths.Count, 0, completedStatus);
                        CompleteDiagnostics(completed.DurationSeconds);
                        _stage = Stage.Done;
                        context.Report(1f, _sceneIndex, Math.Max(1, _scenes.Count), completedStatus, false, "Complete");
                        return PungentAuditScanStepResult.Complete(completedStatus, completed);

                    default:
                        return PungentAuditScanStepResult.Complete("Terrain Usage scan already completed.", _result);
                }
            }
            catch (Exception ex)
            {
                RestoreSceneSetup();
                Debug.LogException(ex);
                PungentScanResult failed = _session != null ? _session.Fail(ex, "Terrain Usage scan failed: " + ex.Message) : null;
                _stage = Stage.Done;
                return PungentAuditScanStepResult.Failed("Terrain Usage scan failed: " + ex.Message, failed);
            }
        }

        private void LoadConfig()
        {
            _includeDisabledGameObjects = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeDisabled", _includeDisabledGameObjects);
            _scanScenesProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanScenesProjectWide", _scanScenesProjectWide);
            _sceneFolderPath = NormalizeFolderPath(UtilityWindowPrefs.GetString(PrefPrefix + "SceneFolderPath", _sceneFolderPath), "Assets");
            _skipPackageScenes = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageScenes", _skipPackageScenes);
            _includeSceneSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeSceneSubfolders", _includeSceneSubfolders);
            _prefilterScenesByTerrainDataDependencies = UtilityWindowPrefs.GetBool(PrefPrefix + "PrefilterScenesByTerrainDataDependencies", _prefilterScenesByTerrainDataDependencies);
            _scanTerrainAssetsProjectWide = UtilityWindowPrefs.GetBool(PrefPrefix + "ScanTerrainProjectWide", _scanTerrainAssetsProjectWide);
            _terrainFolderPath = NormalizeFolderPath(UtilityWindowPrefs.GetString(PrefPrefix + "TerrainFolderPath", _terrainFolderPath), "Assets");
            _skipPackageTerrainAssets = UtilityWindowPrefs.GetBool(PrefPrefix + "SkipPackageTerrain", _skipPackageTerrainAssets);
            _includeTerrainSubfolders = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeTerrainSubfolders", _includeTerrainSubfolders);
        }

        private void GatherLoadedScenes()
        {
            _scenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                if (_skipPackageScenes && IsPackagePath(scene.path))
                    continue;
                _scenes.Add(new SceneTarget
                {
                    path = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path,
                    name = scene.name,
                    loadedScene = scene
                });
            }
        }

        private void GatherSceneAssets()
        {
            _scenes.Clear();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            IEnumerable<string> scenePaths = sceneGuids.Select(AssetDatabase.GUIDToAssetPath).Where(path => !string.IsNullOrEmpty(path));
            if (_skipPackageScenes)
                scenePaths = scenePaths.Where(path => !IsPackagePath(path));
            if (!_scanScenesProjectWide)
                scenePaths = scenePaths.Where(path => IsPathWithinFolderScope(path, _sceneFolderPath, _includeSceneSubfolders));
            foreach (string path in scenePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                _scenes.Add(new SceneTarget { path = path, name = System.IO.Path.GetFileNameWithoutExtension(path) });
        }

        private void GatherTerrainDataAssets()
        {
            _allTerrainDataPaths.Clear();
            string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData");
            IEnumerable<string> terrainPaths = terrainDataGuids.Select(AssetDatabase.GUIDToAssetPath).Where(path => !string.IsNullOrEmpty(path));
            if (_skipPackageTerrainAssets)
                terrainPaths = terrainPaths.Where(path => !IsPackagePath(path));
            if (!_scanTerrainAssetsProjectWide)
                terrainPaths = terrainPaths.Where(path => IsPathWithinFolderScope(path, _terrainFolderPath, _includeTerrainSubfolders));
            _allTerrainDataPaths.AddRange(terrainPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private int ApplyTerrainDependencyPrefilter()
        {
            if (_diagnostics != null)
                _diagnostics.scenesDiscovered = _scenes.Count;
            if (!_prefilterScenesByTerrainDataDependencies ||
                _mode == PungentAuditScanMode.BackgroundIdle ||
                _scenes.Count == 0 ||
                _allTerrainDataPaths.Count == 0)
            {
                return 0;
            }

            HashSet<string> terrainCandidatePaths = new HashSet<string>(_allTerrainDataPaths, StringComparer.OrdinalIgnoreCase);
            List<SceneTarget> keptScenes = new List<SceneTarget>(_scenes.Count);
            int skipped = 0;
            for (int i = 0; i < _scenes.Count; i++)
            {
                SceneTarget target = _scenes[i];
                Stopwatch prefilterStopwatch = Stopwatch.StartNew();
                bool hasDependency = SceneHasTerrainDataDependency(target.path, terrainCandidatePaths);
                prefilterStopwatch.Stop();
                double seconds = prefilterStopwatch.Elapsed.TotalSeconds;
                if (!string.IsNullOrWhiteSpace(target.path))
                    _dependencyPrefilterDurations[target.path] = seconds;
                if (hasDependency)
                {
                    keptScenes.Add(target);
                }
                else
                {
                    skipped++;
                    if (_diagnostics != null)
                    {
                        _diagnostics.sceneTimings.Add(new TerrainUsageSceneTiming
                        {
                            scenePath = target.path,
                            sceneName = target.name,
                            dependencyPrefilterDurationSeconds = seconds,
                            totalSceneStepDurationSeconds = seconds,
                            disposition = "Dependency-prefiltered"
                        });
                    }
                }
            }

            if (skipped > 0)
            {
                _scenes.Clear();
                _scenes.AddRange(keptScenes);
            }
            if (_diagnostics != null)
                _diagnostics.scenesSkipped += skipped;

            return skipped;
        }

        private static bool SceneHasTerrainDataDependency(string scenePath, HashSet<string> terrainCandidatePaths)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || terrainCandidatePaths == null || terrainCandidatePaths.Count == 0)
                return false;

            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (terrainCandidatePaths.Contains(dependencies[i]))
                    return true;
            }

            return false;
        }

        private void BeginDiagnostics()
        {
            _diagnostics = new TerrainUsageScanDiagnostics
            {
                dependencyPrefilterEnabled = _prefilterScenesByTerrainDataDependencies,
                completedUtc = DateTime.UtcNow
            };
            _scanStopwatch.Reset();
            _scanStopwatch.Start();
            _dependencyPrefilterDurations.Clear();
            _resumeMessagePending = false;
            _hasResumed = false;
            _resumeStartedAtSeconds = 0d;
            _scenesSkippedByDependencyPrefilter = 0;
            _publishStatus = string.Empty;
            _publishTotalScanned = 0;
            ResetActiveSceneStep();
        }

        private float GetSceneProgress()
        {
            return 0.24f + (0.60f * _sceneIndex / Mathf.Max(1, _scenes.Count));
        }

        private void BeginSceneTiming(SceneTarget target)
        {
            _activeSceneTiming = new TerrainUsageSceneTiming
            {
                scenePath = target.path,
                sceneName = target.name,
                disposition = "Scanning"
            };
            if (!string.IsNullOrWhiteSpace(target.path) && _dependencyPrefilterDurations.TryGetValue(target.path, out double dependencySeconds))
                _activeSceneTiming.dependencyPrefilterDurationSeconds = dependencySeconds;
            _sceneStepStopwatch.Reset();
            _sceneStepStopwatch.Start();
        }

        private Scene OpenSceneTarget(SceneTarget target)
        {
            Scene scene = target.loadedScene;
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return scene;

            Stopwatch openStopwatch = Stopwatch.StartNew();
            scene = EditorSceneManager.OpenScene(target.path, OpenSceneMode.Single);
            openStopwatch.Stop();
            _sceneSetupDisplaced = true;
            if (_activeSceneTiming != null)
                _activeSceneTiming.sceneOpenDurationSeconds = openStopwatch.Elapsed.TotalSeconds;
            if (_diagnostics != null)
                _diagnostics.scenesOpened++;
            return scene;
        }

        private bool ScanPreparedSceneTarget(SceneTarget target, Scene scene)
        {
            Stopwatch scanStopwatch = Stopwatch.StartNew();
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    _result.AddIssue(PungentScanSeverity.Warning, "Scene could not be loaded", "The scene was skipped because Unity did not load it successfully.", null, target.path, "TERRAIN_USAGE_SCENE_LOAD_FAILED");
                    if (_diagnostics != null)
                        _diagnostics.scenesSkipped++;
                    return false;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(_includeDisabledGameObjects);
                    for (int i = 0; i < terrains.Length; i++)
                    {
                        Terrain terrain = terrains[i];
                        if (terrain == null)
                            continue;
                        TerrainData terrainData = terrain.terrainData;
                        if (terrainData == null)
                        {
                            _result.AddIssue(PungentScanSeverity.Warning, "Terrain has no TerrainData", "Terrain component '" + GetHierarchyPath(terrain.transform) + "' has no TerrainData assigned.", terrain, target.path, "TERRAIN_USAGE_MISSING_TERRAIN_DATA");
                            continue;
                        }

                        string terrainDataPath = AssetDatabase.GetAssetPath(terrainData);
                        if (string.IsNullOrEmpty(terrainDataPath))
                        {
                            _result.AddIssue(PungentScanSeverity.Info, "Runtime or unsaved TerrainData", "Terrain component '" + GetHierarchyPath(terrain.transform) + "' references TerrainData with no AssetDatabase path.", terrain, target.path, "TERRAIN_USAGE_UNSAVED_TERRAIN_DATA");
                            continue;
                        }
                        if (_skipPackageTerrainAssets && IsPackagePath(terrainDataPath))
                            continue;

                        if (!_usedTerrainDataByAssetPath.TryGetValue(terrainDataPath, out List<TerrainUseRecord> list))
                        {
                            list = new List<TerrainUseRecord>();
                            _usedTerrainDataByAssetPath.Add(terrainDataPath, list);
                        }
                        list.Add(new TerrainUseRecord
                        {
                            scenePath = target.path,
                            sceneName = scene.name,
                            terrainObjectPath = GetHierarchyPath(terrain.transform)
                        });
                    }
                }
                return true;
            }
            finally
            {
                scanStopwatch.Stop();
                if (_activeSceneTiming != null)
                    _activeSceneTiming.terrainScanDurationSeconds = scanStopwatch.Elapsed.TotalSeconds;
            }
        }

        private void CompleteSceneTiming(string disposition)
        {
            if (_activeSceneTiming == null || _diagnostics == null)
                return;

            _sceneStepStopwatch.Stop();
            _activeSceneTiming.disposition = disposition;
            _activeSceneTiming.totalSceneStepDurationSeconds = _sceneStepStopwatch.Elapsed.TotalSeconds;
            _diagnostics.sceneTimings.Add(_activeSceneTiming);
        }

        private void ResetActiveSceneStep()
        {
            _sceneOpeningAnnounced = false;
            _sceneReadyForScan = false;
            _activeTarget = default(SceneTarget);
            _activeScene = default(Scene);
            _activeSceneTiming = null;
            _sceneStepStopwatch.Reset();
        }

        private void CompleteDiagnostics(double resultDurationSeconds)
        {
            if (_diagnostics == null)
                return;

            _scanStopwatch.Stop();
            _diagnostics.totalDurationSeconds = resultDurationSeconds > 0d ? resultDurationSeconds : _scanStopwatch.Elapsed.TotalSeconds;
            _diagnostics.scenesSkippedByDependencyPrefilter = _scenesSkippedByDependencyPrefilter;
            _diagnostics.resumed = _hasResumed;
            if (_hasResumed)
                _diagnostics.durationAfterResumeSeconds = Math.Max(0d, _diagnostics.totalDurationSeconds - _resumeStartedAtSeconds);
            double openTotal = 0d;
            int openCount = 0;
            for (int i = 0; i < _diagnostics.sceneTimings.Count; i++)
            {
                TerrainUsageSceneTiming timing = _diagnostics.sceneTimings[i];
                if (timing == null)
                    continue;
                if (timing.sceneOpenDurationSeconds > 0d)
                {
                    openTotal += timing.sceneOpenDurationSeconds;
                    openCount++;
                }
                if (timing.totalSceneStepDurationSeconds > _diagnostics.slowestSceneDurationSeconds)
                {
                    _diagnostics.slowestSceneDurationSeconds = timing.totalSceneStepDurationSeconds;
                    _diagnostics.slowestScenePath = timing.scenePath;
                }
            }
            _diagnostics.averageSceneOpenDurationSeconds = openCount > 0 ? openTotal / openCount : 0d;
            _diagnostics.completedUtc = DateTime.UtcNow;
            s_lastDiagnostics = _diagnostics;
        }

        private void AddSummaryIssues()
        {
            if (_usedTerrainDataByAssetPath.Count == 0 && _sceneIndex > 0)
                _result.AddIssue(PungentScanSeverity.Info, "No scene TerrainData references found", "The scanned scenes did not reference any TerrainData assets in the active scope.", null, null, "TERRAIN_USAGE_NO_USED_REFERENCES");
            if (_unusedTerrainDataPaths.Count > 0)
                _result.AddIssue(PungentScanSeverity.Warning, "Unused TerrainData assets found", _unusedTerrainDataPaths.Count + " TerrainData asset(s) in the selected asset scope were not referenced by scanned scene Terrain components.", null, null, "TERRAIN_USAGE_UNUSED_ASSETS");
            else if (_allTerrainDataPaths.Count > 0)
                _result.AddIssue(PungentScanSeverity.Success, "No unused TerrainData assets found", "Every TerrainData asset in the selected asset scope was referenced by at least one scanned scene Terrain component.", null, null, "TERRAIN_USAGE_NO_UNUSED_ASSETS");
        }

        private void AddUnusedTerrainAssetIssues()
        {
            for (int i = 0; i < _unusedTerrainDataPaths.Count; i++)
            {
                string assetPath = _unusedTerrainDataPaths[i];
                _result.AddIssue(PungentScanSeverity.Warning, "Unused TerrainData asset", "This TerrainData asset was not referenced by scanned scene Terrain components.", null, assetPath, "TERRAIN_USAGE_UNUSED_ASSET");
            }
        }

        private PungentAuditScanStepResult Cancel(string status)
        {
            RestoreSceneSetup(releaseSnapshot: true);
            if (_session != null)
                _session.Cancel(status, false);
            _stage = Stage.Done;
            return PungentAuditScanStepResult.Cancelled(status);
        }

        private PungentAuditScanStepResult PauseAtCheckpoint(PungentAuditScanContext context)
        {
            if (_diagnostics != null && _diagnostics.durationBeforePauseSeconds <= 0d)
                _diagnostics.durationBeforePauseSeconds = _scanStopwatch.Elapsed.TotalSeconds;
            RestoreSceneSetupForPause();
            ResetActiveSceneStep();
            _resumeMessagePending = true;
            string status = BuildPauseStatus();
            float progress = _scenes.Count > 0
                ? 0.24f + (0.60f * _sceneIndex / Mathf.Max(1, _scenes.Count))
                : 0.04f;
            int total = Mathf.Max(1, _scenes.Count);
            context.Report(progress, _sceneIndex, total, status, false, "Paused");
            return PungentAuditScanStepResult.Continue(status);
        }

        private string BuildPauseStatus()
        {
            if (_stage == Stage.ScanScenes && _scenes.Count > 0)
            {
                if (_sceneIndex >= _scenes.Count)
                    return "Paused after scene " + _sceneIndex + " / " + _scenes.Count + ". Original scene setup restored. Resume will calculate unused TerrainData.";
                int nextScene = Mathf.Min(_sceneIndex + 1, _scenes.Count);
                return "Paused after scene " + _sceneIndex + " / " + _scenes.Count + ". Original scene setup restored. Resume will continue at scene " + nextScene + " / " + _scenes.Count + ".";
            }

            return "Terrain Usage scan paused. Original scene setup restored. Progress preserved.";
        }

        private void RestoreSceneSetupForPause()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return;
            RestoreSceneSetup(releaseSnapshot: false);
        }

        private void RestoreSceneSetup(bool releaseSnapshot = true)
        {
            if (!_restoreSceneSetup || _originalSetup == null)
            {
                if (releaseSnapshot)
                {
                    _restoreSceneSetup = false;
                    _originalSetup = null;
                    _sceneSetupDisplaced = false;
                }
                return;
            }

            if (_sceneSetupDisplaced)
            {
                Stopwatch restoreStopwatch = Stopwatch.StartNew();
                EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);
                restoreStopwatch.Stop();
                if (_diagnostics != null)
                    _diagnostics.restoreDurationSeconds += restoreStopwatch.Elapsed.TotalSeconds;
                if (_activeSceneTiming != null)
                    _activeSceneTiming.restoreDurationSeconds += restoreStopwatch.Elapsed.TotalSeconds;
            }

            _sceneSetupDisplaced = false;
            if (releaseSnapshot)
            {
                _restoreSceneSetup = false;
                _originalSetup = null;
            }
        }

        private PungentScanScope GetScanScope()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return PungentScanScope.OpenScenes;
            if (_scanScenesProjectWide && _scanTerrainAssetsProjectWide)
                return PungentScanScope.ProjectAssets;
            if (_scanScenesProjectWide)
                return PungentScanScope.ProjectScenes;
            return PungentScanScope.Custom;
        }

        private string GetScanScopeLabel()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return "Open scenes / " + (_skipPackageTerrainAssets ? "Project TerrainData" : "Project + package TerrainData");
            string sceneScope = _scanScenesProjectWide ? (_skipPackageScenes ? "Project scenes" : "Project + package scenes") : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);
            string terrainScope = _scanTerrainAssetsProjectWide ? (_skipPackageTerrainAssets ? "Project TerrainData" : "Project + package TerrainData") : GetScopeDescription(_terrainFolderPath, _includeTerrainSubfolders);
            return sceneScope + " / " + terrainScope + (_prefilterScenesByTerrainDataDependencies ? " / dependency prefilter" : string.Empty);
        }

        private static string NormalizeFolderPath(string path, string fallbackPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallbackPath;
            path = path.Replace("\\", "/").Trim().TrimEnd('/');
            return AssetDatabase.IsValidFolder(path) ? path : fallbackPath;
        }

        private static string GetParentFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";
            assetPath = assetPath.Replace("\\", "/");
            int lastSlash = assetPath.LastIndexOf('/');
            return lastSlash <= 0 ? "Assets" : assetPath.Substring(0, lastSlash);
        }

        private static bool IsPackagePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.Replace("\\", "/").StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinFolderScope(string assetPath, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath))
                return false;
            assetPath = assetPath.Replace("\\", "/").Trim();
            folderPath = folderPath.Replace("\\", "/").Trim().TrimEnd('/');
            if (includeSubfolders)
                return assetPath.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(assetPath, folderPath, StringComparison.OrdinalIgnoreCase);
            return string.Equals(GetParentFolder(assetPath), folderPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetScopeDescription(string folderPath, bool includeSubfolders)
        {
            string normalized = NormalizeFolderPath(folderPath, "Assets");
            return includeSubfolders ? normalized + " (including subfolders)" : normalized + " (top-level only)";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";
            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }
    }

}
