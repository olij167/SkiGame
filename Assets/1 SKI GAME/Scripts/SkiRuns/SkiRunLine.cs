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

        [SerializeField, Tooltip("Optional. If empty, the tool will infer a Terrain for sampling.")]
        private Terrain explicitTerrain;

        [Header("Difficulty / Colors")]
        [SerializeField] private RunDifficultyProfileSO difficultyProfile;
        [SerializeField] private SkiRunDifficulty difficulty;
        [SerializeField] private Color runColor = Color.white;
        [SerializeField, Tooltip("If enabled, the run can be classified per-segment (local slope). Flags may be colored per spawn instead of using the baked whole-run color.")]
        private bool classifyPerSegment = false;

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

        [Header("Baked Results (Read Only)")]
        [SerializeField] private float bakedLengthMeters;
        [SerializeField] private float bakedAvgSlopeDeg;
        [SerializeField] private float bakedMaxSlopeDeg;

        // Public read access for future systems
        public string RunName => runName;
        public string RunId => runId;
        public IReadOnlyList<Vector3> PointsWorld => pointsWorld;
        public SkiRunDifficulty Difficulty => difficulty;
        public Color RunColor => runColor;
        public float LengthMeters => bakedLengthMeters;
        public float AvgSlopeDeg => bakedAvgSlopeDeg;
        public float MaxSlopeDeg => bakedMaxSlopeDeg;

        private void Reset()
        {
            EnsureRunId();
        }

        private void OnValidate()
        {
            EnsureRunId();

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

        public void SetRunName(string value)
        {
            runName = string.IsNullOrWhiteSpace(value) ? "Run" : value.Trim();
        }

        public void ClearPoints()
        {
            pointsWorld.Clear();
            widthOverrideMeters.Clear();
        }

        public void AddPointWorld(Vector3 p)
        {
            pointsWorld.Add(p);

            // Maintain alignment
            if (widthOverrideMeters == null) widthOverrideMeters = new List<float>();
            widthOverrideMeters.Add(-1f);
        }

        public bool RemoveLastPoint()
        {
            if (pointsWorld.Count == 0) return false;

            pointsWorld.RemoveAt(pointsWorld.Count - 1);

            if (widthOverrideMeters != null && widthOverrideMeters.Count > 0)
                widthOverrideMeters.RemoveAt(widthOverrideMeters.Count - 1);

            return true;
        }

        public void BakeAndApply()
        {
            BakeMetrics();
            ApplyColorToGeneratedFlags();
        }

        public void BakeMetrics()
        {
            if (difficultyProfile == null)
            {
                runColor = Color.white;
                difficulty = SkiRunDifficulty.Green;
                bakedLengthMeters = 0f;
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;
                return;
            }

            if (pointsWorld == null || pointsWorld.Count < 2)
            {
                runColor = difficultyProfile.GetColor(SkiRunDifficulty.Green);
                difficulty = SkiRunDifficulty.Green;
                bakedLengthMeters = 0f;
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;
                return;
            }

            var terrain = ResolveTerrain();
            if (terrain == null)
            {
                // If no terrain exists, we can still compute length but not slope.
                bakedLengthMeters = ComputePolylineLength(pointsWorld);
                bakedAvgSlopeDeg = 0f;
                bakedMaxSlopeDeg = 0f;
                difficulty = difficultyProfile.Classify(0f, 0f, out runColor);
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
            {
                // Conservative run-level label: steepest sampled slope determines overall difficulty/color.
                difficulty = difficultyProfile.ClassifyValue(bakedMaxSlopeDeg, out runColor);
            }
            else
            {
                difficulty = difficultyProfile.Classify(bakedAvgSlopeDeg, bakedMaxSlopeDeg, out runColor);
            }
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

            // Early out if length is too small for any flags.
            float runLen = bakedLengthMeters;
            float spawnStart = Mathf.Max(0f, flagStartOffsetMeters);
            float spawnEnd = Mathf.Max(0f, runLen - Mathf.Max(0f, flagEndInsetMeters));
            if (runLen <= 0.001f || spawnStart >= spawnEnd) return;

            float total = 0f;
            float nextSpawn = spawnStart;

            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 tangent = (b - a) / segLen; // along-run direction

                float segStartDist = total;
                float segEndDist = total + segLen;

                // If nextSpawn is beyond this segment, skip quickly.
                if (nextSpawn > segEndDist)
                {
                    total += segLen;
                    continue;
                }

                // If nextSpawn falls before this segment, clamp it up to segStartDist.
                if (nextSpawn < segStartDist)
                    nextSpawn = segStartDist;

                while (nextSpawn <= segEndDist && nextSpawn <= spawnEnd)
                {
                    float t = (nextSpawn - segStartDist);
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(t / segLen);

                    Vector3 center = a + tangent * t;

                    // Resolve correct terrain for this sample (fixes floating across multi-tile terrains).
                    Terrain terrainAtCenter = ResolveTerrainAt(center);
                    if (terrainAtCenter == null)
                    {
                        nextSpawn += flagSpacingMeters;
                        continue;
                    }

                    // Center ground (no offset) is used for normals and boundary scoring.
                    Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);

                    // Smoothed tangent reduces lateral flipping at kinks
                    Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                    // Stable lateral: based on terrain normal at center
                    Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    // Width at this sample (lerped from per-point overrides)
                    float widthMeters = Mathf.Max(2f, GetWidthMetersAtSample(seg, segT));
                    float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);

                    // Terrain-aware boundary placement
                    Vector3 left = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: true);
                    Vector3 right = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: false);

                    // Final placement height offset
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

                    Quaternion leftRot = ComputeFlagRotation(terrainAtCenter, left, smoothTangent, lateral, isLeft: true);
                    Quaternion rightRot = ComputeFlagRotation(terrainAtCenter, right, smoothTangent, lateral, isLeft: false);

                    GameObject leftFlag = InstantiateFlagPrefab(leftC, left, leftRot, flagPrefab);
                    GameObject rightFlag = InstantiateFlagPrefab(rightC, right, rightRot, flagPrefab);

                    Color pairColor = runColor;

                    if (classifyPerSegment && difficultyProfile != null)
                    {
                        // Use the same 'up' normal you're already sampling for boundary/lateral stability.
                        float localSlopeDeg = Vector3.Angle(up, Vector3.up);
                        difficultyProfile.ClassifyValue(localSlopeDeg, out pairColor);
                    }

                    ApplyColorToFlagClothOnly(leftFlag, pairColor);
                    ApplyColorToFlagClothOnly(rightFlag, pairColor);

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

        /// <summary>
        /// Finds a good boundary point by searching laterally from center.
        /// Rejects candidates that are too steep or too far in height relative to center.
        /// </summary>
        private Vector3 FindBoundaryPoint(
     Terrain terrain,
     Vector3 centerGround,
     Vector3 lateralDir,
     float targetHalfWidth,
     bool isLeft)
        {
            // Center reference height (what we consider "aligned with the run surface")
            float centerYWithOffset = centerGround.y + flagHeightOffset;

            // Fallback: simple offset (snapped later in RebuildFlags)
            Vector3 fallback = centerGround + lateralDir * (isLeft ? targetHalfWidth : -targetHalfWidth);
            if (!terrainAwareBoundaries)
                return fallback;

            float bestD = -1f;
            float bestScore = float.PositiveInfinity;
            Vector3 best = fallback;

            float dir = isLeft ? 1f : -1f;
            float step = Mathf.Max(0.1f, boundarySearchStepMeters);
            int maxSteps = Mathf.CeilToInt(targetHalfWidth / step);

            for (int i = 1; i <= maxSteps; i++)
            {
                float d = i * step;
                if (d > targetHalfWidth + 0.0001f) break;

                Vector3 cand = centerGround + lateralDir * (dir * d);

                // Resolve terrain at candidate (prevents snapping to wrong tile / wrong normal)
                Terrain tCand = ResolveTerrainAt(cand) ?? terrain;
                if (tCand == null) continue;

                // Candidate surface point (with offset for height comparisons)
                Vector3 candSnapped = snapSidesToTerrainIndividually
                    ? SnapToTerrain(tCand, cand, flagHeightOffset)
                    : new Vector3(cand.x, centerYWithOffset, cand.z);

                float heightDelta = Mathf.Abs(candSnapped.y - centerYWithOffset);
                if (heightDelta > boundaryMaxHeightDelta)
                    continue;

                Vector3 n = SampleTerrainNormal(tCand, candSnapped);
                float slopeDeg = Vector3.Angle(n, Vector3.up);
                if (slopeDeg > boundaryMaxSlopeDeg)
                    continue;

                if (boundaryPreferFurthestValid)
                {
                    if (d > bestD)
                    {
                        bestD = d;
                        best = cand; // return unsnapped; RebuildFlags will snap & apply offset once
                    }
                }
                else
                {
                    float widthErr = Mathf.Abs(targetHalfWidth - d);
                    float score = widthErr * 1.0f + slopeDeg * 0.02f + heightDelta * 0.5f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = cand; // return unsnapped; RebuildFlags will snap & apply offset once
                    }
                }
            }

            return best;
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
            if (explicitTerrain != null) return explicitTerrain;

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
            if (explicitTerrain != null)
            {
                // Use explicit terrain if the point lies on it; otherwise fall back.
                Vector3 p = explicitTerrain.transform.position;
                Vector3 s = explicitTerrain.terrainData.size;
                bool inside =
                    worldPos.x >= p.x && worldPos.x <= p.x + s.x &&
                    worldPos.z >= p.z && worldPos.z <= p.z + s.z;

                if (inside) return explicitTerrain;
            }

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
        public void GetPairPreview(
            List<Vector3> outCenters,
            List<Vector3> outLeft,
            List<Vector3> outRight,
            int maxPairs = 500)
        {
            outCenters.Clear();
            outLeft.Clear();
            outRight.Clear();

            if (pointsWorld == null || pointsWorld.Count < 2) return;

            BakeMetrics();

            float runLen = bakedLengthMeters;
            float spawnStart = Mathf.Max(0f, flagStartOffsetMeters);
            float spawnEnd = Mathf.Max(0f, runLen - Mathf.Max(0f, flagEndInsetMeters));
            if (runLen <= 0.001f || spawnStart >= spawnEnd) return;

            float total = 0f;
            float nextSpawn = spawnStart;
            int pairs = 0;

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
                    if (pairs++ >= maxPairs) return;

                    float t = (nextSpawn - segStartDist);
                    float segT = (segLen < 0.0001f) ? 0f : Mathf.Clamp01(t / segLen);

                    Vector3 center = a + tangent * t;
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

                    Vector3 left = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: true);
                    Vector3 right = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, halfW, isLeft: false);

                    // Snap for preview
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

                    outCenters.Add(centerGround + Vector3.up * flagHeightOffset);
                    outLeft.Add(left);
                    outRight.Add(right);

                    nextSpawn += flagSpacingMeters;
                }

                total += segLen;
            }
        }
#endif

#if UNITY_EDITOR
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
            Vector3 prev = pointsWorld[0];

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

                prev = b;
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

            EditorUtility.SetDirty(this);
        }

        public void AutoDetectWidthOverrides(float anchorSpacingMeters, float maxHalfWidthMeters, bool useMinSide = true)
        {
            anchorSpacingMeters = Mathf.Clamp(anchorSpacingMeters, 5f, 200f);
            maxHalfWidthMeters = Mathf.Clamp(maxHalfWidthMeters, 2f, 200f);

            if (pointsWorld == null || pointsWorld.Count < 2)
                return;

            // Ensure widths list aligned
            if (widthOverrideMeters == null) widthOverrideMeters = new List<float>();
            while (widthOverrideMeters.Count < pointsWorld.Count) widthOverrideMeters.Add(-1f);
            while (widthOverrideMeters.Count > pointsWorld.Count) widthOverrideMeters.RemoveAt(widthOverrideMeters.Count - 1);

            float total = 0f;
            float nextAnchor = 0f;

            for (int seg = 0; seg < pointsWorld.Count - 1; seg++)
            {
                Vector3 a = pointsWorld[seg];
                Vector3 b = pointsWorld[seg + 1];

                float segLen = Vector3.Distance(a, b);
                if (segLen < 0.001f) continue;

                Vector3 tangent = (b - a) / segLen;

                float segStart = total;
                float segEnd = total + segLen;

                // advance anchor
                if (nextAnchor < segStart) nextAnchor = segStart;

                while (nextAnchor <= segEnd)
                {
                    float t = nextAnchor - segStart;
                    float segT = Mathf.Clamp01(t / segLen);

                    Vector3 center = a + tangent * t;

                    Terrain terrainAtCenter = ResolveTerrainAt(center);
                    if (terrainAtCenter == null)
                    {
                        nextAnchor += anchorSpacingMeters;
                        continue;
                    }

                    Vector3 centerGround = SnapToTerrain(terrainAtCenter, center, 0f);
                    Vector3 smoothTangent = GetSmoothedTangent(seg, a, b);

                    Vector3 up = SampleTerrainNormal(terrainAtCenter, centerGround);
                    Vector3 lateral = Vector3.Cross(up, smoothTangent);
                    if (lateral.sqrMagnitude < 0.0001f)
                        lateral = Vector3.Cross(Vector3.up, smoothTangent);
                    lateral.Normalize();

                    // Search outward up to maxHalfWidthMeters on each side.
                    // We temporarily treat maxHalfWidthMeters as the "target" so FindBoundaryPoint can choose the furthest valid.
                    float prevPrefer = boundaryPreferFurthestValid ? 1f : 0f;
                    bool prevPreferBool = boundaryPreferFurthestValid;
                    boundaryPreferFurthestValid = true;

                    Vector3 left = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, maxHalfWidthMeters, true);
                    Vector3 right = FindBoundaryPoint(terrainAtCenter, centerGround, lateral, maxHalfWidthMeters, false);

                    boundaryPreferFurthestValid = prevPreferBool;

                    float leftD = Vector3.Distance(centerGround, left);
                    float rightD = Vector3.Distance(centerGround, right);

                    float half = useMinSide ? Mathf.Min(leftD, rightD) : ((leftD + rightD) * 0.5f);
                    float width = Mathf.Clamp(half * 2f, 2f, 200f);

                    // Apply to the nearest existing point index (since we resample, this is close enough).
                    int nearestIdx = FindNearestPointIndex(centerGround);
                    if (nearestIdx >= 0)
                        widthOverrideMeters[nearestIdx] = width;

                    nextAnchor += anchorSpacingMeters;
                }

                total += segLen;
            }

            EditorUtility.SetDirty(this);
        }

        private int FindNearestPointIndex(Vector3 worldPos)
        {
            if (pointsWorld == null || pointsWorld.Count == 0) return -1;
            int best = 0;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < pointsWorld.Count; i++)
            {
                float d = (pointsWorld[i] - worldPos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
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
            Gizmos.color = selected ? c : new Color(c.r, c.g, c.b, 0.35f);

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
