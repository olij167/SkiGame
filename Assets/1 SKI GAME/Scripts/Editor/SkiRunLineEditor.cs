#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiGame.Runs;
using UnityEngine.Rendering;

namespace SkiGame.RunsEditor
{
    [CustomEditor(typeof(SkiRunLine))]
    public sealed class SkiRunLineEditor : Editor
    {
        // --- Scene display toggles ---
        private bool showPointGizmos = true;          // clickable point markers (selection)
        private bool showWidthVisuals = true;         // width ticks (non-interactive) for anchors
        private bool showBoundaryPreview = true;      // left/right corridor lines
        private bool showPairPreview = true;          // crossbars at spawn samples
        private bool showSpacingHandle = true;        // drag handle near start

        // --- Edit interaction toggles ---
        private bool enableShiftClickInsert = true;
        private bool enableAltClickDelete = true;
        private bool editNeighborPoints = false;      // show move handle for selected +/- 1

        // --- Selection behaviour ---
        private int selectedPointIndex = -1;
        private float pointPickSizeScale = 0.08f;

        // --- Preview density / performance ---
        private int previewMaxPairs = 500;

        // Cached preview buffers
        private readonly List<Vector3> _prevCenters = new();
        private readonly List<Vector3> _prevLeft = new();
        private readonly List<Vector3> _prevRight = new();

        // --- Authoring utilities ---
        private float resampleSpacingMeters = 12f;
        private float widthAnchorSpacingMeters = 30f;
        private float autoWidthMaxHalfWidthMeters = 60f;
        private bool autoWidthUseMinSide = true;

        // Width visualisation density
        private bool widthVisualsOnlyAnchors = true;

        // --- Point gizmo clarity / declutter ---
        private bool depthTestPointGizmos = true;     // prevent drawing through terrain
        private bool fadePointsByDistance = true;     // fade points further from camera
        private float fadeNearMeters = 25f;           // fully visible at/near this distance
        private float fadeFarMeters = 250f;           // near-invisible at/after this distance
        private float fadeMinAlpha = 0.08f;           // alpha at fadeFarMeters

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Scene View", EditorStyles.boldLabel);

                showPointGizmos = EditorGUILayout.ToggleLeft("Show Point Gizmos (click to select)", showPointGizmos);
                showWidthVisuals = EditorGUILayout.ToggleLeft("Show Width Visuals (anchors)", showWidthVisuals);
                showBoundaryPreview = EditorGUILayout.ToggleLeft("Show Boundary Preview", showBoundaryPreview);
                showPairPreview = EditorGUILayout.ToggleLeft("Show Pair Preview", showPairPreview);
                showSpacingHandle = EditorGUILayout.ToggleLeft("Show Pair Spacing Handle", showSpacingHandle);

                EditorGUILayout.Space(4);
                enableShiftClickInsert = EditorGUILayout.ToggleLeft("Shift-Click Insert Point (on segment)", enableShiftClickInsert);
                enableAltClickDelete = EditorGUILayout.ToggleLeft("Alt-Click Delete Point", enableAltClickDelete);
                editNeighborPoints = EditorGUILayout.ToggleLeft("Edit Neighbors (selected +/- 1)", editNeighborPoints);

                pointPickSizeScale = EditorGUILayout.Slider("Point Gizmo Size", pointPickSizeScale, 0.03f, 0.18f);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Point Gizmo Clarity", EditorStyles.boldLabel);

                depthTestPointGizmos = EditorGUILayout.ToggleLeft("Occlude Point Gizmos (depth test)", depthTestPointGizmos);
                fadePointsByDistance = EditorGUILayout.ToggleLeft("Fade Points By Camera Distance", fadePointsByDistance);

                using (new EditorGUI.DisabledScope(!fadePointsByDistance))
                {
                    fadeNearMeters = EditorGUILayout.Slider("Fade Near (m)", fadeNearMeters, 5f, 200f);
                    fadeFarMeters = EditorGUILayout.Slider("Fade Far (m)", fadeFarMeters, 25f, 2000f);
                    fadeMinAlpha = EditorGUILayout.Slider("Min Alpha", fadeMinAlpha, 0.01f, 0.35f);
                }

                previewMaxPairs = EditorGUILayout.IntSlider(new GUIContent("Preview Max Pairs"), previewMaxPairs, 50, 5000);

                widthVisualsOnlyAnchors = EditorGUILayout.ToggleLeft("Width Visuals: Anchors Only", widthVisualsOnlyAnchors);

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = selectedPointIndex >= 0;
                    if (GUILayout.Button("Focus Selected"))
                        FocusSelectedPoint();
                    GUI.enabled = true;

                    if (GUILayout.Button("Clear Selection (Esc)"))
                    {
                        selectedPointIndex = -1;
                        SceneView.RepaintAll();
                    }
                }

                var run = (SkiRunLine)target;

                EditorGUILayout.Space(6);
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
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Authoring Utilities", EditorStyles.boldLabel);

                resampleSpacingMeters = EditorGUILayout.Slider("Control Point Spacing (m)", resampleSpacingMeters, 5f, 50f);
                widthAnchorSpacingMeters = EditorGUILayout.Slider("Width Anchor Spacing (m)", widthAnchorSpacingMeters, 10f, 200f);
                autoWidthMaxHalfWidthMeters = EditorGUILayout.Slider("Auto Width Max Half (m)", autoWidthMaxHalfWidthMeters, 5f, 200f);
                autoWidthUseMinSide = EditorGUILayout.ToggleLeft("Auto Width Uses Min Side (safer)", autoWidthUseMinSide);

                var run = (SkiRunLine)target;

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate Control Points (Resample)"))
                    {
                        Undo.RecordObject(run, "Resample Run Points");
                        run.ResamplePointsWorld(resampleSpacingMeters);
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Auto Detect Width (Anchors)"))
                    {
                        Undo.RecordObject(run, "Auto Detect Run Width");
                        run.AutoDetectWidthOverrides(widthAnchorSpacingMeters, autoWidthMaxHalfWidthMeters, autoWidthUseMinSide);
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Clear Width Overrides"))
                {
                    Undo.RecordObject(run, "Clear Width Overrides");
                    var so = new SerializedObject(run);
                    var widthsProp = so.FindProperty("widthOverrideMeters");
                    if (widthsProp != null)
                    {
                        for (int i = 0; i < widthsProp.arraySize; i++)
                            widthsProp.GetArrayElementAtIndex(i).floatValue = -1f;

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var run = (SkiRunLine)target;

            // Esc clears selection
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                selectedPointIndex = -1;
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            SerializedObject so = new SerializedObject(run);
            SerializedProperty pointsProp = so.FindProperty("pointsWorld");
            SerializedProperty widthsProp = so.FindProperty("widthOverrideMeters");

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

            // Shift insert / Alt delete
            HandleInsertDelete(run, so, pointsProp, widthsProp);

            // Draw previews first (so point gizmos sit on top)
            if (showBoundaryPreview || showPairPreview)
                BuildPreview(run, so, pointsProp, widthsProp);

            if (showBoundaryPreview)
                DrawBoundaryPreview(run);

            if (showPairPreview)
                DrawPairPreview(run);

            if (showSpacingHandle)
                DrawSpacingHandle(run, so, pointsProp);

            // Clickable point gizmos (selection), always cheap.
            if (showPointGizmos)
                DrawPointGizmosAndSelection(run, so, pointsProp, widthsProp);

            // Width visuals persistently (anchors only), no slider unless selected.
            if (showWidthVisuals)
                DrawWidthVisuals(run, so, pointsProp, widthsProp);

            // Contextual movement/width handles: selected only (optionally neighbors)
            DrawContextualEditHandles(run, so, pointsProp, widthsProp);
        }

        private void FocusSelectedPoint()
        {
            var run = (SkiRunLine)target;
            var so = new SerializedObject(run);
            var pointsProp = so.FindProperty("pointsWorld");
            if (pointsProp == null) return;
            if (selectedPointIndex < 0 || selectedPointIndex >= pointsProp.arraySize) return;

            Vector3 p = pointsProp.GetArrayElementAtIndex(selectedPointIndex).vector3Value;
            Terrain t = ResolveTerrainAt(p);
            Vector3 ps = SnapToTerrain(t, p);

            SceneView.lastActiveSceneView?.LookAt(ps, SceneView.lastActiveSceneView.rotation, 20f);
        }

        private void DrawPointGizmosAndSelection(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            Event e = Event.current;
            if (e == null) return;

            // Avoid stealing clicks from insert/delete modes
            bool blockSelect = (enableShiftClickInsert && e.shift) || (enableAltClickDelete && e.alt);

            // SceneView camera for distance fade
            Camera cam = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

            // Depth test so points don't draw through terrain
            CompareFunction prevZ = Handles.zTest;
            Handles.zTest = depthTestPointGizmos ? CompareFunction.LessEqual : CompareFunction.Always;

            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;

                Terrain t = ResolveTerrainAt(p);
                Vector3 ps = SnapToTerrain(t, p);

                float baseSize = HandleUtility.GetHandleSize(ps) * pointPickSizeScale;

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
                if (!blockSelect && Handles.Button(ps, Quaternion.identity, baseSize, baseSize * 1.2f, Handles.DotHandleCap))
                {
                    selectedPointIndex = i;
                    SceneView.RepaintAll();
                }

                // Small label for selected
                if (isSelected)
                {
                    Handles.color = new Color(1f, 1f, 1f, 0.95f);
                    Handles.Label(ps + Vector3.up * HandleUtility.GetHandleSize(ps) * 0.05f, $"Point {i}");
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
                Vector3 pSnapped = SnapToTerrain(tPoint, p);

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
            Vector3 pSnapped = SnapToTerrain(tPoint, p);

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
                pointsProp.GetArrayElementAtIndex(i).vector3Value = newPos;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            }
        }

        private void DrawWidthSliderForSelected(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp, int i)
        {
            SerializedProperty runWidthProp = so.FindProperty("runWidthMeters");
            if (runWidthProp == null) return;

            Vector3 p = pointsProp.GetArrayElementAtIndex(i).vector3Value;
            Terrain tPoint = ResolveTerrainAt(p);
            Vector3 pSnapped = SnapToTerrain(tPoint, p);

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

                    int insertAfter = FindClosestSegmentIndex(pointsProp, hitPos);
                    if (insertAfter >= 0)
                    {
                        Undo.RecordObject(run, "Insert Run Point");

                        int insertIndex = insertAfter + 1;

                        pointsProp.InsertArrayElementAtIndex(insertIndex);
                        pointsProp.GetArrayElementAtIndex(insertIndex).vector3Value = hitPos;

                        widthsProp.InsertArrayElementAtIndex(insertIndex);
                        widthsProp.GetArrayElementAtIndex(insertIndex).floatValue = -1f;

                        // Prefer selecting the inserted point
                        selectedPointIndex = insertIndex;

                        so.ApplyModifiedProperties();
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

        private void BuildPreview(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp, SerializedProperty widthsProp)
        {
            _prevCenters.Clear();
            _prevLeft.Clear();
            _prevRight.Clear();

            if (pointsProp.arraySize < 2) return;

            SerializedProperty spacingProp = so.FindProperty("flagSpacingMeters");
            SerializedProperty startOffsetProp = so.FindProperty("flagStartOffsetMeters");
            SerializedProperty endInsetProp = so.FindProperty("flagEndInsetMeters");
            SerializedProperty heightOffsetProp = so.FindProperty("flagHeightOffset");
            SerializedProperty runWidthProp = so.FindProperty("runWidthMeters");
            SerializedProperty snapSidesProp = so.FindProperty("snapSidesToTerrainIndividually");

            SerializedProperty terrainAwareProp = so.FindProperty("terrainAwareBoundaries");
            SerializedProperty searchStepProp = so.FindProperty("boundarySearchStepMeters");
            SerializedProperty maxSlopeProp = so.FindProperty("boundaryMaxSlopeDeg");
            SerializedProperty maxHeightDeltaProp = so.FindProperty("boundaryMaxHeightDelta");
            SerializedProperty preferFurthestProp = so.FindProperty("boundaryPreferFurthestValid");

            if (spacingProp == null || startOffsetProp == null || endInsetProp == null || heightOffsetProp == null ||
                runWidthProp == null || snapSidesProp == null ||
                terrainAwareProp == null || searchStepProp == null || maxSlopeProp == null || maxHeightDeltaProp == null || preferFurthestProp == null)
                return;

            float spacing = Mathf.Max(0.25f, spacingProp.floatValue);
            float startOffset = Mathf.Max(0f, startOffsetProp.floatValue);
            float endInset = Mathf.Max(0f, endInsetProp.floatValue);
            float yOffset = Mathf.Max(0f, heightOffsetProp.floatValue);
            float globalWidth = Mathf.Max(2f, runWidthProp.floatValue);
            bool snapSides = snapSidesProp.boolValue;

            bool terrainAware = terrainAwareProp.boolValue;
            float searchStep = Mathf.Max(0.1f, searchStepProp.floatValue);
            float maxSlope = Mathf.Clamp(maxSlopeProp.floatValue, 0f, 89f);
            float maxHeightDelta = Mathf.Max(0f, maxHeightDeltaProp.floatValue);
            bool preferFurthest = preferFurthestProp.boolValue;

            float runLen = ComputePolylineLength(pointsProp);
            if (runLen <= 0.001f) return;

            float spawnStart = startOffset;
            float spawnEnd = Mathf.Max(0f, runLen - endInset);
            if (spawnStart >= spawnEnd) return;

            float total = 0f;
            float nextSpawn = spawnStart;
            int pairs = 0;

            for (int seg = 0; seg < pointsProp.arraySize - 1; seg++)
            {
                Vector3 a = pointsProp.GetArrayElementAtIndex(seg).vector3Value;
                Vector3 b = pointsProp.GetArrayElementAtIndex(seg + 1).vector3Value;

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 tangent = (b - a) / segLen;
                float segStartDist = total;
                float segEndDist = total + segLen;

                if (nextSpawn > segEndDist)
                {
                    total += segLen;
                    continue;
                }

                if (nextSpawn < segStartDist)
                    nextSpawn = segStartDist;

                while (nextSpawn <= segEndDist && nextSpawn <= spawnEnd)
                {
                    if (pairs++ >= previewMaxPairs) return;

                    float t = nextSpawn - segStartDist;
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(t / segLen);

                    Vector3 center = a + tangent * t;
                    Terrain tCenter = ResolveTerrainAt(center);
                    if (tCenter == null)
                    {
                        nextSpawn += spacing;
                        continue;
                    }

                    Vector3 centerGround = SnapToTerrain(tCenter, center);
                    Vector3 up = SampleNormal(tCenter, centerGround);

                    Vector3 smoothTangent = GetSmoothedTangent(pointsProp, seg, a, b, tangent);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(pointsProp, widthsProp, seg, segT, globalWidth));
                    float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                    Vector3 left = FindBoundaryPointPreview(
                        tCenter,
                        centerGround,
                        lateral,
                        halfW,
                        true,
                        terrainAware,
                        searchStep,
                        maxSlope,
                        maxHeightDelta,
                        preferFurthest,
                        snapSides,
                        yOffset);

                    Vector3 right = FindBoundaryPointPreview(
                        tCenter,
                        centerGround,
                        lateral,
                        halfW,
                        false,
                        terrainAware,
                        searchStep,
                        maxSlope,
                        maxHeightDelta,
                        preferFurthest,
                        snapSides,
                        yOffset);

                    if (snapSides)
                    {
                        left = SnapToTerrain(ResolveTerrainAt(left) ?? tCenter, left);
                        right = SnapToTerrain(ResolveTerrainAt(right) ?? tCenter, right);
                        left.y += yOffset;
                        right.y += yOffset;
                    }
                    else
                    {
                        float y = centerGround.y + yOffset;
                        left.y = y;
                        right.y = y;
                    }

                    _prevCenters.Add(centerGround + Vector3.up * yOffset);
                    _prevLeft.Add(left);
                    _prevRight.Add(right);

                    nextSpawn += spacing;
                }

                total += segLen;
            }
        }

        private void DrawBoundaryPreview(SkiRunLine run)
        {
            if (_prevLeft.Count < 2) return;

            Color c = run.RunColor;
            Color lc = new Color(c.r, c.g, c.b, 0.55f);

            Handles.color = lc;
            Handles.DrawAAPolyLine(3f, _prevLeft.ToArray());
            Handles.DrawAAPolyLine(3f, _prevRight.ToArray());
        }

        private void DrawPairPreview(SkiRunLine run)
        {
            if (_prevLeft.Count == 0) return;

            Color c = run.RunColor;
            Handles.color = new Color(1f, 1f, 1f, 0.8f);

            for (int i = 0; i < _prevLeft.Count; i++)
            {
                Vector3 l = _prevLeft[i];
                Vector3 r = _prevRight[i];

                Handles.DrawLine(l, r);

                float s = HandleUtility.GetHandleSize(_prevCenters[i]) * 0.03f;
                Handles.SphereHandleCap(0, l, Quaternion.identity, s, EventType.Repaint);
                Handles.SphereHandleCap(0, r, Quaternion.identity, s, EventType.Repaint);

                if (i % 15 == 0)
                {
                    Handles.color = new Color(c.r, c.g, c.b, 0.95f);
                    Handles.Label(_prevCenters[i] + Vector3.up * HandleUtility.GetHandleSize(_prevCenters[i]) * 0.03f, $"Pair {i}");
                    Handles.color = new Color(1f, 1f, 1f, 0.8f);
                }
            }
        }

        private void DrawSpacingHandle(SkiRunLine run, SerializedObject so, SerializedProperty pointsProp)
        {
            SerializedProperty spacingProp = so.FindProperty("flagSpacingMeters");
            if (spacingProp == null) return;

            if (pointsProp.arraySize == 0) return;

            Vector3 p0 = pointsProp.GetArrayElementAtIndex(0).vector3Value;
            Terrain t0 = ResolveTerrainAt(p0);
            Vector3 p0s = SnapToTerrain(t0, p0);

            Vector3 dir = SceneView.currentDrawingSceneView != null
                ? SceneView.currentDrawingSceneView.camera.transform.right
                : Vector3.right;

            float handleSize = HandleUtility.GetHandleSize(p0s) * 0.7f;

            float spacing = spacingProp.floatValue;
            Vector3 handlePos = p0s + dir * (handleSize * 0.6f);

            Handles.color = Color.white;
            Handles.Label(handlePos + Vector3.up * handleSize * 0.05f, $"Pair Spacing: {spacing:0.0}m");

            EditorGUI.BeginChangeCheck();
            float newSpacing = Handles.ScaleSlider(spacing, handlePos, dir, Quaternion.identity, handleSize, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                newSpacing = Mathf.Clamp(newSpacing, 1f, 50f);
                Undo.RecordObject(run, "Adjust Pair Spacing");
                spacingProp.floatValue = newSpacing;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(run);
                SceneView.RepaintAll();
            }
        }

        // --- Preview helpers (remain unchanged from your existing toolchain) ---

        private float ComputePolylineLength(SerializedProperty pointsProp)
        {
            float len = 0f;
            for (int i = 0; i < pointsProp.arraySize - 1; i++)
            {
                Vector3 a = pointsProp.GetArrayElementAtIndex(i).vector3Value;
                Vector3 b = pointsProp.GetArrayElementAtIndex(i + 1).vector3Value;
                len += Vector3.Distance(a, b);
            }
            return len;
        }

        private Vector3 GetSmoothedTangent(SerializedProperty pointsProp, int seg, Vector3 a, Vector3 b, Vector3 fallback)
        {
            Vector3 t = fallback;

            if (seg > 0 && seg < pointsProp.arraySize - 2)
            {
                Vector3 prev = pointsProp.GetArrayElementAtIndex(seg - 1).vector3Value;
                Vector3 next = pointsProp.GetArrayElementAtIndex(seg + 2).vector3Value;

                Vector3 ta = (a - prev);
                Vector3 tb = (next - b);

                if (ta.sqrMagnitude > 0.00001f) ta.Normalize();
                if (tb.sqrMagnitude > 0.00001f) tb.Normalize();

                Vector3 blended = (ta + fallback + tb);
                if (blended.sqrMagnitude > 0.00001f)
                    t = blended.normalized;
            }

            return t;
        }

        private float GetWidthMetersAtSample(SerializedProperty pointsProp, SerializedProperty widthsProp, int seg, float segT, float globalWidth)
        {
            int i0 = Mathf.Clamp(seg, 0, widthsProp.arraySize - 1);
            int i1 = Mathf.Clamp(seg + 1, 0, widthsProp.arraySize - 1);

            float w0 = widthsProp.GetArrayElementAtIndex(i0).floatValue;
            float w1 = widthsProp.GetArrayElementAtIndex(i1).floatValue;

            float a = (w0 > 0.01f) ? w0 : globalWidth;
            float b = (w1 > 0.01f) ? w1 : globalWidth;

            return Mathf.Lerp(a, b, segT);
        }

        private Vector3 FindBoundaryPointPreview(
            Terrain terrainAtCenter,
            Vector3 centerGround,
            Vector3 lateral,
            float halfW,
            bool isLeft,
            bool terrainAware,
            float searchStep,
            float maxSlopeDeg,
            float maxHeightDelta,
            bool preferFurthest,
            bool snapSides,
            float yOffset)
        {
            // Defer to SkiRunLine’s boundary behaviour if you already have it implemented there.
            // This editor preview uses a simplified scan fallback if needed.

            Vector3 dir = isLeft ? -lateral : lateral;

            if (!terrainAware)
                return centerGround + dir * halfW;

            Vector3 best = centerGround + dir * halfW;
            float bestD = 0f;

            float maxD = Mathf.Max(1f, halfW);

            for (float d = searchStep; d <= maxD; d += searchStep)
            {
                Vector3 p = centerGround + dir * d;
                Terrain t = ResolveTerrainAt(p) ?? terrainAtCenter;
                if (t == null) continue;

                Vector3 pG = SnapToTerrain(t, p);
                float dh = Mathf.Abs(pG.y - centerGround.y);
                if (dh > maxHeightDelta) continue;

                Vector3 n = SampleNormal(t, pG);
                float slope = Vector3.Angle(n, Vector3.up);
                if (slope > maxSlopeDeg) continue;

                if (preferFurthest)
                {
                    best = pG;
                    bestD = d;
                }
                else
                {
                    // Take first valid
                    best = pG;
                    bestD = d;
                    break;
                }
            }

            if (!snapSides)
            {
                best.y = centerGround.y;
            }

            // yOffset applied later by caller
            return best;
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
    }
}
#endif
