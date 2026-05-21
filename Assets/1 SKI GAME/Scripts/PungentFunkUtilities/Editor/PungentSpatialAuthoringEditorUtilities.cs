using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal struct PungentSpatialSurfaceAuthoringSettings
    {
        private const string PrefPrefix = "PungentFunkUtilities.SpatialAuthoring.Surface.";

        public LayerMask SurfaceMask;
        public float RaycastHeight;
        public float YOffset;
        public float MinY;
        public float MaxY;
        public float MinSlope;
        public float MaxSlope;

        public static PungentSpatialSurfaceAuthoringSettings Load()
        {
            return new PungentSpatialSurfaceAuthoringSettings
            {
                SurfaceMask = EditorPrefs.GetInt(PrefPrefix + "Mask", ~0),
                RaycastHeight = EditorPrefs.GetFloat(PrefPrefix + "RaycastHeight", 50f),
                YOffset = EditorPrefs.GetFloat(PrefPrefix + "YOffset", 0f),
                MinY = EditorPrefs.GetFloat(PrefPrefix + "MinY", -10000f),
                MaxY = EditorPrefs.GetFloat(PrefPrefix + "MaxY", 10000f),
                MinSlope = EditorPrefs.GetFloat(PrefPrefix + "MinSlope", 0f),
                MaxSlope = EditorPrefs.GetFloat(PrefPrefix + "MaxSlope", 90f)
            };
        }

        public void Save()
        {
            RaycastHeight = Mathf.Max(0.01f, RaycastHeight);
            MinSlope = Mathf.Clamp(MinSlope, 0f, 90f);
            MaxSlope = Mathf.Clamp(MaxSlope, 0f, 90f);
            if (MaxY < MinY)
            {
                float min = MinY;
                MinY = MaxY;
                MaxY = min;
            }

            EditorPrefs.SetInt(PrefPrefix + "Mask", SurfaceMask.value);
            EditorPrefs.SetFloat(PrefPrefix + "RaycastHeight", RaycastHeight);
            EditorPrefs.SetFloat(PrefPrefix + "YOffset", YOffset);
            EditorPrefs.SetFloat(PrefPrefix + "MinY", MinY);
            EditorPrefs.SetFloat(PrefPrefix + "MaxY", MaxY);
            EditorPrefs.SetFloat(PrefPrefix + "MinSlope", MinSlope);
            EditorPrefs.SetFloat(PrefPrefix + "MaxSlope", MaxSlope);
        }
    }

    internal static class PungentSpatialAuthoringBoundsUtility
    {
        private const float MinBoundsHeight = 0.25f;

        public static bool TryGetAuthoringBounds(Component component, out Bounds bounds)
        {
            bounds = default;
            if (component is PungentAreaAuthoringShape area)
                return TryGetAreaFootprintBounds(area, out bounds);

            if (component is IPungentSpatialBoundsProvider provider && provider.TryGetSpatialBounds(out bounds))
                return true;

            if (component != null)
            {
                bounds = new Bounds(component.transform.position, Vector3.one);
                return true;
            }

            return false;
        }

        public static bool TryGetRuntimeBounds(Component component, out Bounds bounds)
        {
            bounds = default;
            return component is IPungentSpatialBoundsProvider provider && provider.TryGetSpatialBounds(out bounds);
        }

        public static bool TryGetAreaFootprintBounds(PungentAreaAuthoringShape area, out Bounds bounds)
        {
            bounds = default;
            if (area == null)
                return false;

            List<Vector3> points = area.GetWorldPolygon();
            if (points == null || points.Count == 0)
                return false;

            bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
                bounds.Encapsulate(points[i]);

            if (bounds.size.y < MinBoundsHeight)
                bounds.Expand(new Vector3(0f, MinBoundsHeight - bounds.size.y, 0f));

            return true;
        }
    }

    internal static class PungentSpatialSurfaceAuthoringUtility
    {
        public static string AlignPathPoints(ModularPathSpawner path, bool selectedOnly, int selectedIndex, PungentSpatialSurfaceAuthoringSettings settings)
        {
            if (path == null || path.PointCount <= 0)
                return "No path points to align.";

            if (selectedOnly && (selectedIndex < 0 || selectedIndex >= path.PointCount))
                return "Select a path point before aligning selected.";

            int changed = 0;
            int skipped = 0;
            int start = selectedOnly ? selectedIndex : 0;
            int end = selectedOnly ? selectedIndex + 1 : path.PointCount;
            settings.Save();

            Undo.RecordObject(path, selectedOnly ? "Align Selected Path Point To Surface" : "Align Path Points To Surface");
            for (int i = start; i < end; i++)
            {
                if (TrySampleSurface(path.GetWorldPoint(i), settings, out RaycastHit hit))
                {
                    path.SetWorldPoint(i, hit.point + Vector3.up * settings.YOffset);
                    changed++;
                }
                else
                {
                    skipped++;
                }
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(path);
                PungentPathRebuildScheduler.RequestPreviewRefresh(path, null);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            }

            return "Aligned " + changed + " path point" + (changed == 1 ? string.Empty : "s") + " / skipped " + skipped + ".";
        }

        public static string AlignAreaPoints(PungentAreaAuthoringShape area, bool selectedOnly, int selectedIndex, PungentSpatialSurfaceAuthoringSettings settings)
        {
            if (area == null || area.shapeMode != PungentAreaAuthoringShape.ShapeMode.PolygonXZ || area.localPolygonPoints == null || area.localPolygonPoints.Count == 0)
                return "Area point alignment requires a polygon area.";

            if (selectedOnly && (selectedIndex < 0 || selectedIndex >= area.localPolygonPoints.Count))
                return "Select an area point before aligning selected.";

            int changed = 0;
            int skipped = 0;
            int start = selectedOnly ? selectedIndex : 0;
            int end = selectedOnly ? selectedIndex + 1 : area.localPolygonPoints.Count;
            settings.Save();

            Undo.RecordObject(area, selectedOnly ? "Align Selected Area Point To Surface" : "Align Area Points To Surface");
            for (int i = start; i < end; i++)
            {
                Vector3 world = area.transform.TransformPoint(area.localPolygonPoints[i]);
                if (TrySampleSurface(world, settings, out RaycastHit hit))
                {
                    area.localPolygonPoints[i] = area.transform.InverseTransformPoint(hit.point + Vector3.up * settings.YOffset);
                    changed++;
                }
                else
                {
                    skipped++;
                }
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(area);
                PungentSpatialAuthoringSceneObjectCache.MarkDirty();
                PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            }

            return "Aligned " + changed + " area point" + (changed == 1 ? string.Empty : "s") + " / skipped " + skipped + ".";
        }

        public static string AlignAreaShapeCenter(PungentAreaAuthoringShape area, PungentSpatialSurfaceAuthoringSettings settings)
        {
            if (area == null)
                return "No area selected.";

            settings.Save();
            if (!TrySampleSurface(area.transform.position, settings, out RaycastHit hit))
                return "No valid surface hit for the area center.";

            Undo.RecordObject(area.transform, "Align Area Center To Surface");
            area.transform.position = hit.point + Vector3.up * settings.YOffset;
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            return "Aligned area center to surface.";
        }

        public static string SetAreaBaseFromFootprint(PungentAreaAuthoringShape area)
        {
            if (area == null || !PungentSpatialAuthoringBoundsUtility.TryGetAreaFootprintBounds(area, out Bounds footprint))
                return "No area footprint available.";

            List<Vector3> worldPolygon = area.GetWorldPolygon();
            Undo.RecordObjects(new UnityEngine.Object[] { area, area.transform }, "Set Area Base From Footprint");
            Vector3 position = area.transform.position;
            position.y = footprint.min.y;
            area.transform.position = position;

            if (area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ && area.localPolygonPoints != null && worldPolygon != null && worldPolygon.Count == area.localPolygonPoints.Count)
            {
                for (int i = 0; i < worldPolygon.Count; i++)
                    area.localPolygonPoints[i] = area.transform.InverseTransformPoint(worldPolygon[i]);
            }

            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            return "Set area base to footprint min Y while preserving visible polygon points.";
        }

        public static string SetAreaVerticalRangeFromFootprint(PungentAreaAuthoringShape area)
        {
            if (area == null || !PungentSpatialAuthoringBoundsUtility.TryGetAreaFootprintBounds(area, out Bounds footprint))
                return "No area footprint available.";

            float thickness = Mathf.Max(0.01f, area.height);
            if (area.verticalMode == PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax)
                thickness = Mathf.Max(0.01f, area.maxY - area.minY);

            Undo.RecordObject(area, "Set Area Vertical Range From Footprint");
            area.verticalMode = PungentAreaAuthoringShape.VerticalMode.ExplicitMinMax;
            area.minY = footprint.min.y;
            area.maxY = footprint.max.y + thickness;
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            return "Set area vertical range from footprint.";
        }

        public static bool TrySampleSurface(Vector3 point, PungentSpatialSurfaceAuthoringSettings settings, out RaycastHit hit)
        {
            settings.RaycastHeight = Mathf.Max(0.01f, settings.RaycastHeight);
            Vector3 start = point + Vector3.up * settings.RaycastHeight;
            if (!Physics.Raycast(start, Vector3.down, out hit, settings.RaycastHeight * 2f, settings.SurfaceMask, QueryTriggerInteraction.Ignore))
                return false;

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            return hit.point.y >= settings.MinY &&
                   hit.point.y <= settings.MaxY &&
                   slope >= Mathf.Min(settings.MinSlope, settings.MaxSlope) &&
                   slope <= Mathf.Max(settings.MinSlope, settings.MaxSlope);
        }
    }

    internal static class PungentSpatialAreaRepairUtility
    {
        private const float DuplicateThreshold = 0.05f;

        public static string ConvertShapeToPolygon(PungentAreaAuthoringShape area)
        {
            if (area == null)
                return "No area selected.";

            List<Vector3> world = area.GetWorldPolygon();
            if (world == null || world.Count < 3)
                return "Area shape does not provide a polygon boundary.";

            Undo.RecordObject(area, "Convert Area Shape To Polygon");
            area.shapeMode = PungentAreaAuthoringShape.ShapeMode.PolygonXZ;
            if (area.localPolygonPoints == null)
                area.localPolygonPoints = new List<Vector3>();
            area.localPolygonPoints.Clear();
            for (int i = 0; i < world.Count; i++)
                area.localPolygonPoints.Add(area.transform.InverseTransformPoint(world[i]));

            MarkChanged(area);
            return "Converted area shape to editable polygon.";
        }

        public static string ReverseWinding(PungentAreaAuthoringShape area)
        {
            if (!HasEditablePolygon(area))
                return "Reverse winding requires a polygon area.";

            Undo.RecordObject(area, "Reverse Area Winding");
            area.localPolygonPoints.Reverse();
            MarkChanged(area);
            return "Reversed area polygon winding.";
        }

        public static string NormalizeWinding(PungentAreaAuthoringShape area)
        {
            if (!HasEditablePolygon(area))
                return "Normalize winding requires a polygon area.";

            if (SignedAreaXZ(area.localPolygonPoints) >= 0f)
                return "Area winding is already counter-clockwise.";

            Undo.RecordObject(area, "Normalize Area Winding");
            area.localPolygonPoints.Reverse();
            MarkChanged(area);
            return "Normalized area polygon winding.";
        }

        public static string RemoveNearDuplicates(PungentAreaAuthoringShape area)
        {
            if (!HasEditablePolygon(area))
                return "Duplicate cleanup requires a polygon area.";

            Undo.RecordObject(area, "Remove Area Near-Duplicate Points");
            int removed = 0;
            for (int i = area.localPolygonPoints.Count - 1; i >= 0; i--)
            {
                int next = (i + 1) % area.localPolygonPoints.Count;
                if (area.localPolygonPoints.Count <= 3)
                    break;

                if ((area.localPolygonPoints[i] - area.localPolygonPoints[next]).sqrMagnitude <= DuplicateThreshold * DuplicateThreshold)
                {
                    area.localPolygonPoints.RemoveAt(next);
                    removed++;
                }
            }

            if (removed > 0)
                MarkChanged(area);

            return removed == 0 ? "No near-duplicate area points found." : "Removed " + removed + " near-duplicate area point" + (removed == 1 ? "." : "s.");
        }

        public static string RepairSelfIntersections(PungentAreaAuthoringShape area)
        {
            if (!HasEditablePolygon(area))
                return "Repair requires a polygon area.";

            int count = area.localPolygonPoints.Count;
            if (count < 4)
                return "Area has too few points to self-intersect.";

            Undo.RecordObject(area, "Repair Area Self Intersections");
            int repairs = 0;
            int maxIterations = Mathf.Max(4, count * count);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                if (!TryFindIntersection(area.localPolygonPoints, out int firstEdge, out int secondEdge))
                    break;

                ReverseRange(area.localPolygonPoints, firstEdge + 1, secondEdge);
                repairs++;
            }

            bool stillIntersects = TryFindIntersection(area.localPolygonPoints, out _, out _);
            if (repairs > 0)
                MarkChanged(area);

            if (repairs == 0)
                return "No self-intersections found.";
            return stillIntersects
                ? "Applied " + repairs + " repair pass" + (repairs == 1 ? string.Empty : "es") + "; manual cleanup still needed."
                : "Repaired area self-intersections in " + repairs + " pass" + (repairs == 1 ? "." : "es.");
        }

        private static bool HasEditablePolygon(PungentAreaAuthoringShape area)
        {
            return area != null &&
                   area.shapeMode == PungentAreaAuthoringShape.ShapeMode.PolygonXZ &&
                   area.localPolygonPoints != null &&
                   area.localPolygonPoints.Count >= 3;
        }

        private static float SignedAreaXZ(IList<Vector3> points)
        {
            if (points == null || points.Count < 3)
                return 0f;

            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                area += a.x * b.z - b.x * a.z;
            }

            return area * 0.5f;
        }

        private static bool TryFindIntersection(IList<Vector3> points, out int firstEdge, out int secondEdge)
        {
            firstEdge = -1;
            secondEdge = -1;
            int count = points == null ? 0 : points.Count;
            if (count < 4)
                return false;

            for (int a = 0; a < count; a++)
            {
                int aNext = (a + 1) % count;
                for (int b = a + 1; b < count; b++)
                {
                    int bNext = (b + 1) % count;
                    if (EdgesShareEndpoint(a, aNext, b, bNext))
                        continue;

                    if (SegmentsIntersectXZ(points[a], points[aNext], points[b], points[bNext]))
                    {
                        firstEdge = a;
                        secondEdge = b;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool EdgesShareEndpoint(int a, int aNext, int b, int bNext)
        {
            return a == b || a == bNext || aNext == b || aNext == bNext;
        }

        private static bool SegmentsIntersectXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
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
            return t > 0.0001f && t < 0.9999f && u > 0.0001f && u < 0.9999f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void ReverseRange(List<Vector3> points, int start, int end)
        {
            while (start < end)
            {
                Vector3 tmp = points[start];
                points[start] = points[end];
                points[end] = tmp;
                start++;
                end--;
            }
        }

        private static void MarkChanged(PungentAreaAuthoringShape area)
        {
            EditorUtility.SetDirty(area);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }
    }
#endif
}
