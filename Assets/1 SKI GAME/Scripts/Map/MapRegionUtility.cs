using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Map
{
    public static class MapRegionUtility
    {
        public static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            const float edgeEpsilon = 0.0015f;

            bool inside = false;
            int count = polygon.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = polygon[j];
                Vector2 b = polygon[i];

                if (IsPointOnSegment(a, b, point, edgeEpsilon))
                    return true;

                bool intersect =
                    ((a.y > point.y) != (b.y > point.y)) &&
                    (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.000001f, (b.y - a.y)) + a.x);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private static bool IsPointOnSegment(Vector2 a, Vector2 b, Vector2 p, float epsilon)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;

            float abLenSq = ab.sqrMagnitude;
            if (abLenSq <= 0.0000001f)
                return (p - a).sqrMagnitude <= epsilon * epsilon;

            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abLenSq);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude <= epsilon * epsilon;
        }

        public static List<Vector2> ResolveLoopUv(MapRegionSet set, List<string> vertexIds)
        {
            List<Vector2> pts = new List<Vector2>();
            if (set == null || vertexIds == null)
                return pts;

            for (int i = 0; i < vertexIds.Count; i++)
            {
                if (set.TryGetVertexUv(vertexIds[i], out Vector2 uv))
                    pts.Add(uv);
            }

            return pts;
        }

        public static bool ContainsFace(MapRegionSet set, MapRegionFace face, Vector2 uv)
        {
            if (set == null || face == null || !face.IsValid)
                return false;

            List<Vector2> outer = ResolveLoopUv(set, face.outerVertexIds);
            if (!ContainsPoint(outer, uv))
                return false;

            if (face.holeLoops != null)
            {
                for (int i = 0; i < face.holeLoops.Count; i++)
                {
                    var hole = face.holeLoops[i];
                    if (hole == null || !hole.IsValid)
                        continue;

                    List<Vector2> holePts = ResolveLoopUv(set, hole.vertexIds);
                    if (ContainsPoint(holePts, uv))
                        return false;
                }
            }

            return true;
        }

        public static string ResolveRegionId(MapRegionSet set, Vector2 uv)
        {
            MapRegionFace face = ResolveRegion(set, uv);
            return face != null ? face.id : null;
        }

        public static MapRegionFace ResolveRegion(MapRegionSet set, Vector2 uv)
        {
            if (set == null || set.Faces == null)
                return null;

            MapRegionFace best = null;
            float bestArea = float.MaxValue;
            MapRegionFace rootFace = null;

            for (int i = 0; i < set.Faces.Count; i++)
            {
                var face = set.Faces[i];
                if (face == null || !face.IsValid)
                    continue;

                if (string.Equals(face.id, MapRegionSet.RootFaceId, System.StringComparison.Ordinal))
                    rootFace = face;

                if (!ContainsFace(set, face, uv))
                    continue;

                float area = Mathf.Abs(ComputeSignedArea(ResolveLoopUv(set, face.outerVertexIds)));
                if (best == null || area < bestArea)
                {
                    best = face;
                    bestArea = area;
                }
            }

            if (best != null)
                return best;

            // Explicit fallback: if no child region resolves but the root/base face does,
            // still allow selecting the base layer.
            if (rootFace != null && ContainsFace(set, rootFace, uv))
                return rootFace;

            return null;
        }

        public static float ComputeSignedArea(IReadOnlyList<Vector2> pts)
        {
            if (pts == null || pts.Count < 3)
                return 0f;

            float area = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % pts.Count];
                area += (a.x * b.y) - (b.x * a.y);
            }

            return area * 0.5f;
        }

        public static Vector2 ComputeCentroid(IReadOnlyList<Vector2> pts, Vector2 fallback)
        {
            if (pts == null || pts.Count == 0)
                return fallback;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < pts.Count; i++)
                sum += pts[i];

            return sum / pts.Count;
        }

        public static void NormalizeLoop(List<string> ids)
        {
            if (ids == null)
                return;

            for (int i = ids.Count - 1; i > 0; i--)
            {
                if (string.Equals(ids[i], ids[i - 1], System.StringComparison.Ordinal))
                    ids.RemoveAt(i);
            }

            if (ids.Count > 1 && string.Equals(ids[0], ids[ids.Count - 1], System.StringComparison.Ordinal))
                ids.RemoveAt(ids.Count - 1);
        }

        public static void EnsureClockwise(MapRegionSet set, List<string> ids)
        {
            List<Vector2> pts = ResolveLoopUv(set, ids);
            if (ComputeSignedArea(pts) > 0f)
                ids.Reverse();
        }

        public static void EnsureCounterClockwise(MapRegionSet set, List<string> ids)
        {
            List<Vector2> pts = ResolveLoopUv(set, ids);
            if (ComputeSignedArea(pts) < 0f)
                ids.Reverse();
        }

        public static List<string> BuildBoundaryPath(List<string> loop, int fromIndex, int toIndex, bool forward)
        {
            List<string> result = new List<string>();
            if (loop == null || loop.Count == 0)
                return result;

            int count = loop.Count;
            int i = fromIndex;

            while (true)
            {
                result.Add(loop[i]);

                if (i == toIndex)
                    break;

                i = forward ? (i + 1) % count : (i - 1 + count) % count;
            }

            return result;
        }

        public static bool Approximately(Vector2 a, Vector2 b, float epsilon = 0.00001f)
        {
            return (a - b).sqrMagnitude <= epsilon * epsilon;
        }

        public static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = ab.sqrMagnitude;
            if (denom <= 0.000001f)
                return a;

            float t = Vector2.Dot(p - a, ab) / denom;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        public static float ClosestPointParameterOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = ab.sqrMagnitude;
            if (denom <= 0.000001f)
                return 0f;

            return Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        }
    }
}