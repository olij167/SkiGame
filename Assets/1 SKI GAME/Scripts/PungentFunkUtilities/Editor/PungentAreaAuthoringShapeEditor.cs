using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentAreaAuthoringShape))]
    public sealed class PungentAreaAuthoringShapeEditor : Editor
    {
        private enum AreaInspectorTab { Geometry, SurfaceRepair, Output, Diagnostics, Advanced }

        private AreaInspectorTab _selectedTab = AreaInspectorTab.Geometry;
        private int _selectedPoint = -1;
        private bool _snapToSurface = true;
        private LayerMask _surfaceMask = ~0;
        private double _nextAllowedSceneRepaintTime;
        private double _nextAllowedWindowRepaintTime;
        private int _lastClearHandleSelectionVersion = -1;
        private readonly List<PungentSpatialValidationIssue> _validationIssues = new List<PungentSpatialValidationIssue>();
        private bool _identityFoldout = true;
        private bool _geometryFoldout = true;
        private bool _previewFoldout = false;
        private bool _runtimeFoldout = false;
        private bool _outputFoldout = true;
        private bool _diagnosticsFoldout = true;
        private bool _surfaceRepairFoldout = true;
        private bool _advancedGeometryFoldout = false;
        private bool _advancedFoldout = false;
        private PungentSpatialSurfaceAuthoringSettings _surfaceSettings;
        private string _lastSurfaceRepairStatus = string.Empty;

        private void OnEnable()
        {
            _surfaceSettings = PungentSpatialSurfaceAuthoringSettings.Load();
            _surfaceMask = _surfaceSettings.SurfaceMask;
        }

        public override void OnInspectorGUI()
        {
            PungentAreaAuthoringShape area = target as PungentAreaAuthoringShape;
            if (area == null)
                return;

            serializedObject.Update();
            DrawInspectorHeader(area);
            DrawInspectorTabs();

            EditorGUI.BeginChangeCheck();
            switch (_selectedTab)
            {
                case AreaInspectorTab.Geometry:
                    DrawIdentitySection();
                    DrawGeometrySection(area);
                    break;
                case AreaInspectorTab.SurfaceRepair:
                    DrawSurfaceRepairSection(area);
                    break;
                case AreaInspectorTab.Output:
                    DrawOutputSection(area);
                    break;
                case AreaInspectorTab.Diagnostics:
                    DrawDiagnosticsSection(area);
                    break;
                case AreaInspectorTab.Advanced:
                    DrawPreviewSection();
                    DrawRuntimeSection();
                    DrawAdvancedSection();
                    break;
            }
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
                ApplyInspectorChanges(area);
        }

        private void DrawInspectorTabs()
        {
            EditorGUILayout.Space(3f);
            string[] labels = { "Geometry", "Surface / Repair", "Output", "Diagnostics", "Advanced" };
            _selectedTab = (AreaInspectorTab)GUILayout.Toolbar((int)_selectedTab, labels, GUILayout.Height(26f));
            EditorGUILayout.Space(3f);
        }

        private void DrawInspectorHeader(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Spatial Area", UtilityWindowTheme.Purple, area.SpatialDisplayName + " / " + area.shapeMode);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton(PungentSpatialAuthoringEditorState.IsEditingArea(area) ? "Exit Scene Edit" : "Edit In Scene", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                        PungentSpatialAuthoringEditorState.ToggleEditModeFor(area);
                    using (new EditorGUI.DisabledScope(area.shapeMode != PungentAreaAuthoringShape.ShapeMode.PolygonXZ))
                    {
                        if (GUILayout.Button("Place Point", GUILayout.Height(28f), GUILayout.Width(92f)))
                            AddPolygonPoint(area);
                    }
                    if (GUILayout.Button("Frame", GUILayout.Height(28f), GUILayout.Width(72f)))
                        PungentSpatialAuthoringActions.FrameSpatialObject(area);
                    if (GUILayout.Button("Frame Volume", GUILayout.Height(28f), GUILayout.Width(104f)))
                        PungentSpatialAuthoringActions.FrameSpatialObjectRuntimeVolume(area);
                    if (GUILayout.Button("Open Toolkit", GUILayout.Height(28f), GUILayout.Width(116f)))
                        PungentPathAuthoringToolkitWindow.OpenWithArea(area);
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
                DrawProperty("color", "Area Color", "Scene preview color for this area.");
            }
        }

        private void DrawGeometrySection(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                _geometryFoldout = EditorGUILayout.Foldout(_geometryFoldout, "Geometry", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_geometryFoldout)
                    return;

                DrawProperty("shapeMode", "Shape", "Polygon, rectangle, circle, or bounds-derived area shape.");
                if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ)
                    DrawProperty("rectangleSize", "Rectangle Size", "Local XZ rectangle dimensions.");
                if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.CircleXZ)
                    DrawProperty("circleRadius", "Circle Radius", "Local XZ circle radius.");
                DrawProperty("verticalMode", "Vertical Mode", "Area height interpretation.");
                if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.HeightFromTransform)
                    DrawProperty("height", "Height", "Height above the transform.");
                if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax)
                {
                    DrawProperty("minY", "Min Y", "Explicit world-space minimum Y.");
                    DrawProperty("maxY", "Max Y", "Explicit world-space maximum Y.");
                }

                _advancedGeometryFoldout = EditorGUILayout.Foldout(_advancedGeometryFoldout, "Advanced Geometry", true);
                if (_advancedGeometryFoldout && area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
                    DrawProperty("localPolygonPoints", "Local Polygon Points", "Raw local-space polygon point array. Prefer Scene View editing for normal authoring.");
            }
        }

        private void DrawSurfaceRepairSection(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _surfaceRepairFoldout = EditorGUILayout.Foldout(_surfaceRepairFoldout, "Terrain Alignment & Repair", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_surfaceRepairFoldout)
                    return;

                EditorGUI.BeginChangeCheck();
                _surfaceSettings.SurfaceMask = EditorGUILayout.MaskField(new GUIContent("Surface Mask", "Layer mask used by area alignment and Scene View point placement."), _surfaceSettings.SurfaceMask.value, UnityEditorInternal.InternalEditorUtility.layers);
                _surfaceSettings.RaycastHeight = EditorGUILayout.FloatField(new GUIContent("Raycast Height", "Height above each point used when sampling downward."), _surfaceSettings.RaycastHeight);
                _surfaceSettings.YOffset = EditorGUILayout.FloatField(new GUIContent("Y Offset", "Vertical offset applied after a valid surface hit."), _surfaceSettings.YOffset);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _surfaceSettings.MinY = EditorGUILayout.FloatField(new GUIContent("Min Y", "Reject hits below this world-space Y."), _surfaceSettings.MinY);
                    _surfaceSettings.MaxY = EditorGUILayout.FloatField(new GUIContent("Max Y", "Reject hits above this world-space Y."), _surfaceSettings.MaxY);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _surfaceSettings.MinSlope = EditorGUILayout.Slider(new GUIContent("Min Slope", "Reject hits flatter than this angle."), _surfaceSettings.MinSlope, 0f, 90f);
                    _surfaceSettings.MaxSlope = EditorGUILayout.Slider(new GUIContent("Max Slope", "Reject hits steeper than this angle."), _surfaceSettings.MaxSlope, 0f, 90f);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _surfaceSettings.Save();
                    _surfaceMask = _surfaceSettings.SurfaceMask;
                }

                bool polygon = area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!polygon))
                    {
                        if (GUILayout.Button("Align Selected Point", GUILayout.Height(24f)))
                            RunAreaSurfaceAction(area, PungentSpatialSurfaceAuthoringUtility.AlignAreaPoints(area, true, _selectedPoint, _surfaceSettings));
                        if (GUILayout.Button("Align All Points", GUILayout.Height(24f)))
                            RunAreaSurfaceAction(area, PungentSpatialSurfaceAuthoringUtility.AlignAreaPoints(area, false, _selectedPoint, _surfaceSettings));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set Base From Footprint", GUILayout.Height(24f)))
                        RunAreaSurfaceAction(area, PungentSpatialSurfaceAuthoringUtility.SetAreaBaseFromFootprint(area));
                    if (GUILayout.Button("Align Shape Center", GUILayout.Height(24f)))
                        RunAreaSurfaceAction(area, PungentSpatialSurfaceAuthoringUtility.AlignAreaShapeCenter(area, _surfaceSettings));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set Vertical Range", GUILayout.Height(24f)))
                        RunAreaSurfaceAction(area, PungentSpatialSurfaceAuthoringUtility.SetAreaVerticalRangeFromFootprint(area));
                    if (GUILayout.Button("Convert To Polygon", GUILayout.Height(24f)))
                        RunAreaSurfaceAction(area, PungentSpatialAreaRepairUtility.ConvertShapeToPolygon(area));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!polygon))
                    {
                        if (GUILayout.Button("Repair Intersections", GUILayout.Height(24f)))
                            RunAreaSurfaceAction(area, PungentSpatialAreaRepairUtility.RepairSelfIntersections(area));
                        if (GUILayout.Button("Reverse", GUILayout.Height(24f), GUILayout.Width(78f)))
                            RunAreaSurfaceAction(area, PungentSpatialAreaRepairUtility.ReverseWinding(area));
                        if (GUILayout.Button("Normalize", GUILayout.Height(24f), GUILayout.Width(86f)))
                            RunAreaSurfaceAction(area, PungentSpatialAreaRepairUtility.NormalizeWinding(area));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!polygon))
                    {
                        if (GUILayout.Button("Remove Near-Duplicates", GUILayout.Height(24f)))
                            RunAreaSurfaceAction(area, PungentSpatialAreaRepairUtility.RemoveNearDuplicates(area));
                    }
                    if (GUILayout.Button("Frame Runtime Volume", GUILayout.Height(24f), GUILayout.Width(146f)))
                        PungentSpatialAuthoringActions.FrameSpatialObjectRuntimeVolume(area);
                }

                if (!string.IsNullOrWhiteSpace(_lastSurfaceRepairStatus))
                    EditorGUILayout.HelpBox(_lastSurfaceRepairStatus, MessageType.Info);
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, "Preview", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_previewFoldout)
                    return;

                DrawProperty("drawGizmo", "Draw Gizmo", "Draw selected area gizmos and previews.");
                DrawProperty("drawUnselectedGizmo", "Draw Unselected", "Draw lightweight gizmos when this area is not selected.");
                _snapToSurface = EditorGUILayout.Toggle(new GUIContent("Snap Scene Points", "When adding, inserting, or moving polygon points, raycast to this mask."), _snapToSurface);
                _surfaceMask = _surfaceSettings.SurfaceMask;
                EditorGUILayout.LabelField("Scene Surface Mask", LayerMaskToNames(_surfaceMask), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawRuntimeSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _runtimeFoldout = EditorGUILayout.Foldout(_runtimeFoldout, "Runtime", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_runtimeFoldout)
                    return;

                DrawProperty("runtimeQueryable", "Runtime Queryable", "If disabled, ContainsWorldPoint returns false while shape data remains available.");
            }
        }

        private void DrawOutputSection(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                _outputFoldout = EditorGUILayout.Foldout(_outputFoldout, "Output", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_outputFoldout)
                    return;

                UtilityWindowTheme.SectionTitle("Spatial Output Recipes", UtilityWindowTheme.Teal, "Area boundary capable");
                PungentModularSpatialOutput[] recipes = area.GetComponents<PungentModularSpatialOutput>();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton(recipes.Length == 0 ? "Add Area Boundary Recipe" : "Add Another Recipe", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                        PungentSpatialOutputRecipeEditorUtility.CreateRecipe(area);
                    UtilityWindowTheme.CountPill("Recipes " + recipes.Length, recipes.Length > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 94f);
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

                DrawProperty("stableAreaId", "Stable ID", "Stable generic identifier used by optional bridges and exports.");
            }
        }

        private void DrawDiagnosticsSection(PungentAreaAuthoringShape area)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                _diagnosticsFoldout = EditorGUILayout.Foldout(_diagnosticsFoldout, "Diagnostics", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_diagnosticsFoldout)
                    return;

                _validationIssues.Clear();
                PungentSpatialValidationUtility.ValidateArea(area, _validationIssues);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Points " + area.PolygonPointCount, UtilityWindowTheme.Neutral, 92f);
                    UtilityWindowTheme.CountPill(_validationIssues.Count == 0 ? "No Issues" : "Issues " + _validationIssues.Count, _validationIssues.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    GUILayout.FlexibleSpace();
                }

                for (int i = 0; i < _validationIssues.Count; i++)
                {
                    PungentSpatialValidationIssue issue = _validationIssues[i];
                    MessageType messageType = issue.Severity == PungentSpatialValidationSeverity.Error
                        ? MessageType.Error
                        : issue.Severity == PungentSpatialValidationSeverity.Warning
                            ? MessageType.Warning
                            : MessageType.Info;
                    EditorGUILayout.HelpBox(issue.Message, messageType);
                }

                if (area.PolygonPointCount >= PungentAreaAuthoringShapeSceneHandles.HighPolygonPointWarningThreshold)
                    EditorGUILayout.HelpBox("This area has many vertices. Scene handles may become slower; simplify the polygon if editing becomes sluggish.", MessageType.Info);

                if (area.TryGetSpatialBounds(out Bounds bounds))
                    EditorGUILayout.LabelField("Bounds", bounds.size.ToString("0.##"), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void RunAreaSurfaceAction(PungentAreaAuthoringShape area, string result)
        {
            _lastSurfaceRepairStatus = result;
            PungentSpatialAuthoringEditorState.ReportAction(result);
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            SceneView.RepaintAll();
        }

        private static string LayerMaskToNames(LayerMask mask)
        {
            string[] layers = UnityEditorInternal.InternalEditorUtility.layers;
            List<string> names = new List<string>();
            for (int i = 0; i < layers.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0 && (mask.value & (1 << layer)) != 0)
                    names.Add(layers[i]);
            }

            return names.Count == 0 ? "Nothing" : string.Join(", ", names);
        }

        private void ApplyInspectorChanges(PungentAreaAuthoringShape area)
        {
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            if (PungentEditorPerformanceUtility.TimeGate(ref _nextAllowedWindowRepaintTime, 0.10d))
                Repaint();
        }

        private void OnSceneGUI()
        {
            PungentAreaAuthoringShape area = target as PungentAreaAuthoringShape;
            if (area == null || !area.drawGizmo)
                return;

            PungentSpatialAuthoringEditorState.EnsureInitialized();
            if (!PungentSpatialAuthoringEditorState.ShouldDrawArea(area))
                return;
            if (PungentSpatialAuthoringEditorState.IsAreaEditorToolActive(area))
                return;

            PungentSpatialSceneHandleUtility.BeginSceneGUI();
            PungentSpatialSceneHandleUtility.ProtectSelectionIfNeeded(
                PungentSpatialAuthoringEditorState.IsEditingArea(area) &&
                PungentSpatialAuthoringEditorState.ShouldCaptureSceneInput);

            if (_lastClearHandleSelectionVersion != PungentSpatialAuthoringEditorState.ClearHandleSelectionVersion)
            {
                _lastClearHandleSelectionVersion = PungentSpatialAuthoringEditorState.ClearHandleSelectionVersion;
                _selectedPoint = -1;
            }

            PungentAreaAuthoringShapeSceneHandles.Draw(
                area,
                ref _selectedPoint,
                PungentSpatialAuthoringEditorState.SnapToSurface && _snapToSurface,
                _surfaceMask,
                PungentSpatialAuthoringEditorState.ShowLabels,
                PungentSpatialAuthoringEditorState.HandleSize,
                PungentSpatialAuthoringEditorState.DrawRichPreview,
                PungentSpatialAuthoringEditorState.IsEditingArea(area),
                SceneView.currentDrawingSceneView,
                ownerWindow: null,
                ref _nextAllowedSceneRepaintTime,
                ref _nextAllowedWindowRepaintTime);
        }

        private void AddPolygonPoint(PungentAreaAuthoringShape area)
        {
            if (area == null)
                return;

            if (area.shapeMode != PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
            {
                Undo.RecordObject(area, "Switch Area To Polygon");
                area.shapeMode = PungentAreaAuthoringShape.ShapeMode.PolygonXZ;
                EditorUtility.SetDirty(area);
            }

            Selection.activeGameObject = area.gameObject;
            PungentSpatialAuthoringEditorState.RefreshSelection();
            PungentSpatialAuthoringEditorState.SetEditMode(PungentSpatialEditMode.Add, true);
            PungentSpatialAuthoringEditorState.ReportAction("Place mode active. Click in the Scene view to add a polygon point.");
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.08d);
        }
    }

    internal static class PungentAreaAuthoringShapeSceneHandles
    {
        public const int HighPolygonPointWarningThreshold = 128;

        private static readonly List<Vector3> s_WorldPoints = new List<Vector3>(128);
        private static Vector3[] s_FillPoints = new Vector3[0];
        private static Vector3[] s_ClosedLinePoints = new Vector3[0];

        public static void Draw(
            PungentAreaAuthoringShape area,
            ref int selectedPoint,
            bool snapToSurface,
            LayerMask surfaceMask,
            bool drawLabels,
            float handleScale,
            bool drawFill,
            bool editingActive,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area == null)
                return;

            DrawAreaPreview(area, drawFill);

            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ)
            {
                DrawPolygonPointHandles(area, ref selectedPoint, snapToSurface, surfaceMask, drawLabels, handleScale, editingActive, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                if (editingActive)
                {
                    DrawPolygonContextualPreview(area, selectedPoint, snapToSurface, surfaceMask);
                    HandlePolygonMouseActions(area, ref selectedPoint, snapToSurface, surfaceMask, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                    HandlePolygonDelete(area, ref selectedPoint, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                }
            }
            else if (editingActive)
            {
                DrawPrimitiveShapeHandles(area, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
            }

            if (editingActive)
                DrawVerticalExtentHandles(area, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static void DrawAreaPreview(PungentAreaAuthoringShape area, bool drawFill)
        {
            if (area == null)
                return;

            Color previous = Handles.color;
            Color outline = area.color;
            outline.a = 0.9f;
            Color fill = area.color;
            fill.a = Mathf.Clamp(fill.a, 0.08f, 0.28f);

            if ((area.shapeMode == PungentAreaAuthoringShape.ShapeMode.BoundsFromCollider ||
                 area.shapeMode == PungentAreaAuthoringShape.ShapeMode.BoundsFromRenderers) &&
                area.TryGetSpatialBounds(out Bounds bounds))
            {
                Handles.color = outline;
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.color = previous;
                return;
            }

            area.GetWorldPolygon(s_WorldPoints);
            if (s_WorldPoints.Count < 2)
            {
                Handles.color = previous;
                return;
            }

            EnsureExactArraySize(ref s_FillPoints, s_WorldPoints.Count);
            EnsureExactArraySize(ref s_ClosedLinePoints, s_WorldPoints.Count + 1);
            for (int i = 0; i < s_WorldPoints.Count; i++)
            {
                s_FillPoints[i] = s_WorldPoints[i];
                s_ClosedLinePoints[i] = s_WorldPoints[i];
            }
            s_ClosedLinePoints[s_WorldPoints.Count] = s_WorldPoints[0];

            if (drawFill && s_WorldPoints.Count >= 3 && s_WorldPoints.Count < HighPolygonPointWarningThreshold)
            {
                Handles.color = fill;
                Handles.DrawAAConvexPolygon(s_FillPoints);
            }

            Handles.color = outline;
            Handles.DrawAAPolyLine(3f, s_ClosedLinePoints);

            if (area.TryGetSpatialBounds(out Bounds areaBounds))
                Handles.DrawWireCube(areaBounds.center, areaBounds.size);

            Handles.color = previous;
        }

        private static void DrawPolygonPointHandles(
            PungentAreaAuthoringShape area,
            ref int selectedPoint,
            bool snapToSurface,
            LayerMask surfaceMask,
            bool drawLabels,
            float handleScale,
            bool editingActive,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area.localPolygonPoints == null)
                return;

            int hoveredPoint = -1;
            DrawPolygonEdgeControls(area, editingActive);
            for (int i = 0; i < area.localPolygonPoints.Count; i++)
            {
                Vector3 worldPoint = area.transform.TransformPoint(area.localPolygonPoints[i]);
                float size = HandleUtility.GetHandleSize(worldPoint) * Mathf.Max(0.1f, handleScale) * 0.08f;
                float pickSize = Mathf.Max(size * 2.8f, HandleUtility.GetHandleSize(worldPoint) * 0.04f);
                bool selected = selectedPoint == i;
                int controlId = PungentSpatialSceneHandleUtility.GetPointControlId(area, i);
                bool hovered = PungentSpatialSceneHandleUtility.RegisterPointControl(controlId, worldPoint, pickSize);
                if (hovered)
                    hoveredPoint = i;

                if (editingActive &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Delete &&
                    Event.current != null &&
                    Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 &&
                    HandleUtility.nearestControl == controlId &&
                    !PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(Event.current))
                {
                    selectedPoint = i;
                    DeleteSelectedPolygonPoint(area, ref selectedPoint, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                    Event.current.Use();
                    PungentSpatialAuthoringEditorState.ReportAction("Deleted area point " + i + ".");
                    PungentSpatialAuthoringEditorState.SetHandleSelection("Area", selectedPoint, -1, area.localPolygonPoints.Count);
                    return;
                }

                if (editingActive && PungentSpatialSceneHandleUtility.TryBeginPointDrag(controlId, Event.current))
                {
                    selectedPoint = i;
                    RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                }

                if (editingActive &&
                    selected &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Move &&
                    PungentSpatialSceneHandleUtility.IsDraggingPoint(controlId, Event.current) &&
                    TryMouseToWorld(area, Event.current.mousePosition, snapToSurface, surfaceMask, out Vector3 moved))
                {
                    Undo.RecordObject(area, "Move Area Polygon Point");
                    area.localPolygonPoints[i] = area.transform.InverseTransformPoint(moved);
                    EditorUtility.SetDirty(area);
                    RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                    Event.current.Use();
                }

                if (PungentSpatialSceneHandleUtility.TryEndPointDrag(controlId, Event.current) && selected)
                    PungentSpatialAuthoringEditorState.ReportAction("Moved area point " + i + ".");

                Color color = selected
                    ? UtilityWindowTheme.Amber
                    : hovered
                        ? new Color(0.55f, 1f, 1f, 1f)
                        : UtilityWindowTheme.Cyan;
                PungentSpatialSceneHandleUtility.DrawPointCap(controlId, worldPoint, hovered ? size * 1.25f : size, color);

                if (drawLabels && PungentSpatialAuthoringEditorState.ShouldDrawPointLabel(selected))
                    Handles.Label(worldPoint + Vector3.up * (size * 5f), "A" + i);
            }

            PungentSpatialAuthoringEditorState.SetHandleSelection("Area", selectedPoint, hoveredPoint, area.localPolygonPoints.Count);
        }

        private static void DrawPolygonContextualPreview(PungentAreaAuthoringShape area, int selectedPoint, bool snapToSurface, LayerMask surfaceMask)
        {
            if (area == null || PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(Event.current))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback(string.Empty));
                return;
            }

            PungentSpatialEditMode mode = PungentSpatialAuthoringEditorState.EditMode;
            if (mode == PungentSpatialEditMode.Add || mode == PungentSpatialEditMode.Insert || mode == PungentSpatialEditMode.Split)
            {
                DrawPolygonPlacementPreview(area, selectedPoint, snapToSurface, surfaceMask);
                return;
            }

            if (mode == PungentSpatialEditMode.Delete)
            {
                DrawPolygonDeletePreview(area, snapToSurface, surfaceMask);
                return;
            }

            PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback(string.Empty));
        }

        private static void DrawPolygonPlacementPreview(PungentAreaAuthoringShape area, int selectedPoint, bool snapToSurface, LayerMask surfaceMask)
        {
            if (!TryBuildPolygonPlacementDecision(area, selectedPoint, snapToSurface, surfaceMask, out PungentSpatialPlacementDecision decision))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No valid area placement point under cursor."));
                return;
            }

            Color previous = Handles.color;
            if (decision.Kind == PungentSpatialPlacementKind.Insert && decision.SegmentStart >= 0 && decision.SegmentEnd >= 0)
            {
                Handles.color = new Color(1f, 0.95f, 0.28f, 0.95f);
                Handles.DrawAAPolyLine(
                    8f,
                    area.transform.TransformPoint(area.localPolygonPoints[decision.SegmentStart]),
                    area.transform.TransformPoint(area.localPolygonPoints[decision.SegmentEnd]));
            }

            float size = HandleUtility.GetHandleSize(decision.WorldPoint) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.1f;
            Handles.color = decision.Kind == PungentSpatialPlacementKind.Insert
                ? new Color(1f, 0.95f, 0.28f, 0.92f)
                : new Color(0.22f, 1f, 0.58f, 0.92f);
            Handles.SphereHandleCap(0, decision.WorldPoint, Quaternion.identity, size, EventType.Repaint);
            Handles.DrawWireDisc(decision.WorldPoint, Vector3.up, size * 1.8f);
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(decision.WorldPoint + Vector3.up * (size * 4f), decision.StatusText);
            Handles.color = previous;
            PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback(decision.StatusText));
        }

        private static bool TryBuildPolygonPlacementDecision(
            PungentAreaAuthoringShape area,
            int selectedPoint,
            bool snapToSurface,
            LayerMask surfaceMask,
            out PungentSpatialPlacementDecision decision)
        {
            decision = PungentSpatialPlacementDecision.None(string.Empty);
            Event e = Event.current;
            if (area == null || e == null || !TryMouseToWorld(area, e.mousePosition, snapToSurface, surfaceMask, out Vector3 worldPoint))
                return false;

            bool forceAppend = e.shift && !e.control && !e.command && !e.alt;
            bool forceInsert = (e.control || e.command) && !e.shift && !e.alt;
            PungentSpatialEditMode mode = PungentSpatialAuthoringEditorState.EditMode;
            if (mode == PungentSpatialEditMode.Insert || mode == PungentSpatialEditMode.Split)
                forceInsert = true;

            int count = area.localPolygonPoints != null ? area.localPolygonPoints.Count : 0;
            int insertIndex = count;
            bool insert = false;
            int borderSegment = -1;
            bool projectedToBoundary = false;
            if (!forceAppend && count >= 3)
            {
                borderSegment = FindNearestPolygonSegment(area, worldPoint);
                if (IsWorldPointInsidePolygon(area, worldPoint))
                {
                    worldPoint = ClosestPointOnPolygonSegment(area, borderSegment, worldPoint);
                    if (snapToSurface)
                        worldPoint = SnapPointToSurface(worldPoint, surfaceMask);
                    projectedToBoundary = true;
                }

                insertIndex = Mathf.Clamp(borderSegment + 1, 0, count);
                insert = true;
            }
            else
            {
                insert = !forceAppend && TryFindSmartPolygonInsertIndex(area, worldPoint, e.mousePosition, forceInsert, selectedPoint, out insertIndex);
            }

            int appendIndex = selectedPoint >= 0 && selectedPoint < count ? selectedPoint + 1 : count;
            int pointIndex = insert ? Mathf.Clamp(insertIndex, 0, count) : Mathf.Clamp(appendIndex, 0, count);
            int segmentStart = -1;
            int segmentEnd = -1;
            if (insert && count >= 2)
            {
                segmentStart = borderSegment >= 0 ? borderSegment : Mathf.Clamp(pointIndex - 1, 0, count - 1);
                segmentEnd = (segmentStart + 1) % count;
            }

            string text = insert
                ? (projectedToBoundary ? "Project to boundary and insert" : "Insert area point") + " " + pointIndex + " between A" + segmentStart + " and A" + segmentEnd + "."
                : selectedPoint >= 0 && selectedPoint < count
                    ? "Append area point after A" + selectedPoint + "."
                    : "Append area point " + pointIndex + ".";

            decision = new PungentSpatialPlacementDecision
            {
                Kind = insert ? PungentSpatialPlacementKind.Insert : PungentSpatialPlacementKind.Append,
                WorldPoint = worldPoint,
                PointIndex = pointIndex,
                SegmentStart = segmentStart,
                SegmentEnd = segmentEnd,
                StatusText = text
            };
            return true;
        }

        private static void DrawPolygonDeletePreview(PungentAreaAuthoringShape area, bool snapToSurface, LayerMask surfaceMask)
        {
            Event e = Event.current;
            if (area == null || e == null || !TryMouseToWorld(area, e.mousePosition, snapToSurface, surfaceMask, out Vector3 worldPoint))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No area point under cursor."));
                return;
            }

            int nearest = FindNearestPolygonPoint(area, worldPoint);
            if (nearest < 0)
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No area point under cursor."));
                return;
            }

            Vector3 point = area.transform.TransformPoint(area.localPolygonPoints[nearest]);
            float size = HandleUtility.GetHandleSize(point) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.11f;
            Color previous = Handles.color;
            Handles.color = new Color(1f, 0.28f, 0.22f, 0.95f);
            Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
            Handles.DrawWireDisc(point, Vector3.up, size * 2f);
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(point + Vector3.up * (size * 4f), "Delete A" + nearest);
            Handles.color = previous;
            PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("Delete area point " + nearest + "."));
        }

        private static void DrawPolygonEdgeControls(PungentAreaAuthoringShape area, bool editingActive)
        {
            if (!editingActive || area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 2)
                return;

            Color previous = Handles.color;
            for (int i = 0; i < area.localPolygonPoints.Count; i++)
            {
                int next = (i + 1) % area.localPolygonPoints.Count;
                Vector3 a = area.transform.TransformPoint(area.localPolygonPoints[i]);
                Vector3 b = area.transform.TransformPoint(area.localPolygonPoints[next]);
                int controlId = PungentSpatialSceneHandleUtility.GetEdgeControlId(area, i);
                if (!PungentSpatialSceneHandleUtility.RegisterEdgeControl(controlId, a, b))
                    continue;

                Handles.color = new Color(1f, 0.95f, 0.28f, 0.95f);
                Handles.DrawAAPolyLine(6f, a, b);
            }
            Handles.color = previous;
        }

        private static void DrawPrimitiveShapeHandles(
            PungentAreaAuthoringShape area,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area == null)
                return;

            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.RectangleXZ)
            {
                DrawRectangleSizeHandle(area, Vector3.right, true, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                DrawRectangleSizeHandle(area, Vector3.left, true, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                DrawRectangleSizeHandle(area, Vector3.forward, false, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                DrawRectangleSizeHandle(area, Vector3.back, false, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                return;
            }

            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.CircleXZ)
                DrawCircleRadiusHandle(area, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static void DrawRectangleSizeHandle(
            PungentAreaAuthoringShape area,
            Vector3 localDirection,
            bool xAxis,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            Vector2 half = area.rectangleSize * 0.5f;
            float distance = xAxis ? half.x : half.y;
            Vector3 local = localDirection.normalized * distance;
            Vector3 world = area.transform.TransformPoint(local);
            Vector3 axis = area.transform.TransformDirection(localDirection.normalized);
            float size = HandleUtility.GetHandleSize(world) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.12f;

            Color previous = Handles.color;
            Handles.color = new Color(area.color.r, area.color.g, area.color.b, 0.9f);
            Handles.DrawAAPolyLine(2f, area.transform.position, world);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(world, axis, size, Handles.CubeHandleCap, 0f);
            bool changed = EditorGUI.EndChangeCheck();
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(world + Vector3.up * (size * 4f), xAxis ? "Size X" : "Size Z");
            Handles.color = previous;
            if (!changed)
                return;

            Vector3 movedLocal = area.transform.InverseTransformPoint(moved);
            Undo.RecordObject(area, "Resize Area Rectangle");
            if (xAxis)
                area.rectangleSize.x = Mathf.Max(0.01f, Mathf.Abs(movedLocal.x) * 2f);
            else
                area.rectangleSize.y = Mathf.Max(0.01f, Mathf.Abs(movedLocal.z) * 2f);
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.ReportAction((xAxis ? "Area width " : "Area depth ") + (xAxis ? area.rectangleSize.x : area.rectangleSize.y).ToString("0.##") + ".");
            RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static void DrawCircleRadiusHandle(
            PungentAreaAuthoringShape area,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            Vector3 world = area.transform.TransformPoint(new Vector3(area.circleRadius, 0f, 0f));
            Vector3 axis = area.transform.right;
            float size = HandleUtility.GetHandleSize(world) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.13f;

            Color previous = Handles.color;
            Handles.color = new Color(area.color.r, area.color.g, area.color.b, 0.9f);
            Handles.DrawAAPolyLine(2f, area.transform.position, world);
            Handles.DrawWireDisc(area.transform.position, area.transform.up, area.circleRadius);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(world, axis, size, Handles.SphereHandleCap, 0f);
            bool changed = EditorGUI.EndChangeCheck();
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(world + Vector3.up * (size * 4f), "Radius");
            Handles.color = previous;
            if (!changed)
                return;

            Vector3 movedLocal = area.transform.InverseTransformPoint(moved);
            Undo.RecordObject(area, "Resize Area Circle");
            area.circleRadius = Mathf.Max(0.01f, Mathf.Abs(movedLocal.x));
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.ReportAction("Area radius " + area.circleRadius.ToString("0.##") + ".");
            RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static void DrawVerticalExtentHandles(
            PungentAreaAuthoringShape area,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area == null || area.verticalMode == PungentAreaAuthoringShape.VerticalMode.Infinite)
                return;

            if (!area.TryGetSpatialBounds(out Bounds bounds))
                return;

            Vector3 center = bounds.center;
            float minY = bounds.min.y;
            float maxY = bounds.max.y;
            Color previous = Handles.color;
            Handles.color = new Color(area.color.r, area.color.g, area.color.b, 0.85f);
            Handles.DrawLine(new Vector3(center.x, minY, center.z), new Vector3(center.x, maxY, center.z));
            float capRadius = Mathf.Max(0.1f, bounds.extents.x, bounds.extents.z);
            Handles.DrawWireDisc(new Vector3(center.x, maxY, center.z), Vector3.up, capRadius);
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax)
                Handles.DrawWireDisc(new Vector3(center.x, minY, center.z), Vector3.up, capRadius);

            float size = HandleUtility.GetHandleSize(center) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.08f;
            DrawVerticalHandle(area, true, new Vector3(center.x, maxY, center.z), size, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax)
                DrawVerticalHandle(area, false, new Vector3(center.x, minY, center.z), size, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
            Handles.color = previous;
        }

        private static void DrawVerticalHandle(
            PungentAreaAuthoringShape area,
            bool maxHandle,
            Vector3 world,
            float size,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(world, Vector3.up, size, Handles.ConeHandleCap, 0f);
            bool changed = EditorGUI.EndChangeCheck();
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(world + Vector3.right * (size * 2f), maxHandle ? "Max Y" : "Min Y");
            if (!changed)
                return;

            Undo.RecordObject(area, maxHandle ? "Set Area Max Y" : "Set Area Min Y");
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.HeightFromTransform)
            {
                area.height = Mathf.Max(0.01f, moved.y - area.transform.position.y);
            }
            else if (maxHandle)
            {
                area.maxY = Mathf.Max(moved.y, area.minY);
            }
            else
            {
                area.minY = Mathf.Min(moved.y, area.maxY);
            }

            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.ReportAction(maxHandle ? "Adjusted area max Y." : "Adjusted area min Y.");
            RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static void HandlePolygonMouseActions(
            PungentAreaAuthoringShape area,
            ref int selectedPoint,
            bool snapToSurface,
            LayerMask surfaceMask,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || e.alt)
                return;

            bool forceAppend = e.shift && !e.control && !e.command;
            bool forceInsert = (e.control || e.command) && !e.shift;
            bool place = PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Add ||
                         PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Insert ||
                         PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Split ||
                         forceAppend ||
                         forceInsert;
            bool delete = PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Delete;
            if (!place && !delete)
                return;

            if (PungentSpatialSceneHandleUtility.IsNearestPointControl())
                return;

            if (delete)
            {
                if (!TryMouseToWorld(area, e.mousePosition, snapToSurface, surfaceMask, out Vector3 worldPoint))
                    return;

                selectedPoint = FindNearestPolygonPoint(area, worldPoint);
                DeleteSelectedPolygonPoint(area, ref selectedPoint, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
                PungentSpatialAuthoringEditorState.ReportAction("Deleted nearest area point.");
                e.Use();
                return;
            }

            if (!TryBuildPolygonPlacementDecision(area, selectedPoint, snapToSurface, surfaceMask, out PungentSpatialPlacementDecision decision))
                return;

            bool insert = decision.Kind == PungentSpatialPlacementKind.Insert;
            Undo.RecordObject(area, insert ? "Insert Area Polygon Point" : "Add Area Polygon Point");
            if (area.localPolygonPoints == null)
                area.localPolygonPoints = new List<Vector3>();

            if (!insert || area.localPolygonPoints.Count < 2)
            {
                int appendIndex = Mathf.Clamp(decision.PointIndex, 0, area.localPolygonPoints.Count);
                area.localPolygonPoints.Insert(appendIndex, area.transform.InverseTransformPoint(decision.WorldPoint));
                selectedPoint = Mathf.Clamp(appendIndex, 0, area.localPolygonPoints.Count - 1);
            }
            else
            {
                int insertIndex = Mathf.Clamp(decision.PointIndex, 0, area.localPolygonPoints.Count);
                area.localPolygonPoints.Insert(insertIndex, area.transform.InverseTransformPoint(decision.WorldPoint));
                selectedPoint = insertIndex;
            }

            EditorUtility.SetDirty(area);
            RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
            PungentSpatialAuthoringEditorState.ReportAction(decision.StatusText);
            e.Use();
        }

        private static bool TryFindSmartPolygonInsertIndex(PungentAreaAuthoringShape area, Vector3 worldPoint, Vector2 mousePosition, bool forceInsert, int selectedPoint, out int insertIndex)
        {
            insertIndex = area != null && area.localPolygonPoints != null ? area.localPolygonPoints.Count : 0;
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 2)
                return false;

            insertIndex = Mathf.Clamp(FindNearestPolygonSegment(area, worldPoint) + 1, 0, area.localPolygonPoints.Count);
            if (forceInsert || area.localPolygonPoints.Count >= 3)
                return forceInsert || FindNearestPolygonSegmentScreenDistance(area, mousePosition) <= 18f;

            return selectedPoint < 0 && FindNearestPolygonSegmentScreenDistance(area, mousePosition) <= 18f;
        }

        private static float FindNearestPolygonSegmentScreenDistance(PungentAreaAuthoringShape area, Vector2 mousePosition)
        {
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 2)
                return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            int count = area.localPolygonPoints.Count;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector2 a = HandleUtility.WorldToGUIPoint(area.transform.TransformPoint(area.localPolygonPoints[i]));
                Vector2 b = HandleUtility.WorldToGUIPoint(area.transform.TransformPoint(area.localPolygonPoints[next]));
                float distance = DistancePointToSegment(mousePosition, a, b);
                if (distance < best)
                    best = distance;
            }

            return best;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 0.000001f)
                return Vector2.Distance(p, a);

            float t = Vector2.Dot(p - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            return Vector2.Distance(p, a + ab * t);
        }

        private static void HandlePolygonDelete(
            PungentAreaAuthoringShape area,
            ref int selectedPoint,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area.localPolygonPoints == null || selectedPoint < 0 || selectedPoint >= area.localPolygonPoints.Count)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            if (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace)
                return;

            DeleteSelectedPolygonPoint(area, ref selectedPoint, sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
            PungentSpatialAuthoringEditorState.ReportAction("Deleted selected area point.");
            e.Use();
        }

        private static void DeleteSelectedPolygonPoint(
            PungentAreaAuthoringShape area,
            ref int selectedPoint,
            SceneView sceneView,
            EditorWindow ownerWindow,
            ref double nextAllowedSceneRepaintTime,
            ref double nextAllowedWindowRepaintTime)
        {
            if (area == null || area.localPolygonPoints == null || selectedPoint < 0 || selectedPoint >= area.localPolygonPoints.Count)
                return;

            Undo.RecordObject(area, "Delete Area Polygon Point");
            area.localPolygonPoints.RemoveAt(selectedPoint);
            selectedPoint = Mathf.Clamp(selectedPoint - 1, -1, area.localPolygonPoints.Count - 1);
            EditorUtility.SetDirty(area);
            RequestRepaints(sceneView, ownerWindow, ref nextAllowedSceneRepaintTime, ref nextAllowedWindowRepaintTime);
        }

        private static bool TryMouseToWorld(PungentAreaAuthoringShape area, Vector2 mousePosition, bool snapToSurface, LayerMask surfaceMask, out Vector3 worldPoint)
        {
            worldPoint = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            if (snapToSurface && Physics.Raycast(ray, out RaycastHit hit, 10000f, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                return true;
            }

            Plane plane = new Plane(Vector3.up, area != null ? area.transform.position : Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                worldPoint = ray.GetPoint(enter);
                return true;
            }

            return false;
        }

        private static Vector3 SnapPointToSurface(Vector3 point, LayerMask surfaceMask)
        {
            if (Physics.Raycast(point + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, surfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return point;
        }

        private static int FindNearestPolygonSegment(PungentAreaAuthoringShape area, Vector3 worldPoint)
        {
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 2)
                return 0;

            int count = area.localPolygonPoints.Count;
            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3 a = area.transform.TransformPoint(area.localPolygonPoints[i]);
                Vector3 b = area.transform.TransformPoint(area.localPolygonPoints[next]);
                Vector3 closest = PungentPathAuthoringToolkit.ClosestPointOnSegment(a, b, worldPoint);
                float sqr = (closest - worldPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static Vector3 ClosestPointOnPolygonSegment(PungentAreaAuthoringShape area, int segmentIndex, Vector3 worldPoint)
        {
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 2)
                return worldPoint;

            int count = area.localPolygonPoints.Count;
            int start = Mathf.Clamp(segmentIndex, 0, count - 1);
            int end = (start + 1) % count;
            Vector3 a = area.transform.TransformPoint(area.localPolygonPoints[start]);
            Vector3 b = area.transform.TransformPoint(area.localPolygonPoints[end]);
            return PungentPathAuthoringToolkit.ClosestPointOnSegment(a, b, worldPoint);
        }

        private static bool IsWorldPointInsidePolygon(PungentAreaAuthoringShape area, Vector3 worldPoint)
        {
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count < 3)
                return false;

            bool inside = false;
            Vector2 p = new Vector2(worldPoint.x, worldPoint.z);
            int count = area.localPolygonPoints.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector3 wi = area.transform.TransformPoint(area.localPolygonPoints[i]);
                Vector3 wj = area.transform.TransformPoint(area.localPolygonPoints[j]);
                Vector2 a = new Vector2(wi.x, wi.z);
                Vector2 b = new Vector2(wj.x, wj.z);
                bool crosses = (a.y > p.y) != (b.y > p.y);
                if (crosses)
                {
                    float deltaY = b.y - a.y;
                    if (Mathf.Abs(deltaY) < 0.000001f)
                        continue;

                    float x = (b.x - a.x) * (p.y - a.y) / deltaY + a.x;
                    if (p.x < x)
                        inside = !inside;
                }
            }

            return inside;
        }

        private static int FindNearestPolygonPoint(PungentAreaAuthoringShape area, Vector3 worldPoint)
        {
            if (area == null || area.localPolygonPoints == null || area.localPolygonPoints.Count == 0)
                return -1;

            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < area.localPolygonPoints.Count; i++)
            {
                Vector3 point = area.transform.TransformPoint(area.localPolygonPoints[i]);
                float sqr = (worldPoint - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static void RequestRepaints(SceneView sceneView, EditorWindow ownerWindow, ref double nextAllowedSceneRepaintTime, ref double nextAllowedWindowRepaintTime)
        {
            if (sceneView != null)
                PungentEditorPerformanceUtility.RequestSceneViewRepaintThrottled(sceneView, ref nextAllowedSceneRepaintTime, 0.08d);
            else
                PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref nextAllowedSceneRepaintTime, 0.08d);

            if (ownerWindow != null)
                PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(ownerWindow, ref nextAllowedWindowRepaintTime, 0.08d);
        }

        private static void EnsureExactArraySize(ref Vector3[] array, int count)
        {
            if (array != null && array.Length == count)
                return;

            array = new Vector3[Mathf.Max(0, count)];
        }
    }
#endif
}
