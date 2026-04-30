using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;
using SkiGame.Map.UI;

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
        public string displayName;
        public string id;
        public POIType type;
        public POICategory category;
        public bool hasLabelDisplayOverride;
        public MapUIStyleSettings.MapLabelDisplayMode labelDisplayOverride;
        public Vector3 position;
        public Color color;
        public Sprite markerSprite;
        public float markerSizeMultiplier;
        public string meta;
        public UnityEngine.Object source; // SkiRunLine / LiftLine / MapPOIAnchor / null

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
        [SerializeField] private bool includeWorldPOIs = true;

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

        [Header("Run POI Identity (Stored)")]
        [SerializeField] private List<RunPOIEntry> runEntries = new();

        [Header("Lift POI Identity (Stored)")]
        [SerializeField] private List<LiftPOIEntry> liftEntries = new();

       
        // Cached, rebuilt on Refresh()
        [NonSerialized] private readonly List<POIInfo> _cache = new();
        [NonSerialized] private readonly HashSet<int> _anchoredInstanceIds = new();
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

        private const string ActivityRaceMetaToken = "activity:race";
        private const string ActivityMedicMetaToken = "activity:medic";
        private const string ActivitySnowmobileMetaToken = "activity:snowmobile";

        [Serializable]
        private class CustomPOIEntry
        {
            public string id;
            public string name;
            public bool overrideLabelDisplayMode;
            public MapUIStyleSettings.MapLabelDisplayMode labelDisplayModeOverride = MapUIStyleSettings.MapLabelDisplayMode.Contextual;
            [HideInInspector] public bool alwaysShowLabel;

            public Vector3 position;
            public Color color = Color.yellow;
            public Sprite markerSprite;
            [Min(0.1f)] public float markerSizeMultiplier = 1f;
            [TextArea] public string meta;
        }

        [Serializable]
        private class RunPOIEntry
        {
            public SkiRunLine run;
            public Sprite markerSprite;
            [Min(0.1f)] public float markerSizeMultiplier = 1f;
        }

        [Serializable]
        private class LiftPOIEntry
        {
            public LiftLine lift;
            public string id;
            public string nameOverride;
            public Color color = Color.cyan;
            public Sprite markerSprite;
            [Min(0.1f)] public float markerSizeMultiplier = 1f;
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
            UpgradeSerializedDataIfNeeded();
            Refresh();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpgradeSerializedDataIfNeeded();
        }
#endif

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

            if (includeWorldPOIs)
            {
                var anchoredInstanceIds = AppendWorldPOIs(_cache);
                AppendActivityPOIs(_cache, anchoredInstanceIds);
            }

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
                category = POICategory.Custom,
                hasLabelDisplayOverride = entry.overrideLabelDisplayMode,
                labelDisplayOverride = entry.labelDisplayModeOverride,
                position = entry.position,
                color = entry.color,
                markerSprite = ResolveMarkerSprite(entry.id, POIType.Custom, POICategory.Custom, entry.markerSprite),
                markerSizeMultiplier = ResolveMarkerSizeMultiplier(entry.id, POIType.Custom, POICategory.Custom, entry.markerSizeMultiplier),
                meta = POIMetaUtility.EnsureToken(entry.meta, POIMetaUtility.BuildCategoryToken(POICategory.Custom)),
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
                string id = BuildRunPoiId(r);
                var entry = GetOrCreateRunEntry(r);

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = r.RunName,
                    type = POIType.SkiRun,
                    category = POICategory.None,
                    position = pos,
                    color = r.RunColor,
                    markerSprite = ResolveMarkerSprite(id, POIType.SkiRun, POICategory.None, entry.markerSprite),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(id, POIType.SkiRun, POICategory.None, entry.markerSizeMultiplier),
                    meta = r.Difficulty.ToString(),
                    source = r
                });
            }

            for (int i = runEntries.Count - 1; i >= 0; i--)
            {
                if (runEntries[i] == null || runEntries[i].run == null)
                    runEntries.RemoveAt(i);
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
                        category = POICategory.None,
                        position = lift.bottomStation.position,
                        color = entry.color,
                        markerSprite = ResolveMarkerSprite($"{entry.id}__bottom", POIType.SkiLift, POICategory.None, entry.markerSprite),
                        markerSizeMultiplier = ResolveMarkerSizeMultiplier($"{entry.id}__bottom", POIType.SkiLift, POICategory.None, entry.markerSizeMultiplier),
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
                        category = POICategory.None,
                        position = lift.topStation.position,
                        color = entry.color,
                        markerSprite = ResolveMarkerSprite($"{entry.id}__top", POIType.SkiLift, POICategory.None, entry.markerSprite),
                        markerSizeMultiplier = ResolveMarkerSizeMultiplier($"{entry.id}__top", POIType.SkiLift, POICategory.None, entry.markerSizeMultiplier),
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
                        category = POICategory.None,
                        position = lift.transform.position,
                        color = entry.color,
                        markerSprite = ResolveMarkerSprite($"{entry.id}__mid", POIType.SkiLift, POICategory.None, entry.markerSprite),
                        markerSizeMultiplier = ResolveMarkerSizeMultiplier($"{entry.id}__mid", POIType.SkiLift, POICategory.None, entry.markerSizeMultiplier),
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

        private RunPOIEntry GetOrCreateRunEntry(SkiRunLine run)
        {
            for (int i = 0; i < runEntries.Count; i++)
            {
                var e = runEntries[i];
                if (e != null && e.run == run)
                    return e;
            }

            var created = new RunPOIEntry
            {
                run = run,
                markerSprite = null,
                markerSizeMultiplier = 1f
            };

            runEntries.Add(created);
            return created;
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
                markerSprite = null,
                markerSizeMultiplier = 1f,
                metaOverride = ""
            };

            liftEntries.Add(created);
            return created;
        }

        private static bool HasRelatedComponent<T>(GameObject go) where T : Component
        {
            if (go == null)
                return false;

            return go.GetComponent<T>() != null ||
                   go.GetComponentInParent<T>() != null ||
                   go.GetComponentInChildren<T>(true) != null;
        }

        private static POICategory ResolveEffectiveAnchorCategory(MapPOIAnchor anchor)
        {
            if (anchor == null)
                return POICategory.Custom;

            POICategory authoredCategory = anchor.Category;
            if (authoredCategory != POICategory.None && authoredCategory != POICategory.Custom)
                return authoredCategory;

            GameObject go = anchor.gameObject;
            if (HasRelatedComponent<SkiPassKiosk>(go) || HasRelatedComponent<RaceKiosk>(go))
                return POICategory.Kiosk;

            if (HasRelatedComponent<SkiResortZone>(go))
                return POICategory.Resort;

            if (HasRelatedComponent<CustomizationPortal>(go))
                return POICategory.Shop;

            if (HasRelatedComponent<RaceCourseLine>(go))
                return POICategory.Race;

            if (HasRelatedComponent<MedicTentActivityHub>(go))
                return POICategory.Medical;

            if (HasRelatedComponent<SnowmobileController>(go))
                return POICategory.Vehicle;

            return authoredCategory == POICategory.None ? POICategory.Custom : authoredCategory;
        }

        private HashSet<int> AppendWorldPOIs(List<POIInfo> dst)
        {
            _anchoredInstanceIds.Clear();

#if UNITY_2023_1_OR_NEWER
            var anchors = UnityEngine.Object.FindObjectsByType<MapPOIAnchor>(FindObjectsSortMode.None);
            var resorts = UnityEngine.Object.FindObjectsByType<SkiResortZone>(FindObjectsSortMode.None);
            var portals = UnityEngine.Object.FindObjectsByType<CustomizationPortal>(FindObjectsSortMode.None);
            var kiosks = UnityEngine.Object.FindObjectsByType<SkiPassKiosk>(FindObjectsSortMode.None);
            var raceKiosks = UnityEngine.Object.FindObjectsByType<RaceKiosk>(FindObjectsSortMode.None);
#else
    var anchors = UnityEngine.Object.FindObjectsOfType<MapPOIAnchor>(true);
    var resorts = UnityEngine.Object.FindObjectsOfType<SkiResortZone>(true);
    var portals = UnityEngine.Object.FindObjectsOfType<CustomizationPortal>(true);
    var kiosks = UnityEngine.Object.FindObjectsOfType<SkiPassKiosk>(true);
    var raceKiosks = UnityEngine.Object.FindObjectsOfType<RaceKiosk>(true);
#endif

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !anchor.IncludeInMapBake)
                    continue;

                _anchoredInstanceIds.Add(anchor.gameObject.GetInstanceID());

                string id = !string.IsNullOrWhiteSpace(anchor.CustomId)
                    ? anchor.CustomId.Trim()
                    : BuildStableScenePoiId("anchor", anchor.gameObject);

                POICategory resolvedCategory = ResolveEffectiveAnchorCategory(anchor);

                string meta = anchor.Meta;
                meta = POIMetaUtility.EnsureToken(meta, POIMetaUtility.BuildCategoryToken(resolvedCategory));

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = anchor.DisplayName,
                    type = POIType.Custom,
                    category = resolvedCategory,
                    hasLabelDisplayOverride = anchor.OverrideLabelDisplayMode,
                    labelDisplayOverride = anchor.LabelDisplayModeOverride,
                    position = anchor.WorldPosition,
                    color = anchor.Color,
                    markerSprite = ResolveMarkerSprite(id, POIType.Custom, resolvedCategory, anchor.MarkerSprite),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(id, POIType.Custom, resolvedCategory, anchor.MarkerSizeMultiplier),
                    meta = meta,
                    source = anchor
                });
            }

            AppendAutoWorldPOIs(
                resorts,
                _anchoredInstanceIds,
                dst,
                POICategory.Resort,
                new Color(0.25f, 0.75f, 1f, 1f),
                "Resort",
                GetResortWorldPosition);

            AppendAutoWorldPOIs(
                portals,
                _anchoredInstanceIds,
                dst,
                POICategory.Shop,
                new Color(1f, 0.45f, 0.75f, 1f),
                "Shop",
                c => c != null ? c.transform.position : Vector3.zero);

            AppendAutoWorldPOIs(
                kiosks,
                _anchoredInstanceIds,
                dst,
                POICategory.Kiosk,
                new Color(1f, 0.75f, 0.2f, 1f),
                "Ski Pass Kiosk",
                c => c != null ? c.transform.position : Vector3.zero);

            AppendAutoWorldPOIs(
                raceKiosks,
                _anchoredInstanceIds,
                dst,
                POICategory.Kiosk,
                new Color(0.86f, 0.35f, 1.00f, 1f),
                "Race Kiosk",
                c => c != null ? c.transform.position : Vector3.zero);

            return _anchoredInstanceIds;
        }

        private void AppendActivityPOIs(List<POIInfo> dst, HashSet<int> anchoredInstanceIds)
        {
#if UNITY_2023_1_OR_NEWER
            var races = UnityEngine.Object.FindObjectsByType<RaceCourseLine>(FindObjectsSortMode.None);
            var tents = UnityEngine.Object.FindObjectsByType<MedicTentActivityHub>(FindObjectsSortMode.None);
            var snowmobiles = UnityEngine.Object.FindObjectsByType<SnowmobileController>(FindObjectsSortMode.None);
#else
    var races = UnityEngine.Object.FindObjectsOfType<RaceCourseLine>(true);
    var tents = UnityEngine.Object.FindObjectsOfType<MedicTentActivityHub>(true);
    var snowmobiles = UnityEngine.Object.FindObjectsOfType<SnowmobileController>(true);
#endif

            for (int i = 0; i < races.Length; i++)
            {
                var race = races[i];
                if (race == null)
                    continue;

                if (anchoredInstanceIds != null && anchoredInstanceIds.Contains(race.gameObject.GetInstanceID()))
                    continue;

                string id = !string.IsNullOrWhiteSpace(race.RaceId)
                    ? $"activity-race:{race.RaceId}"
                    : BuildStableScenePoiId("activity-race", race.gameObject);

                string meta = POIMetaUtility.EnsureToken(string.Empty, ActivityRaceMetaToken);

                Color fallback = new Color(1.00f, 0.55f, 0.20f, 1f);
                Color resolvedColor = race.GetResolvedMapLineColor(fallback);

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = string.IsNullOrWhiteSpace(race.RaceName) ? race.name : race.RaceName,
                    type = POIType.Custom,
                    category = POICategory.Race,
                    position = race.StartWorldPosition,
                    color = resolvedColor,
                    markerSprite = ResolveMarkerSprite(id, POIType.Custom, POICategory.Race),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(id, POIType.Custom, POICategory.Race),
                    meta = meta,
                    source = race
                });
            }

            for (int i = 0; i < tents.Length; i++)
            {
                var tent = tents[i];
                if (tent == null)
                    continue;

                if (anchoredInstanceIds != null && anchoredInstanceIds.Contains(tent.gameObject.GetInstanceID()))
                    continue;

                string id = !string.IsNullOrWhiteSpace(tent.TentId)
                    ? $"activity-medic:{tent.TentId}"
                    : BuildStableScenePoiId("activity-medic", tent.gameObject);

                string meta = POIMetaUtility.EnsureToken(string.Empty, ActivityMedicMetaToken);

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = tent.TentName,
                    type = POIType.Custom,
                    category = POICategory.Medical,
                    position = tent.transform.position,
                    color = new Color(0.20f, 1.00f, 1.00f, 1f),
                    markerSprite = ResolveMarkerSprite(id, POIType.Custom, POICategory.Medical),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(id, POIType.Custom, POICategory.Medical),
                    meta = meta,
                    source = tent
                });
            }

            for (int i = 0; i < snowmobiles.Length; i++)
            {
                var snowmobile = snowmobiles[i];
                if (snowmobile == null)
                    continue;

                if (anchoredInstanceIds != null && anchoredInstanceIds.Contains(snowmobile.gameObject.GetInstanceID()))
                    continue;

                string id = BuildStableScenePoiId("activity-snowmobile", snowmobile.gameObject);
                string meta = POIMetaUtility.EnsureToken(string.Empty, ActivitySnowmobileMetaToken);

                dst.Add(new POIInfo
                {
                    id = id,
                    displayName = "Snowmobile",
                    type = POIType.Custom,
                    category = POICategory.Vehicle,
                    position = snowmobile.transform.position,
                    color = new Color(1.00f, 0.90f, 0.25f, 1f),
                    markerSprite = ResolveMarkerSprite(id, POIType.Custom, POICategory.Vehicle),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(id, POIType.Custom, POICategory.Vehicle),
                    meta = meta,
                    source = snowmobile
                });
            }
        }

        private void AppendAutoWorldPOIs<T>(
    T[] objects,
    HashSet<int> anchoredInstanceIds,
    List<POIInfo> dst,
    POICategory category,
    Color color,
    string fallbackName,
    Func<T, Vector3> worldPosResolver) where T : Component
        {
            if (objects == null || dst == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj == null)
                    continue;

                int instanceId = obj.gameObject.GetInstanceID();
                if (anchoredInstanceIds != null && anchoredInstanceIds.Contains(instanceId))
                    continue;

                string stableId = BuildStableScenePoiId($"auto-{category.ToString().ToLowerInvariant()}", obj.gameObject);

                string displayName = ResolveDisplayName(obj.gameObject, fallbackName);
                string meta = POIMetaUtility.BuildCategoryToken(category);

                dst.Add(new POIInfo
                {
                    id = stableId,
                    displayName = displayName,
                    type = POIType.Custom,
                    category = category,
                    position = worldPosResolver(obj),
                    color = color,
                    markerSprite = ResolveMarkerSprite(stableId, POIType.Custom, category),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(stableId, POIType.Custom, category),
                    meta = meta,
                    source = obj
                });
            }
        }

        private static string ResolveDisplayName(GameObject go, string fallbackName)
        {
            if (go == null)
                return fallbackName;

            string n = go.name;
            if (string.IsNullOrWhiteSpace(n))
                return fallbackName;

            n = n.Replace("_", " ").Trim();
            return string.IsNullOrWhiteSpace(n) ? fallbackName : n;
        }

        private static Vector3 GetResortWorldPosition(SkiResortZone zone)
        {
            if (zone == null)
                return Vector3.zero;

            if (zone.insidePoint != null)
                return zone.insidePoint.position;

            if (zone.entrancePoint != null)
                return zone.entrancePoint.position;

            return zone.transform.position;
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
                    category = POICategory.Custom,
                    hasLabelDisplayOverride = c.overrideLabelDisplayMode,
                    labelDisplayOverride = c.labelDisplayModeOverride,
                    position = c.position,
                    color = c.color,
                    markerSprite = ResolveMarkerSprite(c.id, POIType.Custom, POICategory.Custom, c.markerSprite),
                    markerSizeMultiplier = ResolveMarkerSizeMultiplier(c.id, POIType.Custom, POICategory.Custom, c.markerSizeMultiplier),
                    meta = POIMetaUtility.EnsureToken(c.meta, POIMetaUtility.BuildCategoryToken(POICategory.Custom)),
                    source = null
                });
            }
        }

        private void UpgradeSerializedDataIfNeeded()
        {
            if (customPoints == null)
                return;

            for (int i = 0; i < customPoints.Count; i++)
            {
                CustomPOIEntry entry = customPoints[i];
                if (entry == null)
                    continue;

                if (entry.alwaysShowLabel && !entry.overrideLabelDisplayMode)
                {
                    entry.overrideLabelDisplayMode = true;
                    entry.labelDisplayModeOverride = MapUIStyleSettings.MapLabelDisplayMode.Always;
                }
            }
        }

        private static string BuildRunPoiId(SkiRunLine run)
        {
            if (run == null)
                return null;

            if (!string.IsNullOrWhiteSpace(run.RunId))
                return $"{run.RunId}__top";

            return $"{BuildStableScenePoiId("run", run.gameObject)}__top";
        }

        private Sprite ResolveMarkerSprite(string poiId, POIType type, POICategory category, Sprite explicitSprite = null)
        {
            return explicitSprite;
        }

        private float ResolveMarkerSizeMultiplier(string poiId, POIType type, POICategory category, float explicitSizeMultiplier = 1f)
        {
            return SanitizeMarkerSizeMultiplier(explicitSizeMultiplier);
        }

        private static float SanitizeMarkerSizeMultiplier(float value)
        {
            return value > 0f ? Mathf.Max(0.1f, value) : 1f;
        }

        private static string BuildStableHierarchyPath(Transform t)
        {
            if (t == null)
                return "null";

            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
        }

        private static string BuildStableScenePoiId(string prefix, GameObject go)
        {
            if (go == null)
                return $"{prefix}:null";

            string sceneName = go.scene.IsValid() ? go.scene.name : "noscene";
            string path = BuildStableHierarchyPath(go.transform);
            return $"{prefix}:{sceneName}:{path}";
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!drawGizmosWhenNotSelected) return;

#if UNITY_EDITOR
            TryRefreshInEditorForGizmos();
#endif

            DrawPOIGizmos(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

#if UNITY_EDITOR
            TryRefreshInEditorForGizmos();
#endif

            DrawPOIGizmos(selected: true);
        }

#if UNITY_EDITOR
        private void TryRefreshInEditorForGizmos()
        {
            if (Application.isPlaying || !autoRefreshInEditor)
                return;

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < _nextEditorRefreshTime)
                return;

            _nextEditorRefreshTime = now + editorAutoRefreshIntervalSeconds;
            Refresh();
        }
#endif

        private void DrawPOIGizmos(bool selected)
        {
            Gizmos.matrix = Matrix4x4.identity;

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
