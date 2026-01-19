#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SkiGame.Runs;

namespace SkiGame.RunsEditor
{
    public sealed class SkiRunPainterWindow : EditorWindow
    {
        private bool autoBakeOnEdit = true;
        private bool autoRebuildFlagsOnEdit = false;

        private bool paintMode;
        private bool snapToTerrainOnAdd = true;

        [SerializeField] private GameObject defaultFlagPrefab;
        [SerializeField] private RunDifficultyProfileSO defaultDifficultyProfile;
        [SerializeField] private Terrain defaultExplicitTerrain;

        [SerializeField, Range(2f, 200f)] private float defaultRunWidthMeters = 20f;
        [SerializeField, Range(1f, 50f)] private float defaultFlagSpacingMeters = 8f;

        [MenuItem("Tools/Ski Game/Ski Run Painter")]
        public static void Open()
        {
            GetWindow<SkiRunPainterWindow>("Ski Run Painter");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private SkiRunLine SelectedRun
        {
            get
            {
                if (Selection.activeGameObject == null) return null;
                return Selection.activeGameObject.GetComponent<SkiRunLine>();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);

                var run = SelectedRun;
                if (run == null)
                {
                    EditorGUILayout.HelpBox("Select a GameObject with a SkiRunLine component (or create one below).", MessageType.Info);

                    if (GUILayout.Button("Create New Run"))
                        CreateNewRun();

                    return;
                }

                EditorGUILayout.ObjectField("Run Object", run.gameObject, typeof(GameObject), true);

                EditorGUILayout.Space(6);

                paintMode = EditorGUILayout.ToggleLeft("Paint Points (click terrain)", paintMode);
                snapToTerrainOnAdd = EditorGUILayout.ToggleLeft("Snap point to terrain on add", snapToTerrainOnAdd);

                autoBakeOnEdit = EditorGUILayout.ToggleLeft("Auto Bake (update difficulty color on edit)", autoBakeOnEdit);
                autoRebuildFlagsOnEdit = EditorGUILayout.ToggleLeft("Auto Rebuild Flags (slow)", autoRebuildFlagsOnEdit);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Defaults (for new runs)", EditorStyles.boldLabel);

                defaultFlagPrefab = (GameObject)EditorGUILayout.ObjectField("Flag Prefab", defaultFlagPrefab, typeof(GameObject), false);
                defaultDifficultyProfile = (RunDifficultyProfileSO)EditorGUILayout.ObjectField("Difficulty Profile", defaultDifficultyProfile, typeof(RunDifficultyProfileSO), false);
                defaultExplicitTerrain = (Terrain)EditorGUILayout.ObjectField("Explicit Terrain (optional)", defaultExplicitTerrain, typeof(Terrain), true);

                defaultRunWidthMeters = EditorGUILayout.Slider("Run Width (m)", defaultRunWidthMeters, 2f, 200f);
                defaultFlagSpacingMeters = EditorGUILayout.Slider("Flag Spacing (m)", defaultFlagSpacingMeters, 1f, 50f);

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Undo Last Point"))
                    {
                        Undo.RecordObject(run, "Undo Run Point");
                        run.RemoveLastPoint();
                        EditorUtility.SetDirty(run);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Clear Points"))
                    {
                        if (EditorUtility.DisplayDialog("Clear Points", "Remove all points from this run?", "Clear", "Cancel"))
                        {
                            Undo.RecordObject(run, "Clear Run Points");
                            run.ClearPoints();
                            EditorUtility.SetDirty(run);
                            SceneView.RepaintAll();
                        }
                    }
                }

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

                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Tip: Use the SkiRunLine inspector to assign the Difficulty Profile and Flag Prefab.", MessageType.None);
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Quick Create", EditorStyles.boldLabel);
                if (GUILayout.Button("Create New Run (and select it)"))
                    CreateNewRun();
            }
        }

        private void CreateNewRun()
        {
            // Spawn near SceneView look position (raycast from camera center).
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            var sv = SceneView.lastActiveSceneView;
            Camera cam = sv != null ? sv.camera : null;

            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, 50000f) && hit.collider is TerrainCollider)
                {
                    spawnPos = hit.point;
                }
                else
                {
                    // Fallback: place some distance in front of camera
                    spawnPos = cam.transform.position + cam.transform.forward * 20f;
                }
            }

            var go = new GameObject("SkiRun_New");
            Undo.RegisterCreatedObjectUndo(go, "Create Ski Run");
            go.transform.position = spawnPos;
            go.transform.rotation = spawnRot;

            var run = go.AddComponent<SkiRunLine>();

            // Apply defaults (if you have setters, use them; otherwise SerializedObject).
            // Using SerializedObject avoids making runtime setters just for editor convenience.
            var so = new SerializedObject(run);

            var flagProp = so.FindProperty("flagPrefab");
            if (flagProp != null) flagProp.objectReferenceValue = defaultFlagPrefab;

            var diffProp = so.FindProperty("difficultyProfile");
            if (diffProp != null) diffProp.objectReferenceValue = defaultDifficultyProfile;

            var terrainProp = so.FindProperty("explicitTerrain");
            if (terrainProp != null) terrainProp.objectReferenceValue = defaultExplicitTerrain;

            var widthProp = so.FindProperty("runWidthMeters");
            if (widthProp != null) widthProp.floatValue = defaultRunWidthMeters;

            var spacingProp = so.FindProperty("flagSpacingMeters");
            if (spacingProp != null) spacingProp.floatValue = defaultFlagSpacingMeters;

            so.ApplyModifiedPropertiesWithoutUndo();

            so.UpdateIfRequiredOrScript();
            EditorUtility.SetDirty(run);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!paintMode) return;

            var run = SelectedRun;
            if (run == null) return;

            Event e = Event.current;
            if (e == null) return;

            // Avoid selecting other objects while painting.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // Left-click to add point (ignore if alt is held for orbit).
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 50000f))
                {
                    // Restrict to Terrain to avoid accidental clicks on props/rocks/lift towers.
                    if (!(hit.collider is TerrainCollider))
                        return;

                    Vector3 p = hit.point;

                    if (snapToTerrainOnAdd)
                    {
                        // Snap to the terrain we actually hit (multi-terrain safe).
                        if (hit.collider is TerrainCollider tc)
                        {
                            Terrain t = tc.GetComponent<Terrain>();
                            if (t != null)
                            {
                                float h = t.SampleHeight(p) + t.transform.position.y;
                                p.y = h;
                            }
                        }

                    }

                    Undo.RecordObject(run, "Add Run Point");
                    run.AddPointWorld(p);
                    EditorUtility.SetDirty(run);

                    // Respect toggles (prevents sluggishness on long runs).
                    if (autoBakeOnEdit)
                        run.BakeMetrics();

                    if (autoRebuildFlagsOnEdit)
                        run.RebuildFlags();

                    SceneView.RepaintAll();
                    e.Use();
                }
            }

            // Visual hints
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 360, 70), "Ski Run Painter", GUI.skin.window);
            GUILayout.Label("Paint Mode Active");
            GUILayout.Label("Left Click: add point on terrain");
            GUILayout.Label("Esc: exit paint mode");
            GUILayout.EndArea();
            Handles.EndGUI();

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                paintMode = false;
                Repaint();
                e.Use();
            }
        }
    }
}
#endif
