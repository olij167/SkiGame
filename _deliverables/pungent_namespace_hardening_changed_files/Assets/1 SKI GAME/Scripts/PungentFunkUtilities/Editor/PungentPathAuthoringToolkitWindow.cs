using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentPathAuthoringToolkitWindow : EditorWindow
    {
        private enum EditMode { View, Add, Move, Insert, Delete }

        private readonly List<Vector3> _points = new List<Vector3>();
        private Vector2 _scroll;
        private bool _drawScenePreview = true;
        private bool _drawLabels = true;
        private bool _drawRibbon = true;
        private bool _snapToSurface = true;
        private LayerMask _surfaceMask = ~0;
        private float _handleSize = 0.6f;
        private float _pathWidth = 1.5f;
        private EditMode _editMode = EditMode.Move;
        private int _selectedIndex = -1;
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Scene/Path Authoring Toolkit", priority = 945)]
        public static void Open()
        {
            PungentPathAuthoringToolkitWindow window = GetWindow<PungentPathAuthoringToolkitWindow>("Path Toolkit");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header("Path Authoring Toolkit", "Generic scene path authoring helpers: handles, hotkeys, surface snapping, width previews, validation, sampling, and point operations.", _status);

            DrawToolbar();
            DrawValidation();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _points.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(i == _selectedIndex ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
                {
                    if (GUILayout.Button("P" + i, GUILayout.Width(36f)))
                        _selectedIndex = i;
                    _points[i] = EditorGUILayout.Vector3Field(GUIContent.none, _points[i]);
                    if (GUILayout.Button("Snap", GUILayout.Width(48f)))
                        _points[i] = SnapToSurface(_points[i]);
                    if (GUILayout.Button("+", GUILayout.Width(24f)))
                        InsertPointAfter(i);
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        _points.RemoveAt(i);
                        _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _points.Count - 1);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _editMode = (EditMode)EditorGUILayout.EnumPopup(_editMode, GUILayout.Width(96f));
                    _drawScenePreview = GUILayout.Toggle(_drawScenePreview, "Preview", EditorStyles.toolbarButton, GUILayout.Width(70f));
                    _drawLabels = GUILayout.Toggle(_drawLabels, "Labels", EditorStyles.toolbarButton, GUILayout.Width(64f));
                    _drawRibbon = GUILayout.Toggle(_drawRibbon, "Width", EditorStyles.toolbarButton, GUILayout.Width(58f));
                    _snapToSurface = GUILayout.Toggle(_snapToSurface, "Snap", EditorStyles.toolbarButton, GUILayout.Width(54f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add Selection", GUILayout.Width(104f)))
                        AddSelectionPoints();
                    if (GUILayout.Button("Reverse", GUILayout.Width(70f)))
                        _points.Reverse();
                    if (GUILayout.Button("Clear", GUILayout.Width(56f)) && EditorUtility.DisplayDialog("Clear Path", "Clear all toolkit points?", "Clear", "Cancel"))
                        _points.Clear();
                }

                _surfaceMask.value = EditorGUILayout.MaskField("Surface Mask", _surfaceMask.value, UnityEditorInternal.InternalEditorUtility.layers);
                _handleSize = EditorGUILayout.Slider("Handle Size", _handleSize, 0.1f, 3f);
                _pathWidth = EditorGUILayout.Slider("Preview Width", _pathWidth, 0.05f, 20f);
                EditorGUILayout.HelpBox("Scene hotkeys: A = Add, M = Move, I = Insert, D = Delete, Esc = View/clear point selection. Shift-click inserts near segment in Insert mode; Delete mode removes nearest point.", MessageType.Info);
            }
        }

        private void DrawValidation()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                float length = PungentPathAuthoringToolkit.CalculateLength(_points);
                UtilityWindowTheme.SectionTitle("Validation", UtilityWindowTheme.Teal, _points.Count + " points / " + length.ToString("0.0") + "m");
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Segments " + Mathf.Max(0, _points.Count - 1), UtilityWindowTheme.Cyan, 90f);
                    UtilityWindowTheme.CountPill("Width " + _pathWidth.ToString("0.0"), UtilityWindowTheme.Purple, 82f);
                    UtilityWindowTheme.CountPill("Selected " + (_selectedIndex >= 0 ? _selectedIndex.ToString() : "none"), UtilityWindowTheme.Amber, 92f);
                }

                if (_points.Count < 2)
                    EditorGUILayout.HelpBox("Add at least two points to create a usable path.", MessageType.Info);
                else if (PungentPathAuthoringToolkit.HasNearDuplicatePoints(_points, 0.05f))
                    EditorGUILayout.HelpBox("Path contains near-duplicate points.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Path validation passed. This toolkit can now inform Modular Path Builder integration or path-specific editor tools.", MessageType.Info);
            }
        }

        private void AddSelectionPoints()
        {
            foreach (Transform t in Selection.transforms)
            {
                Vector3 position = _snapToSurface ? SnapToSurface(t.position) : t.position;
                _points.Add(position);
            }
            _status = "Added " + Selection.transforms.Length + " selected transform point(s).";
            SceneView.RepaintAll();
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

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (!_drawScenePreview)
                return;

            HandleHotkeys();
            DrawSceneOverlay();
            DrawPathSceneHandles();
            HandleMouseActions(sceneView);
        }

        private void DrawPathSceneHandles()
        {
            Handles.color = UtilityWindowTheme.Cyan;
            if (_points.Count > 1)
                Handles.DrawAAPolyLine(3f, _points.ToArray());

            if (_drawRibbon && _points.Count > 1)
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
                        Repaint();
                    }
                }

                if (_drawLabels)
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
                Repaint();
            }
            else if (_editMode == EditMode.Insert || e.shift)
            {
                int insert = PungentPathAuthoringToolkit.FindNearestSegmentIndex(_points, hit.point);
                _points.Insert(Mathf.Clamp(insert + 1, 0, _points.Count), hit.point);
                _selectedIndex = insert + 1;
                e.Use();
                Repaint();
            }
            else if (_editMode == EditMode.Delete)
            {
                int nearest = PungentPathAuthoringToolkit.FindNearestPointIndex(_points, hit.point);
                if (nearest >= 0)
                {
                    _points.RemoveAt(nearest);
                    _selectedIndex = -1;
                    e.Use();
                    Repaint();
                }
            }
        }

        private void HandleHotkeys()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.A) { _editMode = EditMode.Add; e.Use(); Repaint(); }
            else if (e.keyCode == KeyCode.M) { _editMode = EditMode.Move; e.Use(); Repaint(); }
            else if (e.keyCode == KeyCode.I) { _editMode = EditMode.Insert; e.Use(); Repaint(); }
            else if (e.keyCode == KeyCode.D) { _editMode = EditMode.Delete; e.Use(); Repaint(); }
            else if (e.keyCode == KeyCode.Escape) { _editMode = EditMode.View; _selectedIndex = -1; e.Use(); Repaint(); }
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
    }

    public static class PungentPathAuthoringToolkit
    {
        public static float CalculateLength(IList<Vector3> points)
        {
            if (points == null || points.Count < 2)
                return 0f;
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
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
            Handles.color = new Color(color.r, color.g, color.b, 0.55f);
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
    }
    #endif

}