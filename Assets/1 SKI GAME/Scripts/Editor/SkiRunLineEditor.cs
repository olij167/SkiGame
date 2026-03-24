#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiGame.Runs;
using UnityEngine.Rendering;
using static UnityEditor.Undo;

namespace SkiGame.RunsEditor
{
    [CustomEditor(typeof(SkiRunLine))]
    public sealed class SkiRunLineEditor : Editor
    {
        // --- Scene display toggles ---
        private bool showPointGizmos = true;          // clickable point markers (selection)
        private bool showWidthVisuals = false;         // width ticks (non-interactive) for anchors
        private bool showBoundaryPreview = true;      // left/right corridor lines
        private bool showIntersectionOverlay = true;  // gate/entry overlay for intersections
        private bool showPairPreview = true;          // crossbars at spawn samples
        private bool showSpacingHandle = false;        // drag handle near start

        // Scene hotkey overlay + interaction modes
        private bool showHotkeyOverlay = true;
        private bool gateEditMode = false; // toggled with 'G'

        // Fence editing (editor-only)
        private bool foldFences = true;
        private bool fenceHoleEditMode = false;

        private SkiRunLine.FenceSide fenceAddSide = SkiRunLine.FenceSide.Left;
        private float fenceAddHoleLengthMeters = 8f;

        // Fence hole selection (for delete/highlight)
        private int _selectedFenceHoleIndex = -1;

        // Scene-only vertical lift so handles/gizmos don't sit exactly on the terrain surface.
        // Editor-only (does not affect runtime data).
        private float sceneVisualYOffset = 0.45f;

        // Inspector state (editor-only)
        private int inspectorTab = 0; // 0=Run, 1=Scene, 2=Utilities, 3=Advanced
        private bool foldRunSettings = true;

        private bool foldAdvanced = false;

        // --- Edit interaction toggles ---
        private bool enableShiftClickInsert = true;
        private bool enableAltClickDelete = true;
        private bool editNeighborPoints = false;      // show move handle for selected +/- 1
        private bool autoSnapToTerrainWhenEditing = true;

        // --- Selection behaviour ---
        private int selectedPointIndex = -1;
        private float pointPickSizeScale = 0.08f;

        // --- Preview density / performance ---
        private int previewMaxPairs = 500;

        // --- Preview rebuild control (performance) ---
        private bool previewIncludeOverlapAvoidance = false; // OFF by default: fast preview while editing
        private bool _previewDirty = true;
        private double _lastPreviewBuildTime = -1;
        private const double PreviewRebuildMinInterval = 0.12; // seconds (throttle SceneView rebuilds)

        private int _lastPointsHash = int.MinValue;

        // ------------------------------------------------------------
        // Authoring Mode (single, cohesive tool state)
        // ------------------------------------------------------------
        private enum AuthoringMode { Path = 0, Flags = 1, Fences = 2 }
        private AuthoringMode _authoringMode = AuthoringMode.Path;

        // Fence preview buffers (reused per span/hole)
        private readonly List<Vector3> _tmpFencePreview = new List<Vector3>(512);
        // Reuse an array for Handles.DrawAAPolyLine to avoid GC from List.ToArray().
        private Vector3[] _tmpFencePreviewArray;

        private void DrawAAPolyLineCached(float width, List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return;

            if (_tmpFencePreviewArray == null || _tmpFencePreviewArray.Length != pts.Count)
                _tmpFencePreviewArray = new Vector3[pts.Count];

            for (int i = 0; i < pts.Count; i++)
                _tmpFencePreviewArray[i] = pts[i];

            Handles.DrawAAPolyLine(width, _tmpFencePreviewArray);
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            Undo.postprocessModifications += OnPostprocessModifications; // NEW
            _previewDirty = true;
            _active = this;

            LoadEditorPrefs();

            SceneView.RepaintAll();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.postprocessModifications -= OnPostprocessModifications; // NEW
            if (_active == this) _active = null;

            SaveEditorPrefs();
        }

        private void OnUndoRedo()
        {
            _previewDirty = true;
            SceneView.RepaintAll();
            Repaint();
        }

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (modifications == null || modifications.Length == 0)
                return modifications;

            for (int i = 0; i < modifications.Length; i++)
            {
                var target = modifications[i].currentValue.target;
                if (target == null) continue;

                // Direct edits to any SkiRunLine should invalidate intersection previews.
                if (target is SkiRunLine)
                {
                    _previewDirty = true;
                    SceneView.RepaintAll();
                    break;
                }

                // Some edits come through as Component / GameObject; catch those too.
                if (target is Component c && c.GetComponent<SkiRunLine>() != null)
                {
                    _previewDirty = true;
                    SceneView.RepaintAll();
                    break;
                }

                if (target is GameObject go && go.GetComponent<SkiRunLine>() != null)
                {
                    _previewDirty = true;
                    SceneView.RepaintAll();
                    break;
                }
            }

            return modifications;
        }

        // Cached preview buffers
        private readonly List<Vector3> _prevCenters = new();
        private readonly List<Vector3> _prevLeft = new();
        private readonly List<Vector3> _prevRight = new();

        private readonly List<float> _prevDistances = new List<float>(512);
        private readonly List<float> _ovDistances = new List<float>(512);

        // Gate editing state
        private enum GateSide { Center, Left, Right }
        private bool _gateConsumedThisEvent = false;

        [SerializeField] private bool _showGateHandles = true;
        [SerializeField] private bool _gateEditAffectsNeighbors = false;
        [SerializeField] private int _gateNeighborPairs = 1;
        [SerializeField] private float _gateNeighborRadiusMeters = 25f;
        [SerializeField] private float _gateAddSlackMeters = 2.0f;

        [SerializeField] private bool _showIntersectedRunCorridor = true;
        [SerializeField] private float _intersectedRunPreviewRadiusMeters = 50f;

        private enum FlagPreviewSet { Primary, Overlay }
        private FlagPreviewSet _selectedSet = FlagPreviewSet.Primary;
        private int _selectedPairIndex = -1; // index in the relevant preview list
        private int _selectedSide = 0;       // 0 = left, 1 = right

        // --- Overlap segment selection (editor-only) ---
        // Selected overlap refers to the OTHER run whose corridor overlaps this run, and the index range
        // (in the currently-used overlay preview arrays) where the overlap occurs.
        private SkiRunLine _selectedOverlapOtherRun = null;
        private int _selectedOverlapStartIndex = -1; // inclusive sample index
        private int _selectedOverlapEndIndex = -1;   // inclusive sample index
                                                     // Optional selection metadata (editor-only).

        private bool HasSelectedOverlap =>
            _selectedOverlapOtherRun != null &&
            _selectedOverlapStartIndex >= 0 &&
            _selectedOverlapEndIndex >= _selectedOverlapStartIndex;

        private struct OverlapSeg
        {
            public SkiRunLine other;
            public int start; // inclusive sample index
            public int end;   // inclusive sample index
            public int mid;   // representative sample index
            public int hiddenCount;
            public int visibleCount;
        }

        // Reused scratch list to avoid allocations in SceneGUI.
        private readonly List<OverlapSeg> _tmpOverlapSegs = new List<OverlapSeg>(32);
        // Reused scratch list for ribbon polyline rendering.
        private readonly List<Vector3> _tmpRibbonPts = new List<Vector3>(256);

        // --- Scene label overlap avoidance (editor-only) ---
        // We draw some overlap/intersection diagnostics using GUI labels rather than Handles.Label so we can
        // resolve overlaps in *screen space* (Handles.Label has no built-in layout/packing).
        private readonly List<Rect> _sceneLabelRects = new List<Rect>(96);


        private void DrawWorldLabelPacked(Vector3 worldPos, string text, GUIStyle style, float yWorldOffset = 0f, float yStepPixels = 2f)
        {
            if (string.IsNullOrEmpty(text) || style == null) return;
            // Only draw GUI labels during repaint; avoids unnecessary work and keeps layout stable.
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            Vector3 wp = worldPos + Vector3.up * yWorldOffset;
            Vector2 gui = HandleUtility.WorldToGUIPoint(wp);
            Vector2 size = style.CalcSize(new GUIContent(text));

            // Centered above the anchor point by default.
            Rect r = new Rect(gui.x - size.x * 0.5f, gui.y - size.y, size.x, size.y);

            // Try nudging upward in screen-space until we find a free slot.
            // (We keep it simple; worst-case we accept overlap after a small number of attempts.)
            const int kMaxAttempts = 16;
            for (int attempt = 0; attempt < kMaxAttempts; attempt++)
            {
                bool overlaps = false;
                for (int i = 0; i < _sceneLabelRects.Count; i++)
                {
                    if (_sceneLabelRects[i].Overlaps(r))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                    break;

                r.y -= (size.y + yStepPixels);
            }

            Handles.BeginGUI();
            GUI.Label(r, text, style);
            Handles.EndGUI();

            _sceneLabelRects.Add(r);
        }

        // Secondary preview buffers for intersection overlay (computed with overlap avoidance).
        private readonly List<Vector3> _ovCenters = new();
        private readonly List<Vector3> _ovLeft = new();
        private readonly List<Vector3> _ovRight = new();
        private readonly List<byte> _ovSpawnMask = new();
        private readonly List<SkiRunLine> _ovIntersectedRuns = new();
        private bool _overlayUsesPrimaryPreview;
        private readonly List<Vector3> _ovIntersectedClosest = new();
        private readonly List<Vector3> _ovIntersectedEdgeNormal = new();

        // Per-pair side validity mask:
        // bit0 = left valid, bit1 = right valid. (mask == 3 => both valid)
        private readonly List<byte> _prevSpawnMask = new();

        // Optional: which run was “intercepted” for each preview sample (same count as pairs).
        private readonly List<SkiRunLine> _intersectedRuns = new();

        // Optional: additional intersection debug info (same count as pairs).
        // Populated only for OUTSIDE->INSIDE (ENTRY) corner-gates.
        private readonly List<Vector3> _intersectedClosest = new();
        private readonly List<Vector3> _intersectedEdgeNormal = new();

        // Temp buffers used to draw intersected run corridors without per-frame allocations.
        private readonly HashSet<SkiRunLine> _tmpRunSet = new();
        private readonly Vector3[] _seg2 = new Vector3[2];

        // --- Authoring utilities ---
        private float resampleSpacingMeters = 12f;
        private float widthAnchorSpacingMeters = 30f;

        // Width visualisation density
        private bool widthVisualsOnlyAnchors = true;

        // --- Point gizmo clarity / declutter ---
        private bool depthTestPointGizmos = true;     // prevent drawing through terrain
        private bool fadePointsByDistance = true;     // fade points further from camera
        private float fadeNearMeters = 25f;           // fully visible at/near this distance
        private float fadeFarMeters = 250f;           // near-invisible at/after this distance
        private float fadeMinAlpha = 0.08f;           // alpha at fadeFarMeters

        // --- Inspector workflow cohesion ---
        [SerializeField] private int _workflowMode = 0; // 0=Points, 1=Gates, 2=Fences
        [SerializeField] private bool _foldFenceHolesList = false;

        private void SetWorkflowMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 2);
            _workflowMode = mode;

            // Keep these mutually exclusive so the scene experience matches the inspector.
            gateEditMode = (_workflowMode == 1);
            fenceHoleEditMode = (_workflowMode == 2);

            // In practice you almost always want point gizmos visible in all modes.
            showPointGizmos = true;

            if (gateEditMode)
                _showGateHandles = true;

            SceneView.RepaintAll();
        }

        private string GetWorkflowModeLabel()
        {
            if (gateEditMode) return "Gates";
            if (fenceHoleEditMode) return "Fences";
            return "Points";
        }

        public override void OnInspectorGUI()
        {
            var run = (SkiRunLine)target;
            if (run == null) return;

            serializedObject.Update();

            DrawRunOverviewCard(run);
            EditorGUILayout.Space(6);

            // Always visible: mode + minimal scene interaction state
            DrawAuthoringSection(run);
            EditorGUILayout.Space(6);

            // Always visible: identity + main geometry
            DrawRunIdentitySection();
            EditorGUILayout.Space(6);

            DrawGeometrySection();
            EditorGUILayout.Space(6);

            // Gated sections (only what matters for the active scene tool)
            if (_authoringMode == AuthoringMode.Path)
            {
                DrawPointEditingSection();
                EditorGUILayout.Space(6);
            }

            // Boundaries matter in all modes (they affect corridor + other tools)
            DrawBoundariesSection();
            EditorGUILayout.Space(6);

            if (_authoringMode == AuthoringMode.Flags)
            {
                DrawFlagsAndGatesSection(run);
                EditorGUILayout.Space(6);

                DrawIntersectionsSection();
                EditorGUILayout.Space(6);
            }

            if (_authoringMode == AuthoringMode.Fences)
            {
                DrawFencesSection(run);
                EditorGUILayout.Space(6);
            }

            // Utilities always visible
            DrawUtilitiesSection(run);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                _previewDirty = true;
        }

        private void DrawRunOverviewCard(SkiRunLine run)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Run Overview", EditorStyles.boldLabel);

                var pts = run.PointsWorld;
                int pointCount = pts != null ? pts.Count : 0;
                float len = (pts != null && pts.Count > 1) ? ComputePolylineLength(pts) : 0f;

                EditorGUILayout.LabelField("Points", pointCount.ToString());
                EditorGUILayout.LabelField("Approx Length (m)", len.ToString("0.0"));
                EditorGUILayout.LabelField("Run Width (m)", run.RunWidthMeters.ToString("0.0"));

                var spacingProp = serializedObject.FindProperty("flagSpacingMeters");
                if (spacingProp != null)
                    EditorGUILayout.LabelField("Flag Spacing (m)", spacingProp.floatValue.ToString("0.0"));

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Painter"))
                        SkiRunPainterWindow.OpenAndSelect(run);

                    if (GUILayout.Button("Frame"))
                    {
                        Selection.activeGameObject = run.gameObject;
                        EditorGUIUtility.PingObject(run.gameObject);
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(GetRunQuickStats(serializedObject), EditorStyles.miniLabel);
            }
        }

        private string GetRunQuickStats(SerializedObject so)
        {
            // Points (read via SerializedProperty so we don’t rely on access modifiers)
            var pPoints = so.FindProperty("pointsWorld");
            int points = pPoints != null ? pPoints.arraySize : 0;

            // Compute polyline length cheaply from pointsWorld (XZ+Y distance).
            // This avoids relying on any cached runtime metrics that may or may not exist.
            float length = 0f;
            if (pPoints != null && pPoints.arraySize >= 2)
            {
                Vector3 prev = pPoints.GetArrayElementAtIndex(0).vector3Value;
                for (int i = 1; i < pPoints.arraySize; i++)
                {
                    Vector3 cur = pPoints.GetArrayElementAtIndex(i).vector3Value;
                    length += Vector3.Distance(prev, cur);
                    prev = cur;
                }
            }

            // Flags count: we don’t assume a field name exists.
            // If you later want this, we can hook it to your actual generated flag list/parent.
            string flagsStr = "—";

            return $"Points: {points}   Length: {(length > 0.01f ? $"{length:0}m" : "—")}   Flags: {flagsStr}";
        }

        // ------------------------------------------------------------
        // Simplified Inspector Layout (no global Workflow/Scene/Advanced tabs)
        // ------------------------------------------------------------

        private bool _foldAuthoringAdvanced = false;
        private bool _foldIdentityAdvanced = false;
        private bool _foldGeometryAdvanced = false;

        private bool _foldPointsAdvanced = false;
        private bool _foldFlagsAdvanced = false;
        private bool _foldBoundariesAdvanced = false;
        private bool _foldIntersectionsAdvanced = false;
        private bool _foldFencesAdvanced = false;
        private bool _foldUtilitiesAdvanced = false;

        private void DrawAuthoringSection(SkiRunLine run)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Scene Workflow", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(GetAuthoringModeInspectorHelp(), MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawModeButton("Path (1)", AuthoringMode.Path, run);
                    DrawModeButton("Flags (2)", AuthoringMode.Flags, run);
                    DrawModeButton("Fences (3)", AuthoringMode.Fences, run);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Use the Scene overlay for live editing controls. Use this inspector for run settings and deeper authoring options.", EditorStyles.miniLabel);

                showHotkeyOverlay = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show hotkey overlay (H)", "Shows a compact scene overlay listing useful hotkeys."),
                    showHotkeyOverlay);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Painter"))
                        SkiRunPainterWindow.OpenAndSelect(run, enablePaint: _authoringMode == AuthoringMode.Path);

                    if (GUILayout.Button("Frame In Scene"))
                    {
                        Selection.activeGameObject = run.gameObject;
                        EditorGUIUtility.PingObject(run.gameObject);
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }

                    using (new EditorGUI.DisabledScope(_authoringMode != AuthoringMode.Flags))
                    {
                        if (GUILayout.Button("Rebuild Flags"))
                        {
                            Undo.RecordObject(run, "Rebuild Run Flags");
                            run.RebuildFlags();
                            run.ApplyColorToGeneratedFlags();
                            EditorUtility.SetDirty(run);
                            _previewDirty = true;
                            SceneView.RepaintAll();
                        }
                    }
                }

                _foldAuthoringAdvanced = EditorGUILayout.Foldout(_foldAuthoringAdvanced, "Advanced", true);
                if (_foldAuthoringAdvanced)
                {
                    EditorGUI.indentLevel++;

                    sceneVisualYOffset = EditorGUILayout.Slider(
                        new GUIContent("Scene Visual Y Offset", "Editor-only: lifts handles/gizmos above terrain for readability."),
                        sceneVisualYOffset, 0f, 2f);

                    previewMaxPairs = EditorGUILayout.IntSlider(
                        new GUIContent("Preview Density Limit", "Limits how many sampled preview elements are drawn in the scene for performance."),
                        previewMaxPairs, 20, 5000);

                    previewIncludeOverlapAvoidance = EditorGUILayout.ToggleLeft(
                        new GUIContent("Preview includes overlap avoidance", "If enabled, the scene preview applies corridor overlap avoidance (slower)."),
                        previewIncludeOverlapAvoidance);

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawModeButton(string label, AuthoringMode mode, SkiRunLine run)
        {
            bool isActive = _authoringMode == mode;
            using (new EditorGUI.DisabledScope(isActive))
            {
                if (GUILayout.Button(label, GUILayout.Height(24f)))
                    SetAuthoringMode(mode, run);
            }
        }

        private string GetAuthoringModeInspectorHelp()
        {
            switch (_authoringMode)
            {
                case AuthoringMode.Flags:
                    return "Flags mode: tune gate placement and preview corridor behaviour. Use the Scene overlay for gate editing and visibility toggles.";
                case AuthoringMode.Fences:
                    return "Fences mode: preview run edges and edit fence gaps. Use the Scene overlay to control fence editing affordances.";
                default:
                    return "Path mode: shape the run by selecting, moving, inserting, and deleting points. Open the painter when you want fast terrain-click authoring.";
            }
        }

        private void DrawRunIdentitySection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Identity & Difficulty", EditorStyles.boldLabel);

                TryDrawProp("runName");
                TryDrawProp("difficultyProfile");

                var pOverride = serializedObject.FindProperty("overrideDifficulty");
                if (pOverride != null)
                {
                    EditorGUILayout.PropertyField(pOverride);
                    if (pOverride.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        TryDrawProp("overrideDifficultyValue");
                        EditorGUI.indentLevel--;
                    }
                }

                _foldIdentityAdvanced = EditorGUILayout.Foldout(_foldIdentityAdvanced, "Advanced", true);
                if (_foldIdentityAdvanced)
                {
                    EditorGUI.indentLevel++;
                    TryDrawProp("classifyPerSegment");
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawGeometrySection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);

                TryDrawProp("runWidthMeters");

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Clear Width Overrides", GUILayout.Width(180)))
                    {
                        var run = (SkiRunLine)target;
                        Undo.RecordObject(run, "Clear Width Overrides");
                        run.ClearWidthOverrides();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }

                _foldGeometryAdvanced = EditorGUILayout.Foldout(_foldGeometryAdvanced, "Advanced", true);
                if (_foldGeometryAdvanced)
                {
                    EditorGUI.indentLevel++;
                    TryDrawProp("widthOverrideMeters");

                    showWidthVisuals = EditorGUILayout.ToggleLeft(
                        new GUIContent("Show width visuals (scene)", "Draws width ticks along the run (editor-only)."),
                        showWidthVisuals);

                    widthVisualsOnlyAnchors = EditorGUILayout.ToggleLeft(
                        new GUIContent("Width visuals: anchors only", "If enabled, draws fewer width ticks for clarity/performance."),
                        widthVisualsOnlyAnchors);

                    widthAnchorSpacingMeters = EditorGUILayout.Slider(
                        new GUIContent("Width Anchor Spacing (m)", "Distance between width anchors when \"anchors only\" is enabled."),
                        widthAnchorSpacingMeters, 5f, 150f);

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawPointEditingSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);

                showPointGizmos = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show point gizmos (scene)", "Draws selectable point handles in the scene."),
                    showPointGizmos);

                enableShiftClickInsert = EditorGUILayout.ToggleLeft(
                    new GUIContent("Shift+Click: insert point", "Insert a point into the polyline at the closest segment (supports start/end)."),
                    enableShiftClickInsert);

                enableAltClickDelete = EditorGUILayout.ToggleLeft(
                    new GUIContent("Alt+Click: delete point", "Deletes the selected point (or the clicked point if supported by the current selection)."),
                    enableAltClickDelete);

                autoSnapToTerrainWhenEditing = EditorGUILayout.ToggleLeft(
                    new GUIContent("Snap moved points to terrain", "When moving points, snap them to the terrain height."),
                    autoSnapToTerrainWhenEditing);

                _foldPointsAdvanced = EditorGUILayout.Foldout(_foldPointsAdvanced, "Advanced", true);
                if (_foldPointsAdvanced)
                {
                    EditorGUI.indentLevel++;

                    editNeighborPoints = EditorGUILayout.ToggleLeft(
                        new GUIContent("Edit neighbor points", "Shows small neighbor handles to smooth kinks while editing."),
                        editNeighborPoints);

                    pointPickSizeScale = EditorGUILayout.Slider(
                        new GUIContent("Point handle size", "Scales the clickable size of point handles."),
                        pointPickSizeScale, 0.02f, 0.20f);

                    depthTestPointGizmos = EditorGUILayout.ToggleLeft(
                        new GUIContent("Depth test point gizmos", "If enabled, point handles won’t draw through terrain."),
                        depthTestPointGizmos);

                    fadePointsByDistance = EditorGUILayout.ToggleLeft(
                        new GUIContent("Fade point gizmos by distance", "Fades point handles when far from the scene camera."),
                        fadePointsByDistance);

                    using (new EditorGUI.DisabledScope(!fadePointsByDistance))
                    {
                        fadeNearMeters = EditorGUILayout.Slider(new GUIContent("Fade Near (m)"), fadeNearMeters, 1f, 200f);
                        fadeFarMeters = EditorGUILayout.Slider(new GUIContent("Fade Far (m)"), fadeFarMeters, 10f, 1000f);
                        fadeMinAlpha = EditorGUILayout.Slider(new GUIContent("Fade Min Alpha"), fadeMinAlpha, 0.01f, 0.5f);
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawFlagsAndGatesSection(SkiRunLine run)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Flags & Gates", EditorStyles.boldLabel);

                TryDrawProp("flagPrefab");
                TryDrawProp("flagSpacingMeters");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild Flags"))
                    {
                        Undo.RecordObject(run, "Rebuild Run Flags");
                        run.RebuildFlags();
                        run.ApplyColorToGeneratedFlags();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Bake Metrics"))
                    {
                        Undo.RecordObject(run, "Bake Run Metrics");
                        run.BakeMetrics();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }

                showPairPreview = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show gate pair preview (scene)", "Draws sampled flag pairs/crossbars in the scene."),
                    showPairPreview);

                showSpacingHandle = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show spacing handle (scene)", "Shows a drag handle near the start to adjust spacing quickly."),
                    showSpacingHandle);

                bool newGateEdit = EditorGUILayout.ToggleLeft(
                    new GUIContent("Enable gate editing (G)", "Enables interactive gate-width handles in the scene. Hotkey: G"),
                    gateEditMode);

                if (newGateEdit != gateEditMode)
                {
                    gateEditMode = newGateEdit;
                    if (gateEditMode && _authoringMode != AuthoringMode.Flags)
                        SetAuthoringMode(AuthoringMode.Flags, run);
                    else
                        SaveEditorPrefs();
                }

                _foldFlagsAdvanced = EditorGUILayout.Foldout(_foldFlagsAdvanced, "Advanced", true);
                if (_foldFlagsAdvanced)
                {
                    EditorGUI.indentLevel++;

                    TryDrawProp("flagStartOffsetMeters");
                    TryDrawProp("flagEndInsetMeters");
                    TryDrawProp("flagHeightOffset");
                    TryDrawProp("snapSidesToTerrainIndividually");
                    TryDrawProp("faceInwards");
                    TryDrawProp("faceDownhill");

                    var pAvoid = serializedObject.FindProperty("avoidOtherRunsWhenPlacingFlags");
                    if (pAvoid != null)
                    {
                        EditorGUILayout.PropertyField(pAvoid, new GUIContent(
                            "Avoid other runs (auto placement)",
                            "When enabled, auto-placed flags will try to avoid other run corridors. Manual flags are always respected."));

                        if (pAvoid.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            TryDrawProp("avoidRunClearanceMeters");
                            TryDrawProp("avoidRunVerticalToleranceMeters");
                            TryDrawProp("avoidRunMaxExtraSearchMeters");
                            TryDrawProp("avoidRunUseBroadphaseBounds");
                            EditorGUI.indentLevel--;
                        }
                    }

                    _showGateHandles = EditorGUILayout.ToggleLeft(
                        new GUIContent("Show gate handles (scene)", "Shows width handles when gate editing is enabled."),
                        _showGateHandles);

                    _gateEditAffectsNeighbors = EditorGUILayout.ToggleLeft(
                        new GUIContent("Edits affect neighbors", "Applies width changes to nearby pairs as well."),
                        _gateEditAffectsNeighbors);

                    if (_gateEditAffectsNeighbors)
                    {
                        _gateNeighborPairs = EditorGUILayout.IntSlider(
                            new GUIContent("Neighbor Pairs", "Max number of pairs on each side to affect."),
                            _gateNeighborPairs, 0, 8);

                        _gateNeighborRadiusMeters = EditorGUILayout.Slider(
                            new GUIContent("Neighbor Radius (m)", "Applies width changes within this distance window."),
                            _gateNeighborRadiusMeters, 1f, 150f);
                    }

                    _gateAddSlackMeters = EditorGUILayout.Slider(
                        new GUIContent("Add slack (m)", "Extra slack applied when widening gates to reduce snapping."),
                        _gateAddSlackMeters, 0f, 10f);

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawBoundariesSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Boundaries", EditorStyles.boldLabel);

                TryDrawProp("terrainAwareBoundaries");
                TryDrawProp("boundaryMaxSlopeDeg");
                TryDrawProp("boundaryMaxHeightDelta");

                showBoundaryPreview = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show corridor preview (scene)", "Draws the left/right corridor boundary lines."),
                    showBoundaryPreview);

                _foldBoundariesAdvanced = EditorGUILayout.Foldout(_foldBoundariesAdvanced, "Advanced", true);
                if (_foldBoundariesAdvanced)
                {
                    EditorGUI.indentLevel++;
                    TryDrawProp("boundarySearchStepMeters");
                    TryDrawProp("boundaryPreferFurthestValid");
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawIntersectionsSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Intersections & Overlaps", EditorStyles.boldLabel);

                showIntersectionOverlay = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show intersection overlay (scene)", "Draws entry/exit and overlap diagnostics for intersections."),
                    showIntersectionOverlay);

                _showIntersectedRunCorridor = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show intersected run corridors (scene)", "When intersections are shown, also draws nearby run corridors."),
                    _showIntersectedRunCorridor);

                _foldIntersectionsAdvanced = EditorGUILayout.Foldout(_foldIntersectionsAdvanced, "Advanced", true);
                if (_foldIntersectionsAdvanced)
                {
                    EditorGUI.indentLevel++;
                    _intersectedRunPreviewRadiusMeters = EditorGUILayout.Slider(
                        new GUIContent("Preview Radius (m)", "Radius around the camera/selection used to gather intersected runs for preview."),
                        _intersectedRunPreviewRadiusMeters, 10f, 250f);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawFencesSection(SkiRunLine run)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Fences", EditorStyles.boldLabel);

                var leftEdgeProp = serializedObject.FindProperty("fenceUseLeftEdge");
                var rightEdgeProp = serializedObject.FindProperty("fenceUseRightEdge");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Fence Sides", GUILayout.Width(90));
                    if (leftEdgeProp != null) leftEdgeProp.boolValue = GUILayout.Toggle(leftEdgeProp.boolValue, "Left Edge", "Button");
                    if (rightEdgeProp != null) rightEdgeProp.boolValue = GUILayout.Toggle(rightEdgeProp.boolValue, "Right Edge", "Button");
                }

                TryDrawProp("fenceSegmentPrefab");
                TryDrawProp("fenceCornerPostPrefab");
                TryDrawProp("fencePointSpacingMeters");

                bool newFenceEdit = EditorGUILayout.ToggleLeft(
                    new GUIContent("Enable fence hole editing (scene)", "Shift+Click adds a hole. Click to select. Drag endpoints. Delete removes selected."),
                    fenceHoleEditMode);

                if (newFenceEdit != fenceHoleEditMode)
                {
                    fenceHoleEditMode = newFenceEdit;
                    if (fenceHoleEditMode && _authoringMode != AuthoringMode.Fences)
                        SetAuthoringMode(AuthoringMode.Fences, run);
                    else
                        SaveEditorPrefs();
                }

                fenceAddSide = (SkiRunLine.FenceSide)EditorGUILayout.EnumPopup(
                    new GUIContent("New Hole Side", "Which fence side the next hole will be created on."),
                    fenceAddSide);

                fenceAddHoleLengthMeters = EditorGUILayout.Slider(
                    new GUIContent("New Hole Length (m)", "Default length for newly-added holes."),
                    fenceAddHoleLengthMeters, 1f, 80f);

                _foldFencesAdvanced = EditorGUILayout.Foldout(_foldFencesAdvanced, "Advanced", true);
                if (_foldFencesAdvanced)
                {
                    EditorGUI.indentLevel++;

                    TryDrawProp("fenceUseSmoothSampling");
                    TryDrawProp("fenceSmoothSamplesPerMeter");
                    TryDrawProp("fenceSmoothMinSamplesPerSpan");

                    TryDrawProp("fenceAutoExcludeAroundGates");
                    TryDrawProp("fenceGateExclusionRadiusMeters");
                    TryDrawProp("fenceExclusionPaddingMeters");
                    TryDrawProp("fenceConformTerrainMask");

                    TryDrawProp("fenceBoundarySmoothPasses");
                    TryDrawProp("fenceBoundarySmoothStrength");

                    var holesProp = serializedObject.FindProperty("fenceExcludeHoles");
                    _foldFenceHolesList = EditorGUILayout.Foldout(_foldFenceHolesList, "Holes List (debug/management)", true);
                    if (_foldFenceHolesList && holesProp != null)
                        EditorGUILayout.PropertyField(holesProp, true);

                    EditorGUI.indentLevel--;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clear Holes"))
                    {
                        Undo.RecordObject(target, "Clear Fence Holes");
                        var holesProp = serializedObject.FindProperty("fenceExcludeHoles");
                        if (holesProp != null)
                        {
                            holesProp.ClearArray();
                            serializedObject.ApplyModifiedProperties();
                        }
                        EditorUtility.SetDirty(target);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Clear Generated"))
                    {
                        Undo.RecordObject(target, "Clear Generated Fences");
                        run.ClearGeneratedFences();
                        EditorUtility.SetDirty(target);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void DrawUtilitiesSection(SkiRunLine run)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Bake Metrics"))
                    {
                        Undo.RecordObject(run, "Bake Run Metrics");
                        run.BakeMetrics();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Rebuild Flags"))
                    {
                        Undo.RecordObject(run, "Rebuild Run Flags");
                        run.RebuildFlags();
                        run.ApplyColorToGeneratedFlags();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Rebuild Fences"))
                    {
                        Undo.RecordObject(run, "Rebuild Fences");
                        run.RebuildFences();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }

                _foldUtilitiesAdvanced = EditorGUILayout.Foldout(_foldUtilitiesAdvanced, "Advanced", true);
                if (_foldUtilitiesAdvanced)
                {
                    EditorGUI.indentLevel++;

                    resampleSpacingMeters = EditorGUILayout.Slider(
                        new GUIContent("Resample Spacing (m)", "Resamples the current polyline to evenly spaced points."),
                        resampleSpacingMeters, 1f, 50f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Resample Points"))
                        {
                            Undo.RecordObject(run, "Resample Run Points");
                            run.ResamplePointsWorld(resampleSpacingMeters);
                            EditorUtility.SetDirty(run);
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("Clear Width Overrides"))
                        {
                            Undo.RecordObject(run, "Clear Width Overrides");
                            run.ClearWidthOverrides();
                            EditorUtility.SetDirty(run);
                            SceneView.RepaintAll();
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private bool TryDrawProp(string propertyName)
        {
            var p = serializedObject.FindProperty(propertyName);
            if (p == null) return false;
            EditorGUILayout.PropertyField(p, true);
            return true;
        }

        private static float ComputePolylineLength(IReadOnlyList<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;

            float sum = 0f;
            for (int i = 1; i < pts.Count; i++)
                sum += Vector3.Distance(pts[i - 1], pts[i]);

            return sum;
        }

        private void OnSceneGUI()
        {
            var run = (SkiRunLine)target;

            Event e = Event.current;

            // Hotkeys + overlay (runs even if we early-return later)
            HandleSceneHotkeys(run);


            // Escape clears BOTH point + gate selections
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                selectedPointIndex = -1;
                _selectedPairIndex = -1;
                _selectedSide = 0;
                _selectedSet = FlagPreviewSet.Primary;
                _selectedOverlapOtherRun = null;
                _selectedOverlapStartIndex = -1;
                _selectedOverlapEndIndex = -1;
                _selectedFenceHoleIndex = -1;

                e.Use();
                SceneView.RepaintAll();
                return;
            }

            _gateConsumedThisEvent = false;

            // Other-run preview caches are rebuilt at most once per SceneGUI pass.
            // Clear the per-pass set so the cache can refresh as you edit.
            _otherRunPreviewBuiltThisPass.Clear();

            // IMPORTANT: reuse the Editor's serializedObject; allocating a new SerializedObject
            // every OnSceneGUI call becomes expensive on large scenes.
            serializedObject.UpdateIfRequiredOrScript();
            SerializedObject so = serializedObject;
            SerializedProperty pointsProp = so.FindProperty("pointsWorld");
            SerializedProperty widthsProp = so.FindProperty("widthOverrideMeters");

            // Prevent label-overlap bookkeeping from growing unbounded over time.
            _sceneLabelRects.Clear();

            if (pointsProp == null || widthsProp == null) return;
            if (pointsProp.arraySize < 1) return;

            // Keep widths list aligned with points list (defensive)
            if (widthsProp.arraySize != pointsProp.arraySize)
            {
                int old = widthsProp.arraySize;
                widthsProp.arraySize = pointsProp.arraySize;

                for (int i = old; i < widthsProp.arraySize; i++)
                    widthsProp.GetArrayElementAtIndex(i).floatValue = -1f;

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Mark preview dirty if points changed via Undo/Redo or external tools (PainterWindow, etc.)
            int hash = ComputePointsHash(pointsProp);
            if (hash != _lastPointsHash)
            {
                _lastPointsHash = hash;
                _previewDirty = true;
            }

            // Point editing is disabled while Gate Edit Mode is active (prevents Shift/Alt conflicts)
            if (_authoringMode == AuthoringMode.Path && !gateEditMode && !_gateConsumedThisEvent)
                HandleInsertDelete(run, so, pointsProp, widthsProp);

            // Draw previews first (so point gizmos sit on top)
            // PERF: only rebuild preview when something changed, and throttle rebuild frequency.
            if (showBoundaryPreview || showPairPreview)
            {
                double now = EditorApplication.timeSinceStartup;
                if (_previewDirty && (now - _lastPreviewBuildTime) >= PreviewRebuildMinInterval)
                {
                    BuildPreview(run, so, pointsProp, widthsProp, previewIncludeOverlapAvoidance);
                    _previewDirty = false;
                    _lastPreviewBuildTime = now;
                }
            }

            if (showBoundaryPreview)
                DrawBoundaryPreview(run);

            if (showPairPreview)
            {
                DrawPairPreview(run);

                if (_authoringMode == AuthoringMode.Flags && gateEditMode)
                    HandleGateEditing(run);
            }

            if (showSpacingHandle)
                DrawSpacingHandle(run, so, pointsProp);

            // Clickable point gizmos (selection), always cheap.
            if (showPointGizmos)
                DrawPointGizmosAndSelection(run, so, pointsProp, widthsProp);

            // Width visuals persistently (anchors only), no slider unless selected.
            if (showWidthVisuals)
                DrawWidthVisuals(run, so, pointsProp, widthsProp);

            // Contextual point editing is disabled in Gate Edit Mode (keeps the workflow clean and predictable)
            if (_authoringMode == AuthoringMode.Path && !gateEditMode)
                DrawContextualEditHandles(run, so, pointsProp, widthsProp);

            // Gate context menu only makes sense in Gate Edit Mode
            if (gateEditMode)
                HandleFlagOverrideContextMenu(run);

            // Fence spans / holes (only active in their modes; does not interfere with gates unless you Shift+Click)
            if (_authoringMode == AuthoringMode.Fences)
            {
                DrawFenceBoundaryPreviewForFenceMode(run);
                HandleFenceHoleSceneEditing(run);
            }

        }

        private void DrawPointGizmosAndSelection(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            Event e = Event.current;
            if (e == null) return;

            // Avoid stealing clicks from insert/delete modes
            bool blockSelect =
                gateEditMode ||
                (enableShiftClickInsert && e.shift) ||
                (enableAltClickDelete && e.alt);

            // SceneView camera for distance fade
            Camera cam = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

            // Depth test so points don't draw through terrain
            CompareFunction prevZ = Handles.zTest;
            Handles.zTest = depthTestPointGizmos ? CompareFunction.LessEqual : CompareFunction.Always;

            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;

                // PERF: pointsWorld is already authored/snapped; don't resample terrain per point just to draw gizmos.
                Vector3 ps = p;
                ps.y += sceneVisualYOffset;

                float baseSize = HandleUtility.GetHandleSize(ps) * pointPickSizeScale;

                bool isEndpoint = (i == 0 || i == pointsProp.arraySize - 1);
                float sizeMul = isEndpoint ? 1.35f : 1.0f;
                float buttonSize = baseSize * sizeMul;

                float overrideW = widthsProp.GetArrayElementAtIndex(i).floatValue;
                bool hasOverride = overrideW > 0.01f;
                bool isSelected = (i == selectedPointIndex);

                // Distance-based alpha fade (selected remains fully opaque)
                float alphaMul = 1f;
                if (!isSelected && fadePointsByDistance && cam != null)
                {
                    float d = Vector3.Distance(camPos, ps);
                    float tFade = Mathf.InverseLerp(fadeNearMeters, fadeFarMeters, d);
                    alphaMul = Mathf.Lerp(1f, Mathf.Clamp01(fadeMinAlpha), Mathf.Clamp01(tFade));
                }

                // Color coding: selected > override > default
                Color c =
                    isSelected ? new Color(0.2f, 0.9f, 1f, 1f) :
                    hasOverride ? new Color(1f, 0.8f, 0.2f, 0.95f) :
                    new Color(1f, 1f, 1f, 0.65f);

                c.a *= alphaMul;

                // If fully faded, skip drawing + picking (keeps near points easy to click)
                if (c.a <= 0.02f)
                    continue;

                Handles.color = c;

                // Button makes it clickable without showing full transform controls
                if (!blockSelect && Handles.Button(ps, Quaternion.identity, buttonSize, buttonSize * 1.2f, Handles.DotHandleCap))
                {

                    selectedPointIndex = i;
                    SceneView.RepaintAll();

                }

            }

            Handles.zTest = prevZ;
        }

        private void DrawWidthVisuals(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            SerializedProperty runWidthProp = so.FindProperty("runWidthMeters");
            if (runWidthProp == null) return;

            float globalWidth = Mathf.Max(2f, runWidthProp.floatValue);

            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;

                Terrain tPoint = ResolveTerrainAt(p);
                Vector3 pSnapped = SnapToTerrain(tPoint, p) + Vector3.up * sceneVisualYOffset;

                // Tangent estimate from neighbors
                Vector3 tangent = EstimateTangent(pointsProp, i);
                Vector3 up = SampleNormal(tPoint, pSnapped);

                Vector3 lateral = Vector3.Cross(up, tangent);
                if (lateral.sqrMagnitude < 0.0001f)
                    lateral = Vector3.Cross(Vector3.up, tangent);
                if (lateral.sqrMagnitude < 0.0001f)
                    lateral = Vector3.right;
                lateral.Normalize();

                float overrideW = widthsProp.GetArrayElementAtIndex(i).floatValue;
                bool hasOverride = overrideW > 0.01f;

                bool isAnchor = hasOverride || !widthVisualsOnlyAnchors || IsAnchorIndex(pointsProp, i, widthAnchorSpacingMeters);
                bool isSelected = (i == selectedPointIndex);

                // Always show selected, otherwise respect anchor mode
                if (!isSelected && !isAnchor)
                    continue;

                float w = hasOverride ? overrideW : globalWidth;
                float halfW = Mathf.Max(0.5f, w * 0.5f);

                Vector3 tickEnd = pSnapped + lateral * halfW;

                // Visual only: line + cap
                Handles.color = isSelected
                    ? new Color(0.2f, 0.9f, 1f, 1f)
                    : (hasOverride ? new Color(1f, 0.8f, 0.2f, 0.65f) : new Color(1f, 1f, 1f, 0.35f));

                Handles.DrawLine(pSnapped, tickEnd);
                float capSize = HandleUtility.GetHandleSize(tickEnd) * 0.04f;
                Handles.SphereHandleCap(0, tickEnd, Quaternion.identity, capSize, EventType.Repaint);
            }
        }

        private void DrawContextualEditHandles(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            if (selectedPointIndex < 0 || selectedPointIndex >= pointsProp.arraySize)
                return;

            // Allow editing selected (and optional neighbors) only
            DrawMoveHandleForPoint(run, so, pointsProp, widthsProp, selectedPointIndex, isNeighbor: false);

            if (editNeighborPoints)
            {
                int prev = selectedPointIndex - 1;
                int next = selectedPointIndex + 1;

                if (prev >= 0) DrawMoveHandleForPoint(run, so, pointsProp, widthsProp, prev, isNeighbor: true);
                if (next < pointsProp.arraySize) DrawMoveHandleForPoint(run, so, pointsProp, widthsProp, next, isNeighbor: true);
            }

            // Selected-only width slider handle (optional but recommended)
            DrawWidthSliderForSelected(run, so, pointsProp, widthsProp, selectedPointIndex);
        }

        private void DrawMoveHandleForPoint(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp, int i, bool isNeighbor)
        {
            Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;

            Terrain tPoint = ResolveTerrainAt(p);
            Vector3 pSnapped = SnapToTerrain(tPoint, p) + Vector3.up * sceneVisualYOffset;

            // Use a simpler handle for neighbors to reduce clutter
            float size = HandleUtility.GetHandleSize(pSnapped) * (isNeighbor ? 0.12f : 0.18f);

            Handles.color = isNeighbor ? new Color(1f, 1f, 1f, 0.55f) : new Color(0.2f, 0.9f, 1f, 1f);

            EditorGUI.BeginChangeCheck();

            Vector3 newPos;
            if (isNeighbor)
            {
                newPos = Handles.FreeMoveHandle(pSnapped, size, Vector3.zero, Handles.SphereHandleCap);
            }
            else
            {
                newPos = Handles.PositionHandle(pSnapped, Quaternion.identity);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(run, "Move Run Point");

                // Keep authored points stable: store terrain-snapped position (no artificial +Y offset).
                Terrain tNew = ResolveTerrainAt(newPos, preferred: tPoint);
                Vector3 stored = SnapToTerrain(tNew, newPos);
                pointsProp.GetArrayElementAtIndex(i).vector3Value = stored;

                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(run);
                _previewDirty = true;

                SceneView.RepaintAll();
            }
        }

        private void DrawWidthSliderForSelected(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp, int i)
        {
            SerializedProperty runWidthProp = so.FindProperty("runWidthMeters");
            if (runWidthProp == null) return;

            Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;
            Terrain tPoint = ResolveTerrainAt(p);
            Vector3 pSnapped = SnapToTerrain(tPoint, p) + Vector3.up * sceneVisualYOffset;

            float globalWidth = Mathf.Max(2f, runWidthProp.floatValue);

            Vector3 tangent = EstimateTangent(pointsProp, i);
            Vector3 up = SampleNormal(tPoint, pSnapped);

            Vector3 lateral = Vector3.Cross(up, tangent);
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.Cross(Vector3.up, tangent);
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.right;
            lateral.Normalize();

            float overrideW = widthsProp.GetArrayElementAtIndex(i).floatValue;
            float w = (overrideW > 0.01f) ? overrideW : globalWidth;
            float halfW = Mathf.Max(0.5f, w * 0.5f);

            Vector3 handlePos = pSnapped + lateral * halfW;

            Handles.color = new Color(0.2f, 0.9f, 1f, 1f);
            Handles.DrawLine(pSnapped, handlePos);

            float hSize = HandleUtility.GetHandleSize(handlePos) * 0.14f;

            EditorGUI.BeginChangeCheck();
            Vector3 newHandlePos = Handles.Slider(handlePos, lateral, hSize, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float newHalf = Vector3.Dot(newHandlePos - pSnapped, lateral);
                float newW = Mathf.Clamp(newHalf * 2f, 2f, 200f);

                Undo.RecordObject(run, "Adjust Run Width Override");

                if (newW < 2.1f)
                    widthsProp.GetArrayElementAtIndex(i).floatValue = -1f;
                else
                    widthsProp.GetArrayElementAtIndex(i).floatValue = newW;

                so.ApplyModifiedProperties();
                _previewDirty = true;

                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            }

            Handles.color = Color.white;
            string label = (overrideW > 0.01f) ? $"W: {overrideW:0.0}m" : $"W: {w:0.0}m (global)";
            Handles.Label(handlePos + Vector3.up * HandleUtility.GetHandleSize(handlePos) * 0.04f, label);
        }

        private static Vector3 EstimateTangent(SerializedProperty pointsProp, int i)
        {
            Vector3 tangent = Vector3.forward;

            if (pointsProp.arraySize >= 2)
            {
                if (i == 0)
                    tangent = pointsProp.GetArrayElementAtIndex(1).vector3Value - pointsProp.GetArrayElementAtIndex(0).vector3Value;
                else if (i == pointsProp.arraySize - 1)
                    tangent = pointsProp.GetArrayElementAtIndex(i).vector3Value - pointsProp.GetArrayElementAtIndex(i - 1).vector3Value;
                else
                    tangent = pointsProp.GetArrayElementAtIndex(i + 1).vector3Value - pointsProp.GetArrayElementAtIndex(i - 1).vector3Value;

                if (tangent.sqrMagnitude > 0.000001f) tangent.Normalize();
                else tangent = Vector3.forward;
            }

            return tangent;
        }

        private void HandleInsertDelete(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            Event e = Event.current;
            if (e == null) return;

            // Shift-click insert point (raycast to TerrainCollider)
            if (enableShiftClickInsert &&
                e.type == EventType.MouseDown &&
                e.button == 0 &&
                e.shift && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50000f))
                {
                    if (!(hit.collider is TerrainCollider))
                        return;

                    Vector3 hitPos = hit.point;

                    if (pointsProp.arraySize >= 0)
                    {
                        // Use the same append-biased smart insertion behaviour as the painter:
                        // extending the run should usually append, while intentional clicks
                        // inside the run can still insert between existing points.
                        Undo.RecordObject(run, "Insert Run Point");
                        int insertedIndex = run.InsertPointWorldSmart(hitPos);
                        selectedPointIndex = insertedIndex;

                        // Sync serialized view.
                        so.Update();
                        so.ApplyModifiedProperties();

                        _previewDirty = true;
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                        e.Use();
                    }
                }
            }

            // Alt-click delete point (we do a proximity test against points in screen space)
            if (enableAltClickDelete &&
                e.type == EventType.MouseDown &&
                e.button == 0 &&
                e.alt && !e.shift)
            {
                int idx = PickNearestPointIndex(pointsProp, e.mousePosition);
                if (idx >= 0)
                {
                    Undo.RecordObject(run, "Delete Run Point");

                    pointsProp.DeleteArrayElementAtIndex(idx);
                    widthsProp.DeleteArrayElementAtIndex(idx);

                    // Adjust selection
                    if (selectedPointIndex == idx) selectedPointIndex = -1;
                    else if (selectedPointIndex > idx) selectedPointIndex--;

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(run);
                    SceneView.RepaintAll();

                    e.Use();
                }
            }
        }

        private static int ComputePointsHash(SerializedProperty pointsProp)
        {
            unchecked
            {
                int h = 17;
                int n = pointsProp != null ? pointsProp.arraySize : 0;
                h = h * 31 + n;
                if (pointsProp != null && n > 0)
                {
                    int step = Mathf.Max(1, n / 8);
                    for (int i = 0; i < n; i += step)
                    {
                        Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;
                        h = h * 31 + p.GetHashCode();
                    }
                }
                return h;
            }
        }

        private int PickNearestPointIndex(SerializedProperty pointsProp, Vector2 mousePos)
        {
            if (pointsProp.arraySize == 0) return -1;

            float best = 20f; // pixels
            int bestIdx = -1;

            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;
                Terrain t = ResolveTerrainAt(p);
                Vector3 ps = SnapToTerrain(t, p);

                Vector2 gui = HandleUtility.WorldToGUIPoint(ps);
                float d = Vector2.Distance(gui, mousePos);
                if (d < best)
                {
                    best = d;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private int FindClosestSegmentIndex(SerializedProperty pointsProp, Vector3 worldPoint)
        {
            if (pointsProp.arraySize < 2) return -1;

            int best = -1;
            float bestDist = float.PositiveInfinity;

            Vector2 p = new Vector2(worldPoint.x, worldPoint.z);

            for (int i = 0; i < pointsProp.arraySize - 1; i++)
            {
                Vector3 a3 = pointsProp.GetArrayElementAtIndex(i).vector3Value;
                Vector3 b3 = pointsProp.GetArrayElementAtIndex(i + 1).vector3Value;

                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);

                float d = DistancePointToSegment2D(p, a, b);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private float DistancePointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLen2 = ab.sqrMagnitude;
            if (abLen2 < 0.000001f) return (p - a).magnitude;

            float t = Vector2.Dot(p - a, ab) / abLen2;
            t = Mathf.Clamp01(t);
            Vector2 proj = a + ab * t;
            return Vector2.Distance(p, proj);
        }

        private bool IsAnchorIndex(SerializedProperty pointsProp, int index, float spacingMeters)
        {
            if (pointsProp.arraySize < 2) return true;
            if (index == 0 || index == pointsProp.arraySize - 1) return true;

            spacingMeters = Mathf.Max(1f, spacingMeters);

            // Compute cumulative distance up to this point and snap to spacing grid.
            float dist = 0f;
            for (int i = 0; i < index; i++)
            {
                Vector3 a = pointsProp.GetArrayElementAtIndex(i).vector3Value;
                Vector3 b = pointsProp.GetArrayElementAtIndex(i + 1).vector3Value;
                dist += Vector3.Distance(a, b);
            }

            float nearest = Mathf.Round(dist / spacingMeters) * spacingMeters;
            return Mathf.Abs(dist - nearest) <= (spacingMeters * 0.25f);
        }

        private void BuildPreview(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp, bool includeOverlapAvoidance)
        {
            _prevCenters.Clear();
            _prevLeft.Clear();
            _prevRight.Clear();
            _prevSpawnMask.Clear();
            _intersectedRuns.Clear();
            _intersectedClosest.Clear();
            _intersectedEdgeNormal.Clear();

            if (run == null) return;
            if (pointsProp == null || pointsProp.arraySize < 2) return;

            _prevDistances.Clear();

            // PERF: overlap avoidance is optional in preview. When enabled, this can be expensive.
            run.GetFlagPairsPreview(
                _prevCenters, _prevLeft, _prevRight,
                _prevSpawnMask,
                _intersectedRuns,
                _intersectedClosest,
                _intersectedEdgeNormal,
                _prevDistances,
                previewMaxPairs,
                includeOverlapAvoidance);

            // Intersection overlay is always computed with overlap avoidance, but we keep the primary preview as-is.
            _overlayUsesPrimaryPreview = includeOverlapAvoidance || !showIntersectionOverlay;

            // PERF: the overlay preview is intentionally lower-density to keep long runs responsive.
            // It is only used for intersection ribbons/diagnostics (not the core boundary preview).
            if (showIntersectionOverlay && !includeOverlapAvoidance)
            {
                _ovCenters.Clear();
                _ovLeft.Clear();
                _ovRight.Clear();
                _ovSpawnMask.Clear();
                _ovIntersectedRuns.Clear();
                _ovDistances.Clear();
                _ovIntersectedClosest.Clear();
                _ovIntersectedEdgeNormal.Clear();

                int overlayMaxPairs = Mathf.Min(previewMaxPairs, 200);

                run.GetFlagPairsPreview(
                    _ovCenters, _ovLeft, _ovRight,
                    _ovSpawnMask,
                    _ovIntersectedRuns,
                    outOtherClosestOnRun: _ovIntersectedClosest,
                    outOtherEdgeNormalWorld: _ovIntersectedEdgeNormal,
                    outDistancesMeters: _ovDistances,
                    maxPairs: overlayMaxPairs,
                    includeOverlapAvoidance: true);

                _overlayUsesPrimaryPreview = false;
            }
        }

        private void DrawBoundaryPreview(SkiRunLine run)
        {
            if (run == null) return;
            if (_prevLeft.Count < 2 || _prevRight.Count < 2) return;

            Color c = run.RunColor;
            Color baseEdge = new Color(c.r, c.g, c.b, 0.55f);
            Color overlapEdge = new Color(1f, 0.75f, 0.1f, 1f);

            // Simplified intersection workflow:
            // Build the candidate set from nearby runs (NOT from the overlap-avoidance preview), so
            // intersection highlighting remains available even when previewIncludeOverlapAvoidance is off.
            if (showIntersectionOverlay)
                RebuildOtherRunSet(run, _tmpRunSet);
            else
                _tmpRunSet.Clear();

            bool highlightOverlaps = showIntersectionOverlay && _tmpRunSet.Count > 0;
            float thickness = highlightOverlaps ? 4f : 3f;
            float dottedSpacing = 4f;

            // 1) Selected run edges: dotted-highlight segments that lie inside another run corridor
            DrawEdgeSegments(run, _prevLeft, _prevSpawnMask, sideBit: 0b01,
                normalColor: baseEdge, overlapColor: overlapEdge,
                thickness: thickness, dottedSpacing: dottedSpacing,
                highlightOverlapSegments: highlightOverlaps, overlapRuns: _tmpRunSet);

            DrawEdgeSegments(run, _prevRight, _prevSpawnMask, sideBit: 0b10,
                normalColor: baseEdge, overlapColor: overlapEdge,
                thickness: thickness, dottedSpacing: dottedSpacing,
                highlightOverlapSegments: highlightOverlaps, overlapRuns: _tmpRunSet);

            // 2) Other run edges: draw the other corridor edges (dotted) where they pass through THIS corridor
            if (highlightOverlaps)
                DrawOtherRunOverlapEdgesWithinSelected(run, _tmpRunSet, dottedSpacing);
        }

        // ==========================================================================
        // Intersection Overlay (Scene View)
        // Goals:
        // 1) Use the same reliable FreeMove-based handles as the main gate editor.
        // 2) Visualise intersection classification (inside / crosses edge).
        // 3) Draw a "mirror" overlay on the other run so intersections are visible from both sides.
        // ==========================================================================

        private sealed class OtherRunPreviewCache
        {
            public readonly List<Vector3> centers = new();
            public readonly List<Vector3> left = new();
            public readonly List<Vector3> right = new();
            public readonly List<byte> mask = new();
            public readonly List<SkiRunLine> hitRuns = new();
            public readonly List<float> distances = new();
        }

        private readonly Dictionary<SkiRunLine, OtherRunPreviewCache> _otherRunPreviewCache = new();
        private readonly HashSet<SkiRunLine> _otherRunPreviewBuiltThisPass = new();


        private void DrawEdgeSegments(
            SkiRunLine self,
            List<Vector3> pts,
            List<byte> mask,
            byte sideBit,
            Color normalColor,
            Color overlapColor,
            float thickness,
            float dottedSpacing,
            bool highlightOverlapSegments,
            HashSet<SkiRunLine> overlapRuns)
        {
            if (pts == null || mask == null) return;

            int n = Mathf.Min(pts.Count, mask.Count);
            if (n < 2) return;

            for (int i = 0; i < n - 1; i++)
            {
                bool v0 = (mask[i] & sideBit) != 0;
                bool v1 = (mask[i + 1] & sideBit) != 0;
                if (!v0 || !v1) continue;

                Vector3 p0 = pts[i];
                Vector3 p1 = pts[i + 1];

                bool overlapsOther = false;
                if (highlightOverlapSegments && overlapRuns != null && overlapRuns.Count > 0)
                {
                    Vector3 mid = (p0 + p1) * 0.5f;
                    foreach (var other in overlapRuns)
                    {
                        if (other == null || other == self) continue;
                        if (IsInsideRunCorridorXZ(mid, other, 0f))
                        {
                            overlapsOther = true;
                            break;
                        }
                    }
                }

                if (overlapsOther)
                {
                    Handles.color = overlapColor;
                    Handles.DrawDottedLine(p0, p1, dottedSpacing);
                }
                else
                {
                    Handles.color = normalColor;
                    _seg2[0] = p0;
                    _seg2[1] = p1;
                    Handles.DrawAAPolyLine(thickness, _seg2);
                }
            }
        }

        /// <summary>
        /// Draw dotted segments of OTHER run corridor edges where those edges fall inside the SELECTED run corridor.
        /// This gives designers a simple, direct visual cue for where to manually move flags.
        /// </summary>
        private void DrawOtherRunOverlapEdgesWithinSelected(SkiRunLine selected, HashSet<SkiRunLine> others, float dottedSpacing)
        {
            if (selected == null || others == null || others.Count == 0) return;

            foreach (var other in others)
            {
                if (other == null || other == selected) continue;

                var pts = other.PointsWorld;
                if (pts == null || pts.Count < 2) continue;

                float halfW = Mathf.Max(0.25f, other.RunWidthMeters * 0.5f);

                // Use the other run's color so it's obvious which corridor edge is being shown.
                Color oc = other.RunColor;
                Color c = new Color(oc.r, oc.g, oc.b, 1f);
                Handles.color = c;

                // Draw per-segment to avoid allocations.
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector3 p0 = pts[i];
                    Vector3 p1 = pts[i + 1];

                    Vector3 dir = p1 - p0;
                    dir.y = 0f;
                    float d2 = dir.sqrMagnitude;
                    if (d2 < 0.0001f) continue;
                    dir /= Mathf.Sqrt(d2);

                    // XZ perpendicular.
                    Vector3 n = new Vector3(-dir.z, 0f, dir.x);

                    // Approximate corridor edges (constant width). This is editor-only diagnostics.
                    Vector3 l0 = p0 + n * halfW;
                    Vector3 l1 = p1 + n * halfW;
                    Vector3 r0 = p0 - n * halfW;
                    Vector3 r1 = p1 - n * halfW;

                    // If the *midpoint* of an edge segment is inside the selected corridor, draw it.
                    if (IsInsideRunCorridorXZ((l0 + l1) * 0.5f, selected, 0f))
                    {
                        Vector3 a = SnapToTerrainPreserveOffset(l0, sceneVisualYOffset);
                        Vector3 b = SnapToTerrainPreserveOffset(l1, sceneVisualYOffset);
                        Handles.DrawDottedLine(a, b, dottedSpacing);
                    }

                    if (IsInsideRunCorridorXZ((r0 + r1) * 0.5f, selected, 0f))
                    {
                        Vector3 a = SnapToTerrainPreserveOffset(r0, sceneVisualYOffset);
                        Vector3 b = SnapToTerrainPreserveOffset(r1, sceneVisualYOffset);
                        Handles.DrawDottedLine(a, b, dottedSpacing);
                    }
                }
            }
        }

        private static float SqrDistancePointSegmentXZ(Vector3 p, Vector3 a, Vector3 b, out float t01)
        {
            Vector2 P = new Vector2(p.x, p.z);
            Vector2 A = new Vector2(a.x, a.z);
            Vector2 B = new Vector2(b.x, b.z);
            Vector2 AB = B - A;

            float len2 = AB.sqrMagnitude;
            if (len2 < 0.000001f)
            {
                t01 = 0f;
                return (P - A).sqrMagnitude;
            }

            float t = Vector2.Dot(P - A, AB) / len2;
            t01 = Mathf.Clamp01(t);
            Vector2 C = A + AB * t01;
            return (P - C).sqrMagnitude;
        }

        private static bool IsInsideRunCorridorXZ(Vector3 p, SkiRunLine run, float extraClearanceMeters = 0f)
        {
            var pts = run.PointsWorld;
            if (pts == null || pts.Count < 2) return false;

            float halfW = (run.RunWidthMeters * 0.5f) + Mathf.Max(0f, extraClearanceMeters);
            float halfW2 = halfW * halfW;

            // Closest XZ distance to polyline
            float best = float.PositiveInfinity;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float t01;
                float d2 = SqrDistancePointSegmentXZ(p, pts[i], pts[i + 1], out t01);
                if (d2 < best) best = d2;
            }

            return best <= halfW2;
        }

        // Simplified intersection workflow support:
        // We do NOT auto-move flags. We only highlight when a flag (or gate) sits inside another run's corridor.
        // For performance, we rebuild the candidate set once per SceneGUI pass (or when preview rebuilds).
        private void RebuildOtherRunSet(SkiRunLine self, HashSet<SkiRunLine> outRuns)
        {
            outRuns.Clear();
            if (self == null) return;

#if UNITY_2023_1_OR_NEWER
            var runs = FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
    var runs = FindObjectsOfType<SkiRunLine>(true);
#endif
            if (runs == null) return;

            // Broad-phase bounds in XZ around the selected run.
            var pts = self.PointsWorld;
            if (pts == null || pts.Count == 0)
                return;

            float minX = float.PositiveInfinity, minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = pts[i];
                if (p.x < minX) minX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.x > maxX) maxX = p.x;
                if (p.z > maxZ) maxZ = p.z;
            }

            // Expand by corridor width + a small margin.
            float expand = Mathf.Max(15f, self.RunWidthMeters + 25f);
            minX -= expand; minZ -= expand;
            maxX += expand; maxZ += expand;

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null || r == self) continue;

                var rPts = r.PointsWorld;
                if (rPts == null || rPts.Count < 2) continue;

                // Quick reject via XZ AABB overlap of the other run.
                float rMinX = float.PositiveInfinity, rMinZ = float.PositiveInfinity;
                float rMaxX = float.NegativeInfinity, rMaxZ = float.NegativeInfinity;
                for (int k = 0; k < rPts.Count; k++)
                {
                    Vector3 p = rPts[k];
                    if (p.x < rMinX) rMinX = p.x;
                    if (p.z < rMinZ) rMinZ = p.z;
                    if (p.x > rMaxX) rMaxX = p.x;
                    if (p.z > rMaxZ) rMaxZ = p.z;
                }

                float rExpand = Mathf.Max(10f, r.RunWidthMeters + 20f);
                rMinX -= rExpand; rMinZ -= rExpand;
                rMaxX += rExpand; rMaxZ += rExpand;

                bool overlapXZ = !(rMaxX < minX || rMinX > maxX || rMaxZ < minZ || rMinZ > maxZ);
                if (!overlapXZ) continue;

                outRuns.Add(r);
            }
        }

        private static bool TryFindContainingRunXZ(Vector3 p, SkiRunLine self, HashSet<SkiRunLine> otherRuns, out SkiRunLine containing)
        {
            containing = null;
            if (otherRuns == null || otherRuns.Count == 0) return false;

            foreach (var other in otherRuns)
            {
                if (other == null || other == self) continue;
                if (IsInsideRunCorridorXZ(p, other, extraClearanceMeters: 0f))
                {
                    containing = other;
                    return true;
                }
            }

            return false;
        }

        private void DrawPairPreview(SkiRunLine run)
        {
            if (_prevLeft.Count == 0) return;

            // Ensure we have a candidate set for simplified intersection highlighting even when
            // boundary preview is disabled.
            if (showIntersectionOverlay)
                RebuildOtherRunSet(run, _tmpRunSet);
            else
                _tmpRunSet.Clear();

            // Simplified intersection workflow:
            // - Always draw the primary preview.
            // - Intersections are communicated via tint on flags/gates and dotted overlap lines on corridor edges.
            DrawPairPreviewSet(
                run,
                set: FlagPreviewSet.Primary,
                centers: _prevCenters,
                left: _prevLeft,
                right: _prevRight,
                mask: _prevSpawnMask,
                hitRuns: _intersectedRuns,
                otherClosest: _intersectedClosest,
                otherEdgeNormal: _intersectedEdgeNormal,
                distancesMeters: _prevDistances,
                isGhost: false,
                drawIntersectionDiagnostics: false);
        }

        private void DrawPairPreviewSet(
            SkiRunLine run,
            FlagPreviewSet set,
            List<Vector3> centers,
            List<Vector3> left,
            List<Vector3> right,
            List<byte> mask,
            List<SkiRunLine> hitRuns,
            List<Vector3> otherClosest,
            List<Vector3> otherEdgeNormal,
            List<float> distancesMeters,
            bool isGhost,
            bool drawIntersectionDiagnostics)
        {
            if (centers == null || left == null || right == null) return;
            if (left.Count == 0) return;

            // If an overlap is selected, we decimate per-sample intersection diagnostics to keep the view responsive.
            // NOTE: selected overlap indices are built from the “authoritative” overlap ribbon, which uses GetOverlayPreviewLists().
            // We therefore only draw these diagnostics when drawIntersectionDiagnostics is true (i.e., using that same authoritative set).
            int selS = HasSelectedOverlap ? _selectedOverlapStartIndex : -1;
            int selE = HasSelectedOverlap ? _selectedOverlapEndIndex : -1;

            selS = (selS >= 0) ? Mathf.Clamp(selS, 0, centers.Count - 1) : selS;
            selE = (selE >= 0) ? Mathf.Clamp(selE, 0, centers.Count - 1) : selE;

            int selSpan = (HasSelectedOverlap && selE >= selS) ? Mathf.Max(1, selE - selS) : 0;
            int selStep = (selSpan > 0) ? Mathf.Max(1, selSpan / 16) : int.MaxValue;

            // Baseline vs final visual language:
            // - Ghost: lighter alpha + dotted look where possible
            // - Solid: higher alpha
            float baseAlpha = isGhost ? 0.18f : 0.85f;
            float sphereAlpha = isGhost ? 0.12f : 0.85f;

            // Intersection pairs: distinct tint so users can identify interaction regions at-a-glance.
            float intersectionAlpha = isGhost ? 0.60f : 0.90f;

            // Candidate set for overlap tests.
            // Prefer the set built during boundary preview; if not available, fall back to unique runs in hitRuns.
            if (showIntersectionOverlay && (_tmpRunSet == null || _tmpRunSet.Count == 0) && hitRuns != null)
            {
                _tmpRunSet.Clear();
                for (int i = 0; i < hitRuns.Count; i++)
                {
                    var r = hitRuns[i];
                    if (r != null && r != run) _tmpRunSet.Add(r);
                }
            }

            for (int i = 0; i < left.Count; i++)
            {
                Vector3 l = left[i];
                Vector3 r = right[i];

                byte m = 3;
                if (mask != null && mask.Count == left.Count)
                    m = mask[i];

                bool leftValid = (m & 1) != 0;
                bool rightValid = (m & 2) != 0;
                bool spawnPair = leftValid || rightValid;
                bool fullGate = leftValid && rightValid;

                if (!spawnPair)
                    continue;

                var hitRun = (hitRuns != null && hitRuns.Count == left.Count) ? hitRuns[i] : null;

                // Simplified: classify intersections purely by corridor overlap (no auto-moving).
                SkiRunLine leftIn = null;
                SkiRunLine rightIn = null;

                bool leftInsideOther = showIntersectionOverlay && leftValid &&
                                      TryFindContainingRunXZ(l, run, _tmpRunSet, out leftIn);

                bool rightInsideOther = showIntersectionOverlay && rightValid &&
                                       TryFindContainingRunXZ(r, run, _tmpRunSet, out rightIn);

                bool anyInsideOther = leftInsideOther || rightInsideOther;

                // Base colour language:
                // - normal preview: white
                // - inside another corridor: warm/orange tint
                Color normalC = new Color(1f, 1f, 1f, baseAlpha);
                Color overlapBase = new Color(1f, 0.65f, 0.15f, intersectionAlpha);

                Color overlapL = overlapBase;
                if (leftInsideOther && leftIn != null)
                {
                    Color bc = leftIn.RunColor; bc.a = intersectionAlpha;
                    overlapL = Color.Lerp(overlapBase, bc, 0.35f);
                }

                Color overlapR = overlapBase;
                if (rightInsideOther && rightIn != null)
                {
                    Color bc = rightIn.RunColor; bc.a = intersectionAlpha;
                    overlapR = Color.Lerp(overlapBase, bc, 0.35f);
                }

                // Gate line tint: if both sides are inside the SAME other run, bias toward that run.
                Color gateC = anyInsideOther ? overlapBase : normalC;
                if (leftInsideOther && rightInsideOther && leftIn != null && leftIn == rightIn)
                    gateC = overlapL;

                Color skippedC = new Color(1f, 0.25f, 1f, Mathf.Max(0.25f, baseAlpha));

                Vector3 c0 = centers[i];

                // Draw either a full gate (both sides) or a half-gate (one side)
                if (fullGate)
                {
                    Handles.color = gateC;
                    if (isGhost) Handles.DrawDottedLine(l, r, 4f);
                    else Handles.DrawLine(l, r);
                }
                else
                {
                    if (leftValid)
                    {
                        Handles.color = leftInsideOther ? overlapL : normalC;
                        if (isGhost) Handles.DrawDottedLine(c0, l, 4f);
                        else Handles.DrawLine(c0, l);
                    }
                    else
                    {
                        Handles.color = skippedC;
                        DrawSkipMarker(l, HandleUtility.GetHandleSize(l) * 0.025f);
                    }

                    if (rightValid)
                    {
                        Handles.color = rightInsideOther ? overlapR : normalC;
                        if (isGhost) Handles.DrawDottedLine(c0, r, 4f);
                        else Handles.DrawLine(c0, r);
                    }
                    else
                    {
                        Handles.color = skippedC;
                        DrawSkipMarker(r, HandleUtility.GetHandleSize(r) * 0.025f);
                    }
                }

                float s = HandleUtility.GetHandleSize(c0) * 0.03f;

                if (leftValid)
                {
                    Color sc = leftInsideOther ? overlapL : normalC;
                    Handles.color = new Color(sc.r, sc.g, sc.b, sphereAlpha);
                }

                if (rightValid)
                {
                    Color sc = rightInsideOther ? overlapR : normalC;
                    Handles.color = new Color(sc.r, sc.g, sc.b, sphereAlpha);
                }

                // Intersection diagnostics (closest point / edge normal) only for the authoritative preview set.
                if (drawIntersectionDiagnostics && HasSelectedOverlap &&
                    hitRun != null && hitRun == _selectedOverlapOtherRun &&
                    i >= selS && i <= selE && (i - selS) % selStep == 0 &&
                    otherClosest != null && otherEdgeNormal != null &&
                    i < otherClosest.Count && i < otherEdgeNormal.Count)
                {
                    Vector3 closest = otherClosest[i];
                    Vector3 edgeN = otherEdgeNormal[i];

                    if (closest.sqrMagnitude > 0.0001f)
                    {
                        Handles.color = new Color(0.15f, 1f, 1f, 1f);
                        Handles.DrawDottedLine(c0, closest, 3f);

                        float hs = HandleUtility.GetHandleSize(closest);
                        Handles.SphereHandleCap(0, closest, Quaternion.identity, hs * 0.05f, EventType.Repaint);

                        if (edgeN.sqrMagnitude > 0.0001f)
                        {
                            Vector3 nDir = edgeN.normalized;
                            _seg2[0] = closest;
                            _seg2[1] = closest + nDir * (hs * 0.6f);
                            Handles.DrawAAPolyLine(2f, _seg2);
                        }

                        var s3 = new GUIStyle(EditorStyles.miniLabel) { richText = false };
                        DrawWorldLabelPacked(closest, hitRun.name, s3, yWorldOffset: hs * 0.08f);
                    }
                }

                // Editable handles only in Gate Edit Mode.
                // IMPORTANT: allow handles on BOTH preview sets so intersection (overlay) pairs can be selected and moved.
                if (gateEditMode && distancesMeters != null && i < distancesMeters.Count)
                {
                    float dMeters = distancesMeters[i];

                    TryDrawEditableFlagHandle(run, set, i, isLeft: true, currentWorld: l, mask: m, distanceMeters: dMeters);
                    TryDrawEditableFlagHandle(run, set, i, isLeft: false, currentWorld: r, mask: m, distanceMeters: dMeters);
                }

            }
        }

        private bool TryGetSelectedDistance(out float dMeters)
        {
            dMeters = 0f;
            var list = (_selectedSet == FlagPreviewSet.Primary) ? _prevDistances : _ovDistances;
            if (list == null) return false;
            if (_selectedPairIndex < 0 || _selectedPairIndex >= list.Count) return false;
            dMeters = list[_selectedPairIndex];
            return true;
        }

        private void TryDrawEditableFlagHandle(
            SkiRunLine run,
            FlagPreviewSet set,
            int pairIndex,
            bool isLeft,
            Vector3 currentWorld,
            byte mask,
            float distanceMeters)
        {
            bool valid = isLeft ? ((mask & 0b01) != 0) : ((mask & 0b10) != 0);
            if (!valid) return;

            int side = isLeft ? 0 : 1;

            Vector3 drawPos = currentWorld + Vector3.up * sceneVisualYOffset;
            float s = HandleUtility.GetHandleSize(drawPos) * 0.09f;

            if (Handles.Button(drawPos, Quaternion.identity, s, s, Handles.SphereHandleCap))
            {
                _selectedSet = set;
                _selectedPairIndex = pairIndex;
                _selectedSide = side;
                GUI.changed = true;
            }

            if (_selectedSet != set || _selectedPairIndex != pairIndex || _selectedSide != side)
                return;

            EditorGUI.BeginChangeCheck();
            Vector3 newDraw = Handles.PositionHandle(drawPos, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(run, "Move Flag Pair Preview");

            Vector3 newWorld = newDraw - Vector3.up * sceneVisualYOffset;

            // Preserve the existing “above terrain” offset by measuring it at the current position.
            Terrain tCur = ResolveTerrainAt(currentWorld);
            float curBaseY = SnapToTerrain(tCur, currentWorld).y;
            float offsetY = currentWorld.y - curBaseY;

            Terrain tNew = ResolveTerrainAt(newWorld);
            Vector3 snapped = SnapToTerrain(tNew, newWorld);
            snapped.y += offsetY;

            // Moving a gate side in-scene is an authoring action: write GateOverrides and lock by default
            // so rebuild respects the manual position.
            run.SetGateOverridePosition(distanceMeters, isLeft, snapped, lockPosition: true);

            _previewDirty = true;
            EditorUtility.SetDirty(run);
            SceneView.RepaintAll();
        }

        private void HandleFlagOverrideContextMenu(SkiRunLine run)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.ContextClick) return;
            if (_selectedPairIndex < 0) return;
            if (!TryGetSelectedDistance(out float dMeters)) return;

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Gate/Toggle Pair Enabled"), false, () =>
            {
                Undo.RecordObject(run, "Toggle Gate Pair Enabled");
                bool leftEnabled = run.IsGateSideEnabled(dMeters, isLeft: true);
                bool rightEnabled = run.IsGateSideEnabled(dMeters, isLeft: false);
                run.SetGateOverrideEnabled(dMeters, leftEnabled: !leftEnabled, rightEnabled: !rightEnabled);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddItem(new GUIContent("Gate/Toggle Left Enabled"), false, () =>
            {
                Undo.RecordObject(run, "Toggle Gate Left Enabled");
                bool cur = run.IsGateSideEnabled(dMeters, isLeft: true);
                run.SetGateOverrideEnabled(dMeters, leftEnabled: !cur, rightEnabled: null);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddItem(new GUIContent("Gate/Toggle Right Enabled"), false, () =>
            {
                Undo.RecordObject(run, "Toggle Gate Right Enabled");
                bool cur = run.IsGateSideEnabled(dMeters, isLeft: false);
                run.SetGateOverrideEnabled(dMeters, leftEnabled: null, rightEnabled: !cur);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddSeparator("Gate/");

            menu.AddItem(new GUIContent("Gate/Clear Authored Positions (Both Sides)"), false, () =>
            {
                Undo.RecordObject(run, "Clear Gate Positions");
                run.ClearGateOverridePositions(dMeters);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddItem(new GUIContent("Gate/Lock Positions (Both Sides)"), false, () =>
            {
                Undo.RecordObject(run, "Lock Gate Positions");
                run.SetGateOverrideLocked(dMeters, lockLeft: true, lockRight: true);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddItem(new GUIContent("Gate/Unlock Positions (Both Sides)"), false, () =>
            {
                Undo.RecordObject(run, "Unlock Gate Positions");
                run.SetGateOverrideLocked(dMeters, lockLeft: false, lockRight: false);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.AddSeparator("Gate/");

            menu.AddItem(new GUIContent("Gate/Remove Gate Override Entry"), false, () =>
            {
                Undo.RecordObject(run, "Remove Gate Override");
                run.RemoveGate(dMeters);
                _previewDirty = true;
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            });

            menu.ShowAsContext();
            e.Use();
        }

        private static void DrawSkipMarker(Vector3 p, float size)
        {
            Vector3 a = p + Vector3.right * size;
            Vector3 b = p - Vector3.right * size;
            Vector3 c = p + Vector3.forward * size;
            Vector3 d = p - Vector3.forward * size;
            Handles.DrawLine(a, b);
            Handles.DrawLine(c, d);
        }

        private void DrawSpacingHandle(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp)
        {
            if (!showSpacingHandle) return;
            if (pointsProp == null || pointsProp.arraySize < 2) return;

            SerializedProperty spacingProp = so.FindProperty("flagSpacingMeters");
            if (spacingProp == null) return;

            Vector3 p0 = pointsProp.GetArrayElementAtIndex(0).vector3Value;
            Vector3 p1 = pointsProp.GetArrayElementAtIndex(1).vector3Value;

            // Axis perpendicular to the run direction (more intuitive than camera-right).
            Vector3 runDir = (p1 - p0);
            if (runDir.sqrMagnitude < 0.0001f) runDir = Vector3.forward;

            Vector3 axis = Vector3.Cross(Vector3.up, runDir).normalized;
            if (axis.sqrMagnitude < 0.0001f)
                axis = SceneView.currentDrawingSceneView != null
                    ? SceneView.currentDrawingSceneView.camera.transform.right
                    : Vector3.right;

            float hs = HandleUtility.GetHandleSize(p0);
            Vector3 handlePos = p0 + axis * (hs * 0.85f);

            Handles.color = new Color(1f, 0.9f, 0.2f, 0.95f); // distinct from selection yellow

            EditorGUI.BeginChangeCheck();
            float spacing = spacingProp.floatValue;

            float newSpacing = Handles.ScaleValueHandle(
                spacing,
                handlePos,
                Quaternion.LookRotation(axis),
                hs * 0.18f,
                Handles.CubeHandleCap,
                0.25f);

            if (EditorGUI.EndChangeCheck())
            {
                newSpacing = Mathf.Clamp(newSpacing, 1f, 200f);
                Undo.RecordObject(run, "Adjust Flag Spacing");
                spacingProp.floatValue = newSpacing;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(run);

                _previewDirty = true;
                SceneView.RepaintAll();
            }

            Handles.Label(handlePos + Vector3.up * (hs * 0.08f), $"Spacing: {spacingProp.floatValue:0.0}m");
        }

        // ============================================================================
        // Gate Editing (Scene View) — edit preview gates (left/right) + insert/remove
        // ============================================================================

        private void HandleGateEditing(SkiRunLine run)
        {
            if (run == null) return;
            if (!_showGateHandles) return;
            if (!gateEditMode) return;

            Event e = Event.current;
            if (e == null) return;

            _gateConsumedThisEvent = false;

            // Shift-click: add a gate at the clicked corridor location (nearest preview pair)
            if (enableShiftClickInsert && e.type == EventType.MouseDown && e.shift && e.button == 0)
            {
                if (TryPickNearestPreviewPair(run, out FlagPreviewSet set, out int pairIndex, out int side))
                {
                    if (TryGetPairDistanceMeters(set, pairIndex, out float pickedDMeters))
                    {
                        Undo.RecordObject(run, "Add Gate");

                        // Insert an un-locked gate override at this distance.
                        run.AddInsertedGate(pickedDMeters);

                        EditorUtility.SetDirty(run);
                        _previewDirty = true;
                        SceneView.RepaintAll();

                        e.Use();
                        _gateConsumedThisEvent = true;
                        return;
                    }
                }
            }

            // Alt-click: remove gate at the clicked location (if inserted), otherwise disable/toggle side
            if (enableAltClickDelete && e.type == EventType.MouseDown && e.alt && e.button == 0)
            {
                if (TryPickNearestPreviewPair(run, out FlagPreviewSet set, out int pairIndex, out int side))
                {
                    if (TryGetPairDistanceMeters(set, pairIndex, out float pickedDMeters))
                    {
                        Undo.RecordObject(run, "Remove/Disable Gate");

                        // If it was an inserted gate: remove the whole gate override.
                        // Otherwise: toggle enabled state for clicked side (or full pair if center).
                        if (run.IsInsertedGate(pickedDMeters))
                        {
                            run.RemoveGate(pickedDMeters);
                        }
                        else
                        {
                            if (side < 0)
                            {
                                // center click: toggle full pair
                                bool leftEnabled = run.IsGateSideEnabled(pickedDMeters, isLeft: true);
                                bool rightEnabled = run.IsGateSideEnabled(pickedDMeters, isLeft: false);
                                run.SetGateOverrideEnabled(pickedDMeters, leftEnabled: !leftEnabled, rightEnabled: !rightEnabled);
                            }
                            else
                            {
                                bool isLeft = (side == 0);
                                bool cur = run.IsGateSideEnabled(pickedDMeters, isLeft: isLeft);

                                // NOTE: your API appears to accept nullable bools for per-side toggles
                                // (as your snippet uses null). Keep that contract consistent.
                                if (isLeft) run.SetGateOverrideEnabled(pickedDMeters, leftEnabled: !cur, rightEnabled: null);
                                else run.SetGateOverrideEnabled(pickedDMeters, leftEnabled: null, rightEnabled: !cur);
                            }
                        }

                        EditorUtility.SetDirty(run);
                        _previewDirty = true;
                        SceneView.RepaintAll();

                        e.Use();
                        _gateConsumedThisEvent = true;
                        return;
                    }
                }
            }

            // If the event was consumed by insert/delete, do not also drag.
            if (_gateConsumedThisEvent)
                return;

            // Draw editable handles ONLY for the currently selected pair (much cleaner + avoids handle conflicts).
            if (!showPairPreview) return;

            if (!TryGetSelectedDistance(out float selectedDMeters))
                return;

            var leftList = (_selectedSet == FlagPreviewSet.Primary) ? _prevLeft : _ovLeft;
            var rightList = (_selectedSet == FlagPreviewSet.Primary) ? _prevRight : _ovRight;
            var centerList = (_selectedSet == FlagPreviewSet.Primary) ? _prevCenters : _ovCenters;
            var maskList = (_selectedSet == FlagPreviewSet.Primary) ? _prevSpawnMask : _ovSpawnMask;

            if (leftList == null || rightList == null || centerList == null) return;
            if (_selectedPairIndex < 0 || _selectedPairIndex >= leftList.Count) return;

            byte mask = 3;
            if (maskList != null && maskList.Count == leftList.Count)
                mask = maskList[_selectedPairIndex];

            if ((mask & 0b11) == 0)
                return;

            Vector3 l = leftList[_selectedPairIndex];
            Vector3 r = rightList[_selectedPairIndex];
            Vector3 c0 = centerList[_selectedPairIndex];

            if ((mask & 0b01) != 0) DrawEditableGateSide(run, selectedDMeters, c0, l, isLeft: true);
            if ((mask & 0b10) != 0) DrawEditableGateSide(run, selectedDMeters, c0, r, isLeft: false);

            // Only show width editing when both sides are enabled (otherwise width is ambiguous).
            if ((mask & 0b11) == 0b11)
                DrawEditableGateWidth(run, selectedDMeters, c0, l, r, affectNeighbors: _gateEditAffectsNeighbors);
        }

        private void DrawEditableGateSide(SkiRunLine run, float dMeters, Vector3 center, Vector3 currentWorld, bool isLeft)
        {
            float s = HandleUtility.GetHandleSize(currentWorld) * 0.14f;

            Handles.color = isLeft
                ? new Color(0.35f, 0.9f, 1f, 0.95f)      // cyan
                : new Color(1f, 0.35f, 0.7f, 0.95f);     // magenta/pink

            // Stable-ish per-handle control ID reduces collisions at intersections/overlaps.
            int hint = (run.GetInstanceID() * 397)
                       ^ dMeters.GetHashCode()
                       ^ (isLeft ? 0x1EAF : 0x5B2D);
            int id = GUIUtility.GetControlID(hint, FocusType.Passive);

            EditorGUI.BeginChangeCheck();
            var fmh_2147_71_639050483114859531 = Quaternion.identity; Vector3 newPos = Handles.FreeMoveHandle(id, currentWorld, s, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(run, "Move Gate Flag");

                if (autoSnapToTerrainWhenEditing)
                {
                    Terrain t = ResolveTerrainAt(newPos);
                    if (t != null) newPos = SnapToTerrain(t, newPos);
                }

                // Default unlocked (matches your current intent).
                run.SetGateOverridePosition(dMeters, isLeft, newPos, lockPosition: false);

                EditorUtility.SetDirty(run);
                _previewDirty = true;
                SceneView.RepaintAll();
            }
        }

        private void DrawEditableGateWidth(SkiRunLine run, float dMeters, Vector3 c0, Vector3 l, Vector3 r, bool affectNeighbors)
        {
            Vector3 mid = (l + r) * 0.5f;
            Vector3 dirR = (r - mid);
            Vector3 dirL = (l - mid);

            if (dirR.sqrMagnitude < 0.0001f || dirL.sqrMagnitude < 0.0001f)
                return;

            Vector3 axis = dirR.normalized;

            float hs = HandleUtility.GetHandleSize(mid);
            float handleSize = hs * 0.10f;

            // Place width handle slightly to the right side
            Vector3 handlePos = mid + axis * (hs * 0.25f);

            Handles.color = new Color(1f, 1f, 1f, 0.9f);

            EditorGUI.BeginChangeCheck();
            float curHalfWidth = dirR.magnitude;
            float newHalfWidth = Handles.ScaleValueHandle(curHalfWidth, handlePos,
                Quaternion.LookRotation(axis), handleSize, Handles.CubeHandleCap, 0.05f);
            if (EditorGUI.EndChangeCheck())
            {
                newHalfWidth = Mathf.Max(0.25f, newHalfWidth);
                float scale = newHalfWidth / Mathf.Max(0.0001f, curHalfWidth);

                // Apply symmetric scale from the midpoint.
                Vector3 newR = mid + dirR * scale;
                Vector3 newL = mid + dirL * scale;

                if (autoSnapToTerrainWhenEditing)
                {
                    Terrain tMid = ResolveTerrainAt(mid);
                    Terrain tL = ResolveTerrainAt(newL, preferred: tMid);
                    Terrain tR = ResolveTerrainAt(newR, preferred: tMid);

                    if (tL != null) newL = SnapToTerrain(tL, newL);
                    if (tR != null) newR = SnapToTerrain(tR, newR);
                }

                Undo.RecordObject(run, "Adjust Gate Width");

                if (!affectNeighbors)
                {
                    run.SetGateOverridePosition(dMeters, isLeft: true, newL, lockPosition: false);
                    run.SetGateOverridePosition(dMeters, isLeft: false, newR, lockPosition: false);
                }
                else
                {
                    // Affect nearby pairs by distance window or count (your inspector controls already exist)
                    ApplyWidthToNeighbors(run, dMeters, newL, newR);
                }

                EditorUtility.SetDirty(run);
                _previewDirty = true;
                SceneView.RepaintAll();
            }
        }

        private void ApplyWidthToNeighbors(SkiRunLine run, float centerDMeters, Vector3 newL, Vector3 newR)
        {
            // Uses your existing inspector fields:
            // _gateNeighborPairs, _gateNeighborRadiusMeters

            // Primary: by distance radius (more intuitive).
            for (int i = 0; i < _prevCenters.Count; i++)
            {
                if (!TryGetPairDistanceMeters(FlagPreviewSet.Primary, i, out float d)) continue;
                if (Mathf.Abs(d - centerDMeters) > _gateNeighborRadiusMeters) continue;

                // Preserve each neighbor’s midpoint, apply delta width relative to its current midpoint
                Vector3 l = _prevLeft[i];
                Vector3 r = _prevRight[i];
                Vector3 mid = (l + r) * 0.5f;

                Vector3 srcMid = (newL + newR) * 0.5f;
                Vector3 srcL = newL - srcMid;
                Vector3 srcR = newR - srcMid;

                Vector3 dstL = mid + srcL;
                Vector3 dstR = mid + srcR;

                run.SetGateOverridePosition(d, isLeft: true, dstL, lockPosition: false);
                run.SetGateOverridePosition(d, isLeft: false, dstR, lockPosition: false);
            }
        }

        private bool TryPickNearestPreviewPair(SkiRunLine run, out FlagPreviewSet set, out int pairIndex, out int side)
        {
            set = FlagPreviewSet.Primary;
            pairIndex = -1;
            side = -1;

            Event e = Event.current;
            if (e == null) return false;

            float best = float.PositiveInfinity;
            FlagPreviewSet bestSet = FlagPreviewSet.Primary;
            int bestSide = -1; // -1 center, 0 left, 1 right
            int bestIdx = -1;

            // Helper local (keeps this method allocation-free)
            void Consider(FlagPreviewSet s, int i, Vector3 c0, Vector3 l, Vector3 r)
            {
                float dc = HandleUtility.DistanceToCircle(c0, HandleUtility.GetHandleSize(c0) * 0.12f);
                if (dc < best) { best = dc; bestIdx = i; bestSide = -1; bestSet = s; }

                float dl = HandleUtility.DistanceToCircle(l, HandleUtility.GetHandleSize(l) * 0.12f);
                if (dl < best) { best = dl; bestIdx = i; bestSide = 0; bestSet = s; }

                float dr = HandleUtility.DistanceToCircle(r, HandleUtility.GetHandleSize(r) * 0.12f);
                if (dr < best) { best = dr; bestIdx = i; bestSide = 1; bestSet = s; }
            }

            // Primary set: skip intersection pairs when overlay is enabled (so we pick the authoritative overlay version)
            for (int i = 0; i < _prevCenters.Count; i++)
            {
                if (showIntersectionOverlay && !_overlayUsesPrimaryPreview && _intersectedRuns != null && i < _intersectedRuns.Count)
                {
                    var hit = _intersectedRuns[i];
                    if (hit != null && hit != run)
                        continue; // intersection pairs should be picked from overlay set
                }

                Consider(FlagPreviewSet.Primary, i, _prevCenters[i], _prevLeft[i], _prevRight[i]);
            }

            // Overlay set: consider only intersection pairs (it duplicates primary for non-intersections)
            if (showIntersectionOverlay && !_overlayUsesPrimaryPreview && _ovCenters != null && _ovCenters.Count > 0)
            {
                for (int i = 0; i < _ovCenters.Count; i++)
                {
                    if (_ovIntersectedRuns == null || i >= _ovIntersectedRuns.Count) break;

                    var hit = _ovIntersectedRuns[i];
                    if (hit == null || hit == run) continue;

                    Consider(FlagPreviewSet.Overlay, i, _ovCenters[i], _ovLeft[i], _ovRight[i]);
                }
            }

            // Reject if too far to be intentional
            if (bestIdx < 0 || best > 22f)
                return false;

            set = bestSet;
            pairIndex = bestIdx;
            side = bestSide;
            return true;
        }

        private bool TryGetPairDistanceMeters(FlagPreviewSet set, int idx, out float dMeters)
        {
            dMeters = 0f;

            // Your editor excerpt shows these lists exist in your updated tool:
            // _prevDistances and _ovDistances
            if (set == FlagPreviewSet.Primary)
            {
                if (_prevDistances == null || idx < 0 || idx >= _prevDistances.Count) return false;
                dMeters = _prevDistances[idx];
                return true;
            }
            else
            {
                if (_ovDistances == null || idx < 0 || idx >= _ovDistances.Count) return false;
                dMeters = _ovDistances[idx];
                return true;
            }
        }


        private static Terrain ResolveTerrainAt(Vector3 worldPos, Terrain preferred = null)
        {
            if (preferred != null)
            {
                var tp = preferred.transform.position;
                var s = preferred.terrainData.size;
                bool inside =
                    worldPos.x >= tp.x && worldPos.x <= tp.x + s.x &&
                    worldPos.z >= tp.z && worldPos.z <= tp.z + s.z;

                if (inside) return preferred;
            }

            var terrains = Terrain.activeTerrains;
            if (terrains != null)
            {
                for (int i = 0; i < terrains.Length; i++)
                {
                    var t = terrains[i];
                    if (t == null) continue;

                    var tp = t.transform.position;
                    var s = t.terrainData.size;

                    bool inside =
                        worldPos.x >= tp.x && worldPos.x <= tp.x + s.x &&
                        worldPos.z >= tp.z && worldPos.z <= tp.z + s.z;

                    if (inside) return t;
                }
            }

            return Terrain.activeTerrain;
        }

        private static Vector3 SnapToTerrain(Terrain t, Vector3 worldPos)
        {
            if (t == null) return worldPos;
            float h = t.SampleHeight(worldPos) + t.transform.position.y;
            worldPos.y = h;
            return worldPos;
        }

        private float SampleTerrainY(Vector3 worldPos)
        {
            Terrain t = ResolveTerrainAt(worldPos);
            if (t == null) return worldPos.y;
            return t.SampleHeight(worldPos) + t.transform.position.y;
        }

        private Vector3 SnapToTerrainPreserveOffset(Vector3 worldPos, float yOffset)
        {
            worldPos.y = SampleTerrainY(worldPos) + yOffset;
            return worldPos;
        }

        private static Vector3 SampleNormal(Terrain t, Vector3 worldPos)
        {
            if (t == null || t.terrainData == null) return Vector3.up;

            Vector3 tp = worldPos - t.transform.position;
            Vector3 size = t.terrainData.size;
            float u = Mathf.Clamp01(tp.x / Mathf.Max(0.0001f, size.x));
            float v = Mathf.Clamp01(tp.z / Mathf.Max(0.0001f, size.z));

            Vector3 n = t.terrainData.GetInterpolatedNormal(u, v);
            return n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.up;
        }

        private const string PrefKey = "SkiRunLineEditor.";

        private void SetAuthoringMode(AuthoringMode mode, SkiRunLine run)
        {
            if (_authoringMode == mode) return;

            _authoringMode = mode;

            // Mutually-exclusive interaction modes (prevents Shift/Alt conflicts)
            if (_authoringMode != AuthoringMode.Flags)
                gateEditMode = false;

            if (_authoringMode != AuthoringMode.Fences)
            {
                // Spans are legacy; keep hard-disabled when not explicitly needed.
                fenceHoleEditMode = false;
                _selectedFenceHoleIndex = -1;
            }

            // Defaults when entering modes
            if (_authoringMode == AuthoringMode.Flags)
            {
                gateEditMode = true;
                // Helpful previews while flag authoring
                showBoundaryPreview = true;
                showPairPreview = true;
            }

            if (_authoringMode == AuthoringMode.Fences)
            {
                // New simplified fence authoring: holes are the main editing affordance.
                fenceHoleEditMode = true;

                // Fence authoring benefits from corridor visibility.
                showBoundaryPreview = true;
                showPairPreview = false; // less noise
            }

            // Clear selections (reduces handle collisions at overlaps/intersections)
            selectedPointIndex = -1;
            _selectedPairIndex = -1;
            _selectedSide = 0;
            _selectedSet = FlagPreviewSet.Primary;

            _selectedOverlapOtherRun = null;
            _selectedOverlapStartIndex = -1;
            _selectedOverlapEndIndex = -1;

            _gateConsumedThisEvent = false;

            SaveEditorPrefs();
            SceneView.RepaintAll();
            Repaint();
        }

        private void HandleSceneHotkeys(SkiRunLine run)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            bool ctrl = e.control || e.command;
            bool shift = e.shift;

            bool consumed = false;
            bool previewAffects = false;

            if (!ctrl && !shift)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Tab:
                        CycleAuthoringMode(run);
                        consumed = true;
                        break;

                    // Cohesive authoring modes
                    case KeyCode.Alpha1:
                        SetAuthoringMode(AuthoringMode.Path, run);
                        consumed = true;
                        break;

                    case KeyCode.Alpha2:
                        SetAuthoringMode(AuthoringMode.Flags, run);
                        consumed = true;
                        break;

                    case KeyCode.Alpha3:
                        SetAuthoringMode(AuthoringMode.Fences, run);
                        consumed = true;
                        break;

                    // Gate editing: if you press G outside Flags, it takes you into Flags mode
                    case KeyCode.G:
                        if (_authoringMode != AuthoringMode.Flags)
                        {
                            SetAuthoringMode(AuthoringMode.Flags, run);
                        }
                        else
                        {
                            gateEditMode = !gateEditMode;
                            consumed = true;
                        }
                        consumed = true;
                        break;

                    case KeyCode.H:
                        showHotkeyOverlay = !showHotkeyOverlay;
                        consumed = true;
                        break;

                    case KeyCode.P:
                        showPairPreview = !showPairPreview;
                        previewAffects = true;
                        consumed = true;
                        break;

                    case KeyCode.B:
                        showBoundaryPreview = !showBoundaryPreview;
                        previewAffects = true;
                        consumed = true;
                        break;

                    case KeyCode.I:
                        showIntersectionOverlay = !showIntersectionOverlay;
                        previewAffects = true;
                        consumed = true;
                        break;

                    case KeyCode.O:
                        previewIncludeOverlapAvoidance = !previewIncludeOverlapAvoidance;
                        previewAffects = true;
                        consumed = true;
                        break;
                }
            }

            // “Heavy” operations: require Ctrl+Shift to avoid accidents
            if (ctrl && shift)
            {
                if (e.keyCode == KeyCode.R)
                {
                    Undo.RecordObject(run, "Rebuild Run Flags");
                    run.RebuildFlags();
                    run.ApplyColorToGeneratedFlags();
                    EditorUtility.SetDirty(run);
                    previewAffects = true;
                    consumed = true;
                }
                else if (e.keyCode == KeyCode.M)
                {
                    Undo.RecordObject(run, "Bake Run Metrics");
                    run.BakeMetrics();
                    EditorUtility.SetDirty(run);
                    consumed = true;
                }
            }

            // Fence holes: Delete/Backspace removes selected hole
            if (_authoringMode == AuthoringMode.Fences && fenceHoleEditMode &&
                (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            {
                var holesProp = serializedObject.FindProperty("fenceExcludeHoles");
                if (holesProp != null && _selectedFenceHoleIndex >= 0 && _selectedFenceHoleIndex < holesProp.arraySize)
                {
                    Undo.RecordObject(run, "Delete Fence Hole");
                    holesProp.DeleteArrayElementAtIndex(_selectedFenceHoleIndex);
                    serializedObject.ApplyModifiedProperties();

                    _selectedFenceHoleIndex = -1;

                    EditorUtility.SetDirty(run);
                    _previewDirty = true;
                    SceneView.RepaintAll();
                    Repaint();

                    e.Use();
                    return;
                }
            }

            if (previewAffects)
                _previewDirty = true;

            if (consumed)
            {
                SaveEditorPrefs();
                e.Use();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void CycleAuthoringMode(SkiRunLine run)
        {
            int next = ((int)_authoringMode + 1) % 3;
            SetAuthoringMode((AuthoringMode)next, run);
        }

        private void DrawFenceBoundaryPreviewForFenceMode(SkiRunLine run)
        {
            if (run == null) return;
            if (!showBoundaryPreview) return;
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            float runLen = run.GetTotalLengthMeters();

            var spacingProp = serializedObject.FindProperty("fencePointSpacingMeters");
            float spacing = (spacingProp != null) ? Mathf.Max(0.5f, spacingProp.floatValue) : 2f;

            bool useLeft = serializedObject.FindProperty("fenceUseLeftEdge")?.boolValue ?? true;
            bool useRight = serializedObject.FindProperty("fenceUseRightEdge")?.boolValue ?? true;

            // Left edge preview (match gate-left colour)
            if (useLeft)
            {
                _tmpFencePreview.Clear();
                run.GetFenceBoundaryPreviewPolyline(true, 0f, runLen, spacing, _tmpFencePreview);
                Handles.color = new Color(0.35f, 0.9f, 1f, 0.85f);
                DrawAAPolyLineCached(3.0f, _tmpFencePreview);
            }

            // Right edge preview (match gate-right colour)
            if (useRight)
            {
                _tmpFencePreview.Clear();
                run.GetFenceBoundaryPreviewPolyline(false, 0f, runLen, spacing, _tmpFencePreview);
                Handles.color = new Color(1f, 0.35f, 0.7f, 0.85f);
                DrawAAPolyLineCached(3.0f, _tmpFencePreview);
            }
        }

        private void HandleFenceHoleSceneEditing(SkiRunLine run)
        {
            if (run == null) return;

            Event e = Event.current;
            if (e == null) return;

            // Only meaningful in fence authoring mode.
            if (_authoringMode != AuthoringMode.Fences)
                return;

            float runLen = run.GetTotalLengthMeters();

            SerializedProperty fencePointSpacingMetersProp = serializedObject.FindProperty("fencePointSpacingMeters");
            float fencePointSpacingMeters =
                (fencePointSpacingMetersProp != null) ? fencePointSpacingMetersProp.floatValue : 2f;

            var holesProp = serializedObject.FindProperty("fenceExcludeHoles");
            if (holesProp == null) return;

            // --- Prevent Shift multi-select from stealing our Shift+Click add-hole gesture ---
            // Unity uses Shift as additive selection; we want Shift+Click to be "tool action" in fence hole mode.
            if (fenceHoleEditMode && e.shift && e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            // --- Draw + interact with existing holes ---
            for (int i = 0; i < holesProp.arraySize; i++)
            {
                var hp = holesProp.GetArrayElementAtIndex(i);
                bool enabled = hp.FindPropertyRelative("enabled").boolValue;
                if (!enabled) continue;

                var side = (SkiRunLine.FenceSide)hp.FindPropertyRelative("side").enumValueIndex;
                float a = hp.FindPropertyRelative("startMeters").floatValue;
                float b = hp.FindPropertyRelative("endMeters").floatValue;

                float start = Mathf.Clamp(Mathf.Min(a, b), 0f, runLen);
                float end = Mathf.Clamp(Mathf.Max(a, b), 0f, runLen);

                // Get endpoints on the chosen corridor side
                if (!run.TryGetFenceBoundaryPointAtDistance(side == SkiRunLine.FenceSide.Left, start, out Vector3 pStart)) continue;
                if (!run.TryGetFenceBoundaryPointAtDistance(side == SkiRunLine.FenceSide.Left, end, out Vector3 pEnd)) continue;

                // Lift slightly for readability
                pStart += Vector3.up * sceneVisualYOffset;
                pEnd += Vector3.up * sceneVisualYOffset;

                bool isSelected = (_selectedFenceHoleIndex == i);

                // Preview polyline along the boundary between start and end (Repaint only)
                if (Event.current.type == EventType.Repaint)
                {
                    _tmpFencePreview.Clear();

                    run.GetFenceBoundaryPreviewPolyline(
                        isLeft: side == SkiRunLine.FenceSide.Left,
                        startMeters: start,
                        endMeters: end,
                        spacingMeters: Mathf.Max(0.5f, fencePointSpacingMeters),
                        outWorldPoints: _tmpFencePreview
                    );

                    if (_tmpFencePreview.Count >= 2)
                    {
                        // Apply y offset to preview points without allocating new lists
                        for (int k = 0; k < _tmpFencePreview.Count; k++)
                            _tmpFencePreview[k] += Vector3.up * sceneVisualYOffset;

                        Handles.color = isSelected
                            ? new Color(1.0f, 0.25f, 0.25f, 1.0f)
                            : new Color(1.0f, 0.25f, 0.25f, 0.75f);

                        DrawAAPolyLineCached(isSelected ? 5.0f : 3.5f, _tmpFencePreview);
                    }
                }

                // Clickable "select" button at midpoint (works even when not in edit mode)
                Vector3 mid = Vector3.Lerp(pStart, pEnd, 0.5f);
                float pick = HandleUtility.GetHandleSize(mid) * 0.10f;

                Handles.color = isSelected ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(1f, 0.35f, 0.35f, 0.65f);
                if (Handles.Button(mid, Quaternion.identity, pick, pick * 1.2f, Handles.SphereHandleCap))
                {
                    _selectedFenceHoleIndex = i;
                    GUI.changed = true;
                    SceneView.RepaintAll();
                }

                // Small endpoint markers (also selectable)
                float endPick = HandleUtility.GetHandleSize(pStart) * 0.08f;
                if (Handles.Button(pStart, Quaternion.identity, endPick, endPick * 1.2f, Handles.CubeHandleCap))
                {
                    _selectedFenceHoleIndex = i;
                    GUI.changed = true;
                    SceneView.RepaintAll();
                }
                if (Handles.Button(pEnd, Quaternion.identity, endPick, endPick * 1.2f, Handles.CubeHandleCap))
                {
                    _selectedFenceHoleIndex = i;
                    GUI.changed = true;
                    SceneView.RepaintAll();
                }

                // Label (lightweight, only repaint)
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.color = Color.white;
                    Handles.Label(mid + Vector3.up * (HandleUtility.GetHandleSize(mid) * 0.15f),
                        $"Hole {i} ({side})  {Mathf.Abs(end - start):0.0}m");
                }

                // Draggable handles ONLY for selected hole (keeps view clean)
                if (!fenceHoleEditMode || !isSelected)
                    continue;

                float hs = HandleUtility.GetHandleSize(pStart) * 0.11f;

                EditorGUI.BeginChangeCheck();
                Vector3 newStart = Handles.FreeMoveHandle(pStart, hs, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    // Remove y-offset before projecting back onto run distance
                    newStart -= Vector3.up * sceneVisualYOffset;

                    if (run.TryGetClosestPointOnCenterlineXZ(newStart, out float d, out _, out _, out _))
                    {
                        Undo.RecordObject(run, "Move Fence Hole Start");
                        hp.FindPropertyRelative("startMeters").floatValue = Mathf.Clamp(d, 0f, runLen);
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newEnd = Handles.FreeMoveHandle(pEnd, hs, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    newEnd -= Vector3.up * sceneVisualYOffset;

                    if (run.TryGetClosestPointOnCenterlineXZ(newEnd, out float d, out _, out _, out _))
                    {
                        Undo.RecordObject(run, "Move Fence Hole End");
                        hp.FindPropertyRelative("endMeters").floatValue = Mathf.Clamp(d, 0f, runLen);
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }
            }

            // --- Add new hole: Shift+Click ---
            if (!fenceHoleEditMode) return;

            if (e.type == EventType.MouseDown && e.shift && e.button == 0)
            {
                // Because Shift is also additive selection, be aggressive about consuming the event.
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

                Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(r, out RaycastHit hit, 20000f))
                {
                    if (run.TryGetClosestPointOnCenterlineXZ(hit.point, out float d, out _, out _, out _))
                    {
                        float start = Mathf.Clamp(d, 0f, runLen);
                        float end = Mathf.Clamp(d + fenceAddHoleLengthMeters, 0f, runLen);

                        Undo.RecordObject(run, "Add Fence Hole");

                        int idx = holesProp.arraySize;
                        holesProp.InsertArrayElementAtIndex(idx);

                        var hp = holesProp.GetArrayElementAtIndex(idx);
                        hp.FindPropertyRelative("enabled").boolValue = true;
                        hp.FindPropertyRelative("side").enumValueIndex = (int)fenceAddSide;
                        hp.FindPropertyRelative("startMeters").floatValue = start;
                        hp.FindPropertyRelative("endMeters").floatValue = end;

                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(run);

                        _selectedFenceHoleIndex = idx;

                        SceneView.RepaintAll();
                        e.Use();
                    }
                }
            }
        }

        private void DrawHotkeyOverlay(SkiRunLine run)
        {
            Handles.BeginGUI();

            const float w = 320f;
            const float pad = 10f;
            Rect r = new Rect(pad, pad, w, 500f);

            GUILayout.BeginArea(r, EditorStyles.helpBox);
            GUILayout.Label("Ski Run Gizmos", EditorStyles.boldLabel);

            GUILayout.Label($"Mode: {_authoringMode}" + (_authoringMode == AuthoringMode.Flags && gateEditMode ? " (Gate Edit)" : ""));
            GUILayout.Space(4);

            GUILayout.Label("Authoring Modes:");
            GUILayout.Label("  1   Path");
            GUILayout.Label("  2   Flags");
            GUILayout.Label("  3   Fences");
            GUILayout.Space(4);

            GUILayout.Label("Hotkeys:");
            GUILayout.Label("  G   Toggle Gate Edit (Flags mode)");
            GUILayout.Label("  H   Toggle this overlay");
            GUILayout.Label("  P   Toggle Pair Preview");
            GUILayout.Label("  B   Toggle Boundary Preview");
            GUILayout.Label("  I   Toggle Intersection Overlay");
            GUILayout.Label("  O   Toggle Overlap Avoidance (preview)");
            GUILayout.Label("  Esc Clear selection");

            GUILayout.Space(4);
            GUILayout.Label("Heavy ops:");
            GUILayout.Label("  Ctrl+Shift+R  Rebuild Flags");
            GUILayout.Label("  Ctrl+Shift+M  Bake Metrics");

            if (gateEditMode)
            {
                GUILayout.Space(4);
                GUILayout.Label("Gate Mode:");
                GUILayout.Label("  Shift+Click  Insert gate at nearest pair");
                GUILayout.Label("  Alt+Click    Remove inserted / toggle enabled");
                GUILayout.Label("  Right-click  Gate context menu (selected)");
            }

            if (_authoringMode == AuthoringMode.Fences && fenceHoleEditMode)
            {
                GUILayout.Space(4);
                GUILayout.Label("Fence Mode:");
                GUILayout.Label("  Shift+Click  Add hole (gap) on chosen side");
                GUILayout.Label("  Click       Select hole");
                GUILayout.Label("  Drag        Move selected hole endpoints");
                GUILayout.Label("  Delete      Remove selected hole");
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private string GetOverlayModeLabel()
        {
            switch (_authoringMode)
            {
                case AuthoringMode.Flags: return "Flags";
                case AuthoringMode.Fences: return "Fences";
                default: return "Path";
            }
        }

        private string GetOverlayInstruction()
        {
            switch (_authoringMode)
            {
                case AuthoringMode.Flags:
                    return gateEditMode
                        ? "Edit Gates is active. Drag gate sides in the Scene view."
                        : "Flags mode is active. Enable Edit Gates to adjust gate placement.";
                case AuthoringMode.Fences:
                    return fenceHoleEditMode
                        ? "Edit Gaps is active. Select a gap and press Delete to remove it."
                        : "Fences mode is active. Enable Edit Gaps to add or modify fence holes.";
                default:
                    return showPointGizmos
                        ? "Edit Points is active. Select, move, insert, and delete run points in the Scene view."
                        : "Path mode is active. Enable Edit Points to work with point handles.";
            }
        }

        private string GetOverlayVisibilitySummary()
        {
            var parts = new List<string>(3);
            if (showBoundaryPreview) parts.Add("Corridor");
            if (showPairPreview) parts.Add("Gates");
            if (showIntersectionOverlay) parts.Add("Intersections");
            return parts.Count > 0 ? string.Join(" • ", parts) : "Preview hidden";
        }

        private static SkiRunLineEditor _activeEditorInstance;

        // ------------------------------------------------------------
        // Overlay Bridge (single source of truth = active editor instance)
        // ------------------------------------------------------------

        private static SkiRunLineEditor _active;

        internal static bool OverlayHasActiveRun()
        {
            if (_active == null) return false;
            return _active.target is SkiRunLine;
        }

        internal static string OverlayGetRunName()
        {
            if (_active == null) return "No active run";
            var run = _active.target as SkiRunLine;
            return run != null ? run.name : "No active run";
        }

        internal static string OverlayGetModeLabel() => _active != null ? _active.GetOverlayModeLabel() : "Path";
        internal static string OverlayGetInstruction() => _active != null ? _active.GetOverlayInstruction() : "Select a SkiRunLine to begin.";
        internal static string OverlayGetVisibilitySummary() => _active != null ? _active.GetOverlayVisibilitySummary() : "";

        internal static void OverlayFrameActiveRun()
        {
            if (_active == null) return;
            var run = _active.target as SkiRunLine;
            if (run == null) return;

            Selection.activeGameObject = run.gameObject;
            EditorGUIUtility.PingObject(run.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        internal static void OverlayOpenPainter(bool enablePaint)
        {
            if (_active == null) return;
            var run = _active.target as SkiRunLine;
            if (run == null) return;
            SkiRunPainterWindow.OpenAndSelect(run, enablePaint);
        }

        internal static int OverlayGetMode() => _active != null ? (int)_active._authoringMode : 0;

        internal static void OverlaySetMode(int mode)
        {
            if (_active == null) return;
            var run = _active.target as SkiRunLine;
            if (run == null) return;

            _active.SetAuthoringMode((AuthoringMode)Mathf.Clamp(mode, 0, 2), run);
            _active.Repaint();
        }

        private void OverlaySetBool(ref bool field, bool value, bool affectsPreview)
        {
            field = value;
            if (affectsPreview) _previewDirty = true;

            SaveEditorPrefs();
            Repaint();
            SceneView.RepaintAll();
        }

        // ---- Overlay getters ----
        internal static bool OverlayGetShowBoundaryPreview() => _active != null && _active.showBoundaryPreview;
        internal static bool OverlayGetShowPairPreview() => _active != null && _active.showPairPreview;
        internal static bool OverlayGetShowIntersectionOverlay() => _active != null && _active.showIntersectionOverlay;

        internal static bool OverlayGetShowPointGizmos() => _active != null && _active.showPointGizmos;

        internal static bool OverlayGetGateEditMode() => _active != null && _active.gateEditMode;
        internal static bool OverlayGetFenceHoleEditMode() => _active != null && _active.fenceHoleEditMode;

        // ---- Overlay setters ----
        internal static void OverlaySetShowBoundaryPreview(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.showBoundaryPreview, value, affectsPreview: true);
        }

        internal static void OverlaySetShowPairPreview(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.showPairPreview, value, affectsPreview: true);
        }

        internal static void OverlaySetShowIntersectionOverlay(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.showIntersectionOverlay, value, affectsPreview: true);
        }

        internal static void OverlaySetShowPointGizmos(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.showPointGizmos, value, affectsPreview: false);
        }

        internal static void OverlaySetGateEditMode(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.gateEditMode, value, affectsPreview: false);
        }

        internal static void OverlaySetFenceHoleEditMode(bool value)
        {
            if (_active == null) return;
            _active.OverlaySetBool(ref _active.fenceHoleEditMode, value, affectsPreview: false);
        }

        private void OnFocus()
        {
            _activeEditorInstance = this;
        }

        private void OnDestroy()
        {
            if (_activeEditorInstance == this)
                _activeEditorInstance = null;
        }

        // Called by overlay

        internal static void OverlayToggle(ref bool field, bool value, bool affectsPreview = false)
        {
            if (_activeEditorInstance == null) return;

            field = value;

            if (affectsPreview)
                _activeEditorInstance._previewDirty = true;

            _activeEditorInstance.SaveEditorPrefs();
            SceneView.RepaintAll();
            _activeEditorInstance.Repaint();
        }

        internal void LoadEditorPrefs()
        {
            showBoundaryPreview = EditorPrefs.GetBool(PrefKey + "showBoundaryPreview", showBoundaryPreview);
            showPairPreview = EditorPrefs.GetBool(PrefKey + "showPairPreview", showPairPreview);
            showIntersectionOverlay = EditorPrefs.GetBool(PrefKey + "showIntersectionOverlay", showIntersectionOverlay);
            previewIncludeOverlapAvoidance = EditorPrefs.GetBool(PrefKey + "previewIncludeOverlapAvoidance", previewIncludeOverlapAvoidance);

            showPointGizmos = EditorPrefs.GetBool(PrefKey + "showPointGizmos", showPointGizmos);
            //showAnchorPointMarkers = EditorPrefs.GetBool(PrefKey + "showAnchorPointMarkers", showAnchorPointMarkers);
            showWidthVisuals = EditorPrefs.GetBool(PrefKey + "showWidthHandles", showWidthVisuals);
            showSpacingHandle = EditorPrefs.GetBool(PrefKey + "showSpacingHandle", showSpacingHandle);

            autoSnapToTerrainWhenEditing = EditorPrefs.GetBool(PrefKey + "autoSnapToTerrainWhenEditing", autoSnapToTerrainWhenEditing);

            previewMaxPairs = EditorPrefs.GetInt(PrefKey + "previewMaxPairs", previewMaxPairs);
            sceneVisualYOffset = EditorPrefs.GetFloat(PrefKey + "sceneVisualYOffset", sceneVisualYOffset);

            showHotkeyOverlay = EditorPrefs.GetBool(PrefKey + "showHotkeyOverlay", showHotkeyOverlay);
            gateEditMode = EditorPrefs.GetBool(PrefKey + "gateEditMode", gateEditMode);

            _authoringMode = (AuthoringMode)EditorPrefs.GetInt(PrefKey + "authoringMode", (int)_authoringMode);
            fenceHoleEditMode = EditorPrefs.GetBool(PrefKey + "fenceHoleEditMode", fenceHoleEditMode);


        }

        internal void SaveEditorPrefs()
        {
            EditorPrefs.SetBool(PrefKey + "showBoundaryPreview", showBoundaryPreview);
            EditorPrefs.SetBool(PrefKey + "showPairPreview", showPairPreview);
            EditorPrefs.SetBool(PrefKey + "showIntersectionOverlay", showIntersectionOverlay);
            EditorPrefs.SetBool(PrefKey + "previewIncludeOverlapAvoidance", previewIncludeOverlapAvoidance);

            EditorPrefs.SetBool(PrefKey + "showPointGizmos", showPointGizmos);
            //EditorPrefs.SetBool(PrefKey + "showAnchorPointMarkers", showAnchorPointMarkers);
            EditorPrefs.SetBool(PrefKey + "showWidthHandles", showWidthVisuals);
            EditorPrefs.SetBool(PrefKey + "showSpacingHandle", showSpacingHandle);

            EditorPrefs.SetBool(PrefKey + "autoSnapToTerrainWhenEditing", autoSnapToTerrainWhenEditing);

            EditorPrefs.SetInt(PrefKey + "previewMaxPairs", previewMaxPairs);
            EditorPrefs.SetFloat(PrefKey + "sceneVisualYOffset", sceneVisualYOffset);

            EditorPrefs.SetBool(PrefKey + "showHotkeyOverlay", showHotkeyOverlay);
            EditorPrefs.SetBool(PrefKey + "gateEditMode", gateEditMode);

            EditorPrefs.SetInt(PrefKey + "authoringMode", (int)_authoringMode);
            EditorPrefs.SetBool(PrefKey + "fenceHoleEditMode", fenceHoleEditMode);

        }

    }
}
#endif
