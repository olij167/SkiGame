using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TerrainUsageScannerWindow : EditorWindow
{
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

    private Vector2 _usedScroll;
    private Vector2 _unusedScroll;

    private bool _includeDisabledGameObjects = true;
    private bool _autoPingSelection = false;
    private bool _isScanning = false;

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

    private string _status = "Idle";
    private double _lastScanTimeSeconds = 0d;

    [MenuItem("Tools/Terrain/Terrain Usage Scanner")]
    public static void ShowWindow()
    {
        var window = GetWindow<TerrainUsageScannerWindow>("Terrain Usage Scanner");
        window.minSize = new Vector2(980f, 560f);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Terrain Usage Scanner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scans scene-assigned Terrain components and compares referenced TerrainData assets against a chosen terrain asset search scope.\n\n" +
            "Use scene scope controls to decide which scenes are audited, terrain asset scope controls to decide which TerrainData files are candidates for the unused list, and the unused filter to narrow review and bulk-select candidate folders.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(_isScanning))
        {
            DrawSceneScopeSection();
            EditorGUILayout.Space(8f);
            DrawTerrainScopeSection();
            EditorGUILayout.Space(8f);
            DrawUnusedFilterSection();
            EditorGUILayout.Space(8f);
            DrawGeneralOptionsSection();
            EditorGUILayout.Space(10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan", GUILayout.Height(30f)))
                {
                    Scan();
                }

                if (GUILayout.Button("Clear Results", GUILayout.Height(30f), GUILayout.Width(120f)))
                {
                    ClearResults();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_status);

        if (_lastScanTimeSeconds > 0d)
        {
            EditorGUILayout.LabelField($"Last scan duration: {_lastScanTimeSeconds:F2}s");
        }

        if (_scenePathsScanned.Count > 0)
        {
            EditorGUILayout.LabelField($"Scenes scanned: {_scenePathsScanned.Count}");
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawUsedPanel();
            DrawUnusedPanel();
        }
    }

    private void DrawSceneScopeSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scene Scan Scope", EditorStyles.boldLabel);

            _scanScenesProjectWide = EditorGUILayout.ToggleLeft(
                "Scan scenes project-wide",
                _scanScenesProjectWide);

            using (new EditorGUI.DisabledScope(_scanScenesProjectWide))
            {
                DrawFolderField(
                    label: "Scene Folder",
                    folderAsset: ref _sceneFolderAsset,
                    folderPath: ref _sceneFolderPath,
                    fallbackPath: "Assets");

                _includeSceneSubfolders = EditorGUILayout.ToggleLeft(
                    "Include subfolders",
                    _includeSceneSubfolders);
            }

            _skipPackageScenes = EditorGUILayout.ToggleLeft(
                "Skip scenes under Packages/",
                _skipPackageScenes);

            string effectiveSceneScope = _scanScenesProjectWide
                ? (_skipPackageScenes ? "All project scenes excluding Packages/" : "All scenes including Packages/")
                : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);

            EditorGUILayout.LabelField("Effective scene scope:", effectiveSceneScope);
        }
    }

    private void DrawTerrainScopeSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Terrain Asset Search Scope", EditorStyles.boldLabel);

            _scanTerrainAssetsProjectWide = EditorGUILayout.ToggleLeft(
                "Search terrain assets project-wide",
                _scanTerrainAssetsProjectWide);

            using (new EditorGUI.DisabledScope(_scanTerrainAssetsProjectWide))
            {
                DrawFolderField(
                    label: "Terrain Asset Folder",
                    folderAsset: ref _terrainFolderAsset,
                    folderPath: ref _terrainFolderPath,
                    fallbackPath: "Assets");

                _includeTerrainSubfolders = EditorGUILayout.ToggleLeft(
                    "Include subfolders",
                    _includeTerrainSubfolders);
            }

            _skipPackageTerrainAssets = EditorGUILayout.ToggleLeft(
                "Skip terrain assets under Packages/",
                _skipPackageTerrainAssets);

            string effectiveTerrainScope = _scanTerrainAssetsProjectWide
                ? (_skipPackageTerrainAssets ? "All TerrainData assets excluding Packages/" : "All TerrainData assets including Packages/")
                : GetScopeDescription(_terrainFolderPath, _includeTerrainSubfolders);

            EditorGUILayout.LabelField("Effective terrain asset scope:", effectiveTerrainScope);
        }
    }

    private void DrawUnusedFilterSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Unused Results Filter", EditorStyles.boldLabel);

            _showOnlyUnusedInSelectedFolder = EditorGUILayout.ToggleLeft(
                "Show only unused in selected folder",
                _showOnlyUnusedInSelectedFolder);

            using (new EditorGUI.DisabledScope(!_showOnlyUnusedInSelectedFolder))
            {
                DrawFolderField(
                    label: "Unused Filter Folder",
                    folderAsset: ref _unusedFilterFolderAsset,
                    folderPath: ref _unusedFilterFolderPath,
                    fallbackPath: "Assets");

                _unusedFilterIncludeSubfolders = EditorGUILayout.ToggleLeft(
                    "Include subfolders",
                    _unusedFilterIncludeSubfolders);

                using (new EditorGUILayout.HorizontalScope())
                {
                    int filteredCount = GetFilteredUnusedTerrainPaths().Count;

                    if (GUILayout.Button("Select All Filtered Unused", GUILayout.Height(24f)))
                    {
                        SelectUnusedAssets(GetFilteredUnusedTerrainPaths());
                    }

                    if (GUILayout.Button("Select All Unused In Same Folder", GUILayout.Height(24f)))
                    {
                        SelectAllUnusedInSameFolder();
                    }

                    EditorGUILayout.LabelField($"Filtered unused: {filteredCount}", GUILayout.Width(160f));
                }
            }
        }
    }

    private void DrawGeneralOptionsSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("General Options", EditorStyles.boldLabel);

            _includeDisabledGameObjects = EditorGUILayout.ToggleLeft(
                "Include disabled GameObjects while scanning scenes",
                _includeDisabledGameObjects);

            _autoPingSelection = EditorGUILayout.ToggleLeft(
                "Ping selected asset when clicking result rows",
                _autoPingSelection);
        }
    }

    private void DrawFolderField(string label, ref DefaultAsset folderAsset, ref string folderPath, string fallbackPath)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);

            var newFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                folderAsset,
                typeof(DefaultAsset),
                false);

            if (newFolderAsset != folderAsset)
            {
                folderAsset = newFolderAsset;
                if (folderAsset != null)
                {
                    string selectedPath = AssetDatabase.GetAssetPath(folderAsset);
                    if (AssetDatabase.IsValidFolder(selectedPath))
                    {
                        folderPath = selectedPath;
                    }
                    else
                    {
                        folderAsset = null;
                    }
                }
            }

            if (GUILayout.Button("Use Assets", GUILayout.Width(90f)))
            {
                folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
                folderPath = "Assets";
            }
        }

        folderPath = NormalizeFolderPath(folderPath, fallbackPath);
        EditorGUILayout.LabelField("Path", folderPath);
    }

    private void DrawUsedPanel()
    {
        using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
        {
            EditorGUILayout.LabelField($"Used TerrainData Assets ({_usedTerrainDataByAssetPath.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _usedScroll = EditorGUILayout.BeginScrollView(_usedScroll);

            if (_usedTerrainDataByAssetPath.Count == 0)
            {
                EditorGUILayout.HelpBox("No used TerrainData assets found yet. Run a scan.", MessageType.None);
            }
            else
            {
                foreach (var kvp in _usedTerrainDataByAssetPath.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string assetPath = kvp.Key;
                    List<TerrainUseRecord> uses = kvp.Value;

                    DrawAssetHeader(assetPath, isUnused: false);

                    EditorGUI.indentLevel++;
                    foreach (TerrainUseRecord use in uses.OrderBy(u => u.scenePath).ThenBy(u => u.terrainObjectPath))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"Scene: {use.sceneName}",
                                GUILayout.Width(220f));

                            EditorGUILayout.SelectableLabel(
                                use.terrainObjectPath,
                                EditorStyles.textField,
                                GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(6f);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawUnusedPanel()
    {
        List<string> filteredUnused = GetFilteredUnusedTerrainPaths();

        using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
        {
            string label = _showOnlyUnusedInSelectedFolder
                ? $"Unused TerrainData Assets (Filtered: {filteredUnused.Count} / Total: {_unusedTerrainDataPaths.Count})"
                : $"Unused TerrainData Assets ({_unusedTerrainDataPaths.Count})";

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (_showOnlyUnusedInSelectedFolder)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All Filtered", GUILayout.Width(140f)))
                    {
                        SelectUnusedAssets(filteredUnused);
                    }

                    if (GUILayout.Button("Select All Same Folder", GUILayout.Width(140f)))
                    {
                        SelectAllUnusedInSameFolder();
                    }
                }

                EditorGUILayout.Space(4f);
            }

            _unusedScroll = EditorGUILayout.BeginScrollView(_unusedScroll);

            if (filteredUnused.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _unusedTerrainDataPaths.Count == 0
                        ? "No unused TerrainData assets found yet, or a scan has not been run."
                        : "No unused TerrainData assets match the active folder filter.",
                    MessageType.None);
            }
            else
            {
                foreach (string assetPath in filteredUnused.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    DrawAssetHeader(assetPath, isUnused: true);
                    EditorGUILayout.Space(6f);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawAssetHeader(string assetPath, bool isUnused)
    {
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            if (GUILayout.Button(asset != null ? AssetPreview.GetMiniThumbnail(asset) : null, GUILayout.Width(22f), GUILayout.Height(18f)))
            {
                SelectAndPing(assetPath);
            }

            if (GUILayout.Button(assetPath, labelStyle, GUILayout.ExpandWidth(true)))
            {
                SelectAndPing(assetPath);
            }

            if (asset != null)
            {
                if (GUILayout.Button("Select", GUILayout.Width(60f)))
                {
                    SelectAndPing(assetPath);
                }
            }

            if (isUnused && asset != null)
            {
                if (GUILayout.Button("Same Folder", GUILayout.Width(90f)))
                {
                    SelectUnusedInFolder(GetParentFolder(assetPath), includeSubfolders: false);
                }

                if (GUILayout.Button("Reveal", GUILayout.Width(60f)))
                {
                    RevealProjectAsset(assetPath);
                }
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
        {
            EditorGUIUtility.PingObject(asset);
        }
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

    private void SelectUnusedAssets(List<string> assetPaths)
    {
        UnityEngine.Object[] assets = assetPaths
            .Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>)
            .Where(obj => obj != null)
            .ToArray();

        Selection.objects = assets;

        if (_autoPingSelection && assets.Length > 0)
        {
            EditorGUIUtility.PingObject(assets[0]);
        }
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
        _status = "Idle";
        _lastScanTimeSeconds = 0d;
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

            foreach (string path in terrainDataPathsToConsider)
            {
                _allTerrainDataPaths.Add(path);
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
                        return;
                    }

                    _scenePathsScanned.Add(scenePath);
                    ScanScene(scenePath);
                }

                _unusedTerrainDataPaths.AddRange(
                    _allTerrainDataPaths
                        .Where(path => !_usedTerrainDataByAssetPath.ContainsKey(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

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
        {
            scenePaths = scenePaths.Where(path => !IsPackagePath(path));
        }

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
        {
            terrainPaths = terrainPaths.Where(path => !IsPackagePath(path));
        }

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

    private List<string> GetFilteredUnusedTerrainPaths()
    {
        IEnumerable<string> paths = _unusedTerrainDataPaths;

        if (_showOnlyUnusedInSelectedFolder)
        {
            string folder = NormalizeFolderPath(_unusedFilterFolderPath, "Assets");
            paths = paths.Where(path => IsPathWithinFolderScope(path, folder, _unusedFilterIncludeSubfolders));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
}