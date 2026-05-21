using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public static class PungentSpatialRegionUtility
    {
        private const float Epsilon = 0.00001f;

        public static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                bool crosses = (pi.y > point.y) != (pj.y > point.y);
                if (crosses)
                {
                    float denominator = pj.y - pi.y;
                    if (Mathf.Abs(denominator) > Epsilon)
                    {
                        float projectedX = (pj.x - pi.x) * (point.y - pi.y) / denominator + pi.x;
                        if (point.x < projectedX)
                            inside = !inside;
                    }
                }

                j = i;
            }

            return inside;
        }

        public static bool ContainsPointXZ(IReadOnlyList<Vector3> polygon, Vector3 point)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 pi = polygon[i];
                Vector3 pj = polygon[j];
                bool crosses = (pi.z > point.z) != (pj.z > point.z);
                if (crosses)
                {
                    float denominator = pj.z - pi.z;
                    if (Mathf.Abs(denominator) > Epsilon)
                    {
                        float projectedX = (pj.x - pi.x) * (point.z - pi.z) / denominator + pi.x;
                        if (point.x < projectedX)
                            inside = !inside;
                    }
                }

                j = i;
            }

            return inside;
        }

        public static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return 0f;

            double area = 0d;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return (float)(area * 0.5d);
        }

        public static Vector2 Centroid(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0)
                return Vector2.zero;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < polygon.Count; i++)
                sum += polygon[i];
            return sum / polygon.Count;
        }

        public static bool HasSelfIntersection(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 4)
                return false;

            for (int a = 0; a < polygon.Count; a++)
            {
                int aNext = (a + 1) % polygon.Count;
                for (int b = a + 1; b < polygon.Count; b++)
                {
                    int bNext = (b + 1) % polygon.Count;
                    if (a == b || aNext == b || bNext == a)
                        continue;

                    if (SegmentsIntersect(polygon[a], polygon[aNext], polygon[b], polygon[bNext]))
                        return true;
                }
            }

            return false;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float denominator = (b.x - a.x) * (d.y - c.y) - (b.y - a.y) * (d.x - c.x);
            if (Mathf.Abs(denominator) < Epsilon)
                return false;

            float u = ((c.x - a.x) * (d.y - c.y) - (c.y - a.y) * (d.x - c.x)) / denominator;
            float v = ((c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x)) / denominator;
            return u > Epsilon && u < 1f - Epsilon && v > Epsilon && v < 1f - Epsilon;
        }
    }
}
