using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PungentFunk.Utilities.SceneTools
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif

    /// <summary>
    /// Generic prefab-segment path spawner.
    ///
    /// Use this for fences, rails, barriers, pipes, cables, ropes, decorative borders, guide lines,
    /// modular road edges, or any repeated prefab path. It intentionally has no gameplay-specific
    /// interaction registry; interaction should be handled by normal colliders/layers on the
    /// generated prefabs.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Modular Path Spawner")]
    public class ModularPathSpawner : MonoBehaviour
    {
        public enum ForwardAxis { X, Z }
        public enum PathMode { Polyline, SmoothCatmullRom }

        [Header("Generated Prefabs")]
        [Tooltip("Prefab repeated along the path. Examples: fence segment, rail segment, pipe segment, rope link, barrier section.")]
        [FormerlySerializedAs("fenceSegmentPrefab")]
        public GameObject segmentPrefab;

        [Tooltip("Optional prefab placed at each control point. Examples: post, corner cap, support, marker, junction.")]
        [FormerlySerializedAs("cornerPostPrefab")]
        public GameObject pointPrefab;

        [Header("Path Sampling")]
        [Tooltip("Polyline: straight between control points. Smooth: Catmull-Rom curve sampled into a polyline.")]
        public PathMode pathMode = PathMode.SmoothCatmullRom;

        [Tooltip("Sampling density for Smooth mode. Higher values create smoother curves but more generated sample points.")]
        [Min(0.1f)]
        public float samplesPerMeter = 2.5f;

        [Tooltip("Minimum samples per control-point span in Smooth mode.")]
        [Min(2)]
        public int minSamplesPerSpan = 6;

        [Header("Socket Placement")]
        [Tooltip("If enabled, the tool searches each segment prefab for named start/end socket transforms and uses those to align joins.")]
        public bool useSockets = true;

        [Tooltip("Child transform name marking the segment's start join point.")]
        public string startSocketName = "Start";

        [Tooltip("Child transform name marking the segment's end join point.")]
        public string endSocketName = "End";

        [Tooltip("Optional distance offset along the path before the first segment is placed.")]
        [Min(0f)]
        public float startOffset = 0.0f;

        [Tooltip("If enabled, socketed segments are placed by mapping their start/end sockets to world start/end path positions.")]
        public bool placeByEndpointsWhenUsingSockets = true;

        [Header("Spacing / Length Fallback")]
        [Tooltip("If enabled, spacing is estimated from prefab bounds. If disabled, Fixed Spacing is used.")]
        public bool usePrefabLength = true;

        [Tooltip("Local prefab axis treated as the segment's length direction when sockets are unavailable.")]
        public ForwardAxis prefabLengthAxis = ForwardAxis.Z;

        [Tooltip("Fallback segment length when prefab bounds or sockets cannot be measured reliably.")]
        public float manualSegmentLength = 2.0f;

        [Tooltip("Spacing used between segment centers when Use Prefab Length is disabled.")]
        public float fixedSpacing = 2.0f;

        [Header("Surface Conform")]
        [Tooltip("If enabled, generated pieces are raycast downward onto the configured surface mask.")]
        [FormerlySerializedAs("conformToTerrain")]
        public bool conformToSurface = true;

        [Tooltip("Layer mask used for raycasts when conforming to scene surfaces.")]
        [FormerlySerializedAs("terrainMask")]
        public LayerMask surfaceMask = ~0;

        [Tooltip("Raycast starts from point + up * this value and casts downward.")]
        public float raycastStartHeight = 50f;

        [Tooltip("Additional vertical offset applied after surface sampling.")]
        public float yOffset = 0.0f;

        [Tooltip("If enabled, generated pieces use the sampled surface normal as their up vector. If disabled, pieces remain upright.")]
        [FormerlySerializedAs("alignToTerrainNormal")]
        public bool alignToSurfaceNormal = false;

        [Tooltip("If enabled, segment placement samples both endpoints before computing segment rotation. This reduces seams on uneven surfaces.")]
        [FormerlySerializedAs("sampleTerrainAtSegmentEnds")]
        public bool sampleSurfaceAtSegmentEnds = true;

        [Header("Path Options")]
        [Tooltip("If enabled, the last point connects back to the first point.")]
        public bool closedLoop = false;

        [Tooltip("Controls how point prefabs face between incoming and outgoing directions.")]
        [FormerlySerializedAs("cornerTangentBlend")]
        public float pointTangentBlend = 0.5f;

        [Header("Rebuild")]
        [Tooltip("If enabled, editor point/settings changes can trigger a rebuild after a short debounce delay.")]
        public bool autoRebuildInEditor = true;

        [Tooltip("Debounce time used by the editor to avoid rebuilding every frame while dragging points.")]
        [Min(0f)]
        public float editorRebuildDebounce = 0.15f;

        [Header("Debug")]
        [Tooltip("Draw path and control-point gizmos.")]
        public bool drawGizmos = true;

        [SerializeField, Tooltip("Local-space control points for this path.")]
        private List<Vector3> localPoints = new List<Vector3>();

        [SerializeField, Tooltip("Root that contains generated path pieces.")]
        [FormerlySerializedAs("segmentsRoot")]
        private Transform generatedRoot;

        [NonSerialized] private float _cachedPrefabLength = -1f;
        [NonSerialized] private GameObject _cachedPrefabForLength;
        [NonSerialized] private ForwardAxis _cachedAxis;

    #if UNITY_EDITOR
        [NonSerialized] private Vector3[] _cachedGizmoSampled;
        [NonSerialized] private bool _cachedGizmoClosed;
        [NonSerialized] private int _cachedGizmoKey;
        [NonSerialized] private bool _cachedGizmoValid;
    #endif

        public IReadOnlyList<Vector3> LocalPoints => localPoints;
        public int PointCount => localPoints == null ? 0 : localPoints.Count;

        public Transform GeneratedRoot
        {
            get
            {
                EnsureGeneratedRoot();
                return generatedRoot;
            }
        }

        // Legacy compatibility for old scripts/editor layouts that still reference FencePath terminology.
        [Obsolete("Use segmentPrefab instead.")]
        public GameObject fenceSegmentPrefab { get => segmentPrefab; set => segmentPrefab = value; }

        [Obsolete("Use pointPrefab instead.")]
        public GameObject cornerPostPrefab { get => pointPrefab; set => pointPrefab = value; }

        [Obsolete("Use conformToSurface instead.")]
        public bool conformToTerrain { get => conformToSurface; set => conformToSurface = value; }

        [Obsolete("Use surfaceMask instead.")]
        public LayerMask terrainMask { get => surfaceMask; set => surfaceMask = value; }

        [Obsolete("Use alignToSurfaceNormal instead.")]
        public bool alignToTerrainNormal { get => alignToSurfaceNormal; set => alignToSurfaceNormal = value; }

        [Obsolete("Use sampleSurfaceAtSegmentEnds instead.")]
        public bool sampleTerrainAtSegmentEnds { get => sampleSurfaceAtSegmentEnds; set => sampleSurfaceAtSegmentEnds = value; }

        [Obsolete("Use pointTangentBlend instead.")]
        public float cornerTangentBlend { get => pointTangentBlend; set => pointTangentBlend = value; }

        [Obsolete("Use GeneratedRoot instead.")]
        public Transform SegmentsRoot => GeneratedRoot;

        [Obsolete("Use EnsureGeneratedRoot instead.")]
        public void EnsureSegmentsRoot() => EnsureGeneratedRoot();

        private void OnValidate()
        {
            samplesPerMeter = Mathf.Max(0.1f, samplesPerMeter);
            minSamplesPerSpan = Mathf.Max(2, minSamplesPerSpan);
            manualSegmentLength = Mathf.Max(0.01f, manualSegmentLength);
            fixedSpacing = Mathf.Max(0.01f, fixedSpacing);
            raycastStartHeight = Mathf.Max(0.01f, raycastStartHeight);
            pointTangentBlend = Mathf.Clamp01(pointTangentBlend);
            _cachedPrefabLength = -1f;
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public void EnsureGeneratedRoot()
        {
            if (generatedRoot != null)
                return;

            Transform existing = transform.Find("__GeneratedPathObjects");
            if (existing == null)
                existing = transform.Find("__FenceSegments"); // legacy root name

            if (existing != null)
            {
                generatedRoot = existing;
                if (existing.name == "__FenceSegments")
                    existing.name = "__GeneratedPathObjects";
                return;
            }

            var go = new GameObject("__GeneratedPathObjects");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            generatedRoot = go.transform;
        }

        public Vector3 GetWorldPoint(int index)
        {
            if (localPoints == null || index < 0 || index >= localPoints.Count)
                return transform.position;

            return transform.TransformPoint(localPoints[index]);
        }

        public void SetWorldPoint(int index, Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            if (index < 0 || index >= localPoints.Count)
                return;

            localPoints[index] = transform.InverseTransformPoint(world);
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public void AddWorldPoint(Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            localPoints.Add(transform.InverseTransformPoint(world));
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public void InsertWorldPoint(int index, Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            index = Mathf.Clamp(index, 0, localPoints.Count);
            localPoints.Insert(index, transform.InverseTransformPoint(world));
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public void RemovePoint(int index)
        {
            if (localPoints == null || index < 0 || index >= localPoints.Count)
                return;

            localPoints.RemoveAt(index);
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public void ClearPoints()
        {
            localPoints?.Clear();
    #if UNITY_EDITOR
            _cachedGizmoValid = false;
    #endif
        }

        public float GetFallbackSegmentLength()
        {
            if (!usePrefabLength)
                return Mathf.Max(0.01f, fixedSpacing);

            if (segmentPrefab == null)
                return Mathf.Max(0.01f, manualSegmentLength);

            if (_cachedPrefabForLength != segmentPrefab || _cachedAxis != prefabLengthAxis || _cachedPrefabLength <= 0f)
            {
                _cachedPrefabForLength = segmentPrefab;
                _cachedAxis = prefabLengthAxis;
                _cachedPrefabLength = ComputePrefabLengthApprox(segmentPrefab, prefabLengthAxis);
                if (_cachedPrefabLength <= 0.0001f)
                    _cachedPrefabLength = manualSegmentLength;
            }

            return Mathf.Max(0.01f, _cachedPrefabLength);
        }

        public void ClearGenerated()
        {
            if (generatedRoot == null)
            {
                Transform existing = transform.Find("__GeneratedPathObjects");
                if (existing == null)
                    existing = transform.Find("__FenceSegments");
                generatedRoot = existing;
            }

            if (generatedRoot == null)
                return;

            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                DestroyGeneratedObject(generatedRoot.GetChild(i).gameObject);
        }

        /// <summary>
        /// Rebuilds repeated segment prefabs and optional point prefabs under the generated root.
        /// </summary>
        public void Rebuild()
        {
            EnsureGeneratedRoot();
            ClearGenerated();

            if (localPoints == null || localPoints.Count < 2 || segmentPrefab == null)
                return;

    #if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Modular Path");
    #endif

            if (!BuildSampledWorldPath(out List<Vector3> sampled, out bool sampledClosed))
                return;

            float totalLength = BuildDistanceTable(sampled, sampledClosed, out List<float> cumulative);
            if (totalLength <= 0.0001f)
                return;

            SpawnPointPrefabs();
            SpawnSegmentPrefabs(sampled, sampledClosed, cumulative, totalLength);

    #if UNITY_EDITOR
            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(this);
    #endif
        }

        private void SpawnPointPrefabs()
        {
            if (pointPrefab == null || localPoints == null)
                return;

            int n = localPoints.Count;
            if (n <= 0)
                return;

            Vector3[] controlWorld = new Vector3[n];
            for (int i = 0; i < n; i++)
                controlWorld[i] = transform.TransformPoint(localPoints[i]);

            for (int i = 0; i < n; i++)
            {
                GameObject instance = InstantiateGenerated(pointPrefab, $"__Point_{i:000}");
                if (instance == null)
                    continue;

                Vector3 pos = controlWorld[i];
                Quaternion rot = ComputePointRotation(controlWorld, i);

                if (TrySampleSurface(pos, out RaycastHit hit))
                {
                    pos = hit.point + Vector3.up * yOffset;
                    if (alignToSurfaceNormal)
                        rot = Quaternion.LookRotation(rot * Vector3.forward, hit.normal);
                }
                else
                {
                    pos += Vector3.up * yOffset;
                }

                instance.transform.SetPositionAndRotation(pos, rot);
            }
        }

        private void SpawnSegmentPrefabs(List<Vector3> sampled, bool sampledClosed, List<float> cumulative, float totalLength)
        {
            float segmentLength = GetSegmentPlacementLength();
            if (segmentLength <= 0.0001f)
                return;

            Vector3 socketStart = Vector3.zero;
            Vector3 socketEnd = Vector3.zero;

            bool useEndpointPlacement =
                useSockets &&
                placeByEndpointsWhenUsingSockets &&
                TryGetSocketLocalPositions(segmentPrefab, out socketStart, out socketEnd);

            int index = 0;

            if (useEndpointPlacement)
            {
                for (float startDistance = Mathf.Max(0f, startOffset);
                     startDistance < totalLength - 0.0001f;
                     startDistance += segmentLength)
                {
                    float endDistance = Mathf.Min(startDistance + segmentLength, totalLength);
                    if (endDistance <= startDistance + 0.0001f)
                        break;

                    Vector3 start = SamplePointAtDistance(sampled, sampledClosed, cumulative, startDistance, out _);
                    Vector3 end = SamplePointAtDistance(sampled, sampledClosed, cumulative, endDistance, out _);

                    PlaceSegmentBySockets(index++, start, end, socketStart, socketEnd);
                }
            }
            else
            {
                float spacing = Mathf.Max(0.01f, segmentLength);

                for (float centerDistance = Mathf.Max(0f, startOffset);
                     centerDistance < totalLength + 0.0001f;
                     centerDistance += spacing)
                {
                    Vector3 center = SamplePointAtDistance(sampled, sampledClosed, cumulative, centerDistance, out Vector3 tangent);
                    PlaceSegmentByCenter(index++, center, tangent);
                }
            }
        }

        private float GetSegmentPlacementLength()
        {
            if (useSockets && TryGetSocketLocalPositions(segmentPrefab, out Vector3 a, out Vector3 b))
            {
                float socketLength = Vector3.Distance(a, b);
                if (socketLength > 0.0001f)
                    return socketLength;
            }

            return GetFallbackSegmentLength();
        }

        private void PlaceSegmentBySockets(int index, Vector3 worldStart, Vector3 worldEnd, Vector3 localSocketStart, Vector3 localSocketEnd)
        {
            GameObject instance = InstantiateGenerated(segmentPrefab, $"__Segment_{index:000}");
            if (instance == null)
                return;

            Vector3 start = worldStart;
            Vector3 end = worldEnd;
            Vector3 up = Vector3.up;

            if (conformToSurface && sampleSurfaceAtSegmentEnds)
            {
                bool hitStart = TrySampleSurface(worldStart, out RaycastHit startHit);
                bool hitEnd = TrySampleSurface(worldEnd, out RaycastHit endHit);

                if (hitStart)
                    start = startHit.point + Vector3.up * yOffset;
                else
                    start += Vector3.up * yOffset;

                if (hitEnd)
                    end = endHit.point + Vector3.up * yOffset;
                else
                    end += Vector3.up * yOffset;

                if (alignToSurfaceNormal)
                {
                    if (hitStart && hitEnd)
                        up = (startHit.normal + endHit.normal).normalized;
                    else if (hitStart)
                        up = startHit.normal;
                    else if (hitEnd)
                        up = endHit.normal;
                }
            }
            else
            {
                Vector3 center = (worldStart + worldEnd) * 0.5f;
                if (TrySampleSurface(center, out RaycastHit hit))
                {
                    Vector3 delta = hit.point + Vector3.up * yOffset - center;
                    start += delta;
                    end += delta;
                    if (alignToSurfaceNormal)
                        up = hit.normal;
                }
                else
                {
                    start += Vector3.up * yOffset;
                    end += Vector3.up * yOffset;
                }
            }

            Vector3 worldDir = end - start;
            if (worldDir.sqrMagnitude < 0.000001f)
            {
                DestroyGeneratedObject(instance);
                return;
            }

            Vector3 localDir = localSocketEnd - localSocketStart;
            if (localDir.sqrMagnitude < 0.000001f)
                localDir = prefabLengthAxis == ForwardAxis.X ? Vector3.right : Vector3.forward;

            Quaternion rootRotation = RotationMappingLocalDirectionToWorld(localDir.normalized, worldDir.normalized, up);
            Vector3 rootPosition = start - rootRotation * localSocketStart;

            instance.transform.SetPositionAndRotation(rootPosition, rootRotation);
        }

        private void PlaceSegmentByCenter(int index, Vector3 worldCenter, Vector3 tangent)
        {
            GameObject instance = InstantiateGenerated(segmentPrefab, $"__Segment_{index:000}");
            if (instance == null)
                return;

            Vector3 position = worldCenter;
            Vector3 up = Vector3.up;

            if (TrySampleSurface(worldCenter, out RaycastHit hit))
            {
                position = hit.point + Vector3.up * yOffset;
                if (alignToSurfaceNormal)
                    up = hit.normal;
            }
            else
            {
                position += Vector3.up * yOffset;
            }

            if (tangent.sqrMagnitude < 0.000001f)
                tangent = transform.forward;

            Quaternion rotation = prefabLengthAxis == ForwardAxis.X
                ? RotationMappingLocalDirectionToWorld(Vector3.right, tangent.normalized, up)
                : RotationMappingLocalDirectionToWorld(Vector3.forward, tangent.normalized, up);

            instance.transform.SetPositionAndRotation(position, rotation);
        }

        private GameObject InstantiateGenerated(GameObject prefab, string objectName)
        {
            if (prefab == null)
                return null;

            EnsureGeneratedRoot();

            GameObject instance;
    #if UNITY_EDITOR
            if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(prefab))
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, generatedRoot);
            else
                instance = Instantiate(prefab, generatedRoot);

            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(instance, "Create Generated Path Object");
    #else
            instance = Instantiate(prefab, generatedRoot);
    #endif

            instance.name = objectName;
            return instance;
        }

        private void DestroyGeneratedObject(GameObject go)
        {
            if (go == null)
                return;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(go);
                return;
            }
    #endif
            Destroy(go);
        }

        private bool TrySampleSurface(Vector3 point, out RaycastHit hit)
        {
            hit = default;
            if (!conformToSurface)
                return false;

            float height = Mathf.Max(0.01f, raycastStartHeight);
            Vector3 start = point + Vector3.up * height;
            return Physics.Raycast(start, Vector3.down, out hit, height * 2f, surfaceMask, QueryTriggerInteraction.Ignore);
        }

        public bool BuildSampledWorldPath(out List<Vector3> worldPoints, out bool sampledClosed)
        {
            worldPoints = new List<Vector3>();
            sampledClosed = closedLoop;

            int n = localPoints == null ? 0 : localPoints.Count;
            if (n < 2)
                return false;

            if (pathMode == PathMode.Polyline)
            {
                for (int i = 0; i < n; i++)
                    worldPoints.Add(transform.TransformPoint(localPoints[i]));

                return worldPoints.Count >= 2;
            }

            int spanCount = closedLoop ? n : n - 1;
            if (spanCount <= 0)
                return false;

            List<Vector3> ctrl = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                ctrl.Add(transform.TransformPoint(localPoints[i]));

            for (int i = 0; i < spanCount; i++)
            {
                Vector3 p0 = GetCtrl(ctrl, i - 1, closedLoop);
                Vector3 p1 = GetCtrl(ctrl, i, closedLoop);
                Vector3 p2 = GetCtrl(ctrl, i + 1, closedLoop);
                Vector3 p3 = GetCtrl(ctrl, i + 2, closedLoop);

                float approxSpanLen = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(minSamplesPerSpan, Mathf.CeilToInt(Mathf.Max(0.1f, approxSpanLen) * samplesPerMeter));

                if (i == 0)
                    worldPoints.Add(p1);

                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    worldPoints.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            if (closedLoop && worldPoints.Count > 2 && (worldPoints[worldPoints.Count - 1] - worldPoints[0]).sqrMagnitude < 0.000001f)
                worldPoints.RemoveAt(worldPoints.Count - 1);

            return worldPoints.Count >= 2;
        }

        private static float BuildDistanceTable(List<Vector3> points, bool loop, out List<float> cumulative)
        {
            cumulative = new List<float>(points == null ? 0 : points.Count + 1);
            if (points == null || points.Count < 2)
                return 0f;

            cumulative.Add(0f);
            float total = 0f;
            int segCount = loop ? points.Count : points.Count - 1;

            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % points.Count;
                total += Vector3.Distance(points[i], points[j]);
                cumulative.Add(total);
            }

            return total;
        }

        private static Vector3 SamplePointAtDistance(List<Vector3> points, bool loop, List<float> cumulative, float distance, out Vector3 tangent)
        {
            tangent = Vector3.forward;

            if (points == null || points.Count == 0)
                return Vector3.zero;

            if (points.Count == 1 || cumulative == null || cumulative.Count < 2)
                return points[0];

            float total = cumulative[cumulative.Count - 1];
            if (total <= 0.0001f)
                return points[0];

            distance = loop ? Mathf.Repeat(distance, total) : Mathf.Clamp(distance, 0f, total);

            int segCount = loop ? points.Count : points.Count - 1;
            int segmentIndex = 0;
            for (int i = 0; i < segCount; i++)
            {
                if (distance <= cumulative[i + 1] + 0.0001f)
                {
                    segmentIndex = i;
                    break;
                }
            }

            int nextIndex = (segmentIndex + 1) % points.Count;
            float segmentStart = cumulative[segmentIndex];
            float segmentEnd = cumulative[segmentIndex + 1];
            float span = Mathf.Max(0.0001f, segmentEnd - segmentStart);
            float t = Mathf.Clamp01((distance - segmentStart) / span);

            Vector3 a = points[segmentIndex];
            Vector3 b = points[nextIndex];
            tangent = (b - a);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.forward;
            else
                tangent.Normalize();

            return Vector3.Lerp(a, b, t);
        }

        private Quaternion ComputePointRotation(Vector3[] controlWorld, int index)
        {
            if (controlWorld == null || controlWorld.Length < 2)
                return transform.rotation;

            int n = controlWorld.Length;
            Vector3 current = controlWorld[Mathf.Clamp(index, 0, n - 1)];

            Vector3 incoming = Vector3.zero;
            Vector3 outgoing = Vector3.zero;

            if (index > 0)
                incoming = (current - controlWorld[index - 1]).normalized;
            else if (closedLoop && n > 2)
                incoming = (current - controlWorld[n - 1]).normalized;

            if (index < n - 1)
                outgoing = (controlWorld[index + 1] - current).normalized;
            else if (closedLoop && n > 2)
                outgoing = (controlWorld[0] - current).normalized;

            Vector3 dir;
            if (incoming.sqrMagnitude > 0.0001f && outgoing.sqrMagnitude > 0.0001f)
                dir = Vector3.Slerp(incoming, outgoing, pointTangentBlend).normalized;
            else if (outgoing.sqrMagnitude > 0.0001f)
                dir = outgoing;
            else if (incoming.sqrMagnitude > 0.0001f)
                dir = incoming;
            else
                dir = transform.forward;

            return Quaternion.LookRotation(dir, Vector3.up);
        }

        private static Quaternion RotationMappingLocalDirectionToWorld(Vector3 localDirection, Vector3 worldDirection, Vector3 worldUp)
        {
            if (localDirection.sqrMagnitude < 0.000001f)
                localDirection = Vector3.forward;
            if (worldDirection.sqrMagnitude < 0.000001f)
                worldDirection = Vector3.forward;
            if (worldUp.sqrMagnitude < 0.000001f)
                worldUp = Vector3.up;

            localDirection.Normalize();
            worldDirection.Normalize();
            worldUp.Normalize();

            Quaternion localBasis = Quaternion.LookRotation(localDirection, Vector3.up);
            Quaternion worldBasis = Quaternion.LookRotation(worldDirection, worldUp);
            return worldBasis * Quaternion.Inverse(localBasis);
        }

        private static Vector3 GetCtrl(List<Vector3> points, int index, bool loop)
        {
            int count = points == null ? 0 : points.Count;
            if (count == 0)
                return Vector3.zero;

            if (loop)
            {
                int wrapped = ((index % count) + count) % count;
                return points[wrapped];
            }

            return points[Mathf.Clamp(index, 0, count - 1)];
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f *
                   ((2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private bool TryGetSocketLocalPositions(GameObject prefab, out Vector3 start, out Vector3 end)
        {
            start = Vector3.zero;
            end = Vector3.zero;

            if (prefab == null)
                return false;

            Transform root = prefab.transform;
            Transform startSocket = FindDeepChild(root, string.IsNullOrEmpty(startSocketName) ? "Start" : startSocketName);
            Transform endSocket = FindDeepChild(root, string.IsNullOrEmpty(endSocketName) ? "End" : endSocketName);

            if (startSocket == null || endSocket == null)
                return false;

            start = root.InverseTransformPoint(startSocket.position);
            end = root.InverseTransformPoint(endSocket.position);
            return (end - start).sqrMagnitude > 0.000001f;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform direct = root.GetChild(i);
                if (direct.name == childName)
                    return direct;

                Transform nested = FindDeepChild(direct, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static float ComputePrefabLengthApprox(GameObject prefab, ForwardAxis axis)
        {
            if (prefab == null)
                return -1f;

    #if UNITY_EDITOR
            GameObject temp = null;
            try
            {
                temp = PrefabUtility.IsPartOfPrefabAsset(prefab)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                    : Instantiate(prefab);

                if (temp == null)
                    return -1f;

                temp.hideFlags = HideFlags.HideAndDontSave;
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                temp.transform.localScale = Vector3.one;

                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return -1f;

                Bounds localBounds = new Bounds();
                bool initialized = false;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    Bounds bounds = renderer.bounds;
                    Vector3 center = bounds.center;
                    Vector3 extents = bounds.extents;

                    for (int xi = -1; xi <= 1; xi += 2)
                        for (int yi = -1; yi <= 1; yi += 2)
                            for (int zi = -1; zi <= 1; zi += 2)
                            {
                                Vector3 cornerWorld = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                                Vector3 cornerLocal = temp.transform.InverseTransformPoint(cornerWorld);

                                if (!initialized)
                                {
                                    localBounds = new Bounds(cornerLocal, Vector3.zero);
                                    initialized = true;
                                }
                                else
                                {
                                    localBounds.Encapsulate(cornerLocal);
                                }
                            }
                }

                return axis == ForwardAxis.Z ? localBounds.size.z : localBounds.size.x;
            }
            catch
            {
                return -1f;
            }
            finally
            {
                if (temp != null)
                    DestroyImmediate(temp);
            }
    #else
            MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                return axis == ForwardAxis.Z ? bounds.size.z : bounds.size.x;
            }
            return -1f;
    #endif
        }

    #if UNITY_EDITOR
        private int ComputeGizmoKey()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)pathMode;
                hash = hash * 31 + (closedLoop ? 1 : 0);
                hash = hash * 31 + PointCount;
                if (localPoints != null)
                {
                    for (int i = 0; i < localPoints.Count; i++)
                    {
                        Vector3 p = localPoints[i];
                        hash = hash * 31 + Mathf.RoundToInt(p.x * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(p.y * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(p.z * 1000f);
                    }
                }
                return hash;
            }
        }
    #endif

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            DrawPathGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            DrawPathGizmos(true);
        }

        private void DrawPathGizmos(bool selected)
        {
            int n = PointCount;
            if (n <= 0)
                return;

            Color pointColor = selected ? new Color(0.3f, 1f, 0.7f, 1f) : new Color(0.3f, 0.9f, 1f, 0.75f);
            Color lineColor = selected ? new Color(0.7f, 1f, 1f, 0.9f) : new Color(0.7f, 0.9f, 1f, 0.55f);

            Gizmos.color = pointColor;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = GetWorldPoint(i);
                float size = selected ? 0.18f : 0.12f;
                Gizmos.DrawSphere(p, size);
            }

            if (n < 2)
                return;

            Gizmos.color = lineColor;
            if (BuildSampledWorldPath(out List<Vector3> sampled, out bool sampledClosed))
            {
                for (int i = 0; i < sampled.Count - 1; i++)
                    Gizmos.DrawLine(sampled[i], sampled[i + 1]);

                if (sampledClosed && sampled.Count > 2)
                    Gizmos.DrawLine(sampled[sampled.Count - 1], sampled[0]);
            }
        }
    }

}