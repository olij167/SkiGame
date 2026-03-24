using System;
using System.Collections.Generic;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SkiGame.Runs
{

    [DisallowMultipleComponent]
    public sealed class SkiRunLine : MonoBehaviour
    {

        [Header("Identity")]
        [SerializeField] private string runName = "New Run";
        [SerializeField, Tooltip("Stable ID generated once for future map integrations.")]
        private string runId;

        [Header("Authoring")]
        [SerializeField] private List<Vector3> pointsWorld = new List<Vector3>();

        // ---------------- Runtime distance cache (for run tracking) ----------------
        private readonly List<float> _cumDistCache = new List<float>(256);
        private float _totalLengthCache = 0f;
        private bool _distCacheDirty = true;

        
        [Header("Difficulty / Colors")]
        [SerializeField] private RunDifficultyProfileSO difficultyProfile;
        [SerializeField] private SkiRunDifficulty difficulty;
        [SerializeField] private Color runColor = Color.white;
        [SerializeField, Tooltip("If enabled, the run can be classified per-segment (local slope). Flags may be colored per spawn instead of using the baked whole-run color.")]
        private bool classifyPerSegment = false;
        [SerializeField, Tooltip("If enabled, difficulty is forced to this value instead of being classified from slope metrics.")]
        private bool overrideDifficulty = false;

        [SerializeField, Tooltip("Manual difficulty to use when Override Difficulty is enabled.")]
        private SkiRunDifficulty overrideDifficultyValue = SkiRunDifficulty.Green;

        [Header("Sampling")]
        [SerializeField, Tooltip("Distance between samples along the polyline (meters). Lower = more accurate, higher = faster.")]
        [Range(0.25f, 10f)] private float sampleStepMeters = 2f;

        [Header("Flags (Generated)")]
        [SerializeField] private GameObject flagPrefab;
        [SerializeField, Tooltip("Spacing between spawned flags along the run (meters).")]
        [Range(1f, 100f)] private float flagSpacingMeters = 8f;

        [SerializeField, Tooltip("Distance from the start of the run before the first flag pair spawns (meters).")]
        [Range(0f, 100f)] private float flagStartOffsetMeters = 0f;

        [SerializeField, Tooltip("If > 0, stops spawning this many meters before the end of the run (prevents a pair right at the last point).")]
        [Range(0f, 100f)] private float flagEndInsetMeters = 0f;

        [SerializeField, Tooltip("Vertical offset applied above terrain when placing flags.")]
        [Range(0f, 5f)] private float flagHeightOffset = 0.1f;

        [SerializeField, Tooltip("Full width of the run corridor (meters). Flags spawn at +/- half this distance from the centerline.")]
        [Range(2f, 200f)] private float runWidthMeters = 20f;

        [Header("Boundary Placement (Terrain-aware)")]
        [SerializeField, Tooltip("If enabled, boundary points are chosen by searching laterally for a valid terrain position (prevents flags landing on steep side-walls).")]
        private bool terrainAwareBoundaries = true;

        [SerializeField, Tooltip("Lateral search step (meters). Smaller = more accurate, slower rebuild.")]
        [Range(0.1f, 5f)] private float boundarySearchStepMeters = 0.75f;

        [SerializeField, Tooltip("Candidate boundary points above this slope angle are rejected.")]
        [Range(0f, 89f)] private float boundaryMaxSlopeDeg = 45f;

        [SerializeField, Tooltip("Reject candidate boundary points if their height differs from the centerline by more than this (meters).")]
        [Range(0f, 25f)] private float boundaryMaxHeightDelta = 3f;

        [SerializeField, Tooltip("If enabled, chooses the furthest valid boundary (best for maintaining width). If disabled, chooses the best-scoring candidate near target width.")]
        private bool boundaryPreferFurthestValid = true;

        [Header("Gate Overrides (Editor)")]
        [SerializeField, HideInInspector, Tooltip("Optional per-gate overrides used by the editor for manual flag authoring (positions, enable/disable, insertion, locking).")]
        private List<GateOverride> gateOverrides = new List<GateOverride>();

        [Header("Width Overrides (Per Point)")]
        [SerializeField, Tooltip("Optional per-point full-width overrides (meters). Use -1 to use the global Run Width. Kept in sync with points list.")]
        private List<float> widthOverrideMeters = new List<float>();

        [SerializeField, Tooltip("If enabled, left/right positions are snapped to the terrain individually (recommended).")]
        private bool snapSidesToTerrainIndividually = true;

        [SerializeField, Tooltip("If enabled, flags face toward the centerline (good for boundary flags). If disabled, they use downhill-facing if enabled.")]
        private bool faceInwards = true;

        [SerializeField, Tooltip("Optional: name of the generated left/right sub-containers.")]
        private string generatedLeftName = "Left";
        [SerializeField] private string generatedRightName = "Right";

        [SerializeField, Tooltip("If enabled, flags rotate to face downhill.")]
        private bool faceDownhill = true;

        [SerializeField, Tooltip("Name of the generated flags container child.")]
        private string generatedContainerName = "Flags__Generated";

        // ------------------------------------------------------------
        // Fences (Run-Driven)
        // ------------------------------------------------------------
        public enum FenceSide { Left, Right }

        [Serializable]
        private struct FenceSpan
        {
            public FenceSide side;
            [Min(0f)] public float startMeters;
            [Min(0f)] public float endMeters;
            public bool enabled;
        }
        [System.Serializable]
        public struct FenceHole
        {
            public bool enabled;
            public FenceSide side;
            public float startMeters;
            public float endMeters;
        }

        [Tooltip("Explicit exclusion intervals (holes) carved out of fence spans. These are applied before fences are generated.")]
        [SerializeField] private List<FenceHole> fenceExcludeHoles = new List<FenceHole>();

        // --- Fence Sides (NEW authoring model) ---
        // Each corridor side is treated as a single full-length fence span (minus exclusions).
        [SerializeField, Tooltip("Generate a fence along the LEFT corridor edge for the full run length (minus holes / gate exclusions).")]
        private bool fenceUseLeftEdge = false;

        [SerializeField, Tooltip("Generate a fence along the RIGHT corridor edge for the full run length (minus holes / gate exclusions).")]
        private bool fenceUseRightEdge = false;

        // --- Legacy spans (kept only for migration / backward compatibility) ---
        [SerializeField, HideInInspector] private List<FenceSpan> fenceSpans = new List<FenceSpan>();
        [SerializeField, HideInInspector] private bool fenceLegacySpansMigrated = false;

        [Header("Fence Robustness")]
        [Tooltip("If enabled, fences will automatically carve a small exclusion hole around each gate so fences don't overlap flags.")]
        [SerializeField] private bool fenceAutoExcludeAroundGates = true;

        [Tooltip("Half-width of the carved exclusion hole around each gate distance (meters).")]
        [SerializeField] private float fenceGateExclusionRadiusMeters = 1.25f;

        [Tooltip("Extra padding applied to ALL exclusions (holes + gate exclusions). Helps prevent near-misses.")]
        [SerializeField] private float fenceExclusionPaddingMeters = 0.15f;

        [Tooltip("If non-zero, overrides FencePath.terrainMask for generated fence paths. Use this to EXCLUDE flag/prop layers from conform raycasts. 0 = auto terrain layer.")]
        [SerializeField] private LayerMask fenceConformTerrainMask = 0;

        [Header("Fence Smoothing (Control Polyline)")]
        [Tooltip("Extra smoothing pass applied to the sampled boundary polyline BEFORE handing it to FencePath. Helps remove small sampling jitter.")]
        [SerializeField, Range(0, 5)] private int fenceBoundarySmoothPasses = 2;

        [Tooltip("How strongly each smoothing pass pulls points toward their neighbors (0..1).")]
        [SerializeField, Range(0f, 1f)] private float fenceBoundarySmoothStrength = 0.55f;

        [Header("Fences (Run-Driven)")]
        [SerializeField, Tooltip("Name of the generated fences container child.")]
        private string generatedFencesContainerName = "Fences__Generated";

        [SerializeField, Tooltip("Fence segment prefab used by generated FencePath components.")]
        private GameObject fenceSegmentPrefab;

        [SerializeField, Tooltip("Optional corner post prefab used by generated FencePath components.")]
        private GameObject fenceCornerPostPrefab;

        [SerializeField, Tooltip("Spacing between fence path control points sampled along the run edge (meters). Lower = follows the run edge more tightly.")]
        [Min(0.25f)]
        private float fencePointSpacingMeters = 2.0f;

        [SerializeField, Tooltip("Vertical offset applied when snapping smoothed fence points back to terrain (helps avoid z-fighting).")]
        private float fenceTerrainOffsetY = 0.02f;

        [SerializeField, Tooltip("If enabled, generated FencePath spans will use Smooth (Catmull–Rom) sampling for nicer turns.")]
        private bool fenceUseSmoothSampling = true;

        [SerializeField, Tooltip("Sampling density for Smooth mode on generated FencePath components. Higher = smoother but heavier.")]
        [Min(0.1f)]
        private float fenceSmoothSamplesPerMeter = 2.5f;

        [SerializeField, Tooltip("Minimum samples per control-point span in Smooth mode on generated FencePath components.")]
        [Min(2)]
        private int fenceSmoothMinSamplesPerSpan = 6;

        [Header("Baked Results (Read Only)")]
        [SerializeField] private float bakedLengthMeters;
        [SerializeField] private float bakedAvgSlopeDeg;
        [SerializeField] private float bakedMaxSlopeDeg;
        [SerializeField, HideInInspector] private int _bakeInputsHash;

        private float RunLengthMeters => (bakedLengthMeters > 0.001f) ? bakedLengthMeters : GetTotalLengthMeters();

        // Public read access for future systems
        public string RunName => runName;
        public string RunId => runId;
        public IReadOnlyList<Vector3> PointsWorld => pointsWorld;
        public SkiRunDifficulty Difficulty => difficulty;
        public Color RunColor => runColor;
        public float LengthMeters => bakedLengthMeters;
        public float AvgSlopeDeg => bakedAvgSlopeDeg;
        public float MaxSlopeDeg => bakedMaxSlopeDeg;
        public float RunWidthMeters => runWidthMeters;

        private void Reset()
        {
            EnsureRunId();
            MarkDistanceCacheDirty();
        }

        private void OnValidate()
        {
            EnsureRunId();
            MarkDistanceCacheDirty();

            if (sampleStepMeters < 0.25f) sampleStepMeters = 0.25f;
            if (flagSpacingMeters < 1f) flagSpacingMeters = 1f;

            // Keep width overrides aligned with points
            if (pointsWorld == null) pointsWorld = new List<Vector3>();
            if (widthOverrideMeters == null) widthOverrideMeters = new List<float>();

            while (widthOverrideMeters.Count < pointsWorld.Count)
                widthOverrideMeters.Add(-1f);

            while (widthOverrideMeters.Count > pointsWorld.Count)
                widthOverrideMeters.RemoveAt(widthOverrideMeters.Count - 1);


        }

        private void EnsureRunId()
        {
            if (string.IsNullOrWhiteSpace(runId))
                runId = Guid.NewGuid().ToString("N");
        }

        private void ApplyDifficultyOverrideIfEnabled()
        {
            if (!overrideDifficulty)
                return;

            difficulty = overrideDifficultyValue;

            // If profile exists, derive colour from it; otherwise fall back.
            runColor = (difficultyProfile != null)
                ? difficultyProfile.GetColor(overrideDifficultyValue)
                : Color.white;
        }

        public void AddPointWorld(Vector3 p)
        {
            pointsWorld.Add(p);

            // Maintain alignment
            if (widthOverrideMeters == null) widthOverrideMeters = new List<float>();
            widthOverrideMeters.Add(-1f);

            MarkDistanceCacheDirty();
        }

        /// <summary>
        /// Inserts a point into the run polyline at a specific index, keeping widthOverrideMeters aligned.
        /// </summary>
        public int InsertPointWorld(int index, Vector3 p)
        {
            if (pointsWorld == null)
                pointsWorld = new List<Vector3>();

            if (widthOverrideMeters == null)
                widthOverrideMeters = new List<float>();

            index = Mathf.Clamp(index, 0, pointsWorld.Count);

            pointsWorld.Insert(index, p);
            widthOverrideMeters.Insert(index, -1f);

            MarkDistanceCacheDirty();
            return index;
        }

        /// <summary>
        /// Smart point insertion for editor authoring.
        ///
        /// Behaviour:
        /// - Empty / single-point runs append.
        /// - If the click looks like a continuation of the tail, append to the end.
        /// - Otherwise, insert on the closest segment.
        ///
        /// This preserves intentional in-between editing while making normal sequential
        /// terrain painting much less likely to insert into the middle of the run.
        /// </summary>
        public int InsertPointWorldSmart(Vector3 p)
        {
            if (pointsWorld == null)
                pointsWorld = new List<Vector3>();

            int n = pointsWorld.Count;

            // Empty / single point: append.
            if (n <= 1)
                return InsertPointWorld(n, p);

            // Work in XZ so terrain height variation does not skew intent detection.
            Vector2 pp = new Vector2(p.x, p.z);

            // ------------------------------------------------------------
            // 1) Check whether this looks like a tail extension.
            // ------------------------------------------------------------
            Vector3 prev3 = pointsWorld[n - 2];
            Vector3 last3 = pointsWorld[n - 1];

            Vector2 prev = new Vector2(prev3.x, prev3.z);
            Vector2 last = new Vector2(last3.x, last3.z);

            Vector2 tail = last - prev;
            float tailLen = tail.magnitude;

            if (tailLen > 0.0001f)
            {
                Vector2 tailDir = tail / tailLen;
                Vector2 fromLast = pp - last;
                float fromLastDist = fromLast.magnitude;

                if (fromLastDist > 0.0001f)
                {
                    Vector2 fromLastDir = fromLast / fromLastDist;
                    float forwardDot = Vector2.Dot(tailDir, fromLastDir);

                    // Projection of the clicked point onto the tail direction, measured from the last point.
                    float forwardDistance = Vector2.Dot(fromLast, tailDir);

                    // Distance from the clicked point to the tail ray that extends forward from the last point.
                    float lateralDistance = Mathf.Sqrt(Mathf.Max(0f, fromLast.sqrMagnitude - forwardDistance * forwardDistance));

                    // Authoring-tuned heuristics:
                    // - must be at least somewhat in front of the tail
                    // - allow some lateral slop so terrain painting still feels forgiving
                    // - append when close to the tail endpoint and roughly aligned
                    const float minForwardDot = 0.15f;
                    float appendProximityMeters = Mathf.Max(8f, tailLen * 1.75f);
                    float appendLateralToleranceMeters = Mathf.Max(6f, runWidthMeters * 0.6f);

                    bool isForwardOfTail = forwardDistance > 0f && forwardDot >= minForwardDot;
                    bool isNearTailEndpoint = fromLastDist <= appendProximityMeters;
                    bool isReasonablyAlignedWithTail = lateralDistance <= appendLateralToleranceMeters;

                    if (isForwardOfTail && isNearTailEndpoint && isReasonablyAlignedWithTail)
                        return InsertPointWorld(n, p);
                }
            }

            // ------------------------------------------------------------
            // 2) Fall back to closest-segment insertion.
            // ------------------------------------------------------------
            int bestSeg = -1;
            float bestDist = float.PositiveInfinity;
            float bestTUnclamped = 0f;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a3 = pointsWorld[i];
                Vector3 b3 = pointsWorld[i + 1];

                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);

                Vector2 ab = b - a;
                float abLen2 = ab.sqrMagnitude;

                float tUnclamped = 0f;
                if (abLen2 > 0.000001f)
                    tUnclamped = Vector2.Dot(pp - a, ab) / abLen2;

                float t = Mathf.Clamp01(tUnclamped);
                Vector2 proj = a + ab * t;
                float d = Vector2.Distance(pp, proj);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestSeg = i;
                    bestTUnclamped = tUnclamped;
                }
            }

            if (bestSeg < 0)
                return InsertPointWorld(n, p);

            // If the closest projection lies before the first point or after the last point,
            // treat it as start/end insertion.
            if (bestSeg == 0 && bestTUnclamped < 0f)
                return InsertPointWorld(0, p);

            if (bestSeg == n - 2 && bestTUnclamped > 1f)
                return InsertPointWorld(n, p);

            // Otherwise, insert between bestSeg and bestSeg + 1.
            return InsertPointWorld(bestSeg + 1, p);
        }

        public bool RemoveLastPoint()
        {
            if (pointsWorld.Count == 0) return false;

            pointsWorld.RemoveAt(pointsWorld.Count - 1);

            if (widthOverrideMeters != null && widthOverrideMeters.Count > 0)
                widthOverrideMeters.RemoveAt(widthOverrideMeters.Count - 1);

            return true;
        }

        public void ClearPoints()
        {
            pointsWorld.Clear();
            widthOverrideMeters.Clear();
            MarkDistanceCacheDirty();
        }

        public void ClearWidthOverrides()
        {
            if (widthOverrideMeters == null)
                widthOverrideMeters = new List<float>();

            widthOverrideMeters.Clear();

            int n = (pointsWorld != null) ? pointsWorld.Count : 0;
            for (int i = 0; i < n; i++)
                widthOverrideMeters.Add(-1f);
        }

        private void MarkDistanceCacheDirty()
        {
            _distCacheDirty = true;
        }

        private void EnsureDistanceCache()
        {
            if (!_distCacheDirty)
                return;

            _cumDistCache.Clear();
            _totalLengthCache = 0f;

            if (pointsWorld == null || pointsWorld.Count < 2)
            {
                _distCacheDirty = false;
                return;
            }

            _cumDistCache.Add(0f);
            for (int i = 0; i < pointsWorld.Count - 1; i++)
            {
                _totalLengthCache += Vector3.Distance(pointsWorld[i], pointsWorld[i + 1]);
                _cumDistCache.Add(_totalLengthCache);
            }

            _distCacheDirty = false;
        }

        public float GetTotalLengthMeters()
        {
            EnsureDistanceCache();
            return _totalLengthCache;
        }

        public Vector3 GetEndPointWorld()
        {
            if (pointsWorld == null || pointsWorld.Count == 0)
                return transform.position;
            return pointsWorld[pointsWorld.Count - 1];
        }

        /// <summary>
        /// Runtime query: closest point on the run centerline in XZ, along-distance (meters), corridor half-width, and XZ distance to center.
        /// </summary>
        public bool TryGetClosestPointOnCenterlineXZ(
            Vector3 worldPos,
            out float distanceAlongMeters,
            out float distToCenterXZ,
            out float halfWidthMeters,
            out Vector3 closest)
        {
            distanceAlongMeters = 0f;
            distToCenterXZ = 0f;
            halfWidthMeters = 0f;
            closest = default;

            if (pointsWorld == null || pointsWorld.Count < 2)
                return false;

            if (!TryGetClosestPointOnPolylineXZ(pointsWorld, worldPos, out int segIndex, out float segT, out Vector3 c))
                return false;

            EnsureDistanceCache();

            segIndex = Mathf.Clamp(segIndex, 0, pointsWorld.Count - 2);
            float segLen = Vector3.Distance(pointsWorld[segIndex], pointsWorld[segIndex + 1]);
            float baseDist = (segIndex >= 0 && segIndex < _cumDistCache.Count) ? _cumDistCache[segIndex] : 0f;

            distanceAlongMeters = baseDist + (Mathf.Clamp01(segT) * segLen);
            closest = c;

            distToCenterXZ = Vector2.Distance(new Vector2(worldPos.x, worldPos.z), new Vector2(c.x, c.z));

            float w = Mathf.Max(2f, GetWidthMetersAtSample(segIndex, segT));
            halfWidthMeters = Mathf.Max(0.25f, w * 0.5f);

            return true;
        }


        /// <summary>
        /// Runtime helper: corridor half-width (meters) at a given centerline sample (segment + t).
        /// Mirrors the width logic used by TryGetClosestPointOnCenterlineXZ so run tracking stays consistent.
        /// </summary>
        public float GetHalfWidthMetersAtSample(int segIndex, float segT)
        {
            if (pointsWorld == null || pointsWorld.Count < 2)
                return Mathf.Max(0.25f, runWidthMeters * 0.5f);

            segIndex = Mathf.Clamp(segIndex, 0, pointsWorld.Count - 2);
            float w = Mathf.Max(2f, GetWidthMetersAtSample(segIndex, segT));
            return Mathf.Max(0.25f, w * 0.5f);
        }

        /// <summary>
        /// Detailed closest point query that also returns the segment index + segment t.
        /// </summary>
        public bool TryGetClosestPointOnCenterlineXZ_Detailed(
            Vector3 worldPos,
            out float distanceAlongMeters,
            out float distToCenterXZ,
            out float halfWidthMeters,
            out Vector3 closest,
            out int segIndex,
            out float segT)
        {
            return TryGetClosestPointOnCenterlineXZ_RangedDetailed(
                worldPos,
                0,
                (pointsWorld != null ? pointsWorld.Count - 2 : 0),
                out distanceAlongMeters,
                out distToCenterXZ,
                out halfWidthMeters,
                out closest,
                out segIndex,
                out segT);
        }

        /// <summary>
        /// Ranged closest point query over a segment window [segStart..segEnd] (inclusive).
        /// Use this for stable progression tracking (prevents snapping across overlaps).
        /// </summary>
        public bool TryGetClosestPointOnCenterlineXZ_RangedDetailed(
            Vector3 worldPos,
            int segStart,
            int segEnd,
            out float distanceAlongMeters,
            out float distToCenterXZ,
            out float halfWidthMeters,
            out Vector3 closest,
            out int segIndex,
            out float segT)
        {
            distanceAlongMeters = 0f;
            distToCenterXZ = 0f;
            halfWidthMeters = 0f;
            closest = default;
            segIndex = -1;
            segT = 0f;

            if (pointsWorld == null || pointsWorld.Count < 2)
                return false;

            int segMax = pointsWorld.Count - 2;
            segStart = Mathf.Clamp(segStart, 0, segMax);
            segEnd = Mathf.Clamp(segEnd, 0, segMax);
            if (segEnd < segStart) (segStart, segEnd) = (segEnd, segStart);

            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            float bestD2 = float.PositiveInfinity;
            int bestSeg = -1;
            float bestT = 0f;
            Vector3 bestClosest = default;

            for (int i = segStart; i <= segEnd; i++)
            {
                Vector3 a3 = pointsWorld[i];
                Vector3 b3 = pointsWorld[i + 1];

                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);
                Vector2 ab = b - a;
                float abLen2 = ab.sqrMagnitude;

                float t = 0f;
                if (abLen2 > 1e-6f)
                    t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLen2);

                Vector2 c2 = a + ab * t;
                float d2 = (p - c2).sqrMagnitude;

                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    bestSeg = i;
                    bestT = t;
                    bestClosest = Vector3.LerpUnclamped(a3, b3, t);
                }
            }

            if (bestSeg < 0)
                return false;

            EnsureDistanceCache();

            float segLen = Vector3.Distance(pointsWorld[bestSeg], pointsWorld[bestSeg + 1]);
            float baseDist = (bestSeg >= 0 && bestSeg < _cumDistCache.Count) ? _cumDistCache[bestSeg] : 0f;

            distanceAlongMeters = baseDist + (Mathf.Clamp01(bestT) * segLen);
            closest = bestClosest;

            distToCenterXZ = Vector2.Distance(new Vector2(worldPos.x, worldPos.z), new Vector2(bestClosest.x, bestClosest.z));
            halfWidthMeters = GetHalfWidthMetersAtSample(bestSeg, bestT);

            segIndex = bestSeg;
            segT = bestT;
            return true;
        }

        public void BakeMetrics()
        {
            // Cheap change detection: if inputs didn’t change, don’t re-sample slopes every call.
            int h = 17;

            unchecked
            {
                h = h * 31 + (difficultyProfile ? difficultyProfile.GetInstanceID() : 0);
                h = h * 31 + (classifyPerSegment ? 1 : 0);
                h = h * 31 + Mathf.RoundToInt(sampleStepMeters * 1000f);

                h = h * 31 + (pointsWorld != null ? pointsWorld.Count : 0);
                if (pointsWorld != null)
                {
                    // Quantize positions so tiny float noise doesn’t thrash the cache
                    for (int i = 0; i < pointsWorld.Count; i++)
                    {
                        Vector3 p = pointsWorld[i];
                        h = h * 31 + Mathf.RoundToInt(p.x * 10f);
                        h = h * 31 + Mathf.RoundToInt(p.y * 10f);
                        h = h * 31 + Mathf.RoundToInt(p.z * 10f);
                    }
                }

                h = h * 31 + Mathf.RoundToInt(runWidthMeters * 100f);
                h = h * 31 + (widthOverrideMeters != null ? widthOverrideMeters.Count : 0);
                if (widthOverrideMeters != null)
                {
                    for (int i = 0; i < widthOverrideMeters.Count; i++)
                        h = h * 31 + Mathf.RoundToInt(widthOverrideMeters[i] * 100f);
                }
            }

            if (_bakeInputsHash == h)
                return;

            _bakeInputsHash = h;

            // ---- original BakeMetrics logic below (unchanged) ----
            if (difficultyProfile == null)
            {
                runColor = Color.white;
                difficulty = SkiRunDifficulty.Green;
                bakedLengthMeters = 0f;
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;

                ApplyDifficultyOverrideIfEnabled();
                return;
            }

            if (pointsWorld == null || pointsWorld.Count < 2)
            {
                runColor = difficultyProfile.GetColor(SkiRunDifficulty.Green);
                difficulty = SkiRunDifficulty.Green;
                bakedLengthMeters = 0f;
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;
                ApplyDifficultyOverrideIfEnabled();
                return;
            }

            var terrain = ResolveTerrain();
            if (terrain == null)
            {
                bakedLengthMeters = ComputePolylineLength(pointsWorld);
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;
                difficulty = difficultyProfile.Classify(0f, 0f, out runColor);
                ApplyDifficultyOverrideIfEnabled();
                return;
            }

            bakedLengthMeters = ComputePolylineLength(pointsWorld);

            SampleSlopeAlongPolyline(
                terrain,
                pointsWorld,
                sampleStepMeters,
                out bakedAvgSlopeDeg,
                out bakedMaxSlopeDeg
            );

            if (classifyPerSegment)
                difficulty = difficultyProfile.ClassifyValue(bakedMaxSlopeDeg, out runColor);
            else
                difficulty = difficultyProfile.Classify(bakedAvgSlopeDeg, bakedMaxSlopeDeg, out runColor);

            ApplyDifficultyOverrideIfEnabled();
        }

        public void RebuildFlags()
        {
            if (flagPrefab == null) return;
            if (pointsWorld == null || pointsWorld.Count < 2) return;

            BakeMetrics();

#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Ski Run Flags");
#endif

            Transform container = GetOrCreateGeneratedContainer();
            Transform leftC = GetOrCreateChild(container, generatedLeftName);
            Transform rightC = GetOrCreateChild(container, generatedRightName);

            // Clear existing (left/right only)
#if UNITY_EDITOR
            ClearChildrenUndo(leftC);
            ClearChildrenUndo(rightC);
#else
    ClearChildrenRuntime(leftC);
    ClearChildrenRuntime(rightC);
#endif

            float runLen = bakedLengthMeters;
            float spawnStart = Mathf.Max(0f, flagStartOffsetMeters);
            float spawnEnd = Mathf.Max(0f, runLen - Mathf.Max(0f, flagEndInsetMeters));
            if (runLen <= 0.001f || spawnStart >= spawnEnd) return;

            // Cache overlap candidates once per rebuild.
            List<SkiRunLine> avoidRuns = null;

            float total = 0f;
            float nextSpawn = spawnStart;

            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 tangent = (b - a) / segLen;

                float segStartDist = total;
                float segEndDist = total + segLen;

                if (nextSpawn > segEndDist)
                {
                    total += segLen;
                    continue;
                }

                if (nextSpawn < segStartDist)
                    nextSpawn = segStartDist;

                while (nextSpawn <= segEndDist && nextSpawn <= spawnEnd)
                {
                    float tFromSegStart = (nextSpawn - segStartDist);
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(tFromSegStart / segLen);

                    Vector3 center = a + tangent * tFromSegStart;

                    Terrain terrainAtCenter = ResolveTerrainAt(center);
                    if (terrainAtCenter == null)
                    {
                        nextSpawn += flagSpacingMeters;
                        continue;
                    }

                    Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);
                    Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                    Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(seg, segT));
                    float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                    Vector3 left = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: true, avoidRuns);
                    Vector3 right = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: false, avoidRuns);

                    // ------------------------------------------------------------
                    // Apply Gate Overrides + legacy Flag Pair Overrides (editor-authored)
                    // ------------------------------------------------------------
                    float qDist = QuantizeGateDistance(nextSpawn);

                    Vector3 baseLeft = left;
                    Vector3 baseRight = right;

                    byte sideMask = (byte)(SIDE_LEFT | SIDE_RIGHT);

                    // GateOverrides (new authoring system)
                    ApplyGateOverridesForDistance(
                        qDist,
                        baseLeft,
                        baseRight,
                        avoidRuns,
                        includeOverlapAvoidance: false,
                        ref left,
                        ref right,
                        ref sideMask);

                    // Post-override terrain snap:
                    // Ensures authored/moved gate positions always sit on the terrain height
                    // (fixes “flags not snapping to correct height” after manual edits).
                    if (snapSidesToTerrainIndividually)
                    {
                        if ((sideMask & SIDE_LEFT) != 0)
                            left = SnapToTerrain(ResolveTerrainAt(left) ?? terrainAtCenter, left, flagHeightOffset);

                        if ((sideMask & SIDE_RIGHT) != 0)
                            right = SnapToTerrain(ResolveTerrainAt(right) ?? terrainAtCenter, right, flagHeightOffset);
                    }
                    else
                    {
                        if ((sideMask & SIDE_LEFT) != 0)
                            left.y = centerGround.y + flagHeightOffset;

                        if ((sideMask & SIDE_RIGHT) != 0)
                            right.y = centerGround.y + flagHeightOffset;
                    }

                    if (sideMask == 0)
                    {
                        nextSpawn += flagSpacingMeters;
                        continue;
                    }

                    Quaternion leftRot = ComputeFlagRotation(terrainAtCenter, left, smoothTangent, lateral, isLeft: true);
                    Quaternion rightRot = ComputeFlagRotation(terrainAtCenter, right, smoothTangent, lateral, isLeft: false);

                    GameObject leftFlag = null;
                    GameObject rightFlag = null;

                    if ((sideMask & SIDE_LEFT) != 0)
                        leftFlag = InstantiateFlagPrefab(leftC, left, leftRot, flagPrefab);

                    if ((sideMask & SIDE_RIGHT) != 0)
                        rightFlag = InstantiateFlagPrefab(rightC, right, rightRot, flagPrefab);

                    Color pairColor = runColor;
                    if (!overrideDifficulty && classifyPerSegment && difficultyProfile != null)
                    {
                        float localSlopeDeg = Vector3.Angle(up, Vector3.up);
                        difficultyProfile.ClassifyValue(localSlopeDeg, out pairColor);
                    }

                    if (leftFlag != null)
                    {
                        ApplyColorToFlagClothOnly(leftFlag, pairColor);
                        EnsureReactiveFlag(leftFlag);
                    }

                    if (rightFlag != null)
                    {
                        ApplyColorToFlagClothOnly(rightFlag, pairColor);
                        EnsureReactiveFlag(rightFlag);
                    }

                    nextSpawn += flagSpacingMeters;
                }

                total += segLen;
            }

#if UNITY_EDITOR

            Undo.CollapseUndoOperations(group);
#endif
        }

        public Transform GetOrCreateGeneratedContainer()
        {
            Transform t = transform.Find(generatedContainerName);
            if (t != null) return t;

            var go = new GameObject(generatedContainerName);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Flags Container");
#endif
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        public void ApplyColorToGeneratedFlags()
        {
            Transform container = transform.Find(generatedContainerName);
            if (container == null) return;

            var tints = container.GetComponentsInChildren<RunFlagClothTint>(true);
            for (int i = 0; i < tints.Length; i++)
                tints[i].Tint = runColor;
        }


        private static void EnsureReactiveFlag(GameObject flagRoot)
        {
            if (flagRoot == null) return;

            // If your prefab already has it, we just ensure setup.
            var reactive = flagRoot.GetComponent<SkiFlagReactive>();
            if (reactive == null)
                reactive = flagRoot.AddComponent<SkiFlagReactive>();
        }

        // ------------------------------------------------------------
        // Generated Fences (Run-Driven)
        // ------------------------------------------------------------

        Transform GetOrCreateGeneratedFencesContainer()
        {
            Transform t = transform.Find(generatedFencesContainerName);
            if (t != null) return t;

            var go = new GameObject(generatedFencesContainerName);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Fences Container");
#endif
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        public void ClearGeneratedFences()
        {
            Transform t = transform.Find(generatedFencesContainerName);
            if (t == null) return;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(t.gameObject);
#else
            UnityEngine.Object.Destroy(t.gameObject);
#endif
        }

#if UNITY_EDITOR
        private void TryMigrateLegacyFenceSpans(float runLen)
        {
            if (fenceLegacySpansMigrated) return;
            if (fenceSpans == null || fenceSpans.Count == 0)
            {
                fenceLegacySpansMigrated = true;
                return;
            }

            // Build enabled-interval sets per side.
            var left = new List<Interval>(16);
            var right = new List<Interval>(16);

            for (int i = 0; i < fenceSpans.Count; i++)
            {
                var sp = fenceSpans[i];
                if (!sp.enabled) continue;

                float a = Mathf.Clamp(Mathf.Min(sp.startMeters, sp.endMeters), 0f, runLen);
                float b = Mathf.Clamp(Mathf.Max(sp.startMeters, sp.endMeters), 0f, runLen);
                if (b - a < 0.01f) continue;

                if (sp.side == FenceSide.Left) left.Add(new Interval(a, b));
                else right.Add(new Interval(a, b));
            }

            // If there were no enabled spans, disable both edges.
            fenceUseLeftEdge = left.Count > 0;
            fenceUseRightEdge = right.Count > 0;

            // Convert "enabled intervals" -> "holes as complement" for each side.
            if (fenceExcludeHoles == null) fenceExcludeHoles = new List<FenceHole>(32);

            if (left.Count > 0) AddComplementHoles(left, FenceSide.Left, runLen);
            if (right.Count > 0) AddComplementHoles(right, FenceSide.Right, runLen);

            // Clear legacy spans after migration so they can’t fight the new model.
            fenceSpans.Clear();
            fenceLegacySpansMigrated = true;

            void AddComplementHoles(List<Interval> enabled, FenceSide side, float len)
            {
                enabled.Sort((x, y) => x.a.CompareTo(y.a));

                // Merge overlaps
                var merged = new List<Interval>(enabled.Count);
                Interval cur = enabled[0];
                for (int i = 1; i < enabled.Count; i++)
                {
                    var n = enabled[i];
                    if (n.a <= cur.b + 0.0001f) cur.b = Mathf.Max(cur.b, n.b);
                    else { merged.Add(cur); cur = n; }
                }
                merged.Add(cur);

                float t = 0f;
                for (int i = 0; i < merged.Count; i++)
                {
                    var m = merged[i];
                    if (m.a > t + 0.01f)
                        fenceExcludeHoles.Add(new FenceHole { enabled = true, side = side, startMeters = t, endMeters = m.a });
                    t = Mathf.Max(t, m.b);
                }

                if (t < len - 0.01f)
                    fenceExcludeHoles.Add(new FenceHole { enabled = true, side = side, startMeters = t, endMeters = len });
            }
        }
#endif

        public void RebuildFences()
        {
#if UNITY_EDITOR
            // validate prefabs
            if (fenceSegmentPrefab == null || fenceCornerPostPrefab == null)
            {
                Debug.LogWarning($"{name}: Fence rebuild skipped - missing fence prefabs.");
                return;
            }

            BakeMetrics();

            float runLen = bakedLengthMeters;
            if (runLen <= 0.001f)
            {
                Debug.LogWarning($"{name}: Fence rebuild skipped - run length not baked.");
                return;
            }

            // One-time migration: old authored spans -> left/right toggles + holes.
            TryMigrateLegacyFenceSpans(runLen);

            // container
            Transform root = transform.Find(generatedFencesContainerName);
            if (root == null)
            {
                var go = new GameObject(generatedFencesContainerName);
                go.transform.SetParent(transform, false);
                root = go.transform;
            }

            // wipe old
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            // build gate distances once (for auto-exclusions)
            List<float> gateDistancesAll = null;
            if (fenceAutoExcludeAroundGates)
            {
                gateDistancesAll = new List<float>(256);
                BuildGateDistanceList(0f, runLen, gateDistancesAll);
            }

            // reusable buffers
            var points = new List<Vector3>(256);
            var segs = new List<Interval>(32);

            void BuildSide(FenceSide side)
            {
                // Full-length pseudo-span for this side
                var span = new FenceSpan
                {
                    side = side,
                    startMeters = 0f,
                    endMeters = runLen,
                    enabled = true
                };

                segs.Clear();
                BuildEffectiveFenceSegments(span, gateDistancesAll, segs);
                if (segs.Count == 0) return;

                for (int s = 0; s < segs.Count; s++)
                {
                    Interval seg = segs[s];
                    if (seg.b - seg.a < 0.25f) continue;

                    points.Clear();

                    // sample boundary points for this segment
                    SampleFenceBoundaryPolyline(
                        isLeft: (side == FenceSide.Left),
                        startMeters: seg.a,
                        endMeters: seg.b,
                        spacingMeters: Mathf.Max(0.25f, fencePointSpacingMeters),
                        avoidRuns: null,
                        outWorldPoints: points
                    );

                    if (points.Count < 2) continue;

                    // Create child
                    string childName = $"Fence_{side}_Seg{s:00}";
                    Transform child = root.Find(childName);
                    if (child == null)
                    {
                        var go = new GameObject(childName);
                        go.transform.SetParent(root, false);
                        child = go.transform;
                    }

                    var fp = EnsureFencePath(child);
                    if (fp == null) continue;

                    // configure fence path
                    fp.fenceSegmentPrefab = fenceSegmentPrefab;
                    fp.cornerPostPrefab = fenceCornerPostPrefab;

                    fp.conformToTerrain = true;
                    fp.terrainMask = ResolveFenceTerrainMask();
                    fp.yOffset = 0f;

                    fp.usePrefabLength = true;

                    if (fenceUseSmoothSampling)
                    {
                        fp.pathMode = FencePath.PathMode.SmoothCatmullRom;
                        fp.samplesPerMeter = Mathf.Max(0.1f, fenceSmoothSamplesPerMeter);
                        fp.minSamplesPerSpan = Mathf.Max(4, fenceSmoothMinSamplesPerSpan);
                    }
                    else
                    {
                        fp.pathMode = FencePath.PathMode.Polyline;
                    }

                    fp.ClearPoints();
                    for (int p = 0; p < points.Count; p++)
                        fp.AddWorldPoint(points[p]);

                    fp.Rebuild();
                }
            }

            if (fenceUseLeftEdge) BuildSide(FenceSide.Left);
            if (fenceUseRightEdge) BuildSide(FenceSide.Right);

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        // Small helper for interval math inside this class
        private struct Interval { public float a, b; public Interval(float a, float b) { this.a = a; this.b = b; } }

        private LayerMask ResolveFenceTerrainMask()
        {
            if (fenceConformTerrainMask.value != 0)
                return fenceConformTerrainMask;

            // auto: use active terrain layer if present, otherwise default to everything.
            var t = Terrain.activeTerrain;
            if (t != null) return 1 << t.gameObject.layer;
            return ~0;
        }

        private void BuildEffectiveFenceSegments(FenceSpan span, List<float> gateDistancesAll, List<Interval> outSegs)
        {
            outSegs.Clear();

            float spanStart = Mathf.Clamp(Mathf.Min(span.startMeters, span.endMeters), 0f, RunLengthMeters);
            float spanEnd = Mathf.Clamp(Mathf.Max(span.startMeters, span.endMeters), 0f, RunLengthMeters);

            if (spanEnd <= spanStart) return;

            // start with the full span interval
            outSegs.Add(new Interval(spanStart, spanEnd));

            // subtract explicit holes
            if (fenceExcludeHoles != null && fenceExcludeHoles.Count > 0)
            {
                for (int i = 0; i < fenceExcludeHoles.Count; i++)
                {
                    var h = fenceExcludeHoles[i];
                    if (!h.enabled) continue;
                    if (h.side != span.side) continue;

                    float ha = Mathf.Clamp(Mathf.Min(h.startMeters, h.endMeters), 0f, RunLengthMeters);
                    float hb = Mathf.Clamp(Mathf.Max(h.startMeters, h.endMeters), 0f, RunLengthMeters);
                    if (hb <= ha) continue;

                    // inflate a little (padding)
                    ha = Mathf.Max(0f, ha - fenceExclusionPaddingMeters);
                    hb = Mathf.Min(RunLengthMeters, hb + fenceExclusionPaddingMeters);

                    SubtractInterval(outSegs, ha, hb);
                    if (outSegs.Count == 0) return;
                }
            }

            // subtract around gates (per-side enabled)
            if (fenceAutoExcludeAroundGates && gateDistancesAll != null && gateDistancesAll.Count > 0)
            {
                bool isLeft = (span.side == FenceSide.Left);

                for (int i = 0; i < gateDistancesAll.Count; i++)
                {
                    float d = gateDistancesAll[i];
                    if (d < spanStart || d > spanEnd) continue;

                    if (!IsGateSideEnabled(d, isLeft))
                        continue;

                    float ha = Mathf.Max(0f, d - fenceGateExclusionRadiusMeters - fenceExclusionPaddingMeters);
                    float hb = Mathf.Min(RunLengthMeters, d + fenceGateExclusionRadiusMeters + fenceExclusionPaddingMeters);

                    SubtractInterval(outSegs, ha, hb);
                    if (outSegs.Count == 0) return;
                }
            }
        }

        private static void SubtractInterval(List<Interval> segs, float holeA, float holeB)
        {
            if (segs == null || segs.Count == 0) return;
            if (holeB <= holeA) return;

            for (int i = segs.Count - 1; i >= 0; i--)
            {
                var s = segs[i];

                // no overlap
                if (holeB <= s.a || holeA >= s.b)
                    continue;

                // hole fully covers segment
                if (holeA <= s.a && holeB >= s.b)
                {
                    segs.RemoveAt(i);
                    continue;
                }

                // hole eats left part
                if (holeA <= s.a && holeB < s.b)
                {
                    segs[i] = new Interval(holeB, s.b);
                    continue;
                }

                // hole eats right part
                if (holeA > s.a && holeB >= s.b)
                {
                    segs[i] = new Interval(s.a, holeA);
                    continue;
                }

                // hole splits segment into two
                if (holeA > s.a && holeB < s.b)
                {
                    // replace with left, insert right
                    segs[i] = new Interval(s.a, holeA);
                    segs.Insert(i + 1, new Interval(holeB, s.b));
                    continue;
                }
            }

            // optional cleanup: remove tiny crumbs
            for (int i = segs.Count - 1; i >= 0; i--)
            {
                if (segs[i].b - segs[i].a < 0.25f)
                    segs.RemoveAt(i);
            }
        }

        private static Transform GetOrCreateFenceChild(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.Find(name);
            if (t != null) return t;

            var go = new GameObject(name);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Fence Span");
#endif
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static FencePath EnsureFencePath(Transform t)
        {
            if (t == null) return null;

            var fp = t.GetComponent<FencePath>();
            if (fp != null) return fp;

#if UNITY_EDITOR
            return Undo.AddComponent<FencePath>(t.gameObject);
#else
            return t.gameObject.AddComponent<FencePath>();
#endif
        }

        private void SampleFenceBoundaryPolyline(
            bool isLeft,
            float startMeters,
            float endMeters,
            float spacingMeters,
            List<SkiRunLine> avoidRuns,
            List<Vector3> outWorldPoints)
        {
            if (outWorldPoints == null) return;
            outWorldPoints.Clear();

            float runLen = bakedLengthMeters;
            if (runLen <= 0.001f) return;

            startMeters = Mathf.Clamp(startMeters, 0f, runLen);
            endMeters = Mathf.Clamp(endMeters, 0f, runLen);
            if (endMeters < startMeters) { float tmp = startMeters; startMeters = endMeters; endMeters = tmp; }

            float total = 0f;
            float nextSample = startMeters;

            float stepMeters = Mathf.Max(0.25f, spacingMeters);

            Vector3 centerStart = (pointsWorld != null && pointsWorld.Count > 0) ? pointsWorld[0] : transform.position;


            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 tangent = (b - a) / segLen;

                float segStartDist = total;
                float segEndDist = total + segLen;

                if (nextSample > segEndDist)
                {
                    total += segLen;
                    continue;
                }

                if (nextSample < segStartDist)
                    nextSample = segStartDist;

                while (nextSample <= segEndDist && nextSample < endMeters - 0.0001f)
                {
                    float tFromSegStart = (nextSample - segStartDist);
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(tFromSegStart / segLen);

                    Vector3 center = a + tangent * tFromSegStart;

                    Terrain terrainAtCenter = ResolveTerrainAt(center);
                    if (terrainAtCenter == null)
                    {
                        nextSample += stepMeters;
                        continue;
                    }

                    Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);
                    Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                    Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(seg, segT));
                    float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                    Vector3 edgeUnsnap = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft, avoidRuns, heightOffset: 0f);

                    // Snap final fence control points to terrain (no offset here; FencePath can still conform independently).
                    Terrain tEdge = ResolveTerrainAt(edgeUnsnap) ?? terrainAtCenter;
                    Vector3 edge = (tEdge != null) ? SnapToTerrain(tEdge, edgeUnsnap, 0f) : edgeUnsnap;

                    outWorldPoints.Add(edge);

                    nextSample += stepMeters;
                }

                total += segLen;

                if (nextSample > endMeters - 0.0001f)
                    break;
            }

            // Extra post-smoothing to remove small sampling jitter (XZ only), then re-snap Y to terrain.
            if (fenceUseSmoothSampling && fenceBoundarySmoothPasses > 0 && outWorldPoints.Count >= 3)
            {
                PostSmoothFencePolyline(outWorldPoints, fenceBoundarySmoothPasses, fenceBoundarySmoothStrength, centerStart);
            }

            // Ensure we include the end point.
            if (TrySampleFenceBoundaryAtDistance(isLeft, endMeters, avoidRuns, out Vector3 endPt))
            {
                if (outWorldPoints.Count == 0 || (outWorldPoints[outWorldPoints.Count - 1] - endPt).sqrMagnitude > 0.05f * 0.05f)
                    outWorldPoints.Add(endPt);
            }
        }

        private void PostSmoothFencePolyline(List<Vector3> pts, int passes, float strength, Vector3 centerSeed)
        {
            if (pts == null || pts.Count < 3) return;

            strength = Mathf.Clamp01(strength);
            passes = Mathf.Max(1, passes);

            var tmp = new Vector3[pts.Count];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < pts.Count; i++)
                    tmp[i] = pts[i];

                // Smooth interior points in XZ (only), then snap Y back to terrain.
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    Vector3 a = tmp[i - 1];
                    Vector3 b = tmp[i];
                    Vector3 c = tmp[i + 1];

                    Vector3 avg = (a + b + c) / 3f;

                    b.x = Mathf.Lerp(b.x, avg.x, strength);
                    b.z = Mathf.Lerp(b.z, avg.z, strength);

                    Terrain t = ResolveTerrainAt(b);
                    if (t != null)
                        b = SnapToTerrain(t, b, fenceTerrainOffsetY);

                    pts[i] = b;
                }

                // Keep endpoints stable, but ensure their Y is snapped (prevents vertical drift).
                {
                    Terrain t0 = ResolveTerrainAt(pts[0]);
                    if (t0 != null)
                        pts[0] = SnapToTerrain(t0, pts[0], fenceTerrainOffsetY);

                    Terrain tN = ResolveTerrainAt(pts[pts.Count - 1]);
                    if (tN != null)
                        pts[pts.Count - 1] = SnapToTerrain(tN, pts[pts.Count - 1], fenceTerrainOffsetY);
                }
            }
        }

        private bool TrySampleFenceBoundaryAtDistance(
            bool isLeft,
            float distanceMeters,
            List<SkiRunLine> avoidRuns,
            out Vector3 worldPoint)
        {
            worldPoint = default;

            if (pointsWorld == null || pointsWorld.Count < 2) return false;

            float runLen = bakedLengthMeters;
            if (runLen <= 0.001f) return false;

            distanceMeters = Mathf.Clamp(distanceMeters, 0f, runLen);

            float total = 0f;

            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                float segStartDist = total;
                float segEndDist = total + segLen;

                if (distanceMeters > segEndDist)
                {
                    total += segLen;
                    continue;
                }

                float tFromSegStart = (distanceMeters - segStartDist);
                float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(tFromSegStart / segLen);

                Vector3 tangent = (b - a) / segLen;
                Vector3 center = a + tangent * tFromSegStart;

                Terrain terrainAtCenter = ResolveTerrainAt(center);
                if (terrainAtCenter == null)
                    return false;

                Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);
                Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                Vector3 lateral = Vector3.Cross(up, smoothTangent);
                if (lateral.sqrMagnitude < 0.0001f)
                    lateral = Vector3.Cross(Vector3.up, smoothTangent);
                lateral.Normalize();

                float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(seg, segT));
                float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                Vector3 edgeUnsnap = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft, avoidRuns, heightOffset: 0f);
                Terrain tEdge = ResolveTerrainAt(edgeUnsnap) ?? terrainAtCenter;
                worldPoint = (tEdge != null) ? SnapToTerrain(tEdge, edgeUnsnap, 0f) : edgeUnsnap;

                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper: sample the run boundary point (left/right) at a given distance along the run.
        /// </summary>
        public bool TryGetFenceBoundaryPointAtDistance(bool isLeft, float distanceMeters, out Vector3 worldPoint)
        {
            BakeMetrics();
            return TrySampleFenceBoundaryAtDistance(isLeft, distanceMeters, avoidRuns: null, out worldPoint);
        }

        /// <summary>
        /// Editor helper: sample a boundary polyline along the run edge between two distances.
        /// </summary>
        public void GetFenceBoundaryPreviewPolyline(bool isLeft, float startMeters, float endMeters, float spacingMeters, List<Vector3> outWorldPoints)
        {
            BakeMetrics();
            SampleFenceBoundaryPolyline(isLeft, startMeters, endMeters, spacingMeters, avoidRuns: null, outWorldPoints);
        }
#endif

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.Find(name);
            if (t != null) return t;

            var go = new GameObject(name);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Run Flags SubContainer");
#endif
            go.transform.SetParent(parent, false);
            return go.transform;
        }

#if UNITY_EDITOR
        private static void ClearChildrenUndo(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
        }
#else
private static void ClearChildrenRuntime(Transform t)
{
    if (t == null) return;
    for (int i = t.childCount - 1; i >= 0; i--)
    {
        UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }
}
#endif


        private Quaternion ComputeFlagRotation(Terrain terrain, Vector3 pos, Vector3 tangent, Vector3 lateral, bool isLeft)
        {
            if (faceInwards)
            {
                // Left flag faces right (toward center), right flag faces left (toward center)
                Vector3 inward = isLeft ? -lateral : lateral;
                inward.y = 0f;
                if (inward.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(inward.normalized, Vector3.up);
            }

            if (faceDownhill)
            {
                Vector3 downhill = ComputeDownhillDirection(terrain, pos);
                downhill.y = 0f;
                if (downhill.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(downhill.normalized, Vector3.up);
            }

            // Fallback: face along run direction
            Vector3 fwd = tangent;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(fwd.normalized, Vector3.up);

            return Quaternion.identity;
        }

        private float GetWidthMetersAtSample(int segIndex, float segT)
        {
            // segIndex refers to pointsWorld[segIndex] -> pointsWorld[segIndex+1]
            float wA = GetWidthMetersForPoint(segIndex);
            float wB = GetWidthMetersForPoint(segIndex + 1);
            return Mathf.Lerp(wA, wB, Mathf.Clamp01(segT));
        }

        private float GetWidthMetersForPoint(int pointIndex)
        {
            if (widthOverrideMeters != null &&
                pointIndex >= 0 && pointIndex < widthOverrideMeters.Count &&
                widthOverrideMeters[pointIndex] > 0.01f)
            {
                return widthOverrideMeters[pointIndex];
            }

            return runWidthMeters;
        }

        // ------------------------------------------------------------
        // Boundary / Corridor helpers
        // ------------------------------------------------------------

        // Legacy signature preserved for existing flag/gate generation.
        Vector3 FindBoundaryPoint(
            Terrain terrain,
            Vector3 centerGround,
            Vector3 lateralDir,
            float targetHalfWidth,
            bool isLeft,
            List<SkiRunLine> avoidRuns)
        {
            return FindBoundaryPoint(terrain, centerGround, lateralDir, targetHalfWidth, isLeft, avoidRuns, flagHeightOffset);
        }

        // Height offset is injected so fences (and future systems) are not coupled to flagHeightOffset.
        Vector3 FindBoundaryPoint(
            Terrain terrain,
            Vector3 centerGround,
            Vector3 lateralDir,
            float targetHalfWidth,
            bool isLeft,
            List<SkiRunLine> avoidRuns,
            float heightOffset)
        {
            float centerYWithOffset = centerGround.y + heightOffset;
            float dir = isLeft ? 1f : -1f;
            float step = Mathf.Max(0.1f, boundarySearchStepMeters);

            // Basic fallback: simple offset (snapped later by caller if needed)
            Vector3 fallback = centerGround + lateralDir * (dir * targetHalfWidth);

            // Non terrain-aware placement: still try to move outward until we are clear of other runs.
            if (!terrainAwareBoundaries)
            {
                if (avoidRuns == null || avoidRuns.Count == 0)
                    return fallback;

                float maxDist = targetHalfWidth;
                for (float d = targetHalfWidth; d <= maxDist + 0.0001f; d += step)
                {
                    Vector3 cand = centerGround + lateralDir * (dir * d);
                    Vector3 candSnapped = snapSidesToTerrainIndividually
                        ? SnapToTerrain(ResolveTerrainAt(cand) ?? terrain, cand, heightOffset)
                        : new Vector3(cand.x, centerYWithOffset, cand.z);

                    if (!IsInsideOtherRunCorridors(candSnapped, avoidRuns))
                        return cand;
                }

                return fallback;
            }

            // NOTE:
            // Only widen the search when we actually have other runs to avoid.
            // This prevents unexpected boundary drift around intersections when designers
            // are using the simplified highlight-only workflow.
            float maxDistOuter = targetHalfWidth;
            if (avoidRuns != null && avoidRuns.Count > 0)
                maxDistOuter = targetHalfWidth;

            // Two-phase search:
            // 1) Prefer points on/just outside the nominal half-width (and beyond if needed to avoid overlaps).
            // 2) If nothing valid, fall back to the inner search (maintains previous behavior).
            if (TryFindBoundaryCandidate(terrain, centerGround, lateralDir, dir, targetHalfWidth, targetHalfWidth, maxDistOuter, avoidRuns, heightOffset, out Vector3 outerBest))
                return outerBest;

            if (TryFindBoundaryCandidate(terrain, centerGround, lateralDir, dir, targetHalfWidth, step, targetHalfWidth, avoidRuns, heightOffset, out Vector3 innerBest))
                return innerBest;

            return fallback;
        }

        private bool TryFindBoundaryCandidate(
            Terrain terrain,
            Vector3 centerGround,
            Vector3 lateralDir,
            float dir,
            float targetHalfWidth,
            float startDist,
            float endDist,
            List<SkiRunLine> avoidRuns,
            float heightOffset,
            out Vector3 bestUnsnap)
        {
            bestUnsnap = centerGround + lateralDir * (dir * targetHalfWidth);

            float centerYWithOffset = centerGround.y + heightOffset;
            float step = Mathf.Max(0.1f, boundarySearchStepMeters);

            float bestD = -1f;
            float bestScore = float.PositiveInfinity;
            bool found = false;

            // Ensure deterministic ordering.
            if (endDist < startDist)
            {
                float tmp = startDist; startDist = endDist; endDist = tmp;
            }

            int maxSteps = Mathf.CeilToInt((endDist - startDist) / step);
            for (int i = 0; i <= maxSteps; i++)
            {
                float d = startDist + i * step;
                if (d < 0f) continue;
                if (d > endDist + 0.0001f) break;

                Vector3 cand = centerGround + lateralDir * (dir * d);
                Terrain tCand = ResolveTerrainAt(cand) ?? terrain;
                if (tCand == null) continue;

                Vector3 candSnapped = snapSidesToTerrainIndividually
                    ? SnapToTerrain(tCand, cand, heightOffset)
                    : new Vector3(cand.x, centerYWithOffset, cand.z);

                float heightDelta = Mathf.Abs(candSnapped.y - centerYWithOffset);
                if (heightDelta > boundaryMaxHeightDelta)
                    continue;

                Vector3 n = SampleTerrainNormal(tCand, candSnapped);
                float slopeDeg = Vector3.Angle(n, Vector3.up);
                if (slopeDeg > boundaryMaxSlopeDeg)
                    continue;

                if (avoidRuns != null && avoidRuns.Count > 0)
                {
                    if (IsInsideOtherRunCorridors(candSnapped, avoidRuns))
                        continue;
                }

                found = true;

                if (boundaryPreferFurthestValid)
                {
                    if (d > bestD)
                    {
                        bestD = d;
                        bestUnsnap = cand;
                    }
                }
                else
                {
                    float widthErr = Mathf.Abs(targetHalfWidth - d);
                    float score = widthErr * 1.0f + slopeDeg * 0.02f + heightDelta * 0.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestUnsnap = cand;
                    }
                }
            }

            return found;
        }

        // ------------------------------------------------------------
        // Gate Overrides (Editor-authoring)
        // ------------------------------------------------------------
        [Serializable]
        private struct GateOverride
        {
            // Distance along the run (meters). Quantized for stable lookup.
            public float distanceMeters;

            // Optional authored positions (world).
            public Vector3 leftWorld;
            public Vector3 rightWorld;

            // bit0 = left has authored pos, bit1 = right has authored pos
            public byte posMask;

            // bit0 = left locked, bit1 = right locked
            public byte lockMask;

            // If true, enabledMask is applied (otherwise both sides default enabled).
            public bool hasEnabledOverride;

            // bit0 = left enabled, bit1 = right enabled
            public byte enabledMask;

            // If true, this gate exists even if it doesn't match auto spacing.
            public bool isInserted;
        }

        private const byte SIDE_LEFT = 1 << 0;
        private const byte SIDE_RIGHT = 1 << 1;

        private static float QuantizeGateDistance(float dMeters)
        {
            // 1cm quantization is plenty for editor authoring, and stabilizes float comparisons.
            return Mathf.Round(dMeters * 100f) * 0.01f;
        }

        private int FindGateOverrideIndex(float quantizedDistanceMeters)
        {
            if (gateOverrides == null) return -1;
            for (int i = 0; i < gateOverrides.Count; i++)
            {
                if (Mathf.Abs(gateOverrides[i].distanceMeters - quantizedDistanceMeters) < 0.0005f)
                    return i;
            }
            return -1;
        }

        private GateOverride GetOrCreateGateOverride(float quantizedDistanceMeters, out int index)
        {
            index = FindGateOverrideIndex(quantizedDistanceMeters);
            if (index >= 0) return gateOverrides[index];

            if (gateOverrides == null) gateOverrides = new List<GateOverride>();

            var ov = new GateOverride
            {
                distanceMeters = quantizedDistanceMeters,
                enabledMask = (byte)(SIDE_LEFT | SIDE_RIGHT),
                hasEnabledOverride = false,
                posMask = 0,
                lockMask = 0,
                isInserted = false,
                leftWorld = default,
                rightWorld = default,
            };

            gateOverrides.Add(ov);
            index = gateOverrides.Count - 1;
            return ov;
        }

        /// <summary>
        /// Sets an authored position for the gate side at the given distance. This does NOT lock by default.
        /// Locking is a separate action so authored edits can still be adjusted by auto-generation.
        /// </summary>
        public void SetGateOverridePosition(float distanceMeters, bool isLeft, Vector3 worldPos, bool lockPosition = false)
        {
            float q = QuantizeGateDistance(distanceMeters);
            var ov = GetOrCreateGateOverride(q, out int idx);

            if (isLeft)
            {
                ov.leftWorld = worldPos;
                ov.posMask = (byte)(ov.posMask | SIDE_LEFT);
                if (lockPosition) ov.lockMask = (byte)(ov.lockMask | SIDE_LEFT);
            }
            else
            {
                ov.rightWorld = worldPos;
                ov.posMask = (byte)(ov.posMask | SIDE_RIGHT);
                if (lockPosition) ov.lockMask = (byte)(ov.lockMask | SIDE_RIGHT);
            }

            gateOverrides[idx] = ov;
        }

        public void SetGateOverridePairPositions(float distanceMeters, Vector3 leftWorld, Vector3 rightWorld, bool lockPositions = false)
        {
            float q = QuantizeGateDistance(distanceMeters);
            var ov = GetOrCreateGateOverride(q, out int idx);

            ov.leftWorld = leftWorld;
            ov.rightWorld = rightWorld;
            ov.posMask = (byte)(SIDE_LEFT | SIDE_RIGHT);

            if (lockPositions)
                ov.lockMask = (byte)(SIDE_LEFT | SIDE_RIGHT);

            gateOverrides[idx] = ov;
        }

        public void SetGateOverrideLocked(float distanceMeters, bool lockLeft, bool lockRight)
        {
            float q = QuantizeGateDistance(distanceMeters);
            var ov = GetOrCreateGateOverride(q, out int idx);

            byte m = 0;
            if (lockLeft) m |= SIDE_LEFT;
            if (lockRight) m |= SIDE_RIGHT;
            ov.lockMask = m;

            gateOverrides[idx] = ov;
        }

        public void SetGateOverrideEnabled(float distanceMeters, bool? leftEnabled, bool? rightEnabled)
        {
            float q = QuantizeGateDistance(distanceMeters);
            var ov = GetOrCreateGateOverride(q, out int idx);

            ov.hasEnabledOverride = true;

            bool curLeft = (ov.enabledMask & SIDE_LEFT) != 0;
            bool curRight = (ov.enabledMask & SIDE_RIGHT) != 0;

            if (leftEnabled.HasValue) curLeft = leftEnabled.Value;
            if (rightEnabled.HasValue) curRight = rightEnabled.Value;

            byte m = 0;
            if (curLeft) m |= SIDE_LEFT;
            if (curRight) m |= SIDE_RIGHT;
            ov.enabledMask = m;

            gateOverrides[idx] = ov;
        }

        public void ClearGateOverridePositions(float distanceMeters)
        {
            float q = QuantizeGateDistance(distanceMeters);
            int idx = FindGateOverrideIndex(q);
            if (idx < 0) return;

            var ov = gateOverrides[idx];
            ov.posMask = 0;
            ov.lockMask = 0;
            gateOverrides[idx] = ov;
        }

        public void ClearGateOverrideAll(float distanceMeters)
        {
            float q = QuantizeGateDistance(distanceMeters);
            int idx = FindGateOverrideIndex(q);
            if (idx < 0) return;
            gateOverrides.RemoveAt(idx);
        }
        /// <summary>
        /// Editor convenience: removes the gate override at the provided distance (quantized internally).
        /// This is just a thin wrapper over ClearGateOverrideAll so editor tooling can use a clear semantic name.
        /// </summary>
        public void RemoveGate(float distanceMeters)
        {
            ClearGateOverrideAll(distanceMeters);
        }

        /// <summary>
        /// Editor convenience: returns whether a gate side is enabled at this distance.
        /// If there is no enabled override, sides are considered enabled by default.
        /// </summary>
        public bool IsGateSideEnabled(float distanceMeters, bool isLeft)
        {
            float q = QuantizeGateDistance(distanceMeters);
            int idx = FindGateOverrideIndex(q);
            if (idx < 0) return true;

            var ov = gateOverrides[idx];
            if (!ov.hasEnabledOverride) return true;

            byte side = isLeft ? SIDE_LEFT : SIDE_RIGHT;
            return (ov.enabledMask & side) != 0;
        }


        public void AddInsertedGate(float distanceMeters)
        {
            float q = QuantizeGateDistance(distanceMeters);
            var ov = GetOrCreateGateOverride(q, out int idx);
            ov.isInserted = true;

            // Default inserted gates to enabled unless explicitly disabled.
            if (!ov.hasEnabledOverride)
                ov.enabledMask = (byte)(SIDE_LEFT | SIDE_RIGHT);

            gateOverrides[idx] = ov;
        }

        public bool IsInsertedGate(float distanceMeters)
        {
            float q = QuantizeGateDistance(distanceMeters);
            int idx = FindGateOverrideIndex(q);
            return idx >= 0 && gateOverrides[idx].isInserted;
        }

        private void BuildGateDistanceList(float spawnStart, float spawnEnd, List<float> outDistances)
        {
            outDistances.Clear();

            // Auto distances
            float d = spawnStart;
            int safety = 0;
            while (d <= spawnEnd + 0.0005f && safety++ < 20000)
            {
                outDistances.Add(QuantizeGateDistance(d));
                d += flagSpacingMeters;
            }

            if (gateOverrides == null || gateOverrides.Count == 0)
                return;

            // Remove disabled auto gates + add inserted gates.
            var disable = new HashSet<float>();

            for (int i = 0; i < gateOverrides.Count; i++)
            {
                var ov = gateOverrides[i];

                if (ov.isInserted)
                {
                    if (ov.distanceMeters >= spawnStart - 0.001f && ov.distanceMeters <= spawnEnd + 0.001f)
                        outDistances.Add(QuantizeGateDistance(ov.distanceMeters));
                    continue;
                }

                if (ov.hasEnabledOverride && ov.enabledMask == 0)
                    disable.Add(QuantizeGateDistance(ov.distanceMeters));
            }

            if (disable.Count > 0)
                outDistances.RemoveAll(x => disable.Contains(x));

            outDistances.Sort();
            for (int i = outDistances.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(outDistances[i] - outDistances[i - 1]) < 0.0005f)
                    outDistances.RemoveAt(i);
            }
        }

        private void ApplyGateOverridesForDistance(
     float quantizedDMeters,
     Vector3 baseLeft,
     Vector3 baseRight,
     List<SkiRunLine> avoidRuns,
     bool includeOverlapAvoidance,
     ref Vector3 left,
     ref Vector3 right,
     ref byte sideMask)
        {
            int idx = FindGateOverrideIndex(quantizedDMeters);
            if (idx < 0) return;

            var ov = gateOverrides[idx];

            // Enabled override (optional)
            if (ov.hasEnabledOverride)
            {
                sideMask = 0;
                if ((ov.enabledMask & SIDE_LEFT) != 0) sideMask |= SIDE_LEFT;
                if ((ov.enabledMask & SIDE_RIGHT) != 0) sideMask |= SIDE_RIGHT;

                if (sideMask == 0)
                    return;
            }

            // Apply authored positions if present
            if ((ov.posMask & SIDE_LEFT) != 0)
                left = ov.leftWorld;

            if ((ov.posMask & SIDE_RIGHT) != 0)
                right = ov.rightWorld;

            // NOTE:
            // We intentionally DO NOT reject authored positions based on corridor intersection.
            // Intersections are now a highlight-only workflow: the designer decides how to resolve them.
        }

        // -----------------------------
        // Overlap avoidance helpers
        // -----------------------------

        private List<SkiRunLine> CollectPotentialOverlappingRuns()
        {
            var result = new List<SkiRunLine>();

            // In editor, include inactive objects so baking works across staged scenes/prefabs.
            var all = UnityEngine.Object.FindObjectsOfType<SkiRunLine>(true);
            if (all == null || all.Length == 0) return result;

            // Predeclare to satisfy Unity's definite assignment analysis (avoids CS0170).
            Vector2 myMin = Vector2.zero;
            Vector2 myMax = Vector2.zero;

            bool haveMyBounds = TryGetXZBounds(pointsWorld, out myMin, out myMax);
            //float myPad = (runWidthMeters * 0.5f) + Mathf.Max(0f, avoidRunMaxExtraSearchMeters) + Mathf.Max(0f, avoidRunClearanceMeters);

            for (int i = 0; i < all.Length; i++)
            {
                var other = all[i];
                if (other == null || other == this) continue;
                if (other.pointsWorld == null || other.pointsWorld.Count < 2) continue;

                if (haveMyBounds)
                {
                    // Predeclare for the same reason.
                    Vector2 oMin = Vector2.zero;
                    Vector2 oMax = Vector2.zero;

                    if (TryGetXZBounds(other.pointsWorld, out oMin, out oMax))
                    {
                        float pad = other.runWidthMeters * 0.5f;
                        if ((myMax.x + pad) < oMin.x || (myMin.x - pad) > oMax.x ||
                            (myMax.y + pad) < oMin.y || (myMin.y - pad) > oMax.y)
                            continue;
                    }
                }

                result.Add(other);
            }

            return result;
        }

        private bool IsInsideOtherRunCorridors(Vector3 worldPos, List<SkiRunLine> avoidRuns)
        {
            if (avoidRuns == null || avoidRuns.Count == 0) return false;

            for (int i = 0; i < avoidRuns.Count; i++)
            {
                var r = avoidRuns[i];
                if (r == null) continue;
                //if (r.ContainsPointInCorridorXZ(worldPos, avoidRunClearanceMeters, avoidRunVerticalToleranceMeters))
                //    return true;
            }

            return false;
        }

        // --- Overlap-aware gate orientation (editor + bake) ---
        // Behaviour (per user spec):
        // 1) Gate orientation only: when we are intercepting another run, rotate the gate so the
        //    left<->right axis is perpendicular to the intercepted run's local edge direction.
        // 2) If either flag ends up inside the intercepted corridor, relocate the gate outward to
        //    sit at the intercepted boundary (outside corridor + clearance).
        // 3) If the intercepted run already has a gate pair at that boundary location, skip drawing.

        private static Vector2 ToXZ(Vector3 v) => new Vector2(v.x, v.z);

        private bool TryGetInterceptedRunFrame(
            Vector3 worldPos,
            List<SkiRunLine> avoidRuns,
            out SkiRunLine hitRun,
            out int hitSegIndex,
            out float hitSegT,
            out Vector3 hitClosest,
            out Vector3 edgeNormalWorld)
        {
            hitRun = null;
            hitSegIndex = -1;
            hitSegT = 0f;
            hitClosest = default;
            edgeNormalWorld = Vector3.right;

            if (avoidRuns == null || avoidRuns.Count == 0)
                return false;

            float bestD2 = float.PositiveInfinity;

            for (int i = 0; i < avoidRuns.Count; i++)
            {
                var r = avoidRuns[i];
                if (r == null) continue;

                //// Corridor membership test includes clearance + vertical tolerance.
                //if (!r.ContainsPointInCorridorXZ(worldPos, avoidRunClearanceMeters, avoidRunVerticalToleranceMeters))
                //    continue;

                if (!TryGetClosestPointOnPolylineXZ(r.pointsWorld, worldPos, out int segIdx, out float segT, out Vector3 closest))
                    continue;

                // Choose the closest corridor (XZ) as the "intercepted" run.
                float d2 = (ToXZ(worldPos) - ToXZ(closest)).sqrMagnitude;
                if (d2 >= bestD2)
                    continue;

                // Segment direction on the intercepted run (XZ).
                Vector3 segDir = r.pointsWorld[segIdx + 1] - r.pointsWorld[segIdx];
                segDir.y = 0f;
                if (segDir.sqrMagnitude < 0.000001f)
                    continue;
                segDir.Normalize();

                // Edge normal (perpendicular to run direction in XZ).
                Vector3 n = Vector3.Cross(Vector3.up, segDir);
                n.y = 0f;
                if (n.sqrMagnitude < 0.000001f)
                    continue;
                n.Normalize();

                // Ensure normal points from intercepted run toward the test position.
                Vector3 toP = worldPos - closest;
                toP.y = 0f;
                if (Vector3.Dot(toP, n) < 0f)
                    n = -n;

                bestD2 = d2;
                hitRun = r;
                hitSegIndex = segIdx;
                hitSegT = segT;
                hitClosest = closest;
                edgeNormalWorld = n;
            }

            return hitRun != null;
        }

        // --- Intersection stability helpers -----------------------------------------

        private bool ContainsPointInCorridorXZ_WithExtra(SkiRunLine r, Vector3 p, float extraClearanceMeters)
        {
            // Uses the same corridor membership test but adds a small hysteresis margin.
            return r != null;
            //&& r.ContainsPointInCorridorXZ(
            //    p,
            //    avoidRunClearanceMeters + Mathf.Max(0f, extraClearanceMeters),
            //    avoidRunVerticalToleranceMeters);
        }

        private bool TryGetInterceptedRunFrameStable(
            Vector3 worldPos,
            List<SkiRunLine> avoidRuns,
            SkiRunLine preferredRun,
            float preferStayExtraMeters,
            out SkiRunLine hitRun,
            out int hitSegIndex,
            out float hitSegT,
            out Vector3 hitClosest,
            out Vector3 edgeNormalWorld)
        {
            // 1) Try to “stick” to the preferred run if we’re still inside it (with extra margin).
            if (preferredRun != null && preferredRun != this &&
                ContainsPointInCorridorXZ_WithExtra(preferredRun, worldPos, preferStayExtraMeters) &&
                TryGetClosestPointOnPolylineXZ(preferredRun.pointsWorld, worldPos, out int segIdx, out float segT, out Vector3 closest))
            {
                // Build frame from preferred run segment
                Vector3 segDir = preferredRun.pointsWorld[segIdx + 1] - preferredRun.pointsWorld[segIdx];
                segDir.y = 0f;
                if (segDir.sqrMagnitude > 0.000001f)
                {
                    segDir.Normalize();
                    Vector3 n = Vector3.Cross(Vector3.up, segDir);
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.000001f)
                    {
                        n.Normalize();
                        Vector3 toP = worldPos - closest;
                        toP.y = 0f;
                        if (Vector3.Dot(toP, n) < 0f) n = -n;

                        hitRun = preferredRun;
                        hitSegIndex = segIdx;
                        hitSegT = segT;
                        hitClosest = closest;
                        edgeNormalWorld = n;
                        return true;
                    }
                }
            }

            // 2) Fall back to your existing “choose closest corridor” behavior.
            return TryGetInterceptedRunFrame(worldPos, avoidRuns, out hitRun, out hitSegIndex, out hitSegT, out hitClosest, out edgeNormalWorld);
        }

        private bool TryRefineCorridorBoundary(
            Vector3 outsidePos,
            Vector3 insidePos,
            List<SkiRunLine> avoidRuns,
            SkiRunLine preferredRun,
            int iterations,
            out Vector3 boundaryPos,
            out SkiRunLine hitRun,
            out int hitSegIndex,
            out float hitSegT,
            out Vector3 hitClosest,
            out Vector3 edgeNormalWorld)
        {
            boundaryPos = insidePos;

            hitRun = null;
            hitSegIndex = -1;
            hitSegT = 0f;
            hitClosest = default;
            edgeNormalWorld = Vector3.right;

            // We search for the earliest point along outside->inside that is considered "inside".
            float lo = 0f; // outside
            float hi = 1f; // inside

            bool found = false;

            for (int i = 0; i < Mathf.Max(1, iterations); i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 p = Vector3.Lerp(outsidePos, insidePos, mid);

                bool isInside = TryGetInterceptedRunFrameStable(
                    p, avoidRuns, preferredRun, preferStayExtraMeters: 0f,
                    out SkiRunLine r, out int si, out float st, out Vector3 c, out Vector3 n);

                if (isInside && r != null)
                {
                    found = true;
                    hi = mid;

                    boundaryPos = p;
                    hitRun = r;
                    hitSegIndex = si;
                    hitSegT = st;
                    hitClosest = c;
                    edgeNormalWorld = n;
                }
                else
                {
                    lo = mid;
                }
            }

            return found;
        }

        private bool TryComputeCornerGateOnOtherRunEdge(
            ref Terrain terrainAtCenter,
            Vector3 myCenterGround,
            float myHalfWidth,
            SkiRunLine otherRun,
            int otherSegIndex,
            float otherSegT,
            Vector3 otherClosest,
            Vector3 edgeNormalWorld,
            out Vector3 gateCenterGround,
            out Vector3 cornerAUnsnap,
            out Vector3 cornerBUnsnap,
            out Vector3 otherSegDirXZ)
        {
            gateCenterGround = default;
            cornerAUnsnap = default;
            cornerBUnsnap = default;
            otherSegDirXZ = Vector3.forward;

            if (otherRun == null) return false;
            if (otherRun.pointsWorld == null || otherRun.pointsWorld.Count < 2) return false;
            if (pointsWorld == null || pointsWorld.Count < 2) return false;

            myHalfWidth = Mathf.Max(0.25f, myHalfWidth);

            // 2D helpers
            static float Cross2(Vector2 a, Vector2 b) => (a.x * b.y) - (a.y * b.x);

            static bool TryLineLineIntersectionXZ(
                Vector3 p0World, Vector3 pDirWorld,
                Vector3 q0World, Vector3 qDirWorld,
                out Vector3 hitWorld)
            {
                Vector2 p0 = new Vector2(p0World.x, p0World.z);
                Vector2 pD = new Vector2(pDirWorld.x, pDirWorld.z);
                Vector2 q0 = new Vector2(q0World.x, q0World.z);
                Vector2 qD = new Vector2(qDirWorld.x, qDirWorld.z);

                float denom = Cross2(pD, qD);
                if (Mathf.Abs(denom) < 1e-6f)
                {
                    hitWorld = default;
                    return false;
                }

                Vector2 qp = q0 - p0;
                float t = Cross2(qp, qD) / denom;
                Vector2 hit2 = p0 + pD * t;
                hitWorld = new Vector3(hit2.x, 0f, hit2.y);
                return true;
            }

            // OTHER run segment direction (XZ)
            int otherSi = Mathf.Clamp(otherSegIndex, 0, otherRun.pointsWorld.Count - 2);
            Vector3 otherDir = otherRun.pointsWorld[otherSi + 1] - otherRun.pointsWorld[otherSi];
            otherDir.y = 0f;
            if (otherDir.sqrMagnitude < 0.000001f) return false;
            otherDir.Normalize();
            otherSegDirXZ = otherDir;

            // OTHER edge center (corridor boundary + clearance)
            float otherHalfW = Mathf.Max(0.25f, otherRun.GetWidthMetersAtSample(otherSi, Mathf.Clamp01(otherSegT)) * 0.5f);
            float edgeOffset = otherHalfW;
            //+ Mathf.Max(0f, avoidRunClearanceMeters);
            Vector3 edgeCenter = otherClosest + edgeNormalWorld * edgeOffset;

            // Snap gate center to terrain (ground, no height offset)
            Terrain tGate = ResolveTerrainAt(edgeCenter) ?? terrainAtCenter;
            if (tGate != null)
            {
                gateCenterGround = SnapToTerrain(tGate, edgeCenter, 0f);
                terrainAtCenter = tGate;
            }
            else
            {
                gateCenterGround = edgeCenter;
            }

            // Find THIS run tangent near myCenterGround (XZ)
            if (!TryGetClosestPointOnPolylineXZ(pointsWorld, myCenterGround, out int mySegIndex, out _, out _))
            {
                // Fallback to approximate span if we cannot locate a segment
                float spanFallback = myHalfWidth;
                cornerAUnsnap = gateCenterGround + otherSegDirXZ * spanFallback;
                cornerBUnsnap = gateCenterGround - otherSegDirXZ * spanFallback;
                return true;
            }

            int mySi = Mathf.Clamp(mySegIndex, 0, pointsWorld.Count - 2);
            Vector3 myDir = GetSmoothedTangent(mySi, pointsWorld[mySi], pointsWorld[mySi + 1]);
            myDir.y = 0f;

            if (myDir.sqrMagnitude < 0.000001f)
            {
                myDir = pointsWorld[mySi + 1] - pointsWorld[mySi];
                myDir.y = 0f;
            }

            if (myDir.sqrMagnitude < 0.000001f)
            {
                float spanFallback = myHalfWidth;
                cornerAUnsnap = gateCenterGround + otherSegDirXZ * spanFallback;
                cornerBUnsnap = gateCenterGround - otherSegDirXZ * spanFallback;
                return true;
            }

            myDir.Normalize();

            // THIS run lateral (XZ), used to create the two corridor boundary rails
            Vector3 myLat = new Vector3(-myDir.z, 0f, myDir.x);
            if (myLat.sqrMagnitude < 0.000001f)
            {
                float spanFallback = myHalfWidth;
                cornerAUnsnap = gateCenterGround + otherSegDirXZ * spanFallback;
                cornerBUnsnap = gateCenterGround - otherSegDirXZ * spanFallback;
                return true;
            }
            myLat.Normalize();

            // If nearly parallel, intersection becomes unstable; use angle-aware fallback
            float cos = Mathf.Abs(Vector3.Dot(myDir, otherDir));
            bool nearParallel = cos > 0.985f;

            Vector3 hit0 = default;
            Vector3 hit1 = default;
            bool ok0 = false;
            bool ok1 = false;

            if (!nearParallel)
            {
                Vector3 rail0 = myCenterGround + myLat * myHalfWidth;
                Vector3 rail1 = myCenterGround - myLat * myHalfWidth;

                ok0 = TryLineLineIntersectionXZ(rail0, myDir, edgeCenter, otherDir, out hit0);
                ok1 = TryLineLineIntersectionXZ(rail1, myDir, edgeCenter, otherDir, out hit1);
            }

            if (ok0 && ok1)
            {
                // Order them along the OTHER edge direction for stable A/B
                float s0 = Vector3.Dot(hit0 - edgeCenter, otherDir);
                float s1 = Vector3.Dot(hit1 - edgeCenter, otherDir);

                Vector3 cA = (s0 <= s1) ? hit0 : hit1;
                Vector3 cB = (s0 <= s1) ? hit1 : hit0;

                // Snap corners to terrain ground (no height offset)
                Terrain tA = ResolveTerrainAt(cA) ?? terrainAtCenter;
                if (tA != null) cA = SnapToTerrain(tA, cA, 0f);

                Terrain tB = ResolveTerrainAt(cB) ?? terrainAtCenter;
                if (tB != null) cB = SnapToTerrain(tB, cB, 0f);

                cornerAUnsnap = cA;
                cornerBUnsnap = cB;
                return true;
            }

            // Angle-aware fallback span: myHalfWidth projected onto OTHER edge direction
            float denom = Mathf.Abs(Vector3.Dot(myLat, otherDir));
            denom = Mathf.Max(0.15f, denom);
            float span = Mathf.Max(0.25f, myHalfWidth / denom);
            // Prevent pathological spans when geometry is near-degenerate (can fling flags far off the run).
            // edgeOffset already includes OTHER run half-width + clearance, so it is a safe upper bound.
            float maxSpan = Mathf.Max(0.5f, edgeOffset);
            if (span > maxSpan) span = maxSpan;

            cornerAUnsnap = gateCenterGround + otherSegDirXZ * span;
            cornerBUnsnap = gateCenterGround - otherSegDirXZ * span;

            return true;
        }


        private static bool TryGetClosestPointOnPolylineXZ(
            List<Vector3> pts,
            Vector3 worldPos,
            out int bestSegIndex,
            out float bestSegT,
            out Vector3 bestClosest)
        {
            bestSegIndex = -1;
            bestSegT = 0f;
            bestClosest = default;

            if (pts == null || pts.Count < 2) return false;

            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            float bestD2 = float.PositiveInfinity;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = new Vector2(pts[i].x, pts[i].z);
                Vector2 b = new Vector2(pts[i + 1].x, pts[i + 1].z);
                Vector2 ab = b - a;
                float abLen2 = ab.sqrMagnitude;

                float t = 0f;
                if (abLen2 > 1e-6f)
                    t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLen2);

                Vector2 c2 = a + ab * t;
                float d2 = (p - c2).sqrMagnitude;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    bestSegIndex = i;
                    bestSegT = t;
                    bestClosest = Vector3.LerpUnclamped(pts[i], pts[i + 1], t);
                }
            }

            return bestSegIndex >= 0;
        }

        /// Provides a smoothed tangent direction at a point along a segment (reduces boundary flipping at sharp kinks).
        /// </summary>
        private Vector3 GetSmoothedTangent(int segIndex, Vector3 a, Vector3 b)
        {
            Vector3 t0 = (b - a);
            if (t0.sqrMagnitude < 0.000001f) return Vector3.forward;
            t0.Normalize();

            // Blend with previous and next segment directions if available
            Vector3 sum = t0;
            int count = 1;

            if (segIndex > 0)
            {
                Vector3 p0 = pointsWorld[segIndex - 1];
                Vector3 prev = (a - p0);
                if (prev.sqrMagnitude > 0.000001f)
                {
                    sum += prev.normalized;
                    count++;
                }
            }

            if (segIndex + 2 < pointsWorld.Count)
            {
                Vector3 n1 = pointsWorld[segIndex + 2];
                Vector3 next = (n1 - b);
                if (next.sqrMagnitude > 0.000001f)
                {
                    sum += next.normalized;
                    count++;
                }
            }

            Vector3 t = sum / count;
            if (t.sqrMagnitude < 0.000001f) return t0;
            return t.normalized;
        }

        private Terrain ResolveTerrain()
        {
            // Try find by first point
            if (pointsWorld != null && pointsWorld.Count > 0)
            {
                Terrain t = FindTerrainAt(pointsWorld[0]);
                if (t != null) return t;
            }

            return Terrain.activeTerrain;
        }

        private Terrain ResolveTerrainAt(Vector3 worldPos)
        {
            Terrain tFound = FindTerrainAt(worldPos);
            return tFound != null ? tFound : Terrain.activeTerrain;
        }

        private static Terrain FindTerrainAt(Vector3 worldPos)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains == null) return null;

            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null) continue;

                Vector3 p = t.transform.position;
                Vector3 s = t.terrainData.size;

                bool inside =
                    worldPos.x >= p.x && worldPos.x <= p.x + s.x &&
                    worldPos.z >= p.z && worldPos.z <= p.z + s.z;

                if (inside) return t;
            }

            return null;
        }

        private static bool TryGetXZBounds(List<Vector3> pts, out Vector2 min, out Vector2 max)
        {
            // Always assign to satisfy definite assignment analysis.
            min = Vector2.zero;
            max = Vector2.zero;

            if (pts == null || pts.Count == 0)
                return false;

            float minX = pts[0].x;
            float maxX = pts[0].x;
            float minZ = pts[0].z;
            float maxZ = pts[0].z;

            for (int i = 1; i < pts.Count; i++)
            {
                Vector3 p = pts[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            min = new Vector2(minX, minZ);
            max = new Vector2(maxX, maxZ);
            return true;
        }

        private static float ComputePolylineLength(List<Vector3> pts)
        {
            float len = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
                len += Vector3.Distance(pts[i], pts[i + 1]);
            return len;
        }

        private static void SampleSlopeAlongPolyline(
            Terrain terrain,
            List<Vector3> pts,
            float step,
            out float avgSlopeDeg,
            out float maxSlopeDeg)
        {
            float sum = 0f;
            float max = 0f;
            int count = 0;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.25f, step)));
                for (int s = 0; s <= steps; s++)
                {
                    float t = steps == 0 ? 0f : (float)s / steps;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    Vector3 n = SampleTerrainNormal(terrain, p);
                    float slope = Vector3.Angle(n, Vector3.up);

                    sum += slope;
                    if (slope > max) max = slope;
                    count++;
                }
            }

            avgSlopeDeg = count > 0 ? sum / count : 0f;
            maxSlopeDeg = max;
        }

        private static Vector3 SampleTerrainNormal(Terrain terrain, Vector3 worldPos)
        {
            if (terrain == null || terrain.terrainData == null) return Vector3.up;

            Vector3 tp = worldPos - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            float u = Mathf.Clamp01(tp.x / Mathf.Max(0.0001f, size.x));
            float v = Mathf.Clamp01(tp.z / Mathf.Max(0.0001f, size.z));

            Vector3 n = terrain.terrainData.GetInterpolatedNormal(u, v);
            return n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.up;
        }

        private static Vector3 SnapToTerrain(Terrain terrain, Vector3 worldPos, float yOffset)
        {
            float h = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
            worldPos.y = h + yOffset;
            return worldPos;
        }

        private static Vector3 ComputeDownhillDirection(Terrain terrain, Vector3 worldPos)
        {
            Vector3 n = SampleTerrainNormal(terrain, worldPos);
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, n);
            return downhill.sqrMagnitude > 0.0001f ? downhill.normalized : Vector3.forward;
        }

        private static GameObject InstantiateFlagPrefab(Transform parent, Vector3 pos, Quaternion rot, GameObject prefab)
        {
#if UNITY_EDITOR
            if (prefab != null)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, "Spawn Flag");
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(pos, rot);
                return go;
            }
#endif
            return GameObject.Instantiate(prefab, pos, rot, parent);
        }

        private static void ApplyColorToFlagClothOnly(GameObject flagRoot, Color color)
        {
            if (flagRoot == null) return;

            // Ensure a persistent tint component exists on the flag root.
            var tint = flagRoot.GetComponent<RunFlagClothTint>();
            if (tint == null) tint = flagRoot.AddComponent<RunFlagClothTint>();

            tint.Tint = color; // applies immediately (ExecuteAlways) and persists through Play Mode
        }

#if UNITY_EDITOR

        // per-side validity mask.
        // bit0 = left valid, bit1 = right valid. mask==3 => full gate.
        public void GetFlagPairsPreview(
            List<Vector3> outCenters,
            List<Vector3> outLeft,
            List<Vector3> outRight,
            List<byte> outSideMask,
            List<SkiRunLine> outInterceptedRuns,
            int maxPairs = 500,
            bool includeOverlapAvoidance = true)
        {
            // Backwards-compatible overload.
            GetFlagPairsPreview(
                outCenters, outLeft, outRight,
                outSideMask,
                outInterceptedRuns,
                outOtherClosestOnRun: null,
                outOtherEdgeNormalWorld: null,
                outDistancesMeters: null,
                maxPairs: maxPairs,
                includeOverlapAvoidance: includeOverlapAvoidance);
        }

        // per-side validity mask.
        // bit0 = left valid, bit1 = right valid. mask==3 => full gate.
        /// <summary>
        /// Preview helper used by the editor. Optionally emits additional per-sample debug data for
        /// corridor intersections: closest point on the other run centerline and the other run edge normal.
        /// </summary>
        public void GetFlagPairsPreview(
            List<Vector3> outCenters,
            List<Vector3> outLeft,
            List<Vector3> outRight,
            List<byte> outSideMask,
            List<SkiRunLine> outInterceptedRuns,
            List<Vector3> outOtherClosestOnRun,
            List<Vector3> outOtherEdgeNormalWorld,
            int maxPairs = 500,
            bool includeOverlapAvoidance = true)
        {
            GetFlagPairsPreview(
                outCenters, outLeft, outRight,
                outSideMask,
                outInterceptedRuns,
                outOtherClosestOnRun,
                outOtherEdgeNormalWorld,
                outDistancesMeters: null,
                maxPairs: maxPairs,
                includeOverlapAvoidance: includeOverlapAvoidance);
        }

        // per-side validity mask.
        // bit0 = left valid, bit1 = right valid. mask==3 => full gate.
        /// <summary>
        /// Preview helper used by the editor. Also emits per-gate distance keys (meters along run),
        /// which the editor uses for gate editing and override storage.
        /// </summary>
#if UNITY_EDITOR
        public void GetFlagPairsPreview(
            List<Vector3> outCenters,
            List<Vector3> outLeft,
            List<Vector3> outRight,
            List<byte> outSideMask,
            List<SkiRunLine> outInterceptedRuns,
            List<Vector3> outOtherClosestOnRun,
            List<Vector3> outOtherEdgeNormalWorld,
            List<float> outDistancesMeters,
            int maxPairs = 500,
            bool includeOverlapAvoidance = true)
        {
            if (outCenters == null || outLeft == null || outRight == null) return;

            outCenters.Clear();
            outLeft.Clear();
            outRight.Clear();
            outSideMask?.Clear();
            outInterceptedRuns?.Clear();
            outOtherClosestOnRun?.Clear();
            outOtherEdgeNormalWorld?.Clear();
            outDistancesMeters?.Clear();

            if (pointsWorld == null || pointsWorld.Count < 2) return;

            float runLen = ComputePolylineLength(pointsWorld);
            if (runLen <= 0.001f) return;

            float spawnStart = Mathf.Max(0f, flagStartOffsetMeters);
            float spawnEnd = Mathf.Max(0f, runLen - Mathf.Max(0f, flagEndInsetMeters));
            if (spawnStart >= spawnEnd) return;

            // Cache overlap candidates once.
            // IMPORTANT: We decouple "avoidance for placement" from "intersection info for editor preview".
            // - Placement avoidance stays gated by avoidOtherRunsWhenPlacingFlags (existing behaviour).
            // - Intersection info is produced whenever the caller requested intersection outputs, even if avoidance is off.
            List<SkiRunLine> avoidRunsForPlacement = null;
            if (includeOverlapAvoidance )
                avoidRunsForPlacement = CollectPotentialOverlappingRuns();

            // Intersection info is useful for editor diagnostics even when overlap avoidance is disabled.
            // (This is used for simple tinting + dotted overlap visuals in the editor.)
            bool wantIntersectionInfo =
                (outInterceptedRuns != null || outOtherClosestOnRun != null || outOtherEdgeNormalWorld != null);

            // If we want intersection info but placement avoidance is disabled, still collect runs for intersection tests.
            List<SkiRunLine> runsForIntersection = null;
            if (wantIntersectionInfo)
                runsForIntersection = (avoidRunsForPlacement != null) ? avoidRunsForPlacement : CollectPotentialOverlappingRuns();

            // Distances we will actually preview:
            // - auto spacing
            // - inserted gates
            // - disabled gates removed (if fully disabled)
            var gateDistances = new List<float>(Mathf.Clamp(maxPairs, 8, 4096));
            BuildGateDistanceList(spawnStart, spawnEnd, gateDistances);
            if (gateDistances.Count == 0) return;

            // Keep an XZ list of accepted flags so “moved by intersection logic” gates can be rejected
            // if they violate spacing against already accepted flags.
            var acceptedFlagXZ = new List<Vector2>(Mathf.Max(8, maxPairs) * 2);

            float total = 0f;
            int gateIdx = 0;
            int pairs = 0;

            // Intersection corridor state
            bool prevInsideOtherCorridor = false;

            SkiRunLine prevActiveCorridorRun = null;
            Vector3 lastInsideCenter = Vector3.zero;
            bool haveLastInside = false;
            bool haveLastInsideCenter = true;

            Vector3 lastOutsideCenter = Vector3.zero;
            bool haveLastOutside = false;
            
            SkiRunLine lastRun = null;
            int lastSegIndex = -1;
            float lastSegT = 0f;
            Vector3 lastClosest = default;
            Vector3 lastEdgeN = Vector3.right;

            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                if (gateIdx >= gateDistances.Count)
                    break;

                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f)
                {
                    continue;
                }

                Vector3 tangent = (b - a) / segLen;

                float segStartDist = total;
                float segEndDist = total + segLen;

                // Process all gate distances that fall within this segment.
                while (gateIdx < gateDistances.Count && gateDistances[gateIdx] <= segEndDist + 0.0005f)
                {
                    float dMeters = gateDistances[gateIdx];

                    // If due to float/quantization we land slightly before this segment, skip forward.
                    if (dMeters < segStartDist - 0.0005f)
                    {
                        gateIdx++;
                        continue;
                    }

                    if (pairs++ >= Mathf.Max(1, maxPairs))
                        return;

                    float tFromSegStart = (dMeters - segStartDist);
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(tFromSegStart / segLen);

                    Vector3 center = a + tangent * tFromSegStart;

                    Terrain terrainAtCenter = ResolveTerrainAt(center);
                    if (terrainAtCenter == null)
                    {
                        gateIdx++;
                        continue;
                    }

                    Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);
                    Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                    Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(seg, segT));
                    float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                    Vector3 left = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: true, avoidRunsForPlacement);
                    Vector3 right = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: false, avoidRunsForPlacement);

                    byte mask = 0b11;
                    SkiRunLine interceptedRun = null;

                    Vector3 otherClosest = default;
                    Vector3 otherEdgeN = default;

                    bool movedThisPair = false;
                    const bool ENABLE_CORNER_GATE_AUTHORING = false; // highlight-only workflow

                    // Intersection logic: ENTRY gate on other run edge, hide interior, EXIT gate on other run edge.
                    if (ENABLE_CORNER_GATE_AUTHORING && includeOverlapAvoidance && avoidRunsForPlacement != null && avoidRunsForPlacement.Count > 0)
                    {

                        // Track last outside/inside samples so we can refine the boundary crossing.
                        // (Declare these once outside the segment loop / while loop and persist across iterations.)
                        // Vector3 lastOutsideCenter = default; bool haveLastOutside = false;
                        // Vector3 lastInsideCenter = default;  bool haveLastInsideCenter = false;

                        const float preferStayExtra = 0.75f; // meters of hysteresis to reduce run-flip jitter
                        bool isInside = TryGetInterceptedRunFrameStable(
                            centerGround,
                            avoidRunsForPlacement,
                            preferredRun: prevActiveCorridorRun,
                            preferStayExtraMeters: preferStayExtra,
                            out SkiRunLine hitRun,
                            out int hitSegIndex,
                            out float hitSegT,
                            out Vector3 hitClosest,
                            out Vector3 edgeNormalWorld);

                        // OUTSIDE -> INSIDE (ENTRY)
                        if (!prevInsideOtherCorridor && isInside)
                        {
                            // Refine boundary between lastOutsideCenter and current centerGround (inside)
                            Vector3 boundaryPos = centerGround;
                            SkiRunLine bRun = hitRun;
                            int bSegIndex = hitSegIndex;
                            float bSegT = hitSegT;
                            Vector3 bClosest = hitClosest;
                            Vector3 bEdgeN = edgeNormalWorld;

                            if (haveLastOutside)
                            {
                                // Prefer the newly hit run while refining
                                if (TryRefineCorridorBoundary(
                                    outsidePos: lastOutsideCenter,
                                    insidePos: centerGround,
                                    avoidRuns: avoidRunsForPlacement,
                                    preferredRun: hitRun,
                                    iterations: 7,
                                    out boundaryPos,
                                    out bRun,
                                    out bSegIndex,
                                    out bSegT,
                                    out bClosest,
                                    out bEdgeN))
                                {
                                    // snap refined boundary pos to terrain
                                    boundaryPos = SnapToTerrain(ResolveTerrainAt(boundaryPos) ?? terrainAtCenter, boundaryPos, 0f);
                                }
                            }

                            interceptedRun = bRun;
                            otherClosest = bClosest;
                            otherEdgeN = bEdgeN;

                            if (TryComputeCornerGateOnOtherRunEdge(
                                ref terrainAtCenter,
                                myCenterGround: boundaryPos,     // IMPORTANT: use boundary pos, not coarse sample
                                myHalfWidth: halfW,
                                otherRun: bRun,
                                otherSegIndex: bSegIndex,
                                otherSegT: bSegT,
                                otherClosest: bClosest,
                                edgeNormalWorld: bEdgeN,
                                out Vector3 gateCenterGround,
                                out Vector3 cornerA0,
                                out Vector3 cornerB0,
                                out _))
                            {
                                left = cornerA0;
                                right = cornerB0;
                                centerGround = gateCenterGround;
                                mask = 0b11;
                            }
                            else
                            {
                                mask = 0;
                            }

                            prevInsideOtherCorridor = true;
                            prevActiveCorridorRun = bRun;

                            haveLastInside = true;
                            lastRun = bRun;
                            lastSegIndex = bSegIndex;
                            lastSegT = bSegT;
                            lastClosest = bClosest;
                            lastEdgeN = bEdgeN;

                            haveLastInsideCenter = true;
                            lastInsideCenter = boundaryPos;
                        }
                        // INSIDE
                        else if (prevInsideOtherCorridor)
                        {
                            // If we still consider ourselves inside *any* corridor sample-wise,
                            // we want to stay attached to prevActiveCorridorRun unless we've clearly exited it.
                            bool stillInsidePreferred =
                                prevActiveCorridorRun != null &&
                                ContainsPointInCorridorXZ_WithExtra(prevActiveCorridorRun, centerGround, preferStayExtra);

                            if (stillInsidePreferred)
                            {
                                // hide interior samples
                                mask = 0;
                                interceptedRun = prevActiveCorridorRun;

                                // Update last-inside frame using the preferred run frame (stable)
                                _ = TryGetInterceptedRunFrameStable(
                                    centerGround,
                                    avoidRunsForPlacement,
                                    preferredRun: prevActiveCorridorRun,
                                    preferStayExtraMeters: preferStayExtra,
                                    out SkiRunLine pr,
                                    out int prSeg,
                                    out float prT,
                                    out Vector3 prClosest,
                                    out Vector3 prEdgeN);

                                haveLastInside = true;
                                lastRun = pr;
                                lastSegIndex = prSeg;
                                lastSegT = prT;
                                lastClosest = prClosest;
                                lastEdgeN = prEdgeN;

                                haveLastInsideCenter = true;
                                lastInsideCenter = centerGround;
                            }
                            else
                            {
                                // EXIT: refine boundary between lastInsideCenter and current centerGround (outside)
                                Vector3 boundaryPos = (haveLastInsideCenter ? lastInsideCenter : centerGround);
                                SkiRunLine bRun = lastRun;
                                int bSegIndex = lastSegIndex;
                                float bSegT = lastSegT;
                                Vector3 bClosest = lastClosest;
                                Vector3 bEdgeN = lastEdgeN;

                                if (haveLastInsideCenter)
                                {
                                    // For exit, we want the earliest point along outside->inside; so pass outside=current, inside=lastInside.
                                    if (TryRefineCorridorBoundary(
                                        outsidePos: centerGround,
                                        insidePos: lastInsideCenter,
                                        avoidRuns: avoidRunsForPlacement,
                                        preferredRun: lastRun,
                                        iterations: 7,
                                        out boundaryPos,
                                        out bRun,
                                        out bSegIndex,
                                        out bSegT,
                                        out bClosest,
                                        out bEdgeN))
                                    {
                                        boundaryPos = SnapToTerrain(ResolveTerrainAt(boundaryPos) ?? terrainAtCenter, boundaryPos, 0f);
                                    }
                                    else
                                    {
                                        boundaryPos = lastInsideCenter;
                                    }
                                }

                                interceptedRun = bRun;
                                otherClosest = bClosest;
                                otherEdgeN = bEdgeN;

                                if (haveLastInside && bRun != null &&
                                    TryComputeCornerGateOnOtherRunEdge(
                                        ref terrainAtCenter,
                                        myCenterGround: boundaryPos,  // IMPORTANT: use boundary pos
                                        myHalfWidth: halfW,
                                        otherRun: bRun,
                                        otherSegIndex: bSegIndex,
                                        otherSegT: bSegT,
                                        otherClosest: bClosest,
                                        edgeNormalWorld: bEdgeN,
                                        out Vector3 gateCenterGround,
                                        out Vector3 cornerA0,
                                        out Vector3 cornerB0,
                                        out _))
                                {
                                    left = cornerA0;
                                    right = cornerB0;
                                    centerGround = gateCenterGround;
                                    mask = 0b11;
                                }
                                else
                                {
                                    mask = 0;
                                    interceptedRun = null;
                                }

                                prevInsideOtherCorridor = false;
                                prevActiveCorridorRun = null;
                                haveLastInside = false;
                                haveLastInsideCenter = false;

                                movedThisPair = true;
                            }
                        }

                        // Maintain lastOutside sample when we’re not inside any corridor state.
                        if (!prevInsideOtherCorridor && (!isInside))
                        {
                            haveLastOutside = true;
                            lastOutsideCenter = centerGround;
                        }
                    }
                    else if (runsForIntersection != null && runsForIntersection.Count > 0)
                    {
                        // Intersection INFO only (for editor visuals/handles). Do not move/hide gates.
                        // This ensures intersections appear symmetrically on both runs, even if avoidance is disabled.
                        const float preferStayExtra = 0.75f;

                        bool isInside = TryGetInterceptedRunFrameStable(
                            centerGround,
                            runsForIntersection,
                            preferredRun: prevActiveCorridorRun,
                            preferStayExtraMeters: preferStayExtra,
                            out SkiRunLine hitRun,
                            out int hitSegIndex,
                            out float hitSegT,
                            out Vector3 hitClosest,
                            out Vector3 edgeNormalWorld);

                        if (isInside)
                        {
                            interceptedRun = hitRun;
                            otherClosest = hitClosest;
                            otherEdgeN = edgeNormalWorld;

                            // Maintain state so the preview stays stable across adjacent samples.
                            prevInsideOtherCorridor = true;
                            prevActiveCorridorRun = hitRun;
                            haveLastInside = true;
                            lastRun = hitRun;
                            lastSegIndex = hitSegIndex;
                            lastSegT = hitSegT;
                            lastClosest = hitClosest;
                            lastEdgeN = edgeNormalWorld;
                            haveLastInsideCenter = true;
                            lastInsideCenter = centerGround;
                        }
                        else
                        {
                            prevInsideOtherCorridor = false;
                            prevActiveCorridorRun = null;
                        }
                    }


                    // Snap base positions first (authoring expects flags to sit on terrain).
                    if (snapSidesToTerrainIndividually)
                    {
                        left = SnapToTerrain(ResolveTerrainAt(left) ?? terrainAtCenter, left, flagHeightOffset);
                        right = SnapToTerrain(ResolveTerrainAt(right) ?? terrainAtCenter, right, flagHeightOffset);
                    }
                    else
                    {
                        left.y = centerGround.y + flagHeightOffset;
                        right.y = centerGround.y + flagHeightOffset;
                    }

                    // Apply Gate Overrides (new authoring path)
                    // NOTE: gateDistances are already quantized, but we quantize again for safety.
                    Vector3 baseLeft = left;
                    Vector3 baseRight = right;
                    float qDist = QuantizeGateDistance(dMeters);

                    ApplyGateOverridesForDistance(
                        qDist,
                        baseLeft,
                        baseRight,
                        avoidRunsForPlacement,
                        includeOverlapAvoidance,
                        ref left,
                        ref right,
                        ref mask);

                    // Post-override terrain snap (preview):
                    // Keeps moved/authored gates glued to terrain height consistently.
                    if (snapSidesToTerrainIndividually)
                    {
                        if ((mask & 0b01) != 0)
                            left = SnapToTerrain(ResolveTerrainAt(left) ?? terrainAtCenter, left, flagHeightOffset);

                        if ((mask & 0b10) != 0)
                            right = SnapToTerrain(ResolveTerrainAt(right) ?? terrainAtCenter, right, flagHeightOffset);
                    }
                    else
                    {
                        if ((mask & 0b01) != 0)
                            left.y = centerGround.y + flagHeightOffset;

                        if ((mask & 0b10) != 0)
                            right.y = centerGround.y + flagHeightOffset;
                    }

                    // Spacing rejection for “moved” gates (entry/exit corner gates)
                    if (mask != 0 && movedThisPair)
                    {
                        if (IsTooCloseToExistingFlagsXZ(acceptedFlagXZ, left, right, flagSpacingMeters))
                        {
                            mask = 0;
                            interceptedRun = null;
                            otherClosest = default;
                            otherEdgeN = default;
                        }
                    }

                    if (mask != 0)
                    {
                        acceptedFlagXZ.Add(new Vector2(left.x, left.z));
                        acceptedFlagXZ.Add(new Vector2(right.x, right.z));
                    }

                    outCenters.Add(centerGround + Vector3.up * flagHeightOffset);
                    outLeft.Add(left);
                    outRight.Add(right);
                    outSideMask?.Add(mask);
                    outInterceptedRuns?.Add(interceptedRun);
                    outOtherClosestOnRun?.Add(otherClosest);
                    outOtherEdgeNormalWorld?.Add(otherEdgeN);
                    outDistancesMeters?.Add(dMeters);

                    gateIdx++;
                }

                total += segLen;
            }
        }
#endif

        private static bool IsTooCloseToExistingFlagsXZ(List<Vector2> existingXZ, Vector3 left, Vector3 right, float minSpacing)
        {
            if (existingXZ == null || existingXZ.Count == 0) return false;

            float minSqr = minSpacing * minSpacing;

            Vector2 l = new Vector2(left.x, left.z);
            Vector2 r = new Vector2(right.x, right.z);

            for (int i = 0; i < existingXZ.Count; i++)
            {
                Vector2 e = existingXZ[i];
                if ((e - l).sqrMagnitude < minSqr) return true;
                if ((e - r).sqrMagnitude < minSqr) return true;
            }

            return false;
        }


        public void ResamplePointsWorld(float spacingMeters)
        {
            spacingMeters = Mathf.Clamp(spacingMeters, 1f, 100f);

            if (pointsWorld == null || pointsWorld.Count < 2)
                return;

            var newPts = new List<Vector3>();
            var newWidths = new List<float>();

            // Always keep first point
            newPts.Add(pointsWorld[0]);
            newWidths.Add(-1f);

            float carry = 0f;

            for (int i = 0; i < pointsWorld.Count - 1; i++)
            {
                Vector3 a = pointsWorld[i];
                Vector3 b = pointsWorld[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 dir = (b - a) / segLen;

                float dist = 0f;
                if (i == 0) dist = spacingMeters; // first sample after start

                // Advance along this segment, respecting leftover carry from previous
                float start = (i == 0) ? spacingMeters : (spacingMeters - carry);

                for (float d = start; d < segLen; d += spacingMeters)
                {
                    Vector3 p = a + dir * d;

                    // Snap to terrain tile at this location to avoid floating points.
                    Terrain t = ResolveTerrainAt(p);
                    if (t != null)
                        p = SnapToTerrain(t, p, 0f);

                    newPts.Add(p);
                    newWidths.Add(-1f);
                }

                // Compute carry: how far past the last sample we are at segment end
                float used = (segLen - start);
                if (used < 0f) used = 0f;
                float remainder = used % spacingMeters;
                carry = (remainder <= 0.0001f) ? 0f : (spacingMeters - remainder);
            }

            // Always keep last point (avoid duplicates)
            Vector3 last = pointsWorld[pointsWorld.Count - 1];
            if (newPts.Count == 0 || Vector3.Distance(newPts[newPts.Count - 1], last) > 0.01f)
            {
                Terrain tLast = ResolveTerrainAt(last);
                if (tLast != null)
                    last = SnapToTerrain(tLast, last, 0f);

                newPts.Add(last);
                newWidths.Add(-1f);
            }

            pointsWorld = newPts;
            widthOverrideMeters = newWidths;

            MarkDistanceCacheDirty();
            EditorUtility.SetDirty(this);
        }


#endif

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawRunLine(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRunLine(true);
        }

        private void DrawRunLine(bool selected)
        {
            if (pointsWorld == null || pointsWorld.Count < 2) return;

            Color c = runColor.a > 0f ? runColor : Color.white;
            Gizmos.color = selected ? c : new Color(c.r, c.g, c.b, 1f);

            for (int i = 0; i < pointsWorld.Count - 1; i++)
                Gizmos.DrawLine(pointsWorld[i], pointsWorld[i + 1]);

            // Small point markers when selected
            if (selected)
            {
                float s = 0.35f;
                for (int i = 0; i < pointsWorld.Count; i++)
                    Gizmos.DrawSphere(pointsWorld[i], s);
            }
        }
#endif
    }
}
