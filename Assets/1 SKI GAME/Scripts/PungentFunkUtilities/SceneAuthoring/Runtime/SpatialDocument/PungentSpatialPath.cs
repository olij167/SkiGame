using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSpatialPathHeightMode
    {
        WorldY,
        ProjectionPlane,
        SurfaceSampled
    }

    [Serializable]
    public sealed class PungentSpatialPath
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public bool visible = true;
        public bool locked;
        public bool closedLoop;
        public bool directional = true;
        [Min(0.01f)] public float width = 1.5f;
        [Min(0.01f)] public float corridorWidth = 1.5f;
        public PungentSpatialPathHeightMode heightMode = PungentSpatialPathHeightMode.WorldY;
        public List<Vector3> worldPoints = new List<Vector3>();

        public string StableId => Metadata.StableId;
        public string DisplayName => Metadata.DisplayNameOrFallback("Spatial Path");
        public PungentSpatialObjectMetadata Metadata
        {
            get
            {
                if (metadata == null)
                    metadata = new PungentSpatialObjectMetadata();
                return metadata;
            }
        }

        public void Normalize(string fallbackName)
        {
            Metadata.Normalize(fallbackName);
            width = Mathf.Max(0.01f, width);
            corridorWidth = Mathf.Max(0.01f, corridorWidth);
            if (worldPoints == null)
                worldPoints = new List<Vector3>();
        }

        public float EstimateLength()
        {
            if (worldPoints == null || worldPoints.Count < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < worldPoints.Count; i++)
                length += Vector3.Distance(worldPoints[i - 1], worldPoints[i]);
            if (closedLoop && worldPoints.Count > 2)
                length += Vector3.Distance(worldPoints[worldPoints.Count - 1], worldPoints[0]);
            return length;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            if (worldPoints == null || worldPoints.Count == 0)
                return false;

            bounds = new Bounds(worldPoints[0], Vector3.zero);
            for (int i = 1; i < worldPoints.Count; i++)
                bounds.Encapsulate(worldPoints[i]);
            Vector3 expansion = Vector3.one * Mathf.Max(width, corridorWidth, 0.1f);
            bounds.Expand(expansion);
            return true;
        }

        public bool TrySampleNormalized(float normalizedDistance, out PungentPathSample sample)
        {
            sample = default;
            float total = EstimateLength();
            if (total <= 0.0001f)
                return false;

            return TrySampleDistance(Mathf.Clamp01(normalizedDistance) * total, out sample);
        }

        public bool TrySampleDistance(float distance, out PungentPathSample sample)
        {
            sample = default;
            if (worldPoints == null || worldPoints.Count < 2)
                return false;

            float totalLength = EstimateLength();
            if (totalLength <= 0.0001f)
                return false;

            float remaining = Mathf.Clamp(distance, 0f, totalLength);
            int segmentCount = closedLoop ? worldPoints.Count : worldPoints.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 a = worldPoints[i];
                Vector3 b = worldPoints[(i + 1) % worldPoints.Count];
                float segmentLength = Vector3.Distance(a, b);
                if (segmentLength <= 0.0001f)
                    continue;

                if (remaining <= segmentLength || i == segmentCount - 1)
                {
                    float t = Mathf.Clamp01(remaining / segmentLength);
                    Vector3 tangent = (b - a).normalized;
                    sample = new PungentPathSample
                    {
                        Position = Vector3.Lerp(a, b, t),
                        Tangent = tangent.sqrMagnitude > 0.0001f ? tangent : Vector3.forward,
                        Distance = Mathf.Clamp(distance, 0f, totalLength),
                        NormalizedDistance = Mathf.Clamp01(totalLength <= 0f ? 0f : distance / totalLength),
                        SegmentIndex = i
                    };
                    return true;
                }

                remaining -= segmentLength;
            }

            return false;
        }

        public bool TryProjectPoint(Vector3 worldPoint, out PungentPathProjectionResult result)
        {
            result = default;
            if (worldPoints == null || worldPoints.Count < 2)
                return false;

            float totalLength = EstimateLength();
            float bestSqr = float.PositiveInfinity;
            float distanceBefore = 0f;
            float bestDistance = 0f;
            Vector3 bestPoint = worldPoints[0];
            Vector3 bestTangent = Vector3.forward;
            int bestSegment = 0;
            int segmentCount = closedLoop ? worldPoints.Count : worldPoints.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 a = worldPoints[i];
                Vector3 b = worldPoints[(i + 1) % worldPoints.Count];
                Vector3 ab = b - a;
                float segmentLength = ab.magnitude;
                if (segmentLength <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01(Vector3.Dot(worldPoint - a, ab) / Vector3.Dot(ab, ab));
                Vector3 projected = Vector3.Lerp(a, b, t);
                float sqr = (worldPoint - projected).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestPoint = projected;
                    bestTangent = ab.normalized;
                    bestDistance = distanceBefore + segmentLength * t;
                    bestSegment = i;
                }

                distanceBefore += segmentLength;
            }

            result = new PungentPathProjectionResult
            {
                Position = bestPoint,
                Tangent = bestTangent,
                Distance = bestDistance,
                NormalizedDistance = Mathf.Clamp01(totalLength <= 0f ? 0f : bestDistance / totalLength),
                SqrDistanceToInput = bestSqr,
                SegmentIndex = bestSegment,
                ClosedLoop = closedLoop
            };
            return true;
        }
    }
}
