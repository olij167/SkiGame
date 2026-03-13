#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SkiGame.Runs;

namespace SkiGame.RunsEditor
{
    public sealed class SkiRunPainterWindow : EditorWindow
    {
        private const string PrefKeyPrefix = "SkiGame.SkiRunPainter.";

        // Persisted settings
        private bool paintMode;
        private bool snapToTerrainOnAdd = true;
        private bool autoBakeOnEdit = true;
        private bool autoRebuildFlagsOnEdit = false;

        // QoL: allow locking an active run so painting doesn't depend on Unity selection.
        private bool lockActiveRun = false;
        private SkiRunLine lockedRun;

        // QoL: lightweight run browser
        private bool showRunBrowser = true;
        private bool showUtilities = true;
        private bool showDefaults = true;
        private string runSearch = "";
        private Vector2 runBrowserScroll;

        // Cached run list for the Run Browser (avoids FindObjectsOfType allocations every OnGUI repaint)
        private SkiRunLine[] _runsCache;
        private double _runsCacheNextRefreshTime;
        private const double RunsCacheRefreshSeconds = 0.75;

        private SkiRunLine[] GetRunsCached(bool forceRefresh = false)
        {
            double t = EditorApplication.timeSinceStartup;
            if (forceRefresh || _runsCache == null || t >= _runsCacheNextRefreshTime)
            {
                _runsCache = FindObjectsOfType<SkiRunLine>(true);
                _runsCacheNextRefreshTime = t + RunsCacheRefreshSeconds;
            }
            return _runsCache;
        }

        // Safety: reduce accidental edits when paint mode is on
        private bool requireShiftToPaint = true;

        // Throttle heavy work while painting
        private const double PostEditDelaySeconds = 0.15;
        private double lastEditTime;
        private SkiRunLine pendingPostEditRun;

        // Persisted defaults (assets stored by GUID)
        [SerializeField] private GameObject defaultFlagPrefab;
        [SerializeField] private RunDifficultyProfileSO defaultDifficultyProfile;

        // Not reliably persistable across sessions (scene object)
        [SerializeField] private Terrain defaultExplicitTerrain;

        [SerializeField, Range(2f, 200f)] private float defaultRunWidthMeters = 20f;
        [SerializeField, Range(1f, 50f)] private float defaultFlagSpacingMeters = 8f;

        [MenuItem("SkiGame/Ski Run Painter")]
        public static void Open() => GetWindow<SkiRunPainterWindow>("Ski Run Painter");

        public static void OpenAndSelect(SkiRunLine run, bool enablePaint = false)
        {
            var w = GetWindow<SkiRunPainterWindow>("Ski Run Painter");
            w.Show();
            w.Focus();

            if (run != null)
            {
                w.lockActiveRun = true;
                w.lockedRun = run;
                Selection.activeGameObject = run.gameObject;
                EditorGUIUtility.PingObject(run.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            if (enablePaint)
                w.paintMode = true;

            w.SavePrefs();
            w.Repaint();
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            LoadPrefs();
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += Repaint;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SavePrefs();
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= Repaint;
            EditorApplication.update -= OnEditorUpdate;
        }

        private SkiRunLine SelectedRun
        {
            get
            {
                if (lockActiveRun && lockedRun != null)
                    return lockedRun;

                if (Selection.activeGameObject == null) return null;
                return Selection.activeGameObject.GetComponent<SkiRunLine>();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            DrawActiveRunBlock();
            EditorGUILayout.Space(6);
            DrawPaintSessionBlock();
            EditorGUILayout.Space(6);
            DrawQuickCreateBlock();
            EditorGUILayout.Space(6);
            DrawDefaultsBlock();
            EditorGUILayout.Space(6);
            DrawUtilitiesBlock();
        }

        private void DrawActiveRunBlock()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Active Run", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Use this window to browse, lock, and paint a specific run. Use the Scene overlay for live mode switching.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);

                var selectionRun = (Selection.activeGameObject != null) ? Selection.activeGameObject.GetComponent<SkiRunLine>() : null;

                EditorGUI.BeginChangeCheck();
                bool newLock = EditorGUILayout.ToggleLeft("Lock Active Run (ignore Unity selection)", lockActiveRun);
                if (EditorGUI.EndChangeCheck())
                {
                    lockActiveRun = newLock;
                    if (lockActiveRun && lockedRun == null && selectionRun != null)
                        lockedRun = selectionRun;

                    SavePrefs();
                    Repaint();
                    SceneView.RepaintAll();
                }

                if (lockActiveRun)
                {
                    lockedRun = (SkiRunLine)EditorGUILayout.ObjectField("Locked Run", lockedRun, typeof(SkiRunLine), true);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(selectionRun == null))
                        {
                            if (GUILayout.Button("Use Current Selection"))
                            {
                                lockedRun = selectionRun;
                                SavePrefs();
                                Repaint();
                                SceneView.RepaintAll();
                            }
                        }

                        using (new EditorGUI.DisabledScope(lockedRun == null))
                        {
                            if (GUILayout.Button("Ping"))
                            {
                                EditorGUIUtility.PingObject(lockedRun.gameObject);
                                Selection.activeGameObject = lockedRun.gameObject;
                            }
                        }
                    }
                }

                var run = SelectedRun;
                if (run == null)
                {
                    EditorGUILayout.HelpBox("Select a GameObject with a SkiRunLine component, lock a run above, or create one below.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.ObjectField("Run Object", run.gameObject, typeof(GameObject), true);
                }

                EditorGUILayout.Space(6);
                showRunBrowser = EditorGUILayout.Foldout(showRunBrowser, "Run Browser", true);
                if (showRunBrowser)
                    DrawRunBrowser();
            }
        }

        private void DrawRunBrowser()
        {
            runSearch = EditorGUILayout.TextField("Search", runSearch);
            runBrowserScroll = EditorGUILayout.BeginScrollView(runBrowserScroll, GUILayout.Height(140));

            string search = string.IsNullOrWhiteSpace(runSearch) ? null : runSearch.Trim().ToLowerInvariant();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Runs", GUILayout.Width(40));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    GetRunsCached(forceRefresh: true);
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            var runs = GetRunsCached();
            foreach (var r in runs)
            {
                if (r == null) continue;
                if (search != null && !r.name.ToLowerInvariant().Contains(search))
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(r.name, GUILayout.ExpandWidth(true)))
                    {
                        lockedRun = r;
                        lockActiveRun = true;
                        Selection.activeGameObject = r.gameObject;
                        EditorGUIUtility.PingObject(r.gameObject);
                        SceneView.lastActiveSceneView?.FrameSelected();
                        SavePrefs();
                        Repaint();
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPaintSessionBlock()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Paint Session", EditorStyles.boldLabel);

                paintMode = EditorGUILayout.ToggleLeft("Enable Paint Mode (click terrain in Scene view)", paintMode);
                snapToTerrainOnAdd = EditorGUILayout.ToggleLeft("Snap point to terrain on add", snapToTerrainOnAdd);
                requireShiftToPaint = EditorGUILayout.ToggleLeft("Require Shift to Paint (recommended)", requireShiftToPaint);

                EditorGUILayout.HelpBox(
                    requireShiftToPaint
                        ? "Scene: Shift + LMB to add a point. This keeps painting from interfering with normal scene navigation."
                        : "Scene: LMB to add a point.",
                    MessageType.None);

                EditorGUILayout.Space(4);
                autoBakeOnEdit = EditorGUILayout.ToggleLeft("Auto Bake metrics/colour after edit", autoBakeOnEdit);
                autoRebuildFlagsOnEdit = EditorGUILayout.ToggleLeft("Auto Rebuild Flags after edit (slower)", autoRebuildFlagsOnEdit);

                var run = SelectedRun;
                if (paintMode && run == null)
                    EditorGUILayout.HelpBox("Select or create a run before painting points.", MessageType.Warning);
            }
        }

        private void DrawUtilitiesBlock()
        {
            var run = SelectedRun;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                showUtilities = EditorGUILayout.Foldout(showUtilities, "Utilities", true);
                if (!showUtilities) return;

                if (run == null)
                {
                    EditorGUILayout.HelpBox("Utilities become available when a run is selected or locked.", MessageType.None);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUILayout.Button("Undo Last Point");
                        GUILayout.Button("Clear Points");
                        GUILayout.Button("Bake Metrics");
                        GUILayout.Button("Rebuild Flags");
                    }
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Undo Last Point"))
                    {
                        Undo.RecordObject(run, "Undo Run Point");
                        run.RemoveLastPoint();
                        MarkAndRepaint(run);
                    }

                    if (GUILayout.Button("Clear Points"))
                    {
                        if (EditorUtility.DisplayDialog("Clear Points", "Remove all points from this run?", "Clear", "Cancel"))
                        {
                            Undo.RecordObject(run, "Clear Run Points");
                            run.ClearPoints();
                            MarkAndRepaint(run);
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
                        MarkAndRepaint(run);
                    }

                    if (GUILayout.Button("Rebuild Flags"))
                    {
                        Undo.RecordObject(run, "Rebuild Run Flags");
                        run.RebuildFlags();
                        run.ApplyColorToGeneratedFlags();
                        MarkAndRepaint(run);
                    }
                }

                EditorGUILayout.Space(6);

                if (GUILayout.Button("Rebuild Flags (All Runs)"))
                    RebuildFlagsAllRuns();
            }
        }

        private void DrawDefaultsBlock()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                showDefaults = EditorGUILayout.Foldout(showDefaults, "Defaults For New Runs", true);
                if (!showDefaults) return;

                EditorGUILayout.LabelField("These values are applied when you create a new SkiRunLine from this window.", EditorStyles.wordWrappedMiniLabel);
                defaultFlagPrefab = (GameObject)EditorGUILayout.ObjectField("Flag Prefab", defaultFlagPrefab, typeof(GameObject), false);
                defaultDifficultyProfile = (RunDifficultyProfileSO)EditorGUILayout.ObjectField("Difficulty Profile", defaultDifficultyProfile, typeof(RunDifficultyProfileSO), false);
                defaultRunWidthMeters = EditorGUILayout.Slider("Run Width (m)", defaultRunWidthMeters, 2f, 200f);
                defaultFlagSpacingMeters = EditorGUILayout.Slider("Flag Spacing (m)", defaultFlagSpacingMeters, 1f, 50f);
            }
        }

        private void DrawQuickCreateBlock()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Create", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Create a new run near the current Scene view camera and immediately select it for editing.", EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Create New Run (and select it)"))
                    CreateNewRun();
            }
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!paintMode) return;

            var run = SelectedRun;
            if (run == null) return;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 260, 60), EditorStyles.helpBox);
            GUILayout.Label("Ski Run Painter", EditorStyles.boldLabel);
            GUILayout.Label(requireShiftToPaint ? "Shift + LMB: Add Point" : "LMB: Add Point");
            GUILayout.EndArea();
            Handles.EndGUI();

            var e = Event.current;
            if (e == null) return;

            // We only want to capture input when paint mode conditions are met.
            bool wantsPaintInput = !e.alt && (!requireShiftToPaint || e.shift);

            // Claim scene view input during Layout so clicks don’t select objects instead of painting.
            if (wantsPaintInput && e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            // Only react on left click.
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (!wantsPaintInput) return;

            // Raycast into scene.
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 50000f)) return;

            // Prefer terrain hits; you can relax this if you want mesh painting later.
            if (!(hit.collider is TerrainCollider)) return;

            Vector3 p = hit.point;
            if (snapToTerrainOnAdd)
                p = SnapToTerrain(p);

            // Use the same insertion policy as the SkiRunLine inspector:
            // insert on the closest segment, including start/end when you click beyond.
            Undo.RecordObject(run, "Insert Run Point");
            run.InsertPointWorldSmart(p);

            if (autoBakeOnEdit)
                run.BakeMetrics();

            if (autoRebuildFlagsOnEdit)
                QueuePostEditWork(run);

            MarkAndRepaint(run);

            e.Use();

        }

        private static Terrain FindTerrainAt(Vector3 worldPos)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains != null)
            {
                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain t = terrains[i];
                    if (t == null || t.terrainData == null) continue;

                    Vector3 p = t.transform.position;
                    Vector3 size = t.terrainData.size;

                    if (worldPos.x >= p.x && worldPos.x <= p.x + size.x &&
                        worldPos.z >= p.z && worldPos.z <= p.z + size.z)
                        return t;
                }
            }

            return Terrain.activeTerrain;
        }

        private static Vector3 SnapToTerrain(Vector3 worldPos)
        {
            Terrain t = FindTerrainAt(worldPos);
            if (t == null) return worldPos;

            float h = t.SampleHeight(worldPos) + t.transform.position.y;
            worldPos.y = h;
            return worldPos;
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
                    spawnPos = hit.point;
                else
                    spawnPos = cam.transform.position + cam.transform.forward * 20f;
            }

            var go = new GameObject("SkiRun_New");
            Undo.RegisterCreatedObjectUndo(go, "Create Ski Run");
            go.transform.position = spawnPos;
            go.transform.rotation = spawnRot;

            var run = go.AddComponent<SkiRunLine>();

            // Apply defaults via SerializedObject to avoid runtime-only setters.
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

            Selection.activeGameObject = go;
            MarkAndRepaint(run);
        }

        private void RebuildFlagsAllRuns()
        {
            var runs = FindObjectsOfType<SkiRunLine>(true);
            if (runs == null || runs.Length == 0) return;

            // Ensure flags react to connections immediately.
            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;

                r.RebuildFlags();
                r.ApplyColorToGeneratedFlags();
                EditorUtility.SetDirty(r);
            }

            SceneView.RepaintAll();
        }

        private static void MarkAndRepaint(Object obj)
        {
            EditorUtility.SetDirty(obj);
            SceneView.RepaintAll();
        }

        private void QueuePostEditWork(SkiRunLine run)
        {
            pendingPostEditRun = run;
            lastEditTime = EditorApplication.timeSinceStartup;
        }

        private void OnEditorUpdate()
        {
            if (pendingPostEditRun == null) return;
            if (EditorApplication.timeSinceStartup - lastEditTime < PostEditDelaySeconds) return;

            var run = pendingPostEditRun;
            pendingPostEditRun = null;

            if (run == null) return;

            if (autoRebuildFlagsOnEdit)
            {
                Undo.RecordObject(run, "Rebuild Run Flags");
                run.RebuildFlags();
                run.ApplyColorToGeneratedFlags();
                EditorUtility.SetDirty(run);
            }

            SceneView.RepaintAll();
        }


        // ---------- Persistence ----------
        private void LoadPrefs()
        {
            paintMode = EditorPrefs.GetBool(PrefKeyPrefix + "paintMode", paintMode);
            snapToTerrainOnAdd = EditorPrefs.GetBool(PrefKeyPrefix + "snapToTerrainOnAdd", snapToTerrainOnAdd);
            autoBakeOnEdit = EditorPrefs.GetBool(PrefKeyPrefix + "autoBakeOnEdit", autoBakeOnEdit);
            autoRebuildFlagsOnEdit = EditorPrefs.GetBool(PrefKeyPrefix + "autoRebuildFlagsOnEdit", autoRebuildFlagsOnEdit);

            defaultRunWidthMeters = EditorPrefs.GetFloat(PrefKeyPrefix + "defaultRunWidthMeters", defaultRunWidthMeters);
            defaultFlagSpacingMeters = EditorPrefs.GetFloat(PrefKeyPrefix + "defaultFlagSpacingMeters", defaultFlagSpacingMeters);

            defaultFlagPrefab = LoadAssetByGuid<GameObject>(PrefKeyPrefix + "defaultFlagPrefabGuid", defaultFlagPrefab);
            defaultDifficultyProfile = LoadAssetByGuid<RunDifficultyProfileSO>(PrefKeyPrefix + "defaultDifficultyProfileGuid", defaultDifficultyProfile);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(PrefKeyPrefix + "paintMode", paintMode);
            EditorPrefs.SetBool(PrefKeyPrefix + "snapToTerrainOnAdd", snapToTerrainOnAdd);
            EditorPrefs.SetBool(PrefKeyPrefix + "autoBakeOnEdit", autoBakeOnEdit);
            EditorPrefs.SetBool(PrefKeyPrefix + "autoRebuildFlagsOnEdit", autoRebuildFlagsOnEdit);

            EditorPrefs.SetFloat(PrefKeyPrefix + "defaultRunWidthMeters", defaultRunWidthMeters);
            EditorPrefs.SetFloat(PrefKeyPrefix + "defaultFlagSpacingMeters", defaultFlagSpacingMeters);

            SaveAssetGuid(PrefKeyPrefix + "defaultFlagPrefabGuid", defaultFlagPrefab);
            SaveAssetGuid(PrefKeyPrefix + "defaultDifficultyProfileGuid", defaultDifficultyProfile);
        }

        private static void SaveAssetGuid(string key, Object obj)
        {
            string guid = "";
            if (obj != null)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
            }
            EditorPrefs.SetString(key, guid);
        }

        private static T LoadAssetByGuid<T>(string key, T fallback) where T : Object
        {
            string guid = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(guid)) return fallback;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return fallback;

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : fallback;
        }
    }
}
#endif
