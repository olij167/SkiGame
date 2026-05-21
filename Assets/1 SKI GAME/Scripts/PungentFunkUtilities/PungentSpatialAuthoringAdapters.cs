using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    /// <summary>
    /// Generic 2D footprint kinds for scene spatial authoring. Keep these free of concrete package
    /// or project-specific concepts so optional bridges can consume them safely.
    /// </summary>
    public enum PungentAreaShapeKind
    {
        PolygonXZ,
        RectangleXZ,
        CircleXZ,
        Bounds
    }

    public enum PungentSpatialProjectionMode
    {
        XZ,
        XY,
        YZ
    }

    public struct PungentSpatialPoint2D
    {
        public float X;
        public float Y;

        public PungentSpatialPoint2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector3 ToVector3XZ(float y = 0f)
        {
            return new Vector3(X, y, Y);
        }

        public static PungentSpatialPoint2D FromVector3XZ(Vector3 value)
        {
            return new PungentSpatialPoint2D(value.x, value.z);
        }
    }

    public struct PungentAreaShape
    {
        public PungentAreaShapeKind Kind;
        public PungentSpatialProjectionMode Projection;
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector2 Size;
        public float Radius;
        public Bounds Bounds;
        public Vector3[] WorldPolygon;
    }

    public struct PungentAreaVolume
    {
        public PungentAreaShape Shape;
        public Bounds Bounds;
        public float MinY;
        public float MaxY;
        public bool HasFiniteHeight;
        public bool RuntimeQueryable;
    }

    public interface IPungentAreaShapeProvider
    {
        bool TryGetAreaShape(out PungentAreaShape shape);
    }

    public interface IPungentAreaVolumeProvider
    {
        bool TryGetAreaVolume(out PungentAreaVolume volume);
    }

    public interface IPungentSpatialLabelProvider
    {
        string SpatialId { get; }
        string SpatialDisplayName { get; }
        IReadOnlyList<string> SpatialTags { get; }
        Color SpatialColor { get; }
    }

    public interface IPungentSpatialBoundsProvider
    {
        bool TryGetSpatialBounds(out Bounds bounds);
    }

    public enum PungentSpatialValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public struct PungentSpatialValidationIssue
    {
        public string Code;
        public string Message;
        public PungentSpatialValidationSeverity Severity;
        public Vector3 WorldPosition;

        public PungentSpatialValidationIssue(string code, string message, PungentSpatialValidationSeverity severity, Vector3 worldPosition = default)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
            WorldPosition = worldPosition;
        }
    }

    public static class PungentSpatialValidationUtility
    {
        private const float DuplicatePointThreshold = 0.05f;

        public static void ValidatePath(IPungentPathPointProvider provider, IList<PungentSpatialValidationIssue> results)
        {
            if (results == null)
                return;

            if (provider == null)
            {
                results.Add(new PungentSpatialValidationIssue("path-missing-provider", "No path provider is available.", PungentSpatialValidationSeverity.Error));
                return;
            }

            int count = Mathf.Max(0, provider.PointCount);
            if (count < 2)
                results.Add(new PungentSpatialValidationIssue("path-too-few-points", "Add at least two points to create a usable path.", PungentSpatialValidationSeverity.Warning));

            for (int i = 1; i < count; i++)
            {
                Vector3 previous = provider.GetWorldPoint(i - 1);
                Vector3 current = provider.GetWorldPoint(i);
                if ((current - previous).sqrMagnitude <= DuplicatePointThreshold * DuplicatePointThreshold)
                {
                    results.Add(new PungentSpatialValidationIssue("path-near-duplicate-point", "Path contains near-duplicate neighboring points.", PungentSpatialValidationSeverity.Warning, current));
                    break;
                }
            }
        }

        public static void ValidatePath(IList<Vector3> worldPoints, IList<PungentSpatialValidationIssue> results)
        {
            if (results == null)
                return;

            int count = worldPoints == null ? 0 : worldPoints.Count;
            if (count < 2)
                results.Add(new PungentSpatialValidationIssue("path-too-few-points", "Add at least two points to create a usable path.", PungentSpatialValidationSeverity.Warning));

            for (int i = 1; i < count; i++)
            {
                Vector3 previous = worldPoints[i - 1];
                Vector3 current = worldPoints[i];
                if ((current - previous).sqrMagnitude <= DuplicatePointThreshold * DuplicatePointThreshold)
                {
                    results.Add(new PungentSpatialValidationIssue("path-near-duplicate-point", "Path contains near-duplicate neighboring points.", PungentSpatialValidationSeverity.Warning, current));
                    break;
                }
            }
        }

        public static void ValidateArea(IPungentAreaShapeProvider provider, IList<PungentSpatialValidationIssue> results)
        {
            if (results == null)
                return;

            if (provider == null || !provider.TryGetAreaShape(out PungentAreaShape shape))
            {
                results.Add(new PungentSpatialValidationIssue("area-invalid-shape", "Area shape data is not available.", PungentSpatialValidationSeverity.Error));
                return;
            }

            Vector3[] polygon = shape.WorldPolygon;
            if (shape.Kind == PungentAreaShapeKind.PolygonXZ && (polygon == null || polygon.Length < 3))
            {
                results.Add(new PungentSpatialValidationIssue("area-too-few-points", "Add at least three polygon points to create a usable area footprint.", PungentSpatialValidationSeverity.Warning, shape.Center));
                return;
            }

            if (polygon == null || polygon.Length < 2)
                return;

            for (int i = 0; i < polygon.Length; i++)
            {
                int next = (i + 1) % polygon.Length;
                if ((polygon[next] - polygon[i]).sqrMagnitude <= DuplicatePointThreshold * DuplicatePointThreshold)
                {
                    results.Add(new PungentSpatialValidationIssue("area-near-duplicate-point", "Area contains near-duplicate neighboring points.", PungentSpatialValidationSeverity.Warning, polygon[next]));
                    break;
                }
            }

            if (shape.Kind == PungentAreaShapeKind.PolygonXZ && HasSelfIntersectionXZ(polygon, out Vector3 intersection))
                results.Add(new PungentSpatialValidationIssue("area-self-intersection", "Area polygon edges intersect. Split or reorder points before using this area for generated outputs.", PungentSpatialValidationSeverity.Error, intersection));
        }

        public static bool HasErrors(IList<PungentSpatialValidationIssue> issues)
        {
            if (issues == null)
                return false;

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == PungentSpatialValidationSeverity.Error)
                    return true;
            }

            return false;
        }

        private static bool HasSelfIntersectionXZ(Vector3[] polygon, out Vector3 intersection)
        {
            intersection = default;
            if (polygon == null || polygon.Length < 4)
                return false;

            int count = polygon.Length;
            for (int a = 0; a < count; a++)
            {
                int aNext = (a + 1) % count;
                for (int b = a + 1; b < count; b++)
                {
                    int bNext = (b + 1) % count;
                    if (SegmentsShareEndpoint(a, aNext, b, bNext))
                        continue;

                    if (TryIntersectSegmentsXZ(polygon[a], polygon[aNext], polygon[b], polygon[bNext], out intersection))
                        return true;
                }
            }

            return false;
        }

        private static bool SegmentsShareEndpoint(int a, int aNext, int b, int bNext)
        {
            return a == b || a == bNext || aNext == b || aNext == bNext;
        }

        private static bool TryIntersectSegmentsXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 intersection)
        {
            intersection = default;
            Vector2 p = new Vector2(a.x, a.z);
            Vector2 r = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 q = new Vector2(c.x, c.z);
            Vector2 s = new Vector2(d.x - c.x, d.z - c.z);

            float denominator = Cross(r, s);
            if (Mathf.Abs(denominator) < 0.000001f)
                return false;

            Vector2 qMinusP = q - p;
            float t = Cross(qMinusP, s) / denominator;
            float u = Cross(qMinusP, r) / denominator;
            if (t <= 0.0001f || t >= 0.9999f || u <= 0.0001f || u >= 0.9999f)
                return false;

            Vector2 hit = p + r * t;
            intersection = new Vector3(hit.x, Mathf.Lerp(a.y, b.y, t), hit.y);
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
