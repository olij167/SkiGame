using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.POI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SkiGame.Map
{
    public enum MapLineType
    {
        Unknown = 0,
        SkiRun = 10,
        SkiLift = 20
    }

    [Serializable]
    public struct MapPolyline
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public MapLineType lineType;

        [Header("Visuals")]
        public Color color;

        [Tooltip("Optional: width in meters (authoring metadata). Rendering width is decided by UI.")]
        public float widthMeters;

        [Header("Geometry (World XZ)")]
        [Tooltip("World-space polyline points, projected onto XZ (Vector2.x = worldX, Vector2.y = worldZ).")]
        public List<Vector2> pointsWorldXZ;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(id) &&
            pointsWorldXZ != null &&
            pointsWorldXZ.Count >= 2;
    }

    [Serializable]
    public struct MapMarker
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public POIType type;

        [Header("Visuals")]
        public Color color;

        [TextArea]
        public string meta;

        [Header("Position (World)")]
        public Vector3 worldPosition;

        [Tooltip("Optional: Unity Object backing this marker (SkiRunLine, LiftLine, etc). Not required for baked maps.")]
        public UnityEngine.Object source;

        public bool IsValid => !string.IsNullOrWhiteSpace(id);
    }

    /// <summary>
    /// Baked map data: projection + background + vector overlays (runs/lifts) + markers (POIs).
    /// Runtime UI consumes this without scanning the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "SkiGame/Map/Map Data", fileName = "MapData")]
    public sealed class MapData : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [Header("Versioning")]
        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        public int SchemaVersion => schemaVersion;

        [Header("Projection")]
        [SerializeField] private MapProjection projection;
        public MapProjection Projection => projection;

        [SerializeField] private bool preferCameraProjection = false;
        public bool PreferCameraProjection => preferCameraProjection;

        [Header("Background (Optional)")]
        [Tooltip("Baked orthographic snapshot or stylized map texture.")]
        [SerializeField] private Texture2D backgroundTexture;
        public Texture2D BackgroundTexture => backgroundTexture;

        [Header("Background Mapping (Optional)")]
        [Tooltip("If the baked background texture includes empty borders, set the usable UV rect here.\n" +
         "Markers/lines will be remapped into this rect so overlays align with the image.\n" +
         "Default (0,0)-(1,1) = no inset.")]
        [SerializeField] private Vector2 backgroundUvMin = Vector2.zero;

        [SerializeField] private Vector2 backgroundUvMax = Vector2.one;

        public Vector2 BackgroundUvMin => backgroundUvMin;
        public Vector2 BackgroundUvMax => backgroundUvMax;

        [Header("Vector Layers")]
        [SerializeField] private List<MapPolyline> polylines = new();
        public IReadOnlyList<MapPolyline> Polylines => polylines;

        [Header("Markers")]
        [SerializeField] private List<MapMarker> markers = new();
        public IReadOnlyList<MapMarker> Markers => markers;

        // -------------------------
        // Authoring helpers (used by bake tools; safe at runtime too)
        // -------------------------

        public void SetProjection(MapProjection p) => projection = p;
        public void SetBackground(Texture2D tex) => backgroundTexture = tex;

        public void ClearAll()
        {
            polylines.Clear();
            markers.Clear();
        }

        public void SetPolylines(List<MapPolyline> lines)
        {
            polylines = (lines != null) ? lines : new List<MapPolyline>();
        }

        public void SetMarkers(List<MapMarker> list)
        {
            markers = (list != null) ? list : new List<MapMarker>();
        }

        /// <summary>
        /// Projects a world-space XZ point to normalized map UV.
        /// </summary>
        public Vector2 WorldToMapUV(Vector3 worldPos) => projection.WorldToNormalized(worldPos);

        /// <summary>
        /// Projects a world-space XZ point (packed in Vector2) to normalized UV.
        /// </summary>
        public Vector2 WorldXZToMapUV(Vector2 worldXZ)
        {
            return projection.WorldToNormalized(new Vector3(worldXZ.x, 0f, worldXZ.y));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep schema in sync for now; future migrations can be added later.
            if (schemaVersion <= 0)
                schemaVersion = CurrentSchemaVersion;
        }
#endif


#if UNITY_EDITOR
        [ContextMenu("Map/Compute Background UV Inset From Texture (Corner Color)")]
    private void ComputeBackgroundUvInset_FromCornerColor()
    {
        if (backgroundTexture == null)
        {
            Debug.LogWarning("[MapData] No backgroundTexture assigned.");
            return;
        }

        if (!backgroundTexture.isReadable)
        {
            Debug.LogWarning(
                "[MapData] backgroundTexture is not readable. Enable Read/Write in the texture import settings.");
            return;
        }

        var tex = backgroundTexture;
        int w = tex.width;
        int h = tex.height;
        var px = tex.GetPixels32();

        // Treat pixels "similar to the top-left corner" as border.
        Color32 corner = px[(h - 1) * w + 0];

        static bool Similar(Color32 a, Color32 b, int tol)
        {
            int dr = Mathf.Abs(a.r - b.r);
            int dg = Mathf.Abs(a.g - b.g);
            int db = Mathf.Abs(a.b - b.b);
            int da = Mathf.Abs(a.a - b.a);
            return (dr + dg + db + da) <= tol;
        }

        const int tolerance = 18;          // tighten/loosen if needed
        const float keepRatio = 0.90f;     // row/col considered "border" if >=90% similar

        bool RowIsBorder(int y)
        {
            int similar = 0;
            int idx = y * w;
            for (int x = 0; x < w; x++)
                if (Similar(px[idx + x], corner, tolerance)) similar++;
            return (similar / (float)w) >= keepRatio;
        }

        bool ColIsBorder(int x)
        {
            int similar = 0;
            for (int y = 0; y < h; y++)
                if (Similar(px[y * w + x], corner, tolerance)) similar++;
            return (similar / (float)h) >= keepRatio;
        }

        int minY = 0;
        while (minY < h - 1 && RowIsBorder(minY)) minY++;

        int maxY = h - 1;
        while (maxY > 0 && RowIsBorder(maxY)) maxY--;

        int minX = 0;
        while (minX < w - 1 && ColIsBorder(minX)) minX++;

        int maxX = w - 1;
        while (maxX > 0 && ColIsBorder(maxX)) maxX--;

        if (maxX <= minX || maxY <= minY)
        {
            Debug.LogWarning("[MapData] Failed to detect inset (content bounds collapsed).");
            return;
        }

        Undo.RecordObject(this, "Compute Map Background UV Inset");

        backgroundUvMin = new Vector2(minX / (float)w, minY / (float)h);
        backgroundUvMax = new Vector2((maxX + 1) / (float)w, (maxY + 1) / (float)h);

        EditorUtility.SetDirty(this);

        Debug.Log($"[MapData] Computed BackgroundUvMin={backgroundUvMin:F4} BackgroundUvMax={backgroundUvMax:F4} " +
                  $"(px rect: x[{minX},{maxX}] y[{minY},{maxY}] of {w}x{h})");
    }

    [ContextMenu("Map/Reset Background UV Inset")]
    private void ResetBackgroundUvInset()
    {
        Undo.RecordObject(this, "Reset Map Background UV Inset");
        backgroundUvMin = Vector2.zero;
        backgroundUvMax = Vector2.one;
        EditorUtility.SetDirty(this);
        Debug.Log("[MapData] Reset BackgroundUvMin/Max to (0,0)-(1,1).");
    }
#endif

}
}
