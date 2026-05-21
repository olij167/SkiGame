using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
#if UNITY_2021_2_OR_NEWER
    using UnityEditor.Overlays;
    using UnityEditor.Toolbars;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;
#endif

    public sealed class PungentPathAuthoringToolkitWindow : EditorWindow
    {
        private enum EditMode { View, Add, Move, Insert, Delete, Split }
        private enum ToolkitTab { Overview, SceneObjects, Selection, Generation, Validation, Bridges }
        private enum WorkbenchPage { Overview, Geometry, Output, Diagnostics, Advanced }

        private const string WorkbenchPagePrefKey = "PungentFunkUtilities.SpatialAuthoring.WorkbenchPage";
        private const string WorkbenchLeftWidthPrefKey = "PungentFunkUtilities.SpatialAuthoring.WorkbenchLeftWidth";
        private const float WorkbenchNarrowWidth = 760f;
        private const float WorkbenchLeftMinWidth = 220f;
        private const float WorkbenchLeftDefaultWidth = 260f;
        private const float WorkbenchLeftMaxWidth = 360f;
        private const float WorkbenchSplitterWidth = 5f;

        private readonly List<Vector3> _points = new List<Vector3>();
        private Vector2 _scroll;
        private Vector2 _areaScroll;
        private Vector2 _detailScroll;
        private ToolkitTab _tab = ToolkitTab.Overview;
        private WorkbenchPage _workbenchPage = WorkbenchPage.Overview;
        private float _workbenchLeftColumnWidth = WorkbenchLeftDefaultWidth;
        private bool _drawScenePreview = false;
        private bool _drawLabels = true;
        private bool _drawRibbon = true;
        private bool _snapToSurface = true;
        private LayerMask _surfaceMask = ~0;
        private float _handleSize = 0.6f;
        private float _pathWidth = 1.5f;
        private EditMode _editMode = EditMode.Move;
        private int _selectedIndex = -1;
        private string _status = "Ready.";
        private bool _sceneGuiSubscribed;
        private double _nextAllowedSceneRepaintTime;
        private double _nextAllowedWindowRepaintTime;
        private PungentAreaAuthoringShape _activeArea;
        private SerializedObject _pathSerializedObject;
        private SerializedObject _areaSerializedObject;
        private Vector2 _sceneObjectsScroll;
        private string _sceneObjectSearch = string.Empty;
        private bool _showScenePaths = true;
        private bool _showSceneAreas = true;
        private bool _drawAreaScenePreview = true;
        private bool _drawAreaLabels = true;
        private bool _areaSnapToSurface = true;
        private LayerMask _areaSurfaceMask = ~0;
        private float _areaHandleSize = 0.6f;
        private int _selectedAreaPoint = -1;
        private bool _performanceScanned;
        private int _performancePathCount;
        private int _performancePreviewPathCount;
        private int _performanceAreaCount;
        private int _performancePreviewAreaCount;
        private readonly List<PungentSpatialValidationIssue> _pathValidationIssues = new List<PungentSpatialValidationIssue>();
        private readonly List<PungentSpatialValidationIssue> _areaValidationIssues = new List<PungentSpatialValidationIssue>();
        private bool _hotkeysFoldout;
        private bool _workbenchIdentityFoldout = true;
        private bool _workbenchPreviewFoldout = true;
        private bool _workbenchGeometryFoldout = true;
        private bool _workbenchSurfaceFoldout = false;
        private bool _workbenchRuntimeFoldout = false;
        private bool _workbenchOverviewFoldout = true;
        private bool _workbenchOutputFoldout = true;
        private bool _workbenchDiagnosticsFoldout = true;
        private bool _workbenchLinksFoldout = true;
        private bool _workbenchLegacyPathOutputFoldout = false;
        private bool _workbenchBridgesFoldout = false;
        private bool _workbenchAdvancedGeometryFoldout = false;
        private bool _isResizingWorkbenchSidebar;

        public static void Open()
        {
            PungentSpatialAuthoringWindow.Open();
        }

        public static bool HasOpenWindow()
        {
            return HasOpenInstances<PungentPathAuthoringToolkitWindow>();
        }

        public static void OpenWithArea(PungentAreaAuthoringShape area)
        {
            PungentSpatialAuthoringWindow.OpenWithArea(area);
            return;
#pragma warning disable CS0162
            PungentPathAuthoringToolkitWindow window = GetWindow<PungentPathAuthoringToolkitWindow>("Spatial Authoring");
            window.minSize = new Vector2(620f, 420f);
            window.SetActiveArea(area);
            window._tab = ToolkitTab.Selection;
            window._workbenchPage = WorkbenchPage.Overview;
            window.RefreshSceneGuiSubscription();
            window.Show();
#pragma warning restore CS0162
        }

        public static void OpenWithPath(ModularPathSpawner path)
        {
            PungentSpatialAuthoringWindow.OpenWithPath(path);
            return;
#pragma warning disable CS0162
            PungentPathAuthoringToolkitWindow window = GetWindow<PungentPathAuthoringToolkitWindow>("Spatial Authoring");
            window.minSize = new Vector2(620f, 420f);
            if (path != null)
            {
                Selection.activeGameObject = path.gameObject;
                PungentSpatialAuthoringEditorState.RefreshSelection();
                PungentSpatialAuthoringEditorState.SetEditModeActive(true);
            }
            window._tab = ToolkitTab.Selection;
            window._workbenchPage = WorkbenchPage.Overview;
            window.RefreshSceneGuiSubscription();
            window.Show();
#pragma warning restore CS0162
        }

        private void OnEnable()
        {
            _workbenchPage = (WorkbenchPage)Mathf.Clamp(EditorPrefs.GetInt(WorkbenchPagePrefKey, (int)WorkbenchPage.Overview), 0, Enum.GetValues(typeof(WorkbenchPage)).Length - 1);
            _workbenchLeftColumnWidth = Mathf.Clamp(EditorPrefs.GetFloat(WorkbenchLeftWidthPrefKey, WorkbenchLeftDefaultWidth), WorkbenchLeftMinWidth, WorkbenchLeftMaxWidth);
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            PungentSpatialAuthoringEditorState.Changed += OnSpatialStateChanged;
            Selection.selectionChanged += OnSelectionChanged;
            PungentSpatialAuthoringSceneObjectCache.Changed += RequestWindowRepaint;
            OnSelectionChanged();
            RefreshSceneGuiSubscription();
        }

        private void OnDisable()
        {
            SaveWorkbenchPrefs();
            PungentSpatialAuthoringEditorState.Changed -= OnSpatialStateChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            PungentSpatialAuthoringSceneObjectCache.Changed -= RequestWindowRepaint;
            SetSceneGuiSubscribed(false);
        }

        private void OnGUI()
        {
            PungentEditorPerformanceUtility.RecordWindowRepaint(this);
            UtilityWindowTheme.Header("Spatial Authoring Toolkit (Legacy Surface)", "Compatibility surface for existing scene path and area handles. New document, Plan, Bake, Objects, and Validate workflows live in the Spatial Authoring Workbench.", _status);

            DrawAdaptiveWorkbench();
        }

        private void DrawAdaptiveWorkbench()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner activePath = PungentSpatialAuthoringEditorState.ActivePath;
            PungentAreaAuthoringShape activeArea = PungentSpatialAuthoringEditorState.ActiveArea;
            _activeArea = activeArea;
            bool hasSpatialSelection = activePath != null || activeArea != null;

            bool compact = position.width < WorkbenchNarrowWidth;

            _workbenchLeftColumnWidth = Mathf.Clamp(_workbenchLeftColumnWidth, WorkbenchLeftMinWidth, Mathf.Min(WorkbenchLeftMaxWidth, position.width * 0.45f));
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(_workbenchLeftColumnWidth), GUILayout.ExpandHeight(true)))
                    DrawWorkbenchLeftColumn(activePath, activeArea, false);

                DrawWorkbenchSplitter();

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    if (hasSpatialSelection)
                        DrawWorkbenchPageToolbar(compact);
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    DrawWorkbenchRightPanel(activePath, activeArea);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawWorkbenchLeftColumn(ModularPathSpawner activePath, PungentAreaAuthoringShape activeArea, bool narrow)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                bool hasSelection = activePath != null || activeArea != null;
                UtilityWindowTheme.SectionTitle("Scene Objects", UtilityWindowTheme.Teal, hasSelection ? "Switch target" : "Choose a target");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (hasSelection)
                    {
                        if (GUILayout.Button("New Path", GUILayout.Height(24f)))
                            PungentSpatialAuthoringActions.CreatePathAtScenePivot();
                        if (GUILayout.Button("New Area", GUILayout.Height(24f), GUILayout.Width(82f)))
                            PungentSpatialAuthoringActions.CreateAreaAtScenePivot();
                    }
                    else
                    {
                        if (UtilityWindowTheme.TintedButton("Create Path", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                            PungentSpatialAuthoringActions.CreatePathAtScenePivot();
                        if (GUILayout.Button("Create Area", GUILayout.Height(28f), GUILayout.Width(98f)))
                            PungentSpatialAuthoringActions.CreateAreaAtScenePivot();
                    }
                }

                if (hasSelection)
                {
                    EditorGUILayout.Space(3f);
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(activePath != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, 0.10f, 0.04f, 4, 3)))
                    {
                        EditorGUILayout.LabelField(activePath != null ? "Selected Path" : "Selected Area", activePath != null ? activePath.SpatialDisplayName : activeArea.SpatialDisplayName, UtilityWindowTheme.MutedMiniLabelStyle);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(PungentSpatialAuthoringEditorState.EditModeActive ? "Exit Edit" : "Edit", GUILayout.Height(24f)))
                            {
                                if (activePath != null)
                                    PungentSpatialAuthoringEditorState.ToggleEditModeFor(activePath);
                                else
                                    PungentSpatialAuthoringEditorState.ToggleEditModeFor(activeArea);
                            }
                            if (GUILayout.Button("Frame", GUILayout.Height(24f), GUILayout.Width(58f)))
                                PungentSpatialAuthoringActions.FrameActive();
                        }
                    }
                }
            }

            DrawSceneSpatialObjectList(compact: true, limitCompact: false, scrollCompact: !narrow, fillHeight: !narrow);
        }

        private void DrawWorkbenchSplitter()
        {
            Rect rect = GUILayoutUtility.GetRect(WorkbenchSplitterWidth, WorkbenchSplitterWidth, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            Event e = Event.current;
            if (e.type == EventType.Repaint)
            {
                Color color = _isResizingWorkbenchSidebar
                    ? new Color(UtilityWindowTheme.Teal.r, UtilityWindowTheme.Teal.g, UtilityWindowTheme.Teal.b, 0.65f)
                    : new Color(0.35f, 0.35f, 0.35f, 0.35f);
                EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y, 1f, rect.height), color);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _isResizingWorkbenchSidebar = true;
                e.Use();
            }

            if (_isResizingWorkbenchSidebar && e.type == EventType.MouseDrag)
            {
                float maxWidth = Mathf.Min(WorkbenchLeftMaxWidth, position.width - 300f);
                _workbenchLeftColumnWidth = Mathf.Clamp(e.mousePosition.x, WorkbenchLeftMinWidth, Mathf.Max(WorkbenchLeftMinWidth, maxWidth));
                EditorPrefs.SetFloat(WorkbenchLeftWidthPrefKey, _workbenchLeftColumnWidth);
                Repaint();
                e.Use();
            }

            if (_isResizingWorkbenchSidebar && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizingWorkbenchSidebar = false;
                e.Use();
            }
        }

        private void DrawWorkbenchPageToolbar(bool compact)
        {
            WorkbenchPage[] pages = { WorkbenchPage.Overview, WorkbenchPage.Geometry, WorkbenchPage.Output, WorkbenchPage.Diagnostics, WorkbenchPage.Advanced };
            string[] labels = compact
                ? new[] { "Overview", "Geometry", "Output", "Issues", "Links" }
                : new[] { "Overview", "Geometry", "Output", "Diagnostics", "Links / Templates" };

            int current = Array.IndexOf(pages, _workbenchPage);
            if (current < 0)
                current = 0;

            int selected = GUILayout.Toolbar(current, labels, EditorStyles.toolbarButton, GUILayout.Height(24f));
            if (selected != current)
            {
                SetWorkbenchPage(pages[Mathf.Clamp(selected, 0, pages.Length - 1)]);
                GUI.FocusControl(null);
            }
        }

        private void SetWorkbenchPage(WorkbenchPage page)
        {
            _workbenchPage = page;
            EditorPrefs.SetInt(WorkbenchPagePrefKey, (int)page);
            FocusWorkbenchSection(page);
            _detailScroll = Vector2.zero;
        }

        private void FocusWorkbenchSection(WorkbenchPage page)
        {
            _workbenchOverviewFoldout = page == WorkbenchPage.Overview;
            _workbenchGeometryFoldout = page == WorkbenchPage.Geometry;
            _workbenchOutputFoldout = page == WorkbenchPage.Output;
            _workbenchDiagnosticsFoldout = page == WorkbenchPage.Diagnostics;
            _workbenchLinksFoldout = page == WorkbenchPage.Advanced;
        }

        private void DrawWorkbenchRightPanel(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            if (path == null && area == null)
            {
                DrawWorkbenchEmptyDetailPage();
                return;
            }

            DrawWorkbenchSelectedStack(path, area);
        }

        private void DrawWorkbenchSelectedStack(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            DrawWorkbenchStackSection("Overview", WorkbenchPage.Overview, path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, ref _workbenchOverviewFoldout, () => DrawWorkbenchOverviewPage(path, area));
            DrawWorkbenchStackSection("Geometry", WorkbenchPage.Geometry, path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, ref _workbenchGeometryFoldout, () => DrawWorkbenchGeometryPage(path, area));
            DrawWorkbenchStackSection("Output", WorkbenchPage.Output, UtilityWindowTheme.Green, ref _workbenchOutputFoldout, () => DrawWorkbenchOutputPage(path, area));
            DrawWorkbenchStackSection("Diagnostics", WorkbenchPage.Diagnostics, UtilityWindowTheme.Amber, ref _workbenchDiagnosticsFoldout, () => DrawWorkbenchDiagnosticsPage(path, area));
            DrawWorkbenchStackSection("Links / Templates", WorkbenchPage.Advanced, UtilityWindowTheme.Neutral, ref _workbenchLinksFoldout, () => DrawWorkbenchAdvancedPage(path, area));
        }

        private void DrawWorkbenchStackSection(string label, WorkbenchPage page, Color tint, ref bool foldout, Action body)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foldout = EditorGUILayout.Foldout(foldout, label, true, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (_workbenchPage == page)
                        UtilityWindowTheme.CountPill("Focused", tint, 78f);
                }

                if (foldout)
                    body();
            }
        }

        private void DrawWorkbenchEmptyDetailPage()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scene Dashboard", UtilityWindowTheme.Teal, "No spatial selection");
                EditorGUILayout.HelpBox("Create or select a loaded-scene path/area from the left column. This panel becomes the selected object's setup, output, and diagnostics workbench.", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("Flow", UtilityWindowTheme.Neutral);
                EditorGUILayout.LabelField("1. Design paths and areas with the Scene View overlay.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("2. Use this window to find objects, review status, and configure setup.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("3. Add Spatial Output Recipes and runtime/provider links after the geometry is stable.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawWorkbenchOverviewPage(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            DrawSelectionWorkbenchSummary(path, area);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Selected Object", path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, "Inspect setup, edit geometry in Scene View");
                DrawOverviewIdentitySummary(path, area);
                EditorGUILayout.Space(6f);
                if (path != null)
                    DrawOverviewPathFields(path);
                else
                    DrawOverviewAreaFields(area);
            }

            DrawOverviewStatusPills(path, area);
        }

        private void DrawOverviewIdentitySummary(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            string displayName = path != null ? path.SpatialDisplayName : area.SpatialDisplayName;
            IReadOnlyList<string> tags = path != null ? path.SpatialTags : area.SpatialTags;
            Color color = path != null ? path.pathColor : area.color;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(path != null ? "Path" : "Area", path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, 74f);
                EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                Rect colorRect = GUILayoutUtility.GetRect(42f, 18f, GUILayout.Width(42f));
                EditorGUI.DrawRect(colorRect, color);
            }

            string tagText = tags != null && tags.Count > 0 ? string.Join(", ", tags) : "No categories";
            EditorGUILayout.LabelField("Categories", tagText, UtilityWindowTheme.MutedMiniLabelStyle);
            EditorGUILayout.LabelField("Full setup is edited in the component Inspector. This window is for finding, outputs, diagnostics, and links.", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawOverviewIdentityFields(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            SerializedObject so = GetSelectionSerializedObject(path, area);
            if (so == null)
                return;

            so.Update();
            EditorGUI.BeginChangeCheck();
            DrawSerializedProperty(so, "displayName", "Display Name", "Name shown in overlays, lists, and future exports.");
            DrawSerializedProperty(so, "categories", "Categories / Tags", "Generic category or tag list.");
            DrawSerializedProperty(so, path != null ? "pathColor" : "color", "Color", "Scene preview colour.");
            ApplySelectionSerializedChanges(so, path, area, EditorGUI.EndChangeCheck(), previewRefresh: path != null);
        }

        private void DrawOverviewPathFields(ModularPathSpawner path)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.04f, 4, 3)))
            {
                UtilityWindowTheme.SectionTitle("Geometry Summary", UtilityWindowTheme.Blue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(path.closedLoop ? "Closed" : "Open", path.closedLoop ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 78f);
                    UtilityWindowTheme.CountPill(path.pathMode.ToString(), UtilityWindowTheme.Purple, 128f);
                    if (path.exposePreviewCorridorWidth)
                        UtilityWindowTheme.CountPill("Width " + path.previewCorridorWidth.ToString("0.##"), UtilityWindowTheme.Cyan, 94f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Edit In Scene", GUILayout.Width(112f)))
                        PungentSpatialAuthoringEditorState.ToggleEditModeFor(path);
                }
            }
        }

        private void DrawOverviewAreaFields(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.04f, 4, 3)))
            {
                UtilityWindowTheme.SectionTitle("Geometry Summary", UtilityWindowTheme.Purple);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(area.shapeMode.ToString(), UtilityWindowTheme.Purple, 150f);
                    UtilityWindowTheme.CountPill(area.verticalMode.ToString(), UtilityWindowTheme.Teal, 148f);
                    UtilityWindowTheme.CountPill("Points " + area.PolygonPointCount, UtilityWindowTheme.Neutral, 92f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Edit In Scene", GUILayout.Width(112f)))
                        PungentSpatialAuthoringEditorState.ToggleEditModeFor(area);
                }
            }
        }

        private void DrawOverviewStatusPills(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            int generatedCount = GetGeneratedOutputCount(path, area);
            int issueCount;
            string warning;
            GetValidationSummary(path, area, out issueCount, out warning);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("Status", UtilityWindowTheme.Neutral, "At a glance");
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(path != null ? "Path" : "Area", path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, 74f);
                    UtilityWindowTheme.CountPill("Points " + (path != null ? path.PointCount : area.PolygonPointCount), UtilityWindowTheme.Neutral, 92f);
                    UtilityWindowTheme.CountPill("Generated " + generatedCount, generatedCount > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 116f);
                    UtilityWindowTheme.CountPill(issueCount == 0 ? "No Issues" : "Issues " + issueCount, issueCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 96f);
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(warning))
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        private void DrawWorkbenchGeometryPage(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle(path != null ? "Path Geometry" : "Area Geometry", path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, path != null ? "Shape, sampling, corridor" : "Shape, footprint, height");
                if (path != null)
                    DrawPathGeometrySetup(path);
                else
                    DrawAreaGeometrySetup(area);
            }
        }

        private void DrawWorkbenchOutputPage(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            DrawWorkbenchModularOutputSection(path, area);
        }

        private void DrawWorkbenchDiagnosticsPage(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            DrawWorkbenchDiagnosticsSection(path, area);
        }

        private void DrawWorkbenchAdvancedPage(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("Inspector-Owned Setup", UtilityWindowTheme.Neutral, "Preview, surface, runtime, and stable IDs");
                EditorGUILayout.HelpBox("Detailed component setup now lives in the selected object's Inspector. Use this page for bridge/template handoffs and package-state notes.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping Selected Component", GUILayout.Height(26f)))
                        EditorGUIUtility.PingObject(path != null ? (UnityEngine.Object)path : area);
                    if (GUILayout.Button("Open Toolkit Help", GUILayout.Height(26f), GUILayout.Width(130f)))
                        _status = "Spatial Authoring help entries are planned for the package documentation pass.";
                }
            }
            DrawWorkbenchBridgesSection();
        }

        private void DrawNoSelectionWorkbench()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scene Spatial Objects", UtilityWindowTheme.Teal, "Select or create");
                EditorGUILayout.HelpBox("Select an existing path or area, or create a new one. Point editing happens in the Scene View Spatial Authoring overlay.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create Path", UtilityWindowTheme.Green, GUILayout.Height(30f)))
                        PungentSpatialAuthoringActions.CreatePathAtScenePivot();
                    if (GUILayout.Button("Create Area", GUILayout.Height(30f), GUILayout.Width(120f)))
                        PungentSpatialAuthoringActions.CreateAreaAtScenePivot();
                    if (GUILayout.Button("Refresh List", GUILayout.Height(30f), GUILayout.Width(104f)))
                        PungentSpatialAuthoringSceneObjectCache.ForceRebuild();
                }
            }

            DrawSceneSpatialObjectList(compact: false);
        }

        private void DrawSelectionWorkbenchSummary(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple)))
            {
                string title = path != null ? "Active Path" : "Active Area";
                string summary = path != null
                    ? path.SpatialDisplayName + " / " + path.PointCount + " point(s)"
                    : area.SpatialDisplayName + " / " + area.shapeMode + " / " + area.PolygonPointCount + " point(s)";
                UtilityWindowTheme.SectionTitle(title, path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple, summary);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(PungentSpatialAuthoringEditorState.EditModeActive ? "Exit Scene Edit" : "Edit In Scene", GUILayout.Height(28f), GUILayout.Width(126f)))
                    {
                        if (path != null)
                            PungentSpatialAuthoringEditorState.ToggleEditModeFor(path);
                        else
                            PungentSpatialAuthoringEditorState.ToggleEditModeFor(area);
                    }

                    if (GUILayout.Button("Frame", GUILayout.Height(28f), GUILayout.Width(64f)))
                        PungentSpatialAuthoringActions.FrameActive();
                    if (GUILayout.Button("Ping", GUILayout.Height(28f), GUILayout.Width(58f)))
                        EditorGUIUtility.PingObject(path != null ? path.gameObject : area.gameObject);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(PungentSpatialAuthoringEditorState.EditModeActive ? "Scene Edit" : "Dashboard", PungentSpatialAuthoringEditorState.EditModeActive ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 102f);
                }

                EditorGUILayout.LabelField(PungentSpatialAuthoringEditorState.BuildNextActionHint(), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawWorkbenchIdentitySection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _workbenchIdentityFoldout = EditorGUILayout.Foldout(_workbenchIdentityFoldout, "Stable IDs", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchIdentityFoldout)
                    return;

                SerializedObject so = GetSelectionSerializedObject(path, area);
                if (so == null)
                    return;

                so.Update();
                EditorGUI.BeginChangeCheck();
                DrawSerializedProperty(so, path != null ? "stablePathId" : "stableAreaId", "Stable ID", "Stable generic identifier used by optional bridges and exports.");
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(path != null ? (UnityEngine.Object)path : area);
                    PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                    RequestSceneRepaint();
                }
                else
                {
                    so.ApplyModifiedProperties();
                }
            }
        }

        private void DrawWorkbenchPreviewSection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _workbenchPreviewFoldout = EditorGUILayout.Foldout(_workbenchPreviewFoldout, "Preview", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchPreviewFoldout)
                    return;

                DrawScenePreviewPreferences();

                SerializedObject so = GetSelectionSerializedObject(path, area);
                if (so == null)
                    return;

                so.Update();
                EditorGUI.BeginChangeCheck();
                if (path != null)
                {
                    DrawSerializedProperty(so, "previewWhileEditing", "Preview While Editing", "Draw selected path previews while editing.");
                    DrawSerializedProperty(so, "drawGizmos", "Draw Gizmos", "Draw path gizmos and previews.");
                    DrawSerializedProperty(so, "drawUnselectedGizmo", "Draw Unselected", "Draw lightweight gizmos when this path is not selected.");
                }
                else
                {
                    DrawSerializedProperty(so, "drawGizmo", "Draw Gizmo", "Draw area gizmos and previews.");
                    DrawSerializedProperty(so, "drawUnselectedGizmo", "Draw Unselected", "Draw lightweight area gizmos when this area is not selected.");
                }

                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(path != null ? (UnityEngine.Object)path : area);
                    PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                    RequestSceneRepaint();
                }
                else
                {
                    so.ApplyModifiedProperties();
                }
            }
        }

        private void DrawWorkbenchGeometrySection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(path != null ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple)))
            {
                _workbenchGeometryFoldout = EditorGUILayout.Foldout(_workbenchGeometryFoldout, path != null ? "Path" : "Area", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchGeometryFoldout)
                    return;

                if (path != null)
                    DrawPathGeometrySetup(path);
                else
                    DrawAreaGeometrySetup(area);
            }
        }

        private void DrawPathGeometrySetup(ModularPathSpawner path)
        {
            if (_pathSerializedObject == null || _pathSerializedObject.targetObject != path)
                _pathSerializedObject = new SerializedObject(path);

            _pathSerializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPathProperty("closedLoop", "Closed Loop", "Connect the last control point back to the first.");
            DrawPathProperty("pathMode", "Sampling", "Polyline or smooth Catmull-Rom sampling.");
            DrawPathProperty("exposePreviewCorridorWidth", "Use Corridor Width", "Expose corridor width to previews and Spatial Output Recipes.");
            if (path.exposePreviewCorridorWidth)
            {
                DrawPathProperty("previewCorridorWidth", "Corridor Width", "Authoring corridor width used by previews and corridor-side output recipes.");
                DrawPathProperty("previewCorridorAlpha", "Corridor Alpha", "Transparency for the authoring corridor ribbon preview.");
            }
            _workbenchAdvancedGeometryFoldout = EditorGUILayout.Foldout(_workbenchAdvancedGeometryFoldout, "Advanced Geometry", true, UtilityWindowTheme.SectionHeaderStyle);
            if (_workbenchAdvancedGeometryFoldout)
            {
                DrawPathProperty("samplesPerMeter", "Samples / Meter", "Smooth path sampling density.");
                DrawPathProperty("minSamplesPerSpan", "Min Samples / Span", "Minimum smooth samples per control-point span.");
                SerializedProperty points = _pathSerializedObject.FindProperty("localPoints");
                if (points != null)
                    EditorGUILayout.PropertyField(points, new GUIContent("Local Control Points", "Direct local-space control point array. Prefer Scene View editing for normal use."), true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                _pathSerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(path);
                PungentPathRebuildScheduler.RequestPreviewRefresh(path, this);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                RequestSceneRepaint();
            }
            else
            {
                _pathSerializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawAreaGeometrySetup(PungentAreaAuthoringShape area)
        {
            if (_areaSerializedObject == null || _areaSerializedObject.targetObject != area)
                _areaSerializedObject = new SerializedObject(area);

            _areaSerializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawAreaProperty("shapeMode", "Shape Mode", "Polygon, rectangle, circle, or bounds-derived footprint.");
            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ)
                DrawAreaProperty("rectangleSize", "Rectangle Size", "Local XZ rectangle dimensions.");
            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.CircleXZ)
                DrawAreaProperty("circleRadius", "Circle Radius", "Local XZ circle radius.");
            DrawAreaProperty("verticalMode", "Vertical Mode", "Area height interpretation.");
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.HeightFromTransform)
                DrawAreaProperty("height", "Height", "Height above the transform when using Height From Transform.");
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax)
            {
                DrawAreaProperty("minY", "Min Y", "Explicit world-space minimum Y.");
                DrawAreaProperty("maxY", "Max Y", "Explicit world-space maximum Y.");
            }
            _workbenchAdvancedGeometryFoldout = EditorGUILayout.Foldout(_workbenchAdvancedGeometryFoldout, "Advanced Geometry", true, UtilityWindowTheme.SectionHeaderStyle);
            if (_workbenchAdvancedGeometryFoldout && area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
                DrawAreaProperty("localPolygonPoints", "Local Polygon Points", "Local-space XZ polygon points. Prefer Scene View editing for normal use.");
            if (EditorGUI.EndChangeCheck())
            {
                _areaSerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(area);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                RequestSceneRepaint();
            }
            else
            {
                _areaSerializedObject.ApplyModifiedProperties();
            }

            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Place Polygon Point", GUILayout.Height(24f)))
                        AddAreaPolygonPoint();
                    if (GUILayout.Button("Reset Polygon Rectangle", GUILayout.Height(24f)))
                        ResetAreaPolygonRectangle();
                }
            }
        }

        private void DrawWorkbenchSurfaceSection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _workbenchSurfaceFoldout = EditorGUILayout.Foldout(_workbenchSurfaceFoldout, "Surface", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchSurfaceFoldout)
                    return;

                DrawSpatialSurfacePreferences(path);
                if (path != null)
                    DrawPathSurfaceSetup(path);
                else
                    EditorGUILayout.HelpBox("Area point placement uses the shared Scene Editing Surface settings above. Shape bounds from colliders or renderers still come from the selected area source.", MessageType.Info);
            }
        }

        private void DrawWorkbenchRuntimeSection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _workbenchRuntimeFoldout = EditorGUILayout.Foldout(_workbenchRuntimeFoldout, "Runtime", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchRuntimeFoldout)
                    return;

                if (path != null)
                {
                    EditorGUILayout.LabelField("Path Queries", "nearest point, distance, tangent, width", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Providers", "metadata, bounds, visualization, generated output", UtilityWindowTheme.MutedMiniLabelStyle);
                    if (path.TryEstimateGeneratedCounts(out int samples, out int segments, out int points, out float length))
                        EditorGUILayout.LabelField("Estimate", samples + " samples / " + segments + " segments / " + points + " point prefabs / " + length.ToString("0.0") + "m", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    if (_areaSerializedObject == null || _areaSerializedObject.targetObject != area)
                        _areaSerializedObject = new SerializedObject(area);

                    _areaSerializedObject.Update();
                    EditorGUI.BeginChangeCheck();
                    DrawAreaProperty("runtimeQueryable", "Runtime Query", "Allow ContainsWorldPoint to return true for this area.");
                    if (EditorGUI.EndChangeCheck())
                    {
                        _areaSerializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(area);
                        PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                    }
                    else
                    {
                        _areaSerializedObject.ApplyModifiedProperties();
                    }

                    EditorGUILayout.LabelField("Area Queries", "contains point, bounds, volume, polygon", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Providers", "shape, volume, bounds, visualization", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawWorkbenchModularOutputSection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Output", UtilityWindowTheme.Green, "Spatial Output Recipes first");

                DrawModularSpatialOutputRecipes(path != null ? (Component)path : area);

                if (path != null)
                    DrawLegacyPathOutputFoldout(path);
            }
        }

        private void DrawLegacyPathOutputFoldout(ModularPathSpawner path)
        {
            EditorGUILayout.Space(8f);
            _workbenchLegacyPathOutputFoldout = EditorGUILayout.Foldout(_workbenchLegacyPathOutputFoldout, "Legacy Built-In Output", true, UtilityWindowTheme.SectionHeaderStyle);
            if (!_workbenchLegacyPathOutputFoldout)
            {
                EditorGUILayout.LabelField("Built-in ModularPathSpawner output is preserved for compatibility. Prefer Spatial Output Recipes for new work.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            EditorGUILayout.HelpBox("Compatibility path output. New cross-source generation should use Spatial Output Recipes above.", MessageType.Info);
            DrawBuiltInPathOutput(path);
        }

        private void DrawBuiltInPathOutput(ModularPathSpawner path)
        {
            if (_pathSerializedObject == null || _pathSerializedObject.targetObject != path)
                _pathSerializedObject = new SerializedObject(path);

            UtilityWindowTheme.SectionTitle("Built-In Path Output", UtilityWindowTheme.Green, "Legacy compatibility");
            _pathSerializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPathProperty("segmentPrefab", "Segment Prefab", "Prefab repeated along the path centerline.");
            DrawPathProperty("pointPrefab", "Point Prefab", "Optional prefab placed at each control point.");
            DrawPathProperty("useSockets", "Use Sockets", "Use named child transforms to align segment joins.");
            DrawPathProperty("startSocketName", "Start Socket", "Child transform name used as the segment start socket.");
            DrawPathProperty("endSocketName", "End Socket", "Child transform name used as the segment end socket.");
            DrawPathProperty("placeByEndpointsWhenUsingSockets", "Place By Endpoints", "Map socket positions to sampled path endpoints.");
            DrawPathProperty("usePrefabLength", "Use Prefab Length", "Use prefab bounds to decide spacing.");
            DrawPathProperty(path.usePrefabLength ? "manualSegmentLength" : "fixedSpacing", path.usePrefabLength ? "Manual Length Fallback" : "Fixed Spacing", "Segment placement spacing.");
            DrawPathProperty("startOffset", "Start Offset", "Distance offset before first segment placement.");
            DrawPathProperty("conformToSurface", "Surface Conform", "Raycast generated output to the surface mask.");
            DrawPathProperty("surfaceMask", "Surface Mask", "Layer mask used for surface sampling.");
            DrawPathProperty("raycastStartHeight", "Raycast Height", "Height above each point used when raycasting downward.");
            DrawPathProperty("yOffset", "Y Offset", "Vertical offset applied after surface sampling.");
            DrawPathProperty("alignToSurfaceNormal", "Align To Surface Normal", "Use sampled surface normal as generated object up vector.");
            DrawPathProperty("sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both segment endpoints before placement.");
            DrawPathProperty("autoRebuildInEditor", "Auto Rebuild", "Queue a debounced generated-object rebuild after editor changes.");
            if (path.autoRebuildInEditor)
                DrawPathProperty("editorRebuildDebounce", "Rebuild Debounce", "Delay before automatic generated-object rebuild.");
            bool changed = EditorGUI.EndChangeCheck();
            _pathSerializedObject.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(path);
                PungentPathRebuildScheduler.QueueRebuild(path, this, "Queue Modular Path Rebuild");
                RequestSceneRepaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Generated " + path.ExistingGeneratedChildCount, path.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 118f);
                if (UtilityWindowTheme.TintedButton("Apply Built-In Output", UtilityWindowTheme.Green, GUILayout.Height(28f), GUILayout.Width(156f)))
                    PungentSpatialAuthoringActions.ApplyGeneratedOutput();
                using (new EditorGUI.DisabledScope(path.ExistingGeneratedChildCount == 0))
                {
                    if (GUILayout.Button("Clear Built-In Output", GUILayout.Height(28f), GUILayout.Width(154f)))
                        PungentSpatialAuthoringActions.ClearGeneratedOutput();
                    if (GUILayout.Button("Select Generated", GUILayout.Height(28f), GUILayout.Width(126f)))
                    {
                        Transform root = path.ExistingGeneratedRoot;
                        if (root != null)
                            Selection.activeGameObject = root.gameObject;
                    }
                }
            }
        }

        private void DrawModularSpatialOutputRecipes(Component source)
        {
            if (source == null)
                return;

            EditorGUILayout.Space(8f);
            UtilityWindowTheme.SectionTitle("Spatial Output Recipes", UtilityWindowTheme.Teal, source is PungentAreaAuthoringShape ? "Area boundary capable" : "Centerline and corridor capable");

            PungentModularSpatialOutput[] outputs = source.GetComponents<PungentModularSpatialOutput>();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (UtilityWindowTheme.TintedButton(outputs.Length == 0 ? "Add Spatial Output Recipe" : "Add Another Recipe", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                    CreateModularOutputRecipe(source);

                if (outputs.Length == 0)
                    EditorGUILayout.LabelField("No Spatial Output Recipe is attached to this spatial object.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            for (int i = 0; i < outputs.Length; i++)
                DrawModularSpatialOutputRecipe(outputs[i], i);
        }

        private void DrawModularSpatialOutputRecipe(PungentModularSpatialOutput output, int index)
        {
            if (output == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f, 5, 4)))
            {
                UtilityWindowTheme.SectionTitle("Recipe " + (index + 1), UtilityWindowTheme.Teal, output.sourceMode.ToString());
                SerializedObject so = new SerializedObject(output);
                so.Update();
                EditorGUI.BeginChangeCheck();
                DrawSerializedProperty(so, "source", "Source", "Path or area component used by this recipe.");
                DrawSerializedProperty(so, "sourceMode", "Source Mode", "Center, corridor side, both sides, or area boundary.");
                DrawSerializedProperty(so, "segmentPrefab", "Segment Prefab", "Prefab repeated along this recipe lane.");
                DrawSerializedProperty(so, "pointPrefab", "Point Prefab", "Optional prefab placed at source points.");
                DrawSerializedProperty(so, "useSockets", "Use Sockets", "Use named child transforms to align segment joins.");
                DrawSerializedProperty(so, "startSocketName", "Start Socket", "Child transform name used as the segment start socket.");
                DrawSerializedProperty(so, "endSocketName", "End Socket", "Child transform name used as the segment end socket.");
                DrawSerializedProperty(so, "placeByEndpointsWhenUsingSockets", "Place By Endpoints", "Map sockets to sampled endpoints.");
                DrawSerializedProperty(so, "usePrefabLength", "Use Prefab Length", "Use prefab bounds to decide spacing.");
                DrawSerializedProperty(so, output.usePrefabLength ? "manualSegmentLength" : "fixedSpacing", output.usePrefabLength ? "Manual Length Fallback" : "Fixed Spacing", "Segment placement spacing.");
                DrawSerializedProperty(so, "startOffset", "Start Offset", "Distance offset before first segment placement.");
                DrawSerializedProperty(so, "conformToSurface", "Surface Conform", "Raycast generated output to the surface mask.");
                DrawSerializedProperty(so, "surfaceMask", "Surface Mask", "Layer mask used for surface sampling.");
                DrawSerializedProperty(so, "raycastStartHeight", "Raycast Height", "Height above each point used when raycasting downward.");
                DrawSerializedProperty(so, "yOffset", "Y Offset", "Vertical offset applied after surface sampling.");
                DrawSerializedProperty(so, "alignToSurfaceNormal", "Align To Surface Normal", "Use sampled surface normal as generated object up vector.");
                DrawSerializedProperty(so, "sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both endpoints before placement.");
                DrawSerializedProperty(so, "autoGapSelfIntersections", "Auto Gap Self Intersections", "Skip and bridge path sections where the recipe lane crosses itself.");
                DrawSerializedProperty(so, "autoGapCorridorOverlaps", "Auto Gap Corridor Overlaps", "For corridor-side modes, skip and bridge overlap windows.");
                DrawSerializedProperty(so, "autoGapPaddingNormalized", "Auto Gap Padding", "Normalized padding around detected gap windows.");
                DrawSerializedProperty(so, "manualGaps", "Manual Gaps", "Normalized spans to skip during generation.");
                DrawSerializedProperty(so, "previewWhileEditing", "Preview While Editing", "Draw lightweight recipe preview.");
                DrawSerializedProperty(so, "autoRebuildInEditor", "Auto Rebuild", "Rebuild immediately after recipe changes from this window.");
                if (output.autoRebuildInEditor)
                    DrawSerializedProperty(so, "editorRebuildDebounce", "Rebuild Debounce", "Reserved debounce value for future recipe scheduling.");
                DrawSerializedProperty(so, "drawGizmos", "Draw Gizmos", "Draw recipe preview gizmos.");
                bool changed = EditorGUI.EndChangeCheck();
                so.ApplyModifiedProperties();
                if (changed)
                {
                    EditorUtility.SetDirty(output);
                    if (output.autoRebuildInEditor)
                        output.Rebuild();
                    RequestSceneRepaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Apply Recipe", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                    {
                        PungentSpatialOutputRecipeEditorUtility.ApplyRecipe(output);
                        _status = "Applied Spatial Output Recipe.";
                    }
                    using (new EditorGUI.DisabledScope(output.ExistingGeneratedChildCount == 0))
                    {
                        if (GUILayout.Button("Clear", GUILayout.Height(26f), GUILayout.Width(70f)))
                        {
                            PungentSpatialOutputRecipeEditorUtility.ClearRecipe(output);
                            _status = "Cleared Spatial Output Recipe.";
                        }
                        if (GUILayout.Button("Select Generated", GUILayout.Height(26f), GUILayout.Width(126f)))
                        {
                            Transform root = output.ExistingGeneratedRoot;
                            if (root != null)
                                Selection.activeGameObject = root.gameObject;
                        }
                    }

                    UtilityWindowTheme.CountPill("Generated " + output.ExistingGeneratedChildCount, output.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 118f);
                }
            }
        }

        private void DrawWorkbenchDiagnosticsSection(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Diagnostics", UtilityWindowTheme.Amber, "Validation and performance");

                if (path != null)
                    DrawPathValidation(path);
                else
                    DrawAreaValidation();
                DrawPerformanceTab();
            }
        }

        private void DrawScenePreviewPreferences()
        {
            EditorGUI.BeginChangeCheck();
            bool labels = EditorGUILayout.Toggle(new GUIContent("Labels", "Show selected/full point labels according to preview quality."), PungentSpatialAuthoringEditorState.ShowLabels);
            bool richPreview = EditorGUILayout.Toggle(new GUIContent("Rich Preview / Fill", "Show path ribbons and area fills when preview quality allows it."), PungentSpatialAuthoringEditorState.DrawRichPreview);
            PungentSpatialPreviewQuality quality = (PungentSpatialPreviewQuality)EditorGUILayout.EnumPopup(new GUIContent("Preview Quality", "Controls label and rich preview density."), PungentSpatialAuthoringEditorState.PreviewQuality);
            float handleSize = EditorGUILayout.Slider(new GUIContent("Handle Size", "Scene handle scale for the spatial editing tools."), PungentSpatialAuthoringEditorState.HandleSize, 0.1f, 3f);
            if (EditorGUI.EndChangeCheck())
            {
                PungentSpatialAuthoringEditorState.ShowLabels = labels;
                PungentSpatialAuthoringEditorState.DrawRichPreview = richPreview;
                PungentSpatialAuthoringEditorState.PreviewQuality = quality;
                PungentSpatialAuthoringEditorState.HandleSize = handleSize;
                RequestSceneRepaint();
            }
        }

        private void DrawSpatialSurfacePreferences(ModularPathSpawner path)
        {
            EditorGUI.BeginChangeCheck();
            bool snap = EditorGUILayout.Toggle(new GUIContent("Snap To Surface", "Snap edited points to the selected surface mask where supported."), PungentSpatialAuthoringEditorState.SnapToSurface);
            int mask = EditorGUILayout.MaskField(new GUIContent("Scene Editing Mask", "Layer mask used by area point placement and other shared scene editing operations."), PungentSpatialAuthoringEditorState.SurfaceMask.value, UnityEditorInternal.InternalEditorUtility.layers);
            if (EditorGUI.EndChangeCheck())
            {
                PungentSpatialAuthoringEditorState.SnapToSurface = snap;
                PungentSpatialAuthoringEditorState.SurfaceMask = mask;
                RequestSceneRepaint();
            }

            if (path != null)
                EditorGUILayout.LabelField("Selected Path Surface Mask", "Uses path-specific Surface Mask below for path placement and output.", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawPathSurfaceSetup(ModularPathSpawner path)
        {
            if (_pathSerializedObject == null || _pathSerializedObject.targetObject != path)
                _pathSerializedObject = new SerializedObject(path);

            _pathSerializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPathProperty("conformToSurface", "Surface Conform", "Raycast generated output to the surface mask.");
            DrawPathProperty("surfaceMask", "Surface Mask", "Layer mask used for path placement, surface sampling, and generated output.");
            DrawPathProperty("raycastStartHeight", "Raycast Height", "Height above each point used when raycasting downward.");
            DrawPathProperty("yOffset", "Y Offset", "Vertical offset applied after surface sampling.");
            DrawPathProperty("alignToSurfaceNormal", "Align To Surface Normal", "Use sampled surface normal as generated object up vector.");
            DrawPathProperty("sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both segment endpoints before placement.");
            if (EditorGUI.EndChangeCheck())
            {
                _pathSerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(path);
                PungentPathRebuildScheduler.RequestPreviewRefresh(path, this);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                RequestSceneRepaint();
            }
            else
            {
                _pathSerializedObject.ApplyModifiedProperties();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Align Selected", GUILayout.Height(24f)))
                    _status = PungentSpatialAuthoringActions.AlignActivePathToSurface(selectedOnly: true, EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinY", -10000f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxY", 10000f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinSlope", 0f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxSlope", 90f));
                if (GUILayout.Button("Align All", GUILayout.Height(24f)))
                    _status = PungentSpatialAuthoringActions.AlignActivePathToSurface(selectedOnly: false, EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinY", -10000f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxY", 10000f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinSlope", 0f), EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxSlope", 90f));
            }
        }

        private void DrawWorkbenchBridgesSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _workbenchBridgesFoldout = EditorGUILayout.Foldout(_workbenchBridgesFoldout, "Bridges & Templates", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_workbenchBridgesFoldout)
                    return;

                DrawBridgesTab();
            }
        }

        private void ApplySelectionSerializedChanges(SerializedObject so, ModularPathSpawner path, PungentAreaAuthoringShape area, bool changed, bool previewRefresh)
        {
            so.ApplyModifiedProperties();
            if (!changed)
                return;

            UnityEngine.Object target = path != null ? (UnityEngine.Object)path : area;
            EditorUtility.SetDirty(target);
            if (previewRefresh && path != null)
                PungentPathRebuildScheduler.RequestPreviewRefresh(path, this);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            RequestSceneRepaint();
        }

        private static int GetGeneratedOutputCount(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            Component source = path != null ? (Component)path : area;
            int count = path != null ? path.ExistingGeneratedChildCount : 0;
            if (source == null)
                return count;

            PungentModularSpatialOutput[] outputs = source.GetComponents<PungentModularSpatialOutput>();
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i] != null)
                    count += outputs[i].ExistingGeneratedChildCount;
            }

            return count;
        }

        private static void GetValidationSummary(ModularPathSpawner path, PungentAreaAuthoringShape area, out int issueCount, out string warning)
        {
            List<PungentSpatialValidationIssue> issues = new List<PungentSpatialValidationIssue>();
            if (path != null)
            {
                List<Vector3> points = PungentPathAuthoringAdapterUtility.CopyWorldPoints(path);
                PungentSpatialValidationUtility.ValidatePath(points, issues);
            }
            else if (area != null)
            {
                PungentSpatialValidationUtility.ValidateArea(area, issues);
            }

            issueCount = issues.Count;
            warning = issueCount > 0 ? issues[0].Message : string.Empty;
        }

        private SerializedObject GetSelectionSerializedObject(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            if (path != null)
            {
                if (_pathSerializedObject == null || _pathSerializedObject.targetObject != path)
                    _pathSerializedObject = new SerializedObject(path);
                return _pathSerializedObject;
            }

            if (area != null)
            {
                if (_areaSerializedObject == null || _areaSerializedObject.targetObject != area)
                    _areaSerializedObject = new SerializedObject(area);
                return _areaSerializedObject;
            }

            return null;
        }

        private static void DrawSerializedProperty(SerializedObject so, string propertyName, string label, string tooltip)
        {
            if (so == null)
                return;

            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, "Missing serialized field: " + propertyName, UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void CreateModularOutputRecipe(Component source)
        {
            if (PungentSpatialOutputRecipeEditorUtility.CreateRecipe(source) != null)
                _status = "Added Spatial Output Recipe.";
        }

        #region Legacy tab-era UI helpers

        private void DrawOverviewTab()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Scene Authoring", UtilityWindowTheme.Blue, PungentSpatialAuthoringEditorState.BuildStatusText());
                EditorGUILayout.HelpBox("Use the Scene View Spatial Authoring overlay for day-to-day editing. This window is the dashboard for setup, validation, outputs, and bridge handoffs.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create Path", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                        PungentSpatialAuthoringActions.CreatePathAtScenePivot();
                    if (GUILayout.Button("Create Area", GUILayout.Height(28f), GUILayout.Width(112f)))
                        PungentSpatialAuthoringActions.CreateAreaAtScenePivot();
                    if (GUILayout.Button(PungentSpatialAuthoringEditorState.EditModeActive ? "Exit Edit" : "Enter Edit", GUILayout.Height(28f), GUILayout.Width(96f)))
                        PungentSpatialAuthoringEditorState.SetEditModeActive(!PungentSpatialAuthoringEditorState.EditModeActive);
                }

                DrawOverlaySettings();
                DrawHotkeysFoldout(ref _hotkeysFoldout);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Active Selection", UtilityWindowTheme.Teal);
                ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
                PungentAreaAuthoringShape area = PungentSpatialAuthoringEditorState.ActiveArea;
                if (path == null && area == null)
                {
                    EditorGUILayout.HelpBox("No spatial object is selected. Use the scene object list below to jump to an existing path or area, or create a new one.", MessageType.Info);
                    DrawSceneSpatialObjectList(compact: true);
                }
                else
                {
                    if (path != null)
                        EditorGUILayout.LabelField("Path", path.name + " / " + path.PointCount + " point(s)", UtilityWindowTheme.MutedMiniLabelStyle);
                    if (area != null)
                        EditorGUILayout.LabelField("Area", area.name + " / " + area.shapeMode + " / " + area.PolygonPointCount + " point(s)", UtilityWindowTheme.MutedMiniLabelStyle);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Frame"))
                            PungentSpatialAuthoringActions.FrameActive();
                        if (GUILayout.Button("Ping Object"))
                            EditorGUIUtility.PingObject(path != null ? path.gameObject : area.gameObject);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneObjectsTab()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSceneSpatialObjectList(compact: false);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectionTab()
        {
            ModularPathSpawner activePath = PungentSpatialAuthoringEditorState.ActivePath;
            PungentAreaAuthoringShape activeArea = PungentSpatialAuthoringEditorState.ActiveArea;
            if (activePath != null)
            {
                DrawSelectedPathTab(activePath);
                return;
            }

            if (activeArea != null || _activeArea != null)
            {
                DrawAreasTab();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("No Spatial Selection", UtilityWindowTheme.Teal);
                EditorGUILayout.HelpBox("Create or select a spatial path/area. Existing paths and areas in loaded scenes are listed below for quick access.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create Path", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                        PungentSpatialAuthoringActions.CreatePathAtScenePivot();
                    if (GUILayout.Button("Create Area", GUILayout.Height(28f), GUILayout.Width(120f)))
                        PungentSpatialAuthoringActions.CreateAreaAtScenePivot();
                }
            }
            DrawSceneSpatialObjectList(compact: false);
            EditorGUILayout.EndScrollView();
        }

        #endregion

        private void DrawSceneSpatialObjectList(bool compact, bool limitCompact = true, bool scrollCompact = false, bool fillHeight = false)
        {
            IReadOnlyList<PungentSpatialAuthoringSceneObjectCache.Record> records = PungentSpatialAuthoringSceneObjectCache.GetRecords();
            string search = string.IsNullOrWhiteSpace(_sceneObjectSearch) ? string.Empty : _sceneObjectSearch.Trim();
            List<PungentSpatialAuthoringSceneObjectCache.Record> visible = new List<PungentSpatialAuthoringSceneObjectCache.Record>();
            for (int i = 0; i < records.Count; i++)
            {
                PungentSpatialAuthoringSceneObjectCache.Record record = records[i];
                if (record == null || record.Component == null)
                    continue;
                if (record.Kind == PungentSpatialSceneObjectKind.Path && !_showScenePaths)
                    continue;
                if (record.Kind == PungentSpatialSceneObjectKind.Area && !_showSceneAreas)
                    continue;
                if (!string.IsNullOrEmpty(search) && record.SearchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                visible.Add(record);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scene Spatial Objects", UtilityWindowTheme.Teal, visible.Count + "/" + records.Count + " shown");

                if (compact)
                {
                    EditorGUILayout.LabelField("Search", UtilityWindowTheme.MutedMiniLabelStyle);
                    _sceneObjectSearch = EditorGUILayout.TextField(_sceneObjectSearch, UtilityWindowTheme.ToolbarSearchStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _showScenePaths = GUILayout.Toggle(_showScenePaths, "Paths", EditorStyles.toolbarButton);
                        _showSceneAreas = GUILayout.Toggle(_showSceneAreas, "Areas", EditorStyles.toolbarButton);
                        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(68f)))
                            PungentSpatialAuthoringSceneObjectCache.ForceRebuild();
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Search", GUILayout.Width(52f));
                        _sceneObjectSearch = EditorGUILayout.TextField(_sceneObjectSearch, UtilityWindowTheme.ToolbarSearchStyle);
                        _showScenePaths = GUILayout.Toggle(_showScenePaths, "Paths", EditorStyles.toolbarButton, GUILayout.Width(58f));
                        _showSceneAreas = GUILayout.Toggle(_showSceneAreas, "Areas", EditorStyles.toolbarButton, GUILayout.Width(58f));
                        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(68f)))
                            PungentSpatialAuthoringSceneObjectCache.ForceRebuild();
                    }
                }

                if (records.Count == 0)
                {
                    EditorGUILayout.HelpBox("No spatial paths or areas were found in loaded scenes. Create one from the Scene View Spatial Authoring overlay or the buttons above.", MessageType.Info);
                    return;
                }

                if (visible.Count == 0)
                {
                    EditorGUILayout.HelpBox("No spatial objects match the current search/filter.", MessageType.Info);
                    return;
                }

                Vector2 previousScroll = _sceneObjectsScroll;
                if (!compact || scrollCompact)
                    _sceneObjectsScroll = fillHeight
                        ? EditorGUILayout.BeginScrollView(_sceneObjectsScroll, GUILayout.ExpandHeight(true))
                        : EditorGUILayout.BeginScrollView(_sceneObjectsScroll, GUILayout.MinHeight(180f));

                int limit = compact && limitCompact ? Mathf.Min(visible.Count, 6) : visible.Count;
                for (int i = 0; i < limit; i++)
                    DrawSceneSpatialObjectRow(visible[i], compact);

                if (compact && limitCompact && visible.Count > limit)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField((visible.Count - limit) + " more spatial object(s).", UtilityWindowTheme.MutedMiniLabelStyle);
                        if (GUILayout.Button("Open Scene Objects", GUILayout.Width(132f)))
                            _tab = ToolkitTab.SceneObjects;
                    }
                }

                if (!compact || scrollCompact)
                    EditorGUILayout.EndScrollView();
                else
                    _sceneObjectsScroll = previousScroll;
            }
        }

        private void DrawSceneSpatialObjectRow(PungentSpatialAuthoringSceneObjectCache.Record record, bool compact)
        {
            Color tint = record.IssueCount > 0 ? UtilityWindowTheme.Amber : record.Kind == PungentSpatialSceneObjectKind.Path ? UtilityWindowTheme.Blue : UtilityWindowTheme.Purple;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.12f, 0.05f, 5, 3)))
            {
                if (compact)
                {
                    EditorGUILayout.ObjectField(record.Component, typeof(Component), true);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select", GUILayout.Height(22f)))
                            PungentSpatialAuthoringActions.SelectSpatialObject(record.Component, edit: false);
                        if (GUILayout.Button("Edit", GUILayout.Height(22f), GUILayout.Width(46f)))
                            PungentSpatialAuthoringActions.SelectSpatialObject(record.Component, edit: true);
                        if (GUILayout.Button("Frame", GUILayout.Height(22f), GUILayout.Width(54f)))
                            PungentSpatialAuthoringActions.FrameSpatialObject(record.Component);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(record.Component, typeof(Component), true);
                        if (GUILayout.Button("Select", GUILayout.Width(58f)))
                            PungentSpatialAuthoringActions.SelectSpatialObject(record.Component, edit: false);
                        if (GUILayout.Button("Edit", GUILayout.Width(46f)))
                            PungentSpatialAuthoringActions.SelectSpatialObject(record.Component, edit: true);
                        if (GUILayout.Button("Frame", GUILayout.Width(54f)))
                            PungentSpatialAuthoringActions.FrameSpatialObject(record.Component);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(record.Kind.ToString(), record.Kind == PungentSpatialSceneObjectKind.Path ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Teal, 78f);
                    UtilityWindowTheme.CountPill("Points " + record.PointCount, record.PointCount >= record.HighPointWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 88f);
                    UtilityWindowTheme.CountPill(record.Active ? "Active" : "Inactive", record.Active ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 82f);
                    if (record.IssueCount > 0)
                        UtilityWindowTheme.CountPill("Issues " + record.IssueCount, UtilityWindowTheme.Amber, 88f);
                    if (!compact && record.HasBounds)
                        EditorGUILayout.LabelField("Bounds " + record.Bounds.size.ToString("0.#"), UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                }

                if (!compact && !string.IsNullOrWhiteSpace(record.Warning))
                    EditorGUILayout.HelpBox(record.Warning, MessageType.Warning);
            }
        }

        private void DrawValidationTab()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPathValidation(PungentSpatialAuthoringEditorState.ActivePath);
            DrawAreaValidation();
            DrawPerformanceTab();
            DrawHotkeysFoldout(ref _hotkeysFoldout);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHotkeysFoldout(ref bool expanded)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 3)))
            {
                expanded = EditorGUILayout.Foldout(expanded, "Hotkeys", true);
                if (!expanded)
                    return;

                EditorGUILayout.LabelField("A Place / M Move / D Delete / V View", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Shift forces append. Ctrl/Cmd forces insert near a segment.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Esc clears selected point first, then exits edit mode.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Alt, right mouse, and middle mouse keep normal Scene View navigation.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Use View mode for normal scene selection passthrough.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawOverlaySettings()
        {
            EditorGUI.BeginChangeCheck();
            bool labels = EditorGUILayout.Toggle("Labels", PungentSpatialAuthoringEditorState.ShowLabels);
            bool ribbon = EditorGUILayout.Toggle("Path Ribbon / Area Fill", PungentSpatialAuthoringEditorState.DrawRichPreview);
            bool snap = EditorGUILayout.Toggle("Snap To Surface", PungentSpatialAuthoringEditorState.SnapToSurface);
            PungentSpatialPreviewQuality quality = (PungentSpatialPreviewQuality)EditorGUILayout.EnumPopup("Preview Quality", PungentSpatialAuthoringEditorState.PreviewQuality);
            float handleSize = EditorGUILayout.Slider("Handle Size", PungentSpatialAuthoringEditorState.HandleSize, 0.1f, 3f);
            if (EditorGUI.EndChangeCheck())
            {
                PungentSpatialAuthoringEditorState.ShowLabels = labels;
                PungentSpatialAuthoringEditorState.DrawRichPreview = ribbon;
                PungentSpatialAuthoringEditorState.SnapToSurface = snap;
                PungentSpatialAuthoringEditorState.PreviewQuality = quality;
                PungentSpatialAuthoringEditorState.HandleSize = handleSize;
                RequestSceneRepaint();
            }
        }

        private void DrawSelectedPathTab(ModularPathSpawner path)
        {
            if (path == null)
            {
                DrawSceneSpatialObjectList(compact: false);
                return;
            }

            if (_pathSerializedObject == null || _pathSerializedObject.targetObject != path)
                _pathSerializedObject = new SerializedObject(path);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Active Path", UtilityWindowTheme.Blue, path.SpatialDisplayName);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(PungentSpatialAuthoringEditorState.IsEditingPath(path) ? "Exit Edit" : "Edit In Scene", GUILayout.Height(26f), GUILayout.Width(96f)))
                        PungentSpatialAuthoringEditorState.ToggleEditModeFor(path);
                    if (GUILayout.Button("Frame", GUILayout.Height(26f), GUILayout.Width(58f)))
                        PungentSpatialAuthoringActions.FrameSpatialObject(path);
                    if (GUILayout.Button("Ping", GUILayout.Height(26f), GUILayout.Width(52f)))
                        EditorGUIUtility.PingObject(path.gameObject);
                    if (GUILayout.Button("Add Point Forward", GUILayout.Height(26f), GUILayout.Width(126f)))
                        AddPointForward(path);
                    if (GUILayout.Button("Snap Points", GUILayout.Height(26f), GUILayout.Width(96f)))
                        SnapPathPointsToSurface(path);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Points " + path.PointCount, path.PointCount >= ModularPathSpawner.HighSampledPointWarningThreshold / 4 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan, 92f);
                    UtilityWindowTheme.CountPill(path.closedLoop ? "Closed" : "Open", path.closedLoop ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 78f);
                    UtilityWindowTheme.CountPill(path.pathMode.ToString(), UtilityWindowTheme.Purple, 132f);
                    UtilityWindowTheme.CountPill(path.autoRebuildInEditor ? "Auto Apply" : "Preview Only", path.autoRebuildInEditor ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 110f);
                    GUILayout.FlexibleSpace();
                }
            }

            _pathSerializedObject.Update();
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scene Parameters", UtilityWindowTheme.Teal, "Mirrors the Scene View Spatial Authoring overlay.");
                EditorGUI.BeginChangeCheck();
                DrawPathProperty("stablePathId", "Stable Path ID", "Generic stable ID for future bridge exports.");
                DrawPathProperty("displayName", "Display Name", "Human-readable path name.");
                DrawPathProperty("categories", "Categories / Tags", "Generic category or tag list.");
                DrawPathProperty("pathColor", "Path Color", "Scene preview colour.");
                DrawPathProperty("closedLoop", "Closed Loop", "Connect the last control point back to the first.");
                DrawPathProperty("pathMode", "Sampling", "Polyline or smooth Catmull-Rom sampling.");
                DrawPathProperty("previewCorridorWidth", "Preview Width", "Authoring-only corridor width used by generic previews.");
                DrawPathProperty("previewCorridorAlpha", "Corridor Alpha", "Transparency for the authoring corridor ribbon preview.");
                DrawPathProperty("exposePreviewCorridorWidth", "Expose Width", "Expose Preview Width to generic corridor providers.");
                DrawPathProperty("conformToSurface", "Surface Conform", "Raycast generated output and surface actions to the surface mask.");
                DrawPathProperty("surfaceMask", "Surface Mask", "Layer mask used for surface snapping/conformity.");
                DrawPathProperty("previewWhileEditing", "Preview While Editing", "Draw lightweight path preview without rebuilding generated objects.");
                DrawPathProperty("autoRebuildInEditor", "Auto Rebuild", "Queue a debounced generated rebuild after changes.");
                DrawPathProperty("drawGizmos", "Draw Preview", "Draw path preview and handles.");
                SerializedProperty points = _pathSerializedObject.FindProperty("localPoints");
                if (points != null)
                    EditorGUILayout.PropertyField(points, new GUIContent("Local Control Points"), true);

                if (EditorGUI.EndChangeCheck())
                {
                    _pathSerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(path);
                    PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
                    PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                    RequestSceneRepaint();
                }
                else
                {
                    _pathSerializedObject.ApplyModifiedProperties();
                }
            }

            DrawPathValidation(path);
            DrawHotkeysFoldout(ref _hotkeysFoldout);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPathProperty(string propertyName, string label, string tooltip)
        {
            if (_pathSerializedObject == null)
                return;

            SerializedProperty property = _pathSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, "Missing serialized field: " + propertyName, UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void DrawPathValidation(ModularPathSpawner path)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _pathValidationIssues.Clear();
                List<Vector3> points = new List<Vector3>();
                float length = 0f;
                if (path != null)
                {
                    points = PungentPathAuthoringAdapterUtility.CopyWorldPoints(path);
                    length = PungentPathAuthoringToolkit.CalculateLength(points);
                    PungentSpatialValidationUtility.ValidatePath(points, _pathValidationIssues);
                }

                UtilityWindowTheme.SectionTitle("Path Validation", UtilityWindowTheme.Teal, path != null ? points.Count + " points / " + length.ToString("0.0") + "m" : "No selection");
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Segments " + Mathf.Max(0, points.Count - 1), UtilityWindowTheme.Cyan, 90f);
                    UtilityWindowTheme.CountPill(path != null && path.exposePreviewCorridorWidth ? "Width " + path.previewCorridorWidth.ToString("0.0") : "Width off", UtilityWindowTheme.Purple, 92f);
                    UtilityWindowTheme.CountPill(path != null ? "Generated " + path.ExistingGeneratedChildCount : "Generated 0", path != null && path.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 116f);
                }

                if (path == null)
                    EditorGUILayout.HelpBox("No active path selected.", MessageType.Info);
                else if (_pathValidationIssues.Count > 0)
                    DrawSpatialValidationIssues(_pathValidationIssues);
                else if (points.Count >= ModularPathSpawner.HighSampledPointWarningThreshold / 4)
                    EditorGUILayout.HelpBox("This path contains many control points. Reduced preview quality is recommended while editing.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("Path validation passed for the selected ModularPathSpawner.", MessageType.Info);
            }
        }

        private void DrawAreasTab()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nextPreview = GUILayout.Toggle(_drawAreaScenePreview, "Preview While Editing", EditorStyles.toolbarButton, GUILayout.Width(142f));
                    if (nextPreview != _drawAreaScenePreview)
                    {
                        _drawAreaScenePreview = nextPreview;
                        RefreshSceneGuiSubscription();
                        RequestSceneRepaint();
                    }

                    _drawAreaLabels = GUILayout.Toggle(_drawAreaLabels, "Labels", EditorStyles.toolbarButton, GUILayout.Width(64f));
                    PungentSpatialAuthoringEditorState.ShowLabels = _drawAreaLabels;
                    _areaSnapToSurface = GUILayout.Toggle(_areaSnapToSurface, "Snap", EditorStyles.toolbarButton, GUILayout.Width(58f));
                    PungentSpatialAuthoringEditorState.SnapToSurface = _areaSnapToSurface;
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Use Selection", GUILayout.Width(104f)))
                        OnSelectionChanged();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create On Selection", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                        CreateArea(onSelection: true);
                    if (GUILayout.Button("Create New Area", GUILayout.Height(26f), GUILayout.Width(126f)))
                        CreateArea(onSelection: false);

                    using (new EditorGUI.DisabledScope(_activeArea == null))
                    {
                        if (GUILayout.Button("Frame", GUILayout.Height(26f), GUILayout.Width(58f)))
                            FrameArea(_activeArea);
                        if (GUILayout.Button("Select", GUILayout.Height(26f), GUILayout.Width(62f)))
                            Selection.activeGameObject = _activeArea.gameObject;
                    }
                }

                _areaSurfaceMask.value = EditorGUILayout.MaskField("Surface Mask", _areaSurfaceMask.value, UnityEditorInternal.InternalEditorUtility.layers);
                _areaHandleSize = EditorGUILayout.Slider("Handle Size", _areaHandleSize, 0.1f, 3f);
                PungentSpatialAuthoringEditorState.HandleSize = _areaHandleSize;
            }

            DrawAreaValidation();

            _areaScroll = EditorGUILayout.BeginScrollView(_areaScroll);
            if (_activeArea == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with PungentAreaAuthoringShape, or create a new generic area above.", MessageType.Info);
                DrawFutureAreaBridgePanel();
                EditorGUILayout.EndScrollView();
                return;
            }

            if (_areaSerializedObject == null || _areaSerializedObject.targetObject != _activeArea)
                _areaSerializedObject = new SerializedObject(_activeArea);

            _areaSerializedObject.Update();
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Active Area", UtilityWindowTheme.Blue, _activeArea.SpatialDisplayName);
                EditorGUI.BeginChangeCheck();
                DrawAreaProperty("stableAreaId", "Stable Area ID", "Generic stable ID for future bridge exports.");
                DrawAreaProperty("displayName", "Display Name", "Human-readable area name.");
                DrawAreaProperty("categories", "Categories / Tags", "Generic category or tag list.");
                DrawAreaProperty("color", "Color", "Scene preview colour.");
                DrawAreaProperty("shapeMode", "Shape Mode", "Polygon, rectangle, circle, or bounds-derived footprint.");
                DrawAreaProperty("localPolygonPoints", "Local Polygon Points", "Local-space XZ polygon points.");
                DrawAreaProperty("rectangleSize", "Rectangle Size", "Local XZ rectangle dimensions.");
                DrawAreaProperty("circleRadius", "Circle Radius", "Local XZ circle radius.");
                DrawAreaProperty("verticalMode", "Vertical Mode", "Area height interpretation.");
                DrawAreaProperty("height", "Height", "Height above the transform when using Height From Transform.");
                DrawAreaProperty("minY", "Min Y", "Explicit world-space minimum Y.");
                DrawAreaProperty("maxY", "Max Y", "Explicit world-space maximum Y.");
                DrawAreaProperty("runtimeQueryable", "Runtime Query", "Allow ContainsWorldPoint to return true for this area.");
                DrawAreaProperty("drawGizmo", "Draw Gizmo", "Draw runtime/editor gizmo previews for this area.");
                if (EditorGUI.EndChangeCheck())
                {
                    _areaSerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_activeArea);
                    RequestSceneRepaint();
                }
                else
                {
                    _areaSerializedObject.ApplyModifiedProperties();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Place Polygon Point"))
                        AddAreaPolygonPoint();
                    if (GUILayout.Button("Reset Polygon Rectangle"))
                        ResetAreaPolygonRectangle();
                }
            }

            DrawAreaInfo();
            DrawFutureAreaBridgePanel();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAreaValidation()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                int count = _activeArea != null ? _activeArea.PolygonPointCount : 0;
                UtilityWindowTheme.SectionTitle("Area Validation", UtilityWindowTheme.Purple, _activeArea != null ? _activeArea.shapeMode.ToString() : "No selection");
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Points " + count, count >= PungentAreaAuthoringShapeSceneHandles.HighPolygonPointWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan, 86f);
                    UtilityWindowTheme.CountPill(_activeArea != null && _activeArea.runtimeQueryable ? "Runtime On" : "Runtime Off", _activeArea != null && _activeArea.runtimeQueryable ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 104f);
                    GUILayout.FlexibleSpace();
                }

                _areaValidationIssues.Clear();
                if (_activeArea != null)
                    PungentSpatialValidationUtility.ValidateArea(_activeArea, _areaValidationIssues);

                if (_activeArea == null)
                    EditorGUILayout.HelpBox("No active area selected.", MessageType.Info);
                else if (_areaValidationIssues.Count > 0)
                    DrawSpatialValidationIssues(_areaValidationIssues);
                else if (count >= PungentAreaAuthoringShapeSceneHandles.HighPolygonPointWarningThreshold)
                    EditorGUILayout.HelpBox("This area has many vertices. Scene handles may become slower; simplify the polygon if editing becomes sluggish.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("Area validation passed for this generic footprint.", MessageType.Info);
            }
        }

        private static void DrawSpatialValidationIssues(IList<PungentSpatialValidationIssue> issues)
        {
            if (issues == null)
                return;

            for (int i = 0; i < issues.Count; i++)
            {
                PungentSpatialValidationIssue issue = issues[i];
                MessageType messageType = issue.Severity == PungentSpatialValidationSeverity.Error
                    ? MessageType.Error
                    : issue.Severity == PungentSpatialValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(issue.Message, messageType);
            }
        }

        private void DrawAreaInfo()
        {
            if (_activeArea == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 4, 3)))
            {
                UtilityWindowTheme.SectionTitle("Bounds & Query", UtilityWindowTheme.Teal);
                if (_activeArea.TryGetSpatialBounds(out Bounds bounds))
                    EditorGUILayout.LabelField("Bounds", $"Center {bounds.center.ToString("0.##")} / Size {bounds.size.ToString("0.##")}", UtilityWindowTheme.MutedMiniLabelStyle);

                Vector3 testPoint = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : _activeArea.transform.position;
                bool contains = _activeArea.ContainsWorldPoint(testPoint);
                EditorGUILayout.LabelField("Scene Pivot Test", contains ? "Inside active area" : "Outside active area", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawFutureAreaBridgePanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 3)))
            {
                UtilityWindowTheme.SectionTitle("Future Bridges", UtilityWindowTheme.Neutral);
                EditorGUILayout.HelpBox("Generic map layers, volumes, placement regions, trigger/collider creation, and spatial-query consumers are reserved extension points for future packages. Project-specific adapters should live outside this package.", MessageType.None);
                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Button("Export To Generic Map");
                    GUILayout.Button("Create Trigger From Area");
                    GUILayout.Button("Send To Placement Region");
                }
            }
        }

        private void DrawOutputsTab()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Generated Outputs", UtilityWindowTheme.Green);
                ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
                if (path == null)
                    EditorGUILayout.HelpBox("Select a ModularPathSpawner to apply or clear generated path output.", MessageType.Info);
                else
                {
                    EditorGUILayout.LabelField("Active Path", path.name, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Generated Children", path.ExistingGeneratedChildCount.ToString(), UtilityWindowTheme.MutedMiniLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Apply Preview", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                            PungentSpatialAuthoringActions.ApplyGeneratedOutput();
                        if (GUILayout.Button("Clear Generated", GUILayout.Height(28f), GUILayout.Width(132f)))
                            PungentSpatialAuthoringActions.ClearGeneratedOutput();
                    }
                    EditorGUILayout.HelpBox(path.autoRebuildInEditor ? "Auto rebuild is enabled on this path." : "Preview-only editing is active. Generated objects update when you apply.", path.autoRebuildInEditor ? MessageType.Info : MessageType.None);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBridgesTab()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Generic Provider Hooks", UtilityWindowTheme.Blue);
                EditorGUILayout.HelpBox("Spatial authoring providers are runtime-safe interfaces: paths, path metadata, path queries, area shapes, area volumes, labels, bounds, corridor widths, checkpoint volumes, generated outputs, and exclusion spans. External projects can implement these interfaces outside the package without generic utility references to project classes.", MessageType.Info);

                if (UtilityWindowTheme.TintedButton("Create Spatial Provider Template", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                    PungentSpatialProviderTemplateWindow.Open();

                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Button("Export Path To Generic Volumes");
                    GUILayout.Button("Consume From Spatial Query");
                    GUILayout.Button("Visualize In Scene Gizmo Browser");
                }
                EditorGUILayout.LabelField("Disabled actions are future bridge boundaries; no map, spatial-query, or placement-region package is implemented in this pass.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawPerformanceTab()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Performance", UtilityWindowTheme.Amber);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Spatial Counts", GUILayout.Width(164f)))
                        RebuildPerformanceCache();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(_performanceScanned ? "Counts are explicit-refresh cached." : "Counts not scanned yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Paths " + _performancePathCount, UtilityWindowTheme.Cyan, 86f);
                    UtilityWindowTheme.CountPill("Path Previews " + _performancePreviewPathCount, _performancePreviewPathCount > 25 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 126f);
                    UtilityWindowTheme.CountPill("Areas " + _performanceAreaCount, UtilityWindowTheme.Teal, 86f);
                    UtilityWindowTheme.CountPill("Area Previews " + _performancePreviewAreaCount, _performancePreviewAreaCount > 25 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 126f);
                }

                if (!_performanceScanned)
                    EditorGUILayout.HelpBox("Refresh counts when you want scene-wide preview density. The toolkit does not scan scene components during routine repaint.", MessageType.Info);
                else if (_performancePreviewPathCount + _performancePreviewAreaCount > 50)
                    EditorGUILayout.HelpBox("Scene preview is enabled for many path/area components. Disable previews on inactive components if Scene View repainting becomes sluggish.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("Spatial preview density is within the lightweight editing range.", MessageType.Info);
            }
        }

        private void RebuildPerformanceCache()
        {
            _performancePathCount = 0;
            _performancePreviewPathCount = 0;
            _performanceAreaCount = 0;
            _performancePreviewAreaCount = 0;

            ModularPathSpawner[] paths = Resources.FindObjectsOfTypeAll<ModularPathSpawner>();
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == null || EditorUtility.IsPersistent(paths[i].gameObject))
                    continue;
                _performancePathCount++;
                if (paths[i].previewWhileEditing && paths[i].drawGizmos)
                    _performancePreviewPathCount++;
            }

            PungentAreaAuthoringShape[] areas = Resources.FindObjectsOfTypeAll<PungentAreaAuthoringShape>();
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] == null || EditorUtility.IsPersistent(areas[i].gameObject))
                    continue;
                _performanceAreaCount++;
                if (areas[i].drawGizmo)
                    _performancePreviewAreaCount++;
            }

            _performanceScanned = true;
            _status = "Refreshed scene spatial preview counts.";
        }

        private void AddPointForward(ModularPathSpawner path)
        {
            if (path == null)
                return;

            Undo.RecordObject(path, "Add Spatial Path Point");
            Vector3 basePos = path.PointCount > 0 ? path.GetWorldPoint(path.PointCount - 1) : path.transform.position;
            Vector3 forward = path.transform.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            path.AddWorldPoint(basePos + forward.normalized * 5f);
            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            RequestSceneRepaint();
        }

        private void SnapPathPointsToSurface(ModularPathSpawner path)
        {
            if (path == null || path.PointCount <= 0)
                return;

            Undo.RecordObject(path, "Snap Spatial Path Points");
            float height = Mathf.Max(0.01f, path.raycastStartHeight);
            for (int i = 0; i < path.PointCount; i++)
            {
                Vector3 point = path.GetWorldPoint(i);
                Vector3 start = point + Vector3.up * height;
                if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, height * 2f, path.surfaceMask, QueryTriggerInteraction.Ignore))
                    path.SetWorldPoint(i, hit.point);
            }

            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            RequestSceneRepaint();
        }

        private void AddSelectionPoints()
        {
            int importedProviderPoints = 0;
            foreach (Transform t in Selection.transforms)
            {
                if (PungentPathAuthoringToolkit.TryAppendPathProviderPoints(t, _points, _snapToSurface ? SnapToSurface : null, out int appended))
                {
                    importedProviderPoints += appended;
                    continue;
                }

                Vector3 position = _snapToSurface ? SnapToSurface(t.position) : t.position;
                _points.Add(position);
            }

            _status = importedProviderPoints > 0
                ? "Imported " + importedProviderPoints + " provider point(s) from the selection."
                : "Added " + Selection.transforms.Length + " selected transform point(s).";
            RequestSceneRepaint();
        }

        private void OnSelectionChanged()
        {
            PungentSpatialAuthoringEditorState.RefreshSelection();
            ModularPathSpawner selectedPath = PungentSpatialAuthoringEditorState.ActivePath;
            _pathSerializedObject = selectedPath != null ? new SerializedObject(selectedPath) : null;

            PungentAreaAuthoringShape selectedArea = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PungentAreaAuthoringShape>()
                : null;

            if (selectedArea != null)
                SetActiveArea(selectedArea);
            else
            {
                SetActiveArea(null);
                _status = selectedPath != null ? "Active path: " + selectedPath.name : "No active spatial selection.";
            }

            RequestWindowRepaint();
        }

        private void OnSpatialStateChanged()
        {
            _drawLabels = PungentSpatialAuthoringEditorState.ShowLabels;
            _drawRibbon = PungentSpatialAuthoringEditorState.DrawRichPreview;
            _snapToSurface = PungentSpatialAuthoringEditorState.SnapToSurface;
            _areaSnapToSurface = PungentSpatialAuthoringEditorState.SnapToSurface;
            _areaHandleSize = PungentSpatialAuthoringEditorState.HandleSize;
            _handleSize = PungentSpatialAuthoringEditorState.HandleSize;
            _editMode = (EditMode)(int)PungentSpatialAuthoringEditorState.EditMode;
            RefreshSceneGuiSubscription();
            RequestWindowRepaint();
        }

        private void SetActiveArea(PungentAreaAuthoringShape area)
        {
            _activeArea = area;
            _areaSerializedObject = _activeArea != null ? new SerializedObject(_activeArea) : null;
            _selectedAreaPoint = -1;
            _status = _activeArea != null ? "Active area: " + _activeArea.name : "No active area selected.";
        }

        private void CreateArea(bool onSelection)
        {
            GameObject target = null;
            if (onSelection && Selection.activeGameObject != null)
                target = Selection.activeGameObject;

            if (target == null)
            {
                target = new GameObject("Pungent Area Authoring Shape");
                Undo.RegisterCreatedObjectUndo(target, "Create Area Authoring Shape");
                if (SceneView.lastActiveSceneView != null)
                    target.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            PungentAreaAuthoringShape area = target.GetComponent<PungentAreaAuthoringShape>();
            if (area == null)
                area = Undo.AddComponent<PungentAreaAuthoringShape>(target);

            Selection.activeGameObject = target;
            SetActiveArea(area);
            _tab = ToolkitTab.Selection;
            RequestSceneRepaint();
        }

        private void DrawAreaProperty(string propertyName, string label, string tooltip)
        {
            if (_areaSerializedObject == null)
                return;

            SerializedProperty property = _areaSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, "Missing serialized field: " + propertyName, UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void AddAreaPolygonPoint()
        {
            if (_activeArea == null)
                return;

            if (_activeArea.shapeMode != PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
            {
                Undo.RecordObject(_activeArea, "Switch Area To Polygon");
                _activeArea.shapeMode = PungentAreaAuthoringShape.ShapeMode.PolygonXZ;
                EditorUtility.SetDirty(_activeArea);
                _areaSerializedObject = new SerializedObject(_activeArea);
            }

            Selection.activeGameObject = _activeArea.gameObject;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Add, true);
            PungentSpatialAuthoringEditorState.ReportAction("Place mode active. Click in the Scene view to add a polygon point.");
            RequestSceneRepaint();
        }

        private void ResetAreaPolygonRectangle()
        {
            if (_activeArea == null)
                return;

            Undo.RecordObject(_activeArea, "Reset Area Polygon");
            if (_activeArea.localPolygonPoints == null)
                _activeArea.localPolygonPoints = new List<Vector3>();

            _activeArea.localPolygonPoints.Clear();
            _activeArea.localPolygonPoints.Add(new Vector3(-2f, 0f, -2f));
            _activeArea.localPolygonPoints.Add(new Vector3(-2f, 0f, 2f));
            _activeArea.localPolygonPoints.Add(new Vector3(2f, 0f, 2f));
            _activeArea.localPolygonPoints.Add(new Vector3(2f, 0f, -2f));
            _selectedAreaPoint = -1;
            EditorUtility.SetDirty(_activeArea);
            _areaSerializedObject = new SerializedObject(_activeArea);
            RequestSceneRepaint();
        }

        private void FrameArea(PungentAreaAuthoringShape area)
        {
            if (area == null || SceneView.lastActiveSceneView == null)
                return;

            if (area.TryGetSpatialBounds(out Bounds bounds))
                SceneView.lastActiveSceneView.Frame(bounds, false);
            else
                SceneView.lastActiveSceneView.pivot = area.transform.position;
            SceneView.lastActiveSceneView.Repaint();
        }

        private void InsertPointAfter(int index)
        {
            Vector3 point;
            if (index >= 0 && index < _points.Count - 1)
                point = Vector3.Lerp(_points[index], _points[index + 1], 0.5f);
            else if (_points.Count > 0)
                point = _points[index] + Vector3.forward;
            else
                point = Vector3.zero;
            _points.Insert(Mathf.Clamp(index + 1, 0, _points.Count), _snapToSurface ? SnapToSurface(point) : point);
        }

        private Vector3 SnapToSurface(Vector3 position)
        {
            if (!_snapToSurface)
                return position;
            if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, _surfaceMask))
                return hit.point;
            return position;
        }

        private Vector3 SnapAreaToSurface(Vector3 position)
        {
            if (!_areaSnapToSurface)
                return position;
            if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, _areaSurfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return position;
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (_tab == ToolkitTab.Selection && _drawScenePreview && PungentSpatialAuthoringEditorState.EditModeActive)
            {
                HandleHotkeys();
                DrawSceneOverlay();
                DrawPathSceneHandles();
                HandleMouseActions(sceneView);
            }
            else if (_tab == ToolkitTab.Selection && _drawAreaScenePreview && _activeArea != null && PungentSpatialAuthoringEditorState.EditModeActive)
            {
                DrawAreaSceneOverlay();
                PungentAreaAuthoringShapeSceneHandles.Draw(
                    _activeArea,
                    ref _selectedAreaPoint,
                    _areaSnapToSurface,
                    _areaSurfaceMask,
                    _drawAreaLabels,
                    _areaHandleSize,
                    PungentSpatialAuthoringEditorState.DrawRichPreview,
                    true,
                    sceneView,
                    this,
                    ref _nextAllowedSceneRepaintTime,
                    ref _nextAllowedWindowRepaintTime);
            }
        }

        private void DrawPathSceneHandles()
        {
            Handles.color = UtilityWindowTheme.Cyan;
            if (_points.Count > 1)
            {
                for (int i = 0; i < _points.Count - 1; i++)
                    Handles.DrawAAPolyLine(3f, _points[i], _points[i + 1]);
            }

            if (_drawRibbon && _points.Count > 1 && PungentSpatialAuthoringEditorState.PreviewQuality != PungentSpatialPreviewQuality.Minimal)
                PungentPathAuthoringToolkit.DrawWidthPreview(_points, _pathWidth, UtilityWindowTheme.Teal);

            for (int i = 0; i < _points.Count; i++)
            {
                float size = HandleUtility.GetHandleSize(_points[i]) * _handleSize * 0.08f;
                Handles.color = i == _selectedIndex ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan;
                if (Handles.Button(_points[i], Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                    _selectedIndex = i;

                if (_editMode == EditMode.Move && i == _selectedIndex)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 next = Handles.PositionHandle(_points[i], Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _points[i] = _snapToSurface ? SnapToSurface(next) : next;
                        RequestWindowRepaint();
                    }
                }

                if (_drawLabels && PungentSpatialAuthoringEditorState.ShouldDrawPointLabel(i == _selectedIndex))
                    Handles.Label(_points[i] + Vector3.up * HandleUtility.GetHandleSize(_points[i]) * 0.12f, "P" + i);
            }
        }

        private void HandleMouseActions(SceneView sceneView)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || e.alt)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10000f, _surfaceMask))
                return;

            if (_editMode == EditMode.Add)
            {
                _points.Add(hit.point);
                _selectedIndex = _points.Count - 1;
                e.Use();
                RequestWindowRepaint();
            }
            else if (_editMode == EditMode.Insert || e.shift)
            {
                int insert = PungentPathAuthoringToolkit.FindNearestSegmentIndex(_points, hit.point);
                _points.Insert(Mathf.Clamp(insert + 1, 0, _points.Count), hit.point);
                _selectedIndex = insert + 1;
                e.Use();
                RequestWindowRepaint();
            }
            else if (_editMode == EditMode.Delete)
            {
                int nearest = PungentPathAuthoringToolkit.FindNearestPointIndex(_points, hit.point);
                if (nearest >= 0)
                {
                    _points.RemoveAt(nearest);
                    _selectedIndex = -1;
                    e.Use();
                    RequestWindowRepaint();
                }
            }
        }

        private void HandleHotkeys()
        {
            if (!PungentSpatialAuthoringEditorState.EditModeActive)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.A) { SetToolkitEditMode(EditMode.Add); e.Use(); }
            else if (e.keyCode == KeyCode.M) { SetToolkitEditMode(EditMode.Move); e.Use(); }
            else if (e.keyCode == KeyCode.I) { SetToolkitEditMode(EditMode.Insert); e.Use(); }
            else if (e.keyCode == KeyCode.D) { SetToolkitEditMode(EditMode.Delete); e.Use(); }
            else if (e.keyCode == KeyCode.S) { SetToolkitEditMode(EditMode.Split); e.Use(); }
            else if (e.keyCode == KeyCode.Escape)
            {
                if (_selectedIndex >= 0)
                    _selectedIndex = -1;
                else
                    PungentSpatialAuthoringEditorState.SetEditModeActive(false);
                SetToolkitEditMode(EditMode.View);
                e.Use();
            }
        }

        private void SetToolkitEditMode(EditMode mode)
        {
            _editMode = mode;
            PungentSpatialAuthoringEditorState.SetEditMode((PungentSpatialEditMode)(int)mode, true);
            RequestWindowRepaint();
        }

        private void DrawSceneOverlay()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 104f, 260f, 72f), GUI.skin.box);
            EditorGUILayout.LabelField("Path Toolkit", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("A", EditorStyles.miniButton, GUILayout.Width(28f))) _editMode = EditMode.Add;
                if (GUILayout.Button("M", EditorStyles.miniButton, GUILayout.Width(28f))) _editMode = EditMode.Move;
                if (GUILayout.Button("I", EditorStyles.miniButton, GUILayout.Width(28f))) _editMode = EditMode.Insert;
                if (GUILayout.Button("D", EditorStyles.miniButton, GUILayout.Width(28f))) _editMode = EditMode.Delete;
                GUILayout.Label(_points.Count + " pts / " + PungentPathAuthoringToolkit.CalculateLength(_points).ToString("0.0") + "m", EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawAreaSceneOverlay()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 104f, 300f, 76f), GUI.skin.box);
            EditorGUILayout.LabelField("Spatial Toolkit / Areas", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _areaSnapToSurface = GUILayout.Toggle(_areaSnapToSurface, "Snap", EditorStyles.miniButton, GUILayout.Width(54f));
                _drawAreaLabels = GUILayout.Toggle(_drawAreaLabels, "Labels", EditorStyles.miniButton, GUILayout.Width(62f));
                GUILayout.Label(_activeArea != null ? _activeArea.PolygonPointCount + " pts / " + _activeArea.shapeMode : "No area", EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void RefreshSceneGuiSubscription()
        {
#if UNITY_2021_2_OR_NEWER
            SetSceneGuiSubscribed(false);
#else
            SetSceneGuiSubscribed(PungentSpatialAuthoringEditorState.EditModeActive && _tab == ToolkitTab.Selection && (_drawScenePreview || (_drawAreaScenePreview && _activeArea != null)));
#endif
        }

        private void SetSceneGuiSubscribed(bool subscribed)
        {
            if (_sceneGuiSubscribed == subscribed)
                return;

            if (subscribed)
                SceneView.duringSceneGui += DuringSceneGUI;
            else
                SceneView.duringSceneGui -= DuringSceneGUI;

            _sceneGuiSubscribed = subscribed;
        }

        private void RequestSceneRepaint()
        {
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.10d);
        }

        private void RequestWindowRepaint()
        {
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedWindowRepaintTime, 0.10d);
        }

        private void SaveWorkbenchPrefs()
        {
            EditorPrefs.SetInt(WorkbenchPagePrefKey, (int)_workbenchPage);
            EditorPrefs.SetFloat(WorkbenchLeftWidthPrefKey, Mathf.Clamp(_workbenchLeftColumnWidth, WorkbenchLeftMinWidth, WorkbenchLeftMaxWidth));
        }
    }

    internal enum PungentSpatialSceneObjectKind
    {
        Path,
        Area
    }

    [InitializeOnLoad]
    internal static class PungentSpatialAuthoringSceneObjectCache
    {
        internal sealed class Record
        {
            public PungentSpatialSceneObjectKind Kind;
            public Component Component;
            public GameObject GameObject;
            public string DisplayName;
            public string SearchText;
            public int PointCount;
            public int IssueCount;
            public int HighPointWarningThreshold;
            public bool Active;
            public bool HasBounds;
            public Bounds Bounds;
            public string Warning;
        }

        private static readonly List<Record> Records = new List<Record>();
        private static readonly List<PungentSpatialValidationIssue> Issues = new List<PungentSpatialValidationIssue>();
        private static bool _dirty = true;

        public static event System.Action Changed;

        static PungentSpatialAuthoringSceneObjectCache()
        {
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.playModeStateChanged += _ => MarkDirty();
            AssemblyReloadEvents.afterAssemblyReload += MarkDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public static void MarkDirty()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        public static void ForceRebuild()
        {
            _dirty = true;
            RebuildIfDirty();
            Changed?.Invoke();
        }

        public static IReadOnlyList<Record> GetRecords()
        {
            RebuildIfDirty();
            return Records;
        }

        private static void RebuildIfDirty()
        {
            if (!_dirty)
                return;

            Records.Clear();
            AddPaths();
            AddAreas();
            Records.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _dirty = false;
        }

        private static void AddPaths()
        {
            ModularPathSpawner[] paths = Resources.FindObjectsOfTypeAll<ModularPathSpawner>();
            for (int i = 0; i < paths.Length; i++)
            {
                ModularPathSpawner path = paths[i];
                if (!IsSceneObject(path))
                    continue;

                List<Vector3> points = PungentPathAuthoringAdapterUtility.CopyWorldPoints(path);
                Issues.Clear();
                PungentSpatialValidationUtility.ValidatePath(points, Issues);

                Record record = new Record
                {
                    Kind = PungentSpatialSceneObjectKind.Path,
                    Component = path,
                    GameObject = path.gameObject,
                    DisplayName = string.IsNullOrWhiteSpace(path.SpatialDisplayName) ? path.name : path.SpatialDisplayName,
                    PointCount = path.PointCount,
                    IssueCount = Issues.Count,
                    HighPointWarningThreshold = ModularPathSpawner.HighSampledPointWarningThreshold / 4,
                    Active = path.isActiveAndEnabled && path.gameObject.activeInHierarchy,
                    Warning = BuildWarning(Issues)
                };
                record.HasBounds = path.TryGetSpatialBounds(out record.Bounds);
                record.SearchText = BuildSearchText(record, path.GetType().Name, path.SpatialTags);
                Records.Add(record);
            }
        }

        private static void AddAreas()
        {
            PungentAreaAuthoringShape[] areas = Resources.FindObjectsOfTypeAll<PungentAreaAuthoringShape>();
            for (int i = 0; i < areas.Length; i++)
            {
                PungentAreaAuthoringShape area = areas[i];
                if (!IsSceneObject(area))
                    continue;

                Issues.Clear();
                PungentSpatialValidationUtility.ValidateArea(area, Issues);

                Record record = new Record
                {
                    Kind = PungentSpatialSceneObjectKind.Area,
                    Component = area,
                    GameObject = area.gameObject,
                    DisplayName = string.IsNullOrWhiteSpace(area.SpatialDisplayName) ? area.name : area.SpatialDisplayName,
                    PointCount = area.PolygonPointCount,
                    IssueCount = Issues.Count,
                    HighPointWarningThreshold = PungentAreaAuthoringShapeSceneHandles.HighPolygonPointWarningThreshold,
                    Active = area.isActiveAndEnabled && area.gameObject.activeInHierarchy,
                    Warning = BuildWarning(Issues)
                };
                record.HasBounds = area.TryGetSpatialBounds(out record.Bounds);
                record.SearchText = BuildSearchText(record, area.GetType().Name, area.SpatialTags);
                Records.Add(record);
            }
        }

        private static bool IsSceneObject(Component component)
        {
            return component != null &&
                   component.gameObject != null &&
                   !EditorUtility.IsPersistent(component) &&
                   !EditorUtility.IsPersistent(component.gameObject) &&
                   component.gameObject.scene.IsValid() &&
                   component.gameObject.scene.isLoaded;
        }

        private static string BuildWarning(IList<PungentSpatialValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return string.Empty;

            return issues.Count == 1 ? issues[0].Message : issues.Count + " validation issues. First: " + issues[0].Message;
        }

        private static string BuildSearchText(Record record, string typeName, IReadOnlyList<string> tags)
        {
            string tagText = tags == null ? string.Empty : string.Join(" ", tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray());
            return string.Join(" ", new[]
            {
                record.DisplayName,
                record.GameObject != null ? record.GameObject.name : string.Empty,
                record.Kind.ToString(),
                typeName,
                tagText,
                record.Warning
            }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            MarkDirty();
        }

        private static void OnSceneClosed(Scene scene)
        {
            MarkDirty();
        }
    }

    internal enum PungentSpatialEditMode
    {
        View,
        Add,
        Move,
        Insert,
        Delete,
        Split,
        Output
    }

    internal enum PungentSpatialPreviewQuality
    {
        Full,
        Reduced,
        Minimal
    }

    internal enum PungentSpatialPlacementKind
    {
        None,
        Append,
        Insert,
        Delete,
        Move,
        Blocked
    }

    internal struct PungentSpatialPlacementDecision
    {
        public PungentSpatialPlacementKind Kind;
        public Vector3 WorldPoint;
        public int PointIndex;
        public int SegmentStart;
        public int SegmentEnd;
        public string StatusText;

        public bool IsValid => Kind != PungentSpatialPlacementKind.None && Kind != PungentSpatialPlacementKind.Blocked;

        public static PungentSpatialPlacementDecision None(string status)
        {
            return new PungentSpatialPlacementDecision
            {
                Kind = string.IsNullOrWhiteSpace(status) ? PungentSpatialPlacementKind.None : PungentSpatialPlacementKind.Blocked,
                PointIndex = -1,
                SegmentStart = -1,
                SegmentEnd = -1,
                StatusText = status ?? string.Empty
            };
        }
    }

    internal struct PungentSpatialHandleFeedback
    {
        public string StatusText;

        public PungentSpatialHandleFeedback(string statusText)
        {
            StatusText = statusText ?? string.Empty;
        }
    }

    [InitializeOnLoad]
    internal static class PungentSpatialAuthoringEditorState
    {
        private const string PrefPrefix = "PungentFunkUtilities.SpatialAuthoring.";
        private const string PrefEditModeActive = PrefPrefix + "EditModeActive";
        private const string PrefShowLabels = PrefPrefix + "ShowLabels";
        private const string PrefDrawRichPreview = PrefPrefix + "DrawRichPreview";
        private const string PrefSnapToSurface = PrefPrefix + "SnapToSurface";
        private const string PrefSurfaceMask = PrefPrefix + "SurfaceMask";
        private const string PrefHandleSize = PrefPrefix + "HandleSize";
        private const string PrefPreviewQuality = PrefPrefix + "PreviewQuality";

        private static bool _initialized;
        private static bool _editModeActive;
        private static bool _showLabels;
        private static bool _drawRichPreview;
        private static bool _snapToSurface;
        private static LayerMask _surfaceMask;
        private static float _handleSize;
        private static PungentSpatialEditMode _editMode;
        private static PungentSpatialPreviewQuality _previewQuality;
        private static ModularPathSpawner _activePath;
        private static PungentAreaAuthoringShape _activeArea;
        private static double _nextAllowedSceneRepaintTime;
        private static double _nextAllowedStatusNotifyTime;
        private static bool _toolActivationScheduled;
        private static bool _toolRestoreScheduled;
        private static bool _toolActivationInProgress;
        private static string _handleTargetKind = string.Empty;
        private static int _selectedHandleIndex = -1;
        private static int _hoveredHandleIndex = -1;
        private static int _handleCount;
        private static int _clearHandleSelectionVersion;
        private static string _sceneFeedbackText = string.Empty;
        private static string _lastActionText = string.Empty;

        public static event System.Action Changed;

        static PungentSpatialAuthoringEditorState()
        {
            EnsureInitialized();
        }

        public static ModularPathSpawner ActivePath { get { EnsureInitialized(); return _activePath; } }
        public static PungentAreaAuthoringShape ActiveArea { get { EnsureInitialized(); return _activeArea; } }
        public static bool HasSpatialSelection { get { EnsureInitialized(); return _activePath != null || _activeArea != null; } }
        public static bool ShouldCaptureSceneInput { get { EnsureInitialized(); return _editModeActive && _editMode != PungentSpatialEditMode.View && _editMode != PungentSpatialEditMode.Output; } }
        public static bool HasSelectedHandle { get { EnsureInitialized(); return _selectedHandleIndex >= 0; } }
        public static int SelectedHandleIndex { get { EnsureInitialized(); return _selectedHandleIndex; } }
        public static int ClearHandleSelectionVersion { get { EnsureInitialized(); return _clearHandleSelectionVersion; } }

        public static bool EditModeActive
        {
            get { EnsureInitialized(); return _editModeActive; }
        }

        public static PungentSpatialEditMode EditMode
        {
            get { EnsureInitialized(); return _editMode; }
        }

        public static bool ShowLabels
        {
            get { EnsureInitialized(); return _showLabels; }
            set { EnsureInitialized(); SetBool(ref _showLabels, value, PrefShowLabels); }
        }

        public static bool DrawRichPreview
        {
            get { EnsureInitialized(); return _drawRichPreview; }
            set { EnsureInitialized(); SetBool(ref _drawRichPreview, value, PrefDrawRichPreview); }
        }

        public static bool SnapToSurface
        {
            get { EnsureInitialized(); return _snapToSurface; }
            set { EnsureInitialized(); SetBool(ref _snapToSurface, value, PrefSnapToSurface); }
        }

        public static LayerMask SurfaceMask
        {
            get { EnsureInitialized(); return _surfaceMask; }
            set
            {
                EnsureInitialized();
                if (_surfaceMask.value == value.value)
                    return;
                _surfaceMask = value;
                EditorPrefs.SetInt(PrefSurfaceMask, _surfaceMask.value);
                NotifyChanged();
            }
        }

        public static float HandleSize
        {
            get { EnsureInitialized(); return _handleSize; }
            set
            {
                EnsureInitialized();
                float clamped = Mathf.Clamp(value, 0.1f, 3f);
                if (Mathf.Approximately(_handleSize, clamped))
                    return;
                _handleSize = clamped;
                EditorPrefs.SetFloat(PrefHandleSize, _handleSize);
                NotifyChanged();
            }
        }

        public static PungentSpatialPreviewQuality PreviewQuality
        {
            get { EnsureInitialized(); return _previewQuality; }
            set
            {
                EnsureInitialized();
                if (_previewQuality == value)
                    return;
                _previewQuality = value;
                EditorPrefs.SetInt(PrefPreviewQuality, (int)_previewQuality);
                NotifyChanged();
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _editModeActive = EditorPrefs.GetBool(PrefEditModeActive, false);
            _showLabels = EditorPrefs.GetBool(PrefShowLabels, false);
            _drawRichPreview = EditorPrefs.GetBool(PrefDrawRichPreview, true);
            _snapToSurface = EditorPrefs.GetBool(PrefSnapToSurface, true);
            _surfaceMask = EditorPrefs.GetInt(PrefSurfaceMask, ~0);
            _handleSize = EditorPrefs.GetFloat(PrefHandleSize, 0.6f);
            _previewQuality = (PungentSpatialPreviewQuality)Mathf.Clamp(EditorPrefs.GetInt(PrefPreviewQuality, (int)PungentSpatialPreviewQuality.Reduced), 0, 2);
            _editMode = PungentSpatialEditMode.Move;

            Selection.selectionChanged -= RefreshSelection;
            Selection.selectionChanged += RefreshSelection;
            AssemblyReloadEvents.beforeAssemblyReload -= SavePrefs;
            AssemblyReloadEvents.beforeAssemblyReload += SavePrefs;
            RefreshSelection();
        }

        public static void RefreshSelection()
        {
            EnsureInitialized();
            SyncSelectionStateInternal(notify: true);
        }

        public static bool SyncSelectionState()
        {
            EnsureInitialized();
            return SyncSelectionStateInternal(notify: false);
        }

        private static bool SyncSelectionStateInternal(bool notify)
        {
            GameObject go = ResolveSelectionGameObject();
            ModularPathSpawner nextPath = go != null ? go.GetComponentInParent<ModularPathSpawner>() : null;
            PungentAreaAuthoringShape nextArea = go != null ? go.GetComponentInParent<PungentAreaAuthoringShape>() : null;

            bool changed = _activePath != nextPath || _activeArea != nextArea;
            _activePath = nextPath;
            _activeArea = nextArea;

            bool hasSpatialSelection = _activePath != null || _activeArea != null;
            if (!hasSpatialSelection)
                SetHandleSelection(string.Empty, -1, -1, 0, notify: false);

            if (!hasSpatialSelection && _editModeActive)
            {
                _editModeActive = false;
                EditorPrefs.SetBool(PrefEditModeActive, false);
                changed = true;
            }

            if (changed)
                _sceneFeedbackText = string.Empty;

            if (changed && notify)
                NotifyChanged();

            return changed;
        }

        private static GameObject ResolveSelectionGameObject()
        {
            if (Selection.activeGameObject != null)
                return Selection.activeGameObject;

            if (Selection.activeTransform != null)
                return Selection.activeTransform.gameObject;

            if (Selection.activeObject is Component component)
                return component.gameObject;

            return null;
        }

        public static void SetEditMode(PungentSpatialEditMode mode, bool activate)
        {
            EnsureInitialized();
            if (activate)
                SyncSelectionStateInternal(notify: false);

            if (_editMode == mode && (!activate || _editModeActive))
            {
                if (activate && _editModeActive)
                    RequestEditorToolActivation();
                return;
            }

            _editMode = mode;
            _sceneFeedbackText = string.Empty;
            if (activate)
            {
                _editModeActive = true;
                EditorPrefs.SetBool(PrefEditModeActive, true);
                RequestEditorToolActivation();
            }

            NotifyChanged();
        }

        public static void SetEditModeActive(bool active)
        {
            SetEditModeActive(active, requestToolChange: true);
        }

        public static void ExitEditMode()
        {
            EnsureInitialized();
            RequestClearHandleSelection();
            SetEditModeActive(false);
        }

        private static void SetEditModeActive(bool active, bool requestToolChange)
        {
            EnsureInitialized();
            if (active)
                SyncSelectionStateInternal(notify: false);

            if (_editModeActive == active)
            {
                if (active && requestToolChange)
                    RequestEditorToolActivation();
                return;
            }

            _editModeActive = active;
            _sceneFeedbackText = string.Empty;
            if (_editModeActive && _editMode == PungentSpatialEditMode.View)
                _editMode = PungentSpatialEditMode.Move;
            else if (!_editModeActive)
                _editMode = PungentSpatialEditMode.View;

            EditorPrefs.SetBool(PrefEditModeActive, _editModeActive);
            if (requestToolChange)
            {
                if (_editModeActive)
                    RequestEditorToolActivation();
                else
                    RequestRestorePreviousTool();
            }
            NotifyChanged();
        }

        internal static void NotifyEditorToolActivated(PungentSpatialEditMode defaultMode)
        {
            EnsureInitialized();
            if (_editMode == PungentSpatialEditMode.View)
                _editMode = defaultMode;
            _editModeActive = true;
            EditorPrefs.SetBool(PrefEditModeActive, true);
            NotifyChanged();
        }

        internal static void NotifyEditorToolDeactivated()
        {
            if (_toolActivationInProgress)
                return;

            SetEditModeActive(false, requestToolChange: false);
        }

        public static void ToggleEditModeFor(ModularPathSpawner path)
        {
            if (path != null)
            {
                Selection.activeGameObject = path.gameObject;
                RefreshSelection();
            }
            SetEditModeActive(!IsEditingPath(path));
            if (_editModeActive && _editMode == PungentSpatialEditMode.View)
                SetEditMode(PungentSpatialEditMode.Move, false);
        }

        public static void ToggleEditModeFor(PungentAreaAuthoringShape area)
        {
            if (area != null)
            {
                Selection.activeGameObject = area.gameObject;
                RefreshSelection();
            }
            SetEditModeActive(!IsEditingArea(area));
            if (_editModeActive && _editMode == PungentSpatialEditMode.View)
                SetEditMode(PungentSpatialEditMode.Move, false);
        }

        public static bool IsEditingPath(ModularPathSpawner path)
        {
            EnsureInitialized();
            return _editModeActive && path != null && _activePath == path;
        }

        public static bool IsEditingArea(PungentAreaAuthoringShape area)
        {
            EnsureInitialized();
            return _editModeActive && area != null && _activeArea == area;
        }

        public static bool ShouldDrawPath(ModularPathSpawner path)
        {
            EnsureInitialized();
            return path != null && _activePath == path && path.previewWhileEditing;
        }

        public static bool ShouldDrawArea(PungentAreaAuthoringShape area)
        {
            EnsureInitialized();
            return area != null && _activeArea == area && area.drawGizmo;
        }

        public static bool IsPathEditorToolActive(ModularPathSpawner path)
        {
            EnsureInitialized();
#if UNITY_2021_2_OR_NEWER
            return path != null &&
                   _activePath == path &&
                   UnityEditor.EditorTools.ToolManager.activeToolType == typeof(PungentSpatialPathEditorTool);
#else
            return false;
#endif
        }

        public static bool IsAreaEditorToolActive(PungentAreaAuthoringShape area)
        {
            EnsureInitialized();
#if UNITY_2021_2_OR_NEWER
            return area != null &&
                   _activeArea == area &&
                   UnityEditor.EditorTools.ToolManager.activeToolType == typeof(PungentSpatialAreaEditorTool);
#else
            return false;
#endif
        }

        public static bool ShouldDrawPointLabel(bool selected)
        {
            EnsureInitialized();
            return _showLabels && (selected || _previewQuality == PungentSpatialPreviewQuality.Full) &&
                   PungentSceneGizmoPerformancePolicy.TryConsumeLabel(selected);
        }

        public static string BuildStatusText()
        {
            EnsureInitialized();
            string subject = _activePath != null ? "Path: " + _activePath.name : _activeArea != null ? "Area: " + _activeArea.name : "No spatial selection";
            return (_editModeActive ? "Editing " + ModeDisplayName(_editMode) : "Viewing") + " / " + subject;
        }

        public static string BuildModeChipText()
        {
            EnsureInitialized();
            return _editModeActive ? ModeDisplayName(_editMode).ToUpperInvariant() : "VIEW";
        }

        public static string BuildHandleStatusText()
        {
            EnsureInitialized();
            if (!string.IsNullOrWhiteSpace(_sceneFeedbackText))
                return _sceneFeedbackText;
            if (!string.IsNullOrWhiteSpace(_lastActionText))
                return _lastActionText;
            if (_handleCount <= 0)
                return "No editable points";

            string selected = _selectedHandleIndex >= 0 ? _selectedHandleIndex.ToString() : "none";
            string hovered = _hoveredHandleIndex >= 0 ? _hoveredHandleIndex.ToString() : "none";
            return _handleTargetKind + " points " + _handleCount + " / selected " + selected + " / hover " + hovered;
        }

        public static string BuildPerformanceStatusText()
        {
            EnsureInitialized();
            if (_previewQuality == PungentSpatialPreviewQuality.Minimal)
                return "Minimal preview: labels/fill reduced for performance.";
            if (_previewQuality == PungentSpatialPreviewQuality.Reduced)
                return "Reduced preview: selected objects draw rich controls.";
            return "Full preview: labels and rich previews are enabled.";
        }

        public static string BuildHotkeyHelpText()
        {
            return "A Place, M Move, D Delete, V View, O Output, Esc clears selected point then exits. Shift forces append, Ctrl/Cmd forces insert. Alt/right/middle mouse keep normal Scene navigation.";
        }

        public static string BuildNextActionHint()
        {
            EnsureInitialized();
            if (!HasSpatialSelection)
                return "Create a spatial object, or select an existing path or area.";
            if (!_editModeActive)
                return "Enter edit mode to use Scene handles. Frame or adjust setup fields from here.";
            if (!string.IsNullOrWhiteSpace(_sceneFeedbackText))
                return _sceneFeedbackText;
            switch (_editMode)
            {
                case PungentSpatialEditMode.Add:
                case PungentSpatialEditMode.Insert:
                case PungentSpatialEditMode.Split:
                    return "Click in the Scene view to place a point. Shift appends; Ctrl/Cmd inserts.";
                case PungentSpatialEditMode.Move:
                    return "Click or drag a highlighted point or shape handle.";
                case PungentSpatialEditMode.Delete:
                    return "Click a highlighted point to delete it. Undo is available.";
                case PungentSpatialEditMode.Output:
                    return "Configure and preview the selected Spatial Output Recipe. Gap handles are editable in the Scene view.";
                case PungentSpatialEditMode.View:
                    return "View mode leaves normal scene selection available.";
                default:
                    return BuildHotkeyHelpText();
            }
        }

        private static string ModeDisplayName(PungentSpatialEditMode mode)
        {
            switch (mode)
            {
                case PungentSpatialEditMode.Add:
                    return "Place";
                case PungentSpatialEditMode.Insert:
                case PungentSpatialEditMode.Split:
                    return "Place";
                case PungentSpatialEditMode.Output:
                    return "Output";
                default:
                    return mode.ToString();
            }
        }

        public static void SetHandleSelection(string targetKind, int selectedIndex, int hoveredIndex, int count)
        {
            SetHandleSelection(targetKind, selectedIndex, hoveredIndex, count, notify: true);
        }

        public static void SetHandleFeedback(PungentSpatialHandleFeedback feedback)
        {
            EnsureInitialized();
            string next = feedback.StatusText ?? string.Empty;
            if (_sceneFeedbackText == next)
                return;

            _sceneFeedbackText = next;
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedStatusNotifyTime, 0.12d))
                Changed?.Invoke();
        }

        public static void ReportAction(string status)
        {
            EnsureInitialized();
            _lastActionText = status ?? string.Empty;
            _sceneFeedbackText = string.Empty;
            NotifyChanged();
        }

        private static void SetHandleSelection(string targetKind, int selectedIndex, int hoveredIndex, int count, bool notify)
        {
            targetKind = targetKind ?? string.Empty;
            selectedIndex = selectedIndex < 0 ? -1 : selectedIndex;
            hoveredIndex = hoveredIndex < 0 ? -1 : hoveredIndex;
            count = Mathf.Max(0, count);

            bool sameTarget = _handleTargetKind == targetKind &&
                              _selectedHandleIndex == selectedIndex &&
                              _handleCount == count;
            if (sameTarget &&
                _selectedHandleIndex == selectedIndex &&
                _hoveredHandleIndex == hoveredIndex &&
                _handleCount == count)
                return;

            bool hoverOnly = sameTarget && _hoveredHandleIndex != hoveredIndex;
            _handleTargetKind = targetKind;
            _selectedHandleIndex = selectedIndex;
            _hoveredHandleIndex = hoveredIndex;
            _handleCount = count;
            if (notify)
            {
                if (hoverOnly)
                {
                    if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedStatusNotifyTime, 0.12d))
                        Changed?.Invoke();
                }
                else
                {
                    NotifyChanged();
                }
            }
        }

        public static void RequestClearHandleSelection()
        {
            EnsureInitialized();
            _clearHandleSelectionVersion++;
            SetHandleSelection(_handleTargetKind, -1, -1, _handleCount);
        }

        public static void RequestSceneRepaint()
        {
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.08d);
        }

        private static void SetBool(ref bool field, bool value, string prefKey)
        {
            if (field == value)
                return;

            field = value;
            EditorPrefs.SetBool(prefKey, value);
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            RequestSceneRepaint();
            Changed?.Invoke();
        }

        private static void SavePrefs()
        {
            EditorPrefs.SetBool(PrefEditModeActive, _editModeActive);
            EditorPrefs.SetBool(PrefShowLabels, _showLabels);
            EditorPrefs.SetBool(PrefDrawRichPreview, _drawRichPreview);
            EditorPrefs.SetBool(PrefSnapToSurface, _snapToSurface);
            EditorPrefs.SetInt(PrefSurfaceMask, _surfaceMask.value);
            EditorPrefs.SetFloat(PrefHandleSize, _handleSize);
            EditorPrefs.SetInt(PrefPreviewQuality, (int)_previewQuality);
        }

        private static void RequestEditorToolActivation()
        {
#if UNITY_2021_2_OR_NEWER
            if (!HasSpatialSelection || IsCorrectSpatialToolActive() || _toolActivationScheduled)
                return;

            _toolActivationScheduled = true;
            EditorApplication.delayCall += ActivateEditorToolForSelectionDelayed;
#endif
        }

        private static void RequestRestorePreviousTool()
        {
#if UNITY_2021_2_OR_NEWER
            if (!IsSpatialToolActive() || _toolRestoreScheduled)
                return;

            _toolRestoreScheduled = true;
            EditorApplication.delayCall += RestorePreviousToolDelayed;
#endif
        }

#if UNITY_2021_2_OR_NEWER
        private static bool IsSpatialToolActive()
        {
            System.Type activeToolType = UnityEditor.EditorTools.ToolManager.activeToolType;
            return activeToolType == typeof(PungentSpatialPathEditorTool) ||
                   activeToolType == typeof(PungentSpatialAreaEditorTool);
        }

        private static bool IsCorrectSpatialToolActive()
        {
            System.Type activeToolType = UnityEditor.EditorTools.ToolManager.activeToolType;
            return (_activePath != null && activeToolType == typeof(PungentSpatialPathEditorTool)) ||
                   (_activeArea != null && activeToolType == typeof(PungentSpatialAreaEditorTool));
        }

        private static void ActivateEditorToolForSelectionDelayed()
        {
            _toolActivationScheduled = false;
            EnsureInitialized();
            RefreshSelection();

            if (!_editModeActive || !HasSpatialSelection || IsCorrectSpatialToolActive())
                return;

            try
            {
                _toolActivationInProgress = true;
                if (_activePath != null && Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<ModularPathSpawner>() == _activePath)
                    UnityEditor.EditorTools.ToolManager.SetActiveTool<PungentSpatialPathEditorTool>();
                else if (_activeArea != null && Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<PungentAreaAuthoringShape>() == _activeArea)
                    UnityEditor.EditorTools.ToolManager.SetActiveTool<PungentSpatialAreaEditorTool>();
            }
            catch (System.InvalidOperationException ex)
            {
                Debug.LogWarning("Spatial Authoring could not activate its Scene tool for the current selection. Editing handles remain available through the selected object's scene GUI. " + ex.Message);
            }
            finally
            {
                _toolActivationInProgress = false;
            }
        }

        private static void RestorePreviousToolDelayed()
        {
            _toolRestoreScheduled = false;
            if (_editModeActive || !IsSpatialToolActive())
                return;

            try
            {
                UnityEditor.EditorTools.ToolManager.RestorePreviousTool();
            }
            catch (System.InvalidOperationException)
            {
                // Unity forbids tool changes during some editor callbacks. Leaving the tool selected is safer than throwing through IMGUI.
            }
        }
#endif
    }

    internal static class PungentSpatialAuthoringActions
    {
        public static ModularPathSpawner CreatePathAtScenePivot()
        {
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            GameObject go = new GameObject("Spatial Path");
            Undo.RegisterCreatedObjectUndo(go, "Create Spatial Path");
            go.transform.position = position;

            ModularPathSpawner path = go.AddComponent<ModularPathSpawner>();
            path.previewWhileEditing = true;
            path.autoRebuildInEditor = false;
            path.drawGizmos = true;
            path.drawUnselectedGizmo = false;
            path.AddWorldPoint(position);
            Vector3 forward = SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null
                ? SceneView.lastActiveSceneView.camera.transform.forward
                : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            path.AddWorldPoint(position + forward.normalized * 5f);

            Selection.activeGameObject = go;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Move, true);
            PungentSpatialAuthoringEditorState.ReportAction("Created spatial path. Move points or switch to Place.");
            return path;
        }

        public static PungentAreaAuthoringShape CreateAreaAtScenePivot()
        {
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            GameObject go = new GameObject("Spatial Area");
            Undo.RegisterCreatedObjectUndo(go, "Create Spatial Area");
            go.transform.position = position;

            PungentAreaAuthoringShape area = go.AddComponent<PungentAreaAuthoringShape>();
            area.drawGizmo = true;
            area.drawUnselectedGizmo = false;
            Selection.activeGameObject = go;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Move, true);
            PungentSpatialAuthoringEditorState.ReportAction("Created spatial area. Adjust size handles or switch to Place for polygons.");
            return area;
        }

        public static void FrameActive()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            Component active = PungentSpatialAuthoringEditorState.ActivePath != null
                ? (Component)PungentSpatialAuthoringEditorState.ActivePath
                : PungentSpatialAuthoringEditorState.ActiveArea;
            if (PungentSpatialAuthoringBoundsUtility.TryGetAuthoringBounds(active, out Bounds bounds))
            {
                sceneView.Frame(bounds, false);
                PungentSpatialAuthoringEditorState.ReportAction("Framed authoring footprint.");
            }
        }

        public static void FrameActiveRuntimeVolume()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            Component active = PungentSpatialAuthoringEditorState.ActivePath != null
                ? (Component)PungentSpatialAuthoringEditorState.ActivePath
                : PungentSpatialAuthoringEditorState.ActiveArea;
            FrameSpatialObjectRuntimeVolume(active);
        }

        public static void FrameSpatialObjectRuntimeVolume(Component component)
        {
            if (component == null || SceneView.lastActiveSceneView == null)
                return;

            if (PungentSpatialAuthoringBoundsUtility.TryGetRuntimeBounds(component, out Bounds bounds))
            {
                SceneView.lastActiveSceneView.Frame(bounds, false);
                PungentSpatialAuthoringEditorState.ReportAction("Framed runtime volume.");
            }
        }

        public static void SelectSpatialObject(Component component, bool edit)
        {
            if (component == null || component.gameObject == null)
                return;

            Selection.activeGameObject = component.gameObject;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            if (edit)
                PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Move, true);
        }

        public static void FrameSpatialObject(Component component)
        {
            if (component == null || SceneView.lastActiveSceneView == null)
                return;

            if (PungentSpatialAuthoringBoundsUtility.TryGetAuthoringBounds(component, out Bounds bounds))
            {
                SceneView.lastActiveSceneView.Frame(bounds, false);
                return;
            }

            if (component.transform != null)
                SceneView.lastActiveSceneView.pivot = component.transform.position;
            SceneView.lastActiveSceneView.Repaint();
        }

        public static void ApplyGeneratedOutput()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            if (path == null)
                return;

            PungentPathRebuildScheduler.RebuildNow(path, null, "Apply Spatial Path Generated Output");
            PungentSpatialAuthoringEditorState.ReportAction("Applied generated output for " + path.name + ".");
        }

        public static void ClearGeneratedOutput()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            if (path == null)
                return;

            Undo.RecordObject(path, "Clear Spatial Path Generated Output");
            path.ClearGenerated();
            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path);
            PungentSpatialAuthoringEditorState.ReportAction("Cleared generated output for " + path.name + ".");
        }

        public static PungentModularSpatialOutput CreateGenericOutputRecipeForActive()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            Component source = PungentSpatialAuthoringEditorState.ActivePath != null
                ? (Component)PungentSpatialAuthoringEditorState.ActivePath
                : PungentSpatialAuthoringEditorState.ActiveArea;
            return CreateGenericOutputRecipe(source);
        }

        public static string ReverseActivePath()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            if (path == null || path.PointCount < 2)
                return "No selected path with enough points to reverse.";

            SerializedObject so = new SerializedObject(path);
            SerializedProperty points = so.FindProperty("localPoints");
            if (points == null || !points.isArray || points.arraySize < 2)
                return "Path point data is not editable.";

            Undo.RecordObject(path, "Reverse Path Direction");
            for (int i = 0, j = points.arraySize - 1; i < j; i++, j--)
            {
                Vector3 a = points.GetArrayElementAtIndex(i).vector3Value;
                Vector3 b = points.GetArrayElementAtIndex(j).vector3Value;
                points.GetArrayElementAtIndex(i).vector3Value = b;
                points.GetArrayElementAtIndex(j).vector3Value = a;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            PungentSpatialAuthoringEditorState.ReportAction("Reversed path direction.");
            return "Reversed path direction.";
        }

        public static PungentModularSpatialOutput CreateGenericOutputRecipe(Component source)
        {
            if (source == null)
                return null;

            PungentModularSpatialOutput output = Undo.AddComponent<PungentModularSpatialOutput>(source.gameObject);
            output.source = source;
            output.sourceMode = source is PungentAreaAuthoringShape
                ? PungentModularOutputSourceMode.AreaBoundary
                : PungentModularOutputSourceMode.PathCenter;
            EditorUtility.SetDirty(output);
            Selection.activeGameObject = output.gameObject;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            PungentSpatialAuthoringEditorState.ReportAction("Added Spatial Output Recipe.");
            return output;
        }

        public static string AlignActivePathToSurface(bool selectedOnly, float minY, float maxY, float minSlope, float maxSlope)
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            PungentSpatialSurfaceAuthoringSettings settings = PungentSpatialSurfaceAuthoringSettings.Load();
            if (path != null)
            {
                settings.SurfaceMask = path.surfaceMask;
                settings.RaycastHeight = path.raycastStartHeight;
                settings.YOffset = path.yOffset;
            }
            settings.MinY = minY;
            settings.MaxY = maxY;
            settings.MinSlope = minSlope;
            settings.MaxSlope = maxSlope;
            string result = PungentSpatialSurfaceAuthoringUtility.AlignPathPoints(path, selectedOnly, PungentSpatialAuthoringEditorState.SelectedHandleIndex, settings);
            PungentSpatialAuthoringEditorState.ReportAction(result);
            return result;
        }

        public static string AlignActiveAreaToSurface(bool selectedOnly)
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            PungentSpatialSurfaceAuthoringSettings settings = PungentSpatialSurfaceAuthoringSettings.Load();
            string result = PungentSpatialSurfaceAuthoringUtility.AlignAreaPoints(
                PungentSpatialAuthoringEditorState.ActiveArea,
                selectedOnly,
                PungentSpatialAuthoringEditorState.SelectedHandleIndex,
                settings);
            PungentSpatialAuthoringEditorState.ReportAction(result);
            return result;
        }

        public static string RepairActiveArea()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            string result = PungentSpatialAreaRepairUtility.RepairSelfIntersections(PungentSpatialAuthoringEditorState.ActiveArea);
            PungentSpatialAuthoringEditorState.ReportAction(result);
            return result;
        }
    }

    [InitializeOnLoad]
    internal static class PungentSpatialAuthoringSceneInputRouter
    {
        private static bool _subscribed;

        static PungentSpatialAuthoringSceneInputRouter()
        {
            PungentSpatialAuthoringEditorState.Changed -= RefreshSubscription;
            PungentSpatialAuthoringEditorState.Changed += RefreshSubscription;
            RefreshSubscription();
        }

        private static void RefreshSubscription()
        {
#if UNITY_2021_2_OR_NEWER
            bool shouldSubscribe = PungentSpatialAuthoringEditorState.EditModeActive;
#else
            bool shouldSubscribe = PungentSpatialAuthoringEditorState.EditModeActive ||
                                   PungentSpatialAuthoringEditorState.HasSpatialSelection ||
                                   PungentPathAuthoringToolkitWindow.HasOpenWindow();
#endif
            if (_subscribed == shouldSubscribe)
                return;

            if (shouldSubscribe)
                SceneView.duringSceneGui += DuringSceneGUI;
            else
                SceneView.duringSceneGui -= DuringSceneGUI;
            _subscribed = shouldSubscribe;
        }

        private static void DuringSceneGUI(SceneView sceneView)
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            HandleScopedHotkeys();

#if !UNITY_2021_2_OR_NEWER
            if (!PungentSpatialAuthoringEditorState.HasSpatialSelection && !PungentSpatialAuthoringEditorState.EditModeActive)
                return;

            Handles.BeginGUI();
            DrawOverlayGUI();
            Handles.EndGUI();
#endif
        }

#if !UNITY_2021_2_OR_NEWER
        private static void DrawOverlayGUI()
        {
            Rect rect = new Rect(12f, 82f, 390f, PungentSpatialAuthoringEditorState.EditModeActive ? 132f : 86f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Spatial Authoring", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(PungentSpatialAuthoringEditorState.EditModeActive ? "Exit" : "Edit", EditorStyles.miniButton, GUILayout.Width(48f)))
                    PungentSpatialAuthoringEditorState.SetEditModeActive(!PungentSpatialAuthoringEditorState.EditModeActive);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawModeButton("View", PungentSpatialEditMode.View);
                DrawModeButton("Place", PungentSpatialEditMode.Add);
                DrawModeButton("Move", PungentSpatialEditMode.Move);
                DrawModeButton("Delete", PungentSpatialEditMode.Delete);
                DrawModeButton("Output", PungentSpatialEditMode.Output);
            }

            if (PungentSpatialAuthoringEditorState.EditModeActive)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentSpatialAuthoringEditorState.SnapToSurface = GUILayout.Toggle(PungentSpatialAuthoringEditorState.SnapToSurface, "Snap", EditorStyles.miniButton, GUILayout.Width(54f));
                    PungentSpatialAuthoringEditorState.ShowLabels = GUILayout.Toggle(PungentSpatialAuthoringEditorState.ShowLabels, "Labels", EditorStyles.miniButton, GUILayout.Width(62f));
                    PungentSpatialAuthoringEditorState.DrawRichPreview = GUILayout.Toggle(PungentSpatialAuthoringEditorState.DrawRichPreview, "Rich", EditorStyles.miniButton, GUILayout.Width(50f));
                    if (GUILayout.Button("Frame", EditorStyles.miniButton, GUILayout.Width(54f)))
                        PungentSpatialAuthoringActions.FrameActive();
                    using (new EditorGUI.DisabledScope(PungentSpatialAuthoringEditorState.ActivePath == null))
                    {
                        if (GUILayout.Button("Apply", EditorStyles.miniButton, GUILayout.Width(54f)))
                            PungentSpatialAuthoringActions.ApplyGeneratedOutput();
                        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50f)))
                            PungentSpatialAuthoringActions.ClearGeneratedOutput();
                    }
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(52f)))
                        PungentPathAuthoringToolkitWindow.Open();
                }
            }

            EditorGUILayout.LabelField(PungentSpatialAuthoringEditorState.BuildStatusText(), EditorStyles.miniLabel);
            GUILayout.EndArea();
        }

        private static void DrawModeButton(string label, PungentSpatialEditMode mode)
        {
            bool selected = PungentSpatialAuthoringEditorState.EditMode == mode && PungentSpatialAuthoringEditorState.EditModeActive;
            bool next = GUILayout.Toggle(selected, label, EditorStyles.miniButton, GUILayout.Width(label == "Delete" ? 56f : 48f));
            if (next && !selected)
                PungentSpatialAuthoringEditorState.SetEditMode(mode, true);
        }
#endif

        private static void HandleScopedHotkeys()
        {
            if (!PungentSpatialAuthoringEditorState.EditModeActive)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.alt || e.command || e.control)
                return;

            if (e.keyCode == KeyCode.A) { PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Add, true); e.Use(); }
            else if (e.keyCode == KeyCode.M) { PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Move, true); e.Use(); }
            else if (e.keyCode == KeyCode.D) { PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Delete, true); e.Use(); }
            else if (e.keyCode == KeyCode.V) { PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.View, true); e.Use(); }
            else if (e.keyCode == KeyCode.O) { PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Output, true); e.Use(); }
            else if (e.keyCode == KeyCode.Escape)
            {
                if (PungentSpatialAuthoringEditorState.HasSelectedHandle)
                    PungentSpatialAuthoringEditorState.RequestClearHandleSelection();
                else
                    PungentSpatialAuthoringEditorState.SetEditModeActive(false);
                e.Use();
            }
        }
    }

#if UNITY_2021_2_OR_NEWER
    internal static class PungentSpatialOverlayRefreshBinding
    {
        public static void Bind(VisualElement element, Action refresh)
        {
            if (element == null || refresh == null)
                return;

            bool subscribed = false;
            bool delayedRefreshQueued = false;

            void RefreshNow()
            {
                PungentSpatialAuthoringEditorState.SyncSelectionState();
                refresh();
            }

            void QueueDelayedRefresh()
            {
                if (delayedRefreshQueued)
                    return;

                delayedRefreshQueued = true;
                EditorApplication.delayCall += () =>
                {
                    delayedRefreshQueued = false;
                    if (element.panel == null)
                        return;

                    RefreshNow();
                };
            }

            void Subscribe(AttachToPanelEvent _)
            {
                if (!subscribed)
                {
                    PungentSpatialAuthoringEditorState.Changed -= RefreshNow;
                    PungentSpatialAuthoringEditorState.Changed += RefreshNow;
                    subscribed = true;
                }

                RefreshNow();
                QueueDelayedRefresh();
            }

            void Unsubscribe(DetachFromPanelEvent _)
            {
                if (!subscribed)
                    return;

                PungentSpatialAuthoringEditorState.Changed -= RefreshNow;
                subscribed = false;
            }

            element.RegisterCallback<AttachToPanelEvent>(Subscribe);
            element.RegisterCallback<DetachFromPanelEvent>(Unsubscribe);
            RefreshNow();
        }
    }

    [Overlay(typeof(SceneView), "PungentFunk/Spatial Authoring", true)]
    internal sealed class PungentSpatialAuthoringOverlay : Overlay
    {
        private const string PrefControlsOnRight = "PungentFunkUtilities.SpatialAuthoring.ControlsOnRight";
        private const string PrefAlignMinY = "PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinY";
        private const string PrefAlignMaxY = "PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxY";
        private const string PrefAlignMinSlope = "PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMinSlope";
        private const string PrefAlignMaxSlope = "PungentFunkUtilities.SpatialAuthoring.SurfaceAlignMaxSlope";

        private VisualElement _root;
        private VisualElement _body;
        private VisualElement _rail;
        private VisualElement _contentColumn;
        private VisualElement _creationSection;
        private VisualElement _selectionSection;
        private VisualElement _metadataContainer;
        private VisualElement _railGenerationGroup;
        private VisualElement _commonParametersSection;
        private VisualElement _pathParametersSection;
        private VisualElement _areaParametersSection;
        private VisualElement _surfaceParametersSection;
        private VisualElement _runtimeSection;
        private VisualElement _modularOutputSection;
        private VisualElement _diagnosticsSection;
        private Label _statusLabel;
        private Label _typeChipLabel;
        private Label _modeChipLabel;
        private Label _hintLabel;
        private Label _detailLabel;
        private Label _performanceLabel;
        private Label _targetSummaryLabel;
        private Label _runtimeStatusLabel;
        private Label _modularOutputStatusLabel;
        private Label _diagnosticsStatusLabel;
        private Button _editButton;
        private Button _frameButton;
        private Button _applyButton;
        private Button _clearButton;
        private Button _addGenericOutputButton;
        private Button _recipeValidateButton;
        private Button _recipeApplyButton;
        private Button _recipeClearButton;
        private Button _recipeFrameButton;
        private Button _viewModeButton;
        private Button _placeModeButton;
        private Button _moveModeButton;
        private Button _deleteModeButton;
        private Button _outputModeButton;
        private Foldout _settingsFoldout;
        private Foldout _commonFoldout;
        private Foldout _previewFoldout;
        private Foldout _pathFoldout;
        private Foldout _pathPreviewFoldout;
        private Foldout _pathSurfaceFoldout;
        private Foldout _pathGenerationFoldout;
        private Foldout _areaFoldout;
        private Foldout _areaPreviewFoldout;
        private Foldout _surfaceFoldout;
        private Foldout _runtimeFoldout;
        private Foldout _modularOutputFoldout;
        private Foldout _diagnosticsFoldout;
        private Toggle _controlsRightToggle;
        private Toggle _snapToggle;
        private Toggle _labelsToggle;
        private Toggle _richPreviewToggle;
        private EnumField _previewQualityField;
        private FloatField _handleSizeField;
        private LayerMaskField _sharedSurfaceMaskField;
        private Toggle _pathExposeWidthToggle;
        private FloatField _pathWidthField;
        private Slider _pathCorridorAlphaSlider;
        private Toggle _pathClosedToggle;
        private EnumField _pathSamplingField;
        private FloatField _pathSamplesPerMeterField;
        private IntegerField _pathMinSamplesPerSpanField;
        private Toggle _pathSurfaceConformToggle;
        private Toggle _pathAlignNormalToggle;
        private Toggle _pathSampleEndsToggle;
        private LayerMaskField _pathSurfaceMaskField;
        private FloatField _pathRaycastHeightField;
        private FloatField _pathYOffsetField;
        private Toggle _pathPreviewToggle;
        private Toggle _pathAutoRebuildToggle;
        private Toggle _pathDrawGizmosToggle;
        private Toggle _pathDrawUnselectedGizmoToggle;
        private FloatField _alignMinYField;
        private FloatField _alignMaxYField;
        private FloatField _alignMinSlopeField;
        private FloatField _alignMaxSlopeField;
        private Label _surfaceAlignStatusLabel;
        private EnumField _areaShapeField;
        private FloatField _areaSizeXField;
        private FloatField _areaSizeZField;
        private FloatField _areaRadiusField;
        private EnumField _areaVerticalModeField;
        private FloatField _areaHeightField;
        private FloatField _areaMinYField;
        private FloatField _areaMaxYField;
        private Toggle _areaDrawGizmoToggle;
        private Toggle _areaDrawUnselectedGizmoToggle;
        private Toggle _areaRuntimeQueryableToggle;
        private UnityEngine.Object _metadataTarget;
        private SerializedObject _metadataSerializedObject;
        private bool _appliedControlsOnRight;
        private bool _layoutApplied;

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();
            _root.style.minWidth = 280f;
            _root.style.maxWidth = 360f;
            _root.style.paddingLeft = 8f;
            _root.style.paddingRight = 8f;
            _root.style.paddingTop = 6f;
            _root.style.paddingBottom = 6f;

            VisualElement headerRow = MakeRow();
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 2f;
            _statusLabel = new Label();
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statusLabel.style.flexGrow = 1f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _typeChipLabel = MakeChipLabel();
            _modeChipLabel = MakeChipLabel();
            headerRow.Add(_statusLabel);
            headerRow.Add(_typeChipLabel);
            headerRow.Add(_modeChipLabel);
            _root.Add(headerRow);

            _hintLabel = new Label();
            _hintLabel.style.whiteSpace = WhiteSpace.Normal;
            _hintLabel.style.marginBottom = 4f;
            _root.Add(_hintLabel);

            _detailLabel = new Label();
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailLabel.style.opacity = 0.86f;
            _detailLabel.style.marginBottom = 4f;
            _root.Add(_detailLabel);

            _performanceLabel = new Label();
            _performanceLabel.style.whiteSpace = WhiteSpace.Normal;
            _performanceLabel.style.opacity = 0.72f;
            _performanceLabel.style.marginBottom = 6f;
            _root.Add(_performanceLabel);

            _creationSection = MakeSection();
            VisualElement creationRow = MakeRow();
            creationRow.Add(MakeActionButton("Create Path", () => PungentSpatialAuthoringActions.CreatePathAtScenePivot(), "Create a preview-only spatial path at the Scene view pivot."));
            creationRow.Add(MakeActionButton("Create Area", () => PungentSpatialAuthoringActions.CreateAreaAtScenePivot(), "Create a spatial area at the Scene view pivot."));
            creationRow.Add(MakeActionButton("Open Workbench", PungentPathAuthoringToolkitWindow.Open, "Open the Spatial Authoring Workbench."));
            _creationSection.Add(creationRow);
            _root.Add(_creationSection);

            _body = new VisualElement();
            _body.style.flexDirection = FlexDirection.Row;
            _body.style.alignItems = Align.FlexStart;
            _root.Add(_body);

            _rail = new VisualElement();
            _rail.style.width = 76f;
            _rail.style.minWidth = 76f;
            _rail.style.marginRight = 6f;
            _rail.style.marginLeft = 0f;

            _editButton = MakeRailButton("Edit", ToggleEditMode, "Enter or exit Spatial Authoring edit mode.");
            _viewModeButton = MakeRailButton("View  V", () => SetMode(PungentSpatialEditMode.View), "Allow Scene selection passthrough while staying in the spatial workflow. Hotkey: V.");
            _placeModeButton = MakeRailButton("Place  A", () => SetMode(PungentSpatialEditMode.Add), "Place a new point contextually. Shift appends, Ctrl/Cmd inserts. Hotkey: A.");
            _moveModeButton = MakeRailButton("Move  M", () => SetMode(PungentSpatialEditMode.Move), "Move selected points or shape handles. Hotkey: M.");
            _deleteModeButton = MakeRailButton("Delete  D", () => SetMode(PungentSpatialEditMode.Delete), "Delete the nearest editable point. Hotkey: D.");
            _outputModeButton = MakeRailButton("Output  O", () => SetMode(PungentSpatialEditMode.Output), "Configure and preview Spatial Output Recipes. Hotkey: O.");
            _frameButton = MakeRailButton("Frame", PungentSpatialAuthoringActions.FrameActive, "Frame the active spatial object in the Scene view.");
            _rail.Add(_editButton);
            _rail.Add(_viewModeButton);
            _rail.Add(_placeModeButton);
            _rail.Add(_moveModeButton);
            _rail.Add(_deleteModeButton);
            _rail.Add(_outputModeButton);
            _rail.Add(_frameButton);

            _railGenerationGroup = new VisualElement();
            _railGenerationGroup.style.marginTop = 6f;
            _applyButton = MakeRailButton("Apply", () => PungentSpatialOutputRecipeEditorUtility.ApplyRecipe(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource)), "Apply the selected Spatial Output Recipe.");
            _clearButton = MakeRailButton("Clear", () => PungentSpatialOutputRecipeEditorUtility.ClearRecipe(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource)), "Clear generated objects from the selected Spatial Output Recipe.");
            _railGenerationGroup.Add(_applyButton);
            _railGenerationGroup.Add(_clearButton);
            _rail.Add(_railGenerationGroup);
            _rail.Add(MakeRailButton("Open", PungentPathAuthoringToolkitWindow.Open, "Open the Spatial Authoring Workbench."));

            _contentColumn = new VisualElement();
            _contentColumn.style.flexGrow = 1f;
            _contentColumn.style.minWidth = 220f;

            _selectionSection = MakeSection();
            _targetSummaryLabel = MakeMutedLabel();
            _selectionSection.Add(_targetSummaryLabel);
            _contentColumn.Add(_selectionSection);

            _settingsFoldout = new Foldout { text = "Settings", value = false };
            _controlsRightToggle = MakeToggle("Controls On Right", "Anchor the in-panel control rail on the right side.", value =>
            {
                EditorPrefs.SetBool(PrefControlsOnRight, value);
                _layoutApplied = false;
                PungentSpatialAuthoringEditorState.RequestSceneRepaint();
                Refresh();
            });
            _settingsFoldout.Add(_controlsRightToggle);
            _contentColumn.Add(_settingsFoldout);

            _commonFoldout = new Foldout { text = "Identity", value = true };
            _metadataContainer = new VisualElement();
            _commonFoldout.Add(_metadataContainer);
            _contentColumn.Add(_commonFoldout);

            _previewFoldout = new Foldout { text = "Preview", value = true };
            BuildPreviewParameters(_previewFoldout);
            _contentColumn.Add(_previewFoldout);

            _pathFoldout = new Foldout { text = "Path", value = true };
            BuildPathParameters(_pathFoldout);
            _contentColumn.Add(_pathFoldout);

            _areaFoldout = new Foldout { text = "Area", value = true };
            BuildAreaParameters(_areaFoldout);
            _contentColumn.Add(_areaFoldout);

            _surfaceFoldout = new Foldout { text = "Surface", value = false };
            BuildSurfaceParameters(_surfaceFoldout);
            _contentColumn.Add(_surfaceFoldout);

            _runtimeFoldout = new Foldout { text = "Runtime", value = false };
            BuildRuntimeParameters(_runtimeFoldout);
            _contentColumn.Add(_runtimeFoldout);

            _modularOutputFoldout = new Foldout { text = "Spatial Output Recipe", value = false };
            BuildModularOutputParameters(_modularOutputFoldout);
            _contentColumn.Add(_modularOutputFoldout);

            _diagnosticsFoldout = new Foldout { text = "Diagnostics", value = false };
            BuildDiagnosticsParameters(_diagnosticsFoldout);
            _contentColumn.Add(_diagnosticsFoldout);

            PungentSpatialOverlayRefreshBinding.Bind(_root, Refresh);
            return _root;
        }

        private static VisualElement MakeRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;
            return row;
        }

        private static VisualElement MakeSection()
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 2f;
            section.style.marginBottom = 4f;
            return section;
        }

        private static Label MakeMutedLabel()
        {
            Label label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.78f;
            label.style.marginBottom = 4f;
            return label;
        }

        private static Label MakeChipLabel()
        {
            Label label = new Label();
            label.style.marginLeft = 4f;
            label.style.paddingLeft = 5f;
            label.style.paddingRight = 5f;
            label.style.paddingTop = 1f;
            label.style.paddingBottom = 1f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.borderTopLeftRadius = 5f;
            label.style.borderTopRightRadius = 5f;
            label.style.borderBottomLeftRadius = 5f;
            label.style.borderBottomRightRadius = 5f;
            label.style.backgroundColor = new Color(0.18f, 0.22f, 0.25f, 0.72f);
            label.style.color = new Color(0.88f, 0.95f, 1f, 1f);
            return label;
        }

        private static Button MakeActionButton(string label, Action action, string tooltip)
        {
            Button button = new Button(action) { text = label, tooltip = tooltip };
            button.style.flexGrow = 1f;
            button.style.marginRight = 3f;
            return button;
        }

        private static Button MakeRailButton(string label, Action action, string tooltip)
        {
            Button button = new Button(action) { text = label, tooltip = tooltip };
            button.style.marginBottom = 3f;
            button.style.minHeight = 24f;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }

        private static VisualElement MakeModeRow(string label, string hotkey, PungentSpatialEditMode mode, out Button button, string tooltip)
        {
            VisualElement row = MakeRow();
            row.style.alignItems = Align.Center;
            button = MakeActionButton(label, () => SetMode(mode), tooltip + " Hotkey: " + hotkey + ".");
            Label key = new Label(hotkey);
            key.style.minWidth = 22f;
            key.style.unityTextAlign = TextAnchor.MiddleRight;
            key.style.opacity = 0.72f;
            row.Add(button);
            row.Add(key);
            return row;
        }

        private static void SetMode(PungentSpatialEditMode mode)
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            if (!PungentSpatialAuthoringEditorState.HasSpatialSelection)
                return;

            PungentSpatialAuthoringEditorState.SetEditMode(mode, true);
        }

        private static void ToggleEditMode()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            if (!PungentSpatialAuthoringEditorState.HasSpatialSelection)
                return;

            if (PungentSpatialAuthoringEditorState.EditModeActive)
                PungentSpatialAuthoringEditorState.ExitEditMode();
            else
                PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Move, true);
        }

        private void ApplyRailLayout()
        {
            if (_body == null || _rail == null || _contentColumn == null)
                return;

            bool controlsOnRight = EditorPrefs.GetBool(PrefControlsOnRight, false);
            if (_layoutApplied && _appliedControlsOnRight == controlsOnRight)
                return;

            _body.Clear();
            _rail.style.marginLeft = controlsOnRight ? 6f : 0f;
            _rail.style.marginRight = controlsOnRight ? 0f : 6f;
            if (controlsOnRight)
            {
                _body.Add(_contentColumn);
                _body.Add(_rail);
            }
            else
            {
                _body.Add(_rail);
                _body.Add(_contentColumn);
            }

            _appliedControlsOnRight = controlsOnRight;
            _layoutApplied = true;
        }

        private void RebuildMetadataFields(UnityEngine.Object target, bool pathTarget)
        {
            if (_metadataContainer == null)
                return;

            if (_metadataTarget == target)
            {
                if (_metadataSerializedObject != null)
                    _metadataSerializedObject.UpdateIfRequiredOrScript();
                return;
            }

            _metadataTarget = target;
            _metadataSerializedObject = target != null ? new SerializedObject(target) : null;
            _metadataContainer.Clear();
            if (_metadataSerializedObject == null)
                return;

            AddMetadataProperty(pathTarget ? "stablePathId" : "stableAreaId", "Stable ID");
            AddMetadataProperty("displayName", "Display Name");
            AddMetadataProperty("categories", "Categories / Tags");
            AddMetadataProperty(pathTarget ? "pathColor" : "color", "Color");
            _metadataContainer.Bind(_metadataSerializedObject);
        }

        private void AddMetadataProperty(string propertyName, string label)
        {
            if (_metadataSerializedObject == null)
                return;

            SerializedProperty property = _metadataSerializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            PropertyField field = new PropertyField(property, label);
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ => MarkActiveMetadataChanged());
            _metadataContainer.Add(field);
        }

        private static void MarkActiveMetadataChanged()
        {
            ModularPathSpawner path = ActivePath();
            if (path != null)
            {
                MarkPathChanged(path);
                return;
            }

            PungentAreaAuthoringShape area = ActiveArea();
            if (area != null)
                MarkAreaChanged(area);
        }

        private void AlignActivePath(bool selectedOnly)
        {
            float minY = Mathf.Min(_alignMinYField.value, _alignMaxYField.value);
            float maxY = Mathf.Max(_alignMinYField.value, _alignMaxYField.value);
            float minSlope = Mathf.Clamp(Mathf.Min(_alignMinSlopeField.value, _alignMaxSlopeField.value), 0f, 90f);
            float maxSlope = Mathf.Clamp(Mathf.Max(_alignMinSlopeField.value, _alignMaxSlopeField.value), 0f, 90f);
            string result = PungentSpatialAuthoringActions.AlignActivePathToSurface(selectedOnly, minY, maxY, minSlope, maxSlope);
            if (_surfaceAlignStatusLabel != null)
                _surfaceAlignStatusLabel.text = result;
            PungentSpatialAuthoringEditorState.ReportAction(result);
            Refresh();
        }

        private void BuildPreviewParameters(VisualElement parent)
        {
            _commonParametersSection = MakeSection();
            _labelsToggle = MakeToggle("Labels", "Show selected/full point labels according to preview quality.", value => PungentSpatialAuthoringEditorState.ShowLabels = value);
            _richPreviewToggle = MakeToggle("Rich Preview / Fill", "Show path ribbons and area fills when preview quality allows it.", value => PungentSpatialAuthoringEditorState.DrawRichPreview = value);
            _previewQualityField = new EnumField("Preview Quality", PungentSpatialPreviewQuality.Reduced);
            _previewQualityField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is PungentSpatialPreviewQuality quality)
                    PungentSpatialAuthoringEditorState.PreviewQuality = quality;
            });
            _handleSizeField = new FloatField("Handle Size");
            _handleSizeField.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.HandleSize = evt.newValue);
            _pathPreviewToggle = MakePathToggle("Preview While Editing", "Draw selected path preview while editing.", (path, value) => path.previewWhileEditing = value);
            _pathDrawGizmosToggle = MakePathToggle("Draw Path Gizmos", "Draw path gizmos and previews.", (path, value) => path.drawGizmos = value);
            _pathDrawUnselectedGizmoToggle = MakePathToggle("Draw Path Unselected", "Draw lightweight path gizmos when this path is not selected.", (path, value) => path.drawUnselectedGizmo = value);
            _areaDrawGizmoToggle = MakeAreaToggle("Draw Area Gizmo", "Draw area gizmos and previews.", (area, value) => area.drawGizmo = value);
            _areaDrawUnselectedGizmoToggle = MakeAreaToggle("Draw Area Unselected", "Draw lightweight area gizmos when this area is not selected.", (area, value) => area.drawUnselectedGizmo = value);

            _commonParametersSection.Add(_labelsToggle);
            _commonParametersSection.Add(_richPreviewToggle);
            _commonParametersSection.Add(_previewQualityField);
            _commonParametersSection.Add(_handleSizeField);
            _commonParametersSection.Add(_pathPreviewToggle);
            _commonParametersSection.Add(_pathDrawGizmosToggle);
            _commonParametersSection.Add(_pathDrawUnselectedGizmoToggle);
            _commonParametersSection.Add(_areaDrawGizmoToggle);
            _commonParametersSection.Add(_areaDrawUnselectedGizmoToggle);
            parent.Add(_commonParametersSection);
        }

        private void BuildPathParameters(VisualElement parent)
        {
            _pathParametersSection = MakeSection();
            _pathExposeWidthToggle = MakePathToggle("Use Corridor Width", "Expose the path corridor width to previews and generic adapters.", (path, value) => path.exposePreviewCorridorWidth = value);
            _pathWidthField = new FloatField("Width");
            _pathWidthField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Width");
                path.previewCorridorWidth = Mathf.Max(0.01f, evt.newValue);
                path.exposePreviewCorridorWidth = true;
                MarkPathChanged(path);
            });
            _pathCorridorAlphaSlider = new Slider("Corridor Alpha", 0f, 1f);
            _pathCorridorAlphaSlider.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Corridor Alpha");
                path.previewCorridorAlpha = Mathf.Clamp01(evt.newValue);
                MarkPathChanged(path);
            });
            _pathClosedToggle = MakePathToggle("Closed Loop", "Toggle selected path closed loop.", (path, value) => path.closedLoop = value);
            _pathSamplingField = new EnumField("Sampling", ModularPathSpawner.PathMode.Polyline);
            _pathSamplingField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null || !(evt.newValue is ModularPathSpawner.PathMode mode))
                    return;

                Undo.RecordObject(path, "Set Path Sampling");
                path.pathMode = mode;
                MarkPathChanged(path);
            });
            _pathSamplesPerMeterField = new FloatField("Samples / Meter");
            _pathSamplesPerMeterField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Samples Per Meter");
                path.samplesPerMeter = Mathf.Max(0.1f, evt.newValue);
                MarkPathChanged(path);
            });
            _pathMinSamplesPerSpanField = new IntegerField("Min Samples / Span");
            _pathMinSamplesPerSpanField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Min Samples");
                path.minSamplesPerSpan = Mathf.Max(2, evt.newValue);
                MarkPathChanged(path);
            });
            _pathSurfaceConformToggle = MakePathToggle("Surface Conform", "Conform generated/preview points to the configured surface mask.", (path, value) => path.conformToSurface = value);
            _pathAlignNormalToggle = MakePathToggle("Align To Surface Normal", "Orient generated pieces to sampled surface normals.", (path, value) => path.alignToSurfaceNormal = value);
            _pathSampleEndsToggle = MakePathToggle("Sample Segment Ends", "Sample both ends of generated segments on uneven surfaces.", (path, value) => path.sampleSurfaceAtSegmentEnds = value);
            _pathSurfaceMaskField = new LayerMaskField("Surface Mask");
            _pathSurfaceMaskField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Surface Mask");
                path.surfaceMask = evt.newValue;
                MarkPathChanged(path);
            });
            _pathRaycastHeightField = new FloatField("Raycast Height");
            _pathRaycastHeightField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Raycast Height");
                path.raycastStartHeight = Mathf.Max(0.01f, evt.newValue);
                MarkPathChanged(path);
            });
            _pathYOffsetField = new FloatField("Y Offset");
            _pathYOffsetField.RegisterValueChangedCallback(evt =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, "Set Path Y Offset");
                path.yOffset = evt.newValue;
                MarkPathChanged(path);
            });
            _pathAutoRebuildToggle = MakePathToggle("Auto Rebuild", "Automatically rebuild generated path output in the editor.", (path, value) => path.autoRebuildInEditor = value);

            _pathParametersSection.Add(_pathExposeWidthToggle);
            _pathParametersSection.Add(_pathWidthField);
            _pathParametersSection.Add(_pathCorridorAlphaSlider);
            _pathParametersSection.Add(_pathClosedToggle);
            _pathParametersSection.Add(_pathSamplingField);
            _pathParametersSection.Add(_pathSamplesPerMeterField);
            _pathParametersSection.Add(_pathMinSamplesPerSpanField);

            _pathGenerationFoldout = new Foldout { text = "Generation", value = false };
            _pathGenerationFoldout.Add(_pathAutoRebuildToggle);
            Label generationHint = MakeMutedLabel();
            generationHint.text = "Apply and Clear stay in the side rail so generated-object changes remain explicit.";
            _pathGenerationFoldout.Add(generationHint);
            _pathParametersSection.Add(_pathGenerationFoldout);
            parent.Add(_pathParametersSection);
        }

        private void BuildAreaParameters(VisualElement parent)
        {
            _areaParametersSection = MakeSection();
            _areaShapeField = new EnumField("Shape", PungentAreaAuthoringShape.ShapeMode.PolygonXZ);
            _areaShapeField.RegisterValueChangedCallback(evt =>
            {
                PungentAreaAuthoringShape area = ActiveArea();
                if (area == null || !(evt.newValue is PungentAreaAuthoringShape.ShapeMode mode))
                    return;

                Undo.RecordObject(area, "Set Area Shape");
                area.shapeMode = mode;
                MarkAreaChanged(area);
                Refresh();
            });
            _areaSizeXField = new FloatField("Rect X");
            _areaSizeZField = new FloatField("Rect Z");
            _areaSizeXField.RegisterValueChangedCallback(_ => ApplyAreaSize());
            _areaSizeZField.RegisterValueChangedCallback(_ => ApplyAreaSize());
            _areaRadiusField = new FloatField("Radius");
            _areaRadiusField.RegisterValueChangedCallback(evt =>
            {
                PungentAreaAuthoringShape area = ActiveArea();
                if (area == null)
                    return;

                Undo.RecordObject(area, "Set Area Radius");
                area.circleRadius = Mathf.Max(0.01f, evt.newValue);
                MarkAreaChanged(area);
            });
            _areaVerticalModeField = new EnumField("Vertical Mode", PungentAreaAuthoringShape.VerticalMode.HeightFromTransform);
            _areaVerticalModeField.RegisterValueChangedCallback(evt =>
            {
                PungentAreaAuthoringShape area = ActiveArea();
                if (area == null || !(evt.newValue is PungentAreaAuthoringShape.VerticalMode mode))
                    return;

                Undo.RecordObject(area, "Set Area Vertical Mode");
                area.verticalMode = mode;
                MarkAreaChanged(area);
                Refresh();
            });
            _areaHeightField = new FloatField("Height");
            _areaHeightField.RegisterValueChangedCallback(evt =>
            {
                PungentAreaAuthoringShape area = ActiveArea();
                if (area == null)
                    return;

                Undo.RecordObject(area, "Set Area Height");
                area.height = Mathf.Max(0.01f, evt.newValue);
                MarkAreaChanged(area);
            });
            _areaMinYField = new FloatField("Min Y");
            _areaMaxYField = new FloatField("Max Y");
            _areaMinYField.RegisterValueChangedCallback(_ => ApplyAreaMinMax());
            _areaMaxYField.RegisterValueChangedCallback(_ => ApplyAreaMinMax());

            _areaParametersSection.Add(_areaShapeField);
            _areaParametersSection.Add(_areaSizeXField);
            _areaParametersSection.Add(_areaSizeZField);
            _areaParametersSection.Add(_areaRadiusField);
            _areaParametersSection.Add(_areaVerticalModeField);
            _areaParametersSection.Add(_areaHeightField);
            _areaParametersSection.Add(_areaMinYField);
            _areaParametersSection.Add(_areaMaxYField);
            parent.Add(_areaParametersSection);
        }

        private void BuildSurfaceParameters(VisualElement parent)
        {
            _surfaceParametersSection = MakeSection();
            _snapToggle = MakeToggle("Snap To Surface", "Snap edited points to the shared scene editing surface mask where supported.", value => PungentSpatialAuthoringEditorState.SnapToSurface = value);
            _sharedSurfaceMaskField = new LayerMaskField("Scene Editing Mask");
            _sharedSurfaceMaskField.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.SurfaceMask = evt.newValue);

            _surfaceParametersSection.Add(_snapToggle);
            _surfaceParametersSection.Add(_sharedSurfaceMaskField);

            _pathSurfaceFoldout = new Foldout { text = "Path Surface Output", value = false };
            _pathSurfaceFoldout.Add(_pathSurfaceConformToggle);
            _pathSurfaceFoldout.Add(_pathSurfaceMaskField);
            _pathSurfaceFoldout.Add(_pathRaycastHeightField);
            _pathSurfaceFoldout.Add(_pathYOffsetField);
            _pathSurfaceFoldout.Add(_pathAlignNormalToggle);
            _pathSurfaceFoldout.Add(_pathSampleEndsToggle);
            _pathSurfaceFoldout.Add(MakeSubheading("Re-align"));
            _alignMinYField = new FloatField("Align Min Y");
            _alignMaxYField = new FloatField("Align Max Y");
            _alignMinSlopeField = new FloatField("Align Min Slope");
            _alignMaxSlopeField = new FloatField("Align Max Slope");
            _alignMinYField.RegisterValueChangedCallback(evt => EditorPrefs.SetFloat(PrefAlignMinY, evt.newValue));
            _alignMaxYField.RegisterValueChangedCallback(evt => EditorPrefs.SetFloat(PrefAlignMaxY, evt.newValue));
            _alignMinSlopeField.RegisterValueChangedCallback(evt => EditorPrefs.SetFloat(PrefAlignMinSlope, Mathf.Clamp(evt.newValue, 0f, 90f)));
            _alignMaxSlopeField.RegisterValueChangedCallback(evt => EditorPrefs.SetFloat(PrefAlignMaxSlope, Mathf.Clamp(evt.newValue, 0f, 90f)));
            _surfaceAlignStatusLabel = MakeMutedLabel();
            _pathSurfaceFoldout.Add(_alignMinYField);
            _pathSurfaceFoldout.Add(_alignMaxYField);
            _pathSurfaceFoldout.Add(_alignMinSlopeField);
            _pathSurfaceFoldout.Add(_alignMaxSlopeField);
            VisualElement alignRow = MakeRow();
            alignRow.Add(MakeActionButton("Align Selected", () => AlignActivePath(selectedOnly: true), "Move the selected path point to a valid surface hit."));
            alignRow.Add(MakeActionButton("Align All", () => AlignActivePath(selectedOnly: false), "Move all path points to valid surface hits."));
            _pathSurfaceFoldout.Add(alignRow);
            _pathSurfaceFoldout.Add(_surfaceAlignStatusLabel);
            _surfaceParametersSection.Add(_pathSurfaceFoldout);
            parent.Add(_surfaceParametersSection);
        }

        private void BuildRuntimeParameters(VisualElement parent)
        {
            _runtimeSection = MakeSection();
            _runtimeStatusLabel = MakeMutedLabel();
            _areaRuntimeQueryableToggle = MakeAreaToggle("Runtime Query", "Allow runtime contains/bounds queries.", (area, value) => area.runtimeQueryable = value);
            _runtimeSection.Add(_runtimeStatusLabel);
            _runtimeSection.Add(_areaRuntimeQueryableToggle);
            parent.Add(_runtimeSection);
        }

        private void BuildModularOutputParameters(VisualElement parent)
        {
            _modularOutputSection = MakeSection();
            _modularOutputStatusLabel = MakeMutedLabel();
            _modularOutputSection.Add(_modularOutputStatusLabel);
            VisualElement row = MakeRow();
            _addGenericOutputButton = MakeActionButton("Add Recipe", () => PungentSpatialOutputRecipeEditorUtility.CreateRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource), "Attach a Spatial Output Recipe to the selected path or area.");
            row.Add(_addGenericOutputButton);
            _recipeValidateButton = MakeActionButton("Validate", () =>
            {
                string result = PungentSpatialOutputRecipeEditorUtility.ValidateRecipe(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource));
                PungentSpatialAuthoringEditorState.ReportAction(result);
                Refresh();
            }, "Validate the selected recipe lane.");
            row.Add(_recipeValidateButton);
            _modularOutputSection.Add(row);

            VisualElement actionRow = MakeRow();
            _recipeApplyButton = MakeActionButton("Apply", () => PungentSpatialOutputRecipeEditorUtility.ApplyRecipe(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource)), "Apply the selected Spatial Output Recipe.");
            _recipeClearButton = MakeActionButton("Clear", () => PungentSpatialOutputRecipeEditorUtility.ClearRecipe(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource)), "Clear generated output for the selected recipe.");
            _recipeFrameButton = MakeActionButton("Frame Gen", () => PungentSpatialOutputRecipeEditorUtility.FrameGenerated(PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource)), "Frame generated recipe objects.");
            actionRow.Add(_recipeApplyButton);
            actionRow.Add(_recipeClearButton);
            actionRow.Add(_recipeFrameButton);
            _modularOutputSection.Add(actionRow);

            VisualElement handoffRow = MakeRow();
            handoffRow.Add(MakeActionButton("Open Workbench", PungentPathAuthoringToolkitWindow.Open, "Open the full workbench for detailed recipe setup."));
            _modularOutputSection.Add(handoffRow);
            parent.Add(_modularOutputSection);
        }

        private void BuildDiagnosticsParameters(VisualElement parent)
        {
            _diagnosticsSection = MakeSection();
            _diagnosticsStatusLabel = MakeMutedLabel();
            _diagnosticsSection.Add(_diagnosticsStatusLabel);
            parent.Add(_diagnosticsSection);
        }

        private static Label MakeSubheading(string text)
        {
            Label label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 4f;
            label.style.marginBottom = 3f;
            return label;
        }

        private static Toggle MakeToggle(string label, string tooltip, Action<bool> apply)
        {
            Toggle toggle = new Toggle(label) { tooltip = tooltip };
            toggle.RegisterValueChangedCallback(evt => apply(evt.newValue));
            return toggle;
        }

        private static Toggle MakePathToggle(string label, string tooltip, Action<ModularPathSpawner, bool> apply)
        {
            return MakeToggle(label, tooltip, value =>
            {
                ModularPathSpawner path = ActivePath();
                if (path == null)
                    return;

                Undo.RecordObject(path, label);
                apply(path, value);
                MarkPathChanged(path);
            });
        }

        private static Toggle MakeAreaToggle(string label, string tooltip, Action<PungentAreaAuthoringShape, bool> apply)
        {
            return MakeToggle(label, tooltip, value =>
            {
                PungentAreaAuthoringShape area = ActiveArea();
                if (area == null)
                    return;

                Undo.RecordObject(area, label);
                apply(area, value);
                MarkAreaChanged(area);
            });
        }

        private void Refresh()
        {
            if (_root == null)
                return;

            PungentSpatialAuthoringEditorState.SyncSelectionState();
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            PungentAreaAuthoringShape area = PungentSpatialAuthoringEditorState.ActiveArea;
            bool hasPath = path != null;
            bool hasArea = area != null;
            bool hasSelection = hasPath || hasArea;
            bool editMode = PungentSpatialAuthoringEditorState.EditModeActive;
            bool outputMode = editMode && PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output;
            Component source = hasPath ? (Component)path : area;
            PungentModularSpatialOutput selectedRecipe = PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(source);

            _statusLabel.text = hasPath
                ? path.SpatialDisplayName
                : hasArea
                    ? area.SpatialDisplayName
                    : "Spatial Authoring";
            _typeChipLabel.text = hasPath ? "PATH" : hasArea ? "AREA" : "READY";
            _modeChipLabel.text = PungentSpatialAuthoringEditorState.BuildModeChipText();
            _hintLabel.text = PungentSpatialAuthoringEditorState.BuildNextActionHint();
            _detailLabel.text = PungentSpatialAuthoringEditorState.BuildHandleStatusText();
            _performanceLabel.text = PungentSpatialAuthoringEditorState.BuildPerformanceStatusText();

            SetShown(_creationSection, !hasSelection);
            SetShown(_body, hasSelection);
            SetShown(_selectionSection, hasSelection);
            SetShown(_rail, false);
            SetShown(_settingsFoldout, false);
            SetShown(_commonFoldout, false);
            SetShown(_previewFoldout, false);
            SetShown(_pathFoldout, false);
            SetShown(_areaFoldout, false);
            SetShown(_surfaceFoldout, false);
            SetShown(_runtimeFoldout, false);
            SetShown(_modularOutputFoldout, false);
            SetShown(_diagnosticsFoldout, false);
            SetShown(_railGenerationGroup, false);
            SetShown(_commonParametersSection, false);
            SetShown(_pathParametersSection, false);
            SetShown(_areaParametersSection, false);
            SetShown(_surfaceParametersSection, false);
            SetShown(_runtimeSection, false);
            SetShown(_modularOutputSection, false);
            SetShown(_diagnosticsSection, false);
            SetShown(_pathSurfaceFoldout, false);

            _targetSummaryLabel.text = hasPath
                ? path.SpatialDisplayName + " / " + path.PointCount + " points / " + path.ExistingGeneratedChildCount + " generated. Use Spatial Tools for editing and Tool Settings for active parameters."
                : hasArea
                    ? area.SpatialDisplayName + " / " + area.PolygonPointCount + " points / " + area.shapeMode + ". Use Spatial Tools for editing and Tool Settings for active parameters."
                    : string.Empty;
            _editButton.text = editMode ? "Exit" : "Edit";
            _editButton.SetEnabled(hasSelection);
            _frameButton.SetEnabled(hasSelection);
            _viewModeButton.SetEnabled(hasSelection);
            _moveModeButton.SetEnabled(hasSelection);
            bool pointModeAvailable = hasPath || (hasArea && area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ);
            SetShown(_placeModeButton, pointModeAvailable);
            SetShown(_deleteModeButton, pointModeAvailable);
            _placeModeButton.SetEnabled(pointModeAvailable);
            _deleteModeButton.SetEnabled(pointModeAvailable);
            _outputModeButton.SetEnabled(hasSelection);
            _applyButton.SetEnabled(selectedRecipe != null);
            _clearButton.SetEnabled(selectedRecipe != null && selectedRecipe.ExistingGeneratedChildCount > 0);
            if (_addGenericOutputButton != null)
                _addGenericOutputButton.SetEnabled(hasSelection);
            if (_recipeValidateButton != null)
                _recipeValidateButton.SetEnabled(selectedRecipe != null);
            if (_recipeApplyButton != null)
                _recipeApplyButton.SetEnabled(selectedRecipe != null);
            if (_recipeClearButton != null)
                _recipeClearButton.SetEnabled(selectedRecipe != null && selectedRecipe.ExistingGeneratedChildCount > 0);
            if (_recipeFrameButton != null)
                _recipeFrameButton.SetEnabled(selectedRecipe != null && selectedRecipe.ExistingGeneratedRoot != null);

            _controlsRightToggle.SetValueWithoutNotify(EditorPrefs.GetBool(PrefControlsOnRight, false));
            StyleRailButton(_editButton, editMode, hasSelection);
            RefreshModeButton(_viewModeButton, PungentSpatialEditMode.View, hasSelection, editMode, "View  V");
            RefreshModeButton(_placeModeButton, PungentSpatialEditMode.Add, pointModeAvailable, editMode, "Place  A");
            RefreshModeButton(_moveModeButton, PungentSpatialEditMode.Move, hasSelection, editMode, "Move  M");
            RefreshModeButton(_deleteModeButton, PungentSpatialEditMode.Delete, hasSelection, editMode, "Delete  D");
            RefreshModeButton(_outputModeButton, PungentSpatialEditMode.Output, hasSelection, editMode, "Output  O");

            SetShown(_modularOutputFoldout, hasSelection && outputMode);
            SetShown(_modularOutputSection, hasSelection && outputMode);
            SetShown(_railGenerationGroup, hasSelection && outputMode);
            if (outputMode && _modularOutputFoldout != null)
                _modularOutputFoldout.value = true;
            if (outputMode)
                RefreshSharedStatus(path, area);
        }

        private static void RefreshModeButton(Button button, PungentSpatialEditMode mode, bool hasSelection, bool editMode, string label = null)
        {
            if (button == null)
                return;

            bool active = hasSelection && editMode && PungentSpatialAuthoringEditorState.EditMode == mode;
            button.SetEnabled(hasSelection);
            if (!string.IsNullOrEmpty(label))
                button.text = label;
            StyleRailButton(button, active, hasSelection);
        }

        private static void StyleRailButton(Button button, bool active, bool enabled)
        {
            if (button == null)
                return;

            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            button.style.backgroundColor = active
                ? new StyleColor(new Color(0.10f, 0.42f, 0.52f, 0.95f))
                : enabled
                    ? new StyleColor(new Color(0.16f, 0.17f, 0.18f, 0.60f))
                    : new StyleColor(StyleKeyword.Null);
            button.style.color = active
                ? new StyleColor(Color.white)
                : new StyleColor(StyleKeyword.Null);
        }

        private void RefreshPreviewParameters()
        {
            _snapToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.SnapToSurface);
            _sharedSurfaceMaskField.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.SurfaceMask);
            _labelsToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.ShowLabels);
            _richPreviewToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.DrawRichPreview);
            _previewQualityField.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.PreviewQuality);
            _handleSizeField.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.HandleSize);
        }

        private void RefreshPathParameters(ModularPathSpawner path)
        {
            if (path == null)
                return;

            _pathExposeWidthToggle.SetValueWithoutNotify(path.exposePreviewCorridorWidth);
            _pathWidthField.SetValueWithoutNotify(path.previewCorridorWidth);
            _pathCorridorAlphaSlider.SetValueWithoutNotify(path.previewCorridorAlpha);
            _pathClosedToggle.SetValueWithoutNotify(path.closedLoop);
            _pathSamplingField.SetValueWithoutNotify(path.pathMode);
            _pathSamplesPerMeterField.SetValueWithoutNotify(path.samplesPerMeter);
            _pathMinSamplesPerSpanField.SetValueWithoutNotify(path.minSamplesPerSpan);
            _pathSurfaceConformToggle.SetValueWithoutNotify(path.conformToSurface);
            _pathAlignNormalToggle.SetValueWithoutNotify(path.alignToSurfaceNormal);
            _pathSampleEndsToggle.SetValueWithoutNotify(path.sampleSurfaceAtSegmentEnds);
            _pathSurfaceMaskField.SetValueWithoutNotify(path.surfaceMask);
            _pathRaycastHeightField.SetValueWithoutNotify(path.raycastStartHeight);
            _pathYOffsetField.SetValueWithoutNotify(path.yOffset);
            _pathPreviewToggle.SetValueWithoutNotify(path.previewWhileEditing);
            _pathAutoRebuildToggle.SetValueWithoutNotify(path.autoRebuildInEditor);
            _pathDrawGizmosToggle.SetValueWithoutNotify(path.drawGizmos);
            _pathDrawUnselectedGizmoToggle.SetValueWithoutNotify(path.drawUnselectedGizmo);
            _alignMinYField.SetValueWithoutNotify(EditorPrefs.GetFloat(PrefAlignMinY, -10000f));
            _alignMaxYField.SetValueWithoutNotify(EditorPrefs.GetFloat(PrefAlignMaxY, 10000f));
            _alignMinSlopeField.SetValueWithoutNotify(EditorPrefs.GetFloat(PrefAlignMinSlope, 0f));
            _alignMaxSlopeField.SetValueWithoutNotify(EditorPrefs.GetFloat(PrefAlignMaxSlope, 90f));
        }

        private void RefreshAreaParameters(PungentAreaAuthoringShape area)
        {
            if (area == null)
                return;

            _areaShapeField.SetValueWithoutNotify(area.shapeMode);
            SetShown(_areaSizeXField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ);
            SetShown(_areaSizeZField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ);
            SetShown(_areaRadiusField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.CircleXZ);
            _areaSizeXField.SetValueWithoutNotify(area.rectangleSize.x);
            _areaSizeZField.SetValueWithoutNotify(area.rectangleSize.y);
            _areaRadiusField.SetValueWithoutNotify(area.circleRadius);
            _areaVerticalModeField.SetValueWithoutNotify(area.verticalMode);
            SetShown(_areaHeightField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.HeightFromTransform);
            SetShown(_areaMinYField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax);
            SetShown(_areaMaxYField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax);
            _areaHeightField.SetValueWithoutNotify(area.height);
            _areaMinYField.SetValueWithoutNotify(area.minY);
            _areaMaxYField.SetValueWithoutNotify(area.maxY);
            _areaDrawGizmoToggle.SetValueWithoutNotify(area.drawGizmo);
            _areaDrawUnselectedGizmoToggle.SetValueWithoutNotify(area.drawUnselectedGizmo);
            _areaRuntimeQueryableToggle.SetValueWithoutNotify(area.runtimeQueryable);
        }

        private void RefreshSharedStatus(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            Component source = path != null ? (Component)path : area;
            if (_runtimeStatusLabel != null)
            {
                if (path != null)
                    _runtimeStatusLabel.text = "Runtime: path metadata, sampling, nearest-point projection, width, bounds, visualization, and generated-output provider contracts are active.";
                else if (area != null)
                    _runtimeStatusLabel.text = area.runtimeQueryable
                        ? "Runtime: area contains-point, bounds, volume, shape, and visualization providers are active."
                        : "Runtime: area provider metadata remains available; contains-point queries are disabled.";
                else
                    _runtimeStatusLabel.text = string.Empty;
            }

            if (_modularOutputStatusLabel != null)
            {
                int recipeCount = source != null ? source.GetComponents<PungentModularSpatialOutput>().Length : 0;
                int generatedChildren = 0;
                if (source != null)
                {
                    PungentModularSpatialOutput[] outputs = source.GetComponents<PungentModularSpatialOutput>();
                    for (int i = 0; i < outputs.Length; i++)
                        generatedChildren += outputs[i] != null ? outputs[i].ExistingGeneratedChildCount : 0;
                }
                PungentModularSpatialOutput selectedRecipe = PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(source);
                string preferred = selectedRecipe != null
                    ? "Selected: " + selectedRecipe.sourceMode + "."
                    : area != null
                        ? "Default mode: Area Boundary."
                        : "Default mode: Path Center; corridor side modes are available in the workbench.";
                _modularOutputStatusLabel.text = recipeCount + " Spatial Output Recipe(s), " + generatedChildren + " generated child object(s). " + preferred;
            }

            if (_diagnosticsStatusLabel != null)
            {
                if (path != null)
                {
                    string generated = path.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold
                        ? " High generated child count."
                        : string.Empty;
                    _diagnosticsStatusLabel.text = path.PointCount + " path point(s), " + path.ExistingGeneratedChildCount + " legacy generated child object(s)." + generated + " Open the workbench for full validation.";
                }
                else if (area != null)
                {
                    string points = area.PolygonPointCount >= PungentAreaAuthoringShapeSceneHandles.HighPolygonPointWarningThreshold
                        ? " High polygon point count."
                        : string.Empty;
                    _diagnosticsStatusLabel.text = area.PolygonPointCount + " area point(s), shape " + area.shapeMode + "." + points + " Open the workbench for full validation.";
                }
                else
                {
                    _diagnosticsStatusLabel.text = string.Empty;
                }
            }
        }

        private void ApplyAreaSize()
        {
            PungentAreaAuthoringShape area = ActiveArea();
            if (area == null)
                return;

            Undo.RecordObject(area, "Set Area Size");
            area.rectangleSize = new Vector2(Mathf.Max(0.01f, _areaSizeXField.value), Mathf.Max(0.01f, _areaSizeZField.value));
            MarkAreaChanged(area);
        }

        private void ApplyAreaMinMax()
        {
            PungentAreaAuthoringShape area = ActiveArea();
            if (area == null)
                return;

            Undo.RecordObject(area, "Set Area Min Max");
            area.minY = Mathf.Min(_areaMinYField.value, _areaMaxYField.value);
            area.maxY = Mathf.Max(_areaMinYField.value, _areaMaxYField.value);
            MarkAreaChanged(area);
        }

        private static ModularPathSpawner ActivePath()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            return PungentSpatialAuthoringEditorState.ActivePath;
        }

        private static PungentAreaAuthoringShape ActiveArea()
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            return PungentSpatialAuthoringEditorState.ActiveArea;
        }

        private static void MarkPathChanged(ModularPathSpawner path)
        {
            if (path == null)
                return;

            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }

        private static void MarkAreaChanged(PungentAreaAuthoringShape area)
        {
            if (area == null)
                return;

            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    internal static class PungentSpatialToolbarUtility
    {
        public static bool Resolve(out ModularPathSpawner path, out PungentAreaAuthoringShape area)
        {
            PungentSpatialAuthoringEditorState.SyncSelectionState();
            path = PungentSpatialAuthoringEditorState.ActivePath;
            area = PungentSpatialAuthoringEditorState.ActiveArea;
            return path != null || area != null;
        }

        public static bool CanEditPoints(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            return path != null || (area != null && area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ);
        }

        public static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void StyleActive(VisualElement element, bool active, bool enabled)
        {
            if (element == null)
                return;

            element.style.opacity = enabled ? 1f : 0.45f;
            element.style.backgroundColor = active
                ? new StyleColor(new Color(0.10f, 0.42f, 0.52f, 0.95f))
                : new StyleColor(StyleKeyword.Null);
            element.style.color = active
                ? new StyleColor(Color.white)
                : new StyleColor(StyleKeyword.Null);
        }

        public static void MarkPathChanged(ModularPathSpawner path)
        {
            if (path == null)
                return;

            EditorUtility.SetDirty(path);
            PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }

        public static void MarkAreaChanged(PungentAreaAuthoringShape area)
        {
            if (area == null)
                return;

            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }
    }

    [Overlay(typeof(SceneView), "PungentFunk/Spatial Tools", true)]
    internal sealed class PungentSpatialToolsOverlay : ToolbarOverlay
    {
        public PungentSpatialToolsOverlay()
            : base(
                PungentSpatialEditToolbarButton.Id,
                PungentSpatialViewToolbarButton.Id,
                PungentSpatialPlaceToolbarButton.Id,
                PungentSpatialMoveToolbarButton.Id,
                PungentSpatialDeleteToolbarButton.Id,
                PungentSpatialOutputToolbarButton.Id,
                PungentSpatialFrameToolbarButton.Id)
        {
        }
    }

    [Overlay(typeof(SceneView), "PungentFunk/Spatial Tool Settings", true)]
    internal sealed class PungentSpatialToolSettingsOverlay : ToolbarOverlay
    {
        public PungentSpatialToolSettingsOverlay()
            : base(
                PungentSpatialCommonToolSettings.Id,
                PungentSpatialPathToolSettings.Id,
                PungentSpatialAreaToolSettings.Id,
                PungentSpatialOutputToolSettings.Id)
        {
        }
    }

    internal abstract class PungentSpatialModeToolbarButton : EditorToolbarButton
    {
        private readonly PungentSpatialEditMode _mode;
        private readonly bool _togglesEditMode;
        private readonly bool _requiresPointEditing;

        protected PungentSpatialModeToolbarButton(string label, string tooltip, string iconName, PungentSpatialEditMode mode, bool togglesEditMode = false, bool requiresPointEditing = false)
        {
            _mode = mode;
            _togglesEditMode = togglesEditMode;
            _requiresPointEditing = requiresPointEditing;
            text = label;
            this.tooltip = tooltip;
            if (!string.IsNullOrEmpty(iconName))
            {
                GUIContent content = EditorGUIUtility.IconContent(iconName);
                if (content != null && content.image is Texture2D texture)
                {
                    icon = texture;
                    text = string.Empty;
                }
            }

            clicked += Execute;
            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        protected virtual bool IsVisible(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            return path != null || area != null;
        }

        protected virtual bool IsEnabled(ModularPathSpawner path, PungentAreaAuthoringShape area)
        {
            if (!IsVisible(path, area))
                return false;
            return !_requiresPointEditing || PungentSpatialToolbarUtility.CanEditPoints(path, area);
        }

        private void Execute()
        {
            if (!PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out PungentAreaAuthoringShape area) || !IsEnabled(path, area))
                return;

            if (_togglesEditMode)
                PungentSpatialAuthoringEditorState.SetEditModeActive(!PungentSpatialAuthoringEditorState.EditModeActive);
            else
                PungentSpatialAuthoringEditorState.SetEditMode(_mode, true);
        }

        private void Refresh()
        {
            PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out PungentAreaAuthoringShape area);
            bool visible = IsVisible(path, area);
            bool enabled = IsEnabled(path, area);
            bool active = enabled && PungentSpatialAuthoringEditorState.EditModeActive &&
                (_togglesEditMode || PungentSpatialAuthoringEditorState.EditMode == _mode);
            PungentSpatialToolbarUtility.SetShown(this, visible);
            SetEnabled(enabled);
            PungentSpatialToolbarUtility.StyleActive(this, active, enabled);
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialEditToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Edit";

        public PungentSpatialEditToolbarButton()
            : base("Edit", "Enter or exit Spatial Authoring edit mode.", "d_EditCollider", PungentSpatialEditMode.Move, togglesEditMode: true)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialViewToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/View";

        public PungentSpatialViewToolbarButton()
            : base("View", "View mode. Scene selection can pass through while Spatial Authoring stays active. Hotkey: V.", "ViewToolOrbit", PungentSpatialEditMode.View)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialPlaceToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Place";

        public PungentSpatialPlaceToolbarButton()
            : base("Place", "Place points contextually. Shift appends; Ctrl/Cmd inserts. Hotkey: A.", "d_Toolbar Plus", PungentSpatialEditMode.Add, requiresPointEditing: true)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialMoveToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Move";

        public PungentSpatialMoveToolbarButton()
            : base("Move", "Move points and shape handles. Hotkey: M.", "MoveTool", PungentSpatialEditMode.Move)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialDeleteToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Delete";

        public PungentSpatialDeleteToolbarButton()
            : base("Delete", "Delete editable points. Hotkey: D.", "TreeEditor.Trash", PungentSpatialEditMode.Delete, requiresPointEditing: true)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialOutputToolbarButton : PungentSpatialModeToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Output";

        public PungentSpatialOutputToolbarButton()
            : base("Output", "Preview and configure Spatial Output Recipes. Hotkey: O.", "d_Prefab Icon", PungentSpatialEditMode.Output)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialFrameToolbarButton : EditorToolbarButton
    {
        public const string Id = "PungentFunk/Spatial/Frame";

        public PungentSpatialFrameToolbarButton()
        {
            text = "Frame";
            tooltip = "Frame the selected spatial object in the Scene view.";
            GUIContent content = EditorGUIUtility.IconContent("d_FrameCapture");
            if (content != null && content.image is Texture2D texture)
            {
                icon = texture;
                text = string.Empty;
            }

            clicked += () =>
            {
                if (PungentSpatialToolbarUtility.Resolve(out _, out _))
                    PungentSpatialAuthoringActions.FrameActive();
            };
            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        private void Refresh()
        {
            bool hasSelection = PungentSpatialToolbarUtility.Resolve(out _, out _);
            PungentSpatialToolbarUtility.SetShown(this, hasSelection);
            SetEnabled(hasSelection);
            PungentSpatialToolbarUtility.StyleActive(this, false, hasSelection);
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialCommonToolSettings : VisualElement
    {
        public const string Id = "PungentFunk/Spatial/CommonSettings";
        private readonly Toggle _snapToggle;
        private readonly Toggle _labelsToggle;
        private readonly Toggle _richToggle;
        private readonly FloatField _handleSizeField;
        private readonly EnumField _qualityField;

        public PungentSpatialCommonToolSettings()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _snapToggle = MakeToggle("Snap", "Snap spatial editing to the active surface mask.");
            _snapToggle.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.SnapToSurface = evt.newValue);
            Add(_snapToggle);

            _labelsToggle = MakeToggle("Labels", "Show compact point and handle labels while editing.");
            _labelsToggle.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.ShowLabels = evt.newValue);
            Add(_labelsToggle);

            _richToggle = MakeToggle("Rich", "Show richer path corridors and area fills when the point count allows it.");
            _richToggle.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.DrawRichPreview = evt.newValue);
            Add(_richToggle);

            _qualityField = new EnumField(PungentSpatialPreviewQuality.Reduced) { tooltip = "Preview quality budget for Scene View drawing." };
            _qualityField.style.minWidth = 92f;
            _qualityField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is PungentSpatialPreviewQuality quality)
                    PungentSpatialAuthoringEditorState.PreviewQuality = quality;
            });
            Add(_qualityField);

            _handleSizeField = new FloatField("Handle") { tooltip = "Spatial handle size multiplier." };
            _handleSizeField.style.width = 84f;
            _handleSizeField.RegisterValueChangedCallback(evt => PungentSpatialAuthoringEditorState.HandleSize = evt.newValue);
            Add(_handleSizeField);

            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        private void Refresh()
        {
            bool hasSelection = PungentSpatialToolbarUtility.Resolve(out _, out _);
            PungentSpatialToolbarUtility.SetShown(this, hasSelection);
            _snapToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.SnapToSurface);
            _labelsToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.ShowLabels);
            _richToggle.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.DrawRichPreview);
            _qualityField.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.PreviewQuality);
            _handleSizeField.SetValueWithoutNotify(PungentSpatialAuthoringEditorState.HandleSize);
        }

        private static Toggle MakeToggle(string text, string tooltip)
        {
            Toggle toggle = new Toggle(text) { tooltip = tooltip };
            toggle.style.marginLeft = 0f;
            toggle.style.marginRight = 2f;
            return toggle;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialPathToolSettings : VisualElement
    {
        public const string Id = "PungentFunk/Spatial/PathSettings";
        private readonly Toggle _closedToggle;
        private readonly FloatField _widthField;
        private readonly Slider _alphaSlider;
        private readonly EnumField _samplingField;

        public PungentSpatialPathToolSettings()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _closedToggle = new Toggle("Loop") { tooltip = "Close the selected path loop." };
            _closedToggle.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out _) || path == null)
                    return;
                Undo.RecordObject(path, "Toggle Path Loop");
                path.closedLoop = evt.newValue;
                PungentSpatialToolbarUtility.MarkPathChanged(path);
            });
            Add(_closedToggle);

            _widthField = new FloatField("Width") { tooltip = "Preview corridor width used by handles and modular side output." };
            _widthField.style.width = 88f;
            _widthField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out _) || path == null)
                    return;
                Undo.RecordObject(path, "Set Corridor Width");
                path.exposePreviewCorridorWidth = true;
                path.previewCorridorWidth = Mathf.Max(0.01f, evt.newValue);
                PungentSpatialToolbarUtility.MarkPathChanged(path);
            });
            Add(_widthField);

            _alphaSlider = new Slider("Alpha", 0f, 1f) { tooltip = "Corridor preview transparency." };
            _alphaSlider.style.width = 124f;
            _alphaSlider.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out _) || path == null)
                    return;
                Undo.RecordObject(path, "Set Corridor Alpha");
                path.previewCorridorAlpha = Mathf.Clamp01(evt.newValue);
                PungentSpatialToolbarUtility.MarkPathChanged(path);
            });
            Add(_alphaSlider);

            _samplingField = new EnumField(ModularPathSpawner.PathMode.SmoothCatmullRom) { tooltip = "Path preview and sampling mode." };
            _samplingField.style.minWidth = 128f;
            _samplingField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out _) || path == null || !(evt.newValue is ModularPathSpawner.PathMode mode))
                    return;
                Undo.RecordObject(path, "Set Path Sampling");
                path.pathMode = mode;
                PungentSpatialToolbarUtility.MarkPathChanged(path);
            });
            Add(_samplingField);

            Add(MakeActionButton("Align Sel", () =>
            {
                PungentSpatialAuthoringActions.AlignActivePathToSurface(
                    selectedOnly: true,
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MinY", -10000f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MaxY", 10000f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MinSlope", 0f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MaxSlope", 90f));
            }, "Align the selected path point to the configured surface."));

            Add(MakeActionButton("Align All", () =>
            {
                PungentSpatialAuthoringActions.AlignActivePathToSurface(
                    selectedOnly: false,
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MinY", -10000f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MaxY", 10000f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MinSlope", 0f),
                    EditorPrefs.GetFloat("PungentFunkUtilities.SpatialAuthoring.Surface.MaxSlope", 90f));
            }, "Align all path points to the configured surface."));

            Add(MakeActionButton("Reverse", () => PungentSpatialAuthoringActions.ReverseActivePath(), "Reverse path point order and corridor side meaning."));

            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        private void Refresh()
        {
            PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out _);
            bool outputMode = PungentSpatialAuthoringEditorState.EditModeActive &&
                              PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output;
            bool hasPath = path != null && !outputMode;
            PungentSpatialToolbarUtility.SetShown(this, hasPath);
            if (!hasPath)
                return;

            _closedToggle.SetValueWithoutNotify(path.closedLoop);
            _widthField.SetValueWithoutNotify(path.previewCorridorWidth);
            _alphaSlider.SetValueWithoutNotify(path.previewCorridorAlpha);
            _samplingField.SetValueWithoutNotify(path.pathMode);
        }

        private static Button MakeActionButton(string label, Action clicked, string tooltip)
        {
            Button button = new Button(clicked) { text = label, tooltip = tooltip };
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialAreaToolSettings : VisualElement
    {
        public const string Id = "PungentFunk/Spatial/AreaSettings";
        private readonly EnumField _shapeField;
        private readonly FloatField _sizeXField;
        private readonly FloatField _sizeZField;
        private readonly FloatField _radiusField;
        private readonly EnumField _verticalField;
        private readonly FloatField _heightField;
        private readonly FloatField _minYField;
        private readonly FloatField _maxYField;

        public PungentSpatialAreaToolSettings()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _shapeField = new EnumField(PungentAreaAuthoringShape.ShapeMode.PolygonXZ) { tooltip = "Area shape mode." };
            _shapeField.style.minWidth = 116f;
            _shapeField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null || !(evt.newValue is PungentAreaAuthoringShape.ShapeMode shape))
                    return;
                Undo.RecordObject(area, "Set Area Shape");
                area.shapeMode = shape;
                PungentSpatialToolbarUtility.MarkAreaChanged(area);
            });
            Add(_shapeField);

            _sizeXField = MakeFloat("X", "Rectangle X size.");
            _sizeXField.RegisterValueChangedCallback(_ => ApplySize());
            Add(_sizeXField);

            _sizeZField = MakeFloat("Z", "Rectangle Z size.");
            _sizeZField.RegisterValueChangedCallback(_ => ApplySize());
            Add(_sizeZField);

            _radiusField = MakeFloat("Radius", "Circle radius.");
            _radiusField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null)
                    return;
                Undo.RecordObject(area, "Set Area Radius");
                area.circleRadius = Mathf.Max(0.01f, evt.newValue);
                PungentSpatialToolbarUtility.MarkAreaChanged(area);
            });
            Add(_radiusField);

            _verticalField = new EnumField(PungentAreaAuthoringShape.VerticalMode.HeightFromTransform) { tooltip = "Area vertical extent mode." };
            _verticalField.style.minWidth = 130f;
            _verticalField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null || !(evt.newValue is PungentAreaAuthoringShape.VerticalMode mode))
                    return;
                Undo.RecordObject(area, "Set Area Vertical Mode");
                area.verticalMode = mode;
                PungentSpatialToolbarUtility.MarkAreaChanged(area);
            });
            Add(_verticalField);

            _heightField = MakeFloat("Height", "Height above the area transform.");
            _heightField.RegisterValueChangedCallback(evt =>
            {
                if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null)
                    return;
                Undo.RecordObject(area, "Set Area Height");
                area.height = Mathf.Max(0.01f, evt.newValue);
                PungentSpatialToolbarUtility.MarkAreaChanged(area);
            });
            Add(_heightField);

            _minYField = MakeFloat("Min Y", "Explicit minimum world Y.");
            _minYField.RegisterValueChangedCallback(_ => ApplyMinMax());
            Add(_minYField);

            _maxYField = MakeFloat("Max Y", "Explicit maximum world Y.");
            _maxYField.RegisterValueChangedCallback(_ => ApplyMinMax());
            Add(_maxYField);

            Add(MakeActionButton("Align Sel", () => PungentSpatialAuthoringActions.AlignActiveAreaToSurface(selectedOnly: true), "Align the selected area point to the configured surface."));
            Add(MakeActionButton("Align All", () => PungentSpatialAuthoringActions.AlignActiveAreaToSurface(selectedOnly: false), "Align all polygon area points to the configured surface."));
            Add(MakeActionButton("Repair", () => PungentSpatialAuthoringActions.RepairActiveArea(), "Repair polygon self-intersections while preserving points."));
            Add(MakeActionButton("Reverse", () =>
            {
                PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area);
                string result = PungentSpatialAreaRepairUtility.ReverseWinding(area);
                PungentSpatialAuthoringEditorState.ReportAction(result);
            }, "Reverse area polygon winding."));

            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        private void Refresh()
        {
            PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area);
            bool outputMode = PungentSpatialAuthoringEditorState.EditModeActive &&
                              PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output;
            bool hasArea = area != null && !outputMode;
            PungentSpatialToolbarUtility.SetShown(this, hasArea);
            if (!hasArea)
                return;

            _shapeField.SetValueWithoutNotify(area.shapeMode);
            _sizeXField.SetValueWithoutNotify(area.rectangleSize.x);
            _sizeZField.SetValueWithoutNotify(area.rectangleSize.y);
            _radiusField.SetValueWithoutNotify(area.circleRadius);
            _verticalField.SetValueWithoutNotify(area.verticalMode);
            _heightField.SetValueWithoutNotify(area.height);
            _minYField.SetValueWithoutNotify(area.minY);
            _maxYField.SetValueWithoutNotify(area.maxY);

            PungentSpatialToolbarUtility.SetShown(_sizeXField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ);
            PungentSpatialToolbarUtility.SetShown(_sizeZField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ);
            PungentSpatialToolbarUtility.SetShown(_radiusField, area.shapeMode == PungentAreaAuthoringShape.ShapeMode.CircleXZ);
            PungentSpatialToolbarUtility.SetShown(_heightField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.HeightFromTransform);
            PungentSpatialToolbarUtility.SetShown(_minYField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax);
            PungentSpatialToolbarUtility.SetShown(_maxYField, area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax);
        }

        private void ApplySize()
        {
            if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null)
                return;

            Undo.RecordObject(area, "Set Area Size");
            area.rectangleSize = new Vector2(Mathf.Max(0.01f, _sizeXField.value), Mathf.Max(0.01f, _sizeZField.value));
            PungentSpatialToolbarUtility.MarkAreaChanged(area);
        }

        private void ApplyMinMax()
        {
            if (!PungentSpatialToolbarUtility.Resolve(out _, out PungentAreaAuthoringShape area) || area == null)
                return;

            Undo.RecordObject(area, "Set Area Min Max");
            area.minY = Mathf.Min(_minYField.value, _maxYField.value);
            area.maxY = Mathf.Max(_minYField.value, _maxYField.value);
            PungentSpatialToolbarUtility.MarkAreaChanged(area);
        }

        private static FloatField MakeFloat(string label, string tooltip)
        {
            FloatField field = new FloatField(label) { tooltip = tooltip };
            field.style.width = label.Length > 5 ? 92f : 68f;
            return field;
        }

        private static Button MakeActionButton(string label, Action clicked, string tooltip)
        {
            Button button = new Button(clicked) { text = label, tooltip = tooltip };
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class PungentSpatialOutputToolSettings : VisualElement
    {
        public const string Id = "PungentFunk/Spatial/OutputSettings";
        private readonly ObjectField _recipeField;
        private readonly EnumField _sourceModeField;
        private readonly FloatField _spacingField;
        private readonly Toggle _previewToggle;
        private readonly Button _newButton;
        private readonly Button _validateButton;
        private readonly Button _applyButton;
        private readonly Button _clearButton;
        private bool _refreshing;

        public PungentSpatialOutputToolSettings()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _newButton = MakeActionButton("New Recipe", () =>
            {
                PungentSpatialOutputRecipeEditorUtility.CreateRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource);
                Refresh();
            }, "Create a Spatial Output Recipe on the selected path or area.");
            Add(_newButton);

            _recipeField = new ObjectField("Recipe")
            {
                objectType = typeof(PungentModularSpatialOutput),
                allowSceneObjects = true,
                tooltip = "Selected Spatial Output Recipe."
            };
            _recipeField.style.width = 184f;
            _recipeField.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing)
                    return;
                PungentModularSpatialOutput recipe = evt.newValue as PungentModularSpatialOutput;
                if (RecipeBelongsToActiveSource(recipe))
                    PungentSpatialOutputRecipeEditorUtility.SelectRecipe(recipe);
                Refresh();
            });
            Add(_recipeField);

            _sourceModeField = new EnumField(PungentModularOutputSourceMode.PathCenter) { tooltip = "Lane source mode for the selected Spatial Output Recipe." };
            _sourceModeField.style.minWidth = 126f;
            _sourceModeField.RegisterValueChangedCallback(evt =>
            {
                PungentModularSpatialOutput recipe = CurrentRecipe();
                if (recipe == null || !(evt.newValue is PungentModularOutputSourceMode mode))
                    return;

                Undo.RecordObject(recipe, "Set Spatial Output Recipe Source Mode");
                recipe.sourceMode = mode;
                MarkRecipeChanged(recipe);
            });
            Add(_sourceModeField);

            _spacingField = new FloatField("Spacing") { tooltip = "Fixed spacing, or manual length fallback when prefab length is enabled." };
            _spacingField.style.width = 92f;
            _spacingField.RegisterValueChangedCallback(evt =>
            {
                PungentModularSpatialOutput recipe = CurrentRecipe();
                if (recipe == null)
                    return;

                Undo.RecordObject(recipe, "Set Spatial Output Recipe Spacing");
                if (recipe.usePrefabLength)
                    recipe.manualSegmentLength = Mathf.Max(0.01f, evt.newValue);
                else
                    recipe.fixedSpacing = Mathf.Max(0.01f, evt.newValue);
                MarkRecipeChanged(recipe);
            });
            Add(_spacingField);

            _previewToggle = new Toggle("Preview") { tooltip = "Draw recipe lane and gap previews while editing." };
            _previewToggle.RegisterValueChangedCallback(evt =>
            {
                PungentModularSpatialOutput recipe = CurrentRecipe();
                if (recipe == null)
                    return;

                Undo.RecordObject(recipe, "Toggle Spatial Output Recipe Preview");
                recipe.previewWhileEditing = evt.newValue;
                recipe.drawGizmos = evt.newValue || recipe.drawGizmos;
                MarkRecipeChanged(recipe);
            });
            Add(_previewToggle);

            _validateButton = MakeActionButton("Validate", () =>
            {
                string result = PungentSpatialOutputRecipeEditorUtility.ValidateRecipe(CurrentRecipe());
                PungentSpatialAuthoringEditorState.ReportAction(result);
            }, "Validate the selected recipe lane.");
            Add(_validateButton);

            _applyButton = MakeActionButton("Apply", () => PungentSpatialOutputRecipeEditorUtility.ApplyRecipe(CurrentRecipe()), "Apply the selected Spatial Output Recipe.");
            Add(_applyButton);

            _clearButton = MakeActionButton("Clear", () => PungentSpatialOutputRecipeEditorUtility.ClearRecipe(CurrentRecipe()), "Clear generated objects for the selected recipe.");
            Add(_clearButton);

            PungentSpatialOverlayRefreshBinding.Bind(this, Refresh);
        }

        private void Refresh()
        {
            bool outputMode = PungentSpatialToolbarUtility.Resolve(out ModularPathSpawner path, out PungentAreaAuthoringShape area) &&
                              PungentSpatialAuthoringEditorState.EditModeActive &&
                              PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output;
            PungentSpatialToolbarUtility.SetShown(this, outputMode);
            if (!outputMode)
                return;

            Component source = path != null ? (Component)path : area;
            PungentModularSpatialOutput recipe = PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(source);
            _refreshing = true;
            _recipeField.SetValueWithoutNotify(recipe);
            if (recipe != null)
            {
                _sourceModeField.SetValueWithoutNotify(recipe.sourceMode);
                _spacingField.label = recipe.usePrefabLength ? "Length" : "Spacing";
                _spacingField.SetValueWithoutNotify(recipe.usePrefabLength ? recipe.manualSegmentLength : recipe.fixedSpacing);
                _previewToggle.SetValueWithoutNotify(recipe.previewWhileEditing);
            }
            _refreshing = false;

            bool hasRecipe = recipe != null;
            _recipeField.SetEnabled(PungentSpatialOutputRecipeEditorUtility.GetRecipes(source).Length > 0);
            _sourceModeField.SetEnabled(hasRecipe);
            _spacingField.SetEnabled(hasRecipe);
            _previewToggle.SetEnabled(hasRecipe);
            _validateButton.SetEnabled(hasRecipe);
            _applyButton.SetEnabled(hasRecipe);
            _clearButton.SetEnabled(hasRecipe && recipe.ExistingGeneratedChildCount > 0);
        }

        private static PungentModularSpatialOutput CurrentRecipe()
        {
            return PungentSpatialOutputRecipeEditorUtility.GetSelectedRecipe(PungentSpatialOutputRecipeEditorUtility.ActiveSource);
        }

        private static bool RecipeBelongsToActiveSource(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return true;

            Component source = PungentSpatialOutputRecipeEditorUtility.ActiveSource;
            if (source == null)
                return false;

            PungentModularSpatialOutput[] recipes = PungentSpatialOutputRecipeEditorUtility.GetRecipes(source);
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] == recipe)
                    return true;
            }

            return false;
        }

        private static void MarkRecipeChanged(PungentModularSpatialOutput recipe)
        {
            EditorUtility.SetDirty(recipe);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            if (recipe.autoRebuildInEditor)
                recipe.Rebuild();
        }

        private static Button MakeActionButton(string label, Action clicked, string tooltip)
        {
            Button button = new Button(clicked) { text = label, tooltip = tooltip };
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }
    }

    [UnityEditor.EditorTools.EditorTool("Spatial Authoring Path", typeof(ModularPathSpawner))]
    internal sealed class PungentSpatialPathEditorTool : UnityEditor.EditorTools.EditorTool
    {
        private readonly GUIContent _icon = new GUIContent("Spatial", "Spatial Authoring Path Edit Tool");
        private int _selectedIndex = -1;
        private bool _pendingRebuildAfterDrag;
        private double _nextAllowedSceneRepaintTime;
        public override GUIContent toolbarIcon => _icon;

        public override void OnActivated()
        {
            PungentSpatialAuthoringEditorState.NotifyEditorToolActivated(PungentSpatialEditMode.Move);
        }

        public override void OnWillBeDeactivated()
        {
            PungentSpatialAuthoringEditorState.NotifyEditorToolDeactivated();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            ModularPathSpawner path = PungentSpatialAuthoringEditorState.ActivePath;
            if (path == null)
                return;

            PungentSpatialPathSceneHandles.Draw(
                path,
                ref _selectedIndex,
                ref _pendingRebuildAfterDrag,
                PungentSpatialAuthoringEditorState.IsEditingPath(path),
                window as SceneView,
                ref _nextAllowedSceneRepaintTime);

            if (PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output)
                PungentSpatialOutputRecipeEditorUtility.DrawScenePreviewForActiveSource();
        }
    }

    [UnityEditor.EditorTools.EditorTool("Spatial Authoring Area", typeof(PungentAreaAuthoringShape))]
    internal sealed class PungentSpatialAreaEditorTool : UnityEditor.EditorTools.EditorTool
    {
        private readonly GUIContent _icon = new GUIContent("Spatial", "Spatial Authoring Area Edit Tool");
        private int _selectedPoint = -1;
        private double _nextAllowedSceneRepaintTime;
        private double _nextAllowedWindowRepaintTime;
        public override GUIContent toolbarIcon => _icon;

        public override void OnActivated()
        {
            PungentSpatialAuthoringEditorState.NotifyEditorToolActivated(PungentSpatialEditMode.Move);
        }

        public override void OnWillBeDeactivated()
        {
            PungentSpatialAuthoringEditorState.NotifyEditorToolDeactivated();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            PungentAreaAuthoringShape area = PungentSpatialAuthoringEditorState.ActiveArea;
            if (area == null || !area.drawGizmo)
                return;

            PungentSpatialSceneHandleUtility.BeginSceneGUI();
            PungentSpatialSceneHandleUtility.ProtectSelectionIfNeeded(
                PungentSpatialAuthoringEditorState.IsEditingArea(area) &&
                PungentSpatialAuthoringEditorState.ShouldCaptureSceneInput);

            PungentAreaAuthoringShapeSceneHandles.Draw(
                area,
                ref _selectedPoint,
                PungentSpatialAuthoringEditorState.SnapToSurface,
                PungentSpatialAuthoringEditorState.SurfaceMask,
                PungentSpatialAuthoringEditorState.ShowLabels,
                PungentSpatialAuthoringEditorState.HandleSize,
                PungentSpatialAuthoringEditorState.DrawRichPreview,
                PungentSpatialAuthoringEditorState.IsEditingArea(area),
                window as SceneView,
                ownerWindow: null,
                ref _nextAllowedSceneRepaintTime,
                ref _nextAllowedWindowRepaintTime);

            if (PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Output)
                PungentSpatialOutputRecipeEditorUtility.DrawScenePreviewForActiveSource();
        }
    }
#endif

    public static class PungentPathAuthoringToolkit
    {
        public delegate Vector3 PointPostProcessor(Vector3 point);

        public static float CalculateLength(IList<Vector3> points)
        {
            if (points == null || points.Count < 2)
                return 0f;
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        public static float CalculateLength(IPungentPathPointProvider provider)
        {
            return CalculateLength(PungentPathAuthoringAdapterUtility.CopyWorldPoints(provider));
        }

        public static bool TryAppendPathProviderPoints(Transform root, IList<Vector3> destination, PointPostProcessor postProcessor, out int appended)
        {
            appended = 0;
            if (root == null || destination == null)
                return false;

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IPungentPathPointProvider provider))
                    continue;

                int count = Mathf.Max(0, provider.PointCount);
                for (int p = 0; p < count; p++)
                {
                    Vector3 point = provider.GetWorldPoint(p);
                    destination.Add(postProcessor != null ? postProcessor(point) : point);
                    appended++;
                }

                return appended > 0;
            }

            return false;
        }

        public static bool HasNearDuplicatePoints(IList<Vector3> points, float threshold)
        {
            if (points == null)
                return false;
            float sqr = threshold * threshold;
            for (int i = 1; i < points.Count; i++)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude <= sqr)
                    return true;
            }
            return false;
        }

        public static Vector3 Sample(IList<Vector3> points, float normalizedDistance)
        {
            if (points == null || points.Count == 0)
                return Vector3.zero;
            if (points.Count == 1)
                return points[0];

            float total = CalculateLength(points);
            if (total <= 0.0001f)
                return points[0];

            float target = Mathf.Clamp01(normalizedDistance) * total;
            float travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float segment = Vector3.Distance(points[i - 1], points[i]);
                if (travelled + segment >= target)
                {
                    float t = segment <= 0.0001f ? 0f : (target - travelled) / segment;
                    return Vector3.Lerp(points[i - 1], points[i], t);
                }
                travelled += segment;
            }
            return points[points.Count - 1];
        }

        public static int FindNearestPointIndex(IList<Vector3> points, Vector3 position)
        {
            if (points == null || points.Count == 0)
                return -1;
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = (points[i] - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static int FindNearestSegmentIndex(IList<Vector3> points, Vector3 position)
        {
            if (points == null || points.Count < 2)
                return Mathf.Max(0, (points?.Count ?? 1) - 1);
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 nearest = ClosestPointOnSegment(points[i], points[i + 1], position);
                float distance = (nearest - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float denominator = Vector3.Dot(ab, ab);
            if (denominator <= 0.0001f)
                return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denominator);
            return Vector3.Lerp(a, b, t);
        }

        public static void DrawWidthPreview(IList<Vector3> points, float width, Color color)
        {
            if (points == null || points.Count < 2)
                return;

            Color previous = Handles.color;
            Handles.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a));
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * width * 0.5f;
                if (right.sqrMagnitude < 0.0001f)
                    right = Vector3.right * width * 0.5f;
                Handles.DrawAAPolyLine(1.5f, a + right, b + right);
                Handles.DrawAAPolyLine(1.5f, a - right, b - right);
            }
            Handles.color = previous;
        }

        public static void DrawProviderPreview(IPungentPathPointProvider pointProvider, IPungentCorridorWidthProvider widthProvider, Color lineColor, Color widthColor)
        {
            List<Vector3> points = PungentPathAuthoringAdapterUtility.CopyWorldPoints(pointProvider);
            if (points.Count < 2)
                return;

            Color previous = Handles.color;
            Handles.color = lineColor;
            Handles.DrawAAPolyLine(3f, points.ToArray());

            if (widthProvider != null && widthProvider.TryGetWidthAt(0.5f, out float width))
                DrawWidthPreview(points, width, widthColor);

            Handles.color = previous;
        }

        // TODO: External projects can bridge their own path, checkpoint, corridor, and exclusion systems
        // by implementing the lightweight runtime interfaces in PungentPathAuthoringAdapters.cs outside the package.
    }
    #endif

}
