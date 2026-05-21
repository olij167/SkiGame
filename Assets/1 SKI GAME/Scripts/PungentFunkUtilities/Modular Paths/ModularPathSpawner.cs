using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PungentFunk.Utilities.SceneTools
{

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
    public class ModularPathSpawner : MonoBehaviour,
        IPungentPathPointProvider,
        IPungentCorridorWidthProvider,
        IPungentPathMetadataProvider,
        IPungentPathQueryProvider,
        IPungentGeneratedOutputProvider,
        IPungentSpatialLabelProvider,
        IPungentSpatialBoundsProvider,
        IPungentSpatialVisualizationProvider
    {
        public enum ForwardAxis { X, Z }
        public enum PathMode { Polyline, SmoothCatmullRom }

        [Header("Spatial Authoring Identity")]
        [Tooltip("Stable generic identifier used by optional map, placement, audit, or data export bridges.")]
        public string stablePathId;

        [Tooltip("Human-readable path name shown in generic spatial authoring tools.")]
        public string displayName;

        [Tooltip("Generic category/tag list. Keep project-specific meaning outside the package.")]
        public List<string> categories = new List<string>();

        [Tooltip("Preview color used by generic spatial visualization adapters.")]
        public Color pathColor = new Color(0.25f, 0.85f, 1f, 0.85f);

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
        [Tooltip("Draw the lightweight path preview while editing. Generated prefab rebuilds are controlled separately.")]
        public bool previewWhileEditing = true;

        [Tooltip("If enabled, editor point/settings changes can trigger a rebuild after a short debounce delay.")]
        public bool autoRebuildInEditor = false;

        [Tooltip("Debounce time used by the editor to avoid rebuilding every frame while dragging points.")]
        [Min(0f)]
        public float editorRebuildDebounce = 0.15f;

        [Header("Debug")]
        [Tooltip("Draw path and control-point gizmos.")]
        public bool drawGizmos = true;

        [Tooltip("Draw lightweight gizmos even when this path is not selected. Disabled by default for responsive spatial-authoring scenes.")]
        public bool drawUnselectedGizmo = false;

        [Header("Path/Area Preview")]
        [Tooltip("Expose Preview Corridor Width to generic path/area gizmo adapters. Disabled by default so segment length is not mistaken for corridor width.")]
        public bool exposePreviewCorridorWidth = false;

        [Tooltip("Authoring-only corridor width used by generic path/area previews when Expose Preview Corridor Width is enabled.")]
        [Min(0.01f)]
        public float previewCorridorWidth = 1.5f;

        [Tooltip("Transparency for the authoring corridor ribbon preview.")]
        [Range(0f, 1f)]
        public float previewCorridorAlpha = 0.18f;

        [SerializeField, Tooltip("Local-space control points for this path.")]
        private List<Vector3> localPoints = new List<Vector3>();

        [SerializeField, Tooltip("Root that contains generated path pieces.")]
        [FormerlySerializedAs("segmentsRoot")]
        private Transform generatedRoot;

        [NonSerialized] private float _cachedPrefabLength = -1f;
        [NonSerialized] private GameObject _cachedPrefabForLength;
        [NonSerialized] private ForwardAxis _cachedAxis;
        [NonSerialized] private List<Vector3> _cachedSampledWorldPath;
        [NonSerialized] private List<float> _cachedDistanceTable;
        [NonSerialized] private int _cachedPathHash;
        [NonSerialized] private bool _cachedSampledClosed;
        [NonSerialized] private bool _cachedSampleValid;
        [NonSerialized] private float _cachedTotalLength;

        public const int HighSampledPointWarningThreshold = 1200;
        public const int HighGeneratedObjectWarningThreshold = 500;

        public IReadOnlyList<Vector3> LocalPoints => localPoints;
        public int PointCount => localPoints == null ? 0 : localPoints.Count;
        public string SpatialId => stablePathId;
        public string SpatialDisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public IReadOnlyList<string> SpatialTags => categories;
        public Color SpatialColor => pathColor;
        public int GeneratedOutputCount => ExistingGeneratedRoot != null || segmentPrefab != null || pointPrefab != null ? 1 : 0;

        public Transform GeneratedRoot
        {
            get
            {
                EnsureGeneratedRoot();
                return generatedRoot;
            }
        }

        public Transform ExistingGeneratedRoot
        {
            get
            {
                if (generatedRoot != null)
                    return generatedRoot;

                Transform existing = transform.Find("__GeneratedPathObjects");
                if (existing == null)
                    existing = transform.Find("__FenceSegments");
                return existing;
            }
        }

        public int ExistingGeneratedChildCount
        {
            get
            {
                Transform existing = ExistingGeneratedRoot;
                return existing != null ? existing.childCount : 0;
            }
        }

        // Legacy compatibility for old scripts/editor layouts that still reference earlier segment-path terminology.
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

        private void Reset()
        {
            EnsureStablePathId();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }

        private void OnValidate()
        {
            EnsureStablePathId();
            samplesPerMeter = Mathf.Max(0.1f, samplesPerMeter);
            minSamplesPerSpan = Mathf.Max(2, minSamplesPerSpan);
            manualSegmentLength = Mathf.Max(0.01f, manualSegmentLength);
            fixedSpacing = Mathf.Max(0.01f, fixedSpacing);
            raycastStartHeight = Mathf.Max(0.01f, raycastStartHeight);
            pointTangentBlend = Mathf.Clamp01(pointTangentBlend);
            previewCorridorWidth = Mathf.Max(0.01f, previewCorridorWidth);
            previewCorridorAlpha = Mathf.Clamp01(previewCorridorAlpha);
            _cachedPrefabLength = -1f;
            InvalidatePathCache();
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

        public bool TryGetWidthAt(float normalizedDistance, out float width)
        {
            width = 0f;
            if (!exposePreviewCorridorWidth)
                return false;

            width = Mathf.Max(0.01f, previewCorridorWidth);
            return true;
        }

        public bool TryGetPathMetadata(out PungentPathMetadata metadata)
        {
            metadata = new PungentPathMetadata
            {
                PathId = SpatialId,
                DisplayName = SpatialDisplayName,
                Tags = SpatialTags,
                Color = SpatialColor,
                Projection = PungentSpatialProjectionMode.XZ,
                ClosedLoop = closedLoop,
                ControlPointCount = PointCount
            };

            if (TryGetWidthAt(0.5f, out float width))
            {
                metadata.HasCorridorWidth = true;
                metadata.DefaultCorridorWidth = width;
            }

            if (TryGetSampledPathAndDistanceTable(out List<Vector3> sampled, out bool sampledClosed, out _, out float totalLength))
            {
                metadata.ClosedLoop = sampledClosed;
                metadata.SampledPointCount = sampled == null ? 0 : sampled.Count;
                metadata.TotalLength = totalLength;
                return true;
            }

            return PointCount > 0;
        }

        public bool TrySamplePath(float normalizedDistance, out PungentPathSample sample)
        {
            sample = default;
            if (!TryGetSampledPathAndDistanceTable(out List<Vector3> sampled, out bool sampledClosed, out List<float> cumulative, out float totalLength))
                return false;

            if (totalLength <= 0.0001f)
                return false;

            float clampedNormalized = Mathf.Clamp01(normalizedDistance);
            float distance = clampedNormalized * totalLength;
            Vector3 position = SamplePointAtDistance(sampled, sampledClosed, cumulative, distance, out Vector3 tangent, out int segmentIndex);
            sample = new PungentPathSample
            {
                Position = position,
                Tangent = tangent,
                Distance = distance,
                NormalizedDistance = clampedNormalized,
                SegmentIndex = segmentIndex
            };
            return true;
        }

        public bool TryProjectPoint(Vector3 worldPoint, out PungentPathProjectionResult result)
        {
            result = default;
            if (!TryGetSampledPathAndDistanceTable(out List<Vector3> sampled, out bool sampledClosed, out List<float> cumulative, out float totalLength))
                return false;

            if (sampled == null || sampled.Count < 2 || cumulative == null || cumulative.Count < 2 || totalLength <= 0.0001f)
                return false;

            int segmentCount = sampledClosed ? sampled.Count : sampled.Count - 1;
            float bestSqr = float.PositiveInfinity;
            Vector3 bestPoint = sampled[0];
            Vector3 bestTangent = Vector3.forward;
            float bestDistance = 0f;
            int bestSegment = 0;

            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % sampled.Count;
                Vector3 a = sampled[i];
                Vector3 b = sampled[next];
                Vector3 closest = ClosestPointOnSegment(a, b, worldPoint, out float t);
                float sqr = (closest - worldPoint).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;

                Vector3 tangent = b - a;
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = Vector3.forward;
                else
                    tangent.Normalize();

                float segmentLength = Vector3.Distance(a, b);
                bestSqr = sqr;
                bestPoint = closest;
                bestTangent = tangent;
                bestDistance = Mathf.Clamp(cumulative[i] + segmentLength * t, 0f, totalLength);
                bestSegment = i;
            }

            result = new PungentPathProjectionResult
            {
                Position = bestPoint,
                Tangent = bestTangent,
                Distance = bestDistance,
                NormalizedDistance = Mathf.Clamp01(bestDistance / totalLength),
                SqrDistanceToInput = bestSqr,
                SegmentIndex = bestSegment,
                ClosedLoop = sampledClosed
            };
            return true;
        }

        public bool TryGetSpatialBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            if (!BuildSampledWorldPath(out List<Vector3> sampled, out _))
                return false;

            if (sampled == null || sampled.Count == 0)
                return false;

            bounds = new Bounds(sampled[0], Vector3.zero);
            for (int i = 1; i < sampled.Count; i++)
                bounds.Encapsulate(sampled[i]);

            if (TryGetWidthAt(0.5f, out float width))
                bounds.Expand(Vector3.one * Mathf.Max(0.01f, width));

            return true;
        }

        public bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSpatialBounds(out Bounds bounds))
                return false;

            int sampledCount = 0;
            if (BuildSampledWorldPath(out List<Vector3> sampled, out _))
                sampledCount = sampled != null ? sampled.Count : 0;

            snapshot = new PungentSpatialVisualizationSnapshot
            {
                Kind = PungentSpatialVisualizationKind.Path,
                Owner = this,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
                Color = pathColor,
                Bounds = bounds,
                PointCount = PointCount,
                EstimatedDrawCost = Mathf.Max(PointCount, sampledCount),
                HasWarnings = ExistingGeneratedChildCount >= HighGeneratedObjectWarningThreshold
            };
            return true;
        }

        public bool TryGetGeneratedOutput(int index, out PungentGeneratedOutputDescriptor output)
        {
            output = default;
            if (index != 0 || GeneratedOutputCount <= 0)
                return false;

            int estimated = 0;
            if (TryEstimateGeneratedCounts(out _, out int segmentCount, out int pointCount, out _))
                estimated = segmentCount + pointCount;

            Transform root = ExistingGeneratedRoot;
            output = new PungentGeneratedOutputDescriptor
            {
                StableId = string.IsNullOrWhiteSpace(stablePathId) ? name : stablePathId,
                Kind = "modular-path-generated-objects",
                DisplayName = "Generated Path Objects",
                Root = root,
                ExistingObjectCount = root != null ? root.childCount : 0,
                EstimatedObjectCount = estimated,
                IsPreviewOnly = false,
                MayBeStale = autoRebuildInEditor == false && root != null
            };
            return true;
        }

        public void SetWorldPoint(int index, Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            if (index < 0 || index >= localPoints.Count)
                return;

            localPoints[index] = transform.InverseTransformPoint(world);
            InvalidatePathCache();
        }

        public void AddWorldPoint(Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            localPoints.Add(transform.InverseTransformPoint(world));
            InvalidatePathCache();
        }

        public void InsertWorldPoint(int index, Vector3 world)
        {
            if (localPoints == null)
                localPoints = new List<Vector3>();

            index = Mathf.Clamp(index, 0, localPoints.Count);
            localPoints.Insert(index, transform.InverseTransformPoint(world));
            InvalidatePathCache();
        }

        public void RemovePoint(int index)
        {
            if (localPoints == null || index < 0 || index >= localPoints.Count)
                return;

            localPoints.RemoveAt(index);
            InvalidatePathCache();
        }

        public void ClearPoints()
        {
            localPoints?.Clear();
            InvalidatePathCache();
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
            if (localPoints == null || localPoints.Count < 2 || segmentPrefab == null)
                return;

            int editGroup = !Application.isPlaying && ModularPathSpawnerEditorHooks.BeginEditGroup != null
                ? ModularPathSpawnerEditorHooks.BeginEditGroup("Rebuild Modular Path")
                : -1;

            try
            {
                EnsureGeneratedRoot();
                ClearGenerated();

                if (!TryGetSampledPathAndDistanceTable(out List<Vector3> sampled, out bool sampledClosed, out List<float> cumulative, out float totalLength))
                    return;
                if (totalLength <= 0.0001f)
                    return;

                if (TryEstimateGeneratedCounts(out int sampledPointCount, out int generatedSegmentCount, out int pointPrefabCount, out _))
                {
                    int estimatedObjects = generatedSegmentCount + pointPrefabCount;
                    if (estimatedObjects >= HighGeneratedObjectWarningThreshold)
                    {
                        Debug.LogWarning(
                            $"[PungentFunk Utilities] {name} may generate approximately {estimatedObjects} objects. Consider increasing spacing, reducing samples per meter, or using preview-only while editing.",
                            this);
                    }

                    if (sampledPointCount >= HighSampledPointWarningThreshold)
                    {
                        Debug.LogWarning(
                            $"[PungentFunk Utilities] {name} has {sampledPointCount} sampled path points. Consider reducing Samples Per Meter or Min Samples Per Span if editing becomes sluggish.",
                            this);
                    }
                }

                RebuildGeneratedObjectsWithoutDiff(sampled, sampledClosed, cumulative, totalLength);

                if (!Application.isPlaying)
                    ModularPathSpawnerEditorHooks.SetDirty?.Invoke(this);
            }
            finally
            {
                if (!Application.isPlaying && editGroup >= 0)
                    ModularPathSpawnerEditorHooks.EndEditGroup?.Invoke(editGroup);
            }
        }

        public bool TryEstimateGeneratedCounts(out int sampledPointCount, out int generatedSegmentCount, out int pointPrefabCount, out float totalLength)
        {
            sampledPointCount = 0;
            generatedSegmentCount = 0;
            pointPrefabCount = 0;
            totalLength = 0f;

            if (localPoints == null || localPoints.Count < 2)
                return false;

            pointPrefabCount = pointPrefab != null ? localPoints.Count : 0;

            if (segmentPrefab == null)
                return false;

            if (!TryGetSampledPathAndDistanceTable(out List<Vector3> sampled, out _, out _, out totalLength))
                return false;

            sampledPointCount = sampled == null ? 0 : sampled.Count;
            float segmentLength = GetSegmentPlacementLength();
            if (segmentLength <= 0.0001f || totalLength <= 0.0001f)
                return false;

            float start = Mathf.Max(0f, startOffset);
            if (start >= totalLength)
                return true;

            bool useEndpointPlacement =
                useSockets &&
                placeByEndpointsWhenUsingSockets &&
                TryGetSocketLocalPositions(segmentPrefab, out _, out _);

            if (useEndpointPlacement)
                generatedSegmentCount = Mathf.Max(0, Mathf.CeilToInt((totalLength - start) / segmentLength));
            else
                generatedSegmentCount = Mathf.Max(0, Mathf.FloorToInt((totalLength - start) / Mathf.Max(0.01f, segmentLength)) + 1);

            return true;
        }

        private void RebuildGeneratedObjectsWithoutDiff(List<Vector3> sampled, bool sampledClosed, List<float> cumulative, float totalLength)
        {
            SpawnPointPrefabs();
            // TODO: Future pooling/diff pass should reuse generated children by stable segment/point keys.
            // This helper keeps the existing Undo-safe full rebuild model isolated for that upgrade.
            SpawnSegmentPrefabs(sampled, sampledClosed, cumulative, totalLength);
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
            GameObject instance = null;
            if (!Application.isPlaying)
                instance = ModularPathSpawnerEditorHooks.InstantiatePrefab?.Invoke(prefab, generatedRoot);

            if (instance == null)
                instance = Instantiate(prefab, generatedRoot);

            if (!Application.isPlaying)
                ModularPathSpawnerEditorHooks.RegisterCreatedObject?.Invoke(instance);

            instance.name = objectName;
            return instance;
        }

        private void DestroyGeneratedObject(GameObject go)
        {
            if (go == null)
                return;
            if (!Application.isPlaying)
            {
                if (ModularPathSpawnerEditorHooks.DestroyObject?.Invoke(go) == true)
                    return;

                DestroyImmediate(go);
                return;
            }

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
            if (!TryGetSampledPathAndDistanceTable(out worldPoints, out sampledClosed, out _, out _))
            {
                worldPoints = new List<Vector3>();
                sampledClosed = closedLoop;
                return false;
            }

            return worldPoints != null && worldPoints.Count >= 2;
        }

        private bool TryGetSampledPathAndDistanceTable(out List<Vector3> worldPoints, out bool sampledClosed, out List<float> cumulative, out float totalLength)
        {
            int hash = ComputePathCacheHash();
            if (_cachedSampleValid && _cachedPathHash == hash && _cachedSampledWorldPath != null && _cachedDistanceTable != null)
            {
                worldPoints = _cachedSampledWorldPath;
                sampledClosed = _cachedSampledClosed;
                cumulative = _cachedDistanceTable;
                totalLength = _cachedTotalLength;
                return worldPoints.Count >= 2 && cumulative.Count >= 2;
            }

            if (!BuildSampledWorldPathUncached(out worldPoints, out sampledClosed))
            {
                InvalidatePathCache();
                cumulative = new List<float>();
                totalLength = 0f;
                return false;
            }

            totalLength = BuildDistanceTable(worldPoints, sampledClosed, out cumulative);
            _cachedSampledWorldPath = worldPoints;
            _cachedDistanceTable = cumulative;
            _cachedSampledClosed = sampledClosed;
            _cachedTotalLength = totalLength;
            _cachedPathHash = hash;
            _cachedSampleValid = totalLength > 0.0001f && worldPoints.Count >= 2;
            return _cachedSampleValid;
        }

        private bool BuildSampledWorldPathUncached(out List<Vector3> worldPoints, out bool sampledClosed)
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

        private void InvalidatePathCache()
        {
            _cachedSampleValid = false;
            _cachedTotalLength = 0f;
        }

        private void EnsureStablePathId()
        {
            if (!string.IsNullOrWhiteSpace(stablePathId))
                return;

            stablePathId = Guid.NewGuid().ToString("N");
        }

        private int ComputePathCacheHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (localPoints == null ? 0 : localPoints.Count);
                if (localPoints != null)
                {
                    for (int i = 0; i < localPoints.Count; i++)
                        hash = hash * 31 + localPoints[i].GetHashCode();
                }

                hash = hash * 31 + pathMode.GetHashCode();
                hash = hash * 31 + samplesPerMeter.GetHashCode();
                hash = hash * 31 + minSamplesPerSpan.GetHashCode();
                hash = hash * 31 + closedLoop.GetHashCode();
                hash = hash * 31 + transform.position.GetHashCode();
                hash = hash * 31 + transform.rotation.GetHashCode();
                hash = hash * 31 + transform.lossyScale.GetHashCode();
                return hash;
            }
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
            return SamplePointAtDistance(points, loop, cumulative, distance, out tangent, out _);
        }

        private static Vector3 SamplePointAtDistance(List<Vector3> points, bool loop, List<float> cumulative, float distance, out Vector3 tangent, out int segmentIndex)
        {
            tangent = Vector3.forward;
            segmentIndex = 0;

            if (points == null || points.Count == 0)
                return Vector3.zero;

            if (points.Count == 1 || cumulative == null || cumulative.Count < 2)
                return points[0];

            float total = cumulative[cumulative.Count - 1];
            if (total <= 0.0001f)
                return points[0];

            distance = loop ? Mathf.Repeat(distance, total) : Mathf.Clamp(distance, 0f, total);

            int segCount = loop ? points.Count : points.Count - 1;
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

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point, out float normalized)
        {
            Vector3 ab = b - a;
            float denominator = Vector3.Dot(ab, ab);
            if (denominator <= 0.000001f)
            {
                normalized = 0f;
                return a;
            }

            normalized = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denominator);
            return Vector3.Lerp(a, b, normalized);
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
            return PungentModularOutputGenerationUtility.RotationMappingLocalDirectionToWorld(localDirection, worldDirection, worldUp);
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
            return PungentModularOutputGenerationUtility.ComputePrefabLengthApprox(prefab, axis);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !previewWhileEditing || !drawUnselectedGizmo)
                return;

            DrawPathGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !previewWhileEditing)
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
