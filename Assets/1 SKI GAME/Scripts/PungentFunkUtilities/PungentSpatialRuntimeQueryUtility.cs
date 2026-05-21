using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    /// <summary>
    /// Runtime-safe convenience APIs for generic spatial authoring providers.
    /// These helpers do not depend on editor-only types and are intended for gameplay,
    /// project adapters, and optional bridge packages.
    /// </summary>
    public static class PungentSpatialRuntimeQueryUtility
    {
        public static bool TrySamplePath(Component component, float normalizedDistance, out PungentPathSample sample)
        {
            sample = default;
            return component is IPungentPathQueryProvider provider &&
                   provider.TrySamplePath(Mathf.Clamp01(normalizedDistance), out sample);
        }

        public static bool TryProjectPointOnPath(Component component, Vector3 worldPoint, out PungentPathProjectionResult result)
        {
            result = default;
            return component is IPungentPathQueryProvider provider &&
                   provider.TryProjectPoint(worldPoint, out result);
        }

        public static bool TryGetPathWidth(Component component, float normalizedDistance, out float width)
        {
            width = 0f;
            return component is IPungentCorridorWidthProvider provider &&
                   provider.TryGetWidthAt(Mathf.Clamp01(normalizedDistance), out width);
        }

        public static bool TryGetSpatialBounds(Component component, out Bounds bounds)
        {
            bounds = default;
            return component is IPungentSpatialBoundsProvider provider &&
                   provider.TryGetSpatialBounds(out bounds);
        }

        public static int CopyPathControlPoints(Component component, IList<Vector3> destination)
        {
            destination?.Clear();
            if (destination == null || !(component is IPungentPathPointProvider provider))
                return 0;

            int count = Mathf.Max(0, provider.PointCount);
            for (int i = 0; i < count; i++)
                destination.Add(provider.GetWorldPoint(i));

            return count;
        }

        public static int CopyPathSamples(Component component, IList<Vector3> destination, int sampleCount)
        {
            destination?.Clear();
            if (destination == null || !(component is IPungentPathQueryProvider provider))
                return 0;

            sampleCount = Mathf.Max(2, sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
                if (provider.TrySamplePath(t, out PungentPathSample sample))
                    destination.Add(sample.Position);
            }

            return destination.Count;
        }

        public static bool TryGetAreaVolume(Component component, out PungentAreaVolume volume)
        {
            volume = default;
            return component is IPungentAreaVolumeProvider provider &&
                   provider.TryGetAreaVolume(out volume);
        }

        public static bool TryContainsAreaPoint(Component component, Vector3 worldPoint, out bool contains)
        {
            contains = false;
            if (!(component is IPungentAreaVolumeProvider provider) || !provider.TryGetAreaVolume(out PungentAreaVolume volume))
                return false;

            contains = ContainsPoint(volume, worldPoint);
            return true;
        }

        public static bool TryGetLabels(Component component, out PungentSpatialLabelSnapshot labels)
        {
            labels = default;
            if (!(component is IPungentSpatialLabelProvider provider))
                return false;

            labels = new PungentSpatialLabelSnapshot(
                provider.SpatialId,
                provider.SpatialDisplayName,
                provider.SpatialTags,
                provider.SpatialColor);
            return true;
        }

        public static bool ContainsPoint(PungentAreaVolume volume, Vector3 worldPoint)
        {
            if (!volume.RuntimeQueryable)
                return false;

            if (volume.HasFiniteHeight && (worldPoint.y < volume.MinY || worldPoint.y > volume.MaxY))
                return false;

            PungentAreaShape shape = volume.Shape;
            switch (shape.Kind)
            {
                case PungentAreaShapeKind.RectangleXZ:
                {
                    Vector3 local = Quaternion.Inverse(shape.Rotation) * (worldPoint - shape.Center);
                    Vector2 half = shape.Size * 0.5f;
                    return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
                }
                case PungentAreaShapeKind.CircleXZ:
                {
                    Vector3 local = Quaternion.Inverse(shape.Rotation) * (worldPoint - shape.Center);
                    return new Vector2(local.x, local.z).sqrMagnitude <= shape.Radius * shape.Radius;
                }
                case PungentAreaShapeKind.Bounds:
                    return shape.Bounds.Contains(worldPoint) || volume.Bounds.Contains(worldPoint);
                case PungentAreaShapeKind.PolygonXZ:
                    return ContainsPointXZ(shape.WorldPolygon, worldPoint);
                default:
                    return false;
            }
        }

        private static bool ContainsPointXZ(Vector3[] polygon, Vector3 worldPoint)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            bool inside = false;
            float x = worldPoint.x;
            float z = worldPoint.z;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector3 pi = polygon[i];
                Vector3 pj = polygon[j];
                bool crosses = (pi.z > z) != (pj.z > z);
                if (crosses)
                {
                    float denominator = pj.z - pi.z;
                    if (Mathf.Abs(denominator) < 0.000001f)
                    {
                        j = i;
                        continue;
                    }

                    float projectedX = (pj.x - pi.x) * (z - pi.z) / denominator + pi.x;
                    if (x < projectedX)
                        inside = !inside;
                }

                j = i;
            }

            return inside;
        }
    }

    public readonly struct PungentSpatialLabelSnapshot
    {
        public readonly string SpatialId;
        public readonly string DisplayName;
        public readonly IReadOnlyList<string> Tags;
        public readonly Color Color;

        public PungentSpatialLabelSnapshot(string spatialId, string displayName, IReadOnlyList<string> tags, Color color)
        {
            SpatialId = spatialId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Tags = tags;
            Color = color;
        }
    }

    public enum PungentSpatialVisualizationKind
    {
        Path,
        Area,
        GeneratedOutput
    }

    public struct PungentSpatialVisualizationSnapshot
    {
        public PungentSpatialVisualizationKind Kind;
        public Component Owner;
        public string DisplayName;
        public Color Color;
        public Bounds Bounds;
        public int PointCount;
        public int EstimatedDrawCost;
        public bool HasWarnings;
    }

    public interface IPungentSpatialVisualizationProvider
    {
        bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot);
    }
}
