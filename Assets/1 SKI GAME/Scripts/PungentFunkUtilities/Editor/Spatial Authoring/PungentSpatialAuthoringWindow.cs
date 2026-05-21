using System.Collections.Generic;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    public sealed class PungentSpatialAuthoringWindow : EditorWindow
    {
        private const string LeftWidthPrefKey = "PungentFunkUtilities.SpatialWorkbench.LeftWidth";
        private const float LeftMin = 230f;
        private const float LeftMax = 420f;
        private const float DetailWidth = 310f;
        private const float SplitterWidth = 5f;

        [SerializeField] private PungentSpatialAuthoringAsset _activeAsset;
        [SerializeField] private PungentSpatialWorkbenchTab _tab = PungentSpatialWorkbenchTab.Objects;
        [SerializeField] private PungentSpatialWorkbenchEditMode _editMode = PungentSpatialWorkbenchEditMode.Move;
        [SerializeField] private PungentSpatialWorkbenchTargetKind _targetKind = PungentSpatialWorkbenchTargetKind.Path;
        [SerializeField] private bool _snap = true;
        [SerializeField] private bool _selectedOnly;
        [SerializeField] private bool _showHidden;

        private readonly PungentSpatialProviderCache _cache = new PungentSpatialProviderCache();
        private readonly PungentSpatialPlanCanvasState _planState = new PungentSpatialPlanCanvasState();
        private readonly List<PungentSpatialWorkbenchIssue> _issues = new List<PungentSpatialWorkbenchIssue>();
        private readonly PungentSpatialBackgroundBakeSettings _bakeSettings = new PungentSpatialBackgroundBakeSettings();
        private Vector2 _browserScroll;
        private Vector2 _workspaceScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private bool _filterPaths = true;
        private bool _filterAreas = true;
        private bool _filterRegions = true;
        private bool _filterMarkers = true;
        private bool _filterGenerated = true;
        private bool _invalidOnly;
        private string _selectedRecordKey = string.Empty;
        private float _leftWidth = 280f;
        private bool _resizingLeft;
        private string _lastBakeMessage = string.Empty;

        public static void Open()
        {
            PungentSpatialAuthoringWindow window = GetWindow<PungentSpatialAuthoringWindow>("Spatial Authoring Workbench");
            window.minSize = new Vector2(820f, 460f);
            window.Show();
        }

        public static void OpenWithAsset(PungentSpatialAuthoringAsset asset)
        {
            PungentSpatialAuthoringWindow window = GetWindow<PungentSpatialAuthoringWindow>("Spatial Authoring Workbench");
            window.minSize = new Vector2(820f, 460f);
            window._activeAsset = asset;
            window._tab = PungentSpatialWorkbenchTab.Objects;
            window.MarkCacheDirty();
            window.Show();
        }

        public static void OpenWithPath(ModularPathSpawner path)
        {
            Open();
            if (path != null)
                Selection.activeGameObject = path.gameObject;
        }

        public static void OpenWithArea(PungentAreaAuthoringShape area)
        {
            Open();
            if (area != null)
                Selection.activeGameObject = area.gameObject;
        }

        private void OnEnable()
        {
            _leftWidth = EditorPrefs.GetFloat(LeftWidthPrefKey, 280f);
            _cache.MarkDirty();
            EditorApplication.hierarchyChanged += MarkCacheDirty;
            Selection.selectionChanged += MarkCacheDirty;
            EditorApplication.projectChanged += MarkCacheDirty;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkCacheDirty;
            Selection.selectionChanged -= MarkCacheDirty;
            EditorApplication.projectChanged -= MarkCacheDirty;
            SceneView.duringSceneGui -= DuringSceneGUI;
        }

        private void MarkCacheDirty()
        {
            _cache.MarkDirty();
            Repaint();
        }

        private void OnGUI()
        {
            _cache.RebuildIfDirty(_activeAsset);
            PungentSpatialWorkbenchValidationUtility.ValidateAsset(_activeAsset, _issues);
            PungentSpatialWorkbenchValidationUtility.ValidateCache(_cache, _issues);

            DrawAssetStrip();
            PungentSpatialToolbarGUI.Draw(
                _activeAsset,
                ref _tab,
                ref _editMode,
                ref _targetKind,
                ref _snap,
                ref _selectedOnly,
                ref _showHidden,
                _issues.Count,
                _cache.Records.Count,
                MarkCacheDirty,
                CreateAsset);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftBrowser();
                DrawSplitter();
                DrawMainArea();
            }
        }

        private void DrawAssetStrip()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _activeAsset = (PungentSpatialAuthoringAsset)EditorGUILayout.ObjectField("Spatial Asset", _activeAsset, typeof(PungentSpatialAuthoringAsset), false);
                if (EditorGUI.EndChangeCheck())
                    MarkCacheDirty();

                if (GUILayout.Button("Create Asset", GUILayout.Width(100f)))
                    CreateAsset();
                if (GUILayout.Button("Ping", GUILayout.Width(48f)) && _activeAsset != null)
                    EditorGUIUtility.PingObject(_activeAsset);
            }
        }

        private void DrawLeftBrowser()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(_leftWidth), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("Spatial Objects", EditorStyles.boldLabel);
                _search = EditorGUILayout.TextField(GUIContent.none, _search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _filterPaths = GUILayout.Toggle(_filterPaths, "Path", EditorStyles.miniButtonLeft);
                    _filterAreas = GUILayout.Toggle(_filterAreas, "Area", EditorStyles.miniButtonMid);
                    _filterRegions = GUILayout.Toggle(_filterRegions, "Region", EditorStyles.miniButtonMid);
                    _filterGenerated = GUILayout.Toggle(_filterGenerated, "Output", EditorStyles.miniButtonRight);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _filterMarkers = GUILayout.Toggle(_filterMarkers, "Markers", EditorStyles.miniButtonLeft);
                    _invalidOnly = GUILayout.Toggle(_invalidOnly, "Invalid", EditorStyles.miniButtonMid);
                    _showHidden = GUILayout.Toggle(_showHidden, "Hidden", EditorStyles.miniButtonRight);
                }

                using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_browserScroll, GUILayout.ExpandHeight(true)))
                {
                    _browserScroll = scope.scrollPosition;
                    IReadOnlyList<PungentSpatialProviderCache.Record> records = _cache.Records;
                    for (int i = 0; i < records.Count; i++)
                    {
                        PungentSpatialProviderCache.Record record = records[i];
                        if (!PassesFilters(record))
                            continue;
                        DrawRecordRow(record);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("New Path"))
                        AddPath();
                    if (GUILayout.Button("New Area"))
                        AddArea();
                }
            }
        }

        private void DrawSplitter()
        {
            Rect splitter = GUILayoutUtility.GetRect(SplitterWidth, 1f, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.MouseDown && splitter.Contains(Event.current.mousePosition))
            {
                _resizingLeft = true;
                Event.current.Use();
            }

            if (_resizingLeft && Event.current.type == EventType.MouseDrag)
            {
                _leftWidth = Mathf.Clamp(Event.current.mousePosition.x, LeftMin, LeftMax);
                EditorPrefs.SetFloat(LeftWidthPrefKey, _leftWidth);
                Repaint();
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseUp)
                _resizingLeft = false;

            EditorGUI.DrawRect(splitter, new Color(0f, 0f, 0f, 0.2f));
        }

        private void DrawMainArea()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                switch (_tab)
                {
                    case PungentSpatialWorkbenchTab.Scene:
                        DrawScenePage();
                        break;
                    case PungentSpatialWorkbenchTab.Plan:
                        DrawPlanPage();
                        break;
                    case PungentSpatialWorkbenchTab.Bake:
                        DrawBakePage();
                        break;
                    case PungentSpatialWorkbenchTab.Objects:
                        DrawObjectsPage();
                        break;
                    case PungentSpatialWorkbenchTab.Validate:
                        DrawValidatePage();
                        break;
                }
            }
        }

        private void DrawScenePage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_workspaceScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _workspaceScroll = scope.scrollPosition;
                    EditorGUILayout.HelpBox("Scene View authoring remains active through existing component handles and overlays. This Workbench provides object discovery, status, and handoff controls while the new document Plan canvas handles asset-backed editing.", MessageType.Info);
                    DrawSelectionSummary();
                    if (GUILayout.Button("Open Scene Gizmo Browser", GUILayout.Width(190f)))
                        PungentUtilityRegistry.Open("scene-gizmo-browser");
                    if (GUILayout.Button("Open Asset Placement Lab", GUILayout.Width(190f)))
                        PungentUtilityRegistry.Open("asset-placement-lab");
                }

                DrawDetailsPanel();
            }
        }

        private void DrawPlanPage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                            _planState.status = "Use the next repaint to frame all.";
                        if (GUILayout.Button("Add Path", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                            AddPath();
                        if (GUILayout.Button("Add Area", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                            AddArea();
                        if (GUILayout.Button("Add Region Set", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                            AddRegionSet();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"{_editMode} / {_targetKind}", EditorStyles.miniLabel);
                    }

                    Rect canvasRect = GUILayoutUtility.GetRect(100f, 10000f, 100f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    if (_planState.status == "Use the next repaint to frame all.")
                        PungentSpatialPlanCanvasGUI.FrameAll(canvasRect, _activeAsset, _planState);
                    PungentSpatialPlanCanvasGUI.Draw(canvasRect, _activeAsset, _planState, _editMode, _targetKind);
                    if (GUI.changed && _activeAsset != null)
                        MarkCacheDirty();
                }

                DrawDetailsPanel();
            }
        }

        private void DrawBakePage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_workspaceScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _workspaceScroll = scope.scrollPosition;
                    GUILayout.Label("Background Bake", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Bake creates a document background for the Plan canvas. Safe mode keeps capture bounds aligned to the projection bounds.", MessageType.Info);

                    _bakeSettings.safeAlignToProjection = EditorGUILayout.Toggle("Safe Align To Projection", _bakeSettings.safeAlignToProjection);
                    using (new EditorGUI.DisabledScope(_bakeSettings.safeAlignToProjection))
                    {
                        _bakeSettings.boundsSource = (PungentSpatialBakeBoundsSource)EditorGUILayout.EnumPopup("Bounds Source", _bakeSettings.boundsSource);
                        _bakeSettings.manualBounds = EditorGUILayout.BoundsField("Manual Bounds", _bakeSettings.manualBounds);
                    }

                    _bakeSettings.padding = EditorGUILayout.FloatField("Padding", _bakeSettings.padding);
                    _bakeSettings.cullingMask = PungentLayerMaskField("Culling Mask", _bakeSettings.cullingMask);
                    _bakeSettings.textureSize = EditorGUILayout.Vector2IntField("Texture Size", _bakeSettings.textureSize);
                    _bakeSettings.clearColor = EditorGUILayout.ColorField("Clear Color", _bakeSettings.clearColor);
                    _bakeSettings.outputFolder = EditorGUILayout.TextField("Output Folder", _bakeSettings.outputFolder);
                    _bakeSettings.outputName = EditorGUILayout.TextField("Output Name", _bakeSettings.outputName);

                    if (PungentSpatialBackgroundBakeUtility.TryResolveBounds(_activeAsset, _bakeSettings, out Bounds bounds, out string summary))
                        EditorGUILayout.HelpBox($"Resolved {summary}: center {bounds.center}, size {bounds.size}", MessageType.None);
                    else
                        EditorGUILayout.HelpBox("No valid bounds resolved yet.", MessageType.Warning);

                    using (new EditorGUI.DisabledScope(_activeAsset == null))
                    {
                        if (GUILayout.Button("Bake Background", GUILayout.Width(160f)))
                        {
                            if (PungentSpatialBackgroundBakeUtility.Bake(_activeAsset, _bakeSettings, out _lastBakeMessage))
                                MarkCacheDirty();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(_lastBakeMessage))
                        EditorGUILayout.HelpBox(_lastBakeMessage, MessageType.Info);
                }

                DrawDetailsPanel();
            }
        }

        private void DrawObjectsPage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_workspaceScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _workspaceScroll = scope.scrollPosition;
                    GUILayout.Label("Object Browser", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Records", _cache.Records.Count.ToString());
                    EditorGUILayout.LabelField("Warnings", _cache.WarningCount.ToString());
                    EditorGUILayout.LabelField("Estimated Draw Ops", _cache.DrawCost.ToString());
                    EditorGUILayout.LabelField("Last Rebuild", _cache.LastRebuildTime.ToString("0.00"));
                    EditorGUILayout.Space();
                    DrawBridgePlaceholders();
                }

                DrawDetailsPanel();
            }
        }

        private void DrawValidatePage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_workspaceScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _workspaceScroll = scope.scrollPosition;
                    GUILayout.Label("Validation", EditorStyles.boldLabel);
                    if (GUILayout.Button("Refresh Validation", GUILayout.Width(150f)))
                    {
                        PungentSpatialWorkbenchValidationUtility.ValidateAsset(_activeAsset, _issues);
                        PungentSpatialWorkbenchValidationUtility.ValidateCache(_cache, _issues);
                    }

                    for (int i = 0; i < _issues.Count; i++)
                    {
                        PungentSpatialWorkbenchIssue issue = _issues[i];
                        EditorGUILayout.HelpBox($"{issue.Code}: {issue.Message}", issue.Severity);
                    }
                }

                DrawDetailsPanel();
            }
        }

        private void DrawDetailsPanel()
        {
            using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(_detailScroll, GUILayout.Width(DetailWidth), GUILayout.ExpandHeight(true)))
            {
                _detailScroll = scope.scrollPosition;
                GUILayout.Label("Details", EditorStyles.boldLabel);
                if (_activeAsset == null)
                {
                    EditorGUILayout.HelpBox("Create or assign a Spatial Authoring Asset for document-backed paths, areas, regions, and backgrounds.", MessageType.Info);
                    return;
                }

                PungentSpatialProviderCache.Record selected = GetSelectedRecord();
                if (selected == null)
                {
                    DrawAssetDetails();
                    return;
                }

                switch (selected.kind)
                {
                    case PungentSpatialItemKind.Path:
                        if (selected.sourceAsset == _activeAsset)
                            DrawPathDetails(selected.itemIndex);
                        else
                            DrawComponentDetails(selected);
                        break;
                    case PungentSpatialItemKind.Area:
                        if (selected.sourceAsset == _activeAsset)
                            DrawAreaDetails(selected.itemIndex);
                        else
                            DrawComponentDetails(selected);
                        break;
                    case PungentSpatialItemKind.RegionSet:
                    case PungentSpatialItemKind.RegionFace:
                        DrawRegionDetails(selected.itemIndex);
                        break;
                    case PungentSpatialItemKind.Marker:
                        DrawMarkerDetails(selected.itemIndex);
                        break;
                    default:
                        DrawComponentDetails(selected);
                        break;
                }
            }
        }

        private void DrawAssetDetails()
        {
            EditorGUI.BeginChangeCheck();
            _activeAsset.metadata.displayName = EditorGUILayout.TextField("Display Name", _activeAsset.metadata.displayName);
            _activeAsset.metadata.color = EditorGUILayout.ColorField("Color", _activeAsset.metadata.color);
            _activeAsset.projection.mode = (PungentSpatialProjectionKind)EditorGUILayout.EnumPopup("Projection", _activeAsset.projection.mode);
            _activeAsset.projection.origin = EditorGUILayout.Vector3Field("Origin", _activeAsset.projection.origin);
            _activeAsset.projection.worldMin = EditorGUILayout.Vector2Field("World Min", _activeAsset.projection.worldMin);
            _activeAsset.projection.worldMax = EditorGUILayout.Vector2Field("World Max", _activeAsset.projection.worldMax);
            _activeAsset.projection.yawDegrees = EditorGUILayout.FloatField("Yaw", _activeAsset.projection.yawDegrees);
            _activeAsset.backgroundTexture = (Texture2D)EditorGUILayout.ObjectField("Background", _activeAsset.backgroundTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_activeAsset, "Edit Spatial Asset");
                _activeAsset.Normalize();
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
        }

        private void DrawPathDetails(int index)
        {
            if (_activeAsset.paths == null || index < 0 || index >= _activeAsset.paths.Count || _activeAsset.paths[index] == null)
                return;

            PungentSpatialPath path = _activeAsset.paths[index];
            EditorGUI.BeginChangeCheck();
            path.metadata.displayName = EditorGUILayout.TextField("Display Name", path.metadata.displayName);
            path.metadata.color = EditorGUILayout.ColorField("Color", path.metadata.color);
            path.visible = EditorGUILayout.Toggle("Visible", path.visible);
            path.locked = EditorGUILayout.Toggle("Locked", path.locked);
            path.closedLoop = EditorGUILayout.Toggle("Closed Loop", path.closedLoop);
            path.directional = EditorGUILayout.Toggle("Directional", path.directional);
            path.width = EditorGUILayout.FloatField("Width", path.width);
            path.corridorWidth = EditorGUILayout.FloatField("Corridor Width", path.corridorWidth);
            path.heightMode = (PungentSpatialPathHeightMode)EditorGUILayout.EnumPopup("Height Mode", path.heightMode);
            EditorGUILayout.LabelField("Points", path.worldPoints == null ? "0" : path.worldPoints.Count.ToString());
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_activeAsset, "Edit Spatial Path");
                path.Normalize($"Path {index + 1}");
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
        }

        private void DrawAreaDetails(int index)
        {
            if (_activeAsset.areas == null || index < 0 || index >= _activeAsset.areas.Count || _activeAsset.areas[index] == null)
                return;

            PungentSpatialArea area = _activeAsset.areas[index];
            EditorGUI.BeginChangeCheck();
            area.metadata.displayName = EditorGUILayout.TextField("Display Name", area.metadata.displayName);
            area.borderColor = EditorGUILayout.ColorField("Border", area.borderColor);
            area.fillColor = EditorGUILayout.ColorField("Fill", area.fillColor);
            area.visible = EditorGUILayout.Toggle("Visible", area.visible);
            area.locked = EditorGUILayout.Toggle("Locked", area.locked);
            area.shape = (PungentSpatialAreaShapeMode)EditorGUILayout.EnumPopup("Shape", area.shape);
            area.role = (PungentSpatialAreaRole)EditorGUILayout.EnumPopup("Role", area.role);
            if (area.shape == PungentSpatialAreaShapeMode.Rectangle)
                area.rectangleSize = EditorGUILayout.Vector2Field("Size", area.rectangleSize);
            if (area.shape == PungentSpatialAreaShapeMode.Circle)
                area.circleRadius = EditorGUILayout.FloatField("Radius", area.circleRadius);
            area.verticalMode = (PungentSpatialAreaVerticalMode)EditorGUILayout.EnumPopup("Vertical", area.verticalMode);
            area.height = EditorGUILayout.FloatField("Height", area.height);
            area.minY = EditorGUILayout.FloatField("Min Y", area.minY);
            area.maxY = EditorGUILayout.FloatField("Max Y", area.maxY);
            area.edgeFalloff = EditorGUILayout.FloatField("Edge Falloff", area.edgeFalloff);
            area.priority = EditorGUILayout.IntField("Priority", area.priority);
            EditorGUILayout.LabelField("Boundary Points", area.GetWorldPolygon()?.Length.ToString() ?? "0");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_activeAsset, "Edit Spatial Area");
                area.Normalize($"Area {index + 1}");
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
        }

        private void DrawRegionDetails(int index)
        {
            if (_activeAsset.regionSets == null || index < 0 || index >= _activeAsset.regionSets.Count || _activeAsset.regionSets[index] == null)
                return;

            PungentSpatialRegionSet set = _activeAsset.regionSets[index];
            EditorGUI.BeginChangeCheck();
            set.metadata.displayName = EditorGUILayout.TextField("Display Name", set.metadata.displayName);
            set.metadata.color = EditorGUILayout.ColorField("Color", set.metadata.color);
            set.visible = EditorGUILayout.Toggle("Visible", set.visible);
            set.locked = EditorGUILayout.Toggle("Locked", set.locked);
            EditorGUILayout.LabelField("Shared Vertices", set.vertices == null ? "0" : set.vertices.Count.ToString());
            EditorGUILayout.LabelField("Faces", set.faces == null ? "0" : set.faces.Count.ToString());
            if (GUILayout.Button("Reset To Single Face"))
            {
                Undo.RecordObject(_activeAsset, "Reset Spatial Region Set");
                set.ResetToSingleFace();
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
            EditorGUILayout.HelpBox("Face cutting and partition operations are planned. This MVP supports region display, shared vertices, and validation.", MessageType.Info);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_activeAsset, "Edit Spatial Region Set");
                set.Normalize($"Region Set {index + 1}");
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
        }

        private void DrawMarkerDetails(int index)
        {
            if (_activeAsset.markers == null || index < 0 || index >= _activeAsset.markers.Count || _activeAsset.markers[index] == null)
                return;

            PungentSpatialMarker marker = _activeAsset.markers[index];
            EditorGUI.BeginChangeCheck();
            marker.metadata.displayName = EditorGUILayout.TextField("Display Name", marker.metadata.displayName);
            marker.color = EditorGUILayout.ColorField("Color", marker.color);
            marker.visible = EditorGUILayout.Toggle("Visible", marker.visible);
            marker.locked = EditorGUILayout.Toggle("Locked", marker.locked);
            marker.worldPosition = EditorGUILayout.Vector3Field("World Position", marker.worldPosition);
            marker.useProjectedPosition = EditorGUILayout.Toggle("Use Projected", marker.useProjectedPosition);
            marker.normalizedPosition = EditorGUILayout.Vector2Field("Normalized", marker.normalizedPosition);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_activeAsset, "Edit Spatial Marker");
                marker.Normalize($"Marker {index + 1}");
                EditorUtility.SetDirty(_activeAsset);
                MarkCacheDirty();
            }
        }

        private void DrawComponentDetails(PungentSpatialProviderCache.Record record)
        {
            if (record == null)
                return;

            EditorGUILayout.LabelField("Name", record.displayName);
            EditorGUILayout.LabelField("Kind", record.kind.ToString());
            EditorGUILayout.LabelField("Points", record.pointCount.ToString());
            EditorGUILayout.LabelField("Warnings", record.warningCount.ToString());
            using (new EditorGUI.DisabledScope(record.sourceObject == null))
            {
                if (GUILayout.Button("Select Source"))
                    SelectRecord(record);
                if (GUILayout.Button("Frame Source"))
                    FrameRecord(record);
                if (GUILayout.Button("Ping Source"))
                    EditorGUIUtility.PingObject(record.sourceObject);
            }
        }

        private void DrawSelectionSummary()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("No scene object selected.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Selection", selected.name);
            bool hasPath = selected.GetComponentInParent<IPungentPathPointProvider>() != null || selected.GetComponentInParent<IPungentPathQueryProvider>() != null;
            bool hasArea = selected.GetComponentInParent<IPungentAreaShapeProvider>() != null || selected.GetComponentInParent<IPungentAreaVolumeProvider>() != null;
            EditorGUILayout.LabelField("Spatial Providers", $"{(hasPath ? "Path " : string.Empty)}{(hasArea ? "Area" : string.Empty)}");
        }

        private void DrawBridgePlaceholders()
        {
            EditorGUILayout.HelpBox("Future bridge points are intentionally disabled here until the dedicated packages are present: Runtime Map, Spatial Query/Interaction, Data Sheet, and Board.", MessageType.None);
        }

        private void DrawRecordRow(PungentSpatialProviderCache.Record record)
        {
            bool selected = string.Equals(_selectedRecordKey, record.key);
            Color previous = GUI.color;
            if (selected)
                GUI.color = new Color(0.7f, 0.88f, 1f, 1f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.color = previous;
                Rect swatch = GUILayoutUtility.GetRect(8f, 18f, GUILayout.Width(8f));
                EditorGUI.DrawRect(swatch, record.color);

                if (GUILayout.Button(new GUIContent(record.displayName, record.searchText), EditorStyles.label))
                    SelectRecordInWorkbench(record);

                GUILayout.Label(record.kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(72f));
                if (record.warningCount > 0)
                    GUILayout.Label(record.warningCount.ToString(), EditorStyles.miniButton, GUILayout.Width(24f));
                if (GUILayout.Button("S", EditorStyles.miniButton, GUILayout.Width(22f)))
                    SelectRecord(record);
                if (GUILayout.Button("F", EditorStyles.miniButton, GUILayout.Width(22f)))
                    FrameRecord(record);
            }

            GUI.color = previous;
        }

        private bool PassesFilters(PungentSpatialProviderCache.Record record)
        {
            if (record == null)
                return false;
            if (!record.visible && !_showHidden)
                return false;
            if (_invalidOnly && record.warningCount <= 0)
                return false;
            if (!string.IsNullOrWhiteSpace(_search) && (record.searchText == null || !record.searchText.Contains(_search.ToLowerInvariant())))
                return false;

            switch (record.kind)
            {
                case PungentSpatialItemKind.Path:
                    return _filterPaths;
                case PungentSpatialItemKind.Area:
                    return _filterAreas;
                case PungentSpatialItemKind.RegionSet:
                case PungentSpatialItemKind.RegionFace:
                    return _filterRegions;
                case PungentSpatialItemKind.Marker:
                    return _filterMarkers;
                case PungentSpatialItemKind.GeneratedOutput:
                    return _filterGenerated;
                default:
                    return true;
            }
        }

        private PungentSpatialProviderCache.Record GetSelectedRecord()
        {
            IReadOnlyList<PungentSpatialProviderCache.Record> records = _cache.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (string.Equals(records[i].key, _selectedRecordKey))
                    return records[i];
            }

            return null;
        }

        private void SelectRecordInWorkbench(PungentSpatialProviderCache.Record record)
        {
            _selectedRecordKey = record.key;
            if (record.kind == PungentSpatialItemKind.Path || record.kind == PungentSpatialItemKind.Area)
                _targetKind = record.kind == PungentSpatialItemKind.Path ? PungentSpatialWorkbenchTargetKind.Path : PungentSpatialWorkbenchTargetKind.Area;
            if (record.sourceAsset == _activeAsset)
            {
                _planState.selectedKind = record.kind;
                _planState.selectedIndex = record.itemIndex;
                _planState.selectedVertex = -1;
            }
            Repaint();
        }

        private void SelectRecord(PungentSpatialProviderCache.Record record)
        {
            SelectRecordInWorkbench(record);
            if (record.sourceComponent != null)
                Selection.activeGameObject = record.sourceComponent.gameObject;
            else if (record.sourceObject != null)
                Selection.activeObject = record.sourceObject;
        }

        private void FrameRecord(PungentSpatialProviderCache.Record record)
        {
            if (record == null)
                return;
            SelectRecordInWorkbench(record);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.Frame(record.bounds.size.sqrMagnitude > 0.001f ? record.bounds : new Bounds(Vector3.zero, Vector3.one), false);
        }

        private void CreateAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Spatial Authoring Asset", "PungentSpatialAuthoringAsset", "asset", "Choose where to save the Spatial Authoring Asset.");
            if (string.IsNullOrWhiteSpace(path))
                return;

            PungentSpatialAuthoringAsset asset = CreateInstance<PungentSpatialAuthoringAsset>();
            asset.Normalize();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _activeAsset = asset;
            EditorGUIUtility.PingObject(asset);
            MarkCacheDirty();
        }

        private void AddPath()
        {
            if (_activeAsset == null)
            {
                CreateAsset();
                if (_activeAsset == null)
                    return;
            }

            Undo.RecordObject(_activeAsset, "Add Spatial Path");
            PungentSpatialPath path = new PungentSpatialPath();
            Vector3 a = _activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.4f, 0.5f), _activeAsset.projection.origin.y);
            Vector3 b = _activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.6f, 0.5f), _activeAsset.projection.origin.y);
            path.worldPoints.Add(a);
            path.worldPoints.Add(b);
            path.metadata.displayName = $"Path {_activeAsset.paths.Count + 1}";
            path.Normalize(path.metadata.displayName);
            _activeAsset.paths.Add(path);
            EditorUtility.SetDirty(_activeAsset);
            MarkCacheDirty();
            _tab = PungentSpatialWorkbenchTab.Plan;
            _planState.selectedKind = PungentSpatialItemKind.Path;
            _planState.selectedIndex = _activeAsset.paths.Count - 1;
            _planState.selectedVertex = -1;
        }

        private void AddArea()
        {
            if (_activeAsset == null)
            {
                CreateAsset();
                if (_activeAsset == null)
                    return;
            }

            Undo.RecordObject(_activeAsset, "Add Spatial Area");
            PungentSpatialArea area = new PungentSpatialArea();
            area.metadata.displayName = $"Area {_activeAsset.areas.Count + 1}";
            area.center = _activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.5f, 0.5f), _activeAsset.projection.origin.y);
            area.worldPolygon.Add(_activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.45f, 0.45f), _activeAsset.projection.origin.y));
            area.worldPolygon.Add(_activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.45f, 0.55f), _activeAsset.projection.origin.y));
            area.worldPolygon.Add(_activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.55f, 0.55f), _activeAsset.projection.origin.y));
            area.worldPolygon.Add(_activeAsset.projection.NormalizedToWorldClamped(new Vector2(0.55f, 0.45f), _activeAsset.projection.origin.y));
            area.Normalize(area.metadata.displayName);
            _activeAsset.areas.Add(area);
            EditorUtility.SetDirty(_activeAsset);
            MarkCacheDirty();
            _tab = PungentSpatialWorkbenchTab.Plan;
            _planState.selectedKind = PungentSpatialItemKind.Area;
            _planState.selectedIndex = _activeAsset.areas.Count - 1;
            _planState.selectedVertex = -1;
        }

        private void AddRegionSet()
        {
            if (_activeAsset == null)
            {
                CreateAsset();
                if (_activeAsset == null)
                    return;
            }

            Undo.RecordObject(_activeAsset, "Add Spatial Region Set");
            PungentSpatialRegionSet regionSet = new PungentSpatialRegionSet();
            regionSet.metadata.displayName = $"Region Set {_activeAsset.regionSets.Count + 1}";
            regionSet.ResetToSingleFace();
            regionSet.Normalize(regionSet.metadata.displayName);
            _activeAsset.regionSets.Add(regionSet);
            EditorUtility.SetDirty(_activeAsset);
            MarkCacheDirty();
        }

        private LayerMask PungentLayerMaskField(string label, LayerMask selected)
        {
            selected.value = EditorGUILayout.MaskField(label, selected.value, UnityEditorInternal.InternalEditorUtility.layers);
            return selected;
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (_activeAsset == null)
                return;

            PungentSpatialSceneDrawer.DrawAsset(_activeAsset, _selectedOnly, true);
            if (_tab == PungentSpatialWorkbenchTab.Plan || _tab == PungentSpatialWorkbenchTab.Scene)
            {
                if (_planState.selectedKind == PungentSpatialItemKind.Path)
                    PungentSpatialSceneHandleTool.DrawPathPointHandles(_activeAsset, _planState.selectedIndex, _editMode);
                else if (_planState.selectedKind == PungentSpatialItemKind.Area)
                    PungentSpatialSceneHandleTool.DrawAreaPointHandles(_activeAsset, _planState.selectedIndex, _editMode);
            }
        }
    }
#endif
}
