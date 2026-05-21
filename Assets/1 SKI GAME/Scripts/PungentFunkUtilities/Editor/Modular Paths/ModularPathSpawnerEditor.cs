using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Scene editing for ModularPathSpawner and legacy segment-path compatibility components.
    ///
    /// Controls:
    /// - Shift + Left Click: add point at surface/plane hit
    /// - Ctrl + Left Click: insert point into nearest segment
    /// - Click point: select point
    /// - Drag selected point handle: move point
    /// - Delete/Backspace: remove selected point
    /// </summary>
    [CustomEditor(typeof(ModularPathSpawner), true)]
    public class ModularPathSpawnerEditor : Editor
    {
        private enum PathInspectorTab { Geometry, Surface, Output, Diagnostics, Advanced }

        private ModularPathSpawner _path;
        private PathInspectorTab _selectedTab = PathInspectorTab.Geometry;
        private int _selectedIndex = -1;
        private double _nextAllowedSceneRepaintTime;
        private double _nextAllowedWindowRepaintTime;
        private bool _pendingRebuildAfterDrag;
        private int _lastClearHandleSelectionVersion = -1;
        private bool _identityFoldout = true;
        private bool _geometryFoldout = true;
        private bool _advancedGeometryFoldout = false;
        private bool _previewFoldout = false;
        private bool _surfaceFoldout = false;
        private bool _outputFoldout = true;
        private bool _diagnosticsFoldout = true;
        private bool _advancedFoldout = false;
        private bool _legacyOutputFoldout = false;
        private string _surfaceActionStatus = string.Empty;

        private void OnEnable()
        {
            _path = target as ModularPathSpawner;
            SceneView.duringSceneGui += DuringSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        public override void OnInspectorGUI()
        {
            if (_path == null)
                _path = target as ModularPathSpawner;
            if (_path == null)
                return;

            serializedObject.Update();
            DrawInspectorHeader();
            DrawInspectorTabs();

            EditorGUI.BeginChangeCheck();
            switch (_selectedTab)
            {
                case PathInspectorTab.Geometry:
                    DrawIdentitySection();
                    DrawGeometrySection();
                    break;
                case PathInspectorTab.Surface:
                    DrawSurfaceSection();
                    break;
                case PathInspectorTab.Output:
                    DrawOutputSection();
                    break;
                case PathInspectorTab.Diagnostics:
                    DrawDiagnosticsSection();
                    break;
                case PathInspectorTab.Advanced:
                    DrawPreviewSection();
                    DrawAdvancedSection();
                    DrawLegacyBuiltInOutputSection();
                    break;
            }
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
                ApplyInspectorChanges();
        }

        private void DrawInspectorTabs()
        {
            EditorGUILayout.Space(3f);
            string[] labels = { "Geometry", "Surface", "Output", "Diagnostics", "Advanced" };
            _selectedTab = (PathInspectorTab)GUILayout.Toolbar((int)_selectedTab, labels, GUILayout.Height(26f));
            EditorGUILayout.Space(3f);
        }

        private void DrawInspectorHeader()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Spatial Path", UtilityWindowTheme.Blue, _path.SpatialDisplayName + " / " + _path.PointCount + " point(s)");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton(PungentSpatialAuthoringEditorState.IsEditingPath(_path) ? "Exit Scene Edit" : "Edit In Scene", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                        PungentSpatialAuthoringEditorState.ToggleEditModeFor(_path);
                    if (GUILayout.Button("Frame", GUILayout.Height(28f), GUILayout.Width(72f)))
                        PungentSpatialAuthoringActions.FrameSpatialObject(_path);
                    if (GUILayout.Button("Open Toolkit", GUILayout.Height(28f), GUILayout.Width(116f)))
                        PungentPathAuthoringToolkitWindow.OpenWithPath(_path);
                }

                EditorGUILayout.LabelField(PungentSpatialAuthoringEditorState.BuildNextActionHint(), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawIdentitySection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _identityFoldout = EditorGUILayout.Foldout(_identityFoldout, "Core Setup", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_identityFoldout)
                    return;

                DrawProperty("displayName", "Display Name", "Name shown in overlays, scene lists, and future exports.");
                DrawProperty("categories", "Categories / Tags", "Generic category labels. Keep project-specific meaning outside this package.");
                DrawProperty("pathColor", "Path Color", "Scene preview color for this path.");
            }
        }

        private void DrawGeometrySection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _geometryFoldout = EditorGUILayout.Foldout(_geometryFoldout, "Geometry", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_geometryFoldout)
                    return;

                DrawProperty("closedLoop", "Closed Loop", "Connect the last control point back to the first.");
                DrawProperty("pathMode", "Sampling", "Polyline or smooth Catmull-Rom sampling.");
                DrawProperty("exposePreviewCorridorWidth", "Use Corridor Width", "Expose an authoring corridor for previews and corridor-side output recipes.");
                if (_path.exposePreviewCorridorWidth)
                {
                    DrawProperty("previewCorridorWidth", "Corridor Width", "Authoring corridor width used by previews and generic output recipes.");
                    DrawProperty("previewCorridorAlpha", "Corridor Alpha", "Transparency of the corridor preview ribbon.");
                }
                DrawProperty("pointTangentBlend", "Point Tangent Blend", "How point prefabs face between incoming and outgoing directions.");

                _advancedGeometryFoldout = EditorGUILayout.Foldout(_advancedGeometryFoldout, "Advanced Geometry", true);
                if (_advancedGeometryFoldout)
                {
                    DrawProperty("samplesPerMeter", "Samples / Meter", "Smooth path sampling density.");
                    DrawProperty("minSamplesPerSpan", "Min Samples / Span", "Minimum samples per control-point span.");
                    DrawProperty("localPoints", "Local Control Points", "Raw local-space point array. Prefer Scene View editing for normal authoring.");
                }
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, "Preview", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_previewFoldout)
                    return;

                DrawProperty("previewWhileEditing", "Preview While Editing", "Draw lightweight path previews while editing.");
                DrawProperty("drawGizmos", "Draw Gizmos", "Draw selected path gizmos and previews.");
                DrawProperty("drawUnselectedGizmo", "Draw Unselected", "Draw lightweight gizmos when this path is not selected.");
            }
        }

        private void DrawSurfaceSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                _surfaceFoldout = EditorGUILayout.Foldout(_surfaceFoldout, "Surface", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_surfaceFoldout)
                    return;

                DrawProperty("conformToSurface", "Surface Conform", "Raycast generated output to the configured surface mask.");
                DrawProperty("surfaceMask", "Surface Mask", "Layer mask used for surface sampling.");
                DrawProperty("raycastStartHeight", "Raycast Height", "Height above each point used when raycasting downward.");
                DrawProperty("yOffset", "Y Offset", "Vertical offset applied after surface sampling.");
                DrawProperty("alignToSurfaceNormal", "Align To Normal", "Use sampled surface normal as generated-object up.");
                DrawProperty("sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both segment endpoints before placement.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Align Selected Point", GUILayout.Height(26f)))
                        RunPathSurfaceAction(selectedOnly: true);
                    if (GUILayout.Button("Align All Points", GUILayout.Height(26f)))
                        RunPathSurfaceAction(selectedOnly: false);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reverse Direction", GUILayout.Height(26f)))
                    {
                        Selection.activeGameObject = _path.gameObject;
                        PungentSpatialAuthoringEditorState.RefreshSelection();
                        _surfaceActionStatus = PungentSpatialAuthoringActions.ReverseActivePath();
                        Repaint();
                    }
                }

                if (!string.IsNullOrWhiteSpace(_surfaceActionStatus))
                    EditorGUILayout.HelpBox(_surfaceActionStatus, MessageType.Info);
            }
        }

        private void DrawOutputSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                _outputFoldout = EditorGUILayout.Foldout(_outputFoldout, "Output", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_outputFoldout)
                    return;

                UtilityWindowTheme.SectionTitle("Spatial Output Recipes", UtilityWindowTheme.Teal, "Preferred for new path, corridor, and area-boundary generation");
                PungentModularSpatialOutput[] recipes = _path.GetComponents<PungentModularSpatialOutput>();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton(recipes.Length == 0 ? "Add Spatial Output Recipe" : "Add Another Recipe", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                        PungentSpatialOutputRecipeEditorUtility.CreateRecipe(_path);
                    UtilityWindowTheme.CountPill("Recipes " + recipes.Length, recipes.Length > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 94f);
                }

            }
        }

        private void DrawLegacyBuiltInOutputSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                _legacyOutputFoldout = EditorGUILayout.Foldout(_legacyOutputFoldout, "Legacy Built-In Output", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_legacyOutputFoldout)
                {
                    EditorGUILayout.LabelField("Preserved for existing ModularPathSpawner scenes. Prefer Spatial Output Recipes for new work.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                DrawProperty("segmentPrefab", "Segment Prefab", "Prefab repeated along the path centerline.");
                DrawProperty("pointPrefab", "Point Prefab", "Optional prefab placed at each control point.");
                DrawProperty("useSockets", "Use Sockets", "Use named child transforms to align segment joins.");
                DrawProperty("startSocketName", "Start Socket", "Child transform name marking the segment start.");
                DrawProperty("endSocketName", "End Socket", "Child transform name marking the segment end.");
                DrawProperty("placeByEndpointsWhenUsingSockets", "Place By Endpoints", "Map socket positions to sampled path endpoints.");
                DrawProperty("usePrefabLength", "Use Prefab Length", "Estimate spacing from prefab bounds.");
                DrawProperty(_path.usePrefabLength ? "manualSegmentLength" : "fixedSpacing", _path.usePrefabLength ? "Manual Length Fallback" : "Fixed Spacing", "Segment spacing fallback.");
                DrawProperty("startOffset", "Start Offset", "Distance offset before the first generated segment.");
                DrawProperty("autoRebuildInEditor", "Auto Rebuild", "Queue a debounced generated-object rebuild after editor changes.");
                if (_path.autoRebuildInEditor)
                    DrawProperty("editorRebuildDebounce", "Rebuild Debounce", "Delay before automatic generated-object rebuild.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Apply Legacy Output", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                        PungentPathRebuildScheduler.RebuildNow(_path, null, "Apply Path Preview To Generated Objects");
                    using (new EditorGUI.DisabledScope(_path.ExistingGeneratedChildCount == 0))
                    {
                        if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(78f)))
                        {
                            Undo.RecordObject(_path, "Clear Generated Path Objects");
                            _path.ClearGenerated();
                            EditorUtility.SetDirty(_path);
                            PungentPathRebuildScheduler.RequestPreviewRefresh(_path);
                        }
                    }
                }
            }
        }

        private void DrawAdvancedSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "Advanced", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_advancedFoldout)
                    return;

                DrawProperty("stablePathId", "Stable ID", "Stable generic identifier used by optional bridges and exports.");
                EditorGUILayout.LabelField("Legacy compatibility aliases remain serialized-safe on the runtime component.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawDiagnosticsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                _diagnosticsFoldout = EditorGUILayout.Foldout(_diagnosticsFoldout, "Diagnostics", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_diagnosticsFoldout)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Points " + _path.PointCount, UtilityWindowTheme.Neutral, 92f);
                    UtilityWindowTheme.CountPill("Generated " + _path.ExistingGeneratedChildCount, _path.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 118f);
                    GUILayout.FlexibleSpace();
                }

                DrawPerformanceWarnings();
            }
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void RunPathSurfaceAction(bool selectedOnly)
        {
            PungentSpatialSurfaceAuthoringSettings settings = PungentSpatialSurfaceAuthoringSettings.Load();
            settings.SurfaceMask = _path.surfaceMask;
            settings.RaycastHeight = _path.raycastStartHeight;
            settings.YOffset = _path.yOffset;
            _surfaceActionStatus = PungentSpatialSurfaceAuthoringUtility.AlignPathPoints(_path, selectedOnly, _selectedIndex, settings);
            PungentSpatialAuthoringEditorState.ReportAction(_surfaceActionStatus);
            Repaint();
        }

        private void ApplyInspectorChanges()
        {
            if (_path == null)
                return;

            EditorUtility.SetDirty(_path);
            if (_path.autoRebuildInEditor)
                PungentPathRebuildScheduler.QueueRebuild(_path, null, "Queue Modular Path Rebuild");
            else
                PungentPathRebuildScheduler.RequestPreviewRefresh(_path);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedSceneRepaintTime, 0.08d) && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedWindowRepaintTime, 0.10d))
                Repaint();
        }

        private void DuringSceneGui(SceneView view)
        {
            if (_path == null)
                return;

            PungentSpatialAuthoringEditorState.EnsureInitialized();
            if (!PungentSpatialAuthoringEditorState.ShouldDrawPath(_path))
                return;
            if (PungentSpatialAuthoringEditorState.IsPathEditorToolActive(_path))
                return;

            bool editing = PungentSpatialAuthoringEditorState.IsEditingPath(_path);
            PungentSpatialSceneHandleUtility.BeginSceneGUI();
            if (editing && PungentSpatialAuthoringEditorState.ShouldCaptureSceneInput)
                PungentSpatialSceneHandleUtility.ProtectSelectionIfNeeded(true);

            if (_lastClearHandleSelectionVersion != PungentSpatialAuthoringEditorState.ClearHandleSelectionVersion)
            {
                _lastClearHandleSelectionVersion = PungentSpatialAuthoringEditorState.ClearHandleSelectionVersion;
                _selectedIndex = -1;
            }

            PungentSpatialPathSceneHandles.Draw(
                _path,
                ref _selectedIndex,
                ref _pendingRebuildAfterDrag,
                editing,
                view,
                ref _nextAllowedSceneRepaintTime);
        }

        private void DrawPolyline()
        {
            int n = _path.PointCount;
            if (n < 2)
                return;

            Handles.color = new Color(0.7f, 0.95f, 1f, 0.8f);

            bool minimal = PungentSpatialAuthoringEditorState.PreviewQuality == PungentSpatialPreviewQuality.Minimal ||
                           _path.PointCount >= ModularPathSpawner.HighSampledPointWarningThreshold / 2;

            if (minimal)
            {
                for (int i = 0; i < n - 1; i++)
                    Handles.DrawLine(_path.GetWorldPoint(i), _path.GetWorldPoint(i + 1));

                if (_path.closedLoop && n > 2)
                    Handles.DrawLine(_path.GetWorldPoint(n - 1), _path.GetWorldPoint(0));
                return;
            }

            if (_path.BuildSampledWorldPath(out var sampled, out bool sampledClosed))
            {
                for (int i = 0; i < sampled.Count - 1; i++)
                    Handles.DrawLine(sampled[i], sampled[i + 1]);

                if (sampledClosed && sampled.Count > 2)
                    Handles.DrawLine(sampled[sampled.Count - 1], sampled[0]);
            }
        }

        private void DrawPointHandles(bool editing)
        {
            int n = _path.PointCount;
            int hoveredIndex = -1;
            for (int i = 0; i < n; i++)
            {
                Vector3 wp = _path.GetWorldPoint(i);
                float size = HandleUtility.GetHandleSize(wp) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.08f;
                float pickSize = Mathf.Max(size * 2.6f, HandleUtility.GetHandleSize(wp) * 0.04f);
                bool isSelected = i == _selectedIndex;
                int controlId = PungentSpatialSceneHandleUtility.GetPointControlId(_path, i);
                bool hovered = PungentSpatialSceneHandleUtility.RegisterPointControl(controlId, wp, pickSize);
                if (hovered)
                    hoveredIndex = i;

                if (editing &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Delete &&
                    Event.current != null &&
                    Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 &&
                    HandleUtility.nearestControl == controlId &&
                    !PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(Event.current))
                {
                    Undo.RecordObject(_path, "Delete Path Point");
                    _path.RemovePoint(i);
                    _selectedIndex = Mathf.Clamp(i - 1, -1, _path.PointCount - 1);
                    EditorUtility.SetDirty(_path);
                    QueueRebuild();
                    Event.current.Use();
                    PungentSpatialAuthoringEditorState.SetHandleSelection("Path", _selectedIndex, -1, _path.PointCount);
                    return;
                }

                if (editing && PungentSpatialSceneHandleUtility.TryBeginPointDrag(controlId, Event.current))
                {
                    _selectedIndex = i;
                    if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedWindowRepaintTime, 0.10d))
                        Repaint();
                }
                if (editing &&
                    isSelected &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Move &&
                    PungentSpatialSceneHandleUtility.IsDraggingPoint(controlId, Event.current) &&
                    TryMouseToWorld(Event.current.mousePosition, out Vector3 newPos))
                {
                    Undo.RecordObject(_path, "Move Path Point");
                    _path.SetWorldPoint(i, newPos);
                    EditorUtility.SetDirty(_path);
                    _pendingRebuildAfterDrag = true;
                    PungentPathRebuildScheduler.RequestPreviewRefresh(_path, null);
                    Event.current.Use();
                }

                PungentSpatialSceneHandleUtility.TryEndPointDrag(controlId, Event.current);

                Color color = isSelected
                    ? new Color(0.2f, 1f, 0.45f, 1f)
                    : hovered
                        ? new Color(1f, 0.95f, 0.42f, 1f)
                        : new Color(1f, 0.82f, 0.28f, 0.95f);
                PungentSpatialSceneHandleUtility.DrawPointCap(controlId, wp, hovered ? size * 1.25f : size, color);

                if (PungentSpatialAuthoringEditorState.ShouldDrawPointLabel(isSelected))
                    Handles.Label(wp + Vector3.up * (size * 6f), $"Point {i}");
            }

            PungentSpatialAuthoringEditorState.SetHandleSelection("Path", _selectedIndex, hoveredIndex, n);
        }

        private void HandleMouseAddInsert()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0)
                return;

            bool add = PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Add ||
                       (e.shift && !e.control && !e.alt);
            bool insert = PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Insert ||
                          PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Split ||
                          (e.control && !e.shift && !e.alt);
            if (!add && !insert)
                return;

            if (PungentSpatialSceneHandleUtility.IsNearestSpatialControl())
                return;

            if (!TryMouseToWorld(e.mousePosition, out Vector3 worldHit))
                return;

            Undo.RecordObject(_path, add ? "Add Path Point" : "Insert Path Point");

            if (add)
            {
                _path.AddWorldPoint(worldHit);
                _selectedIndex = _path.PointCount - 1;
            }
            else
            {
                int insertIndex = FindInsertIndex(worldHit);
                _path.InsertWorldPoint(insertIndex, worldHit);
                _selectedIndex = insertIndex;
            }

            EditorUtility.SetDirty(_path);
            QueueRebuild();
            e.Use();
        }

        private void HandleMouseDelete()
        {
            if (PungentSpatialAuthoringEditorState.EditMode != PungentSpatialEditMode.Delete)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || e.alt)
                return;

            if (PungentSpatialSceneHandleUtility.IsNearestSpatialControl())
                return;

            if (!TryMouseToWorld(e.mousePosition, out Vector3 worldHit))
                return;

            int nearest = FindNearestPointIndex(worldHit);
            if (nearest < 0)
                return;

            Undo.RecordObject(_path, "Delete Path Point");
            _path.RemovePoint(nearest);
            _selectedIndex = Mathf.Clamp(nearest - 1, -1, _path.PointCount - 1);
            EditorUtility.SetDirty(_path);
            QueueRebuild();
            e.Use();
        }

        private int FindInsertIndex(Vector3 worldPoint)
        {
            int n = _path.PointCount;
            if (n < 2)
                return n;

            float bestSqr = float.PositiveInfinity;
            int bestSegmentStart = n - 1;
            int segCount = _path.closedLoop ? n : n - 1;

            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = _path.GetWorldPoint(i);
                Vector3 b = _path.GetWorldPoint(j);
                float sqr = SqrDistancePointToSegment(worldPoint, a, b);

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestSegmentStart = i;
                }
            }

            return bestSegmentStart + 1;
        }

        private int FindNearestPointIndex(Vector3 worldPoint)
        {
            int n = _path != null ? _path.PointCount : 0;
            if (n <= 0)
                return -1;

            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                float sqr = (_path.GetWorldPoint(i) - worldPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static float SqrDistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1e-6f)
                return (p - a).sqrMagnitude;

            float t = Vector3.Dot(p - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            Vector3 projection = a + ab * t;
            return (p - projection).sqrMagnitude;
        }

        private void HandleDeleteKey()
        {
            if (_path == null || _selectedIndex < 0 || _selectedIndex >= _path.PointCount)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            if (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace)
                return;

            Undo.RecordObject(_path, "Delete Path Point");
            _path.RemovePoint(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex - 1, -1, _path.PointCount - 1);
            EditorUtility.SetDirty(_path);
            QueueRebuild();
            e.Use();
        }

        private bool TryMouseToWorld(Vector2 mousePosition, out Vector3 worldHit)
        {
            worldHit = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, _path.surfaceMask, QueryTriggerInteraction.Ignore))
            {
                worldHit = hit.point;
                return true;
            }

            Plane plane = new Plane(Vector3.up, _path.transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                worldHit = ray.GetPoint(enter);
                return true;
            }

            return false;
        }

        private void SnapAllPointsToSurface()
        {
            int n = _path.PointCount;
            if (n <= 0)
                return;

            float height = Mathf.Max(0.01f, _path.raycastStartHeight);
            for (int i = 0; i < n; i++)
            {
                Vector3 wp = _path.GetWorldPoint(i);
                Vector3 start = wp + Vector3.up * height;

                if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, height * 2f, _path.surfaceMask, QueryTriggerInteraction.Ignore))
                    _path.SetWorldPoint(i, hit.point);
            }
        }

        private void QueueRebuild()
        {
            if (_path == null)
                return;

            PungentPathRebuildScheduler.QueueRebuild(_path, null, "Rebuild Modular Path");
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedSceneRepaintTime, 0.08d) && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedWindowRepaintTime, 0.10d))
                Repaint();
        }

        private void DrawPerformanceWarnings()
        {
            if (_path == null)
                return;

            if (_path.TryEstimateGeneratedCounts(out int sampledPointCount, out int generatedSegmentCount, out int pointPrefabCount, out _))
            {
                int totalGenerated = generatedSegmentCount + pointPrefabCount;
                if (sampledPointCount >= ModularPathSpawner.HighSampledPointWarningThreshold)
                    EditorGUILayout.HelpBox("This path has a very high sampled point count. Consider reducing Samples Per Meter or Min Samples Per Span while editing.", MessageType.Info);
                if (totalGenerated >= ModularPathSpawner.HighGeneratedObjectWarningThreshold)
                    EditorGUILayout.HelpBox("This path may generate many objects. Consider increasing spacing, reducing samples per meter, or using preview-only while editing.", MessageType.Warning);
                if (_path.autoRebuildInEditor && _path.conformToSurface && sampledPointCount >= ModularPathSpawner.HighSampledPointWarningThreshold / 2)
                    EditorGUILayout.HelpBox("Auto rebuild is enabled while surface sampling is active. Editing may feel slower; use preview-only mode for large paths.", MessageType.Info);
            }

            if (_path.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold)
                EditorGUILayout.HelpBox("Generated children exceed the safe editing threshold. Use Clear Generated or preview-only mode before large edits.", MessageType.Warning);
        }
    }
    #endif

}
