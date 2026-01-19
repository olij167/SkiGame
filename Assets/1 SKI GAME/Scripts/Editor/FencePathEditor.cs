using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene editing for FencePath:
/// - Drag points with handles
/// - Shift+Click terrain to add a point at end
/// - Ctrl+Click terrain to insert into nearest segment
/// - Delete/Backspace deletes selected point
/// - Debounced auto rebuild to avoid editor lag while dragging
/// </summary>
[CustomEditor(typeof(FencePath))]
public class FencePathEditor : Editor
{
    private FencePath _path;
    private SerializedProperty _localPointsProp;

    private int _selectedIndex = -1;

    // Debounce state
    private bool _rebuildQueued;
    private double _rebuildAtTime;

    private void OnEnable()
    {
        _path = (FencePath)target;
        _localPointsProp = serializedObject.FindProperty("localPoints");

        SceneView.duringSceneGui += DuringSceneGui;
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        EditorApplication.update -= EditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Default inspector (keeps your settings visible and grouped)
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild", GUILayout.Height(28)))
            {
                _path.Rebuild();
            }

            if (GUILayout.Button("Clear Generated", GUILayout.Height(28)))
            {
                _path.ClearGenerated();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Point (Forward)"))
            {
                Undo.RecordObject(_path, "Add Fence Point");
                Vector3 basePos = _path.PointCount > 0 ? _path.GetWorldPoint(_path.PointCount - 1) : _path.transform.position;
                Vector3 newPos = basePos + _path.transform.forward * 5f;
                _path.AddWorldPoint(newPos);
                EditorUtility.SetDirty(_path);
                QueueRebuild();
            }

            if (GUILayout.Button("Snap Points To Terrain"))
            {
                SnapAllPointsToTerrain();
                QueueRebuild();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DuringSceneGui(SceneView view)
    {
        if (_path == null) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        DrawPolyline();
        DrawPointHandles();

        HandleMouseAddInsert();
        HandleDeleteKey();
    }

    private void DrawPolyline()
    {
        int n = _path.PointCount;
        if (n < 2) return;

        Handles.color = new Color(1f, 1f, 1f, 0.75f);

        for (int i = 0; i < n - 1; i++)
        {
            Handles.DrawLine(_path.GetWorldPoint(i), _path.GetWorldPoint(i + 1));
        }

        if (_path.closedLoop && n >= 3)
        {
            Handles.DrawLine(_path.GetWorldPoint(n - 1), _path.GetWorldPoint(0));
        }
    }

    private void DrawPointHandles()
    {
        int n = _path.PointCount;

        for (int i = 0; i < n; i++)
        {
            Vector3 wp = _path.GetWorldPoint(i);

            float size = HandleUtility.GetHandleSize(wp) * 0.08f;
            bool isSelected = (i == _selectedIndex);

            Handles.color = isSelected ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.8f, 0.2f, 0.95f);

            // Click-select
            if (Handles.Button(wp, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
            {
                _selectedIndex = i;
                Repaint();
            }

            // Move handle for selected point
            if (isSelected)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(wp, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_path, "Move Fence Point");
                    _path.SetWorldPoint(i, newPos);
                    EditorUtility.SetDirty(_path);
                    QueueRebuild();
                }

                Handles.Label(wp + Vector3.up * (size * 6f), $"Point {i}");
            }
        }
    }

    private void HandleMouseAddInsert()
    {
        Event e = Event.current;
        if (e == null) return;

        // Require mouse down to place, but don't steal drag actions
        if (e.type != EventType.MouseDown || e.button != 0) return;

        // Shift+Click = add point at end
        // Ctrl+Click = insert into nearest segment
        bool add = e.shift && !e.control && !e.alt;
        bool insert = e.control && !e.shift && !e.alt;

        if (!add && !insert) return;

        if (!TryMouseToWorld(e.mousePosition, out Vector3 worldHit))
            return;

        Undo.RecordObject(_path, add ? "Add Fence Point" : "Insert Fence Point");

        if (add)
        {
            _path.AddWorldPoint(worldHit);
            _selectedIndex = _path.PointCount - 1;
        }
        else if (insert)
        {
            int insertIndex = FindInsertIndex(worldHit);
            _path.InsertWorldPoint(insertIndex, worldHit);
            _selectedIndex = insertIndex;
        }

        EditorUtility.SetDirty(_path);
        QueueRebuild();

        e.Use();
    }

    private int FindInsertIndex(Vector3 worldPoint)
    {
        int n = _path.PointCount;
        if (n < 2) return n;

        float bestSqr = float.PositiveInfinity;
        int bestSegmentStart = n - 1;

        int segCount = _path.closedLoop ? n : (n - 1);

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

        // Insert after the start of that segment
        return bestSegmentStart + 1;
    }

    private static float SqrDistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 1e-6f) return (p - a).sqrMagnitude;

        float t = Vector3.Dot(p - a, ab) / abSqr;
        t = Mathf.Clamp01(t);

        Vector3 proj = a + ab * t;
        return (p - proj).sqrMagnitude;
    }

    private void HandleDeleteKey()
    {
        if (_path == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= _path.PointCount) return;

        Event e = Event.current;
        if (e == null) return;

        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            Undo.RecordObject(_path, "Delete Fence Point");
            _path.RemovePoint(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex - 1, -1, _path.PointCount - 1);
            EditorUtility.SetDirty(_path);
            QueueRebuild();
            e.Use();
        }
    }

    private bool TryMouseToWorld(Vector2 mousePosition, out Vector3 worldHit)
    {
        worldHit = default;

        // Raycast from the mouse position into the scene
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        // Prefer physics hits (terrain/colliders). If none, intersect with a horizontal plane through the path transform.
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, _path.terrainMask, QueryTriggerInteraction.Ignore))
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

    private void SnapAllPointsToTerrain()
    {
        int n = _path.PointCount;
        if (n <= 0) return;

        for (int i = 0; i < n; i++)
        {
            Vector3 wp = _path.GetWorldPoint(i);
            Vector3 start = wp + Vector3.up * Mathf.Max(0.01f, _path.raycastStartHeight);

            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, _path.raycastStartHeight * 2f, _path.terrainMask, QueryTriggerInteraction.Ignore))
            {
                _path.SetWorldPoint(i, hit.point);
            }
        }

        EditorUtility.SetDirty(_path);
    }

    private void QueueRebuild()
    {
        if (_path == null) return;
        if (!_path.autoRebuildInEditor) return;

        _rebuildQueued = true;
        _rebuildAtTime = EditorApplication.timeSinceStartup + Math.Max(0.0, _path.editorRebuildDebounce);
    }

    private void EditorUpdate()
    {
        if (_path == null) return;
        if (!_rebuildQueued) return;

        if (EditorApplication.timeSinceStartup >= _rebuildAtTime)
        {
            _rebuildQueued = false;
            _path.Rebuild();
        }
    }
}
