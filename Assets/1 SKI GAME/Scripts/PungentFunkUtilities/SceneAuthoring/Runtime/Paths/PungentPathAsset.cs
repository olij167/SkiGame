using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [Serializable]
    public struct PungentPathPoint
    {
        public Vector3 position;
        public bool hasTangent;
        public Vector3 tangent;
        public bool overrideWidth;
        [Min(0.01f)] public float width;

        public PungentPathPoint(Vector3 position)
        {
            this.position = position;
            hasTangent = false;
            tangent = Vector3.forward;
            overrideWidth = false;
            width = 1f;
        }
    }

    [Serializable]
    public struct PungentPathMarker
    {
        public string id;
        public string label;
        [Range(0f, 1f)] public float normalizedDistance;
        public PungentSpatialProfile profile;
        public Color color;
    }

    [Serializable]
    public struct PungentPathSamplingSettings
    {
        public bool smooth;
        [Min(0.1f)] public float samplesPerMeter;
        [Min(2)] public int minSamplesPerSpan;

        public static PungentPathSamplingSettings Default => new PungentPathSamplingSettings
        {
            smooth = false,
            samplesPerMeter = 2.5f,
            minSamplesPerSpan = 6
        };

        public void Normalize()
        {
            samplesPerMeter = Mathf.Max(0.1f, samplesPerMeter);
            minSamplesPerSpan = Mathf.Max(2, minSamplesPerSpan);
        }
    }

    [CreateAssetMenu(menuName = "PungentFunk Utilities/Scene Authoring/Path Asset", fileName = "PungentPathAsset")]
    public class PungentPathAsset : ScriptableObject
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public List<PungentPathPoint> points = new List<PungentPathPoint>();
        public bool closedLoop;
        public bool directional = true;
        [Min(0.01f)] public float defaultCorridorWidth = 1.5f;
        public bool exposeCorridorWidth = true;
        public PungentPathSamplingSettings sampling = PungentPathSamplingSettings.Default;
        public List<PungentPathMarker> markers = new List<PungentPathMarker>();

        private void OnValidate()
        {
            Normalize(name);
        }

        public void Normalize(string fallbackName)
        {
            if (metadata == null)
                metadata = new PungentSpatialObjectMetadata();
            metadata.Normalize(fallbackName);
            defaultCorridorWidth = Mathf.Max(0.01f, defaultCorridorWidth);
            sampling.Normalize();
            if (points == null)
                points = new List<PungentPathPoint>();
            if (markers == null)
                markers = new List<PungentPathMarker>();
        }
    }

    [ExecuteAlways]
    [AddComponentMenu("Utilities/Spatial/Path Instance")]
    public class PungentPathInstance : MonoBehaviour,
        IPungentPathPointProvider,
        IPungentCorridorWidthProvider,
        IPungentPathMetadataProvider,
        IPungentPathQueryProvider,
        IPungentSpatialLabelProvider,
        IPungentSpatialBoundsProvider,
        IPungentSpatialVisualizationProvider,
        IPungentSpatialObjectMetadataProvider,
        IPungentSpatialValidationProvider
    {
        public PungentPathAsset asset;
        public bool useAssetPoints;
        public bool useAssetMetadata;
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public List<PungentPathPoint> points = new List<PungentPathPoint>();
        public bool closedLoop;
        public bool directional = true;
        [Min(0.01f)] public float defaultCorridorWidth = 1.5f;
        public bool exposeCorridorWidth = true;
        public PungentPathSamplingSettings sampling = PungentPathSamplingSettings.Default;
        public List<PungentPathMarker> markers = new List<PungentPathMarker>();
        public bool drawGizmo = true;
        public bool drawUnselectedGizmo;

        public int PointCount => ActivePoints.Count;
        public string SpatialId => ActiveMetadata.StableId;
        public string SpatialDisplayName => ActiveMetadata.DisplayNameOrFallback(name);
        public IReadOnlyList<string> SpatialTags => ActiveMetadata.TagNames;
        public Color SpatialColor => ActiveMetadata.color;

        public IReadOnlyList<PungentPathPoint> ActivePoints
        {
            get
            {
                if (useAssetPoints && asset != null && asset.points != null)
                    return asset.points;
                if (points == null)
                    points = new List<PungentPathPoint>();
                return points;
            }
        }

        public PungentSpatialObjectMetadata ActiveMetadata
        {
            get
            {
                if (useAssetMetadata && asset != null && asset.metadata != null)
                    return asset.metadata;
                if (metadata == null)
                    metadata = new PungentSpatialObjectMetadata();
                return metadata;
            }
        }

        public bool ActiveClosedLoop => useAssetPoints && asset != null ? asset.closedLoop : closedLoop;
        public float ActiveDefaultCorridorWidth => useAssetPoints && asset != null ? asset.defaultCorridorWidth : defaultCorridorWidth;
        public bool ActiveExposeCorridorWidth => useAssetPoints && asset != null ? asset.exposeCorridorWidth : exposeCorridorWidth;
        public PungentPathSamplingSettings ActiveSampling => useAssetPoints && asset != null ? asset.sampling : sampling;

        private void Reset()
        {
            EnsureDefaultData();
        }

        private void OnEnable()
        {
            EnsureDefaultData();
            PungentSpatialRuntimeRegistry.RegisterGlobal(this);
        }

        private void OnDisable()
        {
            PungentSpatialRuntimeRegistry.UnregisterGlobal(this);
        }

        private void OnValidate()
        {
            Normalize();
        }

        public void EnsureDefaultData()
        {
            if (metadata == null)
                metadata = new PungentSpatialObjectMetadata();
            metadata.Normalize(name);
            defaultCorridorWidth = Mathf.Max(0.01f, defaultCorridorWidth);
            sampling.Normalize();
            if (points == null)
                points = new List<PungentPathPoint>();
            if (points.Count == 0)
            {
                points.Add(new PungentPathPoint(new Vector3(-2f, 0f, 0f)));
                points.Add(new PungentPathPoint(new Vector3(2f, 0f, 0f)));
            }
            if (markers == null)
                markers = new List<PungentPathMarker>();
        }

        public void Normalize()
        {
            EnsureDefaultData();
            asset?.Normalize(asset.name);
        }

        public Vector3 GetWorldPoint(int index)
        {
            IReadOnlyList<PungentPathPoint> activePoints = ActivePoints;
            if (activePoints == null || index < 0 || index >= activePoints.Count)
                return transform.position;

            return transform.TransformPoint(activePoints[index].position);
        }

        public bool TryGetWidthAt(float normalizedDistance, out float width)
        {
            width = 0f;
            if (!ActiveExposeCorridorWidth)
                return false;

            IReadOnlyList<PungentPathPoint> activePoints = ActivePoints;
            if (activePoints == null || activePoints.Count == 0)
            {
                width = Mathf.Max(0.01f, ActiveDefaultCorridorWidth);
                return true;
            }

            int count = activePoints.Count;
            float t = Mathf.Clamp01(normalizedDistance);
            float scaled = count <= 1 ? 0f : t * (ActiveClosedLoop ? count : count - 1);
            int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, count - 1);
            int next = ActiveClosedLoop ? (index + 1) % count : Mathf.Min(count - 1, index + 1);
            float localT = Mathf.Clamp01(scaled - index);
            float a = activePoints[index].overrideWidth ? activePoints[index].width : ActiveDefaultCorridorWidth;
            float b = activePoints[next].overrideWidth ? activePoints[next].width : ActiveDefaultCorridorWidth;
            width = Mathf.Max(0.01f, Mathf.Lerp(a, b, localT));
            return true;
        }

        public bool TryGetPathMetadata(out PungentPathMetadata pathMetadata)
        {
            pathMetadata = new PungentPathMetadata
            {
                PathId = SpatialId,
                DisplayName = SpatialDisplayName,
                Tags = SpatialTags,
                Color = SpatialColor,
                Projection = PungentSpatialProjectionMode.XZ,
                ClosedLoop = ActiveClosedLoop,
                ControlPointCount = PointCount,
                HasCorridorWidth = ActiveExposeCorridorWidth,
                DefaultCorridorWidth = ActiveDefaultCorridorWidth
            };

            if (TryBuildDistanceTable(out _, out List<float> cumulative, out float totalLength))
            {
                pathMetadata.SampledPointCount = cumulative.Count;
                pathMetadata.TotalLength = totalLength;
            }

            return PointCount > 0;
        }

        public bool TrySamplePath(float normalizedDistance, out PungentPathSample sample)
        {
            sample = default;
            if (!TryBuildDistanceTable(out List<Vector3> worldPoints, out List<float> cumulative, out float totalLength))
                return false;
            if (totalLength <= 0.0001f)
                return false;

            float t = Mathf.Clamp01(normalizedDistance);
            return TrySampleAtDistance(worldPoints, cumulative, totalLength, t * totalLength, out sample);
        }

        public bool TryProjectPoint(Vector3 worldPoint, out PungentPathProjectionResult result)
        {
            result = default;
            if (!TryBuildDistanceTable(out List<Vector3> worldPoints, out List<float> cumulative, out float totalLength))
                return false;
            if (worldPoints.Count < 2 || totalLength <= 0.0001f)
                return false;

            int segmentCount = ActiveClosedLoop ? worldPoints.Count : worldPoints.Count - 1;
            float bestSqr = float.PositiveInfinity;
            Vector3 bestPoint = worldPoints[0];
            Vector3 bestTangent = Vector3.forward;
            float bestDistance = 0f;
            int bestSegment = 0;

            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % worldPoints.Count;
                Vector3 a = worldPoints[i];
                Vector3 b = worldPoints[next];
                Vector3 closest = ClosestPointOnSegment(a, b, worldPoint, out float segmentT);
                float sqr = (closest - worldPoint).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;

                Vector3 tangent = b - a;
                if (tangent.sqrMagnitude <= 0.000001f)
                    tangent = Vector3.forward;
                else
                    tangent.Normalize();

                bestSqr = sqr;
                bestPoint = closest;
                bestTangent = tangent;
                bestSegment = i;
                bestDistance = Mathf.Clamp(cumulative[i] + Vector3.Distance(a, b) * segmentT, 0f, totalLength);
            }

            result = new PungentPathProjectionResult
            {
                Position = bestPoint,
                Tangent = bestTangent,
                Distance = bestDistance,
                NormalizedDistance = Mathf.Clamp01(bestDistance / totalLength),
                SqrDistanceToInput = bestSqr,
                SegmentIndex = bestSegment,
                ClosedLoop = ActiveClosedLoop
            };
            return true;
        }

        public bool TryGetSpatialBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            IReadOnlyList<PungentPathPoint> activePoints = ActivePoints;
            if (activePoints == null || activePoints.Count == 0)
                return false;

            bounds = new Bounds(GetWorldPoint(0), Vector3.zero);
            for (int i = 1; i < activePoints.Count; i++)
                bounds.Encapsulate(GetWorldPoint(i));

            if (TryGetWidthAt(0.5f, out float width))
                bounds.Expand(Vector3.one * Mathf.Max(0.01f, width));

            return true;
        }

        public bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSpatialBounds(out Bounds bounds))
                return false;

            snapshot = new PungentSpatialVisualizationSnapshot
            {
                Kind = PungentSpatialVisualizationKind.Path,
                Owner = this,
                DisplayName = SpatialDisplayName,
                Color = SpatialColor,
                Bounds = bounds,
                PointCount = PointCount,
                EstimatedDrawCost = Mathf.Max(PointCount, ActiveClosedLoop ? PointCount : PointCount - 1),
                HasWarnings = PointCount >= 256
            };
            return true;
        }

        public bool TryGetSpatialMetadata(out PungentSpatialObjectMetadata spatialMetadata)
        {
            spatialMetadata = ActiveMetadata;
            return spatialMetadata != null;
        }

        public int GetSpatialValidationIssues(IList<PungentSpatialValidationIssue> results)
        {
            int before = results == null ? 0 : results.Count;
            PungentSpatialValidationUtility.ValidatePath(this, results);
            return results == null ? 0 : results.Count - before;
        }

        private bool TryBuildDistanceTable(out List<Vector3> worldPoints, out List<float> cumulative, out float totalLength)
        {
            worldPoints = new List<Vector3>();
            cumulative = new List<float>();
            totalLength = 0f;

            IReadOnlyList<PungentPathPoint> activePoints = ActivePoints;
            if (activePoints == null || activePoints.Count < 2)
                return false;

            for (int i = 0; i < activePoints.Count; i++)
                worldPoints.Add(GetWorldPoint(i));

            cumulative.Add(0f);
            for (int i = 1; i < worldPoints.Count; i++)
            {
                totalLength += Vector3.Distance(worldPoints[i - 1], worldPoints[i]);
                cumulative.Add(totalLength);
            }

            if (ActiveClosedLoop && worldPoints.Count > 2)
                totalLength += Vector3.Distance(worldPoints[worldPoints.Count - 1], worldPoints[0]);

            return totalLength > 0.0001f;
        }

        private bool TrySampleAtDistance(
            List<Vector3> worldPoints,
            List<float> cumulative,
            float totalLength,
            float distance,
            out PungentPathSample sample)
        {
            sample = default;
            if (worldPoints == null || worldPoints.Count < 2 || cumulative == null || cumulative.Count != worldPoints.Count)
                return false;

            float clamped = Mathf.Clamp(distance, 0f, totalLength);
            int segmentCount = ActiveClosedLoop ? worldPoints.Count : worldPoints.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % worldPoints.Count;
                float start = cumulative[i];
                float end = next == 0 ? totalLength : cumulative[next];
                if (clamped > end && i < segmentCount - 1)
                    continue;

                float span = Mathf.Max(0.0001f, end - start);
                float t = Mathf.Clamp01((clamped - start) / span);
                Vector3 a = worldPoints[i];
                Vector3 b = worldPoints[next];
                Vector3 tangent = b - a;
                if (tangent.sqrMagnitude <= 0.000001f)
                    tangent = Vector3.forward;
                else
                    tangent.Normalize();

                sample = new PungentPathSample
                {
                    Position = Vector3.Lerp(a, b, t),
                    Tangent = tangent,
                    Distance = clamped,
                    NormalizedDistance = Mathf.Clamp01(clamped / totalLength),
                    SegmentIndex = i
                };
                return true;
            }

            return false;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point, out float t)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.000001f)
            {
                t = 0f;
                return a;
            }

            t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return a + ab * t;
        }
    }
}
