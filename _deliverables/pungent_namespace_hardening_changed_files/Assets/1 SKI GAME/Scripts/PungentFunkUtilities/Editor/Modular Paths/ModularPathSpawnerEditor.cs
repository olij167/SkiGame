using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Scene editing for ModularPathSpawner and legacy FencePath compatibility components.
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
        private ModularPathSpawner _path;
        private int _selectedIndex = -1;
        private bool _rebuildQueued;
        private double _rebuildAtTime;

        private void OnEnable()
        {
            _path = target as ModularPathSpawner;
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
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild", GUILayout.Height(28f)))
                {
                    _path.Rebuild();
                    EditorUtility.SetDirty(_path);
                }

                if (GUILayout.Button("Clear Generated", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(_path, "Clear Generated Path Objects");
                    _path.ClearGenerated();
                    EditorUtility.SetDirty(_path);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point Forward"))
                {
                    Undo.RecordObject(_path, "Add Path Point");
                    Vector3 basePos = _path.PointCount > 0 ? _path.GetWorldPoint(_path.PointCount - 1) : _path.transform.position;
                    Vector3 newPos = basePos + _path.transform.forward * 5f;
                    _path.AddWorldPoint(newPos);
                    EditorUtility.SetDirty(_path);
                    QueueRebuild();
                }

                if (GUILayout.Button("Snap Points To Surface"))
                {
                    Undo.RecordObject(_path, "Snap Path Points To Surface");
                    SnapAllPointsToSurface();
                    EditorUtility.SetDirty(_path);
                    QueueRebuild();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Scene controls: Shift+LMB add point, Ctrl+LMB insert point, click a point to select, drag selected point, Delete/Backspace remove selected point.",
                MessageType.Info);
        }

        private void DuringSceneGui(SceneView view)
        {
            if (_path == null)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            DrawPolyline();
            DrawPointHandles();
            HandleMouseAddInsert();
            HandleDeleteKey();
        }

        private void DrawPolyline()
        {
            int n = _path.PointCount;
            if (n < 2)
                return;

            Handles.color = new Color(0.7f, 0.95f, 1f, 0.8f);

            if (_path.BuildSampledWorldPath(out var sampled, out bool sampledClosed))
            {
                for (int i = 0; i < sampled.Count - 1; i++)
                    Handles.DrawLine(sampled[i], sampled[i + 1]);

                if (sampledClosed && sampled.Count > 2)
                    Handles.DrawLine(sampled[sampled.Count - 1], sampled[0]);
            }
        }

        private void DrawPointHandles()
        {
            int n = _path.PointCount;
            for (int i = 0; i < n; i++)
            {
                Vector3 wp = _path.GetWorldPoint(i);
                float size = HandleUtility.GetHandleSize(wp) * 0.08f;
                bool isSelected = i == _selectedIndex;

                Handles.color = isSelected ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(1f, 0.82f, 0.28f, 0.95f);

                if (Handles.Button(wp, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
                {
                    _selectedIndex = i;
                    Repaint();
                }

                if (!isSelected)
                    continue;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(wp, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_path, "Move Path Point");
                    _path.SetWorldPoint(i, newPos);
                    EditorUtility.SetDirty(_path);
                    QueueRebuild();
                }

                Handles.Label(wp + Vector3.up * (size * 6f), $"Point {i}");
            }
        }

        private void HandleMouseAddInsert()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0)
                return;

            bool add = e.shift && !e.control && !e.alt;
            bool insert = e.control && !e.shift && !e.alt;
            if (!add && !insert)
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
            if (_path == null || !_path.autoRebuildInEditor)
                return;

            _rebuildQueued = true;
            _rebuildAtTime = EditorApplication.timeSinceStartup + Math.Max(0.0, _path.editorRebuildDebounce);
        }

        private void EditorUpdate()
        {
            if (_path == null || !_rebuildQueued)
                return;

            if (EditorApplication.timeSinceStartup < _rebuildAtTime)
                return;

            _rebuildQueued = false;
            _path.Rebuild();
            EditorUtility.SetDirty(_path);
        }
    }
    #endif

}