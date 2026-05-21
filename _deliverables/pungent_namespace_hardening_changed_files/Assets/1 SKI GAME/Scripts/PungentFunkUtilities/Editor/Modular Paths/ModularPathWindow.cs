using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Generic utility window for ModularPathSpawner.
    /// </summary>
    public class ModularPathWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.ModularPathBuilder.";
        private const string PrefCreateHeight = PrefPrefix + "CreateHeight";
        private const string PrefActiveHeight = PrefPrefix + "ActiveHeight";
        private const string PrefPointsHeight = PrefPrefix + "PointsHeight";
        private const string PrefCreateFoldout = PrefPrefix + "CreateFoldout";
        private const string PrefActiveFoldout = PrefPrefix + "ActiveFoldout";
        private const string PrefPointsFoldout = PrefPrefix + "PointsFoldout";
        private const string PrefParentUnderSelection = PrefPrefix + "ParentUnderSelection";
        private const string PrefAutoFrameOnCreate = PrefPrefix + "AutoFrameOnCreate";

        private ModularPathSpawner _active;
        private SerializedObject _so;

        private Vector2 _mainScroll;
        private Vector2 _createScroll;
        private Vector2 _activeScroll;
        private Vector2 _pointsScroll;

        private GameObject _defaultSegmentPrefab;
        private GameObject _defaultPointPrefab;
        private bool _parentUnderSelection = true;
        private bool _autoFrameOnCreate = true;

        private float _createHeight = 160f;
        private float _activeHeight = 330f;
        private float _pointsHeight = 240f;
        private bool _createFoldout = true;
        private bool _activeFoldout = true;
        private bool _pointsFoldout = true;

        private Vector2 _resizeStartMouse;
        private float _resizeStartHeight;
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Scene/Modular Path Builder")]
        public static void OpenUtility()
        {
            Open();
        }

        public static void Open()
        {
            ModularPathWindow window = GetWindow<ModularPathWindow>("Path Builder");
            window.minSize = new Vector2(390f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Path Builder");
            LoadPrefs();
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SavePrefs();
        }

        private void OnSelectionChanged()
        {
            ModularPathSpawner selectedPath = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<ModularPathSpawner>()
                : null;

            if (selectedPath != null)
                SetActive(selectedPath);

            Repaint();
        }

        private void SetActive(ModularPathSpawner path)
        {
            _active = path;
            _so = _active != null ? new SerializedObject(_active) : null;
            _status = _active != null ? $"Active: {_active.name}" : "No active path.";
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Modular Path Builder",
                "Create and maintain generic prefab-segment paths for rails, cables, barriers, pipes, ropes, trims, fences, or other repeated scene props.",
                _status);

            DrawToolbar();

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawCreateSection();
            DrawActiveSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                {
                    ModularPathSpawner selectedPath = Selection.activeGameObject != null
                        ? Selection.activeGameObject.GetComponentInParent<ModularPathSpawner>()
                        : null;

                    if (selectedPath != null)
                        SetActive(selectedPath);
                    else
                        _status = "Selection does not contain a ModularPathSpawner.";
                }

                using (new EditorGUI.DisabledScope(_active == null))
                {
                    if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                        FrameObject(_active.gameObject);

                    if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                        RebuildActive("Rebuild Modular Path");
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Help", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    EditorUtility.DisplayDialog(
                        "Modular Path Builder Scene Controls",
                        "When a ModularPathSpawner is selected:\n\n" +
                        "� Shift + Left Click: add point at surface hit\n" +
                        "� Ctrl + Left Click: insert point into nearest segment\n" +
                        "� Click point: select\n" +
                        "� Drag handle: move selected point\n" +
                        "� Delete/Backspace: remove selected point\n\n" +
                        "Generated objects rely on their own colliders/layers for gameplay interactions.",
                        "OK");
                }
            }
        }

        private void DrawCreateSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _createFoldout = EditorGUILayout.Foldout(_createFoldout, "Create Path", true, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill("Spawner", UtilityWindowTheme.Teal, 72f);
                }

                if (!_createFoldout)
                    return;

                _createScroll = EditorGUILayout.BeginScrollView(_createScroll, GUILayout.Height(_createHeight));

                _defaultSegmentPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Segment Prefab", "Prefab repeated along the generated path."),
                    _defaultSegmentPrefab,
                    typeof(GameObject),
                    false);

                _defaultPointPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Point Prefab", "Optional prefab placed at each control point."),
                    _defaultPointPrefab,
                    typeof(GameObject),
                    false);

                _parentUnderSelection = EditorGUILayout.Toggle(
                    new GUIContent("Parent Under Selection", "Creates the path as a child of the currently selected transform when possible."),
                    _parentUnderSelection);

                _autoFrameOnCreate = EditorGUILayout.Toggle(
                    new GUIContent("Frame After Create", "Frames the new path in the active Scene view after creation."),
                    _autoFrameOnCreate);

                EditorGUILayout.HelpBox("New paths are created with two starter points so you can immediately see and edit the path.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Create Path", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                    {
                        ModularPathSpawner created = CreatePath();
                        if (_autoFrameOnCreate && created != null)
                            FrameObject(created.gameObject);
                    }

                    if (GUILayout.Button("Create at Origin", GUILayout.Height(28f), GUILayout.Width(126f)))
                    {
                        ModularPathSpawner created = CreatePath(Vector3.zero);
                        if (_autoFrameOnCreate && created != null)
                            FrameObject(created.gameObject);
                    }
                }

                EditorGUILayout.EndScrollView();
                DrawVerticalResizeHandle(ref _createHeight, 110f, Mathf.Max(120f, position.height - 280f), PrefCreateHeight);
            }
        }

        private ModularPathSpawner CreatePath()
        {
            Vector3 position = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
                position = SceneView.lastActiveSceneView.pivot;

            return CreatePath(position);
        }

        private ModularPathSpawner CreatePath(Vector3 position)
        {
            GameObject go = new GameObject("Modular Path Spawner");
            Undo.RegisterCreatedObjectUndo(go, "Create Modular Path Spawner");

            if (_parentUnderSelection && Selection.activeTransform != null)
                go.transform.SetParent(Selection.activeTransform, true);

            go.transform.position = position;
            ModularPathSpawner path = go.AddComponent<ModularPathSpawner>();
            path.segmentPrefab = _defaultSegmentPrefab;
            path.pointPrefab = _defaultPointPrefab;

            path.AddWorldPoint(position);
            Vector3 second = position + (SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward.FlattenYSafe(Vector3.forward) : Vector3.forward) * 5f;
            path.AddWorldPoint(second);

            path.EnsureGeneratedRoot();
            if (path.segmentPrefab != null)
                path.Rebuild();

            Selection.activeGameObject = go;
            SetActive(path);
            _status = "Created Modular Path Spawner.";
            return path;
        }

        private void DrawActiveSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _activeFoldout = EditorGUILayout.Foldout(_activeFoldout, "Active Path", true, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(_active != null ? _active.PointCount.ToString() : "0", UtilityWindowTheme.Blue, 44f);
                }

                if (!_activeFoldout)
                    return;

                _activeScroll = EditorGUILayout.BeginScrollView(_activeScroll, GUILayout.Height(_activeHeight));

                if (_active == null)
                {
                    EditorGUILayout.HelpBox("Select a GameObject with ModularPathSpawner, or create a new path above.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    DrawVerticalResizeHandle(ref _activeHeight, 170f, Mathf.Max(190f, position.height - 260f), PrefActiveHeight);
                    return;
                }

                if (_so == null || _so.targetObject != _active)
                    _so = new SerializedObject(_active);

                _so.Update();

                DrawQuickActions();
                EditorGUILayout.Space(8f);

                EditorGUI.BeginChangeCheck();
                DrawCoreSettings();
                EditorGUILayout.Space(8f);
                DrawSocketSettings();
                EditorGUILayout.Space(8f);
                DrawSurfaceSettings();
                EditorGUILayout.Space(8f);
                bool changed = EditorGUI.EndChangeCheck();

                _so.ApplyModifiedProperties();

                if (changed)
                {
                    EditorUtility.SetDirty(_active);
                    if (_active.autoRebuildInEditor)
                        _active.Rebuild();
                }

                DrawPointsSection();

                EditorGUILayout.EndScrollView();
                DrawVerticalResizeHandle(ref _activeHeight, 190f, Mathf.Max(210f, position.height - 240f), PrefActiveHeight);
            }
        }

        private void DrawQuickActions()
        {
            UtilityWindowTheme.SectionTitle("Quick Actions", UtilityWindowTheme.Green);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame", GUILayout.Height(24f)))
                    FrameObject(_active.gameObject);

                if (GUILayout.Button("Ping", GUILayout.Height(24f)))
                    EditorGUIUtility.PingObject(_active.gameObject);

                if (GUILayout.Button("Select Generated", GUILayout.Height(24f)))
                {
                    _active.EnsureGeneratedRoot();
                    if (_active.GeneratedRoot != null)
                    {
                        Selection.activeGameObject = _active.GeneratedRoot.gameObject;
                        EditorGUIUtility.PingObject(_active.GeneratedRoot.gameObject);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UtilityWindowTheme.TintedButton("Rebuild", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                    RebuildActive("Rebuild Modular Path");

                if (UtilityWindowTheme.TintedButton("Clear Generated", UtilityWindowTheme.Amber, GUILayout.Height(28f)))
                {
                    Undo.RecordObject(_active, "Clear Generated Path Objects");
                    _active.ClearGenerated();
                    EditorUtility.SetDirty(_active);
                    _status = "Cleared generated path objects.";
                }
            }

            DrawProperty("autoRebuildInEditor", "Auto Rebuild", "Rebuild automatically while points/settings change in the editor.");
            if (_active.autoRebuildInEditor)
                DrawProperty("editorRebuildDebounce", "Rebuild Debounce (s)", "Higher values reduce editor churn while dragging points.");
        }

        private void DrawCoreSettings()
        {
            UtilityWindowTheme.SectionTitle("Prefabs & Spacing", UtilityWindowTheme.Blue);

            DrawProperty("segmentPrefab", "Segment Prefab", "Prefab repeated along the path.");
            DrawProperty("pointPrefab", "Point Prefab", "Optional prefab placed at each control point.");

            EditorGUILayout.Space(4f);
            DrawProperty("pathMode", "Path Mode", "Polyline or Catmull-Rom smoothing.");
            if (_active.pathMode == ModularPathSpawner.PathMode.SmoothCatmullRom)
            {
                DrawProperty("samplesPerMeter", "Samples Per Meter", "Smooth path sampling density.");
                DrawProperty("minSamplesPerSpan", "Min Samples Per Span", "Minimum smooth samples per control-point span.");
            }

            EditorGUILayout.Space(4f);
            DrawProperty("usePrefabLength", "Use Prefab Length", "Use prefab bounds to decide spacing rather than a fixed distance.");
            if (_active.usePrefabLength)
            {
                DrawProperty("prefabLengthAxis", "Prefab Length Axis", "Axis used to estimate segment length.");
                DrawProperty("manualSegmentLength", "Manual Segment Length", "Fallback length if prefab bounds are unreliable.");
            }
            else
            {
                DrawProperty("fixedSpacing", "Fixed Spacing", "Distance between segment centers along the path.");
            }

            DrawProperty("startOffset", "Start Offset", "Distance offset from the start of the path before first placement.");
            DrawProperty("closedLoop", "Closed Loop", "Connect the last point back to the first.");
            DrawProperty("pointTangentBlend", "Point Tangent Blend", "Blends incoming/outgoing direction used for point-prefab facing.");
        }

        private void DrawSocketSettings()
        {
            UtilityWindowTheme.SectionTitle("Socket Placement", UtilityWindowTheme.Purple);
            DrawProperty("useSockets", "Use Sockets", "Use named child transforms to align segment start/end joins.");
            if (_active.useSockets)
            {
                DrawProperty("startSocketName", "Start Socket Name", "Child transform name used as the segment start socket.");
                DrawProperty("endSocketName", "End Socket Name", "Child transform name used as the segment end socket.");
                DrawProperty("placeByEndpointsWhenUsingSockets", "Place By Endpoints", "Map socket positions to sampled path endpoints.");
            }
        }

        private void DrawSurfaceSettings()
        {
            UtilityWindowTheme.SectionTitle("Surface Conform", UtilityWindowTheme.Teal);

            DrawProperty("conformToSurface", "Conform To Surface", "Raycasts points/segments onto the configured mask.");
            if (_active.conformToSurface)
            {
                DrawProperty("surfaceMask", "Surface Mask", "Physics mask used for scene placement and snapping.");
                DrawProperty("raycastStartHeight", "Raycast Start Height", "Height above each point used when raycasting downward.");
                DrawProperty("sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both segment endpoints before computing segment placement.");
            }

            DrawProperty("yOffset", "Y Offset", "Vertical offset applied after surface placement.");
            DrawProperty("alignToSurfaceNormal", "Align To Surface Normal", "Use hit normal as the generated object's up vector.");
            DrawProperty("drawGizmos", "Draw Gizmos", "Draw path and control-point gizmos.");
        }

        private void DrawPointsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.14f, 0.08f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _pointsFoldout = EditorGUILayout.Foldout(_pointsFoldout, "Point Utilities", true, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(_active.PointCount.ToString(), UtilityWindowTheme.Purple, 46f);
                }

                if (!_pointsFoldout)
                    return;

                _pointsScroll = EditorGUILayout.BeginScrollView(_pointsScroll, GUILayout.Height(_pointsHeight));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Point Forward"))
                    {
                        Undo.RecordObject(_active, "Add Path Point");
                        Vector3 basePos = _active.PointCount > 0 ? _active.GetWorldPoint(_active.PointCount - 1) : _active.transform.position;
                        Vector3 newPos = basePos + _active.transform.forward * 5f;
                        _active.AddWorldPoint(newPos);
                        EditorUtility.SetDirty(_active);
                        RebuildIfAuto();
                    }

                    if (GUILayout.Button("Snap All To Surface"))
                    {
                        Undo.RecordObject(_active, "Snap Path Points To Surface");
                        SnapAllPointsToSurface(_active);
                        EditorUtility.SetDirty(_active);
                        RebuildIfAuto();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Clear Points", UtilityWindowTheme.Red))
                    {
                        if (EditorUtility.DisplayDialog("Clear Points", "Remove all points from this path?", "Clear", "Cancel"))
                        {
                            Undo.RecordObject(_active, "Clear Path Points");
                            _active.ClearPoints();
                            _active.ClearGenerated();
                            EditorUtility.SetDirty(_active);
                            _status = "Cleared path points.";
                        }
                    }

                    if (GUILayout.Button("Rebuild Now"))
                        RebuildActive("Rebuild Modular Path");
                }

                EditorGUILayout.Space(6f);
                DrawPointList();

                EditorGUILayout.EndScrollView();
                DrawVerticalResizeHandle(ref _pointsHeight, 120f, Mathf.Max(150f, position.height - 260f), PrefPointsHeight);
            }
        }

        private void DrawPointList()
        {
            int show = Mathf.Min(_active.PointCount, 75);
            if (show <= 0)
            {
                EditorGUILayout.HelpBox("No points yet. Add one from the button above or Shift-click in the Scene view.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(show < _active.PointCount ? "Quick Point List (first 75)" : "Quick Point List", EditorStyles.miniBoldLabel);
            for (int i = 0; i < show; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{i}", GUILayout.Width(34f));

                    Vector3 worldPoint = _active.GetWorldPoint(i);
                    EditorGUI.BeginChangeCheck();
                    Vector3 edited = EditorGUILayout.Vector3Field(GUIContent.none, worldPoint);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_active, "Edit Path Point");
                        _active.SetWorldPoint(i, edited);
                        EditorUtility.SetDirty(_active);
                        RebuildIfAuto();
                    }

                    if (GUILayout.Button("Frame", GUILayout.Width(58f)))
                        FrameWorldPoint(worldPoint);
                }
            }
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            if (_so == null)
                return;

            SerializedProperty property = _so.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, $"Missing serialized field: {propertyName}", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }

        private void RebuildActive(string undoName)
        {
            if (_active == null)
                return;

            Undo.RecordObject(_active, undoName);
            _active.Rebuild();
            EditorUtility.SetDirty(_active);
            _status = "Rebuilt active path.";
        }

        private void RebuildIfAuto()
        {
            if (_active != null && _active.autoRebuildInEditor)
                _active.Rebuild();
        }

        private static void SnapAllPointsToSurface(ModularPathSpawner path)
        {
            if (path == null)
                return;

            float height = Mathf.Max(0.01f, path.raycastStartHeight);
            for (int i = 0; i < path.PointCount; i++)
            {
                Vector3 wp = path.GetWorldPoint(i);
                Vector3 start = wp + Vector3.up * height;
                if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, height * 2f, path.surfaceMask, QueryTriggerInteraction.Ignore))
                    path.SetWorldPoint(i, hit.point);
            }
        }

        private void FrameObject(GameObject go)
        {
            if (go == null || SceneView.lastActiveSceneView == null)
                return;

            Bounds bounds = new Bounds(go.transform.position, Vector3.one * 2f);
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (bounds.size == Vector3.one * 2f)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            SceneView.lastActiveSceneView.Frame(bounds, false);
        }

        private void FrameWorldPoint(Vector3 point)
        {
            if (SceneView.lastActiveSceneView == null)
                return;

            SceneView.lastActiveSceneView.pivot = point;
            SceneView.lastActiveSceneView.Repaint();
        }

        private void DrawVerticalResizeHandle(ref float height, float min, float max, string prefKey)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(new Rect(rect.x + 8f, rect.y + 2f, Mathf.Max(0f, rect.width - 16f), 2f), new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.22f : 0.32f));

            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        _resizeStartMouse = e.mousePosition;
                        _resizeStartHeight = height;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        height = Mathf.Clamp(_resizeStartHeight + (e.mousePosition.y - _resizeStartMouse.y), min, max);
                        EditorPrefs.SetFloat(prefKey, height);
                        Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        EditorPrefs.SetFloat(prefKey, height);
                        e.Use();
                    }
                    break;
            }
        }

        private void LoadPrefs()
        {
            _createHeight = EditorPrefs.GetFloat(PrefCreateHeight, _createHeight);
            _activeHeight = EditorPrefs.GetFloat(PrefActiveHeight, _activeHeight);
            _pointsHeight = EditorPrefs.GetFloat(PrefPointsHeight, _pointsHeight);
            _createFoldout = EditorPrefs.GetBool(PrefCreateFoldout, _createFoldout);
            _activeFoldout = EditorPrefs.GetBool(PrefActiveFoldout, _activeFoldout);
            _pointsFoldout = EditorPrefs.GetBool(PrefPointsFoldout, _pointsFoldout);
            _parentUnderSelection = EditorPrefs.GetBool(PrefParentUnderSelection, _parentUnderSelection);
            _autoFrameOnCreate = EditorPrefs.GetBool(PrefAutoFrameOnCreate, _autoFrameOnCreate);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetFloat(PrefCreateHeight, _createHeight);
            EditorPrefs.SetFloat(PrefActiveHeight, _activeHeight);
            EditorPrefs.SetFloat(PrefPointsHeight, _pointsHeight);
            EditorPrefs.SetBool(PrefCreateFoldout, _createFoldout);
            EditorPrefs.SetBool(PrefActiveFoldout, _activeFoldout);
            EditorPrefs.SetBool(PrefPointsFoldout, _pointsFoldout);
            EditorPrefs.SetBool(PrefParentUnderSelection, _parentUnderSelection);
            EditorPrefs.SetBool(PrefAutoFrameOnCreate, _autoFrameOnCreate);
        }
    }

    internal static class ModularPathBuilderVectorExtensions
    {
        public static Vector3 FlattenYSafe(this Vector3 value, Vector3 fallback)
        {
            value.y = 0f;
            if (value.sqrMagnitude < 0.0001f)
                value = fallback;
            return value.normalized;
        }
    }
    #endif

}