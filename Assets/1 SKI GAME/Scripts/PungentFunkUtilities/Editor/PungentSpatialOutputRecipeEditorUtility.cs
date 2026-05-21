using System.Collections.Generic;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal static class PungentSpatialOutputRecipeEditorUtility
    {
        private struct LanePreview
        {
            public readonly List<Vector3> Points;
            public readonly bool Closed;
            public readonly string Label;

            public LanePreview(List<Vector3> points, bool closed, string label)
            {
                Points = points;
                Closed = closed;
                Label = label ?? string.Empty;
            }
        }

        private static readonly List<LanePreview> s_Lanes = new List<LanePreview>();
        private static readonly List<Vector3> s_Points = new List<Vector3>();
        private static readonly List<float> s_Distances = new List<float>();
        private static readonly List<PungentSpatialValidationIssue> s_AreaIssues = new List<PungentSpatialValidationIssue>();
        private static PungentModularSpatialOutput s_SelectedRecipe;

        public static Component ActiveSource
        {
            get
            {
                PungentSpatialAuthoringEditorState.SyncSelectionState();
                return PungentSpatialAuthoringEditorState.ActivePath != null
                    ? (Component)PungentSpatialAuthoringEditorState.ActivePath
                    : PungentSpatialAuthoringEditorState.ActiveArea;
            }
        }

        public static PungentModularSpatialOutput[] GetRecipes(Component source)
        {
            return source != null ? source.GetComponents<PungentModularSpatialOutput>() : System.Array.Empty<PungentModularSpatialOutput>();
        }

        public static PungentModularSpatialOutput GetSelectedRecipe(Component source, bool fallbackToFirst = true)
        {
            PungentModularSpatialOutput[] recipes = GetRecipes(source);
            if (recipes.Length == 0)
                return null;

            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i] != null && recipes[i] == s_SelectedRecipe)
                    return recipes[i];

            if (!fallbackToFirst)
                return null;

            s_SelectedRecipe = recipes[0];
            return recipes[0];
        }

        public static void SelectRecipe(PungentModularSpatialOutput recipe)
        {
            if (s_SelectedRecipe == recipe)
                return;

            s_SelectedRecipe = recipe;
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
        }

        public static PungentModularSpatialOutput CreateRecipe(Component source)
        {
            PungentModularSpatialOutput recipe = PungentSpatialAuthoringActions.CreateGenericOutputRecipe(source);
            if (recipe != null)
            {
                SelectRecipe(recipe);
                PungentSpatialAuthoringEditorState.ReportAction("Created Spatial Output Recipe.");
            }

            return recipe;
        }

        public static void ApplyRecipe(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return;

            Undo.RecordObject(recipe, "Apply Spatial Output Recipe");
            recipe.Rebuild();
            EditorUtility.SetDirty(recipe);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.ReportAction("Applied Spatial Output Recipe: " + recipe.ExistingGeneratedChildCount + " generated object(s).");
        }

        public static void ClearRecipe(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return;

            Undo.RecordObject(recipe, "Clear Spatial Output Recipe");
            recipe.ClearGenerated();
            EditorUtility.SetDirty(recipe);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.ReportAction("Cleared Spatial Output Recipe generated objects.");
        }

        public static string ValidateRecipe(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return "No Spatial Output Recipe selected.";

            Component source = ResolveSource(recipe);
            if (source == null)
                return "Assign a path or area source before validating.";

            if (recipe.sourceMode == PungentModularOutputSourceMode.AreaBoundary && !(source is PungentAreaAuthoringShape))
                return "Area Boundary mode requires an area source.";

            if (recipe.sourceMode != PungentModularOutputSourceMode.AreaBoundary && !(source is ModularPathSpawner))
                return "Path center/corridor modes require a path source.";

            if (source is ModularPathSpawner path)
                return path.PointCount >= 2
                    ? "Recipe lane is valid: " + path.PointCount + " path point(s)."
                    : "Path recipe needs at least two points.";

            if (source is PungentAreaAuthoringShape area)
            {
                s_AreaIssues.Clear();
                PungentSpatialValidationUtility.ValidateArea(area, s_AreaIssues);
                return s_AreaIssues.Count == 0
                    ? "Area boundary recipe is valid: " + area.PolygonPointCount + " boundary point(s)."
                    : "Area boundary has " + s_AreaIssues.Count + " validation issue(s).";
            }

            return "Unsupported source component.";
        }

        public static string BuildSourceSummary(Component source)
        {
            PungentModularSpatialOutput[] recipes = GetRecipes(source);
            int generated = 0;
            int gaps = 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] == null)
                    continue;
                generated += recipes[i].ExistingGeneratedChildCount;
                gaps += recipes[i].manualGaps != null ? recipes[i].manualGaps.Count : 0;
            }

            return recipes.Length + " recipe(s), " + generated + " generated object(s), " + gaps + " manual gap(s).";
        }

        public static string BuildRecipeSummary(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return "No recipe selected.";

            int gaps = recipe.manualGaps != null ? recipe.manualGaps.Count : 0;
            string sourceName = ResolveSource(recipe) != null ? ResolveSource(recipe).name : "No source";
            return sourceName + " / " + recipe.sourceMode + " / " + recipe.ExistingGeneratedChildCount + " generated / " + gaps + " gap(s).";
        }

        public static void FrameGenerated(PungentModularSpatialOutput recipe)
        {
            if (recipe == null || recipe.ExistingGeneratedRoot == null || SceneView.lastActiveSceneView == null)
                return;

            if (TryGetHierarchyBounds(recipe.ExistingGeneratedRoot, out Bounds bounds))
                SceneView.lastActiveSceneView.Frame(bounds, false);
        }

        public static void DrawScenePreviewForActiveSource()
        {
            Component source = ActiveSource;
            if (source == null)
                return;

            PungentModularSpatialOutput[] recipes = GetRecipes(source);
            for (int i = 0; i < recipes.Length; i++)
                DrawScenePreview(recipes[i], recipes[i] == GetSelectedRecipe(source));
        }

        public static void DrawScenePreview(PungentModularSpatialOutput recipe, bool selected)
        {
            if (recipe == null || !recipe.previewWhileEditing)
                return;

            s_Lanes.Clear();
            if (!TryBuildPreviewLanes(recipe, s_Lanes))
                return;

            Color laneColor = selected
                ? new Color(0.32f, 1f, 0.72f, 0.92f)
                : new Color(0.35f, 0.9f, 1f, 0.42f);
            Color gapColor = new Color(1f, 0.68f, 0.24f, selected ? 0.95f : 0.62f);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < s_Lanes.Count; i++)
            {
                LanePreview lane = s_Lanes[i];
                if (lane.Points == null || lane.Points.Count < 2)
                    continue;

                Handles.color = laneColor;
                DrawLanePolyline(lane.Points, lane.Closed);
                DrawManualGaps(recipe, lane, gapColor, selected);
            }

            s_Lanes.Clear();
            s_Distances.Clear();
        }

        private static void DrawLanePolyline(List<Vector3> points, bool closed)
        {
            for (int i = 0; i < points.Count - 1; i++)
                Handles.DrawAAPolyLine(3f, points[i], points[i + 1]);
            if (closed && points.Count > 2)
                Handles.DrawAAPolyLine(3f, points[points.Count - 1], points[0]);
        }

        private static void DrawManualGaps(PungentModularSpatialOutput recipe, LanePreview lane, Color color, bool selected)
        {
            if (recipe.manualGaps == null || recipe.manualGaps.Count == 0)
                return;

            float length = BuildDistanceTable(lane.Points, lane.Closed, s_Distances);
            if (length <= 0.0001f)
                return;

            for (int i = 0; i < recipe.manualGaps.Count; i++)
            {
                PungentModularOutputGap gap = recipe.manualGaps[i];
                if (gap == null || !gap.enabled)
                    continue;

                Vector3 start = SampleAtNormalized(lane.Points, lane.Closed, s_Distances, gap.startNormalized, out _);
                Vector3 end = SampleAtNormalized(lane.Points, lane.Closed, s_Distances, gap.endNormalized, out _);
                Handles.color = color;
                Handles.DrawDottedLine(start, end, 4f);
                float size = Mathf.Max(HandleUtility.GetHandleSize(start), HandleUtility.GetHandleSize(end)) * 0.055f;
                Handles.SphereHandleCap(0, start, Quaternion.identity, size, EventType.Repaint);
                Handles.SphereHandleCap(0, end, Quaternion.identity, size, EventType.Repaint);

                if (!selected)
                    continue;

                EditorGUI.BeginChangeCheck();
                Vector3 newStart = Handles.FreeMoveHandle(start, size * 1.25f, Vector3.zero, Handles.SphereHandleCap);
                Vector3 newEnd = Handles.FreeMoveHandle(end, size * 1.25f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(recipe, "Edit Spatial Output Recipe Gap");
                    gap.startNormalized = NearestNormalizedOnLane(lane.Points, lane.Closed, newStart);
                    gap.endNormalized = NearestNormalizedOnLane(lane.Points, lane.Closed, newEnd);
                    EditorUtility.SetDirty(recipe);
                    PungentSpatialAuthoringEditorState.ReportAction("Edited recipe gap " + (i + 1) + ".");
                }
            }
        }

        private static bool TryBuildPreviewLanes(PungentModularSpatialOutput recipe, List<LanePreview> results)
        {
            results.Clear();
            Component source = ResolveSource(recipe);
            if (source == null)
                return false;

            if (recipe.sourceMode == PungentModularOutputSourceMode.AreaBoundary)
                return TryBuildAreaBoundary(source, results);

            return TryBuildPathLanes(source, recipe.sourceMode, results);
        }

        private static Component ResolveSource(PungentModularSpatialOutput recipe)
        {
            if (recipe == null)
                return null;
            if (recipe.source != null)
                return recipe.source;

            ModularPathSpawner path = recipe.GetComponent<ModularPathSpawner>();
            if (path != null)
                return path;

            return recipe.GetComponent<PungentAreaAuthoringShape>();
        }

        private static bool TryBuildAreaBoundary(Component source, List<LanePreview> results)
        {
            List<Vector3> points = null;
            if (source is PungentAreaAuthoringShape area)
                points = area.GetWorldPolygon();
            else if (source is IPungentAreaShapeProvider provider &&
                     provider.TryGetAreaShape(out PungentAreaShape shape) &&
                     shape.WorldPolygon != null)
                points = new List<Vector3>(shape.WorldPolygon);

            if (points == null || points.Count < 2)
                return false;

            results.Add(new LanePreview(points, true, "Area Boundary"));
            return true;
        }

        private static bool TryBuildPathLanes(Component source, PungentModularOutputSourceMode mode, List<LanePreview> results)
        {
            bool closed = false;
            s_Points.Clear();
            if (source is ModularPathSpawner path)
            {
                if (!path.BuildSampledWorldPath(out List<Vector3> sampled, out closed) || sampled == null || sampled.Count < 2)
                    return false;
                s_Points.AddRange(sampled);
            }
            else if (source is IPungentPathPointProvider pointProvider)
            {
                s_Points.AddRange(PungentPathAuthoringAdapterUtility.CopyWorldPoints(pointProvider));
                if (source is IPungentPathMetadataProvider metadataProvider &&
                    metadataProvider.TryGetPathMetadata(out PungentPathMetadata metadata))
                    closed = metadata.ClosedLoop;
            }

            if (s_Points.Count < 2)
                return false;

            float halfWidth = ResolveCorridorWidth(source) * 0.5f;
            switch (mode)
            {
                case PungentModularOutputSourceMode.PathCorridorLeft:
                    results.Add(new LanePreview(BuildOffsetLane(s_Points, closed, -halfWidth), closed, "Left"));
                    break;
                case PungentModularOutputSourceMode.PathCorridorRight:
                    results.Add(new LanePreview(BuildOffsetLane(s_Points, closed, halfWidth), closed, "Right"));
                    break;
                case PungentModularOutputSourceMode.PathCorridorBothSides:
                    results.Add(new LanePreview(BuildOffsetLane(s_Points, closed, -halfWidth), closed, "Left"));
                    results.Add(new LanePreview(BuildOffsetLane(s_Points, closed, halfWidth), closed, "Right"));
                    break;
                default:
                    results.Add(new LanePreview(new List<Vector3>(s_Points), closed, "Center"));
                    break;
            }

            return results.Count > 0;
        }

        private static float ResolveCorridorWidth(Component source)
        {
            if (source is ModularPathSpawner path)
                return Mathf.Max(0.01f, path.previewCorridorWidth);

            if (source is IPungentCorridorWidthProvider provider && provider.TryGetWidthAt(0.5f, out float width))
                return Mathf.Max(0.01f, width);

            return 1f;
        }

        private static List<Vector3> BuildOffsetLane(IList<Vector3> source, bool closed, float offset)
        {
            List<Vector3> result = new List<Vector3>(source.Count);
            if (source.Count < 2)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                Vector3 previous = i == 0 ? (closed ? source[source.Count - 1] : source[i]) : source[i - 1];
                Vector3 next = i == source.Count - 1 ? (closed ? source[0] : source[i]) : source[i + 1];
                Vector3 tangent = next - previous;
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = i < source.Count - 1 ? source[i + 1] - source[i] : source[i] - source[i - 1];
                Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
                result.Add(source[i] + side * offset);
            }

            return result;
        }

        private static float BuildDistanceTable(List<Vector3> points, bool closed, List<float> distances)
        {
            distances.Clear();
            if (points == null || points.Count == 0)
                return 0f;

            distances.Add(0f);
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                distances.Add(total);
            }
            if (closed && points.Count > 2)
                total += Vector3.Distance(points[points.Count - 1], points[0]);

            return total;
        }

        private static Vector3 SampleAtNormalized(List<Vector3> points, bool closed, List<float> distances, float normalized, out Vector3 tangent)
        {
            tangent = Vector3.forward;
            float length = BuildDistanceTable(points, closed, distances);
            if (length <= 0.0001f || points == null || points.Count == 0)
                return Vector3.zero;

            float target = Mathf.Clamp01(normalized) * length;
            for (int i = 1; i < distances.Count; i++)
            {
                if (target > distances[i])
                    continue;

                float segmentStart = distances[i - 1];
                float segmentLength = Mathf.Max(0.0001f, distances[i] - segmentStart);
                float t = Mathf.Clamp01((target - segmentStart) / segmentLength);
                tangent = (points[i] - points[i - 1]).normalized;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }

            if (closed && points.Count > 2)
            {
                float segmentStart = distances[distances.Count - 1];
                float segmentLength = Mathf.Max(0.0001f, length - segmentStart);
                float t = Mathf.Clamp01((target - segmentStart) / segmentLength);
                tangent = (points[0] - points[points.Count - 1]).normalized;
                return Vector3.Lerp(points[points.Count - 1], points[0], t);
            }

            tangent = points.Count > 1 ? (points[points.Count - 1] - points[points.Count - 2]).normalized : Vector3.forward;
            return points[points.Count - 1];
        }

        private static float NearestNormalizedOnLane(List<Vector3> points, bool closed, Vector3 worldPoint)
        {
            float total = BuildDistanceTable(points, closed, s_Distances);
            if (total <= 0.0001f || points == null || points.Count < 2)
                return 0f;

            float bestDistance = float.MaxValue;
            float bestAlong = 0f;
            int segmentCount = closed ? points.Count : points.Count - 1;
            float along = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 a = points[i];
                Vector3 b = i == points.Count - 1 ? points[0] : points[i + 1];
                Vector3 ab = b - a;
                float segmentLength = ab.magnitude;
                if (segmentLength <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01(Vector3.Dot(worldPoint - a, ab) / (segmentLength * segmentLength));
                Vector3 projected = Vector3.Lerp(a, b, t);
                float sqr = (worldPoint - projected).sqrMagnitude;
                if (sqr < bestDistance)
                {
                    bestDistance = sqr;
                    bestAlong = along + segmentLength * t;
                }

                along += segmentLength;
            }

            return Mathf.Clamp01(bestAlong / total);
        }

        private static bool TryGetHierarchyBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool found = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!found)
                {
                    bounds = colliders[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            if (found)
                return true;

            bounds = new Bounds(root.position, Vector3.one);
            return true;
        }
    }
#endif
}
