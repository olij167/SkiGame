using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSpatialAreaShapeMode
    {
        Polygon,
        Rectangle,
        Circle,
        Bounds
    }

    public enum PungentSpatialAreaVerticalMode
    {
        Infinite,
        Height,
        MinMax
    }

    public enum PungentSpatialAreaRole
    {
        Generic,
        Placement,
        Trigger,
        Spawn,
        Weather,
        Region,
        Navigation,
        Audio,
        Lighting,
        GameplayRule
    }

    [Serializable]
    public sealed class PungentSpatialArea
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public bool visible = true;
        public bool locked;
        public PungentSpatialAreaShapeMode shape = PungentSpatialAreaShapeMode.Polygon;
        public PungentSpatialAreaRole role = PungentSpatialAreaRole.Generic;
        public Color fillColor = new Color(0.1f, 0.6f, 1f, 0.18f);
        public Color borderColor = new Color(0.1f, 0.6f, 1f, 0.9f);
        public Vector3 center;
        public Quaternion rotation = Quaternion.identity;
        public List<Vector3> worldPolygon = new List<Vector3>();
        [Min(0.01f)] public Vector2 rectangleSize = new Vector2(6f, 4f);
        [Min(0.01f)] public float circleRadius = 3f;
        public Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(6f, 2f, 6f));
        public PungentSpatialAreaVerticalMode verticalMode = PungentSpatialAreaVerticalMode.Height;
        [Min(0.01f)] public float height = 2f;
        public float minY;
        public float maxY = 2f;
        [Min(0f)] public float edgeFalloff;
        public int priority;
        public bool exclusiveWithinLayer;

        public string StableId => Metadata.StableId;
        public string DisplayName => Metadata.DisplayNameOrFallback("Spatial Area");
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
            if (worldPolygon == null)
                worldPolygon = new List<Vector3>();
            rectangleSize.x = Mathf.Max(0.01f, rectangleSize.x);
            rectangleSize.y = Mathf.Max(0.01f, rectangleSize.y);
            circleRadius = Mathf.Max(0.01f, circleRadius);
            height = Mathf.Max(0.01f, height);
            edgeFalloff = Mathf.Max(0f, edgeFalloff);
            if (maxY < minY)
            {
                float swap = minY;
                minY = maxY;
                maxY = swap;
            }
        }

        public Vector3[] GetWorldPolygon()
        {
            switch (shape)
            {
                case PungentSpatialAreaShapeMode.Rectangle:
                    return BuildRectangle();
                case PungentSpatialAreaShapeMode.Circle:
                    return BuildCircle(32);
                case PungentSpatialAreaShapeMode.Bounds:
                    return BuildBoundsFootprint();
                default:
                    return worldPolygon == null ? Array.Empty<Vector3>() : worldPolygon.ToArray();
            }
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            Vector3[] polygon = GetWorldPolygon();
            if (polygon == null || polygon.Length == 0)
                return false;

            bounds = new Bounds(polygon[0], Vector3.zero);
            for (int i = 1; i < polygon.Length; i++)
                bounds.Encapsulate(polygon[i]);

            if (verticalMode == PungentSpatialAreaVerticalMode.MinMax)
            {
                bounds.Encapsulate(new Vector3(bounds.center.x, minY, bounds.center.z));
                bounds.Encapsulate(new Vector3(bounds.center.x, maxY, bounds.center.z));
            }
            else if (verticalMode == PungentSpatialAreaVerticalMode.Height)
            {
                bounds.Expand(new Vector3(0f, height, 0f));
            }

            return true;
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (verticalMode == PungentSpatialAreaVerticalMode.MinMax && (worldPoint.y < minY || worldPoint.y > maxY))
                return false;

            if (verticalMode == PungentSpatialAreaVerticalMode.Height && Mathf.Abs(worldPoint.y - center.y) > height * 0.5f)
                return false;

            switch (shape)
            {
                case PungentSpatialAreaShapeMode.Rectangle:
                {
                    Vector3 local = Quaternion.Inverse(rotation) * (worldPoint - center);
                    Vector2 half = rectangleSize * 0.5f;
                    return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
                }
                case PungentSpatialAreaShapeMode.Circle:
                {
                    Vector3 local = Quaternion.Inverse(rotation) * (worldPoint - center);
                    return new Vector2(local.x, local.z).sqrMagnitude <= circleRadius * circleRadius;
                }
                case PungentSpatialAreaShapeMode.Bounds:
                    return worldBounds.Contains(worldPoint);
                default:
                    return PungentSpatialRegionUtility.ContainsPointXZ(GetWorldPolygon(), worldPoint);
            }
        }

        private Vector3[] BuildRectangle()
        {
            Vector2 half = rectangleSize * 0.5f;
            return new[]
            {
                center + rotation * new Vector3(-half.x, 0f, -half.y),
                center + rotation * new Vector3(-half.x, 0f, half.y),
                center + rotation * new Vector3(half.x, 0f, half.y),
                center + rotation * new Vector3(half.x, 0f, -half.y)
            };
        }

        private Vector3[] BuildCircle(int segments)
        {
            segments = Mathf.Max(8, segments);
            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                points[i] = center + rotation * new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            }

            return points;
        }

        private Vector3[] BuildBoundsFootprint()
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            return new[]
            {
                new Vector3(min.x, center.y, min.z),
                new Vector3(min.x, center.y, max.z),
                new Vector3(max.x, center.y, max.z),
                new Vector3(max.x, center.y, min.z)
            };
        }
    }
}
