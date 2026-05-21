using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentModularOutputSourceMode
    {
        PathCenter,
        PathCorridorLeft,
        PathCorridorRight,
        PathCorridorBothSides,
        AreaBoundary
    }

    [Serializable]
    public class PungentModularOutputGap
    {
        public bool enabled = true;
        public string label = "Gap";
        [Range(0f, 1f)] public float startNormalized;
        [Range(0f, 1f)] public float endNormalized = 0.1f;
    }

    [ExecuteAlways]
    [AddComponentMenu("Utilities/Spatial/Spatial Output Recipe")]
    public class PungentModularSpatialOutput : MonoBehaviour,
        IPungentGeneratedOutputProvider,
        IPungentSpatialBoundsProvider,
        IPungentSpatialVisualizationProvider
    {
        private const string GeneratedRootName = "__GeneratedModularSpatialOutput";

        [Header("Source")]
        [Tooltip("Path or area component used as the generation source.")]
        public Component source;

        [Tooltip("How modular prefabs are placed relative to the source.")]
        public PungentModularOutputSourceMode sourceMode = PungentModularOutputSourceMode.PathCenter;

        [Header("Generated Prefabs")]
        public GameObject segmentPrefab;
        public GameObject pointPrefab;

        [Header("Socket Placement")]
        public bool useSockets = true;
        public string startSocketName = "Start";
        public string endSocketName = "End";
        public bool placeByEndpointsWhenUsingSockets = true;

        [Header("Spacing / Length Fallback")]
        public bool usePrefabLength = true;
        public ModularPathSpawner.ForwardAxis prefabLengthAxis = ModularPathSpawner.ForwardAxis.Z;
        [Min(0.01f)] public float manualSegmentLength = 2.0f;
        [Min(0.01f)] public float fixedSpacing = 2.0f;
        [Min(0f)] public float startOffset = 0f;

        [Header("Surface Conform")]
        public bool conformToSurface = true;
        public LayerMask surfaceMask = ~0;
        [Min(0.01f)] public float raycastStartHeight = 50f;
        public float yOffset = 0f;
        public bool alignToSurfaceNormal;
        public bool sampleSurfaceAtSegmentEnds = true;

        [Header("Gaps")]
        public bool autoGapSelfIntersections = true;
        public bool autoGapCorridorOverlaps = true;
        [Range(0f, 0.05f)] public float autoGapPaddingNormalized = 0.006f;
        public List<PungentModularOutputGap> manualGaps = new List<PungentModularOutputGap>();

        [Header("Preview / Rebuild")]
        public bool previewWhileEditing = true;
        public bool autoRebuildInEditor;
        [Min(0f)] public float editorRebuildDebounce = 0.15f;
        public bool drawGizmos = true;
        public bool drawUnselectedGizmo;

        [SerializeField, Tooltip("Root that contains generated Spatial Output Recipe objects.")]
        private Transform generatedRoot;

        [NonSerialized] private float _cachedPrefabLength = -1f;
        [NonSerialized] private GameObject _cachedPrefabForLength;
        [NonSerialized] private ModularPathSpawner.ForwardAxis _cachedAxis;

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
                return transform.Find(GeneratedRootName);
            }
        }

        public int ExistingGeneratedChildCount
        {
            get
            {
                Transform root = ExistingGeneratedRoot;
                return root != null ? root.childCount : 0;
            }
        }

        public int GeneratedOutputCount => ExistingGeneratedRoot != null || segmentPrefab != null || pointPrefab != null ? 1 : 0;

        private void OnValidate()
        {
            manualSegmentLength = Mathf.Max(0.01f, manualSegmentLength);
            fixedSpacing = Mathf.Max(0.01f, fixedSpacing);
            raycastStartHeight = Mathf.Max(0.01f, raycastStartHeight);
            editorRebuildDebounce = Mathf.Max(0f, editorRebuildDebounce);
            autoGapPaddingNormalized = Mathf.Clamp(autoGapPaddingNormalized, 0f, 0.05f);
            _cachedPrefabLength = -1f;
        }

        public void EnsureGeneratedRoot()
        {
            if (generatedRoot != null)
                return;

            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                generatedRoot = existing;
                return;
            }

            GameObject go = new GameObject(GeneratedRootName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            generatedRoot = go.transform;
        }

        public void ClearGenerated()
        {
            if (generatedRoot == null)
                generatedRoot = transform.Find(GeneratedRootName);
            if (generatedRoot == null)
                return;

            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                DestroyGeneratedObject(generatedRoot.GetChild(i).gameObject);
        }

        public void Rebuild()
        {
            if (segmentPrefab == null && pointPrefab == null)
                return;

            if (!TryBuildSourceLanes(s_SourceLanes) || s_SourceLanes.Count == 0)
                return;

            int editGroup = !Application.isPlaying && ModularPathSpawnerEditorHooks.BeginEditGroup != null
                ? ModularPathSpawnerEditorHooks.BeginEditGroup("Rebuild Spatial Output Recipe")
                : -1;

            try
            {
                EnsureGeneratedRoot();
                ClearGenerated();

                for (int i = 0; i < s_SourceLanes.Count; i++)
                    GenerateLane(s_SourceLanes[i], i);

                if (!Application.isPlaying)
                    ModularPathSpawnerEditorHooks.SetDirty?.Invoke(this);
            }
            finally
            {
                s_SourceLanes.Clear();
                if (!Application.isPlaying && editGroup >= 0)
                    ModularPathSpawnerEditorHooks.EndEditGroup?.Invoke(editGroup);
            }
        }

        public bool TryGetSpatialBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            if (!TryBuildSourceLanes(s_SourceLanes) || s_SourceLanes.Count == 0)
            {
                s_SourceLanes.Clear();
                return false;
            }

            bool initialized = false;
            for (int i = 0; i < s_SourceLanes.Count; i++)
            {
                List<Vector3> points = s_SourceLanes[i].points;
                for (int p = 0; p < points.Count; p++)
                {
                    if (!initialized)
                    {
                        bounds = new Bounds(points[p], Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(points[p]);
                    }
                }
            }

            s_SourceLanes.Clear();
            return initialized;
        }

        public bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSpatialBounds(out Bounds bounds))
                return false;

            snapshot = new PungentSpatialVisualizationSnapshot
            {
                Kind = PungentSpatialVisualizationKind.GeneratedOutput,
                Owner = this,
                DisplayName = name,
                Color = Color.white,
                Bounds = bounds,
                PointCount = ExistingGeneratedChildCount,
                EstimatedDrawCost = ExistingGeneratedChildCount,
                HasWarnings = ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold
            };
            return true;
        }

        public bool TryGetGeneratedOutput(int index, out PungentGeneratedOutputDescriptor output)
        {
            output = default;
            if (index != 0 || GeneratedOutputCount <= 0)
                return false;

            Transform root = ExistingGeneratedRoot;
            output = new PungentGeneratedOutputDescriptor
            {
                StableId = name,
                Kind = "modular-spatial-output",
                DisplayName = "Spatial Output Recipe",
                Root = root,
                ExistingObjectCount = root != null ? root.childCount : 0,
                EstimatedObjectCount = EstimateGeneratedObjectCount(),
                IsPreviewOnly = false,
                MayBeStale = !autoRebuildInEditor && root != null
            };
            return true;
        }

        public int EstimateGeneratedObjectCount()
        {
            if (!TryBuildSourceLanes(s_SourceLanes))
                return 0;

            int total = pointPrefab != null ? CountSourcePoints(s_SourceLanes) : 0;
            float segmentLength = GetSegmentPlacementLength();
            if (segmentPrefab != null && segmentLength > 0.0001f)
            {
                for (int i = 0; i < s_SourceLanes.Count; i++)
                {
                    float length = PungentModularOutputGenerationUtility.BuildDistanceTable(s_SourceLanes[i].points, s_SourceLanes[i].closed, s_DistanceScratch);
                    total += Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0f, length - startOffset) / segmentLength));
                    s_DistanceScratch.Clear();
                }
            }

            s_SourceLanes.Clear();
            return total;
        }

        internal bool TryBuildSourceLanes(List<PungentModularOutputGenerationUtility.Lane> results)
        {
            if (results == null)
                return false;

            results.Clear();
            Component resolved = ResolveSource();
            if (resolved == null)
                return false;

            if (sourceMode == PungentModularOutputSourceMode.AreaBoundary)
                return TryBuildAreaBoundaryLane(resolved, results);

            return TryBuildPathLanes(resolved, results);
        }

        private Component ResolveSource()
        {
            if (source != null)
                return source;

            ModularPathSpawner path = GetComponent<ModularPathSpawner>();
            if (path != null)
                return path;

            PungentAreaAuthoringShape area = GetComponent<PungentAreaAuthoringShape>();
            if (area != null)
                return area;

            return null;
        }

        private bool TryBuildPathLanes(Component resolved, List<PungentModularOutputGenerationUtility.Lane> results)
        {
            bool closed = false;
            List<Vector3> center = s_PointScratch;
            center.Clear();

            if (resolved is ModularPathSpawner path)
            {
                if (!path.BuildSampledWorldPath(out List<Vector3> sampled, out closed) || sampled == null || sampled.Count < 2)
                    return false;
                center.AddRange(sampled);
            }
            else if (resolved is IPungentPathPointProvider pointProvider)
            {
                center.AddRange(PungentPathAuthoringAdapterUtility.CopyWorldPoints(pointProvider));
                if (resolved is IPungentPathMetadataProvider metadataProvider &&
                    metadataProvider.TryGetPathMetadata(out PungentPathMetadata metadata))
                    closed = metadata.ClosedLoop;
            }

            if (center.Count < 2)
                return false;

            float width = ResolveCorridorWidth(resolved);
            float halfWidth = Mathf.Max(0.01f, width) * 0.5f;

            switch (sourceMode)
            {
                case PungentModularOutputSourceMode.PathCorridorLeft:
                    results.Add(new PungentModularOutputGenerationUtility.Lane(PungentModularOutputGenerationUtility.BuildOffsetLane(center, closed, -halfWidth), closed, "Left"));
                    break;
                case PungentModularOutputSourceMode.PathCorridorRight:
                    results.Add(new PungentModularOutputGenerationUtility.Lane(PungentModularOutputGenerationUtility.BuildOffsetLane(center, closed, halfWidth), closed, "Right"));
                    break;
                case PungentModularOutputSourceMode.PathCorridorBothSides:
                    results.Add(new PungentModularOutputGenerationUtility.Lane(PungentModularOutputGenerationUtility.BuildOffsetLane(center, closed, -halfWidth), closed, "Left"));
                    results.Add(new PungentModularOutputGenerationUtility.Lane(PungentModularOutputGenerationUtility.BuildOffsetLane(center, closed, halfWidth), closed, "Right"));
                    break;
                default:
                    results.Add(new PungentModularOutputGenerationUtility.Lane(new List<Vector3>(center), closed, "Center"));
                    break;
            }

            return results.Count > 0;
        }

        private bool TryBuildAreaBoundaryLane(Component resolved, List<PungentModularOutputGenerationUtility.Lane> results)
        {
            List<Vector3> points = null;
            if (resolved is PungentAreaAuthoringShape area)
                points = area.GetWorldPolygon();
            else if (resolved is IPungentAreaShapeProvider areaProvider &&
                     areaProvider.TryGetAreaShape(out PungentAreaShape shape) &&
                     shape.WorldPolygon != null)
                points = new List<Vector3>(shape.WorldPolygon);

            if (points == null || points.Count < 2)
                return false;

            results.Add(new PungentModularOutputGenerationUtility.Lane(points, true, "Area Boundary"));
            return true;
        }

        private float ResolveCorridorWidth(Component resolved)
        {
            if (resolved is ModularPathSpawner path)
                return Mathf.Max(0.01f, path.previewCorridorWidth);

            if (resolved is IPungentCorridorWidthProvider widthProvider &&
                widthProvider.TryGetWidthAt(0.5f, out float width))
                return Mathf.Max(0.01f, width);

            return 1f;
        }

        private void GenerateLane(PungentModularOutputGenerationUtility.Lane lane, int laneIndex)
        {
            if (lane.points == null || lane.points.Count < 2)
                return;

            float totalLength = PungentModularOutputGenerationUtility.BuildDistanceTable(lane.points, lane.closed, s_DistanceScratch);
            if (totalLength <= 0.0001f)
            {
                s_DistanceScratch.Clear();
                return;
            }

            BuildResolvedGaps(lane, totalLength, s_DistanceScratch, s_GapScratch);

            if (pointPrefab != null)
                SpawnPointPrefabs(lane, laneIndex, totalLength, s_DistanceScratch, s_GapScratch);

            if (segmentPrefab != null)
            {
                SpawnSegmentPrefabs(lane, laneIndex, totalLength, s_DistanceScratch, s_GapScratch);
                SpawnAutomaticGapBridges(lane, laneIndex, totalLength, s_DistanceScratch, s_GapScratch);
            }

            s_GapScratch.Clear();
            s_DistanceScratch.Clear();
        }

        private void BuildResolvedGaps(
            PungentModularOutputGenerationUtility.Lane lane,
            float totalLength,
            List<float> cumulative,
            List<PungentModularOutputGenerationUtility.ResolvedGap> gaps)
        {
            gaps.Clear();

            if (manualGaps != null)
            {
                for (int i = 0; i < manualGaps.Count; i++)
                {
                    PungentModularOutputGap manual = manualGaps[i];
                    if (manual == null || !manual.enabled)
                        continue;

                    gaps.Add(new PungentModularOutputGenerationUtility.ResolvedGap(
                        manual.startNormalized,
                        manual.endNormalized,
                        manual.label,
                        bridge: false));
                }
            }

            bool autoGap = autoGapSelfIntersections ||
                           (autoGapCorridorOverlaps && sourceMode != PungentModularOutputSourceMode.PathCenter);
            if (autoGap)
            {
                PungentModularOutputGenerationUtility.AppendIntersectionGaps(
                    lane.points,
                    lane.closed,
                    cumulative,
                    totalLength,
                    Mathf.Clamp(autoGapPaddingNormalized, 0.001f, 0.05f),
                    gaps);
            }
        }

        private void SpawnPointPrefabs(
            PungentModularOutputGenerationUtility.Lane lane,
            int laneIndex,
            float totalLength,
            List<float> cumulative,
            List<PungentModularOutputGenerationUtility.ResolvedGap> gaps)
        {
            for (int i = 0; i < lane.points.Count; i++)
            {
                float normalized = totalLength > 0.0001f && i < cumulative.Count
                    ? Mathf.Clamp01(cumulative[i] / totalLength)
                    : 0f;
                if (PungentModularOutputGenerationUtility.IsBlocked(normalized, gaps))
                    continue;

                GameObject instance = InstantiateGenerated(pointPrefab, $"__Point_{laneIndex:00}_{i:000}");
                if (instance == null)
                    continue;

                Vector3 pos = ApplySurface(lane.points[i], out Vector3 up);
                Quaternion rot = ComputePointRotation(lane.points, lane.closed, i, up);
                instance.transform.SetPositionAndRotation(pos, rot);
            }
        }

        private void SpawnSegmentPrefabs(
            PungentModularOutputGenerationUtility.Lane lane,
            int laneIndex,
            float totalLength,
            List<float> cumulative,
            List<PungentModularOutputGenerationUtility.ResolvedGap> gaps)
        {
            float segmentLength = GetSegmentPlacementLength();
            if (segmentLength <= 0.0001f)
                return;

            Vector3 socketStart = Vector3.zero;
            Vector3 socketEnd = Vector3.zero;
            bool useEndpointPlacement =
                useSockets &&
                placeByEndpointsWhenUsingSockets &&
                PungentModularOutputGenerationUtility.TryGetSocketLocalPositions(segmentPrefab, startSocketName, endSocketName, out socketStart, out socketEnd);

            int index = 0;
            if (useEndpointPlacement)
            {
                for (float startDistance = Mathf.Max(0f, startOffset);
                     startDistance < totalLength - 0.0001f;
                     startDistance += segmentLength)
                {
                    float endDistance = Mathf.Min(startDistance + segmentLength, totalLength);
                    float normalized = Mathf.Clamp01(((startDistance + endDistance) * 0.5f) / totalLength);
                    if (PungentModularOutputGenerationUtility.IsBlocked(normalized, gaps))
                        continue;

                    Vector3 start = PungentModularOutputGenerationUtility.SamplePointAtDistance(lane.points, lane.closed, cumulative, startDistance, out _);
                    Vector3 end = PungentModularOutputGenerationUtility.SamplePointAtDistance(lane.points, lane.closed, cumulative, endDistance, out _);
                    PlaceSegmentBySockets($"{laneIndex:00}_{index++:000}", start, end, socketStart, socketEnd);
                }
            }
            else
            {
                float spacing = Mathf.Max(0.01f, segmentLength);
                for (float centerDistance = Mathf.Max(0f, startOffset);
                     centerDistance < totalLength + 0.0001f;
                     centerDistance += spacing)
                {
                    float normalized = Mathf.Clamp01(centerDistance / totalLength);
                    if (PungentModularOutputGenerationUtility.IsBlocked(normalized, gaps))
                        continue;

                    Vector3 center = PungentModularOutputGenerationUtility.SamplePointAtDistance(lane.points, lane.closed, cumulative, centerDistance, out Vector3 tangent);
                    PlaceSegmentByCenter($"{laneIndex:00}_{index++:000}", center, tangent);
                }
            }
        }

        private void SpawnAutomaticGapBridges(
            PungentModularOutputGenerationUtility.Lane lane,
            int laneIndex,
            float totalLength,
            List<float> cumulative,
            List<PungentModularOutputGenerationUtility.ResolvedGap> gaps)
        {
            for (int i = 0; i < gaps.Count; i++)
            {
                PungentModularOutputGenerationUtility.ResolvedGap gap = gaps[i];
                if (!gap.bridge || gap.Wraps)
                    continue;

                Vector3 start = PungentModularOutputGenerationUtility.SamplePointAtDistance(lane.points, lane.closed, cumulative, gap.StartNormalized * totalLength, out _);
                Vector3 end = PungentModularOutputGenerationUtility.SamplePointAtDistance(lane.points, lane.closed, cumulative, gap.EndNormalized * totalLength, out _);
                s_BridgeScratch.Clear();
                s_BridgeScratch.Add(start);
                s_BridgeScratch.Add(end);
                s_BridgeDistanceScratch.Clear();
                float bridgeLength = PungentModularOutputGenerationUtility.BuildDistanceTable(s_BridgeScratch, false, s_BridgeDistanceScratch);
                if (bridgeLength <= 0.0001f)
                    continue;

                float segmentLength = GetSegmentPlacementLength();
                if (segmentLength <= 0.0001f)
                    continue;

                Vector3 socketStart = Vector3.zero;
                Vector3 socketEnd = Vector3.zero;
                bool useEndpointPlacement =
                    useSockets &&
                    placeByEndpointsWhenUsingSockets &&
                    PungentModularOutputGenerationUtility.TryGetSocketLocalPositions(segmentPrefab, startSocketName, endSocketName, out socketStart, out socketEnd);

                int index = 0;
                for (float d = 0f; d < bridgeLength - 0.0001f; d += segmentLength)
                {
                    if (useEndpointPlacement)
                    {
                        Vector3 a = PungentModularOutputGenerationUtility.SamplePointAtDistance(s_BridgeScratch, false, s_BridgeDistanceScratch, d, out _);
                        Vector3 b = PungentModularOutputGenerationUtility.SamplePointAtDistance(s_BridgeScratch, false, s_BridgeDistanceScratch, Mathf.Min(d + segmentLength, bridgeLength), out _);
                        PlaceSegmentBySockets($"{laneIndex:00}_B{i:00}_{index++:000}", a, b, socketStart, socketEnd);
                    }
                    else
                    {
                        Vector3 center = PungentModularOutputGenerationUtility.SamplePointAtDistance(s_BridgeScratch, false, s_BridgeDistanceScratch, d, out Vector3 tangent);
                        PlaceSegmentByCenter($"{laneIndex:00}_B{i:00}_{index++:000}", center, tangent);
                    }
                }
            }

            s_BridgeScratch.Clear();
            s_BridgeDistanceScratch.Clear();
        }

        private float GetSegmentPlacementLength()
        {
            if (useSockets &&
                PungentModularOutputGenerationUtility.TryGetSocketLocalPositions(segmentPrefab, startSocketName, endSocketName, out Vector3 a, out Vector3 b))
            {
                float socketLength = Vector3.Distance(a, b);
                if (socketLength > 0.0001f)
                    return socketLength;
            }

            if (!usePrefabLength)
                return Mathf.Max(0.01f, fixedSpacing);

            if (segmentPrefab == null)
                return Mathf.Max(0.01f, manualSegmentLength);

            if (_cachedPrefabForLength != segmentPrefab || _cachedAxis != prefabLengthAxis || _cachedPrefabLength <= 0f)
            {
                _cachedPrefabForLength = segmentPrefab;
                _cachedAxis = prefabLengthAxis;
                _cachedPrefabLength = PungentModularOutputGenerationUtility.ComputePrefabLengthApprox(segmentPrefab, prefabLengthAxis);
                if (_cachedPrefabLength <= 0.0001f)
                    _cachedPrefabLength = manualSegmentLength;
            }

            return Mathf.Max(0.01f, _cachedPrefabLength);
        }

        private void PlaceSegmentBySockets(string index, Vector3 worldStart, Vector3 worldEnd, Vector3 localSocketStart, Vector3 localSocketEnd)
        {
            GameObject instance = InstantiateGenerated(segmentPrefab, "__Segment_" + index);
            if (instance == null)
                return;

            Vector3 start = worldStart;
            Vector3 end = worldEnd;
            Vector3 up = Vector3.up;

            if (conformToSurface && sampleSurfaceAtSegmentEnds)
            {
                start = ApplySurface(worldStart, out Vector3 startUp);
                end = ApplySurface(worldEnd, out Vector3 endUp);
                up = (startUp + endUp).sqrMagnitude > 0.0001f ? (startUp + endUp).normalized : Vector3.up;
            }
            else
            {
                Vector3 center = (worldStart + worldEnd) * 0.5f;
                Vector3 conformed = ApplySurface(center, out up);
                Vector3 delta = conformed - center;
                start += delta;
                end += delta;
            }

            Vector3 worldDir = end - start;
            if (worldDir.sqrMagnitude < 0.000001f)
            {
                DestroyGeneratedObject(instance);
                return;
            }

            Vector3 localDir = localSocketEnd - localSocketStart;
            if (localDir.sqrMagnitude < 0.000001f)
                localDir = prefabLengthAxis == ModularPathSpawner.ForwardAxis.X ? Vector3.right : Vector3.forward;

            Quaternion rootRotation = PungentModularOutputGenerationUtility.RotationMappingLocalDirectionToWorld(localDir.normalized, worldDir.normalized, up);
            Vector3 rootPosition = start - rootRotation * localSocketStart;
            instance.transform.SetPositionAndRotation(rootPosition, rootRotation);
        }

        private void PlaceSegmentByCenter(string index, Vector3 worldCenter, Vector3 tangent)
        {
            GameObject instance = InstantiateGenerated(segmentPrefab, "__Segment_" + index);
            if (instance == null)
                return;

            Vector3 position = ApplySurface(worldCenter, out Vector3 up);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = transform.forward;

            Quaternion rotation = prefabLengthAxis == ModularPathSpawner.ForwardAxis.X
                ? PungentModularOutputGenerationUtility.RotationMappingLocalDirectionToWorld(Vector3.right, tangent.normalized, up)
                : PungentModularOutputGenerationUtility.RotationMappingLocalDirectionToWorld(Vector3.forward, tangent.normalized, up);

            instance.transform.SetPositionAndRotation(position, rotation);
        }

        private Vector3 ApplySurface(Vector3 position, out Vector3 up)
        {
            up = Vector3.up;
            if (!conformToSurface)
                return position + Vector3.up * yOffset;

            float height = Mathf.Max(0.01f, raycastStartHeight);
            Vector3 start = position + Vector3.up * height;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, height * 2f, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                if (alignToSurfaceNormal)
                    up = hit.normal;
                return hit.point + Vector3.up * yOffset;
            }

            return position + Vector3.up * yOffset;
        }

        private Quaternion ComputePointRotation(List<Vector3> points, bool closed, int index, Vector3 up)
        {
            if (points == null || points.Count < 2)
                return transform.rotation;

            int n = points.Count;
            Vector3 current = points[Mathf.Clamp(index, 0, n - 1)];
            Vector3 incoming = Vector3.zero;
            Vector3 outgoing = Vector3.zero;

            if (index > 0)
                incoming = (current - points[index - 1]).normalized;
            else if (closed && n > 2)
                incoming = (current - points[n - 1]).normalized;

            if (index < n - 1)
                outgoing = (points[index + 1] - current).normalized;
            else if (closed && n > 2)
                outgoing = (points[0] - current).normalized;

            Vector3 dir = outgoing.sqrMagnitude > 0.0001f ? outgoing : incoming;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward;
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;

            return Quaternion.LookRotation(dir.normalized, up.normalized);
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

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !previewWhileEditing || !drawUnselectedGizmo)
                return;

            DrawOutputGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !previewWhileEditing)
                return;

            DrawOutputGizmos(true);
        }

        private void DrawOutputGizmos(bool selected)
        {
            if (!TryBuildSourceLanes(s_SourceLanes))
                return;

            Color previous = Gizmos.color;
            Gizmos.color = selected ? new Color(0.35f, 1f, 0.72f, 0.9f) : new Color(0.35f, 0.9f, 1f, 0.45f);
            for (int l = 0; l < s_SourceLanes.Count; l++)
            {
                List<Vector3> points = s_SourceLanes[l].points;
                if (points == null || points.Count < 2)
                    continue;

                for (int i = 0; i < points.Count - 1; i++)
                    Gizmos.DrawLine(points[i], points[i + 1]);
                if (s_SourceLanes[l].closed && points.Count > 2)
                    Gizmos.DrawLine(points[points.Count - 1], points[0]);
            }

            Gizmos.color = previous;
            s_SourceLanes.Clear();
        }

        private static int CountSourcePoints(List<PungentModularOutputGenerationUtility.Lane> lanes)
        {
            int count = 0;
            for (int i = 0; i < lanes.Count; i++)
                count += lanes[i].points == null ? 0 : lanes[i].points.Count;
            return count;
        }

        private static readonly List<PungentModularOutputGenerationUtility.Lane> s_SourceLanes = new List<PungentModularOutputGenerationUtility.Lane>();
        private static readonly List<Vector3> s_PointScratch = new List<Vector3>();
        private static readonly List<Vector3> s_BridgeScratch = new List<Vector3>(2);
        private static readonly List<float> s_DistanceScratch = new List<float>();
        private static readonly List<float> s_BridgeDistanceScratch = new List<float>(2);
        private static readonly List<PungentModularOutputGenerationUtility.ResolvedGap> s_GapScratch = new List<PungentModularOutputGenerationUtility.ResolvedGap>();
    }

    internal static class PungentModularOutputGenerationUtility
    {
        internal struct Lane
        {
            public readonly List<Vector3> points;
            public readonly bool closed;
            public readonly string label;

            public Lane(List<Vector3> points, bool closed, string label)
            {
                this.points = points;
                this.closed = closed;
                this.label = label ?? string.Empty;
            }
        }

        internal struct ResolvedGap
        {
            public readonly float StartNormalized;
            public readonly float EndNormalized;
            public readonly string Label;
            public readonly bool bridge;

            public bool Wraps => StartNormalized > EndNormalized;

            public ResolvedGap(float startNormalized, float endNormalized, string label, bool bridge)
            {
                StartNormalized = Mathf.Clamp01(startNormalized);
                EndNormalized = Mathf.Clamp01(endNormalized);
                Label = label ?? string.Empty;
                this.bridge = bridge;
            }
        }

        public static float BuildDistanceTable(IList<Vector3> points, bool loop, List<float> cumulative)
        {
            if (cumulative == null)
                return 0f;

            cumulative.Clear();
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

        public static Vector3 SamplePointAtDistance(IList<Vector3> points, bool loop, IList<float> cumulative, float distance, out Vector3 tangent)
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
            int segmentIndex = 0;
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
            tangent = b - a;
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.forward;
            else
                tangent.Normalize();

            return Vector3.Lerp(a, b, t);
        }

        public static List<Vector3> BuildOffsetLane(IList<Vector3> center, bool closed, float offset)
        {
            List<Vector3> result = new List<Vector3>();
            if (center == null)
                return result;

            int count = center.Count;
            if (count < 2)
            {
                for (int i = 0; i < count; i++)
                    result.Add(center[i]);
                return result;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 prev = center[i > 0 ? i - 1 : closed ? count - 1 : i];
                Vector3 next = center[i < count - 1 ? i + 1 : closed ? 0 : i];
                Vector3 tangent = next - prev;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    if (i < count - 1)
                        tangent = center[i + 1] - center[i];
                    else
                        tangent = center[i] - center[Mathf.Max(0, i - 1)];
                }

                Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
                if (side.sqrMagnitude < 0.0001f)
                    side = Vector3.right;
                result.Add(center[i] + side.normalized * offset);
            }

            return result;
        }

        public static bool IsBlocked(float normalizedDistance, IList<ResolvedGap> gaps)
        {
            if (gaps == null)
                return false;

            normalizedDistance = Mathf.Repeat(normalizedDistance, 1f);
            for (int i = 0; i < gaps.Count; i++)
            {
                ResolvedGap gap = gaps[i];
                if (!gap.Wraps)
                {
                    if (normalizedDistance >= gap.StartNormalized && normalizedDistance <= gap.EndNormalized)
                        return true;
                }
                else if (normalizedDistance >= gap.StartNormalized || normalizedDistance <= gap.EndNormalized)
                {
                    return true;
                }
            }

            return false;
        }

        public static void AppendIntersectionGaps(
            IList<Vector3> points,
            bool closed,
            IList<float> cumulative,
            float totalLength,
            float paddingNormalized,
            List<ResolvedGap> gaps)
        {
            if (points == null || points.Count < 4 || cumulative == null || cumulative.Count < 2 || totalLength <= 0.0001f || gaps == null)
                return;

            int segCount = closed ? points.Count : points.Count - 1;
            for (int a = 0; a < segCount; a++)
            {
                int aNext = (a + 1) % points.Count;
                for (int b = a + 1; b < segCount; b++)
                {
                    int bNext = (b + 1) % points.Count;
                    if (SegmentsShareEndpoint(a, aNext, b, bNext, points.Count, closed))
                        continue;

                    if (!TryIntersectSegmentsXZ(points[a], points[aNext], points[b], points[bNext], out float ta, out float tb))
                        continue;

                    float aNorm = Mathf.Clamp01((cumulative[a] + (cumulative[a + 1] - cumulative[a]) * ta) / totalLength);
                    float bNorm = Mathf.Clamp01((cumulative[b] + (cumulative[b + 1] - cumulative[b]) * tb) / totalLength);
                    gaps.Add(new ResolvedGap(aNorm - paddingNormalized, aNorm + paddingNormalized, "Auto intersection", bridge: true));
                    gaps.Add(new ResolvedGap(bNorm - paddingNormalized, bNorm + paddingNormalized, "Auto intersection", bridge: true));
                }
            }
        }

        public static bool TryGetSocketLocalPositions(GameObject prefab, string startSocketName, string endSocketName, out Vector3 start, out Vector3 end)
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

        public static Quaternion RotationMappingLocalDirectionToWorld(Vector3 localDirection, Vector3 worldDirection, Vector3 worldUp)
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

        public static float ComputePrefabLengthApprox(GameObject prefab, ModularPathSpawner.ForwardAxis axis)
        {
            if (prefab == null)
                return -1f;

            float hookedLength = ModularPathSpawnerEditorHooks.ComputePrefabLength?.Invoke(prefab, axis) ?? -1f;
            if (hookedLength > 0.0001f)
                return hookedLength;

            MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                return axis == ModularPathSpawner.ForwardAxis.Z ? bounds.size.z : bounds.size.x;
            }

            return -1f;
        }

        private static bool TryIntersectSegmentsXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t, out float u)
        {
            t = 0f;
            u = 0f;
            Vector2 p = new Vector2(a.x, a.z);
            Vector2 r = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 q = new Vector2(c.x, c.z);
            Vector2 s = new Vector2(d.x - c.x, d.z - c.z);

            float denominator = Cross(r, s);
            if (Mathf.Abs(denominator) < 0.000001f)
                return false;

            Vector2 qMinusP = q - p;
            t = Cross(qMinusP, s) / denominator;
            u = Cross(qMinusP, r) / denominator;
            return t > 0.0001f && t < 0.9999f && u > 0.0001f && u < 0.9999f;
        }

        private static bool SegmentsShareEndpoint(int a, int aNext, int b, int bNext, int pointCount, bool closed)
        {
            if (a == b || a == bNext || aNext == b || aNext == bNext)
                return true;

            return closed && ((a == 0 && bNext == pointCount - 1) || (b == 0 && aNext == pointCount - 1));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform nested = FindDeepChild(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
