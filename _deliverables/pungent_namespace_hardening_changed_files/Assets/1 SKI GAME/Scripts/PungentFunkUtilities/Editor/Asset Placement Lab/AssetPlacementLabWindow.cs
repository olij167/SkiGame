using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class AssetPlacementLabWindow : EditorWindow
    {
        private enum LabModule
        {
            AreaScatter,
            GridFootprint,
            SocketValidator,
            GroupManager
        }

        private const string PrefPrefix = "PungentFunkUtilities.AssetPlacementLab.";

        private LabModule _module;
        private PungentPlacementAssetSetSO _assetSet;
        private PungentPlacementRuleSetSO _ruleSet;
        private Transform _outputParent;
        private PungentPlacementScatterPattern _scatterPattern = PungentPlacementScatterPattern.RandomMinSpacing;
        private PungentPlacementAreaMode _areaMode = PungentPlacementAreaMode.ManualBounds;
        private Vector3 _areaCenter = Vector3.zero;
        private Vector3 _areaSize = new Vector3(30f, 5f, 30f);
        private int _seed = 12345;
        private int _count = 50;
        private bool _drawPreview = true;
        private bool _drawRejected = false;
        private bool _showCore = true;
        private bool _showRules = true;
        private bool _showResults = true;
        private bool _showAssetSet = true;
        private bool _showGrid = true;
        private bool _showSockets = true;
        private bool _showGroups = true;
        private float _gridCellSize = 1f;
        private Vector3 _gridOrigin = Vector3.zero;
        private bool _snapSelectionRotation;
        private string _status = "Ready";
        private Vector2 _scroll;
        private Vector2 _resultScroll;
        private PungentPlacementResult _previewResult = new PungentPlacementResult();
        private List<PungentPlacementSocketValidator.ValidationMessage> _socketMessages = new List<PungentPlacementSocketValidator.ValidationMessage>();
        private List<PungentPlacedAssetMarker> _sceneMarkers = new List<PungentPlacedAssetMarker>();

        [MenuItem("Tools/Utilities/Placement/Asset Placement Lab")]
        public static void Open()
        {
            AssetPlacementLabWindow window = GetWindow<AssetPlacementLabWindow>();
            window.titleContent = new GUIContent("Asset Placement Lab");
            window.minSize = new Vector2(780f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            _module = (LabModule)UtilityWindowPrefs.GetInt(PrefPrefix + "Module", 0);
            _scatterPattern = (PungentPlacementScatterPattern)UtilityWindowPrefs.GetInt(PrefPrefix + "ScatterPattern", (int)PungentPlacementScatterPattern.RandomMinSpacing);
            _areaMode = (PungentPlacementAreaMode)UtilityWindowPrefs.GetInt(PrefPrefix + "AreaMode", 0);
            _seed = UtilityWindowPrefs.GetInt(PrefPrefix + "Seed", _seed);
            _count = UtilityWindowPrefs.GetInt(PrefPrefix + "Count", _count);
            _drawPreview = UtilityWindowPrefs.GetBool(PrefPrefix + "DrawPreview", true);
            _drawRejected = UtilityWindowPrefs.GetBool(PrefPrefix + "DrawRejected", false);
            _gridCellSize = UtilityWindowPrefs.GetFloat(PrefPrefix + "GridCellSize", 1f);
            _showCore = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowCore", true);
            _showRules = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowRules", true);
            _showResults = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowResults", true);
            _showAssetSet = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowAssetSet", true);
            _showGrid = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowGrid", true);
            _showSockets = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowSockets", true);
            _showGroups = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowGroups", true);
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetInt(PrefPrefix + "Module", (int)_module);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ScatterPattern", (int)_scatterPattern);
            UtilityWindowPrefs.SetInt(PrefPrefix + "AreaMode", (int)_areaMode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Seed", _seed);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Count", _count);
            UtilityWindowPrefs.SetBool(PrefPrefix + "DrawPreview", _drawPreview);
            UtilityWindowPrefs.SetBool(PrefPrefix + "DrawRejected", _drawRejected);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "GridCellSize", _gridCellSize);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowCore", _showCore);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowRules", _showRules);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowResults", _showResults);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowAssetSet", _showAssetSet);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowGrid", _showGrid);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowSockets", _showSockets);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowGroups", _showGroups);
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Asset Placement Lab",
                "Generate, preview, validate, apply, and maintain reusable scene asset placement groups from project-agnostic placement modules.",
                _status);

            DrawTopToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_module)
            {
                case LabModule.AreaScatter:
                    DrawAreaScatterModule();
                    break;
                case LabModule.GridFootprint:
                    DrawGridFootprintModule();
                    break;
                case LabModule.SocketValidator:
                    DrawSocketValidatorModule();
                    break;
                case LabModule.GroupManager:
                    DrawGroupManagerModule();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTopToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _module = (LabModule)EditorGUILayout.EnumPopup(_module, EditorStyles.toolbarPopup, GUILayout.Width(190f));
                if (EditorGUI.EndChangeCheck())
                    GUI.FocusControl(null);

                GUILayout.FlexibleSpace();
                UtilityWindowTheme.ToolbarToggle(ref _drawPreview, "Scene Preview", UtilityWindowTheme.Cyan);
                UtilityWindowTheme.ToolbarToggle(ref _drawRejected, "Rejected", UtilityWindowTheme.Amber);
                if (GUILayout.Button("Repaint Scene", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    SceneView.RepaintAll();
            }
        }

        private void DrawAreaScatterModule()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Area Scatter", UtilityWindowTheme.Blue, _previewResult.summary);
                EditorGUILayout.LabelField("Scatter selected prefab sets across manual or selected bounds using procedural distributions and contextual surface rules.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            DrawCorePlacementSection();
            DrawRulesSection();
            DrawAssetSetBuilderSection();
            DrawResultsSection();
        }

        private void DrawCorePlacementSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                _showCore = EditorGUILayout.Foldout(_showCore, "Core Placement", true);
                if (!_showCore)
                    return;

                _assetSet = (PungentPlacementAssetSetSO)EditorGUILayout.ObjectField("Asset Set", _assetSet, typeof(PungentPlacementAssetSetSO), false);
                _ruleSet = (PungentPlacementRuleSetSO)EditorGUILayout.ObjectField("Rule Set", _ruleSet, typeof(PungentPlacementRuleSetSO), false);
                _outputParent = (Transform)EditorGUILayout.ObjectField("Output Parent", _outputParent, typeof(Transform), true);
                _scatterPattern = (PungentPlacementScatterPattern)EditorGUILayout.EnumPopup("Scatter Pattern", _scatterPattern);
                _areaMode = (PungentPlacementAreaMode)EditorGUILayout.EnumPopup("Area Source", _areaMode);
                _seed = EditorGUILayout.IntField("Seed", _seed);
                _count = Mathf.Max(0, EditorGUILayout.IntField("Requested Count", _count));

                using (new EditorGUI.DisabledScope(_areaMode == PungentPlacementAreaMode.SelectionBounds))
                {
                    _areaCenter = EditorGUILayout.Vector3Field("Area Center", _areaCenter);
                    _areaSize = EditorGUILayout.Vector3Field("Area Size", _areaSize);
                    _areaSize = new Vector3(Mathf.Max(0.01f, _areaSize.x), Mathf.Max(0.01f, _areaSize.y), Mathf.Max(0.01f, _areaSize.z));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Generate Preview", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                        GeneratePreview();
                    if (UtilityWindowTheme.TintedButton("Apply Accepted", UtilityWindowTheme.Blue, GUILayout.Height(26f)))
                        ApplyPreview();
                    if (UtilityWindowTheme.TintedButton("Clear Preview", UtilityWindowTheme.Neutral, GUILayout.Height(26f)))
                    {
                        _previewResult.Clear();
                        _status = "Preview cleared.";
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void DrawRulesSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _showRules = EditorGUILayout.Foldout(_showRules, "Inline Rule Summary", true);
                if (!_showRules)
                    return;

                if (_ruleSet == null)
                {
                    EditorGUILayout.HelpBox("Create or assign a Placement Rule Set for surface raycasts, height/slope filtering, spacing, heatmaps, and overlap checks.", MessageType.Info);
                    if (UtilityWindowTheme.TintedButton("Create Rule Set Asset", UtilityWindowTheme.Teal, GUILayout.Height(24f)))
                        CreateRuleSetAsset();
                    return;
                }

                EditorGUILayout.LabelField("Surface Mask", _ruleSet.surfaceMask.value.ToString(), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Spacing", _ruleSet.enforceMinimumSpacing ? _ruleSet.minimumSpacing.ToString("0.00") : "Disabled", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Height", _ruleSet.restrictHeight ? $"{_ruleSet.heightRange.x:0.0} to {_ruleSet.heightRange.y:0.0}" : "Any", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Slope", _ruleSet.restrictSlope ? $"{_ruleSet.slopeRange.x:0.0}° to {_ruleSet.slopeRange.y:0.0}°" : "Any", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Heatmap", _ruleSet.useHeatmap && _ruleSet.heatmap != null ? _ruleSet.heatmap.name : "None", UtilityWindowTheme.MutedMiniLabelStyle);

                if (UtilityWindowTheme.TintedButton("Select Rule Set", UtilityWindowTheme.Teal, GUILayout.Height(22f)))
                {
                    Selection.activeObject = _ruleSet;
                    EditorGUIUtility.PingObject(_ruleSet);
                }
            }
        }

        private void DrawAssetSetBuilderSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                _showAssetSet = EditorGUILayout.Foldout(_showAssetSet, "Asset Set Builder", true);
                if (!_showAssetSet)
                    return;

                EditorGUILayout.LabelField("Create or extend placement asset sets from selected prefab assets. Footprints are auto-estimated from renderer/collider bounds.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create Asset Set From Selection", UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                        CreateAssetSetFromSelection();
                    using (new EditorGUI.DisabledScope(_assetSet == null))
                    {
                        if (UtilityWindowTheme.TintedButton("Add Selected Prefabs", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                            PungentPlacementAssetSetBuilder.AddSelectedPrefabs(_assetSet);
                    }
                }

                if (_assetSet != null)
                    EditorGUILayout.LabelField($"Entries: {_assetSet.entries?.Count ?? 0}", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawResultsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _showResults = EditorGUILayout.Foldout(_showResults, "Preview Results", true);
                if (!_showResults)
                    return;

                if (_previewResult == null || _previewResult.candidates.Count == 0)
                {
                    EditorGUILayout.LabelField("No preview generated yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                EditorGUILayout.LabelField(_previewResult.summary, EditorStyles.boldLabel);
                _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.Height(180f));
                for (int i = 0; i < _previewResult.candidates.Count; i++)
                {
                    PungentPlacementCandidate c = _previewResult.candidates[i];
                    if (c == null)
                        continue;
                    if (!c.Accepted && !_drawRejected)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string state = c.Accepted ? "OK" : c.rejectionReason.ToString();
                        Color tint = c.Accepted ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber;
                        UtilityWindowTheme.CountPill(state, tint, 96f);
                        EditorGUILayout.LabelField(c.prefab != null ? c.prefab.name : "Missing", GUILayout.Width(150f));
                        EditorGUILayout.LabelField(c.position.ToString("F2"), UtilityWindowTheme.MutedMiniLabelStyle);
                        if (!c.Accepted)
                            EditorGUILayout.LabelField(c.rejectionMessage, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGridFootprintModule()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Grid / Footprint Placement", UtilityWindowTheme.Blue, "manual assist");
                EditorGUILayout.LabelField("Snap selected objects to a configurable grid and author footprint metadata for selected asset-set entries. This is the first granular extraction from the old grid-building system.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                _showGrid = EditorGUILayout.Foldout(_showGrid, "Grid Controls", true);
                if (_showGrid)
                {
                    _gridOrigin = EditorGUILayout.Vector3Field("Grid Origin", _gridOrigin);
                    _gridCellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cell Size", _gridCellSize));
                    _snapSelectionRotation = EditorGUILayout.Toggle("Snap Y Rotation To 90°", _snapSelectionRotation);
                    _assetSet = (PungentPlacementAssetSetSO)EditorGUILayout.ObjectField("Asset Set", _assetSet, typeof(PungentPlacementAssetSetSO), false);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Snap Selected To Grid", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                            SnapSelectionToGrid();
                        if (UtilityWindowTheme.TintedButton("Auto-Footprint Asset Set", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                            AutoFootprintAssetSet();
                    }
                }
            }
        }

        private void DrawSocketValidatorModule()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Socket Validator", UtilityWindowTheme.Blue, "modular assembly");
                EditorGUILayout.LabelField("Validate PungentPlacementSocket markers on selected prefabs or scene objects before using them in future socket graph placement modules.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                _showSockets = EditorGUILayout.Foldout(_showSockets, "Socket Actions", true);
                if (_showSockets)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Add Socket To Selected", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                            PungentPlacementSocketValidator.AddSocketToSelected();
                        if (UtilityWindowTheme.TintedButton("Validate Selection", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                            ValidateSockets();
                    }

                    for (int i = 0; i < _socketMessages.Count; i++)
                    {
                        var msg = _socketMessages[i];
                        if (msg == null)
                            continue;
                        MessageType type = msg.severity == "Error" ? MessageType.Error : msg.severity == "Warning" ? MessageType.Warning : MessageType.Info;
                        EditorGUILayout.HelpBox($"{msg.severity}: {msg.message}", type);
                        if (msg.context != null && GUILayout.Button($"Select {msg.context.name}", GUILayout.Height(20f)))
                            Selection.activeObject = msg.context;
                    }
                }
            }
        }

        private void DrawGroupManagerModule()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Placement Group Manager", UtilityWindowTheme.Blue, _sceneMarkers.Count.ToString());
                EditorGUILayout.LabelField("Scan, select, delete, or adopt generated placement markers in the open scene. This keeps generated content manageable without tying the tool to any gameplay system.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _showGroups = EditorGUILayout.Foldout(_showGroups, "Scene Marker Actions", true);
                if (_showGroups)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Scan Scene", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                            ScanSceneMarkers();
                        if (UtilityWindowTheme.TintedButton("Select All Marked", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                            SelectSceneMarkers();
                        if (UtilityWindowTheme.TintedButton("Adopt Selected", UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                            AdoptSelected();
                        if (UtilityWindowTheme.TintedButton("Delete Marked", UtilityWindowTheme.Red, GUILayout.Height(24f)))
                            DeleteSceneMarkers();
                    }

                    EditorGUILayout.LabelField($"Markers found: {_sceneMarkers.Count}", UtilityWindowTheme.MutedMiniLabelStyle);
                    for (int i = 0; i < Mathf.Min(_sceneMarkers.Count, 80); i++)
                    {
                        PungentPlacedAssetMarker marker = _sceneMarkers[i];
                        if (marker == null)
                            continue;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(marker.gameObject, typeof(GameObject), true);
                            EditorGUILayout.LabelField(marker.groupId, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(90f));
                        }
                    }
                }
            }
        }

        private void GeneratePreview()
        {
            Bounds area = ResolveAreaBounds();
            PungentPlacementContext context = BuildContext(area);
            _previewResult = PungentPlacementScatterUtility.Generate(context);
            _status = _previewResult.summary;
            SceneView.RepaintAll();
        }

        private void ApplyPreview()
        {
            if (_previewResult == null || _previewResult.acceptedCount == 0)
            {
                EditorUtility.DisplayDialog("Asset Placement Lab", "Generate an accepted preview before applying.", "OK");
                return;
            }

            PungentPlacementContext context = BuildContext(ResolveAreaBounds());
            GameObject group = PungentPlacementApplyUtility.ApplyResult(_previewResult, context, $"Placement_{_scatterPattern}_{_seed}");
            _status = group != null ? $"Applied {_previewResult.acceptedCount} object(s)." : "Nothing was applied.";
            if (group != null)
                Selection.activeGameObject = group;
        }

        private Bounds ResolveAreaBounds()
        {
            if (_areaMode == PungentPlacementAreaMode.SelectionBounds && Selection.gameObjects.Length > 0)
                return PungentPlacementBoundsUtility.EncapsulateSelection(Selection.gameObjects);
            return new Bounds(_areaCenter, _areaSize);
        }

        private PungentPlacementContext BuildContext(Bounds area)
        {
            return new PungentPlacementContext
            {
                seed = _seed,
                requestedCount = _count,
                scatterPattern = _scatterPattern,
                assetSet = _assetSet,
                ruleSet = _ruleSet,
                areaBounds = area,
                outputParent = _outputParent,
                previewRejected = _drawRejected,
                heatmap = _ruleSet != null ? _ruleSet.heatmap : null,
                heatmapWorldRect = _ruleSet != null ? _ruleSet.heatmapWorldRect : new Rect(area.min.x, area.min.z, area.size.x, area.size.z),
                useHeatmap = _ruleSet != null && _ruleSet.useHeatmap
            };
        }

        private void CreateAssetSetFromSelection()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Placement Asset Set", "Placement Asset Set", "asset", "Choose where to save the placement asset set.");
            if (string.IsNullOrEmpty(path))
                return;
            _assetSet = PungentPlacementAssetSetBuilder.CreateAssetSetFromSelection(path);
        }

        private void CreateRuleSetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Placement Rule Set", "Placement Rule Set", "asset", "Choose where to save the placement rule set.");
            if (string.IsNullOrEmpty(path))
                return;

            PungentPlacementRuleSetSO rules = CreateInstance<PungentPlacementRuleSetSO>();
            AssetDatabase.CreateAsset(rules, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            _ruleSet = rules;
            Selection.activeObject = rules;
            EditorGUIUtility.PingObject(rules);
        }

        private void SnapSelectionToGrid()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return;

            Undo.SetCurrentGroupName("Snap Selection To Placement Grid");
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null)
                    continue;

                Undo.RecordObject(go.transform, "Snap Selection To Grid");
                Vector3 p = go.transform.position;
                go.transform.position = PungentPlacementGridUtility.SnapWorld(p, _gridOrigin, _gridCellSize);

                if (_snapSelectionRotation)
                {
                    Vector3 e = go.transform.eulerAngles;
                    e.y = Mathf.Round(e.y / 90f) * 90f;
                    go.transform.eulerAngles = e;
                }
            }

            _status = $"Snapped {selected.Length} object(s).";
        }

        private void AutoFootprintAssetSet()
        {
            if (_assetSet == null)
                return;

            Undo.RecordObject(_assetSet, "Auto-Footprint Placement Asset Set");
            for (int i = 0; i < _assetSet.entries.Count; i++)
            {
                if (_assetSet.entries[i] != null)
                {
                    _assetSet.entries[i].footprint.cellSize = _gridCellSize;
                    PungentPlacementAssetSetBuilder.AutoPopulateFootprint(_assetSet.entries[i]);
                }
            }

            EditorUtility.SetDirty(_assetSet);
            AssetDatabase.SaveAssets();
            _status = "Asset set footprints updated.";
        }

        private void ValidateSockets()
        {
            _socketMessages.Clear();
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                _socketMessages.Add(new PungentPlacementSocketValidator.ValidationMessage { severity = "Warning", message = "Select one or more prefabs or scene objects to validate." });
                return;
            }

            for (int i = 0; i < selected.Length; i++)
                _socketMessages.AddRange(PungentPlacementSocketValidator.Validate(selected[i]));

            _status = $"Validated {selected.Length} object(s).";
        }

        private void ScanSceneMarkers()
        {
            _sceneMarkers.Clear();
            PungentPlacedAssetMarker[] markers = Object.FindObjectsOfType<PungentPlacedAssetMarker>();
            _sceneMarkers.AddRange(markers);
            _status = $"Found {_sceneMarkers.Count} placement marker(s).";
        }

        private void SelectSceneMarkers()
        {
            List<GameObject> objects = new List<GameObject>();
            for (int i = 0; i < _sceneMarkers.Count; i++)
            {
                if (_sceneMarkers[i] != null)
                    objects.Add(_sceneMarkers[i].gameObject);
            }
            Selection.objects = objects.ToArray();
        }

        private void AdoptSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return;

            string id = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null)
                    continue;
                PungentPlacedAssetMarker marker = go.GetComponent<PungentPlacedAssetMarker>();
                if (marker == null)
                    marker = Undo.AddComponent<PungentPlacedAssetMarker>(go);
                marker.groupId = id;
                marker.moduleId = "adopted";
                marker.generatedAtUtc = System.DateTime.UtcNow.ToString("u");
            }

            ScanSceneMarkers();
        }

        private void DeleteSceneMarkers()
        {
            if (_sceneMarkers.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Delete Marked Objects", $"Delete {_sceneMarkers.Count} marked object(s) from the scene?", "Delete", "Cancel"))
                return;

            for (int i = _sceneMarkers.Count - 1; i >= 0; i--)
            {
                if (_sceneMarkers[i] != null)
                    Undo.DestroyObjectImmediate(_sceneMarkers[i].gameObject);
            }
            _sceneMarkers.Clear();
            _status = "Marked objects deleted.";
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_drawPreview)
                return;

            Bounds area = ResolveAreaBounds();
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.55f);
            Handles.DrawWireCube(area.center, area.size);

            if (_previewResult == null || _previewResult.candidates == null)
                return;

            for (int i = 0; i < _previewResult.candidates.Count; i++)
            {
                PungentPlacementCandidate c = _previewResult.candidates[i];
                if (c == null)
                    continue;
                if (!c.Accepted && !_drawRejected)
                    continue;

                Handles.color = c.Accepted ? new Color(0.25f, 1f, 0.35f, 0.85f) : new Color(1f, 0.55f, 0.1f, 0.65f);
                float size = HandleUtility.GetHandleSize(c.position) * 0.08f;
                Handles.SphereHandleCap(0, c.position, Quaternion.identity, size, EventType.Repaint);
                Handles.DrawWireCube(c.estimatedBounds.center, c.estimatedBounds.size);
            }
        }
    }
    #endif

}