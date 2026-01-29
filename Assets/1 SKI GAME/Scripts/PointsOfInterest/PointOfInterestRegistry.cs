using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;

namespace SkiGame.POI
{
    public enum POIType
    {
        Unknown = 0,
        SkiRun = 10,
        SkiLift = 20,
        Custom = 90,
    }

    [Serializable]
    public struct POIInfo
    {
        public string id;
        public string displayName;
        public POIType type;
        public Vector3 position;
        public Color color;
        public string meta;
        public UnityEngine.Object source; // SkiRunLine / LiftLine / null (custom)

        public bool IsValid => !string.IsNullOrWhiteSpace(id);
    }

    [DisallowMultipleComponent]
    public sealed class PointOfInterestRegistry : MonoBehaviour
    {
        public static PointOfInterestRegistry Instance { get; private set; }

        /// <summary>
        /// UI systems should subscribe to this and rebuild their list/markers when fired.
        /// This is intentionally coarse-grained to stay lightweight.
        /// </summary>
        public event Action<IReadOnlyList<POIInfo>> OnChanged;

        [Header("Discovery")]
        [SerializeField] private bool includeSkiRuns = true;
        [SerializeField] private bool includeLiftLines = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;

        [SerializeField, Tooltip("World-space size of POI marker gizmos.")]
        private float gizmoSize = 2.0f;

        [SerializeField, Tooltip("If enabled, POI gizmos are drawn even when the registry is not selected.")]
        private bool drawGizmosWhenNotSelected = true;

        [SerializeField, Tooltip("If enabled, POI labels are drawn when the registry is selected (Editor only).")]
        private bool drawLabelsWhenSelected = true;

#if UNITY_EDITOR
        [NonSerialized] private double _lastEditorRefreshTime;
#endif

        [Header("Custom POIs (Stored)")]
        [SerializeField] private List<CustomPOIEntry> customPoints = new();

        [Header("Lift POI Identity (Stored)")]
        [SerializeField] private List<LiftPOIEntry> liftEntries = new();

        // Cached, rebuilt on Refresh()
        [NonSerialized] private readonly List<POIInfo> _cache = new();
        public IReadOnlyList<POIInfo> Current => _cache;

        [Header("Editor Refresh (Performance)")]
        [SerializeField] private bool autoRefreshInEditor = true;

        [SerializeField, Range(0.25f, 10f)]
        private float editorAutoRefreshIntervalSeconds = 2.0f;

        [SerializeField] private bool bakeRunMetricsInEditor = false;

        [SerializeField, Range(0.5f, 30f)]
        private float runMetricsBakeCooldownSeconds = 5.0f;

#if UNITY_EDITOR
        private double _nextEditorRefreshTime;
        private readonly System.Collections.Generic.Dictionary<int, double> _lastRunBakeTime = new();
#endif


        [Serializable]
        private class CustomPOIEntry
        {
            public string id;
            public string name;
            public Vector3 position;
            public Color color = Color.yellow;
            [TextArea] public string meta;
        }

        [Serializable]
        private class LiftPOIEntry
        {
            public LiftLine lift;
            public string id;
            public string nameOverride;
            public Color color = Color.cyan;
            [TextArea] public string metaOverride;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>
        /// Rebuild the POI cache and notify listeners.
        /// Call this after edits (editor) or when scene objects change (runtime).
        /// </summary>
        public void Refresh()
        {
            _cache.Clear();

            if (includeSkiRuns)
                AppendSkiRuns(_cache);

            if (includeLiftLines)
                AppendLiftLines(_cache);

            AppendCustom(_cache);

            OnChanged?.Invoke(_cache);
        }

        // -----------------------
        // Public API for custom POIs (used by editor)
        // -----------------------

        public POIInfo AddCustom(string displayName, Vector3 worldPos, Color color, string meta = "")
        {
            var entry = new CustomPOIEntry
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(displayName) ? "POI" : displayName.Trim(),
                position = worldPos,
                color = color,
                meta = meta ?? ""
            };
            customPoints.Add(entry);

            var info = new POIInfo
            {
                id = entry.id,
                displayName = entry.name,
                type = POIType.Custom,
                position = entry.position,
                color = entry.color,
                meta = entry.meta,
                source = null
            };

            Refresh();
            return info;
        }

        public bool RemoveCustomById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            for (int i = 0; i < customPoints.Count; i++)
            {
                if (customPoints[i] != null && customPoints[i].id == id)
                {
                    customPoints.RemoveAt(i);
                    Refresh();
                    return true;
                }
            }
            return false;
        }

        public bool TryMoveCustom(string id, Vector3 newWorldPos)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            for (int i = 0; i < customPoints.Count; i++)
            {
                var e = customPoints[i];
                if (e != null && e.id == id)
                {
                    e.position = newWorldPos;
                    Refresh();
                    return true;
                }
            }
            return false;
        }

        // -----------------------
        // Queries (UI / gameplay)
        // -----------------------

        public bool TryGetById(string id, out POIInfo info)
        {
            info = default;
            if (string.IsNullOrWhiteSpace(id)) return false;

            for (int i = 0; i < _cache.Count; i++)
            {
                if (_cache[i].id == id)
                {
                    info = _cache[i];
                    return true;
                }
            }
            return false;
        }

        public bool TryFindNearest(Vector3 worldPos, float maxDistance, out POIInfo nearest)
        {
            nearest = default;
            float bestSqr = maxDistance * maxDistance;
            bool got = false;

            for (int i = 0; i < _cache.Count; i++)
            {
                var p = _cache[i];
                float d2 = (p.position - worldPos).sqrMagnitude;
                if (d2 <= bestSqr)
                {
                    bestSqr = d2;
                    nearest = p;
                    got = true;
                }
            }

            return got;
        }

        // -----------------------
        // Discovery implementations (no adapters)
        // -----------------------

        private void AppendSkiRuns(List<POIInfo> dst)
        {
#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
    var runs = UnityEngine.Object.FindObjectsOfType<SkiRunLine>(true);
#endif

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;

#if UNITY_EDITOR
                if (bakeRunMetricsInEditor && !Application.isPlaying)
                {
                    int key = r.GetInstanceID();
                    double now = UnityEditor.EditorApplication.timeSinceStartup;

                    if (!_lastRunBakeTime.TryGetValue(key, out double last) || (now - last) >= runMetricsBakeCooldownSeconds)
                    {
                        _lastRunBakeTime[key] = now;
                        try { r.BakeMetrics(); } catch { /* keep POI system resilient */ }
                    }
                }
#endif

                Vector3 pos = r.transform.position;

                // "Top of run" = highest elevation authored point, if present.
                var pts = r.PointsWorld;
                if (pts != null && pts.Count > 0)
                {
                    int best = 0;
                    float bestY = pts[0].y;
                    for (int p = 1; p < pts.Count; p++)
                    {
                        if (pts[p].y > bestY)
                        {
                            bestY = pts[p].y;
                            best = p;
                        }
                    }
                    pos = pts[best];
                }

                // Marker ID is stable and explicit.
                string id = $"{r.RunId}__top";

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = r.RunName,
                    type = POIType.SkiRun,
                    position = pos,
                    color = r.RunColor,                 // run difficulty color (already computed by BakeMetrics)
                    meta = r.Difficulty.ToString(),
                    source = r
                });
            }
        }

        private void AppendLiftLines(List<POIInfo> dst)
        {
#if UNITY_2023_1_OR_NEWER
            var lifts = UnityEngine.Object.FindObjectsByType<LiftLine>(FindObjectsSortMode.None);
#else
    var lifts = UnityEngine.Object.FindObjectsOfType<LiftLine>(true);
#endif

            // Ensure we have entries (stable IDs) for all lifts in scene.
            for (int i = 0; i < lifts.Length; i++)
            {
                var lift = lifts[i];
                if (lift == null) continue;

                var entry = GetOrCreateLiftEntry(lift);

                string baseName = string.IsNullOrWhiteSpace(entry.nameOverride)
                    ? lift.gameObject.name
                    : entry.nameOverride.Trim();

                string metaBase = string.IsNullOrWhiteSpace(entry.metaOverride)
                    ? $"Carriers: {lift.carrierCount}, Speed: {lift.bandSpeed:0.##}"
                    : entry.metaOverride;

                // Bottom station marker
                if (lift.bottomStation != null)
                {
                    dst.Add(new POIInfo
                    {
                        id = $"{entry.id}__bottom",
                        displayName = $"{baseName} (Bottom)",
                        type = POIType.SkiLift,
                        position = lift.bottomStation.position,
                        color = entry.color,
                        meta = metaBase,
                        source = lift
                    });
                }

                // Top station marker
                if (lift.topStation != null)
                {
                    dst.Add(new POIInfo
                    {
                        id = $"{entry.id}__top",
                        displayName = $"{baseName} (Top)",
                        type = POIType.SkiLift,
                        position = lift.topStation.position,
                        color = entry.color,
                        meta = metaBase,
                        source = lift
                    });
                }

                // Fallback: if stations are missing, at least provide one marker at lift transform.
                if (lift.bottomStation == null && lift.topStation == null)
                {
                    dst.Add(new POIInfo
                    {
                        id = $"{entry.id}__mid",
                        displayName = baseName,
                        type = POIType.SkiLift,
                        position = lift.transform.position,
                        color = entry.color,
                        meta = metaBase,
                        source = lift
                    });
                }
            }

            // Prune deleted lift refs from the stored list.
            for (int i = liftEntries.Count - 1; i >= 0; i--)
            {
                if (liftEntries[i] == null || liftEntries[i].lift == null)
                    liftEntries.RemoveAt(i);
            }
        }

        private LiftPOIEntry GetOrCreateLiftEntry(LiftLine lift)
        {
            for (int i = 0; i < liftEntries.Count; i++)
            {
                var e = liftEntries[i];
                if (e != null && e.lift == lift)
                {
                    if (string.IsNullOrWhiteSpace(e.id))
                        e.id = Guid.NewGuid().ToString("N");
                    return e;
                }
            }

            var created = new LiftPOIEntry
            {
                lift = lift,
                id = Guid.NewGuid().ToString("N"),
                nameOverride = "",
                color = Color.cyan,
                metaOverride = ""
            };

            liftEntries.Add(created);
            return created;
        }

        private void AppendCustom(List<POIInfo> dst)
        {
            for (int i = 0; i < customPoints.Count; i++)
            {
                var c = customPoints[i];
                if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;

                dst.Add(new POIInfo
                {
                    id = c.id,
                    displayName = string.IsNullOrWhiteSpace(c.name) ? "POI" : c.name,
                    type = POIType.Custom,
                    position = c.position,
                    color = c.color,
                    meta = c.meta,
                    source = null
                });
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!drawGizmosWhenNotSelected) return;

#if UNITY_EDITOR
            // Keep cache reasonably fresh in the editor for correct gizmos without manual refresh spam.

            if (!Application.isPlaying && autoRefreshInEditor)
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (now >= _nextEditorRefreshTime)
                {
                    _nextEditorRefreshTime = now + editorAutoRefreshIntervalSeconds;
                    Refresh();
                }
            }
#endif

            DrawPOIGizmos(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

#if UNITY_EDITOR
            if (!Application.isPlaying && autoRefreshInEditor)
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (now >= _nextEditorRefreshTime)
                {
                    _nextEditorRefreshTime = now + editorAutoRefreshIntervalSeconds;
                    Refresh();
                }
            }
#endif

            DrawPOIGizmos(selected: true);
        }

        private void DrawPOIGizmos(bool selected)
        {
            Gizmos.matrix = Matrix4x4.identity;

            // Draw all POIs from cache (runs, lifts, custom).
            // If cache is empty (e.g., newly added component), try to draw after a refresh.
            if (_cache.Count == 0)
                Refresh();

            float solid = Mathf.Max(0.05f, gizmoSize * 0.15f);
            float wire = Mathf.Max(0.1f, gizmoSize * 0.25f);

            for (int i = 0; i < _cache.Count; i++)
            {
                var poi = _cache[i];
                if (!poi.IsValid) continue;

                Color c = poi.color;
                if (!selected)
                    c.a = Mathf.Clamp01(c.a * 0.6f);

                Gizmos.color = c;
                Gizmos.DrawSphere(poi.position, solid);
                Gizmos.DrawWireSphere(poi.position, wire);

#if UNITY_EDITOR
                if (selected && drawLabelsWhenSelected)
                {
                    UnityEditor.Handles.color = Color.white;
                    UnityEditor.Handles.Label(poi.position + Vector3.up * (wire + 0.5f), $"{poi.type}: {poi.displayName}");
                }
#endif
            }
        }

    }
}
