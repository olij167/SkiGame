using UnityEditor;
using UnityEngine;

public class FencePathWindow : EditorWindow
{
    private FencePath _active;
    private SerializedObject _so;

    private Vector2 _scroll;

    // Lightweight “create defaults”
    private GameObject _defaultSegmentPrefab;
    private GameObject _defaultCornerPrefab;
    private bool _parentUnderSelection = true;

    [MenuItem("Tools/Fences/Fence Path Window")]
    public static void Open()
    {
        var w = GetWindow<FencePathWindow>("Fence Paths");
        w.minSize = new Vector2(340, 420);
        w.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        // Prefer a selected FencePath, otherwise keep current.
        var fp = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<FencePath>() : null;
        if (fp != null)
            SetActive(fp);

        Repaint();
    }

    private void SetActive(FencePath fp)
    {
        _active = fp;
        _so = _active != null ? new SerializedObject(_active) : null;
    }

    private void OnGUI()
    {
        DrawHeader();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawCreateSection();

        EditorGUILayout.Space(10);
        DrawActiveSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Fence Path Tool", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Find Selected", GUILayout.Width(110)))
            {
                var fp = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<FencePath>() : null;
                if (fp != null) SetActive(fp);
            }
        }

        EditorGUILayout.HelpBox(
            "Scene Controls (when a FencePath is selected):\n" +
            "- Shift + LMB: Add point at terrain hit\n" +
            "- Ctrl + LMB: Insert point into nearest segment\n" +
            "- Click point: Select\n" +
            "- Drag handle: Move selected point\n" +
            "- Delete/Backspace: Remove selected point\n",
            MessageType.Info);
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("Create", EditorStyles.boldLabel);

        _defaultSegmentPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Default Segment Prefab"),
            _defaultSegmentPrefab,
            typeof(GameObject),
            false);

        _defaultCornerPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Default Corner Post"),
            _defaultCornerPrefab,
            typeof(GameObject),
            false);

        _parentUnderSelection = EditorGUILayout.Toggle(
            new GUIContent("Parent Under Selection", "If enabled, creates the path as a child of the currently selected object (if any)."),
            _parentUnderSelection);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Fence Path", GUILayout.Height(28)))
            {
                CreateFencePath();
            }

            if (GUILayout.Button("Create + Frame", GUILayout.Height(28)))
            {
                var fp = CreateFencePath();
                if (fp != null) FrameObject(fp.gameObject);
            }
        }
    }

    private FencePath CreateFencePath()
    {
        Transform parent = null;
        if (_parentUnderSelection && Selection.activeTransform != null)
            parent = Selection.activeTransform;

        // Place near Scene view pivot if possible
        Vector3 pos = Vector3.zero;
        if (SceneView.lastActiveSceneView != null)
            pos = SceneView.lastActiveSceneView.pivot;

        var go = new GameObject("FencePath");
        Undo.RegisterCreatedObjectUndo(go, "Create Fence Path");

        if (parent != null)
            go.transform.SetParent(parent, true);

        go.transform.position = pos;

        var fp = go.AddComponent<FencePath>();
        fp.fenceSegmentPrefab = _defaultSegmentPrefab;
        fp.cornerPostPrefab = _defaultCornerPrefab;

        // Nice initial two-point stub so you can see something quickly
        fp.ClearPoints();
        fp.AddWorldPoint(pos);
        fp.AddWorldPoint(pos + Vector3.forward * 8f);

        fp.EnsureSegmentsRoot();
        fp.Rebuild();

        Selection.activeGameObject = go;
        SetActive(fp);

        EditorUtility.SetDirty(fp);
        return fp;
    }

    private void DrawActiveSection()
    {
        EditorGUILayout.LabelField("Active Path", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _active = (FencePath)EditorGUILayout.ObjectField(
                new GUIContent("FencePath"),
                _active,
                typeof(FencePath),
                true);

            if (GUILayout.Button("Use Selection", GUILayout.Width(110)))
            {
                var fp = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<FencePath>() : null;
                if (fp != null) SetActive(fp);
            }
        }

        if (_active == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with FencePath (or create one) to edit settings here.", MessageType.Warning);
            return;
        }

        if (_so == null || _so.targetObject != _active)
            _so = new SerializedObject(_active);

        _so.Update();

        DrawQuickActions();
        EditorGUILayout.Space(8);
        DrawCoreSettings();
        EditorGUILayout.Space(8);
        DrawTerrainSettings();
        EditorGUILayout.Space(8);
        DrawPointsUtility();

        _so.ApplyModifiedProperties();
    }

    private void DrawQuickActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Frame", GUILayout.Height(24)))
                FrameObject(_active.gameObject);

            if (GUILayout.Button("Select Segments", GUILayout.Height(24)))
            {
                _active.EnsureSegmentsRoot();
                Selection.activeGameObject = _active.SegmentsRoot.gameObject;
                EditorGUIUtility.PingObject(_active.SegmentsRoot.gameObject);
            }

            if (GUILayout.Button("Ping", GUILayout.Height(24)))
                EditorGUIUtility.PingObject(_active.gameObject);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild", GUILayout.Height(28)))
            {
                _active.Rebuild();
            }

            if (GUILayout.Button("Clear Generated", GUILayout.Height(28)))
            {
                _active.ClearGenerated();
            }
        }

        EditorGUILayout.Space(4);

        // Debounce + auto rebuild toggles (quick access)
        EditorGUILayout.PropertyField(_so.FindProperty("autoRebuildInEditor"), new GUIContent("Auto Rebuild"));
        if (_active.autoRebuildInEditor)
        {
            EditorGUILayout.PropertyField(_so.FindProperty("editorRebuildDebounce"),
                new GUIContent("Rebuild Debounce (s)", "Higher values reduce editor churn while dragging points."));
        }
    }

    private void DrawCoreSettings()
    {
        EditorGUILayout.LabelField("Fence Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_so.FindProperty("fenceSegmentPrefab"), new GUIContent("Segment Prefab"));
        EditorGUILayout.PropertyField(_so.FindProperty("cornerPostPrefab"), new GUIContent("Corner Post Prefab"));

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(_so.FindProperty("usePrefabLength"), new GUIContent("Use Prefab Length"));
        if (_active.usePrefabLength)
        {
            EditorGUILayout.PropertyField(_so.FindProperty("prefabLengthAxis"), new GUIContent("Prefab Length Axis"));
            EditorGUILayout.PropertyField(_so.FindProperty("manualSegmentLength"),
                new GUIContent("Manual Segment Length", "Fallback length if prefab bounds are unreliable, or if you want explicit control."));
        }
        else
        {
            EditorGUILayout.PropertyField(_so.FindProperty("fixedSpacing"),
                new GUIContent("Fixed Spacing", "Distance between segment centers along the path."));
        }

        EditorGUILayout.PropertyField(_so.FindProperty("startOffset"), new GUIContent("Start Offset"));

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(_so.FindProperty("closedLoop"), new GUIContent("Closed Loop"));
        EditorGUILayout.PropertyField(_so.FindProperty("cornerTangentBlend"),
            new GUIContent("Corner Tangent Blend", "Blends incoming/outgoing direction used for corner-post facing."));
    }

    private void DrawTerrainSettings()
    {
        EditorGUILayout.LabelField("Terrain Conform", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_so.FindProperty("conformToTerrain"), new GUIContent("Conform To Terrain"));
        if (_active.conformToTerrain)
        {
            EditorGUILayout.PropertyField(_so.FindProperty("terrainMask"), new GUIContent("Terrain Mask"));
            EditorGUILayout.PropertyField(_so.FindProperty("raycastStartHeight"), new GUIContent("Raycast Start Height"));
        }

        EditorGUILayout.PropertyField(_so.FindProperty("yOffset"), new GUIContent("Y Offset"));
        EditorGUILayout.PropertyField(_so.FindProperty("alignToTerrainNormal"),
            new GUIContent("Align To Terrain Normal", "If enabled, segment up vector uses the terrain normal; otherwise fences stay upright."));
    }

    private void DrawPointsUtility()
    {
        EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"Point Count: {_active.PointCount}");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Point Forward"))
            {
                Undo.RecordObject(_active, "Add Fence Point");
                Vector3 basePos = _active.PointCount > 0 ? _active.GetWorldPoint(_active.PointCount - 1) : _active.transform.position;
                Vector3 newPos = basePos + _active.transform.forward * 5f;
                _active.AddWorldPoint(newPos);
                EditorUtility.SetDirty(_active);

                if (_active.autoRebuildInEditor) _active.Rebuild();
            }

            if (GUILayout.Button("Snap All To Terrain"))
            {
                SnapAllPointsToTerrain(_active);
                EditorUtility.SetDirty(_active);

                if (_active.autoRebuildInEditor) _active.Rebuild();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Points"))
            {
                if (EditorUtility.DisplayDialog("Clear Points", "Remove all points from this FencePath?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(_active, "Clear Fence Points");
                    _active.ClearPoints();
                    EditorUtility.SetDirty(_active);
                    _active.ClearGenerated();
                }
            }

            if (GUILayout.Button("Rebuild Now"))
            {
                _active.Rebuild();
            }
        }

        EditorGUILayout.Space(6);

        // Minimal list view (not full editor) for quick ping/focus
        int show = Mathf.Min(_active.PointCount, 50);
        if (show > 0)
        {
            EditorGUILayout.LabelField("Quick Point List (first 50)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < show; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{i}", GUILayout.Width(30));

                    Vector3 wp = _active.GetWorldPoint(i);
                    EditorGUILayout.Vector3Field(GUIContent.none, wp);

                    if (GUILayout.Button("Frame", GUILayout.Width(60)))
                        FrameWorldPoint(wp);
                }
            }
        }
    }

    private static void SnapAllPointsToTerrain(FencePath path)
    {
        if (path == null || path.PointCount <= 0) return;

        for (int i = 0; i < path.PointCount; i++)
        {
            Vector3 wp = path.GetWorldPoint(i);
            Vector3 start = wp + Vector3.up * Mathf.Max(0.01f, path.raycastStartHeight);

            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit,
                path.raycastStartHeight * 2f, path.terrainMask, QueryTriggerInteraction.Ignore))
            {
                path.SetWorldPoint(i, hit.point);
            }
        }
    }

    private static void FrameObject(GameObject go)
    {
        if (go == null) return;
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static void FrameWorldPoint(Vector3 worldPoint)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;

        sv.pivot = worldPoint;
        sv.size = Mathf.Max(2f, sv.size);
        sv.Repaint();
    }
}
