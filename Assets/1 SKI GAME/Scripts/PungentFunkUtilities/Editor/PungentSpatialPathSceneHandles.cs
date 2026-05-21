using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    internal static class PungentSpatialPathSceneHandles
    {
        public static void Draw(
            ModularPathSpawner path,
            ref int selectedIndex,
            ref bool pendingRebuildAfterDrag,
            bool editingActive,
            SceneView sceneView,
            ref double nextAllowedSceneRepaintTime)
        {
            if (path == null)
                return;

            PungentSpatialSceneHandleUtility.BeginSceneGUI();
            if (editingActive && PungentSpatialAuthoringEditorState.ShouldCaptureSceneInput)
                PungentSpatialSceneHandleUtility.ProtectSelectionIfNeeded(true);

            if (path.previewWhileEditing)
                DrawPolyline(path);

            DrawPointHandles(path, ref selectedIndex, ref pendingRebuildAfterDrag, editingActive, sceneView, ref nextAllowedSceneRepaintTime);
            DrawCorridorWidthHandle(path, selectedIndex, editingActive, sceneView, ref nextAllowedSceneRepaintTime);

            if (editingActive)
            {
                DrawContextualPreview(path, selectedIndex, sceneView);
                HandleMouseAddInsert(path, ref selectedIndex, sceneView, ref nextAllowedSceneRepaintTime);
                HandleMouseDelete(path, ref selectedIndex, sceneView, ref nextAllowedSceneRepaintTime);
                HandleDeleteKey(path, ref selectedIndex, sceneView, ref nextAllowedSceneRepaintTime);
            }

            Event e = Event.current;
            if (pendingRebuildAfterDrag && e != null && e.rawType == EventType.MouseUp)
            {
                pendingRebuildAfterDrag = false;
                QueueRebuild(path, sceneView, ref nextAllowedSceneRepaintTime);
            }
        }

        private static void DrawPolyline(ModularPathSpawner path)
        {
            int n = path.PointCount;
            if (n < 2)
                return;

            Color previous = Handles.color;
            Handles.color = new Color(0.7f, 0.95f, 1f, 0.8f);

            bool minimal = PungentSpatialAuthoringEditorState.PreviewQuality == PungentSpatialPreviewQuality.Minimal ||
                           path.PointCount >= ModularPathSpawner.HighSampledPointWarningThreshold / 2;

            if (minimal || !path.BuildSampledWorldPath(out var sampled, out bool sampledClosed))
            {
                for (int i = 0; i < n - 1; i++)
                    Handles.DrawLine(path.GetWorldPoint(i), path.GetWorldPoint(i + 1));

                if (path.closedLoop && n > 2)
                    Handles.DrawLine(path.GetWorldPoint(n - 1), path.GetWorldPoint(0));
                Handles.color = previous;
                return;
            }

            for (int i = 0; i < sampled.Count - 1; i++)
                Handles.DrawLine(sampled[i], sampled[i + 1]);

            if (sampledClosed && sampled.Count > 2)
                Handles.DrawLine(sampled[sampled.Count - 1], sampled[0]);

            if (path.exposePreviewCorridorWidth && PungentSpatialAuthoringEditorState.DrawRichPreview && sampled.Count < ModularPathSpawner.HighSampledPointWarningThreshold)
                PungentPathAuthoringToolkit.DrawWidthPreview(sampled, path.previewCorridorWidth, new Color(path.pathColor.r, path.pathColor.g, path.pathColor.b, Mathf.Clamp01(path.previewCorridorAlpha)));

            Handles.color = previous;
        }

        private static void DrawPointHandles(
            ModularPathSpawner path,
            ref int selectedIndex,
            ref bool pendingRebuildAfterDrag,
            bool editingActive,
            SceneView sceneView,
            ref double nextAllowedSceneRepaintTime)
        {
            int n = path.PointCount;
            int hoveredIndex = -1;
            DrawEdgeControls(path, editingActive, ref hoveredIndex);

            for (int i = 0; i < n; i++)
            {
                Vector3 worldPoint = path.GetWorldPoint(i);
                float size = HandleUtility.GetHandleSize(worldPoint) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.08f;
                float pickSize = Mathf.Max(size * 3.1f, HandleUtility.GetHandleSize(worldPoint) * 0.055f);
                bool selected = selectedIndex == i;
                int controlId = PungentSpatialSceneHandleUtility.GetPointControlId(path, i);
                bool hovered = PungentSpatialSceneHandleUtility.RegisterPointControl(controlId, worldPoint, pickSize);
                if (hovered)
                    hoveredIndex = i;

                if (editingActive &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Delete &&
                    IsLeftMouseDownOnControl(controlId))
                {
                    Undo.RecordObject(path, "Delete Path Point");
                    path.RemovePoint(i);
                    selectedIndex = Mathf.Clamp(i - 1, -1, path.PointCount - 1);
                    EditorUtility.SetDirty(path);
                    QueueRebuild(path, sceneView, ref nextAllowedSceneRepaintTime);
                    Event.current.Use();
                    PungentSpatialAuthoringEditorState.ReportAction("Deleted path point " + i + ".");
                    PungentSpatialAuthoringEditorState.SetHandleSelection("Path", selectedIndex, -1, path.PointCount);
                    return;
                }

                if (editingActive && PungentSpatialSceneHandleUtility.TryBeginPointDrag(controlId, Event.current))
                    selectedIndex = i;

                if (editingActive &&
                    selected &&
                    PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Move &&
                    PungentSpatialSceneHandleUtility.IsDraggingPoint(controlId, Event.current) &&
                    TryMouseToWorld(path, Event.current.mousePosition, out Vector3 moved))
                {
                    Undo.RecordObject(path, "Move Path Point");
                    path.SetWorldPoint(i, moved);
                    EditorUtility.SetDirty(path);
                    pendingRebuildAfterDrag = true;
                    PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
                    PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                    Event.current.Use();
                }

                if (PungentSpatialSceneHandleUtility.TryEndPointDrag(controlId, Event.current) && selected)
                    PungentSpatialAuthoringEditorState.ReportAction("Moved path point " + i + ".");

                Color color = selected
                    ? new Color(0.2f, 1f, 0.45f, 1f)
                    : hovered
                        ? new Color(1f, 0.95f, 0.42f, 1f)
                        : new Color(1f, 0.82f, 0.28f, 0.95f);
                PungentSpatialSceneHandleUtility.DrawPointCap(controlId, worldPoint, hovered ? size * 1.25f : size, color);

                if (PungentSpatialAuthoringEditorState.ShouldDrawPointLabel(selected))
                    Handles.Label(worldPoint + Vector3.up * (size * 6f), "Point " + i);
            }

            PungentSpatialAuthoringEditorState.SetHandleSelection("Path", selectedIndex, hoveredIndex, n);
        }

        private static void DrawCorridorWidthHandle(
            ModularPathSpawner path,
            int selectedIndex,
            bool editingActive,
            SceneView sceneView,
            ref double nextAllowedSceneRepaintTime)
        {
            if (!editingActive || path == null || path.PointCount < 2)
                return;

            if (!path.exposePreviewCorridorWidth && !PungentSpatialAuthoringEditorState.DrawRichPreview)
                return;

            Vector3 center = GetWidthHandleCenter(path, selectedIndex);
            Vector3 tangent = GetWidthHandleTangent(path, selectedIndex);
            if (tangent.sqrMagnitude < 0.0001f)
                return;

            Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
            if (side.sqrMagnitude < 0.0001f && sceneView != null && sceneView.camera != null)
                side = sceneView.camera.transform.right;
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.right;
            side.Normalize();

            float halfWidth = Mathf.Max(0.01f, path.previewCorridorWidth) * 0.5f;
            Vector3 handlePosition = center + side * halfWidth;
            Vector3 oppositeHandlePosition = center - side * halfWidth;
            float size = HandleUtility.GetHandleSize(handlePosition) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.08f;

            Color previous = Handles.color;
            Handles.color = new Color(path.pathColor.r, path.pathColor.g, path.pathColor.b, 0.88f);
            Handles.DrawAAPolyLine(3f, oppositeHandlePosition, handlePosition);
            Handles.DrawWireDisc(handlePosition, Vector3.up, size * 0.75f);
            Handles.DrawWireDisc(oppositeHandlePosition, Vector3.up, size * 0.75f);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(handlePosition, side, size, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float width = Mathf.Abs(Vector3.Dot(moved - center, side)) * 2f;
                Undo.RecordObject(path, "Set Path Corridor Width");
                path.previewCorridorWidth = Mathf.Max(0.01f, width);
                path.exposePreviewCorridorWidth = true;
                EditorUtility.SetDirty(path);
                PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                PungentSpatialAuthoringEditorState.ReportAction("Path corridor width " + path.previewCorridorWidth.ToString("0.##") + ".");
                if (sceneView != null)
                    PungentEditorPerformanceUtility.RequestSceneViewRepaintThrottled(sceneView, ref nextAllowedSceneRepaintTime, 0.08d);
            }

            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(handlePosition + Vector3.up * (size * 4f), "Width " + path.previewCorridorWidth.ToString("0.##"));

            Handles.color = previous;
        }

        private static void DrawContextualPreview(ModularPathSpawner path, int selectedIndex, SceneView sceneView)
        {
            if (path == null || PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(Event.current))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback(string.Empty));
                return;
            }

            PungentSpatialEditMode mode = PungentSpatialAuthoringEditorState.EditMode;
            if (mode == PungentSpatialEditMode.Add || mode == PungentSpatialEditMode.Insert || mode == PungentSpatialEditMode.Split)
            {
                DrawPlacementPreview(path, selectedIndex);
                return;
            }

            if (mode == PungentSpatialEditMode.Delete)
            {
                DrawDeletePreview(path);
                return;
            }

            PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback(string.Empty));
        }

        private static void DrawPlacementPreview(ModularPathSpawner path, int selectedIndex)
        {
            if (!TryBuildPlacementDecision(path, selectedIndex, out PungentSpatialPlacementDecision decision))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No valid placement point under cursor."));
                return;
            }

            Color previous = Handles.color;
            if (decision.Kind == PungentSpatialPlacementKind.Insert && decision.SegmentStart >= 0 && decision.SegmentEnd >= 0)
            {
                Handles.color = new Color(1f, 0.95f, 0.28f, 0.95f);
                Handles.DrawAAPolyLine(8f, path.GetWorldPoint(decision.SegmentStart), path.GetWorldPoint(decision.SegmentEnd));
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

        private static bool TryBuildPlacementDecision(ModularPathSpawner path, int selectedIndex, out PungentSpatialPlacementDecision decision)
        {
            decision = PungentSpatialPlacementDecision.None(string.Empty);
            Event e = Event.current;
            if (path == null || e == null || !TryMouseToWorld(path, e.mousePosition, out Vector3 worldHit))
                return false;

            bool forceAppend = e.shift && !e.control && !e.command && !e.alt;
            bool forceInsert = (e.control || e.command) && !e.shift && !e.alt;
            PungentSpatialEditMode mode = PungentSpatialAuthoringEditorState.EditMode;
            if (mode == PungentSpatialEditMode.Insert || mode == PungentSpatialEditMode.Split)
                forceInsert = true;

            int insertIndex = path.PointCount;
            bool insert = !forceAppend && TryFindSmartInsertIndex(path, worldHit, e.mousePosition, forceInsert, out insertIndex);
            int n = path.PointCount;
            int segmentStart = -1;
            int segmentEnd = -1;
            if (insert && n >= 2)
            {
                segmentStart = Mathf.Clamp(insertIndex - 1, 0, n - 1);
                segmentEnd = insertIndex >= n ? 0 : Mathf.Clamp(insertIndex, 0, n - 1);
            }

            int nextIndex = insert ? Mathf.Clamp(insertIndex, 0, n) : n;
            string text = insert
                ? "Insert path point " + nextIndex + " between P" + segmentStart + " and P" + segmentEnd + "."
                : selectedIndex >= 0 && selectedIndex < n
                    ? "Append path point after P" + selectedIndex + "."
                    : "Append path point " + nextIndex + ".";

            decision = new PungentSpatialPlacementDecision
            {
                Kind = insert ? PungentSpatialPlacementKind.Insert : PungentSpatialPlacementKind.Append,
                WorldPoint = worldHit,
                PointIndex = nextIndex,
                SegmentStart = segmentStart,
                SegmentEnd = segmentEnd,
                StatusText = text
            };
            return true;
        }

        private static void DrawDeletePreview(ModularPathSpawner path)
        {
            Event e = Event.current;
            if (path == null || e == null || !TryMouseToWorld(path, e.mousePosition, out Vector3 worldHit))
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No path point under cursor."));
                return;
            }

            int nearest = FindNearestPointIndex(path, worldHit);
            if (nearest < 0)
            {
                PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("No path point under cursor."));
                return;
            }

            Vector3 point = path.GetWorldPoint(nearest);
            float size = HandleUtility.GetHandleSize(point) * Mathf.Max(0.1f, PungentSpatialAuthoringEditorState.HandleSize) * 0.11f;
            Color previous = Handles.color;
            Handles.color = new Color(1f, 0.28f, 0.22f, 0.95f);
            Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
            Handles.DrawWireDisc(point, Vector3.up, size * 2f);
            if (PungentSpatialAuthoringEditorState.ShowLabels)
                Handles.Label(point + Vector3.up * (size * 4f), "Delete P" + nearest);
            Handles.color = previous;
            PungentSpatialAuthoringEditorState.SetHandleFeedback(new PungentSpatialHandleFeedback("Delete path point " + nearest + "."));
        }

        private static Vector3 GetWidthHandleCenter(ModularPathSpawner path, int selectedIndex)
        {
            if (path == null || path.PointCount <= 0)
                return Vector3.zero;

            if (selectedIndex >= 0 && selectedIndex < path.PointCount)
                return path.GetWorldPoint(selectedIndex);

            return (path.GetWorldPoint(0) + path.GetWorldPoint(1)) * 0.5f;
        }

        private static Vector3 GetWidthHandleTangent(ModularPathSpawner path, int selectedIndex)
        {
            int n = path != null ? path.PointCount : 0;
            if (n < 2)
                return Vector3.forward;

            if (selectedIndex > 0 && selectedIndex < n - 1)
                return path.GetWorldPoint(selectedIndex + 1) - path.GetWorldPoint(selectedIndex - 1);

            if (selectedIndex == 0)
                return path.GetWorldPoint(1) - path.GetWorldPoint(0);

            if (selectedIndex == n - 1)
                return path.GetWorldPoint(n - 1) - path.GetWorldPoint(n - 2);

            return path.GetWorldPoint(1) - path.GetWorldPoint(0);
        }

        private static void DrawEdgeControls(ModularPathSpawner path, bool editingActive, ref int hoveredIndex)
        {
            int n = path.PointCount;
            if (!editingActive || n < 2)
                return;

            int segmentCount = path.closedLoop ? n : n - 1;
            Color previous = Handles.color;
            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % n;
                Vector3 a = path.GetWorldPoint(i);
                Vector3 b = path.GetWorldPoint(next);
                int controlId = PungentSpatialSceneHandleUtility.GetEdgeControlId(path, i);
                bool hovered = PungentSpatialSceneHandleUtility.RegisterEdgeControl(controlId, a, b);
                if (!hovered)
                    continue;

                hoveredIndex = i;
                Handles.color = new Color(1f, 0.95f, 0.28f, 0.95f);
                Handles.DrawAAPolyLine(6f, a, b);
            }
            Handles.color = previous;
        }

        private static void HandleMouseAddInsert(ModularPathSpawner path, ref int selectedIndex, SceneView sceneView, ref double nextAllowedSceneRepaintTime)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0)
                return;

            bool forceAppend = e.shift && !e.control && !e.command && !e.alt;
            bool forceInsert = (e.control || e.command) && !e.shift && !e.alt;
            bool place = PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Add ||
                         PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Insert ||
                         PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Split ||
                         forceAppend ||
                         forceInsert;
            if (!place)
                return;

            if (PungentSpatialSceneHandleUtility.IsNearestPointControl())
                return;

            if (!TryMouseToWorld(path, e.mousePosition, out Vector3 worldHit))
                return;

            int insertIndex = 0;
            if (PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Insert ||
                PungentSpatialAuthoringEditorState.EditMode == PungentSpatialEditMode.Split)
                forceInsert = true;
            bool insert = !forceAppend && TryFindSmartInsertIndex(path, worldHit, e.mousePosition, forceInsert, out insertIndex);
            Undo.RecordObject(path, insert ? "Insert Path Point" : "Add Path Point");
            if (!insert)
            {
                path.AddWorldPoint(worldHit);
                selectedIndex = path.PointCount - 1;
            }
            else
            {
                path.InsertWorldPoint(insertIndex, worldHit);
                selectedIndex = insertIndex;
            }

            EditorUtility.SetDirty(path);
            QueueRebuild(path, sceneView, ref nextAllowedSceneRepaintTime);
            PungentSpatialAuthoringEditorState.ReportAction(insert ? "Inserted path point " + selectedIndex + "." : "Appended path point " + selectedIndex + ".");
            e.Use();
        }

        private static bool TryFindSmartInsertIndex(ModularPathSpawner path, Vector3 worldPoint, Vector2 mousePosition, bool forceInsert, out int insertIndex)
        {
            insertIndex = path != null ? path.PointCount : 0;
            if (path == null || path.PointCount < 2)
                return false;

            insertIndex = FindInsertIndex(path, worldPoint);
            if (forceInsert || path.closedLoop)
                return true;

            if (ScreenDistanceToPoint(mousePosition, path.GetWorldPoint(0)) < 22f ||
                ScreenDistanceToPoint(mousePosition, path.GetWorldPoint(path.PointCount - 1)) < 22f)
                return false;

            return FindNearestSegmentScreenDistance(path, mousePosition) <= 18f;
        }

        private static float ScreenDistanceToPoint(Vector2 mousePosition, Vector3 worldPoint)
        {
            return Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(worldPoint));
        }

        private static float FindNearestSegmentScreenDistance(ModularPathSpawner path, Vector2 mousePosition)
        {
            int n = path != null ? path.PointCount : 0;
            if (n < 2)
                return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            int segmentCount = path.closedLoop ? n : n - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % n;
                Vector2 a = HandleUtility.WorldToGUIPoint(path.GetWorldPoint(i));
                Vector2 b = HandleUtility.WorldToGUIPoint(path.GetWorldPoint(next));
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

        private static void HandleMouseDelete(ModularPathSpawner path, ref int selectedIndex, SceneView sceneView, ref double nextAllowedSceneRepaintTime)
        {
            if (PungentSpatialAuthoringEditorState.EditMode != PungentSpatialEditMode.Delete)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(e))
                return;

            if (PungentSpatialSceneHandleUtility.IsNearestPointControl())
                return;

            if (!TryMouseToWorld(path, e.mousePosition, out Vector3 worldHit))
                return;

            int nearest = FindNearestPointIndex(path, worldHit);
            if (nearest < 0)
                return;

            Undo.RecordObject(path, "Delete Path Point");
            path.RemovePoint(nearest);
            selectedIndex = Mathf.Clamp(nearest - 1, -1, path.PointCount - 1);
            EditorUtility.SetDirty(path);
            QueueRebuild(path, sceneView, ref nextAllowedSceneRepaintTime);
            PungentSpatialAuthoringEditorState.ReportAction("Deleted path point " + nearest + ".");
            e.Use();
        }

        private static void HandleDeleteKey(ModularPathSpawner path, ref int selectedIndex, SceneView sceneView, ref double nextAllowedSceneRepaintTime)
        {
            if (path == null || selectedIndex < 0 || selectedIndex >= path.PointCount)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace))
                return;

            Undo.RecordObject(path, "Delete Path Point");
            path.RemovePoint(selectedIndex);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, path.PointCount - 1);
            EditorUtility.SetDirty(path);
            QueueRebuild(path, sceneView, ref nextAllowedSceneRepaintTime);
            PungentSpatialAuthoringEditorState.ReportAction("Deleted selected path point.");
            e.Use();
        }

        private static bool IsLeftMouseDownOnControl(int controlId)
        {
            Event e = Event.current;
            return e != null &&
                   e.type == EventType.MouseDown &&
                   e.button == 0 &&
                   HandleUtility.nearestControl == controlId &&
                   !PungentSpatialSceneHandleUtility.IsSceneNavigationEvent(e);
        }

        private static bool TryMouseToWorld(ModularPathSpawner path, Vector2 mousePosition, out Vector3 worldHit)
        {
            worldHit = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, path.surfaceMask, QueryTriggerInteraction.Ignore))
            {
                worldHit = hit.point;
                return true;
            }

            Plane plane = new Plane(Vector3.up, path.transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                worldHit = ray.GetPoint(enter);
                return true;
            }

            return false;
        }

        private static int FindInsertIndex(ModularPathSpawner path, Vector3 worldPoint)
        {
            int n = path.PointCount;
            if (n < 2)
                return n;

            float bestSqr = float.PositiveInfinity;
            int bestSegmentStart = n - 1;
            int segmentCount = path.closedLoop ? n : n - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = path.GetWorldPoint(i);
                Vector3 b = path.GetWorldPoint(j);
                float sqr = SqrDistancePointToSegment(worldPoint, a, b);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestSegmentStart = i;
                }
            }

            return bestSegmentStart + 1;
        }

        private static float SqrDistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 0.000001f)
                return (p - a).sqrMagnitude;

            float t = Vector3.Dot(p - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            Vector3 projection = a + ab * t;
            return (p - projection).sqrMagnitude;
        }

        private static int FindNearestPointIndex(ModularPathSpawner path, Vector3 worldPoint)
        {
            int n = path != null ? path.PointCount : 0;
            if (n <= 0)
                return -1;

            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                float sqr = (path.GetWorldPoint(i) - worldPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static void QueueRebuild(ModularPathSpawner path, SceneView sceneView, ref double nextAllowedSceneRepaintTime)
        {
            if (path == null)
                return;

            PungentPathRebuildScheduler.QueueRebuild(path, null, "Rebuild Modular Path");
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            if (sceneView != null)
                PungentEditorPerformanceUtility.RequestSceneViewRepaintThrottled(sceneView, ref nextAllowedSceneRepaintTime, 0.08d);
        }
    }
#endif
}
