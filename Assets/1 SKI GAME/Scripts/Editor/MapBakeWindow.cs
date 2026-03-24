#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SkiGame.Map;
using SkiGame.POI;
using SkiGame.Runs;
using UnityEditorInternal;

public sealed class MapBakeWindow : EditorWindow
{
    [Header("Target")]
    [SerializeField] private MapData targetMapData;

    [Header("Bake Scope")]
    [SerializeField] private bool includeSkiRuns = true;
    [SerializeField] private bool includeLiftLines = true;
    [SerializeField] private bool includePOIsFromRegistry = true;

    [Header("Projection Bounds")]
    [SerializeField] private float boundsPaddingMeters = 25f;

    [SerializeField, Tooltip("If enabled, the MapProjection bounds (and therefore the baked background in safe mode) " +
                              "will include the full extents of Terrain.activeTerrains. " +
                              "This prevents the background bake from being 'zoomed in' to only runs/lifts/POIs.")]
    private bool includeActiveTerrainsInProjectionBounds = true;

    [Header("Optional Rotation")]
    [SerializeField] private bool useRotation = false;
    [SerializeField] private float rotationYawDeg = 0f;

    [Header("Background Snapshot (Optional)")]
    [SerializeField] private bool bakeBackgroundTexture = true;
    [SerializeField] private int textureSize = 2048;
    [SerializeField] private float captureHeight = 2000f;
    [SerializeField] private Color clearColor = new Color(0.92f, 0.94f, 0.96f, 1f);
    [SerializeField] private LayerMask captureCullingMask = ~0;

    [Header("Alignment Safety (Prevents Misaligned Re-bakes)")]
    [Tooltip("When enabled, the baked background is captured EXACTLY to the MapProjection bounds.\n" +
             "This prevents the common 'markers drift outward' issue caused by terrain-centering or capture union.\n\n" +
             "While enabled:\n" +
             "- Terrain expansion is disabled\n" +
             "- Rotation is blocked for background bakes (rotated overscan cannot be corrected with a simple UV inset)\n" +
             "- MapData.backgroundUvMin/Max are reset to (0,0)-(1,1)")]
    [SerializeField] private bool lockBackgroundToProjectionBounds = true;

    [Tooltip("Only available when Alignment Safety is OFF. If enabled, the background capture bounds expand to include Terrain.activeTerrains bounds.")]
    [SerializeField] private bool allowTerrainExpansionWhenUnlocked = true;

    [Tooltip("Only required when Alignment Safety is OFF and Bake Background Texture is ON.\nType EXACTLY: BAKE UNSAFE")]
    [SerializeField] private string unsafeBakeConfirm = "";

    [Header("Output")]
    [SerializeField] private string outputFolder = "Assets/1 SKI GAME/Art/Map";
    [SerializeField] private string backgroundFileName = "MapBackground.png";

    private const string UnsafePhrase = "BAKE UNSAFE";

    [MenuItem("SkiGame/Map/Bake Map Data")]
    public static void Open()
    {
        var w = GetWindow<MapBakeWindow>("Map Bake");
        w.minSize = new Vector2(460, 610);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Map Bake Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bakes a MapData asset from the current scene:\n" +
            "- Projection bounds from runs, lifts, POIs\n" +
            "- Run + lift polylines\n" +
            "- POI markers from PointOfInterestRegistry\n" +
            "- Optional orthographic background snapshot\n\n" +
            "NOTE: Misalignment almost always comes from the background being baked with different bounds than MapProjection.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        targetMapData = (MapData)EditorGUILayout.ObjectField("Target MapData", targetMapData, typeof(MapData), false);

        EditorGUILayout.Space(10);
        DrawScope();
        EditorGUILayout.Space(8);
        DrawProjection();
        EditorGUILayout.Space(8);
        DrawBackground();
        EditorGUILayout.Space(8);
        DrawOutput();

        EditorGUILayout.Space(14);

        bool canBake = targetMapData != null && ValidateBakeReadiness(showUIWarnings: true);

        using (new EditorGUI.DisabledScope(!canBake))
        {
            if (GUILayout.Button("Bake MapData Now", GUILayout.Height(38)))
            {
                Bake();
            }
        }

        if (targetMapData == null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Assign a MapData asset to bake into (Create via Assets → Create → SkiGame → Map → Map Data).", MessageType.Warning);
        }
    }

    private void DrawScope()
    {
        EditorGUILayout.LabelField("Bake Scope", EditorStyles.boldLabel);
        includeSkiRuns = EditorGUILayout.ToggleLeft("Include Ski Runs (SkiRunLine)", includeSkiRuns);
        includeLiftLines = EditorGUILayout.ToggleLeft("Include Lift Lines (LiftLine)", includeLiftLines);
        includePOIsFromRegistry = EditorGUILayout.ToggleLeft("Include POIs from PointOfInterestRegistry", includePOIsFromRegistry);
    }

    private void DrawProjection()
    {
        EditorGUILayout.LabelField("Projection", EditorStyles.boldLabel);
        boundsPaddingMeters = EditorGUILayout.FloatField(new GUIContent("Bounds Padding (m)"), boundsPaddingMeters);

        includeActiveTerrainsInProjectionBounds = EditorGUILayout.ToggleLeft(
            "Include Active Terrains In Projection Bounds",
            includeActiveTerrainsInProjectionBounds);

        EditorGUILayout.Space(4);

        // Rotation is still useful for projection math, but when Alignment Safety is ON we block rotation for BACKGROUND bakes.
        useRotation = EditorGUILayout.ToggleLeft("Use Rotation (Yaw)", useRotation);
        using (new EditorGUI.DisabledScope(!useRotation))
        {
            rotationYawDeg = EditorGUILayout.Slider(new GUIContent("Rotation Yaw (deg)"), rotationYawDeg, -180f, 180f);
        }

        if (bakeBackgroundTexture && lockBackgroundToProjectionBounds && useRotation)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Alignment Safety is ON and a background bake is enabled.\n" +
                "Rotation will be blocked for the background capture to prevent subtle overscan/misalignment.\n\n" +
                "If you truly need a rotated background image, disable Alignment Safety and use the unsafe bake flow.",
                MessageType.Warning);
        }
    }

    private void DrawBackground()
    {
        EditorGUILayout.LabelField("Background Snapshot", EditorStyles.boldLabel);

        bakeBackgroundTexture = EditorGUILayout.ToggleLeft("Bake Background Texture", bakeBackgroundTexture);

        using (new EditorGUI.DisabledScope(!bakeBackgroundTexture))
        {
            EditorGUILayout.Space(4);

            lockBackgroundToProjectionBounds = EditorGUILayout.ToggleLeft(
                "Alignment Safety: Lock Background To Projection Bounds",
                lockBackgroundToProjectionBounds);

            if (lockBackgroundToProjectionBounds)
            {
                EditorGUILayout.HelpBox(
                    "Safe mode ON:\n" +
                    "- Background capture bounds will match MapProjection bounds exactly.\n" +
                    "- Terrain expansion is disabled.\n" +
                    "- MapData background UV inset is reset to (0,0)-(1,1).\n" +
                    "- Rotation is blocked for background capture to avoid overscan that cannot be corrected with a simple UV inset.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Unsafe mode:\n" +
                    "You can bake a background that does NOT match MapProjection bounds (e.g., terrain-inclusive, rotated).\n" +
                    "This is easy to misalign. A confirmation phrase is required before baking.\n\n" +
                    "If terrain expansion is used WITHOUT rotation, the tool will automatically write a Background UV inset to MapData.",
                    MessageType.Warning);

                allowTerrainExpansionWhenUnlocked = EditorGUILayout.ToggleLeft("Expand Capture Bounds To Include Active Terrains", allowTerrainExpansionWhenUnlocked);

                unsafeBakeConfirm = EditorGUILayout.TextField(new GUIContent($"Type '{UnsafePhrase}' to enable bake"), unsafeBakeConfirm);
            }

            EditorGUILayout.Space(6);

            textureSize = EditorGUILayout.IntPopup(
                "Texture Size",
                textureSize,
                new[] { "1024", "2048", "4096" },
                new[] { 1024, 2048, 4096 }
            );

            captureHeight = EditorGUILayout.FloatField("Capture Height (Y)", captureHeight);
            clearColor = EditorGUILayout.ColorField("Clear Color", clearColor);
            captureCullingMask = LayerMaskField("Culling Mask", captureCullingMask);
        }
    }

    private void DrawOutput()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField(new GUIContent("Output Folder"), outputFolder);
        backgroundFileName = EditorGUILayout.TextField(new GUIContent("Background File Name"), backgroundFileName);

        if (GUILayout.Button("Select Output Folder..."))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Output Folder", Application.dataPath, "");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                string assetsPath = Application.dataPath.Replace("\\", "/");
                selected = selected.Replace("\\", "/");
                if (selected.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
                {
                    outputFolder = "Assets" + selected.Substring(assetsPath.Length);
                }
                else
                {
                    Debug.LogWarning("Selected folder must be inside this project's Assets folder.");
                }
            }
        }
    }

    private bool ValidateBakeReadiness(bool showUIWarnings)
    {
        if (!bakeBackgroundTexture) return true;

        if (lockBackgroundToProjectionBounds)
        {
            // Safe mode: we block rotation for background bake (projection rotation can still exist, but background must not).
            if (useRotation)
            {
                // We don't hard-block the entire bake; we will auto-disable rotation for the background capture.
                // But we do warn in the UI.
                return true;
            }

            return true;
        }

        // Unsafe mode requires confirmation phrase to avoid accidental misaligned rebakes.
        if (!string.Equals(unsafeBakeConfirm?.Trim(), UnsafePhrase, StringComparison.Ordinal))
        {
            if (showUIWarnings)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    $"Unsafe background bake is armed but not confirmed.\n" +
                    $"Type '{UnsafePhrase}' exactly to enable the bake button.",
                    MessageType.Error);
            }
            return false;
        }

        return true;
    }

    private void Bake()
    {
        if (targetMapData == null)
        {
            Debug.LogError("[MapBake] No target MapData assigned.");
            return;
        }

        // Editor guardrails (also enforced here, not just UI)
        if (!ValidateBakeReadiness(showUIWarnings: false))
        {
            Debug.LogError("[MapBake] Bake blocked: unsafe background bake is not confirmed.");
            return;
        }

        // 1) Discover source objects
        var runs = includeSkiRuns ? FindAll<SkiRunLine>() : Array.Empty<SkiRunLine>();
        var lifts = includeLiftLines ? FindAll<LiftLine>() : Array.Empty<LiftLine>();

        PointOfInterestRegistry poiRegistry = null;
        IReadOnlyList<POIInfo> poiList = Array.Empty<POIInfo>();
        if (includePOIsFromRegistry)
        {
            poiRegistry = FindPOIRegistry();
            if (poiRegistry != null)
            {
                try { poiRegistry.Refresh(); } catch { /* keep bake resilient */ }
                poiList = poiRegistry.Current ?? Array.Empty<POIInfo>();
            }
        }

        // 2) Compute bounds (XZ) from all relevant sources
        if (!TryComputeBoundsXZ(runs, lifts, poiList, out Vector2 minXZ, out Vector2 maxXZ))
        {
            Debug.LogError("[MapBake] Could not compute bounds. Ensure you have at least one run/lift/POI in the scene.");
            return;
        }

        // OPTIONAL: expand projection to include active terrains.
        // This prevents safe-mode background bakes from being "zoomed in" to just runs/lifts/POIs.
        if (includeActiveTerrainsInProjectionBounds && TryGetActiveTerrainBoundsXZ(out Vector2 tMin, out Vector2 tMax))
        {
            minXZ = new Vector2(Mathf.Min(minXZ.x, tMin.x), Mathf.Min(minXZ.y, tMin.y));
            maxXZ = new Vector2(Mathf.Max(maxXZ.x, tMax.x), Mathf.Max(maxXZ.y, tMax.y));
        }

        minXZ -= Vector2.one * Mathf.Max(0f, boundsPaddingMeters);
        maxXZ += Vector2.one * Mathf.Max(0f, boundsPaddingMeters);

        var projection = new MapProjection
        {
            worldMinXZ = minXZ,
            worldMaxXZ = maxXZ,
            useRotation = useRotation,
            rotationYawDeg = rotationYawDeg,
            worldOrigin = new Vector3((minXZ.x + maxXZ.x) * 0.5f, 0f, (minXZ.y + maxXZ.y) * 0.5f)
        };

        // 3) Build polylines
        var polylines = new List<MapPolyline>(runs.Length + lifts.Length);

        if (includeSkiRuns)
            AppendRunPolylines(runs, polylines);

        if (includeLiftLines)
            AppendLiftPolylines(lifts, poiList, polylines);

        // 4) Build markers from POI registry
        var markers = new List<MapMarker>(poiList.Count);
        if (includePOIsFromRegistry)
            AppendMarkers(poiList, markers);

        // 5) Optionally bake background
        Texture2D bgTex = null;
        Vector2 insetMin = Vector2.zero;
        Vector2 insetMax = Vector2.one;
        bool wroteInset = false;

        if (bakeBackgroundTexture)
        {
            // If safe mode and rotation is enabled, block rotation for the background bake only.
            // This prevents rotated overscan that cannot be corrected by backgroundUvMin/Max.
            bool backgroundAllowRotation = !lockBackgroundToProjectionBounds;

            bgTex = BakeBackgroundTexture(
                projection,
                lockToProjectionBounds: lockBackgroundToProjectionBounds,
                allowTerrainExpansion: (!lockBackgroundToProjectionBounds && allowTerrainExpansionWhenUnlocked),
                allowRotationForBackground: backgroundAllowRotation,
                out insetMin,
                out insetMax,
                out wroteInset);
        }

        // 6) Apply to MapData
        Undo.RecordObject(targetMapData, "Bake Map Data");

        targetMapData.ClearAll();
        targetMapData.SetProjection(projection);
        targetMapData.SetPolylines(polylines);
        targetMapData.SetMarkers(markers);

        // Projection policy:
        // For baked gameplay maps, prefer deterministic MapProjection alignment by default.
        // A live scene camera should only be used when explicitly required and guaranteed
        // to match the baked background exactly.
        bool shouldPreferCameraProjection = false;

        targetMapData.SetPreferCameraProjection(shouldPreferCameraProjection);

        if (bakeBackgroundTexture)
            targetMapData.SetBackground(bgTex);

        // Enforce inset policy:
        // - Safe mode: always reset to full-UV
        // - Unsafe mode: write inset only when it is valid (unrotated capture union)
        if (bakeBackgroundTexture)
        {
            if (lockBackgroundToProjectionBounds)
            {
                SetBackgroundInsetSerialized(targetMapData, Vector2.zero, Vector2.one);
            }
            else if (wroteInset)
            {
                SetBackgroundInsetSerialized(targetMapData, insetMin, insetMax);
            }
        }

        EditorUtility.SetDirty(targetMapData);

        try
        {
#if UNITY_2022_2_OR_NEWER
            AssetDatabase.SaveAssetIfDirty(targetMapData);
#else
    AssetDatabase.SaveAssets();
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[MapBake] MapData was updated in memory, but saving triggered an editor-side exception. " +
                $"The baked marker data may not persist to disk until the terrain save issue is resolved.\n{ex}");
        }

        Debug.Log($"[MapBake] Baked MapData '{targetMapData.name}'. Lines: {polylines.Count}, Markers: {markers.Count}, Background: {(bgTex ? bgTex.name : "none")}" +
                  $"{(bakeBackgroundTexture ? $" | Inset: {(lockBackgroundToProjectionBounds ? "(reset 0..1)" : (wroteInset ? $"{insetMin}..{insetMax}" : "(none)"))}" : "")}");
    }

    // --------------------------
    // Discovery
    // --------------------------

    private static T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    private static PointOfInterestRegistry FindPOIRegistry()
    {
        if (PointOfInterestRegistry.Instance != null)
            return PointOfInterestRegistry.Instance;

#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<PointOfInterestRegistry>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<PointOfInterestRegistry>(true);
#endif
    }

    // --------------------------
    // Bounds + Builders
    // --------------------------

    private static bool TryComputeBoundsXZ(
        SkiRunLine[] runs,
        LiftLine[] lifts,
        IReadOnlyList<POIInfo> poiList,
        out Vector2 minXZ,
        out Vector2 maxXZ)
    {
        minXZ = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maxXZ = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        bool any = false;

        // Runs
        if (runs != null)
        {
            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;
                var pts = r.PointsWorld;
                if (pts == null) continue;

                for (int p = 0; p < pts.Count; p++)
                {
                    Vector3 w = pts[p];
                    any = true;
                    ExpandBounds(w, ref minXZ, ref maxXZ);
                }
            }
        }

        // Lifts (stations)
        if (lifts != null)
        {
            for (int i = 0; i < lifts.Length; i++)
            {
                var l = lifts[i];
                if (l == null) continue;

                if (l.bottomStation != null)
                {
                    any = true;
                    ExpandBounds(l.bottomStation.position, ref minXZ, ref maxXZ);
                }

                if (l.topStation != null)
                {
                    any = true;
                    ExpandBounds(l.topStation.position, ref minXZ, ref maxXZ);
                }
            }
        }

        // POIs
        if (poiList != null)
        {
            for (int i = 0; i < poiList.Count; i++)
            {
                var p = poiList[i];
                any = true;
                ExpandBounds(p.position, ref minXZ, ref maxXZ);
            }
        }

        if (!any) return false;

        if (!(maxXZ.x > minXZ.x && maxXZ.y > minXZ.y))
        {
            Vector2 c = 0.5f * (minXZ + maxXZ);
            minXZ = c - Vector2.one * 1f;
            maxXZ = c + Vector2.one * 1f;
        }

        return true;
    }

    private static void ExpandBounds(Vector3 world, ref Vector2 minXZ, ref Vector2 maxXZ)
    {
        Vector2 xz = new Vector2(world.x, world.z);
        minXZ.x = Mathf.Min(minXZ.x, xz.x);
        minXZ.y = Mathf.Min(minXZ.y, xz.y);
        maxXZ.x = Mathf.Max(maxXZ.x, xz.x);
        maxXZ.y = Mathf.Max(maxXZ.y, xz.y);
    }

    private static void AppendRunPolylines(SkiRunLine[] runs, List<MapPolyline> dst)
    {
        for (int i = 0; i < runs.Length; i++)
        {
            var r = runs[i];
            if (r == null) continue;

            var pts = r.PointsWorld;
            if (pts == null || pts.Count < 2) continue;

            var line = new MapPolyline
            {
                id = r.RunId,
                displayName = r.RunName,
                lineType = MapLineType.SkiRun,
                color = r.RunColor,
                widthMeters = Mathf.Max(0.01f, r.RunWidthMeters),
                pointsWorld = new List<Vector3>(pts.Count),
                pointsWorldXZ = new List<Vector2>(pts.Count)
            };

            for (int p = 0; p < pts.Count; p++)
            {
                line.pointsWorld.Add(pts[p]);
                line.pointsWorldXZ.Add(new Vector2(pts[p].x, pts[p].z));
            }

            dst.Add(line);
        }
    }

    private static void AppendLiftPolylines(LiftLine[] lifts, IReadOnlyList<POIInfo> poiList, List<MapPolyline> dst)
    {
        for (int i = 0; i < lifts.Length; i++)
        {
            var l = lifts[i];
            if (l == null) continue;

            string liftId = null;
            string displayName = l.gameObject.name;
            Color color = Color.cyan;

            if (poiList != null)
            {
                for (int p = 0; p < poiList.Count; p++)
                {
                    var poi = poiList[p];
                    if (poi.type != POIType.SkiLift) continue;
                    if (poi.source != l) continue;

                    liftId = ExtractLiftBaseId(poi.id);
                    color = poi.color;
                    displayName = StripStationSuffix(poi.displayName);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(liftId))
                liftId = "Lift__" + l.GetInstanceID();

            if (l.bottomStation == null || l.topStation == null)
                continue;

            var pts3 = new List<Vector3>(2)
{
    l.bottomStation.position,
    l.topStation.position
};

            var ptsXZ = new List<Vector2>(2)
{
    new Vector2(l.bottomStation.position.x, l.bottomStation.position.z),
    new Vector2(l.topStation.position.x, l.topStation.position.z)
};

            var line = new MapPolyline
            {
                id = liftId,
                displayName = displayName,
                lineType = MapLineType.SkiLift,
                color = color,
                widthMeters = 2f,
                pointsWorld = pts3,
                pointsWorldXZ = ptsXZ
            };

            dst.Add(line);
        }
    }

    private static void AppendMarkers(IReadOnlyList<POIInfo> poiList, List<MapMarker> dst)
    {
        if (poiList == null) return;

        for (int i = 0; i < poiList.Count; i++)
        {
            var p = poiList[i];
            if (!p.IsValid) continue;

            dst.Add(new MapMarker
            {
                id = p.id,
                displayName = p.displayName,
                type = p.type,
                color = p.color,
                meta = p.meta,
                worldPosition = p.position,
                source = p.source
            });
        }
    }

    // --------------------------
    // Background capture
    // --------------------------
    private static bool TryGetActiveTerrainBoundsXZ(out Vector2 minXZ, out Vector2 maxXZ)
    {
        var terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            minXZ = default;
            maxXZ = default;
            return false;
        }

        minXZ = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maxXZ = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        bool any = false;

        for (int i = 0; i < terrains.Length; i++)
        {
            var t = terrains[i];
            if (t == null || t.terrainData == null) continue;

            Vector3 pos = t.transform.position;
            Vector3 size = t.terrainData.size;

            Vector3 max = pos + new Vector3(size.x, 0f, size.z);

            minXZ.x = Mathf.Min(minXZ.x, pos.x);
            minXZ.y = Mathf.Min(minXZ.y, pos.z);
            maxXZ.x = Mathf.Max(maxXZ.x, max.x);
            maxXZ.y = Mathf.Max(maxXZ.y, max.z);

            any = true;
        }

        return any;
    }

    private Texture2D BakeBackgroundTexture(
        MapProjection projection,
        bool lockToProjectionBounds,
        bool allowTerrainExpansion,
        bool allowRotationForBackground,
        out Vector2 outInsetMin,
        out Vector2 outInsetMax,
        out bool wroteInset)
    {
        outInsetMin = Vector2.zero;
        outInsetMax = Vector2.one;
        wroteInset = false;

        EnsureOutputFolderExists();

        // Create temporary camera
        var go = new GameObject("__MapBakeCamera__");
        go.hideFlags = HideFlags.HideAndDontSave;

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = clearColor;
        cam.cullingMask = captureCullingMask;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = Mathf.Max(5000f, captureHeight + 5000f);
        cam.useOcclusionCulling = false;

        Vector2 projMin = projection.worldMinXZ;
        Vector2 projMax = projection.worldMaxXZ;

        // Compute capture bounds with guardrails
        Vector2 captureMin = projMin;
        Vector2 captureMax = projMax;
        Vector2 captureCenterXZ = 0.5f * (projMin + projMax);

        if (!lockToProjectionBounds && allowTerrainExpansion)
        {
            if (TryGetActiveTerrainBoundsXZ(out Vector2 terrainMin, out Vector2 terrainMax))
            {
                captureCenterXZ = 0.5f * (terrainMin + terrainMax);
                captureMin = new Vector2(Mathf.Min(projMin.x, terrainMin.x), Mathf.Min(projMin.y, terrainMin.y));
                captureMax = new Vector2(Mathf.Max(projMax.x, terrainMax.x), Mathf.Max(projMax.y, terrainMax.y));

                // If we expanded capture bounds beyond projection and we are NOT rotating the background,
                // we can write an inset rect that remaps overlays into the usable background area.
                // (This is exactly what prevents the "markers expect the map to be bigger" problem.)
                // NOTE: This inset is only valid for unrotated captures.
                outInsetMin = new Vector2(
                    (projMin.x - captureMin.x) / Mathf.Max(0.0001f, (captureMax.x - captureMin.x)),
                    (projMin.y - captureMin.y) / Mathf.Max(0.0001f, (captureMax.y - captureMin.y))
                );
                outInsetMax = new Vector2(
                    (projMax.x - captureMin.x) / Mathf.Max(0.0001f, (captureMax.x - captureMin.x)),
                    (projMax.y - captureMin.y) / Mathf.Max(0.0001f, (captureMax.y - captureMin.y))
                );
                wroteInset = true;
            }
        }

        // Background rotation policy:
        // - Safe mode: rotation is blocked
        // - Unsafe mode: allowed if explicitly unlocked
        float yaw = 0f;
        if (allowRotationForBackground && useRotation)
        {
            yaw = rotationYawDeg;
        }
        else if (useRotation)
        {
            Debug.LogWarning("[MapBake] Alignment Safety: background capture rotation was blocked. (Projection rotation remains baked into MapProjection.)");
        }

        // If we are rotating the background capture, the simple inset rect is not valid.
        if (Mathf.Abs(yaw) > 0.0001f)
        {
            if (wroteInset)
            {
                wroteInset = false;
                outInsetMin = Vector2.zero;
                outInsetMax = Vector2.one;
                Debug.LogWarning("[MapBake] Rotation is enabled for background capture; backgroundUvMin/Max inset was NOT written because a simple inset cannot correct rotated overscan.");
            }
        }

        // Position camera above the chosen center
        cam.transform.position = new Vector3(captureCenterXZ.x, captureHeight, captureCenterXZ.y);
        cam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        // Compute capture bounds extents
        float widthMeters = Mathf.Max(1f, (captureMax.x - captureMin.x));
        float heightMeters = Mathf.Max(1f, (captureMax.y - captureMin.y));

        // Aspect / RT sizing (keep textureSize as the long side)
        float aspect = widthMeters / Mathf.Max(0.0001f, heightMeters);
        int w = textureSize;
        int h = Mathf.Max(256, Mathf.RoundToInt(textureSize / Mathf.Max(0.0001f, aspect)));
        if (h > textureSize) { h = textureSize; w = Mathf.Max(256, Mathf.RoundToInt(textureSize * aspect)); }

        cam.aspect = (float)w / h;
        cam.orthographicSize = heightMeters * 0.5f;

        // Render
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.name = "__MapBakeRT__";
        cam.targetTexture = rt;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        cam.Render();

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true, linear: false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);

        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(go);

        // Save PNG
        string safeFile = string.IsNullOrWhiteSpace(backgroundFileName) ? "MapBackground.png" : backgroundFileName.Trim();
        if (!safeFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            safeFile += ".png";

        string pngPath = Path.Combine(outputFolder, safeFile).Replace("\\", "/");
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

        var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

        // Ensure importer settings are suitable for UI usage
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return imported != null ? imported : tex;
    }

    private void EnsureOutputFolderExists()
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
            outputFolder = "Assets/1 SKI GAME/Art/Map";

        if (!outputFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            outputFolder = "Assets/1 SKI GAME/Art/Map";

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string[] parts = outputFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    private static void SetBackgroundInsetSerialized(MapData mapData, Vector2 min, Vector2 max)
    {
        if (mapData == null) return;

        var so = new SerializedObject(mapData);

        var pMin = so.FindProperty("backgroundUvMin");
        var pMax = so.FindProperty("backgroundUvMax");

        if (pMin != null) pMin.vector2Value = min;
        if (pMax != null) pMax.vector2Value = max;

        so.ApplyModifiedProperties();
    }

    // --------------------------
    // Small string helpers
    // --------------------------

    private static string ExtractLiftBaseId(string poiId)
    {
        if (string.IsNullOrWhiteSpace(poiId)) return null;
        int idx = poiId.IndexOf("__", StringComparison.Ordinal);
        return idx > 0 ? poiId.Substring(0, idx) : poiId;
    }

    private static string StripStationSuffix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return displayName;
        int idx = displayName.LastIndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? displayName.Substring(0, idx) : displayName;
    }

    // --------------------------
    // LayerMask field helper
    // --------------------------

    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = InternalEditorUtility.layers;
        int maskWithoutEmpty = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layers[i]);
            if (((1 << layer) & selected.value) != 0)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                mask |= (1 << layer);
            }
        }

        selected.value = mask;
        return selected;
    }
}
#endif
