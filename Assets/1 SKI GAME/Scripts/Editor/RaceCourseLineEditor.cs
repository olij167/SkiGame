#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SkiGame.Runs;

[CustomEditor(typeof(RaceCourseLine))]
public sealed class RaceCourseLineEditor : Editor
{
    private bool showPointGizmos = true;
    private bool showBoundaryPreview = true;
    private bool showCheckpointPreview = true;
    private bool showMidpointInsertHandles = true;
    private bool showSceneHelpOverlay = true;
    private bool autoSnapToTerrainWhenEditing = true;
    private bool enableShiftClickInsert = true;
    private bool enableAltClickDelete = true;

    private int selectedPointIndex = -1;
    private int selectedCheckpointIndex = -1;

    private float pointPickSizeScale = 0.08f;
    private float midpointPickSizeScale = 0.07f;
    private float checkpointPickSizeScale = 0.08f;
    private float sceneVisualYOffset = 0.35f;

    private SkiRunLine appendRunSource;
    private bool appendAsSlice = false;
    private float appendSliceStart01 = 0f;
    private float appendSliceEnd01 = 1f;
    private int appendSliceSamples = -1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Race Authoring", EditorStyles.boldLabel);

        showPointGizmos = EditorGUILayout.Toggle("Show Point Gizmos", showPointGizmos);
        showBoundaryPreview = EditorGUILayout.Toggle("Show Corridor Preview", showBoundaryPreview);
        showCheckpointPreview = EditorGUILayout.Toggle("Show Checkpoint Preview", showCheckpointPreview);
        showMidpointInsertHandles = EditorGUILayout.Toggle("Show Midpoint Inserts", showMidpointInsertHandles);
        showSceneHelpOverlay = EditorGUILayout.Toggle("Show Scene Help Overlay", showSceneHelpOverlay);
        autoSnapToTerrainWhenEditing = EditorGUILayout.Toggle("Snap Moved Points To Terrain", autoSnapToTerrainWhenEditing);
        enableShiftClickInsert = EditorGUILayout.Toggle("Shift Click Insert", enableShiftClickInsert);
        enableAltClickDelete = EditorGUILayout.Toggle("Alt Click Delete", enableAltClickDelete);

        GUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Scene controls:\n" +
            "- Click a point to select it\n" +
            "- Drag selected point handle to move it\n" +
            "- Click midpoint diamonds to insert between points\n" +
            "- Shift + Left Click terrain to smart-insert a point\n" +
            "- Alt + Left Click a point to delete it\n" +
            "- Click a checkpoint label marker to edit position, rotation, and size",
            MessageType.Info);

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Append Ski Run", EditorStyles.boldLabel);

        appendRunSource = (SkiRunLine)EditorGUILayout.ObjectField("Source Run", appendRunSource, typeof(SkiRunLine), true);
        appendAsSlice = EditorGUILayout.Toggle("Append Slice", appendAsSlice);

        if (appendAsSlice)
        {
            appendSliceStart01 = EditorGUILayout.Slider("Slice Start", appendSliceStart01, 0f, 1f);
            appendSliceEnd01 = EditorGUILayout.Slider("Slice End", appendSliceEnd01, 0f, 1f);
            appendSliceSamples = EditorGUILayout.IntField("Slice Samples (-1 auto)", appendSliceSamples);
        }

        using (new EditorGUI.DisabledScope(appendRunSource == null))
        {
            if (GUILayout.Button(appendAsSlice ? "Append Run Slice" : "Append Full Run"))
            {
                var race = (RaceCourseLine)target;
                Undo.RecordObject(race, "Append Ski Run To Race");

                if (appendAsSlice)
                    race.AppendRunSlicePoints(appendRunSource, appendSliceStart01, appendSliceEnd01, appendSliceSamples, true);
                else
                    race.AppendRunPoints(appendRunSource, true);

                EditorUtility.SetDirty(race);
                SceneView.RepaintAll();
            }
        }

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Checkpoint Override", EditorStyles.boldLabel);

        var raceTarget = (RaceCourseLine)target;
        if (selectedCheckpointIndex >= 0 && selectedCheckpointIndex < raceTarget.GeneratedCheckpoints.Count)
        {
            var cp = raceTarget.GeneratedCheckpoints[selectedCheckpointIndex];
            EditorGUILayout.LabelField("Selected Checkpoint", $"CP {selectedCheckpointIndex + 1}");
            EditorGUILayout.Vector3Field("Current Size", new Vector3(cp.width, cp.height, cp.depth));

            if (raceTarget.TryGetCheckpointOverride(selectedCheckpointIndex, out var ov))
            {
                EditorGUILayout.Vector3Field("World Offset", ov.worldOffset);
                EditorGUILayout.FloatField("Yaw Offset", ov.yawOffsetDegrees);
            }
            else
            {
                EditorGUILayout.HelpBox("No explicit override yet. Moving/rotating/resizing the checkpoint will create one.", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Selected Override"))
                {
                    Undo.RecordObject(raceTarget, "Clear Checkpoint Override");
                    raceTarget.ClearCheckpointOverride(selectedCheckpointIndex);
                    EditorUtility.SetDirty(raceTarget);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Deselect Checkpoint"))
                {
                    selectedCheckpointIndex = -1;
                    SceneView.RepaintAll();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Select a checkpoint in the Scene view to edit its transform and size.", MessageType.None);
        }

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Point At Object"))
            {
                Undo.RecordObject(raceTarget, "Add Race Point");
                raceTarget.AddPointWorld(raceTarget.transform.position);
                EditorUtility.SetDirty(raceTarget);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Remove Last"))
            {
                Undo.RecordObject(raceTarget, "Remove Last Race Point");
                raceTarget.RemoveLastPoint();
                EditorUtility.SetDirty(raceTarget);
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Resolve Checkpoints"))
            {
                Undo.RecordObject(raceTarget, "Resolve Race Checkpoints");
                raceTarget.ResolveGeneratedCheckpoints();
                EditorUtility.SetDirty(raceTarget);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Regenerate Gate Visuals"))
            {
                raceTarget.ResolveGeneratedCheckpoints();
                raceTarget.RegenerateCheckpointVisuals();
                EditorUtility.SetDirty(raceTarget);
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Clear Points"))
        {
            if (EditorUtility.DisplayDialog("Clear Race Points", "Remove all race points?", "Clear", "Cancel"))
            {
                Undo.RecordObject(raceTarget, "Clear Race Points");
                raceTarget.ClearPoints();
                selectedPointIndex = -1;
                selectedCheckpointIndex = -1;
                EditorUtility.SetDirty(raceTarget);
                SceneView.RepaintAll();
            }
        }
    }

    private void OnSceneGUI()
    {
        var race = (RaceCourseLine)target;
        var points = race.PointsWorld;
        Event e = Event.current;
        if (points == null)
            return;

        DrawPathPreview(race);

        if (showSceneHelpOverlay)
            DrawSceneHelpOverlay();

        HandleCheckpointEditing(race);
        HandlePointEditing(race, e);
        HandleShiftClickInsert(race, e);
    }

    private void DrawPathPreview(RaceCourseLine race)
    {
        var points = race.PointsWorld;
        if (points == null || points.Count == 0)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        Handles.color = new Color(0.15f, 0.9f, 1f, 1f);
        for (int i = 1; i < points.Count; i++)
            Handles.DrawAAPolyLine(4f, Lift(points[i - 1]), Lift(points[i]));

        if (showBoundaryPreview && points.Count >= 2)
            DrawCorridorPreview(race);

        if (showCheckpointPreview)
            DrawCheckpointPreview(race);

        if (showMidpointInsertHandles && points.Count >= 2)
            DrawMidpointInsertHandles(race);

        if (showPointGizmos)
            DrawPointButtons(race);

        Handles.color = Color.green;
        Handles.SphereHandleCap(0, Lift(points[0]), Quaternion.identity, HandleUtility.GetHandleSize(points[0]) * 0.25f, EventType.Repaint);

        Handles.color = Color.red;
        Handles.SphereHandleCap(0, Lift(points[points.Count - 1]), Quaternion.identity, HandleUtility.GetHandleSize(points[points.Count - 1]) * 0.25f, EventType.Repaint);
    }

    private void DrawCorridorPreview(RaceCourseLine race)
    {
        var pts = race.PointsWorld;
        if (pts == null || pts.Count < 2)
            return;

        float halfWidth = Mathf.Max(1f, race.CourseWidthMeters * 0.5f);
        Handles.color = new Color(1f, 1f, 1f, 0.35f);

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 dir = (b - a).normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, dir).normalized * halfWidth;

            Handles.DrawAAPolyLine(2f, Lift(a + lateral), Lift(b + lateral));
            Handles.DrawAAPolyLine(2f, Lift(a - lateral), Lift(b - lateral));
        }
    }

    private void DrawCheckpointPreview(RaceCourseLine race)
    {
        var checkpoints = race.GeneratedCheckpoints;
        if (checkpoints == null)
            return;

        for (int i = 0; i < checkpoints.Count; i++)
        {
            var cp = checkpoints[i];
            Quaternion rot = cp.forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(cp.forward.normalized, Vector3.up)
                : Quaternion.identity;

            Handles.color = i == selectedCheckpointIndex
                ? new Color(1f, 0.95f, 0.2f, 1f)
                : new Color(1f, 0.75f, 0.1f, 0.9f);

            using (new Handles.DrawingScope(Matrix4x4.TRS(cp.worldPos, rot, Vector3.one)))
            {
                Handles.DrawWireCube(new Vector3(0f, cp.height * 0.5f, 0f), new Vector3(cp.width, cp.height, cp.depth));
            }

            float pickSize = HandleUtility.GetHandleSize(cp.worldPos + Vector3.up * cp.height) * checkpointPickSizeScale;
            if (Handles.Button(cp.worldPos + Vector3.up * (cp.height + 0.2f), Quaternion.identity, pickSize, pickSize * 1.2f, Handles.RectangleHandleCap))
            {
                selectedCheckpointIndex = i;
                selectedPointIndex = -1;
                Repaint();
            }

            Handles.Label(Lift(cp.worldPos + Vector3.up * (cp.height + 0.5f)), $"CP {i + 1}");
        }
    }

    private void DrawMidpointInsertHandles(RaceCourseLine race)
    {
        var points = race.PointsWorld;
        if (points == null || points.Count < 2)
            return;

        Handles.color = new Color(0.5f, 1f, 0.5f, 0.95f);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 mid = Vector3.Lerp(points[i], points[i + 1], 0.5f);
            Vector3 midLift = Lift(mid);
            float size = HandleUtility.GetHandleSize(midLift) * midpointPickSizeScale;

            if (Handles.Button(midLift, Quaternion.identity, size, size * 1.15f, Handles.ConeHandleCap))
            {
                Undo.RecordObject(race, "Insert Midpoint Race Point");
                race.InsertPointWorld(i + 1, mid);
                selectedPointIndex = i + 1;
                selectedCheckpointIndex = -1;
                EditorUtility.SetDirty(race);
                SceneView.RepaintAll();
            }
        }
    }

    private void DrawPointButtons(RaceCourseLine race)
    {
        var points = race.PointsWorld;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = Lift(points[i]);
            float size = HandleUtility.GetHandleSize(p) * pointPickSizeScale;
            bool isSelected = i == selectedPointIndex;

            Handles.color = isSelected ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.2f, 1f, 1f, 0.95f);
            if (Handles.Button(p, Quaternion.identity, size, size * 1.25f, Handles.SphereHandleCap))
            {
                selectedPointIndex = i;
                selectedCheckpointIndex = -1;
                Repaint();
            }

            if (isSelected)
                Handles.Label(p + Vector3.up * 1.25f, i == 0 ? "Start" : i == points.Count - 1 ? "Finish" : $"P{i}");
        }
    }

    private void HandleCheckpointEditing(RaceCourseLine race)
    {
        var checkpoints = race.GeneratedCheckpoints;
        if (checkpoints == null || selectedCheckpointIndex < 0 || selectedCheckpointIndex >= checkpoints.Count)
            return;

        var cp = checkpoints[selectedCheckpointIndex];
        race.TryGetCheckpointOverride(selectedCheckpointIndex, out var ov);

        Vector3 existingOffset = ov.worldOffset;
        float existingYaw = ov.yawOffsetDegrees;

        Vector3 currentPos = cp.worldPos;
        Quaternion currentRot = cp.forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(cp.forward.normalized, Vector3.up)
            : Quaternion.identity;

        EditorGUI.BeginChangeCheck();

        Vector3 movedPos = Handles.PositionHandle(currentPos, currentRot);
        Quaternion movedRot = Handles.RotationHandle(currentRot, currentPos);

        Vector3 center = currentPos + Vector3.up * (cp.height * 0.5f);
        float widthHalf = Handles.ScaleSlider(cp.width * 0.5f, center, currentRot * Vector3.right, currentRot, HandleUtility.GetHandleSize(center) * 0.8f, 0.2f);
        float depthHalf = Handles.ScaleSlider(cp.depth * 0.5f, center, currentRot * Vector3.forward, currentRot, HandleUtility.GetHandleSize(center) * 0.8f, 0.2f);
        float height = Handles.ScaleSlider(cp.height, cp.worldPos, Vector3.up, Quaternion.identity, HandleUtility.GetHandleSize(center) * 0.8f, 0.2f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(race, "Edit Checkpoint Override");

            Vector3 deltaPos = movedPos - currentPos;
            Vector3 newOffset = existingOffset + deltaPos;

            Vector3 currentFlatForward = Vector3.ProjectOnPlane(currentRot * Vector3.forward, Vector3.up).normalized;
            Vector3 newFlatForward = Vector3.ProjectOnPlane(movedRot * Vector3.forward, Vector3.up).normalized;

            float deltaYaw = 0f;
            if (currentFlatForward.sqrMagnitude > 0.0001f && newFlatForward.sqrMagnitude > 0.0001f)
                deltaYaw = Vector3.SignedAngle(currentFlatForward, newFlatForward, Vector3.up);

            race.SetCheckpointTransformOverride(selectedCheckpointIndex, newOffset, true, existingYaw + deltaYaw);
            race.SetCheckpointSizeOverride(
                selectedCheckpointIndex,
                Mathf.Max(0.25f, widthHalf * 2f),
                Mathf.Max(0.25f, height),
                Mathf.Max(0.25f, depthHalf * 2f));

            EditorUtility.SetDirty(race);
            SceneView.RepaintAll();
        }
    }

    private void HandlePointEditing(RaceCourseLine race, Event e)
    {
        var points = race.PointsWorld;
        if (points == null || points.Count == 0)
            return;

        if (enableAltClickDelete && e.alt && e.type == EventType.MouseDown && e.button == 0)
        {
            int deleteIndex = PickNearestPoint(points, e.mousePosition);
            if (deleteIndex >= 0)
            {
                Undo.RecordObject(race, "Delete Race Point");
                race.RemovePointAt(deleteIndex);
                if (selectedPointIndex == deleteIndex)
                    selectedPointIndex = -1;
                else if (selectedPointIndex > deleteIndex)
                    selectedPointIndex--;

                EditorUtility.SetDirty(race);
                e.Use();
                SceneView.RepaintAll();
                return;
            }
        }

        if (selectedPointIndex < 0 || selectedPointIndex >= points.Count)
            return;

        Vector3 current = points[selectedPointIndex];
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(Lift(current), Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(race, "Move Race Point");
            moved = Lower(moved);

            if (autoSnapToTerrainWhenEditing)
                moved = SnapToTerrain(moved);

            race.SetPointWorld(selectedPointIndex, moved);
            EditorUtility.SetDirty(race);
            SceneView.RepaintAll();
        }
    }

    private void HandleShiftClickInsert(RaceCourseLine race, Event e)
    {
        if (!enableShiftClickInsert)
            return;

        if (!(e.shift && e.type == EventType.MouseDown && e.button == 0))
            return;

        if (TryGetScenePoint(e.mousePosition, out Vector3 hitPoint))
        {
            Undo.RecordObject(race, "Insert Race Point");
            race.InsertPointWorldSmart(hitPoint);
            selectedCheckpointIndex = -1;
            EditorUtility.SetDirty(race);
            e.Use();
            SceneView.RepaintAll();
        }
    }

    private void DrawSceneHelpOverlay()
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(12f, 12f, 340f, 125f), "Race Course Tools", GUI.skin.window);
        GUILayout.Label("Click point: select");
        GUILayout.Label("Drag point handle: move");
        GUILayout.Label("Midpoint diamond: insert between points");
        GUILayout.Label("Shift + Click terrain: smart insert");
        GUILayout.Label("Alt + Click point: delete");
        GUILayout.Label("Click CP marker: edit checkpoint transform/size");
        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private int PickNearestPoint(System.Collections.Generic.IReadOnlyList<Vector3> points, Vector2 mousePos)
    {
        int best = -1;
        float bestDist = 18f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 gui = HandleUtility.WorldToGUIPoint(Lift(points[i]));
            float d = Vector2.Distance(gui, mousePos);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private bool TryGetScenePoint(Vector2 mousePos, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private Vector3 SnapToTerrain(Vector3 point)
    {
        Vector3 rayOrigin = point + Vector3.up * 1000f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2500f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return point;
    }

    private Vector3 Lift(Vector3 p) => p + Vector3.up * sceneVisualYOffset;
    private Vector3 Lower(Vector3 p) => p - Vector3.up * sceneVisualYOffset;
}
#endif