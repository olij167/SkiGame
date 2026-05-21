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

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
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

        private Vector2 _usedScroll;
        private Vector2 _unusedScroll;
        private Vector2 _scopeScroll;
        private float _scopePanelHeight = 310f;
        private float _usedPanelWidth = 470f;

        private bool _includeDisabledGameObjects = true;
        private bool _autoPingSelection = false;
        private bool _isScanning = false;
        private bool _resultCacheDirty = true;

        // Scene scan scope
        private bool _scanScenesProjectWide = true;
        private DefaultAsset _sceneFolderAsset;
        private string _sceneFolderPath = "Assets";
        private bool _skipPackageScenes = true;
        private bool _includeSceneSubfolders = true;

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

        [MenuItem("Tools/Utilities/Terrain/Terrain Usage Scanner")]
        public static void ShowWindow()
        {
            var window = GetWindow<TerrainUsageScannerWindow>("Terrain Usage Scanner");
            window.minSize = new Vector2(980f, 560f);
            window.Show();
        }

        [MenuItem("Tools/Terrain/Terrain Usage Scanner", priority = 9000)]
        public static void ShowLegacyWindow()
        {
            ShowWindow();
        }

        private void OnEnable()
        {
            LoadPrefs();
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
                            Scan();

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
            }
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
            _usedTerrainDataByAssetPath.Clear();
            _allTerrainDataPaths.Clear();
            _unusedTerrainDataPaths.Clear();
            _scenePathsScanned.Clear();
            _cachedUsedTerrainPaths.Clear();
            _cachedFilteredUnusedTerrainPaths.Clear();
            _status = "Idle";
            _lastScanTimeSeconds = 0d;
            MarkResultCacheDirty();
            Repaint();
        }

        private void Scan()
        {
            if (_isScanning)
                return;

            _isScanning = true;
            ClearResults();

            DateTime start = DateTime.UtcNow;

            try
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    _status = "Scan cancelled.";
                    return;
                }

                List<string> scenePathsToScan = GetScenePathsToScan();
                List<string> terrainDataPathsToConsider = GetTerrainDataPathsToConsider();

                _allTerrainDataPaths.AddRange(terrainDataPathsToConsider);
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
                            return;
                        }

                        _scenePathsScanned.Add(scenePath);
                        ScanScene(scenePath);
                    }

                    _unusedTerrainDataPaths.AddRange(
                        _allTerrainDataPaths
                            .Where(path => !_usedTerrainDataByAssetPath.ContainsKey(path))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

                    _status =
                        $"Scan complete. Scenes scanned: {scenePathsToScan.Count}, " +
                        $"Terrain candidates: {_allTerrainDataPaths.Count}, " +
                        $"Used TerrainData: {_usedTerrainDataByAssetPath.Count}, " +
                        $"Unused TerrainData: {_unusedTerrainDataPaths.Count}";
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
            }
            finally
            {
                _lastScanTimeSeconds = (DateTime.UtcNow - start).TotalSeconds;
                _isScanning = false;
                MarkResultCacheDirty();
                Repaint();
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

        private void ScanScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                return;

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
                        continue;

                    string terrainDataPath = AssetDatabase.GetAssetPath(terrainData);
                    if (string.IsNullOrEmpty(terrainDataPath))
                        continue;

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

}